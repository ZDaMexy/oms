// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace osu.Game.Skinning.Gameplay.Scripting
{
    /// <summary>
    /// Background-only compiler and verifier for the versioned, numeric OMS presentation language.
    /// The bytecode has bounded counts, fixed-width instructions and no object or native references.
    /// </summary>
    internal static class GameplaySkinScriptCompiler
    {
        public const string SNAPSHOT_CAPABILITY = "gameplay.snapshot.read";
        public const string EVENT_CAPABILITY = "gameplay.events.read";
        public const string SCENE_CAPABILITY = "scene.numeric.write";
        public const string RANDOM_CAPABILITY = "math.random.read";

        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);
        private static readonly string[] input_names = { "time", "delta", "event-kind", "event-value", "combo", "gauge", "beat", "bpm", "running" };
        private static readonly string[] property_names = { "alpha", "x", "y", "rotation", "scale-x", "scale-y" };
        private static readonly byte[] magic = Encoding.ASCII.GetBytes("OMSSBC01");

        public static GameplaySkinScriptProgram Compile(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.Length == 0 || source.Length > GameplaySkinScriptProgram.MAX_SOURCE_BYTES)
                throw error(GameplaySkinScriptFaultCode.SourceLimit);

            string text;
            try
            {
                text = strict_utf8.GetString(source.Span);
            }
            catch (DecoderFallbackException)
            {
                throw error(GameplaySkinScriptFaultCode.InvalidSource);
            }

            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (lines.Length > GameplaySkinScriptProgram.MAX_SOURCE_LINES)
                throw error(GameplaySkinScriptFaultCode.SourceLimit);

            var requests = new List<ScriptCapabilityRequest>();
            var targets = new List<ScriptTarget>();
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            var initial = new List<double>();
            var labels = new Dictionary<string, int>(StringComparer.Ordinal);
            var code = new List<(string[] Words, int Line)>();
            int heapCells = 0;
            bool heapDeclared = false;
            bool header = false;
            bool body = false;

            for (int i = 0; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int line = i + 1;
                string[] words = lines[i].Split('#')[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                    continue;

                if (!header)
                {
                    if (words.Length != 2 || words[0] != "oms-script")
                        throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                    if (words[1] != "1")
                        throw error(GameplaySkinScriptFaultCode.UnsupportedVersion, line);
                    header = true;
                    continue;
                }

                switch (words[0])
                {
                    case "required":
                    case "optional":
                    case "deny":
                        declaration(2);
                        if (!validToken(words[1], 96) || requests.Any(r => r.Id == words[1]) || requests.Count == GameplaySkinScriptProgram.MAX_REQUESTS)
                            throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                        try
                        {
                            GameplaySkinCapabilityId.ValidateToken(words[1], nameof(source));
                        }
                        catch (ArgumentException)
                        {
                            throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                        }
                        requests.Add(new ScriptCapabilityRequest(words[1], words[0] == "required" ? ScriptCapabilityMode.Required
                            : words[0] == "optional" ? ScriptCapabilityMode.Optional : ScriptCapabilityMode.Deny));
                        continue;

                    case "state":
                        declaration(3);
                        if (!validToken(words[1], 64) || initial.Count == GameplaySkinScriptProgram.MAX_REGISTERS || !states.TryAdd(words[1], initial.Count))
                            throw error(GameplaySkinScriptFaultCode.HeapLimit, line);
                        initial.Add(number(words[2], line));
                        continue;

                    case "heap":
                        declaration(2);
                        if (heapDeclared || !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out heapCells)
                                         || heapCells < 0 || heapCells > GameplaySkinScriptProgram.MAX_HEAP_CELLS)
                            throw error(GameplaySkinScriptFaultCode.HeapLimit, line);
                        heapDeclared = true;
                        continue;

                    case "target":
                        declaration(3);
                        var target = new ScriptTarget(words[1], parseProperty(words[2], line));
                        if (!validToken(words[1], 128) || targets.Contains(target) || targets.Count == GameplaySkinScriptProgram.MAX_TARGETS)
                            throw error(GameplaySkinScriptFaultCode.NodeLimit, line);
                        targets.Add(target);
                        continue;
                }

                body = true;
                if (words.Length == 1 && words[0].EndsWith(':'))
                {
                    string label = words[0][..^1];
                    if (!validToken(label, 64) || !labels.TryAdd(label, code.Count))
                        throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                }
                else
                {
                    if (code.Count == GameplaySkinScriptProgram.MAX_INSTRUCTIONS)
                        throw error(GameplaySkinScriptFaultCode.SourceLimit, line);
                    code.Add((words, line));
                }

                void declaration(int count)
                {
                    if (body || words.Length != count)
                        throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                }
            }

            if (!header || code.Count == 0)
                throw error(GameplaySkinScriptFaultCode.InvalidSource);

            var instructions = new ScriptInstruction[code.Count];
            for (int i = 0; i < code.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (string[] words, int line) = code[i];
                ScriptOperand zero = ScriptOperand.Literal(0);
                instructions[i] = words[0] switch
                {
                    "halt" => instruction(ScriptOpcode.Halt, 1),
                    "mov" => unary(ScriptOpcode.Move),
                    "add" => binary(ScriptOpcode.Add),
                    "sub" => binary(ScriptOpcode.Subtract),
                    "mul" => binary(ScriptOpcode.Multiply),
                    "div" => binary(ScriptOpcode.Divide),
                    "min" => binary(ScriptOpcode.Minimum),
                    "max" => binary(ScriptOpcode.Maximum),
                    "lt" => binary(ScriptOpcode.LessThan),
                    "eq" => binary(ScriptOpcode.Equal),
                    "clamp" => instruction(ScriptOpcode.Clamp, 5) with { Destination = register(1), Left = operand(2), Right = operand(3), Third = operand(4) },
                    "read" => instruction(ScriptOpcode.Read, 3) with { Destination = register(1), Argument = namedIndex(input_names, words[2], line) },
                    "random" => instruction(ScriptOpcode.Random, 2) with { Destination = register(1) },
                    "set" => instruction(ScriptOpcode.Set, 4) with { Argument = targetIndex(), Left = operand(3) },
                    "jump" => instruction(ScriptOpcode.Jump, 2) with { Argument = labelIndex(1) },
                    "when" => instruction(ScriptOpcode.When, 3) with { Left = operand(1), Argument = labelIndex(2) },
                    "load" => unary(ScriptOpcode.Load),
                    "store" => instruction(ScriptOpcode.Store, 3) with { Left = operand(1), Right = operand(2) },
                    "granted" => instruction(ScriptOpcode.Granted, 3) with { Destination = register(1), Argument = requestIndex() },
                    _ => throw error(GameplaySkinScriptFaultCode.InvalidSource, line),
                };

                ScriptInstruction instruction(ScriptOpcode opcode, int count)
                {
                    if (words.Length != count)
                        throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                    return new ScriptInstruction(opcode, 0, zero, zero, zero, 0, line);
                }

                ScriptInstruction unary(ScriptOpcode opcode) => instruction(opcode, 3) with { Destination = register(1), Left = operand(2) };
                ScriptInstruction binary(ScriptOpcode opcode) => instruction(opcode, 4) with { Destination = register(1), Left = operand(2), Right = operand(3) };

                int register(int position) => states.TryGetValue(words[position], out int index) ? index : throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                ScriptOperand operand(int position) => states.TryGetValue(words[position], out int index)
                    ? new ScriptOperand(index, 0) : ScriptOperand.Literal(number(words[position], line));
                int labelIndex(int position) => labels.TryGetValue(words[position], out int index) && index < code.Count
                    ? index : throw error(GameplaySkinScriptFaultCode.InvalidSource, line);

                int targetIndex()
                {
                    int index = targets.IndexOf(new ScriptTarget(words[1], parseProperty(words[2], line)));
                    return index >= 0 ? index : throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                }

                int requestIndex()
                {
                    int index = requests.FindIndex(r => r.Id == words[2]);
                    return index >= 0 ? index : throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
                }
            }

            return createVerified(requests.ToArray(), targets.ToArray(), initial.ToArray(), heapCells, instructions,
                Convert.ToHexString(SHA256.HashData(source.Span)), cancellationToken);
        }

        public static byte[] EncodeBytecode(GameplaySkinScriptProgram program)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, strict_utf8, true);
            writer.Write(magic);
            writer.Write(GameplaySkinScriptProgram.RUNTIME_VERSION);
            writer.Write(Convert.FromHexString(program.SourceDigest));
            writer.Write(program.RegisterCount);
            writer.Write(program.HeapCells);
            writer.Write(program.Requests.Count);
            writer.Write(program.Targets.Count);
            writer.Write(program.InstructionCount);
            foreach (ScriptCapabilityRequest request in program.Requests)
            {
                writeToken(request.Id);
                writer.Write((byte)request.Mode);
            }

            foreach (ScriptTarget target in program.Targets)
            {
                writeToken(target.NodeId);
                writer.Write((byte)target.Property);
            }

            for (int i = 0; i < program.RegisterCount; i++)
                writer.Write(program.InitialStateAt(i));

            for (int i = 0; i < program.InstructionCount; i++)
            {
                ScriptInstruction instruction = program.InstructionAt(i);
                writer.Write((byte)instruction.Opcode);
                writer.Write(instruction.Destination);
                writeOperand(instruction.Left);
                writeOperand(instruction.Right);
                writeOperand(instruction.Third);
                writer.Write(instruction.Argument);
                writer.Write(instruction.Line);
            }

            writer.Flush();
            return stream.ToArray();

            void writeToken(string value)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(value);
                writer.Write((byte)bytes.Length);
                writer.Write(bytes);
            }

            void writeOperand(ScriptOperand operand)
            {
                writer.Write(operand.Register);
                writer.Write(operand.Constant);
            }
        }

        public static GameplaySkinScriptProgram DecodeBytecode(ReadOnlyMemory<byte> bytecode, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bytecode.Length > GameplaySkinScriptProgram.MAX_BYTECODE_BYTES)
                throw error(GameplaySkinScriptFaultCode.SourceLimit);

            using var stream = new MemoryStream(bytecode.ToArray(), false);
            using var reader = new BinaryReader(stream, strict_utf8);
            try
            {
                if (!reader.ReadBytes(magic.Length).SequenceEqual(magic) || reader.ReadInt32() != GameplaySkinScriptProgram.RUNTIME_VERSION)
                    throw error(GameplaySkinScriptFaultCode.UnsupportedVersion);
                byte[] sourceDigest = reader.ReadBytes(32);
                if (sourceDigest.Length != 32)
                    throw error(GameplaySkinScriptFaultCode.InvalidBytecode);
                int registerCount = count(GameplaySkinScriptProgram.MAX_REGISTERS);
                int heapCells = count(GameplaySkinScriptProgram.MAX_HEAP_CELLS);
                int requestCount = count(GameplaySkinScriptProgram.MAX_REQUESTS);
                int targetCount = count(GameplaySkinScriptProgram.MAX_TARGETS);
                int instructionCount = count(GameplaySkinScriptProgram.MAX_INSTRUCTIONS);
                var requests = new ScriptCapabilityRequest[requestCount];
                var targets = new ScriptTarget[targetCount];
                double[] initial = new double[registerCount];
                var instructions = new ScriptInstruction[instructionCount];
                for (int i = 0; i < requestCount; i++)
                    requests[i] = new ScriptCapabilityRequest(readToken(96), (ScriptCapabilityMode)reader.ReadByte());
                for (int i = 0; i < targetCount; i++)
                    targets[i] = new ScriptTarget(readToken(128), (GameplaySkinScriptProperty)reader.ReadByte());
                for (int i = 0; i < registerCount; i++)
                    initial[i] = reader.ReadDouble();
                for (int i = 0; i < instructionCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    instructions[i] = new ScriptInstruction((ScriptOpcode)reader.ReadByte(), reader.ReadInt32(), readOperand(), readOperand(), readOperand(), reader.ReadInt32(), reader.ReadInt32());
                }

                if (stream.Position != stream.Length)
                    throw error(GameplaySkinScriptFaultCode.InvalidBytecode);
                return createVerified(requests, targets, initial, heapCells, instructions, Convert.ToHexString(sourceDigest), cancellationToken);
            }
            catch (EndOfStreamException)
            {
                throw error(GameplaySkinScriptFaultCode.InvalidBytecode);
            }

            int count(int maximum)
            {
                int result = reader.ReadInt32();
                if (result < 0 || result > maximum)
                    throw error(GameplaySkinScriptFaultCode.InvalidBytecode);
                return result;
            }

            string readToken(int maximum)
            {
                int length = reader.ReadByte();
                byte[] bytes = reader.ReadBytes(length);
                if (bytes.Length != length || bytes.Any(b => b > 127))
                    throw error(GameplaySkinScriptFaultCode.InvalidBytecode);
                string result = Encoding.ASCII.GetString(bytes);
                if (!validToken(result, maximum))
                    throw error(GameplaySkinScriptFaultCode.InvalidBytecode);
                return result;
            }

            ScriptOperand readOperand() => new ScriptOperand(reader.ReadInt32(), reader.ReadDouble());
        }

        private static GameplaySkinScriptProgram createVerified(ScriptCapabilityRequest[] requests, ScriptTarget[] targets, double[] initial,
                                                                 int heapCells, ScriptInstruction[] instructions, string sourceDigest, CancellationToken cancellationToken)
        {
            foreach (ScriptCapabilityRequest request in requests)
            {
                try
                {
                    GameplaySkinCapabilityId.ValidateToken(request.Id, nameof(requests));
                }
                catch (ArgumentException)
                {
                    throw error(GameplaySkinScriptFaultCode.InvalidBytecode);
                }
            }

            if (requests.Any(r => !Enum.IsDefined(r.Mode)) || requests.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != requests.Length
                                                       || targets.Any(t => !Enum.IsDefined(t.Property)) || targets.Distinct().Count() != targets.Length
                                                       || initial.Any(v => !double.IsFinite(v)) || instructions.Length == 0)
                throw error(GameplaySkinScriptFaultCode.InvalidBytecode);

            foreach (ScriptInstruction instruction in instructions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Enum.IsDefined(instruction.Opcode) || instruction.Line < 1 || instruction.Line > GameplaySkinScriptProgram.MAX_SOURCE_LINES)
                    throw error(GameplaySkinScriptFaultCode.InvalidBytecode);

                // Reserved fields have a fixed zero encoding in V1. Reject malformed payloads instead of silently
                // accepting instruction bytes which the source compiler cannot produce.
                bool hasDestination = instruction.Opcode is not (ScriptOpcode.Halt or ScriptOpcode.Set or ScriptOpcode.Jump or ScriptOpcode.When or ScriptOpcode.Store);
                bool hasLeft = instruction.Opcode is ScriptOpcode.Move or ScriptOpcode.Add or ScriptOpcode.Subtract or ScriptOpcode.Multiply
                    or ScriptOpcode.Divide or ScriptOpcode.Minimum or ScriptOpcode.Maximum or ScriptOpcode.LessThan or ScriptOpcode.Equal
                    or ScriptOpcode.Clamp or ScriptOpcode.Set or ScriptOpcode.When or ScriptOpcode.Load or ScriptOpcode.Store;
                bool hasRight = instruction.Opcode is ScriptOpcode.Add or ScriptOpcode.Subtract or ScriptOpcode.Multiply or ScriptOpcode.Divide
                    or ScriptOpcode.Minimum or ScriptOpcode.Maximum or ScriptOpcode.LessThan or ScriptOpcode.Equal or ScriptOpcode.Clamp or ScriptOpcode.Store;
                bool hasArgument = instruction.Opcode is ScriptOpcode.Read or ScriptOpcode.Set or ScriptOpcode.Jump or ScriptOpcode.When or ScriptOpcode.Granted;
                if ((!hasDestination && instruction.Destination != 0)
                    || (!hasLeft && !isUnused(instruction.Left)) || (!hasRight && !isUnused(instruction.Right))
                    || (instruction.Opcode != ScriptOpcode.Clamp && !isUnused(instruction.Third))
                    || (!hasArgument && instruction.Argument != 0))
                    invalid();

                switch (instruction.Opcode)
                {
                    case ScriptOpcode.Move:
                    case ScriptOpcode.Load:
                        destination();
                        operand(instruction.Left);
                        break;

                    case ScriptOpcode.Add:
                    case ScriptOpcode.Subtract:
                    case ScriptOpcode.Multiply:
                    case ScriptOpcode.Divide:
                    case ScriptOpcode.Minimum:
                    case ScriptOpcode.Maximum:
                    case ScriptOpcode.LessThan:
                    case ScriptOpcode.Equal:
                    case ScriptOpcode.Clamp:
                        destination();
                        operand(instruction.Left);
                        operand(instruction.Right);
                        if (instruction.Opcode == ScriptOpcode.Clamp)
                            operand(instruction.Third);
                        break;

                    case ScriptOpcode.Read:
                        destination();
                        if (!Enum.IsDefined((GameplaySkinScriptInput)instruction.Argument))
                            invalid();
                        capability(InputCapability((GameplaySkinScriptInput)instruction.Argument));
                        break;

                    case ScriptOpcode.Random:
                        destination();
                        capability(RANDOM_CAPABILITY);
                        break;

                    case ScriptOpcode.Set:
                        if ((uint)instruction.Argument >= targets.Length)
                            invalid();
                        operand(instruction.Left);
                        capability(SCENE_CAPABILITY);
                        break;

                    case ScriptOpcode.Jump:
                    case ScriptOpcode.When:
                        if ((uint)instruction.Argument >= instructions.Length)
                            invalid();
                        if (instruction.Opcode == ScriptOpcode.When)
                            operand(instruction.Left);
                        break;

                    case ScriptOpcode.Store:
                        operand(instruction.Left);
                        operand(instruction.Right);
                        break;

                    case ScriptOpcode.Granted:
                        destination();
                        if ((uint)instruction.Argument >= requests.Length)
                            invalid();
                        break;
                }

                void destination()
                {
                    if ((uint)instruction.Destination >= initial.Length)
                        invalid();
                }

                void operand(ScriptOperand value)
                {
                    if (value.Register < -1 || value.Register >= initial.Length || !double.IsFinite(value.Constant))
                        invalid();
                    if (value.Register >= 0 && BitConverter.DoubleToInt64Bits(value.Constant) != 0)
                        invalid();
                }

                void capability(string id)
                {
                    if (!requests.Any(r => r.Id == id && r.Mode != ScriptCapabilityMode.Deny))
                        throw error(GameplaySkinScriptFaultCode.PermissionDenied, instruction.Line);
                }

                void invalid() => throw error(GameplaySkinScriptFaultCode.InvalidBytecode, instruction.Line);
            }

            return new GameplaySkinScriptProgram(requests, targets, initial, heapCells, instructions, sourceDigest);
        }

        private static bool isUnused(ScriptOperand operand)
            => operand.Register == -1 && BitConverter.DoubleToInt64Bits(operand.Constant) == 0;

        public static string InputCapability(GameplaySkinScriptInput input)
            => input is GameplaySkinScriptInput.EventKind or GameplaySkinScriptInput.EventValue ? EVENT_CAPABILITY : SNAPSHOT_CAPABILITY;

        private static GameplaySkinScriptProperty parseProperty(string value, int line)
            => (GameplaySkinScriptProperty)namedIndex(property_names, value, line);

        private static int namedIndex(string[] names, string value, int line)
        {
            int result = Array.IndexOf(names, value);
            return result >= 0 ? result : throw error(GameplaySkinScriptFaultCode.InvalidSource, line);
        }

        private static double number(string value, int line)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) && double.IsFinite(result)
                ? result : throw error(GameplaySkinScriptFaultCode.InvalidSource, line);

        private static bool validToken(string value, int maximum)
        {
            if (value.Length == 0 || value.Length > maximum || value[0] is < 'a' or > 'z')
                return false;
            return value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_');
        }

        private static GameplaySkinScriptException error(GameplaySkinScriptFaultCode code, int line = 0) => new GameplaySkinScriptException(code, line);
    }
}
