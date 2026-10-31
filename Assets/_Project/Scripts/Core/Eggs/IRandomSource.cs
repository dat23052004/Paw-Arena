namespace PawArena.Core.Eggs
{
    /// <summary>Injectable random source so hatch logic remains deterministic in tests.</summary>
    public interface IRandomSource
    {
        int NextInt(int exclusiveMax);
    }
}
