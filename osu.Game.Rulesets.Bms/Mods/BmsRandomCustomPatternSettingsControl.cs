// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Rulesets.Bms.Mods
{
    /// <summary>
    /// Settings control for <see cref="BmsModRandom.CustomPattern"/>. A plain text box plus:
    /// <list type="bullet">
    /// <item>keystroke filtering to the recognised pattern character set (digits + separators + scratch marker);</item>
    /// <item>a live validation/preview line under the box, evaluated against the currently selected chart's key count
    /// (falling back to the typed digit count when no BMS chart is available);</item>
    /// <item>a placeholder reminding that a pattern overrides the Random type and seed.</item>
    /// </list>
    /// Mutual exclusion is shown via placeholder / preview / tooltip, NOT by disabling the sibling settings —
    /// disabling a <c>[SettingSource]</c> bindable makes <see cref="osu.Game.Rulesets.Mods.Mod.CopyFrom"/> throw on
    /// clone / deserialize (value transfer goes through <c>BindTo</c>, which cannot write to a disabled bindable).
    /// </summary>
    public partial class BmsRandomCustomPatternSettingsControl : SettingsItem<string>
    {
        protected override Drawable CreateControl() => new PatternControl
        {
            RelativeSizeAxes = Axes.X,
        };

        private sealed partial class PatternControl : CompositeDrawable, IHasCurrentValue<string>
        {
            private readonly PatternTextBox textBox;
            private readonly OsuSpriteText preview;

            // Current forwards directly to the single text-box bindable: no second bindable to ping-pong with,
            // and the preview handler below is read-only, so there is no value feedback loop.
            public Bindable<string> Current
            {
                get => textBox.Current;
                set => textBox.Current = value;
            }

            [Resolved(CanBeNull = true)]
            private IBindable<WorkingBeatmap>? beatmap { get; set; }

            public PatternControl()
            {
                AutoSizeAxes = Axes.Y;

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                    Children = new Drawable[]
                    {
                        textBox = new PatternTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            CommitOnFocusLost = true,
                            PlaceholderText = BmsModStrings.CustomPatternOverridesHint,
                        },
                        preview = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14),
                            Alpha = 0,
                        },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                textBox.Current.BindValueChanged(_ => updatePreview());

                if (beatmap != null)
                    beatmap.BindValueChanged(_ => updatePreview());

                updatePreview();
            }

            private void updatePreview()
            {
                string pattern = textBox.Current.Value;

                if (string.IsNullOrWhiteSpace(pattern))
                {
                    preview.Alpha = 0;
                    return;
                }

                preview.Alpha = 1;

                int keyCount = resolveKeyCount(pattern);

                if (BmsLaneRearrangement.TryNormaliseCustomPattern(keyCount, pattern, out string normalised))
                {
                    preview.Colour = Color4Extensions.FromHex(@"b3d944");
                    preview.Text = BmsModStrings.CustomPatternPreviewValid(keyCount, normalised);
                }
                else
                {
                    preview.Colour = Color4Extensions.FromHex(@"ff5d5b");
                    preview.Text = BmsModStrings.CustomPatternPreviewInvalid;
                }
            }

            private int resolveKeyCount(string pattern)
            {
                // Validate against the selected chart's real key count when available.
                if (beatmap?.Value?.BeatmapInfo != null && BmsRuleset.TryGetKeyCount(beatmap.Value.BeatmapInfo, out int keyCount))
                    return keyCount;

                // Otherwise infer the intended keymode from how many digits were typed (TryNormalise rejects bad counts).
                return pattern.Count(char.IsAsciiDigit);
            }
        }

        private partial class PatternTextBox : OutlinedTextBox
        {
            protected override bool CanAddCharacter(char character) => BmsLaneRearrangement.IsCustomPatternCharacter(character);
        }
    }
}
