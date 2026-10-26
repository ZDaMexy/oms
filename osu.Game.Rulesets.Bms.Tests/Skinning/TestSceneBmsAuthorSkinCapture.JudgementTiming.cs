// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private enum JudgementTimingCapturePhase
        {
            Disabled, Before, BeforeFrozen, Key, KeyIssued, KeyFrozen, Scratch, ScratchIssued, ScratchFrozen,
            CompletionKey, CompletionKeyIssued, Finished,
        }

        private readonly record struct JudgementTimingCaptureEdge(long Epoch, long Sequence, double GameplayTime);

        private JudgementTimingCapturePhase judgementTimingPhase;
        private BmsInputManager judgementTimingInput = null!;
        private BmsHitObject judgementTimingKey = null!;
        private BmsHitObject judgementTimingScratch = null!;
        private BmsHitObject judgementTimingCompletionKey = null!;
        private BmsGameplayLayoutLane judgementTimingKeyLane = null!;
        private BmsGameplayLayoutLane judgementTimingScratchLane = null!;
        private GameplaySkinLaneGroupId judgementTimingGroup = null!;
        private GameplaySkinJudgementTimingStateSnapshot judgementTimingAfterKey;
        private GameplaySkinEventSubscription? judgementTimingObserver;
        private JudgementTimingCaptureEdge? judgementTimingLastEdge;
        private JudgementTimingCaptureEdge? judgementTimingKeyEdge;
        private JudgementTimingCaptureEdge? judgementTimingScratchEdge;
        private long judgementTimingProbeFrame;
        private long judgementTimingKeyIssuedFrame;
        private long judgementTimingScratchIssuedFrame;
        private double? judgementTimingKeyInputTime;
        private double? judgementTimingScratchInputTime;
        private readonly List<object> judgementTimingInputs = [];
        private readonly List<object> judgementTimingEdges = [];
        private readonly List<GameplaySkinJudgementStateSnapshot> judgementTimingEdgeStates = [];
        private readonly List<object> judgementTimingClockControls = [];

        private void addJudgementTimingCaptureSteps()
        {
            AddStep("prepare actual key and scratch input against the decoded chart", () =>
            {
                Assert.That(player, Is.TypeOf<BmsSoloPlayer>());
                Assert.That(player.GameplayState.Mods.OfType<BmsModAutoplay>(), Is.Empty);
                Assert.That(renderer.HasReplayLoaded.Value, Is.False);
                judgementTimingInput = (BmsInputManager)renderer.GameplayInputManager!;
                Assert.That(judgementTimingInput.UseParentInput, Is.True);
                captureObservedHostActivity = host.IsActive.Value;
                captureHostActivity.BindTo((Bindable<bool>)host.IsActive);
                captureHostActivity.Value = true;
                judgementTimingKeyLane = renderer.Playfield.Lanes.Single(lane => lane.LayoutLane.Action == BmsAction.Key1).LayoutSnapshotLane!;
                judgementTimingScratchLane = renderer.Playfield.Lanes.Single(lane => lane.LayoutLane.Action == BmsAction.Scratch1).LayoutSnapshotLane!;
                judgementTimingGroup = judgementTimingKeyLane.NeutralLane.TopologyEntry.Identity.Group.Id;
                Assert.That(judgementTimingScratchLane.NeutralLane.TopologyEntry.Identity.Group.Id, Is.EqualTo(judgementTimingGroup));
                BmsHitObject[] notes = renderer.Beatmap.HitObjects.OfType<BmsHitObject>()
                    .Where(note => note is not BmsHoldNote && note.CountsForScore && !note.AutoPlay).OrderBy(note => note.StartTime).ToArray();
                judgementTimingKey = notes.First(note => note.LaneIndex == judgementTimingKeyLane.LogicalIndex);
                judgementTimingScratch = notes.First(note => note.LaneIndex == judgementTimingScratchLane.LogicalIndex);
                judgementTimingCompletionKey = notes.First(note => note.LaneIndex == judgementTimingKeyLane.LogicalIndex
                    && note.StartTime > judgementTimingScratch.StartTime + 100);
                judgementTimingObserver = renderer.GameplaySkinEventStream.Subscribe();
                judgementTimingPhase = JudgementTimingCapturePhase.Before;
                File.WriteAllText(Path.Combine(outputDirectory, "judgement-timing-plan.json"), JsonSerializer.Serialize(new
                {
                    boundary = "Actual BmsSoloPlayer with software OmsAction button pulses, simulated host activity and explicitly recorded software clock stops; not physical-device or foreground acceptance",
                    observedHostActive = captureObservedHostActivity,
                    hostActivitySimulated = true,
                    group = judgementTimingGroup.Value,
                    keyLane = judgementTimingKeyLane.LaneId.Value,
                    scratchLane = judgementTimingScratchLane.LaneId.Value,
                    keyNoteTime = judgementTimingKey.StartTime,
                    requestedKeyOffset = -60,
                    scratchNoteTime = judgementTimingScratch.StartTime,
                    requestedScratchOffset = 60,
                    completionKeyTime = judgementTimingCompletionKey.StartTime,
                    requestedCompletionKeyOffset = -5,
                    completionInputPurpose = "A further real on-time key input supplies positive EX when the deliberate non-PG timing samples are GOOD/BAD",
                    initializedAt = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                    mods = player.GameplayState.Mods.Select(mod => mod.Acronym).ToArray(),
                }));
            });
            AddUntilStep("the normal preparation naturally starts the gameplay clock", () => gameplayClock.IsRunning
                || judgementTimingPhase == JudgementTimingCapturePhase.BeforeFrozen);
            AddUntilStep("freeze a real frame before the two timing inputs", () => judgementTimingPhase == JudgementTimingCapturePhase.BeforeFrozen);
            addJudgementTimingSample("before", () =>
                Assert.That(lightingState().JudgementTimingGroups.All(entry => emptyTiming(entry.State)), Is.True));
            AddStep("resume toward the real early key input", () => resumeJudgementTiming(JudgementTimingCapturePhase.Key));
            AddUntilStep("the real key input has been observed", () => judgementTimingPhase == JudgementTimingCapturePhase.KeyFrozen);
            addJudgementTimingSample("key", () =>
            {
                judgementTimingAfterKey = timingGroup(lightingState());
                Assert.That(judgementTimingAfterKey.Keys?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Fast));
                Assert.That(judgementTimingAfterKey.Combined, Is.EqualTo(judgementTimingAfterKey.Keys));
                Assert.That(judgementTimingAfterKey.Scratch, Is.Null);
                assertRealTimingHit(judgementTimingKey, false);
            });
            AddStep("resume toward the real late normal scratch button", () => resumeJudgementTiming(JudgementTimingCapturePhase.Scratch));
            AddUntilStep("the real scratch input has been observed", () => judgementTimingPhase == JudgementTimingCapturePhase.ScratchFrozen);
            addJudgementTimingSample("scratch", () =>
            {
                GameplaySkinJudgementTimingStateSnapshot state = timingGroup(lightingState());
                Assert.That(state.Keys, Is.EqualTo(judgementTimingAfterKey.Keys), "A scratch must retain the key hint's original direction and application time.");
                Assert.That(state.Scratch?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow));
                Assert.That(state.Combined, Is.EqualTo(state.Scratch));
                assertRealTimingHit(judgementTimingScratch, true);
            });
            AddStep("resume the same chart for one further real scoring key", () => resumeJudgementTiming(JudgementTimingCapturePhase.CompletionKey));
            AddUntilStep("the additional real input has resolved", () => judgementTimingPhase == JudgementTimingCapturePhase.Finished);
            AddAssert("the additional normal input produced positive native EX", () => score.TotalScore.Value > 0);
            AddUntilStep("play the remaining chart naturally into its final range", () => score.HasCompleted.Value
                || renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime >= 9000);
        }

        // Runs in the existing frame probe after the real Game update. Input timestamps and results belong to
        // the live input manager and drawables; this probe never seeks or writes a judgement/score/hint state.
        private void sampleJudgementTimingInput()
        {
            if (judgementTimingPhase is JudgementTimingCapturePhase.Disabled or JudgementTimingCapturePhase.Finished)
                return;
            judgementTimingProbeFrame++;
            drainJudgementTimingEdges();
            double time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
            switch (judgementTimingPhase)
            {
                case JudgementTimingCapturePhase.Before when gameplayClock.IsRunning && time >= judgementTimingKey.StartTime - 250:
                    stopJudgementTiming(JudgementTimingCapturePhase.BeforeFrozen);
                    break;
                case JudgementTimingCapturePhase.Key when time >= judgementTimingKey.StartTime - 60:
                    pulseJudgementTiming(OmsAction.Key1P_1, judgementTimingKey, -60);
                    judgementTimingPhase = JudgementTimingCapturePhase.KeyIssued;
                    goto case JudgementTimingCapturePhase.KeyIssued;
                case JudgementTimingCapturePhase.KeyIssued:
                    if (judgementTimingConsumerReady(scratch: false, time))
                        stopJudgementTiming(JudgementTimingCapturePhase.KeyFrozen);
                    break;
                case JudgementTimingCapturePhase.Scratch when time >= judgementTimingScratch.StartTime + 60:
                    pulseJudgementTiming(OmsAction.Key1P_Scratch, judgementTimingScratch, 60);
                    judgementTimingPhase = JudgementTimingCapturePhase.ScratchIssued;
                    goto case JudgementTimingCapturePhase.ScratchIssued;
                case JudgementTimingCapturePhase.ScratchIssued:
                    if (judgementTimingConsumerReady(scratch: true, time))
                        stopJudgementTiming(JudgementTimingCapturePhase.ScratchFrozen);
                    break;
                case JudgementTimingCapturePhase.CompletionKey when time >= judgementTimingCompletionKey.StartTime - 5:
                    pulseJudgementTiming(OmsAction.Key1P_1, judgementTimingCompletionKey, -5);
                    judgementTimingPhase = JudgementTimingCapturePhase.CompletionKeyIssued;
                    goto case JudgementTimingCapturePhase.CompletionKeyIssued;
                case JudgementTimingCapturePhase.CompletionKeyIssued:
                    if (score.HitEvents.Any(hit => ReferenceEquals(hit.HitObject, judgementTimingCompletionKey))
                        || time >= judgementTimingCompletionKey.StartTime + 250)
                    {
                        drainJudgementTimingEdges();
                        judgementTimingObserver!.Dispose();
                        judgementTimingObserver = null;
                        judgementTimingPhase = JudgementTimingCapturePhase.Finished;
                        File.WriteAllText(Path.Combine(outputDirectory, "judgement-timing-inputs.json"), JsonSerializer.Serialize(new
                        {
                            inputs = judgementTimingInputs,
                            edges = judgementTimingEdges,
                            score = score.TotalScore.Value,
                            hits = score.HitEvents.Select(hit => new { hit.HitObject.StartTime, hit.TimeOffset, result = hit.Result.ToString() }).ToArray(),
                        }));
                    }
                    break;
            }
        }

        private void pulseJudgementTiming(OmsAction action, BmsHitObject note, double requestedOffset)
        {
            double time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
            if (judgementTimingPhase == JudgementTimingCapturePhase.Key)
            {
                judgementTimingKeyInputTime = time;
                judgementTimingKeyIssuedFrame = judgementTimingProbeFrame;
            }
            else if (judgementTimingPhase == JudgementTimingCapturePhase.Scratch)
            {
                judgementTimingScratchInputTime = time;
                judgementTimingScratchIssuedFrame = judgementTimingProbeFrame;
            }
            bool pressed = judgementTimingInput.TriggerOmsActionPressed(action);
            bool released = judgementTimingInput.TriggerOmsActionReleased(action);
            judgementTimingInputs.Add(new
            {
                action = action.ToString(),
                route = "BmsInputManager.TriggerOmsActionPressed/Released normal digital button",
                requestedOffset,
                note.StartTime,
                actualInputTime = time,
                observedOffset = time - note.StartTime,
                audioTime = gameplayClock.CurrentTime,
                pressed,
                released,
                judgementTimingInput.UseParentInput,
                hostActive = host.IsActive.Value,
                lateWindow = time > note.StartTime + note.HitWindows.WindowFor(HitResult.Meh),
            });
            drainJudgementTimingEdges();
            File.WriteAllText(Path.Combine(outputDirectory, "judgement-timing-dispatch.json"), JsonSerializer.Serialize(new
            {
                phase = judgementTimingPhase.ToString(),
                inputs = judgementTimingInputs,
                edges = judgementTimingEdges,
                consumer = judgementTimingConsumerEvidence(),
            }));
        }

        private bool judgementTimingConsumerReady(bool scratch, double time)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            long issuedFrame = scratch ? judgementTimingScratchIssuedFrame : judgementTimingKeyIssuedFrame;
            if (judgementTimingProbeFrame <= issuedFrame)
                return false;

            JudgementTimingCaptureEdge? edge = scratch ? judgementTimingScratchEdge : judgementTimingKeyEdge;
            GameplaySkinJudgementTimingStateSnapshot state = timingGroup(lightingState());
            bool hasHint = scratch ? state.Scratch.HasValue : state.Keys.HasValue;
            double noteTime = scratch ? judgementTimingScratch.StartTime : judgementTimingKey.StartTime;
            if (edge is { } applied)
                return runtime.CurrentEpoch == applied.Epoch && runtime.LastSequence >= applied.Sequence
                                                            && (hasHint || time >= noteTime + 250);

            // Preserve a real late/missed-input diagnostic as before, but never freeze a successful producer
            // hint before its exact judgement edge has reached the actual scene consumer.
            return !hasHint && time >= noteTime + 250 && runtime.CurrentEpoch == renderer.GameplaySkinEventStream.CurrentEpoch;
        }

        private object judgementTimingConsumerEvidence()
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            return new
            {
                streamEpoch = renderer.GameplaySkinEventStream.CurrentEpoch,
                runtimeEpoch = runtime.CurrentEpoch,
                runtime.LastSequence,
                runtime.LastGameplayTime,
                lastJudgementEdge = judgementTimingLastEdge,
                keyJudgementEdge = judgementTimingKeyEdge,
                scratchJudgementEdge = judgementTimingScratchEdge,
                probeFrame = judgementTimingProbeFrame,
                keyIssuedFrame = judgementTimingKeyIssuedFrame,
                scratchIssuedFrame = judgementTimingScratchIssuedFrame,
            };
        }

        private void stopJudgementTiming(JudgementTimingCapturePhase phase)
        {
            object consumerBeforeControl = judgementTimingConsumerEvidence();
            gameplayClock.Stop();
            judgementTimingPhase = phase;
            judgementTimingClockControls.Add(new { operation = "GameplayClockContainer.Stop", phase = phase.ToString(),
                time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime, audioTime = gameplayClock.CurrentTime, softwareClockControl = true,
                consumerBeforeControl, consumerAfterControl = judgementTimingConsumerEvidence() });
        }

        private void resumeJudgementTiming(JudgementTimingCapturePhase phase)
        {
            judgementTimingPhase = phase;
            gameplayClock.Start();
            judgementTimingClockControls.Add(new { operation = "GameplayClockContainer.Start", phase = phase.ToString(),
                time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime, audioTime = gameplayClock.CurrentTime, softwareClockControl = true,
                consumer = judgementTimingConsumerEvidence() });
        }

        private void drainJudgementTimingEdges()
            => judgementTimingObserver?.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinJudgementEventPayload judgement)
                {
                    var edge = new JudgementTimingCaptureEdge(envelope.Epoch, envelope.Sequence, envelope.GameplayTime);
                    judgementTimingLastEdge = edge;
                    if (envelope.GroupId == judgementTimingGroup && envelope.LaneId == judgementTimingKeyLane.LaneId
                        && judgementTimingKeyInputTime.HasValue && envelope.GameplayTime >= judgementTimingKeyInputTime.Value
                        && judgementTimingPhase is JudgementTimingCapturePhase.Key or JudgementTimingCapturePhase.KeyIssued)
                        judgementTimingKeyEdge = edge;
                    if (envelope.GroupId == judgementTimingGroup && envelope.LaneId == judgementTimingScratchLane.LaneId
                        && judgementTimingScratchInputTime.HasValue && envelope.GameplayTime >= judgementTimingScratchInputTime.Value
                        && judgementTimingPhase is JudgementTimingCapturePhase.Scratch or JudgementTimingCapturePhase.ScratchIssued)
                        judgementTimingScratchEdge = edge;
                    judgementTimingEdgeStates.Add(judgement.State);
                    judgementTimingEdges.Add(new { envelope.Epoch, envelope.Sequence, envelope.GameplayTime,
                        group = envelope.GroupId?.Value, lane = envelope.LaneId?.Value, judgement.State.ObjectId,
                        grade = judgement.State.Grade.ToString(), judgement.State.Offset, judgement.State.HasPlayerTiming,
                        state = timingEvidence(judgement.State.JudgementTimingState) });
                }
            });

        private void addJudgementTimingSample(string phase, Action assertPhase)
        {
            string name = "judgement-timing-" + phase;
            AddWaitStep("let the real timing edge reach every prepared consumer", 3);
            AddStep("record the actual timing state and native framebuffer", () =>
            {
                drainJudgementTimingEdges();
                GameplaySkinEventStateSnapshot state = lightingState();
                GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
                File.WriteAllText(Path.Combine(outputDirectory, name + ".json"), JsonSerializer.Serialize(new
                {
                    boundary = "Software-triggered normal Player inputs; host activity is simulated; screenshots use explicitly stopped software gameplay clock",
                    phase,
                    playerType = player.GetType().Name,
                    observedHostActive = captureObservedHostActivity,
                    hostActivitySimulated = true,
                    hostActive = host.IsActive.Value,
                    time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                    audioTime = gameplayClock.CurrentTime,
                    gameplayClock.IsRunning,
                    groups = state.JudgementTimingGroups.Select(entry => new { group = entry.GroupId.Value, state = timingEvidence(entry.State) }).ToArray(),
                    state.Score,
                    actualEx = score.TotalScore.Value,
                    inputs = judgementTimingInputs,
                    edges = judgementTimingEdges,
                    softwareClockControls = judgementTimingClockControls,
                    consumer = judgementTimingConsumerEvidence(),
                    variants = judgementTimingVariants(state, assertValues: false),
                    graph = timingGraphEvidence(),
                    state.ChartPresentation,
                    faults = runtime.RuntimeFaults,
                }));
                capture = saveCapture(name + ".png");
            });
            AddUntilStep("wait for the native timing screenshot", () => capture?.IsCompleted == true);
            AddStep("validate the observed state after preserving its evidence", () =>
            {
                capture!.GetAwaiter().GetResult();
                GameplaySkinEventStateSnapshot state = lightingState();
                Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                Assert.That(state.JudgementTimingGroups.Where(entry => entry.GroupId != judgementTimingGroup).All(entry => emptyTiming(entry.State)), Is.True,
                    "Another actual gameplay group must not borrow the first group's key or scratch hint.");
                judgementTimingVariants(state, assertValues: true);
                assertJudgementTimingNextFallback(state);
                assertPhase();
            });
        }

        private object[] judgementTimingVariants(GameplaySkinEventStateSnapshot state, bool assertValues)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinSceneRuntimeNode[] nodes = lightingNodes().Concat(renderer.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                .Where(visual => visual.IsApplied).SelectMany(visual => visual.RuntimeNodes)).Distinct().ToArray();
            var result = new List<object>();
            bool targetTimingVisible = false;
            foreach (GameplaySkinPreparedSceneVariant variant in runtime.PreparedScene.Program.Variants.Where(variant =>
                         variant.Source is GameplaySkinSceneBindingSource.JudgementTimingDirection or GameplaySkinSceneBindingSource.JudgementKeysTimingDirection
                             or GameplaySkinSceneBindingSource.JudgementScratchTimingDirection))
            foreach (GameplaySkinSceneRuntimeNode node in nodes.Where(node => node.PreparedNode.Source.Id == variant.TargetNodeId))
            {
                GameplaySkinLaneGroupId? group = node.PreparedNode.MaterialTarget?.GroupId;
                GameplaySkinJudgementTimingStateSnapshot channels = group == null ? default
                    : state.JudgementTimingGroups.Single(entry => entry.GroupId == group).State;
                GameplaySkinJudgementTimingHint? hint = variant.Source switch
                {
                    GameplaySkinSceneBindingSource.JudgementTimingDirection => channels.Combined,
                    GameplaySkinSceneBindingSource.JudgementKeysTimingDirection => channels.Keys,
                    _ => channels.Scratch,
                };
                string key = hint?.Direction switch { GameplaySkinJudgementTimingDirection.Fast => "fast", GameplaySkinJudgementTimingDirection.Slow => "slow", _ => "none" };
                GameplaySkinPreparedSceneValue expected = variant.SelectValue(key);
                targetTimingVisible |= group == judgementTimingGroup && key != "none" && lightingAlpha(node.ContentDrawable) > 0;
                Sprite? sprite = node.ContentDrawable as Sprite;
                string[] textures = sprite == null ? [] : runtime.PreparedScene.Resources.Where(resource => ReferenceEquals(resource.Texture, sprite.Texture)).Select(resource => resource.Id).ToArray();
                if (assertValues && variant.Property == GameplaySkinSceneProperty.Visible)
                    Assert.That(node.TransformDrawable.Alpha, Is.EqualTo(expected.BooleanValue ? 1 : 0));
                if (assertValues && variant.Property == GameplaySkinSceneProperty.Resource)
                    Assert.That(sprite!.Texture, Is.SameAs(expected.Resource!.Texture));
                result.Add(new { id = node.PreparedNode.Source.Id, node.InstanceId, group = group?.Value,
                    source = variant.Source.ToString(), property = variant.Property.ToString(), direction = key,
                    expectedVisible = variant.Property == GameplaySkinSceneProperty.Visible ? expected.BooleanValue : (bool?)null,
                    expectedResource = variant.Property == GameplaySkinSceneProperty.Resource ? expected.Resource!.Id : null,
                    actualResourceIds = textures, localAlpha = node.TransformDrawable.Alpha, effectiveAlpha = lightingAlpha(node.ContentDrawable),
                    geometry = lightingGeometry(node.ContentDrawable) });
            }
            if (assertValues && runtime.Publication.Snapshot.Context.OptionValues.TryGetValue("timing-display", out GameplaySkinOptionValue option)
                             && option.CanonicalValue != "off")
            {
                Assert.That(result, Is.Not.Empty, "The selected FAST/SLOW art must consume the real group timing directions.");
                if (timingGroup(state).Combined.HasValue)
                    Assert.That(targetTimingVisible, Is.True, "A real hint must reach a visible consumer in its exact native group.");
            }
            return result.ToArray();
        }

        private object[] timingGraphEvidence()
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            return lightingNodes().Where(node => node.InstanceId.StartsWith("gt.", StringComparison.Ordinal)).Select(node => (object)new
            {
                id = node.PreparedNode.Source.Id, node.InstanceId,
                text = node.ContentDrawable is GameplaySkinBitmapText bitmap ? bitmap.AuthorText : node.ContentDrawable is SpriteText text ? text.Text.ToString() : null,
                sources = runtime.PreparedScene.Program.Bindings.Where(binding => binding.TargetNodeId == node.PreparedNode.Source.Id)
                    .Select(binding => binding.Source.ToString()).ToArray(),
                effectiveAlpha = lightingAlpha(node.ContentDrawable),
            }).ToArray();
        }

        private void assertJudgementTimingNextFallback(GameplaySkinEventStateSnapshot state)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinOptionValues options = runtime.Publication.Snapshot.Context.OptionValues;
            if (!options.TryGetValue("graph-target", out GameplaySkinOptionValue primary) || primary.CanonicalValue != "next"
                || !options.TryGetValue("graph-sub-target", out GameplaySkinOptionValue fallback) || fallback.CanonicalValue != "maximum"
                || state.ChartPresentation?.PersonalBestScore != null)
                return;

            GameplaySkinSceneRuntimeNode[] graph = lightingNodes().Where(node => node.InstanceId.StartsWith("gt.", StringComparison.Ordinal)).ToArray();
            var maximumNodes = graph.Where(node => runtime.PreparedScene.Program.Bindings.Any(binding =>
                binding.TargetNodeId == node.PreparedNode.Source.Id && binding.Source == GameplaySkinSceneBindingSource.ScoreMaximum
                && binding.Property == GameplaySkinSceneProperty.Text) && lightingAlpha(node.ContentDrawable) > 0).ToArray();
            Assert.That(maximumNodes, Has.Length.EqualTo(1), "The real C2 NEXT choice must publish its chosen SUB maximum when the matching PB is absent.");
            Assert.That(((GameplaySkinBitmapText)maximumNodes[0].ContentDrawable).AuthorText,
                Is.EqualTo(state.Score.MaximumScore.ToString(CultureInfo.InvariantCulture)));
            Assert.That(graph.Any(node => node.ContentDrawable is SpriteText text && text.Text.ToString() == "SUB MAXIMUM"
                                         && lightingAlpha(node.ContentDrawable) > 0), Is.True);
        }

        private void addJudgementTimingCompletionSteps()
        {
            AddStep("record the same naturally completed chart", () =>
            {
                Assert.That(score.TotalScore.Value, Is.GreaterThan(0));
                Assert.That(score.HasCompleted.Value, Is.True);
                Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                File.WriteAllText(Path.Combine(outputDirectory, "judgement-timing-complete.json"), JsonSerializer.Serialize(new
                {
                    normalPlayer = player.GetType().Name,
                    score = score.TotalScore.Value,
                    highestCombo = score.HighestCombo.Value,
                    completed = score.HasCompleted.Value,
                    groups = lightingState().JudgementTimingGroups.Select(entry => new { group = entry.GroupId.Value, state = timingEvidence(entry.State) }).ToArray(),
                    hits = score.HitEvents.Select(hit => new { hit.HitObject.StartTime, hit.TimeOffset, result = hit.Result.ToString() }).ToArray(),
                }));
            });
            AddUntilStep("the same normal Player naturally reaches results", () => Game.ScreenStack.CurrentScreen is ResultsScreen { IsLoaded: true });
            AddStep("capture the real results screen", () =>
            {
                var results = (ResultsScreen)Game.ScreenStack.CurrentScreen!;
                File.WriteAllText(Path.Combine(outputDirectory, "judgement-timing-results.json"), JsonSerializer.Serialize(new
                {
                    screen = results.GetType().Name,
                    nativeEx = score.TotalScore.Value,
                    savedEx = results.Score?.TotalScore,
                    hostActivitySimulated = true,
                    inputs = judgementTimingInputs,
                    softwareClockControls = judgementTimingClockControls,
                }));
                capture = saveCapture("judgement-timing-results.png");
            });
            AddUntilStep("wait for the native results screenshot", () => capture?.IsCompleted == true);
            AddStep("verify the actual results after preserving the native screenshot", () =>
            {
                capture!.GetAwaiter().GetResult();
                Assert.That(((ResultsScreen)Game.ScreenStack.CurrentScreen!).Score?.TotalScore, Is.GreaterThan(0));
            });
        }

        private void assertRealTimingHit(BmsHitObject note, bool scratch)
        {
            JudgementTimingCaptureEdge? edge = scratch ? judgementTimingScratchEdge : judgementTimingKeyEdge;
            Assert.That(edge, Is.Not.Null, "The actual target judgement must have a captured stream sequence.");
            Assert.That(renderer.GameplaySkinSceneRuntime!.CurrentEpoch, Is.EqualTo(edge!.Value.Epoch));
            Assert.That(renderer.GameplaySkinSceneRuntime.LastSequence, Is.GreaterThanOrEqualTo(edge.Value.Sequence),
                "The scene must consume the actual judgement before the software observation clock stops.");
            var hit = score.HitEvents.Single(entry => ReferenceEquals(entry.HitObject, note));
            Assert.That(hit.Result, Is.AnyOf(HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh));
            Assert.That(scratch ? hit.TimeOffset > 0 : hit.TimeOffset < 0, Is.True);
            GameplaySkinLaneId lane = scratch ? judgementTimingScratchLane.LaneId : judgementTimingKeyLane.LaneId;
            Assert.That(judgementTimingEdgeStates.Any(edge => edge.GroupId == judgementTimingGroup && edge.LaneId == lane
                && edge.HasPlayerTiming && edge.Offset == hit.TimeOffset), Is.True,
                "The actual score hit and marked transport edge must retain the same group, lane and offset.");
        }

        private GameplaySkinJudgementTimingStateSnapshot timingGroup(GameplaySkinEventStateSnapshot state)
            => state.JudgementTimingGroups.Single(entry => entry.GroupId == judgementTimingGroup).State;

        private static bool emptyTiming(GameplaySkinJudgementTimingStateSnapshot state)
            => !state.Combined.HasValue && !state.Keys.HasValue && !state.Scratch.HasValue;

        private static object timingEvidence(GameplaySkinJudgementTimingStateSnapshot state)
            => new { combined = hintEvidence(state.Combined), keys = hintEvidence(state.Keys), scratch = hintEvidence(state.Scratch) };

        private static object? hintEvidence(GameplaySkinJudgementTimingHint? hint)
            => hint is { } value ? new { direction = value.Direction.ToString(), value.AppliedTime } : null;
    }
}
