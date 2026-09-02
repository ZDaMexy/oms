// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Exact source authority for one immutable shared-codec document. Beatmap-local authoring is intentionally absent.
    /// </summary>
    public enum GameplaySkinDocumentSourceKind
    {
        UnboundPackageParse = 0,
        ProtectedFallback = 1,
        RealmPackage = 2,
        ManagedFolder = 3,
        ExternalFolder = 4,
        Compatibility = 5,
    }

    /// <summary>
    /// Path-free identity binding one tokenized document to an exact package/current/layout revision.
    /// </summary>
    public sealed class GameplaySkinDocumentIdentity : IEquatable<GameplaySkinDocumentIdentity>
    {
        public GameplaySkinDocumentSourceKind SourceKind { get; }

        public Guid SourceId { get; }

        public string ContentRevision { get; }

        public long PackageRevision { get; }

        public long CurrentRevision { get; }

        public long LayoutRevision { get; }

        public bool IsBound => SourceKind != GameplaySkinDocumentSourceKind.UnboundPackageParse;

        private GameplaySkinDocumentIdentity(
            GameplaySkinDocumentSourceKind sourceKind,
            Guid sourceId,
            string contentRevision,
            long packageRevision,
            long currentRevision,
            long layoutRevision)
        {
            if (!Enum.IsDefined(sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind));

            ArgumentException.ThrowIfNullOrWhiteSpace(contentRevision);
            ArgumentOutOfRangeException.ThrowIfNegative(packageRevision);
            ArgumentOutOfRangeException.ThrowIfNegative(currentRevision);
            ArgumentOutOfRangeException.ThrowIfNegative(layoutRevision);

            if (sourceKind == GameplaySkinDocumentSourceKind.UnboundPackageParse)
            {
                if (sourceId != Guid.Empty || packageRevision != 0 || currentRevision != 0 || layoutRevision != 0)
                    throw new ArgumentException("An unbound package parse cannot claim package, current or layout identity.", nameof(sourceKind));
            }
            else if (sourceId == Guid.Empty && sourceKind is not GameplaySkinDocumentSourceKind.ProtectedFallback and not GameplaySkinDocumentSourceKind.Compatibility)
            {
                throw new ArgumentException("A bound package source must have a non-empty stable source ID.", nameof(sourceId));
            }

            SourceKind = sourceKind;
            SourceId = sourceId;
            ContentRevision = contentRevision;
            PackageRevision = packageRevision;
            CurrentRevision = currentRevision;
            LayoutRevision = layoutRevision;
        }

        public static GameplaySkinDocumentIdentity CreateUnboundPackageParse(string contentRevision)
            => new GameplaySkinDocumentIdentity(
                GameplaySkinDocumentSourceKind.UnboundPackageParse,
                Guid.Empty,
                contentRevision,
                0,
                0,
                0);

        internal static GameplaySkinDocumentIdentity CreateBound(
            GameplaySkinDocumentSourceKind sourceKind,
            Guid sourceId,
            string contentRevision,
            long packageRevision,
            long currentRevision,
            long layoutRevision)
        {
            if (sourceKind == GameplaySkinDocumentSourceKind.UnboundPackageParse)
                throw new ArgumentException("Use CreateUnboundPackageParse for an unbound identity.", nameof(sourceKind));

            return new GameplaySkinDocumentIdentity(
                sourceKind,
                sourceId,
                contentRevision,
                packageRevision,
                currentRevision,
                layoutRevision);
        }

        public bool Equals(GameplaySkinDocumentIdentity? other)
            => other != null
               && SourceKind == other.SourceKind
               && SourceId == other.SourceId
               && string.Equals(ContentRevision, other.ContentRevision, StringComparison.Ordinal)
               && PackageRevision == other.PackageRevision
               && CurrentRevision == other.CurrentRevision
               && LayoutRevision == other.LayoutRevision;

        public override bool Equals(object? obj) => obj is GameplaySkinDocumentIdentity other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(SourceKind, SourceId, ContentRevision, PackageRevision, CurrentRevision, LayoutRevision);

        /// <summary>
        /// Deliberately omits source ID and content revision.
        /// </summary>
        public override string ToString() => IsBound ? $"{nameof(GameplaySkinDocumentIdentity)}:{SourceKind}:Bound" : $"{nameof(GameplaySkinDocumentIdentity)}:Unbound";
    }
}
