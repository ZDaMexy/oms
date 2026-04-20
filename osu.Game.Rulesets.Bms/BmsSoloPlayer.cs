// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using oms.Input;
using osu.Framework.Bindables;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Bms
{
    public partial class BmsSoloPlayer : SoloPlayer
    {
        protected virtual double PreStartDelay => 5000;

        protected virtual ScheduledDelegate SchedulePreStartDelay(Action onElapsed) => Scheduler.AddDelayed(onElapsed, PreStartDelay);

        private readonly BindableBool gameplayClockPaused = new BindableBool(true);
        private readonly BindableBool hiSpeedHoldPressed = new BindableBool();

        private DrawableBmsRuleset? drawableBmsRuleset;
        private BmsInputManager? gameplayInputManager;
        private BmsPreStartHiSpeedOverlay? hiSpeedOverlay;
        private ScheduledDelegate? startDelayDelegate;
        private bool startDelayElapsed;
        private bool gameplayStartPending;
        private bool suppressClockPausedHandler;

        protected BmsPreStartHiSpeedOverlay? PreStartHiSpeedOverlay => hiSpeedOverlay;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            gameplayClockPaused.BindTarget = GameplayClockContainer.IsPaused;
            gameplayClockPaused.BindValueChanged(_ => handleGameplayClockPausedState(), true);

            if (DrawableRuleset is not DrawableBmsRuleset bmsRuleset || bmsRuleset.GameplayInputManager is not BmsInputManager inputManager)
                return;

            drawableBmsRuleset = bmsRuleset;
            gameplayInputManager = inputManager;

            hiSpeedHoldPressed.BindTarget = inputManager.PreStartHoldPressed;
            hiSpeedHoldPressed.BindValueChanged(_ => updatePreStartAdjustmentState(), true);
            inputManager.Router.ActionPressed += handleGameplayActionPressed;

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
            startDelayElapsed = false;
            startDelayDelegate?.Cancel();
            startDelayDelegate = SchedulePreStartDelay(() =>
            {
                startDelayElapsed = true;
                attemptStartGameplay();
            });

            updatePreStartAdjustmentState();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            startDelayDelegate?.Cancel();
            startDelayDelegate = null;
            gameplayStartPending = false;

            if (gameplayInputManager != null)
                gameplayInputManager.Router.ActionPressed -= handleGameplayActionPressed;

            gameplayClockPaused.UnbindAll();
            hiSpeedHoldPressed.UnbindAll();
            drawableBmsRuleset?.SetAllowAdjustmentWhilePaused(false);
            return base.OnExiting(e);
        }

        private void handleGameplayActionPressed(OmsAction action)
        {
            if (drawableBmsRuleset == null || hiSpeedOverlay == null)
                return;

            if (!OmsBmsActionMap.TryMapToBmsAction(drawableBmsRuleset.Variant, action, out var bmsAction))
                return;

            hiSpeedOverlay.TryHandleActionPress(bmsAction);
        }

        private void updatePreStartAdjustmentState()
        {
            if (drawableBmsRuleset == null)
                return;

            bool adjustmentActive = gameplayStartPending && hiSpeedHoldPressed.Value;

            drawableBmsRuleset.SetAllowAdjustmentWhilePaused(adjustmentActive);

            if (adjustmentActive)
                hiSpeedOverlay?.Show();
            else
                hiSpeedOverlay?.Hide();

            if (!adjustmentActive)
                attemptStartGameplay();
        }

        private void handleGameplayClockPausedState()
        {
            if (suppressClockPausedHandler)
                return;

            if (gameplayClockPaused.Value || !gameplayStartPending)
                return;

            // The clock was unpaused externally (e.g. by WaitingOnFrames binding in base Player).
            // Always suppress it during pre-start so the clock doesn't run from an un-seeked position.
            GameplayClockContainer.Stop();

            attemptStartGameplay();
        }

        private void attemptStartGameplay()
        {
            if (!gameplayStartPending || !startDelayElapsed || hiSpeedHoldPressed.Value)
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
            hiSpeedOverlay?.Hide();
        }
    }
}
