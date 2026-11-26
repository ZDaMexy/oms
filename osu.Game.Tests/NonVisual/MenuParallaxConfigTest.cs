// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MenuParallaxConfigTest
    {
        [TestCase("", 1)]
        [TestCase("MenuParallax = True", 1)]
        [TestCase("MenuParallax = False", 0)]
        [TestCase("MenuParallax = False\nMenuParallaxScale = 1.5", 1.5f)]
        public void TestMigrationAndReload(string contents, float expected)
        {
            using var storage = new TemporaryNativeStorage("menu-parallax-config");
            using (var writer = new StreamWriter(storage.GetStream("game.ini", FileAccess.Write, FileMode.Create)))
                writer.Write(contents);

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<float>(OsuSetting.MenuParallaxScale), Is.EqualTo(expected));
                Assert.That(config.GetBindable<float>(OsuSetting.MenuParallaxScale).Default, Is.EqualTo(1));
                config.Save();
            }

            using var reloaded = new OsuConfigManager(storage);
            Assert.That(reloaded.Get<float>(OsuSetting.MenuParallaxScale), Is.EqualTo(expected));
        }
    }
}
