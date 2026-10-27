// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class BmsReadingSettingsMigrationTest : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private const string legacy_state = """
            {
              "SelectedModAcronyms":["sud","HID","LIFT","RD"],
              "RememberedMods":[
                {"acronym":"SUD","settings":{"cover_percent":321,"cover_opacity":700,"remember_gameplay_changes":true}},
                {"acronym":"HID","settings":{"cover_percent":531,"cover_opacity":825,"remember_gameplay_changes":false}},
                {"acronym":"LIFT","settings":{"lift_units":187,"remember_gameplay_changes":true}},
                {"acronym":"RD","settings":{"seed":1234}}
              ]
            }
            """;

        [Test]
        public void TestLegacyReadingModsMigrateOnceWithFalseRememberTakingPriority()
        {
            AddStep("TestLegacyReadingModsMigrateOnceWithFalseRememberTakingPriority", () =>
            {
                using var storage = new TemporaryNativeStorage($"bms-reading-migration-{Guid.NewGuid():N}");
                using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
                storeSetting(realm, BmsRulesetSetting.PersistedModState, legacy_state);
                storeSetting(realm, BmsRulesetSetting.HiSpeedMode, "Floating");
                storeSetting(realm, BmsRulesetSetting.ScrollSpeed, "9");
                storeSetting(realm, BmsRulesetSetting.FloatingHiSpeed, "1.75");
                storeSetting(realm, BmsRulesetSetting.ClassicHiSpeed, "3.25");

                BmsReadingSettingsSnapshot migrated;

                using (var config = createConfig(realm))
                {
                    migrated = config.CaptureReadingSettings();
                    Assert.Multiple(() =>
                    {
                        Assert.That(migrated, Is.EqualTo(new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Floating, 9, 1.75, 3.25,
                            true, true, true, true, 321, 531, 187, 700, 825)));
                        Assert.That(config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.False);
                        Assert.That(config.GetBindable<bool>(BmsRulesetSetting.ReadingSettingsMigrated).Value, Is.True);
                        Assert.That(config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value, Is.EqualTo(legacy_state),
                            "Reading migration must not rewrite the old source or unrelated Mod settings.");
                    });
                    config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value = """{"SelectedModAcronyms":[],"RememberedMods":[]}""";
                    config.Save();
                }

                using var reopened = createConfig(realm);
                Assert.Multiple(() =>
                {
                    Assert.That(reopened.CaptureReadingSettings(), Is.EqualTo(migrated));
                    Assert.That(reopened.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.False);
                });
            });
        }

        [Test]
        public void TestDefaultLegacySelectionEnablesTheOriginalCombination()
        {
            AddStep("TestDefaultLegacySelectionEnablesTheOriginalCombination", () =>
            {
                using var storage = new TemporaryNativeStorage($"bms-reading-default-migration-{Guid.NewGuid():N}");
                using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
                storeSetting(realm, BmsRulesetSetting.PersistedModState, """{"SelectedModAcronyms":["HID","LIFT"]}""");

                using var config = createConfig(realm);
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Normal, 8, 2.5, 2.5,
                    false, true, true, true, 250, 250, 250, 1000, 1000)));
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.True);
            });
        }

        [Test]
        public void TestRememberedValuesDoNotEnableUnselectedCovers()
        {
            AddStep("TestRememberedValuesDoNotEnableUnselectedCovers", () =>
            {
                using var storage = new TemporaryNativeStorage($"bms-reading-unselected-migration-{Guid.NewGuid():N}");
                using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
                storeSetting(realm, BmsRulesetSetting.PersistedModState, """
                    {"SelectedModAcronyms":[],"RememberedMods":[
                      {"acronym":"SUD","settings":{"cover_percent":410,"remember_gameplay_changes":false}},
                      {"acronym":"LIFT","settings":{"lift_units":190}}
                    ]}
                    """);

                using var config = createConfig(realm);
                var snapshot = config.CaptureReadingSettings();
                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.SuddenEnabled || snapshot.HiddenEnabled || snapshot.LiftEnabled, Is.False);
                    Assert.That(snapshot.SuddenUnits, Is.EqualTo(410));
                    Assert.That(snapshot.LiftUnits, Is.EqualTo(190));
                    Assert.That(config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.False);
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestExplicitNewReadingSettingsNeverResurrectLegacySelection(bool migrationAlreadyMarked)
        {
            AddStep("TestExplicitNewReadingSettingsNeverResurrectLegacySelection", () =>
            {
                using var storage = new TemporaryNativeStorage($"bms-reading-preserve-{Guid.NewGuid():N}");
                using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
                storeSetting(realm, BmsRulesetSetting.PersistedModState, legacy_state);
                storeSetting(realm, BmsRulesetSetting.SuddenEnabled, "False");
                storeSetting(realm, BmsRulesetSetting.LiftEnabled, "True");
                storeSetting(realm, BmsRulesetSetting.LiftUnits, "99");
                storeSetting(realm, BmsRulesetSetting.LaneCoversEnabled, "False");
                storeSetting(realm, BmsRulesetSetting.ReadingSettingsMigrated, migrationAlreadyMarked.ToString());

                using var config = createConfig(realm);
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Normal, 8, 2.5, 2.5,
                    false, false, true, false, 250, 250, 99, 1000, 1000)));
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.True);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.ReadingSettingsMigrated).Value, Is.True);
            });
        }

        [TestCase("{")]
        [TestCase("{\"SelectedModAcronyms\":null}")]
        [TestCase("{\"RememberedMods\":[null]}")]
        public void TestMalformedLegacyStateUsesReadingDefaultsAndCompletesMigration(string rawState)
        {
            AddStep("TestMalformedLegacyStateUsesReadingDefaultsAndCompletesMigration", () =>
            {
                using var storage = new TemporaryNativeStorage($"bms-reading-invalid-migration-{Guid.NewGuid():N}");
                using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
                storeSetting(realm, BmsRulesetSetting.PersistedModState, rawState);

                using var config = createConfig(realm);
                Assert.Multiple(() =>
                {
                    Assert.That(config.CaptureReadingSettings(), Is.EqualTo(new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Normal, 8, 2.5, 2.5,
                        false, false, false, true, 250, 250, 250, 1000, 1000)));
                    Assert.That(config.GetBindable<bool>(BmsRulesetSetting.ReadingSettingsMigrated).Value, Is.True);
                    Assert.That(config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value, Is.EqualTo(rawState));
                });
            });
        }

        [TestCase("1200")]
        [TestCase("\"bad\"")]
        [TestCase("NaN")]
        public void TestInvalidLegacyCoverValueDoesNotDiscardOtherReadingSettings(string invalidValue)
        {
            AddStep("TestInvalidLegacyCoverValueDoesNotDiscardOtherReadingSettings", () =>
            {
                using var storage = new TemporaryNativeStorage($"bms-reading-invalid-value-{Guid.NewGuid():N}");
                using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
                storeSetting(realm, BmsRulesetSetting.PersistedModState, $$$"""
                    {"SelectedModAcronyms":["SUD","LIFT"],"RememberedMods":[
                      {"acronym":"SUD","settings":{"cover_percent":{{{invalidValue}}},"cover_opacity":600}},
                      {"acronym":"LIFT","settings":{"lift_units":123,"remember_gameplay_changes":false}}
                    ]}
                    """);

                using var config = createConfig(realm);
                var snapshot = config.CaptureReadingSettings();
                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.SuddenEnabled && snapshot.LiftEnabled, Is.True);
                    Assert.That(snapshot.SuddenUnits, Is.EqualTo(250));
                    Assert.That(snapshot.SuddenOpacity, Is.EqualTo(600));
                    Assert.That(snapshot.LiftUnits, Is.EqualTo(123));
                    Assert.That(config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.False);
                });
            });
        }

        [Test]
        public void TestLegacyCoversAreNotRestoredAsSelectedOrRememberedMods()
        {
            AddStep("TestLegacyCoversAreNotRestoredAsSelectedOrRememberedMods", () =>
            {
                var ruleset = new BmsRuleset();
                using var config = (BmsRulesetConfigManager)ruleset.CreateConfig(null)!;
                config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value = legacy_state;
                using var persistence = new BmsModStatePersistence(ruleset, config);
                var sudden = new BmsModSudden();
                var hidden = new BmsModHidden();
                var lift = new BmsModLift();
                var random = new BmsModRandom();
                var available = new Dictionary<ModType, IReadOnlyList<Mod>>
                {
                    [ModType.Conversion] = new Mod[] { random },
                    [ModType.System] = new Mod[] { sudden, hidden, lift },
                };

                persistence.ApplyStoredState(available);

                Assert.Multiple(() =>
                {
                    Assert.That(persistence.GetStoredSelection(available), Is.EqualTo(new[] { random }));
                    Assert.That(random.Seed.Value, Is.EqualTo(1234));
                    Assert.That(sudden.CoverPercent.Value, Is.EqualTo(250));
                    Assert.That(hidden.CoverPercent.Value, Is.EqualTo(250));
                    Assert.That(lift.LiftUnits.Value, Is.EqualTo(250));
                });
            });
        }

        [Test]
        public void TestOldPresetCoversAreRemovedFromLiveSelectionWithoutChangingPresetOrReadingSettings()
        {
            AddStep("TestOldPresetCoversAreRemovedFromLiveSelectionWithoutChangingPresetOrReadingSettings", () =>
            {
                var ruleset = new BmsRuleset();
                using var config = (BmsRulesetConfigManager)ruleset.CreateConfig(null)!;
                var reading = config.CaptureReadingSettings() with { HiddenEnabled = true, HiddenUnits = 411, LaneCoversEnabled = false };
                config.SaveReadingSettings(reading);
                config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value = legacy_state;
                using var persistence = new BmsModStatePersistence(ruleset, config);
                var random = new BmsModRandom { Seed = { Value = 5678 } };
                Mod[] oldPreset = { new BmsModSudden(), new BmsModHidden(), new BmsModLift(), random };
                var selected = new Bindable<IReadOnlyList<Mod>>(oldPreset);

                persistence.BindSelectedMods(selected);
                Assert.Multiple(() =>
                {
                    Assert.That(selected.Value, Is.EqualTo(new[] { random }));
                    Assert.That(oldPreset, Has.Length.EqualTo(4));
                    Assert.That(config.CaptureReadingSettings(), Is.EqualTo(reading));
                });

                random.Seed.Value = 2468;
                var saved = BmsModStatePersistence.ReadStoredState(config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value);
                Assert.Multiple(() =>
                {
                    Assert.That(saved.SelectedModAcronyms, Is.EqualTo(new[] { "RD" }));
                    Assert.That(saved.RememberedMods.Select(mod => mod.Acronym), Is.EqualTo(new[] { "RD" }));
                    Assert.That(Convert.ToInt32(saved.RememberedMods.Single().Settings["seed"]), Is.EqualTo(2468));
                });

                selected.Value = new Mod[] { new BmsModLift() };
                Assert.Multiple(() =>
                {
                    Assert.That(selected.Value, Is.Empty, "Applying a later legacy preset must also filter the live selection.");
                    Assert.That(config.CaptureReadingSettings(), Is.EqualTo(reading));
                });
            });
        }

        private static BmsRulesetConfigManager createConfig(RealmAccess realm)
            => new BmsRulesetConfigManager(new SettingsStore(realm), new BmsRuleset().RulesetInfo);

        private static void storeSetting(RealmAccess realm, BmsRulesetSetting setting, string value)
            => realm.Write(r => r.Add(new RealmRulesetSetting
            {
                RulesetName = BmsRuleset.SHORT_NAME,
                Key = setting.ToString(),
                Value = value,
            }));
    }
}
