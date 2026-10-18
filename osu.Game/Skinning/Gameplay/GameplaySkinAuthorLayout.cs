// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>A horizontal span within an authored stage. A span is geometry, never an input lane identity.</summary>
    public readonly record struct GameplaySkinAuthorLaneSpan(double X, double Width);

    /// <summary>
    /// Complete geometry for one existing native stage. Lane/key entries use the native group-local logical order.
    /// Uncovered space is intentionally empty and cannot create an input, object, or event target.
    /// </summary>
    public sealed class GameplaySkinAuthorLayoutStage
    {
        public GameplaySkinLayoutRect Rect { get; }
        public IReadOnlyList<GameplaySkinAuthorLaneSpan> Lanes { get; }
        public IReadOnlyList<GameplaySkinAuthorLaneSpan> Keys { get; }
        public bool HasExplicitKeys { get; }

        public GameplaySkinAuthorLayoutStage(
            GameplaySkinLayoutRect rect,
            IReadOnlyList<GameplaySkinAuthorLaneSpan> lanes,
            IReadOnlyList<GameplaySkinAuthorLaneSpan>? keys = null)
        {
            ArgumentNullException.ThrowIfNull(lanes);
            Rect = rect;
            Lanes = Array.AsReadOnly(lanes.ToArray());
            HasExplicitKeys = keys != null;
            Keys = keys == null ? Lanes : Array.AsReadOnly(keys.ToArray());
        }
    }

    /// <summary>
    /// A bounded static input to the existing ruleset layout solver, selected by exact native context. Rectangles
    /// are relative to the safe screen; spans are relative to the complete stage. Vertical band heights use the
    /// safe-screen height and HitPosition uses the stage height. No game-specific cabinet is named by the engine.
    /// </summary>
    public sealed class GameplaySkinAuthorLayout
    {
        public const int MAX_PROFILES = 32;
        public const int MAX_STAGES = 2;
        public const int MAX_LANES_PER_STAGE = 10;

        private const double epsilon = 0.000001;

        public string Id { get; }
        public string RulesetId { get; }
        public string KeymodeId { get; }
        public string Presentation { get; }
        public IReadOnlyList<GameplaySkinAuthorLayoutStage> Stages { get; }
        public double KeyAreaHeight { get; }
        public double GaugeHeight { get; }
        public double HitPosition { get; }

        public GameplaySkinAuthorLayout(
            string id,
            string rulesetId,
            string keymodeId,
            string presentation,
            IReadOnlyList<GameplaySkinAuthorLayoutStage> stages,
            double keyAreaHeight = 0,
            double gaugeHeight = 0,
            double hitPosition = 1)
        {
            ArgumentNullException.ThrowIfNull(stages);
            if (!IsToken(id) || rulesetId is not ("bms" or "mania") || !IsToken(keymodeId) || !IsToken(presentation)
                || stages.Count is < 1 or > MAX_STAGES
                || !double.IsFinite(keyAreaHeight) || keyAreaHeight is < 0 or > 0.18
                || !double.IsFinite(gaugeHeight) || gaugeHeight is < 0 or > 0.25
                || !double.IsFinite(hitPosition) || hitPosition is < 0 or > 1)
            {
                throw new ArgumentException("Invalid author layout declaration.");
            }

            Id = id;
            RulesetId = rulesetId;
            KeymodeId = keymodeId;
            Presentation = presentation;
            Stages = Array.AsReadOnly(stages.ToArray());
            KeyAreaHeight = keyAreaHeight;
            GaugeHeight = gaugeHeight;
            HitPosition = hitPosition;

            for (int stageIndex = 0; stageIndex < Stages.Count; stageIndex++)
            {
                GameplaySkinAuthorLayoutStage stage = Stages[stageIndex]
                    ?? throw new ArgumentException("An author stage cannot be null.");
                GameplaySkinLayoutRect rect = stage.Rect;
                if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height)
                    || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                    || rect.Right > 1 + epsilon || rect.Bottom + keyAreaHeight + gaugeHeight + (gaugeHeight > 0 ? 0.006 : 0) > 1 + epsilon
                    || stage.Lanes.Count is < 1 or > MAX_LANES_PER_STAGE || stage.Keys.Count != stage.Lanes.Count)
                {
                    throw new ArgumentException("An author stage exceeds its bounded safe geometry.");
                }

                // Existing single/dual ruleset surfaces have one vertical scroll span and hit baseline.
                if (stageIndex > 0 && (Math.Abs(rect.Top - Stages[0].Rect.Top) > epsilon
                                      || Math.Abs(rect.Height - Stages[0].Rect.Height) > epsilon
                                      || rect.Left < Stages[stageIndex - 1].Rect.Right - epsilon))
                {
                    throw new ArgumentException("Author stages must retain disjoint ordered stages and a common vertical span.");
                }

                validateSpans(stage.Lanes, rect, allowOutsideStage: false);
                validateSpans(stage.Keys, rect, allowOutsideStage: true);
            }

            // Keys may extend outwards from a stage, but cannot overlap another stage's keys.
            for (int i = 0; i < Stages.Count; i++)
                for (int j = i + 1; j < Stages.Count; j++)
                    foreach (GameplaySkinAuthorLaneSpan first in Stages[i].Keys)
                        foreach (GameplaySkinAuthorLaneSpan second in Stages[j].Keys)
                        {
                            double firstLeft = Stages[i].Rect.Left + first.X * Stages[i].Rect.Width;
                            double secondLeft = Stages[j].Rect.Left + second.X * Stages[j].Rect.Width;
                            if (firstLeft < secondLeft + second.Width * Stages[j].Rect.Width - epsilon
                                && secondLeft < firstLeft + first.Width * Stages[i].Rect.Width - epsilon)
                            {
                                throw new ArgumentException("Author key surfaces cannot overlap across stages.");
                            }
                        }
        }

        public bool Matches(string rulesetId, string keymodeId, string presentation)
            => RulesetId == rulesetId && KeymodeId == keymodeId && (Presentation == presentation || Presentation == "any");

        public static GameplaySkinAuthorLayout? Select(
            IReadOnlyList<GameplaySkinAuthorLayout> layouts, string rulesetId, string keymodeId, string presentation)
            => layouts.FirstOrDefault(layout => layout.RulesetId == rulesetId && layout.KeymodeId == keymodeId && layout.Presentation == presentation)
               ?? layouts.FirstOrDefault(layout => layout.RulesetId == rulesetId && layout.KeymodeId == keymodeId && layout.Presentation == "any");

        /// <summary>Validate against the already-established native topology, without inventing or renumbering it.</summary>
        public bool FitsTopology(GameplaySkinLaneTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(topology);
            if (topology.GroupsInLogicalOrder.Count != Stages.Count)
                return false;

            for (int i = 0; i < Stages.Count; i++)
            {
                GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[i];
                if (group.LogicalIndex != i || group.LanesInLogicalOrder.Count != Stages[i].Lanes.Count)
                    return false;

                GameplaySkinLaneTopologyEntry[] visual = group.LanesInVisualOrder.ToArray();
                for (int j = 1; j < visual.Length; j++)
                {
                    GameplaySkinAuthorLaneSpan previous = Stages[i].Lanes[visual[j - 1].GroupLocalLogicalIndex];
                    GameplaySkinAuthorLaneSpan next = Stages[i].Lanes[visual[j].GroupLocalLogicalIndex];
                    if (previous.X + previous.Width > next.X + epsilon)
                        return false;
                }
            }

            return true;
        }

        internal static bool IsToken(string? value)
            => value is { Length: > 0 and <= GameplaySkinSceneBudgets.MAX_STABLE_ID_LENGTH }
               && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_');

        private static void validateSpans(IReadOnlyList<GameplaySkinAuthorLaneSpan> spans, GameplaySkinLayoutRect stage, bool allowOutsideStage)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                GameplaySkinAuthorLaneSpan span = spans[i];
                double left = stage.Left + span.X * stage.Width;
                double right = left + span.Width * stage.Width;
                if (!double.IsFinite(span.X) || !double.IsFinite(span.Width) || span.Width <= 0 || span.Width > 2
                    || left < -epsilon || right > 1 + epsilon
                    || !allowOutsideStage && (span.X < 0 || span.X + span.Width > 1 + epsilon))
                {
                    throw new ArgumentException("An author lane/key span exceeds its bounded safe geometry.");
                }

                for (int j = 0; j < i; j++)
                {
                    GameplaySkinAuthorLaneSpan other = spans[j];
                    if (span.X < other.X + other.Width - epsilon && other.X < span.X + span.Width - epsilon)
                        throw new ArgumentException("Author lane/key spans cannot overlap.");
                }
            }
        }
    }
}
