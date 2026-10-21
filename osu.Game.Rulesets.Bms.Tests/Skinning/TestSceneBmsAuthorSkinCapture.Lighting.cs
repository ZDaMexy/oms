// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private double lightingOrigin;
        private double lightingFirstNote;
        private double lightingTarget;
        private double lightingCapturedTime;
        private TaskCompletionSource<bool>? lightingAudioSeek;

        private void addLightingDetailsCaptureSteps()
        {
            AddUntilStep("wait for the complete real lighting scene and its clock authority", () =>
                renderer.GameplaySkinSceneRuntime!.IsSceneReady && lightingState().Timing.StartTime.HasValue);
            AddStep("retain the real entry origin and post-mod first-note times", () =>
            {
                GameplaySkinEventStateSnapshot state = lightingState();
                lightingOrigin = state.Timing.StartTime!.Value;
                Assert.That(lightingOrigin, Is.EqualTo(gameplayClock.StartTime));
                Assert.That(state.ChartPresentation?.Arrivals, Is.Not.Empty);
                lightingFirstNote = state.ChartPresentation!.Arrivals.Min(arrival => arrival.Time);
                Assert.That(state.ChartPresentation.UsesExScore, Is.True);
                Assert.That(renderer.GameplaySkinSceneRuntime!.PreparedScene.Program.Tracks.Any(track =>
                    track.ClockSource == GameplaySkinSceneBindingSource.TimingElapsed), Is.True);
            });

            int index = 0;
            foreach (double elapsed in new[] { 200d, 500d, 1100d })
            {
                double phase = elapsed;
                addLightingSample(++index, () => lightingOrigin + phase, "entry", phase);
            }
            addLightingSample(++index, () => lightingFirstNote - 200, "before-first-note");
            addLightingSample(++index, () => lightingFirstNote + 100, "after-first-note");
            addLightingSample(++index, () => lightingFirstNote + 500, "real-counters");
            AddStep("resume the same complete autoplay chart after lighting captures", () => gameplayClock.Start());
        }

        private void addLightingSample(int index, Func<double> target, string purpose, double? requestedElapsed = null)
        {
            string filename = $"lighting-sample-{index:00}";
            AddStep($"seek the real replay for lighting sample {index}", () =>
            {
                lightingTarget = target();
                gameplayClock.Stop();
                gameplayClock.Seek(lightingTarget); // Preserve the native seek/rewind notification.
                FramedBeatmapClock framed = gameplayClock.ChildrenOfType<FramedBeatmapClock>().Single();
                var source = (IAdjustableClock)framed.Source;
                double sourceTime = lightingTarget - framed.TotalAppliedOffset;
                var acknowledgement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lightingAudioSeek = acknowledgement;
                // TrackVirtual's audio update also writes its position while stopped. Serialise the
                // backing-source seek there; never reset the Player's entry origin or emit skin events.
                host.AudioThread.Scheduler.Add(() => acknowledgement.SetResult(source.Seek(sourceTime)));
            });
            AddUntilStep($"wait for authoritative lighting sample {index}", () =>
                lightingAudioSeek?.Task.IsCompleted == true
                && Math.Abs(renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime - lightingTarget) < 1
                && Math.Abs(renderer.GameplaySkinSceneRuntime!.LastGameplayTime - lightingTarget) < 1);
            AddStep($"record actual lighting geometry and framebuffer {index}", () =>
            {
                lightingCapturedTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
                recordLightingSample(filename, purpose, requestedElapsed);
                capture = saveCapture(filename + ".png");
            });
            AddUntilStep($"wait for actual lighting framebuffer {index}", () => capture?.IsCompleted == true);
            AddStep($"verify lighting sample {index} stayed paused", () =>
            {
                capture!.GetAwaiter().GetResult();
                Assert.That(renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                    Is.EqualTo(lightingCapturedTime).Within(0.1));
                assertLightingTracks(lightingNodes().ToArray(), lightingState());
            });
        }

        private GameplaySkinEventStateSnapshot lightingState()
        {
            GameplaySkinEventStateSnapshot? state = null;
            using GameplaySkinEventSubscription subscription = renderer.GameplaySkinEventStream.Subscribe();
            subscription.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinStateEventPayload snapshot)
                    state = snapshot.State;
            });
            Assert.That(state, Is.Not.Null, "The normal engine subscription must supply a real retained snapshot.");
            return state!;
        }

        private IEnumerable<GameplaySkinSceneRuntimeNode> lightingNodes()
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            foreach (GameplaySkinPreparedSceneNode prepared in runtime.PreparedScene.Roots.SelectMany(lightingTree))
                if (runtime.TryGetRuntimeNode(prepared.InstanceId, out GameplaySkinSceneRuntimeNode? node))
                    yield return node!;
        }

        private static IEnumerable<GameplaySkinPreparedSceneNode> lightingTree(GameplaySkinPreparedSceneNode root)
        {
            yield return root;
            foreach (GameplaySkinPreparedSceneNode child in root.Children.SelectMany(lightingTree))
                yield return child;
        }

        private void recordLightingSample(string filename, string purpose, double? requestedElapsed)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinEventStateSnapshot state = lightingState();
            GameplaySkinSceneRuntimeNode[] nodes = lightingNodes().ToArray();
            Assert.That(state.Timing.StartTime, Is.EqualTo(lightingOrigin), "Seeking must retain the actual Player entry origin.");
            Assert.That(runtime.RuntimeFaults, Is.Empty);
            assertLightingTracks(nodes, state);
            assertLightingGeometry(nodes);
            assertLightingCounters(nodes, state);

            GameplaySkinSceneRuntimeNode[] cues = nodes.Where(node => node.PreparedNode.Source.Id.Contains(".arrival.", StringComparison.Ordinal)
                && node.PreparedNode.Source.Id.EndsWith(".before-note", StringComparison.Ordinal)).ToArray();
            Assert.That(cues.Length, Is.EqualTo(runtime.Publication.Snapshot.LanesInLogicalOrder.Count));
            foreach (GameplaySkinSceneRuntimeNode cue in cues)
            {
                GameplaySkinLaneArrival arrival = state.ChartPresentation!.Arrivals.Single(item =>
                    item.LaneId == cue.PreparedNode.MaterialTarget!.LaneId);
                Assert.That(cue.TransformDrawable.Alpha, Is.EqualTo(lightingCapturedTime < arrival.Time ? 1 : 0));
            }

            File.WriteAllText(Path.Combine(outputDirectory, filename + ".json"), JsonSerializer.Serialize(new
            {
                purpose,
                requestedElapsed,
                requestedGameplayTime = lightingTarget,
                actualGameplayTime = lightingCapturedTime,
                actualElapsed = lightingCapturedTime - lightingOrigin,
                entryOrigin = lightingOrigin,
                audioSeekAccepted = lightingAudioSeek!.Task.GetAwaiter().GetResult(),
                state.Timing,
                state.Score,
                state.ChartPresentation,
                nativeStatistics = score.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                nativeTimingHits = score.HitEvents.Where(hit => hit.HitObject.Judgement.MaxResult == HitResult.Perfect)
                                        .Select(hit => new { hit.TimeOffset, hit.Result, hit.HitObject.StartTime }).ToArray(),
                bga = renderer.ChildrenOfType<DefaultBmsBgaPanelDisplay>().Single().NativeFrameVisuals.Select(lightingGeometry).ToArray(),
                nodes = nodes.Where(node => node.PreparedNode.Source.Id.StartsWith("li", StringComparison.Ordinal)
                    || node.PreparedNode.Source.Id.Contains(".lighting-panels.", StringComparison.Ordinal)
                    || node.PreparedNode.Source.Id.Contains(".arrival.", StringComparison.Ordinal)
                    || node.PreparedNode.Source.Id.Contains(".motion-", StringComparison.Ordinal)
                    || node.PreparedNode.Source.Id.EndsWith(".motor", StringComparison.Ordinal)).Select(node => new
                    {
                        id = node.PreparedNode.Source.Id,
                        node.InstanceId,
                        layer = node.PreparedNode.Layer.ToString(),
                        node.TransformDrawable.Alpha,
                        node.TransformDrawable.Rotation,
                        effectiveAlpha = lightingAlpha(node.ContentDrawable),
                        geometry = lightingGeometry(node.ContentDrawable),
                        text = node.ContentDrawable is SpriteText text ? text.Text.ToString() : null,
                        quad = node.ContentDrawable is GameplaySkinProjectiveSprite sprite ? sprite.AppliedQuad : null,
                    }).ToArray(),
            }));
        }

        private void assertLightingTracks(GameplaySkinSceneRuntimeNode[] nodes, GameplaySkinEventStateSnapshot state)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            double elapsed = lightingCapturedTime - state.Timing.StartTime!.Value;
            foreach (GameplaySkinPreparedSceneTrack track in runtime.PreparedScene.Program.Tracks.Where(track =>
                         track.ClockSource == GameplaySkinSceneBindingSource.TimingElapsed))
            {
                GameplaySkinPreparedSceneKeyframe expected = track.Keyframes.LastOrDefault(frame => frame.Time <= elapsed) ?? track.Keyframes[0];
                foreach (GameplaySkinSceneRuntimeNode node in nodes.Where(node => node.PreparedNode.Source.Id == track.TargetNodeId))
                {
                    if (track.Property == GameplaySkinSceneProperty.ProjectiveQuad)
                    {
                        GameplaySkinProjectiveQuad actual = ((GameplaySkinProjectiveSprite)node.ContentDrawable).AppliedQuad!;
                        Assert.That(actual, Is.Not.Null);
                        Assert.That(actual.Opacity, Is.EqualTo(expected.Value.ProjectiveQuad!.Opacity).Within(0.000001));
                        for (int corner = 0; corner < 4; corner++)
                            Assert.That(actual.GetVertex(corner), Is.EqualTo(expected.Value.ProjectiveQuad.GetVertex(corner)));
                    }
                    else if (track.Property == GameplaySkinSceneProperty.Opacity)
                        Assert.That(node.TransformDrawable.Alpha, Is.EqualTo(expected.Value.NumberValue).Within(0.001));
                }
            }
            GameplaySkinSceneRuntimeNode[] motors = nodes.Where(node => node.PreparedNode.Source.Id.EndsWith(".motor", StringComparison.Ordinal)).ToArray();
            Assert.That(motors.Length, Is.EqualTo(doublePlay ? 2 : 1));
            foreach (GameplaySkinSceneRuntimeNode motor in motors)
            {
                GameplaySkinPreparedSceneTrack track = runtime.PreparedScene.Program.Tracks.Single(track =>
                    track.TargetNodeId == motor.PreparedNode.Source.Id && track.Property == GameplaySkinSceneProperty.Rotation);
                double period = track.Keyframes[^1].Time;
                double phase = ((lightingCapturedTime % period) + period) % period;
                Assert.That(motor.TransformDrawable.Rotation, Is.EqualTo(phase / period * 360).Within(0.02));
            }
        }

        private void assertLightingGeometry(GameplaySkinSceneRuntimeNode[] nodes)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinSceneRuntimeNode[] glows = nodes.Where(node => node.PreparedNode.Source.Id.EndsWith(".motion-beat", StringComparison.Ordinal)).ToArray();
            Assert.That(glows.Length, Is.EqualTo(doublePlay ? 2 : 1));
            Assert.That(glows.All(node => node.PreparedNode.Layer == GameplaySkinSceneLayer.Underlay), Is.True);
            recordBeamLayer(true);
            foreach (GameplaySkinSceneHostedSlot slot in runtime.HostedSlots.Where(slot =>
                         slot.Key.Slot == GameplaySkinSlotCatalog.Note || slot.Key.Slot == GameplaySkinSlotCatalog.LongNoteHead
                         || slot.Key.Slot == GameplaySkinSlotCatalog.LongNoteBody || slot.Key.Slot == GameplaySkinSlotCatalog.LongNoteTail))
                Assert.That(slot.Layer, Is.EqualTo(GameplaySkinSceneLayer.Object), slot.Key.Slot.Id);

            bool graphVisible = runtime.Publication.Snapshot.Context.OptionValues.TryGetValue("graph-visible", out GameplaySkinOptionValue graph) && graph.BooleanValue;
            GameplaySkinSceneRuntimeNode panel = nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".lighting-panels.graph.panel", StringComparison.Ordinal));
            Assert.That(lightingAlpha(panel.ContentDrawable) > 0, Is.EqualTo(graphVisible));
            var bga = renderer.ChildrenOfType<DefaultBmsBgaPanelDisplay>().Single();
            Assert.That(runtime.Publication.Snapshot.BgaViewports, Is.Not.Empty, "These Omni author windows must be admitted, not silently omitted.");
            Assert.That(bga.NativeFrameVisuals.Count, Is.EqualTo(runtime.Publication.Snapshot.BgaViewports.Count));
            foreach (Drawable viewport in bga.NativeFrameVisuals)
            {
                if (graphVisible)
                    Assert.That(lightingDisjoint(panel.ContentDrawable, viewport), Is.True, "Enabled score graph must not cover a real BGA viewport.");
                foreach (GameplaySkinSceneRuntimeNode judge in nodes.Where(node =>
                             node.PreparedNode.Source.Id.Contains(".lighting-panels.judge.", StringComparison.Ordinal)
                             && (node.ContentDrawable is Sprite or SpriteText) && lightingAlpha(node.ContentDrawable) > 0))
                    Assert.That(lightingDisjoint(judge.ContentDrawable, viewport), Is.True, "JUDGE must leave every real BGA viewport clear.");
            }
        }

        private void assertLightingCounters(GameplaySkinSceneRuntimeNode[] nodes, GameplaySkinEventStateSnapshot state)
        {
            foreach ((string suffix, HitResult result) in new[] { ("pg", HitResult.Perfect), ("gr", HitResult.Great), ("gd", HitResult.Good),
                         ("bd", HitResult.Meh), ("pr", HitResult.Miss), ("cb", HitResult.ComboBreak) })
            {
                GameplaySkinSceneRuntimeNode counter = nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".judge.bms-" + suffix, StringComparison.Ordinal));
                if (lightingAlpha(counter.ContentDrawable) > 0)
                    Assert.That(((SpriteText)counter.ContentDrawable).Text.ToString(), Is.EqualTo(score.Statistics.GetValueOrDefault(result).ToString(CultureInfo.InvariantCulture)));
            }
            GameplaySkinSceneRuntimeNode graph = nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".lighting-panels.graph.current", StringComparison.Ordinal));
            if (lightingAlpha(graph.ContentDrawable) > 0)
                Assert.That(((SpriteText)graph.ContentDrawable).Text.ToString(), Is.EqualTo(((BmsScoreProcessor)score).CurrentExScore.ToString(CultureInfo.InvariantCulture)));
            Assert.That(state.Score.Score, Is.EqualTo(((BmsScoreProcessor)score).CurrentExScore));
            if (lightingCapturedTime >= lightingFirstNote + 500)
                Assert.That(state.Score.Statistics.Perfect, Is.Positive, "The counter capture must contain real autoplay judgements.");
            Assert.That(state.Score.Statistics.Fast, Is.Zero, "Exact native autoplay must not count floating-point residue as FAST.");
            Assert.That(state.Score.Statistics.Slow, Is.Zero, "Exact native autoplay must not count floating-point residue as SLOW.");
            foreach (string suffix in new[] { "fast", "slow" })
            {
                int expected = suffix == "fast" ? state.Score.Statistics.Fast : state.Score.Statistics.Slow;
                GameplaySkinSceneRuntimeNode counter = nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".judge.bms-" + suffix, StringComparison.Ordinal));
                if (lightingAlpha(counter.ContentDrawable) > 0)
                    Assert.That(((SpriteText)counter.ContentDrawable).Text.ToString(), Is.EqualTo(expected.ToString(CultureInfo.InvariantCulture)));
            }
            GameplaySkinSceneRuntimeNode offset = nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".judge.offset", StringComparison.Ordinal));
            if (nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".lighting-panels.judge", StringComparison.Ordinal)).TransformDrawable.Alpha > 0)
                Assert.That(offset.TransformDrawable.Alpha > 0, Is.EqualTo(state.Score.OffsetState.IsAvailable));
            if (lightingAlpha(offset.TransformDrawable) > 0)
            {
                GameplaySkinSceneRuntimeNode value = nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".judge.offset-value", StringComparison.Ordinal));
                Assert.That(((SpriteText)value.ContentDrawable).Text.ToString(),
                    Is.EqualTo(state.Score.OffsetState.Milliseconds.ToString("0.00", CultureInfo.InvariantCulture)));
                GameplaySkinSceneRuntimeNode automatic = nodes.Single(node => node.PreparedNode.Source.Id.EndsWith(".judge.automatic", StringComparison.Ordinal));
                Assert.That(automatic.TransformDrawable.Alpha > 0, Is.EqualTo(state.Score.OffsetState.Automatic));
            }
            foreach (GameplaySkinSceneRuntimeNode front in nodes.Where(node => node.PreparedNode.Source.Id.EndsWith(".motion-front", StringComparison.Ordinal)))
                Assert.That(front.TransformDrawable.X, Is.EqualTo(state.Score.Gauge).Within(0.0001));
        }

        private static float lightingAlpha(Drawable drawable)
        {
            float alpha = 1;
            for (Drawable? node = drawable; node != null; node = node.Parent)
                alpha *= node.Alpha;
            return alpha;
        }

        private static object lightingGeometry(Drawable drawable)
        {
            var bounds = drawable.ScreenSpaceDrawQuad.AABBFloat;
            return new { bounds.Left, bounds.Top, bounds.Width, bounds.Height };
        }

        private static bool lightingDisjoint(Drawable first, Drawable second)
        {
            var left = first.ScreenSpaceDrawQuad.AABBFloat;
            var right = second.ScreenSpaceDrawQuad.AABBFloat;
            return left.Right <= right.Left + 0.05 || right.Right <= left.Left + 0.05
                || left.Bottom <= right.Top + 0.05 || right.Bottom <= left.Top + 0.05;
        }
    }
}
