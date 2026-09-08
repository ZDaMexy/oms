// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace osu.Game.Skinning.Gameplay.Scripting
{
    internal enum ScriptCapabilityMode : byte
    {
        Required,
        Optional,
        Deny,
    }

    internal sealed record ScriptCapabilityRequest(string Id, ScriptCapabilityMode Mode);

    internal enum GameplaySkinScriptInput
    {
        Time,
        Delta,
        EventKind,
        EventValue,
        Combo,
        Gauge,
        Beat,
        Bpm,
        Running,
    }

    internal enum GameplaySkinScriptProperty
    {
        Alpha,
        X,
        Y,
        Rotation,
        ScaleX,
        ScaleY,
    }

    internal sealed record ScriptTarget(string NodeId, GameplaySkinScriptProperty Property);

    internal enum ScriptOpcode : byte
    {
        Halt,
        Move,
        Add,
        Subtract,
        Multiply,
        Divide,
        Minimum,
        Maximum,
        LessThan,
        Equal,
        Clamp,
        Read,
        Random,
        Set,
        Jump,
        When,
        Load,
        Store,
        Granted,
    }

    internal readonly record struct ScriptOperand(int Register, double Constant)
    {
        public static ScriptOperand Literal(double value) => new ScriptOperand(-1, value);
    }

    internal readonly record struct ScriptInstruction(
        ScriptOpcode Opcode, int Destination, ScriptOperand Left, ScriptOperand Right, ScriptOperand Third, int Argument, int Line);

    /// <summary>
    /// Verified numeric code. Only the compiler/verifier can construct a program; no author CLR objects enter a callback.
    /// </summary>
    internal sealed class GameplaySkinScriptProgram
    {
        public const int SOURCE_VERSION = 1;
        public const int BYTECODE_VERSION = 1;
        public const int COMPILER_VERSION = 1;
        public const int RUNTIME_VERSION = 1;
        public const int MAX_SOURCE_BYTES = 65536;
        public const int MAX_BYTECODE_BYTES = 262144;
        public const int MAX_INSTRUCTIONS = 2048;
        public const int MAX_SOURCE_LINES = 4096;
        public const int MAX_REGISTERS = 128;
        public const int MAX_HEAP_CELLS = 512;
        public const int MAX_TARGETS = 128;
        public const int MAX_REQUESTS = 32;

        private readonly ScriptInstruction[] instructions;
        private readonly double[] initialState;

        public IReadOnlyList<ScriptCapabilityRequest> Requests { get; }
        public IReadOnlyList<ScriptTarget> Targets { get; }
        public IReadOnlyList<int> SourceMap { get; }
        public int SourceVersion => SOURCE_VERSION;
        public int BytecodeVersion => BYTECODE_VERSION;
        public int CompilerVersion => COMPILER_VERSION;
        public int RuntimeVersion => RUNTIME_VERSION;
        public string Fingerprint { get; }
        public string SourceDigest { get; }
        public int HeapCells { get; }
        public int RegisterCount => initialState.Length;
        public int InstructionCount => instructions.Length;

        internal GameplaySkinScriptProgram(ScriptCapabilityRequest[] requests, ScriptTarget[] targets, double[] initialState,
                                           int heapCells, ScriptInstruction[] instructions, string sourceDigest)
        {
            Requests = Array.AsReadOnly(requests);
            Targets = Array.AsReadOnly(targets);
            this.initialState = initialState;
            HeapCells = heapCells;
            this.instructions = instructions;
            SourceMap = Array.AsReadOnly(instructions.Select(i => i.Line).ToArray());
            SourceDigest = sourceDigest;
            using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Span<byte> compilerVersion = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(compilerVersion, COMPILER_VERSION);
            fingerprint.AppendData(compilerVersion);
            fingerprint.AppendData(GameplaySkinScriptCompiler.EncodeBytecode(this));
            Fingerprint = Convert.ToHexString(fingerprint.GetHashAndReset());
        }

        internal ScriptInstruction InstructionAt(int index) => instructions[index];
        internal double InitialStateAt(int index) => initialState[index];
        internal void ResetState(double[] destination) => initialState.CopyTo(destination, 0);
    }

    internal enum GameplaySkinScriptFaultCode
    {
        InvalidSource,
        UnsupportedVersion,
        InvalidBytecode,
        SourceLimit,
        InstructionLimit,
        FrameInstructionLimit,
        HeapLimit,
        NodeLimit,
        PermissionDenied,
        NonFiniteNumber,
        Cancelled,
        Disabled,
    }

    /// <summary>Source-mapped diagnostics contain stable codes and line numbers, never source text or paths.</summary>
    internal sealed class GameplaySkinScriptException : Exception
    {
        public GameplaySkinScriptFaultCode Code { get; }
        public int Line { get; }

        public GameplaySkinScriptException(GameplaySkinScriptFaultCode code, int line = 0)
            : base($"OMS-SKIN-SCRIPT-{code}: line {line}")
        {
            Code = code;
            Line = line;
        }
    }
}
