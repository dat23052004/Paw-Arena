using System;
using System.Collections.Generic;

namespace PawArena.Core.Economy
{
    /// <summary>Serializable player-scoped paid wheel-ticket balance.</summary>
    [Serializable]
    public sealed class WheelTicketSnapshot
    {
        public int Version = 1;
        public long PaidSpinTickets;
        public List<string> AppliedTransactionIds = new();
        public List<WheelDailyDealState> DailyDeals = new();
    }

    /// <summary>Tracks one Event's Buy 1 daily deal inside the atomic player profile.</summary>
    [Serializable]
    public sealed class WheelDailyDealState
    {
        public string EventId = string.Empty;
        public string ResetKey = string.Empty;
        public bool Used;
    }
}
