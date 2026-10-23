namespace PawArena.Core.Pooling
{
    /// <summary>
    /// Resets reusable runtime objects when they enter or leave a pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called after the object becomes active.
        /// </summary>
        void OnSpawned();

        /// <summary>
        /// Called before the object becomes inactive.
        /// </summary>
        void OnDespawned();
    }
}
