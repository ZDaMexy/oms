// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>Changes the settings target only; it never changes the selected ruleset or beatmap.</summary>
    public partial class SkinScopeSelector : FillFlowContainer
    {
        public readonly Bindable<SkinSelectionScope> Current = new(new SkinSelectionScope("bms"));

        public Func<SkinSelectionScope, bool>? HasCustomisation { get; set; }

        private readonly Dictionary<string, int?> rememberedKeys = new();
        private readonly GridContainer modes;
        private readonly FillFlowContainer keyControls;
        private bool keyGridExpanded;

        public SkinScopeSelector()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Padding = SettingsPanel.CONTENT_PADDING;
            Spacing = new Vector2(0, 6);

            Children = new Drawable[]
            {
                modes = new GridContainer { RelativeSizeAxes = Axes.X, Height = 38 },
                keyControls = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Current.BindValueChanged(change =>
            {
                rememberedKeys[change.OldValue.RulesetId] = change.OldValue.KeyCount;
                keyGridExpanded = false;
                rebuild();
            }, true);
        }

        public void RefreshAssignments()
        {
            if (IsLoaded)
                rebuild();
        }

        private void rebuild()
        {
            string mode = Current.Value.RulesetId;
            modes.Content = new[]
            {
                new Drawable[]
                {
                    modeButton("bms", "BMS"),
                    modeButton("mania", "osu!mania"),
                },
            };
            keyControls.Clear();

            if (mode == "bms")
            {
                keyControls.Add(grid(new int?[] { null, 5, 7, 9, 14 }, 5));
                return;
            }

            keyControls.Add(new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 36,
                Content = new[]
                {
                    new Drawable[]
                    {
                        keyButton(null),
                        new ScopeButton
                        {
                            Text = (Current.Value.KeyCount is int keys ? $"{keys}K" : "选择键数") + (keyGridExpanded ? " · 收起" : " · 展开"),
                            Selected = Current.Value.KeyCount != null,
                            TooltipText = "选择要单独设置的键数；圆点表示已有独立设置。",
                            Action = () => { keyGridExpanded = !keyGridExpanded; rebuild(); },
                        },
                    },
                },
            });
            if (keyGridExpanded)
                keyControls.Add(grid(SkinSelectionScope.GetSupportedKeyCounts(mode).Select(keys => (int?)keys).ToArray(), 5));

            ScopeButton modeButton(string id, string label) => new()
            {
                Text = label,
                Selected = mode == id,
                Action = () => Current.Value = new SkinSelectionScope(id, rememberedKeys.GetValueOrDefault(id)),
            };
        }

        private GridContainer grid(int?[] keys, int columns)
        {
            int rows = (keys.Length + columns - 1) / columns;
            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = rows * 36,
                Content = Enumerable.Range(0, rows).Select(row => Enumerable.Range(0, columns)
                    .Select(column => row * columns + column < keys.Length ? (Drawable)keyButton(keys[row * columns + column]) : new Container())
                    .ToArray()).ToArray(),
            };
        }

        private ScopeButton keyButton(int? keys)
        {
            SkinSelectionScope scope = new(Current.Value.RulesetId, keys);
            bool customised = keys != null && HasCustomisation?.Invoke(scope) == true;
            return new ScopeButton
            {
                Scope = scope,
                Text = (keys is int count ? $"{count}K" : "模式默认") + (customised ? " •" : string.Empty),
                TooltipText = customised ? "已单独设置；可以恢复跟随模式默认。" : "未单独设置的键数跟随模式默认。",
                Selected = Current.Value == scope,
                Action = () => Current.Value = scope,
            };
        }

        public partial class ScopeButton : RoundedButton
        {
            public SkinSelectionScope? Scope { get; init; }
            public bool Selected { get; init; }

            public ScopeButton()
            {
                RelativeSizeAxes = Axes.Both;
                Size = Vector2.One;
                Padding = new MarginPadding(2);
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, OverlayColourProvider colourProvider)
                => BackgroundColour = Selected ? colours.Blue3 : colourProvider.Background3;
        }
    }
}
