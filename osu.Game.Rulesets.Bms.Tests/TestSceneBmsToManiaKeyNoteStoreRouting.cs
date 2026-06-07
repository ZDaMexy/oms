// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    /// <summary>
    /// Player-level regression for the BMS -> mania converted keysound audio chain (P1-J #10 / J6): playable KEY notes,
    /// BGM and scratch all route their keysound through the shared <see cref="BmsKeysoundStore"/> (cross-channel per-WAV
    /// cut), matching BMS-native — so a WAV slot shared between the BGM layer and a KEY note, or the same slot
    /// retriggered across consecutive notes, no longer stacks duplicate one-shot copies. Reads the store's playback log
    /// back under mania autoplay and checks the per-slot <c>Play</c> count against a known expectation.
    ///
    /// The chart shares one WAV slot between the BGM layer and a KEY note (cross-path), repeats one KEY slot, and uses
    /// distinct slots. A trailing sentinel note absorbs an unrelated harness quirk: autoplay under the manual test clock
    /// reliably drops the single temporally-LAST object (confirmed historically — plain pooled Notes miss it too), so
    /// making the sentinel last keeps the four measured notes processed.
    /// </summary>
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsToManiaKeyNoteStoreRouting : PlayerTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };
        private readonly FramedClock referenceClock;
        private readonly BmsDecodedBeatmap sourceBeatmap;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool Autoplay => true;

        public TestSceneBmsToManiaKeyNoteStoreRouting()
        {
            referenceClock = new FramedClock(manualClock);

            // BPM 120 -> measure = 2000 ms. KEY notes from measure 3 so the log is enabled before any of them fire.
            //   t=2000: BGM slot AA (bgm.wav) + scratch slot CC (scratch.wav)
            //   t=4000: KEY key_a (BB, ch11) + KEY key_b (DD, ch12) + KEY bgm-slot AA (ch13, cross-path)
            //   t=6000: KEY key_a (BB, ch11) again  -- same-slot retrigger
            //   t=8000: KEY sentinel (EE, ch11)     -- sacrificial trailing note (absorbs the harness last-object drop)
            // Expected store Play counts for the four MEASURED KEY notes: bgm.wav=2 (BGM auto + cross-path KEY),
            // key_a.wav=2, key_b.wav=1, scratch.wav=1.
            sourceBeatmap = createSource(@"
#TITLE BMS to mania KEY-note store routing
#BPM 120
#WAVAA bgm.wav
#WAVBB key_a.wav
#WAVCC scratch.wav
#WAVDD key_b.wav
#WAVEE sentinel.wav
#00201:AA
#00216:CC
#00311:BB
#00312:DD
#00313:AA
#00411:BB
#00511:EE
", "bms-to-mania-keynote-routing.bme");
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            manualClock.CurrentTime = 0;
            referenceClock.ProcessFrame();
        });

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => sourceBeatmap;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new OsuTestScene.ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        [Test]
        public void TestConvertedKeysoundsRouteThroughStore()
        {
            AddUntilStep("player ready + store hosted", () => keysoundStore != null && keysoundStore.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready));

            AddAssert("store floored to >= 128 channels", () => keysoundStore!.ConcurrentChannels, () => Is.GreaterThanOrEqualTo(128));
            AddAssert("KEY notes routed (5 BmsConvertedKeyNoteHitObject)", () => convertedKeyNoteCount(), () => Is.EqualTo(5));
            AddAssert("BGM + scratch are still sample-only objects", () => convertedSampleOnlyObjectCount(), () => Is.GreaterThanOrEqualTo(2));

            AddStep("enable store playback log", () => keysoundStore!.EnablePlaybackLogForTesting());

            advanceThroughChart();

            // Dump BEFORE asserting so the runtime log is always visible, even when an assertion below fails.
            AddStep("dump store playback log", dumpPlaybackLog);
            AddStep("dump per-file store Play counts", dumpPerFileCounts);
            AddStep("dump autoplay hit count", () => Logger.Log($"[bms->mania keynote] highest combo = {Player.ScoreProcessor.HighestCombo.Value} (expected >= 4 measured KEY notes hit)"));

            // The four measured KEY notes (t<=6000) must all be hit; the t=8000 sentinel absorbs the harness's
            // last-object drop.
            AddAssert("autoplay hit the 4 measured KEY notes", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.GreaterThanOrEqualTo(4));

            // Every keysound — BGM auto layer, scratch, AND every playable KEY note — flows through the single store, so
            // per-slot Play counts match the chart exactly: key_a twice (same-slot retrigger), key_b once, scratch once,
            // and bgm twice (the BGM auto layer plus the cross-path KEY note that reuses the BGM's WAV slot). Before this
            // routing, KEY notes played mania one-shots that never reached the store (so key_a/key_b would be 0 here, and
            // a slot shared by BGM + a KEY note could not cut across the two paths).
            AddAssert("key_a.wav routed twice (same-slot retrigger)", () => storeFileCount("key_a.wav"), () => Is.EqualTo(2));
            AddAssert("key_b.wav routed once", () => storeFileCount("key_b.wav"), () => Is.EqualTo(1));
            AddAssert("bgm.wav routed twice (BGM auto + cross-path KEY)", () => storeFileCount("bgm.wav"), () => Is.EqualTo(2));
            AddAssert("scratch.wav routed once", () => storeFileCount("scratch.wav"), () => Is.EqualTo(1));
        }

        private BmsKeysoundStore? keysoundStore => Player?.ChildrenOfType<BmsKeysoundStore>().SingleOrDefault();

        private int convertedKeyNoteCount()
            => Player?.DrawableRuleset.Objects.OfType<BmsConvertedKeyNoteHitObject>().Count() ?? 0;

        private int convertedSampleOnlyObjectCount()
            => Player?.DrawableRuleset.Objects.Count(hitObject => hitObject is BmsConvertedBgmSampleHitObject or BmsConvertedScratchSampleHitObject) ?? 0;

        private int storeFileCount(string filename)
            => keysoundStore?.PlaybackLogForTesting.Count(record => record.Filename == filename) ?? 0;

        private void dumpPlaybackLog()
        {
            var log = keysoundStore?.PlaybackLogForTesting ?? new List<KeysoundPlaybackRecord>();

            Logger.Log($"[bms->mania keynote] store playback log ({log.Count} entries):");

            foreach (var record in log)
                Logger.Log($"[bms->mania keynote]   t={record.Time:N0} slot={record.CutGroup?.ToString() ?? "-"} decision={record.Decision} ch={record.ChannelIndex} file={record.Filename}");
        }

        private void dumpPerFileCounts()
        {
            foreach (string file in new[] { "bgm.wav", "key_a.wav", "key_b.wav", "scratch.wav" })
                Logger.Log($"[bms->mania keynote] store Play count: {file} = {storeFileCount(file)}");
        }

        private void advanceThroughChart()
        {
            advanceAndSettle(2500); // BGM + scratch at t=2000
            advanceAndSettle(4500); // KEY notes at t=4000
            advanceAndSettle(6500); // KEY key_a retrigger at t=6000
            advanceAndSettle(8500); // sentinel at t=8000
            advanceAndSettle(9500);
        }

        private void advanceAndSettle(double target)
        {
            AddStep($"advance clock to {target:N0} ms", () =>
            {
                manualClock.CurrentTime = target;
                referenceClock.ProcessFrame();
            });

            // Let the frame-stable clock actually catch up to the new position before moving on, so each note's time is
            // crossed on a smooth forward advance (and we never assert before gameplay has processed it).
            AddUntilStep($"frame-stable reaches {target:N0} ms", () => Player.DrawableRuleset.FrameStableClock.CurrentTime, () => Is.GreaterThanOrEqualTo(target - 100));
        }

        private BmsDecodedBeatmap createSource(string text, string path)
        {
            var decodedChart = decoder.DecodeText(text, path);

            return new BmsDecodedBeatmap(decodedChart)
            {
                BeatmapInfo =
                {
                    Ruleset = bms_ruleset_info,
                }
            };
        }
    }
}
