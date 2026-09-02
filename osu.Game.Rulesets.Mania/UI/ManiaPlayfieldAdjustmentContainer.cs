// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// Hosts the already-solved mania layout without deriving a second geometry from drawable size.
    /// </summary>
    public partial class ManiaPlayfieldAdjustmentContainer : PlayfieldAdjustmentContainer
    {
        protected override Container<Drawable> Content { get; }

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; private set; } = null!;

        public ManiaPlayfieldAdjustmentContainer()
        {
            InternalChild = Content = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(GameplaySkinLayoutRevisionOwner? owner, ISkinSource skin)
        {
            GameplaySkinLayoutPublication? publication = owner?.CurrentPublication;

            if (publication == null)
            {
                if (owner == null || owner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                {
                    throw new InvalidOperationException(
                        "A standalone mania core adjustment requires an explicitly cached compatibility layout owner.");
                }

                // Component/editor tests can construct this adjustment shell without a gameplay provider. It does
                // not solve or apply geometry; retain an explicitly-labelled compatibility snapshot for auditing.
                LayoutSnapshot = ManiaGameplaySkinLayout.CreateCompatibility(
                    new[] { new StageDefinition(4) }, skin, useSkinGeometry: false).Snapshot;
                ResolvedMaterialSet = GameplaySkinResolvedMaterialSet.CreateEmpty(LayoutSnapshot);
                ManiaGameplaySkinLayout.ValidateConsumerCarrier(LayoutSnapshot, owner, "core adjustment");
                return;
            }

            ManiaGameplaySkinLayout adapter = publication.GetAdapter<ManiaGameplaySkinLayout>();

            if (!ReferenceEquals(adapter.Snapshot, publication.Snapshot)
                || !ReferenceEquals(publication.Snapshot.Context.PackageRevision, owner!.PackageRevision))
            {
                throw new InvalidOperationException("The mania core adjustment did not retain the exact layout publication.");
            }

            LayoutSnapshot = publication.Snapshot;
            ResolvedMaterialSet = publication.MaterialSet;
            ManiaGameplaySkinLayout.ValidateConsumerCarrier(LayoutSnapshot, owner, "core adjustment");
        }
    }
}
