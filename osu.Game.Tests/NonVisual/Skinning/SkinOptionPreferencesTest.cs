// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class SkinOptionPreferencesTest
    {
        [Test]
        public void TestCancelledUnloadedOptionsControlCanBeDisposed()
            => new SkinOptionsSettings().Dispose();

        [Test]
        public void TestEditingANewRevisionRemovesRetiredOptionIdsOnlyOnWrite()
        {
            using var storage = new TemporaryNativeStorage("skin-options-revision");
            using var config = new OsuConfigManager(storage);
            var preferences = new SkinOptionPreferences(config);
            string[] oldIds = Enumerable.Range(0, 32).Select(i => "old-" + i).ToArray();
            foreach (string id in oldIds)
                preferences.SetOverride(Guid.Empty, "bms", id, "on");
            Assert.That(preferences.GetOverrides(Guid.Empty, "bms").Count, Is.EqualTo(32));
            string[] newIds = oldIds.Skip(1).Append("replacement").ToArray();
            preferences.SetOverride(Guid.Empty, "bms", "replacement", "on", newIds);
            Assert.That(preferences.GetOverrides(Guid.Empty, "bms").Count, Is.EqualTo(32));
            Assert.That(preferences.GetOverrides(Guid.Empty, "bms").ContainsKey("old-0"), Is.False);
            Assert.That(preferences.GetOverrides(Guid.Empty, "bms")["replacement"], Is.EqualTo("on"));
        }

        [Test]
        public void TestPreferencesSurviveRestartAndResetOnlyTheChosenSkinAndMode()
        {
            using var storage = new TemporaryNativeStorage("skin-options");
            Guid first = Guid.NewGuid(), second = Guid.NewGuid();
            using (var config = new OsuConfigManager(storage))
            {
                var preferences = new SkinOptionPreferences(config);
                preferences.SetOverride(first, "bms", "layout", "p2");
                preferences.SetOverride(first, "mania", "notes", "n01");
                preferences.SetOverride(second, "bms", "layout", "p1");
                config.Save();
            }

            using var reloaded = new OsuConfigManager(storage);
            var restored = new SkinOptionPreferences(reloaded);
            Assert.That(restored.GetOverrides(first, "bms")["layout"], Is.EqualTo("p2"));
            var frozen = restored.GetOverrides(first, "bms");
            restored.Reset(first, "bms");
            Assert.That(restored.GetOverrides(first, "bms"), Is.Empty);
            Assert.That(frozen["layout"], Is.EqualTo("p2"), "Existing snapshots are immutable.");
            Assert.That(restored.GetOverrides(first, "mania")["notes"], Is.EqualTo("n01"));
            Assert.That(restored.GetOverrides(second, "bms")["layout"], Is.EqualTo("p1"));
        }

        [TestCase("invalid")]
        [TestCase("{\"duplicate\":{},\"duplicate\":{}}")]
        [TestCase("{\"00000000000000000000000000000000/bms\":{\"notes\":true}}")]
        public void TestMalformedPreferencesUseDefaultsWithoutRewritingOnRead(string raw)
        {
            using var storage = new TemporaryNativeStorage("skin-options-invalid");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.SkinOptions, raw);
            var preferences = new SkinOptionPreferences(config);
            Assert.That(preferences.GetOverrides(Guid.Empty, "bms"), Is.Empty);
            Assert.That(config.Get<string>(OsuSetting.SkinOptions), Is.EqualTo(raw));
        }

        [Test]
        public void TestKeyOverridesInheritOnlyTheSameSkinsModeDefaults()
        {
            using var storage = new TemporaryNativeStorage("skin-options-key-inheritance");
            using var config = new OsuConfigManager(storage);
            var preferences = new SkinOptionPreferences(config);
            Guid first = Guid.NewGuid(), other = Guid.NewGuid();
            preferences.SetOverride(first, "bms", "layout", "p2");
            preferences.SetOverride(first, "bms", "notes", "n02");
            preferences.SetOverride(first, "bms", "layout", "p1", keyCount: 7);
            preferences.SetOverride(first, "mania", "layout", "mania-single");
            preferences.SetOverride(other, "bms", "notes", "n03", keyCount: 7);

            Assert.Multiple(() =>
            {
                Assert.That(preferences.GetOverrides(first, "bms", 5)["layout"], Is.EqualTo("p2"));
                Assert.That(preferences.GetOverrides(first, "bms", 7)["layout"], Is.EqualTo("p1"));
                Assert.That(preferences.GetOverrides(first, "bms", 7)["notes"], Is.EqualTo("n02"));
                Assert.That(preferences.GetLocalOverrides(first, "bms", 7).Keys, Is.EqualTo(new[] { "layout" }));
                Assert.That(preferences.GetOverrides(first, "mania", 7)["layout"], Is.EqualTo("mania-single"));
                Assert.That(preferences.GetOverrides(other, "bms", 7).ContainsKey("layout"), Is.False);
            });

            var frozen = preferences.GetOverrides(first, "bms", 7);
            preferences.Reset(first, "bms", 7);
            Assert.Multiple(() =>
            {
                Assert.That(preferences.GetOverrides(first, "bms", 7)["layout"], Is.EqualTo("p2"));
                Assert.That(frozen["layout"], Is.EqualTo("p1"));
                Assert.That(preferences.GetOverrides(other, "bms", 7)["notes"], Is.EqualTo("n03"));
            });
        }

        [Test]
        public void TestKeyOverridesSurviveRestartAndModeResetLeavesThemExplicit()
        {
            using var storage = new TemporaryNativeStorage("skin-options-keys-persist");
            Guid id = Guid.NewGuid();
            using (var config = new OsuConfigManager(storage))
            {
                var preferences = new SkinOptionPreferences(config);
                preferences.SetOverride(id, "mania", "notes", "round");
                preferences.SetOverride(id, "mania", "notes", "bar", keyCount: 16);
                config.Save();
            }
            using var reloaded = new OsuConfigManager(storage);
            var restored = new SkinOptionPreferences(reloaded);
            restored.Reset(id, "mania");
            Assert.Multiple(() =>
            {
                Assert.That(restored.GetOverrides(id, "mania", 7), Is.Empty);
                Assert.That(restored.GetOverrides(id, "mania", 16)["notes"], Is.EqualTo("bar"));
                Assert.That(restored.GetLocalOverrides(id, "mania", 16)["notes"], Is.EqualTo("bar"));
            });
        }
    }
}
