using System;
using System.Collections.Generic;

namespace PawArena.Core.Match
{
    /// <summary>Immutable server snapshot rendered by clients during mode voting.</summary>
    public sealed class MatchModeVoteState
    {
        private readonly MatchMode[] _availableModes;

        /// <summary>Creates a defensive snapshot of the current authoritative vote.</summary>
        public MatchModeVoteState(
            MatchAssignment assignment,
            IReadOnlyList<MatchMode> availableModes,
            int solosVotes,
            int duosVotes,
            int squadsVotes)
        {
            Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
            if (availableModes == null || availableModes.Count == 0)
            {
                throw new ArgumentException("At least one voting mode is required.", nameof(availableModes));
            }

            if (solosVotes < 0 || duosVotes < 0 || squadsVotes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(solosVotes));
            }

            _availableModes = new MatchMode[availableModes.Count];
            for (var index = 0; index < availableModes.Count; index++)
            {
                var mode = availableModes[index];
                if (!Enum.IsDefined(typeof(MatchMode), mode))
                {
                    throw new ArgumentOutOfRangeException(nameof(availableModes));
                }

                _availableModes[index] = mode;
            }

            SolosVotes = solosVotes;
            DuosVotes = duosVotes;
            SquadsVotes = squadsVotes;
        }

        /// <summary>Gets the latest assignment chosen by the server.</summary>
        public MatchAssignment Assignment { get; }

        /// <summary>Gets the modes valid for the locked participant snapshot.</summary>
        public IReadOnlyList<MatchMode> AvailableModes => _availableModes;

        /// <summary>Gets the number of accepted Solos votes.</summary>
        public int SolosVotes { get; }

        /// <summary>Gets the number of accepted Duos votes.</summary>
        public int DuosVotes { get; }

        /// <summary>Gets the number of accepted Squads votes.</summary>
        public int SquadsVotes { get; }

        /// <summary>Gets the accepted vote count for one mode.</summary>
        public int GetVoteCount(MatchMode mode)
        {
            return mode switch
            {
                MatchMode.Solos => SolosVotes,
                MatchMode.Duos => DuosVotes,
                MatchMode.Squads => SquadsVotes,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }

        /// <summary>Returns whether the server currently accepts votes for one mode.</summary>
        public bool IsAvailable(MatchMode mode)
        {
            for (var index = 0; index < _availableModes.Length; index++)
            {
                if (_availableModes[index] == mode)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
