// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    internal static class BmsGameplayFeedbackLayout
    {
        public static void ApplyJudgementSnapshot(
            Drawable judgementBody,
            ScrollingDirection direction,
            BmsGameplayLayoutSnapshot snapshot,
            GameplaySkinLaneGroupId? stageGroupId = null)
        {
            var playfield = snapshot.PlayfieldRect;
            var judgement = snapshot.JudgementRect;
            float centreX = stageGroupId == null
                ? (judgement.X + judgement.Width / 2 - playfield.X) / playfield.Width
                : 0.5f;
            float centreY = (judgement.Y + judgement.Height / 2 - playfield.Y) / playfield.Height;

            if (direction == ScrollingDirection.Up)
                centreY = 1 - centreY;

            judgementBody.Anchor = judgementBody.Origin = Anchor.Centre;
            judgementBody.RelativePositionAxes = Axes.Both;
            judgementBody.X = centreX;
            judgementBody.Y = centreY;
        }
    }
}
