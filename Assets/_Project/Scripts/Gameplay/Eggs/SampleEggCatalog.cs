using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Eggs;
using PawArena.Core.Inventory;

namespace PawArena.Gameplay.Eggs
{
    /// <summary>
    /// Immediately usable Hallow, Alien and Emote examples. Replace it by assigning an EggCatalog asset.
    /// </summary>
    public sealed class SampleEggCatalog : IEggCatalog
    {
        private readonly List<EggDescriptor> _eggs;
        private readonly Dictionary<string, EggDescriptor> _eggById;

        private SampleEggCatalog(List<EggDescriptor> eggs)
        {
            _eggs = eggs;
            _eggById = new Dictionary<string, EggDescriptor>(StringComparer.Ordinal);
            for (var i = 0; i < eggs.Count; i++)
            {
                _eggById.Add(eggs[i].EggId, eggs[i]);
            }
        }

        public IReadOnlyList<EggDescriptor> Eggs => _eggs;

        public bool TryGet(string eggId, out EggDescriptor egg)
        {
            egg = null;
            return !string.IsNullOrWhiteSpace(eggId) && _eggById.TryGetValue(eggId, out egg);
        }

        public static SampleEggCatalog Create()
        {
            // Fixed event deadline: reopening the game must never reset limited-time availability.
            var demoEnd = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
            return new SampleEggCatalog(new List<EggDescriptor>
            {
                BuildHallowEgg(demoEnd),
                BuildAlienEgg(demoEnd),
                BuildEmoteEgg()
            });
        }

        private static EggDescriptor BuildHallowEgg(DateTimeOffset demoEnd)
        {
            return new EggDescriptor(
                "halloween-egg",
                "Hallow Egg",
                ItemCategory.Fur,
                true,
                null,
                demoEnd,
                10000,
                new[]
                {
                    new EggTierDescriptor(
                        "default",
                        "Hallow Egg",
                        1,
                        0,
                        CreateOptions(200L, 13L, 35L, 99L),
                        new[]
                        {
                            Pool(ItemRarity.Rare, 6750, "fur_pumpkin", "fur_mummy"),
                            Pool(ItemRarity.Epic, 3000, "fur_black_widow", "fur_hologram", "fur_frankencat", "fur_skeleton"),
                            Pool(ItemRarity.Legendary, 200, "fur_snow_leopard"),
                            Pool(ItemRarity.Mythic, 50, "fur_ninja")
                        })
                });
        }

        private static EggDescriptor BuildEmoteEgg()
        {
            return new EggDescriptor(
                "emote-egg",
                "Emote Egg",
                ItemCategory.Emote,
                false,
                null,
                null,
                9000,
                new[]
                {
                    new EggTierDescriptor(
                        "default",
                        "Emote Egg",
                        1,
                        0,
                        CreateOptions(200L, 9L, 25L, 79L),
                        new[]
                        {
                            Pool(ItemRarity.Common, 8900, "emote_wave", "emote_stretch", "emote_lick_paw"),
                            Pool(ItemRarity.Rare, 850, "emote_nap_attack", "emote_tea_ceremony"),
                            Pool(ItemRarity.Legendary, 220, "emote_beam"),
                            Pool(ItemRarity.Mythic, 30, "emote_nine_lives")
                        })
                });
        }

        private static EggDescriptor BuildAlienEgg(DateTimeOffset demoEnd)
        {
            return new EggDescriptor(
                "alien-egg",
                "Alien Egg",
                ItemCategory.Fur,
                true,
                null,
                demoEnd,
                14000,
                new[]
                {
                    new EggTierDescriptor(
                        "tier-1",
                        "Tier 1",
                        1,
                        0,
                        CreateOptions(100L, 5L, 13L, 39L),
                        new[]
                        {
                            Pool(ItemRarity.Rare, 8400, "fur_pumpkin", "fur_mummy", "fur_tiger", "fur_siamese"),
                            Pool(ItemRarity.Epic, 1600, "fur_black_widow", "fur_hologram")
                        }),
                    new EggTierDescriptor(
                        "tier-2",
                        "Tier 2",
                        2,
                        30,
                        CreateOptions(200L, 9L, 25L, 79L),
                        new[]
                        {
                            Pool(ItemRarity.Epic, 10000, "fur_frankencat", "fur_skeleton")
                        }),
                    new EggTierDescriptor(
                        "tier-3",
                        "Tier 3",
                        3,
                        30,
                        CreateOptions(350L, 13L, 35L, 109L),
                        new[]
                        {
                            Pool(ItemRarity.Legendary, 10000, "fur_snow_leopard")
                        }),
                    new EggTierDescriptor(
                        "tier-4",
                        "Tier 4",
                        4,
                        30,
                        CreateOptions(500L, 19L, 49L, 159L),
                        new[]
                        {
                            Pool(ItemRarity.Rare, 2800, "fur_pumpkin", "fur_mummy", "fur_tiger", "fur_siamese"),
                            Pool(ItemRarity.Epic, 5500, "fur_black_widow", "fur_hologram", "fur_frankencat", "fur_skeleton"),
                            Pool(ItemRarity.Legendary, 1400, "fur_snow_leopard"),
                            Pool(ItemRarity.Mythic, 300, "fur_ninja")
                        })
                });
        }

        private static EggHatchOptionDescriptor[] CreateOptions(
            long coinPrice,
            long premiumSinglePrice,
            long premiumTriplePrice,
            long premiumTenPrice)
        {
            return new[]
            {
                new EggHatchOptionDescriptor(EggHatchOptionIds.CoinsSingle, 1, CurrencyType.Coins, coinPrice),
                new EggHatchOptionDescriptor(EggHatchOptionIds.PremiumSingle, 1, CurrencyType.Premium, premiumSinglePrice),
                new EggHatchOptionDescriptor(EggHatchOptionIds.PremiumTriple, 3, CurrencyType.Premium, premiumTriplePrice),
                new EggHatchOptionDescriptor(EggHatchOptionIds.PremiumTen, 10, CurrencyType.Premium, premiumTenPrice)
            };
        }

        private static EggRarityPoolDescriptor Pool(
            ItemRarity rarity,
            int chanceBasisPoints,
            params string[] itemIds)
        {
            return new EggRarityPoolDescriptor(rarity, chanceBasisPoints, itemIds);
        }
    }
}
