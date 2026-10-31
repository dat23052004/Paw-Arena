using System;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Tracks active participants and resolves a winner without depending on Unity presentation.
    /// </summary>
    public sealed class MatchEliminationTracker
    {
        private readonly string _matchId;
        private readonly MatchParticipant[] _participants;
        private readonly ParticipantRoundState[] _states;
        private string _lastEliminatedPlayerId;
        private int _eliminationCount;

        /// <summary>Creates a tracker from the server-locked assignment.</summary>
        public MatchEliminationTracker(MatchAssignment assignment)
        {
            if (assignment == null)
            {
                throw new ArgumentNullException(nameof(assignment));
            }

            _matchId = assignment.MatchId;
            _participants = new MatchParticipant[assignment.Participants.Count];
            _states = new ParticipantRoundState[assignment.Participants.Count];
            for (var index = 0; index < assignment.Participants.Count; index++)
            {
                var participant = assignment.Participants[index];
                for (var previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    if (string.Equals(
                            _participants[previousIndex].PlayerId,
                            participant.PlayerId,
                            StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"Duplicate participant id '{participant.PlayerId}'.",
                            nameof(assignment));
                    }
                }

                _participants[index] = participant;
                _states[index] = ParticipantRoundState.Active;
            }

            ActiveCount = _participants.Length;
        }

        /// <summary>Gets how many participants are still on the arena.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Gets whether the winner or draw has already been locked.</summary>
        public bool IsResolved { get; private set; }

        /// <summary>Gets the locked winner identifier, or an empty string before resolution or on draw.</summary>
        public string WinnerId { get; private set; } = string.Empty;

        /// <summary>Gets a participant state when the id belongs to this round.</summary>
        public bool TryGetState(string playerId, out ParticipantRoundState state)
        {
            var index = FindParticipantIndex(playerId);
            if (index < 0)
            {
                state = default;
                return false;
            }

            state = _states[index];
            return true;
        }

        /// <summary>
        /// Records one server-confirmed fall. Call <see cref="TryResolve"/> after the complete
        /// physics batch so simultaneous falls do not depend on callback order.
        /// </summary>
        public bool TryEliminate(string playerId, out MatchElimination elimination)
        {
            return TryEliminate(playerId, string.Empty, string.Empty, out elimination);
        }

        /// <summary>Records one server-confirmed fall with validated attacker attribution.</summary>
        public bool TryEliminate(
            string playerId,
            string attackerId,
            string cause,
            out MatchElimination elimination)
        {
            elimination = default;
            if (IsResolved)
            {
                return false;
            }

            var index = FindParticipantIndex(playerId);
            if (index < 0 || _states[index] == ParticipantRoundState.Eliminated)
            {
                return false;
            }

            _states[index] = ParticipantRoundState.Eliminated;
            ActiveCount--;
            _eliminationCount++;
            _lastEliminatedPlayerId = _participants[index].PlayerId;
            elimination = new MatchElimination(
                _lastEliminatedPlayerId,
                attackerId ?? string.Empty,
                cause ?? string.Empty,
                _eliminationCount,
                ActiveCount);
            return true;
        }

        /// <summary>
        /// Locks the sole active participant, or the last participant to fall when a complete
        /// physics batch leaves nobody on the arena.
        /// </summary>
        public bool TryResolve(out MatchResult result)
        {
            result = default;
            if (IsResolved || ActiveCount > 1)
            {
                return false;
            }

            var reason = MatchWinReason.LastStanding;
            var winnerId = FindActivePlayerId();
            if (string.IsNullOrEmpty(winnerId))
            {
                winnerId = _lastEliminatedPlayerId;
                reason = MatchWinReason.LastToFall;
            }

            if (string.IsNullOrEmpty(winnerId))
            {
                return false;
            }

            IsResolved = true;
            WinnerId = winnerId;
            result = new MatchResult(_matchId, winnerId, reason);
            return true;
        }

        /// <summary>
        /// Resolves the round at its authoritative deadline. A sole survivor still wins;
        /// otherwise every remaining participant receives a draw.
        /// </summary>
        public bool TryResolveAtDeadline(out MatchResult result)
        {
            if (TryResolve(out result))
            {
                return true;
            }

            if (IsResolved)
            {
                result = default;
                return false;
            }

            IsResolved = true;
            WinnerId = string.Empty;
            result = new MatchResult(_matchId, string.Empty, MatchWinReason.Draw);
            return true;
        }

        private int FindParticipantIndex(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return -1;
            }

            for (var index = 0; index < _participants.Length; index++)
            {
                if (string.Equals(_participants[index].PlayerId, playerId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private string FindActivePlayerId()
        {
            for (var index = 0; index < _participants.Length; index++)
            {
                if (_states[index] == ParticipantRoundState.Active)
                {
                    return _participants[index].PlayerId;
                }
            }

            return string.Empty;
        }
    }
}
