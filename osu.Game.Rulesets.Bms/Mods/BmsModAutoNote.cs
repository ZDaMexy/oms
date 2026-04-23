// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModAutoNote : Mod, IApplicableToBeatmap, IApplicableToDrawableHitObject, IPreserveSettingsWhenDisabled
    {
        public override string Name => "Auto Note";

        public override string Acronym => "ANOT";

        public override LocalisableString Description => BmsModStrings.AutoNoteDescription;

        public override ModType Type => ModType.DifficultyReduction;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override bool RequiresConfiguration => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModAutoNote), typeof(BmsModAutoScratch), typeof(ModAutoplay) };

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.NoteVisibility), nameof(BmsModStrings.NoteVisibilityDescription))]
        public Bindable<AutoNoteVisibility> NoteVisibility { get; } = new Bindable<AutoNoteVisibility>(AutoNoteVisibility.Visible);

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.TintNotes), nameof(BmsModStrings.TintNotesDescription))]
        public BindableBool TintNotes { get; } = new BindableBool(true);

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.NoteTintColour), nameof(BmsModStrings.NoteTintColourDescription))]
        public BindableColour4 NoteTintColour { get; } = new BindableColour4(Color4Extensions.FromHex("ffb84d"));

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!NoteVisibility.IsDefault)
                    yield return (BmsModStrings.NoteVisibility, NoteVisibility.Value.GetLocalisableDescription());

                if (!TintNotes.IsDefault)
                    yield return (BmsModStrings.TintNotes, TintNotes.Value ? CommonStrings.Enabled : CommonStrings.Disabled);

                if (!NoteTintColour.IsDefault && TintNotes.Value)
                    yield return (BmsModStrings.NoteTintColour, NoteTintColour.Value.ToHex());
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is not BmsHitObject bmsHitObject || bmsHitObject.IsScratch)
                    continue;

                applyAutoNoteState(bmsHitObject);

                if (bmsHitObject is not BmsHoldNote holdNote)
                    continue;

                foreach (var bodyTick in holdNote.BodyTicks)
                    bodyTick.CountsForGauge = false;

                if (holdNote.Head?.Judgement is BmsHitObjectJudgement headJudgement)
                    headJudgement.CountsForScore = false;

                if (holdNote.Tail?.Judgement is BmsHoldNoteTailJudgement tailJudgement)
                    tailJudgement.CountsForScore = false;
            }
        }

        public void ApplyToDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            if (drawableHitObject is not DrawableBmsHitObject drawableBmsHitObject)
                return;

            drawableBmsHitObject.ApplyAutoNoteVisuals(
                NoteVisibility.Value == AutoNoteVisibility.Visible,
                TintNotes.Value,
                NoteTintColour.Value);
        }

        private static void applyAutoNoteState(BmsHitObject hitObject)
        {
            hitObject.AutoPlay = true;
            hitObject.CountsForScore = false;

            if (hitObject.Judgement is BmsHitObjectJudgement hitJudgement)
                hitJudgement.CountsForScore = false;

            if (hitObject is BmsHoldNoteTailEvent tailEvent && tailEvent.Judgement is BmsHoldNoteTailJudgement tailJudgement)
                tailJudgement.CountsForScore = false;
        }
    }

    public enum AutoNoteVisibility
    {
        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.AutoScratchVisibilityVisible))]
        Visible,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.AutoScratchVisibilityHidden))]
        Hidden,
    }
}
