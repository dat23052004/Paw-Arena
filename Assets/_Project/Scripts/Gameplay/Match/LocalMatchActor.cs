using PawArena.Core.Match;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Combat;
using UnityEngine;

namespace PawArena.Gameplay.Match
{
    /// <summary>Maps one authoritative participant id to its local presentation object.</summary>
    public sealed class LocalMatchActor
    {
        /// <summary>Creates a local actor handle.</summary>
        public LocalMatchActor(
            string playerId,
            bool isBot,
            GameObject instance,
            CatLocomotion motor,
            CatCombatant combatant,
            int teamIndex = MatchParticipant.NoTeam,
            bool isLocalTeammate = false)
        {
            PlayerId = playerId;
            IsBot = isBot;
            Instance = instance;
            Motor = motor;
            Combatant = combatant;
            TeamIndex = teamIndex;
            IsLocalTeammate = isLocalTeammate;
        }

        /// <summary>Gets the authoritative participant identifier.</summary>
        public string PlayerId { get; }

        /// <summary>Gets whether this actor is server-controlled.</summary>
        public bool IsBot { get; }

        /// <summary>Gets the local presentation object.</summary>
        public GameObject Instance { get; }

        /// <summary>Gets the actor transform.</summary>
        public Transform Transform => Instance == null ? null : Instance.transform;

        /// <summary>Gets the locomotion component.</summary>
        public CatLocomotion Motor { get; }

        /// <summary>Gets the combat state component when present.</summary>
        public CatCombatant Combatant { get; }

        /// <summary>Gets the authoritative team index, or no team for Solos/lobby actors.</summary>
        public int TeamIndex { get; }

        /// <summary>Gets whether this actor is a teammate of the local participant.</summary>
        public bool IsLocalTeammate { get; }

        /// <summary>Gets whether the server has eliminated this participant.</summary>
        public bool IsEliminated { get; internal set; }
    }
}
