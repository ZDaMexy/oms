// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsGameplaySkinTimingEpoch : PlayerTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };
        private readonly FramedClock referenceClock;
        private readonly BmsDecodedBeatmap sourceBeatmap;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        public TestSceneBmsGameplaySkinTimingEpoch()
        {
            referenceClock = new FramedClock(manualClock);

            var decodedChart = new BmsBeatmapDecoder().DecodeText(@"
#TITLE Gameplay Skin Timing Epoch
#BPM 120
#LNTYPE 1
#BPMAA 240
#STOPAB 96
#SCROLLAC 0.5
#BMP01 bga-1.png
#BMP02 bga-2.png
#BMP03 bga-3.png
#BMP04 bga-4.png
#00102:0.5
#001SC:AC00
#00108:00AA
#00109:000000AB
#00104:01020304
#00111:0000CC00
#00121:0000CC00
#00151:EE00FF00
#00611:DD00
", "gameplay-skin-timing-epoch.bme");

            sourceBeatmap = new BmsDecodedBeatmap(decodedChart)
            {
                BeatmapInfo =
                {
                    Ruleset = bms_ruleset_info,
                }
            };
        }

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => sourceBeatmap;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        [Test]
        public void TestProductionClockPublishesBeatBarBpmStopAndScroll()
        {
            GameplaySkinEventSubscription subscription = null!;
            var observed = new List<GameplaySkinEventEnvelope>();

            AddUntilStep("production BMS event runtime ready", productionRuntimeReady);
            AddStep("attach production timing consumer", () =>
            {
                subscription = drawableRuleset.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observed.Add);
                Assert.That(observed.Single().DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
            });

            advanceTrackTo(600);
            AddUntilStep("authoritative clock produces beat", () =>
            {
                subscription.DrainFrame(observed.Add);
                return observed.Any(e => e.EventKind == GameplaySkinEventKind.TimingBeat && e.GameplayTime >= 500);
            });

            advanceTrackTo(2100);
            AddUntilStep("bar and SCROLL change reach production stream", () =>
            {
                subscription.DrainFrame(observed.Add);
                return observed.Any(e => e.EventKind == GameplaySkinEventKind.TimingBar)
                       && timingStates(observed, GameplaySkinEventKind.TimingScrollChanged)
                       .Any(state => state.ScrollMultiplier > 0 && state.ScrollMultiplier < 1);
            });

            advanceTrackTo(2550);
            AddUntilStep("real BPM change reaches production stream", () =>
            {
                subscription.DrainFrame(observed.Add);
                return timingStates(observed, GameplaySkinEventKind.TimingBpmChanged)
                       .Any(state => state.Bpm == 240);
            });

            advanceTrackTo(2700);
            AddUntilStep("STOP start and frozen scroll reach production stream", () =>
            {
                subscription.DrainFrame(observed.Add);
                return timingStates(observed, GameplaySkinEventKind.TimingStopStarted).Any(state => state.IsStopped)
                       && timingStates(observed, GameplaySkinEventKind.TimingScrollChanged)
                          .Any(state => state.IsStopped && state.ScrollMultiplier <= 1e-8);
            });

            advanceTrackTo(3200);
            AddUntilStep("STOP end reaches production stream", () =>
            {
                subscription.DrainFrame(observed.Add);
                return timingStates(observed, GameplaySkinEventKind.TimingStopEnded).Any(state => !state.IsStopped);
            });

            AddStep("timing envelopes retain exact publication and monotonic sequence", () =>
            {
                GameplaySkinEventEnvelope[] edges = observed.Where(e => e.DeliveryKind == GameplaySkinEventDeliveryKind.Edge).ToArray();
                GameplaySkinEventRevision exactRevision = drawableRuleset.LayoutProvider.RevisionOwner!.CurrentPublication!.EventRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(edges, Is.Not.Empty);
                    Assert.That(edges.All(e => e.Revision == exactRevision), Is.True);
                    Assert.That(edges.Select(e => e.Sequence), Is.Ordered.Ascending);
                    Assert.That(edges.Select(e => e.Sequence).Distinct().Count(), Is.EqualTo(edges.Length));
                    Assert.That(edges.All(e => e.GameplayTime >= 0 && e.GameplayTime <= Player.GameplayClockContainer.CurrentTime + 100), Is.True);
                });
            });
            AddStep("detach production timing consumer", () => subscription.Dispose());
        }

        [Test]
        public void TestStopSentinelNeverBecomesPublicBpm()
        {
            GameplaySkinEventSubscription subscription = null!;
            GameplaySkinEventSubscription late = null!;
            var observed = new List<GameplaySkinEventEnvelope>();
            GameplaySkinEventEnvelope lateSnapshot = null!;
            long beforeStopEpoch = -1;

            AddUntilStep("production BMS event runtime ready", productionRuntimeReady);
            AddStep("seek through production clock to real BPM region", () => Player.GameplayClockContainer.Seek(2550));
            AddUntilStep("real BPM seek committed", () => Player.DrawableRuleset.FrameStableClock.CurrentTime >= 2500
                                                           && drawableRuleset.GameplaySkinEventStream.CurrentEpoch >= 2);
            AddStep("attach after real BPM transition", () =>
            {
                subscription = drawableRuleset.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observed.Add);
                var snapshot = (GameplaySkinStateEventPayload)observed.Single().Payload;
                beforeStopEpoch = observed[0].Epoch;

                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.State.Timing.Bpm, Is.EqualTo(240));
                    Assert.That(snapshot.State.Timing.IsStopped, Is.False);
                });
            });
            AddStep("seek production clock into STOP", () => Player.GameplayClockContainer.Seek(2700));
            AddUntilStep("STOP seek committed", () => drawableRuleset.GameplaySkinEventStream.CurrentEpoch == beforeStopEpoch + 1
                                                       && Player.DrawableRuleset.FrameStableClock.CurrentTime >= 2650);
            AddStep("drain atomic STOP reset", () =>
            {
                subscription.DrainFrame(observed.Add);
                GameplaySkinEventEnvelope reset = observed.Last(e => e.DeliveryKind == GameplaySkinEventDeliveryKind.Reset);
                var state = (GameplaySkinStateEventPayload)reset.Payload;

                Assert.Multiple(() =>
                {
                    Assert.That(state.ResetReason, Is.EqualTo(GameplaySkinEventResetReason.Seek));
                    Assert.That(state.State.Timing.Bpm, Is.EqualTo(240));
                });
            });
            AddStep("late attach during STOP receives complete current snapshot", () =>
            {
                late = drawableRuleset.GameplaySkinEventStream.Subscribe();
                var lateEvents = new List<GameplaySkinEventEnvelope>();
                late.DrainFrame(lateEvents.Add);
                lateSnapshot = lateEvents.Single();
            });
            AddStep("STOP freeze sentinel is not projected as BPM 10000", () =>
            {
                var snapshot = (GameplaySkinStateEventPayload)lateSnapshot.Payload;

                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.State.Timing.IsStopped, Is.True);
                    Assert.That(snapshot.State.Timing.Bpm, Is.EqualTo(240),
                        "BmsStopFreezeTimingControlPoint is a scroll-freeze sentinel, not a real BPM authority.");
                    Assert.That(observed.SelectMany(envelope => envelope.Payload switch
                    {
                        GameplaySkinTimingEventPayload timing => new[] { timing.State },
                        GameplaySkinStateEventPayload state => new[] { state.State.Timing },
                        _ => System.Array.Empty<GameplaySkinTimingStateSnapshot>(),
                    }).All(state => state.Bpm != 10000), Is.True,
                        "The read-only public event stream must never expose the STOP sentinel as a BPM change.");
                });
            });
            AddStep("detach STOP consumers", () =>
            {
                late.Dispose();
                subscription.Dispose();
            });
        }

        [Test]
        public void TestSeekRewindRetryResetAtomicallyAndLateAttachIsComplete()
        {
            GameplaySkinEventSubscription subscription = null!;
            GameplaySkinEventSubscription late = null!;
            var observed = new List<GameplaySkinEventEnvelope>();
            long initialEpoch = -1;
            long baseEpoch = -1;
            GameplaySkinEventRevision exactRevision = default;

            AddUntilStep("production BMS event runtime ready", productionRuntimeReady);
            AddStep("attach epoch consumer", () =>
            {
                subscription = drawableRuleset.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observed.Add);
                GameplaySkinEventEnvelope initial = observed.Single();
                initialEpoch = initial.Epoch;
                exactRevision = initial.Revision;
            });

            AddStep("normalise production clock into running chart", () => Player.GameplayClockContainer.Seek(700));
            AddUntilStep("normalisation reset committed", () => drawableRuleset.GameplaySkinEventStream.CurrentEpoch > initialEpoch);
            AddStep("drain normalisation reset", () =>
            {
                subscription.DrainFrame(observed.Add);
                baseEpoch = drawableRuleset.GameplaySkinEventStream.CurrentEpoch;
            });
            AddStep("queue real input edge in old epoch", () =>
                Assert.That(drawableRuleset.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddUntilStep("old epoch has queued input edge", () => subscription.PendingCount > 0);
            AddStep("seek forward with old edge still queued", () => Player.GameplayClockContainer.Seek(1200));
            AddUntilStep("seek epoch committed", () => drawableRuleset.GameplaySkinEventStream.CurrentEpoch == baseEpoch + 1);
            AddStep("seek reset atomically replaces old queue", () => assertAtomicReset(
                subscription, observed, GameplaySkinEventResetReason.Seek, baseEpoch + 1));

            AddStep("queue release edge in seek epoch", () =>
                Assert.That(drawableRuleset.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddUntilStep("seek epoch has queued input edge", () => subscription.PendingCount > 0);
            AddStep("rewind with seek-epoch edge still queued", () => Player.GameplayClockContainer.Seek(500));
            AddUntilStep("rewind epoch committed", () => drawableRuleset.GameplaySkinEventStream.CurrentEpoch == baseEpoch + 2);
            AddStep("rewind reset atomically replaces old queue", () => assertAtomicReset(
                subscription, observed, GameplaySkinEventResetReason.Rewind, baseEpoch + 2));

            AddStep("queue press edge in rewind epoch", () =>
                Assert.That(drawableRuleset.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddUntilStep("rewind epoch has queued input edge", () => subscription.PendingCount > 0);
            AddStep("retry with rewind-epoch edge still queued", () => Player.GameplayClockContainer.Reset(0));
            AddUntilStep("retry epoch committed", () => drawableRuleset.GameplaySkinEventStream.CurrentEpoch == baseEpoch + 3);
            AddStep("retry reset atomically replaces old queue", () => assertAtomicReset(
                subscription, observed, GameplaySkinEventResetReason.Retry, baseEpoch + 3));

            AddStep("late attach after retry receives complete deterministic snapshot", () =>
            {
                subscription.DrainFrame(observed.Add);
                late = drawableRuleset.GameplaySkinEventStream.Subscribe();
                var attached = new List<GameplaySkinEventEnvelope>();
                late.DrainFrame(attached.Add);
                GameplaySkinEventEnvelope snapshotEnvelope = attached.Single();
                var snapshot = (GameplaySkinStateEventPayload)snapshotEnvelope.Payload;
                GameplaySkinLayoutSnapshot layout = drawableRuleset.LayoutSnapshot.Neutral;
                long currentSequence = observed.Where(e => e.Epoch == snapshotEnvelope.Epoch).Max(e => e.Sequence);

                Assert.Multiple(() =>
                {
                    Assert.That(snapshotEnvelope.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                    Assert.That(snapshotEnvelope.Epoch, Is.EqualTo(baseEpoch + 3));
                    Assert.That(snapshotEnvelope.Epoch, Is.EqualTo(drawableRuleset.GameplaySkinEventStream.CurrentEpoch));
                    Assert.That(snapshotEnvelope.Sequence, Is.EqualTo(currentSequence));
                    Assert.That(snapshotEnvelope.Revision, Is.EqualTo(exactRevision));
                    Assert.That(snapshot.State.Inputs.Select(input => input.LaneId),
                        Is.EquivalentTo(layout.Context.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id)));
                    Assert.That(snapshot.State.ActiveObjects.Select(obj => obj.ObjectId), Is.Ordered.Ascending);
                    Assert.That(snapshot.State.BgaViewports.Select(viewport => viewport.ViewportIndex), Is.Ordered.Ascending);
                    Assert.That(snapshot.State.Timing.Bpm, Is.GreaterThan(0));
                });
            });
            AddStep("every retained post-reset envelope belongs to its new epoch", () =>
            {
                GameplaySkinEventEnvelope[] resets = observed.Where(e => e.DeliveryKind == GameplaySkinEventDeliveryKind.Reset).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(resets.TakeLast(3).Select(e => e.Epoch), Is.EqualTo(new[] { baseEpoch + 1, baseEpoch + 2, baseEpoch + 3 }));
                    Assert.That(resets.Select(e => e.Sequence), Is.All.Zero);
                    Assert.That(resets.All(e => e.Revision == exactRevision), Is.True);
                    Assert.That(observed.Skip(1).Where(e => e.DeliveryKind != GameplaySkinEventDeliveryKind.Reset)
                                        .GroupBy(e => e.Epoch)
                                        .All(group => group.Select(e => e.Sequence).SequenceEqual(group.Select(e => e.Sequence).Order())), Is.True);
                });
            });
            AddStep("detach epoch consumers", () =>
            {
                late.Dispose();
                subscription.Dispose();
            });
        }

        [Test]
        public void TestBgaRewindFoldsLowerRevisionIntoOneFourViewportReset()
        {
            GameplaySkinEventSubscription subscription = null!;
            long initialEpoch = -1;
            long highEpoch = -1;
            long highRevision = -1;
            long expectedHighRevision = -1;
            double highTime = double.NaN;
            double lowTime = double.NaN;

            AddUntilStep("production 14K BGA event runtime ready", () =>
                productionRuntimeReady()
                && drawableRuleset.LayoutSnapshot.BgaViewports.Count == 4
                && drawableRuleset.ChildrenOfType<BmsBgaPanel>().Single().ChildrenOfType<BmsBgaPlayer>().Count() == 4);
            AddStep("attach four-viewport BGA consumer", () =>
            {
                subscription = drawableRuleset.GameplaySkinEventStream.Subscribe();
                var initial = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(initial.Add);
                initialEpoch = initial.Single().Epoch;

                var beatmap = (BmsBeatmap)drawableRuleset.Beatmap;
                expectedHighRevision = beatmap.BgaTimeline.Count;
                highTime = beatmap.BgaTimeline.Last().StartTime + 100;
                lowTime = (beatmap.BgaTimeline[0].StartTime + beatmap.BgaTimeline[1].StartTime) / 2;
            });
            AddStep("seek authoritative gameplay clock past all BGA entries", () => Player.GameplayClockContainer.Seek(highTime));
            AddUntilStep("high epoch and all real BGA players select the fourth timeline entry", () =>
            {
                BmsBgaPanel panel = drawableRuleset.ChildrenOfType<BmsBgaPanel>().Single();
                return drawableRuleset.GameplaySkinEventStream.CurrentEpoch >= initialEpoch + 1
                       && Enumerable.Range(0, 4).All(index =>
                    panel.TryGetContentState(index, out _, out long revision) && revision == expectedHighRevision);
            });
            AddStep("capture high BGA revision for all real viewports", () =>
            {
                subscription.DrainFrame(_ => { });
                using GameplaySkinEventSubscription late = drawableRuleset.GameplaySkinEventStream.Subscribe();
                var snapshotEvents = new List<GameplaySkinEventEnvelope>();
                late.DrainFrame(snapshotEvents.Add);
                GameplaySkinEventEnvelope snapshot = snapshotEvents.Single();
                GameplaySkinBgaStateSnapshot[] viewports = ((GameplaySkinStateEventPayload)snapshot.Payload).State.BgaViewports.ToArray();
                highEpoch = snapshot.Epoch;
                highRevision = viewports[0].ContentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(highEpoch, Is.GreaterThanOrEqualTo(initialEpoch + 1));
                    Assert.That(viewports, Has.Length.EqualTo(4));
                    Assert.That(viewports.Select(viewport => viewport.ViewportIndex), Is.EqualTo(new[] { 0, 1, 2, 3 }));
                    Assert.That(viewports.Select(viewport => viewport.ContentRevision), Is.All.EqualTo(highRevision));
                    Assert.That(highRevision, Is.EqualTo(expectedHighRevision));
                    Assert.That(highRevision, Is.GreaterThan(1));
                });
            });
            AddStep("rewind into the first BGA timeline entry", () => Player.GameplayClockContainer.Seek(lowTime));
            AddUntilStep("lower BGA revision commits through one new epoch", () =>
                drawableRuleset.GameplaySkinEventStream.CurrentEpoch >= highEpoch + 1);
            AddStep("one reset atomically carries all four lowered P1-L summaries", () =>
            {
                var events = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(events.Add);
                GameplaySkinEventEnvelope reset = events.Single(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset);
                var state = (GameplaySkinStateEventPayload)reset.Payload;
                GameplaySkinBgaStateSnapshot[] viewports = state.State.BgaViewports.ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(events[0], Is.SameAs(reset),
                        "No lower-revision BGA edge may escape ahead of the complete discontinuity reset.");
                    Assert.That(reset.Epoch, Is.EqualTo(highEpoch + 1));
                    Assert.That(reset.Sequence, Is.Zero);
                    Assert.That(state.ResetReason, Is.EqualTo(GameplaySkinEventResetReason.Rewind));
                    Assert.That(viewports, Has.Length.EqualTo(4));
                    Assert.That(viewports.Select(viewport => viewport.ViewportIndex), Is.EqualTo(new[] { 0, 1, 2, 3 }));
                    Assert.That(viewports.Select(viewport => viewport.ContentRevision), Is.All.EqualTo(1));
                    Assert.That(viewports.All(viewport => viewport.ContentRevision < highRevision), Is.True);
                });
            });
            AddStep("detach BGA rewind consumer", () => subscription.Dispose());
        }

        [Test]
        public void TestSeekAndRewindObjectProgressUsesEnvelopeGameplayTime()
        {
            GameplaySkinEventSubscription subscription = null!;
            long epoch = -1;

            AddUntilStep("production BMS event runtime ready", productionRuntimeReady);
            AddStep("attach object-time consumer", () =>
            {
                subscription = drawableRuleset.GameplaySkinEventStream.Subscribe();
                var initial = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(initial.Add);
                epoch = initial.Single().Epoch;
            });

            assertSeekSnapshotProgress(2200, GameplaySkinEventResetReason.Seek, () => ++epoch, () => subscription);
            assertSeekSnapshotProgress(2400, GameplaySkinEventResetReason.Seek, () => ++epoch, () => subscription);
            assertSeekSnapshotProgress(2250, GameplaySkinEventResetReason.Rewind, () => ++epoch, () => subscription);

            AddStep("detach object-time consumer", () => subscription.Dispose());
        }

        [Test]
        public void TestTerminalLongNoteStateRewindUsesResetTimeAndAllowsRulesetResynchronisation()
        {
            GameplaySkinEventSubscription subscription = null!;
            var terminalEvents = new List<GameplaySkinEventEnvelope>();
            long epoch = -1;
            long objectId = -1;
            BmsHoldNote hold = null!;
            DrawableBmsHoldNote drawableHold = null!;
            GameplaySkinLaneGroupId groupId = null!;
            GameplaySkinLaneId laneId = null!;

            AddUntilStep("production BMS event runtime ready", productionRuntimeReady);
            AddStep("attach terminal rewind consumer", () =>
            {
                subscription = drawableRuleset.GameplaySkinEventStream.Subscribe();
                var initial = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(initial.Add);
                epoch = initial.Single().Epoch;
                hold = drawableRuleset.Beatmap.HitObjects.OfType<BmsHoldNote>().Single();
                objectId = drawableRuleset.GetGameplaySkinObjectId(hold);
            });
            AddStep("seek real player into active long note", () => Player.GameplayClockContainer.Seek(2400));
            AddUntilStep("active seek and real long-note drawable are committed", () =>
            {
                drawableHold = drawableRuleset.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>()
                                               .SingleOrDefault(candidate => ReferenceEquals(candidate.HitObject, hold))!;
                return drawableRuleset.GameplaySkinEventStream.CurrentEpoch == epoch + 1
                       && drawableHold != null;
            });
            AddStep("apply terminal result to real production long-note drawable", () =>
            {
                drawableHold.HitForcefully();
                Assert.Multiple(() =>
                {
                    Assert.That(drawableHold.Judged, Is.True);
                    Assert.That(drawableHold.IsHit, Is.True);
                });
            });
            AddUntilStep("real long-note publishes terminal state", () =>
            {
                subscription.DrainFrame(terminalEvents.Add);
                return terminalEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectStateChanged
                                                      && envelope.Payload is GameplaySkinObjectEventPayload payload
                                                      && payload.State.ObjectId == objectId
                                                      && payload.State.State is GameplaySkinObjectState.Hit or GameplaySkinObjectState.Completed);
            });
            AddStep("capture terminal identity and immediately rewind", () =>
            {
                GameplaySkinObjectStateSnapshot terminal = ((GameplaySkinObjectEventPayload)terminalEvents.Last(envelope =>
                    envelope.EventKind == GameplaySkinEventKind.ObjectStateChanged
                    && envelope.Payload is GameplaySkinObjectEventPayload payload
                    && payload.State.ObjectId == objectId
                    && payload.State.State is GameplaySkinObjectState.Hit or GameplaySkinObjectState.Completed).Payload).State;

                groupId = terminal.GroupId;
                laneId = terminal.LaneId!;

                Assert.Multiple(() =>
                {
                    Assert.That(terminal.State, Is.EqualTo(GameplaySkinObjectState.Hit).Or.EqualTo(GameplaySkinObjectState.Completed));
                    Assert.That(terminal.Progress, Is.EqualTo(1));
                });

                Player.GameplayClockContainer.Seek(2250);
            });
            AddUntilStep("terminal rewind epoch committed", () => drawableRuleset.GameplaySkinEventStream.CurrentEpoch == epoch + 2);
            AddStep("reset is rebuilt from the real active long-note drawable", () =>
            {
                var events = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(events.Add);
                GameplaySkinEventEnvelope reset = events.Single(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset);
                var state = (GameplaySkinStateEventPayload)reset.Payload;
                GameplaySkinObjectStateSnapshot rewound = state.State.ActiveObjects.Single(obj => obj.ObjectId == objectId);
                double expectedProgress = (reset.GameplayTime - rewound.StartTime) / (rewound.EndTime - rewound.StartTime);

                Assert.Multiple(() =>
                {
                    Assert.That(state.ResetReason, Is.EqualTo(GameplaySkinEventResetReason.Rewind));
                    Assert.That(reset.GameplayTime, Is.GreaterThanOrEqualTo(2250).And.LessThanOrEqualTo(Player.GameplayClockContainer.CurrentTime));
                    Assert.That(rewound.State, Is.EqualTo(GameplaySkinObjectState.Visible).Or.EqualTo(GameplaySkinObjectState.Holding),
                        "The reset must use the current DrawableBmsHoldNote state, never the prior terminal event state.");
                    Assert.That(rewound.GroupId, Is.EqualTo(groupId));
                    Assert.That(rewound.LaneId, Is.EqualTo(laneId));
                    Assert.That(rewound.Progress, Is.EqualTo(System.Math.Clamp(expectedProgress, 0, 1)).Within(1e-9));
                    Assert.That(rewound.Progress, Is.LessThan(1));
                });
            });
            AddWaitStep("allow real drawable post-rewind update", 3);
            AddStep("late attach observes legal real ruleset state without a second reset", () =>
            {
                var pending = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(pending.Add);
                using GameplaySkinEventSubscription late = drawableRuleset.GameplaySkinEventStream.Subscribe();
                var attached = new List<GameplaySkinEventEnvelope>();
                late.DrainFrame(attached.Add);
                GameplaySkinEventEnvelope snapshot = attached.Single();
                GameplaySkinObjectStateSnapshot resynchronised = ((GameplaySkinStateEventPayload)snapshot.Payload).State.ActiveObjects
                                                                                                           .Single(obj => obj.ObjectId == objectId);

                Assert.Multiple(() =>
                {
                    Assert.That(drawableRuleset.GameplaySkinEventStream.CurrentEpoch, Is.EqualTo(epoch + 2));
                    Assert.That(snapshot.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                    Assert.That(resynchronised.State, Is.Not.EqualTo(GameplaySkinObjectState.Completed));
                    Assert.That(resynchronised.State, Is.Not.EqualTo(GameplaySkinObjectState.Despawned));
                    Assert.That(resynchronised.GroupId, Is.EqualTo(groupId));
                    Assert.That(resynchronised.LaneId, Is.EqualTo(laneId));
                    Assert.That(resynchronised.Progress, Is.LessThan(1));
                    Assert.That(pending.All(envelope => envelope.DeliveryKind != GameplaySkinEventDeliveryKind.Reset), Is.True);
                });
            });
            AddStep("detach terminal rewind consumer", () => subscription.Dispose());
        }

        private DrawableBmsRuleset drawableRuleset => (DrawableBmsRuleset)Player.DrawableRuleset;

        private bool productionRuntimeReady()
            => Player?.IsLoaded == true
               && Player.DrawableRuleset is DrawableBmsRuleset drawable
               && drawable.GameplaySkinEventRuntime?.IsLoaded == true
               && drawable.LayoutProvider.RevisionOwner?.CurrentPublication != null;

        private void advanceTrackTo(double time)
            => AddStep($"advance authoritative track to {time:N0} ms", () =>
            {
                manualClock.CurrentTime = time;
                referenceClock.ProcessFrame();
            });

        private static IEnumerable<GameplaySkinTimingStateSnapshot> timingStates(
            IEnumerable<GameplaySkinEventEnvelope> events,
            GameplaySkinEventKind kind)
            => events.Where(e => e.EventKind == kind)
                     .Select(e => ((GameplaySkinTimingEventPayload)e.Payload).State);

        private static void assertAtomicReset(
            GameplaySkinEventSubscription subscription,
            ICollection<GameplaySkinEventEnvelope> observed,
            GameplaySkinEventResetReason reason,
            long epoch)
        {
            var drained = new List<GameplaySkinEventEnvelope>();
            subscription.DrainFrame(drained.Add);

            foreach (GameplaySkinEventEnvelope envelope in drained)
                observed.Add(envelope);

            Assert.That(drained, Is.Not.Empty);
            GameplaySkinEventEnvelope first = drained[0];

            Assert.Multiple(() =>
            {
                Assert.That(first.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Reset));
                Assert.That(first.Epoch, Is.EqualTo(epoch));
                Assert.That(first.Sequence, Is.Zero);
                Assert.That(((GameplaySkinStateEventPayload)first.Payload).ResetReason, Is.EqualTo(reason));
                Assert.That(drained.All(e => e.Epoch == epoch), Is.True);
            });
        }

        private void assertSeekSnapshotProgress(
            double destination,
            GameplaySkinEventResetReason reason,
            System.Func<long> nextEpoch,
            System.Func<GameplaySkinEventSubscription> getSubscription)
        {
            long expectedEpoch = -1;
            AddStep($"seek authoritative clock to {destination:N0}", () =>
            {
                expectedEpoch = nextEpoch();
                Player.GameplayClockContainer.Seek(destination);
            });
            AddUntilStep($"{reason} object epoch committed", () => drawableRuleset.GameplaySkinEventStream.CurrentEpoch == expectedEpoch);
            AddStep($"{reason} snapshot object progress matches envelope time", () =>
            {
                var drained = new List<GameplaySkinEventEnvelope>();
                getSubscription().DrainFrame(drained.Add);
                GameplaySkinEventEnvelope reset = drained.Single(e => e.DeliveryKind == GameplaySkinEventDeliveryKind.Reset);
                var snapshot = (GameplaySkinStateEventPayload)reset.Payload;
                GameplaySkinObjectStateSnapshot longNote = snapshot.State.ActiveObjects.Single(obj => obj.Kind == GameplaySkinObjectKind.LongNote);
                double expectedProgress = (reset.GameplayTime - longNote.StartTime) / (longNote.EndTime - longNote.StartTime);

                Assert.Multiple(() =>
                {
                    Assert.That(reset.Epoch, Is.EqualTo(expectedEpoch));
                    Assert.That(((GameplaySkinStateEventPayload)reset.Payload).ResetReason, Is.EqualTo(reason));
                    Assert.That(longNote.Progress, Is.EqualTo(System.Math.Clamp(expectedProgress, 0, 1)).Within(1e-9));
                });
            });
        }
    }
}
