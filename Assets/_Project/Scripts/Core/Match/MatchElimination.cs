using System;

namespace PawArena.Core.Match
{
    /// <summary>Immutable server result for one accepted participant elimination.</summary>
    public readonly struct MatchElimination
    {
        /// <summary>Creates an elimination snapshot.</summary>
        public MatchElimination(string playerId, int eliminationOrder, int activeCount)
            : this(playerId, string.Empty, string.Empty, eliminationOrder, activeCount)
        {
        }

        /// <summary>Creates an elimination snapshot with server-validated attribution.</summary>
        public MatchElimination(
            string playerId,
            string attackerId,
            string cause,
            int eliminationOrder,
            int activeCount)
        {
            PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
            AttackerId = attackerId ?? throw new ArgumentNullException(nameof(attackerId));
            Cause = cause ?? throw new ArgumentNullException(nameof(cause));
            EliminationOrder = eliminationOrder;
            ActiveCount = activeCount;
        }

        /// <summary>Gets the eliminated participant identifier.</summary>
        public string PlayerId { get; }

        /// <summary>Gets the credited attacker identifier, or an empty string for an environmental fall.</summary>
        public string AttackerId { get; }

        /// <summary>Gets the authoritative elimination cause.</summary>
        public string Cause { get; }

        /// <summary>Gets whether another participant received elimination credit.</summary>
        public bool HasAttacker => !string.IsNullOrEmpty(AttackerId);

        /// <summary>Gets the one-based authoritative elimination order.</summary>
        public int EliminationOrder { get; }

        /// <summary>Gets the number of participants still active after this elimination.</summary>
        public int ActiveCount { get; }
    }
}
