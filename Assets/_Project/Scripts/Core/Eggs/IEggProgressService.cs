using System;

namespace PawArena.Core.Eggs
{
    /// <summary>Owns hatch counters and sequential tier unlock state.</summary>
    public interface IEggProgressService
    {
        event Action<string, string, int> TierProgressChanged;

        event Action<string, string> TierUnlocked;

        int GetHatchCount(string eggId, string tierId);

        bool IsTierUnlocked(EggDescriptor egg, EggTierDescriptor tier);

        EggTierDescriptor GetHighestUnlockedTier(EggDescriptor egg);

        string RecordSuccessfulHatches(EggDescriptor egg, EggTierDescriptor tier, int count);

        EggProgressSnapshot CaptureSnapshot();
    }
}
