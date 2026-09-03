// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The immutable prepared payload used by public resource slots which do not have a more specialised ruleset
    /// material type. Texture lookup ends while this value is prepared; a committed consumer only reads it.
    /// </summary>
    public sealed class GameplaySkinPublicSlotMaterial
    {
        public GameplaySkinSlotDescriptor Slot { get; }

        /// <summary>
        /// Exact package-relative author resource token, or <see langword="null"/> for the total programmatic fallback.
        /// This value is intentionally omitted from <see cref="ToString"/> and diagnostics.
        /// </summary>
        public string? ResourceName { get; }

        /// <summary>
        /// Package-owned texture prepared before publication. Its lifetime remains owned by the exact package revision.
        /// </summary>
        public Texture? Texture { get; }

        public bool IsProgrammaticFallback => ResourceName == null;

        private GameplaySkinPublicSlotMaterial(GameplaySkinSlotDescriptor slot, string? resourceName, Texture? texture)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));

            if (slot.ValueType != GameplaySkinSlotValueType.Resource)
                throw new ArgumentException("A public resource material requires a resource-valued catalog slot.", nameof(slot));

            if (resourceName == null)
            {
                if (texture != null)
                    throw new ArgumentException("A programmatic public-slot fallback cannot retain a package texture.", nameof(texture));
            }
            else
            {
                ResourceName = ValidateRelativeResourceName(resourceName);
                Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            }
        }

        public static GameplaySkinPublicSlotMaterial FromPreparedResource(
            GameplaySkinSlotDescriptor slot,
            string resourceName,
            Texture texture)
            => new GameplaySkinPublicSlotMaterial(slot, resourceName, texture);

        public static GameplaySkinPublicSlotMaterial CreateProgrammaticFallback(GameplaySkinSlotDescriptor slot)
            => new GameplaySkinPublicSlotMaterial(slot, null, null);

        public override string ToString()
            => $"{nameof(GameplaySkinPublicSlotMaterial)}:{Slot.Id}:{(IsProgrammaticFallback ? "Programmatic" : "PreparedResource")}";

        internal static string ValidateRelativeResourceName(string resourceName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

            if (resourceName.Length > 256
                || !SkinPackageResourceNameValidator.TryNormalise(resourceName, out string normalisedName, out _)
                || !string.Equals(resourceName, normalisedName, StringComparison.Ordinal))
            {
                throw new ArgumentException("A public gameplay-skin resource must be a bounded package-relative resource name.", nameof(resourceName));
            }

            return normalisedName;
        }
    }

    /// <summary>
    /// Deterministically expands a catalog descriptor into every exact applicable Global/Stage/Group/Lane target in
    /// one immutable layout snapshot. This is the sole target expansion used by generic public-slot preparation.
    /// </summary>
    public static class GameplaySkinPublicSlotMaterialTargets
    {
        public static IReadOnlyList<GameplaySkinResolvedMaterialTarget> Enumerate(
            GameplaySkinSlotDescriptor descriptor,
            GameplaySkinLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(snapshot);

            var targets = new List<GameplaySkinResolvedMaterialTarget>();

            if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Global) != 0)
                addIfApplicable(GameplaySkinResolvedMaterialTarget.Global);

            if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Stage) != 0)
            {
                foreach (GameplaySkinLaneTopologyGroup group in snapshot.Context.Topology.GroupsInLogicalOrder)
                    addIfApplicable(GameplaySkinResolvedMaterialTarget.ForStage(group));
            }

            if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Group) != 0)
            {
                foreach (GameplaySkinLaneTopologyGroup group in snapshot.Context.Topology.GroupsInLogicalOrder)
                    addIfApplicable(GameplaySkinResolvedMaterialTarget.ForGroup(group));
            }

            if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Lane) != 0)
            {
                foreach (GameplaySkinLaneTopologyEntry lane in snapshot.Context.Topology.LanesInLogicalOrder)
                {
                    GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder
                        .Single(candidate => candidate.Identity.Id.Equals(lane.Identity.Group.Id));
                    addIfApplicable(GameplaySkinResolvedMaterialTarget.ForLane(group, lane));
                }
            }

            return Array.AsReadOnly(targets.ToArray());

            void addIfApplicable(GameplaySkinResolvedMaterialTarget target)
            {
                if (GameplaySkinSlotApplicabilityValidator.Validate(descriptor, snapshot, target)
                    == GameplaySkinSlotApplicabilityResult.Applicable)
                {
                    targets.Add(target);
                }
            }
        }
    }

    /// <summary>
    /// Result of resolving generic public resource slots into one pending exact publication.
    /// </summary>
    public sealed class GameplaySkinPublicSlotMaterialResolution
    {
        public IReadOnlyList<GameplaySkinResolvedMaterialEntry> Entries { get; }

        public IReadOnlyList<GameplaySkinResolvedMaterialDiagnostic> Diagnostics { get; }

        internal GameplaySkinPublicSlotMaterialResolution(
            IEnumerable<GameplaySkinResolvedMaterialEntry> entries,
            IEnumerable<GameplaySkinResolvedMaterialDiagnostic> diagnostics)
        {
            Entries = Array.AsReadOnly(entries.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        }
    }

    /// <summary>
    /// Projects exact catalog suppression eligibility into the capability surface used by shared public-slot hosts.
    /// </summary>
    public static class GameplaySkinPublicSlotMaterialCapabilities
    {
        public static GameplaySkinRuntimeCapabilitySet Create(IEnumerable<GameplaySkinSlotDescriptor> descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            return GameplaySkinRuntimeCapabilitySet.Create(descriptors.Select(descriptor =>
                GameplaySkinRuntimeSlotSupport.Create(
                    descriptor,
                    GameplaySkinRuntimeSlotCapability.Provide
                    | (descriptor.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Allowed
                        ? GameplaySkinRuntimeSlotCapability.Suppress
                        : GameplaySkinRuntimeSlotCapability.None))));
        }
    }

    /// <summary>
    /// Shared background-only resolver for public resource slots without a specialised typed material.
    /// </summary>
    public static class GameplaySkinPublicSlotMaterialResolver
    {
        private const string selected_provider_name = "selected.public-slots";
        private const string programmatic_provider_name = "programmatic.public-slots";

        public static GameplaySkinPublicSlotMaterialResolution Resolve(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinDocument? selectedDocument,
            GameplaySkinRuntimeCapabilitySet capabilities,
            IEnumerable<GameplaySkinSlotDescriptor> descriptors,
            GameplaySkinResolvedMaterialSourceIdentity? selectedIdentity,
            GameplaySkinResolvedMaterialSourceIdentity programmaticIdentity,
            Func<string, Texture?> prepareTexture,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(capabilities);
            ArgumentNullException.ThrowIfNull(descriptors);
            ArgumentNullException.ThrowIfNull(programmaticIdentity);
            ArgumentNullException.ThrowIfNull(prepareTexture);

            if (selectedDocument != null)
            {
                ArgumentNullException.ThrowIfNull(selectedIdentity);

                if (!selectedDocument.IsBoundToPublication
                    || !ReferenceEquals(selectedDocument.BoundPublicationSnapshot, snapshot))
                {
                    throw new ArgumentException("Generic public-slot resolution requires the exact bound document publication.", nameof(selectedDocument));
                }
            }

            GameplaySkinSlotDescriptor[] copiedDescriptors = descriptors.ToArray();

            if (copiedDescriptors.Any(descriptor => descriptor == null
                                                    || !GameplaySkinSlotCatalog.TryGet(descriptor.Id, out GameplaySkinSlotDescriptor? catalogued)
                                                    || !ReferenceEquals(catalogued, descriptor)))
            {
                throw new ArgumentException("Generic public-slot resolution requires exact catalog descriptors.", nameof(descriptors));
            }

            if (copiedDescriptors.Select(descriptor => descriptor.Id).Distinct(StringComparer.Ordinal).Count() != copiedDescriptors.Length)
                throw new ArgumentException("Generic public-slot descriptors must be unique by stable catalog ID.", nameof(descriptors));

            var entries = new List<GameplaySkinResolvedMaterialEntry>();
            var diagnostics = new List<GameplaySkinResolvedMaterialDiagnostic>();
            var preparedTextures = new Dictionary<string, Texture?>(StringComparer.Ordinal);

            foreach (GameplaySkinSlotDescriptor descriptor in copiedDescriptors)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!capabilities.TryGet(descriptor, out GameplaySkinRuntimeSlotSupport? support)
                    || support == null
                    || (support.Capabilities & GameplaySkinRuntimeSlotCapability.Provide) == 0)
                {
                    throw new ArgumentException("A generic public-slot descriptor must have runtime Provide capability.", nameof(descriptors));
                }

                foreach (GameplaySkinResolvedMaterialTarget target in GameplaySkinPublicSlotMaterialTargets.Enumerate(descriptor, snapshot))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var context = new PublicSlotContext(descriptor, target);
                    var providers = new List<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<PublicSlotContext>, GameplaySkinPublicSlotMaterial>>();

                    if (selectedDocument != null)
                    {
                        providers.Add(new GameplaySkinDocumentSlotProvider<PublicSlotContext, GameplaySkinPublicSlotMaterial>(
                            selectedDocument,
                            capabilities,
                            selected_provider_name,
                            slot => slot.Target,
                            (entry, slot) =>
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                if (!ReferenceEquals(entry.Descriptor, slot.Descriptor)
                                    || string.IsNullOrWhiteSpace(entry.Value))
                                {
                                    throw new PublicSlotMaterialPreparationException("gameplay-skin.public-resource.invalid");
                                }

                                GameplaySkinPublicSlotMaterial material;

                                try
                                {
                                    string resourceName = GameplaySkinPublicSlotMaterial.ValidateRelativeResourceName(entry.Value);

                                    if (!preparedTextures.TryGetValue(resourceName, out Texture? texture))
                                    {
                                        texture = prepareTexture(resourceName);
                                        preparedTextures.Add(resourceName, texture);
                                    }

                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (texture == null)
                                        throw new PublicSlotMaterialPreparationException("gameplay-skin.public-resource.missing");

                                    material = GameplaySkinPublicSlotMaterial.FromPreparedResource(slot.Descriptor, resourceName, texture);
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (PublicSlotMaterialPreparationException)
                                {
                                    throw;
                                }
                                catch (ArgumentException)
                                {
                                    throw new PublicSlotMaterialPreparationException("gameplay-skin.public-resource.invalid");
                                }

                                return material;
                            }));
                    }

                    providers.Add(new ProgrammaticProvider());
                    GameplaySkinSlotResolution<GameplaySkinPublicSlotMaterial> resolution = GameplaySkinSlotResolver.Resolve(
                        descriptor,
                        context,
                        providers,
                        material => ReferenceEquals(material.Slot, descriptor));
                    var key = new GameplaySkinResolvedMaterialKey(descriptor, target);

                    foreach (GameplaySkinSlotDiagnostic diagnostic in resolution.Diagnostics)
                    {
                        diagnostics.Add(new GameplaySkinResolvedMaterialDiagnostic(
                            mapDiagnostic(diagnostic),
                            key,
                            string.Equals(diagnostic.ProviderName, selected_provider_name, StringComparison.Ordinal)
                                ? selectedIdentity!
                                : programmaticIdentity));
                    }

                    switch (resolution.Result.Kind)
                    {
                        case SkinSlotResultKind.Provide:
                            bool selected = string.Equals(resolution.ProviderName, selected_provider_name, StringComparison.Ordinal);
                            entries.Add(GameplaySkinResolvedMaterialEntry.Provide(
                                descriptor,
                                target,
                                selected ? selectedIdentity! : programmaticIdentity,
                                resolution.Result.Value));
                            break;

                        case SkinSlotResultKind.Suppress:
                            if (!string.Equals(resolution.ProviderName, selected_provider_name, StringComparison.Ordinal))
                                throw new InvalidOperationException("Only the exact selected document may suppress a generic public slot.");

                            entries.Add(GameplaySkinResolvedMaterialEntry.Suppress(descriptor, target, selectedIdentity!));
                            break;

                        default:
                            throw new InvalidOperationException("The generic public-slot provider chain did not produce a total result.");
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new GameplaySkinPublicSlotMaterialResolution(entries, diagnostics);
        }

        private static string mapDiagnostic(GameplaySkinSlotDiagnostic diagnostic)
        {
            if (diagnostic.Exception is GameplaySkinDocumentSlotRejectedException rejection)
                return rejection.Code;

            if (diagnostic.Exception is PublicSlotMaterialPreparationException preparation)
                return preparation.Code;

            return diagnostic.Code switch
            {
                GameplaySkinSlotDiagnosticCode.ProviderFailed => "gameplay-skin.public-resolver.provider-failed",
                GameplaySkinSlotDiagnosticCode.ProvidedValueRejected => "gameplay-skin.public-resolver.value-rejected",
                GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed => "gameplay-skin.public-resolver.validation-failed",
                GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected => "gameplay-skin.public-resolver.suppress-rejected",
                GameplaySkinSlotDiagnosticCode.InvalidResult => "gameplay-skin.public-resolver.invalid-result",
                _ => "gameplay-skin.public-resolver.invalid-result",
            };
        }

        private sealed record PublicSlotContext(
            GameplaySkinSlotDescriptor Descriptor,
            GameplaySkinResolvedMaterialTarget Target);

        private sealed class ProgrammaticProvider :
            IGameplaySkinSlotProvider<GameplaySkinSlotLookup<PublicSlotContext>, GameplaySkinPublicSlotMaterial>
        {
            public string Name => programmatic_provider_name;

            public SkinSlotResult<GameplaySkinPublicSlotMaterial> GetSlot(GameplaySkinSlotLookup<PublicSlotContext> slot)
            {
                ArgumentNullException.ThrowIfNull(slot);

                if (!ReferenceEquals(slot.Descriptor, slot.Context.Descriptor))
                    throw new ArgumentException("A programmatic public-slot fallback requires the exact descriptor context.", nameof(slot));

                return SkinSlotResult<GameplaySkinPublicSlotMaterial>.Provide(
                    GameplaySkinPublicSlotMaterial.CreateProgrammaticFallback(slot.Descriptor));
            }
        }

        private sealed class PublicSlotMaterialPreparationException : Exception
        {
            public string Code { get; }

            public PublicSlotMaterialPreparationException(string code)
                : base(code)
            {
                Code = code;
            }
        }
    }
}
