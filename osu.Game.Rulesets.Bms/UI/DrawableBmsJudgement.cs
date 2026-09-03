// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    internal partial class DrawableBmsJudgement : DrawableJudgement
    {
        private IBindable<ScrollingDirection> direction = null!;
        private GameplaySkinLaneGroupId? stageGroupId;

        [Resolved]
        private BmsGameplayLayoutProvider layoutProvider { get; set; } = null!;

        internal BmsGameplayLayoutSnapshot LayoutSnapshot => layoutProvider.Current;

        public DrawableBmsJudgement()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(1f);
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction = scrollingInfo.Direction.GetBoundCopy();
            direction.BindValueChanged(_ => updateJudgementPosition());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateJudgementPosition();
        }

        protected override Drawable CreateDefaultJudgement(HitResult result) => new BmsJudgementPiece(result);

        internal void InitialiseStage(GameplaySkinLaneGroupId groupId)
        {
            ArgumentNullException.ThrowIfNull(groupId);

            if (stageGroupId != null && !stageGroupId.Equals(groupId))
                throw new InvalidOperationException("A pooled BMS judgement cannot change its exact stage owner.");

            stageGroupId = groupId;
            updateJudgementPosition();
        }

        private void updateJudgementPosition()
        {
            if (JudgementBody == null)
                return;

            BmsGameplayFeedbackLayout.ApplyJudgementSnapshot(JudgementBody, direction.Value, layoutProvider.Current, stageGroupId);
        }
    }
}
