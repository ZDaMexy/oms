// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Screens.Play
{
    public abstract partial class Player
    {
        [Cached]
        private readonly GameplaySkinFailurePresentationController gameplaySkinFailure = new();

        private double? gameplaySkinFailureLastUpdateTime;

        private bool beginGameplaySkinFailure()
        {
            double duration = (DrawableRuleset as IGameplaySkinSceneRuntimeSource)?.GameplaySkinSceneRuntime
                ?.GetFailureOutroDuration(GameplayState.HasFailed) ?? 0;
            if (duration == 0)
                return false;

            gameplaySkinFailure.Begin(duration);
            gameplaySkinFailureLastUpdateTime = null;
            GameplayClockContainer.OnSeek += cancelGameplaySkinFailure;
            GameplayClockContainer.Stop();
            // Keep the frame-stable visual tree updating at the stopped chart time. Live input
            // remains suspended, including raw keyboard/HID queues; only the native outro clock moves.
            GameplayClockContainer.SoftUnpause();
            DrawableRuleset.SetGameplaySkinFailureInputSuspended(true);
            return true;
        }

        private void updateGameplaySkinFailure()
        {
            if (!gameplaySkinFailure.IsActive)
                return;

            if (!this.IsCurrentScreen() || !gameActive.Value)
            {
                gameplaySkinFailureLastUpdateTime = null;
                return;
            }

            double elapsed = gameplaySkinFailureLastUpdateTime is double previous ? Time.Current - previous : 0;
            gameplaySkinFailureLastUpdateTime = Time.Current;
            gameplaySkinFailure.Advance(elapsed, PauseOverlay.State.Value == Visibility.Visible);
            if (gameplaySkinFailure.Finished)
            {
                // Publish and draw the exact terminal frame before pausing the frame-stable tree.
                SchedulerAfterChildren.Add(() =>
                {
                    if (gameplaySkinFailure.Finished)
                        onFailComplete();
                });
            }
        }

        private void cancelGameplaySkinFailure()
        {
            gameplaySkinFailure.Cancel();
            gameplaySkinFailureLastUpdateTime = null;
            if (GameplayClockContainer != null)
                GameplayClockContainer.OnSeek -= cancelGameplaySkinFailure;
            DrawableRuleset?.SetGameplaySkinFailureInputSuspended(false);
        }
    }
}
