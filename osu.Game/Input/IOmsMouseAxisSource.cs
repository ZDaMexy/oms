// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Input
{
    /// <summary>A receipt-time observation from the existing host's relative mouse stream, in device counts.</summary>
    public readonly record struct OmsRelativeMouseMovement(float X, float Y, long Timestamp);

    public interface IOmsMouseAxisSource
    {
        /// <summary>Whether focus, high-precision mode and requested cursor capture are currently available.</summary>
        bool IsActive { get; }

        /// <summary>May run on the host input thread. Consumers enqueue values and never mutate drawables here.</summary>
        event Action<OmsRelativeMouseMovement>? RelativeMouseMoved;

        /// <summary>Invalidates pending relative input, for example when focus or capture is lost.</summary>
        event Action? InputReset;

        /// <summary>
        /// Requests temporary cursor capture while gameplay or an explicitly started device test owns the lease.
        /// Does not change the player's high-precision mouse setting. Dispose to return capture ownership.
        /// </summary>
        IDisposable AcquireCapture();
    }
}
