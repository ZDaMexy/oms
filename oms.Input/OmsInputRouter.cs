// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;

namespace oms.Input
{
    /// <summary>
    /// Routes all input signals (keyboard, HID, XInput, mouse axis) to <see cref="OmsAction"/> events.
    /// </summary>
    public class OmsInputRouter
    {
        private readonly Dictionary<OmsAction, int> pressedActionCounts = new Dictionary<OmsAction, int>();

        public event Action<OmsAction>? ActionPressed;

        public event Action<OmsAction>? ActionReleased;

        public bool IsPressed(OmsAction action) => pressedActionCounts.ContainsKey(action);

        public bool TriggerPressed(OmsAction action)
        {
            int count = pressedActionCounts.GetValueOrDefault(action);
            pressedActionCounts[action] = count + 1;

            if (count > 0)
                return false;

            ActionPressed?.Invoke(action);
            return true;
        }

        public bool TriggerReleased(OmsAction action)
        {
            if (!pressedActionCounts.TryGetValue(action, out int count) || count <= 0)
                return false;

            if (count > 1)
            {
                pressedActionCounts[action] = count - 1;
                return false;
            }

            pressedActionCounts.Remove(action);

            ActionReleased?.Invoke(action);
            return true;
        }
    }
}
