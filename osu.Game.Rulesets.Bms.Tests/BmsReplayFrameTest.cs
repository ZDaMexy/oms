// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Replays;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsReplayFrameTest
    {
        [Test]
        public void TestLegacyRoundTripPreservesAllLaneActions()
        {
            var beatmap = new BmsBeatmap();
            var frame = new BmsReplayFrame(123,
                BmsAction.Scratch1,
                BmsAction.Key7,
                BmsAction.Scratch2,
                BmsAction.Key14,
                BmsAction.LaneCoverFocus);

            LegacyReplayFrame legacyFrame = frame.ToLegacy(beatmap);

            Assert.That((int)(legacyFrame.MouseX ?? 0), Is.EqualTo(
                (1 << (int)BmsAction.Scratch1)
                | (1 << (int)BmsAction.Key7)
                | (1 << (int)BmsAction.Scratch2)
                | (1 << (int)BmsAction.Key14)));

            var roundTripped = new BmsReplayFrame();
            roundTripped.FromLegacy(legacyFrame, beatmap);

            Assert.That(roundTripped.Actions, Is.EquivalentTo(new[]
            {
                BmsAction.Scratch1,
                BmsAction.Key7,
                BmsAction.Scratch2,
                BmsAction.Key14,
            }));
        }

        [Test]
        public void TestLegacyImportIgnoresBitsOutsideLaneActionRange()
        {
            var beatmap = new BmsBeatmap();
            var frame = new BmsReplayFrame();

            frame.FromLegacy(new LegacyReplayFrame(0, 1 << ((int)BmsAction.Key14 + 1), null, ReplayButtonState.None), beatmap);

            Assert.That(frame.Actions, Is.Empty);
        }
    }
}
