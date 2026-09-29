// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
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
