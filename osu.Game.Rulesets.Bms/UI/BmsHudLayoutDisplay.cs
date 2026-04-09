// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
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
        private const float gauge_width = 460;

        private static readonly Vector2 gauge_position = new Vector2(0, 72);
        private static readonly Vector2 combo_position = new Vector2(0, 126);

        private Drawable? wrappedHud;
        private Drawable gaugeBar = null!;
        private ComboCounter comboCounter = null!;

        public DefaultBmsHudLayoutDisplay()
            : base(_ => { })
        {
        }

        public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
        {
            this.wrappedHud = wrappedHud;
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
            gaugeBar.Anchor = Anchor.TopCentre;
            gaugeBar.Origin = Anchor.TopCentre;
            gaugeBar.Position = gauge_position;
            gaugeBar.Width = gauge_width;

            comboCounter.Anchor = Anchor.TopCentre;
            comboCounter.Origin = Anchor.TopCentre;
            comboCounter.Position = combo_position;

            foreach (var combo in this.ChildrenOfType<ComboCounter>().Where(combo => combo != comboCounter))
                combo.Hide();

            foreach (var drawable in this.ChildrenOfType<ISerialisableDrawable>())
                drawable.UsesFixedAnchor = true;
        }
    }
}
