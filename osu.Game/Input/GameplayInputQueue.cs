// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using osu.Game.Screens.Play;

namespace osu.Game.Input
{
    /// <summary>Freezes receipt-time input once per outer frame and applies it only on the matching gameplay clock stop.</summary>
    public sealed class GameplayInputQueue<T> : IDisposable where T : struct
    {
        private readonly Action<T> dispatch;
        private readonly Action releaseAppliedInput;
        private readonly object gate = new object();
        private readonly Queue<RawInput> raw = new Queue<RawInput>(64);
        private readonly PriorityQueue<TimedInput, (double Time, long Sequence)> timed = new PriorityQueue<TimedInput, (double, long)>(64);
        private GameplayInputTime? previousSample;
        private long? clockEpoch;
        private int epoch;
        private int preparedEpoch;
        private bool releaseOnUpdate;
        private bool closed;
        private bool hasPreparedFrame;
        private long sequence;

        public GameplayInputQueue(Action<T> dispatch, Action releaseAppliedInput)
        {
            this.dispatch = dispatch;
            this.releaseAppliedInput = releaseAppliedInput;
        }

        public double? NextInputTime => preparedEpoch == Volatile.Read(ref epoch) && timed.TryPeek(out TimedInput input, out _) ? input.Time : null;

        public bool Enqueue(T value, long timestamp)
        {
            lock (gate)
            {
                if (closed)
                    return false;
                raw.Enqueue(new RawInput(value, timestamp, Sequence: sequence++));
                return true;
            }
        }

        /// <summary>Queues a polled device observation without claiming a hardware contact timestamp.</summary>
        public bool EnqueueObserved(T value, double observationTime, long? captureClockEpoch = null)
        {
            lock (gate)
            {
                if (closed)
                    return false;
                raw.Enqueue(new RawInput(value, 0, observationTime, captureClockEpoch ?? clockEpoch, sequence++));
                return true;
            }
        }

        public void Reset()
        {
            lock (gate)
            {
                if (closed)
                    return;
                epoch++;
                raw.Clear();
                raw.Enqueue(new RawInput(null, 0));
            }
        }

        public void PrepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput, double? observationTime = null)
        {
            GameplayInputTime? sample = timeSource?.InputTime;
            lock (gate)
            {
                hasPreparedFrame = true;
                if (!isLiveInput)
                {
                    raw.Clear();
                    timed.Clear();
                    preparedEpoch = epoch;
                    previousSample = null;
                    releaseOnUpdate = true;
                    return;
                }

                bool clockChanged = sample is GameplayInputTime next && clockEpoch is long previousEpoch && previousEpoch != next.Epoch;
                if (preparedEpoch != epoch || clockChanged)
                {
                    timed.Clear();
                    previousSample = null;
                    releaseOnUpdate = true;
                }
                preparedEpoch = epoch;
                if (sample is GameplayInputTime captured)
                {
                    clockEpoch = captured.Epoch;
                    if (captured.Time < currentTime)
                    {
                        previousSample = captured;
                        return;
                    }
                }

                int count = raw.Count;
                while (count-- > 0 && raw.TryDequeue(out RawInput input))
                {
                    if (input.Value == null)
                    {
                        releaseOnUpdate = true;
                        continue;
                    }
                    if (input.ObservationTime == null && sample is GameplayInputTime boundary && input.Timestamp > boundary.Timestamp)
                    {
                        raw.Enqueue(input);
                        continue;
                    }
                    if (input.ObservationTime != null && sample is GameplayInputTime observedEpoch
                        && input.CaptureClockEpoch.HasValue && input.CaptureClockEpoch.Value != observedEpoch.Epoch)
                        continue;
                    if (input.ObservationTime == null && sample is GameplayInputTime active && input.Timestamp < active.EpochStartTimestamp)
                        continue;
                    double mapped = input.ObservationTime ?? (sample is GameplayInputTime now ? map(input.Timestamp, now) : observationTime ?? currentTime);
                    double deliveryTime = Math.Max(currentTime, mapped);
                    timed.Enqueue(new TimedInput(input.Value.Value, deliveryTime), (deliveryTime, input.Sequence));
                }
                previousSample = sample;
            }
        }

        public void DispatchDue(double currentTime)
        {
            if (preparedEpoch != Volatile.Read(ref epoch))
            {
                timed.Clear();
                releaseOnUpdate = true;
            }
            if (releaseOnUpdate)
            {
                releaseOnUpdate = false;
                releaseAppliedInput();
            }

            while (timed.TryPeek(out TimedInput input, out _) && input.Time <= currentTime)
            {
                if (preparedEpoch != Volatile.Read(ref epoch))
                    break;
                timed.Dequeue();
                dispatch(input.Value);
            }

            if (!hasPreparedFrame)
            {
                // Isolated handler previews have no outer frame owner.
                int count;
                lock (gate)
                {
                    count = raw.Count;
                    preparedEpoch = epoch;
                }
                while (count-- > 0)
                {
                    RawInput input;
                    lock (gate)
                    {
                        if (preparedEpoch != epoch || !raw.TryDequeue(out input))
                            break;
                    }
                    if (input.Value is T value)
                        dispatch(value);
                    else
                        releaseAppliedInput();
                }
            }
        }

        private double map(long timestamp, GameplayInputTime sample)
        {
            if (previousSample is GameplayInputTime previous && previous.Epoch == sample.Epoch
                && previous.Timestamp < sample.Timestamp && timestamp >= previous.Timestamp
                && previous.IsRunning && sample.IsRunning && sample.Time >= previous.Time)
            {
                double fraction = (double)(timestamp - previous.Timestamp) / (sample.Timestamp - previous.Timestamp);
                return previous.Time + (sample.Time - previous.Time) * fraction;
            }
            return sample.IsRunning ? sample.Time + (timestamp - sample.Timestamp) * (1000.0 / Stopwatch.Frequency) * sample.Rate : sample.Time;
        }

        public void Dispose()
        {
            lock (gate)
            {
                closed = true;
                raw.Clear();
                timed.Clear();
            }
        }

        private readonly record struct RawInput(T? Value, long Timestamp, double? ObservationTime = null, long? CaptureClockEpoch = null, long Sequence = 0);
        private readonly record struct TimedInput(T Value, double Time);
    }
}
