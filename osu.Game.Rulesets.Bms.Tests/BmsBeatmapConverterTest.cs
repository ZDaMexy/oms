// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;

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
    }
}
