// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
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
        public void TestParsesPreviewHeader()
        {
            const string text = @"
#TITLE Preview Test
#BPM 120
#PREVIEW preview.ogg
#00111:AA00
#WAVAA hit.wav
";

            var result = decoder.DecodeText(text, "preview.bms");

            Assert.That(result.BeatmapInfo.PreviewFile, Is.EqualTo("preview.ogg"));
        }

        [Test]
        public void TestPreviewHeaderDefaultsToNull()
        {
            const string text = @"
#TITLE No Preview
#BPM 120
#00111:AA00
#WAVAA hit.wav
";

            var result = decoder.DecodeText(text, "no-preview.bms");

            Assert.That(result.BeatmapInfo.PreviewFile, Is.Null);
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
        public void TestRetainsRawChannelCarrierMetadata()
        {
            const string text = @"
#00111:AA00
#00111:BB00
#00113:CC00DD00
";

            var result = decoder.DecodeText(text, "raw-carrier.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.RawChannelEvents, Has.Count.EqualTo(4));
                Assert.That(result.ChannelEvents, Is.SameAs(result.RawChannelEvents));
                Assert.That(result.RawChannelEvents[0].RawChannelToken, Is.EqualTo("11"));
                Assert.That(result.RawChannelEvents[0].SourceLineOrder, Is.EqualTo(0));
                Assert.That(result.RawChannelEvents[1].SourceLineOrder, Is.EqualTo(1));
                Assert.That(result.RawChannelEvents[2].RawChannelToken, Is.EqualTo("13"));
                Assert.That(result.RawChannelEvents[2].SourceLineOrder, Is.EqualTo(2));
                Assert.That(result.RawChannelEvents[3].SourceLineOrder, Is.EqualTo(2));
            });
        }

        [Test]
        public void TestParsesScrollDefinitionsAndKeepsUnknownHeaderBag()
        {
            const string text = @"
#SCROLLAA 1.5
#FOO BAR
#XYZAA 42
#001SC:AA00
";

            var result = decoder.DecodeText(text, "scroll-placeholder.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.BeatmapInfo.ScrollTable[370], Is.EqualTo(1.5).Within(0.001));
                Assert.That(result.BeatmapInfo.UnknownHeaders["FOO"], Is.EqualTo("BAR"));
                Assert.That(result.BeatmapInfo.UnknownHeaders["XYZAA"], Is.EqualTo("42"));
                Assert.That(result.RawChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.RawChannelEvents[0].RawChannelToken, Is.EqualTo("SC"));
                Assert.That(result.RawChannelEvents[0].Channel, Is.EqualTo(-1));
                Assert.That(result.RawChannelEvents[0].RawValue, Is.EqualTo("AA"));
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.BpmChangeEvents, Is.Empty);
                Assert.That(result.StopEvents, Is.Empty);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestParsesScrollEventsIntoTypedSurface()
        {
            const string text = @"
#SCROLLAA 0.5
#SCROLLAB 2.0
#001SC:AAAB
";

            var result = decoder.DecodeText(text, "scroll-typed-surface.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.ScrollEvents, Has.Count.EqualTo(2));
                Assert.That(result.ScrollEvents[0].ScrollIndex, Is.EqualTo(370));
                Assert.That(result.ScrollEvents[0].ScrollValue, Is.EqualTo(0.5).Within(0.001));
                Assert.That(result.ScrollEvents[0].FractionWithinMeasure, Is.EqualTo(0).Within(0.001));
                Assert.That(result.ScrollEvents[1].ScrollIndex, Is.EqualTo(371));
                Assert.That(result.ScrollEvents[1].ScrollValue, Is.EqualTo(2.0).Within(0.001));
                Assert.That(result.ScrollEvents[1].FractionWithinMeasure, Is.EqualTo(0.5).Within(0.001));
                Assert.That(result.RawChannelEvents, Has.Count.EqualTo(2));
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.BpmChangeEvents, Is.Empty);
                Assert.That(result.StopEvents, Is.Empty);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestScrollChannelDoesNotCollideWithOtherUnknownChannels()
        {
            const string text = @"
#SCROLLAA 1.5
#001SC:AA00
#001ZZ:BB00
";

            var result = decoder.DecodeText(text, "scroll-unknown-collision.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.RawChannelEvents, Has.Count.EqualTo(2));
                Assert.That(result.ScrollEvents, Has.Count.EqualTo(1));
                Assert.That(result.ScrollEvents[0].ScrollIndex, Is.EqualTo(370));
                Assert.That(result.ScrollEvents[0].ScrollValue, Is.EqualTo(1.5).Within(0.001));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestParsesBgaEventsIntoTypedSurface()
        {
            const string text = @"
#BMP01 base.png
#BMP02 poor.png
#BMP03 layer.png
#BMP04 layer2.png
#00104:0100
#00106:0200
#00107:0003
#0010A:0004
";

            var result = decoder.DecodeText(text, "bga-typed-surface.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.BgaEvents, Has.Count.EqualTo(4));
                Assert.That(result.BgaEvents[0].Layer, Is.EqualTo(BmsBgaLayer.Base));
                Assert.That(result.BgaEvents[0].BitmapId, Is.EqualTo(1));
                Assert.That(result.BgaEvents[1].Layer, Is.EqualTo(BmsBgaLayer.Poor));
                Assert.That(result.BgaEvents[1].BitmapId, Is.EqualTo(2));
                Assert.That(result.BgaEvents[2].Layer, Is.EqualTo(BmsBgaLayer.Layer));
                Assert.That(result.BgaEvents[2].BitmapId, Is.EqualTo(3));
                Assert.That(result.BgaEvents[2].FractionWithinMeasure, Is.EqualTo(0.5).Within(0.001));
                Assert.That(result.BgaEvents[3].Layer, Is.EqualTo(BmsBgaLayer.Layer2));
                Assert.That(result.BgaEvents[3].BitmapId, Is.EqualTo(4));
                Assert.That(result.BgaEvents[3].FractionWithinMeasure, Is.EqualTo(0.5).Within(0.001));
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestParsesRicherBgaDefinitionHeadersIntoTypedSurface()
        {
            const string text = @"
#BGA01 01 0 0 255 255 16 32
#BGA01 02 1 2 253 254 17 33
#@BGA02 03 10 20 30 40 50 60
#ARGB01 0,0,0,0
#ARGB01 255,128,64,32
#SWBGA01 100:400:16:1:255,255,255,128 01020304
#POORBGA 0
#POORBGA 2
";

            var result = decoder.DecodeText(text, "bga-definition-typed-surface.bme");

            Assert.Multiple(() =>
            {
                Assert.That(result.BeatmapInfo.BgaDefinitions, Has.Count.EqualTo(1));
                Assert.That(result.BeatmapInfo.BgaDefinitions[1].BitmapReference, Is.EqualTo("02"));
                Assert.That(result.BeatmapInfo.BgaDefinitions[1].SourceX1, Is.EqualTo(1));
                Assert.That(result.BeatmapInfo.BgaDefinitions[1].SourceY1, Is.EqualTo(2));
                Assert.That(result.BeatmapInfo.BgaDefinitions[1].SourceX2, Is.EqualTo(253));
                Assert.That(result.BeatmapInfo.BgaDefinitions[1].SourceY2, Is.EqualTo(254));
                Assert.That(result.BeatmapInfo.BgaDefinitions[1].DestinationX, Is.EqualTo(17));
                Assert.That(result.BeatmapInfo.BgaDefinitions[1].DestinationY, Is.EqualTo(33));

                Assert.That(result.BeatmapInfo.AtBgaDefinitions, Has.Count.EqualTo(1));
                Assert.That(result.BeatmapInfo.AtBgaDefinitions[2].BitmapReference, Is.EqualTo("03"));
                Assert.That(result.BeatmapInfo.AtBgaDefinitions[2].SourceX, Is.EqualTo(10));
                Assert.That(result.BeatmapInfo.AtBgaDefinitions[2].SourceY, Is.EqualTo(20));
                Assert.That(result.BeatmapInfo.AtBgaDefinitions[2].Width, Is.EqualTo(30));
                Assert.That(result.BeatmapInfo.AtBgaDefinitions[2].Height, Is.EqualTo(40));
                Assert.That(result.BeatmapInfo.AtBgaDefinitions[2].DestinationX, Is.EqualTo(50));
                Assert.That(result.BeatmapInfo.AtBgaDefinitions[2].DestinationY, Is.EqualTo(60));

                Assert.That(result.BeatmapInfo.ArgbDefinitions, Has.Count.EqualTo(1));
                Assert.That(result.BeatmapInfo.ArgbDefinitions[1].Alpha, Is.EqualTo(255));
                Assert.That(result.BeatmapInfo.ArgbDefinitions[1].Red, Is.EqualTo(128));
                Assert.That(result.BeatmapInfo.ArgbDefinitions[1].Green, Is.EqualTo(64));
                Assert.That(result.BeatmapInfo.ArgbDefinitions[1].Blue, Is.EqualTo(32));

                Assert.That(result.BeatmapInfo.SwBgaDefinitions, Has.Count.EqualTo(1));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].FrameDurationMilliseconds, Is.EqualTo(100));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].TotalDurationMilliseconds, Is.EqualTo(400));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].LineChannel, Is.EqualTo(0x16));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].Loop, Is.True);
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].Alpha, Is.EqualTo(255));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].Red, Is.EqualTo(255));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].Green, Is.EqualTo(255));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].Blue, Is.EqualTo(128));
                Assert.That(result.BeatmapInfo.SwBgaDefinitions[1].Pattern, Is.EqualTo("01020304"));

                Assert.That(result.BeatmapInfo.PoorBgaMode, Is.EqualTo(BmsPoorBgaMode.Undisplayed));
                Assert.That(result.BeatmapInfo.UnknownHeaders.ContainsKey("BGA01"), Is.False);
                Assert.That(result.BeatmapInfo.UnknownHeaders.ContainsKey("@BGA02"), Is.False);
                Assert.That(result.BeatmapInfo.UnknownHeaders.ContainsKey("ARGB01"), Is.False);
                Assert.That(result.BeatmapInfo.UnknownHeaders.ContainsKey("SWBGA01"), Is.False);
                Assert.That(result.BeatmapInfo.UnknownHeaders.ContainsKey("POORBGA"), Is.False);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestProjectsRicherBgaDefinitionsIntoCombinedSurface()
        {
            const string text = @"
#BGA01 02 1 2 253 254 17 33
#@BGA02 03 10 20 30 40 50 60
#ARGB01 255,128,64,32
#SWBGA01 100:400:16:1:255,255,255,128 01020304
#POORBGA 2
";

            var result = decoder.DecodeText(text, "bga-definition-projection-surface.bme");
            var projections = new List<BmsVisualDefinitionProjection>(result.BeatmapInfo.GetVisualDefinitionProjections());

            Assert.Multiple(() =>
            {
                Assert.That(projections, Has.Count.EqualTo(2));
                Assert.That(projections[0].Index, Is.EqualTo(1));
                Assert.That(projections[0].BgaDefinition, Is.EqualTo(new BmsBgaDefinition(1, "02", 1, 2, 253, 254, 17, 33)));
                Assert.That(projections[0].AtBgaDefinition, Is.Null);
                Assert.That(projections[0].ArgbDefinition, Is.EqualTo(new BmsArgbDefinition(1, 255, 128, 64, 32)));
                Assert.That(projections[0].SwBgaDefinition, Is.EqualTo(new BmsSwBgaDefinition(1, 100, 400, 0x16, true, 255, 255, 255, 128, "01020304")));
                Assert.That(projections[0].PoorBgaMode, Is.EqualTo(BmsPoorBgaMode.Undisplayed));

                Assert.That(projections[1].Index, Is.EqualTo(2));
                Assert.That(projections[1].BgaDefinition, Is.Null);
                Assert.That(projections[1].AtBgaDefinition, Is.EqualTo(new BmsAtBgaDefinition(2, "03", 10, 20, 30, 40, 50, 60)));
                Assert.That(projections[1].ArgbDefinition, Is.Null);
                Assert.That(projections[1].SwBgaDefinition, Is.Null);
                Assert.That(projections[1].PoorBgaMode, Is.EqualTo(BmsPoorBgaMode.Undisplayed));

                Assert.That(result.BeatmapInfo.TryGetVisualDefinitionProjection(1, out BmsVisualDefinitionProjection firstProjection), Is.True);
                Assert.That(firstProjection, Is.EqualTo(projections[0]));

                Assert.That(result.BeatmapInfo.TryGetVisualDefinitionProjection(3, out _), Is.False);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestParsesInvisibleObjectEventsIntoTypedSurface()
        {
            const string text = @"
#00131:AA00
#00141:BB00
";

            var result = decoder.DecodeText(text, "invisible-typed-surface.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.InvisibleObjectEvents, Has.Count.EqualTo(2));
                Assert.That(result.InvisibleObjectEvents[0].Channel, Is.EqualTo(0x31));
                Assert.That(result.InvisibleObjectEvents[0].ObjectId, Is.EqualTo(370));
                Assert.That(result.InvisibleObjectEvents[1].Channel, Is.EqualTo(0x41));
                Assert.That(result.InvisibleObjectEvents[1].ObjectId, Is.EqualTo(407));
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestParsesMineEventsIntoTypedSurface()
        {
            const string text = @"
#001D1:0A00
#001E9:ZZ00
";

            var result = decoder.DecodeText(text, "mine-typed-surface.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.MineEvents, Has.Count.EqualTo(2));
                Assert.That(result.MineEvents[0].Channel, Is.EqualTo(0xD1));
                Assert.That(result.MineEvents[0].DamageValue, Is.EqualTo(10));
                Assert.That(result.MineEvents[1].Channel, Is.EqualTo(0xE9));
                Assert.That(result.MineEvents[1].DamageValue, Is.EqualTo(1295));
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestDecodesShiftJisText()
        {
            const string title = "\u30c6\u30b9\u30c8";
            byte[] data = Encoding.GetEncoding("shift_jis").GetBytes($"#TITLE {title}\n#BPM 120\n#WAVAA kick.wav\n#00111:AA00\n");

            var result = decoder.Decode(data, "shiftjis.bms");

            Assert.That(result.BeatmapInfo.Title, Is.EqualTo(title));
        }

        [Test]
        public void TestDecodesUtf8TextBeforeHeuristicDetection()
        {
            const string title = "\u6d4b\u8bd5\u66f2"; // 测试曲
            byte[] data = Encoding.UTF8.GetBytes($"#TITLE {title}\n#BPM 120\n#WAVAA kick.wav\n#00111:AA00\n");

            var result = decoder.Decode(data, "utf8.bms");

            Assert.That(result.BeatmapInfo.Title, Is.EqualTo(title));
        }

        [Test]
        public void TestDecodesUtf8WithBom()
        {
            const string title = "\u6d4b\u8bd5\u66f2"; // 测试曲
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] body = Encoding.UTF8.GetBytes($"#TITLE {title}\n#BPM 120\n#00111:AA00\n");
            byte[] data = new byte[bom.Length + body.Length];
            bom.CopyTo(data, 0);
            body.CopyTo(data, bom.Length);

            var result = decoder.Decode(data, "bom.bms");

            Assert.That(result.BeatmapInfo.Title, Is.EqualTo(title));
        }

        [Test]
        public void TestDecodesEucKrText()
        {
            const string title = "\ud14c\uc2a4\ud2b8"; // 테스트
            byte[] data = Encoding.GetEncoding("euc-kr").GetBytes($"#TITLE {title}\n#ARTIST {title}\n#BPM 120\n#WAVAA kick.wav\n#00111:AA00\n");

            var result = decoder.Decode(data, "euckr.bms");

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
        public void TestPairsLnType2LongNotesAcrossMeasures()
        {
            const string text = @"
#LNTYPE 2
#00151:AAZZ
#00251:ZZ00
";

            var result = decoder.DecodeText(text, "lntype2.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.LongNoteEvents, Has.Count.EqualTo(1));
                Assert.That(result.LongNoteEvents[0].LaneChannel, Is.EqualTo(0x11));
                Assert.That(result.LongNoteEvents[0].HeadObjectId, Is.EqualTo(370));
                Assert.That(result.LongNoteEvents[0].TailObjectId, Is.EqualTo(1295));
                Assert.That(result.LongNoteEvents[0].Encoding, Is.EqualTo(BmsLongNoteEncoding.LnType2));
                Assert.That(result.LongNoteEvents[0].StartMeasureIndex, Is.EqualTo(1));
                Assert.That(result.LongNoteEvents[0].StartFractionWithinMeasure, Is.EqualTo(0).Within(0.001));
                Assert.That(result.LongNoteEvents[0].EndMeasureIndex, Is.EqualTo(2));
                Assert.That(result.LongNoteEvents[0].EndFractionWithinMeasure, Is.EqualTo(0.5).Within(0.001));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestLnType2DuplicateZeroDoesNotOverwriteExistingSegment()
        {
            const string text = @"
#LNTYPE 2
#00151:AA00
#00151:0000
";

            var result = decoder.DecodeText(text, "lntype2-duplicate-zero.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.ObjectEvents, Is.Empty);
                Assert.That(result.LongNoteEvents, Has.Count.EqualTo(1));
                Assert.That(result.LongNoteEvents[0].LaneChannel, Is.EqualTo(0x11));
                Assert.That(result.LongNoteEvents[0].HeadObjectId, Is.EqualTo(370));
                Assert.That(result.LongNoteEvents[0].TailObjectId, Is.EqualTo(370));
                Assert.That(result.LongNoteEvents[0].Encoding, Is.EqualTo(BmsLongNoteEncoding.LnType2));
                Assert.That(result.LongNoteEvents[0].StartFractionWithinMeasure, Is.EqualTo(0).Within(0.001));
                Assert.That(result.LongNoteEvents[0].EndFractionWithinMeasure, Is.EqualTo(0.5).Within(0.001));
                Assert.That(result.Warnings, Is.Empty);
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
        public void TestPreservesSignedExtendedBpm()
        {
            const string text = @"
#BPMAA -180
#00108:AA00
";

            var result = decoder.DecodeText(text, "signed-bpm.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.BpmChangeEvents, Has.Count.EqualTo(1));
                Assert.That(result.BpmChangeEvents[0].Channel, Is.EqualTo(0x08));
                Assert.That(result.BpmChangeEvents[0].SourceValue, Is.EqualTo(370));
                Assert.That(result.BpmChangeEvents[0].Bpm, Is.EqualTo(-180).Within(0.001));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestCompoundsDuplicateChannelLinesBySourceOrder()
        {
            const string text = @"
#00111:AA00
#00111:BB00
";

            var result = decoder.DecodeText(text, "duplicate-channel-lines.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.RawChannelEvents, Has.Count.EqualTo(2));
                Assert.That(result.ObjectEvents, Has.Count.EqualTo(1));
                Assert.That(result.ObjectEvents[0].Channel, Is.EqualTo(0x11));
                Assert.That(result.ObjectEvents[0].ObjectId, Is.EqualTo(407));
                Assert.That(result.ObjectEvents[0].FractionWithinMeasure, Is.EqualTo(0).Within(0.001));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestBgmChannelKeepsSimultaneousLayers()
        {
            const string text = @"
#00101:AA00
#00101:BB00
";

            var result = decoder.DecodeText(text, "bgm-layers.bms");
            var bgmEvents = result.ObjectEvents.Where(objectEvent => objectEvent.AutoPlay).ToList();

            Assert.Multiple(() =>
            {
                // BGM (channel 01) layers placed at the same position must NOT be compounded — both keysounds survive.
                Assert.That(bgmEvents, Has.Count.EqualTo(2));
                Assert.That(bgmEvents.Select(objectEvent => objectEvent.ObjectId), Is.EquivalentTo(new[] { 370, 407 }));
                Assert.That(bgmEvents.All(objectEvent => objectEvent.Channel == 0x01), Is.True);
                Assert.That(bgmEvents.All(objectEvent => objectEvent.FractionWithinMeasure == 0), Is.True);
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestPairsMultipleLnObjLongNotesAcrossLanesWithBgm()
        {
            const string text = @"
#LNOBJ ZZ
#00101:CC00
#00111:AA00ZZ00
#00112:BB00ZZ00
";

            var result = decoder.DecodeText(text, "multi-lnobj.bms");

            Assert.Multiple(() =>
            {
                // Both LN heads were consumed and removed; only the retained BGM autoplay object remains.
                Assert.That(result.ObjectEvents, Has.Count.EqualTo(1));
                Assert.That(result.ObjectEvents[0].AutoPlay, Is.True);
                Assert.That(result.ObjectEvents[0].Channel, Is.EqualTo(0x01));
                Assert.That(result.LongNoteEvents, Has.Count.EqualTo(2));
                Assert.That(result.LongNoteEvents.Select(longNote => longNote.LaneChannel), Is.EquivalentTo(new[] { 0x11, 0x12 }));
                Assert.That(result.LongNoteEvents.All(longNote => longNote.Encoding == BmsLongNoteEncoding.LnObj), Is.True);
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

        [Test]
        public void TestRandomElseBranchExecutesWhenIfNotMatched()
        {
            const string text = @"
#RANDOM 2
#IF 2
#00111:AA00
#ELSE
#00112:BB00
#ENDIF
";

            var result = decoder.DecodeText(text, "random-else.bms");

            Assert.Multiple(() =>
            {
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.ChannelEvents[0].Channel, Is.EqualTo(0x12));
                Assert.That(result.Warnings, Has.Count.EqualTo(1));
                Assert.That(result.Warnings[0], Does.Contain("OMS only executes the #IF 1 branch"));
            });
        }

        [Test]
        public void TestRandomElseBranchSkippedWhenIfMatched()
        {
            const string text = @"
#RANDOM 2
#IF 1
#00111:AA00
#ELSE
#00112:BB00
#ENDIF
";

            var result = decoder.DecodeText(text, "random-else-skipped.bms");

            Assert.Multiple(() =>
            {
                // The #ELSE body must NOT leak in when the #IF 1 branch already matched.
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.ChannelEvents[0].Channel, Is.EqualTo(0x11));
            });
        }

        [Test]
        public void TestSetRandomHonoursPinnedBranchValue()
        {
            const string text = @"
#SETRANDOM 2
#IF 1
#00111:AA00
#ENDIF
#IF 2
#00112:BB00
#ENDIF
";

            var result = decoder.DecodeText(text, "setrandom.bms");

            Assert.Multiple(() =>
            {
                // #SETRANDOM pins the branch value, so the #IF 2 branch is selected deterministically with no warning.
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.ChannelEvents[0].Channel, Is.EqualTo(0x12));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [Test]
        public void TestSwitchExecutesMatchingCaseAndDoesNotMergeOtherCases()
        {
            const string text = @"
#SWITCH 2
#CASE 1
#00111:AA00
#SKIP
#CASE 2
#00112:BB00
#SKIP
#ENDSW
";

            var result = decoder.DecodeText(text, "switch-case.bms");

            Assert.Multiple(() =>
            {
                // #SWITCH defaults to value 1, so only #CASE 1 executes; #CASE 2 must NOT be merged in.
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.ChannelEvents[0].Channel, Is.EqualTo(0x11));
                Assert.That(result.Warnings, Has.Count.EqualTo(1));
                Assert.That(result.Warnings[0], Does.Contain("Switch branching is not fully supported"));
            });
        }

        [Test]
        public void TestSetSwitchFallsBackToDefaultCase()
        {
            const string text = @"
#SETSWITCH 9
#CASE 1
#00111:AA00
#SKIP
#DEF
#00112:BB00
#SKIP
#ENDSW
";

            var result = decoder.DecodeText(text, "setswitch-default.bms");

            Assert.Multiple(() =>
            {
                // No #CASE 9 exists, so the #DEF branch runs; #SETSWITCH is honoured without a warning.
                Assert.That(result.ChannelEvents, Has.Count.EqualTo(1));
                Assert.That(result.ChannelEvents[0].Channel, Is.EqualTo(0x12));
                Assert.That(result.Warnings, Is.Empty);
            });
        }

        [TestCase("#00112:AA00\n#00116:BB00", "chart.bms", BmsKeymode.Key5K)]
        [TestCase("#00111:AA00\n#00112:BB00\n#00113:CC00\n#00114:DD00\n#00115:EE00\n#00116:FF00\n#00118:HH00\n", "chart.bme", BmsKeymode.Key7K)]
        [TestCase("#00119:II00\n", "chart.bme", BmsKeymode.Key7K)]
        [TestCase("#00117:GG00\n", "chart.bms", BmsKeymode.Key9K_Bms)]
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
