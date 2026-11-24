// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    public partial class TestSceneHoldNote : ManiaHitObjectTestScene
    {
        [Test]
        public void TestHoldNote()
        {
            AddToggleStep("toggle hitting", v =>
            {
                foreach (var holdNote in CreatedDrawables.SelectMany(d => d.ChildrenOfType<DrawableHoldNote>()))
                {
                    ((Bindable<bool>)holdNote.IsHolding).Value = v;
                }
            });
        }

        [Test]
        public void TestFadeOnMiss()
        {
            AddStep("miss tick", () =>
            {
                foreach (var holdNote in holdNotes)
                    holdNote.ChildrenOfType<DrawableHoldNoteHead>().First().MissForcefully();
            });
        }

        [Test]
        public void TestLegacyHoldColourResetsWhenDrawableIsReused()
        {
            AddUntilStep("legacy heads, bodies and tails are loaded", () =>
                legacyParts().Any(part => part is LegacyHoldNoteHeadPiece)
                && legacyParts().Any(part => part is LegacyBodyPiece)
                && legacyParts().Any(part => part is LegacyHoldNoteTailPiece));

            AddStep("miss the head", () =>
            {
                foreach (var holdNote in holdNotes)
                    holdNote.Head.MissForcefully();
            });
            AddUntilStep("all legacy hold parts are dimmed", () =>
                legacyParts().All(part => part.Colour.TopLeft.SRGB == (Color4)Colour4.DarkGray));

            AddStep("reuse the same hold drawables for fresh notes", () =>
            {
                foreach (var holdNote in holdNotes)
                {
                    var next = new HoldNote { StartTime = Time.Current + 1000, Duration = 1000, Column = holdNote.HitObject.Column };
                    next.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    holdNote.Apply(next);
                }
            });
            AddUntilStep("all legacy hold parts on the new notes start white", () =>
                legacyParts().Any() && legacyParts().All(part => part.Colour.TopLeft.SRGB == (Color4)Colour4.White));
        }

        private IEnumerable<Drawable> legacyParts() => holdNotes.SelectMany(hold =>
            hold.ChildrenOfType<LegacyManiaColumnElement>().Where(part => part is LegacyHoldNoteHeadPiece or LegacyBodyPiece or LegacyHoldNoteTailPiece));

        private IEnumerable<DrawableHoldNote> holdNotes => CreatedDrawables.SelectMany(d => d.ChildrenOfType<DrawableHoldNote>());

        protected override DrawableManiaHitObject CreateHitObject()
        {
            var note = new HoldNote { Duration = 1000 };
            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            return new DrawableHoldNote(note);
        }
    }
}
