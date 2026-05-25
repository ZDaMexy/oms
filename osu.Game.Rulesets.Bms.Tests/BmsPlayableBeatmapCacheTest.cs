// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsPlayableBeatmapCacheTest
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly BmsRuleset ruleset = new BmsRuleset();

        [Test]
        public void TestWorkingBeatmapReusesModlessPlayableProjectionPerSourceBeatmap()
        {
            var workingBeatmap = new TestWorkingBeatmap(createSourceBeatmap());

            var firstPlayable = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo);
            var secondPlayable = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo);

            Assert.Multiple(() =>
            {
                Assert.That(firstPlayable, Is.TypeOf<BmsBeatmap>());
                Assert.That(secondPlayable, Is.SameAs(firstPlayable));
            });
        }

        [Test]
        public void TestWorkingBeatmapDoesNotShareCachedPlayableProjectionAcrossSourceBeatmaps()
        {
            var firstWorkingBeatmap = new TestWorkingBeatmap(createSourceBeatmap());
            var secondWorkingBeatmap = new TestWorkingBeatmap(createSourceBeatmap());

            var firstPlayable = firstWorkingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo);
            var secondPlayable = secondWorkingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo);

            Assert.That(secondPlayable, Is.Not.SameAs(firstPlayable));
        }

        [Test]
        public void TestWorkingBeatmapBypassesModlessCacheWhenModsPresent()
        {
            var workingBeatmap = new TestWorkingBeatmap(createSourceBeatmap());

            var firstPlayable = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, new[] { new BmsModMirror() });
            var secondPlayable = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, new[] { new BmsModMirror() });

            Assert.That(secondPlayable, Is.Not.SameAs(firstPlayable));
        }

        [Test]
        public void TestLoaderSeededCacheReturnsFinalizedHoldNoteProjection()
        {
            const string text = @"
#BPM 120
#WAVAA hold/head.wav
#WAVZZ hold/tail.wav
#LNTYPE 2
#00151:AAZZ
#00251:ZZ00
";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

            var sourceBeatmap = (BmsDecodedBeatmap)new BmsBeatmapLoader().Load(
                stream,
                "cache-hold.bms",
                new BeatmapInfo(ruleset.RulesetInfo.Clone(), new BeatmapDifficulty(), new BeatmapMetadata()));

            var playable = (BmsBeatmap)new TestWorkingBeatmap(sourceBeatmap).GetPlayableBeatmap(ruleset.RulesetInfo);
            var holdNote = playable.HitObjects.OfType<BmsHoldNote>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(holdNote.Head, Is.Not.Null);
                Assert.That(holdNote.Tail, Is.Not.Null);
                Assert.That(holdNote.BodyTicks, Is.Not.Empty);
            });
        }

        [Test]
        public void TestPrepareScoreInfoForResultsDoesNotReapplyBeatmapModsToPlayableBeatmap()
        {
            var workingBeatmap = new TestWorkingBeatmap(createMirrorSourceBeatmap());

            var modlessPlayable = (BmsBeatmap)workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo);
            var mirroredPlayable = (BmsBeatmap)workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, new Mod[] { new BmsModMirror() });
            var modlessLanes = getLaneSequence(modlessPlayable);
            var mirroredLanes = getLaneSequence(mirroredPlayable);

            Assert.That(mirroredLanes, Is.Not.EqualTo(modlessLanes));

            var score = new ScoreInfo
            {
                Mods = new Mod[] { new BmsModMirror() },
            };

            ruleset.PrepareScoreInfoForResults(score, mirroredPlayable);

            Assert.Multiple(() =>
            {
                Assert.That(getLaneSequence(mirroredPlayable), Is.EqualTo(mirroredLanes));
                Assert.That(score.GetRulesetData<BmsScoreInfoData>(), Is.Not.Null);
            });
        }

        private BmsDecodedBeatmap createSourceBeatmap()
        {
            const string text = @"
#TITLE Cache Test
#ARTIST OMS
#BPM 150
#WAVAA hit.wav
#00111:AA00
";

            return new BmsDecodedBeatmap(decoder.DecodeText(text, "cache-test.bms"));
        }

        private BmsDecodedBeatmap createMirrorSourceBeatmap()
        {
            const string text = @"
#TITLE Cache Mirror Test
#ARTIST OMS
#BPM 150
#WAVAA hit.wav
#00111:AA00
#00213:AA00
";

            return new BmsDecodedBeatmap(decoder.DecodeText(text, "cache-mirror-test.bms"));
        }

        private static int[] getLaneSequence(BmsBeatmap beatmap)
            => beatmap.HitObjects.Select(hitObject => ((BmsHitObject)hitObject).LaneIndex).ToArray();

        private class TestWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap sourceBeatmap;

            public TestWorkingBeatmap(IBeatmap sourceBeatmap)
                : base(sourceBeatmap.BeatmapInfo, null)
            {
                this.sourceBeatmap = sourceBeatmap;
            }

            protected override IBeatmap GetBeatmap() => sourceBeatmap;

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
