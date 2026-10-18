// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>One immutable content combination, owned by the exact retained skin and frozen before layout solving.</summary>
    public sealed class GameplaySkinContentSelection
    {
        private readonly Skin owner;

        public GameplaySkinOptionValues OptionValues { get; }
        public GameplaySkinSceneManifest? Manifest => PreparedPackage.Manifest;
        public GameplaySkinDocument MaterialDocument { get; }
        public IReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram> RecordedAnimations => PreparedPackage.RecordedAnimations;
        internal GameplaySkinPreparedPackage PreparedPackage { get; }

        internal GameplaySkinContentSelection(Skin owner, GameplaySkinOptionValues options, GameplaySkinPreparedPackage package,
                                             GameplaySkinDocument materialDocument)
        {
            this.owner = owner;
            OptionValues = options;
            PreparedPackage = package;
            MaterialDocument = materialDocument;
        }

        internal void ValidateOwner(Skin selected)
        {
            if (!ReferenceEquals(owner, selected))
                throw new InvalidOperationException("Selected content belongs to a different exact skin owner.");
        }

        internal void ValidatePackage(GameplaySkinPackageRevision package)
        {
            if (!package.RetainsExactSource(owner) || !string.Equals(package.ContentRevision, PreparedPackage.ContentRevision, StringComparison.Ordinal))
                throw new InvalidOperationException("Selected content must retain the layout's exact package revision.");
        }
    }
}
