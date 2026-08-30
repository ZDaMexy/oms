// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// The production mania adapter around the one ruleset-neutral immutable layout snapshot.
    /// </summary>
    internal sealed class ManiaGameplaySkinLayout : IGameplaySkinLayoutAdapter
    {
        public const string PLAYFIELD_SURFACE = "mania.playfield";
        public const string BARLINE_SURFACE = "mania.barline";
        public const string HIT_TARGET_SURFACE = "mania.hit-target";
        public const string JUDGEMENT_SURFACE = "mania.judgement";
        public const string HUD_SURFACE = "mania.hud";
        public const string GAUGE_SURFACE = "mania.gauge";
        public const string COMBO_SURFACE = "mania.combo";

        public GameplaySkinLayoutSnapshot Snapshot { get; }

        private ManiaGameplaySkinLayout(GameplaySkinLayoutSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public static GameplaySkinLayoutPublication PrepareAndPublish(
            ManiaBeatmap beatmap,
            ISkinSource skin,
            GameplaySkinLayoutRevisionOwner owner,
            GameHost host,
            GameplaySkinScrollDirection direction)
            => TryPrepareAndPublish(beatmap, skin, owner, host, direction, out GameplaySkinLayoutPublication? publication)
                ? publication!
                : throw new InvalidOperationException("The exact mania package/layout pair could not be published.");

        public static bool TryPrepareAndPublish(
            ManiaBeatmap beatmap,
            ISkinSource skin,
            GameplaySkinLayoutRevisionOwner owner,
            GameHost host,
            GameplaySkinScrollDirection direction,
            [NotNullWhen(true)] out GameplaySkinLayoutPublication? publication)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            ArgumentNullException.ThrowIfNull(skin);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(host);

            if (owner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility
                && owner.CurrentPublication != null)
            {
                throw new InvalidOperationException("An exact mania gameplay root may publish exactly one immutable layout.");
            }

            GameplaySkinPreparedLayout prepared;

            try
            {
                prepared = owner.PreparePublication(layoutRevision =>
                {
                    // Stage vector, topology, environment and skin geometry are captured and solved only after the
                    // exact owner has acquired its fresh work lease and participant-generation barrier.
                    var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
                    ManiaGameplaySkinLaneTopologyPublication topologyPublication = topologyOwner.Publish(beatmap);
                    ManiaGameplaySkinLayoutEnvironment environment = ManiaGameplaySkinLayoutEnvironment.FromHost(host);
                    return GameplaySkinLayoutPublication.Create(new ManiaGameplaySkinLayout(
                        ManiaGameplaySkinLayoutSolver.Solve(
                            topologyPublication,
                            skin,
                            owner.PackageRevision,
                            layoutRevision,
                            environment,
                            direction)));
                });
            }
            catch (GameplaySkinLayoutParticipantBarrierChangedException)
            {
                publication = null;
                return false;
            }

            using (prepared)
            {
                if (!owner.TryCommit(prepared) || owner.CurrentPublication == null)
                {
                    publication = null;
                    return false;
                }
            }

            publication = owner.CurrentPublication;
            return true;
        }

        public static ManiaGameplaySkinLayout CreateCompatibility(
            IEnumerable<StageDefinition> stageDefinitions,
            ISkin skin,
            GameplaySkinScrollDirection direction = GameplaySkinScrollDirection.Down,
            bool useSkinGeometry = true)
        {
            ArgumentNullException.ThrowIfNull(stageDefinitions);
            ArgumentNullException.ThrowIfNull(skin);
            StageDefinition[] copiedStages = stageDefinitions.ToArray();

            if (copiedStages.Length is < 1 or > 2 || copiedStages.Any(stage => stage == null))
                throw new ArgumentException("Compatibility mania layout requires one or two valid stages.", nameof(stageDefinitions));

            var beatmap = new ManiaBeatmap(new StageDefinition(copiedStages[0].Columns));

            foreach (StageDefinition stage in copiedStages.Skip(1))
                beatmap.Stages.Add(new StageDefinition(stage.Columns));

            if (!useSkinGeometry)
                skin = GeometrySuppressedSkinSource.Instance;

            GameplaySkinLayoutRevisionOwner owner = GameplaySkinLayoutRevisionOwner.CreateCompatibility();

            using GameplaySkinPreparedLayout prepared = owner.PreparePublication(layoutRevision =>
            {
                var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
                ManiaGameplaySkinLaneTopologyPublication topologyPublication = topologyOwner.Publish(beatmap);
                return GameplaySkinLayoutPublication.Create(new ManiaGameplaySkinLayout(
                    ManiaGameplaySkinLayoutSolver.Solve(
                        topologyPublication,
                        skin,
                        owner.PackageRevision,
                        layoutRevision,
                        ManiaGameplaySkinLayoutEnvironment.CreateCompatibility(),
                        direction)));
            });

            if (!owner.TryCommit(prepared) || owner.CurrentPublication == null)
                throw new InvalidOperationException("A compatibility mania layout could not be published.");

            return owner.CurrentPublication.GetAdapter<ManiaGameplaySkinLayout>();
        }

        public override string ToString() => nameof(ManiaGameplaySkinLayout);

        internal static void ValidateConsumerCarrier(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinLayoutRevisionOwner? owner,
            string consumer)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (owner == null)
            {
                throw new InvalidOperationException(
                    $"The mania {consumer} requires an exact provider owner or an explicitly cached compatibility owner.");
            }

            if (owner.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility)
            {
                // A compatibility owner is an explicit opt-in marker for detached solver/visual fixtures, not a
                // production lifetime authority. Such fixtures may compose independently produced compatibility
                // snapshots under one test dependency scope. Exact roots never take this branch and are reference-
                // checked against their sole committed publication below.
                if (snapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                    throw new InvalidOperationException($"The mania {consumer} cannot attach an exact snapshot to a compatibility owner.");

                return;
            }

            if (!ReferenceEquals(owner.CurrentPublication?.Snapshot, snapshot)
                || !ReferenceEquals(snapshot.Context.PackageRevision, owner.PackageRevision))
            {
                throw new InvalidOperationException($"The mania {consumer} did not retain its owner's exact committed layout publication.");
            }
        }

        private sealed class GeometrySuppressedSkinSource : ISkinSource
        {
            public static readonly GeometrySuppressedSkinSource Instance = new GeometrySuppressedSkinSource();

            public event Action SourceChanged
            {
                add { }
                remove { }
            }

            public IEnumerable<ISkin> AllSources => new[] { this };

            public ISkin? FindProvider(Func<ISkin, bool> lookupFunction) => lookupFunction(this) ? this : null;

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
                => null;
        }
    }

    internal readonly struct ManiaGameplaySkinLayoutEnvironment
    {
        public GameplaySkinLayoutRect ScreenBounds { get; }

        public GameplaySkinLayoutRect SafeBounds { get; }

        public float AspectRatio { get; }

        public float DpiScale { get; }

        public bool UsedFallback { get; }

        public ManiaGameplaySkinLayoutEnvironment(
            GameplaySkinLayoutRect screenBounds,
            GameplaySkinLayoutRect safeBounds,
            float aspectRatio,
            float dpiScale,
            bool usedFallback = false)
        {
            if (!screenBounds.Contains(safeBounds))
                throw new ArgumentException("Mania safe bounds must be contained by screen bounds.", nameof(safeBounds));

            if (!float.IsFinite(aspectRatio) || aspectRatio <= 0)
                throw new ArgumentOutOfRangeException(nameof(aspectRatio));

            if (!float.IsFinite(dpiScale) || dpiScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(dpiScale));

            ScreenBounds = screenBounds;
            SafeBounds = safeBounds;
            AspectRatio = aspectRatio;
            DpiScale = dpiScale;
            UsedFallback = usedFallback;
        }

        public static ManiaGameplaySkinLayoutEnvironment FromHost(GameHost host)
        {
            if (host.Window == null || host.Window.ClientSize.Width <= 0 || host.Window.ClientSize.Height <= 0)
                return fallback();

            float width = host.Window.ClientSize.Width;
            float height = host.Window.ClientSize.Height;
            MarginPadding padding = host.Window.SafeAreaPadding.Value;

            float left = Math.Clamp(padding.Left / width, 0, 0.45f);
            float right = Math.Clamp(padding.Right / width, 0, 0.45f);
            float top = Math.Clamp(padding.Top / height, 0, 0.45f);
            float bottom = Math.Clamp(padding.Bottom / height, 0, 0.45f);
            float safeWidth = 1 - left - right;
            float safeHeight = 1 - top - bottom;

            float dpiScale = host.Window.Scale;

            if (!float.IsFinite(safeWidth) || safeWidth <= 0
                || !float.IsFinite(safeHeight) || safeHeight <= 0
                || !float.IsFinite(dpiScale) || dpiScale <= 0)
            {
                return fallback();
            }

            return new ManiaGameplaySkinLayoutEnvironment(
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(left, top, safeWidth, safeHeight),
                width / height,
                dpiScale);

            static ManiaGameplaySkinLayoutEnvironment fallback()
                => new ManiaGameplaySkinLayoutEnvironment(
                    GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                    GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                    4f / 3f,
                    1,
                    usedFallback: true);
        }

        public static ManiaGameplaySkinLayoutEnvironment CreateCompatibility()
            => new ManiaGameplaySkinLayoutEnvironment(
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                4f / 3f,
                1,
                usedFallback: true);
    }

    internal static class ManiaGameplaySkinLayoutSolver
    {
        private const float reference_height = 768;
        private const float default_column_width = 80;
        private const float default_special_column_width = 70;
        private const float default_column_spacing = 1;
        private const float default_hit_position = 110;
        private const float default_barline_height = 1;
        private const float maximum_field_width_fraction = 0.72f;
        private const float dual_stage_gap = 32;

        public static GameplaySkinLayoutSnapshot Solve(
            ManiaGameplaySkinLaneTopologyPublication topologyPublication,
            ISkin skin,
            GameplaySkinPackageRevision packageRevision,
            long layoutRevision,
            ManiaGameplaySkinLayoutEnvironment environment,
            GameplaySkinScrollDirection direction)
        {
            ArgumentNullException.ThrowIfNull(topologyPublication);
            ArgumentNullException.ThrowIfNull(skin);
            ArgumentNullException.ThrowIfNull(packageRevision);

            GameplaySkinLaneTopologySnapshot topology = topologyPublication.Publication.Topology;
            var diagnostics = new List<GameplaySkinLayoutDiagnostic>();

            if (environment.UsedFallback)
                diagnostics.Add(new GameplaySkinLayoutDiagnostic("mania.layout.environment-fallback"));

            float hitPosition = readField(
                skin,
                LegacyManiaSkinConfigurationLookups.HitPosition,
                null,
                default_hit_position,
                1,
                320,
                "mania.layout.hit-position-fallback",
                diagnostics);
            float paddingTop = readField(
                skin,
                LegacyManiaSkinConfigurationLookups.StagePaddingTop,
                null,
                0,
                0,
                240,
                "mania.layout.stage-padding-top-fallback",
                diagnostics);
            float paddingBottom = readField(
                skin,
                LegacyManiaSkinConfigurationLookups.StagePaddingBottom,
                null,
                0,
                0,
                240,
                "mania.layout.stage-padding-bottom-fallback",
                diagnostics);
            float barlineHeight = readField(
                skin,
                LegacyManiaSkinConfigurationLookups.BarLineHeight,
                null,
                default_barline_height,
                0.1f,
                32,
                "mania.layout.barline-height-fallback",
                diagnostics);
            float comboPosition = readField(
                skin,
                LegacyManiaSkinConfigurationLookups.ComboPosition,
                null,
                200,
                0,
                reference_height,
                "mania.layout.combo-position-fallback",
                diagnostics);

            var stageInputs = new List<StageInput>(topologyPublication.StageColumnCounts.Count);

            for (int stageIndex = 0; stageIndex < topologyPublication.StageColumnCounts.Count; stageIndex++)
            {
                int count = topologyPublication.StageColumnCounts[stageIndex];
                GameplaySkinLaneTopologyGroup topologyGroup = topology.GroupsInLogicalOrder[stageIndex];

                if (topologyGroup.LogicalIndex != stageIndex
                    || topologyGroup.LanesInLogicalOrder.Count != count)
                {
                    throw new InvalidOperationException("The mania topology group is not coherent with its ordered native stage vector.");
                }

                float[] widths = new float[count];
                float[] leftSpacings = new float[count];
                float[] rightSpacings = new float[count];

                for (int localIndex = 0; localIndex < count; localIndex++)
                {
                    GameplaySkinLaneTopologyEntry topologyLane = topologyGroup.LanesInLogicalOrder[localIndex];

                    if (topologyLane.GroupLocalLogicalIndex != localIndex)
                        throw new InvalidOperationException("The mania topology group-local index is not coherent with its ordered stage vector.");

                    int globalLogicalIndex = topologyLane.GlobalLogicalIndex;
                    float fallbackWidth = topologyLane.Identity.Role == GameplaySkinLaneRole.SpecialKey
                        ? default_special_column_width
                        : default_column_width;
                    widths[localIndex] = readField(
                        skin,
                        LegacyManiaSkinConfigurationLookups.ColumnWidth,
                        globalLogicalIndex,
                        fallbackWidth,
                        8,
                        240,
                        "mania.layout.column-width-fallback",
                        diagnostics);
                    leftSpacings[localIndex] = readField(
                        skin,
                        LegacyManiaSkinConfigurationLookups.LeftColumnSpacing,
                        globalLogicalIndex,
                        default_column_spacing,
                        0,
                        64,
                        "mania.layout.column-left-spacing-fallback",
                        diagnostics);
                    rightSpacings[localIndex] = readField(
                        skin,
                        LegacyManiaSkinConfigurationLookups.RightColumnSpacing,
                        globalLogicalIndex,
                        default_column_spacing,
                        0,
                        64,
                        "mania.layout.column-right-spacing-fallback",
                        diagnostics);
                }

                stageInputs.Add(new StageInput(topologyGroup, widths, leftSpacings, rightSpacings));
            }

            float rawCombinedWidth = stageInputs.Sum(stage => stage.TotalWidth)
                                     + (stageInputs.Count - 1) * dual_stage_gap;
            float aspectReferenceWidth = reference_height * environment.AspectRatio;
            float dpiAdjustment = Math.Clamp(MathF.Sqrt(environment.DpiScale), 0.75f, 1.5f);
            float desiredScale = environment.SafeBounds.Width / aspectReferenceWidth * dpiAdjustment;
            float fitScale = environment.SafeBounds.Width * maximum_field_width_fraction / rawCombinedWidth;
            float scale = Math.Min(desiredScale, fitScale);
            float combinedWidth = rawCombinedWidth * scale;
            float currentX = environment.SafeBounds.Left + (environment.SafeBounds.Width - combinedWidth) / 2;

            float hudHeight = environment.SafeBounds.Height * 0.08f;
            float verticalUnit = (environment.SafeBounds.Height - hudHeight) / reference_height;
            float fieldTop = environment.SafeBounds.Top + hudHeight + paddingTop * verticalUnit;
            float fieldBottom = environment.SafeBounds.Bottom - paddingBottom * verticalUnit;
            float fieldHeight = fieldBottom - fieldTop;

            // The per-field ranges above make this relationship invalid only after an unusually small safe area. Keep
            // fallback deterministic and complete rather than constructing a mixed or negative snapshot.
            if (!float.IsFinite(fieldHeight) || fieldHeight <= environment.SafeBounds.Height * 0.1f)
            {
                diagnostics.Add(new GameplaySkinLayoutDiagnostic("mania.layout.stage-height-fallback"));
                fieldTop = environment.SafeBounds.Top + hudHeight;
                fieldBottom = environment.SafeBounds.Bottom;
                fieldHeight = fieldBottom - fieldTop;
            }

            var groups = new List<GameplaySkinLayoutGroup>(stageInputs.Count);
            var lanesByLogicalIndex = new GameplaySkinLayoutLane[topology.LanesInLogicalOrder.Count];

            for (int stageIndex = 0; stageIndex < stageInputs.Count; stageIndex++)
            {
                StageInput input = stageInputs[stageIndex];
                GameplaySkinLaneTopologyGroup topologyGroup = input.TopologyGroup;

                if (topologyGroup.LogicalIndex != stageIndex)
                    throw new InvalidOperationException("The mania layout input lost its explicit topology group identity.");

                float stageWidth = input.TotalWidth * scale;
                var groupRect = GameplaySkinLayoutRect.Create(currentX, fieldTop, stageWidth, fieldHeight);
                groups.Add(new GameplaySkinLayoutGroup(topologyGroup, groupRect));

                float laneX = currentX;

                foreach (GameplaySkinLaneTopologyEntry topologyLane in topologyGroup.LanesInLogicalOrder)
                {
                    int localIndex = topologyLane.GroupLocalLogicalIndex;
                    laneX += input.LeftSpacings[localIndex] * scale;
                    float laneWidth = input.Widths[localIndex] * scale;
                    lanesByLogicalIndex[topologyLane.GlobalLogicalIndex] = new GameplaySkinLayoutLane(
                        topologyLane,
                        GameplaySkinLayoutRect.Create(laneX, fieldTop, laneWidth, fieldHeight));
                    laneX += laneWidth + input.RightSpacings[localIndex] * scale;
                }

                currentX += stageWidth + dual_stage_gap * scale;
            }

            GameplaySkinLayoutRect playfieldRect = GameplaySkinLayoutRect.Union(groups.Select(group => group.Rect));
            float lineHeight = Math.Max(environment.SafeBounds.Height / 768, 0.001f);
            float barlineSurfaceHeight = barlineHeight * verticalUnit;
            float hitY = direction == GameplaySkinScrollDirection.Down
                ? fieldBottom - hitPosition * verticalUnit
                : fieldTop + hitPosition * verticalUnit;
            hitY = Math.Clamp(hitY, fieldTop, fieldBottom - lineHeight);
            var hitTargetRect = GameplaySkinLayoutRect.Create(playfieldRect.Left, hitY, playfieldRect.Width, lineHeight);
            float judgementHeight = Math.Min(fieldHeight * 0.18f, environment.SafeBounds.Height * 0.16f);
            float judgementY = direction == GameplaySkinScrollDirection.Down
                ? Math.Max(fieldTop, hitY - judgementHeight)
                : Math.Min(fieldBottom - judgementHeight, hitY + lineHeight);
            float comboHeight = Math.Min(environment.SafeBounds.Height * 0.12f, hudHeight);
            float comboCenterY = environment.SafeBounds.Top + comboPosition / reference_height * environment.SafeBounds.Height;
            float comboTop = Math.Clamp(
                comboCenterY - comboHeight / 2,
                environment.SafeBounds.Top,
                environment.SafeBounds.Bottom - comboHeight);

            GameplaySkinLayoutContext context = GameplaySkinLayoutContext.Create(
                "mania",
                $"stages-{string.Join("-", topologyPublication.StageColumnCounts)}",
                string.Join("-", topologyPublication.StageColumnCounts.Select(count => $"{count}k")),
                topologyPublication.StageColumnCounts.Count == 1 ? "mania-single" : "mania-dual",
                topology,
                environment.ScreenBounds,
                environment.SafeBounds,
                environment.AspectRatio,
                environment.DpiScale,
                direction,
                packageRevision,
                topologyRevision: topologyPublication.Publication.Revision,
                layoutRevision);

            return GameplaySkinLayoutSnapshot.Create(
                context,
                groups,
                lanesByLogicalIndex,
                new[]
                {
                    new GameplaySkinLayoutSurface(ManiaGameplaySkinLayout.PLAYFIELD_SURFACE, playfieldRect, 10, true, true),
                    new GameplaySkinLayoutSurface(ManiaGameplaySkinLayout.BARLINE_SURFACE,
                        GameplaySkinLayoutRect.Create(playfieldRect.Left, fieldTop, playfieldRect.Width, barlineSurfaceHeight), 20, true, false),
                    new GameplaySkinLayoutSurface(ManiaGameplaySkinLayout.HIT_TARGET_SURFACE, hitTargetRect, 40, true, false),
                    new GameplaySkinLayoutSurface(ManiaGameplaySkinLayout.JUDGEMENT_SURFACE,
                        GameplaySkinLayoutRect.Create(playfieldRect.Left, judgementY, playfieldRect.Width, judgementHeight), 50, false, false),
                    new GameplaySkinLayoutSurface(ManiaGameplaySkinLayout.HUD_SURFACE,
                        GameplaySkinLayoutRect.Create(environment.SafeBounds.Left, environment.SafeBounds.Top, environment.SafeBounds.Width, hudHeight), 60, false, false),
                    new GameplaySkinLayoutSurface(ManiaGameplaySkinLayout.GAUGE_SURFACE,
                        GameplaySkinLayoutRect.Create(environment.SafeBounds.Left, environment.SafeBounds.Top, environment.SafeBounds.Width * 0.25f, hudHeight), 61, false, false),
                    new GameplaySkinLayoutSurface(ManiaGameplaySkinLayout.COMBO_SURFACE,
                        GameplaySkinLayoutRect.Create(environment.SafeBounds.Left + environment.SafeBounds.Width * 0.375f,
                            comboTop, environment.SafeBounds.Width * 0.25f, comboHeight), 62, false, false),
                },
                diagnostics: diagnostics);
        }

        private static float readField(
            ISkin skin,
            LegacyManiaSkinConfigurationLookups lookup,
            int? globalColumnIndex,
            float fallback,
            float minimum,
            float maximum,
            string diagnostic,
            ICollection<GameplaySkinLayoutDiagnostic> diagnostics)
        {
            float? candidate = skin.GetConfig<ManiaSkinConfigurationLookup, float>(new ManiaSkinConfigurationLookup(lookup, globalColumnIndex))?.Value;

            if (candidate == null)
                return fallback;

            if (!float.IsFinite(candidate.Value) || candidate.Value < minimum || candidate.Value > maximum)
            {
                diagnostics.Add(new GameplaySkinLayoutDiagnostic(diagnostic));
                return fallback;
            }

            return candidate.Value;
        }

        private sealed class StageInput
        {
            public GameplaySkinLaneTopologyGroup TopologyGroup { get; }

            public float[] Widths { get; }

            public float[] LeftSpacings { get; }

            public float[] RightSpacings { get; }

            public float TotalWidth { get; }

            public StageInput(
                GameplaySkinLaneTopologyGroup topologyGroup,
                float[] widths,
                float[] leftSpacings,
                float[] rightSpacings)
            {
                TopologyGroup = topologyGroup ?? throw new ArgumentNullException(nameof(topologyGroup));
                Widths = widths;
                LeftSpacings = leftSpacings;
                RightSpacings = rightSpacings;
                TotalWidth = widths.Sum() + leftSpacings.Sum() + rightSpacings.Sum();
            }
        }
    }

    /// <summary>
    /// Exact stage-local projection of the one gameplay layout snapshot.
    /// </summary>
    internal sealed class ManiaGameplaySkinStageContext
    {
        public GameplaySkinLayoutSnapshot Snapshot { get; }

        public GameplaySkinLayoutGroup Group { get; }

        public ManiaGameplaySkinStageContext(GameplaySkinLayoutSnapshot snapshot, GameplaySkinLaneTopologyGroup group)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Group = snapshot.GetGroup((group ?? throw new ArgumentNullException(nameof(group))).Identity.Id);

            if (!ReferenceEquals(Group.TopologyGroup, group))
                throw new InvalidOperationException("The mania stage context did not retain the exact topology group reference.");
        }
    }

    /// <summary>
    /// Exact lane-local projection of the one gameplay layout snapshot. This is a reference carrier, not geometry.
    /// </summary>
    internal sealed class ManiaGameplaySkinLaneContext
    {
        public GameplaySkinLayoutSnapshot Snapshot { get; }

        public GameplaySkinLayoutLane Lane { get; }

        public ManiaGameplaySkinLaneContext(GameplaySkinLayoutSnapshot snapshot, int globalLogicalIndex)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            GameplaySkinLaneTopologyEntry topologyLane = snapshot.Context.Topology.LanesInLogicalOrder[globalLogicalIndex];

            if (topologyLane.GlobalLogicalIndex != globalLogicalIndex)
                throw new InvalidOperationException("The mania lane context did not resolve the explicit global logical index.");

            Lane = snapshot.GetLane(topologyLane.Identity.Id);
        }
    }

    internal static class ManiaGameplaySkinLayoutProjection
    {
        public static float GetBarLineBaseHeight(ManiaGameplaySkinStageContext stageContext)
        {
            ArgumentNullException.ThrowIfNull(stageContext);
            GameplaySkinLayoutRect safeBounds = stageContext.Snapshot.Context.SafeBounds;
            GameplaySkinLayoutRect barline = stageContext.Snapshot.GetSurface(ManiaGameplaySkinLayout.BARLINE_SURFACE).Rect;
            GameplaySkinLayoutRect hud = stageContext.Snapshot.GetSurface(ManiaGameplaySkinLayout.HUD_SURFACE).Rect;
            return barline.Height / (safeBounds.Height - hud.Height) * 768;
        }

        public static float GetHitTargetInsetFraction(ManiaGameplaySkinStageContext stageContext)
        {
            ArgumentNullException.ThrowIfNull(stageContext);
            GameplaySkinLayoutRect stage = stageContext.Group.Rect;
            GameplaySkinLayoutRect target = stageContext.Snapshot.GetSurface(ManiaGameplaySkinLayout.HIT_TARGET_SURFACE).Rect;
            return stageContext.Snapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? (target.Bottom - stage.Top) / stage.Height
                : (stage.Bottom - target.Top) / stage.Height;
        }

        public static void ApplyJudgementPlacement(Drawable drawable, ManiaGameplaySkinStageContext stageContext)
        {
            ArgumentNullException.ThrowIfNull(drawable);
            ArgumentNullException.ThrowIfNull(stageContext);

            GameplaySkinLayoutRect group = stageContext.Group.Rect;
            GameplaySkinLayoutRect judgement = stageContext.Snapshot.GetSurface(ManiaGameplaySkinLayout.JUDGEMENT_SURFACE).Rect;
            drawable.Anchor = Anchor.TopLeft;
            drawable.Origin = Anchor.Centre;
            drawable.RelativePositionAxes = Axes.Both;
            drawable.Position = new Vector2(
                0.5f,
                (judgement.Top + judgement.Height / 2 - group.Top) / group.Height);
        }
    }
}
