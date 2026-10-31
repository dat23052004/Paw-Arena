using System;
using PawArena.Core.Events;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Core.Services;
using PawArena.Core.Time;
using PawArena.Gameplay.Inventory;
using PawArena.Infrastructure.Persistence;
using PawArena.Infrastructure.Time;
using PawArena.LiveEvents;
using PawArena.UI.Events;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>Composes the configured seasonal event and connects it to the event screen.</summary>
    [DisallowMultipleComponent]
    public sealed class LiveEventBootstrap : MonoBehaviour
    {
        [SerializeField] private EventScreenView _view = null;
        [SerializeField] private LiveEventDefinition _definition = null;
        [Tooltip("Used only by the standalone UI showcase.")]
        [SerializeField] private ItemCatalog _fallbackCatalog = null;
        [SerializeField] private string _standaloneProfileId = "paw-arena-event-preview";

        private ILiveEventService _service;
        private IProfileService _profile;
        private IItemCatalog _catalog;
        private bool _ownsService;

        private void Start()
        {
            _view ??= GetComponent<EventScreenView>();
            if (_view == null || _definition == null)
            {
                Debug.LogError("EventScreenView and LiveEventDefinition are required.", this);
                enabled = false;
                return;
            }

            try
            {
                if (!ServiceRegistry.TryGet<ILiveEventService>(out _service))
                {
                    CreateService();
                    ServiceRegistry.Register(_service);
                    _ownsService = true;
                }

                _service.Changed += HandleEventChanged;
                if (_catalog == null)
                {
                    ServiceRegistry.TryGet(out _catalog);
                    _catalog ??= _fallbackCatalog;
                }

                _view.Bind(
                    _service,
                    _profile?.Wallet ?? ServiceRegistry.GetRequired<PawArena.Core.Economy.IWalletService>(),
                    _catalog);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Live event could not start: {exception.Message}", this);
                enabled = false;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
            }
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy()
        {
            _view?.Unbind();
            if (_service != null)
            {
                _service.Changed -= HandleEventChanged;
                Save();
            }

            if (_ownsService)
            {
                ServiceRegistry.Unregister(_service);
                _service?.Dispose();
            }

            _service = null;
            _profile = null;
            _catalog = null;
        }

        private void CreateService()
        {
            ResolveDependencies(
                out _profile,
                out _catalog,
                out var clock,
                out var eventBus);
            var validationErrors = LiveEventDefinitionValidator.Validate(_definition, _catalog);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Live Event configuration is invalid: " +
                    string.Join(" | ", validationErrors));
            }

            _service = new LiveEventService(
                _profile.PlayerId,
                _profile.PlayerId,
                _definition,
                clock,
                eventBus,
                _profile.Wallet,
                _profile.Inventory,
                _catalog,
                new JsonLiveEventProgressStore(),
                wheelTickets: _profile.WheelTickets,
                persistProfile: _profile.Save);
        }

        private void ResolveDependencies(
            out IProfileService profile,
            out IItemCatalog catalog,
            out IClock clock,
            out IEventBus eventBus)
        {
            if (ServiceRegistry.TryGet(out profile) &&
                ServiceRegistry.TryGet(out catalog) &&
                ServiceRegistry.TryGet(out clock) &&
                ServiceRegistry.TryGet(out eventBus))
            {
                return;
            }

            if (_fallbackCatalog == null)
            {
                throw new InvalidOperationException(
                    "No application services are registered and no fallback catalog is assigned.");
            }

            catalog = _fallbackCatalog;
            clock = new SystemClock();
            eventBus = new EventBus();
            var profileId = string.IsNullOrWhiteSpace(_standaloneProfileId)
                ? "paw-arena-event-preview"
                : _standaloneProfileId.Trim();
            profile = new ProfileService(profileId, new JsonProfileStore(), catalog, clock);
        }

        private void HandleEventChanged() => _profile?.Save();

        private void Save()
        {
            _service?.Save();
            _profile?.Save();
        }

        private void OnValidate()
        {
            _view ??= GetComponent<EventScreenView>();
            _standaloneProfileId = string.IsNullOrWhiteSpace(_standaloneProfileId)
                ? "paw-arena-event-preview"
                : _standaloneProfileId.Trim();
        }
    }
}
