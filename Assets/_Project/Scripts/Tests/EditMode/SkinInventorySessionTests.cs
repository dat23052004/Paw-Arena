using System;
using System.IO;
using NUnit.Framework;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Tests.EditMode.Fakes;

namespace PawArena.Tests.EditMode
{
    /// <summary>Regression coverage for selection, ownership, local persistence and event lifetime.</summary>
    public sealed class SkinInventorySessionTests
    {
        private static ItemDescriptor Skin(string id, UnlockType unlock = UnlockType.Default) =>
            new(id, id, ItemCategory.Fur, ItemRarity.Common, unlock, 0, new[] { SkinInventorySession.SkinSlot });

        [Test]
        public void Select_OwnedSkin_DoesNotEquipOrSave()
        {
            var catalog = new TestItemCatalog(Skin("ginger"), Skin("grey"));
            var store = new MemoryProfileStore();
            var profile = new ProfileService("test", store, catalog, new FakeClock());
            profile.Inventory.TryEquip("ginger", SkinInventorySession.SkinSlot);
            using var session = new SkinInventorySession(profile, catalog);
            Assert.That(session.Select("grey"), Is.True);
            Assert.That(profile.Inventory.IsEquipped("ginger"), Is.True);
            Assert.That(store.TryLoad("test", out _), Is.False);
        }

        [Test]
        public void EquipSelected_UnownedSkin_RejectsAndPreservesEquipment()
        {
            var catalog = new TestItemCatalog(Skin("ginger"), Skin("gold", UnlockType.Grant));
            var profile = new ProfileService("test", new MemoryProfileStore(), catalog, new FakeClock());
            profile.Inventory.TryEquip("ginger", SkinInventorySession.SkinSlot);
            using var session = new SkinInventorySession(profile, catalog);
            session.Select("gold");
            Assert.That(session.EquipSelected(), Is.EqualTo(InventoryOperationResult.NotOwned));
            Assert.That(profile.Inventory.IsEquipped("ginger"), Is.True);
        }

        [Test]
        public void EquipSelected_OwnedSkin_RestoresAfterNewProfileInstance()
        {
            var catalog = new TestItemCatalog(Skin("ginger"), Skin("grey", UnlockType.Grant));
            var store = new MemoryProfileStore();
            var profile = new ProfileService("test", store, catalog, new FakeClock());
            profile.Inventory.TryGrant("grey");
            using var session = new SkinInventorySession(profile, catalog);
            session.Select("grey");
            Assert.That(session.EquipSelected(), Is.EqualTo(InventoryOperationResult.Success));
            var restored = new ProfileService("test", store, catalog, new FakeClock());
            Assert.That(restored.Inventory.IsOwned("grey"), Is.True);
            Assert.That(restored.Inventory.IsEquipped("grey"), Is.True);
            using var restoredSession = new SkinInventorySession(restored, catalog);
            Assert.That(restoredSession.SelectedItemId, Is.EqualTo("grey"));
        }

        [Test]
        public void EquipSelected_SaveFails_ReportsErrorAndSupportsRetry()
        {
            var catalog = new TestItemCatalog(Skin("ginger"));
            var store = new FailingStore();
            var profile = new ProfileService("test", store, catalog, new FakeClock());
            using var session = new SkinInventorySession(profile, catalog);
            session.EquipSelected();
            Assert.That(session.HasSaveError, Is.True);
            Assert.That(profile.Inventory.IsEquipped("ginger"), Is.True);
            store.Fail = false;
            Assert.That(session.Save(), Is.True);
            Assert.That(session.HasSaveError, Is.False);
        }

        [Test]
        public void Select_UnknownId_KeepsPreviousSelection()
        {
            var catalog = new TestItemCatalog(Skin("ginger"));
            using var session = new SkinInventorySession(new ProfileService("test", new MemoryProfileStore(), catalog, new FakeClock()), catalog);
            Assert.That(session.Select("missing"), Is.False);
            Assert.That(session.SelectedItemId, Is.EqualTo("ginger"));
        }

        [Test]
        public void Dispose_InventoryChanges_NoLongerNotifiesView()
        {
            var catalog = new TestItemCatalog(Skin("ginger"));
            var profile = new ProfileService("test", new MemoryProfileStore(), catalog, new FakeClock());
            var session = new SkinInventorySession(profile, catalog);
            var calls = 0;
            session.Changed += () => calls++;
            session.Dispose();
            profile.Inventory.TryEquip("ginger", SkinInventorySession.SkinSlot);
            Assert.That(calls, Is.Zero);
        }

        private sealed class FailingStore : IProfileStore
        {
            public bool Fail = true;
            public bool TryLoad(string playerId, out PlayerProfileSnapshot snapshot) { snapshot = null; return false; }
            public void Save(PlayerProfileSnapshot snapshot) { if (Fail) { throw new IOException("Simulated disk failure"); } }
            public bool Delete(string playerId) => false;
        }
    }
}
