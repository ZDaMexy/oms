// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A tri-state sheared button cycling the <see cref="ConvertedBeatmapsDisplay"/> selection at song select
    /// (Hidden → Shown → ConvertedOnly → …). The current state is conveyed by both the button text and its
    /// highlight colour: Hidden = inactive grey, Shown = the standard highlight, ConvertedOnly = a distinct
    /// accent with the "converts only" label.
    /// </summary>
    public partial class ConvertedBeatmapsDisplayButton : ShearedButton
    {
        public Bindable<ConvertedBeatmapsDisplay> Current { get; } = new Bindable<ConvertedBeatmapsDisplay>();

        public ConvertedBeatmapsDisplayButton(float? width = null, float height = 30f)
            : base(width, height)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindDisabledChanged(disabled => Action = disabled ? null : cycle, true);
            Current.BindValueChanged(_ => updateState(), true);
        }

        private void cycle()
        {
            Current.Value = Current.Value switch
            {
                ConvertedBeatmapsDisplay.Hidden => ConvertedBeatmapsDisplay.Shown,
                ConvertedBeatmapsDisplay.Shown => ConvertedBeatmapsDisplay.ConvertedOnly,
                _ => ConvertedBeatmapsDisplay.Hidden,
            };
        }

        private void updateState()
        {
            switch (Current.Value)
            {
                case ConvertedBeatmapsDisplay.Hidden:
                    Text = UserInterfaceStrings.ShowConverts;
                    DarkerColour = ColourProvider.Background3;
                    LighterColour = ColourProvider.Background1;
                    TextColour = ColourProvider.Content1;
                    break;

                case ConvertedBeatmapsDisplay.Shown:
                    Text = UserInterfaceStrings.ShowConverts;
                    DarkerColour = ColourProvider.Highlight1;
                    LighterColour = ColourProvider.Colour0;
                    TextColour = ColourProvider.Background6;
                    break;

                case ConvertedBeatmapsDisplay.ConvertedOnly:
                    Text = OmsSongSelectStrings.ConvertedBeatmapsConvertedOnly;
                    DarkerColour = ColourProvider.Colour2;
                    LighterColour = ColourProvider.Colour1;
                    TextColour = ColourProvider.Background6;
                    break;
            }
        }
    }
}
