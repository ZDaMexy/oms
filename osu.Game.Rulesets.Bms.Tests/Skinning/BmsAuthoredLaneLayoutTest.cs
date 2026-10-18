// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsAuthoredLaneLayoutTest
    {
        [TestCase(BmsPlayfieldStyle.P1, false)]
        [TestCase(BmsPlayfieldStyle.P2, false)]
        [TestCase(BmsPlayfieldStyle.P1, true)]
        [TestCase(BmsPlayfieldStyle.P2, true)]
        public void TestFiveKeysRetainFarEmptySlotsWithoutCreatingInputs(BmsPlayfieldStyle style, bool compact)
        {
            GameplaySkinAuthorLayout author = createLayout(style, compact);
            var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key5K } };
            BmsGameplayLayoutSnapshot result = new BmsGameplayLayoutProvider(beatmap).PublishForTesting(style,
                new BmsGameplayLayoutConfiguration { AuthorLayout = author });
            float frameLeft = style == BmsPlayfieldStyle.P1 ? compact ? 72 : 71 : compact ? 1410 : 1409;
            float width = compact ? 438 : 440;
            Assert.Multiple(() =>
            {
                Assert.That(result.LanesInLogicalOrder, Has.Count.EqualTo(6));
                Assert.That(result.KeymodeResolution, Is.SameAs(beatmap.BmsInfo.KeymodeResolution));
                Assert.That(result.LanesInLogicalOrder.Select(l => l.Action), Is.EqualTo(new[]
                {
                    BmsAction.Scratch1, BmsAction.Key1, BmsAction.Key2, BmsAction.Key3, BmsAction.Key4, BmsAction.Key5,
                }));
                Assert.That(result.PlayfieldRect.Left * 1920, Is.EqualTo(frameLeft).Within(0.001));
                Assert.That(result.PlayfieldRect.Width * 1920, Is.EqualTo(width).Within(0.001));
                Assert.That(result.LanesInLogicalOrder.Count(l => l.IsScratch), Is.EqualTo(1));
                Assert.That(result.Neutral.Diagnostics.Any(d => d.Code == "bms.layout.author-topology-mismatch"), Is.False);
            });
            foreach (BmsGameplayLayoutLane lane in result.LanesInLogicalOrder)
            {
                GameplaySkinAuthorLaneSpan expected = author.Stages[0].Lanes[lane.GroupLocalLogicalIndex];
                Assert.That(lane.NeutralLane.Rect.Left * 1920, Is.EqualTo(frameLeft + expected.X * width).Within(0.001));
                Assert.That(lane.NeutralLane.Rect.Width * 1920, Is.EqualTo(expected.Width * width).Within(0.001));
                Assert.That(lane.LogicalIndex, Is.EqualTo(lane.GroupLocalLogicalIndex));
            }
            BmsGameplayLayoutLane scratch = result.LanesInLogicalOrder.Single(l => l.IsScratch);
            Assert.Multiple(() =>
            {
                Assert.That(scratch.KeyVisualRect.Width * 1920, Is.EqualTo(150).Within(0.001));
                Assert.That(scratch.KeyVisualRect.Left * 1920, Is.EqualTo(style == BmsPlayfieldStyle.P1 ? 15 : 1755).Within(0.001));
                Assert.That(scratch.KeyVisualRect.Top * 1080, Is.EqualTo(compact ? 726 : 728).Within(0.001));
                Assert.That(result.Neutral.Surfaces.All(s => result.Context.SafeBounds.Contains(s.Rect)), Is.True);
            });
            float activeLeft = result.LanesInLogicalOrder.Min(l => l.NeutralLane.Rect.Left) * 1920;
            float activeRight = result.LanesInLogicalOrder.Max(l => l.NeutralLane.Rect.Right) * 1920;
            Assert.That(style == BmsPlayfieldStyle.P1 ? frameLeft + width - activeRight : activeLeft - frameLeft,
                Is.EqualTo(compact ? 99 : 100).Within(0.001));
        }

        [Test]
        public void TestWrongNativeLaneCountRejectsTheWholeProfileAndRetainsLegacyGeometry()
        {
            GameplaySkinAuthorLayout source = createLayout(BmsPlayfieldStyle.P1, false);
            GameplaySkinAuthorLaneSpan[] seven = Enumerable.Range(0, 7).Select(i => new GameplaySkinAuthorLaneSpan(i / 7d, 1 / 7d)).ToArray();
            var wrong = new GameplaySkinAuthorLayout("wrong", "bms", "5k", "p1",
                new[] { new GameplaySkinAuthorLayoutStage(source.Stages[0].Rect, seven) });
            BmsGameplayLayoutSnapshot solve(GameplaySkinAuthorLayout? layout) => new BmsGameplayLayoutProvider(
                    new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key5K } })
                .PublishForTesting(BmsPlayfieldStyle.P1, new BmsGameplayLayoutConfiguration { AuthorLayout = layout });
            BmsGameplayLayoutSnapshot baseline = solve(null), rejected = solve(wrong);
            Assert.Multiple(() =>
            {
                Assert.That(rejected.LanesInLogicalOrder.Select(l => l.NeutralLane.Rect), Is.EqualTo(baseline.LanesInLogicalOrder.Select(l => l.NeutralLane.Rect)));
                Assert.That(rejected.PlayfieldRect, Is.EqualTo(baseline.PlayfieldRect));
                Assert.That(rejected.Neutral.Diagnostics.Any(d => d.Code == "bms.layout.author-topology-mismatch"), Is.True);
                Assert.That(BmsGameplayLayoutProvider.ResolvePresentation(null), Is.EqualTo(BmsPlayfieldStyle.Center));
            });
        }

        [Test]
        public void TestAuthorTopAndFieldSurviveAnUnusableLegacyBottomBgaFallback()
        {
            GameplaySkinLayoutRect field = GameplaySkinLayoutRect.Create(0.05f, 0.1f, 0.9f, 0.6f);
            GameplaySkinAuthorLayout author = profile(field, 0.1, 0.1);
            BmsGameplayLayoutSnapshot result = solveProfile(author);
            Assert.Multiple(() =>
            {
                Assert.That(result.PlayfieldRect, Is.EqualTo(field));
                Assert.That(result.Neutral.GroupsInLogicalOrder.Single().Rect, Is.EqualTo(field));
                Assert.That(result.LanesInLogicalOrder.All(l => l.NeutralLane.Rect.Top == field.Top && l.NeutralLane.Rect.Height == field.Height), Is.True);
                Assert.That(result.KeyAreaRect.Top, Is.EqualTo(field.Bottom));
                Assert.That(result.GaugeRect.Top, Is.EqualTo(field.Bottom + 0.1f + 0.006f).Within(0.000001));
                Assert.That(result.JudgementLineRect.Bottom, Is.EqualTo(field.Bottom).Within(0.000001));
                Assert.That(result.BgaViewports, Is.Empty);
                Assert.That(result.Neutral.Diagnostics.Any(d => d.Code == "bms.layout.bga-no-safe-viewport"), Is.True);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestOmittedGaugeUsesNativeHeightOrRejectsTheEntireUnsafeProfile(bool fits)
        {
            GameplaySkinAuthorLayout author = profile(GameplaySkinLayoutRect.Create(0.1f, fits ? 0.1f : 0, 0.3f, fits ? 0.6f : 0.995f));
            BmsGameplayLayoutSnapshot original = solveProfile(null), result = solveProfile(author);
            Assert.Multiple(() =>
            {
                Assert.That(result.GaugeRect.Height, Is.EqualTo(original.GaugeRect.Height));
                Assert.That(result.Context.SafeBounds.Contains(result.GaugeRect), Is.True);
                Assert.That(result.Neutral.Diagnostics.Any(d => d.Code == "bms.layout.author-band-out-of-bounds"), Is.EqualTo(!fits));
                Assert.That(result.PlayfieldRect, Is.EqualTo(fits ? author.Stages[0].Rect : original.PlayfieldRect));
            });
            if (!fits)
                Assert.That(result.LanesInLogicalOrder.Select(l => l.NeutralLane.Rect), Is.EqualTo(original.LanesInLogicalOrder.Select(l => l.NeutralLane.Rect)));
        }

        private static GameplaySkinAuthorLayout profile(GameplaySkinLayoutRect rect, double keys = 0, double gauge = 0)
            => new GameplaySkinAuthorLayout("authored.bounds", "bms", "5k", "p1",
                new[] { new GameplaySkinAuthorLayoutStage(rect, Enumerable.Range(0, 6).Select(i => new GameplaySkinAuthorLaneSpan(i / 6d, 1 / 6d)).ToArray()) }, keys, gauge);

        private static BmsGameplayLayoutSnapshot solveProfile(GameplaySkinAuthorLayout? author)
            => new BmsGameplayLayoutProvider(new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key5K } })
                .PublishForTesting(BmsPlayfieldStyle.P1, new BmsGameplayLayoutConfiguration { AuthorLayout = author });

        private static GameplaySkinAuthorLayout createLayout(BmsPlayfieldStyle style, bool compact)
        {
            double width = compact ? 438 : 440;
            double padding = compact ? 3 : 4;
            double white = compact ? 51 : 52;
            double black = compact ? 39 : 40;
            double gap = compact ? 3 : 2;
            bool right = style == BmsPlayfieldStyle.P2;
            double left = right ? compact ? 1410 : 1409 : compact ? 72 : 71;
            var physical = new GameplaySkinAuthorLaneSpan[8];
            double x = padding;
            for (int i = 0; i < 8; i++)
            {
                bool scratch = i == (right ? 7 : 0);
                int normal = right ? i : i - 1;
                double laneWidth = scratch ? 90 : normal % 2 == 0 ? white : black;
                physical[i] = new GameplaySkinAuthorLaneSpan(x / width, laneWidth / width);
                x += laneWidth + gap;
            }
            int[] slots = right ? new[] { 7, 2, 3, 4, 5, 6 } : new[] { 0, 1, 2, 3, 4, 5 };
            GameplaySkinAuthorLaneSpan[] lanes = slots.Select(i => physical[i]).ToArray();
            GameplaySkinAuthorLaneSpan[] keys = (GameplaySkinAuthorLaneSpan[])lanes.Clone();
            keys[0] = new GameplaySkinAuthorLaneSpan((right ? 1755 - left : 15 - left) / width, 150 / width);
            return new GameplaySkinAuthorLayout("authored.five", "bms", "5k", right ? "p2" : "p1",
                new[] { new GameplaySkinAuthorLayoutStage(GameplaySkinLayoutRect.Create((float)(left / 1920), 0, (float)(width / 1920), (compact ? 726f : 728f) / 1080), lanes, keys) },
                120d / 1080, 110d / 1080);
        }
    }
}
