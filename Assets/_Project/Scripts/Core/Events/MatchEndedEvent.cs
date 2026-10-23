namespace PawArena.Core.Events
{
    /// <summary>
    /// Announces a resolved match result to progression and presentation systems.
    /// </summary>
    public readonly struct MatchEndedEvent
    {
        /// <summary>
        /// Creates a resolved match message.
        /// </summary>
        public MatchEndedEvent(string matchId, string winnerId)
        {
            MatchId = matchId;
            WinnerId = winnerId;
        }

        public string MatchId { get; }
        public string WinnerId { get; }
    }
}
