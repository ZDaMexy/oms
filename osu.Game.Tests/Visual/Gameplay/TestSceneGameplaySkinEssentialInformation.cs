// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.NonVisual.Skinning;

namespace osu.Game.Tests.Visual.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneGameplaySkinEssentialInformation : OsuTestScene
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestPublicAndSemanticHudFollowRealScoreClockPauseSeekRetryAndLateAttach(bool authorScene)
        {
            EssentialHost host = null!;
            GameplaySkinEventSubscription observer = null!;
            long epoch = 0;
            AddStep("mount real score and gameplay clock with ordinary HUD", () => Child = host = new EssentialHost(authorScene));
            AddUntilStep("ordinary HUD drawables are loaded in the real clock tree", () => host.DisplaysReady);
            AddStep("before the first note progress is zero", () => assertInformation(host, 1, 0));
            AddStep("judge first real note with the production score processor", () =>
            {
                host.ClockHost.Sample(1500);
                host.Score.ApplyResult(new JudgementResult(host.Beatmap.HitObjects[0], host.Beatmap.HitObjects[0].Judgement)
                {
                    Type = HitResult.Great,
                });
            });
            AddStep("accuracy and progress reach the ordinary consumer", () =>
            {
                Assert.That(host.Score.Accuracy.Value, Is.LessThan(1));
                Assert.That(host.Score.TotalScore.Value, Is.GreaterThan(0));
                assertInformation(host, host.Score.Accuracy.Value, 0.25);
                observer = host.Events.EventStream.Subscribe();
                observer.DrainFrame(envelope =>
                {
                    var payload = (GameplaySkinStateEventPayload)envelope.Payload;
                    Assert.That(payload.State.Score.Accuracy, Is.EqualTo(host.Score.Accuracy.Value));
                    Assert.That(payload.State.Timing.Progress, Is.EqualTo(0.25));
                    epoch = envelope.Epoch;
                });
            });
            AddStep("pause the production clock", () => host.ClockHost.Stop());
            AddWaitStep("allow several UI frames while paused", 3);
            AddStep("paused display holds the exact score and progress", () => assertInformation(host, host.Score.Accuracy.Value, 0.25));
            AddStep("seek while paused", () => host.ClockHost.Seek(2500));
            AddUntilStep("seek commits one complete reset", () => host.Scene.CurrentEpoch > epoch);
            AddStep("seek shows destination progress with retained score", () =>
            {
                assertInformation(host, host.Score.Accuracy.Value, 0.75);
                epoch = host.Scene.CurrentEpoch;
                observer.DrainFrame(_ => { });
            });
            AddStep("retry resets the real score and clock", () =>
            {
                host.Score.ApplyBeatmap(host.Beatmap);
                host.ClockHost.Reset(500);
            });
            AddUntilStep("retry commits the next complete reset", () => host.Scene.CurrentEpoch > epoch);
            AddStep("retry rebuilds both necessary values", () =>
            {
                assertInformation(host, 1, 0);
                bool sawRetry = false;
                observer.DrainFrame(envelope =>
                {
                    if (envelope.Payload is GameplaySkinStateEventPayload { ResetReason: GameplaySkinEventResetReason.Retry } payload)
                    {
                        sawRetry = true;
                        Assert.That(payload.State.Score.Accuracy, Is.EqualTo(1));
                        Assert.That(payload.State.Timing.Progress, Is.Zero);
                    }
                });
                Assert.That(sawRetry, Is.True);
                observer.Dispose();
            });
            AddStep("seek beyond the last note", () => host.ClockHost.Seek(4000));
            AddUntilStep("progress reaches its bounded end", () => host.Scene.LastGameplayTime >= 4000);
            AddStep("completed duration is exactly one hundred percent", () => assertInformation(host, 1, 1));
            if (!authorScene)
            {
                AddStep("resize the ordinary information host", () => host.Size *= 0.5f);
                AddWaitStep("allow the text to fit its real bounds", 2);
                AddStep("all four values remain inside the smaller host", () =>
                {
                    assertInformation(host, 1, 1);
                    GameplaySkinResolvedMaterialKey key = host.Scene.MaterialSet.Entries.Single().Key;
                    host.Scene.TryGetHostedDrawable(key, out Drawable? drawable);
                    Drawable hud = drawable ?? throw new AssertionException("The ordinary information HUD must be mounted before measuring its bounds.");
                    SpriteText text = hud.ChildrenOfType<SpriteText>().Single();
                    Assert.That(text.DrawWidth * text.Scale.X, Is.LessThanOrEqualTo(hud.DrawWidth + 0.01));
                    Assert.That(text.DrawHeight * text.Scale.Y, Is.LessThanOrEqualTo(hud.DrawHeight + 0.01));
                });
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TestEmptyAndInstantBeatmapsHaveZeroProgress(int objectCount)
        {
            EssentialHost host = null!;
            AddStep("mount beatmap without playable duration", () => Child = host = new EssentialHost(true, objectCount));
            AddUntilStep("ordinary author display is loaded in the real clock tree", () => host.DisplaysReady);
            AddStep("before its timestamp progress is zero", () => assertInformation(host, 1, 0));
            AddStep("seek beyond its timestamp", () => host.ClockHost.Seek(4000));
            AddUntilStep("destination is published", () => host.Scene.LastGameplayTime >= 4000);
            AddStep("zero duration stays zero without division by zero", () => assertInformation(host, 1, 0));
        }

        private static void assertInformation(EssentialHost host, double accuracy, double progress)
        {
            Assert.That(host.DisplaysReady, Is.True);
            Assert.That(host.Scene.RuntimeFaults, Is.Empty);
            string accuracyText = (accuracy * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";
            string progressText = (progress * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            if (host.AuthorScene)
            {
                Assert.That(host.Scene.TryGetRuntimeNode("accuracy", out GameplaySkinSceneRuntimeNode? accuracyNode), Is.True);
                Assert.That(((SpriteText)accuracyNode!.ContentDrawable).Text.ToString(), Is.EqualTo(accuracyText));
                Assert.That(host.Scene.TryGetRuntimeNode("progress", out GameplaySkinSceneRuntimeNode? progressNode), Is.True);
                Assert.That(((SpriteText)progressNode!.ContentDrawable).Text.ToString(), Is.EqualTo(progressText));
                Assert.That(host.Scene.TryGetRuntimeNode("accuracy-bar", out GameplaySkinSceneRuntimeNode? accuracyBar), Is.True);
                Assert.That(accuracyBar!.TransformDrawable.Scale.X, Is.EqualTo(accuracy).Within(0.00001));
                Assert.That(host.Scene.TryGetRuntimeNode("progress-bar", out GameplaySkinSceneRuntimeNode? progressBar), Is.True);
                Assert.That(progressBar!.TransformDrawable.Scale.X, Is.EqualTo(progress).Within(0.00001));
            }
            else
            {
                GameplaySkinResolvedMaterialKey key = host.Scene.MaterialSet.Entries.Single().Key;
                Assert.That(host.Scene.TryGetHostedDrawable(key, out Drawable? drawable), Is.True);
                string[] lines = drawable!.ChildrenOfType<SpriteText>().Select(text => text.Text.ToString()).ToArray();
                Assert.That(lines, Is.EquivalentTo(new[]
                {
                    $"{host.Score.TotalScore.Value.ToString(CultureInfo.InvariantCulture)} | {accuracyText} | {host.Score.Combo.Value.ToString(CultureInfo.InvariantCulture)}x | {progressText}",
                }));
            }
        }

        private partial class EssentialHost : Container
        {
            [Cached(typeof(ScoreProcessor))]
            public readonly ManiaScoreProcessor Score = new ManiaScoreProcessor();

            public readonly Beatmap<Note> Beatmap = new Beatmap<Note>
            {
                HitObjects = { new Note { StartTime = 1000 }, new Note { StartTime = 2000 }, new Note { StartTime = 3000 } },
            };

            public readonly bool AuthorScene;
            public GameplaySkinEventRuntimeHost Events { get; private set; } = null!;
            public GameplaySkinSceneRuntimeHost Scene { get; private set; } = null!;
            public readonly EssentialClock ClockHost = new EssentialClock();

            public bool DisplaysReady => Scene?.IsLoaded == true
                                         && Scene.IsSceneReady
                                         && Scene.Layers.HudForeground.IsLoaded
                                         && Scene.Layers.HudForeground.DrawWidth > 0
                                         && Scene.Layers.HudForeground.ChildrenOfType<SpriteText>()
                                                 .Count(text => text.IsLoaded && text.DrawWidth > 0 && text.DrawHeight > 0) == (AuthorScene ? 2 : 1);

            public EssentialHost(bool authorScene, int objectCount = 3)
            {
                AuthorScene = authorScene;
                Beatmap.HitObjects.RemoveRange(objectCount, Beatmap.HitObjects.Count - objectCount);
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                foreach (Note note in Beatmap.HitObjects)
                    note.ApplyDefaults(Beatmap.ControlPointInfo, Beatmap.Difficulty);
                Score.ApplyBeatmap(Beatmap);
                GameplaySkinLayoutPublication publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(renderer.WhitePixel, AuthorScene);
                Events = new GameplaySkinEventRuntimeHost(publication, Beatmap);
                Scene = new GameplaySkinSceneRuntimeHost(publication, Events.EventStream);
                GameplaySkinSceneRuntimeLayers layers = Scene.Layers;
                ClockHost.Children = new Drawable[]
                {
                    Events,
                    Scene,
                    layers.Background,
                    layers.Underlay,
                    layers.Object,
                    layers.GameplayEffects,
                    layers.Overlay,
                    layers.HudForeground,
                };
                Scene.MarkLayersMounted();
                Child = ClockHost;
            }
        }

        private partial class EssentialClock : GameplayClockContainer
        {
            private readonly AdjustableManualClock source;

            public EssentialClock()
                : this(new AdjustableManualClock { CurrentTime = 500 })
            {
            }

            private EssentialClock(AdjustableManualClock source)
                : base(source, false, false)
            {
                this.source = source;
                SoftUnpause();
            }

            public void Sample(double time)
            {
                source.CurrentTime = time;
                UpdateSubTree();
            }
        }

        private class AdjustableManualClock : ManualClock, IAdjustableClock
        {
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;
            public bool Seek(double position)
            {
                CurrentTime = position;
                return true;
            }
            public void Reset()
            {
                IsRunning = false;
                CurrentTime = 0;
            }
            public void ResetSpeedAdjustments() => Rate = 1;
        }
    }
}
