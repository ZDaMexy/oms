// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsComboCounter : ComboCounter
    {
        private TextComponentHost textComponent = null!;
        [Resolved(CanBeNull = true)]
        private BmsGameplayLayoutProvider? layoutProvider { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinLayoutRevisionOwner? layoutOwner { get; set; }

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

        internal GameplaySkinResolvedMaterialSet? ResolvedMaterialSet { get; private set; }

        internal Drawable GameplaySkinFallbackVisual => textComponent.StageVisuals[0];

        internal IReadOnlyList<Drawable> GameplaySkinStageFallbackVisuals => textComponent.StageVisuals;

        protected override double RollingDuration => 80;

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            LayoutSnapshot = BmsGameplayLayoutProvider.ResolveOwnerPublication(
                layoutOwner,
                layoutProvider,
                "bms.layout.missing-combo-publication");
            ResolvedMaterialSet = BmsGameplayLayoutProvider.ResolveOwnerMaterialSet(
                layoutOwner,
                layoutProvider,
                "bms.material.missing-combo-publication");

            if (!ReferenceEquals(ResolvedMaterialSet.Snapshot, LayoutSnapshot.Neutral))
                throw new InvalidOperationException("The BMS combo counter does not retain the material set from its exact publication.");

            textComponent.Initialise(LayoutSnapshot);
            ApplyStageLocalLayout();

            Current.BindTo(scoreProcessor.Combo);
            Current.BindValueChanged(combo =>
            {
                textComponent.UpdateState(combo.NewValue);

                if (combo.NewValue > combo.OldValue && combo.NewValue > 0)
                    textComponent.Pulse();
                else if (combo.OldValue > 1 && combo.NewValue == 0)
                    textComponent.FlashMiss();
            }, true);
        }

        protected override LocalisableString FormatCount(int count) => $@"{count}x";

        protected override IHasText CreateText() => textComponent = new TextComponentHost();

        internal void ApplyStageLocalLayout()
        {
            AutoSizeAxes = Axes.None;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativePositionAxes = Axes.None;
            RelativeSizeAxes = Axes.Both;
            Position = Vector2.Zero;
            Size = Vector2.One;
        }

        // Bare combo readout: just the COMBO label + count, centred, with no background colour block / border so the
        // counter sits cleanly over the playfield centre.
        private partial class TextComponentHost : CompositeDrawable, IHasText
        {
            private readonly List<TextComponent> stageVisuals = new List<TextComponent>();
            private LocalisableString text;

            public LocalisableString Text
            {
                get => text;
                set
                {
                    text = value;

                    foreach (TextComponent stage in stageVisuals)
                        stage.Text = value;
                }
            }

            public IReadOnlyList<TextComponent> StageVisuals => stageVisuals;

            public TextComponentHost()
            {
                RelativeSizeAxes = Axes.Both;
            }

            public void Initialise(BmsGameplayLayoutSnapshot snapshot)
            {
                ArgumentNullException.ThrowIfNull(snapshot);

                if (stageVisuals.Count != 0)
                    throw new InvalidOperationException("The BMS combo stage-local visual graph is immutable after load.");

                GameplaySkinLayoutRect combo = snapshot.ComboRect;

                foreach (GameplaySkinLaneTopologyGroup group in snapshot.Neutral.Context.Topology.GroupsInLogicalOrder)
                {
                    GameplaySkinLayoutRect groupRect = snapshot.Neutral.GetGroup(group.Identity.Id).Rect;
                    var stage = new TextComponent(GameplaySkinResolvedMaterialTarget.ForStage(group))
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.Centre,
                        RelativePositionAxes = Axes.Both,
                        X = groupRect.X + groupRect.Width / 2,
                        Y = combo.Y + combo.Height / 2,
                        Text = text,
                    };
                    stageVisuals.Add(stage);
                }

                InternalChildren = stageVisuals.ToArray();
            }

            public void UpdateState(int combo)
            {
                foreach (TextComponent stage in stageVisuals)
                    stage.UpdateState(combo);
            }

            public void Pulse()
            {
                foreach (TextComponent stage in stageVisuals)
                    stage.Pulse();
            }

            public void FlashMiss()
            {
                foreach (TextComponent stage in stageVisuals)
                    stage.FlashMiss();
            }
        }

        private partial class TextComponent : CompositeDrawable, IHasText
        {
            private readonly OsuSpriteText labelText;
            private readonly OsuSpriteText countText;

            public LocalisableString Text
            {
                get => countText.Text;
                set => countText.Text = value;
            }

            public GameplaySkinResolvedMaterialTarget Target { get; }

            public TextComponent(GameplaySkinResolvedMaterialTarget target)
            {
                Target = target ?? throw new ArgumentNullException(nameof(target));
                AutoSizeAxes = Axes.Both;

                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        labelText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "COMBO",
                            Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                            Colour = BmsDefaultHudPalette.SurfaceSubtext,
                        },
                        countText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.Numeric.With(size: 32, fixedWidth: true),
                            Colour = BmsDefaultHudPalette.SurfaceText,
                            Shadow = true,
                        },
                    }
                };
            }

            public void UpdateState(int combo)
            {
                var accent = combo switch
                {
                    >= 100 => BmsDefaultHudPalette.ComboMilestoneAccent,
                    > 0 => BmsDefaultHudPalette.ComboActiveAccent,
                    _ => BmsDefaultHudPalette.ComboInactiveAccent,
                };

                bool active = combo > 0;

                labelText.Colour = active ? accent : BmsDefaultHudPalette.SurfaceSubtext;
                countText.Colour = active ? BmsDefaultHudPalette.SurfaceText : BmsDefaultHudPalette.SurfaceSubtext;
            }

            public void Pulse()
            {
                countText.ClearTransforms();
                countText.ScaleTo(new Vector2(1.08f), 60, Easing.OutQuint)
                         .Then()
                         .ScaleTo(Vector2.One, 180, Easing.OutQuint);
            }

            public void FlashMiss()
            {
                countText.ClearTransforms();
                countText.ScaleTo(new Vector2(0.94f), 70, Easing.OutQuint)
                         .Then()
                         .ScaleTo(Vector2.One, 220, Easing.OutQuint);
            }
        }
    }
}
