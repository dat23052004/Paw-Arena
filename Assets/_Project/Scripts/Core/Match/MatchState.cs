namespace PawArena.Core.Match
{
    /// <summary>
    /// Represents the authoritative round lifecycle described by the GDD.
    /// </summary>
    public enum MatchState
    {
        Waiting,
        Countdown,
        Spawning,
        Playing,
        Resolving,
        Results,
        Resetting
    }
}
