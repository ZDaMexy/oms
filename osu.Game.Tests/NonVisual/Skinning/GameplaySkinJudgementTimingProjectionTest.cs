// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinJudgementTimingProjectionTest
    {
        [Test]
        public void TestSameTimeHintsKeepIndependentKeyAndScratchValuesInDeliveryOrder()
        {
            var fast = new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Fast, 100);
            var slow = new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Slow, 100);
            GameplaySkinJudgementTimingStateSnapshot state = default;

            state = state.Apply(GameplaySkinLaneRole.Key, fast).Apply(GameplaySkinLaneRole.Scratch, slow);
            Assert.Multiple(() =>
            {
                Assert.That(state.Combined?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow));
                Assert.That(state.Keys?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Fast));
                Assert.That(state.Scratch?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow));
                Assert.That(state.Keys?.AppliedTime, Is.EqualTo(100));
                Assert.That(state.Scratch?.AppliedTime, Is.EqualTo(100));
            });

            state = state.Apply(GameplaySkinLaneRole.SpecialKey, fast);
            Assert.Multiple(() =>
            {
                Assert.That(state.Combined?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Fast));
                Assert.That(state.Keys?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Fast));
                Assert.That(state.Scratch?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow),
                    "A special key retains key semantics and never overwrites scratch timing.");
            });
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TestHintRequiresRealGameplayTime(double time)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Fast, time));

        [Test]
        public void TestNoHintIsDistinctFromAnAlreadyQualifiedDirection()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.None, 0));
            var unmodifiedResult = new JudgementResult(new HitObject(), new Judgement());
            Assert.That(unmodifiedResult, Is.Not.InstanceOf<IGameplaySkinJudgementTimingMetadata>(),
                "Rulesets without an explicit timing provenance source remain unavailable.");
        }
    }
}
