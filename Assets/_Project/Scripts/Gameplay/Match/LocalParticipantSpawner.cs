using System;
using System.Collections.Generic;
using PawArena.Core.Match;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Combat;
using PawArena.Gameplay.Maps;
using PawArena.Gameplay.Player;
using UnityEngine;

namespace PawArena.Gameplay.Match
{
    /// <summary>Applies authoritative participant snapshots to the local test scene.</summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantSpawner : MonoBehaviour
    {
        [SerializeField] private CatPlayerController _player = null;
        [SerializeField] private GameObject _botPrefab = null;
        [SerializeField] private Transform _actorsRoot = null;

        private readonly List<GameObject> _bots = new(AuthoritativeMatchServer.MaximumLobbyMembers - 1);
        private readonly List<LocalMatchActor> _actors = new(AuthoritativeMatchServer.MaximumLobbyMembers);
        private int _activeBotCount;

        /// <summary>Gets the currently presented match actors.</summary>
        public IReadOnlyList<LocalMatchActor> Actors => _actors;

        /// <summary>Spawns all lobby members so the single human can see stationary bots.</summary>
        public bool TrySpawnLobby(IReadOnlyList<LobbyMember> members, MapSpawnPoints spawnPoints)
        {
            if (members == null)
            {
                return false;
            }

            var participants = new MatchParticipant[members.Count];
            for (var index = 0; index < members.Count; index++)
            {
                participants[index] = new MatchParticipant(members[index].PlayerId, members[index].IsBot);
            }

            return TrySpawn(
                participants,
                spawnPoints,
                true,
                default);
        }

        /// <summary>Spawns only participants selected by the authoritative server.</summary>
        public bool TrySpawnMatch(MatchAssignment assignment, MapSpawnPoints spawnPoints)
        {
            if (assignment == null)
            {
                return false;
            }

            if (!TryFindLocalParticipant(assignment.Participants, out var localParticipant))
            {
                return false;
            }

            return TrySpawn(
                assignment.Participants,
                spawnPoints,
                false,
                localParticipant);
        }

        /// <summary>Returns one eliminated actor to a unique lobby spawn point.</summary>
        public bool TryReturnToLobby(
            string playerId,
            MapSpawnPoints lobbySpawnPoints,
            int spawnIndex)
        {
            var actor = FindActor(playerId);
            if (actor == null || lobbySpawnPoints == null || lobbySpawnPoints.Count == 0)
            {
                return false;
            }

            var point = lobbySpawnPoints.GetPoint(Mathf.Clamp(spawnIndex, 0, lobbySpawnPoints.Count - 1));
            if (point == null)
            {
                return false;
            }

            ConfigureActorAtSpawn(actor, point, true);
            actor.IsEliminated = true;
            return true;
        }

        private bool TrySpawn(
            IReadOnlyList<MatchParticipant> participants,
            MapSpawnPoints spawnPoints,
            bool isLobby,
            MatchParticipant localParticipant)
        {
            if (_player == null || _botPrefab == null || spawnPoints == null ||
                participants == null || participants.Count > spawnPoints.Count)
            {
                Debug.LogError("Local participant spawning is not configured correctly.", this);
                return false;
            }

            DeactivateBots();
            _actors.Clear();
            var spawnIndex = 0;
            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                var point = spawnPoints.GetPoint(spawnIndex++);
                if (point == null)
                {
                    DeactivateBots();
                    _actors.Clear();
                    return false;
                }

                var isLocalTeammate = !isLobby && participant.IsTeammateWith(localParticipant);
                if (!participant.IsBot)
                {
                    var motor = _player.GetComponent<CatLocomotion>();
                    var actor = new LocalMatchActor(
                        participant.PlayerId,
                        false,
                        _player.gameObject,
                        motor,
                        _player.GetComponent<CatCombatant>(),
                        participant.TeamIndex,
                        isLocalTeammate);
                    ConfigureActorAtSpawn(actor, point, isLobby);
                    _actors.Add(actor);
                    continue;
                }

                var bot = AcquireBot(point);
                bot.name = $"Bot_{participant.PlayerId}";
                var botMotor = bot.GetComponent<CatLocomotion>();
                var botActor = new LocalMatchActor(
                    participant.PlayerId,
                    true,
                    bot,
                    botMotor,
                    bot.GetComponent<CatCombatant>(),
                    participant.TeamIndex,
                    isLocalTeammate);
                ConfigureActorAtSpawn(botActor, point, isLobby);
                _actors.Add(botActor);
            }

            return true;
        }

        private void OnDestroy() => DestroyBots();

        private void ConfigureActorAtSpawn(LocalMatchActor actor, Transform point, bool isLobby)
        {
            if (actor.Motor == null)
            {
                return;
            }

            if (actor.Combatant != null)
            {
                actor.Combatant.SetAutomaticRingOutRecovery(isLobby);
                actor.Combatant.SetSpawn(point.position, point.rotation);
                actor.Combatant.Respawn();
            }
            else
            {
                actor.Motor.SetSuspended(false);
                actor.Motor.Warp(point.position, point.rotation);
            }

            actor.Motor.SetSuspended(isLobby && actor.IsBot);
            actor.IsEliminated = false;
            ConfigureTeamIndicator(actor, isLobby);
            if (!actor.IsBot)
            {
                _player.SetHome(point.position, point.rotation);
            }
        }

        private static void ConfigureTeamIndicator(LocalMatchActor actor, bool isLobby)
        {
            var indicator = actor.Instance.GetComponent<TeamMemberIndicator>();
            var shouldShow = !isLobby && actor.IsLocalTeammate;
            if (indicator == null && shouldShow)
            {
                indicator = actor.Instance.AddComponent<TeamMemberIndicator>();
            }

            indicator?.SetVisible(shouldShow);
        }

        private static bool TryFindLocalParticipant(
            IReadOnlyList<MatchParticipant> participants,
            out MatchParticipant participant)
        {
            for (var index = 0; index < participants.Count; index++)
            {
                if (!participants[index].IsBot)
                {
                    participant = participants[index];
                    return true;
                }
            }

            participant = default;
            return false;
        }

        private LocalMatchActor FindActor(string playerId)
        {
            for (var index = 0; index < _actors.Count; index++)
            {
                if (string.Equals(_actors[index].PlayerId, playerId, StringComparison.Ordinal))
                {
                    return _actors[index];
                }
            }

            return null;
        }

        private GameObject AcquireBot(Transform point)
        {
            GameObject bot;
            if (_activeBotCount < _bots.Count)
            {
                bot = _bots[_activeBotCount];
            }
            else
            {
                bot = Instantiate(_botPrefab, point.position, point.rotation, _actorsRoot);
                _bots.Add(bot);
            }

            _activeBotCount++;
            if (bot.transform.parent != _actorsRoot)
            {
                bot.transform.SetParent(_actorsRoot, true);
            }

            bot.transform.SetPositionAndRotation(point.position, point.rotation);
            if (!bot.activeSelf)
            {
                bot.SetActive(true);
            }

            return bot;
        }

        private void DeactivateBots()
        {
            RemoveDestroyedBots();
            for (var index = 0; index < _bots.Count; index++)
            {
                var bot = _bots[index];
                if (bot != null && bot.activeSelf)
                {
                    bot.SetActive(false);
                }
            }

            _activeBotCount = 0;
        }

        private void RemoveDestroyedBots()
        {
            for (var index = _bots.Count - 1; index >= 0; index--)
            {
                if (_bots[index] == null)
                {
                    _bots.RemoveAt(index);
                }
            }
        }

        private void DestroyBots()
        {
            for (var index = 0; index < _bots.Count; index++)
            {
                var bot = _bots[index];
                if (bot == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(bot);
                }
                else
                {
                    DestroyImmediate(bot);
                }
            }

            _activeBotCount = 0;
            _bots.Clear();
            _actors.Clear();
        }
    }
}
