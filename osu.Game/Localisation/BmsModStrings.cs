// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BmsModStrings
    {
        private const string prefix = @"osu.Game.Localisation.BmsMod";

        /// <summary>
        /// "Watch a perfect automated BMS playthrough."
        /// </summary>
        public static LocalisableString AutoplayDescription => new TranslatableString(getKey(@"autoplay_description"), @"Watch a perfect automated BMS playthrough.");

        /// <summary>
        /// "Automatically handles scratch notes and removes them from scoring and gauge."
        /// </summary>
        public static LocalisableString AutoScratchDescription => new TranslatableString(getKey(@"auto_scratch_description"), @"Automatically handles scratch notes and removes them from scoring and gauge.");

        /// <summary>
        /// "Automatically handles non-scratch notes and removes them from scoring and gauge."
        /// </summary>
        public static LocalisableString AutoNoteDescription => new TranslatableString(getKey(@"auto_note_description"), @"Automatically handles non-scratch notes and removes them from scoring and gauge.");

        /// <summary>
        /// "Note visibility"
        /// </summary>
        public static LocalisableString NoteVisibility => new TranslatableString(getKey(@"note_visibility"), @"Note visibility");

        /// <summary>
        /// "Controls whether auto-noted notes remain visible on the playfield."
        /// </summary>
        public static LocalisableString NoteVisibilityDescription => new TranslatableString(getKey(@"note_visibility_description"), @"Controls whether auto-noted notes remain visible on the playfield.");

        /// <summary>
        /// "Tint notes"
        /// </summary>
        public static LocalisableString TintNotes => new TranslatableString(getKey(@"tint_notes"), @"Tint notes");

        /// <summary>
        /// "Tint auto-noted notes to make the assist state obvious during play."
        /// </summary>
        public static LocalisableString TintNotesDescription => new TranslatableString(getKey(@"tint_notes_description"), @"Tint auto-noted notes to make the assist state obvious during play.");

        /// <summary>
        /// "Note tint colour"
        /// </summary>
        public static LocalisableString NoteTintColour => new TranslatableString(getKey(@"note_tint_colour"), @"Note tint colour");

        /// <summary>
        /// "The tint applied to auto-noted notes when tinting is enabled."
        /// </summary>
        public static LocalisableString NoteTintColourDescription => new TranslatableString(getKey(@"note_tint_colour_description"), @"The tint applied to auto-noted notes when tinting is enabled.");

        /// <summary>
        /// "Scratch visibility"
        /// </summary>
        public static LocalisableString ScratchVisibility => new TranslatableString(getKey(@"scratch_visibility"), @"Scratch visibility");

        /// <summary>
        /// "Controls whether auto-scratched notes remain visible on the playfield."
        /// </summary>
        public static LocalisableString ScratchVisibilityDescription => new TranslatableString(getKey(@"scratch_visibility_description"), @"Controls whether auto-scratched notes remain visible on the playfield.");

        /// <summary>
        /// "Tint scratch notes"
        /// </summary>
        public static LocalisableString TintScratchNotes => new TranslatableString(getKey(@"tint_scratch_notes"), @"Tint scratch notes");

        /// <summary>
        /// "Tint auto-scratched notes to make the assist state obvious during play."
        /// </summary>
        public static LocalisableString TintScratchNotesDescription => new TranslatableString(getKey(@"tint_scratch_notes_description"), @"Tint auto-scratched notes to make the assist state obvious during play.");

        /// <summary>
        /// "Scratch tint colour"
        /// </summary>
        public static LocalisableString ScratchTintColour => new TranslatableString(getKey(@"scratch_tint_colour"), @"Scratch tint colour");

        /// <summary>
        /// "The tint applied to auto-scratched notes when tinting is enabled."
        /// </summary>
        public static LocalisableString ScratchTintColourDescription => new TranslatableString(getKey(@"scratch_tint_colour_description"), @"The tint applied to auto-scratched notes when tinting is enabled.");

        /// <summary>
        /// "Visible"
        /// </summary>
        public static LocalisableString AutoScratchVisibilityVisible => new TranslatableString(getKey(@"auto_scratch_visibility_visible"), @"Visible");

        /// <summary>
        /// "Hidden"
        /// </summary>
        public static LocalisableString AutoScratchVisibilityHidden => new TranslatableString(getKey(@"auto_scratch_visibility_hidden"), @"Hidden");

        /// <summary>
        /// "Uses charge-note long note timing."
        /// </summary>
        public static LocalisableString ChargeNoteDescription => new TranslatableString(getKey(@"charge_note_description"), @"Uses charge-note long note timing.");

        /// <summary>
        /// "Uses hell charge note long note timing."
        /// </summary>
        public static LocalisableString HellChargeNoteDescription => new TranslatableString(getKey(@"hell_charge_note_description"), @"Uses hell charge note long note timing.");

        /// <summary>
        /// "Uses the assist-easy BMS gauge."
        /// </summary>
        public static LocalisableString GaugeAssistEasyDescription => new TranslatableString(getKey(@"gauge_assist_easy_description"), @"Uses the assist-easy BMS gauge.");

        /// <summary>
        /// "Uses the easy BMS gauge."
        /// </summary>
        public static LocalisableString GaugeEasyDescription => new TranslatableString(getKey(@"gauge_easy_description"), @"Uses the easy BMS gauge.");

        /// <summary>
        /// "Uses the hard BMS survival gauge."
        /// </summary>
        public static LocalisableString GaugeHardDescription => new TranslatableString(getKey(@"gauge_hard_description"), @"Uses the hard BMS survival gauge.");

        /// <summary>
        /// "Uses the EX-HARD BMS survival gauge."
        /// </summary>
        public static LocalisableString GaugeExHardDescription => new TranslatableString(getKey(@"gauge_ex_hard_description"), @"Uses the EX-HARD BMS survival gauge.");

        /// <summary>
        /// "Uses the hazard BMS survival gauge."
        /// </summary>
        public static LocalisableString GaugeHazardDescription => new TranslatableString(getKey(@"gauge_hazard_description"), @"Uses the hazard BMS survival gauge.");

        /// <summary>
        /// "Uses beatoraja life-gauge calculations."
        /// </summary>
        public static LocalisableString GaugeRulesBeatorajaDescription => new TranslatableString(getKey(@"gauge_rules_beatoraja_description"), @"Uses beatoraja life-gauge calculations.");

        /// <summary>
        /// "Uses beatmania IIDX life-gauge calculations."
        /// </summary>
        public static LocalisableString GaugeRulesIidxDescription => new TranslatableString(getKey(@"gauge_rules_iidx_description"), @"Uses beatmania IIDX life-gauge calculations.");

        /// <summary>
        /// "Uses LR2-style life-gauge calculations."
        /// </summary>
        public static LocalisableString GaugeRulesLr2Description => new TranslatableString(getKey(@"gauge_rules_lr2_description"), @"Uses LR2-style life-gauge calculations.");

        /// <summary>
        /// "Downgrades to a lower gauge instead of failing immediately."
        /// </summary>
        public static LocalisableString GaugeAutoShiftDescription => new TranslatableString(getKey(@"gauge_auto_shift_description"), @"Downgrades to a lower gauge instead of failing immediately.");

        /// <summary>
        /// "Starting gauge"
        /// </summary>
        public static LocalisableString StartingGauge => new TranslatableString(getKey(@"starting_gauge"), @"Starting gauge");

        /// <summary>
        /// "The highest gauge tier to begin the chart on."
        /// </summary>
        public static LocalisableString StartingGaugeDescription => new TranslatableString(getKey(@"starting_gauge_description"), @"The highest gauge tier to begin the chart on.");

        /// <summary>
        /// "Floor gauge"
        /// </summary>
        public static LocalisableString FloorGauge => new TranslatableString(getKey(@"floor_gauge"), @"Floor gauge");

        /// <summary>
        /// "The lowest gauge tier GAS may downgrade to."
        /// </summary>
        public static LocalisableString FloorGaugeDescription => new TranslatableString(getKey(@"floor_gauge_description"), @"The lowest gauge tier GAS may downgrade to.");

        /// <summary>
        /// "ASSIST EASY"
        /// </summary>
        public static LocalisableString GaugeTypeAssistEasy => new TranslatableString(getKey(@"gauge_type_assist_easy"), @"ASSIST EASY");

        /// <summary>
        /// "EASY"
        /// </summary>
        public static LocalisableString GaugeTypeEasy => new TranslatableString(getKey(@"gauge_type_easy"), @"EASY");

        /// <summary>
        /// "NORMAL"
        /// </summary>
        public static LocalisableString GaugeTypeNormal => new TranslatableString(getKey(@"gauge_type_normal"), @"NORMAL");

        /// <summary>
        /// "HARD"
        /// </summary>
        public static LocalisableString GaugeTypeHard => new TranslatableString(getKey(@"gauge_type_hard"), @"HARD");

        /// <summary>
        /// "EX-HARD"
        /// </summary>
        public static LocalisableString GaugeTypeExHard => new TranslatableString(getKey(@"gauge_type_ex_hard"), @"EX-HARD");

        /// <summary>
        /// "HAZARD"
        /// </summary>
        public static LocalisableString GaugeTypeHazard => new TranslatableString(getKey(@"gauge_type_hazard"), @"HAZARD");

        /// <summary>
        /// "Uses beatoraja timing windows."
        /// </summary>
        public static LocalisableString JudgeBeatorajaDescription => new TranslatableString(getKey(@"judge_beatoraja_description"), @"Uses beatoraja timing windows.");

        /// <summary>
        /// "Uses the OMS fixed beatmania IIDX timing preset."
        /// </summary>
        public static LocalisableString JudgeIidxDescription => new TranslatableString(getKey(@"judge_iidx_description"), @"Uses the OMS fixed beatmania IIDX timing preset.");

        /// <summary>
        /// "Uses LR2 timing windows."
        /// </summary>
        public static LocalisableString JudgeLr2Description => new TranslatableString(getKey(@"judge_lr2_description"), @"Uses LR2 timing windows.");

        /// <summary>
        /// "Overrides the chart's default #RANK timing tier for judge systems that support difficulty-based windows."
        /// </summary>
        public static LocalisableString JudgeRankDescription => new TranslatableString(getKey(@"judge_rank_description"), @"Overrides the chart's default #RANK timing tier for judge systems that support difficulty-based windows.");

        /// <summary>
        /// "Judge difficulty"
        /// </summary>
        public static LocalisableString JudgeDifficulty => new TranslatableString(getKey(@"judge_difficulty"), @"Judge difficulty");

        /// <summary>
        /// "Overrides the chart's default #RANK timing tier for the active judge system when that judge family supports difficulty-based windows."
        /// </summary>
        public static LocalisableString JudgeDifficultyDescription => new TranslatableString(getKey(@"judge_difficulty_description"), @"Overrides the chart's default #RANK timing tier for the active judge system when that judge family supports difficulty-based windows.");

        /// <summary>
        /// "VERY HARD"
        /// </summary>
        public static LocalisableString JudgeRankVeryHard => new TranslatableString(getKey(@"judge_rank_very_hard"), @"VERY HARD");

        /// <summary>
        /// "HARD"
        /// </summary>
        public static LocalisableString JudgeRankHard => new TranslatableString(getKey(@"judge_rank_hard"), @"HARD");

        /// <summary>
        /// "NORMAL"
        /// </summary>
        public static LocalisableString JudgeRankNormal => new TranslatableString(getKey(@"judge_rank_normal"), @"NORMAL");

        /// <summary>
        /// "EASY"
        /// </summary>
        public static LocalisableString JudgeRankEasy => new TranslatableString(getKey(@"judge_rank_easy"), @"EASY");

        /// <summary>
        /// "VERY EASY"
        /// </summary>
        public static LocalisableString JudgeRankVeryEasy => new TranslatableString(getKey(@"judge_rank_very_easy"), @"VERY EASY");

        /// <summary>
        /// "Masks the upper portion of the playfield (SUDDEN+ style)."
        /// </summary>
        public static LocalisableString SuddenDescription => new TranslatableString(getKey(@"sudden_description"), @"Masks the upper portion of the playfield (SUDDEN+ style).");

        /// <summary>
        /// "Masks the lower portion of the playfield before notes reach the judgement line (HIDDEN+ style)."
        /// </summary>
        public static LocalisableString HiddenDescription => new TranslatableString(getKey(@"hidden_description"), @"Masks the lower portion of the playfield before notes reach the judgement line (HIDDEN+ style).");

        /// <summary>
        /// "Cover value"
        /// </summary>
        public static LocalisableString CoverValue => new TranslatableString(getKey(@"cover_value"), @"Cover value");

        /// <summary>
        /// "0-1000 maps to 0%-100% of playfield coverage."
        /// </summary>
        public static LocalisableString CoverValueDescription => new TranslatableString(getKey(@"cover_value_description"), @"0-1000 maps to 0%-100% of playfield coverage.");

        /// <summary>
        /// "Cover opacity"
        /// </summary>
        public static LocalisableString CoverOpacity => new TranslatableString(getKey(@"cover_opacity"), @"Cover opacity");

        /// <summary>
        /// "0-1000 maps to 0%-100% lane cover opacity."
        /// </summary>
        public static LocalisableString CoverOpacityDescription => new TranslatableString(getKey(@"cover_opacity_description"), @"0-1000 maps to 0%-100% lane cover opacity.");

        /// <summary>
        /// "Remember gameplay changes"
        /// </summary>
        public static LocalisableString RememberGameplayChanges => new TranslatableString(getKey(@"remember_gameplay_changes"), @"Remember gameplay changes");

        /// <summary>
        /// "When enabled, gameplay adjustments update this mod's saved configuration instead of applying only for the current play."
        /// </summary>
        public static LocalisableString RememberGameplayChangesDescription => new TranslatableString(getKey(@"remember_gameplay_changes_description"), @"When enabled, gameplay adjustments update this mod's saved configuration instead of applying only for the current play.");

        /// <summary>
        /// "Raises the judgement line by shortening the lane from the bottom (LIFT-style)."
        /// </summary>
        public static LocalisableString LiftDescription => new TranslatableString(getKey(@"lift_description"), @"Raises the judgement line by shortening the lane from the bottom (LIFT-style).");

        /// <summary>
        /// "Lift value"
        /// </summary>
        public static LocalisableString LiftValue => new TranslatableString(getKey(@"lift_value"), @"Lift value");

        /// <summary>
        /// "0-1000 maps to 0%-100% of the current lane height."
        /// </summary>
        public static LocalisableString LiftValueDescription => new TranslatableString(getKey(@"lift_value_description"), @"0-1000 maps to 0%-100% of the current lane height.");

        /// <summary>
        /// "Mirror button lanes while keeping scratch in place."
        /// </summary>
        public static LocalisableString MirrorDescription => new TranslatableString(getKey(@"mirror_description"), @"Mirror button lanes while keeping scratch in place.");

        /// <summary>
        /// "Randomise button lanes with RANDOM, R-RANDOM, S-RANDOM, or a custom fixed pattern."
        /// </summary>
        public static LocalisableString RandomDescription => new TranslatableString(getKey(@"random_description"), @"Randomise button lanes with RANDOM, R-RANDOM, S-RANDOM, or a custom fixed pattern.");

        /// <summary>
        /// "Random type"
        /// </summary>
        public static LocalisableString RandomType => new TranslatableString(getKey(@"random_type"), @"Random type");

        /// <summary>
        /// "Choose which beatmania-style randomisation rule to apply."
        /// </summary>
        public static LocalisableString RandomTypeDescription => new TranslatableString(getKey(@"random_type_description"), @"Choose which beatmania-style randomisation rule to apply.");

        /// <summary>
        /// "Custom pattern"
        /// </summary>
        public static LocalisableString CustomPattern => new TranslatableString(getKey(@"custom_pattern"), @"Custom pattern");

        /// <summary>
        /// "Optional fixed pattern override. Examples: S7654321, 7654321, or 7654321|7654321 for 14K."
        /// </summary>
        public static LocalisableString CustomPatternDescription => new TranslatableString(getKey(@"custom_pattern_description"), @"Optional fixed pattern override. Examples: S7654321, 7654321, or 7654321|7654321 for 14K.");

        /// <summary>
        /// "RANDOM"
        /// </summary>
        public static LocalisableString RandomModeRandom => new TranslatableString(getKey(@"random_mode_random"), @"RANDOM");

        /// <summary>
        /// "R-RANDOM"
        /// </summary>
        public static LocalisableString RandomModeRRandom => new TranslatableString(getKey(@"random_mode_r_random"), @"R-RANDOM");

        /// <summary>
        /// "S-RANDOM"
        /// </summary>
        public static LocalisableString RandomModeSRandom => new TranslatableString(getKey(@"random_mode_s_random"), @"S-RANDOM");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
