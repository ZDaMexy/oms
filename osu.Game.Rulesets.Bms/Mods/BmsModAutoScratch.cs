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
    public class BmsModAutoScratch : Mod, IApplicableToBeatmap, IApplicableToDrawableHitObject
    {
        public override string Name => "Auto Scratch";

        public override string Acronym => "ASCR";

        public override LocalisableString Description => BmsModStrings.AutoScratchDescription;

        public override ModType Type => ModType.DifficultyReduction;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override bool RequiresConfiguration => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModAutoScratch), typeof(ModAutoplay) };

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.ScratchVisibility), nameof(BmsModStrings.ScratchVisibilityDescription))]
        public Bindable<AutoScratchVisibility> ScratchVisibility { get; } = new Bindable<AutoScratchVisibility>(AutoScratchVisibility.Visible);

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.TintScratchNotes), nameof(BmsModStrings.TintScratchNotesDescription))]
        public BindableBool TintScratchNotes { get; } = new BindableBool(true);

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.ScratchTintColour), nameof(BmsModStrings.ScratchTintColourDescription))]
        public BindableColour4 ScratchTintColour { get; } = new BindableColour4(Color4Extensions.FromHex("55d66b"));

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!ScratchVisibility.IsDefault)
                    yield return (BmsModStrings.ScratchVisibility, ScratchVisibility.Value.GetLocalisableDescription());

                if (!TintScratchNotes.IsDefault)
                    yield return (BmsModStrings.TintScratchNotes, TintScratchNotes.Value ? CommonStrings.Enabled : CommonStrings.Disabled);

                if (!ScratchTintColour.IsDefault && TintScratchNotes.Value)
                    yield return (BmsModStrings.ScratchTintColour, ScratchTintColour.Value.ToHex());
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is not BmsHitObject bmsHitObject || !bmsHitObject.IsScratch)
                    continue;

                applyAutoScratchState(bmsHitObject);

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

            drawableBmsHitObject.ApplyAutoScratchVisuals(
                ScratchVisibility.Value == AutoScratchVisibility.Visible,
                TintScratchNotes.Value,
                ScratchTintColour.Value);
        }

        private static void applyAutoScratchState(BmsHitObject hitObject)
        {
            hitObject.AutoPlay = true;
            hitObject.CountsForScore = false;

            if (hitObject.Judgement is BmsHitObjectJudgement hitJudgement)
                hitJudgement.CountsForScore = false;

            if (hitObject is BmsHoldNoteTailEvent tailEvent && tailEvent.Judgement is BmsHoldNoteTailJudgement tailJudgement)
                tailJudgement.CountsForScore = false;
        }
    }

    public enum AutoScratchVisibility
    {
        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.AutoScratchVisibilityVisible))]
        Visible,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.AutoScratchVisibilityHidden))]
        Hidden,
    }
}
