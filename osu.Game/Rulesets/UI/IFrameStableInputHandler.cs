// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// Optional live input which needs the normal frame-stable gameplay subtree to visit its event times.
    /// Preparing a batch does not dispatch input. The input manager dispatches due events during its normal update,
    /// before hit objects update, so judgement and replay recording observe the same clock.
    /// </summary>
    public interface IFrameStableInputHandler
    {
        /// <summary>Freezes one producer batch per outer frame, rather than consuming newly arriving input during catch-up.</summary>
        void PrepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput);

        /// <summary>Invalidates queued live input when ownership changes, even if it changes twice between outer frames.</summary>
        void ResetInput();

        /// <summary>The next frozen input time, or <c>null</c> when no clock stop is required.</summary>
        double? NextInputTime { get; }
    }
}
