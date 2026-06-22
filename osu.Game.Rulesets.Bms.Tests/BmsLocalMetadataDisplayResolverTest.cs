// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsLocalMetadataDisplayResolverTest
    {
        [Test]
        public void TestResolvesLegacyBmsMetadataFromArtistAndTags()
        {
            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "Ym1024 feat. lamie* /obj:BAECON",
                    ArtistUnicode = "Ym1024 feat. lamie* /obj:BAECON",
                    Tags = "J-Airy Pop",
                }
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtist(beatmap), Is.EqualTo("Ym1024 feat. lamie*"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayArtistUnicode(beatmap), Is.EqualTo("Ym1024 feat. lamie*"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(beatmap), Is.EqualTo("BAECON"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayGenre(beatmap), Is.EqualTo("J-Airy Pop"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayMapperTags(beatmap), Is.Empty);
            });
        }

        [Test]
        public void TestSplitsTrailingBracketTitleTagWhenNoSubtitle()
        {
            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                DifficultyName = "12",
                Metadata = new BeatmapMetadata { Title = "GOODBOUNCE [ANOTHER]", TitleUnicode = "GOODBOUNCE [ANOTHER]" },
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayTitle(beatmap), Is.EqualTo("GOODBOUNCE"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayTitleUnicode(beatmap), Is.EqualTo("GOODBOUNCE"));
                // No #DIFFICULTY / #SUBTITLE: the difficulty name comes from the title tag, not the redundant play level.
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyName(beatmap), Is.EqualTo("ANOTHER"));
            });
        }

        [Test]
        public void TestTitleTagBeatsHeaderDifficultyLabel()
        {
            // Real-world regression (Dead Soul [Revive], #DIFFICULTY 5): the charter's named difficulty in the title
            // tail must NOT be overwritten by the generic #DIFFICULTY category ("Insane").
            var metadata = new BeatmapMetadata { Title = "Dead Soul [Revive]" };

            metadata.SetRulesetData(new DifficultyTable.BmsBeatmapMetadataData
            {
                ChartMetadata = new DifficultyTable.BmsChartMetadata { HeaderDifficulty = 5, PlayLevel = "77" },
            });

            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                DifficultyName = "Insane 77",
                Metadata = metadata,
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyName(beatmap), Is.EqualTo("Revive"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayTitle(beatmap), Is.EqualTo("Dead Soul"));
            });
        }

        [Test]
        public void TestHeaderDifficultyLabelUsedWhenNoTitleTagOrSubtitle()
        {
            // Fallback: with no #SUBTITLE and no title tail tag, the generic #DIFFICULTY category names the difficulty
            // (and the redundant play-level number is not appended).
            var metadata = new BeatmapMetadata { Title = "Song" };

            metadata.SetRulesetData(new DifficultyTable.BmsBeatmapMetadataData
            {
                ChartMetadata = new DifficultyTable.BmsChartMetadata { HeaderDifficulty = 4, PlayLevel = "12" },
            });

            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                DifficultyName = "Another 12",
                Metadata = metadata,
            };

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyName(beatmap), Is.EqualTo("Another"));
        }

        [Test]
        public void TestExplicitSubtitleKeepsTitleAndNamesDifficulty()
        {
            var metadata = new BeatmapMetadata { Title = "Song [ANOTHER]" };

            metadata.SetRulesetData(new DifficultyTable.BmsBeatmapMetadataData
            {
                ChartMetadata = new DifficultyTable.BmsChartMetadata { Subtitle = "Extra Stage" },
            });

            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                DifficultyName = "12",
                Metadata = metadata,
            };

            Assert.Multiple(() =>
            {
                // Charter provided #SUBTITLE, so the title tail is left intact and the subtitle names the difficulty.
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayTitle(beatmap), Is.EqualTo("Song [ANOTHER]"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyName(beatmap), Is.EqualTo("Extra Stage"));
            });
        }

        [Test]
        public void TestDashWrappedTitleTagSplit()
        {
            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                Metadata = new BeatmapMetadata { Title = "Song -HYPER-" },
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayTitle(beatmap), Is.EqualTo("Song"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyName(beatmap), Is.EqualTo("HYPER"));
            });
        }

        [Test]
        public void TestBareNumericPlayLevelIsNotShownAsDifficultyName()
        {
            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                DifficultyName = "12",
                Metadata = new BeatmapMetadata { Title = "Song" },
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayTitle(beatmap), Is.EqualTo("Song"));
                // No descriptor anywhere and the stored name is just the play level -> blank (star conveys the level).
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyName(beatmap), Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public void TestNonBmsBeatmapKeepsRawTitleAndDifficultyName()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "osu" })
            {
                DifficultyName = "Insane",
                Metadata = new BeatmapMetadata { Title = "Song (Special)" },
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayTitle(beatmap), Is.EqualTo("Song (Special)"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyName(beatmap), Is.EqualTo("Insane"));
            });
        }

        [Test]
        public void TestResolvesBmsGenreAndCreatorFromRulesetData()
        {
            var metadata = new BeatmapMetadata
            {
                Artist = "Test Artist",
            };

            metadata.SetRulesetData(new DifficultyTable.BmsBeatmapMetadataData
            {
                ChartMetadata = new DifficultyTable.BmsChartMetadata
                {
                    Genre = "Hardcore",
                    SubArtist = "obj: Test Charter",
                }
            });

            var beatmap = new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone())
            {
                Metadata = metadata,
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(beatmap), Is.EqualTo("Test Charter"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayGenre(beatmap), Is.EqualTo("Hardcore"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayMapperTags(beatmap), Is.Empty);
            });
        }

        [Test]
        public void TestDifficultyLevelCombinesCategoryAndPlayLevel()
        {
            // #DIFFICULTY 2 + #PLAYLEVEL 7 -> the BMS-mode pill shows "NORMAL 7" (tier 2 = blue).
            var beatmap = bmsBeatmapWithChartMetadata(headerDifficulty: 2, playLevel: "7");

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel(beatmap), Is.EqualTo("NORMAL 7"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetBmsDifficultyTier(beatmap), Is.EqualTo(2));
            });
        }

        [Test]
        public void TestDifficultyLevelUnknownWhenHeaderDifficultyMissingOrZero()
        {
            var missing = bmsBeatmapWithChartMetadata(headerDifficulty: null, playLevel: "9");
            var zero = bmsBeatmapWithChartMetadata(headerDifficulty: 0, playLevel: "9");

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel(missing), Is.EqualTo("UNKNOWN 9"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetBmsDifficultyTier(missing), Is.EqualTo(0));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel(zero), Is.EqualTo("UNKNOWN 9"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetBmsDifficultyTier(zero), Is.EqualTo(0));
            });
        }

        [Test]
        public void TestDifficultyLevelKeepsRawPlayLevelText()
        {
            // The play level is the charter's raw #PLAYLEVEL string, kept verbatim (no numeric parsing).
            var beatmap = bmsBeatmapWithChartMetadata(headerDifficulty: 4, playLevel: "12+");

            Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel(beatmap), Is.EqualTo("ANOTHER 12+"));
        }

        [Test]
        public void TestDifficultyLevelLabelOnlyWhenNoPlayLevel()
        {
            var beatmap = bmsBeatmapWithChartMetadata(headerDifficulty: 3, playLevel: string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel(beatmap), Is.EqualTo("HYPER"));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetBmsDifficultyTier(beatmap), Is.EqualTo(3));
            });
        }

        [Test]
        public void TestDifficultyLevelEmptyForNonBmsBeatmap()
        {
            var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = "mania" })
            {
                Metadata = new BeatmapMetadata { Title = "Song" },
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel(beatmap), Is.EqualTo(string.Empty));
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetBmsDifficultyTier(beatmap), Is.EqualTo(0));
            });
        }

        private static BeatmapInfo bmsBeatmapWithChartMetadata(int? headerDifficulty, string playLevel)
        {
            var metadata = new BeatmapMetadata();

            metadata.SetRulesetData(new DifficultyTable.BmsBeatmapMetadataData
            {
                ChartMetadata = new DifficultyTable.BmsChartMetadata { HeaderDifficulty = headerDifficulty, PlayLevel = playLevel },
            });

            return new BeatmapInfo(new BmsRuleset().RulesetInfo.Clone()) { Metadata = metadata };
        }
    }
}
