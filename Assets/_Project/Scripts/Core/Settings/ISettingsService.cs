using System;
using PawArena.Core.Audio;

namespace PawArena.Core.Settings
{
    /// <summary>
    /// Owns validated player settings and broadcasts changes.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Raised with an immutable copy after settings change.
        /// </summary>
        event Action<PlayerSettingsData> Changed;

        /// <summary>
        /// Gets a copy of the current settings.
        /// </summary>
        PlayerSettingsData Current { get; }

        /// <summary>
        /// Replaces all settings after normalization.
        /// </summary>
        void Apply(PlayerSettingsData settings);

        /// <summary>
        /// Sets one persisted audio channel volume.
        /// </summary>
        void SetVolume(AudioBus bus, float volume);

        /// <summary>
        /// Enables or disables camera shake presentation.
        /// </summary>
        void SetScreenShakeEnabled(bool isEnabled);

        /// <summary>
        /// Updates the gift receiving policy.
        /// </summary>
        void SetGiftsPolicy(GiftsPolicy policy);

        /// <summary>
        /// Restores default settings.
        /// </summary>
        void ResetToDefaults();
    }
}
