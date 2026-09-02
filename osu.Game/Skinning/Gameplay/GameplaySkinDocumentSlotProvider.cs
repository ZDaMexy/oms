// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Stable, content-free reason why an author declaration could not become a runtime slot result.
    /// </summary>
    public sealed class GameplaySkinDocumentSlotRejectedException : Exception
    {
        public string Code { get; }

        internal GameplaySkinDocumentSlotRejectedException(string code)
            : base(code)
        {
            Code = code;
        }

        public override string ToString() => Code;
    }

    /// <summary>
    /// The sole shared adapter from a bound codec document to explicit Provide/Inherit/Suppress resolver results.
    /// </summary>
    /// <remarks>
    /// Rulesets supply only exact target projection and resource preparation. Presence, invalid/empty handling,
    /// capability enforcement and three-state semantics remain in core and therefore cannot drift between mania and
    /// BMS. The materializer runs during the owner's background prepare; a committed renderer never invokes it.
    /// </remarks>
    public sealed class GameplaySkinDocumentSlotProvider<TContext, TComponent> :
        IGameplaySkinSlotProvider<GameplaySkinSlotLookup<TContext>, TComponent>
        where TContext : notnull
        where TComponent : notnull
    {
        private readonly GameplaySkinDocument document;
        private readonly GameplaySkinRuntimeCapabilitySet capabilities;
        private readonly Func<TContext, GameplaySkinResolvedMaterialTarget> targetSelector;
        private readonly Func<GameplaySkinDocumentEntry, TContext, TComponent> materializer;

        public string Name { get; }

        public GameplaySkinDocumentSlotProvider(
            GameplaySkinDocument document,
            GameplaySkinRuntimeCapabilitySet capabilities,
            string name,
            Func<TContext, GameplaySkinResolvedMaterialTarget> targetSelector,
            Func<GameplaySkinDocumentEntry, TContext, TComponent> materializer)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.targetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
            this.materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));

            if (!document.IsBoundToPublication)
                throw new ArgumentException("A gameplay skin document provider requires an exact bound publication identity.", nameof(document));
        }

        public SkinSlotResult<TComponent> GetSlot(GameplaySkinSlotLookup<TContext> slot)
        {
            ArgumentNullException.ThrowIfNull(slot);

            if (document.HasFatalDiagnostics)
                throw rejected("gameplay-skin.document-fatal");

            GameplaySkinResolvedMaterialTarget target = targetSelector(slot.Context)
                                                        ?? throw rejected("gameplay-skin.target-invalid");

            if (GameplaySkinSlotApplicabilityValidator.Validate(slot.Descriptor, document.BoundPublicationSnapshot, target)
                != GameplaySkinSlotApplicabilityResult.Applicable)
            {
                throw rejected("gameplay-skin.applicability-unsupported");
            }

            GameplaySkinDocumentEntry entry = document.GetEntry(slot.Descriptor, target);

            if (entry.Presence == GameplaySkinDocumentDeclarationPresence.Absent)
                return SkinSlotResult<TComponent>.Inherit;

            if (entry.Validity == GameplaySkinDocumentValueValidity.Empty)
                throw rejected("gameplay-skin.entry-empty");

            if (entry.Validity != GameplaySkinDocumentValueValidity.Valid)
                throw rejected("gameplay-skin.entry-invalid");

            if (!capabilities.TryGet(slot.Descriptor, out GameplaySkinRuntimeSlotSupport? support) || support == null)
                throw rejected("gameplay-skin.capability-unsupported");

            switch (entry.Operation)
            {
                case GameplaySkinDocumentOperation.Inherit:
                    return SkinSlotResult<TComponent>.Inherit;

                case GameplaySkinDocumentOperation.Suppress:
                    if ((support.Capabilities & GameplaySkinRuntimeSlotCapability.Suppress) == 0)
                        throw rejected("gameplay-skin.suppress-unsupported");

                    return SkinSlotResult<TComponent>.Suppress;

                case GameplaySkinDocumentOperation.Provide:
                    if ((support.Capabilities & GameplaySkinRuntimeSlotCapability.Provide) == 0)
                        throw rejected("gameplay-skin.provide-unsupported");

                    TComponent component = materializer(entry, slot.Context) ?? throw rejected("gameplay-skin.material-invalid");
                    return SkinSlotResult<TComponent>.Provide(component);

                default:
                    throw rejected("gameplay-skin.operation-invalid");
            }
        }

        private static GameplaySkinDocumentSlotRejectedException rejected(string code)
            => new GameplaySkinDocumentSlotRejectedException(code);
    }
}
