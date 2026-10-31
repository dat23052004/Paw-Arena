using System;
using PawArena.Core.Economy;
using PawArena.Core.Eggs;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Core.Services;
using PawArena.Core.Time;
using PawArena.Gameplay.Eggs;
using PawArena.Gameplay.Inventory;
using PawArena.Infrastructure.Persistence;
using PawArena.Infrastructure.Time;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>
    /// Provides the shared profile and egg services when the UI canvas runs without the application root.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class StandaloneEggShopBootstrap : MonoBehaviour
    {
        [Header("Fallback data")]
        [SerializeField] private ItemCatalog _itemCatalog = null;
        [SerializeField] private EggCatalog _eggCatalog = null;

        [Header("Standalone profile")]
        [SerializeField] private string _profileId = "paw-arena-ui-preview";
        [SerializeField, Min(0)] private long _startingCoins = 10000L;

        private IClock _clock;
        private IProfileStore _profileStore;
        private IItemCatalog _runtimeItemCatalog;
        private IProfileService _profile;
        private IEggCatalog _runtimeEggCatalog;
        private IEggProgressService _eggProgress;
        private IEggHatchService _eggHatchService;
        private bool _ownsProfile;
        private bool _registeredClock;
        private bool _registeredProfileStore;
        private bool _registeredItemCatalog;
        private bool _registeredProfile;
        private bool _registeredWallet;
        private bool _registeredInventory;
        private bool _registeredPurchases;
        private bool _registeredEggCatalog;
        private bool _registeredEggProgress;
        private bool _registeredEggHatchService;

        private void Awake()
        {
            if (HasRequiredEggServices())
            {
                return;
            }

            try
            {
                ComposeFallbackServices();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Standalone Egg Shop could not start: {exception.Message}", this);
                enabled = false;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                TrySaveOwnedProfile();
            }
        }

        private void OnApplicationQuit() => TrySaveOwnedProfile();

        private void OnDestroy()
        {
            TrySaveOwnedProfile();
            if (_registeredEggHatchService)
            {
                ServiceRegistry.Unregister(_eggHatchService);
            }

            if (_registeredEggProgress)
            {
                ServiceRegistry.Unregister(_eggProgress);
            }

            if (_registeredEggCatalog)
            {
                ServiceRegistry.Unregister(_runtimeEggCatalog);
            }

            if (_registeredPurchases)
            {
                ServiceRegistry.Unregister(_profile?.Purchases);
            }

            if (_registeredInventory)
            {
                ServiceRegistry.Unregister(_profile?.Inventory);
            }

            if (_registeredWallet)
            {
                ServiceRegistry.Unregister(_profile?.Wallet);
            }

            if (_registeredProfile)
            {
                ServiceRegistry.Unregister(_profile);
            }

            if (_registeredItemCatalog)
            {
                ServiceRegistry.Unregister(_runtimeItemCatalog);
            }

            if (_registeredProfileStore)
            {
                ServiceRegistry.Unregister(_profileStore);
            }

            if (_registeredClock)
            {
                ServiceRegistry.Unregister(_clock);
            }
        }

        private bool HasRequiredEggServices()
        {
            return ServiceRegistry.TryGet<IEggCatalog>(out _)
                && ServiceRegistry.TryGet<IEggProgressService>(out _)
                && ServiceRegistry.TryGet<IEggHatchService>(out _)
                && ServiceRegistry.TryGet<IItemCatalog>(out _);
        }

        private void ComposeFallbackServices()
        {
            ResolveClock();
            ResolveItemCatalog();
            ResolveProfile();
            ResolveEggCatalog();
            ResolveEggProgress();
            ResolveEggHatchService();
        }

        private void ResolveClock()
        {
            if (ServiceRegistry.TryGet(out _clock))
            {
                return;
            }

            _clock = new SystemClock();
            ServiceRegistry.Register(_clock);
            _registeredClock = true;
        }

        private void ResolveItemCatalog()
        {
            if (ServiceRegistry.TryGet(out _runtimeItemCatalog))
            {
                return;
            }

            if (_itemCatalog == null)
            {
                throw new InvalidOperationException($"{nameof(_itemCatalog)} is not assigned.");
            }

            _runtimeItemCatalog = _itemCatalog;
            ServiceRegistry.Register(_runtimeItemCatalog);
            _registeredItemCatalog = true;
        }

        private void ResolveProfile()
        {
            if (ServiceRegistry.TryGet(out _profile))
            {
                RegisterProfileServices();
                return;
            }

            if (!ServiceRegistry.TryGet(out _profileStore))
            {
                _profileStore = new JsonProfileStore();
                ServiceRegistry.Register(_profileStore);
                _registeredProfileStore = true;
            }

            var profileId = string.IsNullOrWhiteSpace(_profileId)
                ? "paw-arena-ui-preview"
                : _profileId.Trim();
            _profile = new ProfileService(profileId, _profileStore, _runtimeItemCatalog, _clock);
            ServiceRegistry.Register(_profile);
            _registeredProfile = true;
            _ownsProfile = true;
            RegisterProfileServices();
            GrantStartingCoins(profileId);
        }

        private void RegisterProfileServices()
        {
            if (!ServiceRegistry.TryGet<IWalletService>(out _))
            {
                ServiceRegistry.Register(_profile.Wallet);
                _registeredWallet = true;
            }

            if (!ServiceRegistry.TryGet<IInventoryService>(out _))
            {
                ServiceRegistry.Register(_profile.Inventory);
                _registeredInventory = true;
            }

            if (!ServiceRegistry.TryGet<IInventoryPurchaseService>(out _))
            {
                ServiceRegistry.Register(_profile.Purchases);
                _registeredPurchases = true;
            }
        }

        private void ResolveEggCatalog()
        {
            if (ServiceRegistry.TryGet(out _runtimeEggCatalog))
            {
                return;
            }

            _runtimeEggCatalog = _eggCatalog != null
                ? _eggCatalog
                : SampleEggCatalog.Create();
            ServiceRegistry.Register(_runtimeEggCatalog);
            _registeredEggCatalog = true;
        }

        private void ResolveEggProgress()
        {
            if (ServiceRegistry.TryGet(out _eggProgress))
            {
                return;
            }

            _eggProgress = _profile.EggProgress;
            ServiceRegistry.Register(_eggProgress);
            _registeredEggProgress = true;
        }

        private void ResolveEggHatchService()
        {
            if (ServiceRegistry.TryGet(out _eggHatchService))
            {
                return;
            }

            _eggHatchService = new EggHatchService(
                _runtimeEggCatalog,
                _runtimeItemCatalog,
                _profile.Inventory,
                _profile.Wallet,
                _eggProgress,
                _profile,
                _clock,
                new SystemRandomSource(),
                EggRefundPolicy.CreateDefault());
            ServiceRegistry.Register(_eggHatchService);
            _registeredEggHatchService = true;
        }

        private void GrantStartingCoins(string profileId)
        {
            if (_startingCoins <= 0L)
            {
                return;
            }

            _profile.Wallet.TryApply(
                $"starter-coins-v1:{profileId}",
                CurrencyType.Coins,
                _startingCoins,
                "starter_balance",
                string.Empty,
                out _);
            _profile.Save();
        }

        private void TrySaveOwnedProfile()
        {
            if (!_ownsProfile || _profile == null)
            {
                return;
            }

            try
            {
                _profile.Save();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Standalone profile save failed: {exception.Message}", this);
            }
        }

        private void OnValidate()
        {
            _profileId = string.IsNullOrWhiteSpace(_profileId)
                ? "paw-arena-ui-preview"
                : _profileId.Trim();
            _startingCoins = Math.Max(0L, _startingCoins);
        }
    }
}
