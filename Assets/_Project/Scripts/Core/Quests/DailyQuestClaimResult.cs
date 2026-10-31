namespace PawArena.Core.Quests
{
    /// <summary>Describes the result of an interactive daily quest claim.</summary>
    public enum DailyQuestClaimResult
    {
        Success,
        NotCompleted,
        AlreadyClaimed,
        RewardFailed
    }
}
