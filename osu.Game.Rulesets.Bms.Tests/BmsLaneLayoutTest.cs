// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsLaneLayoutTest
    {
        [Test]
        public void TestCreates7KLayoutWithScratchLane()
        {
            var layout = BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K);
            float summedLaneWidths = layout.Lanes.Sum(lane => lane.RelativeWidth);

            Assert.Multiple(() =>
            {
                Assert.That(layout.Lanes.Count, Is.EqualTo(8));
                Assert.That(layout.Lanes[0].IsScratch, Is.True);
                Assert.That(layout.Lanes[0].Action, Is.EqualTo(BmsAction.Scratch1));
                Assert.That(layout.Lanes[1].Action, Is.EqualTo(BmsAction.Key1));
                Assert.That(layout.Lanes[7].Action, Is.EqualTo(BmsAction.Key7));
                Assert.That(layout.Lanes.Skip(1).All(lane => !lane.IsScratch), Is.True);
                Assert.That(layout.Lanes[0].RelativeWidth, Is.EqualTo(layout.Profile.ScratchLaneRelativeWidth).Within(0.0001f));
                Assert.That(layout.Lanes[1].RelativeWidth, Is.EqualTo(layout.Profile.NormalLaneRelativeWidth).Within(0.0001f));
                Assert.That(layout.Lanes[0].RelativeSpacingBefore, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(layout.Lanes[1].RelativeSpacingBefore, Is.EqualTo(layout.Profile.ScratchLaneRelativeSpacing).Within(0.0001f));
                Assert.That(layout.Lanes[2].RelativeSpacingBefore, Is.EqualTo(layout.Profile.NormalLaneRelativeSpacing).Within(0.0001f));
                Assert.That(layout.Lanes[1].RelativeStart, Is.EqualTo(layout.Lanes[0].RelativeWidth + layout.Profile.ScratchLaneRelativeSpacing).Within(0.0001f));
                Assert.That(layout.Lanes[0].RelativeWidth, Is.GreaterThan(layout.Lanes[1].RelativeWidth));
                Assert.That(layout.TotalRelativeWidth, Is.GreaterThan(summedLaneWidths));
            });
        }

        [Test]
        public void TestCreates14KLayoutWithDualScratchLanes()
        {
            var layout = BmsLaneLayout.CreateForKeymode(BmsKeymode.Key14K);

            Assert.Multiple(() =>
            {
                Assert.That(layout.Lanes.Count, Is.EqualTo(16));
                Assert.That(layout.Lanes[0].IsScratch, Is.True);
                Assert.That(layout.Lanes[8].IsScratch, Is.True);
                Assert.That(layout.Lanes[0].Action, Is.EqualTo(BmsAction.Scratch1));
                Assert.That(layout.Lanes[8].Action, Is.EqualTo(BmsAction.Scratch2));
                Assert.That(layout.Lanes[15].Action, Is.EqualTo(BmsAction.Key14));
                Assert.That(layout.Lanes.Count(lane => lane.IsScratch), Is.EqualTo(2));
                Assert.That(layout.Lanes[1].RelativeSpacingBefore, Is.EqualTo(layout.Profile.ScratchLaneRelativeSpacing).Within(0.0001f));
                Assert.That(layout.Lanes[8].RelativeSpacingBefore, Is.EqualTo(layout.Profile.ScratchLaneRelativeSpacing).Within(0.0001f));
                Assert.That(layout.Lanes[9].RelativeSpacingBefore, Is.EqualTo(layout.Profile.ScratchLaneRelativeSpacing).Within(0.0001f));
            });
        }

        [Test]
        public void TestCreatesDefaultLayoutProfile()
        {
            var layout = BmsLaneLayout.CreateForKeymode(BmsKeymode.Key14K);

            Assert.Multiple(() =>
            {
                Assert.That(layout.Profile.Keymode, Is.EqualTo(BmsKeymode.Key14K));
                Assert.That(layout.Profile.LaneCount, Is.EqualTo(layout.Lanes.Count));
                Assert.That(layout.Profile.NormalLaneRelativeWidth, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(layout.Profile.ScratchLaneRelativeWidth, Is.GreaterThan(layout.Profile.NormalLaneRelativeWidth));
                Assert.That(layout.Profile.NormalLaneRelativeSpacing, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(layout.Profile.ScratchLaneRelativeSpacing, Is.GreaterThan(layout.Profile.NormalLaneRelativeSpacing));
                Assert.That(layout.Profile.PlayfieldWidth, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(layout.Profile.PlayfieldHeight, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(layout.Profile.HitTargetHeight, Is.EqualTo(16f).Within(0.0001f));
                Assert.That(layout.Profile.HitTargetBarHeight, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(layout.Profile.HitTargetLineHeight, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(layout.Profile.HitTargetGlowRadius, Is.EqualTo(6f).Within(0.0001f));
                Assert.That(layout.Profile.BarLineHeight, Is.EqualTo(2f).Within(0.0001f));
            });
        }
    }
}
