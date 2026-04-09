// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsHoldNote : DrawableBmsHitObject
    {
        public override bool DisplayResult => false;

        private DrawableBmsHoldNoteHead? headDrawable;
        private DrawableBmsHoldNoteTail? tailDrawable;
        private readonly List<DrawableBmsHoldNoteBodyTick> bodyTickDrawables = new List<DrawableBmsHoldNoteBodyTick>();
        private bool isHolding;
        private BmsLongNoteMode? longNoteModeOverrideForTesting;

        [Resolved(CanBeNull = true)]
        private DrawableBmsRuleset? drawableRuleset { get; set; }

        private BmsHoldNote holdNote => (BmsHoldNote)HitObject;

        private BmsLongNoteMode longNoteMode => longNoteModeOverrideForTesting ?? drawableRuleset?.LongNoteMode ?? BmsScoreProcessor.DEFAULT_LONG_NOTE_MODE;

        public DrawableBmsHoldNote(BmsHoldNote hitObject)
            : base(hitObject)
        {
        }

        public override void PlaySamples()
        {
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (headDrawable == null || tailDrawable == null)
                return;

            if (holdNote.AutoPlay)
            {
                if (!headDrawable.Judged && Time.Current >= holdNote.StartTime)
                {
                    isHolding = true;
                    headDrawable.HitForcefully();
                }

                resolveBodyTicksUpToCurrentTime();

                if (headDrawable.Judged && !tailDrawable.Judged && HasReachedHoldTail(holdNote, Time.Current))
                    resolveTail(HitResult.Perfect);

                if (tailDrawable.Judged)
                    finaliseHold();

                return;
            }

            if (!headDrawable.Judged)
            {
                if (HasMissedHoldStartWindow(holdNote, Time.Current))
                    headDrawable.MissForcefully();
            }

            if (!headDrawable.Judged)
                return;

            resolveBodyTicksUpToCurrentTime();

            if (!tailDrawable.Judged)
            {
                if (isHolding && HasReachedHoldTail(holdNote, Time.Current))
                    resolveTail(HitResult.Perfect);
                else if (HasMissedTailReleaseWindow(holdNote, Time.Current))
                    resolveTail(HitResult.Miss);
                else if (!isHolding && HasReachedHoldTail(holdNote, Time.Current))
                    resolveTail(HitResult.Miss);
            }

            if (tailDrawable.Judged)
                finaliseHold();
        }

        public override bool OnPressed(KeyBindingPressEvent<BmsAction> e)
        {
            if (!AcceptsPlayerInput || AllJudged || e.Action != Action.Value || headDrawable == null || tailDrawable == null)
                return false;

            if (headDrawable.Judged)
                return TryApplyLateBodyPress();

            if (CheckHittable?.Invoke(this, Time.Current) == false)
                return false;

            var headResult = ResultForPlayerInput(holdNote.Head!, Time.Current - holdNote.StartTime);

            bool consumedHeadPress = TryApplyHeadPress(headResult);

            if (headResult == HitResult.Miss)
            {
                TryApplyLateBodyPress();
                return consumedHeadPress;
            }

            return consumedHeadPress || TryApplyLateBodyPress();
        }

        public override void OnReleased(KeyBindingReleaseEvent<BmsAction> e)
        {
            if (e.Action != Action.Value || headDrawable == null || tailDrawable == null || !isHolding || tailDrawable.Judged)
                return;

            if (!headDrawable.IsHit && !longNoteMode.RequiresTailJudgement())
                return;

            resolveBodyTicksUpToCurrentTime();

            var releaseResult = ResultForTailRelease(holdNote, Time.Current);

            isHolding = false;

            if (longNoteMode.RequiresTailJudgement())
            {
                if (releaseResult.IsHit() || HasReachedHoldTail(holdNote, Time.Current))
                    resolveTail(releaseResult.IsHit() ? releaseResult : HitResult.Miss);

                return;
            }

            resolveTail(releaseResult.IsHit() ? releaseResult : HitResult.Miss);
        }

        internal bool TryApplyHeadPress(HitResult headResult)
        {
            if (headDrawable == null || tailDrawable == null || headResult == HitResult.None)
                return false;

            headDrawable.ApplyHeadResult(headResult);

            if (headResult.IsHit())
            {
                isHolding = true;
                OnUserPressedSuccessfully?.Invoke(this);
            }

            return true;
        }

        internal bool TryApplyLateBodyPress()
        {
            if (headDrawable == null || tailDrawable == null || !CanApplyLateBodyPress(longNoteMode, holdNote, tailDrawable.Judged, Time.Current))
                return false;

            if (!headDrawable.Judged)
                headDrawable.MissForcefully();

            isHolding = true;
            return true;
        }

        internal bool IsHoldingForTesting => isHolding;

        internal BmsLongNoteMode LongNoteModeOverrideForTesting
        {
            set
            {
                longNoteModeOverrideForTesting = value;
                applyTestingLongNoteMode(value);
            }
        }

        internal static bool HasMissedHoldStartWindow(BmsHoldNote holdNote, double currentTime)
            => holdNote.Head?.HitWindows != null && currentTime - holdNote.StartTime > holdNote.Head.HitWindows.WindowFor(HitResult.Miss);

        internal static bool HasReachedHoldTail(BmsHoldNote holdNote, double currentTime)
            => currentTime >= holdNote.EndTime;

        internal static bool CanApplyLateBodyPress(BmsLongNoteMode longNoteMode, BmsHoldNote holdNote, bool tailJudged, double currentTime)
            => longNoteMode.RequiresTailJudgement()
               && !tailJudged
               && currentTime >= holdNote.StartTime
               && !HasMissedTailReleaseWindow(holdNote, currentTime);

        internal static bool HasMissedTailReleaseWindow(BmsHoldNote holdNote, double currentTime)
        {
            if (holdNote.Tail?.HitWindows == null)
                return false;

            double missWindow = holdNote.Tail.HitWindows is BmsTimingWindows bmsTimingWindows
                ? bmsTimingWindows.WindowFor(HitResult.Miss, isLongNoteRelease: true)
                : holdNote.Tail.HitWindows.WindowFor(HitResult.Miss);

            return currentTime - holdNote.EndTime > missWindow;
        }

        internal static HitResult ResultForTailRelease(BmsHoldNote holdNote, double currentTime)
        {
            if (holdNote.Tail?.HitWindows == null)
                return HitResult.None;

            double releaseOffset = currentTime - holdNote.EndTime;

            var releaseResult = holdNote.Tail.HitWindows is BmsTimingWindows bmsTimingWindows
                ? bmsTimingWindows.Evaluate(releaseOffset, isLongNoteRelease: true)
                : holdNote.Tail.HitWindows.ResultFor(releaseOffset);

            return releaseResult.IsHit() ? releaseResult : HitResult.None;
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
        {
            if (hitObject is BmsHoldNoteHead head)
                return headDrawable = new DrawableBmsHoldNoteHead(head);

            if (hitObject is BmsHoldNoteTailEvent tailEvent)
                return tailDrawable = new DrawableBmsHoldNoteTail(tailEvent);

            if (hitObject is BmsHoldNoteBodyTick bodyTick)
                return registerBodyTick(new DrawableBmsHoldNoteBodyTick(bodyTick));

            return base.CreateNestedHitObject(hitObject);
        }

        protected override void ClearNestedHitObjects()
        {
            base.ClearNestedHitObjects();
            headDrawable = null;
            tailDrawable = null;
            bodyTickDrawables.Clear();
        }

        private void resolveTail(HitResult tailResult)
        {
            if (tailDrawable?.Judged == true)
                return;

            resolveAllBodyTicks();
            isHolding = false;

            tailDrawable?.ApplyTailResult(tailResult);
            finaliseHold();
        }

        private void finaliseHold()
        {
            if (Judged)
                return;

            if (headDrawable == null || tailDrawable == null || !headDrawable.Judged || !tailDrawable.Judged)
                return;

            if (headDrawable.IsHit && tailDrawable.IsHit)
                ApplyMaxResult();
            else
                ApplyMinResult();
        }

        private DrawableBmsHoldNoteBodyTick registerBodyTick(DrawableBmsHoldNoteBodyTick bodyTick)
        {
            bodyTickDrawables.Add(bodyTick);
            return bodyTick;
        }

        private void applyTestingLongNoteMode(BmsLongNoteMode longNoteMode)
        {
            if (holdNote.Tail?.Judgement is BmsHoldNoteTailJudgement tailJudgement)
                tailJudgement.CountsForScore = longNoteMode.RequiresTailJudgement();

            foreach (var bodyTick in holdNote.BodyTicks)
                bodyTick.CountsForGauge = longNoteMode.RequiresBodyGaugeTicks();
        }

        private void resolveBodyTicksUpToCurrentTime()
        {
            bool hitBodyTick = !longNoteMode.RequiresBodyGaugeTicks() || headDrawable?.IsHit == true && isHolding;

            foreach (var bodyTick in bodyTickDrawables)
            {
                if (bodyTick.Judged || Time.Current < bodyTick.HitObject.StartTime)
                    continue;

                bodyTick.ApplyTickResult(hitBodyTick);
            }
        }

        private void resolveAllBodyTicks()
        {
            bool hitBodyTick = !longNoteMode.RequiresBodyGaugeTicks() || headDrawable?.IsHit == true && isHolding;

            foreach (var bodyTick in bodyTickDrawables)
            {
                if (bodyTick.Judged)
                    continue;

                bool hitThisTick = hitBodyTick && Time.Current >= bodyTick.HitObject.StartTime;
                bodyTick.ApplyTickResult(hitThisTick);
            }
        }
    }
}
