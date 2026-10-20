// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using oms.Input;

namespace osu.Game.Rulesets.Bms.Input
{
    /// <summary>
    /// Keeps movement continuity separate from the ordinary scratch attack pulses.
    /// Its two level actions are recorded by the same input/replay path as the lane actions.
    /// </summary>
    internal sealed class BmsScratchMotionTracker
    {
        private readonly Action<BmsAction> press;
        private readonly Action<BmsAction> release;
        private readonly Dictionary<(OmsAction Action, string Source), Movement> movements = new();
        private readonly List<(OmsAction Action, string Source)> expired = new();
        private readonly int[] references = new int[2];

        public BmsScratchMotionTracker(Action<BmsAction> press, Action<BmsAction> release)
        {
            this.press = press;
            this.release = release;
        }

        public double? NextExpiry
        {
            get
            {
                double next = double.PositiveInfinity;
                foreach (Movement movement in movements.Values)
                    next = Math.Min(next, movement.Expires);
                return double.IsPositiveInfinity(next) ? null : next;
            }
        }

        public void Observe(OmsAxisMotion motion, double time)
        {
            int side = motion.Action switch
            {
                OmsAction.Key1P_Scratch => 0,
                OmsAction.Key2P_Scratch => 1,
                _ => -1,
            };
            if (side < 0)
                return;

            var key = (motion.Action, motion.SourceId);
            if (!movements.ContainsKey(key) && references[side]++ == 0)
                press(actionFor(side));
            movements[key] = new Movement(side, motion.DeviceIdentifier, time + motion.StopTimeoutMilliseconds);
        }

        public void Update(double time, double? reachedExpiry = null)
        {
            // The owner resets clock epochs explicitly. A new QPC anchor can round the
            // inverse projection slightly backwards without any actual rewind.
            double expiryCutoff = reachedExpiry.HasValue ? Math.Max(time, reachedExpiry.Value) : time;
            expired.Clear();
            foreach (var pair in movements)
            {
                if (pair.Value.Expires <= expiryCutoff)
                    expired.Add(pair.Key);
            }
            foreach (var key in expired)
                remove(key);
        }

        public void ReleaseDevice(string? identifier)
        {
            expired.Clear();
            foreach (var pair in movements)
            {
                if (pair.Value.Device == identifier)
                    expired.Add(pair.Key);
            }
            foreach (var key in expired)
                remove(key);
        }

        public void Clear()
        {
            movements.Clear();
            expired.Clear();
            for (int side = 0; side < references.Length; side++)
            {
                if (references[side] == 0)
                    continue;
                references[side] = 0;
                release(actionFor(side));
            }
        }

        private void remove((OmsAction Action, string Source) key)
        {
            Movement movement = movements[key];
            movements.Remove(key);
            if (--references[movement.Side] == 0)
                release(actionFor(movement.Side));
        }

        private static BmsAction actionFor(int side) => side == 0 ? BmsAction.Scratch1Motion : BmsAction.Scratch2Motion;

        private readonly record struct Movement(int Side, string? Device, double Expires);
    }
}
