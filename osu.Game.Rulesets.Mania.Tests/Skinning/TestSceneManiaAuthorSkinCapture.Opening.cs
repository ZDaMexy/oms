// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    public partial class TestSceneManiaAuthorSkinCapture
    {
        private static readonly FieldInfo opening_applied_frame = typeof(GameplaySkinRecordedAnimationDrawable)
            .GetField("appliedFrame", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private void addOpeningCaptureSteps()
        {
            int index = 0;
            int[] sampleTimes = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_OPENING_SAMPLES") is string samples
                ? samples.Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray()
                : [600, 1600, 2600, 3800, 4400, 4900];
            foreach (int elapsed in sampleTimes)
            {
                int requested = elapsed;
                string filename = $"opening-{++index:00}-{requested:00000}";
                AddUntilStep($"capture natural preparation at {requested}ms", () =>
                {
                    GameplaySkinEventStateSnapshot state = openingState();
                    if (!state.Timing.Preparation.Available || state.Timing.Preparation.Elapsed < requested)
                        return false;

                    Assert.That(state.Timing.Preparation.Active, Is.True,
                        "The opening must be captured while its real preparation phase is active; it must never be reconstructed by seeking.");
                    Assert.That(player.ChildrenOfType<GameplayClockContainer>().Single().IsRunning, Is.False);
                    recordOpeningSample(filename, requested, state);
                    capture = saveOpeningCapture(filename + ".png");
                    return true;
                });
                AddUntilStep($"wait for natural opening PNG {requested}", () => capture?.IsCompleted == true);
                AddStep($"propagate opening PNG {requested} and record its completion boundary", () =>
                {
                    capture!.GetAwaiter().GetResult();
                    GameplayClockContainer clock = player.ChildrenOfType<GameplayClockContainer>().Single();
                    File.WriteAllText(Path.Combine(output, filename + ".capture.json"), JsonSerializer.Serialize(new
                    {
                        requestedPreparationElapsed = requested,
                        completionBoundary = openingState().Timing,
                        authoritativeGameplayTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                        clock.CurrentTime,
                        clock.IsRunning,
                    }));
                });
            }

            AddUntilStep("the same preparation naturally finishes once", () =>
            {
                GameplaySkinPreparationStateSnapshot preparation = openingState().Timing.Preparation;
                return preparation.Available && !preparation.Active;
            });
            AddStep("opening and GET READY leave the real scene before its first note", () =>
            {
                GameplaySkinEventStateSnapshot state = openingState();
                Assert.Multiple(() =>
                {
                    Assert.That(state.Timing.Preparation.Available, Is.True);
                    Assert.That(state.Timing.Preparation.Active, Is.False);
                    Assert.That(state.Timing.Preparation.Ready, Is.False);
                    Assert.That(state.Timing.Preparation.Elapsed, Is.GreaterThanOrEqualTo(5000));
                    Assert.That(state.ChartPresentation?.Arrivals, Is.Not.Empty);
                    Assert.That(renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                        Is.LessThan(state.ChartPresentation!.Arrivals.Min(arrival => arrival.Time)));
                    Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                });
                foreach (GameplaySkinSceneRuntimeNode node in openingNodes().Where(isOpeningRecordedNode))
                {
                    var animation = (GameplaySkinRecordedAnimationDrawable)node.ContentDrawable;
                    Assert.That(animation.ChildrenOfType<Sprite>().Count(piece => piece.Alpha > 0), Is.Zero,
                        $"The completed recorded opening {node.PreparedNode.Source.Id} must retain no active render pieces.");
                }
                GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
                var cueTargets = runtime.PreparedScene.Program.Bindings
                    .Where(binding => binding.Source == GameplaySkinSceneBindingSource.ArrivalCueOpacity)
                    .Select(binding => binding.TargetNodeId).ToHashSet(StringComparer.Ordinal);
                GameplaySkinSceneRuntimeNode[] cues = openingNodes().Where(node => cueTargets.Contains(node.PreparedNode.Source.Id)).ToArray();
                Assert.That(cues, Is.Not.Empty);
                foreach (GameplaySkinSceneRuntimeNode cue in cues)
                    Assert.That(openingAlpha(cue.ContentDrawable), Is.Zero, "A late first note must not keep its cue after GET READY ends.");
                var rootNodeIds = runtime.PreparedScene.Roots.SelectMany(openingTree).Select(node => node.Source.Id).ToHashSet(StringComparer.Ordinal);
                GameplaySkinPreparedSceneBinding[] panelBindings = [.. runtime.PreparedScene.Program.Bindings.Where(binding =>
                    rootNodeIds.Contains(binding.TargetNodeId) && binding.Source == GameplaySkinSceneBindingSource.Option
                    && binding.OptionId is "graph-visible" or "judge-panel-visible")];
                Assert.That(panelBindings.Select(binding => binding.OptionId).Distinct(),
                    Is.EquivalentTo(new[] { "graph-visible", "judge-panel-visible" }));
                foreach (GameplaySkinPreparedSceneBinding binding in panelBindings)
                {
                    bool enabled = runtime.Publication.Snapshot.Context.OptionValues.Values[binding.OptionId!].BooleanValue;
                    GameplaySkinSceneRuntimeNode panel = openingNodes().Single(node => node.PreparedNode.Source.Id == binding.TargetNodeId);
                    Assert.That(openingAlpha(panel.ContentDrawable) > 0, Is.EqualTo(enabled), binding.OptionId);
                }
                recordOpeningSample("opening-complete", null, state);
            });
        }

        private GameplaySkinEventStateSnapshot openingState()
        {
            GameplaySkinEventStateSnapshot? state = null;
            using GameplaySkinEventSubscription subscription = renderer.GameplaySkinEventStream.Subscribe();
            subscription.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinStateEventPayload snapshot)
                    state = snapshot.State;
            });
            Assert.That(state, Is.Not.Null, "The native event stream must supply its actual retained state.");
            return state!;
        }

        private IEnumerable<GameplaySkinSceneRuntimeNode> openingNodes()
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            foreach (GameplaySkinPreparedSceneNode prepared in runtime.PreparedScene.Roots.SelectMany(openingTree))
                if (runtime.TryGetRuntimeNode(prepared.InstanceId, out GameplaySkinSceneRuntimeNode? node))
                    yield return node!;
        }

        private static IEnumerable<GameplaySkinPreparedSceneNode> openingTree(GameplaySkinPreparedSceneNode root)
        {
            yield return root;
            foreach (GameplaySkinPreparedSceneNode child in root.Children.SelectMany(openingTree))
                yield return child;
        }

        private void recordOpeningSample(string filename, int? requested, GameplaySkinEventStateSnapshot state)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplayClockContainer clock = player.ChildrenOfType<GameplayClockContainer>().Single();
            GameplaySkinSceneRuntimeNode[] nodes = openingNodes().ToArray();
            GameplaySkinSceneRuntimeNode[] recorded = nodes.Where(isOpeningRecordedNode).ToArray();
            GameplaySkinPreparedSceneBinding[] offsetBindings = runtime.PreparedScene.Program.Bindings.Where(binding =>
                binding.Source is GameplaySkinSceneBindingSource.OffsetValue or GameplaySkinSceneBindingSource.OffsetAvailable
                    or GameplaySkinSceneBindingSource.OffsetAutomatic).ToArray();
            var offsetTargets = offsetBindings.Select(binding => binding.TargetNodeId).ToHashSet(StringComparer.Ordinal);
            GameplaySkinPreparedSceneTrack[] entranceTracks = runtime.PreparedScene.Program.Tracks.Where(track =>
                track.ClockSource == GameplaySkinSceneBindingSource.PreparationElapsed && track.Property == GameplaySkinSceneProperty.Y).ToArray();
            var entranceTargets = entranceTracks.Select(track => track.TargetNodeId).ToHashSet(StringComparer.Ordinal);
            GameplaySkinPreparedSceneNode[] judgeRoots = runtime.PreparedScene.Roots.SelectMany(openingTree).Where(prepared =>
                entranceTargets.Contains(prepared.Source.Id) && openingTree(prepared).Any(child => offsetTargets.Contains(child.Source.Id))).ToArray();
            var judgeIds = judgeRoots.SelectMany(openingTree).Select(prepared => prepared.InstanceId).ToHashSet(StringComparer.Ordinal);

            Assert.Multiple(() =>
            {
                Assert.That(runtime.PreparedScene.Program.PreparationDuration, Is.EqualTo(5000));
                Assert.That(recorded.Any(node => node.PreparedNode.Source.ClockSource == GameplaySkinSceneBindingSource.PreparationElapsed), Is.True);
                Assert.That(recorded.Any(node => node.PreparedNode.Source.ClockSource == GameplaySkinSceneBindingSource.PreparationReadyElapsed), Is.True);
                Assert.That(judgeRoots, Is.Not.Empty, "Identify the actual JUDGE entrance from its applied offset bindings, not an author-node name.");
                Assert.That(runtime.RuntimeFaults, Is.Empty);
            });
            foreach (GameplaySkinPreparedSceneBinding binding in offsetBindings)
                foreach (GameplaySkinSceneRuntimeNode node in nodes.Where(node => node.PreparedNode.Source.Id == binding.TargetNodeId))
                {
                    if (binding.Source == GameplaySkinSceneBindingSource.OffsetValue)
                    {
                        if (openingAlpha(node.ContentDrawable) > 0)
                            Assert.That(((SpriteText)node.ContentDrawable).Text.ToString(),
                                Is.EqualTo(state.Score.OffsetState.Milliseconds.ToString("+0.00;-0.00;+0.00", CultureInfo.InvariantCulture)));
                    }
                    else
                        Assert.That(node.TransformDrawable.Alpha > 0,
                            Is.EqualTo(binding.Source == GameplaySkinSceneBindingSource.OffsetAvailable
                                ? state.Score.OffsetState.IsAvailable : state.Score.OffsetState.Automatic));
                }

            string package = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_PACKAGE")!;
            File.WriteAllText(Path.Combine(output, filename + ".json"), JsonSerializer.Serialize(new
            {
                purpose = requested.HasValue ? "natural-parent-clock-opening" : "natural-preparation-completed-before-first-note",
                requestedPreparationElapsed = requested,
                sampleLateness = requested.HasValue ? state.Timing.Preparation.Elapsed - requested.Value : (double?)null,
                package = Path.GetFullPath(package),
                packageBytes = new FileInfo(package).Length,
                keymode = runtime.Publication.Snapshot.Context.KeymodeId,
                presentation = runtime.Publication.Snapshot.Context.PresentationStyleId,
                options = runtime.Publication.Snapshot.Context.OptionValues.Values.ToDictionary(pair => pair.Key, pair => pair.Value.CanonicalValue),
                actualGameplayTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                runtimeGameplayTime = runtime.LastGameplayTime,
                clock.CurrentTime,
                clock.IsRunning,
                state.Timing,
                state.Score,
                state.ChartPresentation,
                nativeStatistics = score.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                nativeHitCount = score.HitEvents.Count,
                recorded = recorded.Select(node => openingRecordedSample(node, state.Timing.Preparation)).ToArray(),
                judgeBindings = offsetBindings.Select(binding => new
                {
                    binding.Id,
                    target = binding.TargetNodeId,
                    source = binding.Source.ToString(),
                    property = binding.Property.ToString(),
                }).ToArray(),
                judgeEntrance = entranceTracks.Where(track => judgeRoots.Any(root => root.Source.Id == track.TargetNodeId)).Select(track => new
                {
                    track.Id,
                    target = track.TargetNodeId,
                    clock = track.ClockSource.ToString(),
                    keyframes = track.Keyframes.Select(frame => new { frame.Time, value = frame.Value.NumberValue }).ToArray(),
                }).ToArray(),
                judgeNodes = nodes.Where(node => judgeIds.Contains(node.InstanceId)).Select(node => new
                {
                    id = node.PreparedNode.Source.Id,
                    node.InstanceId,
                    resource = node.PreparedNode.Source.ResourceId,
                    resolvedResource = node.PreparedNode.Resource?.Id,
                    actualTextureResources = node.ContentDrawable is Sprite sprite
                        ? runtime.PreparedScene.Resources.Where(resource => resource.Texture != null && ReferenceEquals(resource.Texture, sprite.Texture))
                                 .Select(resource => resource.Id).ToArray()
                        : Array.Empty<string>(),
                    node.TransformDrawable.Alpha,
                    node.TransformDrawable.X,
                    node.TransformDrawable.Y,
                    effectiveAlpha = openingAlpha(node.ContentDrawable),
                    text = node.ContentDrawable is SpriteText text ? text.Text.ToString() : null,
                    authorText = (node.ContentDrawable as GameplaySkinBitmapText)?.AuthorText,
                    geometry = openingGeometry(node.ContentDrawable),
                }).ToArray(),
                runtimeFaults = runtime.RuntimeFaults.ToArray(),
            }));
        }

        private static bool isOpeningRecordedNode(GameplaySkinSceneRuntimeNode node) =>
            node.ContentDrawable is GameplaySkinRecordedAnimationDrawable
            && node.PreparedNode.Source.ClockSource is GameplaySkinSceneBindingSource.PreparationElapsed
                or GameplaySkinSceneBindingSource.PreparationReadyElapsed;

        private static object openingRecordedSample(GameplaySkinSceneRuntimeNode node, GameplaySkinPreparationStateSnapshot preparation)
        {
            var animation = (GameplaySkinRecordedAnimationDrawable)node.ContentDrawable;
            int actualFrame = (int)opening_applied_frame.GetValue(animation)!;
            double clock = node.PreparedNode.Source.ClockSource == GameplaySkinSceneBindingSource.PreparationElapsed
                ? preparation.Available ? preparation.Elapsed : -1
                : preparation.Ready ? preparation.ReadyElapsed : -1;
            int activePieces = animation.ChildrenOfType<Sprite>().Count(piece => piece.Alpha > 0);
            Assert.That(activePieces, Is.EqualTo(animation.Program.GetPreparedFrame(actualFrame).PieceCount));
            if (clock < 0 || clock >= animation.Program.Duration)
                Assert.That(activePieces, Is.Zero);
            return new
            {
                id = node.PreparedNode.Source.Id,
                node.InstanceId,
                animation = node.PreparedNode.Source.AnimationId,
                clockSource = node.PreparedNode.Source.ClockSource.ToString(),
                clockMilliseconds = clock,
                expectedFrame = animation.Program.FrameAt(clock),
                actualFrame,
                framePieces = animation.Program.GetPreparedFrame(actualFrame).PieceCount,
                activePieceDrawables = activePieces,
                effectivePieceDrawables = openingAlpha(animation) > 0 ? activePieces : 0,
                animation.Program.FrameCount,
                animation.Program.FramesPerSecond,
                animation.Program.Duration,
                node.TransformDrawable.Alpha,
                effectiveAlpha = openingAlpha(animation),
                geometry = openingGeometry(animation),
            };
        }

        private static float openingAlpha(Drawable drawable)
        {
            float alpha = 1;
            for (Drawable? ancestor = drawable; ancestor != null; ancestor = ancestor.Parent)
                alpha *= ancestor.Alpha;
            return alpha;
        }

        private static object openingGeometry(Drawable drawable)
        {
            var bounds = drawable.ScreenSpaceDrawQuad.AABBFloat;
            return new { bounds.Left, bounds.Top, bounds.Width, bounds.Height };
        }

        private async Task saveOpeningCapture(string filename)
        {
            using Image<Rgba32>? image = await host.TakeScreenshotAsync().ConfigureAwait(false);
            if (image == null || image.Width < 640 || image.Height < 360)
                throw new InvalidOperationException("Desktop opening capture did not return a usable framebuffer.");
            await image.SaveAsPngAsync(Path.Combine(output, filename)).ConfigureAwait(false);
        }
    }
}
