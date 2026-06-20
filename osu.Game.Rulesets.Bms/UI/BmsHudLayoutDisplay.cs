// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsHudLayoutDisplay
    {
        void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter);
    }

    public partial class DefaultBmsHudLayoutDisplay : DefaultSkinComponentsContainer, IBmsHudLayoutDisplay
    {
        // Gauge top edge sits flush just below the judgement line (the playfield bottom = DEFAULT_PLAYFIELD_HEIGHT). A
        // hairline gap keeps it off the hit line itself while still reading as the playfield's bottom segment.
        private const float gauge_top = BmsPlayfieldLayoutProfile.DEFAULT_PLAYFIELD_HEIGHT + 0.002f;

        // Centred relative width used when the playable beatmap / ruleset config can't be resolved (skin editor / tests).
        private const float gauge_fallback_width = 0.4f;

        private readonly Bindable<BmsPlayfieldStyle> playfieldStyle = new Bindable<BmsPlayfieldStyle>();

        private Drawable gaugeBar = null!;
        private ComboCounter comboCounter = null!;

        private BmsKeymode gaugeKeymode = BmsKeymode.Key7K;
        private float gaugePlayfieldWidth = gauge_fallback_width;
        private bool gaugeGeometryResolved;

        [Resolved(CanBeNull = true)]
        private GameplayState? gameplayState { get; set; }

        [Resolved(CanBeNull = true)]
        private IRulesetConfigCache? rulesetConfigCache { get; set; }

        public DefaultBmsHudLayoutDisplay()
            : base(_ => { })
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Mirror the live playfield side anchoring (P1/P2/centre) so the gauge tracks the lanes when the style changes.
            // IRulesetConfigCache is game-global, so this resolves the SAME BmsRulesetConfigManager the playfield uses.
            if (rulesetConfigCache?.GetConfigFor(new BmsRuleset()) is BmsRulesetConfigManager config)
                config.BindWith(BmsRulesetSetting.PlayfieldStyle, playfieldStyle);

            // The playable beatmap (hence keymode / playfield width) is constant for the session — resolve it once.
            if (gameplayState != null)
            {
                var layout = BmsLaneLayout.CreateFor(gameplayState.Beatmap);
                gaugeKeymode = layout.Keymode;
                gaugePlayfieldWidth = layout.Profile.PlayfieldWidth;
                gaugeGeometryResolved = true;
            }

            playfieldStyle.BindValueChanged(_ =>
            {
                applyGaugePlacement();
                applyComboPlacement();
            });
        }

        public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
        {
            this.gaugeBar = gaugeBar;
            this.comboCounter = comboCounter;

            Clear();

            if (wrappedHud != null)
                Add(wrappedHud);

            Add(gaugeBar);
            Add(comboCounter);

            ScheduleAfterChildren(applyDefaults);
        }

        private void applyDefaults()
        {
            applyGaugePlacement();
            applyComboPlacement();

            // Safety net: the BMS default combo is removed from the wrapped HUD at the transformer level
            // (BmsSkinTransformer.stripDefaultHudElements). Any other stray combo gets hidden here.
            foreach (var combo in this.ChildrenOfType<ComboCounter>().Where(combo => combo != comboCounter))
                combo.Hide();

            foreach (var drawable in this.ChildrenOfType<ISerialisableDrawable>())
                drawable.UsesFixedAnchor = true;
        }

        // Centres the combo counter on the playfield centre (the intersection of the playfield's width / height midlines),
        // mirroring the playfield's P1/P2/centre side anchoring. Falls back to screen centre when geometry is unresolved.
        private void applyComboPlacement()
        {
            if (comboCounter == null)
                return;

            var appliedStyle = gaugeGeometryResolved
                ? playfieldStyle.Value.GetAppliedStyle(gaugeKeymode)
                : BmsPlayfieldStyle.Center;

            float halfWidth = (gaugeGeometryResolved ? gaugePlayfieldWidth : gauge_fallback_width) / 2f;

            float centreX = appliedStyle switch
            {
                BmsPlayfieldStyle.P1 => BmsPlayfield.SIDE_ANCHORED_HORIZONTAL_INSET + halfWidth,
                BmsPlayfieldStyle.P2 => 1f - BmsPlayfield.SIDE_ANCHORED_HORIZONTAL_INSET - halfWidth,
                _ => 0.5f,
            };

            comboCounter.Anchor = Anchor.TopLeft;
            comboCounter.Origin = Anchor.Centre;
            comboCounter.RelativePositionAxes = Axes.Both;
            comboCounter.Position = new Vector2(centreX, BmsPlayfieldLayoutProfile.DEFAULT_PLAYFIELD_HEIGHT / 2f);
        }

        // Places the gauge just below the judgement line, width-matched to the playfield strip and mirroring its P1/P2/centre
        // side anchoring. Falls back to a centred default when the playable beatmap / ruleset config are unavailable
        // (skin-editor preview, isolated tests) so the layout never throws.
        private void applyGaugePlacement()
        {
            if (gaugeBar == null)
                return;

            gaugeBar.RelativeSizeAxes = Axes.X;
            gaugeBar.RelativePositionAxes = Axes.Both;
            gaugeBar.Width = gaugeGeometryResolved ? gaugePlayfieldWidth : gauge_fallback_width;
            gaugeBar.Y = gauge_top;

            var appliedStyle = gaugeGeometryResolved
                ? playfieldStyle.Value.GetAppliedStyle(gaugeKeymode)
                : BmsPlayfieldStyle.Center;

            switch (appliedStyle)
            {
                case BmsPlayfieldStyle.P1:
                    gaugeBar.Anchor = gaugeBar.Origin = Anchor.TopLeft;
                    gaugeBar.X = BmsPlayfield.SIDE_ANCHORED_HORIZONTAL_INSET;
                    break;

                case BmsPlayfieldStyle.P2:
                    gaugeBar.Anchor = gaugeBar.Origin = Anchor.TopRight;
                    gaugeBar.X = -BmsPlayfield.SIDE_ANCHORED_HORIZONTAL_INSET;
                    break;

                default:
                    gaugeBar.Anchor = gaugeBar.Origin = Anchor.TopCentre;
                    gaugeBar.X = 0;
                    break;
            }
        }
    }
}
