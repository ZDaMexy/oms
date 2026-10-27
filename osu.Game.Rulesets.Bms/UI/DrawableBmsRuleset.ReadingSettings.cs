// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsRuleset
    {
        // Reuse the native cover controls as private adapters. They never enter the selected or recorded Mod list.
        private readonly BmsModSudden readingSudden = new BmsModSudden();
        private readonly BmsModHidden readingHidden = new BmsModHidden();
        private readonly BmsModLift readingLift = new BmsModLift();
        private readonly List<Action> readingSettingUnbinders = new List<Action>();
        private bool readingSettingsInitialised;
        private bool savingReadingSettings;
        private bool readingSettingsReadOnlyReplay;
        private bool readingLaneCoversEnabled = true;
        private bool readingSuddenMounted;
        private bool readingHiddenMounted;

        private bool hasLegacyReadingMods => selectedSuddenMod != null || selectedHiddenMod != null || selectedLiftMod != null;

        private void initialiseReadingSettings()
        {
            readingSudden.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics());
            readingHidden.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics());
            readingSudden.GameplayEnabled.BindValueChanged(_ => refreshSpeedMetrics());
            readingHidden.GameplayEnabled.BindValueChanged(_ => refreshSpeedMetrics());
            readingLift.LiftUnits.BindValueChanged(change =>
            {
                if (gameplayLiftMod != null)
                    Playfield.LiftUnits.Value = change.NewValue;
                refreshSpeedMetrics();
            });

            applyReadingSettings(Config.CaptureReadingSettings());
            readingSettingsInitialised = true;

            // Explicit changes in settings reach the current root. Runtime edits only persist at the end of a
            // complete adjustment, including Floating's cover/speed compensation.
            trackReadingSetting<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode, (state, value) => state with { HiSpeedMode = value });
            trackReadingSetting<double>(BmsRulesetSetting.ScrollSpeed, (state, value) => state with { NormalHiSpeed = value });
            trackReadingSetting<double>(BmsRulesetSetting.FloatingHiSpeed, (state, value) => state with { FloatingHiSpeed = value });
            trackReadingSetting<double>(BmsRulesetSetting.ClassicHiSpeed, (state, value) => state with { ClassicHiSpeed = value });
            trackReadingSetting<bool>(BmsRulesetSetting.SuddenEnabled, (state, value) => state with { SuddenEnabled = value });
            trackReadingSetting<bool>(BmsRulesetSetting.HiddenEnabled, (state, value) => state with { HiddenEnabled = value });
            trackReadingSetting<bool>(BmsRulesetSetting.LiftEnabled, (state, value) => state with { LiftEnabled = value });
            trackReadingSetting<bool>(BmsRulesetSetting.LaneCoversEnabled, (state, value) => state with { LaneCoversEnabled = value });
            trackReadingSetting<float>(BmsRulesetSetting.SuddenUnits, (state, value) => state with { SuddenUnits = value });
            trackReadingSetting<float>(BmsRulesetSetting.HiddenUnits, (state, value) => state with { HiddenUnits = value });
            trackReadingSetting<float>(BmsRulesetSetting.LiftUnits, (state, value) => state with { LiftUnits = value });
            trackReadingSetting<float>(BmsRulesetSetting.SuddenOpacity, (state, value) => state with { SuddenOpacity = value }, resetAdjustment: false);
            trackReadingSetting<float>(BmsRulesetSetting.HiddenOpacity, (state, value) => state with { HiddenOpacity = value }, resetAdjustment: false);
        }

        private void trackReadingSetting<T>(BmsRulesetSetting setting, Func<BmsReadingSettingsSnapshot, T, BmsReadingSettingsSnapshot> update, bool resetAdjustment = true)
        {
            // GetBindable already returns a bound copy. A second copy would leave a weak, collectable relay.
            IBindable<T> source = Config.GetBindable<T>(setting);
            source.BindValueChanged(change =>
            {
                if (!readingSettingsReadOnlyReplay && !savingReadingSettings)
                    applyReadingSettings(update(captureReadingSettings(), change.NewValue), resetAdjustment);
            });
            readingSettingUnbinders.Add(source.UnbindAll);
        }

        private BmsReadingSettingsSnapshot captureReadingSettings()
        {
            BmsModSudden sudden = selectedSuddenMod ?? readingSudden;
            BmsModHidden hidden = selectedHiddenMod ?? readingHidden;
            BmsModLift lift = selectedLiftMod ?? readingLift;
            return new BmsReadingSettingsSnapshot(
                configHiSpeedMode.Value, configNormalHiSpeed.Value, configFloatingHiSpeed.Value, configClassicHiSpeed.Value,
                getSuddenMod() != null, getHiddenMod() != null, getLiftMod() != null, readingLaneCoversEnabled,
                sudden.CoverPercent.Value, hidden.CoverPercent.Value, lift.LiftUnits.Value,
                sudden.CoverOpacity.Value, hidden.CoverOpacity.Value);
        }

        private void applyReadingSettings(BmsReadingSettingsSnapshot state, bool resetAdjustment = true)
        {
            configNormalHiSpeed.Value = state.NormalHiSpeed;
            configFloatingHiSpeed.Value = state.FloatingHiSpeed;
            configClassicHiSpeed.Value = state.ClassicHiSpeed;
            configHiSpeedMode.Value = state.HiSpeedMode;

            // Historical Mod instances are the parameter source for legacy playback. New plays use settings only.
            if (!hasLegacyReadingMods)
            {
                readingLaneCoversEnabled = state.LaneCoversEnabled;
                readingSudden.CoverPercent.Value = state.SuddenUnits;
                readingHidden.CoverPercent.Value = state.HiddenUnits;
                readingLift.LiftUnits.Value = state.LiftUnits;
                readingSudden.CoverOpacity.Value = state.SuddenOpacity;
                readingHidden.CoverOpacity.Value = state.HiddenOpacity;
                gameplaySuddenMod = state.SuddenEnabled ? readingSudden : null;
                gameplayHiddenMod = state.HiddenEnabled ? readingHidden : null;
                gameplayLiftMod = state.LiftEnabled ? readingLift : null;
                readingSudden.GameplayEnabled.Value = state.SuddenEnabled && state.LaneCoversEnabled;
                readingHidden.GameplayEnabled.Value = state.HiddenEnabled && state.LaneCoversEnabled;
                if (state.SuddenEnabled)
                    mountReadingSudden();
                if (state.HiddenEnabled)
                    mountReadingHidden();
                Playfield.LiftUnits.Value = state.LiftEnabled ? state.LiftUnits : 0;
            }

            if (resetAdjustment)
            {
                floatingCoveredVisibleTime = null;
                restoreFloatingCoverTime = false;
                scratchAdjustmentRemainders.Clear();
            }
            RefreshLaneCoverFocus();
            refreshSpeedMetrics();
        }

        private void mountReadingSudden()
        {
            if (readingSuddenMounted)
                return;
            readingSudden.ApplyToDrawableRuleset(this);
            readingSuddenMounted = true;
        }

        private void mountReadingHidden()
        {
            if (readingHiddenMounted)
                return;
            readingHidden.ApplyToDrawableRuleset(this);
            readingHiddenMounted = true;
        }

        private void rememberReadingSettings()
        {
            if (!readingSettingsInitialised || readingSettingsReadOnlyReplay || !Config.Get<bool>(BmsRulesetSetting.RememberReadingChanges))
                return;
            savingReadingSettings = true;
            try
            {
                Config.SaveReadingSettings(captureReadingSettings());
            }
            finally
            {
                savingReadingSettings = false;
            }
        }

        private void applyReplayReadingSettings(Score? score)
        {
            readingSettingsReadOnlyReplay = score != null && score.Replay is not BmsGeneratedAutoplayReplay;
            if (!readingSettingsReadOnlyReplay)
                return;

            if (score!.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.ReadingSettings is BmsReadingSettingsSnapshot state)
            {
                state.Validate();
                applyReadingSettings(state);
            }
            else
            {
                // Old replays without a snapshot get covers only from their original Mods, never today's settings.
                applyReadingSettings(captureReadingSettings() with
                {
                    SuddenEnabled = false,
                    HiddenEnabled = false,
                    LiftEnabled = false,
                    LaneCoversEnabled = true,
                });
            }
        }
    }
}
