namespace PawArena.Core.Events
{
    /// <summary>
    /// Announces a confirmed temporary knockout.
    /// </summary>
    public readonly struct CatKnockedOutEvent
    {
        /// <summary>
        /// Creates a knockout message.
        /// </summary>
        public CatKnockedOutEvent(string attackerId, string targetId)
        {
            AttackerId = attackerId;
            TargetId = targetId;
        }

        public string AttackerId { get; }
        public string TargetId { get; }
    }
}
