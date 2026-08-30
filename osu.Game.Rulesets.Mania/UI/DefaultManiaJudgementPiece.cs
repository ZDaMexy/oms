// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class DefaultManiaJudgementPiece : DefaultJudgementPiece
    {
        private IBindable<ScrollingDirection> direction = null!;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public DefaultManiaJudgementPiece(HitResult result)
            : base(result)
        {
        }

        [BackgroundDependencyLoader]
        private void load(ManiaGameplaySkinStageContext stageContext)
        {
            LayoutSnapshot = stageContext.Snapshot;
            direction = new Bindable<ScrollingDirection>(stageContext.Snapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? ScrollingDirection.Up
                : ScrollingDirection.Down);
            ManiaGameplaySkinLayoutProjection.ApplyJudgementPlacement(this, stageContext);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            JudgementText.Font = JudgementText.Font.With(size: 25);
        }

        public override void PlayAnimation()
        {
            switch (Result)
            {
                case HitResult.None:
                    this.FadeOutFromOne(800);
                    break;

                case HitResult.Miss:
                    this.ScaleTo(1.6f);
                    this.ScaleTo(1, 100, Easing.In);

                    this.MoveToOffset(new Vector2(0, direction.Value == ScrollingDirection.Up ? -100 : 100), 800, Easing.InQuint);

                    this.RotateTo(0);
                    this.RotateTo(40, 800, Easing.InQuint);

                    this.FadeOutFromOne(800);
                    break;

                default:
                    this.ScaleTo(0.8f);
                    this.ScaleTo(1, 250, Easing.OutElastic);

                    this.Delay(50)
                        .ScaleTo(0.75f, 250)
                        .FadeOut(200);

                    // osu!mania uses a custom fade length, so the base call is intentionally omitted.
                    break;
            }
        }
    }
}
