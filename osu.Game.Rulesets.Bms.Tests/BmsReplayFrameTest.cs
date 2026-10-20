// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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

        [Test]
        public void TestAllSixteenLanesAndBothMotionLevelsRoundTripExactlyThroughLegacyFloat()
        {
            var beatmap = new BmsBeatmap();
            BmsAction[] lanes = Enum.GetValues<BmsAction>().Where(action => action.IsLaneAction()).ToArray();
            BmsAction[] actions = lanes.Concat(new[] { BmsAction.Scratch1Motion, BmsAction.Scratch2Motion }).ToArray();
            var frame = new BmsReplayFrame(123, actions);
            int expectedMask = 0xffff | (1 << 20) | (1 << 21);
            LegacyReplayFrame legacy = frame.ToLegacy(beatmap);
            var restored = new BmsReplayFrame();
            restored.FromLegacy(legacy, beatmap);
            Assert.Multiple(() =>
            {
                Assert.That(lanes, Has.Length.EqualTo(16));
                Assert.That((int)(legacy.MouseX ?? 0), Is.EqualTo(expectedMask));
                Assert.That((float)expectedMask, Is.EqualTo(legacy.MouseX));
                Assert.That(restored.Actions, Is.EquivalentTo(actions));
                Assert.That(restored.InputActions, Is.EquivalentTo(actions));
                Assert.That(restored.InputActions.Where(action => action.IsLaneAction()), Is.EquivalentTo(lanes));
                Assert.That(restored.LaneActionMask, Is.EqualTo(0xffff));
                Assert.That(restored.InputActionMask, Is.EqualTo(expectedMask));
            });
        }

        [TestCase(BmsAction.Scratch1Motion, 20)]
        [TestCase(BmsAction.Scratch2Motion, 21)]
        public void TestMotionLevelIsReplayInputAndNeverALaneAttack(BmsAction action, int bit)
        {
            var beatmap = new BmsBeatmap();
            var frame = new BmsReplayFrame(321, action);
            var restored = new BmsReplayFrame();
            restored.FromLegacy(frame.ToLegacy(beatmap), beatmap);
            Assert.Multiple(() =>
            {
                Assert.That(restored.InputActions, Is.EqualTo(new[] { action }));
                Assert.That(restored.InputActions.Any(action => action.IsLaneAction()), Is.False);
                Assert.That(restored.LaneActionMask, Is.Zero);
                Assert.That(restored.InputActionMask, Is.EqualTo(1 << bit));
            });
        }

        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        [TestCase(19)]
        [TestCase(22)]
        [TestCase(23)]
        public void TestLegacyUnassignedBitsRemainIgnored(int bit)
        {
            var frame = new BmsReplayFrame();
            frame.FromLegacy(new LegacyReplayFrame(0, 1 << bit, null, ReplayButtonState.None), new BmsBeatmap());
            Assert.Multiple(() =>
            {
                Assert.That(frame.Actions, Is.Empty);
                Assert.That(frame.InputActions, Is.Empty);
                Assert.That(frame.LaneActionMask, Is.Zero);
            });
        }

        [Test]
        public void TestRecordingCopiesLiveGameplayActionsAndExcludesUiState()
        {
            var live = new List<BmsAction> { BmsAction.Key1, BmsAction.Scratch1Motion, BmsAction.LaneCoverFocus, BmsAction.PreStartHold };
            var frame = new BmsReplayFrame(123, live);
            live.Clear();
            Assert.That(frame.Actions, Is.EqualTo(new[] { BmsAction.Key1, BmsAction.Scratch1Motion }));
            Assert.That(frame.InputActions, Is.EqualTo(frame.Actions));
            Assert.That(frame.InputActionMask, Is.EqualTo((1 << (int)BmsAction.Key1) | (1 << 20)));
        }
    }
}
