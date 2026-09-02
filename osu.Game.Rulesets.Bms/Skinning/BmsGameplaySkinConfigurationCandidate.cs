// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// One ordered source layer in the BMS lane-resource compatibility plan.
    /// </summary>
    internal enum BmsGameplaySkinConfigurationCandidateSource
    {
        BmsRoleOverride = 0,
        ManiaFullVisualLane = 1,
        ManiaEightColumnDeck = 2,
        ManiaKeyOnly = 3,
        SelectedDocument = 4,
    }

    /// <summary>
    /// A source bucket declaration retained in the compatibility plan without choosing or validating a field value.
    /// </summary>
    internal sealed class BmsGameplaySkinConfigurationCandidate
    {
        public BmsGameplaySkinConfigurationCandidateSource Source { get; }

        public int? ManiaKeys { get; }

        public GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> Snapshot { get; }

        internal BmsGameplaySkinConfigurationCandidate(
            BmsGameplaySkinConfigurationCandidateSource source,
            int? maniaKeys,
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> snapshot)
        {
            bool isMania = source is BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane
                or BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck
                or BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly;

            if (isMania != maniaKeys.HasValue || maniaKeys <= 0)
            {
                if (isMania || maniaKeys.HasValue)
                    throw new ArgumentException("Only mania compatibility candidates must identify a positive Keys bucket.", nameof(maniaKeys));
            }

            if (!Enum.IsDefined(source))
                throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown BMS gameplay skin compatibility source.");

            Source = source;
            ManiaKeys = maniaKeys;
            Snapshot = snapshot;
        }

        /// <summary>
        /// Returns source/declaration state only and never includes resource values.
        /// </summary>
        public override string ToString() => ManiaKeys.HasValue
            ? $"{Source}:Keys{ManiaKeys}:{Snapshot}"
            : $"{Source}:{Snapshot}";
    }

    /// <summary>
    /// An immutable ordered candidate plan for BMS lane-resource configuration compatibility.
    /// </summary>
    /// <remarks>
    /// Candidate order is provider precedence. A declared snapshot still does not mean any particular field is present,
    /// valid or a slot <c>Provide</c>. Ruleset, canonical-package and programmatic fallback authorities are real
    /// providers in the final material resolver and are deliberately not represented by synthetic candidates here.
    /// </remarks>
    internal sealed class BmsGameplaySkinConfigurationCandidatePlan
    {
        public BmsKeymode Keymode { get; }

        public BmsPlayfieldStyle AppliedStyle { get; }

        public GameplaySkinLaneTopologySnapshot Topology { get; }

        public IReadOnlyList<BmsGameplaySkinConfigurationCandidate> Candidates { get; }

        internal BmsGameplaySkinConfigurationCandidatePlan(
            BmsKeymode keymode,
            BmsPlayfieldStyle appliedStyle,
            GameplaySkinLaneTopologySnapshot topology,
            BmsGameplaySkinConfigurationCandidate[] candidates)
        {
            ArgumentNullException.ThrowIfNull(topology);
            ArgumentNullException.ThrowIfNull(candidates);

            if (Array.Exists(candidates, candidate => candidate == null))
                throw new ArgumentException("A BMS compatibility plan cannot contain a null candidate.", nameof(candidates));

            (BmsGameplaySkinConfigurationCandidateSource[] expectedSources, int?[] expectedManiaKeys, int expectedLaneCount) =
                getExpectedShape(keymode);

            if (!candidates.Select(candidate => candidate.Source).SequenceEqual(expectedSources)
                || !candidates.Select(candidate => candidate.ManiaKeys).SequenceEqual(expectedManiaKeys))
                throw new ArgumentException("The candidate sequence does not match the canonical compatibility shape for this BMS keymode.", nameof(candidates));

            if (topology.LanesInLogicalOrder.Count != expectedLaneCount)
                throw new ArgumentException("The compatibility topology lane count does not match its BMS keymode.", nameof(topology));

            if (keymode is BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms or BmsKeymode.Key14K)
            {
                if (appliedStyle != BmsPlayfieldStyle.Center)
                    throw new ArgumentException("Nine-key and fourteen-key compatibility plans must use their resolved centre presentation.", nameof(appliedStyle));
            }
            else if (appliedStyle is not BmsPlayfieldStyle.P1
                     and not BmsPlayfieldStyle.P2
                     and not BmsPlayfieldStyle.Center
                     and not BmsPlayfieldStyle.CenterRightScratch)
            {
                throw new ArgumentException("The single-play compatibility plan uses an unknown presentation style.", nameof(appliedStyle));
            }

            foreach (BmsGameplaySkinConfigurationCandidate candidate in candidates)
            {
                if (candidate.Snapshot.IsDeclared && !ReferenceEquals(candidate.Snapshot.Value.Topology, topology))
                    throw new ArgumentException("Every declared candidate snapshot must target the plan's exact immutable topology.", nameof(candidates));
            }

            Keymode = keymode;
            AppliedStyle = appliedStyle;
            Topology = topology;
            Candidates = Array.AsReadOnly((BmsGameplaySkinConfigurationCandidate[])candidates.Clone());
        }

        private static (BmsGameplaySkinConfigurationCandidateSource[] Sources, int?[] ManiaKeys, int LaneCount) getExpectedShape(BmsKeymode keymode)
        {
            return keymode switch
            {
                BmsKeymode.Key5K => (
                    new[]
                    {
                        BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly,
                    },
                    new int?[] { null, 6, 5 },
                    6),
                BmsKeymode.Key7K => (
                    new[]
                    {
                        BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly,
                    },
                    new int?[] { null, 8, 7 },
                    8),
                BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms => (
                    new[]
                    {
                        BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                    },
                    new int?[] { null, 9 },
                    9),
                BmsKeymode.Key14K => (
                    new[]
                    {
                        BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck,
                        BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly,
                    },
                    new int?[] { null, 16, 8, 14 },
                    16),
                _ => throw new ArgumentOutOfRangeException(nameof(keymode), keymode, "Unsupported BMS keymode for a gameplay skin compatibility plan."),
            };
        }
    }
}
