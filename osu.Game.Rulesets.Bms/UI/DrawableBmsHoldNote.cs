// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
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
        private BmsLongNoteMode? longNoteModeOverrideForTesting;

        private readonly Bindable<BmsLongNoteBodyState> bodyState = new Bindable<BmsLongNoteBodyState>();

        /// <summary>
        /// Live visual state of the long-note body, consumed by skins (e.g. the default body display). Derived
        /// purely from the head/tail judgement and hold state, so an HCN re-grab flips it back to
        /// <see cref="BmsLongNoteBodyState.Holding"/> without any special casing.
        /// </summary>
        public IBindable<BmsLongNoteBodyState> BodyState => bodyState;

        [Resolved(CanBeNull = true)]
        private DrawableBmsRuleset? drawableRuleset { get; set; }

        private BmsHoldNote holdNote => (BmsHoldNote)HitObject;

        private BmsLongNoteMode longNoteMode => longNoteModeOverrideForTesting ?? drawableRuleset?.LongNoteMode ?? BmsScoreProcessor.DEFAULT_LONG_NOTE_MODE;

        public DrawableBmsHoldNote(BmsHoldNote hitObject, BmsGameplayLayoutSnapshot? gameplayLayoutSnapshot = null)
            : base(hitObject, gameplayLayoutSnapshot)
        {
        }

        public override void PlaySamples()
        {
        }

        protected override void OnApply()
        {
            base.OnApply();

            // Reset transient hold state so a pooled reuse never inherits the previous note's broken/holding look.
            IsHoldingForTesting = false;
            bodyState.Value = BmsLongNoteBodyState.Idle;
        }

        protected override void Update()
        {
            base.Update();
            bodyState.Value = computeBodyState();
        }

        private BmsLongNoteBodyState computeBodyState()
        {
            if (IsHoldingForTesting)
                return BmsLongNoteBodyState.Holding;

            // Head still in flight (or not yet created) — the note is approaching and has not been activated.
            if (headDrawable?.Judged != true)
                return BmsLongNoteBodyState.Idle;

            // A clean hold held through to the tail stays "activated" while it fades out, rather than flashing broken.
            if (headDrawable.IsHit && tailDrawable?.IsHit == true)
                return BmsLongNoteBodyState.Holding;

            // Head missed, or the hold was released before completion.
            return BmsLongNoteBodyState.Broken;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (headDrawable == null || tailDrawable == null)
                return;

            if (holdNote.AutoPlay)
            {
                if (!headDrawable.Judged && Time.Current >= holdNote.StartTime)
                {
                    IsHoldingForTesting = true;
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
                if (IsHoldingForTesting && HasReachedHoldTail(holdNote, Time.Current))
                    resolveTail(HitResult.Perfect);
                else if (HasMissedTailReleaseWindow(holdNote, Time.Current))
                    resolveTail(HitResult.Miss);
                else if (!IsHoldingForTesting && HasReachedHoldTail(holdNote, Time.Current))
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
            if (e.Action != Action.Value || headDrawable == null || tailDrawable == null || !IsHoldingForTesting || tailDrawable.Judged)
                return;

            if (!headDrawable.IsHit && !longNoteMode.RequiresTailJudgement())
                return;

            resolveBodyTicksUpToCurrentTime();

            var releaseResult = ResultForTailRelease(holdNote, Time.Current);

            IsHoldingForTesting = false;

            // HCN is the only mode that may be re-grabbed: a non-hit early release leaves the tail unjudged so a
            // later press can resume the hold. LN and CN treat any premature release as terminal — resolve the tail
            // immediately (a miss unless released within the tail window) so the note cannot be re-grabbed.
            if (longNoteMode.AllowsRegrabAfterRelease())
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
                IsHoldingForTesting = true;
                OnUserPressedSuccessfully?.Invoke(this);
            }
            else
            {
                // A pressed POOR/miss on the head never reaches the Hit state, so its keysound would otherwise be
                // silent. Sound it on key-down to match a regular note's pressed-POOR behaviour (see
                // DrawableBmsHitObject.OnPressed). A hit head still plays through its own PlaySamples, so no double-up.
                headDrawable.PlayKeysoundFromPress();
            }

            return true;
        }

        internal bool TryApplyLateBodyPress()
        {
            if (headDrawable == null || tailDrawable == null || !CanApplyLateBodyPress(longNoteMode, holdNote, tailDrawable.Judged, Time.Current))
                return false;

            if (!headDrawable.Judged)
                headDrawable.MissForcefully();

            IsHoldingForTesting = true;
            return true;
        }

        internal bool IsHoldingForTesting { get; private set; }

        internal BmsLongNoteBodyState ComputeBodyStateForTesting() => computeBodyState();

        internal BmsLongNoteMode LongNoteModeOverrideForTesting
        {
            set
            {
                longNoteModeOverrideForTesting = value;
                applyTestingLongNoteMode(value);
            }
        }

        internal static bool HasMissedHoldStartWindow(BmsHoldNote holdNote, double currentTime)
            => holdNote.Head?.HitWindows != null && currentTime - holdNote.StartTime > holdNote.Head.HitWindows.WindowFor(HitResult.Miss) + BmsJudgementSystem.BoundaryEpsilon;

        internal static bool HasReachedHoldTail(BmsHoldNote holdNote, double currentTime)
            => currentTime >= holdNote.EndTime;

        internal static bool CanApplyLateBodyPress(BmsLongNoteMode longNoteMode, BmsHoldNote holdNote, bool tailJudged, double currentTime)
            => longNoteMode.AllowsRegrabAfterRelease()
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

            return currentTime - holdNote.EndTime > missWindow + BmsJudgementSystem.BoundaryEpsilon;
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
                return headDrawable = new DrawableBmsHoldNoteHead(head, GameplayLayoutSnapshot);

            if (hitObject is BmsHoldNoteTailEvent tailEvent)
                return tailDrawable = new DrawableBmsHoldNoteTail(tailEvent, GameplayLayoutSnapshot);

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
            IsHoldingForTesting = false;

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
            bool hitBodyTick = !longNoteMode.RequiresBodyGaugeTicks() || headDrawable?.IsHit == true && IsHoldingForTesting;
            double currentTime = Time.Current;

            foreach (var bodyTick in bodyTickDrawables)
            {
                if (bodyTick.Judged)
                    continue;

                if (currentTime < bodyTick.HitObject.StartTime)
                    break;

                bodyTick.ApplyTickResult(hitBodyTick);
            }
        }

        private void resolveAllBodyTicks()
        {
            bool hitBodyTick = !longNoteMode.RequiresBodyGaugeTicks() || headDrawable?.IsHit == true && IsHoldingForTesting;

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
