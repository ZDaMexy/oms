// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Prepares the complete C4 note/hold/key material surface for one exact C2/C3 publication.
    /// All configuration and texture lookup ends here on the background prepare path.
    /// </summary>
    internal static class ManiaGameplaySkinMaterialResolver
    {
        private const double fixed_frame_length = 1000d / 60;

        internal static GameplaySkinRuntimeCapabilitySet RuntimeCapabilities { get; } = GameplaySkinRuntimeCapabilitySet.Create(new[]
        {
            GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.Note, GameplaySkinRuntimeSlotCapability.Provide),
            GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.LongNoteHead, GameplaySkinRuntimeSlotCapability.Provide),
            GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.LongNoteBody, GameplaySkinRuntimeSlotCapability.Provide),
            GameplaySkinRuntimeSlotSupport.Create(
                GameplaySkinSlotCatalog.LongNoteTail,
                GameplaySkinRuntimeSlotCapability.Provide | GameplaySkinRuntimeSlotCapability.Suppress),
            GameplaySkinRuntimeSlotSupport.Create(
                GameplaySkinSlotCatalog.KeyVisual,
                GameplaySkinRuntimeSlotCapability.Provide | GameplaySkinRuntimeSlotCapability.Suppress),
        });

        public static GameplaySkinResolvedMaterialSet Resolve(
            GameplaySkinLayoutSnapshot snapshot,
            ISkinSource skinSource,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(skinSource);
            cancellationToken.ThrowIfCancellationRequested();

            if (snapshot.Context.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility)
                return GameplaySkinResolvedMaterialSet.CreateEmpty(snapshot);

            GameplaySkinPackageRevision package = snapshot.Context.PackageRevision;
            ISkin[] sources = skinSource.AllSources.ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            SelectedSkin selected = sources
                                    .Select(source => new SelectedSkin(source, unwrapSkin(source) as Skin))
                                    .FirstOrDefault(pair => pair.Raw != null && package.RetainsExactSource(pair.Raw))
                                    ?? throw new InvalidOperationException("The exact mania package revision is not present in the captured skin-source vector.");

            if (!selected.Raw!.AllowsGameplaySkinDocumentAuthoring)
                throw new InvalidOperationException("The selected mania package is not eligible for gameplay-skin document authoring.");

            GameplaySkinDocument selectedDocument = selected.Raw.GameplaySkinDocument.BindToPublication(snapshot);
            GameplaySkinResolvedMaterialSourceIdentity selectedIdentity = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected-common",
                selectedDocument.Identity.ContentRevision);
            GameplaySkinResolvedMaterialSourceIdentity selectedLegacyIdentity = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected-legacy-mania",
                selectedDocument.Identity.ContentRevision);

            if (selectedDocument.Identity.SourceId != package.RecordId
                || selectedDocument.Identity.PackageRevision != package.Generation
                || selectedDocument.Identity.CurrentRevision != package.Generation
                || selectedDocument.Identity.LayoutRevision != snapshot.Context.LayoutRevision)
            {
                throw new InvalidOperationException("The selected mania document did not bind to the exact package/layout publication.");
            }

            var fallbackSources = new List<LegacySource>();
            LegacySource? legacyBeatmapCompatibility = sources
                                                        .Select(source => (Transformed: source, Raw: unwrapSkin(source) as LegacyBeatmapSkin))
                                                        .Where(candidate => candidate.Raw is { AllowsGameplaySkinDocumentAuthoring: false })
                                                        .Select(candidate => new LegacySource(
                                                            "legacy-beatmap-compatibility",
                                                            candidate.Transformed,
                                                            GameplaySkinResolvedMaterialSourceIdentity.Create(
                                                                GameplaySkinResolvedMaterialSourceKind.LegacyBeatmapCompatibility,
                                                                "legacy-beatmap-compatibility",
                                                                candidate.Raw!.GameplaySkinDocument.Identity.ContentRevision)))
                                                        .FirstOrDefault();

            ISkin? rulesetResources = sources.FirstOrDefault(source => unwrapSkin(source) is ResourceStoreBackedSkin);
            if (rulesetResources != null)
            {
                fallbackSources.Add(new LegacySource(
                    "ruleset-legacy",
                    rulesetResources,
                        GameplaySkinResolvedMaterialSourceIdentity.Create(
                        GameplaySkinResolvedMaterialSourceKind.RulesetResources,
                        "ruleset-legacy",
                        "embedded-v1")));
            }

            ISkin? canonical = sources.FirstOrDefault(source =>
            {
                ISkin raw = unwrapSkin(source);
                return raw is Skin candidate
                       && !ReferenceEquals(candidate, selected.Raw)
                       && candidate.SkinInfo.PerformRead(info => info.Protected);
            });

            if (canonical != null && unwrapSkin(canonical) is Skin canonicalSkin)
            {
                fallbackSources.Add(new LegacySource(
                    "canonical-legacy",
                    canonical,
                    GameplaySkinResolvedMaterialSourceIdentity.Create(
                        GameplaySkinResolvedMaterialSourceKind.CanonicalPackage,
                        "canonical-legacy",
                        canonicalSkin.GameplaySkinDocument.Identity.ContentRevision)));
            }

            GameplaySkinResolvedMaterialSourceIdentity programmaticIdentity = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback,
                "programmatic",
                "v1");
            var entries = new List<GameplaySkinResolvedMaterialEntry>();
            var diagnostics = new List<GameplaySkinResolvedMaterialDiagnostic>();
            addDocumentDiagnostics(selectedDocument, snapshot, selectedIdentity, diagnostics, cancellationToken);
            addUnsupportedCapabilityDiagnostics(selectedDocument, snapshot, selectedIdentity, diagnostics, cancellationToken);

            foreach (GameplaySkinLayoutLane layoutLane in snapshot.LanesInLogicalOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameplaySkinLaneTopologyEntry lane = layoutLane.TopologyEntry;
                GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder
                                                               .Single(candidate => candidate.Identity.Id.Equals(lane.Identity.Group.Id));
                GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);

                foreach (ManiaSkinComponents component in supported_components)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!ManiaGameplaySkinMaterialMapping.TryGetDescriptor(component, out GameplaySkinSlotDescriptor? descriptor))
                        throw new InvalidOperationException("The mania C4 component mapping is incomplete.");

                    var lookup = new ManiaMaterialLookup(component, target, group, lane);
                    var providerSources = new Dictionary<string, GameplaySkinResolvedMaterialSourceIdentity>(StringComparer.Ordinal);
                    var providers = new List<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<ManiaMaterialLookup>, ManiaResolvedCandidate>>();

                    // Frozen legacy beatmap visuals stay above the selected package, including selected Suppress.
                    // This provider only reads the transformed legacy resource/config compatibility surface; it
                    // never reads or binds the beatmap skin's GameplaySkinDocument.
                    if (legacyBeatmapCompatibility != null)
                    {
                        addProvider(
                            new LegacyProvider(
                                legacyBeatmapCompatibility.Name,
                                legacyBeatmapCompatibility.Skin,
                                legacyBeatmapCompatibility.Identity,
                                diagnostics,
                                cancellationToken),
                            legacyBeatmapCompatibility.Identity);
                    }

                    addProvider(
                        new GameplaySkinDocumentSlotProvider<ManiaMaterialLookup, ManiaResolvedCandidate>(
                            selectedDocument,
                            RuntimeCapabilities,
                            "selected-common",
                            context => context.Target,
                            (entry, context) =>
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                IManiaGameplaySkinMaterial? material = null;
                                bool created = !string.IsNullOrWhiteSpace(entry.Value)
                                               && tryCreateMaterial(
                                                   selected.Transformed,
                                                   context,
                                                   entry.Value,
                                                   commonDeclaration: true,
                                                   out material);
                                cancellationToken.ThrowIfCancellationRequested();

                                if (!created)
                                {
                                    throw new ManiaMaterialPreparationException("mania.document.resource-missing");
                                }

                                return new ManiaResolvedCandidate(material!, selectedIdentity);
                            }),
                        selectedIdentity);
                    addProvider(
                        new LegacyProvider("selected-legacy-mania", selected.Transformed, selectedLegacyIdentity, diagnostics, cancellationToken),
                        selectedLegacyIdentity);

                    foreach (LegacySource fallback in fallbackSources)
                    {
                        addProvider(
                            new LegacyProvider(fallback.Name, fallback.Skin, fallback.Identity, diagnostics, cancellationToken),
                            fallback.Identity);
                    }

                    addProvider(new ProgrammaticProvider(programmaticIdentity, cancellationToken), programmaticIdentity);

                    GameplaySkinSlotResolution<ManiaResolvedCandidate> resolution = GameplaySkinSlotResolver.Resolve(
                        descriptor!,
                        lookup,
                        providers,
                        candidate => candidate.Material != null);

                    var key = new GameplaySkinResolvedMaterialKey(descriptor!, target);

                    foreach (GameplaySkinSlotDiagnostic diagnostic in resolution.Diagnostics)
                    {
                        providerSources.TryGetValue(diagnostic.ProviderName, out GameplaySkinResolvedMaterialSourceIdentity? diagnosticSource);
                        diagnostics.Add(new GameplaySkinResolvedMaterialDiagnostic(mapDiagnostic(diagnostic), key, diagnosticSource));
                    }

                    switch (resolution.Result.Kind)
                    {
                        case SkinSlotResultKind.Provide:
                            ManiaResolvedCandidate candidate = resolution.Result.Value;
                            entries.Add(GameplaySkinResolvedMaterialEntry.Provide(
                                descriptor!,
                                target,
                                candidate.Source,
                                typeof(IManiaGameplaySkinMaterial),
                                candidate.Material));
                            break;

                        case SkinSlotResultKind.Suppress:
                            if (resolution.ProviderName == null
                                || !providerSources.TryGetValue(resolution.ProviderName, out GameplaySkinResolvedMaterialSourceIdentity? suppressSource))
                            {
                                throw new InvalidOperationException("A suppressed mania material has no stable source authority.");
                            }

                            entries.Add(GameplaySkinResolvedMaterialEntry.Suppress(descriptor!, target, suppressSource));
                            break;

                        default:
                            // The final programmatic provider is total. Reaching inherit means prepare is incomplete;
                            // aborting here keeps the previously committed package/layout/material triple unchanged.
                            throw new InvalidOperationException("The mania material provider chain did not produce a complete result.");
                    }

                    void addProvider(
                        IGameplaySkinSlotProvider<GameplaySkinSlotLookup<ManiaMaterialLookup>, ManiaResolvedCandidate> provider,
                        GameplaySkinResolvedMaterialSourceIdentity sourceIdentity)
                    {
                        providers.Add(provider);
                        providerSources.Add(provider.Name, sourceIdentity);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return GameplaySkinResolvedMaterialSet.Create(
                snapshot,
                GameplaySkinMaterialContractIdentity.Current,
                entries,
                diagnostics);
        }

        private static readonly ManiaSkinComponents[] supported_components =
        {
            ManiaSkinComponents.Note,
            ManiaSkinComponents.HoldNoteHead,
            ManiaSkinComponents.HoldNoteBody,
            ManiaSkinComponents.HoldNoteTail,
            ManiaSkinComponents.KeyArea,
        };

        private static string mapDiagnostic(GameplaySkinSlotDiagnostic diagnostic)
        {
            if (diagnostic.Exception is GameplaySkinDocumentSlotRejectedException rejected)
            {
                return rejected.Code switch
                {
                    "gameplay-skin.document-fatal" => "mania.document.fatal",
                    "gameplay-skin.entry-empty" => "mania.document.empty",
                    "gameplay-skin.entry-invalid" => "mania.document.invalid",
                    "gameplay-skin.capability-unsupported" => "mania.document.unsupported",
                    "gameplay-skin.suppress-unsupported" => "mania.resolver.critical-suppress-rejected",
                    "gameplay-skin.provide-unsupported" => "mania.document.unsupported",
                    "gameplay-skin.target-invalid" => "mania.document.target-invalid",
                    "gameplay-skin.applicability-unsupported" => "mania.document.applicability-unsupported",
                    _ => "mania.document.invalid",
                };
            }

            if (diagnostic.Exception is ManiaMaterialPreparationException preparation)
                return preparation.Code;

            return diagnostic.Code switch
            {
                GameplaySkinSlotDiagnosticCode.ProviderFailed => "mania.resolver.provider-failed",
                GameplaySkinSlotDiagnosticCode.ProvidedValueRejected => "mania.resolver.value-rejected",
                GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed => "mania.resolver.validation-failed",
                GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected => "mania.resolver.critical-suppress-rejected",
                GameplaySkinSlotDiagnosticCode.InvalidResult => "mania.resolver.invalid-result",
                _ => "mania.resolver.invalid-result",
            };
        }

        private static void addDocumentDiagnostics(
            GameplaySkinDocument document,
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSourceIdentity source,
            ICollection<GameplaySkinResolvedMaterialDiagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            foreach (GameplaySkinCodecDiagnostic diagnostic in document.Diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();

                GameplaySkinDocumentEntry? entry = document.Sections
                                                               .SelectMany(section => section.Entries)
                                                               .LastOrDefault(candidate => candidate.LineNumber == diagnostic.LineNumber);

                if (entry != null
                    && !GameplaySkinSlotApplicabilityValidator.IsSelectorApplicable(entry.Target, snapshot))
                {
                    continue;
                }

                GameplaySkinSlotDescriptor? descriptor = null;
                GameplaySkinResolvedMaterialKey? key = null;

                if (diagnostic.SlotId != null
                    && GameplaySkinSlotCatalog.TryGet(diagnostic.SlotId, out descriptor))
                {
                    if (entry != null
                        && ReferenceEquals(entry.Descriptor, descriptor)
                        && tryCreateMaterialTarget(snapshot, entry.Target, out GameplaySkinResolvedMaterialTarget? target))
                    {
                        try
                        {
                            key = new GameplaySkinResolvedMaterialKey(descriptor, target!);
                        }
                        catch (ArgumentException)
                        {
                            // The shared document diagnostic below retains the catalog ID without inventing a target.
                        }
                    }
                }

                string code = diagnostic.Id;
                diagnostics.Add(key != null
                    ? new GameplaySkinResolvedMaterialDiagnostic(code, key, source)
                    : GameplaySkinResolvedMaterialDiagnostic.ForDocument(code, source, descriptor));
            }
        }

        private static void addUnsupportedCapabilityDiagnostics(
            GameplaySkinDocument document,
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSourceIdentity source,
            ICollection<GameplaySkinResolvedMaterialDiagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            foreach (GameplaySkinDocumentEntry entry in document.Sections.SelectMany(section => section.Entries))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.Presence != GameplaySkinDocumentDeclarationPresence.Declared
                    || entry.Descriptor == null
                    || RuntimeCapabilities.TryGet(entry.Descriptor, out _)
                    || GameplaySkinSlotApplicabilityValidator.ValidatePublicationTarget(entry.Target, snapshot)
                    != GameplaySkinDocumentPublicationTargetValidationResult.Valid
                    || !tryCreateMaterialTarget(snapshot, entry.Target, out GameplaySkinResolvedMaterialTarget? target))
                {
                    continue;
                }

                try
                {
                    diagnostics.Add(new GameplaySkinResolvedMaterialDiagnostic(
                        "mania.capability.unsupported-slot",
                        new GameplaySkinResolvedMaterialKey(entry.Descriptor, target!),
                        source));
                }
                catch (ArgumentException)
                {
                    // Invalid scope/index declarations remain represented by the shared codec diagnostic.
                }
            }
        }

        private static bool tryCreateMaterialTarget(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinDocumentTarget documentTarget,
            out GameplaySkinResolvedMaterialTarget? target)
        {
            GameplaySkinLaneTopologySnapshot topology = snapshot.Context.Topology;

            switch (documentTarget.Kind)
            {
                case GameplaySkinDocumentTargetKind.Global:
                    target = GameplaySkinResolvedMaterialTarget.Global;
                    break;

                case GameplaySkinDocumentTargetKind.Stage:
                case GameplaySkinDocumentTargetKind.Group:
                    if (documentTarget.GroupId == null
                        || !topology.TryGetGroup(documentTarget.GroupId, out GameplaySkinLaneTopologyGroup? group)
                        || group == null)
                    {
                        target = null;
                        return false;
                    }

                    target = documentTarget.Kind == GameplaySkinDocumentTargetKind.Stage
                        ? GameplaySkinResolvedMaterialTarget.ForStage(group)
                        : GameplaySkinResolvedMaterialTarget.ForGroup(group);
                    break;

                case GameplaySkinDocumentTargetKind.Lane:
                    if (documentTarget.GroupId == null
                        || documentTarget.LaneId == null
                        || !topology.TryGetGroup(documentTarget.GroupId, out GameplaySkinLaneTopologyGroup? laneGroup)
                        || laneGroup == null
                        || !topology.TryGetLane(documentTarget.LaneId, out GameplaySkinLaneTopologyEntry? lane)
                        || lane == null)
                    {
                        target = null;
                        return false;
                    }

                    target = GameplaySkinResolvedMaterialTarget.ForLane(laneGroup, lane);
                    break;

                default:
                    target = null;
                    return false;
            }

            if (!documentTarget.Matches(snapshot, target))
            {
                target = null;
                return false;
            }

            return true;
        }

        private static ISkin unwrapSkin(ISkin skin)
        {
            while (skin is ISkinTransformer transformer)
                skin = transformer.Skin;

            return skin;
        }

        private sealed record LegacySource(
            string Name,
            ISkin Skin,
            GameplaySkinResolvedMaterialSourceIdentity Identity);

        private sealed record SelectedSkin(ISkin Transformed, Skin? Raw);

        private sealed record ManiaMaterialLookup(
            ManiaSkinComponents Component,
            GameplaySkinResolvedMaterialTarget Target,
            GameplaySkinLaneTopologyGroup Group,
            GameplaySkinLaneTopologyEntry Lane);

        private sealed record ManiaResolvedCandidate(
            IManiaGameplaySkinMaterial Material,
            GameplaySkinResolvedMaterialSourceIdentity Source);

        private sealed class ManiaMaterialPreparationException : Exception
        {
            public string Code { get; }

            public ManiaMaterialPreparationException(string code)
                : base(code)
            {
                Code = code;
            }
        }

        private abstract class ManiaProvider : IGameplaySkinSlotProvider<GameplaySkinSlotLookup<ManiaMaterialLookup>, ManiaResolvedCandidate>
        {
            public string Name { get; }

            public GameplaySkinResolvedMaterialSourceIdentity SourceIdentity { get; }

            protected ManiaProvider(string name, GameplaySkinResolvedMaterialSourceIdentity sourceIdentity)
            {
                Name = name;
                SourceIdentity = sourceIdentity;
            }

            public abstract SkinSlotResult<ManiaResolvedCandidate> GetSlot(GameplaySkinSlotLookup<ManiaMaterialLookup> slot);

            protected SkinSlotResult<ManiaResolvedCandidate> Provide(IManiaGameplaySkinMaterial material)
                => SkinSlotResult<ManiaResolvedCandidate>.Provide(new ManiaResolvedCandidate(material, SourceIdentity));
        }

        private sealed class LegacyProvider : ManiaProvider
        {
            private readonly ISkin skin;
            private readonly List<GameplaySkinResolvedMaterialDiagnostic> diagnostics;
            private readonly CancellationToken cancellationToken;

            public LegacyProvider(
                string name,
                ISkin skin,
                GameplaySkinResolvedMaterialSourceIdentity sourceIdentity,
                List<GameplaySkinResolvedMaterialDiagnostic> diagnostics,
                CancellationToken cancellationToken)
                : base(name, sourceIdentity)
            {
                this.skin = skin;
                this.diagnostics = diagnostics;
                this.cancellationToken = cancellationToken;
            }

            public override SkinSlotResult<ManiaResolvedCandidate> GetSlot(GameplaySkinSlotLookup<ManiaMaterialLookup> slot)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!tryCreateMaterial(skin, slot.Context, null, commonDeclaration: false, out IManiaGameplaySkinMaterial? material))
                    return SkinSlotResult<ManiaResolvedCandidate>.Inherit;

                cancellationToken.ThrowIfCancellationRequested();

                if (slot.Context.Component == ManiaSkinComponents.KeyArea
                    && material is ManiaGameplaySkinKeyMaterial key
                    && ReferenceEquals(key.UpTexture, key.DownTexture))
                {
                    diagnostics.Add(new GameplaySkinResolvedMaterialDiagnostic(
                        "mania.legacy.key-down-fallback",
                        new GameplaySkinResolvedMaterialKey(slot.Descriptor, slot.Context.Target),
                        SourceIdentity));
                }

                return Provide(material!);
            }
        }

        private sealed class ProgrammaticProvider : ManiaProvider
        {
            private readonly CancellationToken cancellationToken;

            public ProgrammaticProvider(
                GameplaySkinResolvedMaterialSourceIdentity sourceIdentity,
                CancellationToken cancellationToken)
                : base("programmatic-fallback", sourceIdentity)
            {
                this.cancellationToken = cancellationToken;
            }

            public override SkinSlotResult<ManiaResolvedCandidate> GetSlot(GameplaySkinSlotLookup<ManiaMaterialLookup> slot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Provide(new ManiaGameplaySkinProgrammaticMaterial(slot.Context.Component));
            }
        }

        private static bool tryCreateMaterial(
            ISkin skin,
            ManiaMaterialLookup lookup,
            string? commonResource,
            bool commonDeclaration,
            out IManiaGameplaySkinMaterial? material)
        {
            string fallback = getFallbackColumnIndex(lookup.Group, lookup.Lane);
            int globalIndex = lookup.Lane.GlobalLogicalIndex;

            switch (lookup.Component)
            {
                case ManiaSkinComponents.Note:
                    return tryCreateNoteMaterial(
                        skin,
                        commonResource ?? getLegacyName(skin, LegacyManiaSkinConfigurationLookups.NoteImage, globalIndex, $"mania-note{fallback}"),
                        globalIndex,
                        lookup.Component,
                        out material);

                case ManiaSkinComponents.HoldNoteHead:
                    if (tryCreateNoteMaterial(
                            skin,
                            commonResource ?? getLegacyName(skin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage, globalIndex, $"mania-note{fallback}H"),
                            globalIndex,
                            lookup.Component,
                            out material))
                        return true;

                    return !commonDeclaration
                           && tryCreateNoteMaterial(
                               skin,
                               getLegacyName(skin, LegacyManiaSkinConfigurationLookups.NoteImage, globalIndex, $"mania-note{fallback}"),
                               globalIndex,
                               lookup.Component,
                               out material);

                case ManiaSkinComponents.HoldNoteTail:
                    if (tryCreateNoteMaterial(
                            skin,
                            commonResource ?? getLegacyName(skin, LegacyManiaSkinConfigurationLookups.HoldNoteTailImage, globalIndex, $"mania-note{fallback}T"),
                            globalIndex,
                            lookup.Component,
                            out material))
                        return true;

                    if (commonDeclaration)
                        return false;

                    if (tryCreateNoteMaterial(
                            skin,
                            getLegacyName(skin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage, globalIndex, $"mania-note{fallback}H"),
                            globalIndex,
                            lookup.Component,
                            out material))
                        return true;

                    return tryCreateNoteMaterial(
                        skin,
                        getLegacyName(skin, LegacyManiaSkinConfigurationLookups.NoteImage, globalIndex, $"mania-note{fallback}"),
                        globalIndex,
                        lookup.Component,
                        out material);

                case ManiaSkinComponents.HoldNoteBody:
                {
                    LegacyNoteBodyStyle? bodyStyle = commonDeclaration
                        ? LegacyNoteBodyStyle.Stretch
                        : skin.GetManiaSkinConfig<LegacyNoteBodyStyle>(LegacyManiaSkinConfigurationLookups.NoteBodyStyle)?.Value;
                    WrapMode wrapMode = bodyStyle == LegacyNoteBodyStyle.Stretch ? WrapMode.ClampToEdge : WrapMode.Repeat;
                    string bodyName = commonResource
                                      ?? getLegacyName(skin, LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage, globalIndex, $"mania-note{fallback}L");

                    if (!tryGetAnimation(skin, bodyName, wrapMode, wrapMode, 30, out ManiaGameplaySkinAnimationMaterial? body))
                    {
                        material = null;
                        return false;
                    }

                    ManiaGameplaySkinAnimationMaterial? light = null;
                    float lightScale = 1;

                    if (!commonDeclaration)
                    {
                        string lightName = getLegacyName(skin, LegacyManiaSkinConfigurationLookups.HoldNoteLightImage, globalIndex, "lightingL");
                        Texture[] lightFrames = getTextures(skin, lightName, default, default, true);

                        if (lightFrames.Length > 0)
                        {
                            double lightFrameLength = Math.Max(fixed_frame_length, 170d / lightFrames.Length);
                            light = new ManiaGameplaySkinAnimationMaterial(lightFrames, lightFrameLength);
                        }

                        lightScale = skin.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.HoldNoteLightScale, globalIndex)?.Value ?? 1;
                        if (!float.IsFinite(lightScale) || lightScale <= 0)
                            lightScale = 1;
                    }

                    material = new ManiaGameplaySkinBodyMaterial(body!, light, lightScale, bodyStyle);
                    return true;
                }

                case ManiaSkinComponents.KeyArea:
                {
                    string upName = commonResource
                                    ?? getLegacyName(skin, LegacyManiaSkinConfigurationLookups.KeyImage, globalIndex, $"mania-key{fallback}");
                    Texture? up = skin.GetTexture(upName, WrapMode.ClampToEdge, default);

                    if (up == null)
                    {
                        material = null;
                        return false;
                    }

                    Texture? down = commonDeclaration
                        ? up
                        : skin.GetTexture(
                            getLegacyName(skin, LegacyManiaSkinConfigurationLookups.KeyImageDown, globalIndex, $"mania-key{fallback}D"),
                            WrapMode.ClampToEdge,
                            default);
                    down ??= up;

                    bool keysUnderNotes = !commonDeclaration
                                          && (skin.GetManiaSkinConfig<bool>(LegacyManiaSkinConfigurationLookups.KeysUnderNotes, globalIndex)?.Value ?? false);
                    material = new ManiaGameplaySkinKeyMaterial(up, down, keysUnderNotes);
                    return true;
                }

                default:
                    material = null;
                    return false;
            }
        }

        private static bool tryCreateNoteMaterial(
            ISkin skin,
            string resourceName,
            int globalIndex,
            ManiaSkinComponents component,
            out IManiaGameplaySkinMaterial? material)
        {
            if (!tryGetAnimation(
                    skin,
                    resourceName,
                    WrapMode.ClampToEdge,
                    WrapMode.ClampToEdge,
                    fixed_frame_length,
                    out ManiaGameplaySkinAnimationMaterial? animation))
            {
                material = null;
                return false;
            }

            float? width = skin.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, globalIndex)?.Value;
            if (width is <= 0 || width.HasValue && !float.IsFinite(width.Value))
                width = null;

            material = new ManiaGameplaySkinNoteMaterial(component, animation!, width);
            return true;
        }

        private static bool tryGetAnimation(
            ISkin skin,
            string resourceName,
            WrapMode wrapModeS,
            WrapMode wrapModeT,
            double frameLength,
            out ManiaGameplaySkinAnimationMaterial? animation)
        {
            Texture[] frames = getTextures(skin, resourceName, wrapModeS, wrapModeT, true);

            if (frames.Length == 0)
            {
                animation = null;
                return false;
            }

            animation = new ManiaGameplaySkinAnimationMaterial(frames, frameLength);
            return true;
        }

        private static Texture[] getTextures(
            ISkin skin,
            string resourceName,
            WrapMode wrapModeS,
            WrapMode wrapModeT,
            bool animatable)
            => skin.GetTextures(resourceName, wrapModeS, wrapModeT, animatable, "-", null, out _);

        private static string getLegacyName(
            ISkin skin,
            LegacyManiaSkinConfigurationLookups lookup,
            int globalIndex,
            string fallback)
            => skin.GetManiaSkinConfig<string>(lookup, globalIndex)?.Value is string configured
               && !string.IsNullOrWhiteSpace(configured)
                ? configured
                : fallback;

        private static string getFallbackColumnIndex(
            GameplaySkinLaneTopologyGroup group,
            GameplaySkinLaneTopologyEntry lane)
        {
            if (lane.Identity.Role == GameplaySkinLaneRole.SpecialKey)
                return "S";

            int distanceToEdge = Math.Min(
                lane.GroupLocalLogicalIndex,
                group.LanesInLogicalOrder.Count - 1 - lane.GroupLocalLogicalIndex);
            return distanceToEdge % 2 == 0 ? "1" : "2";
        }
    }
}
