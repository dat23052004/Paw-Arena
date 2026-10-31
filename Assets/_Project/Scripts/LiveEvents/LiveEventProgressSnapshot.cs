using System;
using System.Collections.Generic;

namespace PawArena.LiveEvents
{
    /// <summary>Serializable local progress for one currently active event.</summary>
    [Serializable]
    public sealed class LiveEventProgressSnapshot
    {
        public int Version = 3;
        public string EventId = string.Empty;
        public string DailyResetKey = string.Empty;
        public int FreeSpinsRemaining;
        public long NextFreeSpinAtUnixTimestamp;
        public int WheelPity;
        public int TotalSpins;
        public List<LiveEventWheelProgressState> Wheels = new();
        public List<LiveEventQuestState> Quests = new();
        public List<string> ClaimedQuestIds = new();
        public List<string> ClaimedRewardIds = new();
        public List<string> PurchasedShopOfferIds = new();
        public List<string> PurchasedBundleIds = new();
    }

    [Serializable]
    public sealed class LiveEventQuestState
    {
        public string QuestId = string.Empty;
        public int Progress;
    }

    /// <summary>Per-Wheel pity and deterministic spin sequence state.</summary>
    [Serializable]
    public sealed class LiveEventWheelProgressState
    {
        public string WheelId = string.Empty;
        public int Pity;
        public int TotalSpins;
    }
}
