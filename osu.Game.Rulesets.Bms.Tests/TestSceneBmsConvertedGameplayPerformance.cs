// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    /// <summary>
    /// Synthetic chart only: measures the real Player's retained sample-only drawables, note sample preparation and
    /// empty-strike feedback. Timings are reported for before/after comparison, not used as machine-dependent gates.
    /// </summary>
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsConvertedGameplayPerformance : PlayerTestScene
    {
        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };
        private readonly FramedClock referenceClock;
        private bool nativeMania;
        private GameplaySkinEventSubscription? skinSubscription;
        private readonly List<GameplaySkinEventEnvelope> skinEvents = new List<GameplaySkinEventEnvelope>();

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        public TestSceneBmsConvertedGameplayPerformance()
        {
            referenceClock = new FramedClock(manualClock);
        }

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            if (nativeMania)
            {
                var headSample = new HitSampleInfo(HitSampleInfo.HIT_NORMAL);
                return new ManiaBeatmap(new StageDefinition(7))
                {
                    BeatmapInfo = { Ruleset = ruleset },
                    HitObjects = new List<ManiaHitObject>
                    {
                        new Note { StartTime = 4000, Column = 0, Samples = new[] { headSample } },
                        new HoldNote
                        {
                            StartTime = 8000, EndTime = 9000, Column = 1,
                            Samples = new[] { headSample },
                            NodeSamples = new List<IList<HitSampleInfo>> { new[] { headSample }, new[] { headSample } },
                        },
                        new Note { StartTime = 20000, Column = 4 },
                    },
                };
            }

            var text = new StringBuilder(@"
#TITLE Converted gameplay performance fixture
#BPM 120
#WAVAA bgm.wav
#WAVBB key.wav
#WAVCC scratch.wav
#WAVDD hold.wav
#WAVEE tail.wav
#WAVFF sentinel.wav
#00211:BB
#00452:DDEE
#01015:FF
");

            for (int measure = 1; measure <= 6; measure++)
            {
                text.AppendLine($"#{measure:000}01:{string.Concat(Enumerable.Repeat("AA", 64))}");
                text.AppendLine($"#{measure:000}16:{string.Concat(Enumerable.Repeat("CC", 16))}");
            }

            return new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(text.ToString(), "converted-performance.bme"))
            {
                BeatmapInfo = { Ruleset = new BmsRuleset().RulesetInfo },
            };
        }

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        [TestCase(false)]
        [TestCase(true)]
        public void TestConvertedGameplayCostProfile(bool automatic)
        {
            CreateTest(() => AddStep("configure deterministic converted player", () =>
            {
                nativeMania = false;
                skinSubscription?.Dispose();
                skinSubscription = null;
                skinEvents.Clear();
                manualClock.CurrentTime = 0;
                referenceClock.ProcessFrame();
                var config = (ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset())!;
                config.SetValue(ManiaRulesetSetting.AutoKeysoundForBms, automatic);
                config.SetValue(ManiaRulesetSetting.ScrollSpeed, 20.0);
            }));

            AddUntilStep("store and track ready", () => store != null
                                                       && store.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready)
                                                       && Beatmap.Value.Track.IsRunning);
            AddStep("record requests and initial graph", () =>
            {
                store!.EnablePlaybackLogForTesting();
                report($"automatic={automatic} loaded_sample_only_drawables={sampleOnlyDrawableCount()} "
                       + $"sample_only_events={Player.DrawableRuleset.Objects.Count(isSampleOnly)}");
                Assert.That(sampleOnlyDrawableCount(), Is.LessThan(240), "future audio events must not retain a full drawable tree");
                skinSubscription = ((DrawableManiaRuleset)Player.DrawableRuleset).GameplaySkinEventStream.Subscribe();
            });
            AddStep("measure first production feedback lookup without warmup", () =>
            {
                Func<HitObject?> getObject = getFeedbackObject(0);
                long before = GC.GetAllocatedBytesForCurrentThread();
                HitObject? selected = getObject();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                report($"automatic={automatic} first_feedback_lookup_allocated_bytes={allocated}");
                var column = ((DrawableManiaRuleset)Player.DrawableRuleset).Playfield.GetColumn(0);
                Assert.That(selected, Is.SameAs(column.HitObjectContainer.Entries.First().HitObject), "before the first event the original silent fallback is retained");
                Assert.That(selected != null && isSampleOnly(selected), Is.True);
                Assert.That(allocated, Is.LessThan(1024), "whole-column lookup preparation must finish during loading, not the first empty strike");
            });

            advanceTo(3900);
            AddStep("record tap sample graph", () => reportNoteSamples("tap", typeof(BmsConvertedKeyNoteHitObject)));
            advanceTo(4000);
            press(ManiaAction.Key1);
            advanceTo(7900);
            AddStep("record hold-head sample graph", () => reportNoteSamples("hold_head", typeof(HeadNote)));
            AddAssert("hold feedback still selects its nested head", () => getFeedbackObject(1)(), () => Is.TypeOf<HeadNote>());
            advanceTo(8000);
            AddStep("press hold", () => bindings.TriggerPressed(ManiaAction.Key2));
            advanceTo(8500);
            AddAssert("held-note feedback still selects its nested tail", () => getFeedbackObject(1)(), () => Is.TypeOf<TailNote>());
            advanceTo(9000);
            AddStep("release hold", () => bindings.TriggerReleased(ManiaAction.Key2));
            advanceTo(14500);

            AddAssert("all BGM requests preserved", () => playCount("bgm.wav"), () => Is.EqualTo(384));
            AddAssert("all scratch requests preserved", () => playCount("scratch.wav"), () => Is.EqualTo(96));
            AddAssert("tap sounds once", () => playCount("key.wav"), () => Is.EqualTo(1));
            AddAssert("hold head sounds once", () => playCount("hold.wav"), () => Is.EqualTo(1));
            AddAssert("hold tail remains silent", () => playCount("tail.wav"), () => Is.Zero);
            AddStep("measure finished-column feedback", () =>
            {
                var column = ((DrawableManiaRuleset)Player.DrawableRuleset).Playfield.GetColumn(0);
                Func<HitObject?> getObject = getFeedbackObject(0);
                Assert.That(getObject(), Is.SameAs(column.HitObjectContainer.Entries.Last().HitObject),
                    "keep the original last-entry fallback, including its silent sample-only sound");

                long before = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                const int iterations = 2048;
                for (int i = 0; i < iterations; i++)
                    getObject();
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                report($"automatic={automatic} finished_column_lookup_iterations={iterations} "
                       + $"elapsed_ms={elapsed:F3} allocated_bytes={allocated} retained_sample_only_drawables={sampleOnlyDrawableCount()}");
                Assert.That(allocated, Is.Zero, "repeated finished-column lookup must reuse the authoritative entry snapshots");
                Assert.That(sampleOnlyDrawableCount(), Is.LessThan(240), "the audio drawable pool must follow the live window, not total event count");
            });

            // Actual presses traverse the production Column feedback path; automatic sound must keep it silent.
            AddStep("repeat real empty strikes", () =>
            {
                for (int i = 0; i < 32; i++)
                {
                    bindings.TriggerPressed(ManiaAction.Key1);
                    bindings.TriggerReleased(ManiaAction.Key1);
                }
            });
            AddAssert("empty strikes never replay BGM through store", () => playCount("bgm.wav"), () => Is.EqualTo(384));
            AddAssert("empty strikes never replay scratch through store", () => playCount("scratch.wav"), () => Is.EqualTo(96));
            AddStep("sample-only sounds never become authored notes", () =>
            {
                skinSubscription!.DrainFrame(skinEvents.Add);
                var objects = skinEvents.Select(envelope => envelope.Payload).OfType<GameplaySkinObjectEventPayload>().Select(payload => payload.State).ToArray();
                Assert.That(objects.Any(state => state.Kind == GameplaySkinObjectKind.Note), Is.True);
                Assert.That(objects.Any(state => state.Kind == GameplaySkinObjectKind.LongNote), Is.True);
                Assert.That(objects.Any(state => state.Kind == GameplaySkinObjectKind.BarLine), Is.True);
                foreach (GameplaySkinObjectStateSnapshot state in objects)
                    assertPlayableSkinObject(state);
            });

            AddStep("rewind before tap", () => Player.GameplayClockContainer.Seek(3900));
            AddUntilStep("rewind caught up", () => Player.DrawableRuleset.FrameStableClock.CurrentTime, () => Is.EqualTo(3900).Within(1));
            AddAssert("feedback cursor re-arms after rewind", () => getFeedbackObject(0)()?.StartTime, () => Is.LessThan(5000));
            AddStep("seek snapshot contains only visible gameplay objects", () =>
            {
                skinSubscription!.DrainFrame(skinEvents.Add);
                var reset = skinEvents.Last(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset);
                var snapshot = ((GameplaySkinStateEventPayload)reset.Payload).State;
                Assert.That(reset.GameplayTime, Is.EqualTo(3900));
                foreach (GameplaySkinObjectStateSnapshot state in snapshot.ActiveObjects)
                    assertPlayableSkinObject(state);
                Assert.That(snapshot.ActiveObjects.Count(state => state.Kind == GameplaySkinObjectKind.Note), Is.EqualTo(1),
                    "The Reset itself must contain the destination's active tap, before any later spawn edges.");
                Assert.That(snapshot.ActiveObjects.Single(state => state.Kind == GameplaySkinObjectKind.Note).State,
                    Is.EqualTo(GameplaySkinObjectState.Scheduled));
                skinSubscription.Dispose();
                skinSubscription = null;
            });
        }

        [Test]
        public void TestNativeManiaKeepsItsOrdinarySamplePreparation()
        {
            CreateTest(() => AddStep("configure ordinary mania", () =>
            {
                nativeMania = true;
                skinSubscription?.Dispose();
                skinSubscription = null;
                manualClock.CurrentTime = 0;
                referenceClock.ProcessFrame();
                var config = (ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset())!;
                config.SetValue(ManiaRulesetSetting.AutoKeysoundForBms, true);
                config.SetValue(ManiaRulesetSetting.ScrollSpeed, 20.0);
            }));
            AddUntilStep("ordinary track ready", () => Beatmap.Value.Track.IsRunning);
            AddAssert("ordinary mania does not host a BMS store", () => store, () => Is.Null);
            advanceTo(3900);
            AddStep("ordinary tap keeps its sample", () => reportNoteSamples("native_tap", typeof(Note), 1));
            advanceTo(7900);
            AddStep("ordinary hold head keeps its sample", () => reportNoteSamples("native_head", typeof(HeadNote), 1));
        }

        private void reportNoteSamples(string label, Type hitObjectType, int expected = 0)
        {
            DrawableNote note = Player.DrawableRuleset.ChildrenOfType<DrawableNote>()
                                      .First(drawable => drawable.HitObject?.GetType() == hitObjectType);
            int samples = note.ChildrenOfType<PoolableSkinnableSample>().Count();
            report($"{label}_attached_samples={samples}");
            Assert.That(samples, Is.EqualTo(expected), "only a store-owned note can skip its ordinary sample player");
        }

        private Func<HitObject?> getFeedbackObject(int columnIndex)
        {
            var column = ((DrawableManiaRuleset)Player.DrawableRuleset).Playfield.GetColumn(columnIndex);
            GameplaySampleTriggerSource source = column.ChildrenOfType<GameplaySampleTriggerSource>().Single();
            return (Func<HitObject?>)typeof(GameplaySampleTriggerSource)
                                    .GetMethod("GetMostValidObject", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .CreateDelegate(typeof(Func<HitObject?>), source);
        }

        private int sampleOnlyDrawableCount()
        {
            var bgmPools = Player.DrawableRuleset.ChildrenOfType<DrawablePool<DrawableBmsConvertedBgmSampleHitObject>>().ToArray();
            var scratchPools = Player.DrawableRuleset.ChildrenOfType<DrawablePool<DrawableBmsConvertedScratchSampleHitObject>>().ToArray();

            if (bgmPools.Length + scratchPools.Length > 0)
                return bgmPools.Sum(pool => pool.CurrentPoolSize) + scratchPools.Sum(pool => pool.CurrentPoolSize);

            // The before-optimisation player retains every non-pooled sample-only drawable in its tree.
            return Player.DrawableRuleset.ChildrenOfType<DrawableBmsConvertedBgmSampleHitObject>().Count()
                   + Player.DrawableRuleset.ChildrenOfType<DrawableBmsConvertedScratchSampleHitObject>().Count();
        }

        private static bool isSampleOnly(HitObject hitObject)
            => hitObject is BmsConvertedBgmSampleHitObject or BmsConvertedScratchSampleHitObject;

        private static void assertPlayableSkinObject(GameplaySkinObjectStateSnapshot state)
        {
            switch (state.Kind)
            {
                case GameplaySkinObjectKind.Note:
                    Assert.That(state.StartTime, Is.EqualTo(4000).Or.EqualTo(20000));
                    break;

                case GameplaySkinObjectKind.LongNote:
                    Assert.That(state.StartTime, Is.EqualTo(8000));
                    Assert.That(state.EndTime, Is.EqualTo(9000));
                    break;

                default:
                    Assert.That(state.Kind, Is.EqualTo(GameplaySkinObjectKind.BarLine));
                    break;
            }
        }

        private static void report(string message)
        {
            Logger.Log($"[converted gameplay profile] {message}");
            TestContext.Progress.WriteLine($"[converted gameplay profile] {message}");
        }

        private void press(ManiaAction action) => AddStep($"press {action}", () =>
        {
            bindings.TriggerPressed(action);
            bindings.TriggerReleased(action);
        });

        private void advanceTo(double target)
        {
            AddStep($"advance to {target}", () =>
            {
                var clock = Player.GameplayClockContainer.ChildrenOfType<FramedBeatmapClock>().Single();
                ((IAdjustableClock)clock.Source).Seek(target - clock.TotalAppliedOffset);
            });
            AddUntilStep($"reach {target}", () => Player.DrawableRuleset.FrameStableClock.CurrentTime, () => Is.EqualTo(target).Within(1));
            AddStep("consume authored gameplay events", () => skinSubscription?.DrainFrame(skinEvents.Add));
        }

        private osu.Framework.Input.Bindings.KeyBindingContainer<ManiaAction> bindings
            => Player.DrawableRuleset.ChildrenOfType<ManiaInputManager>().First().KeyBindingContainer;

        private BmsKeysoundStore? store => Player?.ChildrenOfType<BmsKeysoundStore>().SingleOrDefault();

        private int playCount(string filename) => store!.PlaybackLogForTesting.Count(record => record.Filename == filename);
    }
}
