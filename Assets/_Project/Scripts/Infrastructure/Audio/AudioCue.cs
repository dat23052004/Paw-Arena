using PawArena.Core.Audio;
using UnityEngine;

namespace PawArena.Infrastructure.Audio
{
    /// <summary>
    /// Defines one catalogued music, sound or interface cue.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCue", menuName = "Paw Arena/Audio/Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _cueId = string.Empty;
        [SerializeField] private AudioBus _bus = AudioBus.Sounds;

        [Header("Playback")]
        [SerializeField] private AudioClip _clip = null;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float _minimumPitch = 1f;
        [SerializeField, Range(0.1f, 3f)] private float _maximumPitch = 1f;
        [SerializeField] private bool _loop = false;

        /// <summary>
        /// Gets the stable cue identifier used by gameplay and UI.
        /// </summary>
        public string CueId => _cueId;

        /// <summary>
        /// Gets the output bus.
        /// </summary>
        public AudioBus Bus => _bus;

        /// <summary>
        /// Gets the audio clip.
        /// </summary>
        public AudioClip Clip => _clip;

        /// <summary>
        /// Gets the cue-level normalized volume.
        /// </summary>
        public float Volume => _volume;

        /// <summary>
        /// Gets whether playback should loop.
        /// </summary>
        public bool Loop => _loop;

        /// <summary>
        /// Selects a pitch within the configured range.
        /// </summary>
        public float GetRandomPitch() => Random.Range(_minimumPitch, _maximumPitch);

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_cueId))
            {
                _cueId = name.Trim().ToLowerInvariant().Replace(' ', '_');
            }

            if (_minimumPitch > _maximumPitch)
            {
                (_minimumPitch, _maximumPitch) = (_maximumPitch, _minimumPitch);
            }
        }
    }
}
