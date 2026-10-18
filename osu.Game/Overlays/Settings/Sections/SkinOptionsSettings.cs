// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>Builds controls from the selected package. Preferences never modify a skin or an active gameplay root.</summary>
    public partial class SkinOptionsSettings : FillFlowContainer
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private SkinOptionPreferences preferences { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private Bindable<Skin> current = null!;
        private IBindable<RulesetInfo> currentRuleset = null!;
        private string? saveError;

        public SkinOptionsSettings()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 6);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // Detached settings hosts can omit the application preferences / mode service.
            if (preferences == null || ruleset == null)
                return;
            current = skins.CurrentSkin.GetBoundCopy();
            currentRuleset = ruleset.GetBoundCopy();
            current.BindValueChanged(_ => { saveError = null; rebuild(); });
            currentRuleset.BindValueChanged(_ => { saveError = null; rebuild(); });
            preferences.Changed += preferencesChanged;
            rebuild();
        }

        private void preferencesChanged() => Schedule(rebuild);

        private void rebuild()
        {
            if (IsDisposed)
                return;
            Clear();
            Skin skin = current.Value;
            string mode = currentRuleset.Value.ShortName;
            Add(text("皮肤配置项 · " + (mode == "bms" ? "BMS" : "osu!mania")));
            GameplaySkinOptionDefinition[] definitions = skin.PreparedGameplaySkinPackage?.Manifest?.Options
                .Where(option => option.Rulesets.Count == 0 || option.Rulesets.Contains(mode, StringComparer.Ordinal)).ToArray()
                ?? [];
            if (definitions.Length == 0)
            {
                Add(text("此皮肤未声明可调整的配置项，使用作者默认表现。"));
                return;
            }

            Add(text("仅修改此皮肤在当前模式的配置；下次进入游玩或重新打开预览时生效。"));
            if (saveError != null)
                Add(text(saveError));
            IReadOnlyDictionary<string, string> saved = preferences.GetOverrides(skin.SkinInfo.ID, mode);
            foreach (GameplaySkinOptionDefinition definition in definitions)
            {
                string raw = saved.GetValueOrDefault(definition.Id) ?? definition.DefaultValue;
                if (!definition.TryValidateValue(raw, out GameplaySkinOptionValue value))
                    definition.TryValidateValue(definition.DefaultValue, out value);
                string hint = definition.Keymodes.Count == 0 ? string.Empty : "适用键数：" + string.Join("、", definition.Keymodes);
                switch (definition.Kind)
                {
                    case GameplaySkinOptionKind.Choice:
                        var choice = new Bindable<string>(value.CanonicalValue) { Default = definition.DefaultValue };
                        Add(new SettingsItemV2(new OptionDropdown(definition.Choices)
                        {
                            Caption = definition.Label,
                            HintText = hint,
                            AlwaysShowSearchBar = definition.IsContentCatalog,
                            Current = choice,
                            Items = definition.Choices.Select(item => item.Value),
                        }));
                        choice.BindValueChanged(change => save(definition, change.NewValue));
                        break;

                    case GameplaySkinOptionKind.Toggle:
                        var toggle = new BindableBool(value.BooleanValue) { Default = definition.DefaultValue == "true" };
                        Add(new SettingsItemV2(new FormCheckBox { Caption = definition.Label, HintText = hint, Current = toggle }));
                        toggle.BindValueChanged(change => save(definition, change.NewValue ? "true" : "false"));
                        break;

                    case GameplaySkinOptionKind.Number:
                        definition.TryValidateValue(definition.DefaultValue, out GameplaySkinOptionValue defaultNumber);
                        var number = new BindableDouble
                        {
                            MinValue = definition.Minimum,
                            MaxValue = definition.Maximum,
                            Default = defaultNumber.NumberValue,
                            Value = value.NumberValue,
                        };
                        Add(new SettingsItemV2(new FormSliderBar<double>
                        {
                            Caption = definition.Label,
                            HintText = hint,
                            Current = number,
                            KeyboardStep = (float)definition.Step,
                            TransferValueOnCommit = true,
                        }));
                        number.BindValueChanged(change =>
                        {
                            double snapped = Math.Clamp(definition.Minimum + Math.Round((change.NewValue - definition.Minimum) / definition.Step) * definition.Step,
                                definition.Minimum, definition.Maximum);
                            number.Value = snapped;
                            save(definition, snapped.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                        });
                        break;
                }
            }
            Add(new SettingsButtonV2
            {
                Text = "恢复此皮肤的默认配置",
                Action = () => { saveError = null; preferences.Reset(skin.SkinInfo.ID, mode); },
            });

            void save(GameplaySkinOptionDefinition definition, string value)
            {
                if (definition.TryValidateValue(value, out GameplaySkinOptionValue validated))
                {
                    try
                    {
                        preferences.SetOverride(skin.SkinInfo.ID, mode, definition.Id,
                            validated.CanonicalValue == definition.DefaultValue ? null : validated.CanonicalValue,
                            definitions.Select(item => item.Id).ToArray());
                        saveError = null;
                    }
                    catch (SkinOptionPreferenceBudgetException)
                    {
                        saveError = "皮肤配置存储空间已满，当前改动未保存。";
                        Schedule(rebuild);
                    }
                }
            }
        }

        private static OsuTextFlowContainer text(string value) => new()
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_PADDING.Left },
            Text = value,
        };

        protected override void Dispose(bool isDisposing)
        {
            if (preferences != null)
                preferences.Changed -= preferencesChanged;
            current?.UnbindAll();
            currentRuleset?.UnbindAll();
            base.Dispose(isDisposing);
        }

        private partial class OptionDropdown : FormDropdown<string>
        {
            private readonly IReadOnlyDictionary<string, string> labels;

            public OptionDropdown(IReadOnlyList<GameplaySkinOptionChoice> choices)
                => labels = choices.ToDictionary(choice => choice.Value, choice => choice.Label, StringComparer.Ordinal);

            protected override LocalisableString GenerateItemText(string item) => labels[item];
        }
    }
}
