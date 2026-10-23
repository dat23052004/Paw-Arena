using NUnit.Framework;
using PawArena.Core.Inventory;
using PawArena.Tests.EditMode.Fakes;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies inventory ownership and equipment invariants.
    /// </summary>
    public sealed class InventoryServiceTests
    {
        private static readonly EquipmentSlotId FurSlot = new(EquipmentSlotType.Fur);

        [Test]
        public void TryEquip_WhenItemIsNotOwned_IsRejected()
        {
            var inventory = CreateInventory();

            var result = inventory.TryEquip("fur_ginger", FurSlot);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.NotOwned));
        }

        [Test]
        public void TryEquip_WhenOwned_ReplacesPreviousItemInSlot()
        {
            var inventory = CreateInventory();
            inventory.TryGrant("fur_ginger");
            inventory.TryGrant("fur_grey");
            inventory.TryEquip("fur_ginger", FurSlot);

            var result = inventory.TryEquip("fur_grey", FurSlot);

            Assert.That(result, Is.EqualTo(InventoryOperationResult.Success));
            Assert.That(inventory.TryGetEquipped(FurSlot, out var itemId), Is.True);
            Assert.That(itemId, Is.EqualTo("fur_grey"));
        }

        private static InventoryService CreateInventory()
        {
            var ginger = CreateFur("fur_ginger");
            var grey = CreateFur("fur_grey");
            return new InventoryService(new TestItemCatalog(ginger, grey));
        }

        private static ItemDescriptor CreateFur(string itemId)
        {
            return new ItemDescriptor(
                itemId,
                itemId,
                ItemCategory.Fur,
                ItemRarity.Common,
                UnlockType.Coins,
                250L,
                new[] { FurSlot });
        }
    }
}
