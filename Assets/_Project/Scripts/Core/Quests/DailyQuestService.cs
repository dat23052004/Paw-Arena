using System;
using PawArena.Core.Economy;
using PawArena.Core.Time;

namespace PawArena.Core.Quests
{
    /// <summary>Tracks, resets, and rewards the three lobby daily quests.</summary>
    public sealed class DailyQuestService
    {
        private static readonly int[] Targets = { 5, 1, 3 };
        private static readonly int[] CoinRewards = { 50, 75, 150 };

        private readonly IClock _clock;
        private readonly IWalletService _wallet;
        private readonly string _playerId;
        private readonly int[] _progress = new int[3];
        private readonly bool[] _claimed = new bool[3];
        private DateTime _activeUtcDate;

        /// <summary>Creates a daily quest service backed by the existing wallet ledger.</summary>
        public DailyQuestService(IClock clock, IWalletService wallet, string playerId)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id cannot be empty.", nameof(playerId));
            }

            _playerId = playerId.Trim();
            _activeUtcDate = _clock.UtcNow.UtcDateTime.Date;
        }

        /// <summary>Raised whenever progress, claim state, or the active day changes.</summary>
        public event Action Changed;

        /// <summary>Gets the next UTC reset timestamp.</summary>
        public DateTimeOffset ResetAtUtc
        {
            get
            {
                RefreshDailyReset();
                return new DateTimeOffset(_activeUtcDate.AddDays(1), TimeSpan.Zero);
            }
        }

        /// <summary>Gets one immutable quest snapshot.</summary>
        public DailyQuestProgress GetProgress(DailyQuestId questId)
        {
            RefreshDailyReset();
            var index = GetIndex(questId);
            return new DailyQuestProgress(
                questId,
                _progress[index],
                Targets[index],
                CoinRewards[index],
                _claimed[index]);
        }

        /// <summary>Records the server-confirmed result of one completed match.</summary>
        public void ReportMatch(bool won, bool playedWithFriend)
        {
            RefreshDailyReset();
            Increment(DailyQuestId.PlayMatches);
            if (won)
            {
                Increment(DailyQuestId.WinMatches);
            }

            if (playedWithFriend)
            {
                Increment(DailyQuestId.PlayWithFriend);
            }

            Changed?.Invoke();
        }

        /// <summary>Attempts to claim one completed quest through the idempotent wallet ledger.</summary>
        public DailyQuestClaimResult TryClaim(DailyQuestId questId)
        {
            RefreshDailyReset();
            var index = GetIndex(questId);
            if (_claimed[index])
            {
                return DailyQuestClaimResult.AlreadyClaimed;
            }

            if (_progress[index] < Targets[index])
            {
                return DailyQuestClaimResult.NotCompleted;
            }

            var resetKey = _activeUtcDate.ToString("yyyyMMdd");
            var transactionId = $"daily-quest:{_playerId}:{resetKey}:{questId}";
            var walletResult = _wallet.TryApply(
                transactionId,
                CurrencyType.Coins,
                CoinRewards[index],
                "Daily quest reward",
                questId.ToString(),
                out _);
            if (walletResult != WalletTransactionResult.Success &&
                walletResult != WalletTransactionResult.DuplicateTransaction)
            {
                return DailyQuestClaimResult.RewardFailed;
            }

            _claimed[index] = true;
            Changed?.Invoke();
            return DailyQuestClaimResult.Success;
        }

        /// <summary>Clears daily state when the UTC date changes.</summary>
        public bool RefreshDailyReset()
        {
            var currentDate = _clock.UtcNow.UtcDateTime.Date;
            if (currentDate == _activeUtcDate)
            {
                return false;
            }

            _activeUtcDate = currentDate;
            Array.Clear(_progress, 0, _progress.Length);
            Array.Clear(_claimed, 0, _claimed.Length);
            Changed?.Invoke();
            return true;
        }

        private void Increment(DailyQuestId questId)
        {
            var index = GetIndex(questId);
            _progress[index] = Math.Min(Targets[index], _progress[index] + 1);
        }

        private static int GetIndex(DailyQuestId questId)
        {
            var index = (int)questId;
            if (index < 0 || index >= Targets.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(questId));
            }

            return index;
        }
    }
}
