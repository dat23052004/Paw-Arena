using System;
using System.Collections.Generic;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Immutable result produced by the authoritative server for one match.
    /// </summary>
    public sealed class MatchAssignment
    {
        private readonly MatchParticipant[] _participants;

        /// <summary>Creates an authoritative match assignment.</summary>
        public MatchAssignment(
            string matchId,
            string mapId,
            IReadOnlyList<MatchParticipant> participants,
            DateTimeOffset startsAtUtc,
            MatchMode mode = MatchMode.Solos)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match id cannot be empty.", nameof(matchId));
            }

            if (string.IsNullOrWhiteSpace(mapId))
            {
                throw new ArgumentException("Map id cannot be empty.", nameof(mapId));
            }

            if (participants == null || participants.Count == 0)
            {
                throw new ArgumentException("A match requires at least one participant.", nameof(participants));
            }

            if (!Enum.IsDefined(typeof(MatchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (!MatchModeRules.SupportsParticipantCount(mode, participants.Count))
            {
                throw new ArgumentException(
                    "The participant count cannot form complete teams for the selected mode.",
                    nameof(participants));
            }

            MatchId = matchId.Trim();
            MapId = mapId.Trim();
            StartsAtUtc = startsAtUtc;
            Mode = mode;
            _participants = new MatchParticipant[participants.Count];
            var teamSize = MatchModeRules.GetTeamSize(mode);
            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                var teamIndex = mode == MatchMode.Solos
                    ? MatchParticipant.NoTeam
                    : index / teamSize;
                _participants[index] = new MatchParticipant(
                    participant.PlayerId,
                    participant.IsBot,
                    teamIndex);
            }
        }

        /// <summary>Gets the server-generated match identifier.</summary>
        public string MatchId { get; }

        /// <summary>Gets the server-selected map identifier.</summary>
        public string MapId { get; }

        /// <summary>Gets the exact server-selected participant snapshot.</summary>
        public IReadOnlyList<MatchParticipant> Participants => _participants;

        /// <summary>Gets the server timestamp at which spawning may begin.</summary>
        public DateTimeOffset StartsAtUtc { get; }

        /// <summary>Gets the mode selected by the authoritative server.</summary>
        public MatchMode Mode { get; }
    }
}
