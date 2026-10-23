using NUnit.Framework;
using PawArena.Core.Audio;
using PawArena.Core.Settings;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies settings normalization and independent snapshots.
    /// </summary>
    public sealed class SettingsServiceTests
    {
        [Test]
        public void SetVolume_WhenOutsideRange_ClampsAndRaisesChange()
        {
            var settings = new SettingsService();
            var changedCount = 0;
            settings.Changed += _ => changedCount++;

            settings.SetVolume(AudioBus.Music, 2f);

            Assert.That(settings.Current.MusicVolume, Is.EqualTo(1f));
            Assert.That(changedCount, Is.Zero);

            settings.SetVolume(AudioBus.Music, -1f);
            Assert.That(settings.Current.MusicVolume, Is.Zero);
            Assert.That(changedCount, Is.EqualTo(1));
        }
    }
}
