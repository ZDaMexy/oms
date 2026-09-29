// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsVisualOffsetEmptyPoor : OsuTestScene
    {
        [TestCase(false, -500)]
        [TestCase(false, 0)]
        [TestCase(false, 500)]
        [TestCase(true, -500)]
        [TestCase(true, 0)]
        [TestCase(true, 500)]
        public void TestPreloadingPreservesEmptyPressBehaviour(bool autoPlay, double offset)
        {
            DrawableBmsRuleset ruleset = null!;
            BmsLane lane = null!;
            BmsHitObject note = null!;
            ManualClock manualClock = null!;
            FramedClock framedClock = null!;
            double originalStart = 0;
            bool emptyPoor = false;
            bool requestedKeysound = false;

            AddStep("load single OD note", () =>
            {
                const string text = "#TITLE offset preload\n#BPM 120\n#WAV01 note.wav\n#00411:01\n";
                var decoded = new BmsBeatmapDecoder().DecodeText(text, "offset-preload.bme");
                var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
                note = beatmap.HitObjects.OfType<BmsHitObject>().Single();
                note.AutoPlay = autoPlay;
                manualClock = new ManualClock { CurrentTime = note.StartTime - 100, IsRunning = false };
                framedClock = new FramedClock(manualClock);
                ruleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);
                ruleset.InitialiseCompatibilityLayoutForTesting();
                ruleset.Clock = framedClock;
                ruleset.RelativeSizeAxes = Axes.Both;
                Child = ruleset;
            });
            AddUntilStep("note and samples loaded", () => ruleset?.IsLoaded == true
                && ruleset.Playfield.Lanes.SelectMany(l => l.AllHitObjects).OfType<DrawableBmsHitObject>().Any(d => ReferenceEquals(d.HitObject, note))
                && ruleset.Playfield.KeysoundStore.ChannelPool.All(c => c.LoadState >= LoadState.Ready));
            AddStep("establish unshifted baseline", () =>
            {
                ruleset.Playfield.VisualOffset.Value = 0;
                ruleset.Playfield.AutomaticVisualOffsetEnabled.Value = false;
                lane = ruleset.Playfield.Lanes.Single(l => l.LaneIndex == note.LaneIndex);
                // Compatibility rulesets create non-pooled notes for legacy fixtures. Exercise the same lane pool
                // used by production so a future note genuinely leaves the input candidate set before its lifetime.
                var directDrawable = lane.AllHitObjects.OfType<DrawableBmsHitObject>().Single(d => ReferenceEquals(d.HitObject, note));
                lane.Remove(directDrawable);
                lane.Add(note);
            });
            AddUntilStep("pooled baseline note alive", () => lane.HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>().Any(d => ReferenceEquals(d.HitObject, note)));
            AddAssert("note uses production lane pool", () => lane.HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>().Single(d => ReferenceEquals(d.HitObject, note)).IsInPool);
            AddStep("seek before original loading boundary", () =>
            {
                originalStart = lane.HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>().Single(d => ReferenceEquals(d.HitObject, note)).Entry!.LifetimeStart;
                seek(originalStart - 250);
            });
            AddUntilStep("original note not yet loaded", () => !lane.HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>().Any(d => ReferenceEquals(d.HitObject, note)));
            AddStep("empty press without offset", pressLane);
            AddAssert("baseline empty press has no POOR", () => !emptyPoor);
            AddAssert("baseline armed keysound plays", () => requestedKeysound);

            AddStep("enable visual adjustment", () =>
            {
                ruleset.Playfield.VisualOffset.Value = offset;
                ruleset.Playfield.AutomaticVisualOffsetEnabled.Value = true;
            });
            AddUntilStep("visual preload has instantiated future note", () => lane.HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>().Any(d => ReferenceEquals(d.HitObject, note)));
            AddStep("empty press with visual preload", pressLane);
            AddAssert("preload does not add an empty POOR", () => !emptyPoor);
            AddAssert("preload does not suppress armed keysound", () => requestedKeysound);

            AddStep("reach original judgement interval", () => seek(note.StartTime - 100));
            AddStep("press after original loading boundary", pressLane);
            AddAssert("original empty POOR behaviour retained", () => emptyPoor == !autoPlay);
            AddAssert("original auto-lane sound suppression retained", () => requestedKeysound == !autoPlay);

            void seek(double time)
            {
                manualClock.CurrentTime = time;
                framedClock.ProcessFrame();
                ruleset.UpdateSubTree();
            }

            void pressLane()
            {
                foreach (var channel in ruleset.Playfield.KeysoundStore.ChannelPool)
                    channel.Stop();

                emptyPoor = lane.OnPressed(new KeyBindingPressEvent<BmsAction>(new Framework.Input.States.InputState(), BmsAction.Key1));
                requestedKeysound = ruleset.Playfield.KeysoundStore.ChannelPool.Any(c => c.RequestedPlaying);
            }
        }
    }
}
