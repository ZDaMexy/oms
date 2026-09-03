// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsGameplayLayoutProductionMatrix : OsuTestScene
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private ProductionHarness harness = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            // A live gameplay root participates in the package/layout prepare barrier until it has detached.
            // Keep parameterised cases independent instead of replacing an attached participant in the same frame.
            AddStep("detach previous production root", () =>
            {
                Clear();
                harness = null!;
            });
            AddWaitStep("settle participant detach", 2);
        }

        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.CenterRightScratch)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key9K_Pms, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center)]
        public void TestDecodedChartPublishesOneExactSnapshotToCompleteProductionGraph(BmsKeymode expectedKeymode, BmsPlayfieldStyle requestedStyle)
        {
            BmsBeatmap beatmap = null!;

            AddStep("decode and convert parser-owned chart", () => beatmap = createDecodedBeatmap(expectedKeymode));
            AddStep("mount exact production layout root", () => mountProductionRoot(beatmap, requestedStyle));
            AddUntilStep("complete production graph loaded", completeGraphLoaded);

            AddAssert("parser authority reaches exact typed publication", () =>
            {
                BmsGameplayLayoutSnapshot snapshot = harness.Drawable.LayoutSnapshot;
                (BmsKeymodeResolutionSource source, BmsKeymodeEvidence evidence) = expectedAuthority(expectedKeymode);

                return beatmap.BmsInfo.Keymode == expectedKeymode
                       && ReferenceEquals(snapshot.KeymodeResolution, beatmap.BmsInfo.KeymodeResolution)
                       && snapshot.KeymodeSource == source
                       && snapshot.KeymodeEvidence.HasFlag(evidence)
                       && snapshot.KeymodeDiagnostic.StartsWith("bms.keymode.", StringComparison.Ordinal);
            });
            AddAssert("typed and neutral adapters are one owner publication", () =>
            {
                BmsGameplayLayoutProvider provider = harness.Drawable.LayoutProvider;
                GameplaySkinLayoutPublication publication = provider.RevisionOwner!.CurrentPublication!;
                BmsGameplayLayoutSnapshot snapshot = harness.Drawable.LayoutSnapshot;

                return snapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility
                       && ReferenceEquals(publication.GetAdapter<BmsGameplayLayoutSnapshot>(), snapshot)
                       && ReferenceEquals(publication.Snapshot, snapshot.Neutral)
                       && ReferenceEquals(provider.RevisionOwner.Current, snapshot.Neutral)
                       && ReferenceEquals(provider.RevisionOwner.PackageRevision, snapshot.Context.PackageRevision);
            });
            AddAssert("complete renderer graph retains the exact typed adapter", allConsumersRetainExactSnapshot);
            AddAssert("each measure has exactly one bounded owner per exact C3 group", () => barLineOwnersMatchSnapshot(beatmap));
            AddAssert("drawable lanes project exact solved rects", drawableLaneRectsMatchSnapshot);
            AddAssert("style, lane IDs and explicit indices match parser topology", () => assertTopology(expectedKeymode, requestedStyle));
        }

        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key9K_Pms, BmsPlayfieldStyle.Center)]
        public void TestSparseExplicitOverrideReachesExactProductionOwnerAndCompleteRenderer(BmsKeymode expectedKeymode, BmsPlayfieldStyle requestedStyle)
        {
            BmsBeatmap beatmap = null!;

            AddStep("decode sparse chart with explicit authority", () => beatmap = createSparseOverrideBeatmap(expectedKeymode));
            AddStep("mount override chart in exact production root", () => mountProductionRoot(beatmap, requestedStyle));
            AddUntilStep("complete override production graph loaded", completeGraphLoaded);
            AddAssert("override resolution is exact parser carrier", () =>
            {
                BmsKeymodeResolution resolution = beatmap.BmsInfo.KeymodeResolution;
                BmsGameplayLayoutSnapshot snapshot = harness.Drawable.LayoutSnapshot;
                GameplaySkinLayoutPublication publication = harness.Drawable.LayoutProvider.RevisionOwner!.CurrentPublication!;

                return resolution.Keymode == expectedKeymode
                       && resolution.Source == BmsKeymodeResolutionSource.ExplicitOverride
                       && resolution.Evidence.HasFlag(BmsKeymodeEvidence.ExplicitOverride)
                       && resolution.DiagnosticCode == BmsKeymodeDiagnosticCode.ExplicitOverrideApplied
                       && resolution.StableDiagnostic == "bms.keymode.explicit-override-applied"
                       && ReferenceEquals(snapshot.KeymodeResolution, resolution)
                       && ReferenceEquals(publication.GetAdapter<BmsGameplayLayoutSnapshot>(), snapshot)
                       && ReferenceEquals(publication.Snapshot, snapshot.Neutral)
                       && snapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility;
            });
            AddAssert("override renderer graph retains one exact typed adapter", allConsumersRetainExactSnapshot);
            AddAssert("override bar lines retain exact group owners", () => barLineOwnersMatchSnapshot(beatmap));
            AddAssert("override lane rects use exact solved surfaces", drawableLaneRectsMatchSnapshot);
            AddAssert("override topology uses selected keymode", () => assertTopology(expectedKeymode, requestedStyle));
        }

        [Test]
        public void TestOpaqueCustomBgaFailsClosedWhileCustomHudReceivesExactSnapshot()
        {
            BmsBeatmap beatmap = null!;
            var skin = new ExactCarrierSkin();

            AddStep("decode and convert custom-carrier chart", () => beatmap = createDecodedBeatmap(BmsKeymode.Key7K));
            AddStep("mount custom skin in exact production root", () => mountProductionRoot(beatmap, BmsPlayfieldStyle.Center, skin));
            AddUntilStep("protected BGA and custom HUD loaded", () => customGraphLoaded()
                                                                  && skin.Hud.ComponentsObservedSnapshot
                                                                  && harness.Drawable.ChildrenOfType<BmsBgaPanel>().Single().Drawable is DefaultBmsBgaPanelDisplay);
            AddAssert("opaque custom BGA receives no P1-L authority", () =>
                skin.Bga.LayoutSnapshot == null
                && skin.Bga.InitialisationCount == 0
                && !skin.Bga.SourceObservedSnapshot
                && !skin.Bga.LayoutObservedSnapshot);
            AddAssert("protected BGA retains exact snapshot and viewport", () =>
                harness.Drawable.ChildrenOfType<BmsBgaPanel>().Single().Drawable is DefaultBmsBgaPanelDisplay display
                && ReferenceEquals(display.LayoutSnapshot, harness.Drawable.LayoutSnapshot)
                && harness.Drawable.LayoutSnapshot.BgaViewports.Count > 0);
            AddAssert("custom HUD callback retains exact snapshot and solved rects", () =>
                ReferenceEquals(skin.Hud.LayoutSnapshot, harness.Drawable.LayoutSnapshot)
                && skin.Hud.InitialisationCount == 1
                && skin.Hud.GaugeRect == harness.Drawable.LayoutSnapshot.GaugeRect
                && skin.Hud.ComboRect == harness.Drawable.LayoutSnapshot.ComboRect);
        }

        [Test]
        public void TestFourteenKeyProductionEventsUseFinalLaneAndLaneLocalVisualTargets()
        {
            BmsBeatmap beatmap = null!;
            BmsHitObject mirroredNote = null!;
            BmsMine mirroredMine = null!;
            GameplaySkinEventSubscription subscription = null!;
            var observedEvents = new List<GameplaySkinEventEnvelope>();

            AddStep("decode and mirror event-rich 14K chart", () =>
            {
                beatmap = createEventBeatmap();
                mirroredNote = beatmap.HitObjects.OfType<BmsHitObject>()
                                      .Single(hitObject => hitObject.GetType() == typeof(BmsHitObject) && hitObject.LaneIndex == 1);
                mirroredMine = beatmap.Mines.Single();
                new BmsModMirror().ApplyToBeatmap(beatmap);

                Assert.Multiple(() =>
                {
                    Assert.That(mirroredNote.LaneIndex, Is.EqualTo(7));
                    Assert.That(mirroredMine.LaneIndex, Is.EqualTo(7));
                });
            });
            AddStep("mount exact mirrored 14K production root", () =>
                mountProductionRoot(beatmap, BmsPlayfieldStyle.Center, initialGameplayTime: 1_500));
            AddUntilStep("14K event producer loaded", () => harness?.Drawable?.IsLoaded == true
                                                               && harness.Drawable.GameplaySkinEventRuntime?.IsLoaded == true
                                                               && harness.Drawable.Playfield.Lanes.Count == 16);
            AddStep("attach read-only production event consumer", () =>
            {
                subscription = harness.Drawable.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observedEvents.Add);

                Assert.Multiple(() =>
                {
                    Assert.That(observedEvents, Has.Count.EqualTo(1));
                    Assert.That(observedEvents[0].DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                    Assert.That(observedEvents[0].Revision,
                        Is.EqualTo(harness.Drawable.LayoutProvider.RevisionOwner!.CurrentPublication!.EventRevision));
                    Assert.That(harness.Drawable.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key2P_1), Is.True);
                });
            });
            AddUntilStep("second deck input has exact stable target", () =>
            {
                subscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.InputPressed
                                                      && envelope.GroupId?.Value == "bms.group.deck-2"
                                                      && envelope.LaneId?.Value == "bms.lane.key-8");
            });
            AddStep("release second deck input", () =>
                Assert.That(harness.Drawable.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key2P_1), Is.True));
            AddStep("advance event clock to bar and note window", () => harness.AdvanceTo(3_500));
            AddUntilStep("group-local bar reaches production stream", () =>
            {
                subscription.DrainFrame(observedEvents.Add);
                return getObservedObjectStates(observedEvents)
                       .Where(state => state.Kind == GameplaySkinObjectKind.BarLine
                                       && state.GroupId != null
                                       && state.LaneId == null)
                       .Select(state => state.GroupId)
                       .Distinct()
                       .Count() == 2;
            });
            AddUntilStep("mirrored note reaches production stream", () =>
            {
                subscription.DrainFrame(observedEvents.Add);
                return getObservedObjectStates(observedEvents).Any(state => state.Kind == GameplaySkinObjectKind.Note
                                                                            && state.LaneId?.Equals(
                                                                                harness.Drawable.LayoutSnapshot.GetLaneByLogicalIndex(mirroredNote.LaneIndex).LaneId) == true);
            });
            AddStep("advance event clock to mine window", () => harness.AdvanceTo(4_500));
            AddUntilStep("mirrored mine reaches production stream", () =>
            {
                subscription.DrainFrame(observedEvents.Add);
                return getObservedObjectStates(observedEvents).Any(state => state.Kind == GameplaySkinObjectKind.Mine
                                                                            && state.LaneId?.Equals(
                                                                                harness.Drawable.LayoutSnapshot.GetLaneByLogicalIndex(mirroredMine.LaneIndex).LaneId) == true);
            });
            AddStep("advance event clock to BGA content window", () => harness.AdvanceTo(5_500));
            AddUntilStep("BGA content summary reaches production stream", () =>
            {
                subscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.BgaContentStateChanged
                                                      && ((GameplaySkinBgaEventPayload)envelope.Payload).State.ContentRevision > 0);
            });
            AddStep("assert exact 14K object event identities", () =>
            {
                GameplaySkinLayoutPublication publication = harness.Drawable.LayoutProvider.RevisionOwner!.CurrentPublication!;
                GameplaySkinObjectStateSnapshot[] objects = getObservedObjectStates(observedEvents).ToArray();
                GameplaySkinLaneId finalLane = harness.Drawable.LayoutSnapshot.GetLaneByLogicalIndex(7).LaneId;

                Assert.Multiple(() =>
                {
                    Assert.That(objects.Any(state => state.Kind == GameplaySkinObjectKind.Note
                                                     && state.LaneId == finalLane
                                                     && state.GroupId.Value == "bms.group.deck-1"), Is.True);
                    Assert.That(objects.Any(state => state.Kind == GameplaySkinObjectKind.Mine
                                                     && state.LaneId == finalLane
                                                     && state.GroupId.Value == "bms.group.deck-1"), Is.True,
                        "Mirror must move note and mine event targets with the same final C3 LaneId.");
                    Assert.That(objects.Where(state => state.Kind == GameplaySkinObjectKind.BarLine),
                        Has.All.Property(nameof(GameplaySkinObjectStateSnapshot.LaneId)).Null,
                        "BMS bar lines are exact group-scoped production usages and never acquire lane identity.");
                    Assert.That(objects.Where(state => state.Kind == GameplaySkinObjectKind.BarLine)
                                               .Select(state => state.GroupId.Value)
                                               .Distinct(),
                        Is.EquivalentTo(new[] { "bms.group.deck-1", "bms.group.deck-2" }),
                        "A 14K measure must produce exactly one independently-targeted bar-line usage per deck.");
                    Assert.That(observedEvents.Where(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Edge),
                        Has.All.Property(nameof(GameplaySkinEventEnvelope.Revision)).EqualTo(publication.EventRevision));
                });
            });
            AddStep("detach event consumer", () => subscription.Dispose());
        }

        private static IEnumerable<GameplaySkinObjectStateSnapshot> getObservedObjectStates(IEnumerable<GameplaySkinEventEnvelope> envelopes)
        {
            foreach (GameplaySkinEventEnvelope envelope in envelopes)
            {
                if (envelope.Payload is GameplaySkinObjectEventPayload objectPayload)
                    yield return objectPayload.State;
                else if (envelope.Payload is GameplaySkinStateEventPayload statePayload)
                {
                    foreach (GameplaySkinObjectStateSnapshot activeObject in statePayload.State.ActiveObjects)
                        yield return activeObject;
                }
            }
        }

        private void mountProductionRoot(
            BmsBeatmap beatmap,
            BmsPlayfieldStyle style,
            ISkin? skin = null,
            double initialGameplayTime = 3_500)
        {
            var ruleset = new BmsRuleset();
            var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(ruleset)!;
            config.SetValue(BmsRulesetSetting.PlayfieldStyle, style);

            Child = harness = new ProductionHarness(ruleset, beatmap, config, skin, initialGameplayTime);
        }

        private bool completeGraphLoaded()
        {
            if (harness?.Drawable?.IsLoaded != true)
                return false;

            DrawableBmsRuleset drawable = harness.Drawable;
            return drawable.PreStartSpeedPreviewLayoutSnapshot != null
                   && drawable.BgaLayoutSnapshot != null
                   && drawable.HudLayoutSnapshot != null
                   && drawable.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Any(carrier => carrier.LayoutSnapshot != null)
                   && drawable.ChildrenOfType<BmsGaugeBar>().Any()
                   && drawable.ChildrenOfType<BmsComboCounter>().Any()
                   && drawable.ChildrenOfType<DrawableBmsHitObject>().Any(note => note.HitObject.GetType() == typeof(BmsHitObject))
                   && drawable.ChildrenOfType<DrawableBmsHoldNote>().Any();
        }

        private bool customGraphLoaded()
        {
            if (harness?.Drawable?.IsLoaded != true)
                return false;

            DrawableBmsRuleset drawable = harness.Drawable;
            return drawable.PreStartSpeedPreviewLayoutSnapshot != null
                   && drawable.BgaLayoutSnapshot != null
                   && drawable.HudLayoutSnapshot != null
                   && drawable.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Any(carrier => carrier.LayoutSnapshot != null)
                   && drawable.ChildrenOfType<DrawableBmsHitObject>().Any(note => note.HitObject.GetType() == typeof(BmsHitObject))
                   && drawable.ChildrenOfType<DrawableBmsHoldNote>().Any();
        }

        private bool skinObservedForDebug()
            => false;

        private bool allConsumersRetainExactSnapshot()
        {
            DrawableBmsRuleset drawable = harness.Drawable;
            BmsGameplayLayoutSnapshot snapshot = drawable.LayoutSnapshot;
            DrawableBmsHitObject note = drawable.ChildrenOfType<DrawableBmsHitObject>().First(candidate => candidate.HitObject.GetType() == typeof(BmsHitObject));
            DrawableBmsHoldNote hold = drawable.ChildrenOfType<DrawableBmsHoldNote>().First();
            BmsHudLayoutSnapshotCarrier hudCarrier = drawable.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Single(candidate => ReferenceEquals(candidate.LayoutSnapshot, snapshot));

            return ReferenceEquals(drawable.Playfield.LayoutSnapshot, snapshot)
                   && drawable.Playfield.GroupContainers.All(group => ReferenceEquals(group.LayoutSnapshot, snapshot))
                   && drawable.Playfield.Lanes.All(lane => ReferenceEquals(lane.LayoutSnapshot, snapshot)
                                                          && ReferenceEquals(lane.HitTarget.LayoutSnapshot, snapshot))
                   && drawable.Playfield.BarLinePlayfields.SelectMany(owner => owner.AllHitObjects).OfType<DrawableBmsBarLine>()
                              .All(barLine => ReferenceEquals(barLine.LayoutSnapshot, snapshot))
                   && ReferenceEquals(note.ExactLayoutSnapshot, snapshot)
                   && ReferenceEquals(hold.ExactLayoutSnapshot, snapshot)
                   && hold.ChildrenOfType<DrawableBmsHoldNoteHead>().All(head => ReferenceEquals(head.ExactLayoutSnapshot, snapshot))
                   && hold.ChildrenOfType<DrawableBmsHoldNoteTail>().All(tail => ReferenceEquals(tail.ExactLayoutSnapshot, snapshot))
                   && ReferenceEquals(drawable.PreStartSpeedPreviewLayoutSnapshot, snapshot)
                   && ReferenceEquals(drawable.BgaLayoutSnapshot, snapshot)
                   && ReferenceEquals(drawable.HudLayoutSnapshot, snapshot)
                   && ReferenceEquals(hudCarrier.LayoutSnapshot, snapshot)
                   && drawable.ChildrenOfType<BmsGaugeBar>().Where(gauge => gauge.LayoutSnapshot != null)
                              .All(gauge => ReferenceEquals(gauge.LayoutSnapshot, snapshot))
                   && drawable.ChildrenOfType<BmsComboCounter>().Where(combo => combo.LayoutSnapshot != null)
                              .All(combo => ReferenceEquals(combo.LayoutSnapshot, snapshot));
        }

        private bool barLineOwnersMatchSnapshot(BmsBeatmap beatmap)
        {
            BmsPlayfield playfield = harness.Drawable.Playfield;
            IReadOnlyList<GameplaySkinLayoutGroup> groups = playfield.LayoutSnapshot.Neutral.GroupsInLogicalOrder;

            if (playfield.BarLinePlayfields.Count != groups.Count
                || playfield.Lanes.SelectMany(lane => lane.AllHitObjects).OfType<DrawableBmsBarLine>().Any())
            {
                return false;
            }

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                GameplaySkinLayoutGroup group = groups[groupIndex];
                BmsBarLinePlayfield owner = playfield.BarLinePlayfields[groupIndex];

                if (!ReferenceEquals(owner.LayoutGroup, group)
                    || owner.GroupLogicalIndex != group.TopologyGroup.LogicalIndex
                    || !owner.GroupId.Equals(group.GroupId)
                    || !playfield.NestedPlayfields.Contains(owner)
                    || !playfield.GroupContainers.Single(container => container.GroupId.Equals(group.GroupId)).Children.Contains(owner)
                    || owner.MeasureBarLines.Count != beatmap.MeasureStartTimes.Count
                    || owner.PoolCapacity != GameplaySkinSceneHostPolicy.SpecialisedPoolCapacity(GameplaySkinSlotCatalog.BarLine)
                    || owner.PoolSize > owner.PoolCapacity
                    || owner.MeasureBarLines.Any(barLine => barLine.GroupLogicalIndex != group.TopologyGroup.LogicalIndex
                                                           || barLine.GroupId?.Equals(group.GroupId) != true))
                {
                    return false;
                }
            }

            return playfield.BarLinePlayfields.Sum(owner => owner.MeasureBarLines.Count)
                   == beatmap.MeasureStartTimes.Count * groups.Count;
        }

        private bool drawableLaneRectsMatchSnapshot()
        {
            DrawableBmsRuleset drawable = harness.Drawable;
            BmsGameplayLayoutSnapshot snapshot = drawable.LayoutSnapshot;
            float rootWidth = drawable.Playfield.ScreenSpaceDrawQuad.Width;
            float rootHeight = drawable.Playfield.ScreenSpaceDrawQuad.Height;
            float rootLeft = drawable.Playfield.ScreenSpaceDrawQuad.TopLeft.X;

            return drawable.Playfield.Lanes.All(lane =>
            {
                GameplaySkinLayoutRect rect = lane.LayoutSnapshotLane!.NeutralLane.Rect;
                float actualLeft = (lane.ScreenSpaceDrawQuad.TopLeft.X - rootLeft) / rootWidth;
                float actualWidth = lane.ScreenSpaceDrawQuad.Width / rootWidth;
                float actualHeight = lane.ScreenSpaceDrawQuad.Height / rootHeight;
                return Math.Abs(actualLeft - rect.Left) <= 0.002f
                       && Math.Abs(actualWidth - rect.Width) <= 0.002f
                       && Math.Abs(actualHeight - rect.Height) <= 0.002f;
            });
        }

        private bool assertTopology(BmsKeymode keymode, BmsPlayfieldStyle requestedStyle)
        {
            BmsGameplayLayoutSnapshot snapshot = harness.Drawable.LayoutSnapshot;
            int laneCount = BmsRuleset.GetLaneCount(keymode);

            if (snapshot.Keymode != keymode
                || snapshot.Style != requestedStyle.GetAppliedStyle(keymode)
                || snapshot.LanesInLogicalOrder.Count != laneCount
                || !snapshot.LanesInLogicalOrder.Select(lane => lane.LogicalIndex).SequenceEqual(Enumerable.Range(0, laneCount))
                || !snapshot.LanesInLogicalOrder.Select(lane => lane.VisualIndex).Order().SequenceEqual(Enumerable.Range(0, laneCount))
                || snapshot.LanesInLogicalOrder.Select(lane => lane.LaneId.Value).Distinct().Count() != laneCount)
                return false;

            if (keymode == BmsKeymode.Key14K)
            {
                GameplaySkinLayoutGroup first = snapshot.Neutral.GroupsInLogicalOrder[0];
                GameplaySkinLayoutGroup second = snapshot.Neutral.GroupsInLogicalOrder[1];
                return snapshot.Neutral.GroupsInLogicalOrder.Count == 2
                       && first.TopologyGroup.LanesInLogicalOrder.Count == 8
                       && second.TopologyGroup.LanesInLogicalOrder.Count == 8
                       && snapshot.GetLaneByLogicalIndex(0).LaneId.Value == "bms.lane.scratch-1"
                       && snapshot.GetLaneByLogicalIndex(15).LaneId.Value == "bms.lane.scratch-2"
                       && snapshot.GetLaneByLogicalIndex(15).Action == Input.BmsAction.Scratch2
                       && second.Rect.Left > first.Rect.Right;
            }

            if (keymode is BmsKeymode.Key5K or BmsKeymode.Key7K)
            {
                bool scratchRight = snapshot.Style.UsesScratchVisualRight();
                return snapshot.GetLaneByLogicalIndex(0).IsScratch
                       && (scratchRight
                           ? snapshot.GetLaneByLogicalIndex(0).VisualIndex == laneCount - 1
                           : snapshot.GetLaneByLogicalIndex(0).VisualIndex == 0);
            }

            return snapshot.Neutral.GroupsInLogicalOrder.Count == 1;
        }

        private BmsBeatmap createDecodedBeatmap(BmsKeymode keymode)
        {
            (string fileName, string evidence) = keymode switch
            {
                BmsKeymode.Key5K => ("production-5k.bms", "#00111:0100\n#00113:0100\n#00114:0100\n#00115:0100"),
                BmsKeymode.Key7K => ("production-7k.bme", "#00111:0100\n#00118:0100\n#00119:0100"),
                BmsKeymode.Key9K_Bms => ("production-9k.bms", string.Join("\n", Enumerable.Range(0x11, 9).Select(channel => $"#001{channel:X2}:0100"))),
                BmsKeymode.Key9K_Pms => ("production-9k.pms", "#00111:0100\n#00119:0100"),
                BmsKeymode.Key14K => ("production-14k.bms", "#00111:0100\n#00116:0100\n#00121:0100\n#00126:0100"),
                _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
            };

            string text = $@"
#TITLE Production Layout Matrix
#BPM 120
#RANK 2
#WAV01 note.wav
#WAV02 hold-head.wav
#WAV03 hold-tail.wav
#LNTYPE 1
{evidence}
#00213:0100
#00252:02000300
";
            var chart = decoder.DecodeText(text, fileName);
            var ruleset = new BmsRuleset();
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), ruleset).Convert();
        }

        private BmsBeatmap createSparseOverrideBeatmap(BmsKeymode keymode)
        {
            const string text = @"
#TITLE Sparse Production Layout Override
#BPM 120
#RANK 2
#WAV01 note.wav
#WAV02 hold-head.wav
#WAV03 hold-tail.wav
#LNTYPE 1
#00111:0100
#00213:0100
#00252:02000300
";
            BmsDecodedChart chart = decoder.DecodeText(text, "sparse-production.bms", new BmsBeatmapDecoderOptions(keymode));
            var ruleset = new BmsRuleset();
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), ruleset).Convert();
        }

        private BmsBeatmap createEventBeatmap()
        {
            const string text = @"
#TITLE Production Event 14K
#BPM 120
#RANK 2
#WAV01 note.wav
#WAV02 hold-head.wav
#WAV03 hold-tail.wav
#WAVAA mine.wav
#BMP01 base.png
#LNTYPE 1
#00211:0100
#00221:0100
#002D1:0000AA00
#00204:00000100
#00352:02000300
";
            var chart = decoder.DecodeText(text, "production-event-14k.bms");
            var ruleset = new BmsRuleset();
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), ruleset).Convert();
        }

        private static (BmsKeymodeResolutionSource Source, BmsKeymodeEvidence Evidence) expectedAuthority(BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => (BmsKeymodeResolutionSource.CompleteChannelSet, BmsKeymodeEvidence.CompleteFiveKeyChannelSet),
                BmsKeymode.Key7K => (BmsKeymodeResolutionSource.FileExtension, BmsKeymodeEvidence.BmeFileExtension),
                BmsKeymode.Key9K_Bms => (BmsKeymodeResolutionSource.CompleteChannelSet, BmsKeymodeEvidence.CompleteNineKeyChannelSet),
                BmsKeymode.Key9K_Pms => (BmsKeymodeResolutionSource.FileExtension, BmsKeymodeEvidence.PmsFileExtension),
                BmsKeymode.Key14K => (BmsKeymodeResolutionSource.Player2ChannelEvidence, BmsKeymodeEvidence.Player2Channel),
                _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
            };

        private sealed partial class ProductionHarness : CompositeDrawable
        {
            private readonly ManualClock sourceClock;
            private readonly FramedClock frameClock;

            public RulesetSkinProvidingContainer Provider { get; }

            public DrawableBmsRuleset Drawable { get; }

            public ProductionHarness(
                BmsRuleset ruleset,
                BmsBeatmap beatmap,
                BmsRulesetConfigManager config,
                ISkin? skin,
                double initialGameplayTime)
            {
                RelativeSizeAxes = Axes.Both;

                var gaugeProcessor = new BmsGaugeProcessor(0);
                gaugeProcessor.ApplyBeatmap(beatmap);
                var scoreProcessor = new BmsScoreProcessor();
                scoreProcessor.ApplyBeatmap(beatmap);

                Drawable = (DrawableBmsRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                sourceClock = new ManualClock
                {
                    CurrentTime = initialGameplayTime,
                    IsRunning = false,
                };
                frameClock = new FramedClock(sourceClock);
                frameClock.ProcessFrame();
                Drawable.Clock = frameClock;

                var dependencyHost = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(GameplayState), new GameplayState(beatmap, ruleset)),
                        (typeof(HealthProcessor), gaugeProcessor),
                        (typeof(ScoreProcessor), scoreProcessor),
                    },
                    Child = Drawable,
                };
                Provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null, prepareGameplaySkinLayout: true)
                {
                    Child = dependencyHost,
                };

                var configHost = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(BmsRulesetConfigManager), config),
                    },
                    Child = Provider,
                };

                InternalChild = skin == null
                    ? configHost
                    : new SkinProvidingContainer(skin) { Child = configHost };
            }

            public void AdvanceTo(double gameplayTime)
            {
                sourceClock.CurrentTime = gameplayTime;
                frameClock.ProcessFrame();
            }
        }

        private sealed class ExactCarrierSkin : Skin
        {
            public readonly ExactBgaDisplay Bga = new ExactBgaDisplay();
            public readonly ExactHudDisplay Hud = new ExactHudDisplay();

            public ExactCarrierSkin()
                : base(new SkinInfo(name: nameof(ExactCarrierSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup switch
                {
                    BmsSkinComponentLookup { Component: BmsSkinComponents.BgaPanel } => Bga,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout } => Hud,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeBar } => new Box(),
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter } => new ExactComboCounter(),
                    _ => null,
                };

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed partial class ExactBgaDisplay : CompositeDrawable, IBmsBgaPanelDisplay, IBmsBgaPanelLayoutDisplay
        {
            public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }
            public int InitialisationCount { get; private set; }
            public bool SourceObservedSnapshot { get; private set; }
            public bool LayoutObservedSnapshot { get; private set; }
            public GameplaySkinLayoutRect ViewportRect { get; private set; }

            public ExactBgaDisplay() => RelativeSizeAxes = Axes.Both;

            public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
            {
                InitialisationCount++;
                LayoutSnapshot = snapshot;
                ViewportRect = snapshot.BgaViewports[0];
                InternalChild = new Box
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativePositionAxes = Axes.Both,
                    RelativeSizeAxes = Axes.Both,
                    Position = new Vector2(ViewportRect.X, ViewportRect.Y),
                    Size = new Vector2(ViewportRect.Width, ViewportRect.Height),
                };
            }

            public void SetBgaSource(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode)
                => SourceObservedSnapshot = LayoutSnapshot != null;

            public void SetLayout(BmsBgaPlacement placement)
                => LayoutObservedSnapshot = LayoutSnapshot != null;

            public void NotifyMiss()
            {
            }
        }

        private sealed partial class ExactHudDisplay : Container, IBmsHudLayoutDisplay
        {
            public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }
            public int InitialisationCount { get; private set; }
            public bool ComponentsObservedSnapshot { get; private set; }
            public GameplaySkinLayoutRect GaugeRect { get; private set; }
            public GameplaySkinLayoutRect ComboRect { get; private set; }

            public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
            {
                InitialisationCount++;
                LayoutSnapshot = snapshot;
                GaugeRect = snapshot.GaugeRect;
                ComboRect = snapshot.ComboRect;
            }

            public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
            {
                ComponentsObservedSnapshot = LayoutSnapshot != null;
                gaugeBar.Anchor = gaugeBar.Origin = Anchor.TopLeft;
                gaugeBar.RelativePositionAxes = gaugeBar.RelativeSizeAxes = Axes.Both;
                gaugeBar.Position = new Vector2(GaugeRect.X, GaugeRect.Y);
                gaugeBar.Size = new Vector2(GaugeRect.Width, GaugeRect.Height);
                comboCounter.Anchor = comboCounter.Origin = Anchor.Centre;
                comboCounter.RelativePositionAxes = Axes.Both;
                comboCounter.Position = new Vector2(ComboRect.X + ComboRect.Width / 2, ComboRect.Y + ComboRect.Height / 2);
                AddRange(new[] { gaugeBar, comboCounter });
            }
        }

        private sealed partial class ExactComboCounter : ComboCounter
        {
        }
    }
}
