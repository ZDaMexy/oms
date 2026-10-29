// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsSoloPlayerPreStart : ScreenTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly BmsDecodedBeatmap sourceBeatmap;

        private TestBmsSoloPlayer player = null!;
        private Mod[] selectedMods = null!;
        private BmsDecodedBeatmap? sourceOverride;
        private BmsHiSpeedMode initialHiSpeedMode;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public TestSceneBmsSoloPlayerPreStart()
        {
            sourceBeatmap = createSourceBeatmap(@"
#TITLE Pre-start Hi-Speed Stub
#BPM 60
#RANK 2
#00311:AA00
#WAVAA key1.wav
#WAVBB hold-head.wav
#WAVCC hold-tail.wav
#LNTYPE 1
#00352:BB00CC00
", "pre-start-hi-speed-stub.bme");
        }

        protected override Ruleset CreateRuleset() => new BmsRuleset();

        [SetUp]
        public void SetUp()
        {
            selectedMods = new Mod[] { new BmsModSudden() };
            sourceOverride = null;
            initialHiSpeedMode = BmsHiSpeedMode.Normal;
        }

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            // ScreenStack keeps exited screens until their lifetimes elapse. Reclaim the actual previous tree on
            // Update rather than leaving a pre-start screen's resources behind across test cases.
            AddStep("dispose previous exited gameplay screen", () => player?.RemoveAndDisposeImmediately());
            AddUntilStep("wait for previous gameplay layout owner retirement", () =>
                player?.DrawableBmsRuleset?.LayoutProvider.RevisionOwner?.IsDisposed != false);
            AddStep("set ruleset", () => Ruleset.Value = bms_ruleset_info);
            AddStep("set beatmap", () => Beatmap.Value = CreateWorkingBeatmap(sourceOverride ?? sourceBeatmap));
            AddStep("set gameplay mods", () => SelectedMods.Value = selectedMods
                .Where(mod => mod is not BmsModLaneCover and not BmsModLift).ToArray());
            AddStep("set initial reading configuration", () =>
            {
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;
                var sudden = selectedMods.OfType<BmsModSudden>().SingleOrDefault();
                var hidden = selectedMods.OfType<BmsModHidden>().SingleOrDefault();
                var lift = selectedMods.OfType<BmsModLift>().SingleOrDefault();
                config.SetValue(BmsRulesetSetting.RememberReadingChanges, true);
                config.SaveReadingSettings(new BmsReadingSettingsSnapshot(
                    initialHiSpeedMode, 8, 2.5, 2.5,
                    sudden != null, hidden != null, lift != null, true,
                    sudden?.CoverPercent.Value ?? 250, hidden?.CoverPercent.Value ?? 250, lift?.LiftUnits.Value ?? 250,
                    sudden?.CoverOpacity.Value ?? 1000, hidden?.CoverOpacity.Value ?? 1000));
            });
            AddStep("load BMS solo player", () => LoadScreen(player = new TestBmsSoloPlayer()));
            AddUntilStep("wait for BMS solo player", () => player.IsCurrentScreen() && player.IsLoaded && player.DrawableBmsRuleset?.IsLoaded == true && player.GameplayInputManager != null);
        }

        [Test]
        public void TestRealPreStartPlayerRejectsCurrentRevisionPublicationBeforePrepare()
        {
            SkinCurrentRevision revision = null!;
            Skin owner = null!;
            Task<SkinCurrentRevisionReloadResult>? reload = null;
            int prepareCalls = 0;

            AddStep("capture real pre-start player revision", () =>
            {
                revision = skinManager.CurrentRevision;
                owner = skinManager.CurrentSkin.Value;
                skinManager.CurrentRevisionPrepareStarted = () => prepareCalls++;
            });
            AddStep("request reload while real pre-start player is attached", () =>
                reload = skinManager.ReloadCurrentRevisionAsync());
            AddStep("assert publication rejected before source prepare", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(reload!.IsCompleted, Is.True);
                    Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.LiveGameplayActive));
                    Assert.That(prepareCalls, Is.Zero);
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(revision));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(owner));
                });
            });
        }

        [Test]
        public void TestGaugeBarStaysVisibleWhenHealthBarHidden()
        {
            AddStep("expire pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay clock running", () => player.GameplayClockContainer.IsRunning);
            AddUntilStep("gauge present", () => player.ChildrenOfType<BmsGaugeBar>().Any());
            // Mimic NoFail / ShowHealthBar=false: HealthDisplay fades itself to 0, which must NOT take the BMS groove
            // gauge with it (it is core gameplay info, always shown).
            AddStep("hide generic health bar", () => player.ChildrenOfType<HUDOverlay>().Single().ShowHealthBar.Value = false);
            AddWaitStep("let fade settle", 30);
            AddAssert("BMS gauge stays visible", () => player.ChildrenOfType<BmsGaugeBar>().Single().Alpha > 0);
        }

        [Test]
        public void TestProductionRootPublishesOneExactLayoutSnapshotToEveryBmsConsumer()
        {
            AddUntilStep("root, preview and BGA layout consumers loaded", () =>
                player.DrawableBmsRuleset?.PreStartSpeedPreviewLayoutSnapshot != null
                && player.DrawableBmsRuleset.BgaLayoutSnapshot != null);
            AddUntilStep("HUD snapshot carrier loaded", () =>
                player.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Any(carrier => carrier.LayoutSnapshot != null));
            AddUntilStep("HUD display and components loaded", () =>
                player.ChildrenOfType<DefaultBmsHudLayoutDisplay>().Any()
                && player.ChildrenOfType<BmsGaugeBar>().Any()
                && player.ChildrenOfType<BmsComboCounter>().Any());
            AddStep("seek into the real pooled object lifetime window", () =>
            {
                // C5 Note/LN hosts are lifetime-pooled and are intentionally absent from the hierarchy while all
                // chart objects are still in the future. Seek the real gameplay clock to the first chart object so
                // this production assertion inspects the drawable instances that the engine actually activates,
                // rather than requiring a duplicate always-mounted compatibility visual.
                double firstObjectTime = player.DrawableBmsRuleset!.Objects.Min(hitObject => hitObject.StartTime);
                player.GameplayClockContainer.Seek(firstObjectTime);
            });
            AddUntilStep("note and hold layout consumers loaded", () =>
                player.ChildrenOfType<DrawableBmsHitObject>().Any(drawable => drawable.HitObject.GetType() == typeof(BmsHitObject))
                && player.ChildrenOfType<DrawableBmsHoldNote>().Any());

            AddAssert("all production consumers retain exact root snapshot", () =>
            {
                DrawableBmsRuleset ruleset = player.DrawableBmsRuleset!;
                BmsGameplayLayoutSnapshot snapshot = ruleset.LayoutSnapshot;
                DrawableBmsHitObject note = player.ChildrenOfType<DrawableBmsHitObject>().Single(drawable => drawable.HitObject.GetType() == typeof(BmsHitObject));
                DrawableBmsHoldNote hold = player.ChildrenOfType<DrawableBmsHoldNote>().Single();
                DefaultBmsHudLayoutDisplay hud = player.ChildrenOfType<DefaultBmsHudLayoutDisplay>().Single();
                BmsHudLayoutSnapshotCarrier hudCarrier = player.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Single(carrier => ReferenceEquals(carrier.LayoutSnapshot, snapshot));
                BmsGaugeBar gauge = player.ChildrenOfType<BmsGaugeBar>().Single();
                BmsComboCounter combo = player.ChildrenOfType<BmsComboCounter>().Single();
                GameplaySkinLayoutRevisionOwner owner = ruleset.LayoutProvider.RevisionOwner!;
                GameplaySkinLayoutPublication publication = owner.CurrentPublication!;

                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.Context.PackageRevision.SourceKind, Is.Not.EqualTo(GameplaySkinPackageSourceKind.Compatibility));
                    Assert.That(snapshot.Context.PackageRevision, Is.SameAs(owner.PackageRevision));
                    Assert.That(publication.GetAdapter<BmsGameplayLayoutSnapshot>(), Is.SameAs(snapshot));
                    Assert.That(publication.Snapshot, Is.SameAs(snapshot.Neutral));
                    Assert.That(snapshot.Neutral, Is.SameAs(owner.Current));
                    Assert.That(ruleset.LayoutProvider.LastPrepareWasUpdateThread, Is.False, "Geometry preparation must stay off the update thread.");
                    Assert.That(ruleset.LayoutProvider.LastCommitWasUpdateThread, Is.True, "The prepared immutable reference must commit on the update thread.");
                    Assert.That(ruleset.Playfield.LayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(ruleset.Playfield.GroupContainers.All(group => ReferenceEquals(group.LayoutSnapshot, snapshot)), Is.True);
                    Assert.That(ruleset.Playfield.Lanes.All(lane => ReferenceEquals(lane.LayoutSnapshot, snapshot)), Is.True);
                    Assert.That(ruleset.Playfield.Lanes.All(lane => ReferenceEquals(lane.HitTarget.LayoutSnapshot, snapshot)), Is.True);
                    Assert.That(ruleset.Playfield.LaneCovers.All(cover => ReferenceEquals(cover.LayoutSnapshot, snapshot)), Is.True);
                    Assert.That(ruleset.Playfield.BarLinePlayfields.SelectMany(owner => owner.AllHitObjects).OfType<DrawableBmsBarLine>().All(barLine => ReferenceEquals(barLine.LayoutSnapshot, snapshot)), Is.True);
                    Assert.That(note.ExactLayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(hold.ExactLayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(hold.ChildrenOfType<DrawableBmsHoldNoteHead>().All(head => ReferenceEquals(head.ExactLayoutSnapshot, snapshot)), Is.True);
                    Assert.That(hold.ChildrenOfType<DrawableBmsHoldNoteTail>().All(tail => ReferenceEquals(tail.ExactLayoutSnapshot, snapshot)), Is.True);
                    Assert.That(ruleset.PreStartSpeedPreviewLayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(ruleset.BgaLayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(ruleset.HudLayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(hudCarrier.LayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(hud.LayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(gauge.LayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(combo.LayoutSnapshot, Is.SameAs(snapshot));
                });

                return true;
            });
        }

        [Test]
        public void TestGameplayStartsAfterDelayWithoutHold()
        {
            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay stays hidden", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("pre-start speed preview hidden", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());

            AddUntilStep("gameplay starts after the delay", () => player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay remains hidden after auto-start", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("pre-start speed preview remains hidden after auto-start", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);
        }

        [Test]
        public void TestPreStartSpeedPreviewUsesFirstNonScratchLaneOnlyWhileHolding()
        {
            int expectedLaneIndex = -1;

            AddStep("capture first non-scratch lane", () => expectedLaneIndex = player.DrawableBmsRuleset!.Playfield.Lanes.First(lane => !lane.IsScratch).LaneIndex);
            AddAssert("pre-start speed preview hidden initially", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("pre-start speed preview visible while holding", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == true);
            AddAssert("pre-start speed preview targets first non-scratch lane", () => player.DrawableBmsRuleset!.PreStartSpeedPreviewLaneIndex == expectedLaneIndex);

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("pre-start speed preview hidden after release", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);
        }

        [Test]
        public void TestPreStartSpeedPreviewAnimatesAndFreezesDuringPause()
        {
            float initialProgress = 0;
            float pausedProgress = 0;

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("pre-start speed preview visible", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == true);

            AddStep("capture initial preview progress", () => initialProgress = player.DrawableBmsRuleset!.PreStartSpeedPreviewProgress);
            AddUntilStep("preview progress advances while holding", () => Math.Abs(player.DrawableBmsRuleset!.PreStartSpeedPreviewProgress - initialProgress) > 0.02f);

            AddStep("pause during pre-start hold", () => Assert.That(player.Pause(), Is.True));
            AddUntilStep("preview marked paused", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewPaused == true);
            AddStep("capture paused preview progress", () => pausedProgress = player.DrawableBmsRuleset!.PreStartSpeedPreviewProgress);
            AddWaitStep("wait while paused", 10);
            AddAssert("preview progress stays stable while paused", () => Math.Abs(player.DrawableBmsRuleset!.PreStartSpeedPreviewProgress - pausedProgress) <= 0.005f);

            AddStep("resume from pause", () => player.Resume());
            AddUntilStep("preview unpaused", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewPaused == false);
            AddUntilStep("preview progress advances again after resume", () => Math.Abs(player.DrawableBmsRuleset!.PreStartSpeedPreviewProgress - pausedProgress) > 0.02f);
        }

        [Test]
        public void TestPreStartSpeedPreviewDoesNotReturnDuringActiveGameplayHold()
        {
            AddAssert("pre-start speed preview hidden initially", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay", () => player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start speed preview hidden after gameplay starts", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);

            AddStep("press pre-start hold during gameplay", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddAssert("pre-start overlay stays hidden during gameplay hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("pre-start speed preview stays hidden during gameplay hold", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);

            AddStep("release pre-start hold during gameplay", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddAssert("pre-start speed preview remains hidden after gameplay hold release", () => player.DrawableBmsRuleset?.IsPreStartSpeedPreviewVisible == false);
        }

        [Test]
        public void TestHoldBlocksGameplayStartAndEnablesPreStartAdjustments()
        {
            bool adjustedLaneCoverWhilePaused = false;
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;
            float initialCover = 0;

            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
                initialCover = player.DrawableBmsRuleset.SuddenUnits;
            });

            AddStep("try paused lane cover adjustment without hold", () => adjustedLaneCoverWhilePaused = player.DrawableBmsRuleset!.AdjustLaneCover(1));
            AddAssert("paused lane cover adjustment is blocked before hold", () => !adjustedLaneCoverWhilePaused && player.DrawableBmsRuleset!.SuddenUnits == initialCover);

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("raise hi-speed with an even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True));
            AddStep("release even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True));
            AddAssert("overlay-driven hi-speed adjustment succeeds", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("lower hi-speed with an odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddStep("release odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddAssert("overlay-driven hi-speed decrease succeeds", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - initialHiSpeed) <= 0.0001);

            AddStep("adjust lane cover while hold is active", () => adjustedLaneCoverWhilePaused = player.DrawableBmsRuleset!.AdjustLaneCover(1));
            AddAssert("paused lane cover adjustment succeeds during hold", () => adjustedLaneCoverWhilePaused && player.DrawableBmsRuleset!.SuddenUnits == initialCover + 1);

            AddStep("expire the pre-start delay while still holding", () => player.ExpirePreStartDelay());
            AddAssert("gameplay stays held while hold remains active", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after releasing hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay still waits for a fresh delay after releasing hold", () => !player.GameplayClockContainer.IsRunning);
            AddStep("expire refreshed pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after refreshed delay elapses", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestReleasingHoldBeforeDelayStillWaitsForDelayedStart()
        {
            double initialHiSpeed = 0;

            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddStep("capture initial hi-speed", () => initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value);

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("release pre-start hold before delay elapses", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after early release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("underlying clock still not running", () => !player.GameplayClockContainer.IsRunning);
            AddStep("try raising hi-speed after early release", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True));
            AddStep("release even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True));
            AddAssert("hi-speed stays unchanged after early release", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - initialHiSpeed) <= 0.0001);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts only after delay expires", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestPressingHoldAgainResetsPreStartDelay()
        {
            AddAssert("one initial pre-start delay scheduled", () => player.ScheduledPreStartDelayCount == 1);
            AddAssert("no initial pre-start delays are cancelled", () => player.CancelledScheduledPreStartDelayCount == 0);

            AddStep("press pre-start hold once", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible after first hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);
            AddAssert("first hold reschedules delay", () => player.ScheduledPreStartDelayCount == 2 && player.CancelledScheduledPreStartDelayCount == 1);

            AddStep("release first hold before delay elapses", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after first release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay still waits after first release", () => !player.GameplayClockContainer.IsRunning);

            AddStep("press pre-start hold again", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible after second hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);
            AddAssert("second hold reschedules delay again", () => player.ScheduledPreStartDelayCount == 3 && player.CancelledScheduledPreStartDelayCount == 2);

            AddStep("expire the oldest scheduled delay", () => player.ExpireNextScheduledPreStartDelay());
            AddAssert("oldest cancelled delay does not start gameplay", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("latest delay still pending after oldest expiry", () => player.HasPendingActivePreStartDelay);

            AddStep("expire the second scheduled delay", () => player.ExpireNextScheduledPreStartDelay());
            AddAssert("second cancelled delay does not start gameplay", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("latest delay still pending after second expiry", () => player.HasPendingActivePreStartDelay);

            AddStep("expire the latest scheduled delay while hold remains active", () => player.ExpirePreStartDelay());
            AddAssert("latest delay elapsed but gameplay stays held", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release second hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after second release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("release after elapsed hold schedules one fresh pending delay", () => player.ScheduledPreStartDelayCount == 1 && player.CancelledScheduledPreStartDelayCount == 0);
            AddAssert("gameplay still waits after second release", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("fresh delay is pending after second release", () => player.HasPendingActivePreStartDelay);

            AddStep("expire the refreshed delay after second release", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts only after refreshed delay elapses", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestUnexpectedClockStartIsSuppressedUntilDelayExpires()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay stays hidden", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("force gameplay clock start while pre-start is pending", () => player.ForceGameplayClockStart());
            AddAssert("underlying clock still not running after external start attempt", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay remains hidden after suppression", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestPauseDuringPreStartPreventsGameplayStartingUnderPauseOverlay()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pause overlay hidden initially", () => player.PauseOverlayState == Visibility.Hidden);

            AddStep("pause during pre-start", () => Assert.That(player.Pause(), Is.True));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("expire the pre-start delay while paused", () => player.ExpirePreStartDelay());
            AddAssert("gameplay does not start under pause overlay", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pause overlay remains visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("resume from pause", () => player.Resume());
            AddUntilStep("gameplay starts after resume", () => player.GameplayClockContainer.IsRunning);
            AddUntilStep("pause overlay hidden after resume", () => player.PauseOverlayState == Visibility.Hidden);
        }

        [Test]
        public void TestKeyboardPreStartHoldBindingStillBlocksDelayedGameplayStart()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay hidden initially", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("press keyboard pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Q), Is.True));
            AddAssert("input manager sees keyboard pre-start hold", () => player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay visible while keyboard hold active", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("expire the pre-start delay while keyboard hold is active", () => player.ExpirePreStartDelay());
            AddAssert("gameplay stays held by keyboard pre-start hold", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release keyboard pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.Q), Is.True));
            AddAssert("input manager clears keyboard pre-start hold", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay hidden after keyboard hold release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay still waits for refreshed delay after keyboard hold release", () => !player.GameplayClockContainer.IsRunning);
            AddStep("expire refreshed pre-start delay after keyboard release", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after refreshed delay elapses", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestKeyboardPreStartHoldStillEnablesHiSpeedAdjustment()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;

            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
            });

            AddStep("press keyboard pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Q), Is.True));
            AddUntilStep("overlay visible while keyboard hold active", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("raise hi-speed with keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.X), Is.True));
            AddStep("release keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.X), Is.True));
            AddAssert("keyboard hold enables hi-speed increase", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("lower hi-speed with second keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Z), Is.True));
            AddStep("release second keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.Z), Is.True));
            AddAssert("keyboard hold enables hi-speed decrease", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - initialHiSpeed) <= 0.0001);
        }

        [Test]
        public void TestManualKeyboardPreStartHoldWorksThroughScreenInput()
        {
            AddAssert("pre-start overlay hidden initially", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("press Q via manual input", () => InputManager.PressKey(Key.Q));
            AddUntilStep("manual input reaches pre-start hold", () => player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay visible from manual pre-start hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("release Q via manual input", () => InputManager.ReleaseKey(Key.Q));
            AddUntilStep("manual input clears pre-start hold", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay hidden after manual release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestManualKeyboardPreStartHoldStillWorksWhilePausedDuringPreStart()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;

            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
            });

            AddStep("pause via escape", () => InputManager.Key(Key.Escape));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("press Q via manual input while paused", () => InputManager.PressKey(Key.Q));
            AddUntilStep("manual input reaches pre-start hold while paused", () => player.GameplayInputManager!.PreStartHoldPressed.Value);

            AddStep("press X via manual input while paused", () => InputManager.Key(Key.X));
            AddAssert("manual paused pre-start hold still adjusts hi-speed", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("release Q via manual input while paused", () => InputManager.ReleaseKey(Key.Q));
            AddUntilStep("manual paused pre-start hold clears on release", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
        }

        [Test]
        public void TestResumeDuringPreStartRestoresInteractivePreStartState()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("pause during pre-start", () => Assert.That(player.Pause(), Is.True));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("resume before delay expires", () => player.Resume());
            AddUntilStep("pause overlay hidden", () => player.PauseOverlayState == Visibility.Hidden);
            AddUntilStep("pre-start state restored", () => !player.GameplayClockContainer.IsPaused.Value && !player.GameplayClockContainer.IsRunning);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestKeyboardPreStartHoldStillWorksAfterPauseResumeDuringPreStart()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("pause during pre-start", () => Assert.That(player.Pause(), Is.True));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("resume before delay expires", () => player.Resume());
            AddUntilStep("pre-start state restored after resume", () => !player.GameplayClockContainer.IsPaused.Value && !player.GameplayClockContainer.IsRunning);

            AddStep("press keyboard pre-start hold after resume", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Q), Is.True));
            AddAssert("input manager sees keyboard pre-start hold after resume", () => player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay visible after resume hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("expire the pre-start delay while holding after resume", () => player.ExpirePreStartDelay());
            AddAssert("gameplay stays held after resume while keyboard hold remains active", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release keyboard pre-start hold after resume", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.Q), Is.True));
            AddAssert("input manager clears keyboard pre-start hold after resume", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay hidden after releasing post-resume hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay still waits after releasing post-resume hold", () => !player.GameplayClockContainer.IsRunning);
            AddStep("expire refreshed pre-start delay after post-resume release", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after refreshed post-resume delay", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestGameplayStartResetsTrackEvenIfSourceTrackWasAdvanced()
        {
            double simulatedPreviewProgress = 0;

            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("simulate source track already being at preview progress", () =>
            {
                simulatedPreviewProgress = Math.Min(800, Beatmap.Value.Track.Length * 0.8);
                Beatmap.Value.Track.Seek(simulatedPreviewProgress);
            });

            AddAssert("source track advanced away from start time", () => Math.Abs(Beatmap.Value.Track.CurrentTime - player.GameplayClockContainer.StartTime) > 100);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);
            AddAssert("gameplay clock is closer to start time than simulated preview progress", () =>
            {
                double currentTime = player.GameplayClockContainer.CurrentTime;
                double distanceFromStart = Math.Abs(currentTime - player.GameplayClockContainer.StartTime);
                double distanceFromPreview = Math.Abs(currentTime - simulatedPreviewProgress);
                return distanceFromStart < distanceFromPreview;
            });
            AddAssert("gameplay clock did not continue from simulated preview progress",
                () => player.GameplayClockContainer.CurrentTime,
                () => Is.LessThan(simulatedPreviewProgress - 100));
        }

        [Test]
        public void TestFocusCyclesPersistentTargetWhenStartIsReleased()
        {
            selectedMods = new Mod[] { new BmsModSudden(), new BmsModHidden(), new BmsModLift() };

            AddAssert("three adjustment targets available", () => player.DrawableBmsRuleset?.EnabledAdjustmentTargetCount.Value == 3);
            AddAssert("persistent target starts at sudden", () => player.DrawableBmsRuleset?.ActiveAdjustmentTarget.Value == BmsGameplayAdjustmentTarget.Sudden);

            AddAssert("displayed target remains at persistent (sudden)", () => hasTargetState(BmsGameplayAdjustmentTarget.Sudden, 0, false));
            AddStep("hold and release Start before entering gameplay", () =>
            {
                Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True);
                Assert.That(player.GameplayInputManager.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True);
                player.ExpirePreStartDelay();
            });
            AddUntilStep("the actual gameplay clock is running", () => player.GameplayClockContainer.IsRunning);

            AddStep("press lane cover focus to cycle to hidden", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target cycled to hidden", () => hasTargetState(BmsGameplayAdjustmentTarget.Hidden, 1, false));
            AddStep("release lane cover focus", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target stays at hidden after release", () => hasTargetState(BmsGameplayAdjustmentTarget.Hidden, 1, false));

            AddStep("press lane cover focus to cycle to lift", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target cycled to lift", () => hasLiftPersistentTargetState());
            AddStep("release lane cover focus", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target stays at lift after release", () => hasLiftPersistentTargetState());

            AddStep("press lane cover focus to cycle back to sudden", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target cycled back to sudden", () => hasTargetState(BmsGameplayAdjustmentTarget.Sudden, 0, false));
            AddStep("release lane cover focus", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True));

            AddAssert("Start remains released while gameplay continues", () => !player.GameplayInputManager!.PreStartHoldPressed.Value
                && !player.DrawableBmsRuleset!.AdjustmentHeld && player.GameplayClockContainer.IsRunning);
            AddAssert("cycling targets keeps the saved reading amounts", () =>
            {
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;
                BmsReadingSettingsSnapshot state = config.CaptureReadingSettings();
                return config.Get<bool>(BmsRulesetSetting.RememberReadingChanges)
                       && state.SuddenUnits == 250 && state.HiddenUnits == 250 && state.LiftUnits == 250;
            });
        }

        [Test]
        public void TestHoldKeepsHiSpeedAdjustmentsEnabledAfterDelayExpires()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;

            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
            });

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("expire the pre-start delay while still holding", () => player.ExpirePreStartDelay());
            AddAssert("gameplay still blocked while holding", () => !player.GameplayClockContainer.IsRunning);

            AddStep("raise hi-speed after delay has elapsed", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True));
            AddStep("release even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True));
            AddAssert("hi-speed still adjusts after delay elapsed", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after releasing hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay still waits after releasing hold", () => !player.GameplayClockContainer.IsRunning);
            AddStep("expire refreshed pre-start delay after release", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after refreshed delay elapses", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestHoldEnablesInGameAdjustmentsAndPublishesState()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);

            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
            });

            AddStep("press ingame start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddAssert("pre-start overlay stays hidden during gameplay hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay adjustment state is held", () => player.DrawableBmsRuleset!.AdjustmentHeld);

            AddStep("raise hi-speed with even lane key during gameplay", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True));
            AddStep("release even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True));
            AddAssert("hi-speed increases during gameplay hold", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);
            AddAssert("gameplay adjustment state remains held", () => player.DrawableBmsRuleset!.AdjustmentHeld);

            AddStep("release ingame start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
        }

        [Test]
        public void TestPreStartOverlayReflectsHiSpeedModeAndValueInRealPlayerFlow()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;
            BmsHiSpeedMode hiSpeedMode = default;

            AddStep("capture hi-speed surface", () =>
            {
                hiSpeedMode = player.DrawableBmsRuleset!.HiSpeedMode.Value;
                initialHiSpeed = player.DrawableBmsRuleset.SelectedHiSpeed.Value;
                hiSpeedStep = hiSpeedMode.GetAdjustmentStep();
            });

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);
            AddAssert("overlay mode text matches current mode", () => hasOverlayText(getOverlayModeText(hiSpeedMode)));
            AddAssert("overlay value text matches current hi-speed", () => hasOverlayText(hiSpeedMode.FormatValue(initialHiSpeed)));

            AddStep("raise hi-speed with even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True));
            AddStep("release even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True));
            AddAssert("overlay value text updates after increase", () => hasOverlayText(hiSpeedMode.FormatValue(initialHiSpeed + hiSpeedStep)));

            AddStep("lower hi-speed with odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddStep("release odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddAssert("overlay value text returns after decrease", () => hasOverlayText(hiSpeedMode.FormatValue(initialHiSpeed)));

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after releasing hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
        }

        [TestCase("none")]
        [TestCase("sudden")]
        [TestCase("hidden")]
        [TestCase("both")]
        [TestCase("lift")]
        [TestCase("lift-sudden")]
        public void TestDoubleShortStartTogglesTheSelectedCoversAndPreservesLift(string selection)
        {
            selectedMods = selection switch
            {
                "sudden" => new Mod[] { new BmsModSudden() },
                "hidden" => new Mod[] { new BmsModHidden() },
                "both" => new Mod[] { new BmsModSudden(), new BmsModHidden() },
                "lift" => new Mod[] { new BmsModLift() },
                "lift-sudden" => new Mod[] { new BmsModLift(), new BmsModSudden() },
                _ => Array.Empty<Mod>(),
            };
            bool initiallyCovered = selection is "sudden" or "hidden" or "both" or "lift-sudden";
            float initialLift = 0;
            AddStep("capture actual Lift geometry", () => initialLift = player.DrawableBmsRuleset!.LiftUnits);
            AddStep("double tap Start", () =>
            {
                tapStart(100);
                tapStart(200);
            });
            AddAssert("the selected cover combination switches together", () => initiallyCovered
                ? !player.DrawableBmsRuleset!.SuddenActive && !player.DrawableBmsRuleset.HiddenActive
                : player.DrawableBmsRuleset!.SuddenActive && !player.DrawableBmsRuleset.HiddenActive);
            AddAssert("Lift is retained", () => player.DrawableBmsRuleset!.LiftUnits == initialLift);
            AddStep("double tap Start again", () =>
            {
                tapStart(1000);
                tapStart(1100);
            });
            AddAssert("the same cover combination returns", () => initiallyCovered
                ? player.DrawableBmsRuleset!.SuddenActive == (selection != "hidden") && player.DrawableBmsRuleset.HiddenActive == (selection is "hidden" or "both")
                : !player.DrawableBmsRuleset!.SuddenActive && !player.DrawableBmsRuleset.HiddenActive);
            AddAssert("configured amounts and Lift survive toggling", () => player.DrawableBmsRuleset!.LiftUnits == initialLift
                && (selection == "hidden" || player.DrawableBmsRuleset.SuddenUnits == 250));
        }

        [Test]
        public void TestSecondStartPressTogglesImmediatelyAndCanRemainHeld()
        {
            AddStep("short tap then press Start again", () =>
            {
                tapStart(100);
                player.GestureTime = 220;
                player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold);
            });
            AddAssert("the cover switches before the second press is released", () => !player.DrawableBmsRuleset!.SuddenActive
                && player.DrawableBmsRuleset.AdjustmentHeld);
            AddStep("release the long second press", () =>
            {
                player.GestureTime = 1220;
                player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold);
                tapStart(1300);
            });
            AddAssert("the second release cannot become a third tap", () => !player.DrawableBmsRuleset!.SuddenActive);
        }

        [Test]
        public void TestLongStartAndInputResetCannotCompleteADoubleTap()
        {
            AddStep("hold Start beyond the short-press window", () =>
            {
                player.GestureTime = 100;
                player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold);
                player.GestureTime = 1000;
                player.GameplayInputManager.TriggerOmsActionReleased(OmsAction.UI_PreStartHold);
                tapStart(1100);
            });
            AddAssert("a long hold followed by a short tap leaves Sudden enabled", () => player.DrawableBmsRuleset!.SuddenActive);
            AddStep("invalidate pending tap input", () => ((IFrameStableInputHandler)player.GameplayInputManager!).ResetInput());
            AddWaitStep("deliver the reset", 2);
            AddStep("make one short tap in the new input epoch", () => tapStart(1200));
            AddAssert("a tap cannot pair across input reset", () => player.DrawableBmsRuleset!.SuddenActive);
        }

        [TestCase(BmsHiSpeedMode.Normal)]
        [TestCase(BmsHiSpeedMode.Classic)]
        public void TestStartEffectSwitchesSpeedModeWithoutCyclingTheCoverTarget(BmsHiSpeedMode initialMode)
        {
            initialHiSpeedMode = initialMode;
            selectedMods = new Mod[] { new BmsModSudden(), new BmsModHidden(), new BmsModLift() };
            double previousTime = 0;
            double floatingSpeed = 0;
            AddStep("hold Start in the configured base mode", () =>
            {
                player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold);
                Assert.That(player.DrawableBmsRuleset!.HiSpeedMode.Value, Is.EqualTo(initialMode));
            });
            AddStep("capture current visible time", () => previousTime = player.DrawableBmsRuleset!.GetScrollSpeedMetrics().VisibleLaneTime);
            AddStep("press EFFECT while holding", () => player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus));
            AddAssert("Floating is selected and the previous scrolling speed is retained", () => player.DrawableBmsRuleset!.HiSpeedMode.Value == BmsHiSpeedMode.Floating
                && Math.Abs(player.DrawableBmsRuleset.GetScrollSpeedMetrics().VisibleLaneTime - previousTime) < 1);
            AddAssert("the persistent cover target stays Sudden", () => player.DrawableBmsRuleset!.ActiveAdjustmentTarget.Value == BmsGameplayAdjustmentTarget.Sudden);
            AddStep("release EFFECT", () => player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus));
            AddStep("capture Floating speed before the white key", () => floatingSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value);
            AddStep("lower Floating speed with a white key", () => player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1));
            AddStep("release the white key", () => player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1));
            AddAssert("the actual Floating adjustment is applied", () => player.DrawableBmsRuleset!.SelectedHiSpeed.Value,
                () => Is.EqualTo(floatingSpeed - 0.5).Within(0.000001));
            AddStep("capture adjusted visible time", () => previousTime = player.DrawableBmsRuleset!.GetScrollSpeedMetrics().VisibleLaneTime);
            AddStep("press EFFECT again", () => player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus));
            AddAssert("the same base mode returns with its nearest legal selection", () => player.DrawableBmsRuleset!.HiSpeedMode.Value == initialMode
                && (initialMode == BmsHiSpeedMode.Normal
                    // With two250 covers and250 Lift the edited multiplier exceeds the fastest visible preset.
                    ? player.DrawableBmsRuleset.SelectedHiSpeed.Value == 20
                    : Math.Abs(player.DrawableBmsRuleset.GetScrollSpeedMetrics().VisibleLaneTime - previousTime) < 1));
            AddStep("release EFFECT and Start", () =>
            {
                player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus);
                player.GameplayInputManager.TriggerOmsActionReleased(OmsAction.UI_PreStartHold);
            });
            AddAssert("the mode chord did not toggle the covers", () => player.DrawableBmsRuleset!.SuddenActive && player.DrawableBmsRuleset.HiddenActive);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestClassicLiveKeysTraverseAllElevenChoicesAndSaturate(bool dualPlay)
        {
            initialHiSpeedMode = BmsHiSpeedMode.Classic;
            sourceOverride = createSourceBeatmap("#TITLE Classic live choices\n#BPM 130\n#RANK 2\n"
                + (dualPlay ? "#PLAYER 3\n#00319:AA\n#00329:AA\n" : "#00319:AA\n") + "#WAVAA key1.wav\n", "classic-live-choices.bme");
            OmsAction lower = dualPlay ? OmsAction.Key2P_1 : OmsAction.Key1P_1;
            OmsAction raise = dualPlay ? OmsAction.Key2P_2 : OmsAction.Key1P_2;
            double[] choices = { 1, 1.5, 2, 2.25, 2.5, 2.75, 3, 3.25, 3.5, 3.75, 4 };
            AddStep("hold actual Start", () => player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
            AddStep("start at the lowest Classic choice", () =>
            {
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;
                config.SetValue(BmsRulesetSetting.ClassicHiSpeed, 1d);
                Assert.That(player.DrawableBmsRuleset!.Variant, Is.EqualTo(dualPlay ? 16 : 8));
                Assert.That(player.DrawableBmsRuleset.SelectedHiSpeed.Value, Is.EqualTo(1));
            });
            foreach (double value in choices.Skip(1))
                pressKey(raise, value);
            pressKey(raise, 4);
            foreach (double value in choices.Reverse().Skip(1))
                pressKey(lower, value);
            pressKey(lower, 1);
            AddStep("release Start", () => player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));

            void pressKey(OmsAction action, double expected)
            {
                AddStep($"press {action} toward {expected}", () => player.GameplayInputManager!.TriggerOmsActionPressed(action));
                AddStep("release adjustment key", () => player.GameplayInputManager!.TriggerOmsActionReleased(action));
                AddAssert("native Classic speed matches the next choice", () => player.DrawableBmsRuleset!.SelectedHiSpeed.Value, () => Is.EqualTo(expected));
                AddAssert("READY HOLD shows the actual selected value", () => hasOverlayText(BmsHiSpeedMode.Classic.FormatValue(expected)));
            }
        }

        [TestCase(0.5, -1, 0.5)]
        [TestCase(0.5, 1, 1)]
        [TestCase(1.25, -1, 1)]
        [TestCase(1.25, 1, 1.5)]
        [TestCase(1.75, -1, 1.5)]
        [TestCase(1.75, 1, 2)]
        [TestCase(4.25, -1, 4)]
        [TestCase(4.25, 1, 4.25)]
        [TestCase(10, -1, 4)]
        [TestCase(10, 1, 10)]
        public void TestClassicLiveKeysEnterTheListOnlyInTheRequestedDirection(double initial, int direction, double expected)
        {
            initialHiSpeedMode = BmsHiSpeedMode.Classic;
            OmsAction action = direction > 0 ? OmsAction.Key1P_2 : OmsAction.Key1P_1;
            AddStep("hold actual Start", () => player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
            AddStep("load a supported old Classic value", () =>
            {
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;
                config.SetValue(BmsRulesetSetting.ClassicHiSpeed, initial);
                Assert.That(player.DrawableBmsRuleset!.SelectedHiSpeed.Value, Is.EqualTo(initial));
            });
            AddStep("press the actual white or black key", () => player.GameplayInputManager!.TriggerOmsActionPressed(action));
            AddStep("release adjustment key", () => player.GameplayInputManager!.TriggerOmsActionReleased(action));
            AddAssert("the old value moves only in the requested direction", () => player.DrawableBmsRuleset!.SelectedHiSpeed.Value, () => Is.EqualTo(expected));
            AddAssert("READY HOLD retains the actual value even outside the list", () => hasOverlayText(BmsHiSpeedMode.Classic.FormatValue(expected)));
            AddStep("other mode values retain their independent settings", () =>
            {
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;
                Assert.That(config.Get<double>(BmsRulesetSetting.ScrollSpeed), Is.EqualTo(8));
                Assert.That(config.Get<double>(BmsRulesetSetting.FloatingHiSpeed), Is.EqualTo(2.5));
            });
            AddStep("release Start", () => player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));
        }

        [Test]
        public void TestStartEffectFromInitialFloatingReturnsToNormal()
        {
            initialHiSpeedMode = BmsHiSpeedMode.Floating;
            AddStep("hold Start in initial Floating", () => player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
            AddAssert("Floating is the opening mode", () => player.DrawableBmsRuleset!.HiSpeedMode.Value, () => Is.EqualTo(BmsHiSpeedMode.Floating));
            AddStep("press EFFECT without a prior base mode", () => player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus));
            AddAssert("the existing default return mode is Normal", () => player.DrawableBmsRuleset!.HiSpeedMode.Value, () => Is.EqualTo(BmsHiSpeedMode.Normal));
            AddStep("release EFFECT and Start", () =>
            {
                player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus);
                player.GameplayInputManager.TriggerOmsActionReleased(OmsAction.UI_PreStartHold);
            });
        }

        [Test]
        public void TestSavedFloatingRefloatsTheOpeningTargetAfterTheBpmChanges()
        {
            initialHiSpeedMode = BmsHiSpeedMode.Floating;
            sourceOverride = createSourceBeatmap(@"
#TITLE Saved Floating With BPM Change
#BPM 100
#BPM01 200
#00111:01
#00208:01
#00611:01
", "saved-floating.bme");
            double openingVisibleTime = 0;
            AddUntilStep("the opening native geometry is applied", () => player.DrawableBmsRuleset!.Playfield.GameplaySkinLiftState.IsAvailable);
            AddStep("remember the initial covered time without any Start interaction", () =>
            {
                Assert.That(player.DrawableBmsRuleset!.HiSpeedMode.Value, Is.EqualTo(BmsHiSpeedMode.Floating));
                openingVisibleTime = player.DrawableBmsRuleset.SpeedMetrics.Value.VisibleLaneTime;
                Assert.That(openingVisibleTime, Is.GreaterThan(0));
                player.ExpirePreStartDelay();
            });
            AddUntilStep("gameplay starts", () => player.GameplayClockContainer.IsRunning);
            AddStep("pass the real 100 to 200 BPM timing point", () => player.GameplayClockContainer.Seek(5000));
            AddUntilStep("the live green number follows the doubled BPM", () =>
                Math.Abs(player.DrawableBmsRuleset!.SpeedMetrics.Value.VisibleLaneTime - openingVisibleTime / 2) < 1);
            AddStep("make the first Start scratch adjustment", () =>
            {
                player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold);
                Assert.That(player.DrawableBmsRuleset!.AdjustFromScratch(new OmsAxisMotion(
                    OmsAction.Key1P_Scratch, OmsAxisMode.RelativeMotion, 4, "mouse", null, 50)), Is.True);
            });
            AddAssert("the first scratch restores the saved opening target", () =>
                Math.Abs(player.DrawableBmsRuleset!.SpeedMetrics.Value.VisibleLaneTime - openingVisibleTime) < openingVisibleTime * 0.005);
            AddStep("release Start", () => player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));
        }

        [Test]
        public void TestFloatingScratchLocksVisibleTimeAndRestoresItAfterLiftChanges()
        {
            selectedMods = new Mod[] { new BmsModSudden(), new BmsModLift() };
            double lockedTime = 0;
            double speedWithCover = 0;
            float originalLift = 0;
            AddStep("hold Start and enable Floating", () =>
            {
                player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold);
                if (player.DrawableBmsRuleset!.HiSpeedMode.Value != BmsHiSpeedMode.Floating)
                    player.DrawableBmsRuleset.ToggleHiSpeedMode();
                lockedTime = player.DrawableBmsRuleset.GetScrollSpeedMetrics().VisibleLaneTime;
                originalLift = player.DrawableBmsRuleset.LiftUnits;
            });
            AddStep("turn the scratch with Sudden enabled", () => player.DrawableBmsRuleset!.AdjustFromScratch(
                new OmsAxisMotion(OmsAction.Key1P_Scratch, OmsAxisMode.RelativeMotion, 80, "mouse", null, 50)));
            AddAssert("Sudden moves while the visible time stays anchored", () => player.DrawableBmsRuleset!.SuddenUnits == 270
                && Math.Abs(player.DrawableBmsRuleset.GetScrollSpeedMetrics().VisibleLaneTime - lockedTime) < 1);
            AddStep("switch Sudden off and keep the current speed", () =>
            {
                speedWithCover = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                player.DrawableBmsRuleset.ToggleGameplayLaneCovers();
                player.DrawableBmsRuleset.AdjustFromScratch(new OmsAxisMotion(OmsAction.Key1P_Scratch, OmsAxisMode.RelativeMotion, 80, "mouse", null, 50));
            });
            AddUntilStep("positive scratch lowers Lift while Sudden is off", () => player.DrawableBmsRuleset!.LiftUnits == originalLift - 20);
            AddAssert("Lift adjustment keeps the speed value", () => player.DrawableBmsRuleset!.SelectedHiSpeed.Value == speedWithCover);
            AddStep("switch Sudden back on", () => player.DrawableBmsRuleset!.ToggleGameplayLaneCovers());
            AddAssert("the remembered covered time returns without changing speed", () => player.DrawableBmsRuleset!.SelectedHiSpeed.Value == speedWithCover
                && Math.Abs(player.DrawableBmsRuleset.GetScrollSpeedMetrics().VisibleLaneTime - lockedTime) < 2);
            AddStep("restore Normal mode", () => player.DrawableBmsRuleset!.ToggleHiSpeedMode());
            AddStep("release Start", () => player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));
        }

        private void tapStart(double time)
        {
            player.GestureTime = time;
            Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True);
            player.GestureTime = time + 40;
            Assert.That(player.GameplayInputManager.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True);
        }

        private BmsDecodedBeatmap createSourceBeatmap(string text, string path)
        {
            var beatmap = new BmsDecodedBeatmap(decoder.DecodeText(text, path))
            {
                BeatmapInfo =
                {
                    Ruleset = bms_ruleset_info,
                }
            };

            return beatmap;
        }

        private bool hasTargetState(BmsGameplayAdjustmentTarget expectedTarget, int expectedIndex, bool expectedTemporaryOverride)
        {
            if (player.DrawableBmsRuleset == null)
                return false;

            return player.DrawableBmsRuleset.ActiveAdjustmentTarget.Value == expectedTarget
                   && player.DrawableBmsRuleset.ActiveAdjustmentTargetIndex.Value == expectedIndex
                   && player.DrawableBmsRuleset.IsAdjustmentTargetTemporarilyOverridden.Value == expectedTemporaryOverride;
        }

        private bool hasLiftPersistentTargetState()
        {
            if (player.DrawableBmsRuleset == null)
                return false;

            return player.DrawableBmsRuleset.ActiveAdjustmentTarget.Value == BmsGameplayAdjustmentTarget.Lift
                   && player.DrawableBmsRuleset.ActiveAdjustmentTargetIndex.Value == 2
                   && !player.DrawableBmsRuleset.IsAdjustmentTargetTemporarilyOverridden.Value
                   && player.DrawableBmsRuleset.Playfield.LaneCovers.All(cover => !cover.IsFocused.Value);
        }

        private bool hasOverlayText(string text)
            => tryGetOverlayText(text) != null;

        private OsuSpriteText? tryGetOverlayText(string text)
            => player.PreStartOverlay?.ChildrenOfType<OsuSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);

        private static string getOverlayModeText(BmsHiSpeedMode mode)
            => mode switch
            {
                BmsHiSpeedMode.Normal => @"Normal Hi-Speed",
                BmsHiSpeedMode.Floating => @"Floating Hi-Speed",
                BmsHiSpeedMode.Classic => @"Classic Hi-Speed",
                _ => @"Hi-Speed",
            };

        private partial class TestBmsSoloPlayer : BmsSoloPlayer
        {
            private readonly List<(ScheduledDelegate Delegate, Action Callback)> scheduledPreStartDelays = new List<(ScheduledDelegate Delegate, Action Callback)>();

            public DrawableBmsRuleset? DrawableBmsRuleset => DrawableRuleset as DrawableBmsRuleset;

            public double? GestureTime { get; set; }

            protected override double AdjustmentGestureTime => GestureTime ?? base.AdjustmentGestureTime;

            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            public BmsInputManager? GameplayInputManager => DrawableBmsRuleset?.GameplayInputManager;

            public BmsPreStartHiSpeedOverlay? PreStartOverlay => PreStartHiSpeedOverlay;

            public Visibility PauseOverlayState => PauseOverlay.State.Value;

            public int ScheduledPreStartDelayCount => scheduledPreStartDelays.Count;

            public int CancelledScheduledPreStartDelayCount => scheduledPreStartDelays.Count(entry => entry.Delegate.Cancelled);

            public bool HasPendingActivePreStartDelay => scheduledPreStartDelays.Any(entry => !entry.Delegate.Cancelled);

            public void ForceGameplayClockStart() => GameplayClockContainer.Start();

            public void ExpirePreStartDelay()
            {
                int activeIndex = scheduledPreStartDelays.FindLastIndex(entry => !entry.Delegate.Cancelled);

                if (activeIndex < 0)
                    return;

                var scheduledDelay = scheduledPreStartDelays[activeIndex];
                scheduledPreStartDelays.RemoveAt(activeIndex);
                scheduledDelay.Callback();
            }

            public void ExpireNextScheduledPreStartDelay()
            {
                if (scheduledPreStartDelays.Count == 0)
                    return;

                var scheduledDelay = scheduledPreStartDelays[0];
                scheduledPreStartDelays.RemoveAt(0);

                if (!scheduledDelay.Delegate.Cancelled)
                    scheduledDelay.Callback();
            }

            protected override ScheduledDelegate SchedulePreStartDelay(Action onElapsed)
            {
                var scheduledDelay = Scheduler.AddDelayed(() => { }, double.MaxValue);
                scheduledPreStartDelays.Add((scheduledDelay, onElapsed));
                return scheduledDelay;
            }

            protected override Task ImportScore(Score score) => Task.CompletedTask;
        }
    }
}
