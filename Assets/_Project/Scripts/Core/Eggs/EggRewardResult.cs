using PawArena.Core.Inventory;

namespace PawArena.Core.Eggs
{
    /// <summary>One resolved reward in a hatch result.</summary>
    public sealed class EggRewardResult
    {
        public EggRewardResult(string itemId, ItemRarity rarity, bool wasAlreadyOwned, long refundCoins)
        {
            ItemId = itemId;
            Rarity = rarity;
            WasAlreadyOwned = wasAlreadyOwned;
            RefundCoins = refundCoins;
        }

        public string ItemId { get; }

        public ItemRarity Rarity { get; }

        public bool WasAlreadyOwned { get; }

        public long RefundCoins { get; }
    }
}
