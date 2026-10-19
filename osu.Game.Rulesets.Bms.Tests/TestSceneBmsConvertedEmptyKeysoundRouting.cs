// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsConvertedEmptyKeysoundRouting : PlayerTestScene
    {
        private readonly ManualClock source = new ManualClock { Rate = 0 };
        private readonly FramedClock reference;
        private bool converted;
        private bool hold;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        public TestSceneBmsConvertedEmptyKeysoundRouting()
        {
            reference = new FramedClock(source);
        }

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            if (converted)
            {
                string text = "#TITLE Converted empty-key audio\n#BPM 120\n#LNTYPE 1\n#WAV01 long.wav\n#WAV02 sentinel.wav\n"
                              + (hold ? "#00351:0101\n" : "#00311:01\n") + "#01012:02\n";
                return new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(text, "empty-key.bme"))
                {
                    BeatmapInfo = { Ruleset = new BmsRuleset().RulesetInfo },
                };
            }

            var beatmap = new ManiaBeatmap(new StageDefinition(7)) { BeatmapInfo = { Ruleset = ruleset } };
            var samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) };
            beatmap.HitObjects.Add(hold
                ? new HoldNote { StartTime = 6000, EndTime = 7000, Column = 0, Samples = samples }
                : new Note { StartTime = 6000, Column = 0, Samples = samples });
            beatmap.HitObjects.Add(new Note { StartTime = 20000, Column = 1, Samples = samples });
            return beatmap;
        }

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, reference, audio);

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void TestEarlyAndEmptyPressUseTheOwningAudioPath(bool isConverted, bool isHold)
        {
            CreateTest(() => AddStep("choose converted or ordinary mania with manual keysounds", () =>
            {
                converted = isConverted;
                hold = isHold;
                source.CurrentTime = 0;
                reference.ProcessFrame();
                ((ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset())!)
                    .SetValue(ManiaRulesetSetting.AutoKeysoundForBms, false);
            }));
            AddUntilStep("the player and its keysound ownership are loaded", () => Player?.IsLoaded == true
                && (!converted || store != null && store.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready)));
            AddStep("record the shared store when hosted", () => store?.EnablePlaybackLogForTesting());
            AddUntilStep("the test track is running", () => Beatmap.Value.Track.IsRunning);
            advanceTo(1000);
            pressAndRelease();
            assertRoute(1);
            AddAssert("an early empty press grants no combo", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.Zero);
            advanceTo(7500);
            pressAndRelease();
            assertRoute(2);
            AddStep("pause after the manual audio request", () => Assert.That(Player.Pause(), Is.True));
            AddUntilStep("gameplay is paused", () => Player.GameplayClockContainer.IsPaused.Value);
            if (isConverted)
                AddAssert("all converted voices use the existing pause frequency", () => store!.ChannelPool.All(channel => channel.AggregateFrequency.Value == 0));
        }

        private BmsKeysoundStore? store => Player?.ChildrenOfType<BmsKeysoundStore>().SingleOrDefault();

        private void advanceTo(double time)
        {
            AddStep($"advance the source clock to {time}ms", () =>
            {
                FramedBeatmapClock clock = Player.GameplayClockContainer.ChildrenOfType<FramedBeatmapClock>().Single();
                ((IAdjustableClock)clock.Source).Seek(time - clock.TotalAppliedOffset);
            });
            AddUntilStep("the real ruleset clock settles", () => Player.DrawableRuleset.FrameStableClock.CurrentTime,
                () => Is.EqualTo(time).Within(1));
        }

        private void pressAndRelease()
        {
            AddStep("press the actual first mania key", () => Player.DrawableRuleset.ChildrenOfType<ManiaInputManager>()
                .First().KeyBindingContainer.TriggerPressed(ManiaAction.Key1));
            AddStep("release the actual first mania key", () => Player.DrawableRuleset.ChildrenOfType<ManiaInputManager>()
                .First().KeyBindingContainer.TriggerReleased(ManiaAction.Key1));
            AddWaitStep("allow the ordinary scheduled feedback path to run", 2);
        }

        private void assertRoute(int expectedRequests)
        {
            AddStep("verify the player's actual manual audio route", () =>
            {
                PausableSkinnableSound[] ordinaryFeedback = Player.DrawableRuleset.ChildrenOfType<GameplaySampleTriggerSource>()
                    .SelectMany(trigger => trigger.ChildrenOfType<PausableSkinnableSound>()).ToArray();
                if (converted)
                {
                    Assert.That(store!.PlaybackLogForTesting.Count(record => record.Filename == "long.wav"), Is.EqualTo(expectedRequests),
                        "Converted early and empty presses must use the same slot-aware store as ordinary hits.");
                    Assert.That(store.PlaybackLogForTesting.Where(record => record.Filename == "long.wav").All(record => record.CutGroup == 1), Is.True);
                    Assert.That(ordinaryFeedback.Any(sound => sound.RequestedPlaying), Is.False,
                        "A second ordinary feedback pool must not play converted key audio outside pause/seek/cut authority.");
                }
                else
                {
                    Assert.That(store, Is.Null);
                    Assert.That(ordinaryFeedback.Any(sound => sound.RequestedPlaying), Is.True,
                        "Native mania retains its existing ordinary sample feedback.");
                }
            });
        }
    }
}
