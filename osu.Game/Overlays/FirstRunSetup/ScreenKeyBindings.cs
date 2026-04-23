// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(FirstRunSetupOverlayStrings), nameof(FirstRunSetupOverlayStrings.KeyBindingSetupTitle))]
    public partial class ScreenKeyBindings : WizardScreen
    {
        private readonly Bindable<string> currentScope = new Bindable<string>();
        private readonly Bindable<string> currentSubsection = new Bindable<string>();
        private readonly Dictionary<string, IReadOnlyList<KeyBindingSubsectionOption>> optionsByScope = new Dictionary<string, IReadOnlyList<KeyBindingSubsectionOption>>(StringComparer.Ordinal);

        private readonly List<string> availableScopes = new List<string>();

        private FormDropdown<string> scopeDropdown = null!;
        private FormDropdown<string> subsectionDropdown = null!;
        private Container subsectionContainer = null!;

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            var maniaRuleset = rulesets.AvailableRulesets.FirstOrDefault(r => r.ShortName == "mania");
            var bmsRuleset = rulesets.AvailableRulesets.FirstOrDefault(r => r.ShortName == "bms");

            addScope("全局设定", createGlobalOptions());

            if (maniaRuleset != null)
                addScope("osu!mania", createVariantOptions(maniaRuleset));

            if (bmsRuleset != null)
                addScope("BMS", createVariantOptions(bmsRuleset));

            Content.Children = new Drawable[]
            {
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Text = "选择一个按键绑定大分类和子分类，然后直接在这里修改对应绑定。",
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                new SettingsItemV2(scopeDropdown = new FormDropdown<string>
                {
                    Caption = "大分类",
                    Current = currentScope,
                    Items = availableScopes,
                }),
                new SettingsItemV2(subsectionDropdown = new FormDropdown<string>
                {
                    Caption = "子分类",
                    Current = currentSubsection,
                    Items = Array.Empty<string>(),
                }),
                subsectionContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
            };

            currentScope.BindValueChanged(_ => updateSubsectionChoices(), true);
            currentSubsection.BindValueChanged(_ => updateSubsectionContent(), true);
        }

        private void addScope(string scope, IReadOnlyList<KeyBindingSubsectionOption> options)
        {
            optionsByScope[scope] = options;
            availableScopes.Add(scope);
        }

        private IReadOnlyList<KeyBindingSubsectionOption> createGlobalOptions()
            => new[]
            {
                new KeyBindingSubsectionOption("全局设定", () => new GlobalKeyBindingsSubsection("全局设定", GlobalActionCategory.General)),
                new KeyBindingSubsectionOption("界面与覆盖层", () => new GlobalKeyBindingsSubsection(InputSettingsStrings.OverlaysSection, GlobalActionCategory.Overlays)),
                new KeyBindingSubsectionOption("音频控制", () => new GlobalKeyBindingsSubsection(InputSettingsStrings.AudioSection, GlobalActionCategory.AudioControl)),
                new KeyBindingSubsectionOption("选歌", () => new GlobalKeyBindingsSubsection(InputSettingsStrings.SongSelectSection, GlobalActionCategory.SongSelect)),
                new KeyBindingSubsectionOption("游戏内", () => new GlobalKeyBindingsSubsection(InputSettingsStrings.InGameSection, GlobalActionCategory.InGame)),
                new KeyBindingSubsectionOption("回放", () => new GlobalKeyBindingsSubsection(InputSettingsStrings.ReplaySection, GlobalActionCategory.Replay)),
                new KeyBindingSubsectionOption("编辑器", () => new GlobalKeyBindingsSubsection(InputSettingsStrings.EditorSection, GlobalActionCategory.Editor)),
                new KeyBindingSubsectionOption("编辑器试玩", () => new GlobalKeyBindingsSubsection(InputSettingsStrings.EditorTestPlaySection, GlobalActionCategory.EditorTestPlay)),
            };

        private static IReadOnlyList<KeyBindingSubsectionOption> createVariantOptions(RulesetInfo ruleset)
        {
            var rulesetInstance = ruleset.CreateInstance();

            return rulesetInstance.AvailableVariants
                                  .Select(variant => new KeyBindingSubsectionOption(
                                      rulesetInstance.GetVariantName(variant).ToString(),
                                      () => new VariantBindingsSubsection(ruleset, variant)))
                                  .ToArray();
        }

        private void updateSubsectionChoices()
        {
            if (!optionsByScope.TryGetValue(currentScope.Value, out var options) || options.Count == 0)
            {
                subsectionDropdown.Items = Array.Empty<string>();
                subsectionContainer.Clear();
                return;
            }

            subsectionDropdown.Items = options.Select(option => option.DisplayName).ToArray();

            if (!options.Any(option => option.DisplayName == currentSubsection.Value))
                currentSubsection.Value = options[0].DisplayName;
        }

        private void updateSubsectionContent()
        {
            subsectionContainer.Clear();

            if (!optionsByScope.TryGetValue(currentScope.Value, out var options))
                return;

            KeyBindingSubsectionOption? option = options.FirstOrDefault(candidate => candidate.DisplayName == currentSubsection.Value);

            if (option == null)
                return;

            subsectionContainer.Child = option.CreateDrawable();
        }

        private sealed record KeyBindingSubsectionOption(string DisplayName, Func<Drawable> CreateDrawable);
    }
}
