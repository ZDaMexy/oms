// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsAutomaticKeysoundModTest
    {
        [TestCase("NR")]
        [TestCase("HO")]
        [TestCase("IN")]
        public void TestConversionModsRetainTheCompleteSourceMusic(string acronym)
        {
            var beatmap = convert(@"
#TITLE Conversion mod music
#BPM 120
#WAVAA backing.wav
#WAVBB scratch.wav
#WAVCC head.wav
#WAVDD tail.wav
#WAVEE key.wav
#00101:AA
#00116:BB
#00251:CCDD
#00311:EE
#00411:EE
");
            var originalMusic = drain(new BmsAutomaticKeysoundPlayback(beatmap));
            applyMod(acronym, beatmap);
            var judgedObjects = beatmap.HitObjects.ToArray();

            Assert.That(BmsToManiaKeysoundStoreFactory.ShouldHost(beatmap), Is.True);
            Assert.That(drain(new BmsAutomaticKeysoundPlayback(beatmap)), Is.EqualTo(originalMusic));
            Assert.That(originalMusic.Select(e => e.Sample.LookupNames.First()),
                Is.EquivalentTo(new[] { "backing.wav", "scratch.wav", "head.wav", "key.wav", "key.wav" }));
            Assert.That(beatmap.HitObjects, Is.EqualTo(judgedObjects), "audio cannot undo the selected mod's judged objects");
        }

        [TestCase("NR")]
        [TestCase("HO")]
        [TestCase("IN")]
        public void TestHoldOnlyChartKeepsAutomaticAudioAndIndependentCloneCursors(string acronym)
        {
            var beatmap = convert(@"
#TITLE Hold-only mod music
#BPM 120
#WAVAA head.wav
#WAVBB tail.wav
#00151:AABB
#00351:AABB
");
            var clone = (BmsConvertedManiaBeatmap)beatmap.Clone();
            applyMod(acronym, clone);

            Assert.That(BmsToManiaKeysoundStoreFactory.ShouldHost(clone), Is.True);
            var original = new BmsAutomaticKeysoundPlayback(beatmap);
            var modified = new BmsAutomaticKeysoundPlayback(clone);
            var expected = drain(original);
            Assert.That(expected, Has.Count.EqualTo(2));
            Assert.That(drain(modified), Is.EqualTo(expected), "consuming one clone cannot consume another's music");
            Assert.That(drain(original), Is.Empty);
            Assert.That(beatmap.AutomaticKeysounds, Is.SameAs(clone.AutomaticKeysounds), "immutable snapshots are safe to share");
        }

        private static void applyMod(string acronym, IBeatmap beatmap)
        {
            IApplicableAfterBeatmapConversion mod = acronym switch
            {
                "NR" => new ManiaModNoRelease(),
                "HO" => new ManiaModHoldOff(),
                _ => new ManiaModInvert(),
            };
            mod.ApplyToBeatmap(beatmap);
        }

        private static BmsConvertedManiaBeatmap convert(string text)
        {
            var source = new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(text, "automatic-mods.bme"));
            return (BmsConvertedManiaBeatmap)new BmsToManiaBeatmapConverter(source, new ManiaRuleset()).Convert();
        }

        private static List<BmsAutomaticKeysoundPlayback.Event> drain(BmsAutomaticKeysoundPlayback playback)
        {
            var events = new List<BmsAutomaticKeysoundPlayback.Event>();
            while (playback.TryDequeue(100000, out var sound))
                events.Add(sound);

            return events;
        }
    }
}
