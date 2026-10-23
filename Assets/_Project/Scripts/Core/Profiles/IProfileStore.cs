namespace PawArena.Core.Profiles
{
    /// <summary>
    /// Persists versioned player profile snapshots.
    /// </summary>
    public interface IProfileStore
    {
        /// <summary>
        /// Attempts to load a profile by stable player identifier.
        /// </summary>
        bool TryLoad(string playerId, out PlayerProfileSnapshot snapshot);

        /// <summary>
        /// Persists a complete profile snapshot.
        /// </summary>
        void Save(PlayerProfileSnapshot snapshot);

        /// <summary>
        /// Deletes the local profile when it exists.
        /// </summary>
        bool Delete(string playerId);
    }
}
