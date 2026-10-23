namespace PawArena.Core.Flow
{
    /// <summary>
    /// Represents the top-level states in the Paw Arena experience loop.
    /// </summary>
    public enum GameFlowState
    {
        Boot,
        Lobby,
        PreMatch,
        Playing,
        Spectating,
        Results
    }
}
