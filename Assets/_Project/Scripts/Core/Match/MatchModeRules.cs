using System;

namespace PawArena.Core.Match
{
    /// <summary>Provides shared validation rules for server-owned match modes.</summary>
    public static class MatchModeRules
    {
        /// <summary>Gets the number of players required by one team in the given mode.</summary>
        public static int GetTeamSize(MatchMode mode)
        {
            return mode switch
            {
                MatchMode.Solos => 1,
                MatchMode.Duos => 2,
                MatchMode.Squads => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }

        /// <summary>Returns whether a participant snapshot can form complete teams.</summary>
        public static bool SupportsParticipantCount(MatchMode mode, int participantCount)
        {
            return participantCount > 0 && participantCount % GetTeamSize(mode) == 0;
        }
    }
}
