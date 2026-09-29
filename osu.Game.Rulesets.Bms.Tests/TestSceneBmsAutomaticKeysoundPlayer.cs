// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsAutomaticKeysoundPlayer : PlayerTestScene
    {
        // TrackVirtualManual supports rate zero specifically to prevent wall-time interpolation in tests.
        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };
        private readonly FramedClock referenceClock;
        private bool mania;
        private bool holdsOnly;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        public TestSceneBmsAutomaticKeysoundPlayer()
        {
            referenceClock = new FramedClock(manualClock);
        }

        protected override Ruleset CreatePlayerRuleset() => mania ? new ManiaRuleset() : new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            // At BPM 120 each measure is 2000 ms: BGM/scratch at 2000, keys at 4000 and 6000,
            // hold head/tail at 8000/9000. A distant playable note keeps the player open for assertions.
            var chart = new BmsBeatmapDecoder().DecodeText(@"
#TITLE Automatic keysound player
#BPM 120
#RANK 2
#WAVAA bgm.wav
#WAVBB key.wav
#WAVCC scratch.wav
#WAVDD hold.wav
#WAVEE tail.wav
#WAVFF sentinel.wav
" + (holdsOnly ? @"
#00452:DDEE
#01052:DDEE
" : @"
#00101:AA
#00116:CC
#00211:BB
#00311:BB
#00452:DDEE
#01011:FF
"), "automatic-keysound-player.bme");

            return new BmsDecodedBeatmap(chart)
            {
                BeatmapInfo = { Ruleset = new BmsRuleset().RulesetInfo },
            };
        }

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new OsuTestScene.ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        [TestCase(false)]
        [TestCase(true)]
        public void TestNoInputPlaysKeysoundsButStillMisses(bool convertedToMania)
        {
            loadPlayer(convertedToMania, true);
            advanceTo(2500);
            AddAssert("background plays once", () => playCount("bgm.wav"), () => Is.EqualTo(1));
            AddAssert("scratch plays once", () => playCount("scratch.wav"), () => Is.EqualTo(1));
            advanceTo(4500);
            AddAssert("missed key still sounds", () => playCount("key.wav"), () => Is.EqualTo(1));
            advanceTo(6500);
            advanceTo(8500);
            advanceTo(10500);
            assertChartSounds();
            AddAssert("misses still recorded", () => Player.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss), () => Is.GreaterThan(0));
            AddAssert("automatic sound grants no combo", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.Zero);
            AddAssert("automatic sound grants no score", () => Player.ScoreProcessor.TotalScore.Value, () => Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestHoldOnlyChartCreatesAutomaticSoundPlayer(bool convertedToMania)
        {
            loadPlayer(convertedToMania, true, true);
            advanceTo(8500);
            AddAssert("hold-only chart plays head without input", () => playCount("hold.wav"), () => Is.EqualTo(1));
            advanceTo(10500);
            AddAssert("hold-only chart keeps tail silent", () => playCount("tail.wav"), () => Is.Zero);
            AddAssert("hold-only chart still misses", () => Player.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss), () => Is.GreaterThan(0));
            AddAssert("hold-only chart grants no combo", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSameInputKeepsJudgementsScoreAndHealth(bool convertedToMania)
        {
            HitResult[] baselineResults = Array.Empty<HitResult>();
            double[] baselineOffsets = Array.Empty<double>();
            long baselineScore = 0;
            int baselineCombo = 0;
            double baselineHealth = 0;
            double baselineAccuracy = 0;

            loadPlayer(convertedToMania, false);
            playImperfectInput(false);
            AddStep("capture manual sound performance", () =>
            {
                baselineResults = Player.Results.Select(result => result.Type).ToArray();
                baselineOffsets = Player.Results.Select(result => result.TimeOffset).ToArray();
                baselineScore = Player.ScoreProcessor.TotalScore.Value;
                baselineCombo = Player.ScoreProcessor.HighestCombo.Value;
                baselineHealth = Player.HealthProcessor.Health.Value;
                baselineAccuracy = Player.ScoreProcessor.Accuracy.Value;
                Assert.That(baselineCombo, Is.GreaterThan(0), "the control run must hit notes through real input");
            });

            loadPlayer(convertedToMania, true);
            playImperfectInput(true);
            assertChartSounds();
            AddAssert("same judgement sequence", () => Player.Results.Select(result => result.Type).ToArray(), () => Is.EqualTo(baselineResults));
            AddAssert("same judgement timing errors", () => Player.Results.Select(result => result.TimeOffset).ToArray(), () => Is.EqualTo(baselineOffsets).Within(0.01));
            AddAssert("same score", () => Player.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(baselineScore));
            AddAssert("same accuracy", () => Player.ScoreProcessor.Accuracy.Value, () => Is.EqualTo(baselineAccuracy).Within(0.000001));
            AddAssert("same highest combo", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.EqualTo(baselineCombo));
            AddAssert("same health", () => Player.HealthProcessor.Health.Value, () => Is.EqualTo(baselineHealth).Within(0.000001));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPauseAndBackwardSeekReplayScheduledSound(bool convertedToMania)
        {
            double pausedTime = 0;
            loadPlayer(convertedToMania, true);
            advanceTo(5500);
            AddAssert("first key played", () => playCount("key.wav"), () => Is.EqualTo(1));
            AddStep("pause before second key", () =>
            {
                pausedTime = Player.GameplayClockContainer.CurrentTime;
                Assert.That(Player.Pause(), Is.True);
            });
            AddUntilStep("paused", () => Player.GameplayClockContainer.IsPaused.Value);
            AddStep("advance reference while paused", () =>
            {
                manualClock.CurrentTime += 1000;
                referenceClock.ProcessFrame();
            });
            AddAssert("gameplay clock held", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(pausedTime).Within(1));
            AddAssert("pause cannot play future key", () => playCount("key.wav"), () => Is.EqualTo(1));
            AddStep("resume", () => Player.Resume());
            AddUntilStep("running", () => Player.GameplayClockContainer.IsRunning);
            advanceTo(6500);
            AddUntilStep("second key played", () => playCount("key.wav"), () => Is.EqualTo(2));
            AddStep("seek back before second key", () => Player.GameplayClockContainer.Seek(5700));
            AddUntilStep("rewound", () => Player.DrawableRuleset.FrameStableClock.CurrentTime, () => Is.EqualTo(5700).Within(1));
            advanceTo(6300);
            AddUntilStep("key sounds once more after rewind", () => playCount("key.wav"), () => Is.EqualTo(3));
            AddAssert("rewind does not replay old BGM", () => playCount("bgm.wav"), () => Is.EqualTo(1));
        }

        [Test]
        public void TestManualConvertedHoldUsesSharedStoreAndSilentTail()
        {
            loadPlayer(true, false);
            advanceTo(8030);
            pressKey(2, true);
            AddAssert("manual hold head reaches store once", () => playCount("hold.wav"), () => Is.EqualTo(1));
            AddAssert("original head WAV slot is preserved", () => store!.PlaybackLogForTesting.Single(r => r.Filename == "hold.wav").CutGroup,
                () => Is.EqualTo(Player.DrawableRuleset.Objects.OfType<osu.Game.Rulesets.Bms.Objects.BmsConvertedHoldNoteHitObject>().Single().KeysoundId));
            advanceTo(9000);
            pressKey(2, false);
            advanceTo(10500);
            AddAssert("manual head did not repeat", () => playCount("hold.wav"), () => Is.EqualTo(1));
            AddAssert("manual tail stays silent", () => playCount("tail.wav"), () => Is.Zero);
            AddAssert("hold was played through real input", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.GreaterThan(0));
        }

        private void loadPlayer(bool convertedToMania, bool automatic, bool onlyHolds = false)
        {
            CreateTest(() => AddStep("select mode and automatic keysound", () =>
            {
                mania = convertedToMania;
                holdsOnly = onlyHolds;
                manualClock.CurrentTime = 0;
                referenceClock.ProcessFrame();
                // Opposite preferences prove each mode reads only its own setting.
                ((BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!).SetValue(BmsRulesetSetting.AutoKeysound, convertedToMania ? !automatic : automatic);
                ((ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset())!).SetValue(ManiaRulesetSetting.AutoKeysoundForBms, convertedToMania ? automatic : !automatic);
            }));
            AddUntilStep("keysound channels ready", () => store != null && store.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready));
            AddAssert("automatic sound preference applied", () => store!.AutomaticPlayback, () => Is.EqualTo(automatic));
            AddAssert("no automatic input mod", () => !Player.Mods.Value.OfType<ModAutoplay>().Any());
            AddStep("record sound requests", () => store!.EnablePlaybackLogForTesting());
            AddUntilStep("track running", () => Beatmap.Value.Track.IsRunning);
        }

        private void playImperfectInput(bool automatic)
        {
            advanceTo(2500);
            advanceTo(3970);
            pressKey(1, true);
            pressKey(1, false);
            if (automatic)
                AddAssert("early hit cannot play sound early", () => playCount("key.wav"), () => Is.Zero);
            advanceTo(4000);
            if (automatic)
                AddAssert("early-hit sound plays at chart time", () => playCount("key.wav"), () => Is.EqualTo(1));
            advanceTo(4500);
            advanceTo(6030);
            pressKey(1, true);
            pressKey(1, false);
            advanceTo(7000);
            pressKey(1, true);
            pressKey(1, false);
            pressKey(1, true);
            pressKey(1, false);
            advanceTo(8030);
            pressKey(2, true);
            advanceTo(8500);
            pressKey(2, false);
            advanceTo(10500);
        }

        private void pressKey(int key, bool pressed)
            => AddStep($"{(pressed ? "press" : "release")} key {key}", () =>
            {
                if (mania)
                {
                    var bindings = Player.DrawableRuleset.ChildrenOfType<ManiaInputManager>().First().KeyBindingContainer;
                    var action = key == 1 ? ManiaAction.Key1 : ManiaAction.Key2;
                    if (pressed)
                        bindings.TriggerPressed(action);
                    else
                        bindings.TriggerReleased(action);
                }
                else
                {
                    var bindings = Player.ChildrenOfType<BmsInputManager>().Single().KeyBindingContainer;
                    var action = key == 1 ? BmsAction.Key1 : BmsAction.Key2;
                    if (pressed)
                        bindings.TriggerPressed(action);
                    else
                        bindings.TriggerReleased(action);
                }
            });

        private void assertChartSounds()
        {
            AddAssert("two keys sound exactly once each", () => playCount("key.wav"), () => Is.EqualTo(2));
            AddAssert("hold head sounds once", () => playCount("hold.wav"), () => Is.EqualTo(1));
            AddAssert("hold tail is silent", () => playCount("tail.wav"), () => Is.Zero);
            AddAssert("background does not double", () => playCount("bgm.wav"), () => Is.EqualTo(1));
            AddAssert("scratch does not double", () => playCount("scratch.wav"), () => Is.EqualTo(1));
        }

        private void advanceTo(double target)
        {
            AddStep($"advance gameplay clock to {target:N0} ms", () =>
            {
                // Move only the deterministic test source. GameplayClockContainer.Seek is reserved for the
                // explicit rewind test: it emits OnSeek and would intentionally skip scheduled sounds.
                var clock = Player.GameplayClockContainer.ChildrenOfType<FramedBeatmapClock>().Single();
                ((IAdjustableClock)clock.Source).Seek(target - clock.TotalAppliedOffset);
            });
            AddUntilStep($"gameplay reaches {target:N0} ms", () => Player.DrawableRuleset.FrameStableClock.CurrentTime, () => Is.EqualTo(target).Within(1));
        }

        private BmsKeysoundStore? store => Player?.ChildrenOfType<BmsKeysoundStore>().SingleOrDefault();

        private int playCount(string filename) => store!.PlaybackLogForTesting.Count(record => record.Filename == filename);
    }
}
