using PawArena.Core.Services;
using PawArena.Core.Settings;
using PawArena.UI.Settings;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>Connects the settings presentation to application or preview services.</summary>
    [DisallowMultipleComponent]
    public sealed class SettingsBootstrap : MonoBehaviour
    {
        [SerializeField] private SettingsView _view = null;

        private ISettingsService _settings;

        private void Start()
        {
            _view ??= GetComponent<SettingsView>();
            if (_view == null)
            {
                Debug.LogError($"{nameof(SettingsView)} is required on the UI canvas.", this);
                enabled = false;
                return;
            }

            _settings = ServiceRegistry.TryGet<ISettingsService>(out var registered)
                ? registered
                : new SettingsService();
            _view.Bind(_settings);
        }

        private void OnDestroy()
        {
            _view?.Unbind();
            _settings = null;
        }

        private void OnValidate() => _view ??= GetComponent<SettingsView>();
    }
}
