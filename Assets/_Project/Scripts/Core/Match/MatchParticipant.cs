using System;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Identifies one participant selected by the authoritative server.
    /// </summary>
    public readonly struct MatchParticipant
    {
        /// <summary>Value used when the selected mode has no shared teams.</summary>
        public const int NoTeam = -1;

        /// <summary>Creates a selected match participant.</summary>
        public MatchParticipant(string playerId, bool isBot)
            : this(playerId, isBot, NoTeam)
        {
        }

        /// <summary>Creates a selected participant with a server-assigned team index.</summary>
        public MatchParticipant(string playerId, bool isBot, int teamIndex)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id cannot be empty.", nameof(playerId));
            }

            if (teamIndex < NoTeam)
            {
                throw new ArgumentOutOfRangeException(nameof(teamIndex));
            }

            PlayerId = playerId.Trim();
            IsBot = isBot;
            TeamIndex = teamIndex;
        }

        /// <summary>Gets the stable participant identifier.</summary>
        public string PlayerId { get; }

        /// <summary>Gets whether the participant is a stationary server bot.</summary>
        public bool IsBot { get; }

        /// <summary>Gets the zero-based server team index, or <see cref="NoTeam"/> in Solos.</summary>
        public int TeamIndex { get; }

        /// <summary>Gets whether this participant belongs to a shared team.</summary>
        public bool HasTeam => TeamIndex != NoTeam && !string.IsNullOrEmpty(PlayerId);

        /// <summary>Returns whether another participant is a different member of the same team.</summary>
        public bool IsTeammateWith(MatchParticipant other)
        {
            return HasTeam && other.HasTeam && TeamIndex == other.TeamIndex &&
                !string.Equals(PlayerId, other.PlayerId, StringComparison.Ordinal);
        }
    }
}
