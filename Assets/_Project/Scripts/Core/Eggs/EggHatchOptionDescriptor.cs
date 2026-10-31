using System;
using PawArena.Core.Economy;

namespace PawArena.Core.Eggs
{
    /// <summary>Defines one authoritative way to pay for an egg hatch.</summary>
    public sealed class EggHatchOptionDescriptor
    {
        public EggHatchOptionDescriptor(
            string optionId,
            int quantity,
            CurrencyType currency,
            long price)
        {
            if (string.IsNullOrWhiteSpace(optionId))
            {
                throw new ArgumentException("Option ID is required.", nameof(optionId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            if (price <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(price));
            }

            OptionId = optionId;
            Quantity = quantity;
            Currency = currency;
            Price = price;
        }

        public string OptionId { get; }

        public int Quantity { get; }

        public CurrencyType Currency { get; }

        public long Price { get; }
    }
}
