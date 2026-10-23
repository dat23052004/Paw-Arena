using System;
using PawArena.Core.Audio;

namespace PawArena.Core.Settings
{
    /// <summary>
    /// Validates and stores local player settings.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private PlayerSettingsData _current;

        /// <summary>
        /// Creates a settings service from persisted or default values.
        /// </summary>
        public SettingsService(PlayerSettingsData initialSettings = null)
        {
            _current = initialSettings?.Clone() ?? new PlayerSettingsData();
            _current.Normalize();
        }

        /// <inheritdoc />
        public event Action<PlayerSettingsData> Changed;

        /// <inheritdoc />
        public PlayerSettingsData Current => _current.Clone();

        /// <inheritdoc />
        public void Apply(PlayerSettingsData settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var next = settings.Clone();
            next.Normalize();
            if (AreEqual(_current, next))
            {
                return;
            }

            _current = next;
            Changed?.Invoke(Current);
        }

        /// <inheritdoc />
        public void SetVolume(AudioBus bus, float volume)
        {
            var next = Current;
            switch (bus)
            {
                case AudioBus.Music:
                    next.MusicVolume = volume;
                    break;
                case AudioBus.Sounds:
                    next.SoundsVolume = volume;
                    break;
                case AudioBus.Interface:
                    next.InterfaceVolume = volume;
                    break;
                case AudioBus.Master:
                    throw new ArgumentOutOfRangeException(
                        nameof(bus),
                        bus,
                        "Master volume is runtime-only and is not part of player settings.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }

            Apply(next);
        }

        /// <inheritdoc />
        public void SetScreenShakeEnabled(bool isEnabled)
        {
            var next = Current;
            next.ScreenShakeEnabled = isEnabled;
            Apply(next);
        }

        /// <inheritdoc />
        public void SetGiftsPolicy(GiftsPolicy policy)
        {
            var next = Current;
            next.GiftsPolicy = policy;
            Apply(next);
        }

        /// <inheritdoc />
        public void ResetToDefaults() => Apply(new PlayerSettingsData());

        private static bool AreEqual(PlayerSettingsData left, PlayerSettingsData right)
        {
            return Math.Abs(left.MusicVolume - right.MusicVolume) < 0.0001f &&
                   Math.Abs(left.SoundsVolume - right.SoundsVolume) < 0.0001f &&
                   Math.Abs(left.InterfaceVolume - right.InterfaceVolume) < 0.0001f &&
                   left.ScreenShakeEnabled == right.ScreenShakeEnabled &&
                   left.GiftsPolicy == right.GiftsPolicy;
        }
    }
}
