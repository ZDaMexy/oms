// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using oms.Input;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Bms
{
    public partial class BmsSoloPlayer : SoloPlayer
    {
        private const double short_start_press_duration = 250;
        private const double double_start_press_interval = 350;

        protected virtual double PreStartDelay => 5000;

        protected virtual double AdjustmentGestureTime => Time.Current;

        protected virtual ScheduledDelegate SchedulePreStartDelay(Action onElapsed) => Scheduler.AddDelayed(onElapsed, PreStartDelay);

        private readonly BindableBool gameplayClockPaused = new BindableBool(true);
        private readonly BindableBool hiSpeedHoldPressed = new BindableBool();
        private readonly Bindable<Visibility> pauseOverlayVisibility = new Bindable<Visibility>();

        private DrawableBmsRuleset? drawableBmsRuleset;
        private BmsInputManager? gameplayInputManager;
        private BmsPreStartHiSpeedOverlay? hiSpeedOverlay;
        private ScheduledDelegate? startDelayDelegate;
        private bool startDelayElapsed;
        private bool gameplayStartPending;
        private bool suppressClockPausedHandler;
        private double startPressedAt = double.NaN;
        private double lastShortStartRelease = double.NegativeInfinity;
        private bool startGestureUsed;

        protected BmsPreStartHiSpeedOverlay? PreStartHiSpeedOverlay => hiSpeedOverlay;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!LoadedBeatmapSuccessfully)
                return;

            gameplayClockPaused.BindTarget = GameplayClockContainer.IsPaused;
            gameplayClockPaused.BindValueChanged(_ => handleGameplayClockPausedState(), true);
            pauseOverlayVisibility.BindTarget = PauseOverlay.State;
            pauseOverlayVisibility.BindValueChanged(e =>
            {
                if (e.NewValue == Visibility.Visible)
                    resetAdjustmentGesture();
                updatePreStartSpeedPreviewState();
            }, true);

            if (DrawableRuleset is not DrawableBmsRuleset bmsRuleset || bmsRuleset.GameplayInputManager is not BmsInputManager inputManager)
                return;

            drawableBmsRuleset = bmsRuleset;
            gameplayInputManager = inputManager;

            hiSpeedHoldPressed.BindTarget = inputManager.PreStartHoldPressed;
            hiSpeedHoldPressed.BindValueChanged(state =>
            {
                drawableBmsRuleset.SetAdjustmentHeld(state.NewValue);
                if (state.NewValue)
                    updatePreStartAdjustmentState(resetDelayOnPress: !state.OldValue);
                handleStartGesture(state.NewValue);
                if (!state.NewValue)
                    updatePreStartAdjustmentState(resetDelayOnReleaseAfterElapsed: state.OldValue);
            }, true);
            inputManager.Router.ActionPressed += handleGameplayActionPressed;
            inputManager.AdjustmentMotion += handleScratchAdjustment;
            inputManager.AdjustmentInputReset += resetAdjustmentGesture;

            drawableBmsRuleset.Overlays.Add(hiSpeedOverlay = new BmsPreStartHiSpeedOverlay(
                drawableBmsRuleset.Variant,
                drawableBmsRuleset.HiSpeedMode,
                drawableBmsRuleset.SelectedHiSpeed,
                drawableBmsRuleset.AdjustSelectedHiSpeed));
        }

        protected override void StartGameplay()
        {
            if (drawableBmsRuleset == null)
            {
                base.StartGameplay();
                return;
            }

            if (HasGameplaySkinPreparation)
            {
                gameplayStartPending = true;
                TryBeginGameplaySkinPreparation(preserveDelayOnAutomaticSkip: true);
                SetGameplaySkinPreparationHold(hiSpeedHoldPressed.Value);
                updatePreStartAdjustmentState();
                return;
            }

            // Stop the underlying gameplay clock and seek to StartTime so that:
            //  1) The DecouplingFramedClock no longer follows the source Track
            //     (which may still be playing from song select).
            //  2) base.StartGameplay() won't see IsRunning == true (avoids "Clock failure").
            GameplayClockContainer.Reset(startClock: false);

            // Mark isPaused = false WITHOUT scheduling StartGameplayClock.
            // This lets FrameStabilityContainer process children (rendering the playfield
            // and notes at their start positions) while the clock stays at StartTime.
            suppressClockPausedHandler = true;
            GameplayClockContainer.SoftUnpause();
            suppressClockPausedHandler = false;

            gameplayStartPending = true;
            restartPreStartDelay();

            updatePreStartAdjustmentState();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            startDelayDelegate?.Cancel();
            startDelayDelegate = null;
            gameplayStartPending = false;

            if (gameplayInputManager != null)
            {
                gameplayInputManager.Router.ActionPressed -= handleGameplayActionPressed;
                gameplayInputManager.AdjustmentMotion -= handleScratchAdjustment;
                gameplayInputManager.AdjustmentInputReset -= resetAdjustmentGesture;
            }

            gameplayClockPaused.UnbindAll();
            hiSpeedHoldPressed.UnbindAll();
            pauseOverlayVisibility.UnbindAll();
            drawableBmsRuleset?.SetAllowAdjustmentWhilePaused(false);
            drawableBmsRuleset?.SetAdjustmentHeld(false);
            drawableBmsRuleset?.SetPreStartSpeedPreviewState(false);
            return base.OnExiting(e);
        }

        private void handleGameplayActionPressed(OmsAction action)
        {
            if (drawableBmsRuleset == null || hiSpeedOverlay == null)
                return;

            if (hiSpeedHoldPressed.Value && action != OmsAction.UI_PreStartHold)
            {
                startGestureUsed = true;
                lastShortStartRelease = double.NegativeInfinity;
            }

            if (!OmsBmsActionMap.TryMapToBmsAction(drawableBmsRuleset.Variant, action, out var bmsAction))
                return;

            if (tryHandleHoldAdjustmentAction(bmsAction))
                return;

            hiSpeedOverlay.TryHandleActionPress(bmsAction);
        }

        private void updatePreStartAdjustmentState(bool resetDelayOnPress = false, bool resetDelayOnReleaseAfterElapsed = false)
        {
            if (drawableBmsRuleset == null)
                return;

            bool adjustmentActive = gameplayStartPending && hiSpeedHoldPressed.Value;

            if (GameplaySkinPreparationActive)
                SetGameplaySkinPreparationHold(hiSpeedHoldPressed.Value);

            if (!HasGameplaySkinPreparation && resetDelayOnPress && adjustmentActive)
                restartPreStartDelay();

            drawableBmsRuleset.SetAllowAdjustmentWhilePaused(adjustmentActive);

            if (adjustmentActive)
                hiSpeedOverlay?.Show();
            else
                hiSpeedOverlay?.Hide();

            updatePreStartSpeedPreviewState();

            if (!adjustmentActive)
            {
                if (!HasGameplaySkinPreparation && resetDelayOnReleaseAfterElapsed && gameplayStartPending && startDelayElapsed)
                {
                    restartPreStartDelay();
                    return;
                }

                attemptStartGameplay();
            }
        }

        private bool tryHandleHoldAdjustmentAction(BmsAction action)
        {
            if (drawableBmsRuleset == null || !hiSpeedHoldPressed.Value)
                return false;

            int direction = action.GetHiSpeedAdjustmentDirection(drawableBmsRuleset.Variant);

            if (direction == 0)
                return false;

            return drawableBmsRuleset.AdjustSelectedHiSpeed(direction);
        }

        private void handleScratchAdjustment(OmsAxisMotion motion)
        {
            if (drawableBmsRuleset == null || !hiSpeedHoldPressed.Value)
                return;

            startGestureUsed = true;
            lastShortStartRelease = double.NegativeInfinity;
            drawableBmsRuleset.AdjustFromScratch(motion);
        }

        private void handleStartGesture(bool pressed)
        {
            double now = AdjustmentGestureTime;
            if (pressed)
            {
                startPressedAt = now;
                startGestureUsed = false;
                if (now >= lastShortStartRelease && now - lastShortStartRelease <= double_start_press_interval)
                {
                    drawableBmsRuleset?.ToggleGameplayLaneCovers();
                    startGestureUsed = true;
                    lastShortStartRelease = double.NegativeInfinity;
                }
                return;
            }

            bool shortPress = !startGestureUsed && now >= startPressedAt && now - startPressedAt <= short_start_press_duration;
            startPressedAt = double.NaN;
            if (!shortPress)
            {
                lastShortStartRelease = double.NegativeInfinity;
                return;
            }

            lastShortStartRelease = now;
        }

        private void resetAdjustmentGesture()
        {
            startPressedAt = double.NaN;
            lastShortStartRelease = double.NegativeInfinity;
            startGestureUsed = true;
            drawableBmsRuleset?.SetAdjustmentHeld(false);
        }

        private void restartPreStartDelay()
        {
            if (!gameplayStartPending)
                return;

            startDelayElapsed = false;
            startDelayDelegate?.Cancel();
            startDelayDelegate = SchedulePreStartDelay(() =>
            {
                startDelayElapsed = true;
                attemptStartGameplay();
            });
        }

        private void updatePreStartSpeedPreviewState()
        {
            drawableBmsRuleset?.SetPreStartSpeedPreviewState(
                gameplayStartPending && hiSpeedHoldPressed.Value,
                gameplayStartPending && hiSpeedHoldPressed.Value && pauseOverlayVisibility.Value == Visibility.Visible);
        }

        private void handleGameplayClockPausedState()
        {
            if (HasGameplaySkinPreparation)
                return;

            if (suppressClockPausedHandler)
                return;

            if (gameplayClockPaused.Value || !gameplayStartPending)
                return;

            // The clock was unpaused externally (e.g. by WaitingOnFrames binding in base Player).
            // Always suppress it during pre-start so the clock doesn't run from an un-seeked position.
            GameplayClockContainer.Stop();

            if (!startDelayElapsed || hiSpeedHoldPressed.Value)
            {
                SchedulerAfterChildren.Add(() =>
                {
                    if (!gameplayStartPending)
                        return;

                    suppressClockPausedHandler = true;
                    GameplayClockContainer.SoftUnpause();
                    suppressClockPausedHandler = false;
                });
                return;
            }

            attemptStartGameplay();
        }

        private void attemptStartGameplay()
        {
            if (HasGameplaySkinPreparation)
                return;

            if (!gameplayStartPending || !startDelayElapsed || hiSpeedHoldPressed.Value || PauseOverlay.State.Value == Visibility.Visible)
                return;

            completePendingGameplayStart();

            // Re-pause so that base.StartGameplay() → Reset(startClock: true) → Start()
            // correctly transitions isPaused from true to false and schedules StartGameplayClock.
            suppressClockPausedHandler = true;
            GameplayClockContainer.Stop();
            suppressClockPausedHandler = false;

            base.StartGameplay();
        }

        private void completePendingGameplayStart()
        {
            gameplayStartPending = false;
            startDelayDelegate?.Cancel();
            startDelayDelegate = null;
            drawableBmsRuleset?.SetAllowAdjustmentWhilePaused(false);
            updatePreStartSpeedPreviewState();
            hiSpeedOverlay?.Hide();
        }

        protected override void OnGameplaySkinPreparationCompleted()
        {
            completePendingGameplayStart();
            base.OnGameplaySkinPreparationCompleted();
        }
    }
}
