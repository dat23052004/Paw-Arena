using PawArena.Core.Economy;

namespace PawArena.UI.Shop
{
    /// <summary>Identifies the user intent raised by a Shop purchase control.</summary>
    public enum ShopPurchaseKind
    {
        Hatch,
        EggPack,
        Bundle,
        CoinPack
    }

    /// <summary>Immutable purchase intent forwarded to the existing transaction layer.</summary>
    public readonly struct ShopPurchaseRequest
    {
        public ShopPurchaseRequest(
            ShopPurchaseKind kind,
            string productId,
            int quantity,
            CurrencyType currency,
            long price,
            bool isGift)
        {
            Kind = kind;
            ProductId = productId ?? string.Empty;
            TierId = string.Empty;
            HatchOptionId = string.Empty;
            Quantity = quantity;
            Currency = currency;
            Price = price;
            IsGift = isGift;
        }

        public ShopPurchaseRequest(
            string eggId,
            string tierId,
            string hatchOptionId,
            int quantity,
            CurrencyType displayCurrency,
            long displayPrice)
        {
            Kind = ShopPurchaseKind.Hatch;
            ProductId = eggId ?? string.Empty;
            TierId = tierId ?? string.Empty;
            HatchOptionId = hatchOptionId ?? string.Empty;
            Quantity = quantity;
            Currency = displayCurrency;
            Price = displayPrice;
            IsGift = false;
        }

        public ShopPurchaseKind Kind { get; }
        public string ProductId { get; }
        public string TierId { get; }
        public string HatchOptionId { get; }
        public int Quantity { get; }
        public CurrencyType Currency { get; }
        public long Price { get; }
        public bool IsGift { get; }
    }
}
