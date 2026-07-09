// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Visual-only drawable for a <see cref="BmsMine"/>. Mirrors the bar-line pattern: it scrolls in its lane,
    /// never accepts player input, and resolves through an ignore-judgement so it stays out of the scoring /
    /// combo / statistics path. Phase 1 uses a simple non-skinned circle; skinning is a later phase.
    /// </summary>
    public partial class DrawableBmsMine : DrawableHitObject<BmsMine>
    {
        public override bool DisplayResult => false;

        protected override double InitialLifetimeOffset => 2000;

        public DrawableBmsMine(BmsMine hitObject)
            : base(hitObject)
        {
            HandleUserInput = false;

            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Width = 1;
            Height = 18;

            AddInternal(new Circle
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.55f, 0.55f, 0.6f, 1f),
            });
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (timeOffset >= 0)
                ApplyMaxResult();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            base.UpdateHitStateTransforms(state);

            if (state == ArmedState.Hit || state == ArmedState.Miss)
                this.FadeOut(150).Expire();
        }
    }
}
