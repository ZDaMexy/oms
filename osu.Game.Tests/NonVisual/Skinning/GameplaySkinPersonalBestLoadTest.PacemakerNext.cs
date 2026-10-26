// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public partial class GameplaySkinPersonalBestLoadTest
    {
        [TestCase("score.pacemaker-next")]
        [TestCase("score.pacemaker-next-progress")]
        [TestCase("score.pacemaker-next-label")]
        [TestCase("score.pacemaker-next-available")]
        public void TestNextOnlyConsumerLoadsActualMatchedBestWhileManiaTargetRemainsUnavailable(string consumer)
        {
            AddStep("save an actual mania score with its scored maximum statistics", () =>
            {
                ScoreInfo played = createScoredModel(HitResult.Great);
                expected = played.TotalScore;
                Assert.That(expected, Is.GreaterThan(0));
                save(played);
            });
            mount(consumer);
            AddStep("verify production-loaded fields without any graph option", () =>
            {
                Assert.That(readPersonalBest(), Is.EqualTo(expected));
                using GameplaySkinEventSubscription subscription = loaded.Events.EventStream.Subscribe();
                Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
                GameplaySkinEventStateSnapshot state = ((GameplaySkinStateEventPayload)envelope!.Payload).State;
                GameplaySkinChartPresentation chart = state.ChartPresentation!;
                Assert.That(chart.UsesExScore, Is.False);
                Assert.That(state.Score.MaximumScore, Is.GreaterThan(0));
                GameplaySkinPacemakerNextTarget target = GameplaySkinPacemakerNext.Calculate(chart.UsesExScore,
                    state.Score.MaximumScore, chart.PersonalBestScore);
                Assert.That(target.Available, Is.False, "A genuine mania PB does not create an EX-score target.");
                Assert.That(target.Label, Is.Empty);
                database!.Dispose();
            });
            AddRepeatStep("advance without another saved-score query", () => loaded.ClockHost.Sample(1500), 3);
            AddStep("retain the matched numeric PB through reset and late attachment", () =>
            {
                loaded.Events.RequestReset(GameplaySkinEventResetReason.ConsumerRebuilt);
                loaded.ClockHost.Sample(1500);
                Assert.That(readPersonalBest(), Is.EqualTo(expected));
            });
        }
    }
}
