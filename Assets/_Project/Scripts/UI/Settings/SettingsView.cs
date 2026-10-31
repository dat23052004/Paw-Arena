using System;
using PawArena.Core.Audio;
using PawArena.Core.Settings;
using PawArena.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Settings
{
    /// <summary>Connects the authored settings controls to persisted player settings.</summary>
    [DisallowMultipleComponent]
    public sealed class SettingsView : MonoBehaviour
    {
        private const int AudioBusCount = 3;

        [Header("Screen")]
        [SerializeField] private UIScreen _screen = null;

        [Header("Volume")]
        [SerializeField] private Slider[] _volumeSliders = new Slider[AudioBusCount];
        [SerializeField] private TMP_Text[] _volumePercentLabels = new TMP_Text[AudioBusCount];

        [Header("Preferences")]
        [SerializeField] private Button _screenShakeButton = null;
        [SerializeField] private TMP_Text _screenShakeLabel = null;

        private ISettingsService _settings;
        private bool _isBound;
        private bool _isRefreshing;

        /// <summary>Binds the view to the settings source owned by the application profile.</summary>
        public void Bind(ISettingsService settings)
        {
            Unbind();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ResolveFallbackReferences();
            AddListeners();
            _settings.Changed += HandleSettingsChanged;
            if (_screen != null)
            {
                _screen.Shown += Refresh;
            }

            _isBound = true;
            Refresh();
        }

        /// <summary>Removes runtime listeners while leaving the shared settings service alive.</summary>
        public void Unbind()
        {
            if (!_isBound)
            {
                return;
            }

            RemoveListeners();
            if (_settings != null)
            {
                _settings.Changed -= HandleSettingsChanged;
            }

            if (_screen != null)
            {
                _screen.Shown -= Refresh;
            }

            _settings = null;
            _isBound = false;
        }

        private void OnDestroy() => Unbind();

        private void AddListeners()
        {
            VolumeSlider(0)?.onValueChanged.AddListener(HandleMusicVolumeChanged);
            VolumeSlider(1)?.onValueChanged.AddListener(HandleSoundsVolumeChanged);
            VolumeSlider(2)?.onValueChanged.AddListener(HandleInterfaceVolumeChanged);
            _screenShakeButton?.onClick.AddListener(HandleScreenShakeRequested);
        }

        private void RemoveListeners()
        {
            VolumeSlider(0)?.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
            VolumeSlider(1)?.onValueChanged.RemoveListener(HandleSoundsVolumeChanged);
            VolumeSlider(2)?.onValueChanged.RemoveListener(HandleInterfaceVolumeChanged);
            _screenShakeButton?.onClick.RemoveListener(HandleScreenShakeRequested);
        }

        private void HandleMusicVolumeChanged(float value) => SetVolume(AudioBus.Music, value);
        private void HandleSoundsVolumeChanged(float value) => SetVolume(AudioBus.Sounds, value);
        private void HandleInterfaceVolumeChanged(float value) => SetVolume(AudioBus.Interface, value);

        private void SetVolume(AudioBus bus, float value)
        {
            if (!_isRefreshing)
            {
                _settings?.SetVolume(bus, value);
            }
        }

        private void HandleScreenShakeRequested()
        {
            if (_settings != null && !_isRefreshing)
            {
                _settings.SetScreenShakeEnabled(!_settings.Current.ScreenShakeEnabled);
            }
        }

        private void HandleSettingsChanged(PlayerSettingsData settings) => Refresh(settings);

        private void Refresh()
        {
            if (_settings != null)
            {
                Refresh(_settings.Current);
            }
        }

        private void Refresh(PlayerSettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            _isRefreshing = true;
            SetVolumePresentation(0, settings.MusicVolume);
            SetVolumePresentation(1, settings.SoundsVolume);
            SetVolumePresentation(2, settings.InterfaceVolume);
            if (_screenShakeLabel != null)
            {
                _screenShakeLabel.text = settings.ScreenShakeEnabled ? "On" : "Off";
            }

            _isRefreshing = false;
        }

        private void SetVolumePresentation(int index, float value)
        {
            var normalized = Mathf.Clamp01(value);
            VolumeSlider(index)?.SetValueWithoutNotify(normalized);
            var label = VolumeLabel(index);
            if (label != null)
            {
                label.text = Mathf.RoundToInt(normalized * 100f) + "%";
            }
        }

        private Slider VolumeSlider(int index) =>
            _volumeSliders != null && index >= 0 && index < _volumeSliders.Length
                ? _volumeSliders[index]
                : null;

        private TMP_Text VolumeLabel(int index) =>
            _volumePercentLabels != null && index >= 0 && index < _volumePercentLabels.Length
                ? _volumePercentLabels[index]
                : null;

        private void ResolveFallbackReferences()
        {
            _screen ??= FindComponent<UIScreen>("Window_Settings");
            EnsureArraySize(ref _volumeSliders);
            EnsureArraySize(ref _volumePercentLabels);
            for (var i = 0; i < AudioBusCount; i++)
            {
                _volumeSliders[i] ??= FindComponent<Slider>("Slider_" + i);
                _volumePercentLabels[i] ??= FindComponent<TMP_Text>("SliderPercent_" + i);
            }

            _screenShakeButton ??= FindComponent<Button>("Btn_ShakeToggle");
            _screenShakeLabel ??= FindComponent<TMP_Text>("ShakeToggleLabel");
        }

        private T FindComponent<T>(string objectName) where T : Component
        {
            var descendants = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name == objectName)
                {
                    return descendants[i].GetComponent<T>();
                }
            }

            return null;
        }

        private static void EnsureArraySize<T>(ref T[] array) where T : UnityEngine.Object
        {
            if (array != null && array.Length == AudioBusCount)
            {
                return;
            }

            var resized = new T[AudioBusCount];
            if (array != null)
            {
                Array.Copy(array, resized, Mathf.Min(array.Length, resized.Length));
            }

            array = resized;
        }
    }
}
