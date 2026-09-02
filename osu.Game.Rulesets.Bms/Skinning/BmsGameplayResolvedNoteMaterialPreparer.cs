// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Resolves every hosted BMS Note/LN slot against one exact C3 layout in a single final provider chain.
    /// </summary>
    internal static class BmsGameplayResolvedNoteMaterialPreparer
    {
        public static GameplaySkinLayoutPublication Prepare(
            ISkin skin,
            BmsGameplayLayoutSnapshot layout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(skin);
            ArgumentNullException.ThrowIfNull(layout);
            cancellationToken.ThrowIfCancellationRequested();

            GameplaySkinPackageRevision packageRevision = layout.Neutral.Context.PackageRevision;
            BmsLegacySkin? selectedSource = findExactSelectedSource(skin, packageRevision);
            GameplaySkinResolvedMaterialSet? selectedMaterials = null;
            BmsManagedPackageNoteRevisionBorrow? selectedBorrow = null;

            try
            {
                if (selectedSource != null)
                {
                    BmsManagedPackageSourceRevision sourceBeforePrepare = selectedSource.CaptureManagedPackageSourceRevision();

                    if (sourceBeforePrepare.SkinId != packageRevision.RecordId
                        || !StringComparer.Ordinal.Equals(sourceBeforePrepare.PackageContentRevision, packageRevision.ContentRevision))
                    {
                        throw new InvalidOperationException("The selected BMS package content revision does not match its exact layout publication.");
                    }

                    selectedBorrow = selectedSource.GetOrPrepareManagedPackageNotes(layout, cancellationToken);
                    BmsManagedPackageNoteRevision prepared = selectedBorrow.Revision;
                    BmsManagedPackageSourceRevision currentSourceRevision = selectedSource.CaptureManagedPackageSourceRevision();

                    if (!prepared.SourceRevision.Equals(sourceBeforePrepare)
                        || !prepared.SourceRevision.Equals(currentSourceRevision)
                        || currentSourceRevision.SkinId != packageRevision.RecordId
                        || !StringComparer.Ordinal.Equals(currentSourceRevision.PackageContentRevision, packageRevision.ContentRevision))
                    {
                        throw new InvalidOperationException("The selected BMS package content revision does not match its exact layout publication.");
                    }

                    // This is deliberately a selected-authority partial set. The final resolver below owns every fallback
                    // decision and is the only place which may produce a complete committed set.
                    selectedMaterials = prepared.CreateMaterialSet(layout, GameplaySkinMaterialContractIdentity.Current);
                }

                IReadOnlyList<ISkin> aggregateSources = skin is ISkinSource aggregate
                    ? aggregate.AllSources.ToArray()
                    : new[] { skin };
                var providers = new List<FinalProviderRegistration>();
                int beatmapIndex = 0;
                int rulesetIndex = 0;
                int canonicalIndex = 0;

                foreach (ISkin transformed in aggregateSources)
                {
                    ISkin raw = unwrap(transformed);

                    if (raw is not LegacyBeatmapSkin { AllowsGameplaySkinDocumentAuthoring: false } beatmap)
                        continue;

                    string contentRevision = beatmap.GameplaySkinDocument.Identity.ContentRevision;
                    GameplaySkinResolvedMaterialSourceIdentity identity = GameplaySkinResolvedMaterialSourceIdentity.Create(
                        GameplaySkinResolvedMaterialSourceKind.LegacyBeatmapCompatibility,
                        "legacy-beatmap-direct",
                        string.IsNullOrWhiteSpace(contentRevision) ? "legacy-v1" : contentRevision);
                    providers.Add(new FinalProviderRegistration(
                        new ConventionalMaterialProvider(
                            $"legacy.beatmap-direct-{beatmapIndex++.ToString(CultureInfo.InvariantCulture)}",
                            transformed,
                            layout),
                        identity));
                }

                if (selectedMaterials != null)
                {
                    foreach (BmsPreparedNoteMaterialAuthority authority in BmsPreparedNoteMaterialAuthorityIdentity.SelectedInPrecedenceOrder)
                    {
                        GameplaySkinResolvedMaterialSourceIdentity? identity = selectedMaterials.Entries
                            .Select(entry => entry.Source)
                            .FirstOrDefault(source =>
                                BmsPreparedNoteMaterialAuthorityIdentity.TryGetSelectedAuthority(source, out BmsPreparedNoteMaterialAuthority sourceAuthority)
                                && sourceAuthority == authority);

                        if (identity == null)
                            continue;

                        providers.Add(new FinalProviderRegistration(
                            new SelectedMaterialProvider($"selected.final-{identity.StableId}", selectedMaterials, identity),
                            identity));
                    }
                }

                foreach (ISkin transformed in aggregateSources)
                {
                    if (unwrap(transformed) is not ResourceStoreBackedSkin)
                        continue;

                    GameplaySkinResolvedMaterialSourceIdentity identity = GameplaySkinResolvedMaterialSourceIdentity.Create(
                        GameplaySkinResolvedMaterialSourceKind.RulesetResources,
                        "bms-ruleset-resources",
                        "v1");
                    providers.Add(new FinalProviderRegistration(
                        new ConventionalMaterialProvider(
                            $"ruleset.resources-{rulesetIndex++.ToString(CultureInfo.InvariantCulture)}",
                            transformed,
                            layout),
                        identity));
                }

                foreach (ISkin transformed in aggregateSources)
                {
                    ISkin raw = unwrap(transformed);

                    if (ReferenceEquals(raw, selectedSource)
                        || raw is not Skin candidate
                        || !candidate.SkinInfo.PerformRead(info => info.Protected))
                    {
                        continue;
                    }

                    GameplaySkinResolvedMaterialSourceIdentity identity = GameplaySkinResolvedMaterialSourceIdentity.Create(
                        GameplaySkinResolvedMaterialSourceKind.CanonicalPackage,
                        "canonical-package",
                        candidate.PackageContentRevision ?? "builtin-v1");
                    providers.Add(new FinalProviderRegistration(
                        new ConventionalMaterialProvider(
                            $"canonical.package-{canonicalIndex++.ToString(CultureInfo.InvariantCulture)}",
                            transformed,
                            layout),
                        identity));
                }

                GameplaySkinResolvedMaterialSourceIdentity programmaticIdentity = GameplaySkinResolvedMaterialSourceIdentity.Create(
                    GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback,
                    "bms-programmatic",
                    "v1");
                providers.Add(new FinalProviderRegistration(
                    new ProgrammaticMaterialProvider(layout),
                    programmaticIdentity));

                var entries = new List<GameplaySkinResolvedMaterialEntry>();
                var diagnostics = new List<GameplaySkinResolvedMaterialDiagnostic>();

                if (selectedMaterials != null)
                    diagnostics.AddRange(selectedMaterials.Diagnostics);

                foreach (BmsGameplayLayoutLane lane in layout.LanesInLogicalOrder)
                {
                    foreach (BmsNoteSkinElements element in note_elements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!BmsManagedPackageNoteCompatibilityProvider.TryGetDescriptor(element, out GameplaySkinSlotDescriptor descriptor))
                            throw new InvalidOperationException("The exact BMS material slot has no public catalog descriptor.");

                        GameplaySkinLaneResourceField field = getField(element);
                        var context = new BmsGameplaySkinLaneResourceContext(
                            layout.Neutral.Context.Topology,
                            lane.LaneId,
                            field);
                        GameplaySkinResolvedMaterialTarget target = BmsGameplayNoteMaterialTarget.Create(layout, lane);
                        var key = new GameplaySkinResolvedMaterialKey(descriptor, target);
                        GameplaySkinSlotResolution<IBmsResolvedNoteMaterial> resolution = GameplaySkinSlotResolver.Resolve(
                            descriptor,
                            context,
                            providers.Select(provider => provider.Provider),
                            material => material.Element == element && material.FrameCount > 0);

                        foreach (GameplaySkinSlotDiagnostic diagnostic in resolution.Diagnostics)
                        {
                            FinalProviderRegistration? source = providers.FirstOrDefault(provider =>
                                string.Equals(provider.Provider.Name, diagnostic.ProviderName, StringComparison.Ordinal));
                            diagnostics.Add(new GameplaySkinResolvedMaterialDiagnostic(
                                BmsManagedPackageNoteMaterializer.GetDiagnosticCode(diagnostic),
                                key,
                                source?.Identity));
                        }

                        FinalProviderRegistration winner = providers.FirstOrDefault(provider =>
                            string.Equals(provider.Provider.Name, resolution.ProviderName, StringComparison.Ordinal))
                            ?? throw new InvalidOperationException("The final BMS Note/LN resolver did not retain a terminal authority.");

                        switch (resolution.Result.Kind)
                        {
                            case SkinSlotResultKind.Provide:
                                entries.Add(GameplaySkinResolvedMaterialEntry.Provide(
                                    descriptor,
                                    target,
                                    winner.Identity,
                                    typeof(IBmsResolvedNoteMaterial),
                                    resolution.Result.Value));
                                break;

                            case SkinSlotResultKind.Suppress:
                                entries.Add(GameplaySkinResolvedMaterialEntry.Suppress(descriptor, target, winner.Identity));
                                break;

                            default:
                                throw new InvalidOperationException("The final BMS Note/LN resolver did not produce an explicit Provide or Suppress result.");
                        }
                    }
                }

                GameplaySkinResolvedMaterialSet resolved = GameplaySkinResolvedMaterialSet.Create(
                    layout.Neutral,
                    GameplaySkinMaterialContractIdentity.Current,
                    entries,
                    diagnostics);

                if (selectedBorrow == null)
                    return GameplaySkinLayoutPublication.Create(layout, resolved);

                BmsManagedPackageNoteRevisionBorrow retirement = selectedBorrow;
                selectedBorrow = null;
                return GameplaySkinLayoutPublication.Create(layout, resolved, retirement);
            }
            catch
            {
                selectedBorrow?.Dispose();
                throw;
            }
        }

        private static bool tryCreateConventionalMaterial(
            ISkin source,
            BmsGameplayLayoutSnapshot layout,
            BmsGameplayLayoutLane lane,
            BmsNoteSkinElements element,
            out IBmsResolvedNoteMaterial? material)
        {
            string column = getFallbackColumn(layout, lane);
            string note = $"mania-note{column}";
            string[] candidates = element switch
            {
                BmsNoteSkinElements.Note => new[] { note },
                BmsNoteSkinElements.LongNoteHead => new[] { $"{note}H", note },
                BmsNoteSkinElements.LongNoteBody => new[] { $"{note}L" },
                BmsNoteSkinElements.LongNoteTail => new[] { $"{note}T", $"{note}H", note },
                _ => Array.Empty<string>(),
            };

            foreach (string candidate in candidates)
            {
                Texture[] frames = source.GetTextures(
                    candidate,
                    WrapMode.ClampToEdge,
                    WrapMode.ClampToEdge,
                    true,
                    "-",
                    null,
                    out _);

                if (frames.Length == 0)
                    continue;

                BmsGameplaySkinScalarGeometryResolution? width = element == BmsNoteSkinElements.LongNoteBody
                    ? BmsGameplaySkinScalarGeometryResolver.Resolve(
                        BmsSkinConfigurationLookups.LongNoteBodyWidth,
                        GameplaySkinConfigurationDeclaration<float>.Absent)
                    : null;
                material = new BmsSourceBoundNoteMaterial(element, frames, width);
                return true;
            }

            material = null;
            return false;
        }

        private static string getFallbackColumn(BmsGameplayLayoutSnapshot layout, BmsGameplayLayoutLane lane)
        {
            GameplaySkinLaneTopologyEntry topologyLane = lane.NeutralLane.TopologyEntry;

            if (topologyLane.Identity.Role is GameplaySkinLaneRole.Scratch or GameplaySkinLaneRole.SpecialKey)
                return "S";

            GameplaySkinLaneTopologyGroup group = layout.Neutral.Context.Topology.GroupsInLogicalOrder
                .Single(candidate => candidate.Identity.Id.Equals(topologyLane.Identity.Group.Id));
            int distanceToEdge = Math.Min(
                topologyLane.GroupLocalLogicalIndex,
                group.LanesInLogicalOrder.Count - 1 - topologyLane.GroupLocalLogicalIndex);
            return distanceToEdge % 2 == 0 ? "1" : "2";
        }

        private static GameplaySkinLaneResourceField getField(BmsNoteSkinElements element)
            => element switch
            {
                BmsNoteSkinElements.Note => GameplaySkinLaneResourceFieldCatalog.Note,
                BmsNoteSkinElements.LongNoteHead => GameplaySkinLaneResourceFieldCatalog.LongNoteHead,
                BmsNoteSkinElements.LongNoteBody => GameplaySkinLaneResourceFieldCatalog.LongNoteBody,
                BmsNoteSkinElements.LongNoteTail => GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
                _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Unsupported exact BMS note element."),
            };

        private static BmsNoteSkinElements getElement(GameplaySkinLaneResourceField field)
        {
            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.Note))
                return BmsNoteSkinElements.Note;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteHead))
                return BmsNoteSkinElements.LongNoteHead;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteBody))
                return BmsNoteSkinElements.LongNoteBody;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteTail))
                return BmsNoteSkinElements.LongNoteTail;

            throw new ArgumentException("The final BMS material provider received an unhosted field.", nameof(field));
        }

        private static BmsLegacySkin? findExactSelectedSource(
            ISkin skin,
            GameplaySkinPackageRevision packageRevision)
        {
            IEnumerable<ISkin> sources = skin is ISkinSource aggregate
                ? aggregate.AllSources
                : new[] { skin };
            bool foreignSameIdentity = false;

            foreach (ISkin source in sources)
            {
                ISkin unwrapped = unwrap(source);

                if (unwrapped is not BmsLegacySkin candidate)
                    continue;

                if (packageRevision.RetainsExactSource(candidate))
                    return candidate;

                if (candidate.SkinInfo.ID == packageRevision.RecordId
                    && StringComparer.Ordinal.Equals(candidate.GetCurrentRevisionContentIdentity(), packageRevision.ContentRevision))
                {
                    foreignSameIdentity = true;
                }
            }

            if (foreignSameIdentity)
                throw new InvalidOperationException("The BMS material source chain contains only a foreign same-identity package owner.");

            return null;
        }

        private static ISkin unwrap(ISkin source)
        {
            ISkin current = source;

            while (current is ISkinTransformer transformer && !ReferenceEquals(transformer.Skin, current))
                current = transformer.Skin;

            return current;
        }

        private static readonly BmsNoteSkinElements[] note_elements =
        {
            BmsNoteSkinElements.Note,
            BmsNoteSkinElements.LongNoteHead,
            BmsNoteSkinElements.LongNoteBody,
            BmsNoteSkinElements.LongNoteTail,
        };

        private sealed record FinalProviderRegistration(
            IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, IBmsResolvedNoteMaterial> Provider,
            GameplaySkinResolvedMaterialSourceIdentity Identity);

        private sealed class SelectedMaterialProvider :
            IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, IBmsResolvedNoteMaterial>
        {
            private readonly GameplaySkinResolvedMaterialSet selectedMaterials;
            private readonly GameplaySkinResolvedMaterialSourceIdentity identity;

            public string Name { get; }

            public SelectedMaterialProvider(
                string name,
                GameplaySkinResolvedMaterialSet selectedMaterials,
                GameplaySkinResolvedMaterialSourceIdentity identity)
            {
                Name = name;
                this.selectedMaterials = selectedMaterials;
                this.identity = identity;
            }

            public SkinSlotResult<IBmsResolvedNoteMaterial> GetSlot(
                GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext> slot)
            {
                ArgumentNullException.ThrowIfNull(slot);

                if (!ReferenceEquals(slot.Context.Topology, selectedMaterials.Snapshot.Context.Topology)
                    || !ReferenceEquals(slot.Descriptor, slot.Context.Field.Slot))
                {
                    throw new ArgumentException("The selected material provider must use its exact layout and catalog field.", nameof(slot));
                }

                GameplaySkinResolvedMaterialEntry? entry = selectedMaterials.Entries.SingleOrDefault(candidate =>
                    ReferenceEquals(candidate.Slot, slot.Descriptor)
                    && candidate.Target.LaneId != null
                    && candidate.Target.LaneId.Equals(slot.Context.LaneId));

                if (entry == null || !entry.Source.Equals(identity))
                    return SkinSlotResult<IBmsResolvedNoteMaterial>.Inherit;

                return entry.State == GameplaySkinResolvedMaterialState.Suppress
                    ? SkinSlotResult<IBmsResolvedNoteMaterial>.Suppress
                    : SkinSlotResult<IBmsResolvedNoteMaterial>.Provide(entry.GetMaterial<IBmsResolvedNoteMaterial>());
            }
        }

        private sealed class ConventionalMaterialProvider :
            IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, IBmsResolvedNoteMaterial>
        {
            private readonly ISkin source;
            private readonly BmsGameplayLayoutSnapshot layout;

            public string Name { get; }

            public ConventionalMaterialProvider(string name, ISkin source, BmsGameplayLayoutSnapshot layout)
            {
                Name = name;
                this.source = source;
                this.layout = layout;
            }

            public SkinSlotResult<IBmsResolvedNoteMaterial> GetSlot(
                GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext> slot)
            {
                ArgumentNullException.ThrowIfNull(slot);

                if (!ReferenceEquals(slot.Context.Topology, layout.Neutral.Context.Topology)
                    || !ReferenceEquals(slot.Descriptor, slot.Context.Field.Slot))
                {
                    throw new ArgumentException("A conventional BMS material provider must use its exact layout and catalog field.", nameof(slot));
                }

                BmsGameplayLayoutLane lane = layout.GetLane(slot.Context.LaneId);
                BmsNoteSkinElements element = getElement(slot.Context.Field);
                return tryCreateConventionalMaterial(source, layout, lane, element, out IBmsResolvedNoteMaterial? material)
                    ? SkinSlotResult<IBmsResolvedNoteMaterial>.Provide(material!)
                    : SkinSlotResult<IBmsResolvedNoteMaterial>.Inherit;
            }
        }

        private sealed class ProgrammaticMaterialProvider :
            IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, IBmsResolvedNoteMaterial>
        {
            private readonly BmsGameplayLayoutSnapshot layout;

            public string Name => "programmatic.bms-note";

            public ProgrammaticMaterialProvider(BmsGameplayLayoutSnapshot layout)
            {
                this.layout = layout;
            }

            public SkinSlotResult<IBmsResolvedNoteMaterial> GetSlot(
                GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext> slot)
            {
                ArgumentNullException.ThrowIfNull(slot);

                if (!ReferenceEquals(slot.Context.Topology, layout.Neutral.Context.Topology)
                    || !ReferenceEquals(slot.Descriptor, slot.Context.Field.Slot))
                {
                    throw new ArgumentException("The BMS programmatic provider must use its exact layout and catalog field.", nameof(slot));
                }

                BmsGameplayLayoutLane lane = layout.GetLane(slot.Context.LaneId);
                BmsNoteSkinElements element = getElement(slot.Context.Field);
                return SkinSlotResult<IBmsResolvedNoteMaterial>.Provide(
                    new BmsProgrammaticNoteMaterial(element, lane.LogicalIndex, lane.IsScratch, layout.Keymode));
            }
        }
    }
}
