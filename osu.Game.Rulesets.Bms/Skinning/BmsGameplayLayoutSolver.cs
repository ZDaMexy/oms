// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Exact screen frame used by the BMS adapter. Values are full-screen relative coordinates.
    /// </summary>
    public readonly struct BmsGameplayLayoutEnvironment
    {
        private readonly GameplaySkinLayoutDiagnostic[] diagnostics;

        public GameplaySkinLayoutRect ScreenBounds { get; }

        public GameplaySkinLayoutRect SafeBounds { get; }

        public float AspectRatio { get; }

        public float DpiScale { get; }

        public int RenderPixelWidth { get; }

        public int RenderPixelHeight { get; }

        public GameplaySkinScrollDirection ScrollDirection { get; }

        public IReadOnlyList<GameplaySkinLayoutDiagnostic> Diagnostics
            => Array.AsReadOnly(diagnostics ?? Array.Empty<GameplaySkinLayoutDiagnostic>());

        public BmsGameplayLayoutEnvironment(
            GameplaySkinLayoutRect screenBounds,
            GameplaySkinLayoutRect safeBounds,
            float aspectRatio,
            float dpiScale,
            GameplaySkinScrollDirection scrollDirection = GameplaySkinScrollDirection.Down,
            IEnumerable<GameplaySkinLayoutDiagnostic>? diagnostics = null,
            int renderPixelWidth = 0,
            int renderPixelHeight = 0)
        {
            if (!screenBounds.Contains(safeBounds))
                throw new ArgumentException("BMS safe bounds must remain within the exact screen bounds.", nameof(safeBounds));

            if (!float.IsFinite(aspectRatio) || aspectRatio <= 0)
                throw new ArgumentOutOfRangeException(nameof(aspectRatio));

            if (!float.IsFinite(dpiScale) || dpiScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(dpiScale));

            if (renderPixelWidth == 0 && renderPixelHeight == 0)
            {
                renderPixelHeight = checked((int)Math.Ceiling(1080 * Math.Max(1, dpiScale)));
                renderPixelWidth = checked((int)Math.Ceiling(renderPixelHeight * aspectRatio));
            }

            if (renderPixelWidth <= 0 || renderPixelHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(renderPixelWidth));

            ScreenBounds = screenBounds;
            SafeBounds = safeBounds;
            AspectRatio = aspectRatio;
            DpiScale = dpiScale;
            RenderPixelWidth = renderPixelWidth;
            RenderPixelHeight = renderPixelHeight;
            ScrollDirection = scrollDirection;
            this.diagnostics = diagnostics?.ToArray() ?? Array.Empty<GameplaySkinLayoutDiagnostic>();
        }

        public static BmsGameplayLayoutEnvironment Default { get; } = new BmsGameplayLayoutEnvironment(
            GameplaySkinLayoutRect.Create(0, 0, 1, 1),
            GameplaySkinLayoutRect.Create(0, 0, 1, 1),
            16f / 9f,
            1);
    }

    /// <summary>
    /// Raw, package-derived BMS geometry fields. Each field is independently validated by the sole solver.
    /// </summary>
    public sealed class BmsGameplayLayoutConfiguration
    {
        public float? NormalLaneRelativeWidth { get; init; }
        public float? BlackLaneRelativeWidth { get; init; }
        public float? ScratchLaneRelativeWidth { get; init; }
        public float? NormalLaneRelativeSpacing { get; init; }
        public float? ScratchLaneRelativeSpacing { get; init; }
        public float? PlayfieldWidth { get; init; }
        public float? PlayfieldHeight { get; init; }
        public float? KeyAreaHeight { get; init; }
        public float? GaugeHeight { get; init; }
        public float? ScratchKeyWidth { get; init; }
        public float? BgaWidth { get; init; }
        public float? BgaHeight { get; init; }
        public float? BgaVerticalPosition { get; init; }
        public float? BgaInformationHeight { get; init; }
        public float? HitTargetHeight { get; init; }
        public float? HitTargetBarHeight { get; init; }
        public float? HitTargetLineHeight { get; init; }
        public float? HitTargetGlowRadius { get; init; }
        public float? BarLineHeight { get; init; }

        public static BmsGameplayLayoutConfiguration FromSkin(ISkin? skin, BmsKeymode keymode, ISkin? selectedLayoutSkin)
        {
            if (skin == null)
                return new BmsGameplayLayoutConfiguration();

            return new BmsGameplayLayoutConfiguration
            {
                BlackLaneRelativeWidth = selectedLayoutValue(BmsSkinConfigurationLookups.BlackLaneWidth),
                NormalLaneRelativeWidth = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.NormalLaneWidth, keymode)?.Value,
                ScratchLaneRelativeWidth = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.ScratchLaneWidth, keymode)?.Value,
                NormalLaneRelativeSpacing = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.NormalLaneSpacing, keymode)?.Value,
                ScratchLaneRelativeSpacing = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.ScratchLaneSpacing, keymode)?.Value,
                PlayfieldWidth = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldWidth, keymode)?.Value,
                PlayfieldHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldHeight, keymode)?.Value,
                KeyAreaHeight = selectedLayoutValue(BmsSkinConfigurationLookups.KeyAreaHeight),
                GaugeHeight = selectedLayoutValue(BmsSkinConfigurationLookups.GaugeHeight),
                ScratchKeyWidth = selectedLayoutValue(BmsSkinConfigurationLookups.ScratchKeyWidth),
                BgaWidth = selectedLayoutValue(BmsSkinConfigurationLookups.BgaWidth),
                BgaHeight = selectedLayoutValue(BmsSkinConfigurationLookups.BgaHeight),
                BgaVerticalPosition = selectedLayoutValue(BmsSkinConfigurationLookups.BgaVerticalPosition),
                BgaInformationHeight = selectedLayoutValue(BmsSkinConfigurationLookups.BgaInformationHeight),
                HitTargetHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetHeight, keymode)?.Value,
                HitTargetBarHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetBarHeight, keymode)?.Value,
                HitTargetLineHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetLineHeight, keymode)?.Value,
                HitTargetGlowRadius = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.HitTargetGlowRadius, keymode)?.Value,
                BarLineHeight = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.BarLineHeight, keymode)?.Value,
            };

            float? selectedLayoutValue(BmsSkinConfigurationLookups field)
            {
                // New layout choices are opt-in declarations of the selected package. An absent declaration must
                // not inherit the installation skin's controller or enlarged BGA through the aggregate source.
                if (selectedLayoutSkin is BmsLegacySkin selected)
                {
                    GameplaySkinConfigurationDeclaration<float> declaration = selected.GetAcceptedBmsGeometry(field, keymode);
                    return declaration.IsDeclared ? declaration.Value : null;
                }

                return selectedLayoutSkin?.GetBmsSkinConfig<float>(field, keymode)?.Value;
            }
        }
    }

    /// <summary>
    /// The sole BMS geometry solver. It consumes parser-owned keymode plus an exact topology publication and emits one
    /// complete immutable package/layout pair. No drawable dimensions or hit-object channel scan participate.
    /// </summary>
    public static class BmsGameplayLayoutSolver
    {
        private const float reference_aspect = 16f / 9f;
        private const float side_inset = 0.05f;
        private const float surface_gap = 0.006f;
        private const float gauge_height = 0.036f;
        private const float hud_height = 0.08f;

        internal static BmsGameplayLayoutSnapshot Solve(
            BmsKeymodeResolution keymodeResolution,
            BmsPlayfieldStyle requestedStyle,
            BmsGameplayLayoutConfiguration configuration,
            BmsGameplayLayoutEnvironment environment,
            GameplaySkinPackageRevision packageRevision,
            BmsGameplaySkinLaneTopologyPublication topologyPublication,
            long layoutRevision)
        {
            ArgumentNullException.ThrowIfNull(keymodeResolution);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(packageRevision);
            ArgumentNullException.ThrowIfNull(topologyPublication);

            BmsKeymode keymode = keymodeResolution.Keymode;

            if (topologyPublication.Keymode != keymode)
                throw new ArgumentException("BMS topology publication keymode must match the parser-owned native context.", nameof(topologyPublication));

            BmsPlayfieldStyle style = requestedStyle.GetAppliedStyle(keymode);

            if (topologyPublication.AppliedStyle != style)
                throw new ArgumentException("BMS topology publication style must match the exact presentation context.", nameof(topologyPublication));

            int laneCount = BmsRuleset.GetLaneCount(keymode);
            var diagnostics = new List<GameplaySkinLayoutDiagnostic>(environment.Diagnostics);

            float normalWidth = field(configuration.NormalLaneRelativeWidth, 1f, 0.25f, 4f, "normal-lane-width", diagnostics);
            float blackWidth = field(configuration.BlackLaneRelativeWidth, normalWidth, 0.25f, 4f, "black-lane-width", diagnostics);
            float scratchWidth = field(configuration.ScratchLaneRelativeWidth, 1.5f, 0.25f, 4f, "scratch-lane-width", diagnostics);
            float normalSpacing = field(configuration.NormalLaneRelativeSpacing, 0f, 0f, 2f, "normal-lane-spacing", diagnostics);
            float scratchSpacing = field(configuration.ScratchLaneRelativeSpacing, 0.12f, 0f, 2f, "scratch-lane-spacing", diagnostics);
            float defaultFieldWidth = Math.Clamp(laneCount * 0.06f, 0.35f, 0.8f) * 0.825f;
            float requestedFieldWidth = field(configuration.PlayfieldWidth, defaultFieldWidth, 0.15f, 0.95f, "playfield-width", diagnostics);
            float requestedFieldHeight = field(configuration.PlayfieldHeight, BmsPlayfieldLayoutProfile.DEFAULT_PLAYFIELD_HEIGHT, 0.45f, 0.94f, "playfield-height", diagnostics);
            float targetHeight = field(configuration.HitTargetHeight, 16f, 1f, 128f, "hit-target-height", diagnostics);
            float targetBarHeight = field(configuration.HitTargetBarHeight, Math.Min(12f, targetHeight), 0.5f, targetHeight, "hit-target-bar-height", diagnostics);
            float targetLineHeight = field(configuration.HitTargetLineHeight, Math.Min(3f, targetHeight), 0.5f, targetHeight, "hit-target-line-height", diagnostics);
            float targetGlow = field(configuration.HitTargetGlowRadius, 6f, 0f, 96f, "hit-target-glow-radius", diagnostics);
            float barLineHeight = field(configuration.BarLineHeight, 2f, 0.5f, 32f, "bar-line-height", diagnostics);

            var profile = BmsPlayfieldLayoutProfile.CreateValidated(
                keymode,
                laneCount,
                normalWidth,
                scratchWidth,
                normalSpacing,
                scratchSpacing,
                requestedFieldWidth,
                requestedFieldHeight,
                targetHeight,
                targetBarHeight,
                targetLineHeight,
                targetGlow,
                barLineHeight,
                blackWidth);
            BmsLaneLayout laneLayout = BmsLaneLayout.CreateCanonical(keymode, profile, style);

            GameplaySkinLayoutRect safe = environment.SafeBounds;
            float aspectScale = Math.Clamp(MathF.Sqrt(reference_aspect / environment.AspectRatio), 0.76f, 1.35f);
            float dpiScale = Math.Clamp(MathF.Pow(environment.DpiScale, 0.08f), 0.94f, 1.08f);
            float fieldWidth = Math.Min(safe.Width, requestedFieldWidth * safe.Width * aspectScale * dpiScale);
            float fieldHeight = Math.Min(safe.Height, requestedFieldHeight * safe.Height);
            float inset = Math.Min(side_inset * safe.Width, Math.Max(0, (safe.Width - fieldWidth) / 2));
            float fieldX = style switch
            {
                BmsPlayfieldStyle.P1 => safe.Left + inset,
                BmsPlayfieldStyle.P2 => safe.Right - inset - fieldWidth,
                _ => safe.Left + (safe.Width - fieldWidth) / 2,
            };

            float gaugeHeight = Math.Min(field(configuration.GaugeHeight, gauge_height, 0.02f, 0.12f, "gauge-height", diagnostics) * safe.Height * dpiScale, safe.Height * 0.12f);
            float keyAreaHeight = field(configuration.KeyAreaHeight, 0f, 0f, 0.18f, "key-area-height", diagnostics) * safe.Height;
            float scratchKeyWidth = field(configuration.ScratchKeyWidth, 1f, 1f, 4f, "scratch-key-width", diagnostics);
            float requiredBottomBand = keyAreaHeight + gaugeHeight + (surface_gap + hud_height) * safe.Height;

            if (fieldHeight + requiredBottomBand > safe.Height)
                fieldHeight = safe.Height - requiredBottomBand;

            var playfieldRect = GameplaySkinLayoutRect.Create(fieldX, safe.Top, fieldWidth, fieldHeight);

            float informationHeight = field(configuration.BgaInformationHeight, 0f, 0f, 0.30f, "bga-information-height", diagnostics) * safe.Height;
            var bgaRects = solveBgaViewports(keymode, style, safe, playfieldRect, environment.AspectRatio, gaugeHeight + keyAreaHeight, configuration, diagnostics, informationHeight, ref fieldHeight);

            // A bottom BGA fallback may shorten the field. Recreate the complete snapshot geometry from that one result.
            playfieldRect = GameplaySkinLayoutRect.Create(fieldX, safe.Top, fieldWidth, fieldHeight);
            float gaugeY = playfieldRect.Bottom + keyAreaHeight + surface_gap * safe.Height;
            var gaugeRect = GameplaySkinLayoutRect.Create(fieldX, gaugeY, fieldWidth, gaugeHeight);

            float pixelHeight = safe.Height / Math.Max(480f, 768f * environment.DpiScale);
            float targetRelativeHeight = Math.Min(playfieldRect.Height, Math.Max(pixelHeight, targetHeight * pixelHeight));
            float lineRelativeHeight = Math.Min(targetRelativeHeight, Math.Max(pixelHeight, targetLineHeight * pixelHeight));
            float targetY = environment.ScrollDirection == GameplaySkinScrollDirection.Down
                ? playfieldRect.Bottom - targetRelativeHeight
                : playfieldRect.Top;
            float lineY = environment.ScrollDirection == GameplaySkinScrollDirection.Down
                ? playfieldRect.Bottom - lineRelativeHeight
                : playfieldRect.Top;
            var targetRect = GameplaySkinLayoutRect.Create(playfieldRect.Left, targetY, playfieldRect.Width, targetRelativeHeight);
            var judgementLineRect = GameplaySkinLayoutRect.Create(playfieldRect.Left, lineY, playfieldRect.Width, lineRelativeHeight);
            var keyAreaRect = keyAreaHeight > 0
                ? GameplaySkinLayoutRect.Create(playfieldRect.Left, playfieldRect.Bottom, playfieldRect.Width, keyAreaHeight)
                : targetRect;

            float comboWidth = Math.Min(playfieldRect.Width * 0.62f, safe.Width * 0.28f);
            float comboHeight = Math.Min(playfieldRect.Height * 0.18f, safe.Height * 0.16f);
            var comboRect = GameplaySkinLayoutRect.Create(
                playfieldRect.Left + (playfieldRect.Width - comboWidth) / 2,
                playfieldRect.Top + (playfieldRect.Height - comboHeight) / 2,
                comboWidth,
                comboHeight);
            float judgementWidth = Math.Min(playfieldRect.Width * 0.7f, safe.Width * 0.32f);
            float judgementHeight = Math.Min(playfieldRect.Height * 0.12f, safe.Height * 0.11f);
            float judgementCentreY = environment.ScrollDirection == GameplaySkinScrollDirection.Down
                ? playfieldRect.Bottom - playfieldRect.Height * 0.19f
                : playfieldRect.Top + playfieldRect.Height * 0.19f;
            var judgementRect = GameplaySkinLayoutRect.Create(
                playfieldRect.Left + (playfieldRect.Width - judgementWidth) / 2,
                judgementCentreY - judgementHeight / 2,
                judgementWidth,
                judgementHeight);
            var hudRect = GameplaySkinLayoutRect.Create(safe.Left, safe.Bottom - safe.Height * hud_height, safe.Width, safe.Height * hud_height);

            if (keyAreaHeight > 0)
            {
                // The compact controller layout keeps judgement and combo together above the judgement line.
                comboHeight = playfieldRect.Height * 0.065f;
                float comboY = environment.ScrollDirection == GameplaySkinScrollDirection.Down
                    ? judgementRect.Bottom
                    : judgementRect.Top - comboHeight;
                comboRect = GameplaySkinLayoutRect.Create(comboRect.Left, comboY, comboWidth, comboHeight);
            }

            var neutralLanes = new GameplaySkinLayoutLane[laneCount];
            GameplaySkinLaneTopologySnapshot topology = topologyPublication.Publication.Topology;

            foreach (BmsLaneLayout.Lane lane in laneLayout.Lanes)
            {
                GameplaySkinLaneTopologyEntry entry = topology.LanesInLogicalOrder[lane.LaneIndex];

                if (entry.GlobalLogicalIndex != lane.LaneIndex || entry.GlobalVisualIndex != lane.VisualIndex)
                    throw new InvalidOperationException("BMS solved lane order must retain the exact topology indices.");

                var rect = GameplaySkinLayoutRect.Create(
                    playfieldRect.Left + playfieldRect.Width * lane.RelativeStart / laneLayout.TotalRelativeWidth,
                    playfieldRect.Top,
                    playfieldRect.Width * lane.RelativeWidth / laneLayout.TotalRelativeWidth,
                    playfieldRect.Height);
                neutralLanes[lane.LaneIndex] = new GameplaySkinLayoutLane(entry, rect);
            }

            GameplaySkinLayoutGroup[] groups = topology.GroupsInLogicalOrder
                                                               .Select(group => new GameplaySkinLayoutGroup(
                                                                   group,
                                                                   GameplaySkinLayoutRect.Union(group.LanesInLogicalOrder.Select(lane => neutralLanes[lane.GlobalLogicalIndex].Rect))))
                                                               .ToArray();

            var context = GameplaySkinLayoutContext.Create(
                "bms",
                nativeContextId(keymode),
                keymodeId(keymode),
                styleId(style),
                topology,
                environment.ScreenBounds,
                environment.SafeBounds,
                environment.AspectRatio,
                environment.DpiScale,
                environment.ScrollDirection,
                packageRevision,
                topologyPublication.Publication.Revision,
                layoutRevision,
                environment.RenderPixelWidth,
                environment.RenderPixelHeight);

            var surfaces = new List<GameplaySkinLayoutSurface>
            {
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.Playfield, playfieldRect, 100, true, true),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.HitTarget, targetRect, 500, false, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.KEY_AREA, keyAreaRect, 505, false, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.JudgementLine, judgementLineRect, 510, false, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.Judgement, judgementRect, 520, false, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.LaneCover, playfieldRect, 600, true, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.PreStartPreview, playfieldRect, 550, true, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.Gauge, gaugeRect, 700, true, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.Combo, comboRect, 710, false, false),
                new GameplaySkinLayoutSurface(BmsGameplayLayoutSurfaceIds.Hud, hudRect, 690, false, false),
            };

            foreach (GameplaySkinLayoutLane lane in neutralLanes)
            {
                GameplaySkinLayoutRect laneRect = lane.Rect;
                float left = laneRect.Left;
                float right = laneRect.Right;
                if (keyAreaHeight > 0 && lane.TopologyEntry.Identity.Role == GameplaySkinLaneRole.Scratch)
                {
                    // Expand only away from the neighbouring keys. The note/input lane remains untouched.
                    GameplaySkinLayoutRect groupRect = groups.Single(group => group.TopologyGroup.Identity.Id.Equals(lane.TopologyEntry.Identity.Group.Id)).Rect;
                    bool outwardLeft = laneRect.Left < groupRect.Left + groupRect.Width / 2;
                    float boundary = outwardLeft ? safe.Left : safe.Right;
                    foreach (GameplaySkinLayoutRect bga in bgaRects.Where(rect => rect.Top < keyAreaRect.Bottom && rect.Bottom > keyAreaRect.Top))
                    {
                        if (outwardLeft && bga.Right <= laneRect.Left)
                            boundary = Math.Max(boundary, bga.Right);
                        else if (!outwardLeft && bga.Left >= laneRect.Right)
                            boundary = Math.Min(boundary, bga.Left);
                    }
                    if (outwardLeft)
                        left = Math.Max(boundary, right - laneRect.Width * scratchKeyWidth);
                    else
                        right = Math.Min(boundary, left + laneRect.Width * scratchKeyWidth);
                }
                surfaces.Add(new GameplaySkinLayoutSurface(
                    BmsGameplayLayoutSurfaceIds.KeyVisualPrefix + lane.LaneId.Value,
                    GameplaySkinLayoutRect.Create(left, keyAreaRect.Top, right - left, keyAreaRect.Height), 505, false, false));
            }

            surfaces.AddRange(bgaRects.Select((rect, index) => new GameplaySkinLayoutSurface(
                $"{BmsGameplayLayoutSurfaceIds.BgaPrefix}{index + 1}", rect, 300 + index, true, false)));

            if (informationHeight > 0)
            {
                float headerHeight = informationHeight * 0.4f;
                float footerHeight = informationHeight - headerHeight;
                GameplaySkinLayoutRect primaryBga = bgaRects[0];
                float bgaBottom = bgaRects.Max(rect => rect.Bottom);
                surfaces.Add(new GameplaySkinLayoutSurface("information.song",
                    GameplaySkinLayoutRect.Create(primaryBga.Left, primaryBga.Top - headerHeight, primaryBga.Width, headerHeight), 690, true, false));
                // The same footer tracks the full BGA arrangement, including both rows in 14K.
                float statisticsWidth = Math.Min(primaryBga.Width * 0.44f, safe.Width * 0.14f);
                surfaces.Add(new GameplaySkinLayoutSurface("information.judgements",
                    GameplaySkinLayoutRect.Create(primaryBga.Left, bgaBottom, statisticsWidth, footerHeight), 690, true, false));
                surfaces.Add(new GameplaySkinLayoutSurface("information.tempo",
                    GameplaySkinLayoutRect.Create(primaryBga.Left + statisticsWidth, bgaBottom, primaryBga.Width - statisticsWidth, footerHeight), 690, true, false));
                surfaces.Add(new GameplaySkinLayoutSurface("information.player",
                    GameplaySkinLayoutRect.Create(playfieldRect.Left, hudRect.Top, playfieldRect.Width, hudRect.Height), 690, true, false));
            }

            ensureNonOverlap(playfieldRect, gaugeRect, bgaRects);

            GameplaySkinLayoutSnapshot neutral = GameplaySkinLayoutSnapshot.Create(
                context,
                groups,
                neutralLanes,
                surfaces,
                bgaRects,
                diagnostics);

            return new BmsGameplayLayoutSnapshot(neutral, keymodeResolution, style, profile, laneLayout, laneLayout.Lanes.Select(lane => lane.Action));
        }

        private static GameplaySkinLayoutRect[] solveBgaViewports(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            GameplaySkinLayoutRect safe,
            GameplaySkinLayoutRect playfield,
            float aspectRatio,
            float exactGaugeHeight,
            BmsGameplayLayoutConfiguration configuration,
            ICollection<GameplaySkinLayoutDiagnostic> diagnostics,
            float informationHeight,
            ref float fieldHeight)
        {
            float headerHeight = informationHeight * 0.4f;
            float footerHeight = informationHeight - headerHeight;
            float availableTop = safe.Top + headerHeight;
            float availableHeight = safe.Height * (1 - hud_height) - informationHeight;
            float availableBottom = availableTop + availableHeight;
            float horizontalGap = surface_gap * safe.Width;
            float leftSpace = playfield.Left - safe.Left - horizontalGap;
            float rightSpace = safe.Right - playfield.Right - horizontalGap;
            float preferredWidth = field(configuration.BgaWidth, keymode == BmsKeymode.Key14K ? 0.13f : 0.225f, 0.01f, 1f, "bga-width", diagnostics) * safe.Width;
            float preferredHeight = field(configuration.BgaHeight, 0.30f, 0.01f, 1f, "bga-height", diagnostics) * safe.Height;
            float verticalPosition = field(configuration.BgaVerticalPosition, 0f, 0f, 1f, "bga-vertical-position", diagnostics);
            // Preserve the legacy viewport proportions unless the package supplies a valid maximum box.
            bool authoredSize = configuration.BgaWidth is >= 0.01f and <= 1f || configuration.BgaHeight is >= 0.01f and <= 1f;
            float usableSideWidth = Math.Max(leftSpace, rightSpace);

            if (keymode == BmsKeymode.Key14K)
                usableSideWidth = Math.Min(leftSpace, rightSpace);

            if (usableSideWidth >= safe.Width * 0.075f)
            {
                float width = Math.Min(preferredWidth, usableSideWidth);
                float height = Math.Min(safe.Height * 0.30f, Math.Max(safe.Height * 0.10f, width * aspectRatio / (4f / 3f)));

                if (authoredSize)
                {
                    height = Math.Min(availableHeight, Math.Min(preferredHeight, width * aspectRatio / (4f / 3f)));
                    if (keymode == BmsKeymode.Key14K)
                        height = Math.Min(height, (availableHeight - surface_gap * safe.Height) / 2);
                    width = height * (4f / 3f) / aspectRatio;
                }

                if (keymode == BmsKeymode.Key14K)
                {
                    float xLeft = safe.Left;
                    float xRight = safe.Right - width;
                    float verticalInset = Math.Max(0, availableHeight / 2 - height) * verticalPosition;
                    float yTop = availableTop + verticalInset;
                    float yBottom = availableBottom - height - verticalInset;
                    return new[]
                    {
                        GameplaySkinLayoutRect.Create(xLeft, yTop, width, height),
                        GameplaySkinLayoutRect.Create(xRight, yTop, width, height),
                        GameplaySkinLayoutRect.Create(xLeft, yBottom, width, height),
                        GameplaySkinLayoutRect.Create(xRight, yBottom, width, height),
                    };
                }

                bool placeLeft = style == BmsPlayfieldStyle.P2 || leftSpace > rightSpace;
                float x = placeLeft ? safe.Left : safe.Right - width;
                return new[] { GameplaySkinLayoutRect.Create(x, availableTop + (availableHeight - height) * verticalPosition, width, height) };
            }

            // Extremely narrow matrices cannot fit a useful side viewport. Reserve a deterministic bottom band and
            // shorten the one playfield before any snapshot object is created, keeping the pair internally coherent.
            float bottomHeight = Math.Min(safe.Height * 0.20f, Math.Max(safe.Height * 0.12f, safe.Width * 0.42f * aspectRatio / (4f / 3f)));
            if (authoredSize)
                bottomHeight = Math.Min(bottomHeight, Math.Min(preferredHeight, preferredWidth * aspectRatio / (4f / 3f)));
            float bottomWidth = Math.Min(safe.Width * 0.64f, bottomHeight * (4f / 3f) / aspectRatio);
            if (authoredSize)
                bottomHeight = bottomWidth * aspectRatio / (4f / 3f);
            float gaugeAndGaps = exactGaugeHeight + surface_gap * safe.Height * 2;
            fieldHeight = Math.Min(fieldHeight, safe.Height * (1 - hud_height) - footerHeight - bottomHeight - headerHeight - gaugeAndGaps);
            return new[]
            {
                GameplaySkinLayoutRect.Create(safe.Left + (safe.Width - bottomWidth) / 2, availableBottom - bottomHeight, bottomWidth, bottomHeight),
            };
        }

        private static float field(float? candidate, float fallback, float minimum, float maximum, string id, ICollection<GameplaySkinLayoutDiagnostic> diagnostics)
        {
            if (candidate == null)
                return fallback;

            if (!float.IsFinite(candidate.Value) || candidate.Value < minimum || candidate.Value > maximum)
            {
                diagnostics.Add(new GameplaySkinLayoutDiagnostic($"bms.layout.invalid-{id}"));
                return fallback;
            }

            return candidate.Value;
        }

        private static void ensureNonOverlap(
            GameplaySkinLayoutRect playfield,
            GameplaySkinLayoutRect gauge,
            IEnumerable<GameplaySkinLayoutRect> bgaViewports)
        {
            if (playfield.Intersects(gauge))
                throw new InvalidOperationException("BMS gauge must not overlap the exact playfield rectangle.");

            foreach (GameplaySkinLayoutRect viewport in bgaViewports)
            {
                if (viewport.Intersects(playfield) || viewport.Intersects(gauge))
                    throw new InvalidOperationException("BMS BGA viewport must not overlap playfield or gauge geometry.");
            }
        }

        private static string nativeContextId(BmsKeymode keymode) => keymode switch
        {
            BmsKeymode.Key5K => "bms.chart.5k",
            BmsKeymode.Key7K => "bms.chart.7k",
            BmsKeymode.Key9K_Bms => "bms.chart.9k-bms",
            BmsKeymode.Key9K_Pms => "bms.chart.9k-pms",
            BmsKeymode.Key14K => "bms.chart.14k",
            _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
        };

        private static string keymodeId(BmsKeymode keymode) => keymode switch
        {
            BmsKeymode.Key5K => "5k",
            BmsKeymode.Key7K => "7k",
            BmsKeymode.Key9K_Bms => "9k-bms",
            BmsKeymode.Key9K_Pms => "9k-pms",
            BmsKeymode.Key14K => "14k",
            _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
        };

        private static string styleId(BmsPlayfieldStyle style) => style switch
        {
            BmsPlayfieldStyle.P1 => "p1",
            BmsPlayfieldStyle.P2 => "p2",
            BmsPlayfieldStyle.Center => "center-p1",
            BmsPlayfieldStyle.CenterRightScratch => "center-p2",
            _ => throw new ArgumentOutOfRangeException(nameof(style)),
        };
    }
}
