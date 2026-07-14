// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Engine and host authority which a gameplay skin must never receive.
    /// </summary>
    /// <remarks>
    /// This process-local taxonomy and reserved-authority classifier are a second fail-closed barrier behind the closed capability
    /// allowlist. The listed IDs are not a list of requestable author features and do not define manifest tokens. Package-scoped
    /// resource reads are intentionally distinct from arbitrary filesystem access.
    /// </remarks>
    internal static class GameplaySkinCapabilityHardDenyCatalog
    {
        public static IReadOnlyList<GameplaySkinCapabilityId> All { get; } = Array.AsReadOnly(new[]
        {
            id("gameplay.mutation"),
            id("gameplay.input.inject"),
            id("gameplay.input.write"),
            id("gameplay.lane-order.write"),
            id("gameplay.lane-action.write"),
            id("gameplay.layout.write"),
            id("gameplay.judgement-line.write"),
            id("gameplay.lane-cover.write"),
            id("gameplay.scroll.write"),
            id("gameplay.timing.write"),
            id("gameplay.clock.write"),
            id("gameplay.judgement.write"),
            id("gameplay.score.write"),
            id("gameplay.combo.write"),
            id("gameplay.gauge.write"),
            id("gameplay.chart.write"),
            id("gameplay.beatmap.write"),
            id("gameplay.bga-timeline.write"),
            id("gameplay.bga-playback.write"),
            id("gameplay.bga-seek.write"),
            id("storage.realm.access"),
            id("storage.configuration.write"),
            id("host.network.request"),
            id("host.filesystem.arbitrary"),
            id("host.reflection"),
            id("host.process.spawn"),
            id("host.thread.create"),
            id("host.native-library.load"),
        });

        private static readonly HashSet<GameplaySkinCapabilityId> lookup = All.ToHashSet();

        public static bool IsHardDenied(GameplaySkinCapabilityId capabilityId)
        {
            ArgumentNullException.ThrowIfNull(capabilityId);

            string value = capabilityId.Value;

            return lookup.Contains(capabilityId)
                   || All.Any(reserved => isSameOrDescendant(value, reserved.Value))
                   || (isSameOrDescendant(value, "gameplay") && hasDeniedTerminalAction(value))
                   || isSameOrDescendant(value, "storage.realm")
                   || isSameOrDescendant(value, "storage.configuration")
                   || isSameOrDescendant(value, "host.network")
                   || isSameOrDescendant(value, "host.filesystem.arbitrary")
                   || isSameOrDescendant(value, "host.reflection")
                   || isSameOrDescendant(value, "host.process")
                   || isSameOrDescendant(value, "host.thread")
                   || isSameOrDescendant(value, "host.native-library");
        }

        private static GameplaySkinCapabilityId id(string value) => GameplaySkinCapabilityId.Create(value);

        private static bool isSameOrDescendant(string value, string reservedAuthority)
            => StringComparer.Ordinal.Equals(value, reservedAuthority)
               || value.StartsWith($"{reservedAuthority}.", StringComparison.Ordinal);

        private static bool hasDeniedTerminalAction(string value)
        {
            string terminalSegment = value[(value.LastIndexOf('.') + 1)..];
            string action = terminalSegment[(terminalSegment.LastIndexOf('-') + 1)..];

            return action is "write"
                or "inject"
                or "mutate"
                or "mutation"
                or "update"
                or "set"
                or "control"
                or "seek"
                or "reset"
                or "create"
                or "delete"
                or "remove"
                or "submit"
                or "apply"
                or "trigger"
                or "play"
                or "pause";
        }
    }
}
