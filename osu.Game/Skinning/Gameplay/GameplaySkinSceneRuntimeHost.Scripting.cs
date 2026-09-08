// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Skinning.Gameplay.Scripting;
using osuTK;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>The exact native transform beneath an optional script overlay, retained only while that overlay is applied.</summary>
    internal readonly record struct GameplaySkinSceneScriptBaseline(float Alpha, Vector2 Position, Axes RelativePositionAxes, Vector2 Scale, float Rotation);

    public partial class GameplaySkinSceneRuntimeHost : IGameplaySkinScriptHost
    {
        internal const int SCRIPT_TICKS_PER_SECOND = 60;
        internal const int MAX_SCRIPT_TICKS_PER_FRAME = 120;

        private readonly Dictionary<(string Node, GameplaySkinScriptProperty Property), int> scriptOutputIndexes =
            new Dictionary<(string, GameplaySkinScriptProperty), int>();

        private double[] scriptOutputs = Array.Empty<double>();
        private bool[] scriptOutputSet = Array.Empty<bool>();
        private GameplaySkinScriptAuthorization? scriptAuthorization;
        private GameplaySkinScriptProgram? scriptProgram;
        private uint scriptSeed;
        private long scriptAuthorizationEpoch = -1;
        private bool scriptHasAppliedOutput;
        private long scriptFrame;
        private double scriptTime;
        private double scriptDelta;
        private double scriptObservedTime;
        private double scriptPreviousTickTime;
        private long scriptNextTick;
        private int scriptTicksThisFrame;
        private GameplaySkinTimingStateSnapshot scriptTiming;
        private int scriptEventKind;
        private double scriptEventValue;
        private readonly object scriptAuthorizationScheduleGate = new object();
        private GameplaySkinScriptAuthorization? observedScriptAuthorization;
        private ScheduledDelegate? pendingScriptAuthorizationChange;

        [Resolved]
        private GameHost scriptGameHost { get; set; } = null!;

        internal GameplaySkinScriptInstance? ScriptInstance { get; private set; }

        public string ScriptStatus { get; private set; } = "absent";

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (scriptAuthorization == null)
                return;

            lock (scriptAuthorizationScheduleGate)
            {
                if (disposed)
                    return;

                observedScriptAuthorization = scriptAuthorization;
                observedScriptAuthorization.Changed += onScriptAuthorizationChanged;
                synchroniseScriptAuthorization();
            }
        }

        private void onScriptAuthorizationChanged()
        {
            // Token publication can occur under the persistence worker's lock. Only enqueue here; the game host
            // scheduler remains reachable while FrameStabilityContainer intentionally suspends a paused subtree.
            lock (scriptAuthorizationScheduleGate)
            {
                GameplaySkinScriptAuthorization? token = observedScriptAuthorization;
                if (token == null || pendingScriptAuthorizationChange != null)
                    return;

                pendingScriptAuthorizationChange = scriptGameHost.UpdateThread.Scheduler.Add(() =>
                {
                    lock (scriptAuthorizationScheduleGate)
                    {
                        pendingScriptAuthorizationChange = null;
                        if (disposed || !ReferenceEquals(observedScriptAuthorization, token) || !ReferenceEquals(scriptAuthorization, token))
                            return;

                        synchroniseScriptAuthorization();
                    }
                });
            }
        }

        private void initialiseScript()
        {
            scriptProgram = PreparedScene.ScriptProgram;
            scriptAuthorization = PreparedScene.ScriptAuthorization;
            if (scriptProgram == null)
                return;

            if (scriptAuthorization == null)
                throw new InvalidOperationException("A prepared script requires its exact publication authorization token.");

            scriptOutputs = new double[scriptProgram.Targets.Count];
            scriptOutputSet = new bool[scriptProgram.Targets.Count];
            for (int i = 0; i < scriptProgram.Targets.Count; i++)
            {
                ScriptTarget target = scriptProgram.Targets[i];
                scriptOutputIndexes.Add((target.NodeId, target.Property), i);
            }

            // The engine supplies a stable seed from the already verified compiler fingerprint. No process-local
            // hash, wall clock, user path or gameplay authority object crosses the numeric VM boundary.
            for (int i = 0; i < 8; i++)
            {
                char digit = scriptProgram.Fingerprint[i];
                scriptSeed = (scriptSeed << 4) | (uint)(digit <= '9' ? digit - '0' : char.ToLowerInvariant(digit) - 'a' + 10);
            }

            scriptTiming = timingState;
            synchroniseScriptAuthorization();
        }

        private void beginScriptFrame()
        {
            scriptFrame++;
            scriptTicksThisFrame = 0;
            synchroniseScriptAuthorization();
        }

        private void synchroniseScriptAuthorization()
        {
            if (scriptAuthorization == null)
                return;

            // Consume one atomic effective decision. A late observer cannot reset an epoch already consumed by
            // active gameplay; a revoke/regrant still changes epoch even if its final permission mask is identical.
            var rights = scriptAuthorization.RuntimeRights;
            if (rights.Epoch == scriptAuthorizationEpoch)
                return;

            ScriptInstance?.Disable();
            restoreScriptOutput();
            scriptAuthorizationEpoch = rights.Epoch;
            anchorScriptTicks(scriptObservedTime);
            if (!rights.RequiredSatisfied)
            {
                ScriptStatus = "awaiting-authorization";
                reportScript();
                return;
            }

            ScriptInstance = new GameplaySkinScriptInstance(scriptProgram!, scriptSeed);
            ScriptStatus = "running";
            reportScript();
        }

        private void beforeScriptEvent(GameplaySkinEventRecord record)
        {
            synchroniseScriptAuthorization();
            if (record.Payload.Family is not (GameplaySkinEventPayloadFamily.State or GameplaySkinEventPayloadFamily.Publication))
                advanceScriptTicks(record.GameplayTime);
        }

        private void consumeScriptEvent(GameplaySkinEventRecord record)
        {
            scriptObservedTime = record.GameplayTime;
            // Fractional render high-water samples must not change earlier tick input. The script observes the
            // timing snapshot stamped on the same canonical event stream in every rendering schedule.
            scriptTiming = record.AuthoritativeTiming;
            synchroniseScriptAuthorization();
            if (ScriptInstance?.IsEnabled != true)
                return;

            if (record.Payload.Family is GameplaySkinEventPayloadFamily.State or GameplaySkinEventPayloadFamily.Publication)
            {
                ScriptInstance.Reset(scriptSeed);
                restoreScriptOutput();
                anchorScriptTicks(record.GameplayTime);
            }

            // Callback occurrence is itself an observable event channel, even if the program never reads kind/value.
            // Keep the engine's complete baseline/reset above, but deliver author events only with current consent.
            if (!((IGameplaySkinScriptHost)this).IsGranted("gameplay.events.read"))
                return;

            scriptTime = record.GameplayTime;
            scriptDelta = 0;
            scriptEventKind = (int)record.EventKind;
            scriptEventValue = record.Payload.Family switch
            {
                GameplaySkinEventPayloadFamily.Input => record.Payload.GetInput(record.GroupId!, record.LaneId!).Strength,
                GameplaySkinEventPayloadFamily.Object => record.Payload.GetObject(record.GroupId!, record.LaneId).Progress,
                GameplaySkinEventPayloadFamily.Judgement => record.Payload.GetJudgement(record.GroupId, record.LaneId).Offset,
                GameplaySkinEventPayloadFamily.Score => record.EventKind switch
                {
                    GameplaySkinEventKind.ComboChanged => scoreState.Combo,
                    GameplaySkinEventKind.GaugeChanged => scoreState.Gauge,
                    _ => scoreState.Score,
                },
                GameplaySkinEventPayloadFamily.Timing => record.EventKind switch
                {
                    GameplaySkinEventKind.TimingBar => timingState.BarIndex,
                    GameplaySkinEventKind.TimingBpmChanged => timingState.Bpm,
                    GameplaySkinEventKind.TimingScrollChanged => timingState.ScrollMultiplier,
                    GameplaySkinEventKind.TimingStopStarted => 1,
                    GameplaySkinEventKind.TimingStopEnded => 0,
                    _ => timingState.Beat,
                },
                GameplaySkinEventPayloadFamily.Bga => (int)record.Payload.GetBga().ContentState,
                GameplaySkinEventPayloadFamily.Lifecycle => (int)lifecycleState,
                _ => 0,
            };
            executeScript();
        }

        private void runScriptFrame(double gameplayTime)
        {
            scriptObservedTime = gameplayTime;
            synchroniseScriptAuthorization();
            if (ScriptInstance?.IsEnabled != true)
                return;

            // The scene runs before the native playfield, which can still publish judgement/object edges at this
            // frame's time after our queue is empty. Only a later engine time seals that timestamp's event sequence.
            advanceScriptTicks(gameplayTime);
            if (!ScriptInstance.IsEnabled)
                return;

            int applications = 0;
            for (int i = 0; i < scriptOutputSet.Length; i++)
            {
                if (scriptOutputSet[i] && runtimeNodesBySourceId.TryGetValue(scriptProgram!.Targets[i].NodeId, out List<GameplaySkinSceneRuntimeNode>? targets))
                    applications += targets.Count;
            }

            // One author target can expand to many lane/template/native pool clones. Count the actual work before
            // touching any node, independently of the VM's opcode and set-call budgets.
            if (applications > GameplaySkinSceneBudgets.MAX_PROPERTY_APPLICATIONS_PER_FRAME)
            {
                ScriptInstance.Fail(GameplaySkinScriptFaultCode.NodeLimit);
                onScriptFault();
                return;
            }

            for (int i = 0; i < scriptOutputSet.Length; i++)
            {
                if (!scriptOutputSet[i])
                    continue;

                ScriptTarget target = scriptProgram!.Targets[i];
                if (!runtimeNodesBySourceId.TryGetValue(target.NodeId, out List<GameplaySkinSceneRuntimeNode>? nodes))
                    continue; // The prepared target may currently have no attached pooled instance.

                foreach (GameplaySkinSceneRuntimeNode node in nodes)
                {
                    if (!((IGameplaySkinScriptHost)this).IsGranted("scene.numeric.write"))
                    {
                        ScriptInstance.Fail(GameplaySkinScriptFaultCode.PermissionDenied);
                        onScriptFault();
                        synchroniseScriptAuthorization();
                        return;
                    }

                    node.ScriptBaseline ??= new GameplaySkinSceneScriptBaseline(node.TransformDrawable.Alpha, node.TransformDrawable.Position,
                        node.TransformDrawable.RelativePositionAxes, node.TransformDrawable.Scale, node.TransformDrawable.Rotation);
                    scriptHasAppliedOutput = true;
                    applyRuntimeNumberProperty(node, sceneProperty(target.Property), scriptOutputs[i]);
                }
            }

            // Consent can change on the persistence worker during an otherwise bounded callback. Reconcile again
            // before this frame becomes visible, so an old overlay cannot outlive its final grant snapshot.
            synchroniseScriptAuthorization();
            reportScript();
        }

        private void anchorScriptTicks(double gameplayTime)
        {
            scriptPreviousTickTime = gameplayTime;
            scriptNextTick = (long)Math.Floor(gameplayTime / 1000 * SCRIPT_TICKS_PER_SECOND) + 1;
        }

        private void advanceScriptTicks(double highWater)
        {
            if (ScriptInstance?.IsEnabled != true || lifecycleState != GameplaySkinLifecycleState.Running)
                return;

            while (true)
            {
                // Tick occurrence reveals gameplay clock progression even without a read opcode. Admit every
                // author callback through snapshot consent, including revocation during bounded catch-up.
                if (!((IGameplaySkinScriptHost)this).IsGranted("gameplay.snapshot.read"))
                    return;

                double time = scriptNextTick * 1000.0 / SCRIPT_TICKS_PER_SECOND;
                if (time >= highWater)
                    return;

                if (scriptTicksThisFrame == MAX_SCRIPT_TICKS_PER_FRAME)
                {
                    ScriptInstance.Fail(GameplaySkinScriptFaultCode.FrameInstructionLimit);
                    onScriptFault();
                    return;
                }

                scriptTicksThisFrame++;
                scriptNextTick++;
                scriptTime = time;
                scriptDelta = time - scriptPreviousTickTime;
                scriptPreviousTickTime = time;
                scriptEventKind = -1;
                scriptEventValue = 0;
                if (!executeScript())
                    return;
            }
        }

        private bool executeScript()
        {
            if (ScriptInstance!.Execute(this, scriptFrame))
                return true;

            onScriptFault();
            return false;
        }

        private void onScriptFault()
        {
            ScriptStatus = "faulted";
            restoreScriptOutput();
            reportScript();
        }

        private void restoreScriptOutput()
        {
            Array.Clear(scriptOutputSet);
            if (!scriptHasAppliedOutput)
                return;

            scriptHasAppliedOutput = false;
            foreach (GameplaySkinSceneRuntimeNode node in runtimeNodes.Values)
            {
                if (node.ScriptBaseline is not GameplaySkinSceneScriptBaseline baseline)
                    continue;

                node.TransformDrawable.Alpha = baseline.Alpha;
                node.TransformDrawable.Position = baseline.Position;
                node.TransformDrawable.RelativePositionAxes = baseline.RelativePositionAxes;
                node.TransformDrawable.Scale = baseline.Scale;
                node.TransformDrawable.Rotation = baseline.Rotation;
                node.ScriptBaseline = null;
            }

            // C5 tracks and bindings may have changed while the script owned an overlay. Restore their current
            // authoritative projection as well as the exact native transform captured before the first write.
            foreach (StateMachineInstance instance in stateMachineInstances)
                applyStateAssignments(instance);
            sampleTracks(scriptObservedTime);
            applyBindings(GameplaySkinSceneStateFamily.All);
            applyVariants(GameplaySkinSceneStateFamily.All);
        }

        private void detachScript()
        {
            lock (scriptAuthorizationScheduleGate)
            {
                if (observedScriptAuthorization != null)
                    observedScriptAuthorization.Changed -= onScriptAuthorizationChanged;
                observedScriptAuthorization = null;
                pendingScriptAuthorizationChange?.Cancel();
                pendingScriptAuthorizationChange = null;
                ScriptInstance?.Disable();
                ScriptStatus = scriptProgram == null ? "absent" : "detached";
                reportScript();
            }
        }

        private void reportScript()
        {
            GameplaySkinScriptProfiler profiler = ScriptInstance?.Profiler ?? default;
            GameplaySkinScriptException? fault = ScriptInstance?.Fault;
            scriptAuthorization?.ReportRuntime(ScriptStatus, profiler.Callbacks, profiler.Instructions, profiler.ElapsedTicks,
                profiler.HeapBytes, fault?.Code.ToString(), fault?.Line ?? 0);
        }

        bool IGameplaySkinScriptHost.IsGranted(string capabilityId)
        {
            if (disposed || scriptAuthorization == null)
                return false;

            var rights = scriptAuthorization.RuntimeRights;
            int bit = capabilityId switch
            {
                "gameplay.snapshot.read" => 1,
                "gameplay.events.read" => 2,
                "scene.numeric.write" => 4,
                "math.random.read" => 8,
                _ => 0,
            };
            return rights.RequiredSatisfied && (rights.GrantMask & bit) != 0;
        }

        double IGameplaySkinScriptHost.Read(GameplaySkinScriptInput input)
        {
            // The persistence worker may revoke consent between opcode admission and this actual API boundary.
            if (!((IGameplaySkinScriptHost)this).IsGranted(GameplaySkinScriptCompiler.InputCapability(input)))
                throw new GameplaySkinScriptException(GameplaySkinScriptFaultCode.PermissionDenied);

            return input switch
            {
                GameplaySkinScriptInput.Time => scriptTime,
                GameplaySkinScriptInput.Delta => scriptDelta,
                GameplaySkinScriptInput.EventKind => scriptEventKind,
                GameplaySkinScriptInput.EventValue => scriptEventValue,
                GameplaySkinScriptInput.Combo => scoreState.Combo,
                GameplaySkinScriptInput.Gauge => scoreState.Gauge,
                GameplaySkinScriptInput.Beat => scriptTiming.Beat,
                GameplaySkinScriptInput.Bpm => scriptTiming.Bpm,
                GameplaySkinScriptInput.Running => lifecycleState == GameplaySkinLifecycleState.Running ? 1 : 0,
                _ => throw new InvalidOperationException("A verified script requested an unknown snapshot field."),
            };
        }

        void IGameplaySkinScriptHost.Set(string nodeId, GameplaySkinScriptProperty property, double value)
        {
            if (!((IGameplaySkinScriptHost)this).IsGranted("scene.numeric.write"))
                throw new GameplaySkinScriptException(GameplaySkinScriptFaultCode.PermissionDenied);

            if (!double.IsFinite(value))
                throw new GameplaySkinScriptException(GameplaySkinScriptFaultCode.NonFiniteNumber);

            int output = scriptOutputIndexes[(nodeId, property)];
            scriptOutputs[output] = value;
            scriptOutputSet[output] = true;
        }

        private static GameplaySkinSceneProperty sceneProperty(GameplaySkinScriptProperty property) => property switch
        {
            GameplaySkinScriptProperty.Alpha => GameplaySkinSceneProperty.Opacity,
            GameplaySkinScriptProperty.X => GameplaySkinSceneProperty.X,
            GameplaySkinScriptProperty.Y => GameplaySkinSceneProperty.Y,
            GameplaySkinScriptProperty.Rotation => GameplaySkinSceneProperty.Rotation,
            GameplaySkinScriptProperty.ScaleX => GameplaySkinSceneProperty.ScaleX,
            GameplaySkinScriptProperty.ScaleY => GameplaySkinSceneProperty.ScaleY,
            _ => throw new InvalidOperationException("A verified script requested an unknown scene property."),
        };
    }
}
