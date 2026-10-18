// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class SkinSelectionPreferencesTest
    {
        [Test]
        public void TestExistingModeChoicesBecomeDefaultsWithoutChangingLegacyConfiguration()
        {
            using var storage = new TemporaryNativeStorage("skin-scope-migration");
            using var config = new OsuConfigManager(storage);
            Guid bms = Guid.NewGuid(), mania = Guid.NewGuid();
            config.SetValue(OsuSetting.SkinBms, bms.ToString());
            config.SetValue(OsuSetting.SkinMania, mania.ToString());
            string legacy = config.Get<string>(OsuSetting.Skin);
            var preferences = new SkinSelectionPreferences(config);

            Assert.Multiple(() =>
            {
                Assert.That(preferences.GetEffectiveSkinId(new SkinSelectionScope("bms", 7)), Is.EqualTo(bms));
                Assert.That(preferences.GetEffectiveSkinId(new SkinSelectionScope("mania", 4)), Is.EqualTo(mania));
                Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("bms", 7)), Is.Null);
                Assert.That(config.Get<string>(OsuSetting.Skin), Is.EqualTo(legacy));
                Assert.That(config.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo("{}"));
            });
        }

        [Test]
        public void TestSparseAssignmentsPersistAndResetOnlyTheChosenKeyCount()
        {
            using var storage = new TemporaryNativeStorage("skin-scope-persist");
            Guid modeDefault = Guid.NewGuid(), explicitSeven = Guid.NewGuid(), mania = Guid.NewGuid();
            using (var config = new OsuConfigManager(storage))
            {
                var preferences = new SkinSelectionPreferences(config);
                preferences.SetSkin(new SkinSelectionScope("bms"), modeDefault);
                preferences.SetSkin(new SkinSelectionScope("bms", 7), explicitSeven);
                preferences.SetSkin(new SkinSelectionScope("mania", 7), mania);
                config.Save();
            }

            using var reloaded = new OsuConfigManager(storage);
            var restored = new SkinSelectionPreferences(reloaded);
            Assert.Multiple(() =>
            {
                Assert.That(restored.GetEffectiveSkinId(new SkinSelectionScope("bms", 5)), Is.EqualTo(modeDefault));
                Assert.That(restored.GetEffectiveSkinId(new SkinSelectionScope("bms", 7)), Is.EqualTo(explicitSeven));
                Assert.That(restored.GetUsages(explicitSeven), Is.EqualTo(new[] { new SkinSelectionScope("bms", 7) }));
            });
            restored.Reset(new SkinSelectionScope("bms", 7));
            Assert.Multiple(() =>
            {
                Assert.That(restored.GetConfiguredSkinId(new SkinSelectionScope("bms", 7)), Is.Null);
                Assert.That(restored.GetEffectiveSkinId(new SkinSelectionScope("bms", 7)), Is.EqualTo(modeDefault));
                Assert.That(restored.GetEffectiveSkinId(new SkinSelectionScope("mania", 7)), Is.EqualTo(mania));
            });
        }

        [Test]
        public void TestChangingDefaultLeavesExplicitChoiceEvenWhenItOriginallyMatchedDefault()
        {
            using var storage = new TemporaryNativeStorage("skin-scope-defaults");
            using var config = new OsuConfigManager(storage);
            var preferences = new SkinSelectionPreferences(config);
            Guid first = Guid.NewGuid(), next = Guid.NewGuid();
            preferences.SetSkin(new SkinSelectionScope("mania"), first);
            preferences.SetSkin(new SkinSelectionScope("mania", 7), first);
            preferences.SetSkin(new SkinSelectionScope("mania"), next);

            Assert.Multiple(() =>
            {
                Assert.That(preferences.GetEffectiveSkinId(new SkinSelectionScope("mania", 4)), Is.EqualTo(next));
                Assert.That(preferences.GetEffectiveSkinId(new SkinSelectionScope("mania", 7)), Is.EqualTo(first));
            });
        }

        [TestCase("not-json")]
        [TestCase("{\"bms\":{\"6\":\"00000000-0000-0000-0000-000000000000\"}}")]
        [TestCase("{\"mania\":{\"11\":\"00000000-0000-0000-0000-000000000000\"}}")]
        [TestCase("{\"bms\":{},\"bms\":{}}")]
        [TestCase("{\"mania\":{\"7\":true}}")]
        public void TestMalformedKeyAssignmentsPreserveRawSettingsAndFollowModeDefault(string raw)
        {
            using var storage = new TemporaryNativeStorage("skin-scope-invalid");
            using var config = new OsuConfigManager(storage);
            Guid mode = Guid.NewGuid();
            config.SetValue(OsuSetting.SkinBms, mode.ToString());
            config.SetValue(OsuSetting.SkinKeyOverrides, raw);
            var preferences = new SkinSelectionPreferences(config);
            Assert.Multiple(() =>
            {
                Assert.That(preferences.GetEffectiveSkinId(new SkinSelectionScope("bms", 7)), Is.EqualTo(mode));
                Assert.That(config.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo(raw));
            });
        }

        [Test]
        public void TestSupportedCountsIncludeManiaDualStagesAndExcludeNonPlayableOddCounts()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SkinSelectionScope.GetSupportedKeyCounts("bms"), Is.EqualTo(new[] { 5, 7, 9, 14 }));
                Assert.That(SkinSelectionScope.GetSupportedKeyCounts("mania"), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20 }));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SkinSelectionScope("mania", 11));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SkinSelectionScope("bms", 8));
            });
        }
    }
}
