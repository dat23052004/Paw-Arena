using System;
using PawArena.Core.Eggs;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.Core.Settings;

namespace PawArena.Core.Profiles
{
    /// <summary>Normalizes older local profile snapshots into the current schema.</summary>
    public static class PlayerProfileMigrator
    {
        /// <summary>The latest profile schema version understood by the runtime.</summary>
        public const int CurrentVersion = 3;

        /// <summary>Migrates and normalizes one loaded snapshot in memory.</summary>
        public static PlayerProfileSnapshot Migrate(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            snapshot.Stats ??= new PlayerStatsData();
            snapshot.Wallet ??= new WalletSnapshot();
            snapshot.Inventory ??= new InventorySnapshot();
            snapshot.Inventory.OwnedItemIds ??= new System.Collections.Generic.List<string>();
            snapshot.Inventory.Equipment ??= new System.Collections.Generic.List<EquipmentEntryData>();
            snapshot.Inventory.Version = 2;
            snapshot.EggProgress ??= new EggProgressSnapshot();
            snapshot.EggProgress.Eggs ??= new System.Collections.Generic.List<EggProgressEntryData>();
            snapshot.WheelTickets ??= new WheelTicketSnapshot();
            snapshot.WheelTickets.AppliedTransactionIds ??= new System.Collections.Generic.List<string>();
            snapshot.WheelTickets.DailyDeals ??= new System.Collections.Generic.List<WheelDailyDealState>();
            snapshot.Settings ??= new PlayerSettingsData();
            snapshot.Version = CurrentVersion;
            return snapshot;
        }
    }
}
