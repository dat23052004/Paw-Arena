using System;
using System.Collections.Generic;
using PawArena.Core.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace PawArena.Infrastructure.Audio
{
    /// <summary>
    /// Plays catalogued cues through prewarmed voices and configurable mixer buses.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        private const float MinimumDecibels = -80f;
        private const float MinimumLinearVolume = 0.0001f;

        [Header("Catalog")]
        [SerializeField] private AudioCatalog _catalog = null;

        [Header("Mixer")]
        [SerializeField] private AudioMixer _mixer = null;
        [SerializeField] private AudioMixerGroup _musicGroup = null;
        [SerializeField] private AudioMixerGroup _soundsGroup = null;
        [SerializeField] private AudioMixerGroup _interfaceGroup = null;
        [SerializeField] private string _masterVolumeParameter = "MasterVolume";
        [SerializeField] private string _musicVolumeParameter = "MusicVolume";
        [SerializeField] private string _soundsVolumeParameter = "SoundsVolume";
        [SerializeField] private string _interfaceVolumeParameter = "InterfaceVolume";

        [Header("Sources")]
        [SerializeField] private AudioSource _musicSource = null;
        [SerializeField, Min(1)] private int _initialVoiceCount = 8;
        [SerializeField, Min(1)] private int _maximumVoiceCount = 24;
        [SerializeField, Min(0.01f)] private float _musicFadeSeconds = 0.35f;

        private readonly List<AudioSource> _voices = new();
        private readonly float[] _volumes = { 1f, 1f, 1f, 1f };
        private int _nextVoiceIndex;
        private float _musicTargetVolume;
        private bool _stopMusicAfterFade;

        /// <inheritdoc />
        public event Action<AudioBus, float> VolumeChanged;

        /// <inheritdoc />
        public event Action<bool> MuteChanged;

        /// <inheritdoc />
        public bool IsMuted { get; private set; }

        private void Awake()
        {
            if (_musicSource == null)
            {
                _musicSource = GetComponent<AudioSource>();
            }

            ConfigureSource(_musicSource, _musicGroup);
            _musicSource.loop = true;

            if (_catalog == null)
            {
                Debug.LogError($"{nameof(_catalog)} is not assigned.", this);
                enabled = false;
                return;
            }

            _maximumVoiceCount = Mathf.Max(_initialVoiceCount, _maximumVoiceCount);
            for (var i = 0; i < _initialVoiceCount; i++)
            {
                _voices.Add(CreateVoice());
            }

            ApplyAllMixerVolumes();
        }

        private void Update()
        {
            if (_musicSource == null ||
                Mathf.Approximately(_musicSource.volume, _musicTargetVolume))
            {
                return;
            }

            var step = UnityEngine.Time.unscaledDeltaTime / _musicFadeSeconds;
            _musicSource.volume = Mathf.MoveTowards(
                _musicSource.volume,
                _musicTargetVolume,
                step);

            if (_stopMusicAfterFade && Mathf.Approximately(_musicSource.volume, 0f))
            {
                _musicSource.Stop();
                _musicSource.clip = null;
                _stopMusicAfterFade = false;
            }
        }

        /// <inheritdoc />
        public float GetVolume(AudioBus bus) => _volumes[(int)bus];

        /// <inheritdoc />
        public void SetVolume(AudioBus bus, float volume)
        {
            var normalizedVolume = Mathf.Clamp01(volume);
            var index = (int)bus;
            if (Mathf.Approximately(_volumes[index], normalizedVolume))
            {
                return;
            }

            _volumes[index] = normalizedVolume;
            ApplyMixerVolume(bus);
            VolumeChanged?.Invoke(bus, normalizedVolume);
        }

        /// <inheritdoc />
        public void SetMuted(bool isMuted)
        {
            if (IsMuted == isMuted)
            {
                return;
            }

            IsMuted = isMuted;
            ApplyMixerVolume(AudioBus.Master);
            MuteChanged?.Invoke(isMuted);
        }

        /// <inheritdoc />
        public bool PlayMusic(string cueId, bool restart = false)
        {
            if (!_catalog.TryGet(cueId, out var cue) ||
                cue.Clip == null ||
                cue.Bus != AudioBus.Music)
            {
                return false;
            }

            if (!restart && _musicSource.isPlaying && _musicSource.clip == cue.Clip)
            {
                _musicTargetVolume = cue.Volume;
                _stopMusicAfterFade = false;
                return true;
            }

            _musicSource.Stop();
            _musicSource.outputAudioMixerGroup = ResolveMixerGroup(AudioBus.Music);
            _musicSource.clip = cue.Clip;
            _musicSource.loop = cue.Loop || cue.Bus == AudioBus.Music;
            _musicSource.pitch = 1f;
            _musicSource.volume = 0f;
            _musicTargetVolume = cue.Volume;
            _stopMusicAfterFade = false;
            _musicSource.Play();
            return true;
        }

        /// <inheritdoc />
        public void StopMusic()
        {
            if (!_musicSource.isPlaying)
            {
                return;
            }

            _musicTargetVolume = 0f;
            _stopMusicAfterFade = true;
        }

        /// <inheritdoc />
        public bool PlayOneShot(string cueId, float volumeScale = 1f)
        {
            if (!_catalog.TryGet(cueId, out var cue) || cue.Clip == null)
            {
                return false;
            }

            if (cue.Bus == AudioBus.Music)
            {
                return PlayMusic(cueId);
            }

            var voice = AcquireVoice();
            voice.outputAudioMixerGroup = ResolveMixerGroup(cue.Bus);
            voice.clip = cue.Clip;
            voice.loop = false;
            voice.pitch = cue.GetRandomPitch();
            voice.volume = cue.Volume * Mathf.Max(0f, volumeScale);
            voice.Play();
            return true;
        }

        private AudioSource AcquireVoice()
        {
            for (var i = 0; i < _voices.Count; i++)
            {
                if (!_voices[i].isPlaying)
                {
                    return _voices[i];
                }
            }

            if (_voices.Count < _maximumVoiceCount)
            {
                var voice = CreateVoice();
                _voices.Add(voice);
                return voice;
            }

            var reusedVoice = _voices[_nextVoiceIndex];
            _nextVoiceIndex = (_nextVoiceIndex + 1) % _voices.Count;
            reusedVoice.Stop();
            return reusedVoice;
        }

        private AudioSource CreateVoice()
        {
            var voiceObject = new GameObject($"Audio Voice {_voices.Count + 1}");
            voiceObject.transform.SetParent(transform, false);
            var voice = voiceObject.AddComponent<AudioSource>();
            ConfigureSource(voice, _soundsGroup);
            return voice;
        }

        private static void ConfigureSource(AudioSource source, AudioMixerGroup group)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
        }

        private AudioMixerGroup ResolveMixerGroup(AudioBus bus)
        {
            return bus switch
            {
                AudioBus.Music => _musicGroup,
                AudioBus.Interface => _interfaceGroup,
                _ => _soundsGroup
            };
        }

        private void ApplyAllMixerVolumes()
        {
            ApplyMixerVolume(AudioBus.Master);
            ApplyMixerVolume(AudioBus.Music);
            ApplyMixerVolume(AudioBus.Sounds);
            ApplyMixerVolume(AudioBus.Interface);
        }

        private void ApplyMixerVolume(AudioBus bus)
        {
            if (_mixer == null)
            {
                return;
            }

            var parameterName = GetMixerParameterName(bus);
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            var linearVolume = bus == AudioBus.Master && IsMuted ? 0f : _volumes[(int)bus];
            var decibels = linearVolume <= MinimumLinearVolume
                ? MinimumDecibels
                : Mathf.Log10(linearVolume) * 20f;
            _mixer.SetFloat(parameterName, decibels);
        }

        private string GetMixerParameterName(AudioBus bus)
        {
            return bus switch
            {
                AudioBus.Master => _masterVolumeParameter,
                AudioBus.Music => _musicVolumeParameter,
                AudioBus.Sounds => _soundsVolumeParameter,
                AudioBus.Interface => _interfaceVolumeParameter,
                _ => throw new ArgumentOutOfRangeException(nameof(bus), bus, null)
            };
        }

        private void OnValidate()
        {
            _maximumVoiceCount = Mathf.Max(_initialVoiceCount, _maximumVoiceCount);
            _musicFadeSeconds = Mathf.Max(0.01f, _musicFadeSeconds);
        }
    }
}
