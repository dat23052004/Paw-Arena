namespace PawArena.Core.Events
{
    /// <summary>
    /// Announces one validated active ability use.
    /// </summary>
    public readonly struct AbilityUsedEvent
    {
        /// <summary>
        /// Creates an ability-used message.
        /// </summary>
        public AbilityUsedEvent(string playerId, string abilityId)
        {
            PlayerId = playerId;
            AbilityId = abilityId;
        }

        public string PlayerId { get; }
        public string AbilityId { get; }
    }
}
