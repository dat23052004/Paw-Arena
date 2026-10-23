using System;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.Core.Settings;
using PawArena.Core.Time;

namespace PawArena.Core.Profiles
{
    /// <summary>
    /// Composes wallet, inventory, settings and stats for one offline profile.
    /// </summary>
    public sealed class ProfileService : IProfileService
    {
        private const int CurrentProfileVersion = 1;

        private readonly IProfileStore _store;
        private readonly IItemCatalog _catalog;
        private readonly IClock _clock;
        private readonly long _createdAtUnixTimestamp;
        private PlayerStatsData _stats;

        /// <summary>
        /// Loads an existing profile or creates a new local profile.
        /// </summary>
        public ProfileService(
            string playerId,
            IProfileStore store,
            IItemCatalog catalog,
            IClock clock)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player ID is required.", nameof(playerId));
            }

            PlayerId = playerId;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            var hasSnapshot = _store.TryLoad(playerId, out var snapshot) && snapshot != null;
            if (!hasSnapshot)
            {
                snapshot = CreateDefaultSnapshot(playerId);
            }

            _createdAtUnixTimestamp = snapshot.CreatedAtUnixTimestamp > 0L
                ? snapshot.CreatedAtUnixTimestamp
                : _clock.UtcNow.ToUnixTimeSeconds();

            _stats = snapshot.Stats?.Clone() ?? new PlayerStatsData();
            _stats.Normalize();
            Wallet = new WalletService(_clock, snapshot.Wallet);
            Inventory = new InventoryService(_catalog, snapshot.Inventory);
            Purchases = new InventoryPurchaseService(_catalog, Inventory, Wallet);
            Settings = new SettingsService(snapshot.Settings);
            GrantDefaultItems();
        }

        /// <inheritdoc />
        public event Action<PlayerStatsData> StatsChanged;

        /// <inheritdoc />
        public event Action Saved;

        /// <inheritdoc />
        public string PlayerId { get; }

        /// <inheritdoc />
        public IWalletService Wallet { get; }

        /// <inheritdoc />
        public IInventoryService Inventory { get; }

        /// <inheritdoc />
        public IInventoryPurchaseService Purchases { get; }

        /// <inheritdoc />
        public ISettingsService Settings { get; }

        /// <inheritdoc />
        public PlayerStatsData Stats => _stats.Clone();

        /// <inheritdoc />
        public void RecordMatchResult(bool won, int knockouts)
        {
            if (knockouts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(knockouts));
            }

            _stats.Knockouts += knockouts;
            if (won)
            {
                _stats.Wins++;
                _stats.CurrentWinStreak++;
                _stats.BestWinStreak = Math.Max(
                    _stats.BestWinStreak,
                    _stats.CurrentWinStreak);
            }
            else
            {
                _stats.CurrentWinStreak = 0;
            }

            StatsChanged?.Invoke(Stats);
        }

        /// <inheritdoc />
        public void Save()
        {
            _store.Save(CaptureSnapshot());
            Saved?.Invoke();
        }

        /// <inheritdoc />
        public PlayerProfileSnapshot CaptureSnapshot()
        {
            return new PlayerProfileSnapshot
            {
                Version = CurrentProfileVersion,
                PlayerId = PlayerId,
                CreatedAtUnixTimestamp = _createdAtUnixTimestamp,
                Stats = Stats,
                Wallet = Wallet.CaptureSnapshot(),
                Inventory = Inventory.CaptureSnapshot(),
                Settings = Settings.Current
            };
        }

        private PlayerProfileSnapshot CreateDefaultSnapshot(string playerId)
        {
            return new PlayerProfileSnapshot
            {
                Version = CurrentProfileVersion,
                PlayerId = playerId,
                CreatedAtUnixTimestamp = _clock.UtcNow.ToUnixTimeSeconds()
            };
        }

        private void GrantDefaultItems()
        {
            var items = _catalog.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.UnlockType == UnlockType.Default && !Inventory.IsOwned(item.ItemId))
                {
                    Inventory.TryGrant(item.ItemId);
                }
            }
        }
    }
}
