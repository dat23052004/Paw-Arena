using System;
using System.Collections.Generic;

namespace PawArena.Core.Eggs
{
    /// <summary>Serializable player progress for all tiered eggs.</summary>
    [Serializable]
    public sealed class EggProgressSnapshot
    {
        public int Version = 1;
        public List<EggProgressEntryData> Eggs = new();
    }

    [Serializable]
    public sealed class EggProgressEntryData
    {
        public string EggId;
        public int HighestUnlockedTierOrder = 1;
        public List<EggTierProgressData> Tiers = new();
    }

    [Serializable]
    public sealed class EggTierProgressData
    {
        public string TierId;
        public int TotalHatches;
    }
}
