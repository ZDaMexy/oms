// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsGameplaySkinAutoAssist : PlayerTestScene
    {
        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };
        private readonly FramedClock referenceClock;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        public TestSceneBmsGameplaySkinAutoAssist()
        {
            referenceClock = new FramedClock(manualClock);
        }

        protected override Ruleset CreatePlayerRuleset() => new BmsRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("""
                #TITLE Actual object auto-assist projection
                #BPM 120
                #LNTYPE 1
                #WAV01 note.wav
                #00151:01000001
                #00156:01000001
                #00211:01
                #00216:01
                #01211:01
                """, "auto-assist-projection.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset },
            };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => new ClockBackedTestWorkingBeatmap(beatmap, storyboard, referenceClock, audioManager);

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(true, false, false);

        [TestCase(GameplaySkinObjectAutoAssist.None, false, true, true)]
        [TestCase(GameplaySkinObjectAutoAssist.None, true, true, true)]
        [TestCase(GameplaySkinObjectAutoAssist.Scratch, false, true, true)]
        [TestCase(GameplaySkinObjectAutoAssist.Scratch, false, false, false)]
        [TestCase(GameplaySkinObjectAutoAssist.Note, false, true, true)]
        [TestCase(GameplaySkinObjectAutoAssist.Note, false, false, false)]
        public void TestActualAutoAssistFlagsReachObjectsAndSurviveSeekAndLateAttach(GameplaySkinObjectAutoAssist assist, bool autoplay, bool visible, bool tint)
        {
            DrawableBmsRuleset renderer = null!;
            GameplaySkinEventSubscription subscription = null!;
            long epoch = -1;
            var tintColour = new Color4(64, 200, 120, 255);
            AddStep("load actual BMS assistance mode", () =>
            {
                Mod[] mods = assist switch
                {
                    GameplaySkinObjectAutoAssist.Scratch => new Mod[]
                    {
                        new BmsModHellChargeNote(), new BmsModAutoScratch
                        {
                            ScratchVisibility = { Value = visible ? AutoScratchVisibility.Visible : AutoScratchVisibility.Hidden },
                            TintScratchNotes = { Value = tint }, ScratchTintColour = { Value = tintColour },
                        },
                    },
                    GameplaySkinObjectAutoAssist.Note => new Mod[]
                    {
                        new BmsModHellChargeNote(), new BmsModAutoNote
                        {
                            NoteVisibility = { Value = visible ? AutoNoteVisibility.Visible : AutoNoteVisibility.Hidden },
                            TintNotes = { Value = tint }, NoteTintColour = { Value = tintColour },
                        },
                    },
                    _ => autoplay ? new Mod[] { new BmsModHellChargeNote(), new BmsModAutoplay() } : new Mod[] { new BmsModHellChargeNote() },
                };
                LoadPlayer(mods);
            });
            AddUntilStep("actual BMS event runtime loaded", () =>
            {
                if (Player?.IsLoaded != true || Player.DrawableRuleset is not DrawableBmsRuleset loaded
                    || loaded.GameplaySkinEventRuntime == null || loaded.GameplaySkinEventStream.CurrentEpoch <= 0)
                    return false;

                renderer = loaded;
                return true;
            });
            AddStep("attach to actual player stream", () =>
            {
                subscription = renderer.GameplaySkinEventStream.Subscribe();
                var attached = new List<GameplaySkinEventEnvelope>();
                subscription.DrainFrame(attached.Add);
                epoch = attached.Single().Epoch;
                BmsHitObject[] objects = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().ToArray();
                Assert.That(objects.Any(note => note.IsScratch), Is.True);
                Assert.That(objects.Any(note => !note.IsScratch), Is.True);
                foreach (BmsHitObject note in objects)
                {
                    bool assisted = isAssisted(note);
                    Assert.That(note.CountsForScore, Is.EqualTo(!assisted));
                    if (assisted)
                        Assert.That(note.AutoPlay, Is.True);
                }
            });

            assertDestination(2200, GameplaySkinEventResetReason.Seek, GameplaySkinObjectKind.LongNote);
            assertDestination(2400, GameplaySkinEventResetReason.Seek, GameplaySkinObjectKind.LongNote);
            assertDestination(2250, GameplaySkinEventResetReason.Rewind, GameplaySkinObjectKind.LongNote);
            // The ordinary key/scratch notes start at 4000ms, after both long notes have ended. Sample their real
            // visible window separately instead of assuming that a future note is already in the drawable graph.
            assertDestination(3950, GameplaySkinEventResetReason.Seek, GameplaySkinObjectKind.Note);
            AddStep("detach actual BMS assistance consumer", () => subscription.Dispose());

            bool isAssisted(BmsHitObject note)
                => assist == GameplaySkinObjectAutoAssist.Scratch && note.IsScratch
                   || assist == GameplaySkinObjectAutoAssist.Note && !note.IsScratch;

            void assertDestination(double destination, GameplaySkinEventResetReason reason, GameplaySkinObjectKind expectedKind)
            {
                AddStep($"seek actual gameplay to {destination}", () => Player.GameplayClockContainer.Seek(destination));
                AddUntilStep("new actual gameplay epoch committed", () => renderer.GameplaySkinEventStream.CurrentEpoch == epoch + 1);
                // Only a displayed note needs a mounted material. Hidden assistance sets the native host's Alpha
                // to zero during OnApply; its invisible child tree need not finish loading to prove that it hides.
                // The exact active objects, non-empty native surfaces and their Alpha/Colour are still asserted below.
                if (expectedKind == GameplaySkinObjectKind.Note && assist != GameplaySkinObjectAutoAssist.None && visible)
                {
                    AddUntilStep("actual assisted note materials mounted", () =>
                    {
                        BmsAsyncNoteDrawable[] surfaces = assistedOrdinarySurfaces().ToArray();
                        return surfaces.Length > 0 && surfaces.All(surface => surface.IsLoaded
                            && surface.Drawable is { IsLoaded: true } and not BmsPublishedNotePendingDrawable && surface.PendingLoadTask == null);
                    });
                }
                AddStep("exact objects retain actual assistance after reset and late attach", () =>
                {
                    var delivered = new List<GameplaySkinEventEnvelope>();
                    subscription.DrainFrame(delivered.Add);
                    GameplaySkinEventEnvelope reset = delivered.Single(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset);
                    var state = (GameplaySkinStateEventPayload)reset.Payload;
                    Assert.That(state.ResetReason, Is.EqualTo(reason));
                    GameplaySkinObjectStateSnapshot[] objects = state.State.ActiveObjects.Where(obj => obj.Kind is GameplaySkinObjectKind.Note or GameplaySkinObjectKind.LongNote).ToArray();
                    Assert.That(objects.Count(obj => obj.Kind == expectedKind), Is.EqualTo(2));
                    foreach (GameplaySkinObjectStateSnapshot obj in objects)
                    {
                        BmsHitObject owner = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().Single(note => renderer.GetGameplaySkinObjectId(note) == obj.ObjectId);
                        Assert.That(obj.AutoAssist, Is.EqualTo(isAssisted(owner) ? assist : GameplaySkinObjectAutoAssist.None));
                    }

                    foreach (BmsHoldNote hold in renderer.Beatmap.HitObjects.OfType<BmsHoldNote>())
                    {
                        long id = renderer.GetGameplaySkinObjectId(hold);
                        Assert.That(renderer.GetGameplaySkinObjectId(hold.Head!), Is.EqualTo(id));
                        Assert.That(renderer.GetGameplaySkinObjectId(hold.Tail!), Is.EqualTo(id));
                        Assert.That(hold.BodyTicks.Select(renderer.GetGameplaySkinObjectId), Is.All.EqualTo(id));
                    }

                    if (expectedKind == GameplaySkinObjectKind.Note)
                    {
                        int assistedVisuals = 0;
                        foreach (BmsAsyncNoteDrawable visual in assistedOrdinarySurfaces())
                        {
                            assistedVisuals++;
                            Assert.That(visual.Alpha, Is.EqualTo(visible ? 1 : 0));
                            Assert.That(visual.Colour.TopLeft.SRGB, Is.EqualTo(tint ? tintColour : Color4.White));
                        }

                        if (assist != GameplaySkinObjectAutoAssist.None)
                            Assert.That(assistedVisuals, Is.GreaterThan(0));
                    }

                    using GameplaySkinEventSubscription late = renderer.GameplaySkinEventStream.Subscribe();
                    var lateEvents = new List<GameplaySkinEventEnvelope>();
                    late.DrainFrame(lateEvents.Add);
                    var lateState = (GameplaySkinStateEventPayload)lateEvents.Single().Payload;
                    Assert.That(lateState.State.ActiveObjects.Select(obj => (obj.ObjectId, obj.AutoAssist)),
                        Is.EquivalentTo(state.State.ActiveObjects.Select(obj => (obj.ObjectId, obj.AutoAssist))));
                    epoch = reset.Epoch;
                });
            }

            IEnumerable<BmsAsyncNoteDrawable> assistedOrdinarySurfaces()
                => renderer.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                           .Where(drawable => drawable.HitObject is BmsHitObject note && isAssisted(note)
                               && note is not (BmsHoldNote or BmsHoldNoteHead or BmsHoldNoteTailEvent))
                           .Select(drawable => drawable.ChildrenOfType<BmsAsyncNoteDrawable>().Single());
        }
    }
}
