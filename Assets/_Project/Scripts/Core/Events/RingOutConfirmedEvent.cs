namespace PawArena.Core.Events
{
    /// <summary>
    /// Announces one authoritative player elimination.
    /// </summary>
    public readonly struct RingOutConfirmedEvent
    {
        /// <summary>
        /// Creates a ring-out message.
        /// </summary>
        public RingOutConfirmedEvent(string eliminatedPlayerId, string attackerId, string cause)
        {
            EliminatedPlayerId = eliminatedPlayerId;
            AttackerId = attackerId;
            Cause = cause;
        }

        public string EliminatedPlayerId { get; }
        public string AttackerId { get; }
        public string Cause { get; }
    }
}
