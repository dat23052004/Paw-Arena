using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Events;
using PawArena.Core.Match;
using PawArena.Core.Profiles;
using PawArena.Core.Quests;
using PawArena.Core.Services;
using PawArena.Core.Time;
using PawArena.Gameplay.Combat;
using PawArena.Gameplay.Maps;
using PawArena.Gameplay.Match;
using PawArena.Infrastructure.Time;
using PawArena.UI.Hud;
using PawArena.UI.Navigation;
using UnityEngine;

namespace PawArena.Bootstrap
{
    /// <summary>
    /// Composes the local authoritative server and read-only client presentation for the test slice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalMatchSession : MonoBehaviour
    {
        private const string LocalPlayerId = "local-player";
        private const string RingOutCause = "RingOut";

        [Header("Server Data")]
        [SerializeField] private ArenaMapCatalog _mapCatalog = null;
        [SerializeField, Range(0, AuthoritativeMatchServer.MaximumLobbyMembers - 1)]
        private int _botCount = 7;
        [SerializeField, Min(0f), Tooltip("Seconds the server keeps the winner visible before returning everyone to MainLobby.")]
        private float _returnToLobbyDelaySeconds = AuthoritativeMatchServer.DefaultResultsDisplaySeconds;
        [SerializeField, Min(0f), Tooltip("Seconds spent in MainLobby before the server selects the next map.")]
        private float _nextMatchSelectionDelaySeconds =
            AuthoritativeMatchServer.DefaultNextMatchSelectionDelaySeconds;
        [SerializeField, Min(1f), Tooltip("Server-owned play time before an unresolved match is declared a draw.")]
        private float _matchDurationSeconds = AuthoritativeMatchServer.DefaultMatchDurationSeconds;
        [SerializeField, Min(0f), Tooltip("Seconds after a hit or grab that the server may award ring-out credit.")]
        private float _killCreditWindowSeconds = 10f;

        [Header("Client Presentation")]
        [SerializeField] private MatchManager _matchManager = null;
        [SerializeField] private ArenaMapLoader _mapLoader = null;
        [SerializeField] private LocalParticipantSpawner _participantSpawner = null;
        [SerializeField] private LocalSpectatorController _spectatorController = null;
        [SerializeField] private LobbyHudView _lobbyHud = null;
        [SerializeField] private MatchCountdownView _countdownView = null;
        [SerializeField] private SpectatorHudView _spectatorHud = null;
        [SerializeField] private LobbyDailyQuestHudView _dailyQuestHud = null;
        [SerializeField] private GameplayAbilityHudView _abilityHud = null;
        [SerializeField] private KillFeedHudView _killFeedHud = null;
        [SerializeField] private CatPlayerCombat _playerCombat = null;

        private readonly bool[] _fallenThisStep = new bool[AuthoritativeMatchServer.MaximumLobbyMembers];
        private AuthoritativeMatchServer _server;
        private DailyQuestService _dailyQuests;
        private IEventBus _eventBus;
        private IUIService _uiService;
        private MatchAssignment _visibleAssignment;
        private string _visibleMapName = string.Empty;
        private bool _roundPresentationActive;
        private bool _localPlayerActiveInRound;
        private float _nextDailyResetCheck;

        /// <summary>Raised for a platform-specific friend invitation adapter.</summary>
        public event Action InviteFriendsRequested;

        /// <summary>Gets the local server used by the current offline test slice.</summary>
        public AuthoritativeMatchServer Server => _server;

        private void Start()
        {
            _playerCombat ??= FindAnyObjectByType<CatPlayerCombat>(FindObjectsInactive.Include);
            _killFeedHud ??= FindAnyObjectByType<KillFeedHudView>(FindObjectsInactive.Include);
            _killFeedHud ??= gameObject.AddComponent<KillFeedHudView>();
            if (!ValidateDependencies())
            {
                enabled = false;
                return;
            }

            if (!ServiceRegistry.TryGet(out _uiService))
            {
                _uiService = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            }

            ServiceRegistry.TryGet(out _eventBus);

            if (!_dailyQuestHud.Initialize())
            {
                Debug.LogError("Daily Quest HUD could not bind or build its complete panel.", this);
                enabled = false;
                return;
            }

            if (!_killFeedHud.Initialize())
            {
                Debug.LogError("Kill feed HUD could not bind or build its presentation.", this);
                enabled = false;
                return;
            }

            var members = CreateLobbyMembers();
            var mapRules = CreateMapRules();
            var clock = ServiceRegistry.TryGet<IClock>(out var registeredClock)
                ? registeredClock
                : new SystemClock();
            var wallet = ServiceRegistry.TryGet<IWalletService>(out var registeredWallet)
                ? registeredWallet
                : new WalletService(clock);
            _dailyQuests = new DailyQuestService(clock, wallet, LocalPlayerId);
            _server = new AuthoritativeMatchServer(
                clock,
                members,
                mapRules,
                Environment.TickCount,
                TimeSpan.FromSeconds(_returnToLobbyDelaySeconds),
                TimeSpan.FromSeconds(_matchDurationSeconds),
                TimeSpan.FromSeconds(_nextMatchSelectionDelaySeconds));
            _server.MatchScheduled += HandleMatchScheduled;
            _server.ModeVoteStateChanged += HandleModeVoteStateChanged;
            _server.MatchCancelled += HandleMatchCancelled;
            _server.MatchStartAuthorized += HandleMatchStartAuthorized;
            _server.ParticipantEliminated += HandleParticipantEliminated;
            _server.MatchResolved += HandleMatchResolved;
            _server.LobbyReturnAuthorized += HandleLobbyReturnAuthorized;
            _server.NextMatchSelectionAuthorized += HandleNextMatchSelectionAuthorized;
            _matchManager.CountdownChanged += HandleCountdownChanged;
            _matchManager.SpawnRequested += HandleSpawnRequested;
            _countdownView.ModeVoteRequested += HandleModeVoteRequested;
            _spectatorController.TargetChanged += HandleSpectatorTargetChanged;
            _spectatorHud.PreviousRequested += HandleSpectatorPreviousRequested;
            _spectatorHud.NextRequested += HandleSpectatorNextRequested;
            _spectatorHud.StopRequested += HandleStopSpectatingRequested;
            _spectatorHud.InviteRequested += HandleInviteFriendsRequested;
            _dailyQuestHud.InviteRequested += HandleInviteFriendsRequested;
            _dailyQuestHud.QuestRequested += HandleDailyQuestRequested;
            _dailyQuests.Changed += RefreshDailyQuestHud;
            _playerCombat.AbilityChanged += HandleAbilityChanged;
            _lobbyHud.AfkChanged += HandleAfkChanged;
            _spectatorHud.Hide();
            _abilityHud.Hide();
            _killFeedHud.ClearAndHide();
            _dailyQuestHud.SetVisible(true);
            RefreshDailyQuestHud();

            if (!_participantSpawner.TrySpawnLobby(_server.Members, _mapLoader.LobbySpawnPoints))
            {
                Debug.LogError("The local lobby participants could not be spawned.", this);
                enabled = false;
                return;
            }

            _server.TryScheduleMatch();
        }

        private void Update()
        {
            _server?.Tick();
            if (_dailyQuests != null && Time.unscaledTime >= _nextDailyResetCheck)
            {
                _nextDailyResetCheck = Time.unscaledTime + 30f;
                _dailyQuests.RefreshDailyReset();
            }

            if (_localPlayerActiveInRound && _playerCombat != null)
            {
                _abilityHud.SetCooldown(
                    _playerCombat.SelectedCooldownNormalized,
                    _playerCombat.SelectedCooldown);
            }
        }

        private void LateUpdate()
        {
            if (_roundPresentationActive)
            {
                EvaluateAuthoritativeRingOuts();
            }
        }

        private void OnDestroy()
        {
            if (_server != null)
            {
                _server.MatchScheduled -= HandleMatchScheduled;
                _server.ModeVoteStateChanged -= HandleModeVoteStateChanged;
                _server.MatchCancelled -= HandleMatchCancelled;
                _server.MatchStartAuthorized -= HandleMatchStartAuthorized;
                _server.ParticipantEliminated -= HandleParticipantEliminated;
                _server.MatchResolved -= HandleMatchResolved;
                _server.LobbyReturnAuthorized -= HandleLobbyReturnAuthorized;
                _server.NextMatchSelectionAuthorized -= HandleNextMatchSelectionAuthorized;
            }

            if (_matchManager != null)
            {
                _matchManager.CountdownChanged -= HandleCountdownChanged;
                _matchManager.SpawnRequested -= HandleSpawnRequested;
            }

            if (_countdownView != null)
            {
                _countdownView.ModeVoteRequested -= HandleModeVoteRequested;
            }

            if (_spectatorController != null)
            {
                _spectatorController.TargetChanged -= HandleSpectatorTargetChanged;
            }

            if (_spectatorHud != null)
            {
                _spectatorHud.PreviousRequested -= HandleSpectatorPreviousRequested;
                _spectatorHud.NextRequested -= HandleSpectatorNextRequested;
                _spectatorHud.StopRequested -= HandleStopSpectatingRequested;
                _spectatorHud.InviteRequested -= HandleInviteFriendsRequested;
            }

            if (_dailyQuestHud != null)
            {
                _dailyQuestHud.InviteRequested -= HandleInviteFriendsRequested;
                _dailyQuestHud.QuestRequested -= HandleDailyQuestRequested;
            }

            if (_dailyQuests != null)
            {
                _dailyQuests.Changed -= RefreshDailyQuestHud;
            }

            if (_playerCombat != null)
            {
                _playerCombat.AbilityChanged -= HandleAbilityChanged;
            }

            if (_lobbyHud != null)
            {
                _lobbyHud.AfkChanged -= HandleAfkChanged;
            }
        }

        private void OnValidate()
        {
            if (!float.IsFinite(_returnToLobbyDelaySeconds) || _returnToLobbyDelaySeconds < 0f)
            {
                _returnToLobbyDelaySeconds = AuthoritativeMatchServer.DefaultResultsDisplaySeconds;
            }

            if (!float.IsFinite(_matchDurationSeconds) || _matchDurationSeconds < 1f)
            {
                _matchDurationSeconds = AuthoritativeMatchServer.DefaultMatchDurationSeconds;
            }

            if (!float.IsFinite(_nextMatchSelectionDelaySeconds) ||
                _nextMatchSelectionDelaySeconds < 0f)
            {
                _nextMatchSelectionDelaySeconds =
                    AuthoritativeMatchServer.DefaultNextMatchSelectionDelaySeconds;
            }

            if (!float.IsFinite(_killCreditWindowSeconds) || _killCreditWindowSeconds < 0f)
            {
                _killCreditWindowSeconds = 10f;
            }
        }

        private List<LobbyMember> CreateLobbyMembers()
        {
            var members = new List<LobbyMember>(_botCount + 1)
            {
                new(LocalPlayerId, false)
            };
            for (var index = 0; index < _botCount; index++)
            {
                members.Add(new LobbyMember($"bot-{index + 1:00}", true));
            }

            return members;
        }

        private List<ArenaMapRule> CreateMapRules()
        {
            var rules = new List<ArenaMapRule>(_mapCatalog.Maps.Count);
            for (var index = 0; index < _mapCatalog.Maps.Count; index++)
            {
                var definition = _mapCatalog.Maps[index];
                if (definition != null)
                {
                    rules.Add(definition.ToRule());
                }
            }

            return rules;
        }

        private void HandleMatchScheduled(MatchAssignment assignment)
        {
            _localPlayerActiveInRound = false;
            _spectatorController.StopSpectating();
            _spectatorHud.Hide();
            _abilityHud.Hide();
            _killFeedHud.ClearAndHide();
            _dailyQuestHud.SetVisible(true);
            _visibleAssignment = assignment;
            _visibleMapName = _mapCatalog.TryGet(assignment.MapId, out var definition)
                ? definition.DisplayName
                : assignment.MapId;
            if (!_mapLoader.TryLoad(assignment.MapId, out _))
            {
                Debug.LogError("Client could not present the server-selected arena.", this);
                return;
            }

            if (!_matchManager.TryBeginCountdown(assignment))
            {
                Debug.LogError("Client could not enter the authoritative countdown.", this);
            }
        }

        private void HandleModeVoteStateChanged(MatchModeVoteState voteState)
        {
            if (voteState == null)
            {
                return;
            }

            _visibleAssignment = voteState.Assignment;
            HandleCountdownChanged(Mathf.CeilToInt(_matchManager.CountdownRemaining));
        }

        private void HandleMatchCancelled()
        {
            _visibleAssignment = null;
            _visibleMapName = string.Empty;
            _roundPresentationActive = false;
            _localPlayerActiveInRound = false;
            _spectatorController.StopSpectating();
            _spectatorHud.Hide();
            _abilityHud.Hide();
            _killFeedHud.ClearAndHide();
            _dailyQuestHud.SetVisible(true);
            _mapLoader.UnloadGameplayMap();
            _matchManager.TryCancelStart();
            _countdownView.ShowAfk();
        }

        private void HandleMatchStartAuthorized(MatchAssignment assignment)
        {
            if (!_matchManager.TryAuthorizeSpawn(assignment))
            {
                Debug.LogError("Client rejected a valid server start authorization.", this);
            }
        }

        private void HandleSpawnRequested(MatchAssignment assignment)
        {
            var spawnPoints = _mapLoader.CurrentSpawnPoints;
            if ((spawnPoints == null && !_mapLoader.TryLoad(assignment.MapId, out spawnPoints)) ||
                !_participantSpawner.TrySpawnMatch(assignment, spawnPoints))
            {
                Debug.LogError("The authoritative match could not be presented locally.", this);
                _matchManager.TryCancelStart();
                return;
            }

            if (!_matchManager.TryBeginPlaying())
            {
                Debug.LogError("Client could not enter the playing state after spawning.", this);
                return;
            }

            _roundPresentationActive = true;
            _localPlayerActiveInRound = true;
            _uiService?.CloseAll();
            _countdownView.Hide();
            _spectatorHud.Hide();
            _killFeedHud.ClearAndHide();
            _killFeedHud.SetVisible(true);
            _dailyQuestHud.SetVisible(false);
            ShowSelectedAbility();
        }

        private void HandleParticipantEliminated(MatchElimination elimination)
        {
            _killFeedHud.ShowElimination(
                FormatPlayerName(elimination.AttackerId),
                FormatPlayerName(elimination.PlayerId),
                elimination.HasAttacker);
            _eventBus?.Publish(new RingOutConfirmedEvent(
                elimination.PlayerId,
                elimination.AttackerId,
                elimination.Cause));

            if (!_participantSpawner.TryReturnToLobby(
                    elimination.PlayerId,
                    _mapLoader.LobbySpawnPoints,
                    elimination.EliminationOrder - 1))
            {
                Debug.LogError($"Could not return '{elimination.PlayerId}' to MainLobby.", this);
            }

            if (string.Equals(elimination.PlayerId, LocalPlayerId, StringComparison.Ordinal))
            {
                _localPlayerActiveInRound = false;
                _abilityHud.Hide();
                _dailyQuestHud.SetVisible(true);
                if (!_matchManager.TryBeginSpectating())
                {
                    Debug.LogError("Client could not enter spectator flow after elimination.", this);
                }

                _spectatorController.BeginSpectating(_participantSpawner.Actors);
            }
            else if (_spectatorController.IsSpectating)
            {
                _spectatorController.RefreshTarget();
            }

            RefreshSpectatorHud(elimination.ActiveCount);
        }

        private void HandleMatchResolved(MatchResult result)
        {
            _roundPresentationActive = false;
            _localPlayerActiveInRound = false;
            _spectatorHud.Hide();
            _abilityHud.Hide();
            _dailyQuestHud.SetVisible(true);
            _dailyQuests.ReportMatch(
                string.Equals(result.WinnerId, LocalPlayerId, StringComparison.Ordinal),
                HasHumanFriend(_visibleAssignment));
            if (!_matchManager.TryResolveMatch() || !_matchManager.TryShowResults())
            {
                Debug.LogError("Client could not enter match results after server resolution.", this);
            }

            if (result.IsDraw)
            {
                _countdownView.ShowDraw();
                return;
            }

            _spectatorController.FollowWinner(result.WinnerId);
            _countdownView.ShowWinner(
                FormatPlayerName(result.WinnerId),
                string.Equals(result.WinnerId, LocalPlayerId, StringComparison.Ordinal));
        }

        private void HandleLobbyReturnAuthorized()
        {
            _visibleAssignment = null;
            _visibleMapName = string.Empty;
            _roundPresentationActive = false;
            _localPlayerActiveInRound = false;
            _spectatorHud.Hide();
            _abilityHud.Hide();
            _killFeedHud.ClearAndHide();
            _dailyQuestHud.SetVisible(true);

            if (!_matchManager.TryResetToLobby())
            {
                Debug.LogError("Client could not return to the lobby after match results.", this);
            }

            _spectatorController.StopSpectating();
            if (!_participantSpawner.TrySpawnLobby(_server.Members, _mapLoader.LobbySpawnPoints))
            {
                Debug.LogError("Lobby participants could not be restored after match results.", this);
                return;
            }

            _countdownView.Hide();
        }

        private void HandleNextMatchSelectionAuthorized()
        {
            if (!_server.TryScheduleMatch())
            {
                _mapLoader.UnloadGameplayMap();
                _countdownView.ShowAfk();
            }
        }

        private void HandleCountdownChanged(int seconds)
        {
            if (_visibleAssignment == null)
            {
                return;
            }

            if (_mapCatalog.TryGet(_visibleAssignment.MapId, out var definition))
            {
                _countdownView.ShowCountdown(
                    _visibleMapName,
                    definition.Thumbnail,
                    definition.ThumbnailTint,
                    _visibleAssignment.Participants.Count,
                    seconds,
                    definition.ShowModeSelection,
                    _visibleAssignment.Mode,
                    _server.CurrentModeVoteState?.AvailableModes ?? definition.SupportedModes,
                    _server.CurrentModeVoteState);
                return;
            }

            _countdownView.ShowCountdown(_visibleMapName, _visibleAssignment.Participants.Count, seconds);
        }

        private void HandleAfkChanged(bool isAfk)
        {
            _server?.SetMemberAfk(LocalPlayerId, isAfk);
        }

        private void HandleModeVoteRequested(MatchMode mode)
        {
            _server?.TrySubmitModeVote(LocalPlayerId, mode);
        }

        private void HandleSpectatorTargetChanged(string playerId)
        {
            RefreshSpectatorHud(_server?.CurrentRound?.ActiveCount ?? 0);
        }

        private void HandleSpectatorPreviousRequested()
        {
            if (_spectatorController.IsSpectating)
            {
                _spectatorController.TrySelectPrevious();
                return;
            }

            TryResumeSpectating(-1);
        }

        private void HandleSpectatorNextRequested()
        {
            if (_spectatorController.IsSpectating)
            {
                _spectatorController.TrySelectNext();
                return;
            }

            TryResumeSpectating(1);
        }

        private void HandleStopSpectatingRequested()
        {
            _spectatorController.PauseSpectating();
            RefreshSpectatorHud(_server?.CurrentRound?.ActiveCount ?? 0);
        }

        private void HandleInviteFriendsRequested() => InviteFriendsRequested?.Invoke();

        private void HandleDailyQuestRequested(DailyQuestId questId)
        {
            if (_dailyQuests == null ||
                _dailyQuests.TryClaim(questId) != DailyQuestClaimResult.Success)
            {
                return;
            }

            if (!ServiceRegistry.TryGet<IProfileService>(out var profile))
            {
                return;
            }

            try
            {
                profile.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Daily Quest reward was granted but profile save failed: {exception.Message}", this);
            }
        }

        private void HandleAbilityChanged(CatCombatAbility ability) => ShowSelectedAbility();

        private void RefreshDailyQuestHud()
        {
            if (_dailyQuests == null || _dailyQuestHud == null)
            {
                return;
            }

            _dailyQuestHud.SetQuestProgress(_dailyQuests.GetProgress(DailyQuestId.PlayMatches));
            _dailyQuestHud.SetQuestProgress(_dailyQuests.GetProgress(DailyQuestId.WinMatches));
            _dailyQuestHud.SetQuestProgress(_dailyQuests.GetProgress(DailyQuestId.PlayWithFriend));
            _dailyQuestHud.SetResetAtUtc(_dailyQuests.ResetAtUtc);
        }

        private void RefreshSpectatorHud(int activeCount)
        {
            if (!CanLocalPlayerSpectate(activeCount))
            {
                _spectatorHud.Hide();
                return;
            }

            if (!_spectatorController.IsSpectating ||
                string.IsNullOrEmpty(_spectatorController.CurrentTargetId))
            {
                _spectatorHud.ShowAvailable();
                return;
            }

            _spectatorHud.Show(
                FormatPlayerName(_spectatorController.CurrentTargetId),
                activeCount > 1,
                1);
        }

        private bool CanLocalPlayerSpectate(int activeCount)
        {
            return _roundPresentationActive && activeCount > 0 &&
                   _server?.CurrentRound != null &&
                   _server.CurrentRound.TryGetState(LocalPlayerId, out var state) &&
                   state == ParticipantRoundState.Eliminated;
        }

        private void TryResumeSpectating(int direction)
        {
            var activeCount = _server?.CurrentRound?.ActiveCount ?? 0;
            if (!CanLocalPlayerSpectate(activeCount))
            {
                _spectatorHud.Hide();
                return;
            }

            if (_spectatorController.ResumeSpectating(direction) ||
                _spectatorController.BeginSpectating(_participantSpawner.Actors))
            {
                RefreshSpectatorHud(activeCount);
            }
        }

        private void ShowSelectedAbility()
        {
            if (!_localPlayerActiveInRound || _playerCombat == null)
            {
                return;
            }

            _abilityHud.ShowAbility(
                (int)_playerCombat.SelectedAbility,
                FormatAbilityName(_playerCombat.SelectedAbility),
                _playerCombat.SelectedCooldownNormalized,
                _playerCombat.SelectedCooldown);
        }

        private static bool HasHumanFriend(MatchAssignment assignment)
        {
            if (assignment == null)
            {
                return false;
            }

            var humanCount = 0;
            for (var index = 0; index < assignment.Participants.Count; index++)
            {
                if (!assignment.Participants[index].IsBot && ++humanCount > 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatAbilityName(CatCombatAbility ability)
        {
            return ability switch
            {
                CatCombatAbility.NineLives => "Nine Lives",
                CatCombatAbility.FluffUp => "Fluff Up",
                CatCombatAbility.SharkAttack => "Shark Attack",
                CatCombatAbility.WindShurikens => "Wind Shurikens",
                _ => ability.ToString()
            };
        }

        private void EvaluateAuthoritativeRingOuts()
        {
            if (_server?.CurrentRound == null || _server.CurrentRound.IsResolved)
            {
                return;
            }

            var actors = _participantSpawner.Actors;
            var lastToFallIndex = -1;
            var highestFallenY = float.NegativeInfinity;
            for (var index = 0; index < actors.Count; index++)
            {
                var actor = actors[index];
                var hasFallen = !actor.IsEliminated && actor.Transform != null &&
                                actor.Transform.position.y < _mapLoader.RingOutY;
                _fallenThisStep[index] = hasFallen;
                if (hasFallen && actor.Transform.position.y > highestFallenY)
                {
                    highestFallenY = actor.Transform.position.y;
                    lastToFallIndex = index;
                }
            }

            if (lastToFallIndex >= 0)
            {
                for (var index = 0; index < actors.Count; index++)
                {
                    if (_fallenThisStep[index] && index != lastToFallIndex)
                    {
                        TryEliminateActor(actors[index]);
                    }
                }

                TryEliminateActor(actors[lastToFallIndex]);
            }

            _server.TryResolveRound();
        }

        private void TryEliminateActor(LocalMatchActor actor)
        {
            _server.TryEliminate(actor.PlayerId, ResolveRecentAttackerId(actor), RingOutCause);
        }

        private string ResolveRecentAttackerId(LocalMatchActor eliminatedActor)
        {
            var combatant = eliminatedActor?.Combatant;
            var attacker = combatant?.LastAttacker;
            if (attacker == null ||
                Time.unscaledTime - combatant.LastAttackedAt > _killCreditWindowSeconds)
            {
                return string.Empty;
            }

            var actors = _participantSpawner.Actors;
            for (var index = 0; index < actors.Count; index++)
            {
                var candidate = actors[index];
                if (candidate != eliminatedActor && candidate.Combatant == attacker)
                {
                    return candidate.PlayerId;
                }
            }

            return string.Empty;
        }

        private static string FormatPlayerName(string playerId)
        {
            return string.Equals(playerId, LocalPlayerId, StringComparison.Ordinal)
                ? "Bạn"
                : playerId;
        }

        private bool ValidateDependencies()
        {
            var isValid = true;
            isValid &= ValidateReference(_mapCatalog, nameof(_mapCatalog));
            isValid &= ValidateReference(_matchManager, nameof(_matchManager));
            isValid &= ValidateReference(_mapLoader, nameof(_mapLoader));
            isValid &= ValidateReference(_participantSpawner, nameof(_participantSpawner));
            isValid &= ValidateReference(_spectatorController, nameof(_spectatorController));
            isValid &= ValidateReference(_lobbyHud, nameof(_lobbyHud));
            isValid &= ValidateReference(_countdownView, nameof(_countdownView));
            isValid &= ValidateReference(_spectatorHud, nameof(_spectatorHud));
            isValid &= ValidateReference(_dailyQuestHud, nameof(_dailyQuestHud));
            isValid &= ValidateReference(_abilityHud, nameof(_abilityHud));
            isValid &= ValidateReference(_killFeedHud, nameof(_killFeedHud));
            isValid &= ValidateReference(_playerCombat, nameof(_playerCombat));
            return isValid;
        }

        private bool ValidateReference(UnityEngine.Object value, string fieldName)
        {
            if (value != null)
            {
                return true;
            }

            Debug.LogError($"{fieldName} is not assigned.", this);
            return false;
        }
    }
}
