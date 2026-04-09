// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
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
            DisplayedAssetName = beatmapInfo?.StageFile ?? beatmapInfo?.BackgroundFile ?? beatmapInfo?.BannerFile ?? string.Empty;

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
        private string displayedAssetName = string.Empty;

        public DefaultBmsBackgroundLayerDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
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
                                        Text = "STAGEFILE / BACKBMP",
                                        Colour = BmsDefaultPlayfieldPalette.MetadataLabel,
                                    },
                                    assetLabel = new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = "No STAGEFILE/BACKBMP",
                                        Colour = BmsDefaultPlayfieldPalette.MetadataMissing,
                                    }
                                }
                            }
                        }
                    }
                }
            };

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

            labelContainer.Alpha = hasDisplayedAsset ? 0.85f : 0.45f;
            assetLabel.Text = hasDisplayedAsset ? displayedAssetName : "No STAGEFILE/BACKBMP";
            assetLabel.Colour = hasDisplayedAsset ? BmsDefaultPlayfieldPalette.MetadataAsset : BmsDefaultPlayfieldPalette.MetadataMissing;
        }
    }
}
