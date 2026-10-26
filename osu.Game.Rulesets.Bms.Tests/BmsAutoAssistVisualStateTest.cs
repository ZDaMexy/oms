// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsAutoAssistVisualStateTest
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestOneTimeDrawableModConfigurationSurvivesPlaceholderAndUnassistedPoolReuse(bool scratch, bool reverseModOrder)
        {
            var scratchColour = new Color4(200, 64, 96, 255);
            var noteColour = new Color4(64, 120, 200, 255);
            var scratchMod = new BmsModAutoScratch
            {
                ScratchVisibility = { Value = AutoScratchVisibility.Hidden },
                ScratchTintColour = { Value = scratchColour },
            };
            var noteMod = new BmsModAutoNote
            {
                NoteVisibility = { Value = AutoNoteVisibility.Visible },
                NoteTintColour = { Value = noteColour },
            };
            var clock = new FramedClock(new ManualClock { CurrentTime = 500 });
            using var drawable = new DrawableBmsHitObject(createNote(scratch)) { Clock = clock };
            BmsAsyncNoteDrawable visual = drawable.ChildrenOfType<BmsAsyncNoteDrawable>().Single();

            // This is the playfield's real ordering: drawable mods run once against the pool placeholder, before
            // Apply assigns a real assisted object. Both role settings must survive either callback order.
            if (reverseModOrder)
            {
                noteMod.ApplyToDrawableHitObject(drawable);
                scratchMod.ApplyToDrawableHitObject(drawable);
            }
            else
            {
                scratchMod.ApplyToDrawableHitObject(drawable);
                noteMod.ApplyToDrawableHitObject(drawable);
            }
            assertVisual(1, Color4.White);

            applyAssisted();
            drawable.Apply(createNote(scratch));
            assertVisual(1, Color4.White);
            applyAssisted();
            BmsHitObject autoplay = createNote(scratch);
            autoplay.AutoPlay = true;
            drawable.Apply(autoplay);
            assertVisual(1, Color4.White);

            void applyAssisted()
            {
                BmsHitObject note = createNote(scratch);
                var beatmap = new BmsBeatmap { HitObjects = { note } };
                scratchMod.ApplyToBeatmap(beatmap);
                noteMod.ApplyToBeatmap(beatmap);
                Assert.That(note.AutoPlay, Is.True);
                Assert.That(note.CountsForScore, Is.False);
                drawable.Apply(note);
                assertVisual(scratch ? 0 : 1, scratch ? scratchColour : noteColour);
            }

            void assertVisual(float alpha, Color4 colour)
            {
                Assert.That(visual.Alpha, Is.EqualTo(alpha));
                Assert.That(visual.Colour.TopLeft.SRGB, Is.EqualTo(colour));
            }
        }

        private static BmsHitObject createNote(bool scratch)
        {
            var note = new BmsHitObject { StartTime = 1000, LaneIndex = scratch ? 0 : 1, IsScratch = scratch, Keymode = BmsKeymode.Key7K };
            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return note;
        }
    }
}
