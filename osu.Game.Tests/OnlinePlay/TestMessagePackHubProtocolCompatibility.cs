// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;

namespace osu.Game.Tests.OnlinePlay
{
    [TestFixture]
    public class TestMessagePackHubProtocolCompatibility
    {
        private const string invocation_target = "ApplyCompatibilityState";

        [Test]
        public void TestInvocationRoundTripPreservesModSettingsAndUnionState()
        {
            // Use the same serializer options registered by HubClientConnector, without creating a connection.
            var protocol = new MessagePackHubProtocol(Options.Create(new MessagePackHubProtocolOptions
            {
                SerializerOptions = SignalRUnionWorkaroundResolver.OPTIONS
            }));

            var mod = new APIMod
            {
                Acronym = "DT",
                Settings = new Dictionary<string, object>
                {
                    ["speed_change"] = 1.25,
                    ["adjust_pitch"] = false
                }
            };

            var outgoing = new InvocationMessage("compatibility-1", invocation_target, new object[]
            {
                mod,
                new TeamVersusUserState { TeamID = 2 }
            });

            var output = new ArrayBufferWriter<byte>();
            protocol.WriteMessage(outgoing, output);
            var input = new ReadOnlySequence<byte>(output.WrittenMemory);

            Assert.That(protocol.TryParseMessage(ref input, new CompatibilityInvocationBinder(), out var parsed), Is.True);
            Assert.That(input.IsEmpty, Is.True, "The complete protocol frame must be consumed.");

            if (parsed is InvocationBindingFailureMessage failure)
                Assert.Fail(failure.BindingFailure.SourceException.ToString());

            Assert.That(parsed, Is.TypeOf<InvocationMessage>());
            var incoming = (InvocationMessage)parsed!;
            Assert.That(incoming.InvocationId, Is.EqualTo(outgoing.InvocationId));
            Assert.That(incoming.Target, Is.EqualTo(invocation_target));
            Assert.That(incoming.Arguments, Has.Length.EqualTo(2));
            Assert.That(incoming.Arguments[0], Is.TypeOf<APIMod>());
            Assert.That(incoming.Arguments[1], Is.TypeOf<TeamVersusUserState>());

            var incomingMod = (APIMod)incoming.Arguments[0]!;
            Assert.That(incomingMod.Acronym, Is.EqualTo(mod.Acronym));
            Assert.That(incomingMod.Settings, Has.Count.EqualTo(2));
            Assert.That(incomingMod.Settings["speed_change"], Is.EqualTo(1.25));
            Assert.That(incomingMod.Settings["adjust_pitch"], Is.False);
            Assert.That(((TeamVersusUserState)incoming.Arguments[1]!).TeamID, Is.EqualTo(2));
        }

        private sealed class CompatibilityInvocationBinder : IInvocationBinder
        {
            public IReadOnlyList<Type> GetParameterTypes(string methodName)
                => methodName == invocation_target
                    ? new[] { typeof(APIMod), typeof(MatchUserState) }
                    : throw new InvalidOperationException($"Unexpected invocation target: {methodName}");

            public Type GetReturnType(string invocationId) => throw new InvalidOperationException("This invocation has no return value.");

            public Type GetStreamItemType(string streamId) => throw new InvalidOperationException("This invocation has no stream items.");
        }
    }
}
