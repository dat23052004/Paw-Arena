using PawArena.Core.Profiles;

namespace PawArena.Tests.EditMode.Fakes
{
    /// <summary>
    /// Keeps one profile snapshot in memory for profile service tests.
    /// </summary>
    public sealed class MemoryProfileStore : IProfileStore
    {
        private PlayerProfileSnapshot _snapshot;

        /// <inheritdoc />
        public bool TryLoad(string playerId, out PlayerProfileSnapshot snapshot)
        {
            snapshot = _snapshot;
            return snapshot != null && snapshot.PlayerId == playerId;
        }

        /// <inheritdoc />
        public void Save(PlayerProfileSnapshot snapshot) => _snapshot = snapshot;

        /// <inheritdoc />
        public bool Delete(string playerId)
        {
            if (_snapshot == null || _snapshot.PlayerId != playerId)
            {
                return false;
            }

            _snapshot = null;
            return true;
        }
    }
}
