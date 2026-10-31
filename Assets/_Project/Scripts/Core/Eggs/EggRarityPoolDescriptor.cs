using System;
using System.Collections.Generic;
using PawArena.Core.Inventory;

namespace PawArena.Core.Eggs
{
    /// <summary>Defines the rarity chance and the uniformly selected items inside it.</summary>
    public sealed class EggRarityPoolDescriptor
    {
        private readonly string[] _itemIds;

        public EggRarityPoolDescriptor(
            ItemRarity rarity,
            int chanceBasisPoints,
            IReadOnlyList<string> itemIds)
        {
            if (chanceBasisPoints <= 0 || chanceBasisPoints > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(chanceBasisPoints));
            }

            if (itemIds == null || itemIds.Count == 0)
            {
                throw new ArgumentException("A rarity pool needs at least one item.", nameof(itemIds));
            }

            Rarity = rarity;
            ChanceBasisPoints = chanceBasisPoints;
            _itemIds = new string[itemIds.Count];
            for (var i = 0; i < itemIds.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(itemIds[i]))
                {
                    throw new ArgumentException("Pool item IDs cannot be empty.", nameof(itemIds));
                }

                _itemIds[i] = itemIds[i];
            }
        }

        public ItemRarity Rarity { get; }

        public int ChanceBasisPoints { get; }

        public IReadOnlyList<string> ItemIds => _itemIds;
    }
}
