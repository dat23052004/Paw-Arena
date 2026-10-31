using System;
using System.Collections.Generic;
using PawArena.Core.Inventory;

namespace PawArena.Core.Eggs
{
    /// <summary>Applies egg-specific multipliers to the shared rarity refund table.</summary>
    public sealed class EggRefundPolicy : IEggRefundPolicy
    {
        private readonly Dictionary<ItemRarity, long> _baseCoins;

        public EggRefundPolicy(IReadOnlyDictionary<ItemRarity, long> baseCoins)
        {
            if (baseCoins == null)
            {
                throw new ArgumentNullException(nameof(baseCoins));
            }

            _baseCoins = new Dictionary<ItemRarity, long>();
            foreach (var entry in baseCoins)
            {
                if (entry.Value < 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(baseCoins));
                }

                _baseCoins[entry.Key] = entry.Value;
            }
        }

        public long CalculateCoins(ItemRarity rarity, int eggMultiplierBasisPoints)
        {
            if (eggMultiplierBasisPoints <= 0 || !_baseCoins.TryGetValue(rarity, out var baseValue))
            {
                return 0L;
            }

            return checked((baseValue * eggMultiplierBasisPoints + 5000L) / 10000L);
        }

        public static EggRefundPolicy CreateDefault()
        {
            return new EggRefundPolicy(new Dictionary<ItemRarity, long>
            {
                [ItemRarity.Common] = 25L,
                [ItemRarity.Rare] = 61L,
                [ItemRarity.Epic] = 93L,
                [ItemRarity.Legendary] = 180L,
                [ItemRarity.Mythic] = 400L
            });
        }
    }
}
