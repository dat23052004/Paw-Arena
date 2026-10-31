namespace PawArena.Core.Eggs
{
    /// <summary>Executes authoritative egg hatches.</summary>
    public interface IEggHatchService
    {
        EggHatchResult TryHatch(EggHatchRequest request);
    }
}
