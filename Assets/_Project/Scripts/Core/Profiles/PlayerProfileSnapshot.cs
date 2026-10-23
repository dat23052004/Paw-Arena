using System;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.Core.Settings;

namespace PawArena.Core.Profiles
{
    /// <summary>
    /// Versioned serializable aggregate for local offline profile persistence.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfileSnapshot
    {
        public int Version = 1;
        public string PlayerId;
        public long CreatedAtUnixTimestamp;
        public PlayerStatsData Stats = new();
        public WalletSnapshot Wallet = new();
        public InventorySnapshot Inventory = new();
        public PlayerSettingsData Settings = new();
    }
}
