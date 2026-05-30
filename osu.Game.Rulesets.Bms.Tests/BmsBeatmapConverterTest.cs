// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI.Scrolling;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsBeatmapConverterTest
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        [Test]
        public void TestConvertsMetadataAndHitObjectTypes()
        {
            const string text = @"
#TITLE Example Song
#SUBTITLE Extra Stage
#SUBARTIST obj: Test Charter
#ARTIST Test Artist
#COMMENT notes: dense ending
#GENRE Hardcore
#BPM 120
#PLAYLEVEL 12
#DIFFICULTY 4
#RANK 1
#BACKBMP background.png
#WAVAA bgm.wav
#WAVBB notes/key.wav
#WAVCC hold/head.ogg
#WAVDD hold/tail.wav
#00101:AA00
#00111:BB00
#LNTYPE 1
#00151:CC00DD00
";

            var decodedChart = decoder.DecodeText(text, "example.bme");
            var sourceBeatmap = new BmsDecodedBeatmap(decodedChart);
            var converter = new BmsBeatmapConverter(sourceBeatmap, new BmsRuleset());
            var convertedBeatmap = (BmsBeatmap)converter.Convert();

            Assert.Multiple(() =>
            {
                Assert.That(converter.CanConvert(), Is.True);
                Assert.That(convertedBeatmap.BmsInfo.Title, Is.EqualTo("Example Song"));
                Assert.That(convertedBeatmap.BmsInfo.Subtitle, Is.EqualTo("Extra Stage"));
                Assert.That(convertedBeatmap.BmsInfo.SubArtist, Is.EqualTo("obj: Test Charter"));
                Assert.That(convertedBeatmap.BmsInfo.Comment, Is.EqualTo("notes: dense ending"));
                Assert.That(convertedBeatmap.Metadata.Title, Is.EqualTo("Example Song"));
                Assert.That(convertedBeatmap.Metadata.Artist, Is.EqualTo("Test Artist"));
                Assert.That(convertedBeatmap.Metadata.Author.Username, Is.EqualTo("Test Charter"));
                Assert.That(convertedBeatmap.Metadata.Tags, Is.Empty);
                Assert.That(convertedBeatmap.Metadata.BackgroundFile, Is.EqualTo("background.png"));
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata(), Is.Not.Null);
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata()!.Subtitle, Is.EqualTo("Extra Stage"));
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata()!.SubArtist, Is.EqualTo("obj: Test Charter"));
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata()!.Genre, Is.EqualTo("Hardcore"));
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata()!.PlayLevel, Is.EqualTo("12"));
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata()!.HeaderDifficulty, Is.EqualTo(4));
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata()!.JudgeRank, Is.EqualTo(1));
                Assert.That(convertedBeatmap.BeatmapInfo.DifficultyName, Is.EqualTo("Another 12"));
                Assert.That(convertedBeatmap.BeatmapInfo.Ruleset.ShortName, Is.EqualTo("bms"));
                Assert.That(convertedBeatmap.Difficulty.SliderMultiplier, Is.EqualTo(1).Within(0.001));
                Assert.That(convertedBeatmap.BeatmapInfo.Difficulty.SliderMultiplier, Is.EqualTo(1).Within(0.001));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsBgmEvent>().Count(), Is.EqualTo(1));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHitObject>().Count(hitObject => hitObject is not BmsHoldNote), Is.EqualTo(1));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Count(), Is.EqualTo(1));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote).LaneIndex, Is.EqualTo(1));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().LaneIndex, Is.EqualTo(1));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsBgmEvent>().Single().KeysoundSample?.Filename, Is.EqualTo("bgm.wav"));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote).KeysoundSample?.Filename, Is.EqualTo("notes/key.wav"));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().HeadKeysoundId, Is.EqualTo(444));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().TailKeysoundId, Is.EqualTo(481));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().HeadKeysoundSample?.Filename, Is.EqualTo("hold/head.ogg"));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().TailKeysoundSample?.Filename, Is.EqualTo("hold/tail.wav"));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().Head, Is.TypeOf<BmsHoldNoteHead>());
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().Head?.StartTime, Is.EqualTo(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().StartTime).Within(0.001));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().Head?.KeysoundSample?.Filename, Is.EqualTo("hold/head.ogg"));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().Tail, Is.TypeOf<BmsHoldNoteTailEvent>());
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().Tail?.StartTime, Is.EqualTo(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().EndTime).Within(0.001));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single().Tail?.KeysoundSample?.Filename, Is.EqualTo("hold/tail.wav"));
            });
        }

        [Test]
        public void TestExtractsChartCreatorFromArtistSuffix()
        {
            const string text = @"
#TITLE started
#ARTIST Ym1024 feat. lamie* /obj:BAECON
#GENRE J-Airy Pop
#BPM 140
#00111:AA00
#WAVAA hit.wav
";

            var decodedChart = decoder.DecodeText(text, "creator-in-artist.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.Metadata.Artist, Is.EqualTo("Ym1024 feat. lamie*"));
                Assert.That(convertedBeatmap.Metadata.Author.Username, Is.EqualTo("BAECON"));
                Assert.That(convertedBeatmap.Metadata.Tags, Is.Empty);
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata(), Is.Not.Null);
                Assert.That(convertedBeatmap.Metadata.GetChartMetadata()!.Genre, Is.EqualTo("J-Airy Pop"));
            });
        }

        [Test]
        public void TestConvertsLnType2LongNotes()
        {
            const string text = @"
#BPM 120
#WAVAA hold/head.wav
#WAVZZ hold/tail.wav
#LNTYPE 2
#00151:AAZZ
#00251:ZZ00
";

            var decodedChart = decoder.DecodeText(text, "lntype2.bms");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var holdNote = convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHoldNote>().Count(), Is.EqualTo(1));
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsHitObject>().Count(hitObject => hitObject is not BmsHoldNote), Is.EqualTo(0));
                Assert.That(holdNote.LaneIndex, Is.EqualTo(1));
                Assert.That(holdNote.HeadKeysoundId, Is.EqualTo(370));
                Assert.That(holdNote.TailKeysoundId, Is.EqualTo(1295));
                Assert.That(holdNote.HeadKeysoundSample?.Filename, Is.EqualTo("hold/head.wav"));
                Assert.That(holdNote.TailKeysoundSample?.Filename, Is.EqualTo("hold/tail.wav"));
                Assert.That(holdNote.EndTime, Is.GreaterThan(holdNote.StartTime));
            });
        }

        [Test]
        public void TestMetadataBackgroundPrefersStageFileOverBackbmp()
        {
            const string text = @"
#TITLE Example Song
#ARTIST Test Artist
#STAGEFILE stage.png
#BACKBMP fallback.png
#00111:AA00
";

            var decodedChart = decoder.DecodeText(text, "example.bms");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            Assert.That(convertedBeatmap.Metadata.BackgroundFile, Is.EqualTo("stage.png"));
        }

        [Test]
        public void TestMetadataBackgroundFallsBackToProjectedBgaBitmap()
        {
            const string text = @"
#TITLE Example Song
#ARTIST Test Artist
#BMP01 projected.png
#BGA01 01 0 0 255 255 0 0
#00111:AA00
";

            var decodedChart = decoder.DecodeText(text, "projected-background.bms");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            Assert.That(convertedBeatmap.Metadata.BackgroundFile, Is.EqualTo("projected.png"));
        }

        [Test]
        public void TestMapsStandardBmeScratchAndUpperKeysToSevenKeyLayout()
        {
            const string text = @"
#TITLE Layout Mapping
#BPM 120
#WAVAA key1.wav
#WAVBB scratch.wav
#WAVCC key7.wav
#00111:AA00
#00116:BB00
#00119:CC00
";

            var decodedChart = decoder.DecodeText(text, "layout.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            var key1 = convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.KeysoundSample?.Filename == "key1.wav");
            var scratch = convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.KeysoundSample?.Filename == "scratch.wav");
            var key7 = convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.KeysoundSample?.Filename == "key7.wav");

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.BmsInfo.Keymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(key1.LaneIndex, Is.EqualTo(1));
                Assert.That(key1.IsScratch, Is.False);
                Assert.That(scratch.LaneIndex, Is.EqualTo(0));
                Assert.That(scratch.IsScratch, Is.True);
                Assert.That(key7.LaneIndex, Is.EqualTo(7));
                Assert.That(key7.IsScratch, Is.False);
            });
        }

        [Test]
        public void TestKeysoundSampleInfoNormalisesAndRejectsTraversal()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BmsKeysoundSampleInfo.TryCreate("sounds\\bgm.wav", out var sampleInfo), Is.True);
                Assert.That(sampleInfo?.Filename, Is.EqualTo("sounds/bgm.wav"));
                Assert.That(sampleInfo?.LookupNames.ToArray(), Is.EqualTo(new[] { "sounds/bgm.wav", "sounds/bgm" }));
                Assert.That(BmsKeysoundSampleInfo.TryCreate("../outside.wav", out _), Is.False);
            });
        }

        [Test]
        public void TestComputesTimingsForMeasureLengthBpmAndStop()
        {
            const string text = @"
#TITLE Timing Chart
#BPM 120
#BPMAA 240
#STOPAB 96
#00102:0.5
#00108:00AA
#00109:000000AB
#00112:0000AA00
#00212:AA00
";

            var decodedChart = decoder.DecodeText(text, "timing.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var firstNote = convertedBeatmap.HitObjects.OfType<BmsHitObject>().First();
            var secondNote = convertedBeatmap.HitObjects.OfType<BmsHitObject>().Last();
            var timingPoints = convertedBeatmap.ControlPointInfo.TimingPoints;

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.BmsInfo.MeasureLengthControlPoints, Has.Count.EqualTo(1));
                Assert.That(convertedBeatmap.BmsInfo.MeasureLengthControlPoints[0].Multiplier, Is.EqualTo(0.5).Within(0.001));
                Assert.That(firstNote.StartTime, Is.EqualTo(2500).Within(0.001));
                Assert.That(secondNote.StartTime, Is.EqualTo(3250).Within(0.001));
                Assert.That(timingPoints, Has.Count.EqualTo(4));
                Assert.That(timingPoints[0].Time, Is.EqualTo(0).Within(0.001));
                Assert.That(timingPoints[0].BPM, Is.EqualTo(120).Within(0.001));
                Assert.That(timingPoints.Any(point => point.Time == 2500 && point.BPM == 240), Is.True);
                Assert.That(timingPoints.Any(point => point.Time == 2625 && point.BeatLength == 6), Is.True);
                Assert.That(timingPoints.Any(point => point.Time == 3125 && point.BPM == 240), Is.True);
            });
        }

        [Test]
        public void TestScrollEventsBuildEffectPointsForRuntimeConsumer()
        {
            const string text = @"
#TITLE Scroll Effect
#BPM 120
#SCROLLAA 0.5
#SCROLLAB 2.0
#001SC:AAAB
#00111:CC00
#00211:DD00
";

            var decodedChart = decoder.DecodeText(text, "scroll-effect.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var effectPoints = convertedBeatmap.ControlPointInfo.EffectPoints;

            Assert.Multiple(() =>
            {
                Assert.That(effectPoints, Has.Count.EqualTo(2));
                Assert.That(effectPoints[0].Time, Is.EqualTo(2000).Within(0.001));
                Assert.That(effectPoints[0].ScrollSpeed, Is.EqualTo(0.5).Within(0.001));
                Assert.That(effectPoints[1].Time, Is.EqualTo(3000).Within(0.001));
                Assert.That(effectPoints[1].ScrollSpeed, Is.EqualTo(2.0).Within(0.001));
                Assert.That(convertedBeatmap.ControlPointInfo.EffectPointAt(2000).ScrollSpeed, Is.EqualTo(0.5).Within(0.001));
                Assert.That(convertedBeatmap.ControlPointInfo.EffectPointAt(2999).ScrollSpeed, Is.EqualTo(0.5).Within(0.001));
                Assert.That(convertedBeatmap.ControlPointInfo.EffectPointAt(3000).ScrollSpeed, Is.EqualTo(2.0).Within(0.001));
            });
        }

        [Test]
        public void TestSamePositionBpmAndStopApplyBeforeObjectTime()
        {
            const string text = @"
#TITLE Control Event Order
#BPM 120
#BPMAA 240
#STOPAB 96
#00108:AA00
#00109:AB00
#00111:CC00
";

            var decodedChart = decoder.DecodeText(text, "same-position-control-order.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var note = convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single();
            var timingPoints = convertedBeatmap.ControlPointInfo.TimingPoints;

            Assert.Multiple(() =>
            {
                Assert.That(note.StartTime, Is.EqualTo(2500).Within(0.001));
                Assert.That(timingPoints.Any(point => point.Time == 2000 && point.BeatLength == 6), Is.True);
                Assert.That(timingPoints.Any(point => point.Time == 2500 && point.BPM == 240), Is.True);
            });
        }

        [Test]
        public void TestSignedBpmUsesMagnitudeForTimelineProgression()
        {
            const string text = @"
#TITLE Signed BPM Timing
#BPM 120
#BPMAA -180
#00108:AA00
#00211:BB00
";

            var decodedChart = decoder.DecodeText(text, "signed-bpm-timing.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var note = convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single();
            var timingPoints = convertedBeatmap.ControlPointInfo.TimingPoints;

            Assert.Multiple(() =>
            {
                Assert.That(note.StartTime, Is.EqualTo(3333.3333333333).Within(0.001));
                Assert.That(timingPoints.Any(point => point.Time == 2000 && point.BPM == 180), Is.True);
            });
        }

        [Test]
        public void TestBuildsMinePlacementsWithoutLeakingIntoJudgedObjects()
        {
            const string text = @"
#TITLE Mine Chart
#BPM 120
#WAVAA a.wav
#00111:AA00
#001D1:00AA0000
";

            var decodedChart = decoder.DecodeText(text, "mines.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            Assert.Multiple(() =>
            {
                // Mine channel D1 mirrors visible channel 0x11 => lane 1 (7K); mine sits at measure 1, fraction 1/4.
                Assert.That(convertedBeatmap.Mines, Has.Count.EqualTo(1));
                Assert.That(convertedBeatmap.Mines[0].LaneIndex, Is.EqualTo(1));
                Assert.That(convertedBeatmap.Mines[0].StartTime, Is.EqualTo(2500).Within(0.001));

                // Mines must NOT leak into the judged hit-object / statistics path.
                Assert.That(convertedBeatmap.HitObjects.OfType<BmsMine>(), Is.Empty);
                Assert.That(convertedBeatmap.BeatmapInfo.TotalObjectCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestBuildsMineOnRightmostKeyLane()
        {
            // Regression: the rightmost 7K key (channel 0x19 => lane index 7, since scratch takes lane 0) had its mines
            // dropped because buildMines bounded by key count (7) instead of lane count (8). A note on 0x19 forces 7K.
            const string text = @"
#TITLE Rightmost Mine
#BPM 120
#WAVAA a.wav
#00119:AA00
#001D9:00AA0000
";

            var decodedChart = decoder.DecodeText(text, "rightmost-mine.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            Assert.Multiple(() =>
            {
                // Mine channel D9 mirrors visible channel 0x19 => rightmost key lane 7; it must NOT be dropped.
                Assert.That(convertedBeatmap.Mines, Has.Count.EqualTo(1));
                Assert.That(convertedBeatmap.Mines[0].LaneIndex, Is.EqualTo(7));
            });
        }

        [Test]
        public void TestBuildsLaneKeysoundTimelineIncludingInvisibleObjects()
        {
            const string text = @"
#TITLE Lane Keysounds
#BPM 120
#WAVAA inv.wav
#WAVBB note.wav
#00131:AA00
#00211:BB00
";

            var decodedChart = decoder.DecodeText(text, "lane-keysounds.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            // Channel 0x31 (invisible) mirrors visible channel 0x11 => lane 1 in the 7K layout; the lane keysound
            // timeline must carry the invisible-object keysound (armed earlier) ahead of the later visible note.
            var timeline = convertedBeatmap.GetLaneKeysoundTimeline(1);

            Assert.Multiple(() =>
            {
                Assert.That(timeline, Has.Count.EqualTo(2));
                Assert.That(timeline[0].Time, Is.EqualTo(2000).Within(0.001));
                Assert.That(timeline[0].Sample.Filename, Is.EqualTo("inv.wav"));
                Assert.That(timeline[1].Time, Is.EqualTo(4000).Within(0.001));
                Assert.That(timeline[1].Sample.Filename, Is.EqualTo("note.wav"));
            });
        }

        [Test]
        public void TestSubUnitBpmUsesMagnitudeWithoutClampingToOne()
        {
            const string text = @"
#TITLE Sub-unit BPM
#BPM 120
#BPMAA 0.5
#00108:AA00
#00211:BB00
";

            var decodedChart = decoder.DecodeText(text, "sub-unit-bpm.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var note = convertedBeatmap.HitObjects.OfType<BmsHitObject>().Single();

            // Measure 0 at 120 BPM (beat length 500) spans 2000ms; the 0.5 BPM change at the start of measure 1 must
            // use beat length 120000 (NOT clamped to 1 BPM => 60000), so measure 1 spans 480000ms => note at 482000ms.
            Assert.That(note.StartTime, Is.EqualTo(482000).Within(0.001));
        }

        [Test]
        public void TestCapturesMeasureStartTimes()
        {
            const string text = @"
#TITLE Measure Starts
#BPM 120
#00102:0.5
#00111:AA00
#00211:BB00
";

            var decodedChart = decoder.DecodeText(text, "measure-starts.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.MeasureStartTimes.Count, Is.EqualTo(3));
                Assert.That(convertedBeatmap.MeasureStartTimes[0], Is.EqualTo(0).Within(0.001));
                Assert.That(convertedBeatmap.MeasureStartTimes[1], Is.EqualTo(2000).Within(0.001));
                Assert.That(convertedBeatmap.MeasureStartTimes[2], Is.EqualTo(3000).Within(0.001));
            });
        }

        [Test]
        public void TestAudioLessBeatmapLengthUsesAbsoluteEndTime()
        {
            const string text = @"
#TITLE Virtual Track Length
#BPM 120
#00111:AA00
#00211:BB00
";

            var decodedChart = decoder.DecodeText(text, "virtual-length.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var playableObjects = convertedBeatmap.HitObjects.OfType<BmsHitObject>().OrderBy(hitObject => hitObject.StartTime).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(convertedBeatmap.Metadata.AudioFile, Is.Empty);
                Assert.That(playableObjects[0].StartTime, Is.EqualTo(2000).Within(0.001));
                Assert.That(playableObjects[1].StartTime, Is.EqualTo(4000).Within(0.001));
                Assert.That(convertedBeatmap.GetLastObjectTime(), Is.EqualTo(4000).Within(0.001));
                Assert.That(convertedBeatmap.BeatmapInfo.Length, Is.EqualTo(4000).Within(0.001));
            });
        }

        [Test]
        public void TestScrollProfileFreezesDistanceDuringStop()
        {
            // Same timing chart as TestComputesTimingsForMeasureLengthBpmAndStop: a STOP freezes scroll across
            // [2625, 3125]ms. The stop-motion scroll profile (P1-L Phase 2) must hold distance constant there while
            // still advancing on normal sections. Note times / control points are unchanged (verified elsewhere).
            const string text = @"
#TITLE Timing Chart
#BPM 120
#BPMAA 240
#STOPAB 96
#00102:0.5
#00108:00AA
#00109:000000AB
#00112:0000AA00
#00212:AA00
";

            var decodedChart = decoder.DecodeText(text, "stop-profile.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var profile = convertedBeatmap.ScrollProfile;

            Assert.That(profile, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(profile!.PositionDelta(2625, 3125), Is.EqualTo(0).Within(1e-6)); // frozen during STOP
                Assert.That(profile.PositionDelta(0, 2000), Is.GreaterThan(0)); // normal section advances
                Assert.That(profile.PositionDelta(3125, 3250), Is.GreaterThan(0)); // resumes after the freeze
            });
        }

        [Test]
        public void TestScrollProfileDegeneratesToConstantForSingleBpmChart()
        {
            // For a single-BPM chart (base BPM = chart BPM, scroll 1) distance must track time: D(t) ~= t, so the
            // bypass would render identically to constant scroll. This is the regression-friendly degenerate property.
            const string text = @"
#TITLE Constant
#BPM 120
#00111:AA00
#00211:BB00
#00311:CC00
";

            var decodedChart = decoder.DecodeText(text, "constant-profile.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var profile = convertedBeatmap.ScrollProfile;

            Assert.That(profile, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(profile!.DistanceAt(0), Is.EqualTo(0).Within(0.001));
                Assert.That(profile.DistanceAt(1000), Is.EqualTo(1000).Within(0.001));
                Assert.That(profile.DistanceAt(2000), Is.EqualTo(2000).Within(0.001));
                Assert.That(profile.DistanceAt(4000), Is.EqualTo(4000).Within(0.001));
                Assert.That(profile.IsStopMotionGimmick, Is.False); // normal chart: Auto must not engage
            });
        }

        [Test]
        public void TestScrollProfileSnapsAcrossExtremeBpm()
        {
            // An extreme BPM (1,320,000 — DEAD SOUL's value) covers a normal measure's distance in near-zero time:
            // the snap. The base stays at 120 (it dominates playing time), so the 120 region keeps slope ~1.
            const string text = @"
#TITLE Snap
#BPM 120
#BPMAA 1320000
#00208:AA00
#00311:CC00
";

            var decodedChart = decoder.DecodeText(text, "snap-profile.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var profile = convertedBeatmap.ScrollProfile;

            Assert.That(profile, Is.Not.Null);

            double maxSlope = 0;

            for (int i = 1; i < profile!.KnotTimes.Count; i++)
            {
                double dt = profile.KnotTimes[i] - profile.KnotTimes[i - 1];

                if (dt > 0)
                    maxSlope = Math.Max(maxSlope, (profile.KnotDistances[i] - profile.KnotDistances[i - 1]) / dt);
            }

            Assert.Multiple(() =>
            {
                Assert.That(profile.DistanceAt(1000), Is.EqualTo(1000).Within(0.001)); // 120-BPM base region: slope ~1
                Assert.That(maxSlope, Is.GreaterThan(1000)); // 1,320,000 / 120 ~= 11000x => snap
                Assert.That(profile.IsStopMotionGimmick, Is.True); // auto-detected (Step D)
            });
        }

        [Test]
        public void TestStopMotionAlgorithmFreezesOnConvertedStopRegion()
        {
            // End-to-end: feed the REAL converter-built profile into the stop-motion algorithm and confirm an object
            // holds its on-screen position while the play head sits anywhere inside the converted STOP [2625, 3125]ms,
            // and moves outside it. Same chart as TestScrollProfileFreezesDistanceDuringStop.
            const string text = @"
#TITLE Timing Chart
#BPM 120
#BPMAA 240
#STOPAB 96
#00102:0.5
#00108:00AA
#00109:000000AB
#00112:0000AA00
#00212:AA00
";

            var decodedChart = decoder.DecodeText(text, "stop-pipeline.bme");
            var convertedBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
            var algorithm = new BmsStopMotionScrollAlgorithm(convertedBeatmap.ScrollProfile!);

            const double time_range = 1000;
            const float scroll_length = 500;

            float frozenAtStopStart = algorithm.PositionAt(3250, 2625, time_range, scroll_length);
            float frozenMidStop = algorithm.PositionAt(3250, 3125, time_range, scroll_length);
            float beforeStop = algorithm.PositionAt(3250, 0, time_range, scroll_length);

            Assert.Multiple(() =>
            {
                Assert.That(frozenMidStop, Is.EqualTo(frozenAtStopStart).Within(1e-6)); // frozen across the STOP
                Assert.That(beforeStop, Is.GreaterThan(frozenAtStopStart)); // farther away before the freeze
            });
        }
    }
}
