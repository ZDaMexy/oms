// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Text;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsBeatmapDecoderTest
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        [Test]
        public void TestParsesHeadersAndIndexedTables()
        {
            const string text = @"
#TITLE Example Song
#SUBTITLE Extra Stage
#SUBARTIST obj: Test Charter
#ARTIST Test Artist
#COMMENT notes: dense ending
#GENRE Hardcore
#BPM 150
#PLAYLEVEL 12
#DIFFICULTY 4
#RANK 1
#TOTAL 256
#STAGEFILE stage.png
#BANNER banner.png
#BACKBMP back.png
#WAVAA keysound.wav
#BMP0A frame.png
#BPMAA 180
#STOP0B 96
#LNOBJ ZZ
#LNTYPE 1
#00111:AA00
";

            var result = decoder.DecodeText(text, "example.bme");

            Assert.Multiple(() =>
            {
                Assert.That(result.BeatmapInfo.Title, Is.EqualTo("Example Song"));
                Assert.That(result.BeatmapInfo.Subtitle, Is.EqualTo("Extra Stage"));
                Assert.That(result.BeatmapInfo.SubArtist, Is.EqualTo("obj: Test Charter"));
                Assert.That(result.BeatmapInfo.Artist, Is.EqualTo("Test Artist"));
                Assert.That(result.BeatmapInfo.Comment, Is.EqualTo("notes: dense ending"));
                Assert.That(result.BeatmapInfo.Genre, Is.EqualTo("Hardcore"));
                Assert.That(result.BeatmapInfo.InitialBpm, Is.EqualTo(150).Within(0.001));
                Assert.That(result.BeatmapInfo.PlayLevel, Is.EqualTo("12"));
                Assert.That(result.BeatmapInfo.HeaderDifficulty, Is.EqualTo(4));
                Assert.That(result.BeatmapInfo.Rank, Is.EqualTo(1));
                Assert.That(result.BeatmapInfo.Total, Is.EqualTo(256).Within(0.001));
                Assert.That(result.BeatmapInfo.StageFile, Is.EqualTo("stage.png"));
                Assert.That(result.BeatmapInfo.BannerFile, Is.EqualTo("banner.png"));
                Assert.That(result.BeatmapInfo.BackgroundFile, Is.EqualTo("back.png"));
                Assert.That(result.BeatmapInfo.KeysoundTable[370], Is.EqualTo("keysound.wav"));
                Assert.That(result.BeatmapInfo.BitmapTable[10], Is.EqualTo("frame.png"));
                Assert.That(result.BeatmapInfo.ExtendedBpmTable[370], Is.EqualTo(180).Within(0.001));
                Assert.That(result.BeatmapInfo.StopTable[11], Is.EqualTo(96).Within(0.001));
                Assert.That(result.BeatmapInfo.LongNoteObjectId, Is.EqualTo(1295));
                Assert.That(result.BeatmapInfo.LongNoteType, Is.EqualTo(1));
                Assert.That(result.BeatmapInfo.Keymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.ObjectEvents, Has.Count.EqualTo(1));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestParsesMeasureLengthAndChannelSlices()
        {
            const string text = @"
#00202:0.75
#00211:AA00BB00
";

            var result = decoder.DecodeText(text, "measure.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.BeatmapInfo.MeasureLengthControlPoints, Has.Count.EqualTo(1));
                Assert.That(result.BeatmapInfo.MeasureLengthControlPoints[0].MeasureIndex, Is.EqualTo(2));
                Assert.That(result.BeatmapInfo.MeasureLengthControlPoints[0].Multiplier, Is.EqualTo(0.75).Within(0.001));
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(2));
                Assert.That(result.ChannelEvents[0].RawValue, Is.EqualTo("AA"));
                Assert.That(result.ChannelEvents[0].FractionWithinMeasure, Is.EqualTo(0).Within(0.001));
                Assert.That(result.ChannelEvents[1].RawValue, Is.EqualTo("BB"));
                Assert.That(result.ChannelEvents[1].FractionWithinMeasure, Is.EqualTo(0.5).Within(0.001));
            });
        }

        [Test]
        public void TestDecodesShiftJisText()
        {
            const string title = "\u30c6\u30b9\u30c8";
            byte[] data = Encoding.GetEncoding("shift_jis").GetBytes($"#TITLE {title}\n#00111:AA00\n");

            var result = decoder.Decode(data, "shiftjis.bms");

            Assert.That(result.BeatmapInfo.Title, Is.EqualTo(title));
        }

        [Test]
        public void TestPairsLnObjLongNotes()
        {
            const string text = @"
#LNOBJ ZZ
#00111:AA00ZZ00
";

            var result = decoder.DecodeText(text, "lnobj.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.LongNoteEvents, Has.Count.EqualTo(1));
                Assert.That(result.LongNoteEvents[0].LaneChannel, Is.EqualTo(0x11));
                Assert.That(result.LongNoteEvents[0].HeadObjectId, Is.EqualTo(370));
                Assert.That(result.LongNoteEvents[0].TailObjectId, Is.EqualTo(1295));
                Assert.That(result.LongNoteEvents[0].Encoding, Is.EqualTo(BmsLongNoteEncoding.LnObj));
                Assert.That(result.LongNoteEvents[0].StartFractionWithinMeasure, Is.EqualTo(0).Within(0.001));
                Assert.That(result.LongNoteEvents[0].EndFractionWithinMeasure, Is.EqualTo(0.5).Within(0.001));
            });
        }

        [Test]
        public void TestPairsLnType1LongNotes()
        {
            const string text = @"
#LNTYPE 1
#00151:AA00BB00
";

            var result = decoder.DecodeText(text, "lntype1.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.LongNoteEvents, Has.Count.EqualTo(1));
                Assert.That(result.LongNoteEvents[0].LaneChannel, Is.EqualTo(0x11));
                Assert.That(result.LongNoteEvents[0].HeadObjectId, Is.EqualTo(370));
                Assert.That(result.LongNoteEvents[0].TailObjectId, Is.EqualTo(407));
                Assert.That(result.LongNoteEvents[0].Encoding, Is.EqualTo(BmsLongNoteEncoding.LnType1));
            });
        }

        [Test]
        public void TestParsesBpmAndStopEvents()
        {
            const string text = @"
#BPMAA 180
#STOPAB 96
#00103:9600
#00108:AA00
#00109:AB00
";

            var result = decoder.DecodeText(text, "timing.bme");

            Assert.Multiple(() =>
            {
                Assert.That(result.BpmChangeEvents, Has.Count.EqualTo(2));
                Assert.That(result.BpmChangeEvents[0].Channel, Is.EqualTo(0x03));
                Assert.That(result.BpmChangeEvents[0].Bpm, Is.EqualTo(150).Within(0.001));
                Assert.That(result.BpmChangeEvents[0].SourceValue, Is.EqualTo(150));
                Assert.That(result.BpmChangeEvents[1].Channel, Is.EqualTo(0x08));
                Assert.That(result.BpmChangeEvents[1].Bpm, Is.EqualTo(180).Within(0.001));
                Assert.That(result.BpmChangeEvents[1].SourceValue, Is.EqualTo(370));
                Assert.That(result.StopEvents, Has.Count.EqualTo(1));
                Assert.That(result.StopEvents[0].StopIndex, Is.EqualTo(371));
                Assert.That(result.StopEvents[0].StopValue, Is.EqualTo(96).Within(0.001));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestExecutesIf1BranchInsideRandomBlock()
        {
            const string text = @"
#TITLE Base Title
#RANDOM 2
#IF 2
#TITLE Ignored Title
#00112:BB00
#ENDIF
#IF 1
#TITLE Chosen Title
#00111:AA00
#ENDIF
#00113:CC00
";

            var result = decoder.DecodeText(text, "random-branch.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.BeatmapInfo.Title, Is.EqualTo("Chosen Title"));
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(2));
                Assert.That(result.ChannelEvents[0].Channel, Is.EqualTo(0x11));
                Assert.That(result.ChannelEvents[1].Channel, Is.EqualTo(0x13));
                Assert.That(result.Warnings, Has.Count.EqualTo(1));
                Assert.That(result.Warnings[0], Does.Contain("OMS only executes the #IF 1 branch"));
            });
        }

        [Test]
        public void TestSkipsRandomBlockWhenIf1BranchMissing()
        {
            const string text = @"
#TITLE Base Title
#RANDOM 2
#IF 2
#TITLE Ignored Title
#00111:AA00
#ENDIF
#00112:BB00
";

            var result = decoder.DecodeText(text, "random-skip.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.BeatmapInfo.Title, Is.EqualTo("Base Title"));
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.ChannelEvents[0].Channel, Is.EqualTo(0x12));
                Assert.That(result.Warnings, Has.Count.EqualTo(2));
                Assert.That(result.Warnings[0], Does.Contain("OMS only executes the #IF 1 branch"));
                Assert.That(result.Warnings[1], Does.Contain("no #IF 1 branch was found"));
            });
        }

        [TestCase("#00112:AA00\n#00116:BB00", "chart.bms", BmsKeymode.Key5K)]
        [TestCase("#00111:AA00\n#00112:BB00\n#00113:CC00\n#00114:DD00\n#00115:EE00\n#00116:FF00\n#00118:HH00\n", "chart.bme", BmsKeymode.Key7K)]
        [TestCase("#00119:II00\n", "chart.bme", BmsKeymode.Key7K)]
        [TestCase("#00111:AA00\n#00112:BB00\n#00113:CC00\n#00114:DD00\n#00115:EE00\n#00116:FF00\n#00117:GG00\n#00118:HH00\n#00119:II00\n", "chart.bms", BmsKeymode.Key9K_Bms)]
        [TestCase("#00122:AA00\n", "chart.bme", BmsKeymode.Key14K)]
        [TestCase("#00111:AA00\n", "chart.pms", BmsKeymode.Key9K_Pms)]
        public void TestDetectsKeymode(string text, string fileName, BmsKeymode expected)
        {
            var result = decoder.DecodeText(text, fileName);

            Assert.That(result.BeatmapInfo.Keymode, Is.EqualTo(expected));
        }
    }
}
