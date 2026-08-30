// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class BmsToManiaBeatmapConverterTest
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly ManiaRuleset maniaRuleset = new ManiaRuleset();

        [Test]
        public void TestFiveKeyScratchUsesSampleOnlyAutoplayWithoutExtraJudgementColumn()
        {
            const string text = @"
#TITLE 5K Mapping
#BPM 120
#WAVAA key1.wav
#WAVBB scratch.wav
#WAVCC key5.wav
#00111:AA00
#00116:BB00
#00115:CC00
";

            var convertedBeatmap = convertToMania(text, "fivekey.bms", keymodeOverride: BmsKeymode.Key5K);
            var key1 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "key1.wav");
            var key5 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "key5.wav");
            var scratch = convertedBeatmap.HitObjects.OfType<BmsConvertedScratchSampleHitObject>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.Stages, Has.Count.EqualTo(1));
                Assert.That(convertedBeatmap.TotalColumns, Is.EqualTo(5));
                Assert.That(key1.Column, Is.EqualTo(0));
                Assert.That(scratch.Column, Is.EqualTo(0));
                Assert.That(key5.Column, Is.EqualTo(4));
                Assert.That(getSampleFilename(scratch), Is.EqualTo("scratch.wav"));
                // Regression guard: the scratch keysound rides on KeysoundSample with an EMPTY Samples, so pressing the
                // key for the scratch's anchor column does not sound it via mania's per-key feedback (GameplaySampleTriggerSource).
                Assert.That(scratch.KeysoundSample?.Filename, Is.EqualTo("scratch.wav"));
                Assert.That(scratch.Samples, Is.Empty);
                Assert.That(convertedBeatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(2));
                Assert.That(convertedBeatmap.BeatmapInfo.EndTimeObjectCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestSevenKeyScratchHoldKeepsHeadSampleButSilencesTailWithoutCreatingJudgedObjects()
        {
            const string text = @"
#TITLE 7K Scratch Hold
#BPM 120
#WAVAA key1.wav
#WAVBB scratch.wav
#WAVCC key7.wav
#WAVDD scratch-head.wav
#WAVEE scratch-tail.wav
#00111:AA00
#00116:BB00
#00119:CC00
#LNTYPE 1
#00256:DD00EE00
";

            var convertedBeatmap = convertToMania(text, "sevenkey.bme");
            var key1 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "key1.wav");
            var key7 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "key7.wav");
            var scratchSamples = convertedBeatmap.HitObjects.OfType<BmsConvertedScratchSampleHitObject>().OrderBy(hitObject => hitObject.StartTime).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.Stages, Has.Count.EqualTo(1));
                Assert.That(convertedBeatmap.TotalColumns, Is.EqualTo(7));
                Assert.That(key1.Column, Is.EqualTo(0));
                Assert.That(key7.Column, Is.EqualTo(6));
                Assert.That(convertedBeatmap.HitObjects.OfType<HoldNote>(), Is.Empty);
                // Scratch LN head still sounds; the tail (scratch-tail.wav) is dropped to match "LN tail silent".
                Assert.That(scratchSamples.Select(getSampleFilename), Is.EqualTo(new[] { "scratch.wav", "scratch-head.wav" }));
                Assert.That(convertedBeatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void TestSimultaneousScratchAndKeyShareJudgementColumnWithoutInflatingCounts()
        {
            const string text = @"
#TITLE Scratch Chord
#BPM 120
#WAVAA key1.wav
#WAVBB scratch.wav
#00111:AA00
#00116:BB00
";

            var convertedBeatmap = convertToMania(text, "scratch-chord.bme");
            var key1 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "key1.wav");
            var scratch = convertedBeatmap.HitObjects.OfType<BmsConvertedScratchSampleHitObject>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(key1.StartTime, Is.EqualTo(scratch.StartTime).Within(0.001));
                Assert.That(key1.Column, Is.EqualTo(scratch.Column));
                Assert.That(convertedBeatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(1));
            });
        }

        [TestCase("ninekey.bms")]
        [TestCase("ninekey.pms")]
        public void TestNineKeySourceMapsOneToOne(string filename)
        {
            const string text = @"
#TITLE 9K Mapping
#BPM 120
#WAVAA lane1.wav
#WAVBB lane7.wav
#WAVCC lane9.wav
#00111:AA00
#00117:BB00
#00119:CC00
";

            var convertedBeatmap = convertToMania(text, filename);
            var lane1 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "lane1.wav");
            var lane7 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "lane7.wav");
            var lane9 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "lane9.wav");

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.Stages, Has.Count.EqualTo(1));
                Assert.That(convertedBeatmap.TotalColumns, Is.EqualTo(9));
                Assert.That(lane1.Column, Is.EqualTo(0));
                Assert.That(lane7.Column, Is.EqualTo(6));
                Assert.That(lane9.Column, Is.EqualTo(8));
            });
        }

        [Test]
        public void TestFourteenKeyUsesDualStageWithoutScratchJudgementColumns()
        {
            const string text = @"
#TITLE 14K Mapping
#BPM 120
#WAVAA left1.wav
#WAVBB leftScratch.wav
#WAVCC left7.wav
#WAVDD right1.wav
#WAVEE right7.wav
#WAVFF rightScratch.wav
#00111:AA00
#00116:BB00
#00119:CC00
#00121:DD00
#00129:EE00
#00126:FF00
";

            var convertedBeatmap = convertToMania(text, "fourteenkey.bme");
            var left1 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "left1.wav");
            var left7 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "left7.wav");
            var right1 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "right1.wav");
            var right7 = convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "right7.wav");
            var scratchSamples = convertedBeatmap.HitObjects.OfType<BmsConvertedScratchSampleHitObject>().OrderBy(hitObject => hitObject.Column).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.Stages, Has.Count.EqualTo(2));
                Assert.That(convertedBeatmap.Stages[0].Columns, Is.EqualTo(7));
                Assert.That(convertedBeatmap.Stages[1].Columns, Is.EqualTo(7));
                Assert.That(convertedBeatmap.TotalColumns, Is.EqualTo(14));
                Assert.That(left1.Column, Is.EqualTo(0));
                Assert.That(left7.Column, Is.EqualTo(6));
                Assert.That(right1.Column, Is.EqualTo(7));
                Assert.That(right7.Column, Is.EqualTo(13));
                Assert.That(scratchSamples.Select(hitObject => (hitObject.Column, getSampleFilename(hitObject))), Is.EqualTo(new[] { (0, "leftScratch.wav"), (13, "rightScratch.wav") }));
            });
        }

        [TestCase(BmsKeymode.Key5K, 5)]
        [TestCase(BmsKeymode.Key7K, 7)]
        [TestCase(BmsKeymode.Key9K_Bms, 9)]
        [TestCase(BmsKeymode.Key9K_Pms, 9)]
        [TestCase(BmsKeymode.Key14K, 14)]
        public void TestSongSelectKeyCountUsesStoredBmsKeymodeNotConvertHeuristic(BmsKeymode keymode, int expectedKeyCount)
        {
            // Regression: in song select a BMS chart shown under the mania ruleset resolves its "[NK]" badge and its
            // KC attribute bar through ManiaRuleset.GetKeyCount -> ManiaBeatmapConverter.GetColumnCount. The stored
            // BeatmapInfo keeps its bms source ruleset, so the osu!stable hold-ratio/OD convert heuristic used to run
            // and collapse every keymode to 6/7 (the "键数显示错误，错的五花八门" symptom). The fix trusts the
            // authoritative CircleSize for a bms source, exactly like a native mania beatmap. CircleSize for a stored
            // BMS chart is always the keymode column count (set by BmsBeatmapConverter).
            int storedKeyCount = BmsRuleset.GetKeyCount(keymode);
            Assert.That(storedKeyCount, Is.EqualTo(expectedKeyCount));

            var beatmapInfo = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty { CircleSize = storedKeyCount, OverallDifficulty = 8 }, new BeatmapMetadata())
            {
                // A mostly-tap chart (low long-note ratio): the old heuristic took its "< 0.2 special objects" branch
                // and returned 7 for every keymode here — wrong for 5K/9K/14K and only accidentally right for 7K.
                TotalObjectCount = 1000,
                EndTimeObjectCount = 0,
            };

            Assert.That(maniaRuleset.GetKeyCount(beatmapInfo), Is.EqualTo(expectedKeyCount));
        }

        [Test]
        public void TestConversionIgnoresMutatedSourceWrapperHitObjects()
        {
            const string text = @"
#TITLE Modless Gate
#BPM 120
#WAVAA key1.wav
#WAVBB scratch.wav
#00111:AA00
#00116:BB00
";

            var convertedBeatmap = convertToMania(text, "modless-gate.bme", sourceBeatmap =>
            {
                sourceBeatmap.HitObjects = new List<HitObject>
                {
                    new BmsHitObject
                    {
                        StartTime = 0,
                        LaneIndex = 7,
                        Keymode = BmsKeymode.Key7K,
                        IsScratch = false,
                        KeysoundSample = new BmsKeysoundSampleInfo("poison.wav"),
                    }
                };
            });

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.HitObjects.OfType<Note>().Any(note => getSampleFilename(note) == "poison.wav"), Is.False);
                Assert.That(convertedBeatmap.HitObjects.OfType<Note>().Single(note => getSampleFilename(note) == "key1.wav").Column, Is.EqualTo(0));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsConvertedScratchSampleHitObject>().Single(hitObject => getSampleFilename(hitObject) == "scratch.wav").Column, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestScratchSamplesDoNotAffectConvertedDifficultyOrStoredCounts()
        {
            // Playable notes (key1 col0 + key5 col4) alongside a DENSE scratch lane (channel 16). The scratch taps become
            // sample-only objects anchored to column 0; unless excluded from the difficulty input they pile onto column 0
            // and inflate the converted star (K12).
            const string text = @"
#TITLE Scratch Difficulty
#BPM 160
#WAVAA key1.wav
#WAVBB scratch.wav
#WAVCC key5.wav
#00111:AA00AA00AA00AA00
#00115:CC00CC00CC00CC00
#00116:BBBBBBBBBBBBBBBB
#00211:AA00AA00AA00AA00
#00215:CC00CC00CC00CC00
#00216:BBBBBBBBBBBBBBBB
";

            var convertedBeatmap = convertToMania(text, "scratch-stats.bme");
            var scorableBeatmap = createScorableBeatmap(convertedBeatmap);

            // Precondition: the converted beatmap really carries sample-only scratch objects the scorable projection drops.
            Assert.That(convertedBeatmap.HitObjects.OfType<BmsConvertedScratchSampleHitObject>().Any(), Is.True);
            Assert.That(convertedBeatmap.HitObjects.Count, Is.GreaterThan(scorableBeatmap.HitObjects.Count));

            double convertedStar = calculateConvertedManiaStarRating(convertedBeatmap);
            double scorableStar = calculateConvertedManiaStarRating(scorableBeatmap);

            Assert.Multiple(() =>
            {
                // Counts already excluded sample-only objects — but this only ever guarded the metadata fields, which the
                // difficulty calculator never reads.
                Assert.That(convertedBeatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(scorableBeatmap.HitObjects.Count));
                Assert.That(convertedBeatmap.BeatmapInfo.EndTimeObjectCount, Is.EqualTo(0));
                // The REAL guard (K12): ManiaDifficultyCalculator must yield the same star whether or not the sample-only
                // objects are present — i.e. it must not count them. Before the K12 filter these two diverged (the scratch
                // objects inflated column 0). A non-zero scorable star ensures we are not trivially comparing 0 == 0.
                Assert.That(scorableStar, Is.GreaterThan(0));
                Assert.That(convertedStar, Is.EqualTo(scorableStar).Within(1e-9));
            });
        }

        [Test]
        public void TestBgmSampleOnlyObjectsDoNotInflateConvertedStarRating()
        {
            // Playable notes (key1 col0 + key5 col4) alongside a DENSE autoplay BGM layer (channel 01). Every BGM event
            // becomes a sample-only object pinned to column 0; unless excluded from the difficulty input they saturate
            // column 0 and inflate the converted star for keysound-heavy charts (K12).
            const string text = @"
#TITLE BGM Difficulty
#BPM 160
#WAVAA key1.wav
#WAVCC key5.wav
#WAVZZ bgm.wav
#00111:AA00AA00AA00AA00
#00115:CC00CC00CC00CC00
#00101:ZZZZZZZZZZZZZZZZ
#00211:AA00AA00AA00AA00
#00215:CC00CC00CC00CC00
#00201:ZZZZZZZZZZZZZZZZ
";

            var convertedBeatmap = convertToMania(text, "bgm-difficulty.bms", keymodeOverride: BmsKeymode.Key5K);
            var scorableBeatmap = createScorableBeatmap(convertedBeatmap);

            Assert.That(convertedBeatmap.HitObjects.OfType<BmsConvertedBgmSampleHitObject>().Any(), Is.True);
            Assert.That(convertedBeatmap.HitObjects.Count, Is.GreaterThan(scorableBeatmap.HitObjects.Count));

            double convertedStar = calculateConvertedManiaStarRating(convertedBeatmap);
            double scorableStar = calculateConvertedManiaStarRating(scorableBeatmap);

            Assert.Multiple(() =>
            {
                Assert.That(scorableStar, Is.GreaterThan(0));
                Assert.That(convertedStar, Is.EqualTo(scorableStar).Within(1e-9));
            });
        }

        [Test]
        public void TestConvertedBeatmapSanitisesBmsOnlyControlPoints()
        {
            const string text = @"
#TITLE Timing Chart
#BPM 120
#WAVAA key1.wav
#BPMAA 240
#STOPAB 96
#SCROLLAC 0.5
#00102:0.5
#00108:00AA
#00109:000000AB
#001SC:00AC
#00112:0000AA00
#00212:AA00
";

            var convertedBeatmap = convertToMania(text, "timing-scroll.bme");
            var timingPoints = convertedBeatmap.ControlPointInfo.TimingPoints;
            var notes = convertedBeatmap.HitObjects.OfType<Note>().OrderBy(note => note.StartTime).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(notes, Has.Length.EqualTo(2));
                Assert.That(notes[0].StartTime, Is.EqualTo(2500).Within(0.001));
                Assert.That(notes[1].StartTime, Is.EqualTo(3250).Within(0.001));
                Assert.That(timingPoints, Has.Count.EqualTo(2));
                Assert.That(timingPoints.Any(point => Math.Abs(point.BeatLength - 6) <= 0.0001), Is.False);
                Assert.That(timingPoints[0].Time, Is.EqualTo(0).Within(0.001));
                Assert.That(timingPoints[0].BPM, Is.EqualTo(120).Within(0.001));
                Assert.That(timingPoints[1].Time, Is.EqualTo(2500).Within(0.001));
                Assert.That(timingPoints[1].BPM, Is.EqualTo(240).Within(0.001));
                Assert.That(convertedBeatmap.ControlPointInfo.EffectPoints, Is.Empty);
            });
        }

        [Test]
        public void TestScratchOnlyChartIsRejectedAsInvalidForRuleset()
        {
            const string text = @"
#TITLE Scratch Only
#BPM 120
#WAVAA scratch.wav
#00116:AA00
";

            var ex = Assert.Throws<BeatmapInvalidForRulesetException>(() => convertToMania(text, "scratch-only.bme"));
            Assert.That(ex!.Message, Does.Contain("no scorable mania hit objects"));
        }

        [Test]
        public void TestEmptyChartIsRejectedAsInvalidForRuleset()
        {
            const string text = @"
#TITLE Empty
#BPM 120
";

            Assert.Throws<BeatmapInvalidForRulesetException>(() => convertToMania(text, "empty.bme"));
        }

        [Test]
        public void TestExtremeBpmIsPreservedInManiaTimingPointsAndNotMistakenForStopFreezeSentinel()
        {
            // BMS allows extreme BPMs; mania TimingControlPoint.BeatLengthBindable clamps to MinValue = 6 so a
            // legitimate BPM of 10000 produces BeatLength = 6, which is also what a STOP-freeze marker carries.
            // The mania converter must distinguish the two by type (BmsStopFreezeTimingControlPoint), not by value,
            // so the timing point at 10000 BPM must survive into the converted mania ControlPointInfo.
            const string text = @"
#TITLE Extreme BPM
#BPM 120
#WAVAA k1.wav
#BPMAB 10000
#00111:AA00
#00208:AB
#00211:AA00
";

            var convertedBeatmap = convertToMania(text, "extreme-bpm.bme");
            var notes = convertedBeatmap.HitObjects.OfType<Note>().OrderBy(n => n.StartTime).ToArray();
            var timingPoints = convertedBeatmap.ControlPointInfo.TimingPoints.OrderBy(tp => tp.Time).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(notes, Has.Length.EqualTo(2));
                Assert.That(timingPoints, Has.Length.EqualTo(2));
                Assert.That(timingPoints[0].BPM, Is.EqualTo(120).Within(0.001));
                Assert.That(timingPoints[1].BPM, Is.EqualTo(10000).Within(0.001));
            });
        }

        [Test]
        public void TestStopFreezeIsStrippedFromManiaWhileExtremeBpmTimingSurvives()
        {
            // Combined regression: a chart that uses STOP AND an extreme BPM both produce BeatLength = 6 internally.
            // The dedicated subclass marker must remove only the STOP one.
            const string text = @"
#TITLE Stop Plus Extreme BPM
#BPM 120
#WAVAA k1.wav
#STOPAB 96
#BPMAC 10000
#00109:00AB
#00108:00AC
#00111:AA00
";

            var convertedBeatmap = convertToMania(text, "stop-plus-extreme.bme");
            var timingPoints = convertedBeatmap.ControlPointInfo.TimingPoints.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(timingPoints.Any(point => point is BmsStopFreezeTimingControlPoint), Is.False);
                Assert.That(timingPoints.Any(point => Math.Abs(point.BPM - 10000) <= 0.001), Is.True,
                    "extreme-BPM timing point should survive past the STOP-freeze strip even though both share BeatLength = 6 internally.");
            });
        }

        [Test]
        public void TestRepeatedConversionReusesCachedBmsPlayableSource()
        {
            const string text = @"
#TITLE Cached BMS source
#BPM 120
#WAVAA key1.wav
#00111:AA00
";

            var decodedChart = decoder.DecodeText(text, "cache-source.bme");
            var sourceBeatmap = new BmsDecodedBeatmap(decodedChart);

            Assert.That(sourceBeatmap.TryGetCachedModlessPlayableBeatmap(new BmsRuleset().RulesetInfo, out _), Is.False);

            _ = (ManiaBeatmap)maniaRuleset.CreateBeatmapConverter(sourceBeatmap).Convert();

            Assert.That(sourceBeatmap.TryGetCachedModlessPlayableBeatmap(new BmsRuleset().RulesetInfo, out var cachedBmsPlayable), Is.True);
            Assert.That(cachedBmsPlayable, Is.InstanceOf<BmsBeatmap>());

            _ = (ManiaBeatmap)maniaRuleset.CreateBeatmapConverter(sourceBeatmap).Convert();

            Assert.That(sourceBeatmap.TryGetCachedModlessPlayableBeatmap(new BmsRuleset().RulesetInfo, out var cachedAfterSecond), Is.True);
            Assert.That(cachedAfterSecond, Is.SameAs(cachedBmsPlayable),
                "second mania conversion should reuse the previously cached BMS playable rather than re-run BmsBeatmapConverter.");
        }

        [Test]
        public void TestAllowGameplayWithRulesetAllowsBmsToManiaConversion()
        {
            var beatmapInfo = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty(), new BeatmapMetadata());

            Assert.That(beatmapInfo.AllowGameplayWithRuleset(maniaRuleset.RulesetInfo, allowConversion: true), Is.True);
        }

        [Test]
        public void TestRequiresRulesetSwitchOnlyWhenBmsToManiaConversionIsHidden()
        {
            var beatmapInfo = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone(), new BeatmapDifficulty(), new BeatmapMetadata());

            Assert.Multiple(() =>
            {
                Assert.That(beatmapInfo.RequiresRulesetSwitch(maniaRuleset.RulesetInfo, allowConversion: true), Is.False);
                Assert.That(beatmapInfo.RequiresRulesetSwitch(maniaRuleset.RulesetInfo, allowConversion: false), Is.True);
            });
        }

        [Test]
        public void TestBgmEventsBecomeSampleOnlyObjectsWithoutAffectingScorableCounts()
        {
            const string text = @"
#TITLE BGM Layer
#BPM 120
#WAVAA key1.wav
#WAVBB bgm1.wav
#WAVCC bgm2.wav
#00111:AA00
#00101:BB00CC00
";

            var convertedBeatmap = convertToMania(text, "bgm.bms", keymodeOverride: BmsKeymode.Key5K);
            var notes = convertedBeatmap.HitObjects.OfType<Note>().ToArray();
            var bgmSamples = convertedBeatmap.HitObjects.OfType<BmsConvertedBgmSampleHitObject>().OrderBy(hitObject => hitObject.StartTime).ToArray();

            Assert.Multiple(() =>
            {
                // The playable note carries its keysound as before; the two autoplay BGM (channel 0x01) layers are no
                // longer dropped, but surface as sample-only objects that do not inflate the scorable counts.
                Assert.That(notes.Select(getSampleFilename), Is.EqualTo(new[] { "key1.wav" }));
                Assert.That(bgmSamples.Select(getSampleFilename), Is.EqualTo(new[] { "bgm1.wav", "bgm2.wav" }));
                // Slot + sample are carried so the shared store (J6) can play with per-WAV cut and pause/seek handling.
                Assert.That(bgmSamples.Select(hitObject => hitObject.KeysoundSample?.Filename), Is.EqualTo(new[] { "bgm1.wav", "bgm2.wav" }));
                Assert.That(bgmSamples.Select(hitObject => hitObject.KeysoundId.HasValue), Has.All.True);
                // Regression guard: Samples MUST stay empty. mania fires the next object's Samples as per-key sound
                // feedback (GameplaySampleTriggerSource on every key down); since BGM is pinned to column 0, a populated
                // Samples made pressing key1 audibly trigger bgm1 (overlapping, bypassing the store and pause). The
                // keysound rides on KeysoundSample (store path) instead.
                Assert.That(bgmSamples.Select(hitObject => hitObject.Samples.Count), Has.All.Zero);
                Assert.That(bgmSamples.Select(hitObject => hitObject.Column), Has.All.EqualTo(0));
                Assert.That(convertedBeatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(notes.Length));
                Assert.That(convertedBeatmap.BeatmapInfo.EndTimeObjectCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestConvertedHoldNoteKeepsHeadKeysoundButSilencesTailToMatchBmsContract()
        {
            const string text = @"
#TITLE LN Tail Silent
#BPM 120
#WAVAA head.wav
#WAVBB tail.wav
#WAVCC key7.wav
#LNTYPE 1
#00151:AA00BB00
#00117:CC00
";

            var convertedBeatmap = convertToMania(text, "ln-tail.bme");
            var hold = convertedBeatmap.HitObjects.OfType<HoldNote>().Single();

            Assert.Multiple(() =>
            {
                // Head still sounds (NodeSamples[0]); the tail node sample is emptied so mania does not replay the
                // head WAV on release, matching the BMS-side "LN tail silent" contract (P1-J #3a) and avoiding the
                // LNTYPE1 head/tail double.
                Assert.That(getSampleFilename(hold), Is.EqualTo("head.wav"));
                Assert.That(hold.NodeSamples, Has.Count.EqualTo(2));
                Assert.That(hold.NodeSamples[0].OfType<BmsKeysoundSampleInfo>().Single().Filename, Is.EqualTo("head.wav"));
                Assert.That(hold.NodeSamples[1], Is.Empty);
            });
        }

        private ManiaBeatmap convertToMania(
            string text,
            string filename,
            Action<BmsDecodedBeatmap>? mutateSource = null,
            BmsKeymode? keymodeOverride = null)
        {
            BmsBeatmapDecoderOptions? options = keymodeOverride.HasValue
                ? new BmsBeatmapDecoderOptions(keymodeOverride.Value)
                : null;
            var decodedChart = decoder.DecodeText(text, filename, options);
            var sourceBeatmap = new BmsDecodedBeatmap(decodedChart);

            mutateSource?.Invoke(sourceBeatmap);

            var convertedBeatmap = (ManiaBeatmap)maniaRuleset.CreateBeatmapConverter(sourceBeatmap).Convert();

            foreach (var hitObject in convertedBeatmap.HitObjects)
                hitObject.ApplyDefaults(convertedBeatmap.ControlPointInfo, convertedBeatmap.Difficulty);

            return convertedBeatmap;
        }

        private ManiaBeatmap createScorableBeatmap(ManiaBeatmap convertedBeatmap)
        {
            var scorableBeatmap = new ManiaBeatmap(new StageDefinition(convertedBeatmap.Stages[0].Columns));

            for (int i = 1; i < convertedBeatmap.Stages.Count; i++)
                scorableBeatmap.Stages.Add(new StageDefinition(convertedBeatmap.Stages[i].Columns));

            scorableBeatmap.BeatmapInfo = new BeatmapInfo(maniaRuleset.RulesetInfo.Clone(), convertedBeatmap.Difficulty.Clone(), convertedBeatmap.Metadata);
            scorableBeatmap.ControlPointInfo = convertedBeatmap.ControlPointInfo;
            scorableBeatmap.HitObjects = convertedBeatmap.HitObjects.Where(hitObject => hitObject is not (BmsConvertedScratchSampleHitObject or BmsConvertedBgmSampleHitObject)).ToList();

            return scorableBeatmap;
        }

        private double calculateConvertedManiaStarRating(ManiaBeatmap beatmap)
        {
            // Run the production star calculator over EXACTLY this beatmap. A pass-through working beatmap is required:
            // the normal WorkingBeatmap.GetPlayableBeatmap would re-run ManiaBeatmapConverter and regenerate plain Notes,
            // erasing the sample-only BGM/scratch objects these tests are about (and thus hiding the very bug under test).
            var workingBeatmap = new PreconvertedWorkingBeatmap(beatmap);
            return new ManiaDifficultyCalculator(maniaRuleset.RulesetInfo, workingBeatmap).Calculate().StarRating;
        }

        private sealed class PreconvertedWorkingBeatmap : FlatWorkingBeatmap
        {
            private readonly IBeatmap playable;

            public PreconvertedWorkingBeatmap(IBeatmap playable)
                : base(playable)
            {
                this.playable = playable;
            }

            // Return the already-converted beatmap untouched (no re-conversion / ApplyDefaults pass), so the difficulty
            // calculator sees the converter's sample-only objects exactly as produced.
            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token) => playable;
        }

        // The converted keysound lives on Samples for playable notes, but on KeysoundSample (with an intentionally EMPTY
        // Samples) for the autoplay sample-only objects (BGM / scratch). Those play through the shared store, and an
        // empty Samples keeps them out of mania's per-key sample feedback (GameplaySampleTriggerSource), which would
        // otherwise sound the BGM/scratch keysound on a key press in that column (the "press key1 -> bgm1" bug).
        private static string? getSampleFilename(HitObject hitObject)
            => hitObject switch
            {
                BmsConvertedScratchSampleHitObject scratch => scratch.KeysoundSample?.Filename,
                BmsConvertedBgmSampleHitObject bgm => bgm.KeysoundSample?.Filename,
                _ => hitObject.Samples.OfType<BmsKeysoundSampleInfo>().SingleOrDefault()?.Filename,
            };
    }
}
