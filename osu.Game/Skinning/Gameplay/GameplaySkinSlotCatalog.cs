// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The SV1-1 ruleset-neutral semantic taxonomy of gameplay skin slot families.
    /// </summary>
    /// <remarks>
    /// Catalog order is deterministic for diagnostics and fixtures, but has no render, z-order or provider precedence meaning.
    /// Per-lane and per-ruleset context is supplied by a separate lookup object. Cardinality, scene host shape and author manifest mapping
    /// remain later contracts; these descriptors do not map one-to-one to current legacy lookups.
    /// </remarks>
    public static class GameplaySkinSlotCatalog
    {
        // Minimum playable layer. LaneSurface is the composite readability fallback for lane background, boundary and scratch role;
        // legacy adapters may need multiple existing resources to fulfil it.
        public static GameplaySkinSlotDescriptor LaneSurface { get; } = critical("playfield.lane-surface");
        public static GameplaySkinSlotDescriptor JudgementLine { get; } = critical("playfield.judgement-line");
        public static GameplaySkinSlotDescriptor Note { get; } = critical("object.note");
        public static GameplaySkinSlotDescriptor LongNoteHead { get; } = critical("object.long-note.head");
        public static GameplaySkinSlotDescriptor LongNoteBody { get; } = critical("object.long-note.body");
        public static GameplaySkinSlotDescriptor Mine { get; } = critical("object.mine");
        // Only requested while lane cover is active. This is the concrete fill inside an engine-enforced geometry/clip host;
        // a skin never owns or changes the actual covered area.
        public static GameplaySkinSlotDescriptor LaneCoverFill { get; } = critical("playfield.lane-cover.fill");

        // Optional presentation. Gameplay state continues to exist when any of these slots is suppressed.
        public static GameplaySkinSlotDescriptor LongNoteTail { get; } = optional("object.long-note.tail");
        public static GameplaySkinSlotDescriptor KeyVisual { get; } = optional("playfield.key");
        public static GameplaySkinSlotDescriptor KeyFlash { get; } = optional("effect.key-flash");
        public static GameplaySkinSlotDescriptor HitExplosion { get; } = optional("effect.hit-explosion");
        public static GameplaySkinSlotDescriptor JudgementDisplay { get; } = optional("hud.judgement");
        public static GameplaySkinSlotDescriptor ComboDisplay { get; } = optional("hud.combo");
        public static GameplaySkinSlotDescriptor GaugeVisual { get; } = optional("hud.gauge");
        public static GameplaySkinSlotDescriptor TextHud { get; } = optional("hud.text");
        public static GameplaySkinSlotDescriptor BarLine { get; } = optional("playfield.bar-line");
        public static GameplaySkinSlotDescriptor StageBackground { get; } = optional("stage.background");
        public static GameplaySkinSlotDescriptor StageForeground { get; } = optional("stage.foreground");
        public static GameplaySkinSlotDescriptor PlayfieldBackdrop { get; } = optional("playfield.backdrop");
        public static GameplaySkinSlotDescriptor PlayfieldBaseplate { get; } = optional("playfield.baseplate");
        public static GameplaySkinSlotDescriptor LaneCoverDecoration { get; } = optional("playfield.lane-cover.decoration");
        public static GameplaySkinSlotDescriptor Turntable { get; } = optional("playfield.turntable");
        public static GameplaySkinSlotDescriptor Laser { get; } = optional("playfield.laser");
        // Presentation of an engine-owned read-only content surface only; it never owns a BGA player, timeline or clock.
        public static GameplaySkinSlotDescriptor BgaViewport { get; } = optional("bga.viewport");
        public static GameplaySkinSlotDescriptor BgaFrame { get; } = optional("bga.frame");
        public static GameplaySkinSlotDescriptor Decoration { get; } = optional("decoration");

        private static readonly IReadOnlyList<GameplaySkinSlotDescriptor> all = Array.AsReadOnly(new[]
        {
            LaneSurface,
            JudgementLine,
            Note,
            LongNoteHead,
            LongNoteBody,
            Mine,
            LaneCoverFill,
            LongNoteTail,
            KeyVisual,
            KeyFlash,
            HitExplosion,
            JudgementDisplay,
            ComboDisplay,
            GaugeVisual,
            TextHud,
            BarLine,
            StageBackground,
            StageForeground,
            PlayfieldBackdrop,
            PlayfieldBaseplate,
            LaneCoverDecoration,
            Turntable,
            Laser,
            BgaViewport,
            BgaFrame,
            Decoration,
        });

        private static readonly IReadOnlyDictionary<string, GameplaySkinSlotDescriptor> by_id =
            all.ToDictionary(slot => slot.Id, StringComparer.Ordinal);

        /// <summary>
        /// All known slot descriptors.
        /// </summary>
        public static IReadOnlyList<GameplaySkinSlotDescriptor> All => all;

        /// <summary>
        /// Looks up an exact stable ID using ordinal, case-sensitive comparison.
        /// </summary>
        /// <remarks>
        /// Unknown or malformed IDs return <see langword="false"/>. A parser must diagnose that result rather than treating
        /// an unknown slot as optional or dynamically registering it.
        /// </remarks>
        public static bool TryGet(string? id, [NotNullWhen(true)] out GameplaySkinSlotDescriptor? descriptor)
        {
            if (id != null && by_id.TryGetValue(id, out GameplaySkinSlotDescriptor? found))
            {
                descriptor = found;
                return true;
            }

            descriptor = null;
            return false;
        }

        private static GameplaySkinSlotDescriptor critical(string id) => new GameplaySkinSlotDescriptor(id, SkinSlotRequirement.Critical);

        private static GameplaySkinSlotDescriptor optional(string id) => new GameplaySkinSlotDescriptor(id, SkinSlotRequirement.Optional);
    }
}
