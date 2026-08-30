// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// The BMS-native view of one lane in the exact neutral gameplay layout snapshot.
    /// </summary>
    public sealed class BmsGameplayLayoutLane
    {
        public GameplaySkinLayoutLane NeutralLane { get; }

        public GameplaySkinLaneId LaneId => NeutralLane.LaneId;

        public int LogicalIndex => NeutralLane.TopologyEntry.GlobalLogicalIndex;

        public int VisualIndex => NeutralLane.TopologyEntry.GlobalVisualIndex;

        public int GroupLogicalIndex { get; }

        public int GroupLocalLogicalIndex => NeutralLane.TopologyEntry.GroupLocalLogicalIndex;

        public int GroupLocalVisualIndex => NeutralLane.TopologyEntry.GroupLocalVisualIndex;

        public BmsAction Action { get; }

        public bool IsScratch => NeutralLane.TopologyEntry.Identity.Role == GameplaySkinLaneRole.Scratch;

        internal BmsGameplayLayoutLane(GameplaySkinLayoutLane neutralLane, BmsAction action, int groupLogicalIndex)
        {
            NeutralLane = neutralLane ?? throw new ArgumentNullException(nameof(neutralLane));
            Action = action;
            GroupLogicalIndex = groupLogicalIndex;
        }
    }

    /// <summary>
    /// The one immutable BMS gameplay layout publication consumed by a complete gameplay tree.
    /// </summary>
    /// <remarks>
    /// This is an adapter over the exact ruleset-neutral snapshot, not a second geometry model. The internal profile and
    /// lane-layout views expose only metrics already validated by the one solver; neither can be independently constructed
    /// on a production path.
    /// </remarks>
    public sealed class BmsGameplayLayoutSnapshot : IGameplaySkinLayoutAdapter
    {
        private readonly BmsGameplayLayoutLane[] lanes;
        private readonly Dictionary<GameplaySkinLaneId, BmsGameplayLayoutLane> lanesById;

        public GameplaySkinLayoutSnapshot Neutral { get; }

        GameplaySkinLayoutSnapshot IGameplaySkinLayoutAdapter.Snapshot => Neutral;

        public GameplaySkinLayoutContext Context => Neutral.Context;

        public BmsKeymode Keymode { get; }

        public BmsPlayfieldStyle Style { get; }

        public BmsKeymodeResolution KeymodeResolution { get; }

        public BmsKeymodeResolutionSource KeymodeSource => KeymodeResolution.Source;

        public BmsKeymodeEvidence KeymodeEvidence => KeymodeResolution.Evidence;

        public string KeymodeDiagnostic => KeymodeResolution.StableDiagnostic;

        /// <summary>
        /// Validated pixel metrics retained inside the solved snapshot. This is not an independently constructible
        /// renderer geometry authority.
        /// </summary>
        internal BmsPlayfieldLayoutProfile Profile { get; }

        internal BmsLaneLayout LaneLayout { get; }

        public IReadOnlyList<BmsGameplayLayoutLane> LanesInLogicalOrder { get; }

        public GameplaySkinLayoutRect PlayfieldRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.Playfield).Rect;

        public GameplaySkinLayoutRect HitTargetRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.HitTarget).Rect;

        public GameplaySkinLayoutRect JudgementLineRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.JudgementLine).Rect;

        public GameplaySkinLayoutRect JudgementRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.Judgement).Rect;

        public GameplaySkinLayoutRect LaneCoverRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.LaneCover).Rect;

        public GameplaySkinLayoutRect PreStartPreviewRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.PreStartPreview).Rect;

        public GameplaySkinLayoutRect GaugeRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.Gauge).Rect;

        public GameplaySkinLayoutRect ComboRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.Combo).Rect;

        public GameplaySkinLayoutRect HudRect => Neutral.GetSurface(BmsGameplayLayoutSurfaceIds.Hud).Rect;

        public IReadOnlyList<GameplaySkinLayoutRect> BgaViewports => Neutral.BgaViewports;

        /// <summary>
        /// Projects one already-validated profile-local vertical metric into the exact playfield-relative coordinate
        /// system. The solved hit-target surface is the scale carrier, so DPI/safe-frame fallback can never diverge
        /// between neutral geometry and a renderer which consumes a profile appearance metric.
        /// </summary>
        internal float ProjectVerticalProfileMetric(float metric)
        {
            if (!float.IsFinite(metric) || metric < 0)
                throw new ArgumentOutOfRangeException(nameof(metric));

            return metric / Profile.HitTargetHeight * HitTargetRect.Height / PlayfieldRect.Height;
        }

        internal BmsGameplayLayoutSnapshot(
            GameplaySkinLayoutSnapshot neutral,
            BmsKeymodeResolution keymodeResolution,
            BmsPlayfieldStyle style,
            BmsPlayfieldLayoutProfile profile,
            BmsLaneLayout laneLayout,
            IEnumerable<BmsAction> laneActions)
        {
            Neutral = neutral ?? throw new ArgumentNullException(nameof(neutral));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            LaneLayout = laneLayout ?? throw new ArgumentNullException(nameof(laneLayout));
            KeymodeResolution = keymodeResolution ?? throw new ArgumentNullException(nameof(keymodeResolution));
            Keymode = keymodeResolution.Keymode;
            Style = style;

            BmsAction[] copiedActions = laneActions?.ToArray() ?? throw new ArgumentNullException(nameof(laneActions));

            if (copiedActions.Length != neutral.LanesInLogicalOrder.Count)
                throw new ArgumentException("Every exact neutral lane must have one BMS action.", nameof(laneActions));

            Dictionary<GameplaySkinLaneGroupId, int> groupLogicalIndices = neutral.Context.Topology.GroupsInLogicalOrder
                                                                                 .ToDictionary(group => group.Identity.Id, group => group.LogicalIndex);
            lanes = neutral.LanesInLogicalOrder
                           .Select((lane, index) => new BmsGameplayLayoutLane(
                               lane,
                               copiedActions[index],
                               groupLogicalIndices[lane.TopologyEntry.Identity.Group.Id]))
                           .ToArray();
            LanesInLogicalOrder = Array.AsReadOnly(lanes);
            lanesById = lanes.ToDictionary(lane => lane.LaneId);
        }

        public BmsGameplayLayoutLane GetLaneByLogicalIndex(int logicalIndex)
        {
            if ((uint)logicalIndex >= (uint)lanes.Length)
                throw new ArgumentOutOfRangeException(nameof(logicalIndex), logicalIndex, "Lane index is not part of the exact BMS layout snapshot.");

            return lanes[logicalIndex];
        }

        public BmsGameplayLayoutLane GetLane(GameplaySkinLaneId laneId)
        {
            ArgumentNullException.ThrowIfNull(laneId);

            return lanesById.TryGetValue(laneId, out BmsGameplayLayoutLane? lane)
                ? lane
                : throw new KeyNotFoundException("Lane identity is not part of the exact BMS layout snapshot.");
        }

        public override string ToString() => $"BmsGameplayLayout:{Keymode}:{Style}:Revision{Context.LayoutRevision}";
    }

    public static class BmsGameplayLayoutSurfaceIds
    {
        public const string Playfield = "bms.playfield";
        public const string HitTarget = "bms.hit-target";
        public const string JudgementLine = "bms.judgement-line";
        public const string Judgement = "bms.judgement";
        public const string LaneCover = "bms.lane-cover";
        public const string PreStartPreview = "bms.pre-start-preview";
        public const string BgaPrefix = "bms.bga-";
        public const string Hud = "bms.hud";
        public const string Gauge = "bms.gauge";
        public const string Combo = "bms.combo";
    }
}
