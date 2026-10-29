// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsAutomaticOffset : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private DrawableBmsRuleset ruleset = null!;
        private ManualClock clock = null!;
        private FramedClock framedClock = null!;
        private BmsHitObject note = null!;
        private DrawableBmsHitObject drawable = null!;

        private void createScene(AutomaticOffsetStyle style, double offset, bool hold = false)
        {
            AddStep("create BMS with offset", () =>
            {
                config.SetValue(OsuSetting.AutomaticOffsetStyle, style);
                config.SetValue(OsuSetting.BmsVisualOffset, offset);
                string channel = hold ? "#00151:0101\n" : "#00111:01\n";
                var chart = new BmsBeatmapDecoder().DecodeText("#TITLE Offset\n#BPM 120\n#RANK 2\n#WAV01 key.wav\n" + channel, "offset.bme");
                var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), new BmsRuleset()).Convert();
                note = beatmap.HitObjects.OfType<BmsHitObject>().Single();
                clock = new ManualClock { CurrentTime = note.StartTime - 500, IsRunning = false };
                framedClock = new FramedClock(clock);
                ruleset = new DrawableBmsRuleset(new BmsRuleset(), beatmap) { RelativeSizeAxes = Axes.Both, Clock = framedClock };
                ruleset.InitialiseCompatibilityLayoutForTesting();
                Child = ruleset;
            });
            AddUntilStep("note loaded", () =>
            {
                drawable = ruleset.Playfield.Lanes.SelectMany(l => l.AllHitObjects).OfType<DrawableBmsHitObject>().FirstOrDefault(d => d.HitObject == note)!;
                return drawable?.IsLoaded == true;
            });
        }

        private void seek(double time)
        {
            clock.CurrentTime = time;
            framedClock.ProcessFrame();
            ruleset.UpdateSubTree();
        }

        [TestCase(AutomaticOffsetStyle.Off, -500, -500)]
        [TestCase(AutomaticOffsetStyle.Lazer, 500, 500)]
        [TestCase(AutomaticOffsetStyle.Beatoraja, 0, 1)]
        [TestCase(AutomaticOffsetStyle.Beatoraja, -500, -499)]
        public void TestRealHitKeepsJudgementAndKeysoundTiming(AutomaticOffsetStyle style, double initial, double expected)
        {
            createScene(style, initial);
            AddStep("seek to late input", () => seek(note.StartTime + 30));
            AddUntilStep("clock reaches late input", () => drawable.Time.Current == note.StartTime + 30);
            AddStep("press 30ms late", () =>
            {
                Assert.That(drawable.OnPressed(new KeyBindingPressEvent<BmsAction>(new InputState(), BmsAction.Key1)), Is.True);
            });
            AddAssert("same GREAT", () => drawable.Result.Type, () => Is.EqualTo(HitResult.Great));
            AddAssert("same actual error", () => drawable.Result.TimeOffset, () => Is.EqualTo(30).Within(0.01));
            AddAssert("keysound requested immediately", () => ruleset.Playfield.KeysoundStore.ChannelPool.Any(c => c.RequestedPlaying));
            AddAssert("only selected style adjusts", () => config.Get<double>(OsuSetting.BmsVisualOffset), () => Is.EqualTo(expected));
            AddStep("stop auto adjustment", () => config.SetValue(OsuSetting.AutomaticOffsetStyle, AutomaticOffsetStyle.Off));
            AddStep("later sample cannot change fixed value", () => ruleset.ApplyAutomaticOffsetSample(HitResult.Great, 60));
            AddAssert("fixed value retained", () => ruleset.Playfield.VisualOffset.Value, () => Is.EqualTo(expected));
        }

        [Test]
        public void TestRecordingEndFlushesDisplayAndReplayDoesNotChangePreferences()
        {
            Score score = null!;
            createScene(AutomaticOffsetStyle.Beatoraja, 20);
            AddStep("start recording", () => ruleset.SetRecordTarget(score = new Score()));
            AddStep("seek to recorded input", () => seek(note.StartTime + 30));
            AddUntilStep("clock reaches recorded input", () => drawable.Time.Current == note.StartTime + 30);
            AddStep("hit and end recording", () =>
            {
                drawable.OnPressed(new KeyBindingPressEvent<BmsAction>(new InputState(), BmsAction.Key1));
                ruleset.SetRecordTarget(null!);
            });
            AddAssert("history saved before score clone", () => score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.VisualOffset!.OffsetAt(note.StartTime + 30), () => Is.EqualTo(21));
            AddStep("change personal preference and load replay", () =>
            {
                config.SetValue(OsuSetting.BmsVisualOffset, -42d);
                ruleset.SetReplayScore(score);
                seek(note.StartTime - 100);
            });
            AddAssert("replay initial offset", () => ruleset.Playfield.VisualOffset.Value, () => Is.EqualTo(20));
            AddStep("seek forward", () => seek(note.StartTime + 100));
            AddAssert("replay recorded offset", () => ruleset.Playfield.VisualOffset.Value, () => Is.EqualTo(21));
            AddStep("replay sample cannot learn", () => ruleset.ApplyAutomaticOffsetSample(HitResult.Great, 60));
            AddAssert("personal offset unchanged", () => config.Get<double>(OsuSetting.BmsVisualOffset), () => Is.EqualTo(-42));
            AddStep("load historical replay", () => ruleset.SetReplayScore(new Score()));
            AddAssert("old replay has zero visual correction", () => ruleset.Playfield.VisualOffset.Value, () => Is.Zero);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void TestReplayAutomaticDisplayUsesActualModeRatherThanOffsetChangeCount(bool automatic, bool manualChange)
        {
            Score score = null!;
            createScene(automatic ? AutomaticOffsetStyle.Beatoraja : AutomaticOffsetStyle.Off, 0);
            AddStep("record the actual opening automatic mode", () => ruleset.SetRecordTarget(score = new Score()));
            AddStep("advance to a real manual configuration boundary", () => seek(note.StartTime - 200));
            AddStep("change only the manual offset when requested then flush", () =>
            {
                if (manualChange)
                    config.SetValue(OsuSetting.BmsVisualOffset, 20d);
                ruleset.SetRecordTarget(null!);
                var saved = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
                Assert.That(saved.ReadingCalculationVersion, Is.EqualTo(1));
                Assert.That(saved.VisualOffset!.Automatic!.Initial, Is.EqualTo(automatic));
                Assert.That(saved.VisualOffset.Automatic.Changes, Is.Empty);
                Assert.That(saved.VisualOffset.Changes.Count, Is.EqualTo(manualChange ? 1 : 0));
            });
            AddStep("load the recording against different personal preferences", () =>
            {
                config.SetValue(OsuSetting.AutomaticOffsetStyle, automatic ? AutomaticOffsetStyle.Off : AutomaticOffsetStyle.Beatoraja);
                config.SetValue(OsuSetting.BmsVisualOffset, -42d);
                ruleset.SetReplayScore(score);
                seek(note.StartTime - 250);
            });
            AddAssert("initial automatic UI is known independently of dynamic scrolling", () =>
                ruleset.GetAppliedVisualOffsetState().AutomaticAvailable
                && ruleset.GetAppliedVisualOffsetState().Automatic == automatic
                && ruleset.GetAppliedVisualOffsetState().Milliseconds == 0
                && ruleset.Playfield.AutomaticVisualOffsetEnabled.Value == manualChange);
            AddStep("seek beyond the manual offset boundary", () => seek(note.StartTime - 100));
            AddAssert("the real offset history does not invent automatic mode", () =>
                ruleset.Playfield.VisualOffset.Value == (manualChange ? 20 : 0)
                && ruleset.GetAppliedVisualOffsetState().AutomaticAvailable
                && ruleset.GetAppliedVisualOffsetState().Automatic == automatic);
            AddAssert("playback does not learn either personal preference", () =>
                config.Get<double>(OsuSetting.BmsVisualOffset) == -42
                && config.Get<AutomaticOffsetStyle>(OsuSetting.AutomaticOffsetStyle) == (automatic ? AutomaticOffsetStyle.Off : AutomaticOffsetStyle.Beatoraja));
        }

        [Test]
        public void TestAutoOnlyChangesUseNativeReplayClockAndRestoreOnSeekAndRetry()
        {
            Score score = null!;
            Score retry = null!;
            string original = string.Empty;
            createScene(AutomaticOffsetStyle.Off, 0);
            AddStep("start an actual native recording", () => ruleset.SetRecordTarget(score = new Score()));
            AddStep("enable automatic mode at its native clock", () =>
            {
                seek(note.StartTime - 400);
                config.SetValue(OsuSetting.AutomaticOffsetStyle, AutomaticOffsetStyle.Beatoraja);
            });
            AddStep("disable automatic mode later without changing numeric offset", () =>
            {
                seek(note.StartTime - 200);
                config.SetValue(OsuSetting.AutomaticOffsetStyle, AutomaticOffsetStyle.Off);
                ruleset.SetRecordTarget(null!);
                var saved = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
                Assert.That(saved.VisualOffset!.Changes, Is.Empty);
                Assert.That(saved.VisualOffset.Automatic!.Changes.Select(change => change.Time),
                    Is.EqualTo(new[] { note.StartTime - 400, note.StartTime - 200 }));
                Assert.That(saved.VisualOffset.Automatic.Changes.Select(change => change.Enabled), Is.EqualTo(new bool?[] { true, false }));
                Assert.That(score.Replay.Frames.Any(frame => frame.Time == note.StartTime - 400), Is.True);
                Assert.That(score.Replay.Frames.Any(frame => frame.Time == note.StartTime - 200), Is.True);
                original = score.ScoreInfo.RulesetDataJson;
                config.SetValue(OsuSetting.AutomaticOffsetStyle, AutomaticOffsetStyle.Beatoraja);
                config.SetValue(OsuSetting.BmsVisualOffset, -42d);
                ruleset.SetReplayScore(score);
            });
            AddStep("seek before either mode edge", () => seek(note.StartTime - 450));
            AddAssert("recorded initial Off is known", () => ruleset.GetAppliedVisualOffsetState().AutomaticAvailable && !ruleset.GetAppliedVisualOffsetState().Automatic);
            AddStep("seek between the mode edges", () => seek(note.StartTime - 300));
            AddAssert("recorded On remains known with zero offset changes", () => ruleset.GetAppliedVisualOffsetState().AutomaticAvailable && ruleset.GetAppliedVisualOffsetState().Automatic);
            AddStep("seek after the Off edge", () => seek(note.StartTime - 100));
            AddAssert("recorded Off returns", () => ruleset.GetAppliedVisualOffsetState().AutomaticAvailable && !ruleset.GetAppliedVisualOffsetState().Automatic);
            AddStep("rewind and reattach the same history", () =>
            {
                ruleset.SetReplayScore(score);
                seek(note.StartTime - 300);
            });
            AddAssert("reset restores the same On interval", () => ruleset.GetAppliedVisualOffsetState().AutomaticAvailable && ruleset.GetAppliedVisualOffsetState().Automatic);
            AddAssert("viewing preserves source metadata and personal preferences", () => score.ScoreInfo.RulesetDataJson == original
                && config.Get<double>(OsuSetting.BmsVisualOffset) == -42
                && config.Get<AutomaticOffsetStyle>(OsuSetting.AutomaticOffsetStyle) == AutomaticOffsetStyle.Beatoraja);
            AddStep("a fresh recording captures today's real initial mode", () =>
            {
                ruleset.SetReplayScore(null!);
                ruleset.SetRecordTarget(retry = new Score());
                ruleset.SetRecordTarget(null!);
                var saved = retry.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.VisualOffset!;
                Assert.That(saved.Automatic!.Initial, Is.True);
                Assert.That(saved.Automatic.Changes, Is.Empty);
                Assert.That(saved.InitialOffset, Is.EqualTo(-42));
            });
        }

        [Test]
        public void TestLegacyManualOffsetHistoryRemainsDynamicButItsAutomaticModeIsUnknown()
        {
            createScene(AutomaticOffsetStyle.Beatoraja, -42);
            AddStep("load literal old offset-only metadata", () =>
            {
                var score = new Score
                {
                    ScoreInfo = { Ruleset = new BmsRuleset().RulesetInfo,
                        RulesetDataJson = "{\"visual_offset\":{\"initial\":0,\"changes\":[{\"time\":1000,\"offset\":20}]}}" },
                    Replay = new Replay { Frames = { new BmsReplayFrame(0), new BmsReplayFrame(20000) } },
                };
                string original = score.ScoreInfo.RulesetDataJson;
                ruleset.SetReplayScore(score);
                seek(500);
                Assert.That(ruleset.Playfield.AutomaticVisualOffsetEnabled.Value, Is.True, "Manual offset history still requires its original dynamic scrolling wrapper.");
                Assert.That(ruleset.GetAppliedVisualOffsetState().IsAvailable, Is.True);
                Assert.That(ruleset.GetAppliedVisualOffsetState().AutomaticAvailable, Is.False);
                Assert.That(ruleset.Playfield.VisualOffset.Value, Is.Zero);
                seek(1200);
                Assert.That(ruleset.GetAppliedVisualOffsetState().AutomaticAvailable, Is.False);
                Assert.That(ruleset.GetAppliedVisualOffsetState().Milliseconds, Is.EqualTo(20));
                seek(500);
                Assert.That(ruleset.Playfield.VisualOffset.Value, Is.Zero);
                Assert.That(score.ScoreInfo.RulesetDataJson, Is.EqualTo(original));
                Assert.That(config.Get<double>(OsuSetting.BmsVisualOffset), Is.EqualTo(-42));
            });
        }

        [TestCase("{\"initial\":0,\"changes\":[],\"automatic\":{\"changes\":[]}}")]
        [TestCase("{\"initial\":0,\"changes\":[],\"automatic\":{\"initial\":false,\"changes\":[{\"time\":1000}]}}")]
        public void TestMalformedAutomaticHistoryIsRejectedBeforeTheReplayIsAdopted(string visualOffset)
        {
            createScene(AutomaticOffsetStyle.Beatoraja, -42);
            AddStep("reject malformed automatic metadata at the actual playback boundary", () =>
            {
                var score = new Score
                {
                    ScoreInfo = { Ruleset = new BmsRuleset().RulesetInfo, RulesetDataJson = "{\"visual_offset\":" + visualOffset + "}" },
                };
                string original = score.ScoreInfo.RulesetDataJson;
                Assert.Throws<InvalidDataException>(() => ruleset.SetReplayScore(score));
                Assert.That(ruleset.HasReplayLoaded.Value, Is.False);
                Assert.That(ruleset.GetAppliedVisualOffsetState().AutomaticAvailable, Is.True);
                Assert.That(ruleset.GetAppliedVisualOffsetState().Automatic, Is.True);
                Assert.That(ruleset.Playfield.VisualOffset.Value, Is.EqualTo(-42));
                Assert.That(score.ScoreInfo.RulesetDataJson, Is.EqualTo(original));
                Assert.That(config.Get<double>(OsuSetting.BmsVisualOffset), Is.EqualTo(-42));
            });
        }

        [TestCase(BmsLongNoteMode.LN, 0, -2)]
        [TestCase(BmsLongNoteMode.CN, 1, -1)]
        [TestCase(BmsLongNoteMode.HCN, 1, -1)]
        public void TestLongNoteSamplesActualHeadAndReleaseOnce(BmsLongNoteMode mode, double afterHead, double afterRelease)
        {
            createScene(AutomaticOffsetStyle.Beatoraja, 0, hold: true);
            AddStep("seek to late head", () => seek(note.StartTime + 30));
            AddUntilStep("clock reaches late head", () => drawable.Time.Current == note.StartTime + 30);
            AddStep("press long note late", () =>
            {
                ((DrawableBmsHoldNote)drawable).LongNoteModeOverrideForTesting = mode;
                drawable.OnPressed(new KeyBindingPressEvent<BmsAction>(new InputState(), BmsAction.Key1));
            });
            AddAssert("head sample appropriate to mode", () => config.Get<double>(OsuSetting.BmsVisualOffset), () => Is.EqualTo(afterHead));
            AddStep("seek to release", () => seek(((BmsHoldNote)note).EndTime - 60));
            AddUntilStep("clock reaches release", () => drawable.Time.Current == ((BmsHoldNote)note).EndTime - 60);
            AddStep("release 60ms early", () =>
            {
                Assert.That(((DrawableBmsHoldNote)drawable).IsHoldingForTesting, Is.True, "hold survives until release");
                Assert.That(drawable.Time.Current, Is.EqualTo(((BmsHoldNote)note).EndTime - 60).Within(0.01), "release uses requested clock");
                drawable.OnReleased(new KeyBindingReleaseEvent<BmsAction>(new InputState(), BmsAction.Key1));
            });
            AddAssert("only semantic tail completion sampled", () => config.Get<double>(OsuSetting.BmsVisualOffset), () => Is.EqualTo(afterRelease));
            AddStep("repeat release", () => drawable.OnReleased(new KeyBindingReleaseEvent<BmsAction>(new InputState(), BmsAction.Key1)));
            AddAssert("completed hold cannot sample twice", () => config.Get<double>(OsuSetting.BmsVisualOffset), () => Is.EqualTo(afterRelease));
        }
    }
}
