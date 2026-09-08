// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinScriptVmTest
    {
        private const string stateful_source = """
            oms-script 1
            required gameplay.snapshot.read
            required gameplay.events.read
            required scene.numeric.write
            optional math.random.read
            state energy 0
            state sample 0
            state allowed 0
            heap 4
            target node.glow alpha
            read sample event-value
            mul sample sample 0.1
            add energy energy sample
            mul energy energy 0.8
            granted allowed math.random.read
            when allowed sparkle
            jump render
            sparkle:
            random sample
            mul sample sample 0.01
            add energy energy sample
            render:
            store 0 energy
            load sample 0
            clamp sample sample 0 1
            set node.glow alpha sample
            halt
            """;

        [Test]
        public void TestAuthorSourceProducesStatefulNumericOutputAndDeterministicReset()
        {
            var program = compile(stateful_source);
            var vm = new GameplaySkinScriptInstance(program, 892);
            var host = new Host();
            var first = new List<double>();
            for (int frame = 0; frame < 20; frame++)
            {
                Assert.That(vm.Execute(host, frame), Is.True);
                first.Add(host.LastValue);
            }

            Assert.That(first.Distinct().Count(), Is.GreaterThan(1));
            vm.Reset(892);
            for (int frame = 0; frame < 20; frame++)
            {
                Assert.That(vm.Execute(host, frame + 20), Is.True);
                Assert.That(host.LastValue, Is.EqualTo(first[frame]));
            }

            Assert.Multiple(() =>
            {
                Assert.That(host.LastNode, Is.EqualTo("node.glow"));
                Assert.That(vm.Profiler.Callbacks, Is.EqualTo(40));
                Assert.That(vm.Profiler.Instructions, Is.GreaterThan(40));
                Assert.That(vm.Profiler.ElapsedTicks, Is.GreaterThan(0));
                Assert.That(vm.Profiler.HeapBytes, Is.EqualTo(7 * sizeof(double)));
            });
        }

        [Test]
        public void TestSourceAndBytecodeAreDeterministicAndMapped()
        {
            GameplaySkinScriptProgram first = compile(stateful_source);
            byte[] bytecode = GameplaySkinScriptCompiler.EncodeBytecode(first);
            GameplaySkinScriptProgram decoded = GameplaySkinScriptCompiler.DecodeBytecode(bytecode);
            Assert.Multiple(() =>
            {
                Assert.That(decoded.Fingerprint, Is.EqualTo(first.Fingerprint));
                Assert.That(decoded.SourceMap, Is.EqualTo(first.SourceMap));
                Assert.That(decoded.Requests, Is.EqualTo(first.Requests));
                Assert.That(decoded.Targets, Is.EqualTo(first.Targets));
                Assert.That(GameplaySkinScriptCompiler.EncodeBytecode(decoded), Is.EqualTo(bytecode));
                Assert.That(GameplaySkinScriptCompiler.EncodeBytecode(compile(stateful_source)), Is.EqualTo(bytecode));
                Assert.That(compile(stateful_source + "\n# new source revision").Fingerprint, Is.Not.EqualTo(first.Fingerprint));
            });
        }

        [Test]
        public void TestRequiredDenialMakesNoHostReadOrWrite()
        {
            var vm = new GameplaySkinScriptInstance(compile(stateful_source), 1);
            var host = new Host { Granted = false };
            Assert.That(vm.Execute(host, 0), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(vm.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.PermissionDenied));
                Assert.That(host.Reads, Is.Zero);
                Assert.That(host.Writes, Is.Zero);
            });
        }

        [Test]
        public void TestOptionalDeniedRequestCanBeQueriedAndSkipped()
        {
            var vm = new GameplaySkinScriptInstance(compile("""
                oms-script 1
                optional math.random.read
                state available 0
                granted available math.random.read
                when available use-random
                halt
                use-random:
                random available
                halt
                """), 1);
            Assert.That(vm.Execute(new Host { Granted = false }, 0), Is.True);
            Assert.That(vm.Fault, Is.Null);
        }

        [Test]
        public void TestExplicitDenyCannotBecomeGrantedEvenIfHostClaimsSupport()
        {
            var vm = new GameplaySkinScriptInstance(compile("""
                oms-script 1
                deny math.random.read
                state available 0
                granted available math.random.read
                when available fail
                halt
                fail:
                div available 1 0
                halt
                """), 1);
            Assert.That(vm.Execute(new Host(), 0), Is.True);
        }

        [Test]
        public void TestRevocationBetweenActualApiCallsStopsExistingInstance()
        {
            var vm = new GameplaySkinScriptInstance(compile("""
                oms-script 1
                required scene.numeric.write
                target node.glow alpha
                set node.glow alpha 0.3
                set node.glow alpha 0.9
                halt
                """), 1);
            var host = new Host { RevokeAfterWrite = true };
            Assert.That(vm.Execute(host, 0), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(host.Writes, Is.EqualTo(1));
                Assert.That(host.LastValue, Is.EqualTo(0.3));
                Assert.That(vm.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.PermissionDenied));
                Assert.That(vm.Fault.Line, Is.EqualTo(5));
            });
            host.Granted = true;
            vm.Reset(1);
            Assert.That(vm.Execute(host, 1), Is.False, "Only a newly negotiated instance may regain authority.");
        }

        [Test]
        public void TestInfiniteLoopIsPreemptedAtInstructionBoundary()
        {
            var vm = new GameplaySkinScriptInstance(compile("oms-script 1\nloop:\njump loop\n"), 1);
            Assert.That(vm.Execute(new Host(), 0), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(vm.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.InstructionLimit));
                Assert.That(vm.Fault.Line, Is.EqualTo(3));
                Assert.That(vm.Profiler.Instructions, Is.EqualTo(GameplaySkinScriptInstance.MAX_CALLBACK_INSTRUCTIONS));
            });
        }

        [Test]
        public void TestPerFrameLimitIncludesAllCallbacksAndEngineReset()
        {
            var vm = new GameplaySkinScriptInstance(compile("""
                oms-script 1
                state count 0
                state again 0
                mov count 0
                loop:
                add count count 1
                lt again count 1000
                when again loop
                halt
                """), 1);
            var host = new Host();
            for (int i = 0; i < 5; i++)
            {
                Assert.That(vm.Execute(host, 0), Is.True);
                vm.Reset(1);
            }

            Assert.That(vm.Execute(host, 0), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(vm.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.FrameInstructionLimit));
                Assert.That(vm.Profiler.Instructions, Is.EqualTo(GameplaySkinScriptInstance.MAX_FRAME_INSTRUCTIONS));
            });
        }

        [TestCase("-1")]
        [TestCase("2")]
        [TestCase("0.5")]
        [TestCase("1e100")]
        public void TestDynamicHeapIndexIsCheckedBeforeMemoryAccess(string index)
        {
            var vm = new GameplaySkinScriptInstance(compile($"oms-script 1\nheap 2\nstore {index} 1\nhalt\n"), 1);
            Assert.That(vm.Execute(new Host(), 0), Is.False);
            Assert.That(vm.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.HeapLimit));
        }

        [Test]
        public void TestNodeWritesAreBoundedBeforeHostCall()
        {
            var vm = new GameplaySkinScriptInstance(compile("""
                oms-script 1
                required scene.numeric.write
                target node.glow alpha
                loop:
                set node.glow alpha 1
                jump loop
                """), 1);
            var host = new Host();
            Assert.That(vm.Execute(host, 0), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(vm.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.NodeLimit));
                Assert.That(host.Writes, Is.EqualTo(GameplaySkinScriptInstance.MAX_FRAME_WRITES));
            });
        }

        [Test]
        public void TestNonFiniteArithmeticAndHostBoundaryFailureFuseOnlyInstance()
        {
            var arithmetic = new GameplaySkinScriptInstance(compile("oms-script 1\nstate v 0\ndiv v 1 0\nhalt\n"), 1);
            Assert.That(arithmetic.Execute(new Host(), 0), Is.False);
            Assert.That(arithmetic.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.NonFiniteNumber));
            var first = new GameplaySkinScriptInstance(compile(stateful_source), 1);
            var second = new GameplaySkinScriptInstance(compile(stateful_source), 1);
            Assert.That(first.Execute(new Host { FailWrite = true }, 0), Is.False);
            Assert.That(first.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.NodeLimit));
            Assert.That(second.Execute(new Host(), 0), Is.True);
        }

        [Test]
        public void TestCancellationAndDisableCannotRunAnotherInstruction()
        {
            var vm = new GameplaySkinScriptInstance(compile(stateful_source), 1);
            var host = new Host();
            Assert.That(vm.Execute(host, 0, new CancellationToken(true)), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(vm.Fault!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.Cancelled));
                Assert.That(vm.Profiler.Instructions, Is.Zero);
                Assert.That(host.Reads, Is.Zero);
                Assert.That(host.Writes, Is.Zero);
            });
            var disabled = new GameplaySkinScriptInstance(compile(stateful_source), 1);
            disabled.Disable();
            Assert.That(disabled.Execute(host, 0), Is.False);
            Assert.That(disabled.Profiler.Callbacks, Is.Zero);
        }

        [TestCase("oms-script 2\nhalt\n", (int)GameplaySkinScriptFaultCode.UnsupportedVersion)]
        [TestCase("oms-script 1\nheap 513\nhalt\n", (int)GameplaySkinScriptFaultCode.HeapLimit)]
        [TestCase("oms-script 1\nstate a 0\nread a combo\nhalt\n", (int)GameplaySkinScriptFaultCode.PermissionDenied)]
        [TestCase("oms-script 1\nstate a 0\nread a network\nhalt\n", (int)GameplaySkinScriptFaultCode.InvalidSource)]
        [TestCase("oms-script 1\nstate a 0\nreflection a\nhalt\n", (int)GameplaySkinScriptFaultCode.InvalidSource)]
        [TestCase("oms-script 1\nstate a 0\njump absent\nhalt\n", (int)GameplaySkinScriptFaultCode.InvalidSource)]
        [TestCase("oms-script 1\nstate a NaN\nhalt\n", (int)GameplaySkinScriptFaultCode.InvalidSource)]
        public void TestUntrustedSourceRejectedWithStableDiagnostic(string source, int expected)
        {
            GameplaySkinScriptException exception = Assert.Throws<GameplaySkinScriptException>(() => compile(source))!;
            Assert.That((int)exception.Code, Is.EqualTo(expected));
            Assert.That(exception.Message, Does.Not.Contain(source));
        }

        [TestCase("gameplay..read")]
        [TestCase("gameplay.read_")]
        public void TestMalformedSourceCapabilityPreservesItsDeclarationLine(string capability)
        {
            GameplaySkinScriptException exception = Assert.Throws<GameplaySkinScriptException>(() => compile($"oms-script 1\nrequired {capability}\nhalt\n"))!;
            Assert.That(exception.Code, Is.EqualTo(GameplaySkinScriptFaultCode.InvalidSource));
            Assert.That(exception.Line, Is.EqualTo(2));
            Assert.That(exception.Message, Does.Not.Contain(capability));
        }

        [Test]
        public void TestSourceMemoryInstructionAndUtf8Budgets()
        {
            string registers = "oms-script 1\n" + string.Concat(Enumerable.Range(0, 129).Select(i => $"state v{i} 0\n")) + "halt\n";
            Assert.That(Assert.Throws<GameplaySkinScriptException>(() => compile(registers))!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.HeapLimit));
            string instructions = "oms-script 1\n" + string.Concat(Enumerable.Repeat("halt\n", GameplaySkinScriptProgram.MAX_INSTRUCTIONS + 1));
            Assert.That(Assert.Throws<GameplaySkinScriptException>(() => compile(instructions))!.Code, Is.EqualTo(GameplaySkinScriptFaultCode.SourceLimit));
            Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.Compile(new byte[GameplaySkinScriptProgram.MAX_SOURCE_BYTES + 1]));
            Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.Compile(new byte[] { 0xFF, 0xFE }));
        }

        [Test]
        public void TestMalformedBytecodeCannotBypassVerifier()
        {
            // V1 header is 64 bytes; each numeric instruction is exactly 49 bytes, little endian.
            byte[] valid = GameplaySkinScriptCompiler.EncodeBytecode(compile("oms-script 1\nloop:\njump loop\n"));
            Assert.That(valid.Length, Is.EqualTo(113));
            foreach ((int offset, int value) in new[] { (44, -1), (44, 129), (48, 513), (52, 33), (56, 129), (60, 2049), (105, 2), (109, 0) })
            {
                byte[] malformed = valid.ToArray();
                BitConverter.GetBytes(value).CopyTo(malformed, offset);
                Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.DecodeBytecode(malformed), $"offset {offset}");
            }

            byte[] unknownOpcode = valid.ToArray();
            unknownOpcode[64] = 255;
            Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.DecodeBytecode(unknownOpcode));
            Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.DecodeBytecode(valid.AsMemory(0, valid.Length - 1)));
            Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.DecodeBytecode(valid.Concat(new byte[] { 0 }).ToArray()));
            byte[] futureRuntime = valid.ToArray();
            futureRuntime[8] = 2;
            Assert.That(Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.DecodeBytecode(futureRuntime))!.Code,
                Is.EqualTo(GameplaySkinScriptFaultCode.UnsupportedVersion));
        }

        [Test]
        public void TestBytecodeMutationFixturesAlwaysRejectOrRemainBounded()
        {
            byte[] bytecode = GameplaySkinScriptCompiler.EncodeBytecode(compile(stateful_source));
            var random = new Random(712);
            for (int i = 0; i < 500; i++)
            {
                byte[] mutation = bytecode.ToArray();
                mutation[random.Next(mutation.Length)] ^= (byte)(1 << random.Next(8));
                try
                {
                    var program = GameplaySkinScriptCompiler.DecodeBytecode(mutation);
                    var vm = new GameplaySkinScriptInstance(program, 1);
                    vm.Execute(new Host(), i);
                    Assert.That(vm.Profiler.Instructions, Is.LessThanOrEqualTo(GameplaySkinScriptInstance.MAX_CALLBACK_INSTRUCTIONS));
                    Assert.That(vm.Profiler.HeapBytes, Is.LessThanOrEqualTo((GameplaySkinScriptProgram.MAX_HEAP_CELLS + GameplaySkinScriptProgram.MAX_REGISTERS) * sizeof(double)));
                }
                catch (GameplaySkinScriptException)
                {
                    // A mutated numeric literal may still be a valid program. Rejection is required only for malformed instructions.
                }
            }
        }

        [Test]
        public void TestReservedBytecodeFieldsCannotCarryHiddenVersionedOperands()
        {
            byte[] halt = GameplaySkinScriptCompiler.EncodeBytecode(compile("oms-script 1\nhalt\n"));
            // A halt has no destination, argument or operands. V1 must reject data in every reserved field.
            foreach (int offset in new[] { 65, 69, 73, 81, 85, 93, 97, 105 })
            {
                byte[] mutation = halt.ToArray();
                mutation[offset] ^= 1;
                GameplaySkinScriptException exception = Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.DecodeBytecode(mutation))!;
                Assert.That(exception.Code, Is.EqualTo(GameplaySkinScriptFaultCode.InvalidBytecode));
                Assert.That(exception.Line, Is.EqualTo(2));
            }

            byte[] move = GameplaySkinScriptCompiler.EncodeBytecode(compile("oms-script 1\nstate value 0\nmov value value\nhalt\n"));
            // The unused immediate in a register operand must also be canonical, rather than an ignored covert payload.
            BitConverter.GetBytes(double.NaN).CopyTo(move, 64 + 8 + 1 + 4 + 4);
            Assert.Throws<GameplaySkinScriptException>(() => GameplaySkinScriptCompiler.DecodeBytecode(move));
        }

        [Test]
        public void TestCompilerAndDecoderObserveCancellationBeforeAllocation()
        {
            Assert.Throws<OperationCanceledException>(() => GameplaySkinScriptCompiler.Compile(Encoding.UTF8.GetBytes(stateful_source), new CancellationToken(true)));
            byte[] bytecode = GameplaySkinScriptCompiler.EncodeBytecode(compile(stateful_source));
            Assert.Throws<OperationCanceledException>(() => GameplaySkinScriptCompiler.DecodeBytecode(bytecode, new CancellationToken(true)));
        }

        private static GameplaySkinScriptProgram compile(string source) => GameplaySkinScriptCompiler.Compile(Encoding.UTF8.GetBytes(source));

        private sealed class Host : IGameplaySkinScriptHost
        {
            public bool Granted = true;
            public bool RevokeAfterWrite;
            public bool FailWrite;
            public int Reads;
            public int Writes;
            public double LastValue;
            public string? LastNode;

            public bool IsGranted(string capabilityId) => Granted;

            public double Read(GameplaySkinScriptInput input)
            {
                Reads++;
                return 1;
            }

            public void Set(string nodeId, GameplaySkinScriptProperty property, double value)
            {
                if (FailWrite)
                    throw new GameplaySkinScriptException(GameplaySkinScriptFaultCode.NodeLimit);
                Writes++;
                LastNode = nodeId;
                LastValue = value;
                if (RevokeAfterWrite)
                    Granted = false;
            }
        }
    }
}
