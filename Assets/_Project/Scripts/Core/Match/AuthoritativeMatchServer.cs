using System;
using System.Collections.Generic;
using PawArena.Core.Time;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Owns lobby eligibility, match selection, and start authorization.
    /// Clients may observe its events but cannot choose match data.
    /// </summary>
    public sealed class AuthoritativeMatchServer
    {
        /// <summary>Maximum number of members retained by one lobby.</summary>
        public const int MaximumLobbyMembers = 16;

        /// <summary>Required warning period before the server authorizes spawning.</summary>
        public const int CountdownSeconds = 15;

        /// <summary>Default time that match results remain visible before returning to MainLobby.</summary>
        public const int DefaultResultsDisplaySeconds = 5;

        /// <summary>Default authoritative play time before an unresolved match becomes a draw.</summary>
        public const int DefaultMatchDurationSeconds = 120;

        /// <summary>Default lobby intermission before the server may select the next map.</summary>
        public const int DefaultNextMatchSelectionDelaySeconds = 3;

        private readonly IClock _clock;
        private readonly MatchSelectionService _selector;
        private readonly List<LobbyMember> _members;
        private readonly List<ArenaMapRule> _maps;
        private readonly Dictionary<string, MatchMode> _modeVotes =
            new(StringComparer.Ordinal);
        private readonly TimeSpan _resultsDisplayDuration;
        private readonly TimeSpan _matchDuration;
        private readonly TimeSpan _nextMatchSelectionDelay;
        private bool _startAuthorized;
        private MatchMode _modeVoteTieBreaker;

        /// <summary>Creates a local host for the same authority rules used by a future remote server.</summary>
        public AuthoritativeMatchServer(
            IClock clock,
            IReadOnlyList<LobbyMember> members,
            IReadOnlyList<ArenaMapRule> maps,
            int randomSeed)
            : this(
                clock,
                members,
                maps,
                randomSeed,
                TimeSpan.FromSeconds(DefaultResultsDisplaySeconds),
                TimeSpan.FromSeconds(DefaultMatchDurationSeconds),
                TimeSpan.FromSeconds(DefaultNextMatchSelectionDelaySeconds))
        {
        }

        /// <summary>Creates a local host with an explicit server-owned results duration.</summary>
        public AuthoritativeMatchServer(
            IClock clock,
            IReadOnlyList<LobbyMember> members,
            IReadOnlyList<ArenaMapRule> maps,
            int randomSeed,
            TimeSpan resultsDisplayDuration)
            : this(
                clock,
                members,
                maps,
                randomSeed,
                resultsDisplayDuration,
                TimeSpan.FromSeconds(DefaultMatchDurationSeconds),
                TimeSpan.FromSeconds(DefaultNextMatchSelectionDelaySeconds))
        {
        }

        /// <summary>
        /// Creates a local host with explicit server-owned results and match durations.
        /// </summary>
        public AuthoritativeMatchServer(
            IClock clock,
            IReadOnlyList<LobbyMember> members,
            IReadOnlyList<ArenaMapRule> maps,
            int randomSeed,
            TimeSpan resultsDisplayDuration,
            TimeSpan matchDuration)
            : this(
                clock,
                members,
                maps,
                randomSeed,
                resultsDisplayDuration,
                matchDuration,
                TimeSpan.FromSeconds(DefaultNextMatchSelectionDelaySeconds))
        {
        }

        /// <summary>
        /// Creates a local host with explicit server-owned result, match, and next-map timings.
        /// </summary>
        public AuthoritativeMatchServer(
            IClock clock,
            IReadOnlyList<LobbyMember> members,
            IReadOnlyList<ArenaMapRule> maps,
            int randomSeed,
            TimeSpan resultsDisplayDuration,
            TimeSpan matchDuration,
            TimeSpan nextMatchSelectionDelay)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (resultsDisplayDuration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(resultsDisplayDuration));
            }

            if (matchDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(matchDuration));
            }

            if (nextMatchSelectionDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(nextMatchSelectionDelay));
            }

            if (members == null || members.Count == 0 || members.Count > MaximumLobbyMembers)
            {
                throw new ArgumentOutOfRangeException(nameof(members));
            }

            if (maps == null || maps.Count == 0)
            {
                throw new ArgumentException("At least one map is required.", nameof(maps));
            }

            _members = new List<LobbyMember>(members.Count);
            for (var index = 0; index < members.Count; index++)
            {
                _members.Add(members[index] ?? throw new ArgumentException("Lobby member cannot be null."));
            }

            _maps = new List<ArenaMapRule>(maps.Count);
            for (var index = 0; index < maps.Count; index++)
            {
                _maps.Add(maps[index]);
            }

            _selector = new MatchSelectionService(randomSeed);
            _resultsDisplayDuration = resultsDisplayDuration;
            _matchDuration = matchDuration;
            _nextMatchSelectionDelay = nextMatchSelectionDelay;
        }

        /// <summary>Raised after the server locks the map, participants, and start timestamp.</summary>
        public event Action<MatchAssignment> MatchScheduled;

        /// <summary>Raised after the server accepts a vote and recomputes the winning mode.</summary>
        public event Action<MatchModeVoteState> ModeVoteStateChanged;

        /// <summary>Raised if the server invalidates a countdown before it starts.</summary>
        public event Action MatchCancelled;

        /// <summary>Raised when the server timestamp reaches the assignment start timestamp.</summary>
        public event Action<MatchAssignment> MatchStartAuthorized;

        /// <summary>Raised after the server accepts one ring-out.</summary>
        public event Action<MatchElimination> ParticipantEliminated;

        /// <summary>Raised once after the server locks the round result.</summary>
        public event Action<MatchResult> MatchResolved;

        /// <summary>Raised when the authoritative match deadline starts or is cleared.</summary>
        public event Action<DateTimeOffset?> MatchDeadlineChanged;

        /// <summary>Raised once when the results period ends and clients may return to MainLobby.</summary>
        public event Action LobbyReturnAuthorized;

        /// <summary>Raised once when the lobby intermission ends and a new map may be selected.</summary>
        public event Action NextMatchSelectionAuthorized;

        /// <summary>Gets the current authoritative lobby snapshot.</summary>
        public IReadOnlyList<LobbyMember> Members => _members;

        /// <summary>Gets the current server assignment, if one has been scheduled.</summary>
        public MatchAssignment CurrentAssignment { get; private set; }

        /// <summary>Gets the latest immutable mode vote snapshot during countdown.</summary>
        public MatchModeVoteState CurrentModeVoteState { get; private set; }

        /// <summary>Gets the current elimination tracker after the start is authorized.</summary>
        public MatchEliminationTracker CurrentRound { get; private set; }

        /// <summary>Gets the configured amount of authoritative play time per match.</summary>
        public TimeSpan MatchDuration => _matchDuration;

        /// <summary>Gets the current authoritative match deadline while a round is active.</summary>
        public DateTimeOffset? MatchEndsAtUtc { get; private set; }

        /// <summary>Gets the non-negative amount of authoritative play time remaining.</summary>
        public TimeSpan MatchTimeRemaining
        {
            get
            {
                if (!MatchEndsAtUtc.HasValue)
                {
                    return TimeSpan.Zero;
                }

                var remaining = MatchEndsAtUtc.Value - _clock.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>Gets the server timestamp for returning to MainLobby after a resolved round.</summary>
        public DateTimeOffset? ReturnToLobbyAtUtc { get; private set; }

        /// <summary>Gets when the server may select the next map after returning to MainLobby.</summary>
        public DateTimeOffset? NextMatchSelectionAtUtc { get; private set; }

        /// <summary>Attempts to schedule a match from the current server-owned lobby state.</summary>
        public bool TryScheduleMatch()
        {
            if (CurrentAssignment != null || NextMatchSelectionAtUtc.HasValue)
            {
                return false;
            }

            var startsAtUtc = _clock.UtcNow.AddSeconds(CountdownSeconds);
            if (!_selector.TryCreateAssignment(_members, _maps, startsAtUtc, out var assignment))
            {
                return false;
            }

            CurrentAssignment = assignment;
            _modeVotes.Clear();
            _modeVoteTieBreaker = assignment.Mode;
            CurrentModeVoteState = CreateModeVoteState(assignment);
            _startAuthorized = false;
            MatchScheduled?.Invoke(assignment);
            ModeVoteStateChanged?.Invoke(CurrentModeVoteState);
            return true;
        }

        /// <summary>
        /// Accepts one human vote intent while retaining final authority over the selected mode.
        /// </summary>
        public bool TrySubmitModeVote(string playerId, MatchMode mode)
        {
            if (CurrentAssignment == null || CurrentModeVoteState == null || _startAuthorized ||
                _clock.UtcNow >= CurrentAssignment.StartsAtUtc ||
                !CurrentModeVoteState.IsAvailable(mode))
            {
                return false;
            }

            var member = FindMember(playerId);
            if (member == null || member.IsBot || member.IsAfk ||
                !ContainsParticipant(CurrentAssignment, playerId))
            {
                return false;
            }

            if (_modeVotes.TryGetValue(playerId, out var previousMode) && previousMode == mode)
            {
                return true;
            }

            _modeVotes[playerId] = mode;
            ApplyWinningMode();
            CurrentModeVoteState = CreateModeVoteState(CurrentAssignment);
            ModeVoteStateChanged?.Invoke(CurrentModeVoteState);
            return true;
        }

        /// <summary>Updates a human member's AFK state and reschedules when appropriate.</summary>
        public bool SetMemberAfk(string playerId, bool isAfk)
        {
            var member = FindMember(playerId);
            if (member == null || member.IsBot)
            {
                return false;
            }

            if (member.IsAfk == isAfk)
            {
                return true;
            }

            member.SetAfk(isAfk);
            if (isAfk && CurrentAssignment != null && !_startAuthorized)
            {
                ClearScheduledMatch();
                MatchCancelled?.Invoke();
            }
            else if (!isAfk && CurrentAssignment == null)
            {
                TryScheduleMatch();
            }

            return true;
        }

        /// <summary>Advances server time and authorizes spawning only after the deadline.</summary>
        public void Tick()
        {
            var now = _clock.UtcNow;
            if (ReturnToLobbyAtUtc.HasValue && now >= ReturnToLobbyAtUtc.Value)
            {
                ResetResolvedRound();
                NextMatchSelectionAtUtc = now.Add(_nextMatchSelectionDelay);
                LobbyReturnAuthorized?.Invoke();
                return;
            }

            if (NextMatchSelectionAtUtc.HasValue && now >= NextMatchSelectionAtUtc.Value)
            {
                NextMatchSelectionAtUtc = null;
                NextMatchSelectionAuthorized?.Invoke();
                return;
            }

            if (CurrentAssignment == null)
            {
                return;
            }

            if (!_startAuthorized)
            {
                if (now < CurrentAssignment.StartsAtUtc)
                {
                    return;
                }

                ApplyWinningMode();
                CurrentModeVoteState = CreateModeVoteState(CurrentAssignment);
                _startAuthorized = true;
                CurrentRound = new MatchEliminationTracker(CurrentAssignment);
                MatchEndsAtUtc = CurrentAssignment.StartsAtUtc.Add(_matchDuration);
                MatchDeadlineChanged?.Invoke(MatchEndsAtUtc);
                MatchStartAuthorized?.Invoke(CurrentAssignment);
            }

            if (MatchEndsAtUtc.HasValue && now >= MatchEndsAtUtc.Value &&
                CurrentRound != null && CurrentRound.TryResolveAtDeadline(out var result))
            {
                CompleteRoundResolution(result);
            }
        }

        /// <summary>Records one server-confirmed participant fall.</summary>
        public bool TryEliminate(string playerId)
        {
            return TryEliminate(playerId, string.Empty, string.Empty);
        }

        /// <summary>Records one server-confirmed fall and validates its attacker attribution.</summary>
        public bool TryEliminate(string playerId, string attackerId, string cause)
        {
            if (!_startAuthorized || CurrentRound == null ||
                (MatchEndsAtUtc.HasValue && _clock.UtcNow >= MatchEndsAtUtc.Value))
            {
                return false;
            }

            var acceptedAttackerId = string.Empty;
            if (!string.IsNullOrWhiteSpace(attackerId) &&
                !string.Equals(playerId, attackerId, StringComparison.Ordinal) &&
                CurrentRound.TryGetState(attackerId, out _))
            {
                acceptedAttackerId = attackerId;
            }

            if (!CurrentRound.TryEliminate(
                    playerId,
                    acceptedAttackerId,
                    cause ?? string.Empty,
                    out var elimination))
            {
                return false;
            }

            ParticipantEliminated?.Invoke(elimination);
            return true;
        }

        /// <summary>Resolves the current physics batch and emits the winner once.</summary>
        public bool TryResolveRound()
        {
            if (!_startAuthorized || CurrentRound == null)
            {
                return false;
            }

            var deadlineReached = MatchEndsAtUtc.HasValue && _clock.UtcNow >= MatchEndsAtUtc.Value;
            var didResolve = deadlineReached
                ? CurrentRound.TryResolveAtDeadline(out var result)
                : CurrentRound.TryResolve(out result);
            if (!didResolve)
            {
                return false;
            }

            CompleteRoundResolution(result);
            return true;
        }

        private void ResetResolvedRound()
        {
            ClearScheduledMatch();
            CurrentRound = null;
            ReturnToLobbyAtUtc = null;
            _startAuthorized = false;
        }

        private void CompleteRoundResolution(MatchResult result)
        {
            ReturnToLobbyAtUtc = _clock.UtcNow.Add(_resultsDisplayDuration);
            ClearMatchDeadline();
            MatchResolved?.Invoke(result);
        }

        private void ClearMatchDeadline()
        {
            if (!MatchEndsAtUtc.HasValue)
            {
                return;
            }

            MatchEndsAtUtc = null;
            MatchDeadlineChanged?.Invoke(null);
        }

        private MatchModeVoteState CreateModeVoteState(MatchAssignment assignment)
        {
            var map = FindMap(assignment.MapId);
            var availableModes = new List<MatchMode>();
            for (var index = 0; index < map.SupportedModes.Count; index++)
            {
                var mode = map.SupportedModes[index];
                if (MatchModeRules.SupportsParticipantCount(mode, assignment.Participants.Count))
                {
                    availableModes.Add(mode);
                }
            }

            return new MatchModeVoteState(
                assignment,
                availableModes,
                CountVotes(MatchMode.Solos),
                CountVotes(MatchMode.Duos),
                CountVotes(MatchMode.Squads));
        }

        private bool ApplyWinningMode()
        {
            var selectedMode = SelectWinningMode();
            if (selectedMode == CurrentAssignment.Mode)
            {
                return false;
            }

            CurrentAssignment = new MatchAssignment(
                CurrentAssignment.MatchId,
                CurrentAssignment.MapId,
                CurrentAssignment.Participants,
                CurrentAssignment.StartsAtUtc,
                selectedMode);
            return true;
        }

        private MatchMode SelectWinningMode()
        {
            var availableModes = CurrentModeVoteState.AvailableModes;
            var highestVoteCount = -1;
            for (var index = 0; index < availableModes.Count; index++)
            {
                highestVoteCount = Math.Max(
                    highestVoteCount,
                    CountVotes(availableModes[index]));
            }

            if (CurrentModeVoteState.IsAvailable(_modeVoteTieBreaker) &&
                CountVotes(_modeVoteTieBreaker) == highestVoteCount)
            {
                return _modeVoteTieBreaker;
            }

            for (var index = 0; index < availableModes.Count; index++)
            {
                if (CountVotes(availableModes[index]) == highestVoteCount)
                {
                    return availableModes[index];
                }
            }

            return _modeVoteTieBreaker;
        }

        private int CountVotes(MatchMode mode)
        {
            var count = 0;
            foreach (var vote in _modeVotes.Values)
            {
                if (vote == mode)
                {
                    count++;
                }
            }

            return count;
        }

        private ArenaMapRule FindMap(string mapId)
        {
            for (var index = 0; index < _maps.Count; index++)
            {
                if (string.Equals(_maps[index].MapId, mapId, StringComparison.Ordinal))
                {
                    return _maps[index];
                }
            }

            throw new InvalidOperationException($"Map '{mapId}' is missing from the server catalog.");
        }

        private static bool ContainsParticipant(MatchAssignment assignment, string playerId)
        {
            for (var index = 0; index < assignment.Participants.Count; index++)
            {
                if (string.Equals(
                        assignment.Participants[index].PlayerId,
                        playerId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearScheduledMatch()
        {
            ClearMatchDeadline();
            CurrentAssignment = null;
            CurrentModeVoteState = null;
            _modeVotes.Clear();
            _modeVoteTieBreaker = MatchMode.Solos;
        }

        private LobbyMember FindMember(string playerId)
        {
            for (var index = 0; index < _members.Count; index++)
            {
                if (string.Equals(_members[index].PlayerId, playerId, StringComparison.Ordinal))
                {
                    return _members[index];
                }
            }

            return null;
        }
    }
}
