// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    internal static class BmsGameplayFeedbackLayout
    {
        public const float JudgementDistanceFromEdge = 140f;

        public static readonly Vector2 DefaultGameplayFeedbackPosition = new Vector2(320, 64);

        public static Anchor GetJudgementAnchor(ScrollingDirection direction)
            => direction == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;

        public static float GetJudgementOffset(ScrollingDirection direction)
            => direction == ScrollingDirection.Up ? JudgementDistanceFromEdge : -JudgementDistanceFromEdge;

        public static void ApplyJudgementDefaults(Drawable judgementBody, ScrollingDirection direction)
        {
            Anchor anchor = GetJudgementAnchor(direction);

            judgementBody.Anchor = anchor;
            judgementBody.Origin = anchor;
            judgementBody.Y = GetJudgementOffset(direction);
        }

        public static void ApplyGameplayFeedbackDefaults(Drawable gameplayFeedback)
        {
            gameplayFeedback.Anchor = Anchor.TopCentre;
            gameplayFeedback.Origin = Anchor.TopCentre;
            gameplayFeedback.Position = DefaultGameplayFeedbackPosition;

            if (gameplayFeedback is ISerialisableDrawable serialisableDrawable)
                serialisableDrawable.UsesFixedAnchor = true;
        }
    }
}
