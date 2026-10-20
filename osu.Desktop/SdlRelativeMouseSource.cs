// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Platform;
using osu.Game.Input;
using osuTK;

namespace osu.Desktop
{
    /// <summary>Observes SDL's existing relative stream without registering another native input window.</summary>
    internal partial class SdlRelativeMouseSource : Component, IOmsMouseAxisSource
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private IWindow? window;
        private MouseHandler? mouse;
        private IBindable<bool>? active;
        private Bindable<ConfineMouseMode>? confineMode;
        private Bindable<WindowMode>? windowMode;
        private int requests;
        private bool captured;
        private CursorState addedCursorState;
        private bool closed;
        private Action updateCapture = null!;
        private Func<bool>? getRelativeMouseMode;
        private Action<Action<Vector2>>? removeRelativeMouseObserver;
        private Action<Vector2> relativeMouseObserver = null!;

        public bool IsActive => Volatile.Read(ref requests) > 0 && active?.Value == true
                                && mouse?.UseRelativeMode.Value == true && getRelativeMouseMode?.Invoke() == true;

        public event Action<OmsRelativeMouseMovement>? RelativeMouseMoved;
        public event Action? InputReset;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config)
        {
            window = host.Window;
            mouse = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault() ?? throw new InvalidOperationException("The existing framework MouseHandler is required for high-precision mouse input.");

            // ppy.osu.Framework 2026.303.0 exposes this existing SDL stream through an internal
            // interface. Bind its exact verified contract once; no reflection runs per input event.
            Type? sdlContract = typeof(GameHost).Assembly.GetType("osu.Framework.Platform.ISDLWindow");
            var relativeEvent = sdlContract?.GetEvent("MouseMoveRelative");
            var relativeGetter = sdlContract?.GetProperty("RelativeMouseMode")?.GetMethod;
            if (sdlContract == null || !sdlContract.IsInstanceOfType(window) || relativeEvent == null || relativeEvent.EventHandlerType != typeof(Action<Vector2>)
                || relativeEvent.AddMethod == null || relativeEvent.RemoveMethod == null
                || relativeGetter == null || relativeGetter.ReturnType != typeof(bool) || relativeGetter.GetParameters().Length != 0)
            {
                throw new InvalidOperationException("The pinned osu!framework SDL relative mouse input contract is unavailable. Expected ISDLWindow.MouseMoveRelative and RelativeMouseMode from ppy.osu.Framework 2026.303.0.");
            }

            relativeMouseObserver = onRelativeMove;
            getRelativeMouseMode = relativeGetter.CreateDelegate<Func<bool>>(window);
            var addRelativeMouseObserver = relativeEvent.AddMethod.CreateDelegate<Action<Action<Vector2>>>(window);
            removeRelativeMouseObserver = relativeEvent.RemoveMethod.CreateDelegate<Action<Action<Vector2>>>(window);

            updateCapture = applyCapture;
            confineMode = config.GetBindable<ConfineMouseMode>(FrameworkSetting.ConfineMouseMode).GetBoundCopy();
            windowMode = config.GetBindable<WindowMode>(FrameworkSetting.WindowMode).GetBoundCopy();
            confineMode.BindValueChanged(_ => scheduleCapture());
            windowMode.BindValueChanged(_ => scheduleCapture());
            active = host.IsActive.GetBoundCopy();
            active.BindValueChanged(_ => captureConditionsChanged());
            mouse.UseRelativeMode.ValueChanged += onRelativeModeChanged;
            addRelativeMouseObserver(relativeMouseObserver);
            scheduleCapture();
        }

        public IDisposable AcquireCapture()
        {
            ObjectDisposedException.ThrowIf(closed, this);
            Interlocked.Increment(ref requests);
            scheduleCapture();
            return new CaptureLease(this);
        }

        private void releaseCapture()
        {
            int previous;
            do
            {
                previous = Volatile.Read(ref requests);
                if (previous == 0)
                    return;
            } while (Interlocked.CompareExchange(ref requests, previous - 1, previous) != previous);
            scheduleCapture();
            if (Volatile.Read(ref requests) == 0)
                InputReset?.Invoke();
        }

        private void onRelativeModeChanged(ValueChangedEvent<bool> _) => captureConditionsChanged();

        private void captureConditionsChanged()
        {
            if (!IsActive)
                InputReset?.Invoke();
            scheduleCapture();
        }

        private void scheduleCapture()
        {
            if (updateCapture != null)
                host.InputThread.Scheduler.Add(updateCapture);
        }

        private void applyCapture()
        {
            if (window == null)
                return;
            bool shouldCapture = Volatile.Read(ref requests) > 0 && active?.Value == true && mouse?.UseRelativeMode.Value == true;
            if (shouldCapture)
            {
                if (!captured)
                    addedCursorState = (CursorState.Hidden | CursorState.Confined) & ~window.CursorState;
                window.CursorState |= CursorState.Hidden | CursorState.Confined;
            }
            else if (captured)
            {
                CursorState restored = window.CursorState & ~addedCursorState;
                bool userConfined = confineMode?.Value == ConfineMouseMode.Always
                                    || confineMode?.Value == ConfineMouseMode.Fullscreen && windowMode?.Value != WindowMode.Windowed;
                if (userConfined)
                    restored |= CursorState.Confined;
                else
                    restored &= ~CursorState.Confined;
                window.CursorState = restored;
            }
            captured = shouldCapture;
        }

        private void onRelativeMove(Vector2 delta)
        {
            if (!IsActive)
                return;
            float scale = host.Window.Scale;
            RelativeMouseMoved?.Invoke(new OmsRelativeMouseMovement(delta.X / scale, delta.Y / scale, Stopwatch.GetTimestamp()));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                closed = true;
                removeRelativeMouseObserver?.Invoke(relativeMouseObserver);
                if (mouse != null)
                    mouse.UseRelativeMode.ValueChanged -= onRelativeModeChanged;
                Interlocked.Exchange(ref requests, 0);
                scheduleCapture();
                InputReset?.Invoke();
                active?.UnbindAll();
                confineMode?.UnbindAll();
                windowMode?.UnbindAll();
            }
            base.Dispose(isDisposing);
        }

        private sealed class CaptureLease : IDisposable
        {
            private SdlRelativeMouseSource? owner;

            public CaptureLease(SdlRelativeMouseSource owner) => this.owner = owner;

            public void Dispose() => Interlocked.Exchange(ref owner, null)?.releaseCapture();
        }
    }
}
