// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneManiaBatchedHoldInput : PlayerTestScene
    {
        private const double head_time = 2000;
        private const double tail_time = 4000;

        private readonly FramedClock referenceClock = new FramedClock(new ManualClock { Rate = 0 });

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        protected override bool AllowFail => true;

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => new ManiaBeatmap(new StageDefinition(1))
            {
                HitObjects =
                {
                    new HoldNote { StartTime = head_time, EndTime = tail_time, Column = 0 },
                    new Note { StartTime = 16000, Column = 0 },
                },
                Difficulty = { CircleSize = 1, OverallDifficulty = 5, DrainRate = 5 },
                BeatmapInfo = { Ruleset = ruleset },
            };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void TestTapAtHeadReleasesTheHoldAndPreservesTheBrokenTailCap(bool sameInputBatch, bool regrab)
        {
            DrawableManiaRuleset renderer = null!;
            ManiaInputManager nativeInput = null!;
            DrawableHoldNote hold = null!;
            var headResults = new List<HitResult>();
            var tailResults = new List<HitResult>();
            var bodyResults = new List<HitResult>();
            bool holdingAfterTap = false;
            bool brokenAfterTap = false;
            int comboAfterTap = -1;
            int comboBreaksAfterTap = -1;

            AddStep("load native mania player", LoadPlayer);
            AddUntilStep("player accepts real keyboard input", () => Player?.IsLoaded == true && Player.Alpha == 1
                && Player.DrawableRuleset is DrawableManiaRuleset);
            AddStep("observe actual hold results and its primary input manager", () =>
            {
                renderer = (DrawableManiaRuleset)Player.DrawableRuleset;
                nativeInput = renderer.Playfield.Stages.Single().Columns.Single().Dependencies.Get<ManiaInputManager>();
                renderer.NewResult += result =>
                {
                    switch (result.HitObject)
                    {
                        case HeadNote:
                            headResults.Add(result.Type);
                            break;

                        case TailNote:
                            tailResults.Add(result.Type);
                            break;

                        case HoldNoteBody:
                            bodyResults.Add(result.Type);
                            break;
                    }
                };
            });
            seekTo(head_time, "the hold head");
            AddUntilStep("the real hold drawable is alive", () =>
                renderer.Playfield.Stages.Single().Columns.Single().HitObjectContainer.AliveObjects.OfType<DrawableHoldNote>().Any());
            AddStep("capture the actual hold", () =>
                hold = renderer.Playfield.Stages.Single().Columns.Single().HitObjectContainer.AliveObjects.OfType<DrawableHoldNote>().Single());

            if (sameInputBatch)
            {
                // ManualInputManager.Key enqueues both physical keyboard edges in one input batch.
                // No drawable Update, direct action trigger, or judgement mutation occurs between them.
                AddStep("enqueue a complete physical tap in one input batch", () => InputManager.Key(Key.Space));
            }
            else
            {
                AddStep("press the physical key", () => InputManager.PressKey(Key.Space));
                AddUntilStep("a separate drawable update observes the pressed hold", () => hold.Head.IsHit && hold.IsHolding.Value);
                AddStep("release the physical key after that update", () => InputManager.ReleaseKey(Key.Space));
            }

            AddUntilStep("the head was judged and the native key is released", () => hold.Head.AllJudged
                && !nativeInput.KeyBindingContainer.PressedActions.Any());
            AddStep("capture the actual released hold and score", () =>
            {
                holdingAfterTap = hold.Result.IsHolding(renderer.FrameStableClock.CurrentTime);
                brokenAfterTap = hold.Body.HasHoldBreak;
                comboAfterTap = Player.ScoreProcessor.Combo.Value;
                comboBreaksAfterTap = bodyResults.Count(result => result == HitResult.ComboBreak);
            });

            if (regrab)
            {
                seekTo(3000, "the middle of the hold");
                AddStep("regrab using the physical key", () => InputManager.PressKey(Key.Space));
                AddUntilStep("the native key and hold are pressed again", () =>
                    nativeInput.KeyBindingContainer.PressedActions.Contains(ManiaAction.Key1) && hold.IsHolding.Value);
                seekTo(tail_time, "the hold tail");
                AddStep("release exactly at the tail", () => InputManager.ReleaseKey(Key.Space));
            }
            else
                seekTo(tail_time + 500, "after the unheld tail's miss window");

            AddUntilStep("the tail result is delivered and no key remains pressed", () => tailResults.Count > 0
                && !nativeInput.KeyBindingContainer.PressedActions.Any());
            AddStep("a complete tap really breaks the hold and cannot earn an intact tail", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(headResults, Is.EqualTo(new[] { HitResult.Perfect }), "the real head is hit once");
                    Assert.That(holdingAfterTap, Is.False, "the authoritative hold state follows the release in the same input batch");
                    Assert.That(brokenAfterTap, Is.True, "the early release breaks the actual hold body");
                    Assert.That(comboAfterTap, Is.Zero, "the early release breaks combo immediately");
                    Assert.That(comboBreaksAfterTap, Is.EqualTo(1), "the body reports exactly one break at the early release");
                    Assert.That(bodyResults, Is.EqualTo(new[] { HitResult.ComboBreak }), "regrab and completion do not duplicate the body break");
                    Assert.That(tailResults, Is.EqualTo(new[] { regrab ? HitResult.Meh : HitResult.Miss }),
                        "a broken hold keeps the release cap even after a real regrab");
                    Assert.That(Player.ScoreProcessor.Combo.Value, Is.EqualTo(regrab ? 1 : 0), "final combo follows the actual tail result");
                    Assert.That(Player.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(1),
                        "only the head earns a perfect judgement");
                });
            });

            void seekTo(double destination, string description)
            {
                AddStep($"seek production clock to {description}", () => Player.GameplayClockContainer.Seek(destination));
                AddUntilStep($"clock reaches {description}", () => renderer.FrameStableClock.CurrentTime,
                    () => Is.EqualTo(destination).Within(0.01));
            }
        }
    }
}
