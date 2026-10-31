using System;
using System.Collections.Generic;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Selects maps and participants exclusively on the authoritative server.
    /// </summary>
    public sealed class MatchSelectionService
    {
        private readonly Random _random;

        /// <summary>Creates a deterministic selector from a server-owned random seed.</summary>
        public MatchSelectionService(int randomSeed)
        {
            _random = new Random(randomSeed);
        }

        /// <summary>
        /// Attempts to create an assignment while excluding AFK members and prioritizing humans.
        /// </summary>
        public bool TryCreateAssignment(
            IReadOnlyList<LobbyMember> lobbyMembers,
            IReadOnlyList<ArenaMapRule> maps,
            DateTimeOffset startsAtUtc,
            out MatchAssignment assignment)
        {
            assignment = null;
            if (lobbyMembers == null || maps == null)
            {
                return false;
            }

            var eligibleHumans = new List<LobbyMember>();
            var eligibleBots = new List<LobbyMember>();
            for (var index = 0; index < lobbyMembers.Count; index++)
            {
                var member = lobbyMembers[index];
                if (member == null || member.IsAfk)
                {
                    continue;
                }

                if (member.IsBot)
                {
                    eligibleBots.Add(member);
                }
                else
                {
                    eligibleHumans.Add(member);
                }
            }

            if (eligibleHumans.Count == 0)
            {
                return false;
            }

            var eligibleCount = eligibleHumans.Count + eligibleBots.Count;
            var candidateMaps = new List<ArenaMapRule>();
            for (var index = 0; index < maps.Count; index++)
            {
                var map = maps[index];
                if (HasValidMode(map, eligibleCount))
                {
                    candidateMaps.Add(map);
                }
            }

            if (candidateMaps.Count == 0)
            {
                return false;
            }

            var selectedMap = candidateMaps[_random.Next(candidateMaps.Count)];
            var maximumPlayers = Math.Min(selectedMap.MaximumPlayers, eligibleCount);
            var compatibleCounts = GetMostCompatibleParticipantCounts(selectedMap, maximumPlayers);
            var participantCount = compatibleCounts[_random.Next(compatibleCounts.Count)];
            var validModes = GetCompatibleModes(selectedMap, participantCount);
            var selectedMode = validModes[_random.Next(validModes.Count)];

            Shuffle(eligibleHumans);
            Shuffle(eligibleBots);
            var participants = new List<MatchParticipant>(participantCount);
            var humanCount = Math.Min(eligibleHumans.Count, participantCount);
            for (var index = 0; index < humanCount; index++)
            {
                participants.Add(new MatchParticipant(eligibleHumans[index].PlayerId, false));
            }

            var requiredBots = participantCount - participants.Count;
            for (var index = 0; index < requiredBots; index++)
            {
                participants.Add(new MatchParticipant(eligibleBots[index].PlayerId, true));
            }

            assignment = new MatchAssignment(
                Guid.NewGuid().ToString("N"),
                selectedMap.MapId,
                participants,
                startsAtUtc,
                selectedMode);
            return true;
        }

        private static bool HasValidMode(ArenaMapRule map, int eligibleCount)
        {
            var maximumPlayers = Math.Min(map.MaximumPlayers, eligibleCount);
            for (var participantCount = map.MinimumPlayers;
                 participantCount <= maximumPlayers;
                 participantCount++)
            {
                for (var modeIndex = 0; modeIndex < map.SupportedModes.Count; modeIndex++)
                {
                    if (MatchModeRules.SupportsParticipantCount(
                            map.SupportedModes[modeIndex],
                            participantCount))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<int> GetMostCompatibleParticipantCounts(
            ArenaMapRule map,
            int maximumPlayers)
        {
            var result = new List<int>();
            var bestModeCount = 0;
            for (var participantCount = map.MinimumPlayers;
                 participantCount <= maximumPlayers;
                 participantCount++)
            {
                var compatibleModeCount = 0;
                for (var modeIndex = 0; modeIndex < map.SupportedModes.Count; modeIndex++)
                {
                    if (MatchModeRules.SupportsParticipantCount(
                            map.SupportedModes[modeIndex],
                            participantCount))
                    {
                        compatibleModeCount++;
                    }
                }

                if (compatibleModeCount < bestModeCount)
                {
                    continue;
                }

                if (compatibleModeCount > bestModeCount)
                {
                    bestModeCount = compatibleModeCount;
                    result.Clear();
                }

                if (compatibleModeCount > 0)
                {
                    result.Add(participantCount);
                }
            }

            return result;
        }

        private static List<MatchMode> GetCompatibleModes(
            ArenaMapRule map,
            int participantCount)
        {
            var result = new List<MatchMode>(map.SupportedModes.Count);
            for (var index = 0; index < map.SupportedModes.Count; index++)
            {
                var mode = map.SupportedModes[index];
                if (MatchModeRules.SupportsParticipantCount(mode, participantCount))
                {
                    result.Add(mode);
                }
            }

            return result;
        }

        private void Shuffle<T>(IList<T> values)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var swapIndex = _random.Next(index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }
    }
}
