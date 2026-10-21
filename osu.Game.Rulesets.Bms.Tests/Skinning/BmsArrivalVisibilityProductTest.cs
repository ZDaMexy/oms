// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Bms.UI.Scrolling;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center)]
        public void TestArrivalVisibilityUsesNativeBmsPositionAndLiveOffsetAndLift(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            var lift = new BmsModLift();
            double originalRange = 0;

            AddStep("select the real SP and DP lane package", () =>
            {
                (_, candidate) = createCandidate(writeLaneMaterialTemplatePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("exact package selected", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount fixed-clock native scrolling owners", () =>
            {
                var fixture = new ExactBmsProductionFixture(createLaneMaterialTemplateChart(keymode), "arrival-visibility.bme", style,
                    mods: new Mod[] { lift }, initialGameplayTime: -1000, keymodeOverride: keymode);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                renderer.ShowBms();
            });
            AddUntilStep("native areas have applied their actual LIFT geometry", () => renderer.BmsReady
                && renderer.BmsDrawable.Playfield.Lanes.All(lane => lane.HitObjectArea.HasAppliedProjection && lane.HitObjectContainer.DrawHeight > 0));
            AddStep("use the original constant algorithm", () => renderer.BmsDrawable.VisualisationMethod = ScrollVisualisationMethod.Constant);
            AddStep("native positions use the actual offset rather than the 500ms preload allowance", () =>
            {
                var ruleset = renderer.BmsDrawable;
                var projection = (IGameplaySkinArrivalVisibilityProjection)ruleset;
                originalRange = ruleset.ScrollingInfo.TimeRange.Value;
                double sourceTime = ruleset.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
                double[] noteTimes = renderer.BmsBeatmap.HitObjects.Select(note => note.StartTime).ToArray();

                ruleset.Playfield.AutomaticVisualOffsetEnabled.Value = true;
                foreach (double offset in new[] { -500d, 0, 500 })
                {
                    ruleset.Playfield.VisualOffset.Value = offset;
                    foreach (BmsHitObject note in renderer.BmsBeatmap.HitObjects.OfType<BmsHitObject>().Where(note => !note.AutoPlay))
                    {
                        GameplaySkinLaneId laneId = ruleset.LayoutSnapshot.Context.Topology.LanesInLogicalOrder[note.LaneIndex].Identity.Id;
                        double entry = note.StartTime - originalRange - offset;
                        Assert.That(projection.IsArrivalVisible(laneId, note.StartTime, entry - 1), Is.False, $"{laneId}: offset {offset}, before entry");
                        Assert.That(projection.IsArrivalVisible(laneId, note.StartTime, entry + 1), Is.True, $"{laneId}: offset {offset}, after entry");
                        Assert.That(projection.IsArrivalVisible(laneId, note.StartTime, note.StartTime + 1000), Is.True,
                            "A 200ms prediction must not miss fast notes which have already crossed the hit line.");
                    }
                }

                Assert.That(ruleset.GameplaySkinEventRuntime.AuthoritativeGameplayTime, Is.EqualTo(sourceTime), "Projection never seeks the real clock.");
                Assert.That(renderer.BmsBeatmap.HitObjects.Select(note => note.StartTime), Is.EqualTo(noteTimes), "Projection never moves chart events.");

                GameplaySkinLaneId firstLane = ruleset.LayoutSnapshot.Context.Topology.LanesInLogicalOrder[0].Identity.Id;
                projection.IsArrivalVisible(firstLane, 2000, 1000);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++)
                    projection.IsArrivalVisible(firstLane, 2000, 1000);
                Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.Zero, "The live projection allocates nothing per sample.");
            });
            AddStep("increase only the native LIFT setting", () => lift.LiftUnits.Value = 500);
            AddUntilStep("native viewport and time range follow the new LIFT", () => renderer.BmsDrawable.Playfield.GameplaySkinLiftState.Units == 500
                && renderer.BmsDrawable.ScrollingInfo.TimeRange.Value < originalRange);
            AddStep("arrival follows the changed native viewport without a new publication", () =>
            {
                var ruleset = renderer.BmsDrawable;
                var projection = (IGameplaySkinArrivalVisibilityProjection)ruleset;
                BmsHitObject note = renderer.BmsBeatmap.HitObjects.OfType<BmsHitObject>().First(note => !note.AutoPlay);
                GameplaySkinLaneId laneId = ruleset.LayoutSnapshot.Context.Topology.LanesInLogicalOrder[note.LaneIndex].Identity.Id;
                double entry = note.StartTime - ruleset.ScrollingInfo.TimeRange.Value - ruleset.Playfield.VisualOffset.Value;
                Assert.That(projection.IsArrivalVisible(laneId, note.StartTime, entry - 1), Is.False);
                Assert.That(projection.IsArrivalVisible(laneId, note.StartTime, entry + 1), Is.True);
            });
            AddStep("retire native arrival owners", () => renderer.Expire());
            AddUntilStep("native owners detached", () => renderer.Parent == null);
        }

        [Test]
        public void TestArrivalVisibilityFollowsNativeBmsStopPlateau()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;

            AddStep("select a real package", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("exact package selected", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount a real native lane", () =>
            {
                var fixture = new ExactBmsProductionFixture("#TITLE STOP arrival\n#BPM 120\n#WAV01 note.wav\n#00211:01\n",
                    "stop-arrival.bme", BmsPlayfieldStyle.Center, initialGameplayTime: -1000, keymodeOverride: BmsKeymode.Key7K);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                renderer.ShowBms();
            });
            AddUntilStep("real native viewport loaded", () => renderer.BmsReady && renderer.BmsDrawable.Playfield.Lanes[1].HitObjectContainer.DrawHeight > 0);
            AddStep("sample the actual STOP-aware scrolling owner", () =>
            {
                var ruleset = renderer.BmsDrawable;
                var projection = (IGameplaySkinArrivalVisibilityProjection)ruleset;
                double range = ruleset.ScrollingInfo.TimeRange.Value;
                var profile = new BmsScrollProfile(new[] { 0d, 1000, 2000, 4000 }, new[] { 0d, range, range, 3 * range }, 500);
                BmsLane lane = ruleset.Playfield.Lanes[1];
                ((BmsScrollingInfo)lane.ScrollingInfo).EngageStopMotion(new BmsStopMotionScrollAlgorithm(profile));
                GameplaySkinLaneId laneId = lane.LayoutSnapshotLane!.LaneId;

                // The chart freezes between 1000 and 2000. Its first note at 4000 reaches one screen's distance
                // only at 3000; subtracting the visible milliseconds from hit time cannot find this boundary.
                Assert.That(lane.HitObjectContainer.PositionAtTime(4000, 1000), Is.EqualTo(lane.HitObjectContainer.PositionAtTime(4000, 1999)));
                Assert.That(projection.IsArrivalVisible(laneId, 4000, 1000), Is.False);
                Assert.That(projection.IsArrivalVisible(laneId, 4000, 1999), Is.False);
                Assert.That(projection.IsArrivalVisible(laneId, 4000, 2999), Is.False);
                Assert.That(projection.IsArrivalVisible(laneId, 4000, 3001), Is.True);
            });
            AddStep("retire STOP owner", () => renderer.Expire());
            AddUntilStep("STOP owner detached", () => renderer.Parent == null);
        }

        [Test]
        public void TestArrivalCueFadesBeforeTheFirstRowAndStaysRetiredAcrossRewindUntilRetry()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinEventRuntimeHost events = null!;
            double entry = 0;

            AddStep("select an exact DP package", () =>
            {
                (_, candidate) = createCandidate(writeLaneMaterialTemplatePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("exact DP package selected", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount a first chord on both decks and a later key", () =>
            {
                const string chart = "#TITLE Shared first-row cue\n#BPM 120\n#WAV01 note.wav\n#00211:01\n#00221:01\n#00312:01\n";
                var fixture = new ExactBmsProductionFixture(chart, "first-row-cue.bms", BmsPlayfieldStyle.Center,
                    initialGameplayTime: -1000, keymodeOverride: BmsKeymode.Key14K);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                renderer.ShowBms();
            });
            AddUntilStep("native first-row geometry loaded", () => renderer.BmsReady
                && renderer.BmsDrawable.Playfield.Lanes.All(lane => lane.HitObjectContainer.DrawHeight > 0));
            AddStep("retain actual host and seek outside the prediction window", () =>
            {
                var ruleset = renderer.BmsDrawable;
                events = ruleset.GameplaySkinEventRuntime!;
                ruleset.VisualisationMethod = ScrollVisualisationMethod.Constant;
                ruleset.Playfield.VisualOffset.Value = 0;
                ruleset.Playfield.AutomaticVisualOffsetEnabled.Value = false;
                entry = 4000 - ruleset.ScrollingInfo.TimeRange.Value;
                renderer.AdvanceBmsTo(entry - 400);
            });
            AddUntilStep("host reaches the earlier chart time", () => Math.Abs(events.AuthoritativeGameplayTime - (entry - 400)) < 0.01);
            AddAssert("first-row cue has not started fading", () => captureArrivalTiming(events).ArrivalCueFadeStartTime, () => Is.Null);
            AddStep("advance toward native appearance without any key input", () => renderer.AdvanceBmsTo(entry - 100));
            AddUntilStep("host reaches the real prediction window", () => Math.Abs(events.AuthoritativeGameplayTime - (entry - 100)) < 0.01);
            AddStep("shared cue starts before either first-row note appears", () =>
            {
                GameplaySkinTimingStateSnapshot timing = captureArrivalTiming(events);
                Assert.That(timing.ArrivalCueFadeStartTime, Is.Not.Null);
                Assert.That(timing.ArrivalCueFadeStartTime!.Value, Is.LessThan(entry - 100));
                using GameplaySkinEventSubscription observer = events.EventStream.Subscribe();
                Assert.That(observer.TryDequeue(out GameplaySkinEventEnvelope? snapshot), Is.True);
                GameplaySkinChartPresentation chart = ((GameplaySkinStateEventPayload)snapshot!.Payload).State.ChartPresentation!;
                Assert.That(chart.ArrivalCueAvailable, Is.True);
                Assert.That(chart.Arrivals.Count(arrival => chart.IsFirstRow(arrival.LaneId, arrival.GroupId)), Is.EqualTo(2));
                Assert.That(chart.Arrivals.Count, Is.EqualTo(3), "The older per-lane arrival metadata is retained unchanged.");
            });
            AddStep("reach the incoming edge without hitting either note", () => renderer.AdvanceBmsTo(entry + 1));
            AddUntilStep("host reaches native first visibility", () => Math.Abs(events.AuthoritativeGameplayTime - (entry + 1)) < 0.01);
            AddAssert("cue is completely gone before the first hit", () => captureArrivalTiming(events).ArrivalCueFadeStartTime,
                () => Is.LessThanOrEqualTo(events.AuthoritativeGameplayTime - 200));
            AddStep("rewind before the opening prediction window", () => renderer.AdvanceBmsTo(entry - 1000));
            AddUntilStep("native rewind converges", () => Math.Abs(events.AuthoritativeGameplayTime - (entry - 1000)) < 0.01);
            AddAssert("ordinary rewind does not replay the cue", () => captureArrivalTiming(events).ArrivalCueFadeStartTime,
                () => Is.LessThanOrEqualTo(events.AuthoritativeGameplayTime - 200));
            AddStep("perform the actual host retry reset", () => events.RequestReset(GameplaySkinEventResetReason.Retry));
            AddUntilStep("a strong retry restores the first-row cue", () => captureArrivalTiming(events).ArrivalCueFadeStartTime == null);
            AddStep("retire first-row owners", () => renderer.Expire());
            AddUntilStep("first-row owners detached", () => renderer.Parent == null);
        }

        [TestCase(5, false)]
        [TestCase(6, false)]
        [TestCase(7, false)]
        [TestCase(8, true)]
        [TestCase(14, false)]
        [TestCase(16, true)]
        public void TestArrivalVisibilityUsesActualManiaColumnsAndVelocity(int keys, bool reverse)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            ManiaRulesetConfigManager config = null!;
            ManiaScrollingDirection originalDirection = default;

            AddStep("select an ordinary package without changing mania note behaviour", () =>
            {
                (_, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
                config = (ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset())!;
                originalDirection = config.Get<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection);
                config.SetValue(ManiaRulesetSetting.ScrollDirection, reverse ? ManiaScrollingDirection.Up : ManiaScrollingDirection.Down);
            });
            AddUntilStep("exact package selected", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount actual single or dual mania stages with a two-times velocity control point", () =>
            {
                int[] stages = keys > 8 ? new[] { keys / 2, keys / 2 } : new[] { keys };
                Add(renderer = new ExactLayoutJourneyHost(manager, maniaStageColumns: stages));
                renderer.ManiaDrawable.Beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                renderer.ManiaDrawable.Beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 2 });
                renderer.ShowMania();
            });
            AddUntilStep("every original column has real dimensions", () => renderer.ManiaReady
                && renderer.ManiaDrawable.Playfield.Stages.All(stage => stage.Columns.All(column => column.HitObjectContainer.DrawHeight > 0)));
            AddStep("first-row prediction uses real SV and direction on every column", () =>
            {
                var ruleset = renderer.ManiaDrawable;
                var projection = (IGameplaySkinArrivalVisibilityProjection)ruleset;
                double entry = 2000 - ruleset.ScrollingInfo.TimeRange.Value / 2;
                foreach (GameplaySkinLaneTopologyEntry lane in ruleset.LayoutSnapshot.Context.Topology.LanesInLogicalOrder)
                {
                    Assert.That(projection.IsArrivalVisible(lane.Identity.Id, 2000, entry - 1), Is.False, $"{lane.Identity.Id}: before actual SV entry");
                    Assert.That(projection.IsArrivalVisible(lane.Identity.Id, 2000, entry + 1), Is.True, $"{lane.Identity.Id}: after actual SV entry");
                    Assert.That(projection.IsArrivalVisible(lane.Identity.Id, 2000, 3000), Is.True, "Prediction remains true after crossing the hit line.");
                }
                Assert.That(ruleset.LayoutSnapshot.Context.Topology.LanesInLogicalOrder.Count, Is.EqualTo(keys));
                Assert.That(ruleset.ScrollingInfo.Direction.Value, Is.EqualTo(reverse ? ScrollingDirection.Up : ScrollingDirection.Down));
            });
            AddStep("retire mania owner and restore isolated direction preference", () =>
            {
                renderer.Expire();
                config.SetValue(ManiaRulesetSetting.ScrollDirection, originalDirection);
            });
            AddUntilStep("mania owner detached", () => renderer.Parent == null);
        }

        private static GameplaySkinTimingStateSnapshot captureArrivalTiming(GameplaySkinEventRuntimeHost events)
        {
            using GameplaySkinEventSubscription observer = events.EventStream.Subscribe();
            Assert.That(observer.TryDequeue(out GameplaySkinEventEnvelope? snapshot), Is.True);
            return ((GameplaySkinStateEventPayload)snapshot!.Payload).State.Timing;
        }
    }
}
