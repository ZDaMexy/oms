// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A ruleset-native, immutable projection of the one neutral gameplay layout snapshot.
    /// </summary>
    /// <remarks>
    /// Implementations may retain renderer-facing metrics which were produced by the same solver, but must not own a
    /// second current revision or independently recompute geometry.
    /// </remarks>
    public interface IGameplaySkinLayoutAdapter
    {
        GameplaySkinLayoutSnapshot Snapshot { get; }
    }

    /// <summary>
    /// The indivisible publication unit committed by one gameplay layout owner.
    /// </summary>
    /// <remarks>
    /// Keeping the neutral snapshot, its ruleset-native adapter and its fully resolved material set in this one immutable
    /// object prevents a consumer from observing any mixture of package, layout or material revisions.
    /// </remarks>
    public sealed class GameplaySkinLayoutPublication : IDisposable
    {
        private PublicationRetirement? retirement;

        public GameplaySkinLayoutSnapshot Snapshot { get; }

        public IGameplaySkinLayoutAdapter Adapter { get; }

        public GameplaySkinResolvedMaterialSet MaterialSet { get; }

        /// <summary>
        /// The exact immutable C5 scene graph/resources prepared with this package/layout/material publication.
        /// </summary>
        public GameplaySkinPreparedScene PreparedScene { get; }

        /// <summary>
        /// Exact initial revision vector used by the read-only event stream for this committed root.
        /// </summary>
        public GameplaySkinEventRevision EventRevision => GameplaySkinEventRevision.Create(
            Snapshot.Context.PackageRevision.Generation,
            Snapshot.Context.LayoutRevision,
            MaterialSet.LayoutRevision,
            PreparedScene.SceneRevision);

        private GameplaySkinLayoutPublication(
            GameplaySkinLayoutSnapshot snapshot,
            IGameplaySkinLayoutAdapter adapter,
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinPreparedScene? preparedScene = null,
            IDisposable? retirement = null)
        {
            IDisposable? sceneRetirement = null;

            try
            {
                sceneRetirement = preparedScene?.TakeRetirement();
                this.retirement = sceneRetirement == null && retirement == null
                    ? null
                    : new PublicationRetirement(sceneRetirement, retirement);

                // The aggregate owns both handles only after its construction and field assignment completed. Keep
                // the locals live until then so even an allocation failure while creating the aggregate cannot strand
                // a prepared scene texture or an exact-package borrow outside every owner.
                if (this.retirement != null)
                {
                    sceneRetirement = null;
                    retirement = null;
                }

                Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
                Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
                MaterialSet = materialSet ?? throw new ArgumentNullException(nameof(materialSet));
                PreparedScene = preparedScene ?? GameplaySkinPreparedScene.CreateEmpty(snapshot, materialSet);

                if (!ReferenceEquals(adapter.Snapshot, snapshot))
                    throw new ArgumentException("A gameplay layout adapter must retain the exact neutral snapshot being published.", nameof(adapter));

                if (!ReferenceEquals(materialSet.Snapshot, snapshot)
                    || !ReferenceEquals(materialSet.PackageRevision, snapshot.Context.PackageRevision)
                    || materialSet.LayoutRevision != snapshot.Context.LayoutRevision)
                {
                    throw new ArgumentException(
                        "A resolved gameplay skin material set must retain the exact package and layout snapshot being published.",
                        nameof(materialSet));
                }

                bool supportedContract = materialSet.ContractIdentity.IsCurrentFor(materialSet.RuntimeSupportProfile)
                                         && ReferenceEquals(
                                             materialSet.RuntimeSupportProfile,
                                             GameplaySkinRuntimeSupportProfile.ForRuleset(snapshot.Context.RulesetId))
                                         || snapshot.Context.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility
                                         && materialSet.IsEmpty
                                         && ReferenceEquals(materialSet.RuntimeSupportProfile, GameplaySkinRuntimeSupportProfile.CompatibilityEmpty)
                                         && materialSet.ContractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty);

                if (!supportedContract)
                {
                    throw new ArgumentException(
                        "An exact gameplay layout publication must carry the current catalog/codec/resolver/runtime-support contract and exact ruleset profile; only a detached compatibility package may carry the empty compatibility contract.",
                        nameof(materialSet));
                }

                if (!ReferenceEquals(PreparedScene.Snapshot, snapshot)
                    || !ReferenceEquals(PreparedScene.PackageRevision, snapshot.Context.PackageRevision)
                    || !ReferenceEquals(PreparedScene.MaterialSet, materialSet)
                    || PreparedScene.LayoutRevision != snapshot.Context.LayoutRevision
                    || PreparedScene.MaterialRevision != materialSet.LayoutRevision
                    || PreparedScene.SceneRevision != snapshot.Context.LayoutRevision)
                {
                    throw new ArgumentException(
                        "A prepared gameplay skin scene must retain the exact package/layout/material publication.",
                        nameof(preparedScene));
                }
            }
            catch
            {
                if (this.retirement != null)
                    DisposeRetirement();
                else
                {
                    // Aggregate construction itself may fail before the field can take ownership. Retire both
                    // provisional handles independently and exactly once in that case.
                    try
                    {
                        retirement?.Dispose();
                    }
                    finally
                    {
                        sceneRetirement?.Dispose();
                    }
                }

                throw;
            }
        }

        public static GameplaySkinLayoutPublication Create<TAdapter>(TAdapter adapter)
            where TAdapter : class, IGameplaySkinLayoutAdapter
        {
            ArgumentNullException.ThrowIfNull(adapter);
            return new GameplaySkinLayoutPublication(
                adapter.Snapshot,
                adapter,
                GameplaySkinResolvedMaterialSet.CreateEmpty(adapter.Snapshot));
        }

        public static GameplaySkinLayoutPublication Create<TAdapter>(
            TAdapter adapter,
            GameplaySkinResolvedMaterialSet materialSet)
            where TAdapter : class, IGameplaySkinLayoutAdapter
        {
            ArgumentNullException.ThrowIfNull(adapter);
            ArgumentNullException.ThrowIfNull(materialSet);
            return new GameplaySkinLayoutPublication(adapter.Snapshot, adapter, materialSet);
        }

        /// <summary>
        /// Creates the indivisible C5 package/layout/material/scene publication.
        /// </summary>
        public static GameplaySkinLayoutPublication Create<TAdapter>(
            TAdapter adapter,
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinPreparedScene preparedScene)
            where TAdapter : class, IGameplaySkinLayoutAdapter
        {
            ArgumentNullException.ThrowIfNull(adapter);
            ArgumentNullException.ThrowIfNull(materialSet);
            ArgumentNullException.ThrowIfNull(preparedScene);
            return new GameplaySkinLayoutPublication(adapter.Snapshot, adapter, materialSet, preparedScene);
        }

        /// <summary>
        /// Creates a publication carrying resources which must remain alive for exactly the committed root lifetime.
        /// </summary>
        /// <remarks>
        /// Ownership is transferred to the returned publication immediately. A failed publication validation, aborted
        /// prepared carrier or rejected commit disposes <paramref name="retirement"/>; a successful commit transfers it
        /// to the exact <see cref="GameplaySkinLayoutRevisionOwner"/> until that root is disposed.
        /// </remarks>
        public static GameplaySkinLayoutPublication Create<TAdapter>(
            TAdapter adapter,
            GameplaySkinResolvedMaterialSet materialSet,
            IDisposable retirement)
            where TAdapter : class, IGameplaySkinLayoutAdapter
        {
            ArgumentNullException.ThrowIfNull(adapter);
            ArgumentNullException.ThrowIfNull(materialSet);
            ArgumentNullException.ThrowIfNull(retirement);
            return new GameplaySkinLayoutPublication(adapter.Snapshot, adapter, materialSet, retirement: retirement);
        }

        /// <summary>
        /// Creates the indivisible C5 publication and transfers its aggregate provisional retirement.
        /// </summary>
        public static GameplaySkinLayoutPublication Create<TAdapter>(
            TAdapter adapter,
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinPreparedScene preparedScene,
            IDisposable retirement)
            where TAdapter : class, IGameplaySkinLayoutAdapter
        {
            ArgumentNullException.ThrowIfNull(adapter);
            ArgumentNullException.ThrowIfNull(materialSet);
            ArgumentNullException.ThrowIfNull(preparedScene);
            ArgumentNullException.ThrowIfNull(retirement);
            return new GameplaySkinLayoutPublication(adapter.Snapshot, adapter, materialSet, preparedScene, retirement);
        }

        public TAdapter GetAdapter<TAdapter>()
            where TAdapter : class, IGameplaySkinLayoutAdapter
            => Adapter as TAdapter
               ?? throw new InvalidOperationException($"The committed gameplay layout adapter is not {typeof(TAdapter).Name}.");

        internal static GameplaySkinLayoutPublication CreateNeutral(GameplaySkinLayoutSnapshot snapshot)
            => new GameplaySkinLayoutPublication(
                snapshot,
                new NeutralAdapter(snapshot),
                GameplaySkinResolvedMaterialSet.CreateEmpty(snapshot));

        internal IDisposable? TakeRetirement() => Interlocked.Exchange(ref retirement, null);

        internal void DisposeRetirement() => Interlocked.Exchange(ref retirement, null)?.Dispose();

        /// <summary>
        /// Retires an owned provisional publication which has not been admitted to a prepared carrier.
        /// </summary>
        /// <remarks>
        /// Once a <see cref="GameplaySkinPreparedLayout"/> has claimed this publication, disposal is a no-op because
        /// the carrier or committed root owns the retirement handle instead.
        /// </remarks>
        public void Dispose() => DisposeRetirement();

        private sealed class PublicationRetirement : IDisposable
        {
            private IDisposable[]? targets;

            public PublicationRetirement(params IDisposable?[] targets)
            {
                this.targets = targets.Where(target => target != null).Cast<IDisposable>().ToArray();
            }

            public void Dispose()
            {
                IDisposable[]? current = Interlocked.Exchange(ref targets, null);

                if (current == null)
                    return;

                Exception? firstFailure = null;

                for (int i = current.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        current[i].Dispose();
                    }
                    catch (Exception exception)
                    {
                        firstFailure ??= exception;
                    }
                }

                if (firstFailure != null)
                    ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
        }

        private sealed class NeutralAdapter : IGameplaySkinLayoutAdapter
        {
            public GameplaySkinLayoutSnapshot Snapshot { get; }

            public NeutralAdapter(GameplaySkinLayoutSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }
    }
}
