// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Stable author-facing identifiers and fixed package filenames for the first declarative scene contract.
    /// </summary>
    public static class GameplaySkinSceneContracts
    {
        public const string MANIFEST_CONTRACT_ID = "oms-gameplay-skin-manifest.v1";
        public const string SCENE_CONTRACT_ID = "oms-gameplay-skin-scene.v1";
        public const string EVENT_CONTRACT_ID = "oms-gameplay-skin-event.v1";
        public const string MANIFEST_FILE_NAME = "gameplay-skin.json";
        public const string SCENE_FILE_NAME = "gameplay-skin.scene.json";
    }

    /// <summary>
    /// Hard decode-time limits. Preparation and rendering may impose stricter host-specific limits later.
    /// </summary>
    public static class GameplaySkinSceneBudgets
    {
        public const int MAX_MANIFEST_BYTES = 64 * 1024;
        public const int MAX_SCENE_BYTES = 512 * 1024;
        public const int MAX_JSON_DEPTH = 32;
        public const int MAX_STABLE_ID_LENGTH = 128;
        public const int MAX_RESOURCE_PATH_LENGTH = 240;
        public const int MAX_RESOURCES = 256;
        public const int MAX_NODES = 2048;
        public const int MAX_NODE_DEPTH = 16;
        public const int MAX_CHILDREN_PER_NODE = 128;
        public const int MAX_PROPERTIES_PER_OBJECT = 32;
        public const int MAX_EFFECTS_PER_NODE = 8;
        public const int MAX_EFFECTS = 512;
        public const int MAX_TRACKS = 512;
        public const int MAX_KEYFRAMES_PER_TRACK = 512;
        public const int MAX_KEYFRAMES = 8192;
        public const int MAX_STATE_MACHINES = 128;
        public const int MAX_STATES = 1024;
        public const int MAX_STATE_ASSIGNMENTS = 4096;
        public const int MAX_TRANSITIONS = 2048;
        public const int MAX_BINDINGS = 512;
        public const int MAX_VARIANTS = 512;
        public const int MAX_VARIANT_CASES_PER_VARIANT = 32;
        public const int MAX_VARIANT_CASES = 4096;
        public const int MAX_TEMPLATES = 128;
        public const int MAX_INSTANCES = 1024;
        public const int MAX_EXPANDED_TEMPLATE_NODES = 8192;
        public const int MAX_PROPERTY_APPLICATIONS_PER_FRAME = 16_384;
        public const int MAX_STATE_PROPERTY_APPLICATIONS_PER_EVENT = 8_192;
        public const int MAX_TEXT_CHARACTERS = 64 * 1024;
        public const double MAX_TRACK_TIME = 86_400_000;
        public const double MAX_ABSOLUTE_POSITION = 4;
        public const double MAX_RELATIVE_SIZE = 4;
        public const double MAX_SCALE = 8;
        public const double MAX_ABSOLUTE_ROTATION = 36_000;
        public const double MAX_ABSOLUTE_Z = 32_768;
        public const double MIN_FONT_SIZE = 1;
        public const double MAX_FONT_SIZE = 128;
        public const double MAX_CORNER_RADIUS = 256;
        public const double MAX_EFFECT_BLUR_RADIUS = 64;
        public const double MAX_EFFECT_OUTLINE_WIDTH = 32;
        public const double MAX_EFFECT_SHADOW_OFFSET = 128;
        public const double MAX_EFFECT_STRENGTH = 4;
    }

    /// <summary>
    /// The sole numeric range authority for authored and engine-bound V1 scene properties. These finite ranges keep
    /// conversion to framework floats, text atlases and allowlisted effect buffers bounded and deterministic.
    /// </summary>
    internal static class GameplaySkinSceneNumericRange
    {
        public static bool IsAllowed(GameplaySkinSceneProperty property, double value)
        {
            if (!double.IsFinite(value))
                return false;

            return property switch
            {
                GameplaySkinSceneProperty.Opacity => value is >= 0 and <= 1,
                GameplaySkinSceneProperty.X or GameplaySkinSceneProperty.Y => Math.Abs(value) <= GameplaySkinSceneBudgets.MAX_ABSOLUTE_POSITION,
                GameplaySkinSceneProperty.Width or GameplaySkinSceneProperty.Height => value is >= 0 and <= GameplaySkinSceneBudgets.MAX_RELATIVE_SIZE,
                GameplaySkinSceneProperty.ScaleX or GameplaySkinSceneProperty.ScaleY => value is >= 0 and <= GameplaySkinSceneBudgets.MAX_SCALE,
                GameplaySkinSceneProperty.Rotation => Math.Abs(value) <= GameplaySkinSceneBudgets.MAX_ABSOLUTE_ROTATION,
                GameplaySkinSceneProperty.Z => Math.Abs(value) <= GameplaySkinSceneBudgets.MAX_ABSOLUTE_Z,
                GameplaySkinSceneProperty.FontSize => value is >= GameplaySkinSceneBudgets.MIN_FONT_SIZE and <= GameplaySkinSceneBudgets.MAX_FONT_SIZE,
                GameplaySkinSceneProperty.CornerRadius => value is >= 0 and <= GameplaySkinSceneBudgets.MAX_CORNER_RADIUS,
                // Numeric sources may be rendered as bounded text without changing geometry or allocating a new atlas.
                GameplaySkinSceneProperty.Text => true,
                _ => false,
            };
        }

        public static double ClampBoundValue(GameplaySkinSceneProperty property, double value)
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A bound scene value must be finite.");

            return property switch
            {
                GameplaySkinSceneProperty.Opacity => Math.Clamp(value, 0, 1),
                GameplaySkinSceneProperty.X or GameplaySkinSceneProperty.Y => Math.Clamp(
                    value,
                    -GameplaySkinSceneBudgets.MAX_ABSOLUTE_POSITION,
                    GameplaySkinSceneBudgets.MAX_ABSOLUTE_POSITION),
                GameplaySkinSceneProperty.Width or GameplaySkinSceneProperty.Height => Math.Clamp(
                    value,
                    0,
                    GameplaySkinSceneBudgets.MAX_RELATIVE_SIZE),
                GameplaySkinSceneProperty.ScaleX or GameplaySkinSceneProperty.ScaleY => Math.Clamp(
                    value,
                    0,
                    GameplaySkinSceneBudgets.MAX_SCALE),
                GameplaySkinSceneProperty.Rotation => Math.Clamp(
                    value,
                    -GameplaySkinSceneBudgets.MAX_ABSOLUTE_ROTATION,
                    GameplaySkinSceneBudgets.MAX_ABSOLUTE_ROTATION),
                GameplaySkinSceneProperty.Z => Math.Clamp(
                    value,
                    -GameplaySkinSceneBudgets.MAX_ABSOLUTE_Z,
                    GameplaySkinSceneBudgets.MAX_ABSOLUTE_Z),
                GameplaySkinSceneProperty.FontSize => Math.Clamp(
                    value,
                    GameplaySkinSceneBudgets.MIN_FONT_SIZE,
                    GameplaySkinSceneBudgets.MAX_FONT_SIZE),
                GameplaySkinSceneProperty.CornerRadius => Math.Clamp(
                    value,
                    0,
                    GameplaySkinSceneBudgets.MAX_CORNER_RADIUS),
                GameplaySkinSceneProperty.Text => value,
                _ => throw new ArgumentOutOfRangeException(nameof(property), property, "This scene property cannot consume a numeric binding."),
            };
        }
    }

    public enum GameplaySkinSceneDecodeStatus
    {
        Absent = 0,
        Invalid = 1,
        Valid = 2,
    }

    /// <summary>
    /// Stable, persistence-safe scene codec diagnostics. Numeric values form part of the public diagnostic contract.
    /// </summary>
    public enum GameplaySkinSceneDiagnosticCode
    {
        InvalidUtf8 = 1,
        InvalidJson = 2,
        DuplicateField = 3,
        UnknownField = 4,
        MissingField = 5,
        UnsupportedContract = 6,
        InvalidValueType = 7,
        InvalidStableId = 8,
        DuplicateStableId = 9,
        UnsafeResourcePath = 10,
        DuplicateNormalizedPath = 11,
        InvalidResource = 12,
        UnknownResource = 13,
        UnknownNodeType = 14,
        UnknownProperty = 15,
        UnknownEffect = 16,
        UnknownEvent = 17,
        UnknownBindingSource = 18,
        UnknownTarget = 19,
        InvalidIndex = 20,
        InvalidReference = 21,
        InvalidPropertyValue = 22,
        InvalidAnimation = 23,
        InvalidStateMachine = 24,
        BudgetExceeded = 25,
        UnknownSlot = 26,
        RuntimeSlotNotApplicable = 27,
    }

    /// <summary>
    /// A deliberately value-free diagnostic suitable for persistence and user-facing aggregation.
    /// </summary>
    public sealed class GameplaySkinSceneDiagnostic
    {
        public GameplaySkinSceneDiagnosticCode Code { get; }

        public string Id => $"OMS-SKIN-SCENE-{(int)Code:000}";

        internal GameplaySkinSceneDiagnostic(GameplaySkinSceneDiagnosticCode code)
        {
            if (!Enum.IsDefined(code))
                throw new ArgumentOutOfRangeException(nameof(code), code, null);

            Code = code;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Immutable outcome which keeps a missing package file distinct from invalid author content.
    /// </summary>
    public sealed class GameplaySkinSceneDecodeResult<T>
        where T : class
    {
        public GameplaySkinSceneDecodeStatus Status { get; }

        public T? Value { get; }

        public IReadOnlyList<GameplaySkinSceneDiagnostic> Diagnostics { get; }

        internal GameplaySkinSceneDecodeResult(GameplaySkinSceneDecodeStatus status, T? value, IEnumerable<GameplaySkinSceneDiagnostic> diagnostics)
        {
            ArgumentNullException.ThrowIfNull(diagnostics);

            if (status == GameplaySkinSceneDecodeStatus.Valid && value == null
                || status != GameplaySkinSceneDecodeStatus.Valid && value != null)
            {
                throw new ArgumentException("Only a valid decode result may carry a value.", nameof(value));
            }

            GameplaySkinSceneDiagnostic[] copiedDiagnostics = diagnostics.ToArray();

            if (status == GameplaySkinSceneDecodeStatus.Valid && copiedDiagnostics.Length > 0
                || status == GameplaySkinSceneDecodeStatus.Absent && copiedDiagnostics.Length > 0
                || status == GameplaySkinSceneDecodeStatus.Invalid && copiedDiagnostics.Length == 0)
            {
                throw new ArgumentException("The decode status and diagnostic collection are inconsistent.", nameof(diagnostics));
            }

            Status = status;
            Value = value;
            Diagnostics = Array.AsReadOnly(copiedDiagnostics);
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneDecodeResult<T>)}:{Status}:{Diagnostics.Count}";
    }

    public enum GameplaySkinSceneResourceType
    {
        Texture = 1,
    }

    public sealed class GameplaySkinSceneResource
    {
        public string Id { get; }

        public GameplaySkinSceneResourceType Type { get; }

        public string Path { get; }

        internal GameplaySkinSceneResource(string id, GameplaySkinSceneResourceType type, string path)
        {
            Id = id;
            Type = type;
            Path = path;
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneResource)}:{Id}:{Type}";
    }

    public sealed class GameplaySkinSceneManifest
    {
        public string Contract { get; }

        public string SceneFile { get; }

        public string SceneContract { get; }

        public string EventContract { get; }

        public IReadOnlyList<GameplaySkinSceneResource> Resources { get; }

        private readonly IReadOnlyDictionary<string, GameplaySkinSceneResource> resourcesById;

        internal GameplaySkinSceneManifest(IEnumerable<GameplaySkinSceneResource> resources)
        {
            ArgumentNullException.ThrowIfNull(resources);

            GameplaySkinSceneResource[] copiedResources = resources.ToArray();
            Contract = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID;
            SceneFile = GameplaySkinSceneContracts.SCENE_FILE_NAME;
            SceneContract = GameplaySkinSceneContracts.SCENE_CONTRACT_ID;
            EventContract = GameplaySkinSceneContracts.EVENT_CONTRACT_ID;
            Resources = Array.AsReadOnly(copiedResources);
            resourcesById = new ReadOnlyDictionary<string, GameplaySkinSceneResource>(copiedResources.ToDictionary(resource => resource.Id, StringComparer.Ordinal));
        }

        public bool TryGetResource(string id, out GameplaySkinSceneResource resource)
        {
            ArgumentNullException.ThrowIfNull(id);
            return resourcesById.TryGetValue(id, out resource!);
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneManifest)}:{Resources.Count}";
    }

    public enum GameplaySkinSceneNodeType
    {
        Sprite = 1,
        Container = 2,
        Text = 3,
        Mask = 4,
        Clip = 5,
    }

    public enum GameplaySkinSceneTargetKind
    {
        Global = 1,
        Stage = 2,
        Group = 3,
        Lane = 4,
        Hud = 6,
        Bga = 7,
    }

    public enum GameplaySkinSceneBlendMode
    {
        Inherit = 1,
        Alpha = 2,
        Additive = 3,
        Multiply = 4,
        Screen = 5,
    }

    public enum GameplaySkinScenePropertyValueKind
    {
        Boolean = 1,
        Number = 2,
        String = 3,
    }

    /// <summary>
    /// Closed, decode-time compiled property vocabulary. Runtime code switches this enum and never reparses
    /// author strings on a frame boundary.
    /// </summary>
    public enum GameplaySkinSceneProperty
    {
        Unspecified = 0,
        Resource = 1,
        Opacity = 2,
        Visible = 3,
        X = 4,
        Y = 5,
        Width = 6,
        Height = 7,
        ScaleX = 8,
        ScaleY = 9,
        Rotation = 10,
        Z = 11,
        Anchor = 12,
        Origin = 13,
        Colour = 14,
        FontSize = 15,
        Text = 16,
        FillMode = 17,
        Alignment = 18,
        MaskMode = 19,
        ClipMode = 20,
        CornerRadius = 21,
    }

    public enum GameplaySkinSceneBindingSource
    {
        Unspecified = 0,
        LayoutStage = 1,
        LayoutGroup = 2,
        LayoutLane = 3,
        InputPressed = 4,
        ObjectState = 5,
        JudgementResult = 6,
        JudgementOffset = 7,
        ScoreValue = 8,
        ComboValue = 9,
        GaugeValue = 10,
        TimingBeat = 11,
        TimingMeasure = 12,
        TimingBpm = 13,
        BgaContentState = 14,
    }

    public enum GameplaySkinSceneEvent
    {
        Unspecified = 0,
        GameplayAttach = 1,
        GameplayStart = 2,
        GameplayPause = 3,
        GameplayComplete = 5,
        InputKeyDown = 8,
        InputKeyUp = 9,
        ObjectSpawn = 10,
        ObjectState = 12,
        JudgementHit = 13,
        // Numeric values 14-19 are reserved from pre-V1 drafts. Value updates are Snapshot-projectable bindings,
        // not state-machine edges whose history a late attach could never reconstruct.
        TimingStop = 20,
        // Numeric value 21 is reserved from the same pre-V1 draft.
        BgaState = 22,
        GameplayLoaded = 23,
        GameplayFailed = 24,
    }

    public sealed class GameplaySkinScenePropertyValue
    {
        public GameplaySkinScenePropertyValueKind Kind { get; }

        public bool BooleanValue { get; }

        public double NumberValue { get; }

        public string? StringValue { get; }

        private GameplaySkinScenePropertyValue(GameplaySkinScenePropertyValueKind kind, bool booleanValue, double numberValue, string? stringValue)
        {
            Kind = kind;
            BooleanValue = booleanValue;
            NumberValue = numberValue;
            StringValue = stringValue;
        }

        internal static GameplaySkinScenePropertyValue FromBoolean(bool value) => new GameplaySkinScenePropertyValue(GameplaySkinScenePropertyValueKind.Boolean, value, 0, null);

        internal static GameplaySkinScenePropertyValue FromNumber(double value) => new GameplaySkinScenePropertyValue(GameplaySkinScenePropertyValueKind.Number, false, value, null);

        internal static GameplaySkinScenePropertyValue FromString(string value) => new GameplaySkinScenePropertyValue(GameplaySkinScenePropertyValueKind.String, false, 0, value);

        public override string ToString() => $"{nameof(GameplaySkinScenePropertyValue)}:{Kind}";
    }

    public sealed class GameplaySkinSceneTarget
    {
        public GameplaySkinSceneTargetKind Kind { get; }

        public string? StableId { get; }

        public int? Index { get; }

        internal GameplaySkinSceneTarget(GameplaySkinSceneTargetKind kind, string? stableId, int? index)
        {
            Kind = kind;
            StableId = stableId;
            Index = index;
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneTarget)}:{Kind}";
    }

    public sealed class GameplaySkinSceneEffect
    {
        public string Id { get; }

        public string Type { get; }

        public IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> Properties { get; }

        internal GameplaySkinSceneEffect(string id, string type, IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties)
        {
            Id = id;
            Type = type;
            Properties = copyDictionary(properties);
        }

        private static IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> copyDictionary(IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties)
            => new ReadOnlyDictionary<string, GameplaySkinScenePropertyValue>(properties
                                                                              .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                                                                              .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

        public override string ToString() => $"{nameof(GameplaySkinSceneEffect)}:{Id}:{Type}";
    }

    public sealed class GameplaySkinSceneNode
    {
        public string Id { get; }

        public GameplaySkinSceneNodeType Type { get; }

        public GameplaySkinSceneTarget Target { get; }

        /// <summary>
        /// Optional public catalog slot driven by this node. Catalog membership is validated by the codec;
        /// ruleset applicability and target-scope admission remain preparation-time concerns.
        /// </summary>
        public string? SlotId { get; }

        public string? ResourceId { get; }

        public GameplaySkinSceneBlendMode Blend { get; }

        public IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> Properties { get; }

        public IReadOnlyList<GameplaySkinSceneEffect> Effects { get; }

        public IReadOnlyList<GameplaySkinSceneNode> Children { get; }

        internal GameplaySkinSceneNode(
            string id,
            GameplaySkinSceneNodeType type,
            GameplaySkinSceneTarget target,
            string? slotId,
            string? resourceId,
            GameplaySkinSceneBlendMode blend,
            IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties,
            IEnumerable<GameplaySkinSceneEffect> effects,
            IEnumerable<GameplaySkinSceneNode> children)
        {
            Id = id;
            Type = type;
            Target = target;
            SlotId = slotId;
            ResourceId = resourceId;
            Blend = blend;
            Properties = new ReadOnlyDictionary<string, GameplaySkinScenePropertyValue>(properties
                                                                                         .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                                                                                         .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            Effects = Array.AsReadOnly(effects.ToArray());
            Children = Array.AsReadOnly(children.ToArray());
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneNode)}:{Id}:{Type}";
    }

    public enum GameplaySkinSceneTrackType
    {
        Frame = 1,
        Tween = 2,
    }

    public enum GameplaySkinSceneEasing
    {
        Step = 1,
        Linear = 2,
        In = 3,
        Out = 4,
        InOut = 5,
    }

    public sealed class GameplaySkinSceneKeyframe
    {
        public string Id { get; }

        public double Time { get; }

        public GameplaySkinScenePropertyValue Value { get; }

        internal GameplaySkinSceneKeyframe(string id, double time, GameplaySkinScenePropertyValue value)
        {
            Id = id;
            Time = time;
            Value = value;
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneKeyframe)}:{Id}";
    }

    public sealed class GameplaySkinSceneTrack
    {
        public string Id { get; }

        public GameplaySkinSceneTrackType Type { get; }

        public string TargetNodeId { get; }

        public string PropertyId { get; }

        public GameplaySkinSceneProperty Property { get; }

        public GameplaySkinSceneEasing Easing { get; }

        public bool Loop { get; }

        public IReadOnlyList<GameplaySkinSceneKeyframe> Keyframes { get; }

        internal GameplaySkinSceneTrack(
            string id,
            GameplaySkinSceneTrackType type,
            string targetNodeId,
            string propertyId,
            GameplaySkinSceneEasing easing,
            bool loop,
            IEnumerable<GameplaySkinSceneKeyframe> keyframes)
        {
            Id = id;
            Type = type;
            TargetNodeId = targetNodeId;
            PropertyId = propertyId;
            Property = GameplaySkinSceneVocabulary.ParseProperty(propertyId);
            Easing = easing;
            Loop = loop;
            Keyframes = Array.AsReadOnly(keyframes.ToArray());
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneTrack)}:{Id}:{Type}";
    }

    public sealed class GameplaySkinSceneStateAssignment
    {
        public string Id { get; }

        public string TargetNodeId { get; }

        public string PropertyId { get; }

        public GameplaySkinSceneProperty Property { get; }

        public GameplaySkinScenePropertyValue Value { get; }

        internal GameplaySkinSceneStateAssignment(string id, string targetNodeId, string propertyId, GameplaySkinScenePropertyValue value)
        {
            Id = id;
            TargetNodeId = targetNodeId;
            PropertyId = propertyId;
            Property = GameplaySkinSceneVocabulary.ParseProperty(propertyId);
            Value = value;
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneStateAssignment)}:{Id}";
    }

    public sealed class GameplaySkinSceneState
    {
        public string Id { get; }

        public IReadOnlyList<GameplaySkinSceneStateAssignment> Assignments { get; }

        internal GameplaySkinSceneState(string id, IEnumerable<GameplaySkinSceneStateAssignment>? assignments = null)
        {
            Id = id;
            Assignments = Array.AsReadOnly((assignments ?? Array.Empty<GameplaySkinSceneStateAssignment>()).ToArray());
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneState)}:{Id}";
    }

    public sealed class GameplaySkinSceneTransition
    {
        public string Id { get; }

        public string FromStateId { get; }

        public string ToStateId { get; }

        public string EventId { get; }

        public GameplaySkinSceneEvent Event { get; }

        internal GameplaySkinSceneTransition(string id, string fromStateId, string toStateId, string eventId)
        {
            Id = id;
            FromStateId = fromStateId;
            ToStateId = toStateId;
            EventId = eventId;
            Event = GameplaySkinSceneVocabulary.ParseEvent(eventId);
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneTransition)}:{Id}";
    }

    public sealed class GameplaySkinSceneStateMachine
    {
        public string Id { get; }

        public string InitialStateId { get; }

        public IReadOnlyList<GameplaySkinSceneState> States { get; }

        public IReadOnlyList<GameplaySkinSceneTransition> Transitions { get; }

        internal GameplaySkinSceneStateMachine(
            string id,
            string initialStateId,
            IEnumerable<GameplaySkinSceneState> states,
            IEnumerable<GameplaySkinSceneTransition> transitions)
        {
            Id = id;
            InitialStateId = initialStateId;
            States = Array.AsReadOnly(states.ToArray());
            Transitions = Array.AsReadOnly(transitions.ToArray());
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneStateMachine)}:{Id}";
    }

    public sealed class GameplaySkinSceneBinding
    {
        public string Id { get; }

        public string TargetNodeId { get; }

        public string PropertyId { get; }

        public GameplaySkinSceneProperty Property { get; }

        public string SourceId { get; }

        public GameplaySkinSceneBindingSource Source { get; }

        internal GameplaySkinSceneBinding(string id, string targetNodeId, string propertyId, string sourceId)
        {
            Id = id;
            TargetNodeId = targetNodeId;
            PropertyId = propertyId;
            Property = GameplaySkinSceneVocabulary.ParseProperty(propertyId);
            SourceId = sourceId;
            Source = GameplaySkinSceneVocabulary.ParseBindingSource(sourceId);
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneBinding)}:{Id}";
    }

    /// <summary>
    /// One typed enum-key to already-prepared sprite resource mapping. Both identifiers are author ABI; the runtime
    /// never interprets a path or performs resource lookup outside the immutable prepared scene.
    /// </summary>
    public sealed class GameplaySkinSceneVariantCase
    {
        public string Id { get; }

        public string Key { get; }

        public string ResourceId { get; }

        internal GameplaySkinSceneVariantCase(string id, string key, string resourceId)
        {
            Id = id;
            Key = key;
            ResourceId = resourceId;
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneVariantCase)}:{Id}";
    }

    /// <summary>
    /// A closed, typed V1 sprite variant driven only by one read-only engine enum source.
    /// </summary>
    public sealed class GameplaySkinSceneVariant
    {
        public string Id { get; }

        public string TargetNodeId { get; }

        public GameplaySkinSceneProperty Property => GameplaySkinSceneProperty.Resource;

        public string SourceId { get; }

        public GameplaySkinSceneBindingSource Source { get; }

        public string DefaultResourceId { get; }

        public IReadOnlyList<GameplaySkinSceneVariantCase> Cases { get; }

        private readonly IReadOnlyDictionary<string, GameplaySkinSceneVariantCase> casesByKey;

        internal GameplaySkinSceneVariant(
            string id,
            string targetNodeId,
            string sourceId,
            string defaultResourceId,
            IEnumerable<GameplaySkinSceneVariantCase> cases)
        {
            Id = id;
            TargetNodeId = targetNodeId;
            SourceId = sourceId;
            Source = GameplaySkinSceneVocabulary.ParseBindingSource(sourceId);
            DefaultResourceId = defaultResourceId;
            GameplaySkinSceneVariantCase[] copiedCases = cases.ToArray();
            Cases = Array.AsReadOnly(copiedCases);
            casesByKey = new ReadOnlyDictionary<string, GameplaySkinSceneVariantCase>(
                copiedCases.ToDictionary(item => item.Key, StringComparer.Ordinal));
        }

        public string SelectResource(string key)
            => casesByKey.TryGetValue(key, out GameplaySkinSceneVariantCase? item) ? item.ResourceId : DefaultResourceId;

        public override string ToString() => $"{nameof(GameplaySkinSceneVariant)}:{Id}";
    }

    internal static class GameplaySkinSceneVocabulary
    {
        public static GameplaySkinSceneProperty ParseProperty(string id) => id switch
        {
            "resource" => GameplaySkinSceneProperty.Resource,
            "opacity" => GameplaySkinSceneProperty.Opacity,
            "visible" => GameplaySkinSceneProperty.Visible,
            "x" => GameplaySkinSceneProperty.X,
            "y" => GameplaySkinSceneProperty.Y,
            "width" => GameplaySkinSceneProperty.Width,
            "height" => GameplaySkinSceneProperty.Height,
            "scale-x" => GameplaySkinSceneProperty.ScaleX,
            "scale-y" => GameplaySkinSceneProperty.ScaleY,
            "rotation" => GameplaySkinSceneProperty.Rotation,
            "z" => GameplaySkinSceneProperty.Z,
            "anchor" => GameplaySkinSceneProperty.Anchor,
            "origin" => GameplaySkinSceneProperty.Origin,
            "colour" => GameplaySkinSceneProperty.Colour,
            "font-size" => GameplaySkinSceneProperty.FontSize,
            "text" => GameplaySkinSceneProperty.Text,
            "fill-mode" => GameplaySkinSceneProperty.FillMode,
            "alignment" => GameplaySkinSceneProperty.Alignment,
            "mask-mode" => GameplaySkinSceneProperty.MaskMode,
            "clip-mode" => GameplaySkinSceneProperty.ClipMode,
            "corner-radius" => GameplaySkinSceneProperty.CornerRadius,
            _ => GameplaySkinSceneProperty.Unspecified,
        };

        public static string PropertyId(GameplaySkinSceneProperty property) => property switch
        {
            GameplaySkinSceneProperty.Resource => "resource",
            GameplaySkinSceneProperty.Opacity => "opacity",
            GameplaySkinSceneProperty.Visible => "visible",
            GameplaySkinSceneProperty.X => "x",
            GameplaySkinSceneProperty.Y => "y",
            GameplaySkinSceneProperty.Width => "width",
            GameplaySkinSceneProperty.Height => "height",
            GameplaySkinSceneProperty.ScaleX => "scale-x",
            GameplaySkinSceneProperty.ScaleY => "scale-y",
            GameplaySkinSceneProperty.Rotation => "rotation",
            GameplaySkinSceneProperty.Z => "z",
            GameplaySkinSceneProperty.Anchor => "anchor",
            GameplaySkinSceneProperty.Origin => "origin",
            GameplaySkinSceneProperty.Colour => "colour",
            GameplaySkinSceneProperty.FontSize => "font-size",
            GameplaySkinSceneProperty.Text => "text",
            GameplaySkinSceneProperty.FillMode => "fill-mode",
            GameplaySkinSceneProperty.Alignment => "alignment",
            GameplaySkinSceneProperty.MaskMode => "mask-mode",
            GameplaySkinSceneProperty.ClipMode => "clip-mode",
            GameplaySkinSceneProperty.CornerRadius => "corner-radius",
            _ => throw new ArgumentOutOfRangeException(nameof(property), property, "Unknown scene property."),
        };

        public static GameplaySkinSceneBindingSource ParseBindingSource(string id) => id switch
        {
            "layout.stage" => GameplaySkinSceneBindingSource.LayoutStage,
            "layout.group" => GameplaySkinSceneBindingSource.LayoutGroup,
            "layout.lane" => GameplaySkinSceneBindingSource.LayoutLane,
            "input.pressed" => GameplaySkinSceneBindingSource.InputPressed,
            "object.state" => GameplaySkinSceneBindingSource.ObjectState,
            "judgement.result" => GameplaySkinSceneBindingSource.JudgementResult,
            "judgement.offset" => GameplaySkinSceneBindingSource.JudgementOffset,
            "score.value" => GameplaySkinSceneBindingSource.ScoreValue,
            "combo.value" => GameplaySkinSceneBindingSource.ComboValue,
            "gauge.value" => GameplaySkinSceneBindingSource.GaugeValue,
            "timing.beat" => GameplaySkinSceneBindingSource.TimingBeat,
            "timing.measure" => GameplaySkinSceneBindingSource.TimingMeasure,
            "timing.bpm" => GameplaySkinSceneBindingSource.TimingBpm,
            "bga.content-state" => GameplaySkinSceneBindingSource.BgaContentState,
            _ => GameplaySkinSceneBindingSource.Unspecified,
        };

        public static GameplaySkinSceneEvent ParseEvent(string id) => id switch
        {
            "gameplay.attach" => GameplaySkinSceneEvent.GameplayAttach,
            "gameplay.loaded" => GameplaySkinSceneEvent.GameplayLoaded,
            "gameplay.start" => GameplaySkinSceneEvent.GameplayStart,
            "gameplay.pause" => GameplaySkinSceneEvent.GameplayPause,
            "gameplay.complete" => GameplaySkinSceneEvent.GameplayComplete,
            "gameplay.fail" => GameplaySkinSceneEvent.GameplayFailed,
            "input.key.down" => GameplaySkinSceneEvent.InputKeyDown,
            "input.key.up" => GameplaySkinSceneEvent.InputKeyUp,
            "object.spawn" => GameplaySkinSceneEvent.ObjectSpawn,
            "object.state" => GameplaySkinSceneEvent.ObjectState,
            "judgement.hit" => GameplaySkinSceneEvent.JudgementHit,
            "timing.stop" => GameplaySkinSceneEvent.TimingStop,
            "bga.state" => GameplaySkinSceneEvent.BgaState,
            _ => GameplaySkinSceneEvent.Unspecified,
        };
    }

    public sealed class GameplaySkinSceneTemplate
    {
        public string Id { get; }

        public GameplaySkinSceneNode Root { get; }

        internal GameplaySkinSceneTemplate(string id, GameplaySkinSceneNode root)
        {
            Id = id;
            Root = root;
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneTemplate)}:{Id}";
    }

    public sealed class GameplaySkinSceneInstance
    {
        public string Id { get; }

        public string TemplateId { get; }

        public GameplaySkinSceneTarget Target { get; }

        internal GameplaySkinSceneInstance(string id, string templateId, GameplaySkinSceneTarget target)
        {
            Id = id;
            TemplateId = templateId;
            Target = target;
        }

        public override string ToString() => $"{nameof(GameplaySkinSceneInstance)}:{Id}";
    }

    public sealed class GameplaySkinSceneDocument
    {
        public string Contract { get; }

        public GameplaySkinSceneNode Root { get; }

        public IReadOnlyList<GameplaySkinSceneTrack> Tracks { get; }

        public IReadOnlyList<GameplaySkinSceneStateMachine> StateMachines { get; }

        public IReadOnlyList<GameplaySkinSceneBinding> Bindings { get; }

        public IReadOnlyList<GameplaySkinSceneVariant> Variants { get; }

        public IReadOnlyList<GameplaySkinSceneTemplate> Templates { get; }

        public IReadOnlyList<GameplaySkinSceneInstance> Instances { get; }

        internal GameplaySkinSceneDocument(
            GameplaySkinSceneNode root,
            IEnumerable<GameplaySkinSceneTrack> tracks,
            IEnumerable<GameplaySkinSceneStateMachine> stateMachines,
            IEnumerable<GameplaySkinSceneBinding> bindings,
            IEnumerable<GameplaySkinSceneTemplate> templates,
            IEnumerable<GameplaySkinSceneInstance> instances)
            : this(root, tracks, stateMachines, bindings, Array.Empty<GameplaySkinSceneVariant>(), templates, instances)
        {
        }

        internal GameplaySkinSceneDocument(
            GameplaySkinSceneNode root,
            IEnumerable<GameplaySkinSceneTrack> tracks,
            IEnumerable<GameplaySkinSceneStateMachine> stateMachines,
            IEnumerable<GameplaySkinSceneBinding> bindings,
            IEnumerable<GameplaySkinSceneVariant> variants,
            IEnumerable<GameplaySkinSceneTemplate> templates,
            IEnumerable<GameplaySkinSceneInstance> instances)
        {
            Root = root;
            Contract = GameplaySkinSceneContracts.SCENE_CONTRACT_ID;
            Tracks = Array.AsReadOnly(tracks.ToArray());
            StateMachines = Array.AsReadOnly(stateMachines.ToArray());
            Bindings = Array.AsReadOnly(bindings.ToArray());
            Variants = Array.AsReadOnly(variants.ToArray());
            Templates = Array.AsReadOnly(templates.ToArray());
            Instances = Array.AsReadOnly(instances.ToArray());
        }

        public override string ToString()
            => $"{nameof(GameplaySkinSceneDocument)}:{Tracks.Count}:{StateMachines.Count}:{Bindings.Count}:{Variants.Count}:{Templates.Count}:{Instances.Count}";
    }
}
