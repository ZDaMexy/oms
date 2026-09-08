// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading;

namespace osu.Game.Skinning.Gameplay.Scripting
{
    /// <summary>Engine implementation only. The author VM receives numeric values, never this CLR object.</summary>
    internal interface IGameplaySkinScriptHost
    {
        bool IsGranted(string capabilityId);
        double Read(GameplaySkinScriptInput input);
        void Set(string nodeId, GameplaySkinScriptProperty property, double value);
    }

    internal readonly record struct GameplaySkinScriptProfiler(
        long Callbacks, long Instructions, int MaximumCallbackInstructions, long ElapsedTicks, int HeapBytes, long Writes);

    /// <summary>
    /// Fixed-memory interpreter. Every operation, including each backwards branch, consumes budget before execution.
    /// No opcode allocates memory, exposes an object, invokes unbounded libraries or reads a wall clock.
    /// </summary>
    internal sealed class GameplaySkinScriptInstance
    {
        public const int MAX_CALLBACK_INSTRUCTIONS = 4096;
        public const int MAX_FRAME_INSTRUCTIONS = 16384;
        public const int MAX_FRAME_WRITES = 256;

        private readonly GameplaySkinScriptProgram program;
        private readonly double[] registers;
        private readonly double[] heap;
        private uint randomState;
        private long currentFrame = long.MinValue;
        private int frameInstructions;
        private int frameWrites;
        private long callbacks;
        private long totalInstructions;
        private int maximumCallbackInstructions;
        private long elapsedTicks;
        private long writes;

        public GameplaySkinScriptException? Fault { get; private set; }
        public bool IsEnabled { get; private set; } = true;
        public GameplaySkinScriptProfiler Profiler => new GameplaySkinScriptProfiler(callbacks, totalInstructions, maximumCallbackInstructions,
            elapsedTicks, (registers.Length + heap.Length) * sizeof(double), writes);

        public GameplaySkinScriptInstance(GameplaySkinScriptProgram program, uint seed)
        {
            this.program = program;
            registers = new double[program.RegisterCount];
            heap = new double[program.HeapCells];
            Reset(seed);
        }

        /// <summary>Engine Snapshot/Reset only. A failed instance stays fused; only a new publication may replace it.</summary>
        public void Reset(uint seed)
        {
            program.ResetState(registers);
            Array.Clear(heap);
            randomState = seed == 0 ? 0x6D2B79F5 : seed;
        }

        public void Disable() => IsEnabled = false;

        public void Fail(GameplaySkinScriptFaultCode code, int line = 0)
        {
            IsEnabled = false;
            Fault ??= new GameplaySkinScriptException(code, line);
        }

        /// <summary>
        /// Invoked for each canonical event and for a frame. All invocations in the same engine frame share one budget.
        /// The host checks the current authorization token at each API boundary, so a saved grant cannot survive revocation.
        /// </summary>
        public bool Execute(IGameplaySkinScriptHost host, long frameId, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
                return false;

            if (currentFrame != frameId)
            {
                currentFrame = frameId;
                frameInstructions = 0;
                frameWrites = 0;
            }

            long started = Stopwatch.GetTimestamp();
            int executed = 0;
            int line = 0;
            callbacks++;
            try
            {
                for (int i = 0; i < program.Requests.Count; i++)
                {
                    ScriptCapabilityRequest request = program.Requests[i];
                    if (request.Mode == ScriptCapabilityMode.Required && !host.IsGranted(request.Id))
                        return fail(GameplaySkinScriptFaultCode.PermissionDenied);
                }

                for (int pc = 0; pc < program.InstructionCount;)
                {
                    ScriptInstruction instruction = program.InstructionAt(pc++);
                    line = instruction.Line;
                    if (cancellationToken.IsCancellationRequested)
                        return fail(GameplaySkinScriptFaultCode.Cancelled);
                    if (executed == MAX_CALLBACK_INSTRUCTIONS)
                        return fail(GameplaySkinScriptFaultCode.InstructionLimit);
                    if (frameInstructions == MAX_FRAME_INSTRUCTIONS)
                        return fail(GameplaySkinScriptFaultCode.FrameInstructionLimit);

                    executed++;
                    frameInstructions++;
                    double result;
                    switch (instruction.Opcode)
                    {
                        case ScriptOpcode.Halt:
                            return true;

                        case ScriptOpcode.Move:
                            result = value(instruction.Left);
                            break;

                        case ScriptOpcode.Add:
                            result = value(instruction.Left) + value(instruction.Right);
                            break;

                        case ScriptOpcode.Subtract:
                            result = value(instruction.Left) - value(instruction.Right);
                            break;

                        case ScriptOpcode.Multiply:
                            result = value(instruction.Left) * value(instruction.Right);
                            break;

                        case ScriptOpcode.Divide:
                            result = value(instruction.Left) / value(instruction.Right);
                            break;

                        case ScriptOpcode.Minimum:
                            result = Math.Min(value(instruction.Left), value(instruction.Right));
                            break;

                        case ScriptOpcode.Maximum:
                            result = Math.Max(value(instruction.Left), value(instruction.Right));
                            break;

                        case ScriptOpcode.LessThan:
                            result = value(instruction.Left) < value(instruction.Right) ? 1 : 0;
                            break;

                        case ScriptOpcode.Equal:
                            result = value(instruction.Left) == value(instruction.Right) ? 1 : 0;
                            break;

                        case ScriptOpcode.Clamp:
                            double lower = value(instruction.Right);
                            double upper = value(instruction.Third);
                            if (lower > upper)
                                return fail(GameplaySkinScriptFaultCode.NonFiniteNumber);
                            result = Math.Min(upper, Math.Max(lower, value(instruction.Left)));
                            break;

                        case ScriptOpcode.Read:
                            var input = (GameplaySkinScriptInput)instruction.Argument;
                            if (!host.IsGranted(GameplaySkinScriptCompiler.InputCapability(input)))
                                return fail(GameplaySkinScriptFaultCode.PermissionDenied);
                            result = host.Read(input);
                            break;

                        case ScriptOpcode.Random:
                            if (!host.IsGranted(GameplaySkinScriptCompiler.RANDOM_CAPABILITY))
                                return fail(GameplaySkinScriptFaultCode.PermissionDenied);
                            randomState ^= randomState << 13;
                            randomState ^= randomState >> 17;
                            randomState ^= randomState << 5;
                            result = randomState / 4294967296.0;
                            break;

                        case ScriptOpcode.Set:
                            if (!host.IsGranted(GameplaySkinScriptCompiler.SCENE_CAPABILITY))
                                return fail(GameplaySkinScriptFaultCode.PermissionDenied);
                            if (frameWrites == MAX_FRAME_WRITES)
                                return fail(GameplaySkinScriptFaultCode.NodeLimit);
                            frameWrites++;
                            writes++;
                            ScriptTarget target = program.Targets[instruction.Argument];
                            host.Set(target.NodeId, target.Property, value(instruction.Left));
                            continue;

                        case ScriptOpcode.Jump:
                            pc = instruction.Argument;
                            continue;

                        case ScriptOpcode.When:
                            if (value(instruction.Left) != 0)
                                pc = instruction.Argument;
                            continue;

                        case ScriptOpcode.Load:
                            if (!heapIndex(instruction.Left, out int readIndex))
                                return fail(GameplaySkinScriptFaultCode.HeapLimit);
                            result = heap[readIndex];
                            break;

                        case ScriptOpcode.Store:
                            if (!heapIndex(instruction.Left, out int writeIndex))
                                return fail(GameplaySkinScriptFaultCode.HeapLimit);
                            heap[writeIndex] = value(instruction.Right);
                            continue;

                        case ScriptOpcode.Granted:
                            ScriptCapabilityRequest request = program.Requests[instruction.Argument];
                            result = request.Mode != ScriptCapabilityMode.Deny && host.IsGranted(request.Id) ? 1 : 0;
                            break;

                        default:
                            throw new InvalidOperationException("Verified script contains an unknown instruction.");
                    }

                    if (!double.IsFinite(result))
                        return fail(GameplaySkinScriptFaultCode.NonFiniteNumber);
                    registers[instruction.Destination] = result;
                }

                return true;
            }
            catch (GameplaySkinScriptException exception)
            {
                // Only a documented host boundary failure is a recoverable script error. Programming faults remain visible.
                Fail(exception.Code, exception.Line == 0 ? line : exception.Line);
                return false;
            }
            finally
            {
                totalInstructions += executed;
                maximumCallbackInstructions = Math.Max(maximumCallbackInstructions, executed);
                elapsedTicks += Stopwatch.GetTimestamp() - started;
            }

            double value(ScriptOperand operand) => operand.Register < 0 ? operand.Constant : registers[operand.Register];

            bool heapIndex(ScriptOperand operand, out int index)
            {
                double number = value(operand);
                index = (int)number;
                return number >= 0 && number < heap.Length && number == index;
            }

            bool fail(GameplaySkinScriptFaultCode code)
            {
                Fail(code, line);
                return false;
            }
        }
    }
}
