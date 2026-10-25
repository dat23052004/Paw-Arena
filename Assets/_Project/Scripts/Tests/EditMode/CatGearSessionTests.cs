using NUnit.Framework;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Tests.EditMode.Fakes;

namespace PawArena.Tests.EditMode
{
    public sealed class CatGearSessionTests
    {
        private static ItemDescriptor Gear(string id, EquipmentSlotId slot, UnlockType unlock = UnlockType.Default) =>
            new(id, id, ItemCategory.Gear, ItemRarity.Common, unlock, 0, new[] { slot });
        private static TestItemCatalog Catalog() => new(
            new ItemDescriptor("skin", "skin", ItemCategory.Fur, ItemRarity.Common, UnlockType.Default, 0, new[] { SkinInventorySession.SkinSlot }),
            Gear("band", CatGearSlots.Head), Gear("hair", CatGearSlots.Head), Gear("belt", CatGearSlots.Waist),
            Gear("rope", CatGearSlots.Waist), Gear("fan", CatGearSlots.Hand), Gear("locked", CatGearSlots.Waist, UnlockType.Grant));

        [Test]
        public void Equip_AllThreeSlots_PreservesSkinAndReplacesOnlyMatchingSlot()
        {
            var catalog = Catalog();
            var profile = new ProfileService("test", new MemoryProfileStore(), catalog, new FakeClock());
            using var session = new SkinInventorySession(profile, catalog, true);
            foreach (var id in new[] { "skin", "band", "belt", "fan", "hair" }) { session.Select(id); Assert.That(session.EquipSelected(), Is.EqualTo(InventoryOperationResult.Success)); }
            foreach (var id in new[] { "skin", "hair", "belt", "fan" }) { Assert.That(profile.Inventory.IsEquipped(id), Is.True, id); }
            Assert.That(profile.Inventory.IsEquipped("band"), Is.False);
        }

        [Test]
        public void SelectGear_DoesNotEquipAndRejectsUnownedOrWrongSlot()
        {
            var catalog = Catalog();
            var profile = new ProfileService("test", new MemoryProfileStore(), catalog, new FakeClock());
            using var session = new SkinInventorySession(profile, catalog, true);
            session.Select("belt");
            Assert.That(profile.Inventory.IsEquipped("belt"), Is.False);
            session.Select("locked");
            Assert.That(session.EquipSelected(), Is.EqualTo(InventoryOperationResult.NotOwned));
            Assert.That(profile.Inventory.TryEquip("fan", CatGearSlots.Head), Is.EqualTo(InventoryOperationResult.InvalidSlot));
        }

        [Test]
        public void Unequip_SelectedGear_ClearsOnlyThatSlotAndSaves()
        {
            var catalog = Catalog(); var store = new MemoryProfileStore();
            var profile = new ProfileService("test", store, catalog, new FakeClock());
            using var session = new SkinInventorySession(profile, catalog, true);
            foreach (var id in new[] { "skin", "band", "belt", "fan" }) { session.Select(id); session.EquipSelected(); }
            session.Select("belt");
            Assert.That(session.UnequipSelected(), Is.EqualTo(InventoryOperationResult.Success));
            var restored = new ProfileService("test", store, catalog, new FakeClock());
            Assert.That(restored.Inventory.IsEquipped("belt"), Is.False);
            foreach (var id in new[] { "skin", "band", "fan" }) { Assert.That(restored.Inventory.IsEquipped(id), Is.True); }
        }

        [Test]
        public void Unequip_UnequippedSelection_DoesNotRemoveOtherItemInSlot()
        {
            var catalog = Catalog();
            var profile = new ProfileService("test", new MemoryProfileStore(), catalog, new FakeClock());
            using var session = new SkinInventorySession(profile, catalog, true);
            session.Select("belt"); session.EquipSelected(); session.Select("rope");
            Assert.That(session.UnequipSelected(), Is.EqualTo(InventoryOperationResult.NotEquipped));
            Assert.That(profile.Inventory.IsEquipped("belt"), Is.True);
            session.Select("skin");
            Assert.That(session.UnequipSelected(), Is.EqualTo(InventoryOperationResult.InvalidSlot));
        }

        [Test]
        public void Save_Reload_RestoresAllThreeGearSlotsAndSkin()
        {
            var catalog = Catalog(); var store = new MemoryProfileStore();
            var profile = new ProfileService("test", store, catalog, new FakeClock());
            using var session = new SkinInventorySession(profile, catalog, true);
            foreach (var id in new[] { "skin", "hair", "rope", "fan" }) { session.Select(id); session.EquipSelected(); }
            var restored = new ProfileService("test", store, catalog, new FakeClock());
            foreach (var id in new[] { "skin", "hair", "rope", "fan" }) { Assert.That(restored.Inventory.IsEquipped(id), Is.True, id); }
        }
    }
}
