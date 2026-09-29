// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class AutomaticOffsetConfigTest
    {
        [TestCase("", AutomaticOffsetStyle.Off)]
        [TestCase("AutomaticallyAdjustBeatmapOffset = False", AutomaticOffsetStyle.Off)]
        [TestCase("AutomaticallyAdjustBeatmapOffset = True", AutomaticOffsetStyle.Lazer)]
        [TestCase("AutomaticallyAdjustBeatmapOffset = True\nAutomaticOffsetStyle = Off", AutomaticOffsetStyle.Off)]
        [TestCase("AutomaticallyAdjustBeatmapOffset = True\nAutomaticOffsetStyle = Beatoraja", AutomaticOffsetStyle.Beatoraja)]
        [TestCase("AutomaticOffsetStyle = Lazer", AutomaticOffsetStyle.Lazer)]
        [TestCase("AutomaticOffsetStyle = 123", AutomaticOffsetStyle.Off)]
        public void TestMigrationAndReload(string contents, AutomaticOffsetStyle expected)
        {
            using var storage = new TemporaryNativeStorage("automatic-offset-config");
            using (var writer = new StreamWriter(storage.GetStream("game.ini", FileAccess.Write, FileMode.Create)))
                writer.Write(contents);

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<AutomaticOffsetStyle>(OsuSetting.AutomaticOffsetStyle), Is.EqualTo(expected));
                Assert.That(config.GetBindable<AutomaticOffsetStyle>(OsuSetting.AutomaticOffsetStyle).Default, Is.EqualTo(AutomaticOffsetStyle.Off));
                config.Save();
            }

            using var reloaded = new OsuConfigManager(storage);
            Assert.That(reloaded.Get<AutomaticOffsetStyle>(OsuSetting.AutomaticOffsetStyle), Is.EqualTo(expected));
        }

        [Test]
        public void TestSwitchingOffDoesNotRestoreLegacyAutomaticAdjustment()
        {
            using var storage = new TemporaryNativeStorage("automatic-offset-switch");
            using (var writer = new StreamWriter(storage.GetStream("game.ini", FileAccess.Write, FileMode.Create)))
                writer.Write("AutomaticallyAdjustBeatmapOffset = True");

            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.AutomaticOffsetStyle, AutomaticOffsetStyle.Beatoraja);
                config.SetValue(OsuSetting.AutomaticOffsetStyle, AutomaticOffsetStyle.Off);
                config.Save();
            }

            using var reloaded = new OsuConfigManager(storage);
            Assert.That(reloaded.Get<AutomaticOffsetStyle>(OsuSetting.AutomaticOffsetStyle), Is.EqualTo(AutomaticOffsetStyle.Off));
        }

        [TestCase(-600, -500)]
        [TestCase(600, 500)]
        [TestCase(12.2, 12)]
        public void TestVisualOffsetBoundsAndPrecision(double requested, double expected)
        {
            using var storage = new TemporaryNativeStorage("automatic-offset-bounds");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.BmsVisualOffset, requested);
            Assert.That(config.Get<double>(OsuSetting.BmsVisualOffset), Is.EqualTo(expected));
        }
    }
}
