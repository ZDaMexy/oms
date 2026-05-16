// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsOrderedHitPolicyTest
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        [Test]
        public void TestScratchStreamAllowsNextHitAfterEarlierObjectJudged()
        {
            var beatmap = createScratchStreamBeatmap();
            var scratchNotes = getScratchNotes(beatmap);

            Assert.That(scratchNotes, Has.Length.EqualTo(2));

            var manualClock = new ManualClock
            {
                CurrentTime = scratchNotes[0].StartTime,
                IsRunning = true,
            };

            var (playfield, firstDrawable, secondDrawable, testClock) = createPlayfieldWithScratchDrawables(createScratchStreamBeatmap(), scratchNotes, manualClock);

            testClock.ProcessFrame();
            playfield.UpdateSubTree();

            Assert.Multiple(() =>
            {
                Assert.That(firstDrawable.OnPressed(createPressEvent()), Is.True);
                Assert.That(firstDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(secondDrawable.Judged, Is.False);
            });

            manualClock.CurrentTime = scratchNotes[1].StartTime;
            testClock.ProcessFrame();
            playfield.UpdateSubTree();

            Assert.Multiple(() =>
            {
                Assert.That(secondDrawable.OnPressed(createPressEvent()), Is.True);
                Assert.That(secondDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
            });
        }

        [Test]
        public void TestScratchStreamLateHitForcesEarlierObjectMiss()
        {
            var beatmap = createScratchStreamBeatmap("DDDD00000000000000000000");
            var scratchNotes = getScratchNotes(beatmap);

            Assert.That(scratchNotes, Has.Length.EqualTo(2));
            Assert.That(scratchNotes[1].StartTime - scratchNotes[0].StartTime, Is.LessThan(scratchNotes[0].HitWindows.WindowFor(HitResult.Miss)));

            var manualClock = new ManualClock
            {
                CurrentTime = scratchNotes[1].StartTime,
                IsRunning = true,
            };

            var (playfield, firstDrawable, secondDrawable, testClock) = createPlayfieldWithScratchDrawables(beatmap, scratchNotes, manualClock);

            testClock.ProcessFrame();
            playfield.UpdateSubTree();

            Assert.Multiple(() =>
            {
                Assert.That(secondDrawable.OnPressed(createPressEvent()), Is.True);
                Assert.That(firstDrawable.Result.Type, Is.EqualTo(HitResult.Miss));
                Assert.That(secondDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
            });
        }

        private BmsBeatmap createScratchStreamBeatmap(string scratchLaneData = "DDDD")
        {
            string text = $@"
#TITLE Scratch Stream Stub
#BPM 120
#RANK 2
#00101:AA00
#WAVAA bgm.wav
#WAVDD scratch.wav
    #00116:{scratchLaneData}
";

            var decodedChart = decoder.DecodeText(text, "ordered-hit-scratch-stream-stub.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }

        private static BmsHitObject[] getScratchNotes(BmsBeatmap beatmap)
            => beatmap.HitObjects.OfType<BmsHitObject>()
                      .Where(hitObject => hitObject.IsScratch && hitObject is not BmsHoldNote)
                      .OrderBy(hitObject => hitObject.StartTime)
                      .ToArray();

        private static (BmsPlayfield Playfield, DrawableBmsHitObject FirstDrawable, DrawableBmsHitObject SecondDrawable, FramedClock TestClock) createPlayfieldWithScratchDrawables(BmsBeatmap beatmap, BmsHitObject[] scratchNotes, ManualClock manualClock)
        {
            var testClock = new FramedClock(manualClock);

            var playfield = new BmsPlayfield(beatmap)
            {
                Clock = testClock,
            };

            var firstDrawable = new DrawableBmsHitObject(scratchNotes[0])
            {
                Clock = testClock,
            };

            var secondDrawable = new DrawableBmsHitObject(scratchNotes[1])
            {
                Clock = testClock,
            };

            firstDrawable.Apply(scratchNotes[0]);
            secondDrawable.Apply(scratchNotes[1]);

            playfield.Add(firstDrawable);
            playfield.Add(secondDrawable);

            return (playfield, firstDrawable, secondDrawable, testClock);
        }

        private static KeyBindingPressEvent<BmsAction> createPressEvent()
            => new KeyBindingPressEvent<BmsAction>(new osu.Framework.Input.States.InputState(), BmsAction.Scratch1);
    }
}
