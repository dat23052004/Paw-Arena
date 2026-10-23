using System;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.Core.Settings;

namespace PawArena.Core.Profiles
{
    /// <summary>
    /// Owns the active local profile and its domain services.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// Raised after profile statistics change.
        /// </summary>
        event Action<PlayerStatsData> StatsChanged;

        /// <summary>
        /// Raised after the profile is persisted.
        /// </summary>
        event Action Saved;

        /// <summary>
        /// Gets the active player identifier.
        /// </summary>
        string PlayerId { get; }

        /// <summary>
        /// Gets the wallet service for the profile.
        /// </summary>
        IWalletService Wallet { get; }

        /// <summary>
        /// Gets the inventory service for the profile.
        /// </summary>
        IInventoryService Inventory { get; }

        /// <summary>
        /// Gets the item purchase coordinator.
        /// </summary>
        IInventoryPurchaseService Purchases { get; }

        /// <summary>
        /// Gets the settings service for the profile.
        /// </summary>
        ISettingsService Settings { get; }

        /// <summary>
        /// Gets a copy of the current profile statistics.
        /// </summary>
        PlayerStatsData Stats { get; }

        /// <summary>
        /// Records an authoritative match result.
        /// </summary>
        void RecordMatchResult(bool won, int knockouts);

        /// <summary>
        /// Persists the current profile state.
        /// </summary>
        void Save();

        /// <summary>
        /// Captures the current versioned snapshot.
        /// </summary>
        PlayerProfileSnapshot CaptureSnapshot();
    }
}
