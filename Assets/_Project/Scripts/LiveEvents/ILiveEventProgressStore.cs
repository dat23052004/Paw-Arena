namespace PawArena.LiveEvents
{
    /// <summary>Persists live-event progress independently from seasonal content assets.</summary>
    public interface ILiveEventProgressStore
    {
        bool TryLoad(string playerId, out LiveEventProgressSnapshot snapshot);
        void Save(string playerId, LiveEventProgressSnapshot snapshot);
    }
}
