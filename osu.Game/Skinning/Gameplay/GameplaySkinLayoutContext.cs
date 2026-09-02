// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The package source which owns a gameplay layout publication.
    /// </summary>
    public enum GameplaySkinPackageSourceKind
    {
        ProtectedFallback,
        RealmPackage,
        ManagedFolder,
        ExternalFolder,
        Compatibility,
    }

    /// <summary>
    /// An immutable, process-local description of the exact package revision retained by a gameplay root.
    /// </summary>
    /// <remarks>
    /// This is a runtime coherence token, not an authoring or serialisation ABI. <see cref="ToString"/> deliberately
    /// omits package identity and content data so diagnostics cannot disclose a user path or package name.
    /// </remarks>
    public sealed class GameplaySkinPackageRevision
    {
        private readonly SkinCurrentRevision? exactRevision;

        public long Generation { get; }

        public Guid RecordId { get; }

        public string ContentRevision { get; }

        public GameplaySkinPackageSourceKind SourceKind { get; }

        private GameplaySkinPackageRevision(
            long generation,
            Guid recordId,
            string contentRevision,
            GameplaySkinPackageSourceKind sourceKind,
            SkinCurrentRevision? exactRevision)
        {
            Generation = generation;
            RecordId = recordId;
            ContentRevision = contentRevision;
            SourceKind = sourceKind;
            this.exactRevision = exactRevision;
        }

        internal static GameplaySkinPackageRevision Create(SkinCurrentRevision revision)
        {
            ArgumentNullException.ThrowIfNull(revision);

            return new GameplaySkinPackageRevision(
                revision.Generation,
                revision.RecordId,
                revision.ContentRevision,
                revision.SourceKind switch
                {
                    SkinCurrentRevisionSourceKind.ProtectedFallback => GameplaySkinPackageSourceKind.ProtectedFallback,
                    SkinCurrentRevisionSourceKind.RealmPackage => GameplaySkinPackageSourceKind.RealmPackage,
                    SkinCurrentRevisionSourceKind.ManagedFolder => GameplaySkinPackageSourceKind.ManagedFolder,
                    SkinCurrentRevisionSourceKind.ExternalFolder => GameplaySkinPackageSourceKind.ExternalFolder,
                    _ => GameplaySkinPackageSourceKind.Compatibility,
                },
                revision);
        }

        internal static GameplaySkinPackageRevision CreateCompatibility()
            => new GameplaySkinPackageRevision(0, Guid.Empty, "unversioned", GameplaySkinPackageSourceKind.Compatibility, null);

        internal bool RetainsExact(SkinCurrentRevision revision) => ReferenceEquals(exactRevision, revision);

        /// <summary>
        /// Whether <paramref name="source"/> is the exact immutable owner retained by this package revision.
        /// </summary>
        /// <remarks>
        /// A record ID is not sufficient because an old and a current same-ID owner may coexist while leases drain.
        /// The retained <see cref="SkinCurrentRevision"/> is the C2 authority which binds the owner reference to
        /// <see cref="ContentRevision"/> and <see cref="Generation"/> before this C3 token is published.
        /// </remarks>
        public bool RetainsExactSource(Skin source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return exactRevision != null
                   && ReferenceEquals(exactRevision.Owner, source)
                   && exactRevision.Generation == Generation
                   && exactRevision.RecordId == RecordId
                   && string.Equals(exactRevision.ContentRevision, ContentRevision, StringComparison.Ordinal);
        }

        public override string ToString()
            => $"{nameof(GameplaySkinPackageRevision)}:{SourceKind}:Generation{Generation}";
    }

    /// <summary>
    /// A finite, positive rectangle in full-screen relative coordinates.
    /// </summary>
    public readonly struct GameplaySkinLayoutRect : IEquatable<GameplaySkinLayoutRect>
    {
        public float X { get; }

        public float Y { get; }

        public float Width { get; }

        public float Height { get; }

        public float Left => X;

        public float Top => Y;

        public float Right => X + Width;

        public float Bottom => Y + Height;

        private GameplaySkinLayoutRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public static GameplaySkinLayoutRect Create(float x, float y, float width, float height)
        {
            ensureFinite(x, nameof(x));
            ensureFinite(y, nameof(y));
            ensureFinite(width, nameof(width));
            ensureFinite(height, nameof(height));

            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Layout rectangle width must be positive.");

            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Layout rectangle height must be positive.");

            float right = x + width;
            float bottom = y + height;
            ensureFinite(right, nameof(width));
            ensureFinite(bottom, nameof(height));

            return new GameplaySkinLayoutRect(x, y, width, height);
        }

        public bool Contains(GameplaySkinLayoutRect other, float tolerance = 0.0001f)
            => other.Left >= Left - tolerance
               && other.Top >= Top - tolerance
               && other.Right <= Right + tolerance
               && other.Bottom <= Bottom + tolerance;

        public bool Intersects(GameplaySkinLayoutRect other, float tolerance = 0.0001f)
            => Left < other.Right - tolerance
               && Right > other.Left + tolerance
               && Top < other.Bottom - tolerance
               && Bottom > other.Top + tolerance;

        public static GameplaySkinLayoutRect Union(IEnumerable<GameplaySkinLayoutRect> rectangles)
        {
            ArgumentNullException.ThrowIfNull(rectangles);
            GameplaySkinLayoutRect[] copied = rectangles.ToArray();

            if (copied.Length == 0)
                throw new ArgumentException("At least one rectangle is required.", nameof(rectangles));

            float left = copied.Min(rect => rect.Left);
            float top = copied.Min(rect => rect.Top);
            float right = copied.Max(rect => rect.Right);
            float bottom = copied.Max(rect => rect.Bottom);
            return Create(left, top, right - left, bottom - top);
        }

        public bool Equals(GameplaySkinLayoutRect other)
            => X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is GameplaySkinLayoutRect other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public static bool operator ==(GameplaySkinLayoutRect left, GameplaySkinLayoutRect right) => left.Equals(right);

        public static bool operator !=(GameplaySkinLayoutRect left, GameplaySkinLayoutRect right) => !left.Equals(right);

        private static void ensureFinite(float value, string parameterName)
        {
            if (!float.IsFinite(value))
                throw new ArgumentOutOfRangeException(parameterName, value, "Layout geometry must be finite.");
        }
    }

    public enum GameplaySkinScrollDirection
    {
        Down,
        Up,
    }

    /// <summary>
    /// The single ruleset-neutral input frame from which one gameplay layout snapshot is solved.
    /// </summary>
    /// <remarks>
    /// Native context, keymode and presentation style are opaque exact tokens supplied by the ruleset adapter. The
    /// topology is the engine-owned identity/order authority. Geometry consumers must retain the resulting snapshot;
    /// they must never treat this context or the topology-only revision as already-solved geometry.
    /// </remarks>
    public sealed class GameplaySkinLayoutContext
    {
        public string RulesetId { get; }

        public string NativeContextId { get; }

        public string KeymodeId { get; }

        public string PresentationStyleId { get; }

        public GameplaySkinLaneTopologySnapshot Topology { get; }

        public GameplaySkinLayoutRect ScreenBounds { get; }

        public GameplaySkinLayoutRect SafeBounds { get; }

        public float AspectRatio { get; }

        public float DpiScale { get; }

        public GameplaySkinScrollDirection ScrollDirection { get; }

        public GameplaySkinPackageRevision PackageRevision { get; }

        public long TopologyRevision { get; }

        public long LayoutRevision { get; }

        private GameplaySkinLayoutContext(
            string rulesetId,
            string nativeContextId,
            string keymodeId,
            string presentationStyleId,
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinLayoutRect screenBounds,
            GameplaySkinLayoutRect safeBounds,
            float aspectRatio,
            float dpiScale,
            GameplaySkinScrollDirection scrollDirection,
            GameplaySkinPackageRevision packageRevision,
            long topologyRevision,
            long layoutRevision)
        {
            RulesetId = rulesetId;
            NativeContextId = nativeContextId;
            KeymodeId = keymodeId;
            PresentationStyleId = presentationStyleId;
            Topology = topology;
            ScreenBounds = screenBounds;
            SafeBounds = safeBounds;
            AspectRatio = aspectRatio;
            DpiScale = dpiScale;
            ScrollDirection = scrollDirection;
            PackageRevision = packageRevision;
            TopologyRevision = topologyRevision;
            LayoutRevision = layoutRevision;
        }

        public static GameplaySkinLayoutContext Create(
            string rulesetId,
            string nativeContextId,
            string keymodeId,
            string presentationStyleId,
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinLayoutRect screenBounds,
            GameplaySkinLayoutRect safeBounds,
            float aspectRatio,
            float dpiScale,
            GameplaySkinScrollDirection scrollDirection,
            GameplaySkinPackageRevision packageRevision,
            long topologyRevision,
            long layoutRevision)
        {
            validateToken(rulesetId, nameof(rulesetId));
            validateToken(nativeContextId, nameof(nativeContextId));
            validateToken(keymodeId, nameof(keymodeId));
            validateToken(presentationStyleId, nameof(presentationStyleId));
            ArgumentNullException.ThrowIfNull(topology);
            ArgumentNullException.ThrowIfNull(packageRevision);

            if (!screenBounds.Contains(safeBounds))
                throw new ArgumentException("Safe bounds must be contained by the screen bounds.", nameof(safeBounds));

            if (!float.IsFinite(aspectRatio) || aspectRatio <= 0)
                throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "Aspect ratio must be finite and positive.");

            if (!float.IsFinite(dpiScale) || dpiScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(dpiScale), dpiScale, "DPI scale must be finite and positive.");

            if (!Enum.IsDefined(scrollDirection))
                throw new ArgumentOutOfRangeException(nameof(scrollDirection), scrollDirection, "Scroll direction must be a defined value.");

            ArgumentOutOfRangeException.ThrowIfNegative(topologyRevision);
            ArgumentOutOfRangeException.ThrowIfNegative(layoutRevision);

            return new GameplaySkinLayoutContext(
                rulesetId,
                nativeContextId,
                keymodeId,
                presentationStyleId,
                topology,
                screenBounds,
                safeBounds,
                aspectRatio,
                dpiScale,
                scrollDirection,
                packageRevision,
                topologyRevision,
                layoutRevision);
        }

        private static void validateToken(string value, string parameterName)
        {
            ArgumentException.ThrowIfNullOrEmpty(value, parameterName);

            if (value.Length > 80 || value.Any(character => !isTokenCharacter(character)))
                throw new ArgumentException("Gameplay layout context tokens must be short lowercase ASCII values.", parameterName);
        }

        private static bool isTokenCharacter(char character)
            => character is >= 'a' and <= 'z'
               or >= '0' and <= '9'
               or '.' or '-';

        public override string ToString() => nameof(GameplaySkinLayoutContext);
    }
}
