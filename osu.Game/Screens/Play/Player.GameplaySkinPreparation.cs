// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Screens.Play
{
    public abstract partial class Player
    {
        [Cached]
        private readonly GameplaySkinPreparationController gameplaySkinPreparation = new();

        private readonly Bindable<bool> gameplaySkinPreparationClockPaused = new(true);
        private bool suppressGameplaySkinPreparationClockGuard;

        /// <summary>Only the selected, prepared author's explicit declaration enables this path.</summary>
        protected bool HasGameplaySkinPreparation => gameplaySkinPreparation.IsConfigured;

        protected bool GameplaySkinPreparationActive => gameplaySkinPreparation.State.Active;

        private void configureGameplaySkinPreparation()
        {
            double duration = (DrawableRuleset as IGameplaySkinSceneRuntimeSource)?.GameplaySkinSceneRuntime?.PreparedScene.Program.PreparationDuration ?? 0;
            gameplaySkinPreparation.Configure(duration);

            if (!HasGameplaySkinPreparation)
                return;

            gameplaySkinPreparationClockPaused.BindTarget = GameplayClockContainer.IsPaused;
            gameplaySkinPreparationClockPaused.BindValueChanged(paused =>
            {
                if (!paused.NewValue && GameplaySkinPreparationActive && !suppressGameplaySkinPreparationClockGuard)
                    suppressPrematureGameplaySkinClockStart();
            });
            GameplayClockContainer.OnSeek += handleGameplaySkinPreparationSeek;
        }

        /// <summary>
        /// BMS retains its existing preparation delay even when its normal intro is automatically skipped.
        /// Other players may skip this optional decorative phase on a quick restart.
        /// </summary>
        protected bool TryBeginGameplaySkinPreparation(bool preserveDelayOnAutomaticSkip = false)
        {
            if (!HasGameplaySkinPreparation || (!preserveDelayOnAutomaticSkip && Configuration.AutomaticallySkipIntro))
                return false;

            // Reset before publishing Active so the reset's own seek is not a user skip.
            // SoftUnpause renders the frame-stable tree while the source audio remains stopped.
            GameplayClockContainer.Stop();
            GameplayClockContainer.Reset(startClock: false);
            gameplaySkinPreparation.Begin();
            softUnpauseGameplaySkinPreparation();
            return true;
        }

        protected void SetGameplaySkinPreparationHold(bool held) => gameplaySkinPreparation.SetHold(held);

        /// <summary>Ruleset-specific pre-start overlays relinquish their ownership before actual clock start.</summary>
        protected virtual void OnGameplaySkinPreparationCompleted()
        {
        }

        protected override void Update()
        {
            base.Update();

            if (!GameplaySkinPreparationActive || !this.IsCurrentScreen())
                return;

            if (!gameplaySkinPreparation.Advance(Time.Elapsed, PauseOverlay.State.Value == Visibility.Visible))
                return;

            gameplaySkinPreparation.Complete();
            OnGameplaySkinPreparationCompleted();

            // SoftUnpause deliberately left IsPaused=false. Restore the real transition
            // so Start() schedules exactly one source-clock start after the initial frame.
            GameplayClockContainer.Stop();
            startGameplayImmediately();

            if (DrawableRuleset.FrameStableClock.WaitingOnFrames.Value)
                GameplayClockContainer.Stop();
        }

        private void softUnpauseGameplaySkinPreparation()
        {
            suppressGameplaySkinPreparationClockGuard = true;
            GameplayClockContainer.SoftUnpause();
            suppressGameplaySkinPreparationClockGuard = false;
        }

        private void suppressPrematureGameplaySkinClockStart()
        {
            GameplayClockContainer.Stop();

            // Start() queues its underlying source-clock start after its own children.
            // Keep IsPaused=true through that callback. The Player's later scheduler
            // restores rendering only after the queued start has observed the barrier.
            SchedulerAfterChildren.Add(() =>
            {
                if (GameplaySkinPreparationActive && PauseOverlay.State.Value != Visibility.Visible)
                    softUnpauseGameplaySkinPreparation();
            });
        }

        private void handleGameplaySkinPreparationSeek()
        {
            if (!GameplaySkinPreparationActive || gameplaySkinPreparation.IsHeld)
                return;

            // The seek already selected its authoritative destination. Do not Reset()
            // back to StartTime when a user skips an intro or seeks an observed replay.
            // Revoke an interrupted visual phase, rather than freezing a three-second
            // lane opening forever at the one-second position where the skip occurred.
            gameplaySkinPreparation.Cancel();
            OnGameplaySkinPreparationCompleted();
            GameplayClockContainer.Stop();

            if (PauseOverlay.State.Value != Visibility.Visible && !DrawableRuleset.FrameStableClock.WaitingOnFrames.Value)
                GameplayClockContainer.Start();
        }

        private void cancelGameplaySkinPreparation()
        {
            gameplaySkinPreparation.Cancel();
            gameplaySkinPreparationClockPaused.UnbindAll();

            if (GameplayClockContainer != null)
                GameplayClockContainer.OnSeek -= handleGameplaySkinPreparationSeek;
        }
    }
}
