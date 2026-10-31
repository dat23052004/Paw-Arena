using System;

namespace PawArena.Core.Match
{
    /// <summary>Immutable result emitted once after an authoritative round resolves.</summary>
    public readonly struct MatchResult
    {
        /// <summary>Creates a resolved match result.</summary>
        public MatchResult(string matchId, string winnerId, MatchWinReason winReason)
        {
            MatchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
            WinnerId = winnerId ?? throw new ArgumentNullException(nameof(winnerId));
            WinReason = winReason;
        }

        /// <summary>Gets the match identifier.</summary>
        public string MatchId { get; }

        /// <summary>Gets the authoritative winner identifier.</summary>
        public string WinnerId { get; }

        /// <summary>Gets how the authoritative match was resolved.</summary>
        public MatchWinReason WinReason { get; }

        /// <summary>Gets whether the server resolved the match without a winner.</summary>
        public bool IsDraw => WinReason == MatchWinReason.Draw;

        /// <summary>Gets whether this result contains an authoritative winner.</summary>
        public bool HasWinner => !IsDraw && !string.IsNullOrEmpty(WinnerId);
    }
}
