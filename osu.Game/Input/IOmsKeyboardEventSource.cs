// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Input.Bindings;

namespace osu.Game.Input
{
    public interface IOmsKeyboardEventSource
    {
        void RegisterSink(IOmsKeyboardEventSink sink);

        void UnregisterSink(IOmsKeyboardEventSink sink);

        /// <summary>Whether the native listener is available and owns this exact gameplay sink.</summary>
        bool IsActiveFor(IOmsKeyboardEventSink sink);

        /// <summary>Whether this source can provide a binding key, including virtual modifier families; fixed for this source's lifetime.</summary>
        bool SupportsKey(InputKey key);
    }

    public interface IOmsKeyboardEventSink
    {
        /// <summary>
        /// Queues a bound key transition with its <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> receipt timestamp.
        /// No gameplay runs on the caller's thread.
        /// </summary>
        bool HandleRawKeyPressed(InputKey key, long timestamp);

        /// <inheritdoc cref="HandleRawKeyPressed"/>
        bool HandleRawKeyReleased(InputKey key, long timestamp);

        /// <summary>Discards pending transitions and queues release of this sink's applied keyboard state.</summary>
        void ResetRawKeyboardState();
    }
}
