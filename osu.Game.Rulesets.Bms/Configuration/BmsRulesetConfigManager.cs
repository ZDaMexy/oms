// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets.Bms.Configuration
{
    public class BmsRulesetConfigManager : RulesetConfigManager<BmsRulesetSetting>
    {
        public const double NORMAL_HI_SPEED_MIN = 1.0;
        public const double NORMAL_HI_SPEED_MAX = 20.0;
        public const double NORMAL_HI_SPEED_PRECISION = 0.1;

        public const double FLOATING_HI_SPEED_MIN = 0.5;
        public const double FLOATING_HI_SPEED_MAX = 10.0;
        public const double FLOATING_HI_SPEED_PRECISION = double.Epsilon;

        public const double CLASSIC_HI_SPEED_MIN = 0.5;
        public const double CLASSIC_HI_SPEED_MAX = 10.0;
        public const double CLASSIC_HI_SPEED_PRECISION = 0.25;

        public BmsRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : this(settings, ruleset, variant, hasStoredReadingSettings(settings, ruleset, variant))
        {
        }

        private BmsRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant, bool hadStoredReadingSettings)
            : base(settings, ruleset, variant)
        {
            migrateReadingSettings(hadStoredReadingSettings);
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(BmsRulesetSetting.HiSpeedMode, BmsHiSpeedMode.Normal);
            SetDefault(BmsRulesetSetting.ScrollSpeed, 8.0, NORMAL_HI_SPEED_MIN, NORMAL_HI_SPEED_MAX, NORMAL_HI_SPEED_PRECISION);
            AddBindable(BmsRulesetSetting.FloatingHiSpeed, new ReadingSpeedBindable(2.5)
            {
                MinValue = FLOATING_HI_SPEED_MIN,
                MaxValue = FLOATING_HI_SPEED_MAX,
                Precision = FLOATING_HI_SPEED_PRECISION,
            });
            SetDefault(BmsRulesetSetting.ClassicHiSpeed, 2.50, CLASSIC_HI_SPEED_MIN, CLASSIC_HI_SPEED_MAX, CLASSIC_HI_SPEED_PRECISION);
            SetDefault(BmsRulesetSetting.SuddenEnabled, false);
            SetDefault(BmsRulesetSetting.HiddenEnabled, false);
            SetDefault(BmsRulesetSetting.LiftEnabled, false);
            SetDefault(BmsRulesetSetting.LaneCoversEnabled, true);
            SetDefault(BmsRulesetSetting.SuddenUnits, 250f, 0f, 1000f, 1f);
            SetDefault(BmsRulesetSetting.HiddenUnits, 250f, 0f, 1000f, 1f);
            SetDefault(BmsRulesetSetting.LiftUnits, 250f, 0f, 1000f, 1f);
            SetDefault(BmsRulesetSetting.SuddenOpacity, 1000f, 0f, 1000f, 1f);
            SetDefault(BmsRulesetSetting.HiddenOpacity, 1000f, 0f, 1000f, 1f);
            SetDefault(BmsRulesetSetting.RememberReadingChanges, true);
            SetDefault(BmsRulesetSetting.ReadingSettingsMigrated, false);
            SetDefault(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.Center);
            SetDefault(BmsRulesetSetting.PlayfieldWidth, 0.0, 0.0, 1.0, 0.01);
            SetDefault(BmsRulesetSetting.PlayfieldHeight, 0.0, 0.0, 1.0, 0.01);
            SetDefault(BmsRulesetSetting.LaneWidth, 1.0, 0.5, 2.0, 0.01);
            SetDefault(BmsRulesetSetting.LaneSpacing, 0.0, 0.0, 0.4, 0.01);
            SetDefault(BmsRulesetSetting.ScratchLaneWidthRatio, 1.25, 1.0, 2.0, 0.01);
            SetDefault(BmsRulesetSetting.ScratchLaneSpacing, 0.12, 0.0, 0.4, 0.01);
            SetDefault(BmsRulesetSetting.HitTargetHeight, 16.0, 12.0, 32.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetBarHeight, 12.0, 4.0, 16.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetLineHeight, 3.0, 1.0, 8.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetGlowRadius, 6.0, 0.0, 12.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetVerticalOffset, 0.0, 0.0, 160.0, 1.0);
            SetDefault(BmsRulesetSetting.BarLineHeight, 2.0, 1.0, 6.0, 0.5);
            SetDefault(BmsRulesetSetting.GimmickScrollMode, BmsGimmickScrollMode.Auto);
            SetDefault(BmsRulesetSetting.ShowBga, true);
            SetDefault(BmsRulesetSetting.BgaVideoTranscode, true);
            SetDefault(BmsRulesetSetting.PersistedModState, string.Empty);
            SetDefault(BmsRulesetSetting.AutoKeysound, false);
        }

        public BmsReadingSettingsSnapshot CaptureReadingSettings() => new BmsReadingSettingsSnapshot(
            GetBindable<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode).Value,
            GetBindable<double>(BmsRulesetSetting.ScrollSpeed).Value,
            GetBindable<double>(BmsRulesetSetting.FloatingHiSpeed).Value,
            GetBindable<double>(BmsRulesetSetting.ClassicHiSpeed).Value,
            GetBindable<bool>(BmsRulesetSetting.SuddenEnabled).Value,
            GetBindable<bool>(BmsRulesetSetting.HiddenEnabled).Value,
            GetBindable<bool>(BmsRulesetSetting.LiftEnabled).Value,
            GetBindable<bool>(BmsRulesetSetting.LaneCoversEnabled).Value,
            GetBindable<float>(BmsRulesetSetting.SuddenUnits).Value,
            GetBindable<float>(BmsRulesetSetting.HiddenUnits).Value,
            GetBindable<float>(BmsRulesetSetting.LiftUnits).Value,
            GetBindable<float>(BmsRulesetSetting.SuddenOpacity).Value,
            GetBindable<float>(BmsRulesetSetting.HiddenOpacity).Value);

        private sealed class ReadingSpeedBindable : BindableDouble
        {
            public ReadingSpeedBindable(double value)
                : base(value)
            {
            }

            // Settings persistence must not round native scratch/refloat values to display decimals.
            public override string ToString(string? format, IFormatProvider? provider) => Value.ToString(format ?? "R", provider ?? CultureInfo.InvariantCulture);

            protected override Bindable<double> CreateInstance() => new ReadingSpeedBindable(Default);
        }

        public void SaveReadingSettings(BmsReadingSettingsSnapshot snapshot)
        {
            snapshot.Validate();

            GetBindable<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode).Value = snapshot.HiSpeedMode;
            GetBindable<double>(BmsRulesetSetting.ScrollSpeed).Value = snapshot.NormalHiSpeed;
            GetBindable<double>(BmsRulesetSetting.FloatingHiSpeed).Value = snapshot.FloatingHiSpeed;
            GetBindable<double>(BmsRulesetSetting.ClassicHiSpeed).Value = snapshot.ClassicHiSpeed;
            GetBindable<bool>(BmsRulesetSetting.SuddenEnabled).Value = snapshot.SuddenEnabled;
            GetBindable<bool>(BmsRulesetSetting.HiddenEnabled).Value = snapshot.HiddenEnabled;
            GetBindable<bool>(BmsRulesetSetting.LiftEnabled).Value = snapshot.LiftEnabled;
            GetBindable<bool>(BmsRulesetSetting.LaneCoversEnabled).Value = snapshot.LaneCoversEnabled;
            GetBindable<float>(BmsRulesetSetting.SuddenUnits).Value = snapshot.SuddenUnits;
            GetBindable<float>(BmsRulesetSetting.HiddenUnits).Value = snapshot.HiddenUnits;
            GetBindable<float>(BmsRulesetSetting.LiftUnits).Value = snapshot.LiftUnits;
            GetBindable<float>(BmsRulesetSetting.SuddenOpacity).Value = snapshot.SuddenOpacity;
            GetBindable<float>(BmsRulesetSetting.HiddenOpacity).Value = snapshot.HiddenOpacity;
        }

        private void migrateReadingSettings(bool hadStoredReadingSettings)
        {
            var migrated = GetBindable<bool>(BmsRulesetSetting.ReadingSettingsMigrated);

            if (migrated.Value)
                return;

            if (!hadStoredReadingSettings)
            {
                var legacyState = BmsModStatePersistence.ReadStoredState(GetBindable<string>(BmsRulesetSetting.PersistedModState).Value);
                var selected = new HashSet<string>(legacyState.SelectedModAcronyms, StringComparer.OrdinalIgnoreCase);
                var sudden = legacyState.RememberedMods.LastOrDefault(mod => mod.Acronym.Equals("SUD", StringComparison.OrdinalIgnoreCase));
                var hidden = legacyState.RememberedMods.LastOrDefault(mod => mod.Acronym.Equals("HID", StringComparison.OrdinalIgnoreCase));
                var lift = legacyState.RememberedMods.LastOrDefault(mod => mod.Acronym.Equals("LIFT", StringComparison.OrdinalIgnoreCase));
                var initial = CaptureReadingSettings();

                SaveReadingSettings(new BmsReadingSettingsSnapshot(initial.HiSpeedMode, initial.NormalHiSpeed, initial.FloatingHiSpeed, initial.ClassicHiSpeed,
                    selected.Contains("SUD"), selected.Contains("HID"), selected.Contains("LIFT"), true,
                    readLegacyUnits(sudden, "cover_percent", 250), readLegacyUnits(hidden, "cover_percent", 250), readLegacyUnits(lift, "lift_units", 250),
                    readLegacyUnits(sudden, "cover_opacity", 1000), readLegacyUnits(hidden, "cover_opacity", 1000)));

                GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value = !legacyState.RememberedMods.Any(mod =>
                    BmsModStatePersistence.IsLegacyReadingMod(mod.Acronym)
                    && mod.Settings.TryGetValue("remember_gameplay_changes", out object? value) && value is false);
            }

            migrated.Value = true;
        }

        private static float readLegacyUnits(APIMod? mod, string key, float defaultValue)
        {
            if (mod == null || !mod.Settings.TryGetValue(key, out object? rawValue))
                return defaultValue;

            double value = rawValue switch
            {
                long number => number,
                double number => number,
                _ => double.NaN,
            };

            if (double.IsFinite(value) && value >= 0 && value <= 1000)
                return (float)value;

            Logger.Log($"The saved BMS {mod.Acronym} setting '{key}' is invalid; its reading-setting default will be used.", level: LogLevel.Important);
            return defaultValue;
        }

        private static bool hasStoredReadingSettings(SettingsStore? settings, RulesetInfo ruleset, int? variant)
        {
            if (settings == null)
                return false;

            string rulesetName = ruleset.ShortName;
            int rulesetVariant = variant ?? 0;

            // Defaults are inserted by the base constructor, so check for explicit new keys before it runs.
            return settings.Realm.Realm.All<RealmRulesetSetting>()
                           .Where(setting => setting.RulesetName == rulesetName && setting.Variant == rulesetVariant)
                           .AsEnumerable().Any(setting => setting.Key is nameof(BmsRulesetSetting.SuddenEnabled)
                               or nameof(BmsRulesetSetting.HiddenEnabled) or nameof(BmsRulesetSetting.LiftEnabled)
                               or nameof(BmsRulesetSetting.LaneCoversEnabled)
                               or nameof(BmsRulesetSetting.SuddenUnits) or nameof(BmsRulesetSetting.HiddenUnits) or nameof(BmsRulesetSetting.LiftUnits)
                               or nameof(BmsRulesetSetting.SuddenOpacity) or nameof(BmsRulesetSetting.HiddenOpacity)
                               or nameof(BmsRulesetSetting.RememberReadingChanges));
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings();
    }

    public enum BmsRulesetSetting
    {
        HiSpeedMode,
        ScrollSpeed,
        FloatingHiSpeed,
        ClassicHiSpeed,
        SuddenEnabled,
        HiddenEnabled,
        LiftEnabled,
        LaneCoversEnabled,
        SuddenUnits,
        HiddenUnits,
        LiftUnits,
        SuddenOpacity,
        HiddenOpacity,
        RememberReadingChanges,
        ReadingSettingsMigrated,
        PlayfieldStyle,
        PlayfieldWidth,
        PlayfieldHeight,
        LaneWidth,
        LaneSpacing,
        ScratchLaneWidthRatio,
        ScratchLaneSpacing,
        HitTargetHeight,
        HitTargetBarHeight,
        HitTargetLineHeight,
        HitTargetGlowRadius,
        HitTargetVerticalOffset,
        BarLineHeight,

        // Retired: the keysound pool now auto-grows on demand (BmsKeysoundStore), so the manual channel-count
        // baseline no longer has a UI and is no longer read. Settings persist by enum NAME, so dropping this member
        // does not shift the others; any value left in an existing config is simply ignored.
        GimmickScrollMode,
        ShowBga,
        BgaVideoTranscode,
        PersistedModState,
        AutoKeysound,
    }
}
