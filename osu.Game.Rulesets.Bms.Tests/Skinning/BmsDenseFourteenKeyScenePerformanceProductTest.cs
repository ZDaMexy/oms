// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        private const int dense_measure_count = 8;
        private const double dense_step_milliseconds = 500;
        private const int dense_pass_frame_count = 48;

        private static readonly int[] dense_lane_channels =
        {
            0x16, 0x11, 0x12, 0x13, 0x14, 0x15, 0x18, 0x19,
            0x21, 0x22, 0x23, 0x24, 0x25, 0x28, 0x29, 0x26,
        };

        [Test]
        public void TestDenseFourteenKeyAuthorSceneEventAndNativePoolsStayWithinProductionBudgets()
        {
            Live<SkinInfo> candidate = null!;
            BmsBeatmap beatmap = null!;
            DenseFourteenKeyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            GameplaySkinEventSubscription subscription = null!;
            var eventObservation = new DenseEventObservation();
            var firstPassPools = new DensePoolObservation();
            long firstEpoch = -1;
            int pendingOldEpochEdges = 0;
            long steadyAllocatedBytes = -1;
            int runtimeInstancesAfterFirstPass = 0;
            int steadyNotes = 0;
            int steadyHolds = 0;
            int steadyHoldHeads = 0;
            int steadyHoldTails = 0;
            int steadyHoldBodyTicks = 0;
            int steadyMines = 0;
            int steadyBarLines = 0;
            int steadyRuntimeInstances = 0;
            TimeSpan pressureElapsed = TimeSpan.Zero;
            var pressureStopwatch = new Stopwatch();
            int firstPassFrame = 0;
            int settlePassFrame = 0;
            int steadyPassFrame = 0;

            AddStep("create and select dense 14K author package", () =>
            {
                (_, candidate) = createCandidate(
                    writeDenseFourteenKeyAuthorPackage,
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact dense package revision", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("decode and convert dense 14K BMS", () =>
            {
                beatmap = createDenseFourteenKeyBeatmap();
                Assert.Multiple(() =>
                {
                    Assert.That(beatmap.BmsInfo.Keymode, Is.EqualTo(BmsKeymode.Key14K));
                    Assert.That(beatmap.HitObjects.OfType<BmsHoldNote>().Count(), Is.EqualTo(dense_measure_count * dense_lane_channels.Length));
                    Assert.That(beatmap.HitObjects.Count(hitObject => hitObject.GetType() == typeof(BmsHitObject)),
                        Is.EqualTo(dense_measure_count * dense_lane_channels.Length * 16));
                    Assert.That(beatmap.Mines, Has.Count.EqualTo(dense_measure_count * dense_lane_channels.Length * 4));
                    Assert.That(beatmap.MeasureStartTimes.Count, Is.GreaterThanOrEqualTo(dense_measure_count + 1));
                });
            });
            AddStep("mount exact dense 14K DrawableBmsRuleset", () =>
            {
                Add(renderer = new DenseFourteenKeyHost(manager, beatmap));
            });
            AddUntilStep("wait for complete dense scene production graph", () => renderer.Ready);
            AddStep("capture exact mounted scene and event authority", () =>
            {
                sceneHost = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                subscription = renderer.Drawable.GameplaySkinEventStream.Subscribe();
                drainAll(subscription, eventObservation);

                GameplaySkinLayoutPublication publication = renderer.LayoutProbe.Publication!;
                Assert.Multiple(() =>
                {
                    Assert.That(publication.PreparedScene.HasAuthorScene, Is.True);
                    Assert.That(sceneHost.Publication, Is.SameAs(publication));
                    Assert.That(sceneHost.PreparedScene, Is.SameAs(publication.PreparedScene));
                    Assert.That(sceneHost.MaterialSet, Is.SameAs(publication.MaterialSet));
                    Assert.That(sceneHost.EventStream, Is.SameAs(renderer.Drawable.GameplaySkinEventStream));
                    Assert.That(renderer.Drawable.LayoutSnapshot.Keymode, Is.EqualTo(BmsKeymode.Key14K));
                    Assert.That(renderer.Drawable.LayoutSnapshot.Neutral.GroupsInLogicalOrder, Has.Count.EqualTo(2));
                    Assert.That(renderer.Drawable.Playfield.Lanes, Has.Count.EqualTo(16));
                    Assert.That(sceneHost.PendingCreationCount, Is.Zero);
                    Assert.That(sceneHost.RuntimeNodeCount, Is.Positive);
                    Assert.That(sceneHost.CreatedThisFrame, Is.LessThanOrEqualTo(GameplaySkinPreparedSceneBudgets.MAX_CREATIONS_PER_FRAME));
                    Assert.That(sceneHost.RuntimeInstanceCount, Is.LessThanOrEqualTo(GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES));
                });
            });
            AddStep("start first dense sustained production pass", pressureStopwatch.Restart);
            AddRepeatStep("run first dense sustained production frames", () =>
                runDenseFrame(renderer, sceneHost, subscription, eventObservation, firstPassPools, firstPassFrame++), dense_pass_frame_count);
            AddStep("assert first dense sustained production pass", () =>
            {
                pressureStopwatch.Stop();
                pressureElapsed = pressureStopwatch.Elapsed;
                runtimeInstancesAfterFirstPass = sceneHost.RuntimeInstanceCount;

                Assert.Multiple(() =>
                {
                    Assert.That(firstPassPools.UsedNotes, Is.Not.Empty);
                    Assert.That(firstPassPools.UsedHolds, Is.Not.Empty);
                    Assert.That(firstPassPools.UsedMines, Is.Not.Empty);
                    Assert.That(firstPassPools.UsedBarLines, Is.Not.Empty);
                    Assert.That(firstPassPools.UsedHoldLanes, Has.Count.EqualTo(16));
                    Assert.That(firstPassPools.UsedMineLanes, Has.Count.EqualTo(16));
                    Assert.That(firstPassPools.UsedBarLineGroups, Is.EquivalentTo(new[] { 0, 1 }));
                    Assert.That(renderer.Drawable.Playfield.BarLinePlayfields.All(owner =>
                        owner.PoolCapacity == GameplaySkinSceneHostPolicy.SpecialisedPoolCapacity(GameplaySkinSlotCatalog.BarLine)
                        && owner.PoolSize <= owner.PoolCapacity), Is.True);
                    Assert.That(firstPassPools.MaximumCreatedPerLaneAndFamily,
                        Is.LessThanOrEqualTo(BmsLane.MAXIMUM_NOTE_POOL_SIZE));
                    Assert.That(firstPassPools.MaximumBodyTicks,
                        Is.LessThanOrEqualTo(16 * BmsLane.MAXIMUM_BODY_TICK_POOL_SIZE));
                    Assert.That(firstPassPools.MaximumActiveObjects, Is.Positive);
                    Assert.That(sceneHost.PendingCreationCount, Is.Zero);
                    Assert.That(firstPassPools.MaximumSceneCreationsPerFrame,
                        Is.LessThanOrEqualTo(GameplaySkinPreparedSceneBudgets.MAX_CREATIONS_PER_FRAME));
                    Assert.That(firstPassPools.MaximumRuntimeInstances,
                        Is.LessThanOrEqualTo(GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES));
                    Assert.That(eventObservation.MaximumPending,
                        Is.LessThanOrEqualTo(GameplaySkinEventBudgets.MAX_PENDING_EVENTS_PER_SUBSCRIPTION));
                    Assert.That(eventObservation.MaximumDrain,
                        Is.LessThanOrEqualTo(GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME));
                    Assert.That(eventObservation.ObjectEdges[GameplaySkinObjectKind.LongNote], Is.Positive);
                    Assert.That(eventObservation.ObjectEdges[GameplaySkinObjectKind.Mine], Is.Positive);
                    Assert.That(eventObservation.ObjectEdges[GameplaySkinObjectKind.BarLine], Is.Positive);
                    Assert.That(eventObservation.GroupsWithObjectEdges,
                        Is.SupersetOf(new[] { "bms.group.deck-1", "bms.group.deck-2" }));
                });
            });
            AddUntilStep("wait for first-pass prepared pool visuals", () => firstPassPools.PreparedVisualLoadsSettled);
            AddStep("queue old epoch input edges through real BMS input", () =>
            {
                firstEpoch = renderer.Drawable.GameplaySkinEventStream.CurrentEpoch;
                Assert.That(renderer.Drawable.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True);
                Assert.That(renderer.Drawable.GameplayInputManager.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True);
                Assert.That(renderer.Drawable.GameplayInputManager.TriggerOmsActionPressed(OmsAction.Key2P_1), Is.True);
                Assert.That(renderer.Drawable.GameplayInputManager.TriggerOmsActionReleased(OmsAction.Key2P_1), Is.True);
                pendingOldEpochEdges = subscription.PendingCount;
                Assert.That(pendingOldEpochEdges, Is.GreaterThanOrEqualTo(4));
            });
            AddStep("request rewind through the real gameplay clock", () => renderer.AdvanceTo(0));
            AddUntilStep("wait for atomic rewind epoch replacement", () =>
                renderer.Drawable.GameplaySkinEventStream.CurrentEpoch > firstEpoch
                && subscription.PendingCount > 0);
            AddStep("assert rewind atomically replaced queued old epoch", () =>
            {
                long resetEpoch = renderer.Drawable.GameplaySkinEventStream.CurrentEpoch;
                int pendingAfterReset = subscription.PendingCount;
                var resetBatch = new List<GameplaySkinEventEnvelope>(pendingAfterReset);
                subscription.DrainFrame(resetBatch.Add);

                Assert.Multiple(() =>
                {
                    Assert.That(resetEpoch, Is.GreaterThan(firstEpoch));
                    Assert.That(pendingAfterReset, Is.Positive.And.LessThanOrEqualTo(GameplaySkinEventBudgets.MAX_PENDING_EVENTS_PER_SUBSCRIPTION));
                    Assert.That(resetBatch, Is.Not.Empty);
                    Assert.That(resetBatch[0].DeliveryKind,
                        Is.EqualTo(GameplaySkinEventDeliveryKind.Reset).Or.EqualTo(GameplaySkinEventDeliveryKind.Snapshot),
                        "Several native replay callbacks can advance more than one epoch before this deliberately stalled consumer drains; " +
                        "the stream must then atomically reattach it with the latest complete snapshot.");
                    Assert.That(resetBatch[0].Sequence, Is.Zero);
                    Assert.That(resetBatch.All(envelope => envelope.Epoch == resetEpoch), Is.True,
                        "An atomic rewind reset must remove every queued envelope from the old epoch.");
                    Assert.That(((GameplaySkinStateEventPayload)resetBatch[0].Payload).State.ActiveObjects.Count,
                        Is.LessThanOrEqualTo(GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS));
                });

                foreach (GameplaySkinEventEnvelope envelope in resetBatch)
                    eventObservation.Observe(envelope);

                drainAll(subscription, eventObservation);
            });
            AddStep("warm identical replay before freezing pool inventory", () =>
            {
                Assert.That(settlePassFrame, Is.Zero);
            });
            AddRepeatStep("run warm retained-pool replay frames", () =>
                runDenseFrame(renderer, sceneHost, subscription, eventObservation, firstPassPools, settlePassFrame++), dense_pass_frame_count);
            AddUntilStep("wait for warm retained-pool replay visuals", () => firstPassPools.PreparedVisualLoadsSettled);
            AddStep("capture settled production pool inventory", () =>
            {
                steadyNotes = renderer.Drawable.Playfield.Lanes.Sum(lane => lane.NotePoolSize);
                steadyHolds = renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNotePoolSize);
                steadyHoldHeads = renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNoteHeadPoolSize);
                steadyHoldTails = renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNoteTailPoolSize);
                steadyHoldBodyTicks = renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNoteBodyTickPoolSize);
                steadyMines = renderer.Drawable.Playfield.Lanes.Sum(lane => lane.MinePoolSize);
                steadyBarLines = renderer.Drawable.Playfield.BarLinePlayfields.Sum(owner => owner.PoolSize);
                steadyRuntimeInstances = sceneHost.RuntimeInstanceCount;
            });
            AddStep("replay dense window without further pool or scene growth", () =>
            {
                Assert.That(steadyPassFrame, Is.Zero);
            });
            AddRepeatStep("run steady retained-pool replay frames", () =>
                runDenseFrame(renderer, sceneHost, subscription, eventObservation, firstPassPools, steadyPassFrame++), dense_pass_frame_count);
            AddUntilStep("wait for retained-pool replay visuals", () => firstPassPools.PreparedVisualLoadsSettled);
            AddStep("assert retained-pool replay has no growth", () =>
            {

                Assert.Multiple(() =>
                {
                    Assert.That(renderer.Drawable.Playfield.Lanes.Sum(lane => lane.NotePoolSize), Is.EqualTo(steadyNotes));
                    Assert.That(renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNotePoolSize), Is.EqualTo(steadyHolds));
                    Assert.That(renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNoteHeadPoolSize), Is.EqualTo(steadyHoldHeads));
                    Assert.That(renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNoteTailPoolSize), Is.EqualTo(steadyHoldTails));
                    Assert.That(renderer.Drawable.Playfield.Lanes.Sum(lane => lane.HoldNoteBodyTickPoolSize), Is.EqualTo(steadyHoldBodyTicks));
                    Assert.That(renderer.Drawable.Playfield.Lanes.Sum(lane => lane.MinePoolSize), Is.EqualTo(steadyMines));
                    Assert.That(renderer.Drawable.Playfield.BarLinePlayfields.Sum(owner => owner.PoolSize), Is.EqualTo(steadyBarLines));
                    Assert.That(firstPassPools.AllNotes.Count, Is.LessThanOrEqualTo(steadyNotes));
                    Assert.That(firstPassPools.AllHolds.Count, Is.LessThanOrEqualTo(steadyHolds));
                    Assert.That(firstPassPools.AllMines.Count, Is.LessThanOrEqualTo(steadyMines));
                    Assert.That(firstPassPools.AllBarLines.Count, Is.LessThanOrEqualTo(steadyBarLines));
                    Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(steadyRuntimeInstances),
                        "After prepared pool visuals settle, the identical dense window must reuse specialised scene visuals and native pools.");
                    Assert.That(sceneHost.RuntimeInstanceCount, Is.GreaterThanOrEqualTo(runtimeInstancesAfterFirstPass)
                        .And.LessThanOrEqualTo(GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES));
                });
            });
            AddStep("measure allocation-free committed scene steady frames", () =>
            {
                double steadyStart = renderer.EndTime + 10_000;

                for (int frame = 0; frame < 256; frame++)
                    sceneHost.ProcessFrame();

                int instancesBefore = sceneHost.RuntimeInstanceCount;
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

                for (int frame = 0; frame < 2048; frame++)
                    sceneHost.ProcessFrame();

                steadyAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                Assert.Multiple(() =>
                {
                    Assert.That(steadyAllocatedBytes, Is.Zero,
                        "The mounted production author scene must not allocate on unchanged steady-state frames.");
                    Assert.That(sceneHost.RuntimeInstanceCount, Is.EqualTo(instancesBefore));
                    Assert.That(sceneHost.PendingCreationCount, Is.Zero);
                    Assert.That(sceneHost.CreatedThisFrame, Is.Zero);
                    Assert.That(sceneHost.RuntimeFaults, Is.Empty);
                });
            });
            AddStep("assert routed native scene ownership remains production-visible", () =>
            {
                GameplaySkinSceneHostedSlot[] movingGates = sceneHost.HostedSlots.Where(slot =>
                    slot.Key.Slot == GameplaySkinSlotCatalog.Note
                    || slot.Key.Slot == GameplaySkinSlotCatalog.LongNoteHead
                    || slot.Key.Slot == GameplaySkinSlotCatalog.LongNoteBody
                    || slot.Key.Slot == GameplaySkinSlotCatalog.LongNoteTail
                    || slot.Key.Slot == GameplaySkinSlotCatalog.Mine
                    || slot.Key.Slot == GameplaySkinSlotCatalog.BarLine).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(movingGates.Count(gate => gate.RoutedNodes.Count > 0), Is.EqualTo(12));
                    Assert.That(movingGates.Where(gate => gate.RoutedNodes.Count > 0),
                        Has.All.Property(nameof(GameplaySkinSceneHostedSlot.Route)).EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(movingGates.Where(gate => gate.RoutedNodes.Count > 0),
                        Has.All.Property(nameof(GameplaySkinSceneHostedSlot.IsReplacementReady)).True);
                    Assert.That(firstPassPools.SceneAppliedMineCount, Is.Positive);
                    Assert.That(firstPassPools.SceneAppliedBarLineCount, Is.Positive);
                    Assert.That(pressureElapsed, Is.LessThan(TimeSpan.FromSeconds(30)),
                        "The in-process dense traversal budget is deliberately generous for shared CI hosts.");
                    TestContext.Progress.WriteLine(
                        $"dense14k pressure={pressureElapsed.TotalMilliseconds:0}ms; " +
                        $"pool={firstPassPools.AllMainObjects.Count}; max-active={firstPassPools.MaximumActiveObjects}; " +
                        $"runtime={firstPassPools.MaximumRuntimeInstances}; max-pending={eventObservation.MaximumPending}; " +
                        $"events={eventObservation.TotalEnvelopes}; steady-alloc={steadyAllocatedBytes}B");
                });
            });
            AddStep("detach dense production root", () =>
            {
                subscription.Dispose();
                renderer.Expire();
            });
            AddUntilStep("wait for dense root detach", () => renderer.Parent == null);
        }

        private static void runDenseFrame(
            DenseFourteenKeyHost renderer,
            GameplaySkinSceneRuntimeHost sceneHost,
            GameplaySkinEventSubscription subscription,
            DenseEventObservation events,
            DensePoolObservation pools,
            int frame)
        {
            renderer.AdvanceTo(frame * dense_step_milliseconds);
            renderer.Drawable.UpdateSubTree();
            pools.Observe(renderer.Drawable, sceneHost);
            drainAll(subscription, events);
        }

        private static void drainAll(GameplaySkinEventSubscription subscription, DenseEventObservation observation)
        {
            observation.MaximumPending = Math.Max(observation.MaximumPending, subscription.PendingCount);

            while (subscription.PendingCount > 0)
            {
                int drained = subscription.DrainFrame(observation.Observe);
                observation.MaximumDrain = Math.Max(observation.MaximumDrain, drained);

                if (drained == 0)
                    throw new AssertionException("A non-empty production event subscription made no bounded drain progress.");
            }
        }

        private static BmsBeatmap createDenseFourteenKeyBeatmap()
        {
            var text = new StringBuilder();
            text.AppendLine("#TITLE C5 Dense 14K Production Budget")
                .AppendLine("#BPM 120")
                .AppendLine("#RANK 2")
                .AppendLine("#LNTYPE 1")
                .AppendLine("#WAV01 note.wav")
                .AppendLine("#WAV02 hold.wav")
                .AppendLine("#WAVAA mine.wav");

            string notes = tokenLine(32, index => (index & 1) == 1 ? "01" : "00");
            string sustainedHold = tokenLine(32, index => index is 0 or 31 ? "02" : "00");
            string mines = tokenLine(32, index => index % 8 == 4 ? "AA" : "00");

            for (int measure = 1; measure <= dense_measure_count; measure++)
            {
                foreach (int channel in dense_lane_channels)
                {
                    text.Append('#').Append(measure.ToString("000")).Append(channel.ToString("X2")).Append(':').AppendLine(notes);
                    text.Append('#').Append(measure.ToString("000")).Append((channel + 0x40).ToString("X2")).Append(':').AppendLine(sustainedHold);
                    text.Append('#').Append(measure.ToString("000")).Append((channel + 0xC0).ToString("X2")).Append(':').AppendLine(mines);
                }
            }

            var chart = new BmsBeatmapDecoder().DecodeText(text.ToString(), "c5-dense-14k.bms");
            var ruleset = new BmsRuleset();
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), ruleset).Convert();

            static string tokenLine(int count, Func<int, string> token)
            {
                var result = new StringBuilder(count * 2);

                for (int index = 0; index < count; index++)
                    result.Append(token(index));

                return result.ToString();
            }
        }

        private static void writeDenseFourteenKeyAuthorPackage(string root)
        {
            string notes = Path.Combine(root, "notes");
            Directory.CreateDirectory(notes);
            File.WriteAllBytes(Path.Combine(notes, "dense.png"), createPng(new Rgba32(50, 210, 180, 255)));

            var skin = new StringBuilder()
                .AppendLine("[General]")
                .AppendLine("Name: C5 dense 14K author scene")
                .AppendLine("Author: OMS tests")
                .AppendLine("Version: 2.7")
                .AppendLine()
                .AppendLine("[Bms]")
                .AppendLine("Keymode: 14K")
                .AppendLine("LongNoteBodyWidth: 0.4")
                .AppendLine()
                .AppendLine("[GameplaySkin.Common:1]");
            skin.AppendLine("Target: Global ruleset=bms keymode=14k stage-mode=dual")
                .AppendLine("decoration: resource Provide \"notes/dense\"");
            appendLaneMaterialTarget(skin, "bms.group.deck-1", "bms.lane.key-1", 0, 0, 1, 1, 1, 1);
            appendMovingMaterialEntries(skin);
            appendStageMaterialTarget(skin, "bms.group.deck-1", 0, 0);
            skin.AppendLine("hud.text: resource Provide \"notes/dense\"");
            appendGroupMaterialTarget(skin, "bms.group.deck-1", 0, 0);
            skin.AppendLine("playfield.bar-line: resource Provide \"notes/dense\"");
            appendLaneMaterialTarget(skin, "bms.group.deck-2", "bms.lane.key-8", 1, 1, 8, 8, 0, 0);
            appendMovingMaterialEntries(skin);
            appendStageMaterialTarget(skin, "bms.group.deck-2", 1, 1);
            skin.AppendLine("hud.text: resource Provide \"notes/dense\"");
            appendGroupMaterialTarget(skin, "bms.group.deck-2", 1, 1);
            skin.AppendLine("playfield.bar-line: resource Provide \"notes/dense\"");
            File.WriteAllText(Path.Combine(root, "skin.ini"), skin.ToString());

            var manifest = new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = new JArray(),
            };
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME), manifest.ToString());

            var children = new JArray
            {
                sceneNode("node.decoration", "container", new JObject { ["kind"] = "global" }, "decoration", new JObject
                {
                    ["opacity"] = 0.5,
                    ["visible"] = true,
                }),
                sceneNode("node.hud.deck-1", "text", stageTarget("bms.group.deck-1", 0), "hud.text", new JObject
                {
                    ["text"] = "0",
                    ["font-size"] = 24,
                    ["alignment"] = "centre",
                }),
                sceneNode("node.hud.deck-2", "text", stageTarget("bms.group.deck-2", 1), "hud.text", new JObject
                {
                    ["text"] = "0",
                    ["font-size"] = 24,
                    ["alignment"] = "centre",
                }),
            };
            appendMovingSceneNodes(children, "deck-1", "bms.group.deck-1", "bms.lane.key-1", 0, 1);
            appendMovingSceneNodes(children, "deck-2", "bms.group.deck-2", "bms.lane.key-8", 1, 8);

            JObject rootNode = sceneNode("node.root", "container", new JObject { ["kind"] = "global" }, null, new JObject());
            rootNode["blend"] = "inherit";
            rootNode["children"] = children;
            var scene = new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = rootNode,
                ["tracks"] = new JArray(new JObject
                {
                    ["id"] = "track.decoration-opacity",
                    ["type"] = "tween",
                    ["target"] = "node.decoration",
                    ["property"] = "opacity",
                    ["easing"] = "in-out",
                    ["loop"] = true,
                    ["keyframes"] = new JArray
                    {
                        new JObject { ["id"] = "keyframe.decoration-low", ["time"] = 0, ["value"] = 0.25 },
                        new JObject { ["id"] = "keyframe.decoration-high", ["time"] = 1000, ["value"] = 0.75 },
                    },
                }),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "binding.combo.deck-1",
                        ["target"] = "node.hud.deck-1",
                        ["property"] = "text",
                        ["source"] = "combo.value",
                    },
                    new JObject
                    {
                        ["id"] = "binding.combo.deck-2",
                        ["target"] = "node.hud.deck-2",
                        ["property"] = "text",
                        ["source"] = "combo.value",
                    },
                },
                ["templates"] = new JArray(),
                ["instances"] = new JArray(),
            };
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), scene.ToString());
        }

        private static void appendLaneMaterialTarget(
            StringBuilder skin,
            string group,
            string lane,
            int groupLogical,
            int groupVisual,
            int globalLogical,
            int globalVisual,
            int localLogical,
            int localVisual)
            => skin.Append("Target: Lane ruleset=bms keymode=14k stage-mode=dual group=").Append(group)
                   .Append(" lane=").Append(lane)
                   .Append(" group-logical=").Append(groupLogical)
                   .Append(" group-visual=").Append(groupVisual)
                   .Append(" global-logical=").Append(globalLogical)
                   .Append(" global-visual=").Append(globalVisual)
                   .Append(" group-local-logical=").Append(localLogical)
                   .Append(" group-local-visual=").Append(localVisual)
                   .AppendLine();

        private static void appendGroupMaterialTarget(StringBuilder skin, string group, int groupLogical, int groupVisual)
            => skin.Append("Target: Group ruleset=bms keymode=14k stage-mode=dual group=").Append(group)
                   .Append(" group-logical=").Append(groupLogical)
                   .Append(" group-visual=").Append(groupVisual)
                   .AppendLine();

        private static void appendStageMaterialTarget(StringBuilder skin, string group, int groupLogical, int groupVisual)
            => skin.Append("Target: Stage ruleset=bms keymode=14k stage-mode=dual group=").Append(group)
                   .Append(" group-logical=").Append(groupLogical)
                   .Append(" group-visual=").Append(groupVisual)
                   .AppendLine();

        private static void appendMovingMaterialEntries(StringBuilder skin)
            => skin.AppendLine("object.note: resource Provide \"notes/dense\"")
                   .AppendLine("object.long-note.head: resource Provide \"notes/dense\"")
                   .AppendLine("object.long-note.body: resource Provide \"notes/dense\"")
                   .AppendLine("object.long-note.tail: resource Provide \"notes/dense\"")
                   .AppendLine("object.mine: resource Provide \"notes/dense\"");

        private static void appendMovingSceneNodes(
            JArray children,
            string suffix,
            string group,
            string lane,
            int groupIndex,
            int laneIndex)
        {
            JObject laneTarget = new JObject { ["kind"] = "lane", ["id"] = lane, ["index"] = laneIndex };
            JObject properties = new JObject { ["opacity"] = 0.75, ["visible"] = true };
            children.Add(sceneNode($"node.note.{suffix}", "container", laneTarget, "object.note", properties));
            children.Add(sceneNode($"node.long-note-head.{suffix}", "container", laneTarget, "object.long-note.head", properties));
            children.Add(sceneNode($"node.long-note-body.{suffix}", "container", laneTarget, "object.long-note.body", properties));
            children.Add(sceneNode($"node.long-note-tail.{suffix}", "container", laneTarget, "object.long-note.tail", properties));
            children.Add(sceneNode($"node.mine.{suffix}", "container", laneTarget, "object.mine", properties));
            children.Add(sceneNode(
                $"node.bar-line.{suffix}",
                "container",
                new JObject { ["kind"] = "group", ["id"] = group, ["index"] = groupIndex },
                "playfield.bar-line",
                properties));
        }

        private static JObject stageTarget(string group, int index)
            => new JObject { ["kind"] = "stage", ["id"] = group, ["index"] = index };

        private static JObject sceneNode(string id, string type, JObject target, string? slot, JObject properties)
        {
            var node = new JObject
            {
                ["id"] = id,
                ["type"] = type,
                ["target"] = target.DeepClone(),
                ["blend"] = "alpha",
                ["properties"] = properties.DeepClone(),
                ["effects"] = new JArray(),
                ["children"] = new JArray(),
            };

            if (slot != null)
                node["slot"] = slot;

            return node;
        }

        private sealed partial class DenseFourteenKeyHost : SkinProvidingContainer
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached]
            private readonly BmsRulesetConfigManager rulesetConfig;

            [Cached(typeof(ScoreProcessor))]
            private readonly ScoreProcessor scoreProcessor;

            [Cached(typeof(HealthProcessor))]
            private readonly HealthProcessor healthProcessor;

            private readonly ManualClock sourceClock;
            private readonly FramedClock frameClock;

            public DrawableBmsRuleset Drawable { get; }

            public GameplayLayoutPublicationProbe LayoutProbe { get; }

            public double EndTime { get; }

            public bool Ready => LayoutProbe.Publication != null
                                 && Drawable.IsLoaded
                                 && Drawable.GameplaySkinEventRuntime?.IsLoaded == true
                                 && Drawable.GameplaySkinSceneRuntime?.IsLoaded == true
                                 && Drawable.GameplaySkinSceneRuntime.PendingCreationCount == 0
                                 && Drawable.PreStartSpeedPreviewMaterialSet != null
                                 && Drawable.BgaMaterialSet != null
                                 && Drawable.HudMaterialSet != null
                                 && Drawable.GaugeMaterialSet != null
                                 && Drawable.ComboMaterialSet != null
                                 && Drawable.Playfield.GroupContainers.All(group => group.IsLoaded)
                                 && Drawable.Playfield.Lanes.All(lane => lane.IsLoaded);

            public DenseFourteenKeyHost(SkinManager skinManager, BmsBeatmap beatmap)
                : base(skinManager.CurrentSkin.Value)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;

                var ruleset = new BmsRuleset();
                rulesetConfig = new BmsRulesetConfigManager(null, ruleset.RulesetInfo);
                scoreProcessor = ruleset.CreateScoreProcessor();
                healthProcessor = ruleset.CreateHealthProcessor(0);
                healthProcessor.ApplyBeatmap(beatmap);
                scoreProcessor.ApplyBeatmap(beatmap);
                sourceClock = new ManualClock { CurrentTime = 0, IsRunning = false };
                frameClock = new FramedClock(sourceClock);
                frameClock.ProcessFrame();
                EndTime = Math.Max(
                    beatmap.HitObjects.OfType<BmsHoldNote>().Max(hold => hold.EndTime),
                    beatmap.Mines.Max(mine => mine.StartTime));

                Drawable = (DrawableBmsRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Drawable.Clock = frameClock;
                LayoutProbe = new GameplayLayoutPublicationProbe();
                var provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null, prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            Drawable,
                            LayoutProbe,
                        },
                    },
                };
                InternalChild = provider;
            }

            public void AdvanceTo(double time)
            {
                sourceClock.CurrentTime = time;
                frameClock.ProcessFrame();
            }
        }

        private sealed class DensePoolObservation
        {
            public readonly HashSet<DrawableBmsHitObject> AllNotes = new HashSet<DrawableBmsHitObject>();
            public readonly HashSet<DrawableBmsHoldNote> AllHolds = new HashSet<DrawableBmsHoldNote>();
            public readonly HashSet<DrawableBmsMine> AllMines = new HashSet<DrawableBmsMine>();
            public readonly HashSet<DrawableBmsBarLine> AllBarLines = new HashSet<DrawableBmsBarLine>();
            public readonly HashSet<DrawableBmsHitObject> UsedNotes = new HashSet<DrawableBmsHitObject>();
            public readonly HashSet<DrawableBmsHoldNote> UsedHolds = new HashSet<DrawableBmsHoldNote>();
            public readonly HashSet<DrawableBmsMine> UsedMines = new HashSet<DrawableBmsMine>();
            public readonly HashSet<DrawableBmsBarLine> UsedBarLines = new HashSet<DrawableBmsBarLine>();
            private readonly HashSet<DrawableBmsHoldNoteHead> allHeads = new HashSet<DrawableBmsHoldNoteHead>();
            private readonly HashSet<DrawableBmsHoldNoteTail> allTails = new HashSet<DrawableBmsHoldNoteTail>();
            private readonly HashSet<DrawableBmsHoldNoteBodyTick> allBodyTicks = new HashSet<DrawableBmsHoldNoteBodyTick>();
            private readonly Dictionary<PoolableDrawable, int> owningTarget = new Dictionary<PoolableDrawable, int>();
            public readonly HashSet<int> UsedHoldLanes = new HashSet<int>();
            public readonly HashSet<int> UsedMineLanes = new HashSet<int>();
            public readonly HashSet<int> UsedBarLineGroups = new HashSet<int>();

            public int MaximumCreatedPerLaneAndFamily { get; private set; }
            public int MaximumBodyTicks { get; private set; }
            public int MaximumActiveObjects { get; private set; }
            public int MaximumSceneCreationsPerFrame { get; private set; }
            public int MaximumRuntimeInstances { get; private set; }
            public int SceneAppliedMineCount { get; private set; }
            public int SceneAppliedBarLineCount { get; private set; }

            public IReadOnlyCollection<object> AllMainObjects
                => AllNotes.Cast<object>().Concat(AllHolds).Concat(AllMines).Concat(AllBarLines).ToArray();

            public bool PreparedVisualLoadsSettled
                => AllNotes.Cast<DrawableHitObject>()
                           .Concat(AllHolds)
                           .SelectMany(hitObject => hitObject.ChildrenOfType<BmsAsyncNoteDrawable>())
                           .All(host => host.PendingLoadTask?.IsCompleted != false);

            public void Observe(DrawableBmsRuleset drawable, GameplaySkinSceneRuntimeHost sceneHost)
            {
                int active = 0;
                (DrawableHitObject Drawable, int Target)[] acquired = drawable.Playfield.Lanes
                    .SelectMany((lane, laneIndex) => lane.AllHitObjects.Select(hitObject => (hitObject, laneIndex)))
                    .ToArray();
                (DrawableHitObject Drawable, int Target)[] acquiredBarLines = drawable.Playfield.BarLinePlayfields
                    .SelectMany(owner => owner.AllHitObjects.Select(hitObject => (hitObject, owner.GroupLogicalIndex)))
                    .ToArray();
                DrawableBmsHitObject[] notes = acquired.Where(candidate => candidate.Drawable.GetType() == typeof(DrawableBmsHitObject))
                                                            .Select(candidate => (DrawableBmsHitObject)candidate.Drawable)
                                                            .ToArray();
                DrawableBmsHoldNote[] holds = acquired.Select(candidate => candidate.Drawable).OfType<DrawableBmsHoldNote>().ToArray();
                DrawableBmsMine[] mines = acquired.Select(candidate => candidate.Drawable).OfType<DrawableBmsMine>().ToArray();
                DrawableBmsBarLine[] bars = acquiredBarLines.Select(candidate => candidate.Drawable).OfType<DrawableBmsBarLine>().ToArray();
                DrawableBmsHoldNoteHead[] heads = holds.SelectMany(hold => hold.ChildrenOfType<DrawableBmsHoldNoteHead>()).ToArray();
                DrawableBmsHoldNoteTail[] tails = holds.SelectMany(hold => hold.ChildrenOfType<DrawableBmsHoldNoteTail>()).ToArray();
                DrawableBmsHoldNoteBodyTick[] bodyTicks = holds.SelectMany(hold => hold.ChildrenOfType<DrawableBmsHoldNoteBodyTick>()).ToArray();

                foreach ((DrawableHitObject candidate, int target) in acquired.Concat(acquiredBarLines))
                    owningTarget.TryAdd(candidate, target);

                foreach (DrawableBmsHoldNote hold in holds)
                {
                    int lane = owningTarget[hold];

                    foreach (PoolableDrawable component in hold.ChildrenOfType<PoolableDrawable>())
                        owningTarget.TryAdd(component, lane);
                }

                allHeads.UnionWith(heads);
                allTails.UnionWith(tails);
                allBodyTicks.UnionWith(bodyTicks);

                observe(notes, AllNotes, UsedNotes, null, ref active);
                observe(holds, AllHolds, UsedHolds, UsedHoldLanes, ref active);
                observe(mines, AllMines, UsedMines, UsedMineLanes, ref active);
                observe(bars, AllBarLines, UsedBarLines, UsedBarLineGroups, ref active);

                MaximumBodyTicks = Math.Max(MaximumBodyTicks, allBodyTicks.Count);
                MaximumCreatedPerLaneAndFamily = Math.Max(MaximumCreatedPerLaneAndFamily, new[]
                {
                    maximumPerLane(AllNotes),
                    maximumPerLane(AllHolds),
                    maximumPerLane(AllMines),
                    maximumPerLane(AllBarLines),
                    maximumPerLane(allHeads),
                    maximumPerLane(allTails),
                }.Max());

                if (MaximumCreatedPerLaneAndFamily > BmsLane.MAXIMUM_NOTE_POOL_SIZE
                    || MaximumBodyTicks > drawable.Playfield.Lanes.Count * BmsLane.MAXIMUM_BODY_TICK_POOL_SIZE)
                {
                    throw new AssertionException("A dense BMS lane exceeded its frozen production drawable-pool ceiling.");
                }

                MaximumActiveObjects = Math.Max(MaximumActiveObjects, active);
                MaximumSceneCreationsPerFrame = Math.Max(MaximumSceneCreationsPerFrame, sceneHost.CreatedThisFrame);
                MaximumRuntimeInstances = Math.Max(MaximumRuntimeInstances, sceneHost.RuntimeInstanceCount);
                SceneAppliedMineCount = AllMines.Count(mine => mine.AppliedSceneNodeIds.Count > 0);
                SceneAppliedBarLineCount = AllBarLines.Count(bar => bar.AppliedSceneNodeIds.Count > 0);

                int maximumPerLane<T>(IEnumerable<T> candidates)
                    where T : PoolableDrawable
                    => candidates.GroupBy(candidate => owningTarget[candidate]).Select(group => group.Count()).DefaultIfEmpty().Max();
            }

            private void observe<T>(
                IEnumerable<T> candidates,
                ISet<T> all,
                ISet<T> used,
                ISet<int>? usedLanes,
                ref int active)
                where T : DrawableHitObject
            {
                foreach (T candidate in candidates)
                {
                    all.Add(candidate);

                    if (candidate.Entry == null)
                        continue;

                    active++;
                    used.Add(candidate);

                    usedLanes?.Add(owningTarget[candidate]);
                }
            }
        }

        private sealed class DenseEventObservation
        {
            private long epoch = -1;
            private long sequence = -1;

            public readonly Dictionary<GameplaySkinObjectKind, int> ObjectEdges = Enum.GetValues<GameplaySkinObjectKind>()
                .ToDictionary(kind => kind, _ => 0);
            public readonly HashSet<string> GroupsWithObjectEdges = new HashSet<string>(StringComparer.Ordinal);

            public int MaximumPending { get; set; }
            public int MaximumDrain { get; set; }
            public int TotalEnvelopes { get; private set; }

            public void Observe(GameplaySkinEventEnvelope envelope)
            {
                if (envelope.Epoch != epoch)
                {
                    if (epoch >= 0 && envelope.Epoch <= epoch)
                        throw new AssertionException("The production event epoch did not advance monotonically.");

                    epoch = envelope.Epoch;
                    sequence = envelope.Sequence;
                }
                else
                {
                    if (envelope.Sequence <= sequence)
                        throw new AssertionException("The production event sequence did not advance monotonically.");

                    sequence = envelope.Sequence;
                }

                TotalEnvelopes++;

                if (envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Edge
                    && envelope.Payload is GameplaySkinObjectEventPayload objectPayload)
                {
                    ObjectEdges[objectPayload.State.Kind]++;
                    GroupsWithObjectEdges.Add(objectPayload.State.GroupId.Value);
                }
            }
        }
    }
}
