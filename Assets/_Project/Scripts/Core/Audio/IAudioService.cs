using System;

namespace PawArena.Core.Audio
{
    /// <summary>
    /// Plays catalogued audio and owns runtime mixer settings.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Raised after an audio bus volume changes.
        /// </summary>
        event Action<AudioBus, float> VolumeChanged;

        /// <summary>
        /// Raised after the global mute state changes.
        /// </summary>
        event Action<bool> MuteChanged;

        /// <summary>
        /// Gets whether all audio output is muted.
        /// </summary>
        bool IsMuted { get; }

        /// <summary>
        /// Gets the normalized volume for a bus.
        /// </summary>
        float GetVolume(AudioBus bus);

        /// <summary>
        /// Sets the normalized volume for a bus.
        /// </summary>
        void SetVolume(AudioBus bus, float volume);

        /// <summary>
        /// Enables or disables all audio output.
        /// </summary>
        void SetMuted(bool isMuted);

        /// <summary>
        /// Starts a music cue and returns false when the cue is unavailable.
        /// </summary>
        bool PlayMusic(string cueId, bool restart = false);

        /// <summary>
        /// Stops the current music cue using the configured fade duration.
        /// </summary>
        void StopMusic();

        /// <summary>
        /// Plays a non-music cue and returns false when it is unavailable.
        /// </summary>
        bool PlayOneShot(string cueId, float volumeScale = 1f);
    }
}
