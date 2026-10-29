// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsReadingSettings : OsuTestScene
    {
        private BmsRulesetConfigManager config = null!;
        private DrawableBmsRuleset renderer = null!;
        private BmsBeatmap beatmap = null!;
        private ManualClock clock = null!;

        protected override bool UseFreshStoragePerRun => true;

        [TearDownSteps]
        public void TearDown()
        {
            retireGame();
            AddStep("dispose isolated reading configuration", () => config?.Dispose());
        }

        [TestCase(BmsHiSpeedMode.Normal)]
        [TestCase(BmsHiSpeedMode.Floating)]
        [TestCase(BmsHiSpeedMode.Classic)]
        public void TestNewPlayUsesReadingSettingsWithoutSelectingMods(BmsHiSpeedMode mode)
        {
            BmsReadingSettingsSnapshot initial = settings(mode);
            prepare(initial);
            loadGame();
            assertRuntimeReading(() => initial);
            AddAssert("reading controls do not add gameplay mods", () => renderer.Mods, () => Is.Empty);
            AddAssert("all three native adjustment targets are available", () => renderer.EnabledAdjustmentTargetCount.Value, () => Is.EqualTo(3));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestFloatingScratchRemembersTheWholeReadingGroup(bool remember, bool bothCovers)
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating, hidden: bothCovers);
            BmsReadingSettingsSnapshot expected = initial;
            double initialVisibleTime = 0;
            prepare(initial, remember);
            loadGame();
            AddStep("capture the actual covered time", () => initialVisibleTime = renderer.GetScrollSpeedMetrics().VisibleLaneTime);
            AddStep("hold Start and turn the scratch", () =>
            {
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
            });
            AddStep("the native adjustment changes cover and compensating speed together", () =>
            {
                // Source float32 operation boundaries limit the refloat interval's arithmetic precision;
                // HUD decimal formatting does not quantise the stored multiplier.
                double timeTolerance = (float.BitIncrement((float)initialVisibleTime) - (float)initialVisibleTime) * 8;
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.SuddenUnits, Is.EqualTo(initial.SuddenUnits + 20));
                    Assert.That(renderer.HiddenUnits, Is.EqualTo(bothCovers ? initial.HiddenUnits + 20 : 0));
                    Assert.That(renderer.SelectedHiSpeed.Value, Is.Not.EqualTo(initial.FloatingHiSpeed));
                    Assert.That(renderer.GetScrollSpeedMetrics().VisibleLaneTime, Is.EqualTo(initialVisibleTime).Within(timeTolerance));
                });

                expected = remember
                    ? new BmsReadingSettingsSnapshot(initial.HiSpeedMode, initial.NormalHiSpeed, renderer.SelectedHiSpeed.Value, initial.ClassicHiSpeed,
                        initial.SuddenEnabled, initial.HiddenEnabled, initial.LiftEnabled, initial.LaneCoversEnabled,
                        renderer.SuddenUnits, bothCovers ? renderer.HiddenUnits : initial.HiddenUnits, initial.LiftUnits,
                        initial.SuddenOpacity, initial.HiddenOpacity)
                    : initial;
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(expected), "Remember must include the compensating Floating Hi-Speed.");
                Assert.That(config.Get<bool>(BmsRulesetSetting.RememberReadingChanges), Is.EqualTo(remember));
            });
            recreateConfigAndGame();
            AddAssert("a recreated manager reads the complete saved group", () => config.CaptureReadingSettings(), () => Is.EqualTo(expected));
            assertRuntimeReading(() => expected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestClassicFloatingRoundTripRemembersTheWholeReadingGroup(bool remember)
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Classic);
            BmsReadingSettingsSnapshot floating = initial;
            BmsReadingSettingsSnapshot returned = initial;
            BmsReadingSettingsSnapshot expected = initial;
            prepare(initial, remember);
            loadGame(chartText: "#TITLE Classic Floating reading\n#BPM 130\n#RANK 2\n#00811:01\n");
            AddStep("enter Floating and manually lower its speed", () =>
            {
                Assert.That(renderer.ToggleHiSpeedMode(), Is.True);
                Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(BmsHiSpeedMode.Floating));
                Assert.That(renderer.AdjustSelectedHiSpeed(-1), Is.True);
                floating = initial with { HiSpeedMode = BmsHiSpeedMode.Floating, FloatingHiSpeed = renderer.SelectedHiSpeed.Value };
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(remember ? floating : initial));
            });
            AddStep("return to Classic with the completed speed adjustment", () =>
            {
                Assert.That(renderer.ToggleHiSpeedMode(), Is.True);
                Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(BmsHiSpeedMode.Classic));
                Assert.That(renderer.SelectedHiSpeed.Value, Is.Not.EqualTo(initial.ClassicHiSpeed));
                returned = floating with { HiSpeedMode = BmsHiSpeedMode.Classic, ClassicHiSpeed = renderer.SelectedHiSpeed.Value };
                expected = remember ? returned : initial;
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(expected));
                Assert.That(config.Get<double>(BmsRulesetSetting.ScrollSpeed), Is.EqualTo(initial.NormalHiSpeed), "Classic navigation must not overwrite Normal Hi-Speed.");
            });
            assertRuntimeReading(() => returned);
            recreateConfigAndGame();
            assertRuntimeReading(() => expected);
        }

        [TestCase(BmsHiSpeedMode.Normal, BmsHiSpeedMode.Classic)]
        [TestCase(BmsHiSpeedMode.Classic, BmsHiSpeedMode.Normal)]
        public void TestExplicitLiveBaseModeUpdatesTheFloatingReturnMode(BmsHiSpeedMode initialMode, BmsHiSpeedMode explicitMode)
        {
            prepare(settings(initialMode), remember: false);
            loadGame();
            AddStep("enter Floating from the original base mode", () => Assert.That(renderer.ToggleHiSpeedMode(), Is.True));
            AddStep("select a different base mode and Floating in settings", () =>
            {
                config.SetValue(BmsRulesetSetting.HiSpeedMode, explicitMode);
                Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(explicitMode));
                config.SetValue(BmsRulesetSetting.HiSpeedMode, BmsHiSpeedMode.Floating);
                Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(BmsHiSpeedMode.Floating));
            });
            AddStep("leave Floating using the live control", () => Assert.That(renderer.ToggleHiSpeedMode(), Is.True));
            AddAssert("the most recent explicit live base mode returns", () => renderer.HiSpeedMode.Value, () => Is.EqualTo(explicitMode));
            AddAssert("temporary navigation does not overwrite the explicit settings", () => config.Get<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode),
                () => Is.EqualTo(BmsHiSpeedMode.Floating));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestModeAndCoverDisplayToggleKeepTheSelectedDoubleCoverCombination(bool remember)
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Normal);
            BmsReadingSettingsSnapshot expected = initial;
            prepare(initial, remember);
            loadGame();
            AddStep("change mode and speed then hide both covers", () =>
            {
                Assert.That(renderer.ToggleHiSpeedMode(), Is.True);
                Assert.That(renderer.AdjustSelectedHiSpeed(3), Is.True);
                Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(BmsHiSpeedMode.Floating));
                    Assert.That(renderer.SuddenActive, Is.False);
                    Assert.That(renderer.HiddenActive, Is.False);
                    Assert.That(renderer.LiftUnits, Is.EqualTo(initial.LiftUnits));
                });

                expected = remember
                    ? new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Floating, initial.NormalHiSpeed, renderer.SelectedHiSpeed.Value, initial.ClassicHiSpeed,
                        true, true, true, false, initial.SuddenUnits, initial.HiddenUnits, initial.LiftUnits, initial.SuddenOpacity, initial.HiddenOpacity)
                    : initial;
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(expected));
                Assert.That(config.Get<bool>(BmsRulesetSetting.RememberReadingChanges), Is.EqualTo(remember));
            });
            recreateConfigAndGame();
            assertRuntimeReading(() => expected);
            AddStep("reopen the hidden saved combination", () =>
            {
                if (renderer.SuddenActive || renderer.HiddenActive)
                    Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
                Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
            });
            AddAssert("both selected covers return together", () => renderer.SuddenActive && renderer.HiddenActive);
            AddAssert("Lift is independent of the cover display switch", () => renderer.LiftUnits, () => Is.EqualTo(initial.LiftUnits));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSuddenAddedDuringLiftOnlyPlayFollowsRemember(bool remember)
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating, sudden: false, hidden: false);
            BmsReadingSettingsSnapshot expected = initial;
            prepare(initial, remember);
            loadGame();
            AddStep("double Start adds Sudden while retaining Lift", () =>
            {
                Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(40)), Is.True);
                Assert.That(renderer.SuddenActive, Is.True);
                Assert.That(renderer.HiddenActive, Is.False);
                Assert.That(renderer.LiftUnits, Is.EqualTo(initial.LiftUnits));
                expected = remember
                    ? new BmsReadingSettingsSnapshot(initial.HiSpeedMode, initial.NormalHiSpeed, renderer.SelectedHiSpeed.Value, initial.ClassicHiSpeed,
                        true, false, true, true, renderer.SuddenUnits, initial.HiddenUnits, initial.LiftUnits,
                        initial.SuddenOpacity, initial.HiddenOpacity)
                    : initial;
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(expected));
                Assert.That(config.Get<bool>(BmsRulesetSetting.RememberReadingChanges), Is.EqualTo(remember));
            });
            recreateConfigAndGame();
            assertRuntimeReading(() => expected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLiftOnlyAdjustmentFollowsTheSameRememberSwitch(bool remember)
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating, sudden: false, hidden: false);
            BmsReadingSettingsSnapshot expected = initial;
            prepare(initial, remember);
            loadGame();
            AddStep("adjust the actual Lift target", () =>
            {
                Assert.That(renderer.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Lift));
                Assert.That(renderer.AdjustGameplayAdjustment(20), Is.True);
            });
            AddUntilStep("native Lift geometry updates", () => renderer.LiftUnits == initial.LiftUnits + 20);
            AddStep("only remembered Lift reaches personal settings", () =>
            {
                expected = remember
                    ? new BmsReadingSettingsSnapshot(initial.HiSpeedMode, initial.NormalHiSpeed, initial.FloatingHiSpeed, initial.ClassicHiSpeed,
                        false, false, true, initial.LaneCoversEnabled, initial.SuddenUnits, initial.HiddenUnits, renderer.LiftUnits,
                        initial.SuddenOpacity, initial.HiddenOpacity)
                    : initial;
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(expected));
                Assert.That(config.Get<bool>(BmsRulesetSetting.RememberReadingChanges), Is.EqualTo(remember));
            });
            recreateConfigAndGame();
            assertRuntimeReading(() => expected);
        }

        [Test]
        public void TestOpacityEditPreservesFloatingCoverTargetAfterLiftAdjustment()
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating, hidden: false);
            BmsReadingSettingsSnapshot beforeOpacity = default;
            double targetTime = 0;
            int targetGreen = 0;
            prepare(initial);
            loadGame();
            AddStep("remember the covered Floating target and hide Sudden", () =>
            {
                targetTime = renderer.GetScrollSpeedMetrics().VisibleLaneTime;
                targetGreen = renderer.GetScrollSpeedMetrics().GreenNumber;
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
                Assert.That(renderer.SuddenActive, Is.False);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
            });
            AddUntilStep("the native Lift has moved while Sudden is hidden", () => renderer.LiftUnits == initial.LiftUnits - 20);
            collectTransientConfigurationCopies();
            AddStep("edit only Sudden opacity through reading settings", () =>
            {
                beforeOpacity = config.CaptureReadingSettings();
                Assert.That(beforeOpacity.LaneCoversEnabled, Is.False);
                config.SetValue(BmsRulesetSetting.SuddenOpacity, 410f);
            });
            AddAssert("the opacity edit leaves every other personal reading value intact", () => config.CaptureReadingSettings(),
                () => Is.EqualTo(beforeOpacity with { SuddenOpacity = 410 }));
            AddStep("reopen Sudden after the explicit opacity edit", () =>
            {
                Assert.That(renderer.SuddenActive, Is.False);
                Assert.That(renderer.LiftUnits, Is.EqualTo(initial.LiftUnits - 20));
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(initial.FloatingHiSpeed));
                Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
            });
            AddStep("the original covered green target returns at native white-number precision", () =>
            {
                BmsScrollSpeedMetrics metrics = renderer.GetScrollSpeedMetrics();
                // Refloating rounds the required Sudden amount to one WN unit. Half a unit is the maximum
                // time error; the two independently rounded GN values may differ by one additional integer.
                double timeTolerance = metrics.RuntimeTimeRange / 2000 + 0.000001;
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.SuddenActive, Is.True);
                    Assert.That(renderer.HiddenActive, Is.False);
                    Assert.That(metrics.VisibleLaneTime, Is.EqualTo(targetTime).Within(timeTolerance));
                    Assert.That(metrics.GreenNumber, Is.EqualTo(targetGreen).Within(Math.Ceiling(timeTolerance * 0.6) + 1));
                    Assert.That(renderer.Playfield.LaneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Sudden).CoverOpacity.Value,
                        Is.EqualTo(410));
                    Assert.That(config.Get<float>(BmsRulesetSetting.SuddenOpacity), Is.EqualTo(410));
                    Assert.That(config.Get<float>(BmsRulesetSetting.LiftUnits), Is.EqualTo(initial.LiftUnits - 20));
                });
            });
        }

        [Test]
        public void TestExplicitSettingsUpdatePausedPlayEvenWhenGameplayRememberIsDisabled()
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating);
            var edited = new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Classic, 9.2, 1.75, 4.25,
                false, true, false, true, 345, 165, 95, 410, 590);
            prepare(initial, remember: false);
            loadGame();
            AddStep("pause the actual ruleset", () =>
            {
                renderer.IsPaused.Value = true;
                clock.IsRunning = false;
                renderer.SetAdjustmentHeld(true);
            });
            AddStep("ordinary gameplay controls cannot change paused settings", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.False);
                    Assert.That(renderer.AdjustGameplayAdjustment(20), Is.False);
                    Assert.That(renderer.AdjustSelectedHiSpeed(1), Is.False);
                    Assert.That(renderer.ToggleHiSpeedMode(), Is.False);
                    Assert.That(renderer.ToggleGameplayLaneCovers(), Is.False);
                    Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial));
                });
            });
            collectTransientConfigurationCopies();
            AddStep("save an explicit edit from reading settings", () => config.SaveReadingSettings(edited));
            assertRuntimeReading(() => edited);
            AddAssert("explicit settings persist independently of Remember", () => config.CaptureReadingSettings(), () => Is.EqualTo(edited));
            AddAssert("explicit editing leaves Remember disabled", () => config.Get<bool>(BmsRulesetSetting.RememberReadingChanges), () => Is.False);
            recreateConfigAndGame();
            assertRuntimeReading(() => edited);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestHistoricalReplayUsesOnlyLegacyParametersAndCannotWritePersonalSettings(bool legacyAdjustments)
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating);
            Mod[] legacyMods = legacyAdjustments
                ? new Mod[]
                {
                    new BmsModSudden { CoverPercent = { Value = 340 }, CoverOpacity = { Value = 420 } },
                    new BmsModLift { LiftUnits = { Value = 135 } },
                }
                : Array.Empty<Mod>();
            Score replay = null!;
            prepare(initial);
            loadGame(legacyMods);
            AddStep("load a historical replay without a reading snapshot", () =>
            {
                replay = new Score
                {
                    Replay = new Replay { Frames = { new BmsReplayFrame(0), new BmsReplayFrame(20000) } },
                    ScoreInfo = { Mods = legacyMods },
                };
                renderer.SetReplayScore(replay);
            });
            AddUntilStep("historical replay owns input and native Lift", () => renderer.HasReplayLoaded.Value && renderer.LiftUnits == (legacyAdjustments ? 135 : 0));
            AddStep("personal cover selection does not leak into old playback", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.SuddenActive, Is.EqualTo(legacyAdjustments));
                    Assert.That(renderer.HiddenActive, Is.False);
                    Assert.That(renderer.SuddenUnits, Is.EqualTo(legacyAdjustments ? 340 : 0));
                    Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial));
                    Assert.That(replay.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.ReadingSettings, Is.Null);
                });
            });
            assertReplayCannotAdjust();
            AddAssert("historical playback never rewrites personal settings", () => config.CaptureReadingSettings(), () => Is.EqualTo(initial));
            recreateConfigAndGame();
            assertRuntimeReading(() => initial);
        }

        [Test]
        public void TestRecordedReplayKeepsInitialReadingSnapshotAndDoesNotLearnLaterPersonalSettings()
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating);
            var laterPersonalSettings = new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Classic, 9.2, 1.75, 4.25,
                false, false, false, false, 345, 165, 95, 410, 590);
            Score recorded = null!;
            prepare(initial);
            loadGame();
            AddStep("record with the actual native recorder", () => renderer.SetRecordTarget(recorded = new Score()));
            AddAssert("recording stores the opening reading preferences", () => readingPreferences(recorded.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.ReadingSettings!.Value),
                () => Is.EqualTo(initial));
            AddStep("change the current play then finish recording", () =>
            {
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
                renderer.SetRecordTarget(null!);
            });
            AddAssert("later adjustments do not mutate the opening preferences", () => readingPreferences(recorded.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.ReadingSettings!.Value),
                () => Is.EqualTo(initial));
            AddStep("set unrelated personal values and load that recording", () =>
            {
                config.SaveReadingSettings(laterPersonalSettings);
                clock.CurrentTime = 900;
                renderer.SetReplayScore(recorded);
            });
            assertRuntimeReading(() => initial);
            assertReplayCannotAdjust();
            AddAssert("replay viewing leaves later personal settings intact", () => config.CaptureReadingSettings(), () => Is.EqualTo(laterPersonalSettings));
        }

        [Test]
        public void TestGeneratedAutoplayRemainsACurrentPlayForReadingAdjustments()
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Floating);
            Score generated = null!;
            prepare(initial);
            loadGame();
            AddStep("load freshly generated native autoplay", () =>
            {
                var autoplay = new BmsModAutoplay();
                renderer.SetReplayScore(generated = autoplay.CreateScoreFromReplayData(beatmap, new Mod[] { autoplay }));
            });
            AddUntilStep("generated autoplay is loaded", () => renderer.HasReplayLoaded.Value);
            AddAssert("generated autoplay explicitly uses the current native reading calculation", () => generated.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.ReadingCalculationVersion,
                () => Is.EqualTo(1));
            AddStep("current-play adjustment remains available", () =>
            {
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.SuddenUnits, Is.EqualTo(initial.SuddenUnits + 20));
                    Assert.That(config.Get<float>(BmsRulesetSetting.SuddenUnits), Is.EqualTo(renderer.SuddenUnits));
                    Assert.That(config.Get<double>(BmsRulesetSetting.FloatingHiSpeed), Is.EqualTo(renderer.SelectedHiSpeed.Value));
                    Assert.That(generated.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.ReadingCalculationVersion, Is.EqualTo(1));
                });
            });
        }

        private void collectTransientConfigurationCopies()
            => AddStep("collect transient configuration copies before editing", () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

        private void prepare(BmsReadingSettingsSnapshot initial, bool remember = true)
        {
            AddStep("create isolated persistent reading configuration", () =>
            {
                config = new BmsRulesetConfigManager(new SettingsStore(Realm), new BmsRuleset().RulesetInfo);
                config.SetValue(BmsRulesetSetting.RememberReadingChanges, remember);
                config.SaveReadingSettings(initial);
                config.Save();
            });
        }

        private void loadGame(Mod[]? mods = null, string chartText = "#TITLE Reading settings\n#BPM 120\n#RANK 2\n#00811:01\n")
        {
            AddStep("load a real BMS ruleset with the reading configuration", () =>
            {
                var ruleset = new BmsRuleset();
                var source = new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(chartText, "reading-settings.bme"));
                beatmap = (BmsBeatmap)new BmsBeatmapConverter(source, ruleset).Convert();
                clock = new ManualClock { CurrentTime = 1000, IsRunning = true };
                renderer = new DrawableBmsRuleset(ruleset, beatmap, mods)
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock),
                };
                renderer.InitialiseCompatibilityLayoutForTesting();
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(IRulesetConfigCache), new ReadingConfigCache(config)) },
                    Child = renderer,
                };
            });
            AddUntilStep("native ruleset, controls and geometry are loaded", () => renderer.IsLoaded
                && renderer.GameplayInputManager?.IsLoaded == true && renderer.Playfield.GameplaySkinLiftState.IsAvailable);
        }

        private void retireGame()
        {
            GameplaySkinLayoutRevisionOwner? previousOwner = null;
            DrawableBmsRuleset? previousRenderer = null;
            AddStep("retire the previous gameplay tree", () =>
            {
                previousRenderer = renderer;
                previousOwner = renderer?.LayoutProvider.RevisionOwner;
                Clear();
                // This fixture created the detached compatibility owner; a production PlayerLoader owns its
                // own publication. Disposing the drawable tree alone does not retire our fixture-owned owner.
                previousOwner?.Dispose();
            });
            AddUntilStep("the previous gameplay tree and publication are retired", () =>
                (previousRenderer == null || (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(previousRenderer)!)
                && (previousOwner == null || previousOwner.IsDisposed && previousOwner.CurrentPublication == null));
        }

        private void recreateConfigAndGame()
        {
            BmsReadingSettingsSnapshot beforeRetire = default;
            AddStep("capture persisted preferences before retiring the old game", () => beforeRetire = config.CaptureReadingSettings());
            retireGame();
            AddStep("flush and recreate the configuration from the isolated database", () =>
            {
                bool remember = config.Get<bool>(BmsRulesetSetting.RememberReadingChanges);
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(beforeRetire), "Retiring gameplay cannot mutate reading preferences.");
                config.Save();
                config.Dispose();
                config = new BmsRulesetConfigManager(new SettingsStore(Realm), new BmsRuleset().RulesetInfo);
                Assert.That(config.Get<bool>(BmsRulesetSetting.RememberReadingChanges), Is.EqualTo(remember));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(beforeRetire), "Saving and recreating the manager must preserve the complete reading preferences.");
            });
            loadGame();
        }

        private void assertRuntimeReading(Func<BmsReadingSettingsSnapshot> expected)
        {
            AddUntilStep("native Lift matches the reading settings", () => renderer.LiftUnits == (expected().LiftEnabled ? expected().LiftUnits : 0));
            AddStep("the loaded native controls match the complete reading settings", () =>
            {
                BmsReadingSettingsSnapshot value = expected();
                double speed = value.HiSpeedMode switch
                {
                    BmsHiSpeedMode.Normal => value.NormalHiSpeed,
                    BmsHiSpeedMode.Floating => value.FloatingHiSpeed,
                    BmsHiSpeedMode.Classic => value.ClassicHiSpeed,
                    _ => throw new ArgumentOutOfRangeException(),
                };
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(value.HiSpeedMode));
                    Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(speed));
                    Assert.That(renderer.SuddenActive, Is.EqualTo(value.SuddenEnabled && value.LaneCoversEnabled));
                    Assert.That(renderer.HiddenActive, Is.EqualTo(value.HiddenEnabled && value.LaneCoversEnabled));
                    Assert.That(renderer.SuddenUnits, Is.EqualTo(value.SuddenEnabled ? value.SuddenUnits : 0));
                    Assert.That(renderer.HiddenUnits, Is.EqualTo(value.HiddenEnabled ? value.HiddenUnits : 0));
                    if (value.SuddenEnabled)
                        Assert.That(renderer.Playfield.LaneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Sudden).CoverOpacity.Value,
                            Is.EqualTo(value.SuddenOpacity));
                    if (value.HiddenEnabled)
                        Assert.That(renderer.Playfield.LaneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Hidden).CoverOpacity.Value,
                            Is.EqualTo(value.HiddenOpacity));
                });
            });
        }

        private void assertReplayCannotAdjust()
        {
            AddStep("historical and recorded replay controls are read-only", () =>
            {
                float sudden = renderer.SuddenUnits;
                float hidden = renderer.HiddenUnits;
                float lift = renderer.LiftUnits;
                double speed = renderer.SelectedHiSpeed.Value;
                bool suddenActive = renderer.SuddenActive;
                bool hiddenActive = renderer.HiddenActive;
                BmsHiSpeedMode mode = renderer.HiSpeedMode.Value;
                renderer.SetAdjustmentHeld(true);
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.False);
                    Assert.That(renderer.AdjustGameplayAdjustment(20), Is.False);
                    Assert.That(renderer.AdjustLaneCover(20), Is.False);
                    Assert.That(renderer.AdjustSelectedHiSpeed(1), Is.False);
                    Assert.That(renderer.ToggleHiSpeedMode(), Is.False);
                    Assert.That(renderer.ToggleGameplayLaneCovers(), Is.False);
                    Assert.That(renderer.SuddenUnits, Is.EqualTo(sudden));
                    Assert.That(renderer.HiddenUnits, Is.EqualTo(hidden));
                    Assert.That(renderer.LiftUnits, Is.EqualTo(lift));
                    Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(speed));
                    Assert.That(renderer.SuddenActive, Is.EqualTo(suddenActive));
                    Assert.That(renderer.HiddenActive, Is.EqualTo(hiddenActive));
                    Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(mode));
                });
            });
        }

        private static BmsReadingSettingsSnapshot settings(BmsHiSpeedMode mode, bool sudden = true, bool hidden = true)
            => new BmsReadingSettingsSnapshot(mode, 8, 2.5, 3.5, sudden, hidden, true, true, 250, 125, 80, 650, 750);

        private static OmsAxisMotion scratch(double delta)
            => new OmsAxisMotion(OmsAction.Key1P_Scratch, OmsAxisMode.RelativeMotion, delta, "reading-settings-test", null, 50);

        private static BmsReadingSettingsSnapshot readingPreferences(BmsReadingSettingsSnapshot value)
            => value with { NormalHiSpeedMultiplier = null };

        private sealed class ReadingConfigCache : IRulesetConfigCache
        {
            private readonly BmsRulesetConfigManager config;

            public ReadingConfigCache(BmsRulesetConfigManager config)
            {
                this.config = config;
            }

            public IRulesetConfigManager GetConfigFor(Ruleset ruleset) => config;
        }
    }
}
