// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private void addFeedbackCaptureSteps()
        {
            AddUntilStep("the real preparation finishes before feedback samples", () =>
                renderer.GameplaySkinSceneRuntime!.IsSceneReady && !lightingState().Timing.Preparation.Active);
            foreach (double time in new[] { 8000d, 8200d })
            {
                double target = time;
                TaskCompletionSource<bool>? acknowledgement = null;
                AddStep($"seek the actual replay to gauge phase {target}", () =>
                {
                    gameplayClock.Stop();
                    gameplayClock.Seek(target);
                    FramedBeatmapClock framed = gameplayClock.ChildrenOfType<FramedBeatmapClock>().Single();
                    var source = (IAdjustableClock)framed.Source;
                    double sourceTime = target - framed.TotalAppliedOffset;
                    acknowledgement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    host.AudioThread.Scheduler.Add(() => acknowledgement.SetResult(source.Seek(sourceTime)));
                });
                AddUntilStep("the real score and scene have caught up to that stopped clock", () =>
                    acknowledgement?.Task.IsCompleted == true
                    && Math.Abs(renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime - target) < 0.1
                    && Math.Abs(renderer.GameplaySkinSceneRuntime!.LastGameplayTime - target) < 0.1);
                AddStep("record the actual palette-matched frontier and framebuffer", () =>
                {
                    Assert.That(acknowledgement!.Task.GetAwaiter().GetResult(), Is.True);
                    GameplaySkinEventStateSnapshot state = lightingState();
                    GameplaySkinSceneRuntimeNode[] nodes = lightingNodes().ToArray();
                    var samples = nodes.Where(node => node.PreparedNode.Source.Id.EndsWith(".sample.fill", StringComparison.Ordinal)).ToArray();
                    Assert.That(samples.Length, Is.EqualTo(1), "Single and double play share one player's gauge.");
                    Assert.That(state.Score.Gauge, Is.GreaterThanOrEqualTo(0.8), "The frontier must be sampled past the Normal clear threshold.");
                    Assert.That(state.Score.Statistics.Fast, Is.Zero);
                    Assert.That(state.Score.Statistics.Slow, Is.Zero);
                    Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                    foreach (GameplaySkinSceneRuntimeNode sample in samples)
                    {
                        string frontId = sample.PreparedNode.Source.Id[..^".sample.fill".Length];
                        var front = nodes.Single(node => node.PreparedNode.Source.Id == frontId && sample.PreparedNode.MaterialTarget!.Equals(node.PreparedNode.MaterialTarget));
                        Assert.That(front.TransformDrawable.X, Is.EqualTo(state.Score.Gauge).Within(0.0001));
                        Assert.That(front.TransformDrawable.Alpha, Is.EqualTo(target == 8000 ? 0.2 : 1).Within(0.001));
                        Assert.That(nodes.Any(node => node != sample && node.ContentDrawable is Sprite original
                            && !node.PreparedNode.Source.Id.EndsWith(".sample.fill", StringComparison.Ordinal)
                            && ReferenceEquals(original.Texture, ((Sprite)sample.ContentDrawable).Texture)), Is.True,
                            "The frontier must use the actual full-fill texture, including the current gauge mode.");
                    }
                    string filename = $"feedback-gauge-{target:00000}";
                    File.WriteAllText(Path.Combine(outputDirectory, filename + ".json"), JsonSerializer.Serialize(new
                    {
                        target,
                        state.Score,
                        samples = samples.Select(sample => new
                        {
                            sample.InstanceId,
                            sample.PreparedNode.Source.ResourceId,
                            fill = lightingGeometry(sample.ContentDrawable),
                            clip = lightingGeometry(sample.RootDrawable.Parent!),
                        }).ToArray(),
                    }));
                    capture = saveCapture(filename + ".png");
                });
                AddUntilStep("wait for the actual gauge framebuffer", () => capture?.IsCompleted == true);
                AddStep("propagate the gauge capture result", () => capture!.GetAwaiter().GetResult());
            }
            AddStep("resume the same chart into its default Full Combo effect", () => gameplayClock.Start());
        }
    }
}
