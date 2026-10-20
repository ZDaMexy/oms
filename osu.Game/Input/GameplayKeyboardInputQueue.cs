// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Input.Bindings;
using osu.Game.Screens.Play;

namespace osu.Game.Input
{
    /// <summary>Keyboard action semantics over the shared receipt-time input queue.</summary>
    public sealed class GameplayKeyboardInputQueue : IDisposable
    {
        private readonly GameplayInputQueue<Transition> queue;

        public GameplayKeyboardInputQueue(Action<InputKey, bool> dispatch, Action releaseAppliedKeys)
        {
            queue = new GameplayInputQueue<Transition>(input => dispatch(input.Key, input.Pressed), releaseAppliedKeys);
        }

        public double? NextInputTime => queue.NextInputTime;

        public bool Enqueue(InputKey key, long timestamp, bool pressed) => queue.Enqueue(new Transition(key, pressed), timestamp);

        public void Reset() => queue.Reset();

        public void PrepareFrame(IGameplayInputTimeSource? timeSource, double currentTime, bool isLiveInput, double? observationTime = null)
            => queue.PrepareFrame(timeSource, currentTime, isLiveInput, observationTime);

        public void DispatchDue(double currentTime) => queue.DispatchDue(currentTime);

        public void Dispose() => queue.Dispose();

        private readonly record struct Transition(InputKey Key, bool Pressed);
    }
}
