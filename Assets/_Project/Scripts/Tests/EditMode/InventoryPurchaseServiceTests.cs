using NUnit.Framework;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;
using PawArena.Tests.EditMode.Fakes;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies the coin purchase coordinator across wallet and inventory services.
    /// </summary>
    public sealed class InventoryPurchaseServiceTests
    {
        [Test]
        public void TryPurchase_WhenFundsAreAvailable_DebitsAndGrantsItem()
        {
            var item = new ItemDescriptor(
                "ability_hiss",
                "Hiss",
                ItemCategory.Ability,
                ItemRarity.Common,
                UnlockType.Coins,
                750L,
                new[] { new EquipmentSlotId(EquipmentSlotType.Ability) });
            var catalog = new TestItemCatalog(item);
            var inventory = new InventoryService(catalog);
            var wallet = new WalletService(new FakeClock());
            wallet.TryApply(
                "seed-coins",
                CurrencyType.Coins,
                1000L,
                "test_seed",
                string.Empty,
                out _);
            var purchases = new InventoryPurchaseService(catalog, inventory, wallet);

            var result = purchases.TryPurchase("purchase-hiss", item.ItemId);

            Assert.That(result, Is.EqualTo(InventoryPurchaseResult.Success));
            Assert.That(wallet.GetBalance(CurrencyType.Coins), Is.EqualTo(250L));
            Assert.That(inventory.IsOwned(item.ItemId), Is.True);
        }
    }
}
