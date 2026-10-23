using System;

namespace PawArena.Core.Settings
{
    /// <summary>
    /// Serializable player-owned accessibility and presentation settings.
    /// </summary>
    [Serializable]
    public sealed class PlayerSettingsData
    {
        public float MusicVolume = 1f;
        public float SoundsVolume = 1f;
        public float InterfaceVolume = 1f;
        public bool ScreenShakeEnabled = true;
        public GiftsPolicy GiftsPolicy = GiftsPolicy.Everyone;

        /// <summary>
        /// Creates an independent copy safe for callers to modify.
        /// </summary>
        public PlayerSettingsData Clone()
        {
            return new PlayerSettingsData
            {
                MusicVolume = MusicVolume,
                SoundsVolume = SoundsVolume,
                InterfaceVolume = InterfaceVolume,
                ScreenShakeEnabled = ScreenShakeEnabled,
                GiftsPolicy = GiftsPolicy
            };
        }

        /// <summary>
        /// Clamps serialized values to supported ranges.
        /// </summary>
        public void Normalize()
        {
            MusicVolume = Clamp01(MusicVolume);
            SoundsVolume = Clamp01(SoundsVolume);
            InterfaceVolume = Clamp01(InterfaceVolume);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
