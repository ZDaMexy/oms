// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsAutomaticKeysoundPlaybackTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestCompleteMusicWithoutPlayingTailsOrArmedInvisibleSounds(bool mania)
        {
            var beatmap = convert(@"
#TITLE Automatic music
#BPM 120
#WAVAA shared.wav
#WAVBB shared.wav
#WAVCC head.wav
#WAVDD tail.wav
#WAVEE invisible.wav
#00101:AA
#00111:BB
#00216:AA
#00351:CCDD
#00431:EE
", mania);
            var originalObjects = beatmap.HitObjects.ToArray();
            var playback = new BmsAutomaticKeysoundPlayback(beatmap);

            Assert.That(drain(playback, 1999), Is.Empty);
            var chord = drain(playback, 2000);
            Assert.That(chord.Select(e => e.Sample.LookupNames.First()), Is.EqualTo(new[] { "shared.wav", "shared.wav" }));
            Assert.That(chord.Select(e => e.CutGroup).Distinct().Count(), Is.EqualTo(2), "same file does not collapse distinct WAV slots");
            Assert.That(drain(playback, 2000), Is.Empty, "a second update at the same time cannot repeat a chord");

            var rest = drain(playback, 10000);
            Assert.That(rest.Select(e => e.Sample.LookupNames.First()), Is.EqualTo(new[] { "shared.wav", "head.wav" }));
            Assert.That(rest[0].CutGroup, Is.EqualTo(chord[0].CutGroup), "scratch and BGM share the original slot");
            Assert.That(beatmap.HitObjects, Is.EqualTo(originalObjects), "audio projection leaves scoring objects intact");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSeekSkipsPastMusicAndRearmsFutureEvents(bool mania)
        {
            var playback = new BmsAutomaticKeysoundPlayback(convert(@"
#TITLE Seek music
#BPM 120
#WAVAA key.wav
#00111:AA
#00211:AA
#00311:AA
", mania));

            playback.Seek(4001);
            Assert.That(drain(playback, 4001), Is.Empty, "forward seek must not burst skipped music");
            Assert.That(drain(playback, 6000), Has.Count.EqualTo(1));
            playback.Seek(4000);
            Assert.That(drain(playback, 4000), Has.Count.EqualTo(1), "event at the seek destination sounds once");
            Assert.That(drain(playback, 4000), Is.Empty);
            playback.Seek(0);
            Assert.That(drain(playback, 6001), Has.Count.EqualTo(3), "restart re-arms the full score without dropping crossed events");
        }

        [Test]
        public void TestOnlyLongNotesRetainWavSlotAndHostSharedStore()
        {
            var beatmap = convert(@"
#TITLE Only long notes
#BPM 120
#WAVAA head.wav
#WAVBB tail.wav
#00151:AABB
", true);
            var hold = beatmap.HitObjects.OfType<BmsConvertedHoldNoteHitObject>().Single();
            Assert.That(BmsToManiaKeysoundStoreFactory.ShouldHost(beatmap), Is.True);
            Assert.That(((IHasManiaKeysound)hold).KeysoundCutGroup, Is.Not.Null);
            Assert.That(hold.NodeSamples[0].Single(), Is.EqualTo(hold.KeysoundSample));
            Assert.That(hold.NodeSamples[1], Is.Empty);
            Assert.That(drain(new BmsAutomaticKeysoundPlayback(beatmap), 10000).Single().CutGroup, Is.EqualTo(hold.KeysoundId));
        }

        private static IBeatmap convert(string text, bool mania)
        {
            var source = new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(text, "automatic.bme"));
            return mania
                ? new ManiaRuleset().CreateBeatmapConverter(source).Convert()
                : new BmsBeatmapConverter(source, new BmsRuleset()).Convert();
        }

        private static List<BmsAutomaticKeysoundPlayback.Event> drain(BmsAutomaticKeysoundPlayback playback, double time)
        {
            var events = new List<BmsAutomaticKeysoundPlayback.Event>();
            while (playback.TryDequeue(time, out var keysound))
                events.Add(keysound);
            return events;
        }
    }
}
