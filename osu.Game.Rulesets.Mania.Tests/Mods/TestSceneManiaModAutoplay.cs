// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Mods
{
    public partial class TestSceneManiaModAutoplay : ModTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        [Test]
        public void TestPerfectScoreOnShortHoldNote()
        {
            CreateModTest(new ModTestData
            {
                Autoplay = true,
                CreateBeatmap = () => new ManiaBeatmap(new StageDefinition(1))
                {
                    HitObjects = new List<ManiaHitObject>
                    {
                        new HoldNote
                        {
                            StartTime = 100,
                            EndTime = 100,
                        },
                        new HoldNote
                        {
                            StartTime = 100.1,
                            EndTime = 150,
                        },
                    }
                },
                PassCondition = () => Player.ScoreProcessor.Combo.Value == 4
            });
        }

        [Test]
        public void TestAutoplayIgnoresSampleOnlyScratchObjects()
        {
            CreateModTest(new ModTestData
            {
                Autoplay = true,
                CreateBeatmap = () => new ManiaBeatmap(new StageDefinition(1))
                {
                    HitObjects = new List<ManiaHitObject>
                    {
                        new BmsConvertedScratchSampleHitObject
                        {
                            StartTime = 100,
                            Column = 0,
                        },
                        new Note
                        {
                            StartTime = 100,
                            Column = 0,
                        },
                    }
                },
                PassCondition = () => Player.ScoreProcessor.Combo.Value == 1
            });
        }

        [Test]
        public void TestAutoplayHoldsLongNoteAlongsideSampleOnlyObject()
        {
            // Regression guard: a hold note's own MaxResult is IgnoreHit (not combo-affecting), so a naive
            // "skip anything not combo-affecting" autoplay filter drops every long note. Autoplay must still press a
            // genuine long hold for its full duration (combo comes from the nested head + tail), while a co-located
            // sample-only object (here the BGM keysound variant) is ignored.
            CreateModTest(new ModTestData
            {
                Autoplay = true,
                CreateBeatmap = () => new ManiaBeatmap(new StageDefinition(1))
                {
                    HitObjects = new List<ManiaHitObject>
                    {
                        new BmsConvertedBgmSampleHitObject
                        {
                            StartTime = 100,
                            Column = 0,
                        },
                        new HoldNote
                        {
                            StartTime = 200,
                            EndTime = 1200,
                            Column = 0,
                        },
                    }
                },
                PassCondition = () => Player.ScoreProcessor.Combo.Value == 2
            });
        }
    }
}
