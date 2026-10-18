// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestAuthoredDualStageGapReachesAllSixteenNativeNotes()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            AddStep("select native double-deck reference geometry", () =>
            {
                (_, candidate) = createCandidate(root => File.WriteAllText(Path.Combine(root, "skin.ini"), """
                    [General]
                    Name: authored double deck spacing
                    Author: OMS tests
                    Version: 2.7
                    [Bms]
                    Keymode: 14K
                    PlayfieldWidth: 0.526041666667
                    PlayfieldHeight: 0.674074074074
                    DualStageGap: 0.128712871287
                    LaneSidePadding: 0.009090909091
                    NormalLaneWidth: 1
                    BlackLaneWidth: 0.769230769231
                    ScratchLaneWidth: 1.730769230769
                    NormalLaneSpacing: 0.038461538462
                    ScratchLaneSpacing: 0.038461538462
                    """), typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("double deck package committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                                                               && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount a real 14K renderer at native reference resolution", () =>
            {
                string chart = "#TITLE Double deck geometry\n#PLAYER 3\n#BPM 120\n#WAV01 note.wav\n";
                foreach (string channel in new[] { "11", "12", "13", "14", "15", "18", "19", "16", "21", "22", "23", "24", "25", "28", "29", "26" })
                    chart += "#001" + channel + ":01\n";
                Add(renderer = new ExactLayoutJourneyHost(manager,
                    exactBmsFixture: new ExactBmsProductionFixture(chart, "author-double-deck.bme", BmsPlayfieldStyle.Center))
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(1920, 1080),
                });
                renderer.ShowBms();
            });
            AddUntilStep("all sixteen native notes are alive", () => renderer.BmsReady
                && renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>().Count() == 16);
            AddStep("measure the actual note drawables against the original decks", () =>
            {
                BmsPlayfield playfield = renderer.BmsDrawable.Playfield;
                Assert.That(renderer.BmsDrawable.LayoutSnapshot.Keymode, Is.EqualTo(BmsKeymode.Key14K));
                float[] starts = { 459, 551, 605, 647, 701, 743, 797, 839, 1029, 1083, 1125, 1179, 1221, 1275, 1317, 1371 };
                float[] widths = { 90, 52, 40, 52, 40, 52, 40, 52, 52, 40, 52, 40, 52, 40, 52, 90 };
                foreach (DrawableBmsHitObject note in playfield.AllHitObjects.OfType<DrawableBmsHitObject>())
                {
                    int index = ((BmsHitObject)note.HitObject).LaneIndex;
                    Vector2 left = playfield.ToLocalSpace(note.ScreenSpaceDrawQuad.TopLeft);
                    Vector2 right = playfield.ToLocalSpace(note.ScreenSpaceDrawQuad.BottomRight);
                    Assert.That(left.X, Is.EqualTo(starts[index]).Within(0.01), $"Native DP lane {index}");
                    Assert.That(right.X - left.X, Is.EqualTo(widths[index]).Within(0.01), $"Native DP width {index}");
                }
            });
        }

        [TestCase(BmsPlayfieldStyle.P1, false, 71, 440)]
        [TestCase(BmsPlayfieldStyle.P2, false, 1409, 440)]
        [TestCase(BmsPlayfieldStyle.P1, true, 75, 432)]
        [TestCase(BmsPlayfieldStyle.P2, true, 1413, 432)]
        public void TestAuthoredSideInsetReachesReal1920By1080SevenKeyLanes(BmsPlayfieldStyle style, bool padded, float expectedLeft, float expectedWidth)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;

            AddStep("select exact authored placement package", () =>
            {
                (_, candidate) = createCandidate(root =>
                {
                    File.WriteAllText(Path.Combine(root, "skin.ini"), """
                    [General]
                    Name: authored side placement
                    Author: OMS tests
                    Version: 2.7
                    [Bms]
                    Keymode: 7K
                    PlayfieldWidth: 0.229166667
                    PlayfieldHeight: 0.674074074
                    PlayfieldSideInset: 0.036979167
                    """ + (padded ? "\nLaneSidePadding: 0.009090909\nNormalLaneWidth: 1\nBlackLaneWidth: 0.769230769\nScratchLaneWidth: 1.730769231\nNormalLaneSpacing: 0.038461538\nScratchLaneSpacing: 0.038461538\n" : ""));
                    writePresentationOnlyTestPackage(root);
                }, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("exact placement package committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                                                                 && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real renderer at 1920 by 1080", () =>
            {
                var fixture = new ExactBmsProductionFixture("#TITLE Author placement\n#BPM 120\n#WAV01 note.wav\n#00111:01\n#00112:01\n#00113:01\n#00114:01\n#00115:01\n#00118:01\n#00119:01\n#00116:01\n",
                    "authored-placement.bme", style);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture)
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(1920, 1080),
                });
                renderer.ShowBms();
            });
            AddUntilStep("all native lanes retain the authored layout publication", () => renderer.BmsReady);
            AddUntilStep("all real short-note drawables are alive", () => renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                .Count(note => note.HitObject.GetType() == typeof(BmsHitObject)) == 8);
            AddStep("verify real lane draw bounds and timing authority", () =>
            {
                BmsPlayfield playfield = renderer.BmsDrawable.Playfield;
                BmsGameplayLayoutSnapshot snapshot = renderer.BmsDrawable.LayoutSnapshot;
                Vector2[] topLeft = playfield.Lanes.Select(lane => playfield.ToLocalSpace(lane.ScreenSpaceDrawQuad.TopLeft)).ToArray();
                Vector2[] bottomRight = playfield.Lanes.Select(lane => playfield.ToLocalSpace(lane.ScreenSpaceDrawQuad.BottomRight)).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.Keymode, Is.EqualTo(BmsKeymode.Key7K));
                    Assert.That(snapshot.Style, Is.EqualTo(style));
                    Assert.That(snapshot.PlayfieldRect.Width * 1920, Is.EqualTo(440).Within(0.001));
                    Assert.That(playfield.DrawSize, Is.EqualTo(new Vector2(1920, 1080)));
                    Assert.That(topLeft.Min(point => point.X), Is.EqualTo(expectedLeft).Within(0.01));
                    Assert.That(topLeft.Min(point => point.Y), Is.Zero.Within(0.01));
                    Assert.That(bottomRight.Max(point => point.X) - topLeft.Min(point => point.X), Is.EqualTo(expectedWidth).Within(0.01));
                    Assert.That(bottomRight.Max(point => point.Y) - topLeft.Min(point => point.Y), Is.EqualTo(728).Within(0.01));
                    Assert.That(playfield.Lanes.All(lane => ReferenceEquals(lane.LayoutSnapshot, snapshot)), Is.True);
                    Assert.That(snapshot.Profile.HitTargetVerticalOffset, Is.Zero);
                    Assert.That(snapshot.BgaViewports.All(viewport => !viewport.Intersects(snapshot.PlayfieldRect) && !viewport.Intersects(snapshot.GaugeRect)), Is.True);
                    Assert.That(snapshot.Neutral.Diagnostics.Any(diagnostic => diagnostic.Code == "bms.layout.invalid-playfield-side-inset"), Is.False);
                });

                if (padded)
                {
                    float[] starts = style == BmsPlayfieldStyle.P1
                        ? new float[] { 75, 167, 221, 263, 317, 359, 413, 455 }
                        : new float[] { 1755, 1413, 1467, 1509, 1563, 1605, 1659, 1701 };
                    float[] widths = { 90, 52, 40, 52, 40, 52, 40, 52 };

                    foreach (DrawableBmsHitObject note in playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                                                                  .Where(note => note.HitObject.GetType() == typeof(BmsHitObject)))
                    {
                        int logical = ((BmsHitObject)note.HitObject).LaneIndex;
                        float left = playfield.ToLocalSpace(note.ScreenSpaceDrawQuad.TopLeft).X;
                        float right = playfield.ToLocalSpace(note.ScreenSpaceDrawQuad.BottomRight).X;
                        Assert.That(left, Is.EqualTo(starts[logical]).Within(0.01), $"Native note lane {logical}");
                        Assert.That(right - left, Is.EqualTo(widths[logical]).Within(0.01), $"Native note width {logical}");
                    }
                }
            });
        }
    }
}
