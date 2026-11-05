// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osuTK.Input;

namespace osu.Tools.StandardSkinCapture;

public partial class TestSceneStandardSkinCapture
{
    private static bool IsFailureSkipCase => CaptureSettings.Current.ChartVariant == "failure-skip";
    private static bool IsFailureCase => CaptureSettings.Current.ChartVariant is "failure" or "failure-skip";
    private double stoppedFailureClock;
    private double stoppedFailureGameplayTime;
    private long stoppedFailureScore;
    private int stoppedFailureHits;
    private int previousFailureFrame = -1;
    private double failureSkipRequestTime;
    private double failureSkipRequestElapsed;
    private double failureSkipMaximumWait;
    private double failureSkipReachedTime;

    private sealed record FailureAnimationObservation(string[] AnimationIds, int AppliedFrame, int VisibleTexturedPieces, double EffectiveAlpha);

    private void AddFailureCaptures()
    {
        AddUntilStep("native Hazard actually fails on omitted replay input", () => player.GameplayState.HasFailed && ReadState().Timing.FailureElapsed.HasValue);
        AddStep("retain the actual failed gameplay boundary", () =>
        {
            var renderer = ((CaptureFailurePlayer)player).NativeRuleset;
            Assert.That(renderer.Mods.OfType<BmsModGaugeHazard>().Count(), Is.EqualTo(1));
            Assert.That(renderer.Mods.OfType<ModAutoplay>(), Is.Empty);
            Assert.That(renderer.Mods.OfType<ModNoFail>(), Is.Empty);
            Assert.That(player.Score.Replay.Frames.Cast<BmsReplayFrame>().All(frame => frame.Actions.Count == 0), Is.True);
            Assert.That(player.GameplayState.HasPassed, Is.False);
            Assert.That(score.HitEvents, Is.Not.Empty, "A real missed judgement, not a probe event, must cause failure.");
            Assert.That(ReadState().FailedAt.HasValue, Is.True);
            stoppedFailureClock = clock.CurrentTime;
            stoppedFailureGameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime");
            stoppedFailureScore = score.TotalScore.Value;
            stoppedFailureHits = score.HitEvents.Count;
            settings.Write("failure-start.json", new
            {
                actualPlayer = player.GetType().FullName,
                nativeMods = renderer.Mods.Select(mod => mod.Acronym).ToArray(),
                gameplayTime = stoppedFailureGameplayTime,
                state = ReadState(),
                score = stoppedFailureScore,
                hitEvents = stoppedFailureHits,
                nativeEmptyReplay = true,
                injectedSkinEvents = false,
                manuallyCalledPerformFail = false,
            });
        });
        foreach (int sample in IsFailureSkipCase ? new[] { 300 } : new[] { 300, 1500, 2800 })
        {
            string name = $"failure-{sample:00000}";
            AddUntilStep("natural failure presentation reaches " + sample + " ms", () =>
            {
                Assert.That(player.ChildrenOfType<FailOverlay>().Single().State.Value, Is.EqualTo(Visibility.Hidden),
                    "The failure outro ended before its requested visible frame.");
                return ReadState().Timing.FailureElapsed >= sample && ObserveFailureAnimations().Any(item => item.VisibleTexturedPieces > 0);
            });
            AddStep("capture original Stage Failed at " + sample + " ms", () =>
            {
                AssertFailureTimeAndScoringStopped();
                FailureAnimationObservation[] animations = ObserveFailureAnimations();
                int currentFrame = animations.Where(item => item.VisibleTexturedPieces > 0).Max(item => item.AppliedFrame);
                Assert.That(currentFrame, Is.GreaterThan(previousFailureFrame), "The source movie must advance while chart time stays stopped.");
                previousFailureFrame = currentFrame;
                RecordGameplay(name, checked((int)Math.Round(stoppedFailureGameplayTime)));
                settings.Write(name + "-native.json", new
                {
                    requestedFailureElapsed = sample,
                    actualFailureElapsed = ReadState().Timing.FailureElapsed,
                    stoppedFailureClock,
                    stoppedFailureGameplayTime,
                    currentClock = clock.CurrentTime,
                    currentGameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime"),
                    score = score.TotalScore.Value,
                    hitEvents = score.HitEvents.Count,
                    animations,
                    inputSuspended = FailureInputSuspended(),
                    judgementDisplay = runtime.Publication.Snapshot.Context.OptionValues.TryGetValue("judgement-display", out var choice) ? choice.CanonicalValue : null,
                    originalFailureTrigger = "gameplay.fail",
                    forcedVisibility = false,
                });
                screenshot = Capture(name);
            });
            AddUntilStep("wait for failure framebuffer", () => screenshot?.IsCompleted == true);
            AddStep("propagate failure framebuffer errors", () => screenshot!.GetAwaiter().GetResult());
        }
        if (IsFailureSkipCase)
            AddFailureSkipInput();
        else
            AddUntilStep("native outro naturally reaches the ordinary FailOverlay", () =>
                player.ChildrenOfType<FailOverlay>().Single().State.Value == Visibility.Visible);
        AddWaitStep("let the ordinary failure overlay enter", 8);
        AddStep("capture the genuine failure overlay and stopped score", () =>
        {
            AssertFailureTimeAndScoringStopped();
            Assert.That(Game.ScreenStack.CurrentScreen, Is.SameAs(player));
            Assert.That(player.Score.ScoreInfo.Passed, Is.False);
            Assert.That(score.HasCompleted.Value, Is.False, "The failed fixture must not pretend that its remaining chart was scored.");
            settings.Write("native-failure-overlay.json", new
            {
                overlay = player.ChildrenOfType<FailOverlay>().Single().GetType().FullName,
                player.GameplayState.HasFailed,
                player.GameplayState.HasPassed,
                score = score.TotalScore.Value,
                hitEvents = score.HitEvents.Count,
                state = ReadState(),
                naturallyReached = !IsFailureSkipCase,
                skippedWithEscape = IsFailureSkipCase,
                forcedResults = false,
            });
            screenshot = Capture("native-failure-overlay");
        });
        AddUntilStep("wait for ordinary failure framebuffer", () => screenshot?.IsCompleted == true);
        AddStep("finish the genuine failure scenario receipt", () =>
        {
            screenshot!.GetAwaiter().GetResult();
            settings.VerifyPackage();
            settings.Write("completed.json", new
            {
                success = true,
                settings.PackageSha256,
                settings.Ruleset,
                settings.Keys,
                settings.Presentation,
                settings.ChartVariant,
                settings.Assist,
                screenshots = captured.ToArray(),
                actualPlayer = player.GetType().FullName,
                nativeFailed = true,
                passed = false,
                completeScoredChart = false,
                naturalFailureOverlay = !IsFailureSkipCase,
                escapeSkippedFailureOutro = IsFailureSkipCase,
                naturalResults = false,
                chartTimeAndScoreStopped = true,
                failureMovieAdvanced = previousFailureFrame > 0,
                humanOrDeviceAcceptance = false,
                originalGameEquivalence = false,
            });
            RestoreHostActivity();
        });
    }

    private void AddFailureSkipInput()
    {
        AddStep("press real Escape while the source failure movie is playing", () =>
        {
            AssertFailureTimeAndScoringStopped();
            Assert.That(player.ChildrenOfType<FailOverlay>().Single().State.Value, Is.EqualTo(Visibility.Hidden));
            Assert.That(previousFailureFrame, Is.GreaterThan(0));
            failureSkipRequestElapsed = ReadState().Timing.FailureElapsed!.Value;
            double duration = runtime.PreparedScene.Program.FailureOutro;
            Assert.That(duration, Is.EqualTo(3000), "This source-specific fixture must observe the original three-second outro.");
            Assert.That(failureSkipRequestElapsed, Is.InRange(300, 1500), "Escape must precede the natural end with sufficient headroom.");
            // Use the outer test clock, not FailureElapsed: a legitimate skip clamps the latter
            // to the terminal frame. The margin also prevents a natural completion from passing.
            failureSkipMaximumWait = duration - failureSkipRequestElapsed - 250;
            failureSkipRequestTime = Time.Current;
            settings.Write("failure-skip-request.json", new
            {
                failureSkipRequestElapsed,
                failureSkipRequestTime,
                failureSkipMaximumWait,
                duration,
                animations = ObserveFailureAnimations(),
                input = "InputManager.PressKey(Key.Escape), held until the ordinary FailOverlay appears, then released",
                directlyCalledExitOrFail = false,
                injectedSkinEvents = false,
            });
            InputManager.PressKey(Key.Escape);
        });
        AddUntilStep("Escape opens the ordinary FailOverlay before the natural outro end", () =>
        {
            double elapsed = Time.Current - failureSkipRequestTime;
            bool visible = player.ChildrenOfType<FailOverlay>().Single().State.Value == Visibility.Visible;
            if (visible || elapsed >= failureSkipMaximumWait)
                InputManager.ReleaseKey(Key.Escape);
            Assert.That(elapsed, Is.LessThan(failureSkipMaximumWait), "Escape did not shorten the actual failure presentation.");
            AssertFailureTimeAndScoringStopped();
            if (visible)
                failureSkipReachedTime = Time.Current;
            return visible;
        });
        AddStep("retain the genuine early failure menu boundary", () =>
        {
            Assert.That(Game.ScreenStack.CurrentScreen, Is.SameAs(player), "The first Escape must show the failed-play menu, not leave Player.");
            settings.Write("failure-skip-result.json", new
            {
                failureSkipRequestElapsed,
                elapsedUntilOverlay = failureSkipReachedTime - failureSkipRequestTime,
                failureSkipMaximumWait,
                currentClock = clock.CurrentTime,
                currentGameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime"),
                score = score.TotalScore.Value,
                hitEvents = score.HitEvents.Count,
                state = ReadState(),
                escapeReleased = true,
                nativeFailureOverlay = true,
                directlyCalledExitOrFail = false,
                injectedSkinEvents = false,
            });
        });
    }

    private void AssertFailureTimeAndScoringStopped()
    {
        Assert.That(player.GameplayState.HasFailed, Is.True);
        Assert.That(clock.IsRunning, Is.False);
        Assert.That(clock.CurrentTime, Is.EqualTo(stoppedFailureClock).Within(.001));
        Assert.That(ReadProperty<double>(events, "AuthoritativeGameplayTime"), Is.EqualTo(stoppedFailureGameplayTime).Within(.001));
        Assert.That(score.TotalScore.Value, Is.EqualTo(stoppedFailureScore));
        Assert.That(score.HitEvents.Count, Is.EqualTo(stoppedFailureHits));
        Assert.That(FailureInputSuspended(), Is.True);
        Assert.That(((CaptureFailurePlayer)player).NativeRuleset.GameplayInputManager!.UseParentInput, Is.False);
        Assert.That(runtime.RuntimeFaults, Is.Empty);
    }

    private bool FailureInputSuspended()
        => (bool)typeof(DrawableRuleset<HitObject>).GetField("gameplaySkinFailureInputSuspended", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(((CaptureFailurePlayer)player).NativeRuleset)!;

    private FailureAnimationObservation[] ObserveFailureAnimations()
        => player.ChildrenOfType<CompositeDrawable>()
            .Where(drawable => drawable.GetType().FullName == "osu.Game.Skinning.Gameplay.GameplaySkinRecordedAnimationDrawable")
            .Where(drawable => ReadProperty<GameplaySkinRecordedAnimationProgram>(drawable, "Program").Trigger == "gameplay.fail")
            .Select(drawable =>
            {
                var program = ReadProperty<GameplaySkinRecordedAnimationProgram>(drawable, "Program");
                int visible = drawable.ChildrenOfType<Sprite>().Count(sprite =>
                {
                    if (!sprite.IsPresent || sprite.Texture == null || EffectiveAlpha(sprite) <= .001)
                        return false;
                    object piece = ReadField<object>(sprite, "piece");
                    return ReadProperty<int>(piece, "VertexCount") >= 3
                           && (ReadProperty<osuTK.Vector4>(piece, "Multiply").W > 0 || ReadProperty<osuTK.Vector4>(piece, "Add").W > 0);
                });
                return new FailureAnimationObservation(runtime.PreparedScene.RecordedAnimations.Where(pair => ReferenceEquals(pair.Value, program))
                    .Select(pair => pair.Key).ToArray(), ReadField<int>(drawable, "appliedFrame"), visible, EffectiveAlpha(drawable));
            }).ToArray();

    private partial class CaptureFailurePlayer : BmsSoloPlayer
    {
        public DrawableBmsRuleset NativeRuleset => (DrawableBmsRuleset)DrawableRuleset;

        protected override void PrepareReplay()
        {
            // Let the real native recorder initialise this new play's metadata. No score, gauge,
            // reading or skin values are authored by the fixture; only subsequent physical input is empty.
            base.PrepareReplay();
            DrawableRuleset.SetRecordTarget(null!);
            Score.Replay = new Replay { Frames = { new BmsReplayFrame(0), new BmsReplayFrame(60000) } };
            DrawableRuleset.SetReplayScore(Score);
        }

        protected override Task ImportScore(Score score) => Task.CompletedTask;
    }
}
