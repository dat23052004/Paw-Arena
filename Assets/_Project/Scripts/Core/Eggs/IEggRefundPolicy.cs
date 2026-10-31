using PawArena.Core.Inventory;

namespace PawArena.Core.Eggs
{
    /// <summary>Calculates the coin compensation for an already-owned reward.</summary>
    public interface IEggRefundPolicy
    {
        long CalculateCoins(ItemRarity rarity, int eggMultiplierBasisPoints);
    }
}
