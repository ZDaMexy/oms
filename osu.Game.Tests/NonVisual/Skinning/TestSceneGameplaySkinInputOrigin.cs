// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [HeadlessTest]
    public partial class TestSceneGameplaySkinInputOrigin : OsuTestScene
    {
        [Test]
        public void TestNativeInputOriginsSurvivePauseAndRebuildButAreNotInventedBySeekRetry()
        {
            NativeInputHost host = null!;
            GameplaySkinInputStateSnapshot pressed = default;
            double releasedAt = 0;
            long epoch = 0;
            AddStep("mount the production input event host under its authoritative clock", () => Child = host = new NativeInputHost());
            AddUntilStep("production host is loaded", () => host.Events?.IsLoaded == true);
            AddStep("observe an actual press and repeated state sample", () =>
            {
                host.ClockHost.Sample(1000.125);
                host.Input(true);
                pressed = host.InputSnapshot();
                Assert.That(pressed.PressedAt, Is.EqualTo(host.Events.AuthoritativeGameplayTime));
                host.ClockHost.Sample(1050.375);
                host.Input(true, 0.5f);
                Assert.That(host.InputSnapshot().PressedAt, Is.EqualTo(pressed.PressedAt));
            });
            AddStep("pause retains the original held input time", () =>
            {
                host.ClockHost.Stop();
                GameplaySkinInputStateSnapshot paused = host.InputSnapshot();
                Assert.That(paused.IsPressed, Is.True);
                Assert.That(paused.PressedAt, Is.EqualTo(pressed.PressedAt));
                Assert.That(paused.ReleasedAt, Is.Null);
                host.ClockHost.SoftUnpause();
            });
            AddStep("release and duplicate release keep the real release edge", () =>
            {
                host.ClockHost.Sample(1100.625);
                releasedAt = host.Events.AuthoritativeGameplayTime;
                host.Input(false);
                host.ClockHost.Sample(1150.875);
                host.Input(false);
                GameplaySkinInputStateSnapshot released = host.InputSnapshot();
                Assert.That(released.PressedAt, Is.EqualTo(pressed.PressedAt));
                Assert.That(released.ReleasedAt, Is.EqualTo(releasedAt));
                epoch = host.Events.EventStream.CurrentEpoch;
                host.Events.RequestReset(GameplaySkinEventResetReason.ConsumerRebuilt);
            });
            AddUntilStep("consumer rebuild commits", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("late attach retains both original origins", () =>
            {
                GameplaySkinInputStateSnapshot rebuilt = host.InputSnapshot();
                Assert.That(rebuilt.PressedAt, Is.EqualTo(pressed.PressedAt));
                Assert.That(rebuilt.ReleasedAt, Is.EqualTo(releasedAt));
                host.ClockHost.Sample(1200);
                host.Input(true);
                Assert.That(host.InputSnapshot().ReleasedAt, Is.Null);
                epoch = host.Events.EventStream.CurrentEpoch;
                host.ClockHost.Seek(500);
            });
            AddUntilStep("real rewind commits", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("held input remains true without a synthetic rewind origin", () =>
            {
                GameplaySkinInputStateSnapshot rewound = host.InputSnapshot();
                Assert.That(rewound.IsPressed, Is.True);
                Assert.That(rewound.PressedAt, Is.Null);
                Assert.That(rewound.ReleasedAt, Is.Null);
                host.ClockHost.Sample(550);
                host.Input(true);
                Assert.That(host.InputSnapshot().PressedAt, Is.Null, "A held sample after seek is not a real press edge.");
                host.ClockHost.Sample(600);
                releasedAt = host.Events.AuthoritativeGameplayTime;
                host.Input(false);
                GameplaySkinInputStateSnapshot releaseOnly = host.InputSnapshot();
                Assert.That(releaseOnly.PressedAt, Is.Null);
                Assert.That(releaseOnly.ReleasedAt, Is.EqualTo(releasedAt));
                host.ClockHost.Sample(620);
                host.Input(true);
                epoch = host.Events.EventStream.CurrentEpoch;
                host.ClockHost.Reset(0);
                // Reconstructed engine edges may arrive while the complete reset is still pending.
                host.Input(false);
                host.Input(true);
            });
            AddUntilStep("retry retains the input rebuilt before its publication barrier", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("retry uses the actual zero-time input edge", () =>
            {
                GameplaySkinInputStateSnapshot retried = host.InputSnapshot();
                Assert.That(retried.IsPressed, Is.True);
                Assert.That(retried.PressedAt, Is.Zero);
                Assert.That(retried.ReleasedAt, Is.Null);
            });
        }

        [TestCase(GameplaySkinEventResetReason.ConsumerRebuilt, true)]
        [TestCase(GameplaySkinEventResetReason.ConsumerRebuilt, false)]
        [TestCase(GameplaySkinEventResetReason.CapacityExceeded, true)]
        [TestCase(GameplaySkinEventResetReason.CapacityExceeded, false)]
        [TestCase(GameplaySkinEventResetReason.Seek, true)]
        [TestCase(GameplaySkinEventResetReason.Seek, false)]
        [TestCase(GameplaySkinEventResetReason.Rewind, true)]
        [TestCase(GameplaySkinEventResetReason.Rewind, false)]
        [TestCase(GameplaySkinEventResetReason.Retry, true)]
        [TestCase(GameplaySkinEventResetReason.Retry, false)]
        public void TestEditorClockRollbackWhileResetIsPendingDiscardsOnlyInvalidOrigins(GameplaySkinEventResetReason reason, bool pressed)
        {
            EditorInputHost host = null!;
            long epoch = 0;
            GameplaySkinInputStateSnapshot rebuilt = default;
            AddStep("mount a real host on an editor-style manual clock without GameplayClockContainer", () => Child = host = new EditorInputHost());
            AddUntilStep("editor-style input host is loaded", () => host.Events?.IsLoaded == true);
            AddStep("observe input at an intermediate position while the reset is already pending", () =>
            {
                host.Sample(2170);
                Assert.That(host.Events.AuthoritativeGameplayTime, Is.EqualTo(2170));
                epoch = host.Events.EventStream.CurrentEpoch;
                host.Events.RequestReset(reason);
                host.Input(true, 0.75f);
                if (!pressed)
                    host.Input(false);
                // This is the editor timestamp-reset ordering: the host already has a reset reason and the
                // physical input state, then its inherited clock moves back without an OnSeek notification.
                host.Sample(0);
            });
            AddUntilStep("the final zero-time epoch commits without a future-origin exception", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("retain physical input state without inventing an origin at the reset destination", () =>
            {
                GameplaySkinInputStateSnapshot input = host.InputSnapshot();
                Assert.That(host.Events.AuthoritativeGameplayTime, Is.Zero);
                Assert.That(input.IsPressed, Is.EqualTo(pressed));
                Assert.That(input.Strength, Is.EqualTo(pressed ? 0.75f : 0));
                Assert.That(input.PressedAt, Is.Null);
                Assert.That(input.ReleasedAt, Is.Null);
                host.Input(!pressed);
                host.Input(pressed);
                rebuilt = host.InputSnapshot();
                Assert.That(rebuilt.PressedAt, Is.Zero, "A new actual input edge can legitimately originate at zero.");
                Assert.That(rebuilt.ReleasedAt, Is.EqualTo(pressed ? (double?)null : 0));
                epoch = host.Events.EventStream.CurrentEpoch;
                host.Events.RequestReset(GameplaySkinEventResetReason.ConsumerRebuilt);
            });
            AddUntilStep("a normal same-clock consumer rebuild commits", () => host.Events.EventStream.CurrentEpoch > epoch);
            AddStep("valid reconstructed origins survive rebuild and late attach", () =>
            {
                GameplaySkinInputStateSnapshot retained = host.InputSnapshot();
                Assert.That(retained.IsPressed, Is.EqualTo(rebuilt.IsPressed));
                Assert.That(retained.Strength, Is.EqualTo(rebuilt.Strength));
                Assert.That(retained.PressedAt, Is.EqualTo(rebuilt.PressedAt));
                Assert.That(retained.ReleasedAt, Is.EqualTo(rebuilt.ReleasedAt));
            });
        }

        private partial class EditorInputHost : Container, IGameplaySkinEventObjectSnapshotSource
        {
            private readonly ManualClock source = new ManualClock { CurrentTime = 500 };
            private readonly FramedClock framedClock;
            public GameplaySkinEventRuntimeHost Events { get; private set; } = null!;
            private GameplaySkinInputStateSnapshot target;

            public EditorInputHost()
            {
                RelativeSizeAxes = Axes.Both;
                Clock = framedClock = new FramedClock(source);
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                GameplaySkinLayoutPublication publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(renderer.WhitePixel, false);
                target = publication.PreparedScene.InitialEventState.Inputs.First();
                Child = Events = new GameplaySkinEventRuntimeHost(publication, new Beatmap(), null, this);
            }

            public void Sample(double time)
            {
                source.CurrentTime = time;
                framedClock.ProcessFrame();
                UpdateSubTree();
            }

            public void Input(bool pressed, float strength = 1) => Events.PublishInput(target.GroupId, target.LaneId, pressed, strength);

            public GameplaySkinInputStateSnapshot InputSnapshot()
            {
                using GameplaySkinEventSubscription subscription = Events.EventStream.Subscribe();
                Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
                return ((GameplaySkinStateEventPayload)envelope!.Payload).State.Inputs.Single(input => input.LaneId == target.LaneId);
            }

            public IEnumerable<GameplaySkinObjectStateSnapshot> CreateGameplaySkinActiveObjectSnapshot(double gameplayTime)
                => Array.Empty<GameplaySkinObjectStateSnapshot>();
        }

        private partial class NativeInputHost : Container
        {
            public readonly NativeClock ClockHost = new NativeClock();
            public GameplaySkinEventRuntimeHost Events { get; private set; } = null!;
            private GameplaySkinInputStateSnapshot target;

            public NativeInputHost()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                GameplaySkinLayoutPublication publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(renderer.WhitePixel, false);
                target = publication.PreparedScene.InitialEventState.Inputs.First();
                Events = new GameplaySkinEventRuntimeHost(publication, new Beatmap());
                ClockHost.Child = Events;
                Child = ClockHost;
            }

            public void Input(bool pressed, float strength = 1) => Events.PublishInput(target.GroupId, target.LaneId, pressed, strength);

            public GameplaySkinInputStateSnapshot InputSnapshot()
            {
                using GameplaySkinEventSubscription subscription = Events.EventStream.Subscribe();
                Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
                return ((GameplaySkinStateEventPayload)envelope!.Payload).State.Inputs.Single(input => input.LaneId == target.LaneId);
            }
        }

        private partial class NativeClock : GameplayClockContainer
        {
            private readonly AdjustableClock source;

            public NativeClock()
                : this(new AdjustableClock { CurrentTime = 500 })
            {
            }

            private NativeClock(AdjustableClock source)
                : base(source, false, false)
            {
                this.source = source;
                SoftUnpause();
            }

            public void Sample(double time)
            {
                source.CurrentTime = time;
                UpdateSubTree();
            }
        }

        private class AdjustableClock : ManualClock, IAdjustableClock
        {
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;
            public bool Seek(double position)
            {
                CurrentTime = position;
                return true;
            }
            public void Reset()
            {
                IsRunning = false;
                CurrentTime = 0;
            }
            public void ResetSpeedAdjustments() => Rate = 1;
        }
    }
}
