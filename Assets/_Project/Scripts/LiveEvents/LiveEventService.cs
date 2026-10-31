using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Events;
using PawArena.Core.Inventory;
using PawArena.Core.Time;

namespace PawArena.LiveEvents
{
    /// <summary>Coordinates all mutable state for one data-driven live event.</summary>
    public sealed class LiveEventService : ILiveEventService
    {
        private readonly string _playerId;
        private readonly string _localPlayerId;
        private readonly IClock _clock;
        private readonly IEventBus _eventBus;
        private readonly IWalletService _wallet;
        private readonly IInventoryService _inventory;
        private readonly IItemCatalog _catalog;
        private readonly ILiveEventProgressStore _store;
        private readonly IWheelTicketService _wheelTickets;
        private readonly Dictionary<string, LiveEventQuestState> _questStates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _claimedQuestIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _claimedRewardIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _purchasedShopOfferIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _purchasedBundleIds = new(StringComparer.Ordinal);

        private LiveEventProgressSnapshot _snapshot;
        private EventSchedulePolicy _schedule;
        private EventShopService _shop;
        private EventBundleService _bundles;
        private EventWheelScheduleService _wheelSchedule;
        private EventPurchaseCoordinator _purchases;
        private EventWheelService _wheel;
        private bool _disposed;

        public LiveEventService(
            string playerId,
            string localPlayerId,
            LiveEventDefinition definition,
            IClock clock,
            IEventBus eventBus,
            IWalletService wallet,
            IInventoryService inventory,
            IItemCatalog catalog,
            ILiveEventProgressStore store,
            Random random = null,
            IWheelTicketService wheelTickets = null,
            Action persistProfile = null)
        {
            _playerId = string.IsNullOrWhiteSpace(playerId)
                ? throw new ArgumentException("Player ID is required.", nameof(playerId))
                : playerId.Trim();
            _localPlayerId = string.IsNullOrWhiteSpace(localPlayerId)
                ? _playerId
                : localPlayerId.Trim();
            Definition = definition != null
                ? definition
                : throw new ArgumentNullException(nameof(definition));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _wheelTickets = wheelTickets ?? new WheelTicketService();
            _ = random;

            _schedule = new EventSchedulePolicy(Definition, _clock);
            RestoreProgress();
            _shop = new EventShopService(Definition, _schedule);
            _bundles = new EventBundleService(Definition, _schedule);
            _wheelSchedule = new EventWheelScheduleService(Definition, _schedule);
            _purchases = new EventPurchaseCoordinator(
                _wallet,
                _inventory,
                _catalog,
                _wheelTickets,
                persistProfile);
            _wheel = new EventWheelService(
                _playerId,
                Definition.EventId,
                _wheelSchedule,
                _snapshot,
                _clock,
                _wheelTickets,
                _purchases);
            EnsureWheelRefill();
            Subscribe();
        }

        public event Action Changed;

        public LiveEventDefinition Definition { get; }
        public LiveEventWheelDefinition ActiveWheel => _wheelSchedule.ActiveWheel;
        public int FreeSpinsRemaining
        {
            get
            {
                EnsureWheelRefill();
                return _wheel.FreeSpinsRemaining;
            }
        }
        public long PaidSpinsRemaining => _wheel.PaidSpinsRemaining;
        public int WheelPity => _wheel.Pity;
        public bool IsActive => _schedule.IsActive;
        public bool IsHardQuestUnlocked => AreAllDailyQuestsCompleted();
        public int CurrentRewardIndex => FindCurrentRewardIndex();
        public TimeSpan EventTimeRemaining => _schedule.EventTimeRemaining;
        public TimeSpan DailyResetTimeRemaining => _schedule.DailyResetTimeRemaining;
        public TimeSpan NextFreeSpinTimeRemaining
        {
            get
            {
                EnsureWheelRefill();
                return _wheel.NextFreeSpinTimeRemaining;
            }
        }
        public IReadOnlyList<LiveEventShopOfferDefinition> ActiveShopOffers => _shop.ActiveOffers;
        public TimeSpan ShopRotationTimeRemaining => _shop.RotationTimeRemaining;

        public LiveEventQuestProgress GetQuestProgress(LiveEventQuestDefinition quest)
        {
            if (quest == null)
            {
                return new LiveEventQuestProgress(0, 1, false);
            }

            EnsureDailyReset();
            var progress = _questStates.TryGetValue(quest.QuestId, out var state)
                ? state.Progress
                : 0;
            return new LiveEventQuestProgress(
                Math.Min(progress, quest.Target),
                quest.Target,
                _claimedQuestIds.Contains(quest.QuestId));
        }

        public bool IsRewardClaimed(string rewardId) =>
            !string.IsNullOrWhiteSpace(rewardId) && _claimedRewardIds.Contains(rewardId);

        public LiveEventRewardState GetRewardState(string rewardId)
        {
            var rewardIndex = FindRewardIndex(rewardId);
            if (rewardIndex < 0)
            {
                return LiveEventRewardState.Locked;
            }

            if (_claimedRewardIds.Contains(rewardId))
            {
                return LiveEventRewardState.Owned;
            }

            return rewardIndex == CurrentRewardIndex && IsRewardTimeUnlocked(Definition.Rewards[rewardIndex])
                ? LiveEventRewardState.Current
                : LiveEventRewardState.Locked;
        }

        public TimeSpan GetRewardUnlockTimeRemaining(string rewardId)
        {
            var reward = FindReward(rewardId);
            if (reward == null || !TryGetRewardUnlockHourOffset(reward, out var unlockHourOffset))
            {
                return TimeSpan.Zero;
            }

            return ClampRemaining(_schedule.StartsAt.AddHours(unlockHourOffset) - _clock.UtcNow);
        }

        public LiveEventClaimAllQuote GetClaimAllQuote(CurrencyType paymentCurrency)
        {
            if (!IsActive || !IsSupportedRewardPaymentCurrency(paymentCurrency))
            {
                return new LiveEventClaimAllQuote(paymentCurrency, 0, 0L);
            }

            var rewards = new List<LiveEventRewardDefinition>();
            var result = TryBuildRewardClaimBatch(paymentCurrency, rewards, out var totalPrice);
            return result == LiveEventOperationResult.Success
                ? new LiveEventClaimAllQuote(paymentCurrency, rewards.Count, totalPrice)
                : new LiveEventClaimAllQuote(paymentCurrency, 0, 0L);
        }

        public LiveEventWheelPackQuote GetWheelPackQuote(int spinCount) =>
            _wheel.GetPackQuote(spinCount, _schedule.DailyResetKey);

        public bool IsShopOfferPurchased(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId))
            {
                return false;
            }

            var offer = _shop?.FindOffer(offerId);
            return _purchasedShopOfferIds.Contains(offerId) ||
                   offer != null && _inventory.IsOwned(offer.ItemId);
        }

        public bool IsBundlePurchased(string bundleId) =>
            !string.IsNullOrWhiteSpace(bundleId) && _purchasedBundleIds.Contains(bundleId);

        public bool IsAvailable(int startHourOffset, int endHourOffset) =>
            _schedule.IsAvailable(startHourOffset, endHourOffset);

        public TimeSpan GetAvailabilityTimeRemaining(int endHourOffset) =>
            _schedule.GetAvailabilityTimeRemaining(endHourOffset);

        public void ReportProgress(LiveEventQuestMetric metric, int amount = 1)
        {
            ThrowIfDisposed();
            EnsureDailyReset();
            if (!IsActive || amount <= 0)
            {
                return;
            }

            var changed = AdvanceMatchingQuests(Definition.DailyQuests, metric, amount);
            if (IsHardQuestUnlocked)
            {
                changed |= AdvanceMatchingQuests(Definition.HardQuests, metric, amount);
            }

            if (!changed)
            {
                return;
            }

            Save();
            Changed?.Invoke();
        }

        public LiveEventOperationResult TryClaimQuest(string questId)
        {
            ThrowIfDisposed();
            EnsureDailyReset();
            if (!IsActive)
            {
                return LiveEventOperationResult.EventInactive;
            }

            if (!TryFindQuest(questId, out var quest, out var group))
            {
                return LiveEventOperationResult.NotFound;
            }

            if (group == LiveEventQuestGroup.Hard && !IsHardQuestUnlocked)
            {
                return LiveEventOperationResult.Locked;
            }

            if (_claimedQuestIds.Contains(questId))
            {
                return LiveEventOperationResult.AlreadyClaimed;
            }

            if (GetQuestProgress(quest).Current < quest.Target)
            {
                return LiveEventOperationResult.NotCompleted;
            }

            if (quest.CandyReward > 0L)
            {
                var walletResult = _wallet.TryApply(
                    BuildTransactionId("quest", questId),
                    CurrencyType.Candy,
                    quest.CandyReward,
                    "live_event_quest",
                    Definition.EventId,
                    out _);
                if (walletResult != WalletTransactionResult.Success &&
                    walletResult != WalletTransactionResult.DuplicateTransaction)
                {
                    return LiveEventOperationResult.InvalidConfiguration;
                }
            }

            _claimedQuestIds.Add(questId);
            Save();
            Changed?.Invoke();
            return LiveEventOperationResult.Success;
        }

        public LiveEventOperationResult TryClaimReward(string rewardId) =>
            TryClaimReward(rewardId, CurrencyType.Candy);

        public LiveEventOperationResult TryClaimReward(
            string rewardId,
            CurrencyType paymentCurrency)
        {
            ThrowIfDisposed();
            EnsureDailyReset();
            if (!IsActive)
            {
                return LiveEventOperationResult.EventInactive;
            }

            var rewardIndex = FindRewardIndex(rewardId);
            if (rewardIndex < 0)
            {
                return LiveEventOperationResult.NotFound;
            }

            var reward = Definition.Rewards[rewardIndex];

            if (_claimedRewardIds.Contains(rewardId))
            {
                return LiveEventOperationResult.AlreadyClaimed;
            }

            if (rewardIndex != CurrentRewardIndex)
            {
                return LiveEventOperationResult.Locked;
            }

            if (!TryGetRewardUnlockHourOffset(reward, out var unlockHourOffset))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            if (_clock.UtcNow < _schedule.StartsAt.AddHours(unlockHourOffset))
            {
                return LiveEventOperationResult.Locked;
            }

            if (!IsSupportedRewardPaymentCurrency(paymentCurrency) ||
                !IsGrantConfigurationValid(reward.Grant))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var price = GetRewardPrice(reward, paymentCurrency);
            if (_wallet.GetBalance(paymentCurrency) < price)
            {
                return LiveEventOperationResult.InsufficientFunds;
            }

            var rewards = new List<LiveEventRewardDefinition>(1) { reward };
            if (!CanApplyRewardBatch(rewards, paymentCurrency, price))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var payment = TryPay(
                paymentCurrency,
                price,
                GetRewardPaymentCategory(paymentCurrency),
                rewardId);
            if (payment != LiveEventOperationResult.Success)
            {
                return payment;
            }

            var grantResult = TryApplyGrant(reward.Grant, "reward-grant", rewardId);
            if (grantResult != LiveEventOperationResult.Success)
            {
                return grantResult;
            }

            _claimedRewardIds.Add(rewardId);
            Save();
            Changed?.Invoke();
            return LiveEventOperationResult.Success;
        }

        public LiveEventOperationResult TryClaimAllRewards(CurrencyType paymentCurrency)
        {
            ThrowIfDisposed();
            EnsureDailyReset();
            if (!IsActive)
            {
                return LiveEventOperationResult.EventInactive;
            }

            if (!IsSupportedRewardPaymentCurrency(paymentCurrency))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var rewards = new List<LiveEventRewardDefinition>();
            var batchResult = TryBuildRewardClaimBatch(paymentCurrency, rewards, out var totalPrice);
            if (batchResult != LiveEventOperationResult.Success)
            {
                return batchResult;
            }

            if (_wallet.GetBalance(paymentCurrency) < totalPrice)
            {
                return LiveEventOperationResult.InsufficientFunds;
            }

            if (!CanApplyRewardBatch(rewards, paymentCurrency, totalPrice))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                var payment = TryPay(
                    paymentCurrency,
                    GetRewardPrice(reward, paymentCurrency),
                    GetRewardPaymentCategory(paymentCurrency),
                    reward.RewardId);
                if (payment != LiveEventOperationResult.Success)
                {
                    return payment;
                }
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                var grantResult = TryApplyGrant(
                    reward.Grant,
                    "reward-grant",
                    reward.RewardId);
                if (grantResult != LiveEventOperationResult.Success)
                {
                    return grantResult;
                }
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                _claimedRewardIds.Add(rewards[i].RewardId);
            }

            Save();
            Changed?.Invoke();
            return LiveEventOperationResult.Success;
        }

        public LiveEventOperationResult TryPurchaseShopOffer(string offerId)
        {
            ThrowIfDisposed();
            var offer = _shop.FindOffer(offerId);
            if (offer == null)
            {
                return LiveEventOperationResult.NotFound;
            }

            if (!_shop.IsOfferActive(offerId))
            {
                return LiveEventOperationResult.EventInactive;
            }

            if (_purchasedShopOfferIds.Contains(offerId))
            {
                return LiveEventOperationResult.AlreadyPurchased;
            }

            var purchase = _purchases.TryPurchaseShopOffer(Definition.EventId, offer);
            if (purchase != LiveEventOperationResult.Success)
            {
                return purchase;
            }

            _purchasedShopOfferIds.Add(offerId);
            Save();
            Changed?.Invoke();
            return LiveEventOperationResult.Success;
        }

        public LiveEventOperationResult TryPurchaseWheelPack(int spinCount)
        {
            ThrowIfDisposed();
            if (!IsActive)
            {
                return LiveEventOperationResult.EventInactive;
            }

            var result = _wheel.TryPurchasePack(spinCount, _schedule.DailyResetKey);
            if (result == LiveEventOperationResult.Success)
            {
                Changed?.Invoke();
            }

            return result;
        }

        public LiveEventSpinResult TrySpin()
        {
            ThrowIfDisposed();
            EnsureDailyReset();
            if (!IsActive)
            {
                return new LiveEventSpinResult(LiveEventOperationResult.EventInactive, null);
            }

            var result = _wheel.TrySpin();
            if (!result.Succeeded)
            {
                return result;
            }

            Save();
            Changed?.Invoke();
            return result;
        }

        public LiveEventSpinResult TrySpin(bool useFreeSpin) => TrySpin();

        public LiveEventOperationResult TryPurchaseBundle(string bundleId)
        {
            ThrowIfDisposed();
            var bundle = _bundles.Find(bundleId);
            if (bundle == null)
            {
                return LiveEventOperationResult.NotFound;
            }

            if (!_bundles.IsActive(bundle))
            {
                return LiveEventOperationResult.EventInactive;
            }

            if (_purchasedBundleIds.Contains(bundleId))
            {
                return LiveEventOperationResult.AlreadyPurchased;
            }

            var purchase = _purchases.TryPurchaseBundle(Definition.EventId, bundle);
            if (purchase != LiveEventOperationResult.Success)
            {
                return purchase;
            }

            _purchasedBundleIds.Add(bundleId);
            Save();
            Changed?.Invoke();
            return LiveEventOperationResult.Success;
        }

        public void Save()
        {
            SynchronizeSnapshotCollections();
            _store.Save(_playerId, _snapshot);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _eventBus.Unsubscribe<MatchEndedEvent>(HandleMatchEnded);
            _eventBus.Unsubscribe<CatKnockedOutEvent>(HandleCatKnockedOut);
            _eventBus.Unsubscribe<AbilityUsedEvent>(HandleAbilityUsed);
            _eventBus.Unsubscribe<RingOutConfirmedEvent>(HandleRingOutConfirmed);
            _disposed = true;
        }

        private void RestoreProgress()
        {
            _store.TryLoad(_playerId, out _snapshot);
            _snapshot = LiveEventProgressMigrator.Migrate(
                _snapshot,
                Definition,
                _clock.UtcNow);

            RestoreSet(_snapshot.ClaimedQuestIds, _claimedQuestIds);
            RestoreSet(_snapshot.ClaimedRewardIds, _claimedRewardIds);
            RestoreSet(_snapshot.PurchasedShopOfferIds, _purchasedShopOfferIds);
            RestoreSet(_snapshot.PurchasedBundleIds, _purchasedBundleIds);
            for (var i = 0; i < _snapshot.Quests.Count; i++)
            {
                var state = _snapshot.Quests[i];
                if (state != null && !string.IsNullOrWhiteSpace(state.QuestId))
                {
                    _questStates[state.QuestId] = state;
                }
            }

            EnsureDailyReset();
        }

        private void Subscribe()
        {
            _eventBus.Subscribe<MatchEndedEvent>(HandleMatchEnded);
            _eventBus.Subscribe<CatKnockedOutEvent>(HandleCatKnockedOut);
            _eventBus.Subscribe<AbilityUsedEvent>(HandleAbilityUsed);
            _eventBus.Subscribe<RingOutConfirmedEvent>(HandleRingOutConfirmed);
        }

        private void HandleMatchEnded(MatchEndedEvent message)
        {
            ReportProgress(LiveEventQuestMetric.PlayMatch);
            if (string.Equals(message.WinnerId, _localPlayerId, StringComparison.Ordinal))
            {
                ReportProgress(LiveEventQuestMetric.WinMatch);
            }
        }

        private void HandleCatKnockedOut(CatKnockedOutEvent message)
        {
            if (string.Equals(message.AttackerId, _localPlayerId, StringComparison.Ordinal))
            {
                ReportProgress(LiveEventQuestMetric.KnockOut);
            }
        }

        private void HandleAbilityUsed(AbilityUsedEvent message)
        {
            if (string.Equals(message.PlayerId, _localPlayerId, StringComparison.Ordinal))
            {
                ReportProgress(LiveEventQuestMetric.UseAbility);
            }
        }

        private void HandleRingOutConfirmed(RingOutConfirmedEvent message)
        {
            if (string.Equals(message.AttackerId, _localPlayerId, StringComparison.Ordinal))
            {
                ReportProgress(LiveEventQuestMetric.ThrowOpponent);
            }
        }

        private bool AdvanceMatchingQuests(
            IReadOnlyList<LiveEventQuestDefinition> quests,
            LiveEventQuestMetric metric,
            int amount)
        {
            var changed = false;
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || quest.Metric != metric || string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    continue;
                }

                if (!_questStates.TryGetValue(quest.QuestId, out var state))
                {
                    state = new LiveEventQuestState { QuestId = quest.QuestId };
                    _questStates.Add(quest.QuestId, state);
                }

                var next = Math.Min(quest.Target, checked(state.Progress + amount));
                if (next == state.Progress)
                {
                    continue;
                }

                state.Progress = next;
                changed = true;
            }

            return changed;
        }

        private void EnsureDailyReset()
        {
            var resetKey = _schedule.DailyResetKey;
            if (string.Equals(_snapshot.DailyResetKey, resetKey, StringComparison.Ordinal))
            {
                return;
            }

            for (var i = 0; i < Definition.DailyQuests.Count; i++)
            {
                var questId = Definition.DailyQuests[i]?.QuestId;
                if (string.IsNullOrWhiteSpace(questId))
                {
                    continue;
                }

                _questStates.Remove(questId);
                _claimedQuestIds.Remove(questId);
            }

            _snapshot.DailyResetKey = resetKey;
            Save();
        }

        private void EnsureWheelRefill()
        {
            if (_wheel != null && _wheel.ReconcileFreeSpins())
            {
                Save();
                Changed?.Invoke();
            }
        }

        private bool AreAllDailyQuestsCompleted()
        {
            if (Definition.DailyQuests.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < Definition.DailyQuests.Count; i++)
            {
                var quest = Definition.DailyQuests[i];
                if (quest == null || GetQuestProgressWithoutReset(quest) < quest.Target)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetQuestProgressWithoutReset(LiveEventQuestDefinition quest) =>
            _questStates.TryGetValue(quest.QuestId, out var state) ? state.Progress : 0;

        private bool TryFindQuest(
            string questId,
            out LiveEventQuestDefinition quest,
            out LiveEventQuestGroup group)
        {
            quest = FindQuest(Definition.DailyQuests, questId);
            if (quest != null)
            {
                group = LiveEventQuestGroup.Daily;
                return true;
            }

            quest = FindQuest(Definition.HardQuests, questId);
            group = LiveEventQuestGroup.Hard;
            return quest != null;
        }

        private static LiveEventQuestDefinition FindQuest(
            IReadOnlyList<LiveEventQuestDefinition> quests,
            string questId)
        {
            for (var i = 0; i < quests.Count; i++)
            {
                if (quests[i] != null && string.Equals(quests[i].QuestId, questId, StringComparison.Ordinal))
                {
                    return quests[i];
                }
            }

            return null;
        }

        private LiveEventRewardDefinition FindReward(string rewardId)
        {
            var rewardIndex = FindRewardIndex(rewardId);
            return rewardIndex >= 0 ? Definition.Rewards[rewardIndex] : null;
        }

        private int FindRewardIndex(string rewardId)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                return -1;
            }

            for (var i = 0; i < Definition.Rewards.Count; i++)
            {
                var reward = Definition.Rewards[i];
                if (reward != null && string.Equals(reward.RewardId, rewardId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindCurrentRewardIndex()
        {
            for (var i = 0; i < Definition.Rewards.Count; i++)
            {
                var reward = Definition.Rewards[i];
                if (reward != null && !_claimedRewardIds.Contains(reward.RewardId))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsRewardTimeUnlocked(LiveEventRewardDefinition reward) =>
            TryGetRewardUnlockHourOffset(reward, out var unlockHourOffset) &&
            IsActive &&
            _clock.UtcNow >= _schedule.StartsAt.AddHours(unlockHourOffset);

        private bool TryGetRewardUnlockHourOffset(
            LiveEventRewardDefinition reward,
            out int unlockHourOffset)
        {
            unlockHourOffset = reward?.UnlockHourOffset ?? 0;
            if (reward == null || string.IsNullOrWhiteSpace(reward.WeekId))
            {
                return reward != null;
            }

            for (var i = 0; i < Definition.RewardWeeks.Count; i++)
            {
                var week = Definition.RewardWeeks[i];
                if (week == null || !string.Equals(week.WeekId, reward.WeekId, StringComparison.Ordinal))
                {
                    continue;
                }

                unlockHourOffset = Math.Max(unlockHourOffset, week.StartHourOffset);
                return true;
            }

            return false;
        }

        private LiveEventOperationResult TryBuildRewardClaimBatch(
            CurrencyType paymentCurrency,
            ICollection<LiveEventRewardDefinition> rewards,
            out long totalPrice)
        {
            totalPrice = 0L;
            if (!IsSupportedRewardPaymentCurrency(paymentCurrency))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var currentRewardIndex = CurrentRewardIndex;
            if (currentRewardIndex < 0)
            {
                return LiveEventOperationResult.AlreadyClaimed;
            }

            var rewardIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = currentRewardIndex; i < Definition.Rewards.Count; i++)
            {
                var reward = Definition.Rewards[i];
                if (reward == null || string.IsNullOrWhiteSpace(reward.RewardId) ||
                    !rewardIds.Add(reward.RewardId) ||
                    !TryGetRewardUnlockHourOffset(reward, out var unlockHourOffset))
                {
                    return LiveEventOperationResult.InvalidConfiguration;
                }

                if (_claimedRewardIds.Contains(reward.RewardId))
                {
                    continue;
                }

                if (_clock.UtcNow < _schedule.StartsAt.AddHours(unlockHourOffset))
                {
                    return rewards.Count == 0
                        ? LiveEventOperationResult.Locked
                        : LiveEventOperationResult.Success;
                }

                if (!IsGrantConfigurationValid(reward.Grant))
                {
                    return LiveEventOperationResult.InvalidConfiguration;
                }

                try
                {
                    totalPrice = checked(totalPrice + GetRewardPrice(reward, paymentCurrency));
                }
                catch (OverflowException)
                {
                    return LiveEventOperationResult.InvalidConfiguration;
                }

                rewards.Add(reward);
            }

            return rewards.Count > 0
                ? LiveEventOperationResult.Success
                : LiveEventOperationResult.AlreadyClaimed;
        }

        private bool CanApplyRewardBatch(
            IReadOnlyList<LiveEventRewardDefinition> rewards,
            CurrencyType paymentCurrency,
            long totalPrice)
        {
            var currencyGrants = new Dictionary<CurrencyType, long>();
            try
            {
                for (var i = 0; i < rewards.Count; i++)
                {
                    var grant = rewards[i].Grant;
                    if (grant.Kind != LiveEventGrantKind.Currency)
                    {
                        continue;
                    }

                    currencyGrants.TryGetValue(grant.Currency, out var currentTotal);
                    currencyGrants[grant.Currency] = checked(currentTotal + grant.Amount);
                }

                foreach (CurrencyType currency in Enum.GetValues(typeof(CurrencyType)))
                {
                    var balance = _wallet.GetBalance(currency);
                    if (currency == paymentCurrency)
                    {
                        balance = checked(balance - totalPrice);
                    }

                    if (currencyGrants.TryGetValue(currency, out var grantTotal))
                    {
                        _ = checked(balance + grantTotal);
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            return true;
        }

        private static bool IsSupportedRewardPaymentCurrency(CurrencyType paymentCurrency) =>
            paymentCurrency == CurrencyType.Candy || paymentCurrency == CurrencyType.Premium;

        private static long GetRewardPrice(
            LiveEventRewardDefinition reward,
            CurrencyType paymentCurrency) =>
            paymentCurrency == CurrencyType.Premium ? reward.PremiumPrice : reward.CandyPrice;

        private static string GetRewardPaymentCategory(CurrencyType paymentCurrency) =>
            paymentCurrency == CurrencyType.Premium
                ? "reward-payment-premium"
                : "reward-payment-candy";

        private LiveEventOperationResult TryPay(
            CurrencyType currency,
            long amount,
            string category,
            string sourceId)
        {
            if (amount <= 0L)
            {
                return LiveEventOperationResult.Success;
            }

            var result = _wallet.TryApply(
                BuildTransactionId(category, sourceId),
                currency,
                -amount,
                "live_event_purchase",
                Definition.EventId,
                out _);
            return result switch
            {
                WalletTransactionResult.Success => LiveEventOperationResult.Success,
                WalletTransactionResult.DuplicateTransaction => LiveEventOperationResult.Success,
                WalletTransactionResult.InsufficientFunds => LiveEventOperationResult.InsufficientFunds,
                _ => LiveEventOperationResult.InvalidConfiguration
            };
        }

        private LiveEventOperationResult TryApplyGrant(
            LiveEventGrantDefinition grant,
            string category,
            string sourceId)
        {
            if (grant == null)
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            if (grant.Kind == LiveEventGrantKind.Currency)
            {
                var walletResult = _wallet.TryApply(
                    BuildTransactionId(category, sourceId),
                    grant.Currency,
                    grant.Amount,
                    "live_event_reward",
                    Definition.EventId,
                    out _);
                return walletResult == WalletTransactionResult.Success ||
                       walletResult == WalletTransactionResult.DuplicateTransaction
                    ? LiveEventOperationResult.Success
                    : LiveEventOperationResult.InvalidConfiguration;
            }

            if (string.IsNullOrWhiteSpace(grant.ItemId) || !_catalog.TryGet(grant.ItemId, out _))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var inventoryResult = _inventory.TryGrant(grant.ItemId);
            return inventoryResult == InventoryOperationResult.Success ||
                   inventoryResult == InventoryOperationResult.AlreadyOwned
                ? LiveEventOperationResult.Success
                : LiveEventOperationResult.InvalidConfiguration;
        }

        private bool IsGrantConfigurationValid(LiveEventGrantDefinition grant)
        {
            if (grant == null)
            {
                return false;
            }

            return grant.Kind == LiveEventGrantKind.Currency ||
                   !string.IsNullOrWhiteSpace(grant.ItemId) && _catalog.TryGet(grant.ItemId, out _);
        }

        private string BuildTransactionId(string category, string sourceId) =>
            $"live-event:{Definition.EventId}:{category}:{sourceId}";

        private void SynchronizeSnapshotCollections()
        {
            _snapshot.Quests.Clear();
            foreach (var state in _questStates.Values)
            {
                _snapshot.Quests.Add(new LiveEventQuestState
                {
                    QuestId = state.QuestId,
                    Progress = state.Progress
                });
            }

            _snapshot.Quests.Sort((left, right) => string.CompareOrdinal(left.QuestId, right.QuestId));
            SynchronizeSet(_claimedQuestIds, _snapshot.ClaimedQuestIds);
            SynchronizeSet(_claimedRewardIds, _snapshot.ClaimedRewardIds);
            SynchronizeSet(_purchasedShopOfferIds, _snapshot.PurchasedShopOfferIds);
            SynchronizeSet(_purchasedBundleIds, _snapshot.PurchasedBundleIds);
        }

        private static void RestoreSet(IEnumerable<string> source, ISet<string> destination)
        {
            foreach (var value in source)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    destination.Add(value);
                }
            }
        }

        private static void SynchronizeSet(ISet<string> source, List<string> destination)
        {
            destination.Clear();
            destination.AddRange(source);
            destination.Sort(StringComparer.Ordinal);
        }

        private static TimeSpan ClampRemaining(TimeSpan remaining) =>
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LiveEventService));
            }
        }
    }
}
