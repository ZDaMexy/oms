// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;

namespace osu.Game.Skinning.Gameplay
{
    [Flags]
    internal enum GameplaySkinSceneStateFamily
    {
        None = 0,
        Layout = 1 << 0,
        Input = 1 << 1,
        Object = 1 << 2,
        Judgement = 1 << 3,
        Score = 1 << 4,
        Timing = 1 << 5,
        Bga = 1 << 6,
        All = Layout | Input | Object | Judgement | Score | Timing | Bga,
    }

    /// <summary>
    /// Hard C5 preparation and runtime-admission budgets which are independent of author input.
    /// </summary>
    public static class GameplaySkinPreparedSceneBudgets
    {
        public const int MAX_RESOURCE_BYTES = 16 * 1024 * 1024;
        public const int MAX_TOTAL_RESOURCE_BYTES = 64 * 1024 * 1024;
        public const long MAX_TEXTURE_PIXELS = 16 * 1024 * 1024;
        public const long MAX_TOTAL_TEXTURE_PIXELS = 64 * 1024 * 1024;
        public const long MAX_TOTAL_DECODED_TEXTURE_BYTES = 256 * 1024 * 1024;
        public const int MAX_PREPARED_NODES = GameplaySkinSceneBudgets.MAX_EXPANDED_TEMPLATE_NODES;
        public const int MAX_RUNTIME_INSTANCES = 32768;
        public const int MAX_RUNTIME_EFFECT_INSTANCES = 8192;
        public const int EFFECT_SURFACES_PER_EFFECT = 3;
        public const long MAX_RUNTIME_EFFECT_SURFACE_PIXELS = 128 * 1024 * 1024;
        public const long MAX_RUNTIME_EFFECT_SURFACE_BYTES = MAX_RUNTIME_EFFECT_SURFACE_PIXELS * 4;
        public const int MAX_RUNTIME_TEXT_GLYPHS = 4096;
        public const int TEXT_ATLAS_PAGE_SIZE = 2048;
        public const int TEXT_GLYPH_PADDING_PIXELS = 8;
        public const long MAX_RUNTIME_TEXT_GLYPH_PIXELS = 64 * 1024 * 1024;
        public const long MAX_RUNTIME_TEXT_GLYPH_BYTES = MAX_RUNTIME_TEXT_GLYPH_PIXELS * 4;
        public const int MAX_DYNAMIC_TEXT_GLYPHS_PER_NODE = 384;
        public const int MAX_CREATIONS_PER_FRAME = 256;
        public const int MAX_SPECIALISED_VISUALS_PER_KEY = 256;
        public const int MAX_HIT_EXPLOSION_VISUALS_PER_KEY = 16;
        public const int MAX_HUD_SOURCE_OWNERS = 64;
        public const int MAX_HUD_SOURCE_OWNERS_PER_SLOT = 32;
        public const int MAX_HUD_PARTITION_RECORDS = 1280;
        public const int MAX_HUD_RESIDUAL_RECORDS = 1344;
        public const int MAX_HUD_RUNTIME_FACTORY_INSTANCES = 512;
        public const int MAX_HUD_CAPTURE_SURFACES = 5;
        public const long MAX_HUD_CAPTURE_SURFACE_PIXELS = 64 * 1024 * 1024;
    }

    /// <summary>
    /// Sole prepare-time policy for mapping a catalog slot to a render stratum and a bounded native pool.
    /// Runtime consumers read this result; they do not negotiate or infer routes.
    /// </summary>
    internal static class GameplaySkinSceneHostPolicy
    {
        public static GameplaySkinSceneLayer LayerFor(GameplaySkinSlotDescriptor descriptor)
        {
            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.StageBackground)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.PlayfieldBackdrop)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BgaViewport))
                return GameplaySkinSceneLayer.Background;

            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LaneSurface)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LaneDivider)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.JudgementLine)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.HitTarget)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.PlayfieldBaseplate))
                return GameplaySkinSceneLayer.Underlay;

            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.Note)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LongNoteHead)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LongNoteBody)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LongNoteTail)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.Mine)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BarLine)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.KeyVisual))
                return GameplaySkinSceneLayer.Object;

            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.KeyFlash)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.HitExplosion))
                return GameplaySkinSceneLayer.GameplayEffects;

            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.JudgementDisplay)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.ComboDisplay)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.GaugeVisual)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.TextHud))
                return GameplaySkinSceneLayer.HudForeground;

            return GameplaySkinSceneLayer.Overlay;
        }

        /// <summary>
        /// Stable ordering within one shared scene layer. Higher osu! drawable depth renders behind lower depth.
        /// </summary>
        public static float BaseDepthFor(GameplaySkinSlotDescriptor descriptor)
        {
            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.StageBackground))
                return 1;

            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.PlayfieldBackdrop))
                return 0;

            return 0;
        }

        public static bool RequiresNativeGeometry(GameplaySkinSlotDescriptor descriptor, string rulesetId)
            => ReferenceEquals(descriptor, GameplaySkinSlotCatalog.Note)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LongNoteHead)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LongNoteBody)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LongNoteTail)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.KeyVisual)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.HitExplosion)
               || (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.Mine)
                   && string.Equals(rulesetId, "bms", StringComparison.Ordinal))
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LaneCoverFill)
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LaneCoverDecoration)
               || ((ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BgaViewport)
                    || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BgaFrame))
                   && string.Equals(rulesetId, "bms", StringComparison.Ordinal))
               || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BarLine);

        public static int SpecialisedPoolCapacity(GameplaySkinSlotDescriptor descriptor)
        {
            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.KeyVisual))
                return 1;

            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.HitExplosion))
                return GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY;

            if (ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LaneCoverFill)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.LaneCoverDecoration)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BgaViewport)
                || ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BgaFrame))
                return 4;

            return GameplaySkinPreparedSceneBudgets.MAX_SPECIALISED_VISUALS_PER_KEY;
        }
    }

    /// <summary>
    /// Selects an existing C3 surface for a public slot. This class never solves or mutates geometry; it only narrows
    /// a stable target rectangle against rectangles already frozen in the exact layout snapshot.
    /// </summary>
    internal static class GameplaySkinSceneSurfaceResolver
    {
        public static bool TryResolve(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinSlotDescriptor slot,
            GameplaySkinResolvedMaterialTarget target,
            GameplaySkinLayoutRect targetRect,
            GameplaySkinSceneTargetKind? authoredTargetKind,
            out GameplaySkinLayoutRect rect)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(target);

            rect = targetRect;

            // These drawables retain native scrolling/object geometry. The scene visual is local to that owner.
            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.Note)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.LongNoteHead)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.LongNoteBody)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.LongNoteTail)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.Mine)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.BarLine)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.KeyVisual))
                return true;

            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.BgaViewport)
                || ReferenceEquals(slot, GameplaySkinSlotCatalog.BgaFrame))
            {
                // An authored bga target already resolved its explicit viewport index during preparation.
                if (authoredTargetKind == GameplaySkinSceneTargetKind.Bga)
                    return true;

                // A global author target decorates viewport zero only when the exact C3 publication actually owns a
                // BGA viewport. Rulesets whose versioned runtime-support profile marks BGA not applicable cannot
                // acquire a synthetic material/scene host from the event stream's empty-state compatibility summary.
                if (snapshot.BgaViewports.Count == 0)
                    return false;

                rect = snapshot.BgaViewports[0];
                return true;
            }

            string ruleset = snapshot.Context.RulesetId;
            string? surfaceId = null;
            bool intersectTarget = false;
            bool projectStageWidth = false;

            if (ReferenceEquals(slot, GameplaySkinSlotCatalog.JudgementLine))
            {
                surfaceId = ruleset == "bms" ? "bms.judgement-line" : ruleset == "mania" ? "mania.hit-target" : null;
                intersectTarget = true;
            }
            else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.HitTarget)
                     || ReferenceEquals(slot, GameplaySkinSlotCatalog.KeyFlash)
                     || ReferenceEquals(slot, GameplaySkinSlotCatalog.HitExplosion))
            {
                surfaceId = surface(ruleset, "hit-target");
                intersectTarget = true;
            }
            else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.LaneCoverFill)
                     || ReferenceEquals(slot, GameplaySkinSlotCatalog.LaneCoverDecoration))
            {
                surfaceId = ruleset == "bms" ? "bms.lane-cover" : ruleset == "mania" ? "mania.playfield" : null;
                intersectTarget = true;
            }
            else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.PlayfieldBackdrop)
                     || ReferenceEquals(slot, GameplaySkinSlotCatalog.PlayfieldBaseplate))
            {
                surfaceId = surface(ruleset, "playfield");
                intersectTarget = true;
            }
            else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.JudgementDisplay))
            {
                surfaceId = surface(ruleset, "judgement");
                projectStageWidth = true;
            }
            else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.ComboDisplay))
            {
                surfaceId = surface(ruleset, "combo");
                projectStageWidth = true;
            }
            else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.GaugeVisual))
            {
                surfaceId = surface(ruleset, "gauge");
                projectStageWidth = true;
            }
            else if (ReferenceEquals(slot, GameplaySkinSlotCatalog.TextHud))
            {
                surfaceId = surface(ruleset, "hud");
                projectStageWidth = true;
            }

            // Stage roots, lane surfaces/dividers, turntable/laser and free decoration use their exact C3 target.
            if (surfaceId == null)
                return true;

            GameplaySkinLayoutSurface? exactSurface = snapshot.Surfaces.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, surfaceId, StringComparison.Ordinal));

            if (exactSurface == null)
                return false;

            if (!intersectTarget && !projectStageWidth)
            {
                rect = exactSurface.Rect;
                return true;
            }

            // Stage-scoped HUD/judgement slots retain the C3 surface's authoritative vertical band while taking
            // the exact group's horizontal range. This matters for dual-stage layouts whose gauge/HUD can sit
            // outside both the playfield group's Y range and (for a global left-side gauge) its X range; intersecting
            // either axis would incorrectly erase the second stage instead of projecting the frozen semantic band.
            if (projectStageWidth)
            {
                rect = GameplaySkinLayoutRect.Create(targetRect.Left, exactSurface.Rect.Top, targetRect.Width, exactSurface.Rect.Height);
                return true;
            }

            float left = Math.Max(exactSurface.Rect.Left, targetRect.Left);
            float top = Math.Max(exactSurface.Rect.Top, targetRect.Top);
            float right = Math.Min(exactSurface.Rect.Right, targetRect.Right);
            float bottom = Math.Min(exactSurface.Rect.Bottom, targetRect.Bottom);

            if (right <= left || bottom <= top)
                return false;

            rect = GameplaySkinLayoutRect.Create(left, top, right - left, bottom - top);
            return true;
        }

        private static string? surface(string ruleset, string semantic)
            => ruleset is "bms" or "mania" ? $"{ruleset}.{semantic}" : null;
    }

    /// <summary>
    /// Stable fail-closed error raised before an exact publication can be committed.
    /// </summary>
    public sealed class GameplaySkinScenePreparationException : Exception
    {
        public GameplaySkinSceneDiagnosticCode Code { get; }

        internal GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode code)
            : base($"Gameplay skin scene preparation failed with OMS-SKIN-SCENE-{(int)code:000}.")
        {
            Code = code;
        }
    }

    /// <summary>
    /// One package resource captured and prepared before update-thread commit.
    /// </summary>
    public sealed class GameplaySkinPreparedSceneResource
    {
        public string Id { get; }

        public GameplaySkinSceneResourceType Type { get; }

        public string ContentRevision { get; }

        public int EncodedBytes { get; }

        public long DecodedBytes { get; }

        public Texture? Texture { get; }

        internal GameplaySkinPreparedSceneResource(
            GameplaySkinSceneResource source,
            string contentRevision,
            int encodedBytes,
            long decodedBytes,
            Texture? texture)
        {
            Id = source.Id;
            Type = source.Type;
            ContentRevision = contentRevision;
            EncodedBytes = encodedBytes;
            DecodedBytes = decodedBytes;
            Texture = texture;
        }
    }

    /// <summary>
    /// One immutable property value compiled for runtime consumption. Resource values retain the exact prepared
    /// resource and texture selected during background preparation; a renderer never resolves an author string.
    /// </summary>
    public sealed class GameplaySkinPreparedSceneValue
    {
        public GameplaySkinScenePropertyValueKind Kind { get; }

        public bool BooleanValue { get; }

        public double NumberValue { get; }

        public string? StringValue { get; }

        public GameplaySkinPreparedSceneResource? Resource { get; }

        public Texture? Texture => Resource?.Texture;

        internal GameplaySkinPreparedSceneValue(
            GameplaySkinSceneProperty property,
            GameplaySkinScenePropertyValue source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Kind = source.Kind;
            BooleanValue = source.BooleanValue;
            NumberValue = source.NumberValue;
            StringValue = property == GameplaySkinSceneProperty.Resource ? null : source.StringValue;

            if (property != GameplaySkinSceneProperty.Resource)
                return;

            if (source.Kind != GameplaySkinScenePropertyValueKind.String || string.IsNullOrEmpty(source.StringValue))
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);

            if (!resources.TryGetValue(source.StringValue, out GameplaySkinPreparedSceneResource? resource))
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.UnknownResource);

            if (resource.Type != GameplaySkinSceneResourceType.Texture || resource.Texture == null)
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);

            Resource = resource;
        }
    }

    public sealed class GameplaySkinPreparedSceneKeyframe
    {
        public string Id { get; }

        public double Time { get; }

        public GameplaySkinPreparedSceneValue Value { get; }

        internal GameplaySkinPreparedSceneKeyframe(
            GameplaySkinSceneKeyframe source,
            GameplaySkinSceneProperty property,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Id = source.Id;
            Time = source.Time;
            Value = new GameplaySkinPreparedSceneValue(property, source.Value, resources);
        }
    }

    public sealed class GameplaySkinPreparedSceneTrack
    {
        public string Id { get; }

        public GameplaySkinSceneTrackType Type { get; }

        public string TargetNodeId { get; }

        public GameplaySkinSceneProperty Property { get; }

        public GameplaySkinSceneEasing Easing { get; }

        public bool Loop { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneKeyframe> Keyframes { get; }

        internal GameplaySkinPreparedSceneTrack(
            GameplaySkinSceneTrack source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Id = source.Id;
            Type = source.Type;
            TargetNodeId = source.TargetNodeId;
            Property = source.Property;
            Easing = source.Easing;
            Loop = source.Loop;
            Keyframes = Array.AsReadOnly(source.Keyframes
                                               .Select(keyframe => new GameplaySkinPreparedSceneKeyframe(keyframe, Property, resources))
                                               .ToArray());
        }
    }

    public sealed class GameplaySkinPreparedSceneStateAssignment
    {
        public string Id { get; }

        public string TargetNodeId { get; }

        public GameplaySkinSceneProperty Property { get; }

        public GameplaySkinPreparedSceneValue Value { get; }

        internal GameplaySkinPreparedSceneStateAssignment(
            GameplaySkinSceneStateAssignment source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Id = source.Id;
            TargetNodeId = source.TargetNodeId;
            Property = source.Property;
            Value = new GameplaySkinPreparedSceneValue(Property, source.Value, resources);
        }
    }

    public sealed class GameplaySkinPreparedSceneState
    {
        public string Id { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneStateAssignment> Assignments { get; }

        internal GameplaySkinPreparedSceneState(
            GameplaySkinSceneState source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Id = source.Id;
            Assignments = Array.AsReadOnly(source.Assignments
                                                 .Select(assignment => new GameplaySkinPreparedSceneStateAssignment(assignment, resources))
                                                 .ToArray());
        }
    }

    public sealed class GameplaySkinPreparedSceneTransition
    {
        public string Id { get; }

        public string FromStateId { get; }

        public string ToStateId { get; }

        public GameplaySkinSceneEvent Event { get; }

        internal GameplaySkinPreparedSceneTransition(GameplaySkinSceneTransition source)
        {
            Id = source.Id;
            FromStateId = source.FromStateId;
            ToStateId = source.ToStateId;
            Event = source.Event;
        }
    }

    public sealed class GameplaySkinPreparedSceneStateMachine
    {
        private readonly IReadOnlyDictionary<string, GameplaySkinPreparedSceneState> statesById;
        private readonly ISet<string> referencedNodeIds;

        public string Id { get; }

        public string InitialStateId { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneState> States { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneTransition> Transitions { get; }

        internal GameplaySkinPreparedSceneStateMachine(
            GameplaySkinSceneStateMachine source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Id = source.Id;
            InitialStateId = source.InitialStateId;
            GameplaySkinPreparedSceneState[] states = source.States
                                                             .Select(state => new GameplaySkinPreparedSceneState(state, resources))
                                                             .ToArray();
            States = Array.AsReadOnly(states);
            Transitions = Array.AsReadOnly(source.Transitions.Select(transition => new GameplaySkinPreparedSceneTransition(transition)).ToArray());
            statesById = new ReadOnlyDictionary<string, GameplaySkinPreparedSceneState>(
                states.ToDictionary(state => state.Id, StringComparer.Ordinal));
            referencedNodeIds = new HashSet<string>(
                states.SelectMany(state => state.Assignments).Select(assignment => assignment.TargetNodeId),
                StringComparer.Ordinal);
        }

        internal bool ReferencesNode(string nodeId) => referencedNodeIds.Contains(nodeId);

        internal bool TryGetState(string stateId, out GameplaySkinPreparedSceneState state)
            => statesById.TryGetValue(stateId, out state!);
    }

    public sealed class GameplaySkinPreparedSceneBinding
    {
        public string Id { get; }

        public string TargetNodeId { get; }

        public GameplaySkinSceneProperty Property { get; }

        public GameplaySkinSceneBindingSource Source { get; }

        internal GameplaySkinSceneStateFamily StateFamily { get; }

        internal GameplaySkinPreparedSceneBinding(GameplaySkinSceneBinding source)
        {
            Id = source.Id;
            TargetNodeId = source.TargetNodeId;
            Property = source.Property;
            Source = source.Source;
            StateFamily = GameplaySkinPreparedSceneProgram.StateFamilyFor(source.Source);
        }
    }

    public sealed class GameplaySkinPreparedSceneVariantCase
    {
        public string Id { get; }

        public string Key { get; }

        public GameplaySkinPreparedSceneResource Resource { get; }

        public Texture Texture => Resource.Texture!;

        internal GameplaySkinPreparedSceneVariantCase(
            GameplaySkinSceneVariantCase source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Id = source.Id;
            Key = source.Key;
            Resource = RequireTexture(source.ResourceId, resources);
        }

        internal static GameplaySkinPreparedSceneResource RequireTexture(
            string resourceId,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            if (!resources.TryGetValue(resourceId, out GameplaySkinPreparedSceneResource? resource))
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.UnknownResource);

            if (resource.Type != GameplaySkinSceneResourceType.Texture || resource.Texture == null)
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);

            return resource;
        }
    }

    public sealed class GameplaySkinPreparedSceneVariant
    {
        private readonly IReadOnlyDictionary<string, GameplaySkinPreparedSceneVariantCase> casesByKey;

        public string Id { get; }

        public string TargetNodeId { get; }

        public GameplaySkinSceneBindingSource Source { get; }

        internal GameplaySkinSceneStateFamily StateFamily { get; }

        public GameplaySkinPreparedSceneResource DefaultResource { get; }

        public Texture DefaultTexture => DefaultResource.Texture!;

        public IReadOnlyList<GameplaySkinPreparedSceneVariantCase> Cases { get; }

        internal GameplaySkinPreparedSceneVariant(
            GameplaySkinSceneVariant source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Id = source.Id;
            TargetNodeId = source.TargetNodeId;
            Source = source.Source;
            StateFamily = GameplaySkinPreparedSceneProgram.StateFamilyFor(source.Source);
            DefaultResource = GameplaySkinPreparedSceneVariantCase.RequireTexture(source.DefaultResourceId, resources);
            GameplaySkinPreparedSceneVariantCase[] cases = source.Cases
                                                                  .Select(item => new GameplaySkinPreparedSceneVariantCase(item, resources))
                                                                  .ToArray();
            Cases = Array.AsReadOnly(cases);
            casesByKey = new ReadOnlyDictionary<string, GameplaySkinPreparedSceneVariantCase>(
                cases.ToDictionary(item => item.Key, StringComparer.Ordinal));
        }

        public GameplaySkinPreparedSceneResource SelectResource(string key)
            => casesByKey.TryGetValue(key, out GameplaySkinPreparedSceneVariantCase? item) ? item.Resource : DefaultResource;
    }

    /// <summary>
    /// The sole immutable runtime program compiled from author tracks, state machines, bindings and variants.
    /// Runtime consumers never traverse the source document or resolve a resource identifier.
    /// </summary>
    public sealed class GameplaySkinPreparedSceneProgram
    {
        private readonly ISet<GameplaySkinSceneBindingSource> usedBindingSources;
        private readonly ISet<GameplaySkinSceneEvent> usedEvents;

        public bool HasAuthorScene { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneTrack> Tracks { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneStateMachine> StateMachines { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneBinding> Bindings { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneVariant> Variants { get; }

        internal GameplaySkinPreparedSceneProgram(
            GameplaySkinSceneDocument? document,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            HasAuthorScene = document != null;
            Tracks = Array.AsReadOnly((document?.Tracks ?? Array.Empty<GameplaySkinSceneTrack>())
                                      .Select(track => new GameplaySkinPreparedSceneTrack(track, resources))
                                      .ToArray());
            StateMachines = Array.AsReadOnly((document?.StateMachines ?? Array.Empty<GameplaySkinSceneStateMachine>())
                                             .Select(machine => new GameplaySkinPreparedSceneStateMachine(machine, resources))
                                             .ToArray());
            Bindings = Array.AsReadOnly((document?.Bindings ?? Array.Empty<GameplaySkinSceneBinding>())
                                        .Select(binding => new GameplaySkinPreparedSceneBinding(binding))
                                        .ToArray());
            Variants = Array.AsReadOnly((document?.Variants ?? Array.Empty<GameplaySkinSceneVariant>())
                                        .Select(variant => new GameplaySkinPreparedSceneVariant(variant, resources))
                                        .ToArray());
            usedBindingSources = new HashSet<GameplaySkinSceneBindingSource>(
                Bindings.Select(binding => binding.Source).Concat(Variants.Select(variant => variant.Source)));
            usedEvents = new HashSet<GameplaySkinSceneEvent>(
                StateMachines.SelectMany(machine => machine.Transitions).Select(transition => transition.Event));
        }

        /// <summary>
        /// Constant-time prepared capability query for native hosts. This never scans author structures at commit or runtime.
        /// </summary>
        public bool UsesBindingSource(GameplaySkinSceneBindingSource source) => usedBindingSources.Contains(source);

        /// <summary>
        /// Constant-time prepared capability query for native hosts. This never scans author structures at commit or runtime.
        /// </summary>
        public bool UsesEvent(GameplaySkinSceneEvent sceneEvent) => usedEvents.Contains(sceneEvent);

        internal static GameplaySkinSceneStateFamily StateFamilyFor(GameplaySkinSceneBindingSource source) => source switch
        {
            GameplaySkinSceneBindingSource.LayoutStage => GameplaySkinSceneStateFamily.Layout,
            GameplaySkinSceneBindingSource.LayoutGroup => GameplaySkinSceneStateFamily.Layout,
            GameplaySkinSceneBindingSource.LayoutLane => GameplaySkinSceneStateFamily.Layout,
            GameplaySkinSceneBindingSource.InputPressed => GameplaySkinSceneStateFamily.Input,
            GameplaySkinSceneBindingSource.ObjectState => GameplaySkinSceneStateFamily.Object,
            GameplaySkinSceneBindingSource.JudgementResult => GameplaySkinSceneStateFamily.Judgement,
            GameplaySkinSceneBindingSource.JudgementOffset => GameplaySkinSceneStateFamily.Judgement,
            GameplaySkinSceneBindingSource.ScoreValue => GameplaySkinSceneStateFamily.Score,
            GameplaySkinSceneBindingSource.ComboValue => GameplaySkinSceneStateFamily.Score,
            GameplaySkinSceneBindingSource.GaugeValue => GameplaySkinSceneStateFamily.Score,
            GameplaySkinSceneBindingSource.TimingBeat => GameplaySkinSceneStateFamily.Timing,
            GameplaySkinSceneBindingSource.TimingMeasure => GameplaySkinSceneStateFamily.Timing,
            GameplaySkinSceneBindingSource.TimingBpm => GameplaySkinSceneStateFamily.Timing,
            GameplaySkinSceneBindingSource.BgaContentState => GameplaySkinSceneStateFamily.Bga,
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// One allowlisted node with its author target resolved against the exact C3 layout.
    /// </summary>
    public sealed class GameplaySkinPreparedSceneNode
    {
        public string InstanceId { get; }

        public GameplaySkinSceneNode Source { get; }

        /// <summary>
        /// Exact authored target after template-instance override. This retains HUD/BGA identity even when the C4
        /// material scope is global and is the sole event/binding scope for the runtime node.
        /// </summary>
        public GameplaySkinSceneTarget ResolvedTarget { get; }

        public GameplaySkinLayoutRect Rect { get; }

        public GameplaySkinResolvedMaterialTarget? MaterialTarget { get; }

        public GameplaySkinSlotDescriptor? Slot { get; }

        public GameplaySkinPreparedSceneResource? Resource { get; }

        /// <summary>
        /// Sole prepare-time texture choice, from either an exact manifest resource or this node's exact C4 public
        /// material. Runtime code never performs a material/resource fallback lookup.
        /// </summary>
        public Texture? ResolvedTexture { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneNode> Children { get; }

        /// <summary>
        /// Frozen stratum resolved during background preparation. A renderer cannot reinterpret slot layering.
        /// </summary>
        public GameplaySkinSceneLayer Layer { get; }

        /// <summary>
        /// Nearest public-slot root which owns this node transactionally, or null for free decoration/dispatch trees.
        /// </summary>
        public GameplaySkinResolvedMaterialKey? OwningSlotKey { get; }

        public bool AllowsLayerDispatch { get; }

        internal GameplaySkinPreparedSceneNode(
            string instanceId,
            GameplaySkinSceneNode source,
            GameplaySkinLayoutRect rect,
            GameplaySkinResolvedMaterialTarget? materialTarget,
            GameplaySkinSlotDescriptor? slot,
            GameplaySkinPreparedSceneResource? resource,
            IEnumerable<GameplaySkinPreparedSceneNode> children)
            : this(
                instanceId,
                source,
                source.Target,
                rect,
                materialTarget,
                slot,
                resource,
                resource?.Texture,
                children,
                slot == null ? GameplaySkinSceneLayer.Underlay : GameplaySkinSceneHostPolicy.LayerFor(slot),
                slot != null && materialTarget != null ? new GameplaySkinResolvedMaterialKey(slot, materialTarget) : null,
                source.Type == GameplaySkinSceneNodeType.Container
                && slot == null
                && source.Properties.Count == 0
                && source.Effects.Count == 0)
        {
        }

        internal GameplaySkinPreparedSceneNode(
            string instanceId,
            GameplaySkinSceneNode source,
            GameplaySkinSceneTarget resolvedTarget,
            GameplaySkinLayoutRect rect,
            GameplaySkinResolvedMaterialTarget? materialTarget,
            GameplaySkinSlotDescriptor? slot,
            GameplaySkinPreparedSceneResource? resource,
            Texture? resolvedTexture,
            IEnumerable<GameplaySkinPreparedSceneNode> children,
            GameplaySkinSceneLayer layer,
            GameplaySkinResolvedMaterialKey? owningSlotKey,
            bool allowsLayerDispatch)
        {
            InstanceId = instanceId;
            Source = source;
            ResolvedTarget = resolvedTarget;
            Rect = rect;
            MaterialTarget = materialTarget;
            Slot = slot;
            Resource = resource;
            ResolvedTexture = resolvedTexture;
            Children = Array.AsReadOnly(children.ToArray());
            Layer = layer;
            OwningSlotKey = owningSlotKey;
            AllowsLayerDispatch = allowsLayerDispatch;
        }
    }

    /// <summary>
    /// Immutable prepare-time route for one exact C4 material key. FailureRoute is the already-decided local
    /// fallback used only if construction of the authored/semantic replacement faults.
    /// </summary>
    public sealed class GameplaySkinPreparedHostedSlot
    {
        public GameplaySkinResolvedMaterialEntry Entry { get; }

        public GameplaySkinResolvedMaterialKey Key => Entry.Key;

        public GameplaySkinSceneHostRoute Route { get; }

        public GameplaySkinSceneHostRoute FailureRoute { get; }

        public GameplaySkinSceneLayer Layer { get; }

        /// <summary>
        /// Exact C3 surface selected during background preparation. Runtime hosts only instantiate this rectangle.
        /// </summary>
        public GameplaySkinLayoutRect Rect { get; }

        public int SpecialisedPoolCapacity { get; }

        /// <summary>
        /// Optional immutable texture fallback frozen during background preparation for a specialised native host.
        /// Runtime factories never reopen the resolved material set to decide ownership or fallback.
        /// </summary>
        public Texture? SpecialisedTexture { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneNode> RoutedNodes { get; }

        internal GameplaySkinPreparedHostedSlot(
            GameplaySkinResolvedMaterialEntry entry,
            GameplaySkinSceneHostRoute route,
            GameplaySkinSceneHostRoute failureRoute,
            GameplaySkinSceneLayer layer,
            GameplaySkinLayoutRect rect,
            int specialisedPoolCapacity,
            IEnumerable<GameplaySkinPreparedSceneNode> routedNodes)
        {
            Entry = entry;
            Route = route;
            FailureRoute = failureRoute;
            Layer = layer;
            Rect = rect;
            SpecialisedPoolCapacity = specialisedPoolCapacity;
            RoutedNodes = Array.AsReadOnly(routedNodes.ToArray());
            SpecialisedTexture = route == GameplaySkinSceneHostRoute.Specialised
                                 && entry.State == GameplaySkinResolvedMaterialState.Provide
                                 && entry.Material is GameplaySkinPublicSlotMaterial material
                                 && !material.IsProgrammaticFallback
                ? material.Texture
                : null;
        }
    }

    /// <summary>
    /// The single immutable output of manifest/scene/resource/target/template preparation.
    /// </summary>
    public sealed class GameplaySkinPreparedScene : IDisposable
    {
        private readonly IReadOnlyDictionary<string, int> textGlyphReservationsByNodeId;
        private IDisposable? retirement;

        public GameplaySkinLayoutSnapshot Snapshot { get; }

        public GameplaySkinPackageRevision PackageRevision => Snapshot.Context.PackageRevision;

        public GameplaySkinResolvedMaterialSet MaterialSet { get; }

        public long LayoutRevision => Snapshot.Context.LayoutRevision;

        public long MaterialRevision => MaterialSet.LayoutRevision;

        public long SceneRevision => Snapshot.Context.LayoutRevision;

        public string ContractId => GameplaySkinSceneContracts.SCENE_CONTRACT_ID;

        public string EventContractId => GameplaySkinSceneContracts.EVENT_CONTRACT_ID;

        public string ContentRevision { get; }

        /// <summary>
        /// Sole immutable runtime program. The captured source document is not retained after background
        /// compilation, so a renderer cannot accidentally regain parser or string-resource authority.
        /// </summary>
        public GameplaySkinPreparedSceneProgram Program { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneResource> Resources { get; }

        public IReadOnlyList<GameplaySkinPreparedSceneNode> Roots { get; }

        public IReadOnlyList<GameplaySkinPreparedHostedSlot> HostedSlots { get; }

        /// <summary>
        /// Exact immutable routing/factory plan for the existing core HUD compatibility owners. The plan is built
        /// with the package/layout/material/scene publication; the update-thread adapter may only instantiate its
        /// fixed bounded containers and must never serialise or reconstruct an arbitrary HUD drawable.
        /// </summary>
        public GameplaySkinPreparedHudPlan HudPlan { get; }

        public bool HasAuthorScene => Program.HasAuthorScene;

        public int PreparedNodeCount { get; }

        public int ReservedRuntimeInstanceCount { get; }

        public int PreparedEffectCount { get; }

        public long PreparedEffectSurfacePixels { get; }

        public long PreparedEffectSurfaceBytes => PreparedEffectSurfacePixels * 4;

        public int ReservedTextGlyphs { get; }

        public long ReservedTextGlyphPixels { get; }

        public long ReservedTextGlyphBytes => ReservedTextGlyphPixels * 4;

        /// <summary>
        /// Complete deterministic state used by the first production event-stream attach. Ruleset producers replace
        /// timing, score and object state only through typed engine events or a complete epoch reset.
        /// </summary>
        public GameplaySkinEventStateSnapshot InitialEventState { get; }

        internal GameplaySkinPreparedScene(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materialSet,
            string contentRevision,
            GameplaySkinSceneManifest? manifest,
            GameplaySkinSceneDocument? document,
            IEnumerable<GameplaySkinPreparedSceneResource> resources,
            IEnumerable<GameplaySkinPreparedSceneNode> roots,
            IDisposable? retirement = null)
        {
            this.retirement = retirement;

            try
            {
                Snapshot = snapshot;
                MaterialSet = materialSet;
                ContentRevision = contentRevision;

                GameplaySkinPreparedSceneResource[] copiedResources = resources.ToArray();
                GameplaySkinPreparedSceneNode[] copiedRoots = roots.ToArray();
                Resources = Array.AsReadOnly(copiedResources);
                Roots = Array.AsReadOnly(copiedRoots);
                HostedSlots = createHostedSlots(snapshot, materialSet, copiedRoots);
                HudPlan = GameplaySkinPreparedHudPlan.Create(snapshot, materialSet, HostedSlots);
                InitialEventState = createInitialEventState(snapshot);
                var resourcesById = new ReadOnlyDictionary<string, GameplaySkinPreparedSceneResource>(
                    copiedResources.ToDictionary(resource => resource.Id, StringComparer.Ordinal));
                Program = new GameplaySkinPreparedSceneProgram(document, resourcesById);
                textGlyphReservationsByNodeId = createTextGlyphReservations(document);
                IReadOnlyDictionary<string, double> textFontSizesByNodeId = createTextFontSizeReservations(document);

                if (!ReferenceEquals(materialSet.Snapshot, snapshot))
                    throw new ArgumentException("A prepared scene must retain the exact material/layout publication.", nameof(materialSet));

                long preparedNodeCount = copiedRoots.Sum(root => (long)countNodes(root));
                RuntimeReservation reservation = reserveRuntime(
                    copiedRoots,
                    HostedSlots,
                    snapshot.Context,
                    textGlyphReservationsByNodeId,
                    textFontSizesByNodeId);
                long preparedVisuals = checked(reservation.Instances + HudPlan.ReservedRuntimeFactoryInstances);
                long preparedEffectCount = checked(reservation.Effects + HudPlan.ReservedCaptureSurfaces);
                long preparedEffectSurfacePixels = checked(reservation.EffectSurfacePixels + HudPlan.ReservedCaptureSurfacePixels);
                long reservedTextGlyphs = reservation.TextGlyphs;
                long reservedTextGlyphPixels = reservation.TextGlyphPixels;

                if (preparedNodeCount > GameplaySkinPreparedSceneBudgets.MAX_PREPARED_NODES
                    || preparedVisuals > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES
                    || preparedEffectCount > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_EFFECT_INSTANCES
                    || preparedEffectSurfacePixels > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_EFFECT_SURFACE_PIXELS
                    || checked(preparedEffectSurfacePixels * 4) > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_EFFECT_SURFACE_BYTES
                    || reservedTextGlyphs > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_TEXT_GLYPHS
                    || reservedTextGlyphPixels > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_TEXT_GLYPH_PIXELS
                    || checked(reservedTextGlyphPixels * 4) > GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_TEXT_GLYPH_BYTES)
                {
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                }

                PreparedNodeCount = (int)preparedNodeCount;
                ReservedRuntimeInstanceCount = (int)preparedVisuals;
                PreparedEffectCount = (int)preparedEffectCount;
                PreparedEffectSurfacePixels = preparedEffectSurfacePixels;
                ReservedTextGlyphs = (int)reservedTextGlyphs;
                ReservedTextGlyphPixels = reservedTextGlyphPixels;
            }
            catch (OverflowException)
            {
                Dispose();
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal int GetTextGlyphReservation(string nodeId)
        {
            ArgumentException.ThrowIfNullOrEmpty(nodeId);
            return textGlyphReservationsByNodeId.GetValueOrDefault(nodeId);
        }

        /// <summary>
        /// Transfers the provisional resource lifetime to the exact publication which admits this scene.
        /// </summary>
        internal IDisposable? TakeRetirement() => Interlocked.Exchange(ref retirement, null);

        /// <summary>
        /// Releases resources when scene preparation completed but no publication claimed the result.
        /// </summary>
        public void Dispose() => Interlocked.Exchange(ref retirement, null)?.Dispose();

        internal static GameplaySkinPreparedScene CreateEmpty(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materialSet)
            => new GameplaySkinPreparedScene(
                snapshot,
                materialSet,
                Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant(),
                null,
                null,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                Array.Empty<GameplaySkinPreparedSceneNode>());

        private static IReadOnlyList<GameplaySkinPreparedHostedSlot> createHostedSlots(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materialSet,
            IReadOnlyList<GameplaySkinPreparedSceneNode> roots)
        {
            var sceneClaims = new Dictionary<GameplaySkinResolvedMaterialKey, List<GameplaySkinPreparedSceneNode>>();

            foreach (GameplaySkinPreparedSceneNode node in enumeratePreparedNodes(roots))
            {
                if (node.Slot == null || node.MaterialTarget == null)
                    continue;

                var key = new GameplaySkinResolvedMaterialKey(node.Slot, node.MaterialTarget);

                if (!sceneClaims.TryGetValue(key, out List<GameplaySkinPreparedSceneNode>? claims))
                    sceneClaims.Add(key, claims = new List<GameplaySkinPreparedSceneNode>());

                claims.Add(node);
            }

            var result = new List<GameplaySkinPreparedHostedSlot>(materialSet.Entries.Count);

            foreach (GameplaySkinResolvedMaterialEntry entry in materialSet.Entries)
            {
                GameplaySkinSceneHostRoute route;
                GameplaySkinSceneHostRoute failureRoute = GameplaySkinSceneHostRoute.Programmatic;
                GameplaySkinPublicSlotMaterial? publicMaterial = entry.State == GameplaySkinResolvedMaterialState.Provide
                    ? entry.Material as GameplaySkinPublicSlotMaterial
                    : null;

                if (entry.State == GameplaySkinResolvedMaterialState.Suppress)
                {
                    route = GameplaySkinSceneHostRoute.Suppressed;
                    failureRoute = GameplaySkinSceneHostRoute.Suppressed;
                }
                else if (publicMaterial == null
                         || GameplaySkinSceneHostPolicy.RequiresNativeGeometry(entry.Slot, snapshot.Context.RulesetId))
                    route = GameplaySkinSceneHostRoute.Specialised;
                else if (sceneClaims.ContainsKey(entry.Key))
                {
                    route = GameplaySkinSceneHostRoute.Scene;
                    failureRoute = publicMaterial.IsProgrammaticFallback
                        ? GameplaySkinSceneHostRoute.Programmatic
                        : GameplaySkinSceneHostRoute.Semantic;
                }
                else if (publicMaterial.IsProgrammaticFallback)
                    route = GameplaySkinSceneHostRoute.Programmatic;
                else
                    route = GameplaySkinSceneHostRoute.Semantic;

                result.Add(new GameplaySkinPreparedHostedSlot(
                    entry,
                    route,
                    failureRoute,
                    GameplaySkinSceneHostPolicy.LayerFor(entry.Slot),
                    resolveHostedSlotRect(snapshot, entry),
                    route == GameplaySkinSceneHostRoute.Specialised
                        ? GameplaySkinSceneHostPolicy.SpecialisedPoolCapacity(entry.Slot)
                        : 0,
                    sceneClaims.GetValueOrDefault(entry.Key) ?? Enumerable.Empty<GameplaySkinPreparedSceneNode>()));
            }

            return Array.AsReadOnly(result.ToArray());
        }

        private static GameplaySkinLayoutRect resolveHostedSlotRect(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialEntry entry)
        {
            GameplaySkinResolvedMaterialTarget target = entry.Target;
            GameplaySkinLayoutRect targetRect = target.Kind switch
            {
                GameplaySkinResolvedMaterialTargetKind.Global => snapshot.Context.SafeBounds,
                GameplaySkinResolvedMaterialTargetKind.Stage or GameplaySkinResolvedMaterialTargetKind.Group =>
                    snapshot.GetGroup(target.GroupId!).Rect,
                GameplaySkinResolvedMaterialTargetKind.Lane => snapshot.GetLane(target.LaneId!).Rect,
                _ => throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget),
            };

            if (!GameplaySkinSceneSurfaceResolver.TryResolve(
                    snapshot,
                    entry.Slot,
                    target,
                    targetRect,
                    null,
                    out GameplaySkinLayoutRect rect))
            {
                throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);
            }

            return rect;
        }

        private static RuntimeReservation reserveRuntime(
            IReadOnlyList<GameplaySkinPreparedSceneNode> roots,
            IReadOnlyList<GameplaySkinPreparedHostedSlot> hostedSlots,
            GameplaySkinLayoutContext context,
            IReadOnlyDictionary<string, int> glyphReservations,
            IReadOnlyDictionary<string, double> fontSizes)
        {
            IReadOnlyDictionary<GameplaySkinResolvedMaterialKey, GameplaySkinPreparedHostedSlot> plans =
                hostedSlots.ToDictionary(plan => plan.Key);
            RuntimeReservation result = default;

            foreach (GameplaySkinPreparedSceneNode root in roots)
                result = result.Add(reserveMountedTree(root, plans, context, glyphReservations, fontSizes));

            foreach (GameplaySkinPreparedHostedSlot plan in hostedSlots)
            {
                if (plan.Route == GameplaySkinSceneHostRoute.Semantic)
                {
                    bool isText = ReferenceEquals(plan.Key.Slot, GameplaySkinSlotCatalog.ComboDisplay)
                                  || ReferenceEquals(plan.Key.Slot, GameplaySkinSlotCatalog.TextHud)
                                  || ReferenceEquals(plan.Key.Slot, GameplaySkinSlotCatalog.JudgementDisplay);
                    result = result.Add(new RuntimeReservation(
                        1,
                        0,
                        0,
                        isText ? 32 : 0,
                        isText ? reserveGlyphPixels(32, 24, context) : 0));
                }

                if (plan.Route != GameplaySkinSceneHostRoute.Specialised)
                    continue;

                RuntimeReservation oneVisual = default;

                foreach (GameplaySkinPreparedSceneNode routed in plan.RoutedNodes)
                    oneVisual = oneVisual.Add(reserveWholeTree(routed, context, glyphReservations, fontSizes));

                // A native typed/public material with no author node still instantiates one immutable texture visual.
                if (oneVisual.Instances == 0 && plan.SpecialisedTexture != null)
                    oneVisual = new RuntimeReservation(1, 0, 0, 0, 0);

                result = result.Add(oneVisual.Multiply(plan.SpecialisedPoolCapacity));
            }

            return result;
        }

        private static RuntimeReservation reserveMountedTree(
            GameplaySkinPreparedSceneNode node,
            IReadOnlyDictionary<GameplaySkinResolvedMaterialKey, GameplaySkinPreparedHostedSlot> plans,
            GameplaySkinLayoutContext context,
            IReadOnlyDictionary<string, int> glyphReservations,
            IReadOnlyDictionary<string, double> fontSizes)
        {
            if (node.Slot != null && node.MaterialTarget != null)
            {
                var key = new GameplaySkinResolvedMaterialKey(node.Slot, node.MaterialTarget);

                if (!plans.TryGetValue(key, out GameplaySkinPreparedHostedSlot? plan)
                    || plan.Route != GameplaySkinSceneHostRoute.Scene)
                    return default;
            }

            RuntimeReservation result = reserveOneNode(node, context, glyphReservations, fontSizes);

            foreach (GameplaySkinPreparedSceneNode child in node.Children)
                result = result.Add(reserveMountedTree(child, plans, context, glyphReservations, fontSizes));

            return result;
        }

        private static RuntimeReservation reserveWholeTree(
            GameplaySkinPreparedSceneNode node,
            GameplaySkinLayoutContext context,
            IReadOnlyDictionary<string, int> glyphReservations,
            IReadOnlyDictionary<string, double> fontSizes)
        {
            RuntimeReservation result = reserveOneNode(node, context, glyphReservations, fontSizes);

            foreach (GameplaySkinPreparedSceneNode child in node.Children)
                result = result.Add(reserveWholeTree(child, context, glyphReservations, fontSizes));

            return result;
        }

        private static RuntimeReservation reserveOneNode(
            GameplaySkinPreparedSceneNode node,
            GameplaySkinLayoutContext context,
            IReadOnlyDictionary<string, int> glyphReservations,
            IReadOnlyDictionary<string, double> fontSizes)
        {
            int glyphs = node.Source.Type == GameplaySkinSceneNodeType.Text
                ? glyphReservations.GetValueOrDefault(node.Source.Id)
                : 0;
            double fontSize = fontSizes.GetValueOrDefault(node.Source.Id, 16);
            return new RuntimeReservation(
                1,
                node.Source.Effects.Count,
                countOwnEffectSurfacePixels(node, context),
                glyphs,
                glyphs == 0 ? 0 : reserveGlyphPixels(glyphs, fontSize, context));
        }

        private static IEnumerable<GameplaySkinPreparedSceneNode> enumeratePreparedNodes(
            IEnumerable<GameplaySkinPreparedSceneNode> roots)
        {
            foreach (GameplaySkinPreparedSceneNode root in roots)
            {
                yield return root;

                foreach (GameplaySkinPreparedSceneNode child in enumeratePreparedNodes(root.Children))
                    yield return child;
            }
        }

        private readonly struct RuntimeReservation
        {
            public readonly long Instances;
            public readonly long Effects;
            public readonly long EffectSurfacePixels;
            public readonly long TextGlyphs;
            public readonly long TextGlyphPixels;

            public RuntimeReservation(long instances, long effects, long effectSurfacePixels, long textGlyphs, long textGlyphPixels)
            {
                Instances = instances;
                Effects = effects;
                EffectSurfacePixels = effectSurfacePixels;
                TextGlyphs = textGlyphs;
                TextGlyphPixels = textGlyphPixels;
            }

            public RuntimeReservation Add(RuntimeReservation other)
                => new RuntimeReservation(
                    checked(Instances + other.Instances),
                    checked(Effects + other.Effects),
                    checked(EffectSurfacePixels + other.EffectSurfacePixels),
                    checked(TextGlyphs + other.TextGlyphs),
                    checked(TextGlyphPixels + other.TextGlyphPixels));

            public RuntimeReservation Multiply(int factor)
                => new RuntimeReservation(
                    checked(Instances * factor),
                    checked(Effects * factor),
                    checked(EffectSurfacePixels * factor),
                    checked(TextGlyphs * factor),
                    checked(TextGlyphPixels * factor));
        }

        private static int countNodes(GameplaySkinPreparedSceneNode node)
            => 1 + node.Children.Sum(countNodes);

        private static long countEffects(GameplaySkinPreparedSceneNode node)
            => node.Source.Effects.Count + node.Children.Sum(countEffects);

        private static long countEffectSurfacePixels(GameplaySkinPreparedSceneNode node, GameplaySkinLayoutContext context)
            => checked(countOwnEffectSurfacePixels(node, context)
                       + node.Children.Sum(child => countEffectSurfacePixels(child, context)));

        private static long countOwnEffectSurfacePixels(GameplaySkinPreparedSceneNode node, GameplaySkinLayoutContext context)
        {
            if (node.Source.Effects.Count == 0)
                return 0;

            double width = Math.Ceiling(context.RenderPixelWidth * node.Rect.Width / context.ScreenBounds.Width);
            double height = Math.Ceiling(context.RenderPixelHeight * node.Rect.Height / context.ScreenBounds.Height);
            double logicalPixelScale = Math.Max(context.DpiScale, context.RenderPixelHeight / 768d);
            long result = 0;

            foreach (GameplaySkinSceneEffect effect in node.Source.Effects)
            {
                double expansion = effect.Type switch
                {
                    "blur" or "glow" => getEffectNumber(effect, "radius"),
                    "outline" => getEffectNumber(effect, "width"),
                    "shadow" => getEffectNumber(effect, "blur")
                                + Math.Max(Math.Abs(getEffectNumber(effect, "x")), Math.Abs(getEffectNumber(effect, "y"))),
                    _ => throw new OverflowException(),
                };
                double padding = Math.Ceiling(expansion * logicalPixelScale);
                double pixels = (width + padding * 2)
                                * (height + padding * 2)
                                * GameplaySkinPreparedSceneBudgets.EFFECT_SURFACES_PER_EFFECT;

                if (!double.IsFinite(pixels) || pixels > long.MaxValue)
                    throw new OverflowException();

                result = checked(result + (long)Math.Ceiling(pixels));
            }

            return result;

            static double getEffectNumber(GameplaySkinSceneEffect effect, string property)
                => effect.Properties.TryGetValue(property, out GameplaySkinScenePropertyValue? value)
                   && value.Kind == GameplaySkinScenePropertyValueKind.Number
                    ? value.NumberValue
                    : 0;
        }

        private static long reserveGlyphPixels(int glyphs, double fontSize, GameplaySkinLayoutContext context)
        {
            double pixelScale = Math.Max(context.DpiScale, context.RenderPixelHeight / 768d);
            long cell = checked((long)Math.Ceiling(fontSize * pixelScale) + GameplaySkinPreparedSceneBudgets.TEXT_GLYPH_PADDING_PIXELS * 2);
            // Four-times cell area covers atlas padding, shelf fragmentation and glyph bearings; byte cost is
            // accounted separately as RGBA in ReservedTextGlyphBytes.
            long cells = checked(glyphs * cell * cell * 4);
            // Reserve at least one complete atlas page for every independently rendered text visual. This is
            // intentionally conservative and keeps framework page padding/allocation inside the hard admission cap.
            long atlasPage = (long)GameplaySkinPreparedSceneBudgets.TEXT_ATLAS_PAGE_SIZE
                             * GameplaySkinPreparedSceneBudgets.TEXT_ATLAS_PAGE_SIZE;
            return Math.Max(cells, atlasPage);
        }

        private static long countReservedTextGlyphs(
            GameplaySkinPreparedSceneNode node,
            IReadOnlyDictionary<string, int> reservations)
            => (node.Source.Type == GameplaySkinSceneNodeType.Text ? reservations.GetValueOrDefault(node.Source.Id) : 0)
               + node.Children.Sum(child => countReservedTextGlyphs(child, reservations));

        private static long countReservedTextGlyphPixels(
            GameplaySkinPreparedSceneNode node,
            IReadOnlyDictionary<string, int> reservations,
            IReadOnlyDictionary<string, double> fontSizes)
        {
            long current = 0;

            if (node.Source.Type == GameplaySkinSceneNodeType.Text)
            {
                int glyphs = reservations.GetValueOrDefault(node.Source.Id);
                long fontSize = (long)Math.Ceiling(fontSizes.GetValueOrDefault(node.Source.Id, 16));
                current = checked(glyphs * fontSize * fontSize);
            }

            return checked(current + node.Children.Sum(child => countReservedTextGlyphPixels(child, reservations, fontSizes)));
        }

        private static IReadOnlyDictionary<string, int> createTextGlyphReservations(GameplaySkinSceneDocument? document)
        {
            var reservations = new Dictionary<string, int>(StringComparer.Ordinal);

            if (document == null)
                return new ReadOnlyDictionary<string, int>(reservations);

            IEnumerable<GameplaySkinSceneNode> nodes = enumerateNodes(document.Root)
                                                       .Concat(document.Templates.SelectMany(template => enumerateNodes(template.Root)));

            foreach (GameplaySkinSceneNode node in nodes)
            {
                if (node.Type != GameplaySkinSceneNodeType.Text)
                    continue;

                int initial = node.Properties.TryGetValue("text", out GameplaySkinScenePropertyValue? value)
                              && value.Kind == GameplaySkinScenePropertyValueKind.String
                    ? value.StringValue?.Length ?? 0
                    : 0;
                reservations.Add(node.Id, initial);
            }

            foreach (GameplaySkinSceneTrack track in document.Tracks)
            {
                if (track.Property == GameplaySkinSceneProperty.Text && reservations.ContainsKey(track.TargetNodeId))
                    reserve(track.TargetNodeId, track.Keyframes.Max(keyframe => textLength(keyframe.Value)));
            }

            foreach (GameplaySkinSceneStateAssignment assignment in document.StateMachines
                                                                                 .SelectMany(machine => machine.States)
                                                                                 .SelectMany(state => state.Assignments))
            {
                if (assignment.Property == GameplaySkinSceneProperty.Text && reservations.ContainsKey(assignment.TargetNodeId))
                    reserve(assignment.TargetNodeId, textLength(assignment.Value));
            }

            foreach (GameplaySkinSceneBinding binding in document.Bindings)
            {
                if (binding.Property == GameplaySkinSceneProperty.Text && reservations.ContainsKey(binding.TargetNodeId))
                    reserve(binding.TargetNodeId, GameplaySkinPreparedSceneBudgets.MAX_DYNAMIC_TEXT_GLYPHS_PER_NODE);
            }

            return new ReadOnlyDictionary<string, int>(reservations);

            void reserve(string nodeId, int glyphs)
                => reservations[nodeId] = Math.Max(reservations[nodeId], glyphs);

            static int textLength(GameplaySkinScenePropertyValue value) => value.Kind switch
            {
                GameplaySkinScenePropertyValueKind.String => value.StringValue?.Length ?? 0,
                GameplaySkinScenePropertyValueKind.Boolean => 5,
                GameplaySkinScenePropertyValueKind.Number => GameplaySkinPreparedSceneBudgets.MAX_DYNAMIC_TEXT_GLYPHS_PER_NODE,
                _ => 0,
            };
        }

        private static IReadOnlyDictionary<string, double> createTextFontSizeReservations(GameplaySkinSceneDocument? document)
        {
            var reservations = new Dictionary<string, double>(StringComparer.Ordinal);

            if (document == null)
                return new ReadOnlyDictionary<string, double>(reservations);

            IEnumerable<GameplaySkinSceneNode> nodes = enumerateNodes(document.Root)
                                                       .Concat(document.Templates.SelectMany(template => enumerateNodes(template.Root)));

            foreach (GameplaySkinSceneNode node in nodes)
            {
                if (node.Type != GameplaySkinSceneNodeType.Text)
                    continue;

                double initial = node.Properties.TryGetValue("font-size", out GameplaySkinScenePropertyValue? value)
                                 && value.Kind == GameplaySkinScenePropertyValueKind.Number
                    ? value.NumberValue
                    : 16;
                reservations.Add(node.Id, initial);
            }

            foreach (GameplaySkinSceneTrack track in document.Tracks)
            {
                if (track.Property == GameplaySkinSceneProperty.FontSize && reservations.ContainsKey(track.TargetNodeId))
                    reserve(track.TargetNodeId, track.Keyframes.Max(keyframe => keyframe.Value.NumberValue));
            }

            foreach (GameplaySkinSceneStateAssignment assignment in document.StateMachines
                                                                                 .SelectMany(machine => machine.States)
                                                                                 .SelectMany(state => state.Assignments))
            {
                if (assignment.Property == GameplaySkinSceneProperty.FontSize && reservations.ContainsKey(assignment.TargetNodeId))
                    reserve(assignment.TargetNodeId, assignment.Value.NumberValue);
            }

            foreach (GameplaySkinSceneBinding binding in document.Bindings)
            {
                if (binding.Property == GameplaySkinSceneProperty.FontSize && reservations.ContainsKey(binding.TargetNodeId))
                    reserve(binding.TargetNodeId, GameplaySkinSceneBudgets.MAX_FONT_SIZE);
            }

            return new ReadOnlyDictionary<string, double>(reservations);

            void reserve(string nodeId, double fontSize)
                => reservations[nodeId] = Math.Max(reservations[nodeId], fontSize);
        }

        private static IEnumerable<GameplaySkinSceneNode> enumerateNodes(GameplaySkinSceneNode root)
        {
            yield return root;

            foreach (GameplaySkinSceneNode child in root.Children)
            {
                foreach (GameplaySkinSceneNode descendant in enumerateNodes(child))
                    yield return descendant;
            }
        }

        private static GameplaySkinEventStateSnapshot createInitialEventState(GameplaySkinLayoutSnapshot snapshot)
        {
            GameplaySkinInputStateSnapshot[] inputs = snapshot.Context.Topology.LanesInLogicalOrder
                .Select(lane => new GameplaySkinInputStateSnapshot(lane.Identity.Group.Id, lane.Identity.Id, false, 0))
                .ToArray();
            GameplaySkinBgaStateSnapshot[] bga = snapshot.BgaViewports
                .Select((viewport, index) => new GameplaySkinBgaStateSnapshot(index, viewport, GameplaySkinBgaContentState.Empty, 0))
                .ToArray();

            return new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Loaded,
                inputs,
                Array.Empty<GameplaySkinObjectStateSnapshot>(),
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 1),
                new GameplaySkinTimingStateSnapshot(0, -1, 120, false, 1),
                bga);
        }

        private static GameplaySkinScenePreparationException fail(GameplaySkinSceneDiagnosticCode code)
            => new GameplaySkinScenePreparationException(code);
    }

    /// <summary>
    /// Sole C5 background preparer. Consumers receive only its immutable result and cannot reopen package content.
    /// </summary>
    public static class GameplaySkinScenePreparer
    {
        public static GameplaySkinPreparedScene Prepare(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materialSet,
            ISkinSource skinSource,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(materialSet);
            ArgumentNullException.ThrowIfNull(skinSource);
            cancellationToken.ThrowIfCancellationRequested();

            if (!ReferenceEquals(materialSet.Snapshot, snapshot))
                throw new ArgumentException("Scene preparation requires the exact resolved material set.", nameof(materialSet));

            if (snapshot.Context.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility)
                return GameplaySkinPreparedScene.CreateEmpty(snapshot, materialSet);

            Skin selected = findExactSelectedSkin(snapshot.Context.PackageRevision, skinSource);

            if (!selected.AllowsGameplaySkinDocumentAuthoring)
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);

            GameplaySkinDocument selectedDocument = selected.GameplaySkinDocument.BindToPublication(snapshot);

            if (selectedDocument.Identity.SourceId != snapshot.Context.PackageRevision.RecordId
                || selectedDocument.Identity.PackageRevision != snapshot.Context.PackageRevision.Generation
                || selectedDocument.Identity.CurrentRevision != snapshot.Context.PackageRevision.Generation
                || selectedDocument.Identity.LayoutRevision != snapshot.Context.LayoutRevision)
            {
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }

            if (!tryCaptureResource(
                    selected,
                    GameplaySkinSceneContracts.MANIFEST_FILE_NAME,
                    GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES,
                    out byte[] manifestBytes))
            {
                return GameplaySkinPreparedScene.CreateEmpty(snapshot, materialSet);
            }

            cancellationToken.ThrowIfCancellationRequested();
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> manifestResult = GameplaySkinSceneCodec.DecodeManifest(manifestBytes);
            GameplaySkinSceneManifest manifest = requireValid(manifestResult);

            if (!tryCaptureResource(
                    selected,
                    manifest.SceneFile,
                    GameplaySkinSceneBudgets.MAX_SCENE_BYTES,
                    out byte[] sceneBytes))
            {
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
            }

            cancellationToken.ThrowIfCancellationRequested();
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> sceneResult = GameplaySkinSceneCodec.DecodeScene(sceneBytes, manifest);
            GameplaySkinSceneDocument document = requireValid(sceneResult);
            var preparedResources = new List<GameplaySkinPreparedSceneResource>(manifest.Resources.Count);
            var preparedResourceRetirement = new PreparedSceneResourceRetirement();
            var capturedResources = new List<CapturedSceneResource>(manifest.Resources.Count);
            int totalEncodedBytes = 0;
            long totalTexturePixels = 0;
            long totalDecodedBytes = 0;

            try
            {
                // Capture and inspect every resource before decoding any image or allocating any GPU texture.
                // Image.Identify() reads format metadata only; this first pass therefore rejects both individual
                // and aggregate decompression bombs while the exact previous publication remains untouched.
                foreach (GameplaySkinSceneResource resource in manifest.Resources)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!tryCaptureResource(selected, resource.Path, GameplaySkinPreparedSceneBudgets.MAX_RESOURCE_BYTES, out byte[] bytes))
                        throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);

                    totalEncodedBytes = checked(totalEncodedBytes + bytes.Length);

                    if (totalEncodedBytes > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_RESOURCE_BYTES)
                        throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    long decodedBytes = bytes.Length;

                    if (resource.Type == GameplaySkinSceneResourceType.Texture)
                    {
                        ImageInfo info = identifyTexture(bytes);
                        long pixels = checked((long)info.Width * info.Height);

                        if (pixels <= 0 || pixels > GameplaySkinPreparedSceneBudgets.MAX_TEXTURE_PIXELS)
                            throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                        decodedBytes = checked(pixels * 4);
                        totalTexturePixels = checked(totalTexturePixels + pixels);

                        if (totalTexturePixels > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_TEXTURE_PIXELS)
                            throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    }

                    totalDecodedBytes = checked(totalDecodedBytes + decodedBytes);

                    if (totalDecodedBytes > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_DECODED_TEXTURE_BYTES)
                        throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    capturedResources.Add(new CapturedSceneResource(resource, bytes, decodedBytes));
                }

                // Only a package whose complete encoded and decoded footprint passed the first pass may enter the
                // framework decoder. The captured bytes are the same immutable bytes inspected above.
                foreach (CapturedSceneResource captured in capturedResources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Texture? texture = null;

                    if (captured.Source.Type == GameplaySkinSceneResourceType.Texture)
                    {
                        texture = prepareTexture(selected, captured.Source.Path, captured.Bytes);
                        preparedResourceRetirement.Add(texture);
                    }

                    preparedResources.Add(new GameplaySkinPreparedSceneResource(
                        captured.Source,
                        Convert.ToHexString(SHA256.HashData(captured.Bytes)).ToLowerInvariant(),
                        captured.Bytes.Length,
                        captured.DecodedBytes,
                        texture));
                }

                var resourcesById = preparedResources.ToDictionary(resource => resource.Id, StringComparer.Ordinal);
                var programmedNodeIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (GameplaySkinSceneTrack track in document.Tracks)
                    programmedNodeIds.Add(track.TargetNodeId);

                foreach (GameplaySkinSceneStateAssignment assignment in document.StateMachines
                                                                                 .SelectMany(machine => machine.States)
                                                                                 .SelectMany(state => state.Assignments))
                {
                    programmedNodeIds.Add(assignment.TargetNodeId);
                }

                foreach (GameplaySkinSceneBinding binding in document.Bindings)
                    programmedNodeIds.Add(binding.TargetNodeId);

                foreach (GameplaySkinSceneVariant variant in document.Variants)
                    programmedNodeIds.Add(variant.TargetNodeId);

                int preparedNodeCount = 0;
                var roots = new List<GameplaySkinPreparedSceneNode>
                {
                    prepareNode(document.Root, null, string.Empty, snapshot, materialSet, selectedDocument, resourcesById, programmedNodeIds, ref preparedNodeCount,
                        null, null, false, cancellationToken),
                };

                IReadOnlyDictionary<string, GameplaySkinSceneTemplate> templates = document.Templates.ToDictionary(template => template.Id, StringComparer.Ordinal);

                foreach (GameplaySkinSceneInstance instance in document.Instances)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!templates.TryGetValue(instance.TemplateId, out GameplaySkinSceneTemplate? template))
                        throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);

                    roots.Add(prepareNode(
                        template.Root,
                        instance.Target,
                        $"{instance.Id}/",
                        snapshot,
                        materialSet,
                        selectedDocument,
                        resourcesById,
                        programmedNodeIds,
                        ref preparedNodeCount,
                        null,
                        null,
                        false,
                        cancellationToken));
                }

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                appendHash(hash, manifestBytes);
                appendHash(hash, sceneBytes);

                foreach (GameplaySkinPreparedSceneResource resource in preparedResources)
                {
                    appendHash(hash, Encoding.UTF8.GetBytes(resource.Id));
                    appendHash(hash, Convert.FromHexString(resource.ContentRevision));
                }

                string contentRevision = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                return new GameplaySkinPreparedScene(
                    snapshot,
                    materialSet,
                    contentRevision,
                    manifest,
                    document,
                    preparedResources,
                    roots,
                    preparedResourceRetirement);
            }
            catch (OverflowException)
            {
                preparedResourceRetirement.Dispose();
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
            catch
            {
                preparedResourceRetirement.Dispose();
                throw;
            }
        }

        private static GameplaySkinPreparedSceneNode prepareNode(
            GameplaySkinSceneNode node,
            GameplaySkinSceneTarget? rootTargetOverride,
            string instancePrefix,
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinDocument selectedDocument,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources,
            ISet<string> programmedNodeIds,
            ref int preparedNodeCount,
            GameplaySkinSceneLayer? parentLayer,
            GameplaySkinResolvedMaterialKey? inheritedOwner,
            bool parentAllowsLayerDispatch,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (++preparedNodeCount > GameplaySkinPreparedSceneBudgets.MAX_PREPARED_NODES)
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            GameplaySkinSceneTarget target = rootTargetOverride ?? node.Target;
            (GameplaySkinLayoutRect rect, GameplaySkinResolvedMaterialTarget? materialTarget) = resolveTarget(target, snapshot);
            GameplaySkinSlotDescriptor? slot = null;

            if (node.SlotId != null)
            {
                if (!GameplaySkinSlotCatalog.TryGet(node.SlotId, out slot) || materialTarget == null)
                    throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);

                if (!materialSet.RuntimeSupportProfile.IsSupported(slot))
                    throw fail(GameplaySkinSceneDiagnosticCode.RuntimeSlotNotApplicable);

                if ((slot.AllowedScopes & materialTarget.Scope) == 0
                    || GameplaySkinSlotApplicabilityValidator.Validate(slot, snapshot, materialTarget)
                    != GameplaySkinSlotApplicabilityResult.Applicable)
                {
                    throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);
                }

                GameplaySkinDocumentEntry selectedDeclaration = selectedDocument.GetEntry(slot, materialTarget);

                // A scene node is the visual body of this exact package's existing C4 Provide declaration. It is not
                // a fourth presence state and must never turn Absent/Inherit/invalid content (or a lower-authority
                // fallback Provide) into an authored replacement.
                if (selectedDeclaration.Presence != GameplaySkinDocumentDeclarationPresence.Declared
                    || selectedDeclaration.Validity != GameplaySkinDocumentValueValidity.Valid
                    || selectedDeclaration.Operation != GameplaySkinDocumentOperation.Provide)
                {
                    throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
                }

                if (!GameplaySkinSceneSurfaceResolver.TryResolve(
                        snapshot,
                        slot,
                        materialTarget,
                        rect,
                        target.Kind,
                        out rect))
                {
                    throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);
                }
            }

            if (slot != null && inheritedOwner != null)
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);

            if (slot == null && inheritedOwner != null && !inheritedOwner.Target.Equals(materialTarget))
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);

            GameplaySkinResolvedMaterialKey? owningSlotKey = slot != null && materialTarget != null
                ? new GameplaySkinResolvedMaterialKey(slot, materialTarget)
                : inheritedOwner;

            if (slot != null
                && (!materialSet.TryGet(owningSlotKey!, out GameplaySkinResolvedMaterialEntry? owningEntry)
                    || owningEntry.State != GameplaySkinResolvedMaterialState.Provide
                    || !owningEntry.Source.IsSelectedDocumentDeclaration
                    || !string.Equals(
                        owningEntry.Source.ContentRevision,
                        selectedDocument.Identity.ContentRevision,
                        StringComparison.Ordinal)))
            {
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
            }
            GameplaySkinSceneLayer layer = slot == null
                ? parentLayer ?? GameplaySkinSceneLayer.Underlay
                : GameplaySkinSceneHostPolicy.LayerFor(slot);

            if (parentLayer.HasValue && parentLayer.Value != layer && !parentAllowsLayerDispatch)
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);

            bool allowsLayerDispatch = node.Type == GameplaySkinSceneNodeType.Container
                                       && slot == null
                                       && owningSlotKey == null
                                       && node.ResourceId == null
                                       && node.Blend == GameplaySkinSceneBlendMode.Inherit
                                       && node.Properties.Count == 0
                                       && node.Effects.Count == 0
                                       && !programmedNodeIds.Contains(node.Id);

            // A slot-less node may only be a completely inert structural dispatcher which lets independently
            // owned public-slot roots reach their frozen scene strata. Rendering or animating anything outside
            // a resolved material key would create an unadvertised capability and bypass Provide/Inherit/Suppress.
            if (owningSlotKey == null && !allowsLayerDispatch)
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);

            GameplaySkinPreparedSceneResource? resource = null;

            if (node.ResourceId != null && !resources.TryGetValue(node.ResourceId, out resource))
                throw fail(GameplaySkinSceneDiagnosticCode.UnknownResource);

            Texture? resolvedTexture = resource?.Texture;

            if (node.Type == GameplaySkinSceneNodeType.Sprite && resolvedTexture == null)
            {
                if (owningSlotKey == null
                    || !materialSet.TryGet(owningSlotKey, out GameplaySkinResolvedMaterialEntry? ownerEntry)
                    || ownerEntry?.State != GameplaySkinResolvedMaterialState.Provide
                    || ownerEntry.Material is not GameplaySkinPublicSlotMaterial publicMaterial
                    || publicMaterial.Texture == null)
                {
                    throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
                }

                resolvedTexture = publicMaterial.Texture;
            }

            var children = new List<GameplaySkinPreparedSceneNode>(node.Children.Count);

            foreach (GameplaySkinSceneNode child in node.Children)
            {
                children.Add(prepareNode(
                    child,
                    rootTargetOverride,
                    instancePrefix,
                    snapshot,
                    materialSet,
                    selectedDocument,
                    resources,
                    programmedNodeIds,
                    ref preparedNodeCount,
                    layer,
                    owningSlotKey,
                    allowsLayerDispatch,
                    cancellationToken));
            }

            return new GameplaySkinPreparedSceneNode(
                instancePrefix + node.Id,
                node,
                target,
                rect,
                materialTarget,
                slot,
                resource,
                resolvedTexture,
                children,
                layer,
                owningSlotKey,
                allowsLayerDispatch);
        }

        private static (GameplaySkinLayoutRect Rect, GameplaySkinResolvedMaterialTarget? MaterialTarget) resolveTarget(
            GameplaySkinSceneTarget target,
            GameplaySkinLayoutSnapshot snapshot)
        {
            GameplaySkinLaneTopologySnapshot topology = snapshot.Context.Topology;

            switch (target.Kind)
            {
                case GameplaySkinSceneTargetKind.Global:
                    requireNoIdentity(target);
                    return (snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global);

                case GameplaySkinSceneTargetKind.Stage:
                case GameplaySkinSceneTargetKind.Group:
                {
                    GameplaySkinLaneTopologyGroup group = resolveGroup(target, topology);
                    GameplaySkinLayoutRect rect = snapshot.GetGroup(group.Identity.Id).Rect;
                    return (rect, target.Kind == GameplaySkinSceneTargetKind.Stage
                        ? GameplaySkinResolvedMaterialTarget.ForStage(group)
                        : GameplaySkinResolvedMaterialTarget.ForGroup(group));
                }

                case GameplaySkinSceneTargetKind.Lane:
                {
                    GameplaySkinLaneTopologyEntry lane = resolveLane(target, topology);
                    if (!topology.TryGetGroup(lane.Identity.Group.Id, out GameplaySkinLaneTopologyGroup? group) || group == null)
                        throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);

                    return (snapshot.GetLane(lane.Identity.Id).Rect, GameplaySkinResolvedMaterialTarget.ForLane(group, lane));
                }

                case GameplaySkinSceneTargetKind.Hud:
                    if (target.StableId == null)
                    {
                        requireNoIdentity(target);
                        return (snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global);
                    }

                    GameplaySkinLaneTopologyGroup hudGroup = resolveGroup(target, topology);
                    return (snapshot.GetGroup(hudGroup.Identity.Id).Rect, GameplaySkinResolvedMaterialTarget.ForStage(hudGroup));

                case GameplaySkinSceneTargetKind.Bga:
                {
                    if (target.StableId != null)
                        throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);

                    int index = target.Index ?? 0;

                    if (index < 0 || index >= snapshot.BgaViewports.Count)
                        throw fail(GameplaySkinSceneDiagnosticCode.InvalidIndex);

                    return (snapshot.BgaViewports[index], GameplaySkinResolvedMaterialTarget.Global);
                }

                default:
                    throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);
            }
        }

        private static GameplaySkinLaneTopologyGroup resolveGroup(GameplaySkinSceneTarget target, GameplaySkinLaneTopologySnapshot topology)
        {
            GameplaySkinLaneTopologyGroup? group = target.StableId == null
                ? null
                : topology.GroupsInLogicalOrder.FirstOrDefault(candidate => string.Equals(candidate.Identity.Id.Value, target.StableId, StringComparison.Ordinal));

            if (group == null
                || target.Index != group.LogicalIndex)
            {
                throw fail(target.Index == null ? GameplaySkinSceneDiagnosticCode.InvalidIndex : GameplaySkinSceneDiagnosticCode.UnknownTarget);
            }

            return group;
        }

        private static GameplaySkinLaneTopologyEntry resolveLane(GameplaySkinSceneTarget target, GameplaySkinLaneTopologySnapshot topology)
        {
            GameplaySkinLaneTopologyEntry? lane = target.StableId == null
                ? null
                : topology.LanesInLogicalOrder.FirstOrDefault(candidate => string.Equals(candidate.Identity.Id.Value, target.StableId, StringComparison.Ordinal));

            if (lane == null
                || target.Index != lane.GlobalLogicalIndex)
            {
                throw fail(target.Index == null ? GameplaySkinSceneDiagnosticCode.InvalidIndex : GameplaySkinSceneDiagnosticCode.UnknownTarget);
            }

            return lane;
        }

        private static void requireNoIdentity(GameplaySkinSceneTarget target)
        {
            if (target.StableId != null || target.Index != null)
                throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);
        }

        private static Skin findExactSelectedSkin(GameplaySkinPackageRevision package, ISkinSource source)
        {
            foreach (ISkin candidate in source.AllSources)
            {
                ISkin raw = candidate;

                while (raw is ISkinTransformer transformer && !ReferenceEquals(transformer.Skin, raw))
                    raw = transformer.Skin;

                if (raw is Skin skin && package.RetainsExactSource(skin))
                    return skin;
            }

            throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
        }

        private static bool tryCaptureResource(Skin selected, string path, int maximumBytes, out byte[] bytes)
        {
            try
            {
                return selected.TryCaptureGameplaySkinResource(path, maximumBytes, out bytes);
            }
            catch (InvalidDataException)
            {
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
            catch (ArgumentException)
            {
                throw fail(GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
            }
            catch (IOException)
            {
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            catch (UnauthorizedAccessException)
            {
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            catch (OutOfMemoryException)
            {
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
        }

        private static Texture prepareTexture(Skin selected, string path, byte[] bytes)
        {
            try
            {
                return selected.PrepareGameplaySkinTexture(path, bytes)
                       ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            catch (GameplaySkinScenePreparationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException)
            {
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
            catch
            {
                // Texture loader exception messages may contain decoder or path details. Collapse corrupt author
                // content into the stable value-free preparation diagnostic before it crosses the C5 boundary.
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
        }

        private static ImageInfo identifyTexture(byte[] bytes)
        {
            try
            {
                return Image.Identify(bytes)
                       ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            catch (GameplaySkinScenePreparationException)
            {
                throw;
            }
            catch (OutOfMemoryException)
            {
                // Treat metadata which cannot be inspected within the bounded preparation process as author
                // content exceeding the C5 decode budget. Do not leak decoder details or enter full decode.
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
            catch
            {
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
        }

        private static T requireValid<T>(GameplaySkinSceneDecodeResult<T> result)
            where T : class
        {
            if (result.Status != GameplaySkinSceneDecodeStatus.Valid || result.Value == null)
                throw fail(result.Diagnostics.FirstOrDefault()?.Code ?? GameplaySkinSceneDiagnosticCode.InvalidReference);

            return result.Value;
        }

        private static void appendHash(IncrementalHash hash, byte[] bytes)
        {
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }

        private static GameplaySkinScenePreparationException fail(GameplaySkinSceneDiagnosticCode code)
            => new GameplaySkinScenePreparationException(code);

        private sealed class CapturedSceneResource
        {
            public GameplaySkinSceneResource Source { get; }

            public byte[] Bytes { get; }

            public long DecodedBytes { get; }

            public CapturedSceneResource(GameplaySkinSceneResource source, byte[] bytes, long decodedBytes)
            {
                Source = source;
                Bytes = bytes;
                DecodedBytes = decodedBytes;
            }
        }

        private sealed class PreparedSceneResourceRetirement : IDisposable
        {
            private List<IDisposable>? resources = new List<IDisposable>();

            public void Add(IDisposable resource)
            {
                ArgumentNullException.ThrowIfNull(resource);
                List<IDisposable> current = Volatile.Read(ref resources)
                                            ?? throw new ObjectDisposedException(nameof(PreparedSceneResourceRetirement));
                current.Add(resource);
            }

            public void Dispose()
            {
                List<IDisposable>? current = Interlocked.Exchange(ref resources, null);

                if (current == null)
                    return;

                Exception? firstFailure = null;

                for (int i = current.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        current[i].Dispose();
                    }
                    catch (Exception exception)
                    {
                        firstFailure ??= exception;
                    }
                }

                if (firstFailure != null)
                    ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
        }
    }
}
