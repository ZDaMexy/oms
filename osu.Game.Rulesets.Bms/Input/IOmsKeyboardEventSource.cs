// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Input.Bindings;

namespace osu.Game.Rulesets.Bms.Input
{
    public interface IOmsKeyboardEventSource
    {
        void RegisterSink(IOmsKeyboardEventSink sink);

        void UnregisterSink(IOmsKeyboardEventSink sink);
    }

    public interface IOmsKeyboardEventSink
    {
        bool HandleRawKeyPressed(InputKey key);

        bool HandleRawKeyReleased(InputKey key);

        void ResetRawKeyboardState();
    }
}
