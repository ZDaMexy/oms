// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsSkinDecoderTest
    {
        private const string sample_skin =
            "[General]\n" +
            "Keymodes: 7K, 14K\n" +
            "\n" +
            "[Bms]\n" +
            "Keymode: 7K\n" +
            "PlayfieldWidth: 0.7\n" +
            "ScratchLaneWidth: 1.5\n" +
            "LongNoteBodyWidth: 0.6\n" +
            "NoteColourWhite: 243,243,243\n" +
            "NoteColourScratch: 252,0,20\n" +
            "LaneDividerColour: 88,102,128,200\n" +
            "NoteImage1: notes/white\n" +
            "NoteImageSH: notes/scratch_head\n" +
            "HitTargetImage: stage/hit_target // optional trailing comment\n" +
            "UnknownFutureKey: should_be_ignored\n" +
            "\n" +
            "[Bms]\n" +
            "Keymode: 14K\n" +
            "PlayfieldWidth: 0.95\n";

        [Test]
        public void TestDeclaredKeymodesParsed()
        {
            var decoder = decode(sample_skin);
            Assert.That(decoder.DeclaredKeymodes, Is.EquivalentTo(new[] { BmsKeymode.Key7K, BmsKeymode.Key14K }));
        }

        [Test]
        public void TestPerKeymodeBucketing()
        {
            var decoder = decode(sample_skin);

            Assert.That(decoder.Configurations, Has.Count.EqualTo(2));
            Assert.That(config(decoder, BmsKeymode.Key7K).Geometry[BmsSkinConfigurationLookups.PlayfieldWidth], Is.EqualTo(0.7f));
            Assert.That(config(decoder, BmsKeymode.Key14K).Geometry[BmsSkinConfigurationLookups.PlayfieldWidth], Is.EqualTo(0.95f));
        }

        [Test]
        public void TestGeometryParsed()
        {
            var c = config(decode(sample_skin), BmsKeymode.Key7K);

            Assert.That(c.Geometry[BmsSkinConfigurationLookups.PlayfieldWidth], Is.EqualTo(0.7f));
            Assert.That(c.Geometry[BmsSkinConfigurationLookups.ScratchLaneWidth], Is.EqualTo(1.5f));
            Assert.That(c.Geometry[BmsSkinConfigurationLookups.LongNoteBodyWidth], Is.EqualTo(0.6f));
        }

        [Test]
        public void TestColoursParsed()
        {
            var c = config(decode(sample_skin), BmsKeymode.Key7K);

            Assert.That(c.Colours[BmsSkinConfigurationLookups.NoteColourWhite], Is.EqualTo(new Color4(243, 243, 243, 255)));
            Assert.That(c.Colours[BmsSkinConfigurationLookups.NoteColourScratch], Is.EqualTo(new Color4(252, 0, 20, 255)));
            Assert.That(c.Colours[BmsSkinConfigurationLookups.LaneDividerColour], Is.EqualTo(new Color4(88, 102, 128, 200)));
        }

        [Test]
        public void TestPerLaneAndGlobalImagesStored()
        {
            var c = config(decode(sample_skin), BmsKeymode.Key7K);

            Assert.That(c.ImageLookups["NoteImage1"], Is.EqualTo("notes/white"));
            Assert.That(c.ImageLookups["NoteImageSH"], Is.EqualTo("notes/scratch_head"));
            Assert.That(c.ImageLookups["HitTargetImage"], Is.EqualTo("stage/hit_target"));
        }

        [Test]
        public void TestUnknownKeyIgnoredNotThrown()
        {
            var c = config(decode(sample_skin), BmsKeymode.Key7K);

            Assert.That(c.ImageLookups.ContainsKey("UnknownFutureKey"), Is.False);
            // The 7K bucket only stored the recognised keys above (3 geometry, 3 colour, 3 image).
            Assert.That(c.Geometry, Has.Count.EqualTo(3));
            Assert.That(c.Colours, Has.Count.EqualTo(3));
            Assert.That(c.ImageLookups, Has.Count.EqualTo(3));
        }

        [Test]
        public void TestKeysBeforeKeymodeAreHeldThenFlushed()
        {
            // "PlayfieldWidth" appears before "Keymode:" — it must be held and applied once the bucket resolves.
            var decoder = decode(
                "[Bms]\n" +
                "PlayfieldWidth: 0.5\n" +
                "Keymode: 7K\n" +
                "PlayfieldHeight: 0.9\n");

            var c = config(decoder, BmsKeymode.Key7K);
            Assert.That(c.Geometry[BmsSkinConfigurationLookups.PlayfieldWidth], Is.EqualTo(0.5f));
            Assert.That(c.Geometry[BmsSkinConfigurationLookups.PlayfieldHeight], Is.EqualTo(0.9f));
        }

        [Test]
        public void TestMalformedColourIgnored()
        {
            var decoder = decode(
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteColourWhite: not,a,colour\n");

            var c = config(decoder, BmsKeymode.Key7K);
            Assert.That(c.Colours.ContainsKey(BmsSkinConfigurationLookups.NoteColourWhite), Is.False);
        }

        private static BmsSkinDecoder decode(string ini)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(ini);
            return decoder;
        }

        private static BmsSkinConfiguration config(BmsSkinDecoder decoder, BmsKeymode keymode)
            => decoder.Configurations.First(c => c.Keymode == keymode);
    }
}
