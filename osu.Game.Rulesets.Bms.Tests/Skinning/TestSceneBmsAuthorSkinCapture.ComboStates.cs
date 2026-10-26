// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private enum ComboCapturePhase
        {
            Disabled, Key, Palette, Miss, Frozen,
        }

        private BmsInputManager comboCaptureInput = null!;
        private BmsHitObject comboCaptureKey = null!;
        private BmsHitObject comboCaptureMiss = null!;
        private GameplaySkinLaneGroupId comboCaptureGroup = null!;
        private OmsAction comboCaptureAction;
        private ComboCapturePhase comboCapturePhase;
        private GameplaySkinCurrentJudgementStateSnapshot comboCaptureJudgement;
        private GameplaySkinScoreStateSnapshot comboCaptureScore;
        private GameplaySkinSceneRuntimeNode[] comboCaptureCarriers = null!;
        private double comboCaptureTrackOrigin;
        private long comboCaptureEpoch;
        private int comboCaptureScoringHitCount;
        private int comboCapturePalette;
        private int comboCaptureGroupIndex;
        private bool comboCaptureKeyDispatched;
        private double comboCaptureFrozenTime;
        private readonly List<object> comboCaptureInputs = [];
        private readonly List<object> comboCaptureClockControls = [];

        private static IBeatmap createComboStatesChart(BmsRuleset ruleset, bool doublePlay, bool fiveKey)
        {
            var chart = new StringBuilder("#TITLE COMBO states / OMS native author capture\n#ARTIST Offline reference chart\n#DIFFICULTY 3\n#PLAYLEVEL 1\n#BPM 120\n#LNTYPE 1\n#WAV01 note.wav\n");
            if (doublePlay)
                chart.Append("#PLAYER 3\n");
            for (int deck = 0; deck < (doublePlay ? 2 : 1); deck++)
            {
                string regular = deck == 0 ? "1" : "2";
                string hold = deck == 0 ? "5" : "6";
                // DP deliberately uses different real origins. Each deck earns Combo 1 after the preceding
                // deck's real Miss; the other deck cannot supply its judgement clock or visible grade.
                chart.Append('#').Append((2 + deck * 2).ToString("000")).Append(regular).Append("1:01\n");
                chart.Append('#').Append((3 + deck * 2).ToString("000")).Append(regular).Append("2:01\n");
                chart.Append('#').Append((doublePlay ? 6 : 4).ToString("000")).Append(hold).Append("1:01000100\n");
                chart.Append('#').Append((doublePlay ? 7 : 5).ToString("000")).Append(regular).Append("1:01\n");
            }
            return new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(chart.ToString(), fiveKey ? "combo-states-capture.bms" : "combo-states-capture.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
        }

        private void addComboStatesCaptureSteps()
        {
            AddStep("prepare normal Player input for actual COMBO states", () =>
            {
                Assert.That(player, Is.TypeOf<BmsSoloPlayer>());
                Assert.That(player.GameplayState.Mods.OfType<BmsModAutoplay>(), Is.Empty);
                Assert.That(player.GameplayState.Mods.OfType<BmsModAutoScratch>(), Is.Empty);
                Assert.That(player.GameplayState.Mods.OfType<BmsModAutoNote>(), Is.Empty);
                Assert.That(renderer.HasReplayLoaded.Value, Is.False);
                comboCaptureInput = (BmsInputManager)renderer.GameplayInputManager!;
                Assert.That(comboCaptureInput.UseParentInput, Is.True);
                captureObservedHostActivity = host.IsActive.Value;
                captureHostActivity.BindTo((Bindable<bool>)host.IsActive);
                captureHostActivity.Value = true;
                Assert.That(comboCaptureNodes().Select(node => node.PreparedNode.MaterialTarget!.GroupId).Distinct().Count(), Is.EqualTo(doublePlay ? 2 : 1));
                string display = renderer.GameplaySkinSceneRuntime!.Publication.Snapshot.Context.OptionValues.Values["judgement-display"].CanonicalValue;
                Assert.That(display, Is.EqualTo("normal").Or.EqualTo("label-only"));
                File.WriteAllText(Path.Combine(outputDirectory, "combo-states-plan.json"), JsonSerializer.Serialize(new
                {
                    boundary = "Actual BmsSoloPlayer, software OmsAction key pulses and simulated host activity; naturally advanced gameplay clock stopped for native frames; no Seek or fabricated score/judgement",
                    phasePolicy = "OMS author adaptation: blue+b, white+g, pink+r; observed floor(elapsed modulo 300 / 100); no claim about original game's period or initial phase",
                    naturalWindows = new[] { new[] { 40, 90 }, new[] { 140, 190 }, new[] { 240, 290 } },
                    observedHostActive = captureObservedHostActivity,
                    hostActivitySimulated = true,
                    style = style.ToString(),
                    doublePlay,
                    fiveKey,
                    display,
                    longNoteMode = longNoteMode.ToString(),
                    mods = player.GameplayState.Mods.Select(mod => mod.Acronym).ToArray(),
                    chart = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().Select(note => new { note.StartTime, note.LaneIndex, kind = note.GetType().Name }).ToArray(),
                }));
            });

            int groups = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_KEYMODE") == "14K" ? 2 : 1;
            for (int group = 0; group < groups; group++)
            {
                int groupIndex = group;
                AddStep("select the exact COMBO capture group " + groupIndex, () =>
                {
                    comboCaptureGroupIndex = groupIndex;
                    BmsAction action = groupIndex == 0 ? BmsAction.Key1 : BmsAction.Key8;
                    BmsAction missAction = groupIndex == 0 ? BmsAction.Key2 : BmsAction.Key9;
                    BmsGameplayLayoutLane keyLane = renderer.Playfield.Lanes.Single(lane => lane.LayoutLane.Action == action).LayoutSnapshotLane!;
                    BmsGameplayLayoutLane missLane = renderer.Playfield.Lanes.Single(lane => lane.LayoutLane.Action == missAction).LayoutSnapshotLane!;
                    comboCaptureGroup = keyLane.NeutralLane.TopologyEntry.Identity.Group.Id;
                    GameplaySkinPreparedSceneProgram program = renderer.GameplaySkinSceneRuntime!.PreparedScene.Program;
                    GameplaySkinSceneRuntimeNode[] groupNodes = [.. comboCaptureNodes().Where(node => node.PreparedNode.MaterialTarget!.GroupId == comboCaptureGroup)];
                    GameplaySkinPreparedSceneVariant normalChoice = program.Variants.Single(variant => variant.Source == GameplaySkinSceneBindingSource.Option
                        && variant.OptionId == "judgement-display" && variant.Property == GameplaySkinSceneProperty.Visible
                        && variant.SelectValue("normal").BooleanValue && !variant.SelectValue("label-only").BooleanValue);
                    GameplaySkinSceneRuntimeNode normalGate = groupNodes.Single(node => node.PreparedNode.Source.Id == normalChoice.TargetNodeId);
                    var normalIds = lightingTree(normalGate.PreparedNode).Select(node => node.InstanceId).ToHashSet(StringComparer.Ordinal);
                    GameplaySkinPreparedSceneBinding positiveBinding = program.Bindings.Single(binding => binding.Source == GameplaySkinSceneBindingSource.ComboActive
                        && binding.Property == GameplaySkinSceneProperty.Visible
                        && groupNodes.Any(node => normalIds.Contains(node.PreparedNode.InstanceId) && node.PreparedNode.Source.Id == binding.TargetNodeId));
                    GameplaySkinSceneRuntimeNode positiveGate = groupNodes.Single(node => normalIds.Contains(node.PreparedNode.InstanceId)
                        && node.PreparedNode.Source.Id == positiveBinding.TargetNodeId);
                    var positiveChildren = positiveGate.PreparedNode.Children.Select(node => node.InstanceId).ToHashSet(StringComparer.Ordinal);
                    var phaseTargets = program.Tracks.Where(track => track.Property == GameplaySkinSceneProperty.Opacity
                        && track.Trigger == GameplaySkinSceneEvent.JudgementHit && track.Loop
                        && track.Keyframes.Select(frame => frame.Time).SequenceEqual(new[] { 0d, 100, 200, 300 }))
                        .Select(track => track.TargetNodeId).ToHashSet(StringComparer.Ordinal);
                    comboCaptureCarriers = [.. groupNodes.Where(node => positiveChildren.Contains(node.PreparedNode.InstanceId) && phaseTargets.Contains(node.PreparedNode.Source.Id))];
                    Assert.That(comboCaptureCarriers, Has.Length.EqualTo(3));
                    Assert.That(missLane.NeutralLane.TopologyEntry.Identity.Group.Id, Is.EqualTo(comboCaptureGroup));
                    comboCaptureKey = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().First(note => note is not BmsHoldNote && note.LaneIndex == keyLane.LogicalIndex);
                    comboCaptureMiss = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().Single(note => note is not BmsHoldNote && note.LaneIndex == missLane.LogicalIndex);
                    Assert.That(comboCaptureKey.AutoPlay || comboCaptureMiss.AutoPlay, Is.False);
                    Assert.That(comboCaptureKey.CountsForScore && comboCaptureMiss.CountsForScore, Is.True);
                    Assert.That(score.Combo.Value, Is.Zero);
                    Assert.That(renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime, Is.LessThan(comboCaptureKey.StartTime - 100));
                    comboCaptureAction = groupIndex == 0 ? OmsAction.Key1P_1 : OmsAction.Key2P_1;
                    comboCaptureKeyDispatched = false;
                    comboCapturePalette = 0;
                    comboCapturePhase = ComboCapturePhase.Key;
                });
                for (int palette = 0; palette < 3; palette++)
                {
                    int phase = palette;
                    addComboStatesSample($"g{groupIndex}-pg-{new[] { "blue", "green", "pink" }[phase]}", false);
                    if (phase < 2)
                        AddStep("naturally resume to the next COMBO palette", () => resumeComboCapture(ComboCapturePhase.Palette, phase + 1));
                }
                AddStep("leave the next ordinary key unpressed for a real Miss", () => resumeComboCapture(ComboCapturePhase.Miss, 0));
                addComboStatesSample($"g{groupIndex}-miss-zero", true);
                AddStep("resume the remaining real chart after this group", () => resumeComboCapture(ComboCapturePhase.Disabled, 0));
            }
        }

        // Runs after the real Game update. It dispatches only normal software input and stops the existing clock;
        // the chart's real drawables and score processor remain the sole judgement and Combo producers.
        private void sampleComboStatesCaptureInput()
        {
            if (comboCapturePhase is ComboCapturePhase.Disabled or ComboCapturePhase.Frozen || !gameplayClock.IsRunning)
                return;
            double time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
            if (comboCapturePhase == ComboCapturePhase.Key)
            {
                if (!comboCaptureKeyDispatched && time >= comboCaptureKey.StartTime - 5)
                {
                    comboCaptureKeyDispatched = true;
                    bool pressed = comboCaptureInput.TriggerOmsActionPressed(comboCaptureAction);
                    bool released = comboCaptureInput.TriggerOmsActionReleased(comboCaptureAction);
                    comboCaptureInputs.Add(new
                    {
                        group = comboCaptureGroup.Value,
                        action = comboCaptureAction.ToString(),
                        time,
                        noteTime = comboCaptureKey.StartTime,
                        requestedOffset = -5,
                        pressed,
                        released
                    });
                    File.WriteAllText(Path.Combine(outputDirectory, $"combo-states-input-g{comboCaptureGroupIndex}.json"), JsonSerializer.Serialize(comboCaptureInputs));
                    Assert.That(pressed && released, Is.True);
                }
                if (!score.HitEvents.Any(hit => ReferenceEquals(hit.HitObject, comboCaptureKey)))
                    return;
                Assert.That(score.HitEvents.Single(hit => ReferenceEquals(hit.HitObject, comboCaptureKey)).Result, Is.EqualTo(HitResult.Perfect),
                    "The native palette samples require a real on-time Perfect, not a replacement grade or a forged counter.");
                GameplaySkinEventStateSnapshot state = lightingState();
                GameplaySkinCurrentJudgementStateSnapshot[] current = comboGroupJudgements(state, GameplaySkinJudgementGrade.Perfect);
                if (current.Length == 0)
                    return;
                comboCaptureJudgement = current.Single();
                comboCaptureScore = state.Score;
                comboCaptureEpoch = renderer.GameplaySkinEventStream.CurrentEpoch;
                comboCaptureScoringHitCount = score.HitEvents.Count(hit => hit.HitObject is BmsHitObject { CountsForScore: true } && hit.Result != HitResult.IgnoreHit);
                Assert.That(comboCaptureScore.Combo, Is.EqualTo(1));
                Assert.That(comboCaptureScore.Score, Is.EqualTo(score.TotalScore.Value).And.GreaterThan(0));
                Assert.That(comboCaptureJudgement.Judgement.HasPlayerTiming, Is.True);
                comboCapturePhase = ComboCapturePhase.Palette;
            }
            if (comboCapturePhase == ComboCapturePhase.Miss)
            {
                if (!score.HitEvents.Any(hit => ReferenceEquals(hit.HitObject, comboCaptureMiss)))
                    return;
                Assert.That(score.HitEvents.Single(hit => ReferenceEquals(hit.HitObject, comboCaptureMiss)).Result, Is.EqualTo(HitResult.Miss));
                GameplaySkinEventStateSnapshot state = lightingState();
                GameplaySkinCurrentJudgementStateSnapshot[] current = comboGroupJudgements(state, GameplaySkinJudgementGrade.Miss);
                if (current.Length == 0)
                    return;
                comboCaptureJudgement = current.Single();
                Assert.That(state.Score.Score, Is.EqualTo(comboCaptureScore.Score), "A real Miss keeps EX while breaking Combo.");
                Assert.That(state.Score.Combo, Is.Zero);
                comboCaptureScore = state.Score;
                comboCaptureScoringHitCount = score.HitEvents.Count(hit => hit.HitObject is BmsHitObject { CountsForScore: true } && hit.Result != HitResult.IgnoreHit);
            }
            // The group snapshot and its three real phase carriers must refer to the same application before
            // sampling. LastGameplayTime is only the last discrete envelope, not ProcessFrame's timing high-water.
            if (comboCaptureCarriers.Any(node => node.JudgementAnimationStart != comboCaptureJudgement.AppliedTime || node.JudgementAnimationEpoch != comboCaptureEpoch))
                return;
            comboCaptureTrackOrigin = comboCaptureCarriers[0].JudgementAnimationStart!.Value;
            double trackSampleTime = comboCaptureTrackSampleTime();
            double elapsed = trackSampleTime - comboCaptureTrackOrigin;
            double windowStart = comboCapturePhase == ComboCapturePhase.Miss ? 40 : comboCapturePalette * 100 + 40;
            if (elapsed < windowStart)
                return;
            Assert.That(elapsed, Is.LessThanOrEqualTo(windowStart + 50),
                "The host skipped the requested natural capture window; do not seek, reset the origin or fabricate a replacement phase.");
            comboCaptureFrozenTime = time;
            gameplayClock.Stop();
            comboCapturePhase = ComboCapturePhase.Frozen;
            comboCaptureClockControls.Add(new
            {
                operation = "GameplayClockContainer.Stop",
                time,
                audioTime = gameplayClock.CurrentTime,
                group = comboCaptureGroup.Value,
                appliedTime = comboCaptureJudgement.AppliedTime,
                trackOrigin = comboCaptureTrackOrigin,
                trackSampleTime,
                elapsed,
                softwareClockControl = true
            });
        }

        // Read the exact high-water actually supplied to sampleTracks; this opt-in fixture exposes no new
        // production source/API and never writes a clock, event, animation origin or retained judgement.
        private double comboCaptureTrackSampleTime()
            => (double)typeof(GameplaySkinSceneRuntimeHost).GetField("sceneGameplayTime", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(renderer.GameplaySkinSceneRuntime)!;

        private GameplaySkinCurrentJudgementStateSnapshot[] comboGroupJudgements(GameplaySkinEventStateSnapshot state, GameplaySkinJudgementGrade grade)
            => [.. state.CurrentJudgements.Where(current => current.Scope == GameplaySkinJudgementScope.Group
                && current.Judgement.GroupId == comboCaptureGroup && current.Judgement.Grade == grade)];

        private void resumeComboCapture(ComboCapturePhase phase, int palette)
        {
            comboCapturePhase = phase;
            comboCapturePalette = palette;
            comboCaptureClockControls.Add(new
            {
                operation = "GameplayClockContainer.Start",
                time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                audioTime = gameplayClock.CurrentTime,
                group = comboCaptureGroup.Value,
                softwareClockControl = true
            });
            gameplayClock.Start();
        }

        private void addComboStatesSample(string phase, bool zero)
        {
            string filename = "combo-states-" + phase;
            AddUntilStep("freeze the natural " + phase + " native frame", () => comboCapturePhase == ComboCapturePhase.Frozen);
            AddWaitStep("let the stopped same-origin COMBO state reach its real author nodes", 3);
            AddStep("record actual COMBO readiness before waiting for visible material", () => writeComboStatesEvidence(filename + "-ready", zero, false));
            AddUntilStep("wait for the displayed COMBO label and numeric material", () =>
            {
                GameplaySkinSceneRuntimeNode[] visible = [.. comboCaptureNodes().Where(node => node.PreparedNode.MaterialTarget!.GroupId == comboCaptureGroup
                    && lightingAlpha(node.ContentDrawable) > 0 && node.ContentDrawable is Sprite or GameplaySkinBitmapText)];
                return visible.Any(node => node.ContentDrawable is Sprite)
                       && visible.All(node => node.ContentDrawable.IsLoaded && (node.ContentDrawable is not Sprite sprite || sprite.Texture != null));
            });
            AddStep("record the actual COMBO source, geometry, palette and framebuffer", () =>
            {
                writeComboStatesEvidence(filename, zero, false);
                capture = saveCapture(filename + ".png");
            });
            AddUntilStep("wait for the native COMBO screenshot", () => capture?.IsCompleted == true);
            AddStep("verify the same input and judgement origin survived native capture", () =>
            {
                capture!.GetAwaiter().GetResult();
                writeComboStatesEvidence(filename + "-verified", zero, true);
            });
        }

        private GameplaySkinSceneRuntimeNode[] comboCaptureNodes()
        {
            var ids = renderer.GameplaySkinSceneRuntime!.PreparedScene.Roots.SelectMany(lightingTree)
                .Where(root => ReferenceEquals(root.Slot, GameplaySkinSlotCatalog.JudgementDisplay))
                .SelectMany(lightingTree).Select(node => node.InstanceId).ToHashSet(StringComparer.Ordinal);
            return [.. lightingNodes().Where(node => ids.Contains(node.InstanceId))];
        }

        private void writeComboStatesEvidence(string filename, bool zero, bool assertValues)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinEventStateSnapshot state = lightingState();
            GameplaySkinSceneRuntimeNode[] nodes = comboCaptureNodes();
            GameplaySkinSceneRuntimeNode[] group = [.. nodes.Where(node => node.PreparedNode.MaterialTarget!.GroupId == comboCaptureGroup)];
            GameplaySkinSceneRuntimeNode[] labels = [.. group.Where(node => node.ContentDrawable is Sprite && lightingAlpha(node.ContentDrawable) > 0)];
            GameplaySkinSceneRuntimeNode[] digits = [.. group.Where(node => node.ContentDrawable is GameplaySkinBitmapText && lightingAlpha(node.ContentDrawable) > 0)];
            double time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
            double trackSampleTime = comboCaptureTrackSampleTime();
            double elapsed = trackSampleTime - comboCaptureTrackOrigin;
            int actualPalette = (int)Math.Floor(((elapsed % 300) + 300) % 300 / 100);
            bool labelOnly = runtime.Publication.Snapshot.Context.OptionValues.Values["judgement-display"].CanonicalValue == "label-only";
            GameplaySkinCurrentJudgementStateSnapshot current = comboGroupJudgements(state, zero ? GameplaySkinJudgementGrade.Miss : GameplaySkinJudgementGrade.Perfect).Single();
            var carrierTargetIds = comboCaptureCarriers.Select(node => node.PreparedNode.Source.Id).ToHashSet(StringComparer.Ordinal);
            var matchingPhaseTracks = runtime.PreparedScene.Program.Tracks.Select((track, index) => (Track: track, Index: index)).Where(item =>
                item.Track.Property == GameplaySkinSceneProperty.Opacity && item.Track.Trigger == GameplaySkinSceneEvent.JudgementHit
                && item.Track.Loop && carrierTargetIds.Contains(item.Track.TargetNodeId)
                && item.Track.Keyframes.Select(frame => frame.Time).SequenceEqual(new[] { 0d, 100, 200, 300 })).ToArray();
            File.WriteAllText(Path.Combine(outputDirectory, filename + ".json"), JsonSerializer.Serialize(new
            {
                boundary = "Software-triggered actual normal Player input; host activity simulated; natural gameplay clock stopped for native capture, never sought or reset",
                zero,
                labelOnly,
                group = comboCaptureGroup.Value,
                time,
                audioTime = gameplayClock.CurrentTime,
                gameplayClock.IsRunning,
                appliedTime = current.AppliedTime,
                trackOrigin = comboCaptureTrackOrigin,
                trackSampleTime,
                lastConsumedEnvelopeTime = runtime.LastGameplayTime,
                elapsed,
                actualPalette = zero ? null : new[] { "blue+b", "white+g", "pink+r" }[actualPalette],
                streamEpoch = renderer.GameplaySkinEventStream.CurrentEpoch,
                runtime.CurrentEpoch,
                runtime.LastSequence,
                state.Score,
                phaseCarriers = comboCaptureCarriers.Select(node => new
                {
                    node.InstanceId,
                    id = node.PreparedNode.Source.Id,
                    group = node.PreparedNode.MaterialTarget!.GroupId!.Value,
                    node.JudgementAnimationStart,
                    node.JudgementAnimationEpoch,
                    localAlpha = node.TransformDrawable.Alpha,
                    effectiveAlpha = lightingAlpha(node.ContentDrawable),
                    childRows = node.PreparedNode.Children.Select(row => row.InstanceId).ToArray(),
                }).ToArray(),
                matchingPhaseTracks = matchingPhaseTracks.Select(item => new
                {
                    programIndex = item.Index,
                    item.Track.Id,
                    item.Track.TargetNodeId,
                    type = item.Track.Type.ToString(),
                    easing = item.Track.Easing.ToString(),
                    property = item.Track.Property.ToString(),
                    trigger = item.Track.Trigger.ToString(),
                    clockSource = item.Track.ClockSource.ToString(),
                    item.Track.ClockOptionId,
                    item.Track.Loop,
                    keyframes = item.Track.Keyframes.Select(frame => new
                    {
                        frame.Time,
                        kind = frame.Value.Kind.ToString(),
                        number = frame.Value.NumberValue,
                        text = frame.Value.StringValue,
                        resource = frame.Value.Resource?.Id,
                    }).ToArray(),
                    runtimeMatches = nodes.Where(node => node.PreparedNode.Source.Id == item.Track.TargetNodeId).Select(node => new
                    {
                        node.InstanceId,
                        node.RuntimeScopeId,
                        declaredInstance = node.PreparedNode.InstanceId,
                        declaredSource = node.PreparedNode.Source.Id,
                        declaredType = node.PreparedNode.Source.Type.ToString(),
                        materialSlot = node.PreparedNode.OwningSlotKey?.Slot.Id,
                        materialGroup = node.PreparedNode.MaterialTarget?.GroupId?.Value,
                        resolvedTarget = node.PreparedNode.ResolvedTarget.ToString(),
                        node.BoundObjectId,
                        node.JudgementAnimationStart,
                        node.JudgementAnimationEpoch,
                        isSelectedCarrier = comboCaptureCarriers.Contains(node),
                        childRows = node.PreparedNode.Children.Select(child => new { child.InstanceId, source = child.Source.Id }).ToArray(),
                    }).ToArray(),
                    declarations = runtime.PreparedScene.Roots.SelectMany(lightingTree).Where(node => node.Source.Id == item.Track.TargetNodeId).Select(node => new
                    {
                        node.InstanceId,
                        source = node.Source.Id,
                        materialSlot = node.OwningSlotKey?.Slot.Id,
                        materialGroup = node.MaterialTarget?.GroupId?.Value,
                        resolvedTarget = node.ResolvedTarget.ToString(),
                    }).ToArray(),
                }).ToArray(),
                actualEx = score.TotalScore.Value,
                actualCombo = score.Combo.Value,
                allHitEventCount = score.HitEvents.Count,
                scoringBmsHitCount = score.HitEvents.Count(hit => hit.HitObject is BmsHitObject { CountsForScore: true } && hit.Result != HitResult.IgnoreHit),
                baselineScoringBmsHitCount = comboCaptureScoringHitCount,
                inputs = comboCaptureInputs,
                softwareClockControls = comboCaptureClockControls,
                judgements = state.CurrentJudgements.Where(retained => retained.Scope == GameplaySkinJudgementScope.Group).Select(retained => new
                {
                    group = retained.Judgement.GroupId!.Value,
                    retained.Judgement.ObjectId,
                    grade = retained.Judgement.Grade.ToString(),
                    retained.Judgement.Offset,
                    retained.Judgement.HasPlayerTiming,
                    retained.AppliedTime,
                    retained.DisplayUntil,
                }).ToArray(),
                nativeNodes = nodes.Where(node => node.ContentDrawable is Sprite or GameplaySkinBitmapText).Select(node =>
                {
                    GameplaySkinPreparedBitmapFont? font = runtime.PreparedScene.GetBitmapFont(node.PreparedNode.Source.Id);
                    return new
                    {
                        node.InstanceId,
                        id = node.PreparedNode.Source.Id,
                        group = node.PreparedNode.MaterialTarget!.GroupId!.Value,
                        node.JudgementAnimationStart,
                        node.JudgementAnimationEpoch,
                        localAlpha = node.TransformDrawable.Alpha,
                        effectiveAlpha = lightingAlpha(node.ContentDrawable),
                        node.ContentDrawable.IsLoaded,
                        geometry = lightingGeometry(node.ContentDrawable),
                        text = (node.ContentDrawable as GameplaySkinBitmapText)?.AuthorText,
                        font = font?.Source.Id,
                        glyphs = font?.Source.Glyphs.Select(glyph => new { glyph.Character, glyph.ResourceId, glyph.Advance, glyph.Rect }).ToArray(),
                        glyphResources = font?.Source.Glyphs.Select(glyph => glyph.ResourceId).Distinct().Select(id => runtime.PreparedScene.Resources.Single(resource => resource.Id == id))
                            .Select(resource => new { resource.Id, resource.ContentRevision }).ToArray(),
                        actualResources = node.ContentDrawable is Sprite sprite
                            ? runtime.PreparedScene.Resources.Where(resource => ReferenceEquals(resource.Texture, sprite.Texture))
                                .Select(resource => new { resource.Id, resource.ContentRevision }).ToArray() : null,
                    };
                }).ToArray(),
                nativeHits = score.HitEvents.Select(hit => new
                {
                    kind = hit.HitObject.GetType().Name,
                    hit.HitObject.StartTime,
                    hit.TimeOffset,
                    result = hit.Result.ToString(),
                    maximumResult = hit.HitObject.Judgement.MaxResult.ToString(),
                    countsForScore = hit.HitObject is BmsHitObject obj ? obj.CountsForScore : (bool?)null,
                    countedScoringBmsHit = hit.HitObject is BmsHitObject { CountsForScore: true } && hit.Result != HitResult.IgnoreHit,
                }).ToArray(),
                faults = runtime.RuntimeFaults,
            }));
            if (!assertValues)
                return;

            Assert.That(runtime.RuntimeFaults, Is.Empty);
            Assert.That(gameplayClock.IsRunning, Is.False);
            Assert.That(time, Is.EqualTo(comboCaptureFrozenTime).Within(0.1));
            Assert.That(trackSampleTime, Is.EqualTo(time).Within(0.1), "After the stopped frame drains, the track high-water must reach the same actual gameplay clock.");
            Assert.That(runtime.LastGameplayTime, Is.LessThanOrEqualTo(trackSampleTime), "The last discrete envelope may precede, but cannot overtake, its consumer's sampled high-water.");
            Assert.That(renderer.GameplaySkinEventStream.CurrentEpoch, Is.EqualTo(comboCaptureEpoch), "Stopping/resuming must not create a reset epoch.");
            Assert.That(runtime.CurrentEpoch, Is.EqualTo(comboCaptureEpoch));
            Assert.That(comboCaptureTrackOrigin, Is.EqualTo(current.AppliedTime));
            Assert.That(state.Score.Score, Is.EqualTo(comboCaptureScore.Score));
            Assert.That(score.TotalScore.Value, Is.EqualTo(comboCaptureScore.Score));
            Assert.That(state.Score.Combo, Is.EqualTo(zero ? 0 : 1));
            Assert.That(score.Combo.Value, Is.EqualTo(state.Score.Combo));
            Assert.That(score.HitEvents.Count(hit => hit.HitObject is BmsHitObject { CountsForScore: true } && hit.Result != HitResult.IgnoreHit), Is.EqualTo(comboCaptureScoringHitCount),
                "No additional scoring BMS result may arrive during a palette sample; native bar-line IgnoreHit events continue independently.");
            Assert.That(current.AppliedTime, Is.EqualTo(comboCaptureJudgement.AppliedTime));
            Assert.That(current.Judgement.ObjectId, Is.EqualTo(comboCaptureJudgement.Judgement.ObjectId));
            Assert.That(labels, Has.Length.EqualTo(1));
            Assert.That(digits, Has.Length.EqualTo(zero || labelOnly ? 0 : 1));
            foreach (GameplaySkinSceneRuntimeNode digit in digits)
                Assert.That(((GameplaySkinBitmapText)digit.ContentDrawable).AuthorText, Is.EqualTo(state.Score.Combo.ToString(CultureInfo.InvariantCulture)));
            foreach (IGrouping<GameplaySkinLaneGroupId?, GameplaySkinSceneRuntimeNode> other in nodes.Where(node => node.PreparedNode.MaterialTarget!.GroupId != comboCaptureGroup)
                         .GroupBy(node => node.PreparedNode.MaterialTarget!.GroupId))
                if (!state.CurrentJudgements.Any(retained => retained.Scope == GameplaySkinJudgementScope.Group && retained.Judgement.GroupId == other.Key))
                    Assert.That(other.Where(node => node.ContentDrawable is Sprite or GameplaySkinBitmapText).All(node => lightingAlpha(node.ContentDrawable) == 0), Is.True,
                        "An unjudged or expired deck cannot borrow this deck's visible grade or phase.");

            if (zero || labelOnly)
            {
                GameplaySkinSceneRuntimeNode row = group.Single(node => node.PreparedNode.Children.Any(child => child.InstanceId == labels[0].PreparedNode.InstanceId));
                Assert.That(row.PreparedNode.Source.Properties["layout"].StringValue, Is.EqualTo("horizontal"));
                Assert.That(row.PreparedNode.Children.Count, Is.EqualTo(1), "The BMS Combo-zero and label-only rows must contain only the real label, with no invisible digit cell.");
                Assert.That(labels[0].ContentDrawable.ScreenSpaceDrawQuad.Centre.X, Is.EqualTo(row.TransformDrawable.ScreenSpaceDrawQuad.Centre.X).Within(0.1));
                Assert.That(labels[0].ContentDrawable.ScreenSpaceDrawQuad.Centre.Y, Is.EqualTo(row.TransformDrawable.ScreenSpaceDrawQuad.Centre.Y).Within(0.1));
            }
            if (!zero)
            {
                Assert.That(actualPalette, Is.EqualTo(comboCapturePalette));
                var phaseTargets = comboCaptureCarriers.Select(node => node.PreparedNode.Source.Id).ToHashSet(StringComparer.Ordinal);
                GameplaySkinPreparedSceneTrack[] tracks = [.. runtime.PreparedScene.Program.Tracks.Where(track => track.Property == GameplaySkinSceneProperty.Opacity
                    && track.Trigger == GameplaySkinSceneEvent.JudgementHit && track.Loop
                    && phaseTargets.Contains(track.TargetNodeId)
                    && track.Keyframes.Select(frame => frame.Time).SequenceEqual(new[] { 0d, 100, 200, 300 }))];
                Assert.That(tracks, Has.Length.EqualTo(3));
                var fonts = new List<string>();
                foreach (GameplaySkinPreparedSceneTrack track in tracks)
                {
                    Assert.That(track.Type, Is.EqualTo(GameplaySkinSceneTrackType.Tween));
                    Assert.That(track.Easing, Is.EqualTo(GameplaySkinSceneEasing.Step));
                    GameplaySkinSceneRuntimeNode carrier = group.Single(node => node.PreparedNode.Source.Id == track.TargetNodeId);
                    Assert.That(carrier.JudgementAnimationStart, Is.EqualTo(current.AppliedTime));
                    Assert.That(carrier.JudgementAnimationEpoch, Is.EqualTo(comboCaptureEpoch));
                    Assert.That(carrier.TransformDrawable.Alpha, Is.EqualTo(track.Keyframes[actualPalette].Value.NumberValue));
                    GameplaySkinPreparedSceneNode rowDeclaration = carrier.PreparedNode.Children.Single();
                    GameplaySkinSceneRuntimeNode row = group.Single(node => node.PreparedNode.InstanceId == rowDeclaration.InstanceId);
                    Assert.That(row.PreparedNode.Children.Count, Is.EqualTo(2));
                    GameplaySkinSceneRuntimeNode label = group.Single(node => node.PreparedNode.InstanceId == row.PreparedNode.Children[0].InstanceId);
                    GameplaySkinSceneRuntimeNode digit = group.Single(node => node.PreparedNode.InstanceId == row.PreparedNode.Children[1].InstanceId);
                    Assert.That(label.ContentDrawable, Is.InstanceOf<Sprite>());
                    Assert.That(digit.ContentDrawable, Is.TypeOf<GameplaySkinBitmapText>());
                    Assert.That(((Sprite)label.ContentDrawable).Texture, Is.SameAs(label.PreparedNode.ResolvedTexture));
                    GameplaySkinPreparedBitmapFont font = runtime.PreparedScene.GetBitmapFont(digit.PreparedNode.Source.Id)!;
                    Assert.That(((GameplaySkinBitmapText)digit.ContentDrawable).Font.Family, Is.EqualTo(font.Source.Id));
                    Assert.That(font.Source.Id, Is.EqualTo(digit.PreparedNode.Source.Properties["bitmap-font"].StringValue));
                    Assert.That(font.Source.Glyphs, Has.Count.EqualTo(10));
                    fonts.Add(font.Source.Id);
                    int rowPalette = Enumerable.Range(0, 3).Single(index => track.Keyframes[index].Value.NumberValue == 1);
                    if (rowPalette != 0)
                    {
                        string labelResource = label.PreparedNode.Resource!.Id;
                        Assert.That(labelResource, Does.EndWith(rowPalette == 1 ? "g" : "r"));
                        Assert.That(font.Source.Id, Is.EqualTo(labelResource + "f"), "The actual white/green and pink/red label/font pairs must remain together.");
                        Assert.That(font.Source.Glyphs.All(glyph => glyph.ResourceId == labelResource[..^1]), Is.True);
                        Assert.That(font.Source.Glyphs.All(glyph => glyph.Rect is { } rect && (rowPalette == 1 ? rect.Y == 0 : rect.Y > 0)), Is.True);
                    }
                    if (track.Keyframes[actualPalette].Value.NumberValue == 1)
                    {
                        Assert.That(((Sprite)labels[0].ContentDrawable).Texture, Is.SameAs(((Sprite)label.ContentDrawable).Texture));
                        if (!labelOnly)
                        {
                            Assert.That(labels[0], Is.SameAs(label));
                            Assert.That(digits[0], Is.SameAs(digit));
                        }
                    }
                }
                Assert.That(fonts.Distinct().Count(), Is.EqualTo(3), "The three PGREAT pairs must use three actual numeric bitmap fonts.");
                if (labelOnly)
                {
                    GameplaySkinPreparedSceneTrack labelTrack = runtime.PreparedScene.Program.Tracks.Single(track => track.TargetNodeId == labels[0].PreparedNode.Source.Id
                        && track.Property == GameplaySkinSceneProperty.Resource && track.Trigger == GameplaySkinSceneEvent.JudgementHit);
                    Assert.That(labelTrack.Type, Is.EqualTo(GameplaySkinSceneTrackType.Frame));
                    Assert.That(labelTrack.Loop, Is.True);
                    Assert.That(labelTrack.Keyframes.Select(frame => frame.Time), Is.EqualTo(new[] { 0d, 100, 200, 300 }));
                    Assert.That(labels[0].JudgementAnimationStart, Is.EqualTo(current.AppliedTime));
                    Assert.That(labels[0].JudgementAnimationEpoch, Is.EqualTo(comboCaptureEpoch));
                    Assert.That(((Sprite)labels[0].ContentDrawable).Texture, Is.SameAs(labelTrack.Keyframes[actualPalette].Value.Resource!.Texture));
                }
            }
        }
    }
}
