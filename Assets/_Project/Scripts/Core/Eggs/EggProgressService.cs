using System;
using System.Collections.Generic;

namespace PawArena.Core.Eggs
{
    /// <summary>Player-scoped tier progress independent from item ownership.</summary>
    public sealed class EggProgressService : IEggProgressService
    {
        private sealed class RuntimeEntry
        {
            public int HighestUnlockedTierOrder = 1;
            public readonly Dictionary<string, int> HatchCounts = new(StringComparer.Ordinal);
        }

        private readonly Dictionary<string, RuntimeEntry> _entries = new(StringComparer.Ordinal);

        public EggProgressService(EggProgressSnapshot snapshot = null)
        {
            if (snapshot?.Eggs == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.Eggs.Count; i++)
            {
                var source = snapshot.Eggs[i];
                if (source == null || string.IsNullOrWhiteSpace(source.EggId))
                {
                    continue;
                }

                var entry = new RuntimeEntry
                {
                    HighestUnlockedTierOrder = Math.Max(1, source.HighestUnlockedTierOrder)
                };

                if (source.Tiers != null)
                {
                    for (var tierIndex = 0; tierIndex < source.Tiers.Count; tierIndex++)
                    {
                        var tier = source.Tiers[tierIndex];
                        if (tier != null && !string.IsNullOrWhiteSpace(tier.TierId))
                        {
                            entry.HatchCounts[tier.TierId] = Math.Max(0, tier.TotalHatches);
                        }
                    }
                }

                _entries[source.EggId] = entry;
            }
        }

        public event Action<string, string, int> TierProgressChanged;

        public event Action<string, string> TierUnlocked;

        public int GetHatchCount(string eggId, string tierId)
        {
            if (!_entries.TryGetValue(eggId, out var entry)
                || !entry.HatchCounts.TryGetValue(tierId, out var count))
            {
                return 0;
            }

            return count;
        }

        public bool IsTierUnlocked(EggDescriptor egg, EggTierDescriptor tier)
        {
            if (egg == null || tier == null)
            {
                return false;
            }

            var entry = GetOrCreateEntry(egg.EggId);
            return tier.Order <= entry.HighestUnlockedTierOrder;
        }

        public EggTierDescriptor GetHighestUnlockedTier(EggDescriptor egg)
        {
            if (egg == null)
            {
                throw new ArgumentNullException(nameof(egg));
            }

            var entry = GetOrCreateEntry(egg.EggId);
            var selected = egg.Tiers[0];
            for (var i = 1; i < egg.Tiers.Count; i++)
            {
                if (egg.Tiers[i].Order > entry.HighestUnlockedTierOrder)
                {
                    break;
                }

                selected = egg.Tiers[i];
            }

            return selected;
        }

        public string RecordSuccessfulHatches(
            EggDescriptor egg,
            EggTierDescriptor tier,
            int count)
        {
            if (egg == null)
            {
                throw new ArgumentNullException(nameof(egg));
            }

            if (tier == null)
            {
                throw new ArgumentNullException(nameof(tier));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var entry = GetOrCreateEntry(egg.EggId);
            entry.HatchCounts.TryGetValue(tier.TierId, out var previousCount);
            var nextCount = previousCount > int.MaxValue - count
                ? int.MaxValue
                : previousCount + count;
            entry.HatchCounts[tier.TierId] = nextCount;
            TierProgressChanged?.Invoke(egg.EggId, tier.TierId, nextCount);

            EggTierDescriptor nextTier = null;
            for (var i = 0; i < egg.Tiers.Count; i++)
            {
                if (egg.Tiers[i].Order == tier.Order + 1)
                {
                    nextTier = egg.Tiers[i];
                    break;
                }
            }

            if (nextTier == null
                || entry.HighestUnlockedTierOrder >= nextTier.Order
                || nextCount < nextTier.RequiredPreviousTierHatches)
            {
                return null;
            }

            entry.HighestUnlockedTierOrder = nextTier.Order;
            TierUnlocked?.Invoke(egg.EggId, nextTier.TierId);
            return nextTier.TierId;
        }

        public EggProgressSnapshot CaptureSnapshot()
        {
            var snapshot = new EggProgressSnapshot();
            foreach (var eggEntry in _entries)
            {
                var data = new EggProgressEntryData
                {
                    EggId = eggEntry.Key,
                    HighestUnlockedTierOrder = eggEntry.Value.HighestUnlockedTierOrder
                };

                foreach (var tierEntry in eggEntry.Value.HatchCounts)
                {
                    data.Tiers.Add(new EggTierProgressData
                    {
                        TierId = tierEntry.Key,
                        TotalHatches = tierEntry.Value
                    });
                }

                snapshot.Eggs.Add(data);
            }

            return snapshot;
        }

        private RuntimeEntry GetOrCreateEntry(string eggId)
        {
            if (!_entries.TryGetValue(eggId, out var entry))
            {
                entry = new RuntimeEntry();
                _entries.Add(eggId, entry);
            }

            return entry;
        }
    }
}
