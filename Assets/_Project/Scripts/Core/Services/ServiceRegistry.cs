using System;
using System.Collections.Generic;

namespace PawArena.Core.Services
{
    /// <summary>
    /// Stores application services registered by the composition root.
    /// </summary>
    public static class ServiceRegistry
    {
        private static readonly Dictionary<Type, object> Services = new();

        /// <summary>
        /// Gets the number of currently registered services.
        /// </summary>
        public static int Count => Services.Count;

        /// <summary>
        /// Registers a service against its contract type.
        /// </summary>
        public static void Register<TService>(TService service, bool replace = false)
            where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var serviceType = typeof(TService);
            if (!replace && Services.ContainsKey(serviceType))
            {
                throw new InvalidOperationException(
                    $"A service for {serviceType.FullName} is already registered.");
            }

            Services[serviceType] = service;
        }

        /// <summary>
        /// Attempts to resolve a service without throwing when it is absent.
        /// </summary>
        public static bool TryGet<TService>(out TService service)
            where TService : class
        {
            if (Services.TryGetValue(typeof(TService), out var registeredService))
            {
                service = (TService)registeredService;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Resolves a required service or throws a descriptive exception.
        /// </summary>
        public static TService GetRequired<TService>()
            where TService : class
        {
            if (TryGet<TService>(out var service))
            {
                return service;
            }

            throw new InvalidOperationException(
                $"No service for {typeof(TService).FullName} has been registered.");
        }

        /// <summary>
        /// Removes a service when it is currently registered.
        /// </summary>
        public static bool Unregister<TService>(TService expectedService = null)
            where TService : class
        {
            var serviceType = typeof(TService);
            if (!Services.TryGetValue(serviceType, out var registeredService))
            {
                return false;
            }

            if (expectedService != null && !ReferenceEquals(expectedService, registeredService))
            {
                return false;
            }

            return Services.Remove(serviceType);
        }

        /// <summary>
        /// Clears all services during application teardown or domain reset.
        /// </summary>
        public static void Reset() => Services.Clear();
    }
}
