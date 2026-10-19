// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsHoldReplayRewind : PlayerTestScene
    {
        private readonly FramedClock referenceClock = new FramedClock(new ManualClock { Rate = 0 });
        private bool scratchHold;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText($"""
                #TITLE Manual hold replay rewind
                #BPM 120
                #RANK 2
                #LNTYPE 1
                #WAV01 hold-head.wav
                #WAV02 hold-tail.wav
                #WAV03 sentinel.wav
                #0015{(scratchHold ? 6 : 1)}:01
                #0035{(scratchHold ? 6 : 1)}:02
                #00811:03
                """, "hold-replay-rewind.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset },
            };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        [TestCase(BmsLongNoteMode.LN, ReplayHoldState.Held)]
        [TestCase(BmsLongNoteMode.CN, ReplayHoldState.Held)]
        [TestCase(BmsLongNoteMode.HCN, ReplayHoldState.Held)]
        [TestCase(BmsLongNoteMode.LN, ReplayHoldState.Released)]
        [TestCase(BmsLongNoteMode.CN, ReplayHoldState.Released)]
        [TestCase(BmsLongNoteMode.HCN, ReplayHoldState.Released)]
        [TestCase(BmsLongNoteMode.LN, ReplayHoldState.Unpressed)]
        [TestCase(BmsLongNoteMode.CN, ReplayHoldState.Unpressed)]
        [TestCase(BmsLongNoteMode.HCN, ReplayHoldState.Unpressed)]
        public void TestManualHoldReplayIsUnchangedAfterRewindingFromCompletedTail(BmsLongNoteMode mode, ReplayHoldState input)
        {
            DrawableBmsRuleset renderer = null!;
            BmsHoldNote hold = null!;
            HitObjectLifetimeEntry entry = null!;
            Dictionary<HitResult, int> originalStatistics = null!;
            HitResult originalHead = HitResult.None;
            HitResult originalTail = HitResult.None;
            double originalGauge = 0;
            BmsLongNoteBodyState? rewoundBody = null;
            bool rewoundKeyPressed = false;
            BmsLongNoteBodyState? expectedBody = input == ReplayHoldState.Held
                ? BmsLongNoteBodyState.Holding
                : input == ReplayHoldState.Released && mode != BmsLongNoteMode.HCN ? null : BmsLongNoteBodyState.Broken;

            AddStep("load player with selected long-note mode", () =>
            {
                scratchHold = false;
                LoadPlayer(mode switch
                {
                    BmsLongNoteMode.LN => Array.Empty<Mod>(),
                    BmsLongNoteMode.CN => new Mod[] { new BmsModChargeNote() },
                    BmsLongNoteMode.HCN => new Mod[] { new BmsModHellChargeNote() },
                    _ => throw new ArgumentOutOfRangeException(nameof(mode)),
                });
            });
            AddUntilStep("real BMS player loaded", () => Player?.IsLoaded == true && Player.DrawableRuleset is DrawableBmsRuleset);
            AddStep("attach manual press/release replay without autoplay", () =>
            {
                renderer = (DrawableBmsRuleset)Player.DrawableRuleset;
                hold = ((BmsBeatmap)renderer.Beatmap).HitObjects.OfType<BmsHoldNote>().Single();
                entry = renderer.Playfield.Lanes.Single(lane => lane.LaneIndex == hold.LaneIndex)
                                .HitObjectContainer.OrderedEntries.Single(candidate => ReferenceEquals(candidate.HitObject, hold));

                var replay = new Replay { Frames = { new BmsReplayFrame(0) } };
                if (input != ReplayHoldState.Unpressed)
                {
                    replay.Frames.Add(new BmsReplayFrame(hold.StartTime, BmsAction.Key1));
                    replay.Frames.Add(new BmsReplayFrame(input == ReplayHoldState.Released ? hold.StartTime + 500 : hold.EndTime + 20));
                }
                replay.Frames.Add(new BmsReplayFrame(hold.EndTime + 500));

                var replayScore = new Score { Replay = replay, ScoreInfo = { Mods = renderer.Mods.ToArray() } };
                BmsScoreInfoData.InitialiseNewPlay(replayScore.ScoreInfo);
                renderer.SetRecordTarget(null!);
                renderer.SetReplayScore(replayScore);
                Assert.That(renderer.Mods.OfType<ModAutoplay>(), Is.Empty, "This must exercise scored manual replay input, not object-side autoplay.");
            });

            seekTo(() => hold.StartTime + 2000, "initial body");
            AddAssert("initial body follows actual replay input", bodyState, () => Is.EqualTo(expectedBody));

            seekTo(() => hold.EndTime + 100, "first tail completion");
            AddStep("retain original judgement, gauge and score results", () =>
            {
                Assert.That(entry.AllJudged, Is.True);
                originalHead = headResult();
                originalTail = tailResult();
                Assert.That(originalHead, Is.EqualTo(input == ReplayHoldState.Unpressed ? HitResult.Miss : HitResult.Perfect));
                Assert.That(originalTail, Is.EqualTo(input == ReplayHoldState.Held
                    ? mode.RequiresTailJudgement() ? HitResult.Perfect : HitResult.IgnoreHit
                    : mode.RequiresTailJudgement() ? HitResult.Miss : HitResult.IgnoreMiss));
                originalStatistics = Player.ScoreProcessor.Statistics.ToDictionary(pair => pair.Key, pair => pair.Value);
                originalGauge = Player.HealthProcessor.Health.Value;
            });

            seekTo(() => hold.StartTime + 2000, "rewound body");
            AddStep("observe restored body and production replay key state", () =>
            {
                rewoundBody = bodyState();
                rewoundKeyPressed = renderer.ChildrenOfType<BmsInputManager>().Single().KeyBindingContainer.PressedActions.Contains(BmsAction.Key1);
            });
            seekTo(() => hold.EndTime + 100, "second tail completion");
            AddStep("rewind preserves held and unheld gameplay outcomes", () => Assert.Multiple(() =>
            {
                Assert.That(rewoundKeyPressed, Is.EqualTo(input == ReplayHoldState.Held), "Replay input itself must have reached the requested body state.");
                Assert.That(rewoundBody, Is.EqualTo(expectedBody),
                    "Rewinding must restore the earlier hold state without inventing a re-grab on an unpressed or released LN/CN.");
                Assert.That(entry.AllJudged, Is.True);
                Assert.That(headResult(), Is.EqualTo(originalHead), "The replay's original head result must survive the round trip.");
                Assert.That(tailResult(), Is.EqualTo(originalTail), "The replay's original tail result must survive the round trip.");
                Assert.That(Player.ScoreProcessor.Statistics, Is.EquivalentTo(originalStatistics));
                Assert.That(Player.HealthProcessor.Health.Value, Is.EqualTo(originalGauge).Within(1e-9));
            }));

            BmsLongNoteBodyState? bodyState() => renderer.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>()
                                                        .SingleOrDefault(drawable => ReferenceEquals(drawable.HitObject, hold))?.BodyState.Value;

            HitResult headResult() => entry.NestedEntries.Single(nested => ReferenceEquals(nested.HitObject, hold.Head)).Result!.Type;

            HitResult tailResult() => entry.NestedEntries.Single(nested => ReferenceEquals(nested.HitObject, hold.Tail)).Result!.Type;

            void seekTo(Func<double> destination, string description)
            {
                AddStep($"seek production clock to {description}", () => Player.GameplayClockContainer.Seek(destination()));
                AddUntilStep($"replay catches up to {description}", () => renderer.FrameStableClock.CurrentTime, () => Is.EqualTo(destination()).Within(0.01));
            }
        }

        [Test]
        public void TestMissedHeadHcnBodyRecoversOnlyWhileReplayKeyIsHeld()
        {
            DrawableBmsRuleset renderer = null!;
            BmsHoldNote hold = null!;
            HitObjectLifetimeEntry entry = null!;
            double missedGauge = 0;
            double heldGauge = 0;
            double releasedGauge = 0;
            double finalGauge = 0;

            AddStep("load actual HCN player", () =>
            {
                scratchHold = false;
                LoadPlayer(new Mod[] { new BmsModHellChargeNote() });
            });
            AddUntilStep("real HCN player loaded", () => Player?.IsLoaded == true && Player.DrawableRuleset is DrawableBmsRuleset);
            AddStep("attach missed-head and re-grab replay", () =>
            {
                renderer = (DrawableBmsRuleset)Player.DrawableRuleset;
                hold = ((BmsBeatmap)renderer.Beatmap).HitObjects.OfType<BmsHoldNote>().Single();
                entry = renderer.Playfield.Lanes.Single(lane => lane.LaneIndex == hold.LaneIndex)
                                .HitObjectContainer.OrderedEntries.Single(candidate => ReferenceEquals(candidate.HitObject, hold));
                var replay = new Replay
                {
                    Frames =
                    {
                        new BmsReplayFrame(0),
                        new BmsReplayFrame(hold.StartTime + 800, BmsAction.Key1),
                        new BmsReplayFrame(hold.StartTime + 1400),
                        new BmsReplayFrame(hold.StartTime + 2000, BmsAction.Key1),
                        new BmsReplayFrame(hold.EndTime + 20),
                        new BmsReplayFrame(hold.EndTime + 500),
                    }
                };
                var replayScore = new Score { Replay = replay, ScoreInfo = { Mods = renderer.Mods.ToArray() } };
                BmsScoreInfoData.InitialiseNewPlay(replayScore.ScoreInfo);
                renderer.SetRecordTarget(null!);
                renderer.SetReplayScore(replayScore);
            });

            seekTo(() => hold.StartTime + 700, "missed head before first late grab");
            AddStep("head is missed and unheld body drains", () =>
            {
                Assert.That(headResult(), Is.EqualTo(HitResult.Miss));
                assertTicks(0, 700, HitResult.IgnoreMiss);
                missedGauge = Player.HealthProcessor.Health.Value;
            });

            seekTo(() => hold.StartTime + 1200, "first recovered body interval");
            AddStep("late press recovers actual ticks and gauge without repairing head", () =>
            {
                Assert.That(headResult(), Is.EqualTo(HitResult.Miss));
                assertTicks(800, 1200, HitResult.IgnoreHit);
                heldGauge = Player.HealthProcessor.Health.Value;
                Assert.That(heldGauge, Is.GreaterThan(missedGauge));
            });

            seekTo(() => hold.StartTime + 1800, "released body interval");
            AddStep("release resumes actual body damage", () =>
            {
                assertTicks(1400, 1800, HitResult.IgnoreMiss);
                releasedGauge = Player.HealthProcessor.Health.Value;
                Assert.That(releasedGauge, Is.LessThan(heldGauge));
            });

            seekTo(() => hold.StartTime + 2500, "second recovered body interval");
            AddStep("re-grab recovers actual ticks and gauge again", () =>
            {
                Assert.That(headResult(), Is.EqualTo(HitResult.Miss));
                assertTicks(2000, 2500, HitResult.IgnoreHit);
                Assert.That(Player.HealthProcessor.Health.Value, Is.GreaterThan(releasedGauge));
            });

            seekTo(() => hold.EndTime + 100, "first missed-head HCN completion");
            AddStep("retain scored tail and completed gauge", () =>
            {
                Assert.That(entry.AllJudged, Is.True);
                Assert.That(tailResult(), Is.EqualTo(HitResult.Perfect));
                finalGauge = Player.HealthProcessor.Health.Value;
            });

            seekTo(() => hold.StartTime + 1800, "rewound released HCN interval");
            AddStep("rewind preserves genuinely released HCN body", () =>
            {
                Assert.That(renderer.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().Single(drawable => ReferenceEquals(drawable.HitObject, hold)).BodyState.Value,
                    Is.EqualTo(BmsLongNoteBodyState.Broken));
                Assert.That(renderer.ChildrenOfType<BmsInputManager>().Single().KeyBindingContainer.PressedActions.Contains(BmsAction.Key1), Is.False);
                Assert.That(Player.HealthProcessor.Health.Value, Is.EqualTo(releasedGauge).Within(1e-9));
            });
            seekTo(() => hold.EndTime + 100, "replayed missed-head HCN completion");
            AddStep("recovered HCN results survive replay rewind", () => Assert.Multiple(() =>
            {
                Assert.That(headResult(), Is.EqualTo(HitResult.Miss));
                Assert.That(tailResult(), Is.EqualTo(HitResult.Perfect));
                assertTicks(2000, 2500, HitResult.IgnoreHit);
                Assert.That(Player.HealthProcessor.Health.Value, Is.EqualTo(finalGauge).Within(1e-9));
            }));

            HitResult headResult() => entry.NestedEntries.Single(nested => ReferenceEquals(nested.HitObject, hold.Head)).Result!.Type;

            HitResult tailResult() => entry.NestedEntries.Single(nested => ReferenceEquals(nested.HitObject, hold.Tail)).Result!.Type;

            void assertTicks(double after, double through, HitResult expected)
            {
                HitObjectLifetimeEntry[] ticks = entry.NestedEntries.Where(nested => nested.HitObject is BmsHoldNoteBodyTick
                    && nested.HitObject.StartTime > hold.StartTime + after && nested.HitObject.StartTime <= hold.StartTime + through).ToArray();
                Assert.That(ticks, Has.Length.GreaterThanOrEqualTo(2));
                Assert.That(ticks.Select(tick => tick.Result?.Type), Is.All.EqualTo(expected));
            }

            void seekTo(Func<double> destination, string description)
            {
                AddStep($"seek production clock to {description}", () => Player.GameplayClockContainer.Seek(destination()));
                AddUntilStep($"replay catches up to {description}", () => renderer.FrameStableClock.CurrentTime, () => Is.EqualTo(destination()).Within(0.01));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestAssistedHoldRestoresItsAutomaticStateWithoutReplayKeyPresses(bool scratch)
        {
            DrawableBmsRuleset renderer = null!;
            BmsHoldNote hold = null!;
            HitObjectLifetimeEntry entry = null!;
            Dictionary<HitResult, int> originalStatistics = null!;
            double originalGauge = 0;

            AddStep("load automatic key or scratch hold", () =>
            {
                scratchHold = scratch;
                LoadPlayer(new Mod[] { new BmsModHellChargeNote(), scratch ? new BmsModAutoScratch() : new BmsModAutoNote() });
            });
            AddUntilStep("real assisted player loaded", () => Player?.IsLoaded == true && Player.DrawableRuleset is DrawableBmsRuleset);
            AddStep("attach replay with no gameplay key presses", () =>
            {
                renderer = (DrawableBmsRuleset)Player.DrawableRuleset;
                hold = ((BmsBeatmap)renderer.Beatmap).HitObjects.OfType<BmsHoldNote>().Single();
                entry = renderer.Playfield.Lanes.Single(lane => lane.LaneIndex == hold.LaneIndex)
                                .HitObjectContainer.OrderedEntries.Single(candidate => ReferenceEquals(candidate.HitObject, hold));
                Assert.That(hold.IsScratch, Is.EqualTo(scratch));
                Assert.That(hold.AutoPlay, Is.True);
                Assert.That(hold.CountsForScore, Is.False);
                var replayScore = new Score
                {
                    Replay = new Replay { Frames = { new BmsReplayFrame(0), new BmsReplayFrame(hold.EndTime + 500) } },
                    ScoreInfo = { Mods = renderer.Mods.ToArray() },
                };
                BmsScoreInfoData.InitialiseNewPlay(replayScore.ScoreInfo);
                renderer.SetRecordTarget(null!);
                renderer.SetReplayScore(replayScore);
            });

            seekTo(() => hold.StartTime + 2000, "first automatic hold body");
            assertAutomaticBody();
            seekTo(() => hold.EndTime + 100, "first assisted tail completion");
            AddStep("retain completed assisted results", () =>
            {
                Assert.That(entry.AllJudged, Is.True);
                Assert.That(entry.NestedEntries.Select(nested => nested.Result?.Type), Is.All.EqualTo(HitResult.IgnoreHit));
                originalStatistics = Player.ScoreProcessor.Statistics.ToDictionary(pair => pair.Key, pair => pair.Value);
                originalGauge = Player.HealthProcessor.Health.Value;
            });
            seekTo(() => hold.StartTime + 2000, "rewound automatic hold body");
            assertAutomaticBody();
            seekTo(() => hold.EndTime + 100, "second assisted tail completion");
            AddStep("assisted results survive rewind without requiring physical input", () => Assert.Multiple(() =>
            {
                Assert.That(entry.AllJudged, Is.True);
                Assert.That(entry.NestedEntries.Select(nested => nested.Result?.Type), Is.All.EqualTo(HitResult.IgnoreHit));
                Assert.That(Player.ScoreProcessor.Statistics, Is.EquivalentTo(originalStatistics));
                Assert.That(Player.HealthProcessor.Health.Value, Is.EqualTo(originalGauge).Within(1e-9));
            }));

            void assertAutomaticBody()
            {
                AddStep("assisted body remains held with no replay actions", () => Assert.Multiple(() =>
                {
                    Assert.That(renderer.ChildrenOfType<BmsInputManager>().Single().KeyBindingContainer.PressedActions, Is.Empty);
                    Assert.That(renderer.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().Single(drawable => ReferenceEquals(drawable.HitObject, hold)).BodyState.Value,
                        Is.EqualTo(BmsLongNoteBodyState.Holding));
                }));
            }

            void seekTo(Func<double> destination, string description)
            {
                AddStep($"seek production clock to {description}", () => Player.GameplayClockContainer.Seek(destination()));
                AddUntilStep($"replay catches up to {description}", () => renderer.FrameStableClock.CurrentTime, () => Is.EqualTo(destination()).Within(0.01));
            }
        }

        public enum ReplayHoldState
        {
            Held,
            Released,
            Unpressed,
        }
    }
}
