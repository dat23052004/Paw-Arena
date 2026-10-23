using NUnit.Framework;
using PawArena.Core.Inventory;
using PawArena.Core.Profiles;
using PawArena.Tests.EditMode.Fakes;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies default grants and win streak rules in the profile aggregate.
    /// </summary>
    public sealed class ProfileServiceTests
    {
        [Test]
        public void Constructor_WhenCatalogHasDefaultItem_GrantsIt()
        {
            var defaultFur = new ItemDescriptor(
                "fur_ginger",
                "Ginger",
                ItemCategory.Fur,
                ItemRarity.Common,
                UnlockType.Default,
                0L,
                new[] { new EquipmentSlotId(EquipmentSlotType.Fur) });

            var profile = new ProfileService(
                "player-1",
                new MemoryProfileStore(),
                new TestItemCatalog(defaultFur),
                new FakeClock());

            Assert.That(profile.Inventory.IsOwned(defaultFur.ItemId), Is.True);
        }

        [Test]
        public void RecordMatchResult_WhenLossFollowsWins_ResetsCurrentButKeepsBestStreak()
        {
            var profile = new ProfileService(
                "player-1",
                new MemoryProfileStore(),
                new TestItemCatalog(),
                new FakeClock());
            profile.RecordMatchResult(true, 2);
            profile.RecordMatchResult(true, 1);

            profile.RecordMatchResult(false, 0);

            Assert.That(profile.Stats.CurrentWinStreak, Is.Zero);
            Assert.That(profile.Stats.BestWinStreak, Is.EqualTo(2));
            Assert.That(profile.Stats.Wins, Is.EqualTo(2));
            Assert.That(profile.Stats.Knockouts, Is.EqualTo(3));
        }
    }
}
