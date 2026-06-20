// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsBgaPlayerTest
    {
        private static BmsBgaTimelineEntry image(double time)
            => new BmsBgaTimelineEntry(time, BmsBgaLayer.Base, $"f{time}.png", false);

        [Test]
        public void TestEmptyTimelineHasNoActiveEntry()
        {
            Assert.That(BmsBgaPlayer.GetActiveIndex(Array.Empty<BmsBgaTimelineEntry>(), 0), Is.EqualTo(-1));
            Assert.That(BmsBgaPlayer.GetActiveIndex(Array.Empty<BmsBgaTimelineEntry>(), 5000), Is.EqualTo(-1));
        }

        [Test]
        public void TestBeforeFirstEntryIsInactive()
        {
            var entries = new[] { image(1000), image(2000) };
            Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 999.999), Is.EqualTo(-1));
        }

        [Test]
        public void TestSelectsLatestEntryAtOrBeforeTime()
        {
            var entries = new[] { image(1000), image(2000), image(3000) };

            Assert.Multiple(() =>
            {
                Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 1000), Is.EqualTo(0)); // exact boundary
                Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 1500), Is.EqualTo(0)); // between
                Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 2000), Is.EqualTo(1)); // exact boundary
                Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 99999), Is.EqualTo(2)); // after last
            });
        }

        [Test]
        public void TestEqualTimeEntriesResolveToLastSourceWins()
        {
            // Two switches at the same time: the later one (higher index) must win.
            var entries = new[] { image(1000), image(2000), image(2000) };
            Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 2000), Is.EqualTo(2));
        }

        [Test]
        public void TestSeekBackwardReselectsCorrectly()
        {
            // The selection is a pure function of time, so a backward clock seek re-resolves with no residual state.
            var entries = new[] { image(1000), image(2000), image(3000) };

            Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 3500), Is.EqualTo(2));
            Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 1500), Is.EqualTo(0));
            Assert.That(BmsBgaPlayer.GetActiveIndex(entries, 0), Is.EqualTo(-1));
        }

        // Default-skin placement is a screen corner mirroring the playfield side. 5K/7K honour the style; other keymodes
        // are forced to Center style by GetAppliedStyle (=> top-right), and 14K double-play also uses a corner (top-right).
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, BmsBgaPlacement.TopRight)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, BmsBgaPlacement.TopLeft)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center, BmsBgaPlacement.TopRight)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch, BmsBgaPlacement.TopRight)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2, BmsBgaPlacement.TopLeft)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.P1, BmsBgaPlacement.TopRight)] // 9K forced Center style => top-right
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.P1, BmsBgaPlacement.TopRight)] // 14K => top-right corner regardless
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center, BmsBgaPlacement.TopRight)]
        public void TestResolveDefaultPlacementMirrorsPlayfield(BmsKeymode keymode, BmsPlayfieldStyle style, BmsBgaPlacement expected)
        {
            Assert.That(BmsBgaPanel.ResolveDefaultPlacement(keymode, style), Is.EqualTo(expected));
        }
    }
}
