// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
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

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            lane = null!;
            drawable = null!;

            var beatmap = createPlayableBeatmap();
            drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);

            note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);

            manualClock = new ManualClock
            {
                CurrentTime = note.StartTime,
                IsRunning = false,
            };

            testClock = new FramedClock(manualClock);

            Child = drawableRuleset = drawableRuleset.With(ruleset =>
            {
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
#WAVBB key1.wav
#00111:BB00
";

            var decodedChart = decoder.DecodeText(text, "shared-keysound-timing-stub.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }

        private static KeyBindingPressEvent<BmsAction> createPressEvent(BmsAction action)
            => new KeyBindingPressEvent<BmsAction>(new osu.Framework.Input.States.InputState(), action);
    }
}
