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
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsNaturalMissTiming : PlayerTestScene
    {
        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };
        private readonly FramedClock referenceClock;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        public TestSceneBmsNaturalMissTiming()
        {
            referenceClock = new FramedClock(manualClock);
        }

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("""
                #TITLE Natural key and scratch miss feedback
                #BPM 120
                #RANK 2
                #WAV01 note.wav
                #00111:01
                #00216:01
                #01011:01
                """, "natural-miss-feedback.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset },
            };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(false, false, false);

        [TestCase(false)]
        [TestCase(true)]
        public void TestRealProducerShowsNaturalMissSlowWithoutCountingItAsPlayerTiming(bool autoplay)
        {
            DrawableBmsRuleset renderer = null!;
            GameplaySkinEventSubscription subscription = null!;
            var observed = new List<GameplaySkinEventEnvelope>();
            GameplaySkinJudgementStateSnapshot keyJudgement = default;
            GameplaySkinLaneGroupId groupId = null!;
            long epoch = -1;

            AddStep("reset the authoritative track", () =>
            {
                manualClock.CurrentTime = 0;
                referenceClock.ProcessFrame();
            });
            AddStep("load the actual BMS player", () =>
                LoadPlayer(autoplay ? new Mod[] { new BmsModAutoplay() } : Array.Empty<Mod>()));
            AddUntilStep("the real producer and exact publication are ready", () =>
                Player?.IsLoaded == true && Player.DrawableRuleset is DrawableBmsRuleset ruleset
                                        && ruleset.GameplaySkinEventRuntime?.IsLoaded == true
                                        && ruleset.LayoutProvider.RevisionOwner?.CurrentPublication != null);
            AddStep("observe production feedback before either note", () =>
            {
                renderer = (DrawableBmsRuleset)Player.DrawableRuleset;
                subscription = renderer.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observed.Add);
                epoch = renderer.GameplaySkinEventStream.CurrentEpoch;
                Assert.That(current(renderer).JudgementTimingGroups.All(entry => entry.State.Combined == null), Is.True);
            });

            advanceTrackTo(2600);
            AddUntilStep("the unpressed key head is naturally judged", () =>
                Player.ScoreProcessor.HitEvents.Any(hit => hit.HitObject is BmsHitObject && hit.HitObject.StartTime == 2000));
            AddStep("key feedback comes from the unchanged real judgement", () =>
            {
                subscription.DrainFrame(observed.Add);
                keyJudgement = observed.Select(edge => edge.Payload).OfType<GameplaySkinJudgementEventPayload>().Last().State;
                groupId = keyJudgement.GroupId!;
                var actual = Player.ScoreProcessor.HitEvents.Single(hit => hit.HitObject is BmsHitObject && hit.HitObject.StartTime == 2000);
                GameplaySkinEventStateSnapshot snapshot = current(renderer);
                Assert.Multiple(() =>
                {
                    Assert.That(actual.Result, Is.EqualTo(autoplay ? HitResult.Perfect : HitResult.Miss));
                    Assert.That(keyJudgement.Grade, Is.EqualTo(autoplay ? GameplaySkinJudgementGrade.Perfect : GameplaySkinJudgementGrade.Miss));
                    Assert.That(keyJudgement.Offset, Is.EqualTo(actual.TimeOffset));
                    Assert.That(renderer.LayoutProvider.RevisionOwner!.CurrentPublication!.Snapshot.GetLane(keyJudgement.LaneId!).TopologyEntry.Identity.Role,
                        Is.EqualTo(GameplaySkinLaneRole.Key));
                    Assert.That(snapshot.Inputs.All(input => !input.IsPressed), Is.True);
                    Assert.That(group(snapshot, groupId).Keys?.Direction,
                        Is.EqualTo(autoplay ? null : (GameplaySkinJudgementTimingDirection?)GameplaySkinJudgementTimingDirection.Slow));
                    Assert.That(group(snapshot, groupId).Scratch, Is.Null);
                    Assert.That(snapshot.Score.Statistics.Fast, Is.Zero);
                    Assert.That(snapshot.Score.Statistics.Slow, Is.Zero);
                    if (!autoplay)
                    {
                        Assert.That(keyJudgement.HasPlayerTiming, Is.False);
                        Assert.That(observed.Any(edge => edge.EventKind == GameplaySkinEventKind.InputPressed), Is.False);
                    }
                });
            });

            advanceTrackTo(3400);
            AddUntilStep("the transient key POOR display expires", () => current(renderer).CurrentJudgements.Count == 0);
            AddStep("key SLOW survives the grade expiry", () =>
                Assert.That(group(current(renderer), groupId).Keys, Is.EqualTo(keyJudgement.JudgementTimingState.Keys)));

            advanceTrackTo(4600);
            AddUntilStep("the unpressed scratch head is naturally judged", () =>
                Player.ScoreProcessor.HitEvents.Any(hit => hit.HitObject is BmsHitObject && hit.HitObject.StartTime == 4000));
            AddStep("scratch feedback retains the key channel and zero cumulative timing counts", () =>
            {
                subscription.DrainFrame(observed.Add);
                GameplaySkinJudgementStateSnapshot scratch = observed.Select(edge => edge.Payload).OfType<GameplaySkinJudgementEventPayload>().Last().State;
                GameplaySkinEventStateSnapshot snapshot = current(renderer);
                var actual = Player.ScoreProcessor.HitEvents.Single(hit => hit.HitObject is BmsHitObject && hit.HitObject.StartTime == 4000);
                Assert.Multiple(() =>
                {
                    Assert.That(scratch.GroupId, Is.EqualTo(groupId));
                    Assert.That(renderer.LayoutProvider.RevisionOwner!.CurrentPublication!.Snapshot.GetLane(scratch.LaneId!).TopologyEntry.Identity.Role,
                        Is.EqualTo(GameplaySkinLaneRole.Scratch));
                    Assert.That(actual.Result, Is.EqualTo(autoplay ? HitResult.Perfect : HitResult.Miss));
                    Assert.That(scratch.Offset, Is.EqualTo(actual.TimeOffset));
                    Assert.That(group(snapshot, groupId).Keys, Is.EqualTo(keyJudgement.JudgementTimingState.Keys));
                    Assert.That(group(snapshot, groupId).Scratch?.Direction,
                        Is.EqualTo(autoplay ? null : (GameplaySkinJudgementTimingDirection?)GameplaySkinJudgementTimingDirection.Slow));
                    Assert.That(snapshot.Score.Statistics.Fast, Is.Zero);
                    Assert.That(snapshot.Score.Statistics.Slow, Is.Zero);
                    Assert.That(Player.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss), Is.EqualTo(autoplay ? 0 : 2));
                    Assert.That(renderer.GameplaySkinEventStream.CurrentEpoch, Is.EqualTo(epoch), "Ordinary progression does not invent a seek/reset.");
                    if (!autoplay)
                    {
                        Assert.That(scratch.HasPlayerTiming, Is.False);
                        Assert.That(snapshot.Inputs.All(input => !input.IsPressed), Is.True);
                        Assert.That(observed.Any(edge => edge.EventKind == GameplaySkinEventKind.InputPressed), Is.False);
                    }
                });
            });
            AddStep("detach the production consumer", () => subscription.Dispose());
        }

        private void advanceTrackTo(double time)
            => AddStep($"advance the real track to {time} ms without sending gameplay input", () =>
            {
                manualClock.CurrentTime = time;
                referenceClock.ProcessFrame();
            });

        private static GameplaySkinEventStateSnapshot current(DrawableBmsRuleset renderer)
        {
            using GameplaySkinEventSubscription late = renderer.GameplaySkinEventStream.Subscribe();
            GameplaySkinEventStateSnapshot state = null!;
            late.DrainFrame(edge => state = ((GameplaySkinStateEventPayload)edge.Payload).State);
            return state;
        }

        private static GameplaySkinJudgementTimingStateSnapshot group(GameplaySkinEventStateSnapshot state, GameplaySkinLaneGroupId groupId)
            => state.JudgementTimingGroups.Single(entry => entry.GroupId == groupId).State;
    }
}
