namespace PawArena.Core.Quests
{
    /// <summary>Immutable presentation snapshot for one daily quest.</summary>
    public readonly struct DailyQuestProgress
    {
        /// <summary>Creates one quest progress snapshot.</summary>
        public DailyQuestProgress(
            DailyQuestId questId,
            int progress,
            int target,
            int coinReward,
            bool isClaimed)
        {
            QuestId = questId;
            Progress = progress;
            Target = target;
            CoinReward = coinReward;
            IsClaimed = isClaimed;
        }

        /// <summary>Gets the stable quest identifier.</summary>
        public DailyQuestId QuestId { get; }

        /// <summary>Gets the current capped progress.</summary>
        public int Progress { get; }

        /// <summary>Gets the completion target.</summary>
        public int Target { get; }

        /// <summary>Gets the coin reward paid on a successful claim.</summary>
        public int CoinReward { get; }

        /// <summary>Gets whether the reward was already claimed today.</summary>
        public bool IsClaimed { get; }

        /// <summary>Gets whether the progress target has been reached.</summary>
        public bool IsCompleted => Progress >= Target;
    }
}
