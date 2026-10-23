using System;
using PawArena.Core.Audio;
using PawArena.Core.Economy;
using PawArena.Core.Events;
using PawArena.Core.Flow;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Core.Scenes;
using PawArena.Core.Services;
using PawArena.Core.Settings;
using PawArena.Core.Time;
using PawArena.Gameplay.Flow;
using PawArena.Gameplay.Inventory;
using PawArena.Infrastructure.Audio;
using PawArena.Infrastructure.Persistence;
using PawArena.Infrastructure.Scenes;
using PawArena.Infrastructure.Time;
using PawArena.UI.Navigation;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>
    /// Creates and registers application services in a deterministic composition root.
    /// </summary>
    public sealed class PawArenaBootstrapper : MonoBehaviour
    {
        [Header("Runtime Services")]
        [SerializeField] private AudioService _audioService = null;
        [SerializeField] private UnitySceneService _sceneService = null;
        [SerializeField] private GameFlowController _gameFlow = null;
        [SerializeField] private UIStackController _uiService = null;

        [Header("Data")]
        [SerializeField] private ItemCatalog _itemCatalog = null;
        [SerializeField] private string _localPlayerId = "local-player";

        [Header("Lifetime")]
        [SerializeField] private bool _persistAcrossScenes = true;

        private static PawArenaBootstrapper _instance;

        private IProfileService _profileService;
        private IEventBus _eventBus;
        private bool _isInitialized;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (_persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (!ValidateDependencies())
            {
                enabled = false;
                return;
            }

            ComposeServices();
        }

        private void Start()
        {
            if (_isInitialized && _gameFlow.CurrentState == GameFlowState.Boot)
            {
                _gameFlow.TryChangeState(GameFlowState.Lobby);
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                TrySaveProfile();
            }
        }

        private void OnApplicationQuit() => TrySaveProfile();

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            if (_profileService != null)
            {
                _profileService.Settings.Changed -= HandleSettingsChanged;
            }

            _eventBus?.Clear();
            ServiceRegistry.Reset();
            _instance = null;
            _isInitialized = false;
        }

        private bool ValidateDependencies()
        {
            var isValid = true;
            isValid &= ValidateReference(_audioService, nameof(_audioService));
            isValid &= ValidateReference(_sceneService, nameof(_sceneService));
            isValid &= ValidateReference(_gameFlow, nameof(_gameFlow));
            isValid &= ValidateReference(_itemCatalog, nameof(_itemCatalog));
            return isValid;
        }

        private bool ValidateReference(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"{fieldName} is not assigned.", this);
            return false;
        }

        private void ComposeServices()
        {
            ServiceRegistry.Reset();

            IClock clock = new SystemClock();
            IProfileStore profileStore = new JsonProfileStore();
            _eventBus = new EventBus();
            _profileService = new ProfileService(
                _localPlayerId,
                profileStore,
                _itemCatalog,
                clock);

            ServiceRegistry.Register<IClock>(clock);
            ServiceRegistry.Register<IEventBus>(_eventBus);
            ServiceRegistry.Register<IProfileStore>(profileStore);
            ServiceRegistry.Register<IItemCatalog>(_itemCatalog);
            ServiceRegistry.Register<IProfileService>(_profileService);
            ServiceRegistry.Register<IWalletService>(_profileService.Wallet);
            ServiceRegistry.Register<IInventoryService>(_profileService.Inventory);
            ServiceRegistry.Register<IInventoryPurchaseService>(_profileService.Purchases);
            ServiceRegistry.Register<ISettingsService>(_profileService.Settings);
            ServiceRegistry.Register<IAudioService>(_audioService);
            ServiceRegistry.Register<ISceneService>(_sceneService);
            ServiceRegistry.Register<IGameFlowService>(_gameFlow);

            if (_uiService != null)
            {
                ServiceRegistry.Register<IUIService>(_uiService);
            }

            _profileService.Settings.Changed += HandleSettingsChanged;
            ApplySettings(_profileService.Settings.Current);
            _isInitialized = true;
        }

        private void HandleSettingsChanged(PlayerSettingsData settings)
        {
            ApplySettings(settings);
            TrySaveProfile();
        }

        private void ApplySettings(PlayerSettingsData settings)
        {
            _audioService.SetVolume(AudioBus.Music, settings.MusicVolume);
            _audioService.SetVolume(AudioBus.Sounds, settings.SoundsVolume);
            _audioService.SetVolume(AudioBus.Interface, settings.InterfaceVolume);
        }

        private void TrySaveProfile()
        {
            if (!_isInitialized || _profileService == null)
            {
                return;
            }

            try
            {
                _profileService.Save();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to save the local profile: {exception.Message}", this);
            }
        }

        private void OnValidate()
        {
            if (_audioService == null)
            {
                _audioService = GetComponentInChildren<AudioService>(true);
            }

            if (_sceneService == null)
            {
                _sceneService = GetComponentInChildren<UnitySceneService>(true);
            }

            if (_gameFlow == null)
            {
                _gameFlow = GetComponentInChildren<GameFlowController>(true);
            }

            if (_uiService == null)
            {
                _uiService = GetComponentInChildren<UIStackController>(true);
            }

            _localPlayerId = string.IsNullOrWhiteSpace(_localPlayerId)
                ? "local-player"
                : _localPlayerId.Trim();
        }
    }
}
