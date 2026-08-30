// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsSharedKeysoundTiming : OsuTestScene
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        private ManualClock manualClock = null!;
        private FramedClock testClock = null!;
        private DrawableBmsRuleset drawableRuleset = null!;
        private BmsLane lane = null!;
        private DrawableBmsHitObject drawable = null!;
        private BmsHitObject note = null!;
        private BmsHoldNote holdNote = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            lane = null!;
            drawable = null!;

            var beatmap = createPlayableBeatmap();
            drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);

            note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            holdNote = beatmap.HitObjects.OfType<BmsHoldNote>().Single();

            manualClock = new ManualClock
            {
                CurrentTime = note.StartTime,
                IsRunning = false,
            };

            testClock = new FramedClock(manualClock);

            Child = drawableRuleset = drawableRuleset.With(ruleset =>
            {
                ruleset.InitialiseCompatibilityLayoutForTesting();
                ruleset.RelativeSizeAxes = Axes.Both;
                ruleset.Clock = testClock;
            });
        });

        [Test]
        public void TestDrawableHitTriggersSharedKeysoundImmediately()
        {
            bool hitHandled = false;
            bool requestedImmediately = false;
            string? requestedFilename = null;

            AddUntilStep("drawable ruleset ready", () => isSceneReady());
            AddStep("press drawable", () =>
            {
                manualClock.CurrentTime = note.StartTime;
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();

                hitHandled = drawable.OnPressed(createPressEvent(BmsAction.Key1));
                requestedImmediately = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
                requestedFilename = getRequestedFilename();
            });

            AddAssert("drawable hit is handled", () => hitHandled);
            AddAssert("shared store requested immediately", () => requestedImmediately);
            AddAssert("shared store plays note keysound", () => requestedFilename, () => Is.EqualTo("key1.wav"));
        }

        [Test]
        public void TestLaneReplayTriggersSharedKeysoundImmediately()
        {
            bool primedLaneKeysound = false;
            bool requestedImmediately = false;
            string? requestedFilename = null;

            AddUntilStep("drawable ruleset ready", () => isSceneReady());
            AddStep("prime lane keysound from hit", () =>
            {
                manualClock.CurrentTime = note.StartTime;
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();

                primedLaneKeysound = drawable.OnPressed(createPressEvent(BmsAction.Key1));

                foreach (var channel in drawableRuleset.Playfield.KeysoundStore.ChannelPool)
                    channel.Stop();

                manualClock.CurrentTime = note.StartTime + 100;
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();
            });
            AddStep("press lane replay", () =>
            {
                _ = lane.OnPressed(createPressEvent(BmsAction.Key1));
                requestedImmediately = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
                requestedFilename = getRequestedFilename();
            });

            AddAssert("lane keysound primed", () => primedLaneKeysound);
            AddAssert("lane replay requested immediately", () => requestedImmediately);
            AddAssert("lane replay uses note keysound", () => requestedFilename, () => Is.EqualTo("key1.wav"));
        }

        [Test]
        public void TestAutoPlayNoteSuppressesRedundantLaneKeysound()
        {
            bool noteRequestedKeysound = false;
            string? noteRequestedFilename = null;
            bool laneRequestedKeysound = false;

            AddStep("load actual autoplay drawable ruleset", () =>
            {
                lane = null!;
                drawable = null!;

                var beatmap = createPlayableBeatmap();
                note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
                holdNote = beatmap.HitObjects.OfType<BmsHoldNote>().Single();

                manualClock = new ManualClock
                {
                    CurrentTime = note.StartTime - 100,
                    IsRunning = false,
                };
                testClock = new FramedClock(manualClock);

                Child = drawableRuleset = ((DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new[] { new BmsModAutoplay() })).With(ruleset =>
                {
                    ruleset.InitialiseCompatibilityLayoutForTesting();
                    ruleset.RelativeSizeAxes = Axes.Both;
                    ruleset.Clock = testClock;
                });
            });
            AddUntilStep("drawable ruleset ready", () => isSceneReady());
            AddStep("auto-play the note + let it sound itself", () =>
            {
                manualClock.CurrentTime = note.StartTime;
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();

                noteRequestedKeysound = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
                noteRequestedFilename = getRequestedFilename();

                // Clear whatever the note's own auto-apply sounded, so the next step measures only the lane's behaviour.
                foreach (var channel in drawableRuleset.Playfield.KeysoundStore.ChannelPool)
                    channel.Stop();
            });
            AddStep("press lane as the autoplay replay would", () =>
            {
                _ = lane.OnPressed(createPressEvent(BmsAction.Key1));
                laneRequestedKeysound = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
            });

            // In autoplay the note already sounds itself; the lane must NOT also sound its armed keysound, or every
            // note would double. Mirrors a 100%-perfect play, where the hit note consumes the press and the lane is
            // silent. (Contrast TestLaneReplayTriggersSharedKeysoundImmediately, where a player note DOES sound here.)
            AddAssert("auto-play note requested shared store", () => noteRequestedKeysound);
            AddAssert("auto-play note uses expected keysound", () => noteRequestedFilename, () => Is.EqualTo("key1.wav"));
            AddAssert("lane keysound suppressed for auto-play note", () => !laneRequestedKeysound);
        }

        [TestCase("boundary-5k.bms", BmsKeymode.Key5K, 0x15, 5, "boundary-5k.wav")]
        [TestCase("boundary-7k.bme", null, 0x19, 7, "boundary-7k.wav")]
        [TestCase("boundary-9k.bms", null, 0x17, 6, "boundary-9k.wav")]
        [TestCase("boundary-9k.pms", null, 0x19, 8, "boundary-pms.wav")]
        [TestCase("boundary-k14.bme", null, 0x29, 14, "boundary-k14.wav")]
        [TestCase("boundary-s2.bme", null, 0x26, 15, "boundary-s2.wav")]
        public void TestDecodedBoundaryLaneTimelineActuallyPlaysThroughSharedStore(string fileName, BmsKeymode? keymodeOverride, int channel, int expectedLaneIndex, string expectedFilename)
        {
            BmsLane boundaryLane = null!;
            bool requestedImmediately = false;
            string? requestedFilename = null;

            AddStep("replace with decoded boundary chart", () =>
            {
                var options = keymodeOverride.HasValue ? new BmsBeatmapDecoderOptions(keymodeOverride.Value) : null;
                string text = $"#TITLE Boundary Lane Sound Host\n#BPM 120\n#WAVAA {expectedFilename}\n#001{channel:X2}:AA00\n";
                var decodedChart = decoder.DecodeText(text, fileName, options);
                var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
                var boundaryNote = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.LaneIndex == expectedLaneIndex);

                manualClock = new ManualClock
                {
                    CurrentTime = boundaryNote.StartTime + 100,
                    IsRunning = false,
                };
                testClock = new FramedClock(manualClock);

                Child = drawableRuleset = ((DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap)).With(ruleset =>
                {
                    ruleset.InitialiseCompatibilityLayoutForTesting();
                    ruleset.RelativeSizeAxes = Axes.Both;
                    ruleset.Clock = testClock;
                });
            });
            AddUntilStep("boundary ruleset ready", () =>
            {
                if (drawableRuleset?.IsLoaded != true || drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.LoadState < LoadState.Ready))
                    return false;

                boundaryLane = drawableRuleset.Playfield.Lanes.Single(playfieldLane => playfieldLane.LaneIndex == expectedLaneIndex);
                return boundaryLane.IsLoaded;
            });
            AddStep("press decoded boundary lane", () =>
            {
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();

                _ = boundaryLane.OnPressed(createPressEvent(boundaryLane.LayoutLane.Action));
                requestedImmediately = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
                requestedFilename = getRequestedFilename();
            });

            AddAssert("boundary lane requested shared store", () => requestedImmediately);
            AddAssert("boundary lane played decoded timeline sample", () => requestedFilename, () => Is.EqualTo(expectedFilename));
        }

        [Test]
        public void TestMirrorUsesOnePostModLaneForObjectTimelineSkinAndSharedStore()
        {
            BmsLane targetLane = null!;
            BmsHitObject movedNote = null!;
            DrawableBmsHitObject movedDrawable = null!;
            bool requestedImmediately = false;
            string? requestedFilename = null;

            AddStep("decode and mirror distinct lane sounds", () =>
            {
                const string text = @"
#TITLE Mirror Lane Identity Host
#BPM 120
#WAVAA left.wav
#WAVBB right.wav
#00111:AA00
#00119:BB00
";

                var decodedChart = decoder.DecodeText(text, "mirror-lane-identity.bme");
                var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
                movedNote = beatmap.HitObjects.OfType<BmsHitObject>().Single(note => note.KeysoundSample?.Filename == "left.wav");

                new BmsModMirror().ApplyToBeatmap(beatmap);

                manualClock = new ManualClock
                {
                    CurrentTime = movedNote.StartTime + 100,
                    IsRunning = false,
                };
                testClock = new FramedClock(manualClock);

                Child = drawableRuleset = ((DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap)).With(ruleset =>
                {
                    ruleset.InitialiseCompatibilityLayoutForTesting();
                    ruleset.RelativeSizeAxes = Axes.Both;
                    ruleset.Clock = testClock;
                });
            });
            AddUntilStep("mirrored target lane ready", () =>
            {
                if (drawableRuleset?.IsLoaded != true || drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.LoadState < LoadState.Ready))
                    return false;

                targetLane = drawableRuleset.Playfield.Lanes.Single(playfieldLane => playfieldLane.LaneIndex == movedNote.LaneIndex);
                movedDrawable = targetLane.AllHitObjects.OfType<DrawableBmsHitObject>().Single(drawable => ReferenceEquals(drawable.HitObject, movedNote));
                return targetLane.IsLoaded && movedDrawable.IsLoaded;
            });
            AddAssert("mirror object target is rightmost key", () => movedNote.LaneIndex, () => Is.EqualTo(7));
            AddAssert("object and skin retain post-mod LaneId", () =>
                targetLane.LayoutSnapshotLane?.LaneId,
                () => Is.EqualTo(drawableRuleset.LayoutSnapshot.GetLaneByLogicalIndex(movedNote.LaneIndex).LaneId));
            AddAssert("drawable retains exact post-mod snapshot", () => movedDrawable.ExactLayoutSnapshot, () => Is.SameAs(drawableRuleset.LayoutSnapshot));
            AddStep("press post-mod lane through real shared store", () =>
            {
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();

                _ = targetLane.OnPressed(createPressEvent(targetLane.LayoutLane.Action));
                requestedImmediately = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
                requestedFilename = getRequestedFilename();
            });
            AddAssert("post-mod lane requested shared store", () => requestedImmediately);
            AddAssert("post-mod lane played the moved object timeline", () => requestedFilename, () => Is.EqualTo("left.wav"));
        }

        [Test]
        public void TestSRandomDisablesArmedTimelineButAutoplayObjectUsesPostModLaneAndSharedStore()
        {
            BmsBeatmap scatteredBeatmap = null!;
            BmsHitObject scatteredNote = null!;
            BmsLane targetLane = null!;
            DrawableBmsHitObject scatteredDrawable = null!;
            bool noteRequestedKeysound = false;
            string? noteRequestedFilename = null;

            AddStep("decode and S-RANDOM one keysounded note", () =>
            {
                const string text = @"
#TITLE S-Random Lane Identity Host
#BPM 120
#WAVAA scattered.wav
#00111:AA00
";

                var decodedChart = decoder.DecodeText(text, "s-random-lane-identity.bme");
                scatteredBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
                scatteredNote = scatteredBeatmap.HitObjects.OfType<BmsHitObject>().Single();
                var random = new BmsModRandom();
                random.RandomMode.Value = BmsRandomMode.SRandom;
                random.Seed.Value = 20260417;
                random.ApplyToBeatmap(scatteredBeatmap);

                manualClock = new ManualClock
                {
                    CurrentTime = scatteredNote.StartTime - 100,
                    IsRunning = false,
                };
                testClock = new FramedClock(manualClock);

                Child = drawableRuleset = ((DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(scatteredBeatmap, new[] { new BmsModAutoplay() })).With(ruleset =>
                {
                    ruleset.InitialiseCompatibilityLayoutForTesting();
                    ruleset.RelativeSizeAxes = Axes.Both;
                    ruleset.Clock = testClock;
                });
            });
            AddUntilStep("S-RANDOM target lane ready", () =>
            {
                if (drawableRuleset?.IsLoaded != true || drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.LoadState < LoadState.Ready))
                    return false;

                targetLane = drawableRuleset.Playfield.Lanes.Single(playfieldLane => playfieldLane.LaneIndex == scatteredNote.LaneIndex);
                scatteredDrawable = targetLane.AllHitObjects.OfType<DrawableBmsHitObject>().Single(drawable => ReferenceEquals(drawable.HitObject, scatteredNote));
                return targetLane.IsLoaded && scatteredDrawable.IsLoaded;
            });
            AddAssert("S-RANDOM armed timeline carries stable disabled token", () => scatteredBeatmap.LaneKeysoundTimelineDiagnostic,
                () => Is.EqualTo("bms.keysound.timeline.disabled-s-random"));
            AddAssert("S-RANDOM does not pretend to migrate an armed timeline", () => scatteredBeatmap.GetLaneKeysoundTimeline(scatteredNote.LaneIndex), () => Is.Empty);
            AddAssert("S-RANDOM object and skin use post-mod LaneId", () => targetLane.LayoutSnapshotLane?.LaneId,
                () => Is.EqualTo(drawableRuleset.LayoutSnapshot.GetLaneByLogicalIndex(scatteredNote.LaneIndex).LaneId));
            AddStep("autoplay post-mod object through real shared store", () =>
            {
                manualClock.CurrentTime = scatteredNote.StartTime;
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();

                noteRequestedKeysound = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
                noteRequestedFilename = getRequestedFilename();
            });
            AddAssert("S-RANDOM autoplay object requested shared store", () => noteRequestedKeysound);
            AddAssert("S-RANDOM autoplay object kept its own source WAV", () => noteRequestedFilename, () => Is.EqualTo("scattered.wav"));
            AddAssert("S-RANDOM drawable retains exact target snapshot", () => scatteredDrawable.ExactLayoutSnapshot, () => Is.SameAs(drawableRuleset.LayoutSnapshot));
        }

        [Test]
        public void TestPoorPressStillTriggersKeysound()
        {
            bool pressHandled = false;
            bool requestedImmediately = false;
            string? requestedFilename = null;
            HitResult judged = HitResult.None;

            AddUntilStep("drawable ruleset ready", () => isSceneReady());
            AddStep("press drawable at POOR offset", () =>
            {
                var windows = (BmsTimingWindows)note.HitWindows;

                // Land inside the POOR/miss window but outside the closest hit window, so the press judges the note a
                // non-hit (POOR) and consumes the input — the case that used to be silent.
                double poorOffset = (windows.WindowFor(HitResult.Meh) + windows.WindowFor(HitResult.Miss)) / 2;

                manualClock.CurrentTime = note.StartTime + poorOffset;
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();

                pressHandled = drawable.OnPressed(createPressEvent(BmsAction.Key1));
                requestedImmediately = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
                requestedFilename = getRequestedFilename();
                judged = drawable.Result.Type;
            });

            AddAssert("press handled", () => pressHandled);
            AddAssert("note judged a pressed POOR/miss", () => judged, () => Is.EqualTo(HitResult.Miss));
            AddAssert("shared store still requested keysound on POOR", () => requestedImmediately);
            AddAssert("keysound is the note keysound", () => requestedFilename, () => Is.EqualTo("key1.wav"));
        }

        [Test]
        public void TestHoldNoteTailKeysoundStaysSilentWhileHeadSounds()
        {
            DrawableBmsHoldNote holdDrawable = null!;
            string? headFilename = null;
            bool tailRequested = false;

            AddUntilStep("drawable ruleset ready", () => isSceneReady());
            AddStep("seek to hold note", () =>
            {
                manualClock.CurrentTime = holdNote.StartTime;
                testClock.ProcessFrame();
                drawableRuleset.UpdateSubTree();
            });
            AddUntilStep("hold drawable + nested alive", () =>
            {
                holdDrawable = drawableRuleset.Playfield.Lanes
                                              .SelectMany(playfieldLane => playfieldLane.AllHitObjects)
                                              .OfType<DrawableBmsHoldNote>()
                                              .FirstOrDefault()!;

                return holdDrawable?.IsLoaded == true
                       && holdDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Any()
                       && holdDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Any();
            });

            AddStep("play head keysound", () =>
            {
                holdDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single().PlaySamples();
                headFilename = getRequestedFilename();

                foreach (var channel in drawableRuleset.Playfield.KeysoundStore.ChannelPool)
                    channel.Stop();
            });

            AddStep("play tail keysound", () =>
            {
                holdDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single().PlaySamples();
                tailRequested = drawableRuleset.Playfield.KeysoundStore.ChannelPool.Any(channel => channel.RequestedPlaying);
            });

            // The head keysound sounds; the long-note tail must stay silent (LR2/beatoraja behaviour). Otherwise an
            // LNTYPE1 tail that repeats the head WAV double-triggers it ("stomp your fee feet").
            AddAssert("head keysound sounded", () => headFilename, () => Is.EqualTo("lnhead.wav"));
            AddAssert("tail keysound stayed silent", () => !tailRequested);
        }

        private string? getRequestedFilename()
            => drawableRuleset.Playfield.KeysoundStore.ChannelPool.FirstOrDefault(channel => channel.RequestedPlaying)
                       ?.Samples.OfType<BmsKeysoundSampleInfo>().SingleOrDefault()
                       ?.Filename;

        private bool isSceneReady()
        {
            if (drawableRuleset?.IsLoaded != true)
                return false;

            lane ??= drawableRuleset.Playfield.Lanes.Single(playfieldLane => playfieldLane.LaneIndex == note.LaneIndex && playfieldLane.IsScratch == note.IsScratch);
            drawable ??= lane.AllHitObjects.OfType<DrawableBmsHitObject>().Single(hitObject => ReferenceEquals(hitObject.HitObject, note));

            return drawable.IsLoaded && drawableRuleset.Playfield.KeysoundStore.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready);
        }

        private BmsBeatmap createPlayableBeatmap()
        {
            const string text = @"
#TITLE Shared Keysound Timing Stub
#BPM 120
#RANK 2
#LNTYPE 1
#WAVBB key1.wav
#WAVCC lnhead.wav
#WAVDD lntail.wav
#00111:BB00
#00152:CC00DD00
";

            var decodedChart = decoder.DecodeText(text, "shared-keysound-timing-stub.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }

        private static KeyBindingPressEvent<BmsAction> createPressEvent(BmsAction action)
            => new KeyBindingPressEvent<BmsAction>(new Framework.Input.States.InputState(), action);
    }
}
