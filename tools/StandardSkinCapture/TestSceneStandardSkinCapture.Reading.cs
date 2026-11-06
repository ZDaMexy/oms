// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Tools.StandardSkinCapture;

public partial class TestSceneStandardSkinCapture
{
    private static bool IsReadingCase => CaptureSettings.Current.ChartVariant == "reading";
    private bool UsesEarlierReadingSource => CaptureSettings.Current.Options.TryGetValue("frame", out string? frame) && frame is "iidx23" or "iidx24";
    private readonly Stopwatch readingWaitWatch = new();
    private object? initialReadingWaitObservation;
    private bool readingWaitDiagnosticWritten;

    private void ConfigureReadingCase()
    {
        var config = (BmsRulesetConfigManager)Game.Dependencies.Get<IRulesetConfigCache>().GetConfigFor(ruleset)!;
        config.SetValue(BmsRulesetSetting.RememberReadingChanges, false);
        config.SetValue(BmsRulesetSetting.HiSpeedMode, Enum.Parse<BmsHiSpeedMode>(settings.ReadingMode, true));
        config.SetValue(BmsRulesetSetting.ScrollSpeed, 8d);
        config.SetValue(BmsRulesetSetting.FloatingHiSpeed, 2.5d);
        config.SetValue(BmsRulesetSetting.ClassicHiSpeed, 3.5d);
        config.SetValue(BmsRulesetSetting.SuddenEnabled, true);
        // The 26 source offers SUDDEN+HIDDEN and LIFT+SUDDEN separately.
        // Exercise both real source combinations; Hidden+Lift is an OMS extension.
        bool hiddenCombination = settings.ReadingMode == "floating";
        config.SetValue(BmsRulesetSetting.HiddenEnabled, hiddenCombination);
        config.SetValue(BmsRulesetSetting.LiftEnabled, !hiddenCombination);
        config.SetValue(BmsRulesetSetting.LaneCoversEnabled, true);
        config.SetValue(BmsRulesetSetting.SuddenUnits, 125f);
        config.SetValue(BmsRulesetSetting.HiddenUnits, 75f);
        config.SetValue(BmsRulesetSetting.LiftUnits, 80f);
        settings.Write("reading-input-contract.json", new
        {
            requestedMode = settings.ReadingMode,
            requestedFrame = settings.Options.GetValueOrDefault("frame"),
            expectedAuthoredNormalSpeedPreview = !UsesEarlierReadingSource,
            expectedSourceFirstRowCue = !UsesEarlierReadingSource,
            nativePreStartPreview = "Without an authored rectangle, OMS retains its real lane preview in all three speed modes while Start is held before play.",
            sourceCoverCombination = hiddenCombination ? "SUDDEN+HIDDEN" : "LIFT+SUDDEN",
            initialNativeSettings = config.CaptureReadingSettings(),
            inputOwner = "Real BmsSoloPlayer, using BmsInputManager.TriggerOmsActionPressed/Released with UI_PreStartHold and Key1P_2",
            synthesizedNativeInput = true,
            physicalDeviceQualified = false,
            directlySetAdjustmentHeld = false,
            injectedSkinEventsOrNumbers = false,
            scoreInput = "Native generator supplies an input schedule only; ordinary BmsSoloPlayer receives Oms key/scratch actions and records the actual play, without Autoplay or replay ownership",
        });
    }

    private void AddReadingCaptures()
    {
        AddStep("hold Start through the real BMS input owner", () =>
        {
            var reading = (CaptureReadingPlayer)player;
            AssertReadingSourceCapabilities(reading);
            Assert.That(reading.NativeRuleset.HasReplayLoaded.Value, Is.False);
            Assert.That(reading.Input.UseParentInput, Is.True);
            Assert.That(reading.NativeRuleset.Mods.Any(mod => mod is ModAutoplay or ModNoFail), Is.False);
            Assert.That(reading.Input.PreStartHoldPressed.Value, Is.False, "No premature Start is sent from LoadComplete.");
            Assert.That(reading.Input.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True);
            readingWaitWatch.Restart();
            initialReadingWaitObservation = ReadingWaitObservation();
        });
        AddUntilStep("native Start panel and preparation are held", () =>
        {
            var reading = (CaptureReadingPlayer)player;
            var state = ReadState();
            bool complete = reading.Input.PreStartHoldPressed.Value && reading.NativeRuleset.AdjustmentHeld
                            && state.Timing.Preparation.Active && state.Timing.Preparation.Elapsed >= 300
                            && state.Timing.AdjustmentState.Adjusting;
            // One bounded diagnostic before the runner's ten-second timeout. Stopwatch is
            // only the probe wait duration; it never supplies gameplay or skin event time.
            if (!readingWaitDiagnosticWritten && (complete || readingWaitWatch.ElapsedMilliseconds >= 1500))
            {
                settings.Write("reading-start-wait-diagnostic.json", new
                {
                    gateSatisfied = complete,
                    diagnosticWaitMilliseconds = readingWaitWatch.ElapsedMilliseconds,
                    initial = initialReadingWaitObservation,
                    current = ReadingWaitObservation(),
                    inputObservations = reading.InputObservations.ToArray(),
                    diagnosticMutatesSourceState = false,
                });
                readingWaitDiagnosticWritten = true;
            }
            return complete;
        });
        AddReadingScreenshot("reading-held", held: true);
        if (UsesEarlierReadingSource)
            AddStep("the selected earlier source contributes no first-row cue", () => AssertReadingSourceCapabilities((CaptureReadingPlayer)player));
        else
            AddUntilStep("the source first-row cue is actually visible", () => ReadingNodes().Any(node =>
                ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source.Id.Contains(".arrival.", StringComparison.Ordinal)
                && node.ContentDrawable is Sprite { Texture: not null } && EffectiveAlpha(node.ContentDrawable) > .01));
        AddReadingScreenshot("reading-first-row", held: true);
        AddStep("change speed with the actual black-key Start gesture", () =>
        {
            var reading = (CaptureReadingPlayer)player;
            int presses = settings.ReadingMode == "normal" ? 12 : 1;
            for (int i = 0; i < presses; i++)
            {
                Assert.That(reading.Input.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True);
                Assert.That(reading.Input.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True);
            }
            if (settings.ReadingMode == "normal")
                Assert.That(reading.NativeRuleset.SelectedHiSpeed.Value, Is.EqualTo(20));
        });
        AddWaitStep("let actual reading values and source selector refresh", 3);
        AddReadingScreenshot("reading-adjusted", held: true);
        AddUntilStep("held Start outlasts its real preparation countdown", () =>
            ReadState().Timing.Preparation.Active && ReadState().Timing.Preparation.Remaining == 0);
        AddStep("release actual Start and let the original READY sequence run", () =>
            Assert.That(((CaptureReadingPlayer)player).Input.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
        AddUntilStep("real READY has a visible source animation frame", () =>
        {
            var preparation = ReadState().Timing.Preparation;
            return preparation.Ready && preparation.ReadyElapsed >= 300 && ReadyHasVisiblePieces();
        });
        AddReadingScreenshot("reading-ready", held: false);
        AddUntilStep("the native Start owner naturally starts chart time", () => !ReadState().Timing.Preparation.Active && clock.IsRunning);
        AddStep("schedule chart keys through ordinary native input ownership", () =>
            settings.Write("reading-native-chart-input-plan.json", ((CaptureReadingPlayer)player).StartSyntheticChartInput()));
    }

    private void ValidateReadingChartInput()
    {
        var reading = (CaptureReadingPlayer)player;
        Assert.That(reading.NativeRuleset.HasReplayLoaded.Value, Is.False);
        Assert.That(reading.NativeRuleset.Mods.Any(mod => mod is ModAutoplay or ModNoFail), Is.False);
        Assert.That(reading.DeliveredInputFrames, Is.EqualTo(reading.PlannedInputFrames));
        Assert.That(reading.HeldChartActions, Is.Empty);
        Assert.That(reading.ChartInputObservations.All(observation => observation.DeliveredTime >= observation.IntendedTime
                                                                    && observation.DeliveredTime - observation.IntendedTime <= 50), Is.True,
            "Native frame-driven input must be delivered within 50 ms of its real chart-clock deadline.");
        Assert.That(reading.Score.Replay.Frames, Is.Not.Empty, "The ordinary native recorder must retain the actual synthetic input.");
        settings.Write("reading-native-chart-input-complete.json", new
        {
            inputOwner = "Ordinary BmsSoloPlayer with native OmsAction API; generated sequence was never attached as Replay",
            plannedFrames = reading.PlannedInputFrames,
            deliveredFrames = reading.DeliveredInputFrames,
            transitions = reading.ChartInputObservations.ToArray(),
            actualMods = reading.NativeRuleset.Mods.Select(mod => mod.Acronym).ToArray(),
            recordedReplayType = reading.Score.Replay.GetType().FullName,
            recordedReplayFrames = reading.Score.Replay.Frames.Count,
            actualScore = score.TotalScore.Value,
            actualHitEvents = score.HitEvents.Count,
            actualFullCombo = score.IsFullCombo,
            synthesizedInput = true, replayOwnership = false, humanOrDeviceAcceptance = false,
        });
    }

    private object ReadingWaitObservation()
    {
        var reading = (CaptureReadingPlayer)player;
        GameplaySkinEventStateSnapshot state = ReadState();
        return new
        {
            gates = new
            {
                inputHeld = reading.Input.PreStartHoldPressed.Value,
                nativeAdjustmentHeld = reading.NativeRuleset.AdjustmentHeld,
                preparationActive = state.Timing.Preparation.Active,
                preparationElapsedAtLeast300 = state.Timing.Preparation.Elapsed >= 300,
                publishedAdjusting = state.Timing.AdjustmentState.Adjusting,
            },
            state.Timing.Preparation,
            publishedAdjustment = state.Timing.AdjustmentState,
            directNativeAdjustment = reading.NativeRuleset.GameplaySkinAdjustmentState,
            authoritativeGameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime"),
            scenePreparationDuration = runtime.PreparedScene.Program.PreparationDuration,
            nativePlayer = reading.PreparationDiagnostics(),
            nativeClock = new { clock.CurrentTime, clock.IsRunning, paused = clock.IsPaused.Value, clock.Alpha },
            frameStableClock = new
            {
                reading.NativeRuleset.FrameStableClock.CurrentTime,
                reading.NativeRuleset.FrameStableClock.IsRunning,
                waitingOnFrames = reading.NativeRuleset.FrameStableClock.WaitingOnFrames.Value,
            },
            replay = new
            {
                type = reading.Score.Replay.GetType().FullName,
                frames = reading.Score.Replay.Frames.Count,
                loaded = reading.NativeRuleset.HasReplayLoaded.Value,
                readingCalculationVersion = ReadField<int>(reading.NativeRuleset, "readingCalculationVersion"),
                settingsReadOnly = ReadField<bool>(reading.NativeRuleset, "readingSettingsReadOnlyReplay"),
            },
            reading.Input.UseParentInput,
            canAdjustGameplaySettings = ReadProperty<bool>(reading.NativeRuleset, "canAdjustGameplaySettings"),
            rulesetPaused = reading.NativeRuleset.IsPaused.Value,
            currentPlayer = ReferenceEquals(Game.ScreenStack.CurrentScreen, player),
            player.Alpha,
            hostActive = host.IsActive.Value,
            runtime.IsSceneReady,
            runtimeFaults = runtime.RuntimeFaults.Count,
        };
    }

    private void AddReadingScreenshot(string name, bool held)
    {
        AddStep("capture native reading controls " + name, () =>
        {
            var reading = (CaptureReadingPlayer)player;
            AssertReadingSourceCapabilities(reading);
            bool hasAuthoredPreview = reading.NativeRuleset.LayoutSnapshot.HasAuthoredNormalSpeedPreview;
            bool expectedPreviewVisible = held && (!hasAuthoredPreview || settings.ReadingMode == "normal");
            GameplaySkinEventStateSnapshot state = ReadState();
            GameplaySkinAdjustmentState adjustment = state.Timing.AdjustmentState;
            Assert.That(reading.Input.PreStartHoldPressed.Value, Is.EqualTo(held));
            Assert.That(reading.NativeRuleset.AdjustmentHeld, Is.EqualTo(held));
            Assert.That(adjustment.Adjusting, Is.EqualTo(held));
            Assert.That(adjustment.Mode.ToString().ToLowerInvariant(), Is.EqualTo(settings.ReadingMode));
            Assert.That(adjustment.IsAvailable && adjustment.ActualHiSpeed.HasValue, Is.True);
            Assert.That(adjustment.GreenNumberRangeAvailable, Is.True, "The synthetic source really changes from 150 to 180 BPM.");
            Assert.That(adjustment.GreenNumber, Is.EqualTo(reading.NativeRuleset.SpeedMetrics.Value.GreenNumber));
            Assert.That(reading.NativeRuleset.SuddenUnits, Is.EqualTo(125));
            Assert.That(reading.NativeRuleset.HiddenUnits, Is.EqualTo(settings.ReadingMode == "floating" ? 75 : 0));
            Assert.That(reading.NativeRuleset.LiftUnits, Is.EqualTo(settings.ReadingMode == "floating" ? 0 : 80));
            Assert.That(reading.NativeRuleset.SuddenActive, Is.True);
            Assert.That(reading.NativeRuleset.HiddenActive, Is.EqualTo(settings.ReadingMode == "floating"));
            Assert.That(state.Timing.LiftState.IsAvailable, Is.True);
            Assert.That(state.Timing.LiftState.Units, Is.EqualTo(settings.ReadingMode == "floating" ? 0 : 80));
            var previews = reading.ChildrenOfType<BmsPreStartSpeedPreview>().ToArray();
            Assert.That(previews, Is.Not.Empty);
            Assert.That(previews.All(preview => preview.LayoutSnapshot.HasAuthoredNormalSpeedPreview == hasAuthoredPreview), Is.True);
            Assert.That(previews.Any(preview => preview.IsPreviewVisible), Is.EqualTo(expectedPreviewVisible));
            if (expectedPreviewVisible)
            {
                foreach (BmsPreStartSpeedPreview preview in previews.Where(preview => preview.IsPreviewVisible))
                {
                    var notes = ReadField<Array>(preview, "previewNotes").Cast<CompositeDrawable>().ToArray();
                    Assert.That(notes, Has.Length.EqualTo(2));
                    Assert.That(notes.All(note => note.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null && EffectiveAlpha(sprite) > .01)), Is.True,
                        "Both native preview notes must have applied textured visuals, not just an active parent.");
                    var bounds = preview.ScreenSpaceDrawQuad.AABBFloat;
                    Assert.That(notes.SelectMany(note => note.ChildrenOfType<Sprite>()).Any(sprite =>
                    {
                        var rect = sprite.ScreenSpaceDrawQuad.AABBFloat;
                        return sprite.Texture != null && EffectiveAlpha(sprite) > .01 && rect.Right > bounds.Left && rect.Left < bounds.Right
                            && rect.Bottom > bounds.Top && rect.Top < bounds.Bottom;
                    }), Is.True, "At least one moving note must intersect the actual preview viewport.");
                }
            }
            var author = ReadingNodes().Where(node => ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source.Id.Contains(".reading.", StringComparison.Ordinal)).ToArray();
            if (held)
            {
                Assert.That(author.Any(node => ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source.Id.EndsWith(".reading.green", StringComparison.Ordinal)
                    && EffectiveAlpha(node.ContentDrawable) > .01), Is.True);
                if (settings.ReadingMode == "normal")
                {
                    string page = reading.NativeRuleset.SelectedHiSpeed.Value >= 11 ? ".normal.page1.row" : ".normal.page0.row";
                    Assert.That(author.Count(node => ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source.Id.Contains(page, StringComparison.Ordinal)
                        && EffectiveAlpha(node.ContentDrawable) > .01), Is.EqualTo(10));
                }
            }
            RecordGameplay(name, checked((int)Math.Round(ReadProperty<double>(events, "AuthoritativeGameplayTime"))));
            settings.Write(name + "-native.json", new
            {
                actualPlayer = player.GetType().FullName,
                inputPath = "BmsInputManager router -> real BmsSoloPlayer Start/adjustment handlers",
                synthesizedNativeInput = true,
                physicalDeviceQualified = false,
                held,
                clock.IsRunning,
                clock.CurrentTime,
                state.Timing.Preparation,
                adjustment,
                requestedFrame = settings.Options.GetValueOrDefault("frame"),
                hasAuthoredNormalSpeedPreview = hasAuthoredPreview,
                previewPresentation = hasAuthoredPreview ? "author-normal-side-window" : "native-pre-start-lane",
                expectedPreviewVisible,
                expectedSourceFirstRowCue = !UsesEarlierReadingSource,
                sourceFirstRowCuePreparedNodes = runtime.PreparedScene.Roots.SelectMany(Walk)
                    .Count(node => node.Source.Id.Contains(".arrival.", StringComparison.Ordinal)),
                nativeMode = reading.NativeRuleset.HiSpeedMode.Value.ToString(),
                selectedNativeLevel = reading.NativeRuleset.SelectedHiSpeed.Value,
                nativeMetrics = reading.NativeRuleset.SpeedMetrics.Value,
                previews = previews.Select(preview => new
                {
                    preview.IsPreviewActive,
                    preview.IsPreviewVisible,
                    preview.IsPreviewPaused,
                    preview.PrimaryNoteProgress,
                    preview.LayoutSnapshot.HasAuthoredNormalSpeedPreview,
                    parentType = preview.Parent?.GetType().FullName,
                    nativeRect = Rect(preview.LayoutSnapshot.PreStartPreviewRect),
                    actualViewport = new
                    {
                        preview.ScreenSpaceDrawQuad.AABBFloat.X,
                        preview.ScreenSpaceDrawQuad.AABBFloat.Y,
                        preview.ScreenSpaceDrawQuad.AABBFloat.Width,
                        preview.ScreenSpaceDrawQuad.AABBFloat.Height,
                    },
                    effectiveAlpha = EffectiveAlpha(preview),
                    texturedNotes = ReadField<Array>(preview, "previewNotes").Cast<CompositeDrawable>().Select(note => new
                    {
                        rect = new
                        {
                            note.ScreenSpaceDrawQuad.AABBFloat.X,
                            note.ScreenSpaceDrawQuad.AABBFloat.Y,
                            note.ScreenSpaceDrawQuad.AABBFloat.Width,
                            note.ScreenSpaceDrawQuad.AABBFloat.Height,
                        },
                        visibleTexturedPieces = note.ChildrenOfType<Sprite>().Count(sprite => sprite.Texture != null && EffectiveAlpha(sprite) > .01),
                    }).ToArray(),
                }).ToArray(),
                author = author.Select(NodeEvidence).ToArray(),
                readyVisiblePieces = ReadyHasVisiblePieces(),
                runtimeFaults = runtime.RuntimeFaults.Count,
            });
            screenshot = Capture(name);
        });
        AddUntilStep("wait for native reading framebuffer", () => screenshot?.IsCompleted == true);
        AddStep("propagate native reading framebuffer errors", () => screenshot!.GetAwaiter().GetResult());
    }

    private void AssertReadingSourceCapabilities(CaptureReadingPlayer reading)
    {
        // Only the explicitly selected, source-qualified 23/24 contributions omit these
        // later artworks. A missing rectangle on any other package remains a failure.
        Assert.That(reading.NativeRuleset.LayoutSnapshot.HasAuthoredNormalSpeedPreview, Is.EqualTo(!UsesEarlierReadingSource),
            "The exact selected source must agree with its authored Normal preview capability.");
        if (!UsesEarlierReadingSource)
            return;

        Assert.That(runtime.Publication.Snapshot.Context.OptionValues.TryGetValue("frame", out GameplaySkinOptionValue selectedFrame), Is.True);
        Assert.That(selectedFrame.CanonicalValue, Is.EqualTo(settings.Options["frame"]));
        GameplaySkinPreparedSceneNode[] nodes = runtime.PreparedScene.Roots.SelectMany(Walk).ToArray();
        Assert.That(nodes.Where(node => node.Source.Id.Contains(".arrival.", StringComparison.Ordinal)), Is.Empty,
            "The 23/24 source must not acquire a first-row cue from the later source.");
        Assert.That(nodes.Where(node => node.Source.Id.Contains(".reading.preview.", StringComparison.Ordinal)), Is.Empty,
            "The 23/24 source must not acquire the later Normal side-window artwork; its native lane preview remains available.");
    }

    private IEnumerable<GameplaySkinSceneRuntimeNode> ReadingNodes()
    {
        MethodInfo lookup = typeof(GameplaySkinSceneRuntimeHost).GetMethod("TryGetRuntimeNode", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (GameplaySkinPreparedSceneNode prepared in runtime.PreparedScene.Roots.SelectMany(Walk))
        {
            object?[] arguments = [prepared.InstanceId, null];
            if ((bool)lookup.Invoke(runtime, arguments)!)
                yield return (GameplaySkinSceneRuntimeNode)arguments[1]!;
        }
    }

    private bool ReadyHasVisiblePieces()
        => ReadingNodes().Where(node => ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source.Id.EndsWith(".reading.ready.art", StringComparison.Ordinal))
            .Any(node => node.ContentDrawable is CompositeDrawable animation && EffectiveAlpha(animation) > .01
                         && ReadField<int>(animation, "appliedFrame") >= 0
                         && animation.ChildrenOfType<Sprite>().Any(sprite => sprite.IsPresent && sprite.Texture != null && EffectiveAlpha(sprite) > .01));

    private partial class CaptureReadingPlayer : BmsSoloPlayer
    {
        public sealed record InputObservation(string Event, double PlayerTime, bool InputHeld, bool NativeAdjustmentHeld);
        public sealed record ChartInputObservation(double IntendedTime, double DeliveredTime, string Action, bool Pressed);
        public readonly List<InputObservation> InputObservations = new();
        public readonly List<ChartInputObservation> ChartInputObservations = new();
        public readonly HashSet<BmsAction> HeldChartActions = new();
        private BmsReplayFrame[] inputPlan = Array.Empty<BmsReplayFrame>();
        private bool chartInputScheduled;

        public int PlannedInputFrames => inputPlan.Length;
        public int DeliveredInputFrames { get; private set; }

        public DrawableBmsRuleset NativeRuleset => (DrawableBmsRuleset)DrawableRuleset;
        public BmsInputManager Input => NativeRuleset.GameplayInputManager!;

        public object PreparationDiagnostics()
        {
            object controller = typeof(osu.Game.Screens.Play.Player)
                .GetField("gameplaySkinPreparation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
            return new
            {
                configured = HasGameplaySkinPreparation,
                active = GameplaySkinPreparationActive,
                controllerHeld = ReadProperty<bool>(controller, "IsHeld"),
                controllerState = ReadProperty<object>(controller, "State"),
                startPending = (bool)typeof(BmsSoloPlayer).GetField("gameplayStartPending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!,
                pauseOverlay = PauseOverlay.State.Value.ToString(),
            };
        }

        protected override Score CreateScore(IBeatmap beatmap)
        {
            // Use the native generator only as a plan of physical key transitions. The
            // live score and recorder are created by Player; no Replay input owner is attached.
            inputPlan = new BmsModAutoplay().CreateReplayData(beatmap, Mods.Value).Replay.Frames.Cast<BmsReplayFrame>().ToArray();
            if (OmitFirstTwoChordsForPersonalBest)
                OmitFirstTwoNativeChordsForPersonalBest(beatmap);
            return base.CreateScore(beatmap);
        }

        public object StartSyntheticChartInput()
        {
            Assert.That(chartInputScheduled, Is.False);
            Assert.That(inputPlan, Is.Not.Empty);
            Assert.That(GameplaySkinPreparationActive, Is.False);
            Assert.That(GameplayClockContainer.IsRunning, Is.True);
            Assert.That(NativeRuleset.HasReplayLoaded.Value, Is.False);
            Assert.That(Input.UseParentInput, Is.True);
            Assert.That(Input.PreStartHoldPressed.Value, Is.False);
            Assert.That(inputPlan[0].Time, Is.GreaterThan(Input.Clock.CurrentTime), "Input must be scheduled before the first actual note, without rewinding.");
            chartInputScheduled = true;
            return new
            {
                source = "Native BmsAutoGenerator action/timestamp plan, delivered via native OmsAction API on the input manager's chart clock",
                replayOwnership = false,
                player = GetType().FullName,
                nativeMods = NativeRuleset.Mods.Select(mod => mod.Acronym).ToArray(),
                scheduledAtChartTime = Input.Clock.CurrentTime,
                inputFrames = inputPlan.Select(frame => new { frame.Time, actions = frame.Actions.Select(action => action.ToString()).ToArray() }).ToArray(),
                injectedSkinEvents = false, injectedScoreOrGauge = false, humanOrDeviceAcceptance = false,
            };
        }

        protected override void Update()
        {
            base.Update();

            // Consume the plan from this ordinary Player's update. Input remains owned
            // by the live ruleset; deadlines use its actual chart clock, never wall time.
            if (!chartInputScheduled || !GameplayClockContainer.IsRunning)
                return;

            while (DeliveredInputFrames < inputPlan.Length && inputPlan[DeliveredInputFrames].Time <= Input.Clock.CurrentTime)
                DispatchChartInput(inputPlan[DeliveredInputFrames]);
        }

        private void DispatchChartInput(BmsReplayFrame frame)
        {
            Assert.That(NativeRuleset.HasReplayLoaded.Value, Is.False);
            Assert.That(Input.UseParentInput, Is.True);
            Assert.That(Input.PreStartHoldPressed.Value, Is.False);
            Assert.That(GameplayState.HasFailed, Is.False);
            var next = frame.Actions.ToHashSet();
            foreach (BmsAction action in HeldChartActions.Except(next).OrderBy(action => action).ToArray())
                Deliver(action, false);
            foreach (BmsAction action in next.Except(HeldChartActions).OrderBy(action => action))
                Deliver(action, true);
            HeldChartActions.Clear();
            HeldChartActions.UnionWith(next);
            DeliveredInputFrames++;

            void Deliver(BmsAction action, bool pressed)
            {
                Assert.That(OmsBmsActionMap.TryMapToOmsAction(NativeRuleset.Variant, action, out OmsAction native), Is.True);
                Assert.That(pressed ? Input.TriggerOmsActionPressed(native) : Input.TriggerOmsActionReleased(native), Is.True);
                Assert.That(Input.IsLaneHeld(action), Is.EqualTo(pressed));
                ChartInputObservations.Add(new ChartInputObservation(frame.Time, Input.Clock.CurrentTime, native.ToString(), pressed));
            }
        }

        protected override Task ImportScore(Score score) => IsPersonalBestCase ? base.ImportScore(score) : Task.CompletedTask;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (LoadedBeatmapSuccessfully)
            {
                Input.AdjustmentInputReset += () => ObserveInput("native-input-reset");
                Input.Router.ActionPressed += action => { if (action == OmsAction.UI_PreStartHold) ObserveInput("Start-pressed"); };
                Input.Router.ActionReleased += action => { if (action == OmsAction.UI_PreStartHold) ObserveInput("Start-released"); };
            }
        }

        private void ObserveInput(string name)
        {
            if (InputObservations.Count == 12)
                InputObservations.RemoveAt(0);
            InputObservations.Add(new InputObservation(name, Time.Current, Input.PreStartHoldPressed.Value, NativeRuleset.AdjustmentHeld));
        }
    }
}
