// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ManiaHitsoundVolumeConfigTest
    {
        [TestCase("", 0.37)]
        [TestCase("ManiaHitsoundVolume = 0.6", 0.6)]
        [TestCase("ManiaHitsoundVolume = 0", 0)]
        public void TestPreviousEffectVolumeIsUsedOnlyForMissingSetting(string existingConfig, double expected)
        {
            using var storage = new TemporaryNativeStorage("mania-hitsound-volume-config");
            using (var writer = new StreamWriter(storage.GetStream("game.ini", FileAccess.Write, FileMode.Create)))
                writer.Write(existingConfig);

            using (var config = new OsuConfigManager(storage))
            {
                config.MigrateManiaHitsoundVolume(0.37);
                Assert.That(config.Get<double>(OsuSetting.ManiaHitsoundVolume), Is.EqualTo(expected));
                config.Save();
            }

            using var reloaded = new OsuConfigManager(storage);
            reloaded.MigrateManiaHitsoundVolume(0.85);
            Assert.That(reloaded.Get<double>(OsuSetting.ManiaHitsoundVolume), Is.EqualTo(expected));
        }
    }
}
