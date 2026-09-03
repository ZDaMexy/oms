// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinEventRuntimeHostTest
    {
        [Test]
        public void TestBgaViewportAlwaysComesFromExactLayout()
        {
            GameplaySkinLayoutRect first = GameplaySkinLayoutRect.Create(0.1f, 0.1f, 0.3f, 0.4f);
            GameplaySkinLayoutRect second = GameplaySkinLayoutRect.Create(0.6f, 0.1f, 0.3f, 0.4f);

            Assert.Multiple(() =>
            {
                Assert.That(
                    GameplaySkinEventRuntimeHost.ResolveBgaViewport(new[] { first, second }, 0),
                    Is.EqualTo(first));
                Assert.That(
                    GameplaySkinEventRuntimeHost.ResolveBgaViewport(new[] { first, second }, 1),
                    Is.EqualTo(second));
            });
        }

        [Test]
        public void TestBgaViewportRejectsEveryNonPublishedIndex()
        {
            GameplaySkinLayoutRect viewport = GameplaySkinLayoutRect.Create(0.1f, 0.1f, 0.8f, 0.8f);

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => GameplaySkinEventRuntimeHost.ResolveBgaViewport(Array.Empty<GameplaySkinLayoutRect>(), -1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(
                    () => GameplaySkinEventRuntimeHost.ResolveBgaViewport(Array.Empty<GameplaySkinLayoutRect>(), 0),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(
                    () => GameplaySkinEventRuntimeHost.ResolveBgaViewport(Array.Empty<GameplaySkinLayoutRect>(), 1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(
                    () => GameplaySkinEventRuntimeHost.ResolveBgaViewport(new[] { viewport }, 1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSnapshotProgressUsesAuthoritativeTimeWithoutChangingEngineState()
        {
            GameplaySkinLaneGroupId groupId = GameplaySkinLaneGroupId.Create("stage-0");
            GameplaySkinLaneId laneId = GameplaySkinLaneId.Create("lane-0");
            var holding = new GameplaySkinObjectStateSnapshot(
                17,
                GameplaySkinObjectKind.LongNote,
                GameplaySkinObjectState.Holding,
                groupId,
                laneId,
                100,
                500,
                1);

            GameplaySkinObjectStateSnapshot rewound = GameplaySkinEventRuntimeHost.SnapshotObjectAt(holding, 300);

            Assert.Multiple(() =>
            {
                Assert.That(rewound.State, Is.EqualTo(GameplaySkinObjectState.Holding));
                Assert.That(rewound.Progress, Is.EqualTo(0.5));
                Assert.That(rewound.StartTime, Is.EqualTo(100));
                Assert.That(rewound.EndTime, Is.EqualTo(500));
            });
        }

        [Test]
        public void TestInstantObjectProgressChangesAtAuthoritativeStartTime()
        {
            GameplaySkinLaneGroupId groupId = GameplaySkinLaneGroupId.Create("stage-0");
            GameplaySkinLaneId laneId = GameplaySkinLaneId.Create("lane-0");
            var note = new GameplaySkinObjectStateSnapshot(
                17,
                GameplaySkinObjectKind.Note,
                GameplaySkinObjectState.Visible,
                groupId,
                laneId,
                100,
                100,
                0);

            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinEventRuntimeHost.SnapshotObjectAt(note, 99).Progress, Is.Zero);
                Assert.That(GameplaySkinEventRuntimeHost.SnapshotObjectAt(note, 100).Progress, Is.EqualTo(1));
                Assert.That(GameplaySkinEventRuntimeHost.SnapshotObjectAt(note, 101).Progress, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestGenericTimingProjectionIsCumulativeAcrossTempoAndSignatureChanges()
        {
            var beatmap = new Beatmap();
            var first = new TimingControlPoint
            {
                BeatLength = 500,
                TimeSignature = TimeSignature.SimpleQuadruple,
            };
            var second = new TimingControlPoint
            {
                BeatLength = 250,
                TimeSignature = TimeSignature.SimpleTriple,
            };

            beatmap.ControlPointInfo.Add(0, first);
            beatmap.ControlPointInfo.Add(2000, second);
            beatmap.ControlPointInfo.Add(2500, new EffectControlPoint { ScrollSpeed = 2 });

            var projection = new GameplaySkinBeatmapTimingProjection(beatmap);
            GameplaySkinTimingStateSnapshot beforeChange = projection.Sample(1750);
            GameplaySkinTimingStateSnapshot afterChange = projection.Sample(2250);
            GameplaySkinTimingStateSnapshot nextMeasure = projection.Sample(2750);

            // The projection owns a defensive copy; later editor/control-point mutation cannot split this publication.
            second.BeatLength = 1000;
            GameplaySkinTimingStateSnapshot repeated = projection.Sample(2250);

            Assert.Multiple(() =>
            {
                Assert.That(beforeChange.Beat, Is.EqualTo(3.5));
                Assert.That(beforeChange.BarIndex, Is.Zero);
                Assert.That(afterChange.Beat, Is.EqualTo(5));
                Assert.That(afterChange.BarIndex, Is.EqualTo(1));
                Assert.That(afterChange.Bpm, Is.EqualTo(240));
                Assert.That(afterChange.ScrollMultiplier, Is.EqualTo(1));
                Assert.That(nextMeasure.Beat, Is.EqualTo(7));
                Assert.That(nextMeasure.BarIndex, Is.EqualTo(2));
                Assert.That(nextMeasure.ScrollMultiplier, Is.EqualTo(2));
                Assert.That(repeated.Beat, Is.EqualTo(afterChange.Beat));
                Assert.That(repeated.BarIndex, Is.EqualTo(afterChange.BarIndex));
                Assert.That(repeated.Bpm, Is.EqualTo(afterChange.Bpm));
            });
        }
    }
}
