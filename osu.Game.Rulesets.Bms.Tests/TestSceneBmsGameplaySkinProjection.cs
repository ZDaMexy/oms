// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsGameplaySkinProjection : PlayerTestScene
    {
        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };
        private readonly FramedClock referenceClock;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        public TestSceneBmsGameplaySkinProjection()
        {
            referenceClock = new FramedClock(manualClock);
        }

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("""
                #TITLE Typed skin projection epoch
                #BPM 120
                #LNTYPE 1
                #WAV01 note.wav
                #00151:01000100
                #00811:01
                """, "typed-skin-projection.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset },
            };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        [TestCase(BmsLongNoteMode.LN, GameplaySkinLongNoteMode.Standard)]
        [TestCase(BmsLongNoteMode.CN, GameplaySkinLongNoteMode.Charge)]
        [TestCase(BmsLongNoteMode.HCN, GameplaySkinLongNoteMode.HellCharge)]
        public void TestActualPlayerSeekAndRewindRetainNativeLongNoteModeAndGaugeState(BmsLongNoteMode mode, GameplaySkinLongNoteMode expected)
        {
            DrawableBmsRuleset drawable = null!;
            GameplaySkinEventSubscription subscription = null!;
            long epoch = -1;
            AddStep("load actual player with the selected long-note mode", () => LoadPlayer(mode switch
            {
                BmsLongNoteMode.LN => Array.Empty<Mod>(),
                BmsLongNoteMode.CN => new Mod[] { new BmsModChargeNote() },
                BmsLongNoteMode.HCN => new Mod[] { new BmsModHellChargeNote() },
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            }));
            AddUntilStep("actual BMS player and read-only runtime loaded", () =>
            {
                if (Player?.IsLoaded != true || Player.DrawableRuleset is not DrawableBmsRuleset loaded
                    || loaded.GameplaySkinEventRuntime == null)
                    return false;

                drawable = loaded;
                return loaded.GameplaySkinEventStream.CurrentEpoch > 0;
            });
            AddStep("attach to the player-owned stream", () =>
            {
                subscription = drawable.GameplaySkinEventStream.Subscribe();
                var attached = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(attached.Add);
                epoch = attached.Single().Epoch;
                Assert.That(((BmsBeatmap)drawable.Beatmap).HitObjects.OfType<BmsHoldNote>().Single().LongNoteMode, Is.EqualTo(mode));
            });

            assertDestination(2200, GameplaySkinEventResetReason.Seek);
            assertDestination(2400, GameplaySkinEventResetReason.Seek);
            assertDestination(2250, GameplaySkinEventResetReason.Rewind);
            AddStep("detach actual player projection consumer", () => subscription.Dispose());

            void assertDestination(double destination, GameplaySkinEventResetReason reason)
            {
                AddStep($"seek actual gameplay clock to {destination}", () => Player.GameplayClockContainer.Seek(destination));
                AddUntilStep("one player-owned discontinuity epoch committed", () => drawable.GameplaySkinEventStream.CurrentEpoch == epoch + 1);
                AddStep("mode and actual gauge semantics survive the complete reset", () =>
                {
                    var events = new List<GameplaySkinEventEnvelope>();
                    subscription.DrainFrame(events.Add);
                    GameplaySkinEventEnvelope reset = events.Single(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset);
                    var payload = (GameplaySkinStateEventPayload)reset.Payload;
                    GameplaySkinObjectStateSnapshot hold = payload.State.ActiveObjects.Single(obj => obj.Kind == GameplaySkinObjectKind.LongNote);
                    var gauge = (BmsGaugeProcessor)Player.HealthProcessor;
                    double progress = Math.Clamp((reset.GameplayTime - hold.StartTime) / (hold.EndTime - hold.StartTime), 0, 1);
                    Assert.Multiple(() =>
                    {
                        Assert.That(payload.ResetReason, Is.EqualTo(reason));
                        Assert.That(hold.LongNoteMode, Is.EqualTo(expected));
                        Assert.That(hold.Progress, Is.EqualTo(progress).Within(1e-9));
                        Assert.That(payload.State.Score.GaugeState, Is.EqualTo(((IGameplaySkinGaugeStateSource)gauge).GameplaySkinGaugeState));
                        Assert.That(payload.State.Score.GaugeState.Type, Is.EqualTo(GameplaySkinGaugeType.Normal));
                    });
                    epoch = reset.Epoch;

                    using GameplaySkinEventSubscription late = drawable.GameplaySkinEventStream.Subscribe();
                    var lateEvents = new List<GameplaySkinEventEnvelope>();
                    late.DrainFrame(lateEvents.Add);
                    Assert.That(((GameplaySkinStateEventPayload)lateEvents.Single().Payload).State.ActiveObjects
                        .Single(obj => obj.ObjectId == hold.ObjectId).LongNoteMode, Is.EqualTo(expected));
                });
            }
        }
    }
}
