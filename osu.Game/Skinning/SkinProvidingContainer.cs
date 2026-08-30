// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A container which adds a provided <see cref="ISkin"/> to the DI skin lookup hierarchy.
    /// </summary>
    /// <remarks>
    /// This container will expose an <see cref="ISkinSource"/> to its children.
    /// The source will first consider the skin provided via the constructor (if any), then fallback
    /// to any <see cref="ISkinSource"/> providers in the parent DI hierarchy.
    /// </remarks>
    public partial class SkinProvidingContainer : Container, ISkinSource
    {
        public event Action? SourceChanged;

        protected ISkinSource? ParentSource { get; private set; }

        private SkinManager? skinManager;

        /// <summary>
        /// The exact immutable package revision retained by this provider root. Gameplay layout adapters cache this
        /// descriptor into their child dependency tree and bind every solved snapshot to it.
        /// </summary>
        protected GameplaySkinPackageRevision? GameplayPackageRevision { get; private set; }

        /// <summary>
        /// The C2 registration which owns <see cref="GameplayPackageRevision"/>. Exposed only inside osu.Game so the
        /// gameplay root can bind layout preparation to the existing participant/work lease protocol.
        /// </summary>
        private protected SkinRevisionParticipantRegistration? GameplayRevisionParticipant { get; private set; }

        /// <summary>
        /// The revision lifetime represented by this production consumer. Gameplay roots override this to block live
        /// publication because their geometry is constructed once and cannot be coherently rebuilt in place.
        /// </summary>
        private protected virtual SkinRevisionParticipantKind RevisionParticipantKind
            => SkinRevisionParticipantKind.CoherentVisualConsumer;

        /// <summary>
        /// Whether this participant can consume a gameplay-layout publication being prepared by a sibling root.
        /// Non-layout resource/sample providers retain the ordinary C2 lease and reload barrier contract, but cannot
        /// invalidate a layout carrier which their subtree is explicitly unable to observe.
        /// </summary>
        private protected virtual bool AffectsGameplayLayoutPublication => false;

        /// <summary>
        /// Whether falling back to parent <see cref="ISkinSource"/>s is allowed in this container.
        /// </summary>
        protected virtual bool AllowFallingBackToParent => true;

        protected virtual bool AllowDrawableLookup(ISkinComponentLookup lookup) => true;

        protected virtual bool AllowTextureLookup(string componentName) => true;

        protected virtual bool AllowSampleLookup(ISampleInfo sampleInfo) => true;

        protected virtual bool AllowConfigurationLookup => true;

        protected virtual bool AllowColourLookup => true;

        private readonly object sourceSetLock = new object();

        /// <summary>
        /// A dictionary mapping each <see cref="ISkin"/> source to a wrapper which handles lookup allowances.
        /// </summary>
        private (ISkin skin, DisableableSkinSource wrapped)[] skinSources = Array.Empty<(ISkin skin, DisableableSkinSource wrapped)>();

        /// <summary>
        /// Constructs a new <see cref="SkinProvidingContainer"/> initialised with a single skin source.
        /// </summary>
        public SkinProvidingContainer(ISkin? skin)
            : this()
        {
            if (skin != null)
                SetSources(new[] { skin });
        }

        /// <summary>
        /// Constructs a new <see cref="SkinProvidingContainer"/> with no sources.
        /// </summary>
        protected SkinProvidingContainer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            ParentSource = dependencies.Get<ISkinSource>();
            if (ParentSource != null)
                ParentSource.SourceChanged += TriggerSourceChanged;

            skinManager = dependencies.Get<SkinManager>();
            if (skinManager != null)
            {
                GameplayRevisionParticipant = skinManager.RegisterRevisionParticipant(
                    RevisionParticipantKind,
                    GetType().Name,
                    prepareCommit: RevisionParticipantKind == SkinRevisionParticipantKind.CoherentVisualConsumer
                        ? PrepareCurrentRevisionAsync
                        : null,
                    affectsGameplayLayoutPublication: AffectsGameplayLayoutPublication);
                GameplayPackageRevision = GameplaySkinPackageRevision.Create(GameplayRevisionParticipant.CurrentRevision);
            }

            dependencies.CacheAs<ISkinSource>(this);

            TriggerSourceChanged();

            return dependencies;
        }

        public ISkin? FindProvider(Func<ISkin, bool> lookupFunction)
        {
            foreach (var (skin, lookupWrapper) in skinSources)
            {
                if (lookupFunction(lookupWrapper))
                    return skin;
            }

            if (!AllowFallingBackToParent)
                return null;

            return ParentSource?.FindProvider(lookupFunction);
        }

        public IEnumerable<ISkin> AllSources
        {
            get
            {
                foreach (var i in skinSources)
                    yield return i.skin;

                if (AllowFallingBackToParent && ParentSource != null)
                {
                    foreach (var skin in ParentSource.AllSources)
                        yield return skin;
                }
            }
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            foreach (var (_, lookupWrapper) in skinSources)
            {
                Drawable? sourceDrawable;
                if ((sourceDrawable = lookupWrapper.GetDrawableComponent(lookup)) != null)
                    return sourceDrawable;
            }

            if (!AllowFallingBackToParent)
                return null;

            return ParentSource?.GetDrawableComponent(lookup);
        }

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
        {
            foreach (var (_, lookupWrapper) in skinSources)
            {
                Texture? sourceTexture;
                if ((sourceTexture = lookupWrapper.GetTexture(componentName, wrapModeS, wrapModeT)) != null)
                    return sourceTexture;
            }

            if (!AllowFallingBackToParent)
                return null;

            return ParentSource?.GetTexture(componentName, wrapModeS, wrapModeT);
        }

        public ISample? GetSample(ISampleInfo sampleInfo)
        {
            foreach (var (_, lookupWrapper) in skinSources)
            {
                ISample? sourceSample;
                if ((sourceSample = lookupWrapper.GetSample(sampleInfo)) != null)
                    return sourceSample;
            }

            if (!AllowFallingBackToParent)
                return null;

            return ParentSource?.GetSample(sampleInfo);
        }

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
        {
            try
            {
                Skin.LogLookupDebug(this, lookup, Skin.LookupDebugType.Enter);

                foreach (var (_, lookupWrapper) in skinSources)
                {
                    IBindable<TValue>? bindable;
                    if ((bindable = lookupWrapper.GetConfig<TLookup, TValue>(lookup)) != null)
                        return bindable;
                }

                if (!AllowFallingBackToParent)
                    return null;

                return ParentSource?.GetConfig<TLookup, TValue>(lookup);
            }
            finally
            {
                Skin.LogLookupDebug(this, lookup, Skin.LookupDebugType.Exit);
            }
        }

        /// <summary>
        /// Replace the sources used for lookups in this container.
        /// </summary>
        /// <remarks>
        /// This does not implicitly fire a <see cref="SourceChanged"/> event. Consider calling <see cref="TriggerSourceChanged"/> if required.
        /// </remarks>
        /// <param name="sources">The new sources.</param>
        protected void SetSources(IEnumerable<ISkin> sources)
        {
            lock (sourceSetLock)
            {
                foreach (var skin in skinSources)
                {
                    if (skin.skin is ISkinSource source)
                        source.SourceChanged -= TriggerSourceChanged;
                }

                skinSources = sources
                              // Shouldn't be required after NRT is applied to all calling sources.
                              .Where(skin => skin.IsNotNull())
                              .Select(skin => (skin, new DisableableSkinSource(skin, this))).ToArray();

                foreach (var skin in skinSources)
                {
                    if (skin.skin is ISkinSource source)
                        source.SourceChanged += TriggerSourceChanged;
                }
            }
        }

        /// <summary>
        /// Invoked after any consumed source change, before the external <see cref="SourceChanged"/> event is fired.
        /// This is also invoked once initially during <see cref="CreateChildDependencies"/> to ensure sources are ready for children consumption.
        /// </summary>
        protected virtual void RefreshSources() { }

        /// <summary>
        /// Provider trees cannot be changed by a post-commit source event. A subtype may opt in only by preparing an
        /// immutable source array and an infallible swap receipt for the complete subtree.
        /// </summary>
        private protected virtual System.Threading.Tasks.Task<SkinRevisionParticipantCommit?> PrepareCurrentRevisionAsync(
            SkinCurrentRevision nextRevision,
            System.Threading.CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult<SkinRevisionParticipantCommit?>(null);

        protected void TriggerSourceChanged()
        {
            // Ordinary provider trees are lifecycle holders for C2. They keep their exact source array and revision
            // lease until natural detach; late provider instances are constructed against the committed revision.
            if (skinManager?.IsCurrentRevisionPublicationBroadcast == true)
                return;

            bool refreshed = false;

            try
            {
                // Expose to implementations, giving them a chance to react before notifying external consumers.
                RefreshSources();
                refreshed = true;

                Delegate[] handlers = SourceChanged?.GetInvocationList() ?? Array.Empty<Delegate>();

                foreach (Delegate handler in handlers)
                {
                    try
                    {
                        ((Action)handler)();
                    }
                    catch
                    {
                        // The manager owns path-free diagnostics. One broken descendant must not prevent the remaining
                        // participant tree from observing an already committed revision.
                    }
                }
            }
            finally
            {
                // Acquire the committed revision before detaching the previous one, and only after this entire
                // synchronous subtree has refreshed.
                if (refreshed)
                    GameplayRevisionParticipant?.AdoptCurrentRevision();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            // Must be done before base.Dispose()
            SourceChanged = null;

            base.Dispose(isDisposing);

            if (ParentSource != null)
                ParentSource.SourceChanged -= TriggerSourceChanged;

            foreach (var i in skinSources)
            {
                if (i.skin is ISkinSource source)
                    source.SourceChanged -= TriggerSourceChanged;
            }

            GameplayRevisionParticipant?.Dispose();
            GameplayRevisionParticipant = null;
            GameplayPackageRevision = null;
        }

        private class DisableableSkinSource : ISkin
        {
            private readonly ISkin skin;
            private readonly SkinProvidingContainer provider;

            public DisableableSkinSource(ISkin skin, SkinProvidingContainer provider)
            {
                this.skin = skin;
                this.provider = provider;
            }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (provider.AllowDrawableLookup(lookup))
                    return skin.GetDrawableComponent(lookup);

                return null;
            }

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
            {
                if (provider.AllowTextureLookup(componentName))
                    return skin.GetTexture(componentName, wrapModeS, wrapModeT);

                return null;
            }

            public ISample? GetSample(ISampleInfo sampleInfo)
            {
                if (provider.AllowSampleLookup(sampleInfo))
                    return skin.GetSample(sampleInfo);

                return null;
            }

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
            {
                try
                {
                    Skin.LogLookupDebug(this, lookup, Skin.LookupDebugType.Enter);

                    switch (lookup)
                    {
                        case GlobalSkinColours:
                        case SkinComboColourLookup:
                        case SkinCustomColourLookup:
                            if (provider.AllowColourLookup)
                                return skin.GetConfig<TLookup, TValue>(lookup);

                            break;

                        default:
                            if (provider.AllowConfigurationLookup)
                                return skin.GetConfig<TLookup, TValue>(lookup);

                            break;
                    }

                    return null;
                }
                finally
                {
                    Skin.LogLookupDebug(this, lookup, Skin.LookupDebugType.Exit);
                }
            }

            public override string ToString() => $"{GetType().ReadableName()} {{ Skin: {skin} }}";
        }
    }
}
