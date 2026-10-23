using System;
using PawArena.Core.Pooling;
using UnityEngine;
using UnityEngine.Pool;

namespace PawArena.Infrastructure.Pooling
{
    /// <summary>
    /// Reuses poolable Unity components for projectiles, VFX and damage numbers.
    /// </summary>
    public sealed class ComponentPool<TComponent> where TComponent : Component, IPoolable
    {
        private readonly ObjectPool<TComponent> _pool;
        private readonly TComponent _prefab;
        private readonly Transform _parent;

        /// <summary>
        /// Creates a component pool with bounded capacity.
        /// </summary>
        public ComponentPool(
            TComponent prefab,
            Transform parent = null,
            int defaultCapacity = 16,
            int maximumSize = 128)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            if (defaultCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultCapacity));
            }

            if (maximumSize < defaultCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSize));
            }

            _pool = new ObjectPool<TComponent>(
                Create,
                OnGet,
                OnRelease,
                OnDestroy,
                true,
                defaultCapacity,
                maximumSize);
        }

        /// <summary>
        /// Gets the number of inactive pooled objects.
        /// </summary>
        public int CountInactive => _pool.CountInactive;

        /// <summary>
        /// Gets and positions a reusable component.
        /// </summary>
        public TComponent Get(Vector3 position, Quaternion rotation)
        {
            var component = _pool.Get();
            component.transform.SetPositionAndRotation(position, rotation);
            return component;
        }

        /// <summary>
        /// Returns a component to the pool.
        /// </summary>
        public void Release(TComponent component) => _pool.Release(component);

        /// <summary>
        /// Destroys every inactive pooled object.
        /// </summary>
        public void Clear() => _pool.Clear();

        private TComponent Create()
        {
            var component = UnityEngine.Object.Instantiate(_prefab, _parent);
            component.gameObject.SetActive(false);
            return component;
        }

        private static void OnGet(TComponent component)
        {
            component.gameObject.SetActive(true);
            component.OnSpawned();
        }

        private void OnRelease(TComponent component)
        {
            component.OnDespawned();
            component.gameObject.SetActive(false);
            if (_parent != null)
            {
                component.transform.SetParent(_parent, false);
            }
        }

        private static void OnDestroy(TComponent component)
        {
            if (component != null)
            {
                UnityEngine.Object.Destroy(component.gameObject);
            }
        }
    }
}
