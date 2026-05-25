// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsBackgroundLayerDisplay
    {
        void SetDisplayedAssetName(string displayedAssetName);
    }

    public partial class BmsBackgroundLayer : SkinnableDrawable
    {
        public string DisplayedAssetName { get; }

        public bool HasDisplayedAsset => !string.IsNullOrWhiteSpace(DisplayedAssetName);

        public BmsBackgroundLayer(BmsBeatmapInfo? beatmapInfo)
            : base(new BmsSkinComponentLookup(BmsSkinComponents.StaticBackgroundLayer), _ => new DefaultBmsBackgroundLayerDisplay())
        {
            DisplayedAssetName = beatmapInfo?.GetPreferredBackgroundAssetReference() ?? string.Empty;

            RelativeSizeAxes = Axes.Both;
            CentreComponent = false;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);

            if (Drawable is IBmsBackgroundLayerDisplay display)
                display.SetDisplayedAssetName(DisplayedAssetName);
        }
    }

    public partial class DefaultBmsBackgroundLayerDisplay : CompositeDrawable, IBmsBackgroundLayerDisplay
    {
        private FillFlowContainer labelContainer = null!;
        private OsuSpriteText assetLabel = null!;
        private Sprite backgroundSprite = null!;
        private string displayedAssetName = string.Empty;

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        public DefaultBmsBackgroundLayerDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                backgroundSprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Alpha = 0,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.14f,
                    Colour = BmsDefaultPlayfieldPalette.MetadataWash,
                },
                new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    CornerRadius = 10,
                    BorderThickness = 1,
                    BorderColour = BmsDefaultPlayfieldPalette.MetadataPanelBorder,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = BmsDefaultPlayfieldPalette.MetadataPanelBackground,
                        },
                        new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Horizontal = 18, Vertical = 14 },
                            Child = labelContainer = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Spacing = new Vector2(0, 6),
                                Alpha = 0.45f,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = "STAGEFILE / BACKBMP / BGA",
                                        Colour = BmsDefaultPlayfieldPalette.MetadataLabel,
                                    },
                                    assetLabel = new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = "No STAGEFILE/BACKBMP/BGA",
                                        Colour = BmsDefaultPlayfieldPalette.MetadataMissing,
                                    }
                                }
                            }
                        }
                    }
                }
            };

            workingBeatmap?.BindValueChanged(_ => updateDisplay());
            updateDisplay();
        }

        public void SetDisplayedAssetName(string displayedAssetName)
        {
            this.displayedAssetName = displayedAssetName ?? string.Empty;
            updateDisplay();
        }

        private void updateDisplay()
        {
            if (labelContainer == null || assetLabel == null)
                return;

            bool hasDisplayedAsset = !string.IsNullOrWhiteSpace(displayedAssetName);
            var background = workingBeatmap?.Value?.GetBackground();

            if (backgroundSprite != null)
            {
                backgroundSprite.Texture = background;
                backgroundSprite.Alpha = background != null ? 0.32f : 0;
            }

            labelContainer.Alpha = hasDisplayedAsset ? 0.85f : 0.45f;
            assetLabel.Text = hasDisplayedAsset ? displayedAssetName : "No STAGEFILE/BACKBMP/BGA";
            assetLabel.Colour = hasDisplayedAsset ? BmsDefaultPlayfieldPalette.MetadataAsset : BmsDefaultPlayfieldPalette.MetadataMissing;
        }
    }
}
