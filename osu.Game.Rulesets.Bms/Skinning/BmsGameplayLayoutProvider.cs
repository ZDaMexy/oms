// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Gameplay-root owner of the exact BMS package/layout pair.
    /// </summary>
    /// <remarks>
    /// The parser-owned <see cref="BmsBeatmap.BmsInfo"/> is the only keymode source. A provider never scans hit objects,
    /// layout width or channels. Publication delegates preparation/commit and package lifetime checks to the shared C2
    /// owner, then exposes that exact committed neutral reference through one BMS adapter.
    /// </remarks>
    [Cached]
    public sealed class BmsGameplayLayoutProvider
    {
        private readonly BmsBeatmap beatmap;

        public BmsGameplayLayoutSnapshot Current
            => RevisionOwner?.CurrentPublication?.GetAdapter<BmsGameplayLayoutSnapshot>()
               ?? throw new InvalidOperationException("bms.layout.missing-exact-publication");

        public BmsKeymode Keymode => beatmap.BmsInfo.Keymode;

        internal GameplaySkinLayoutRevisionOwner? RevisionOwner { get; private set; }

        internal bool? LastPrepareWasUpdateThread => RevisionOwner?.LastPrepareWasUpdateThread;

        internal bool? LastCommitWasUpdateThread => RevisionOwner?.LastCommitWasUpdateThread;

        public BmsGameplayLayoutProvider(BmsBeatmap beatmap)
        {
            this.beatmap = beatmap ?? throw new ArgumentNullException(nameof(beatmap));
        }

        internal static BmsGameplayLayoutSnapshot ResolveOwnerPublication(
            GameplaySkinLayoutRevisionOwner? owner,
            BmsGameplayLayoutProvider? explicitCompatibilityProvider,
            string missingDiagnostic)
        {
            if (owner != null)
            {
                return owner.CurrentPublication?.GetAdapter<BmsGameplayLayoutSnapshot>()
                       ?? throw new InvalidOperationException(missingDiagnostic);
            }

            // Isolated component tests must opt into a provider whose owner is explicitly marked Compatibility.
            // An exact production provider is never accepted as a substitute for the enclosing owner publication.
            if (explicitCompatibilityProvider?.RevisionOwner?.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility)
                return explicitCompatibilityProvider.Current;

            throw new InvalidOperationException(missingDiagnostic);
        }

        internal void AttachRevisionOwner(GameplaySkinLayoutRevisionOwner owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (ReferenceEquals(RevisionOwner, owner))
                return;

            if (RevisionOwner != null)
                throw new InvalidOperationException("A BMS gameplay layout provider cannot change its exact revision owner.");

            RevisionOwner = owner;
        }

        internal void AttachCommittedPublication(GameplaySkinLayoutRevisionOwner owner)
        {
            if (owner.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility)
                throw new InvalidOperationException("Production BMS gameplay cannot attach a compatibility layout publication.");

            AttachRevisionOwner(owner);

            BmsGameplayLayoutSnapshot snapshot = Current;

            if (!ReferenceEquals(snapshot.Neutral, owner.Current))
                throw new InvalidOperationException("The BMS typed adapter does not retain the exact owner publication.");
        }

        internal BmsGameplayLayoutSnapshot PublishForTesting(
            BmsPlayfieldStyle style,
            BmsGameplayLayoutConfiguration configuration,
            BmsGameplayLayoutEnvironment? environmentOverride = null)
        {
            RevisionOwner ??= GameplaySkinLayoutRevisionOwner.CreateCompatibility();

            if (RevisionOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                throw new InvalidOperationException("Testing publication is restricted to a compatibility owner.");

            return tryPrepareAndCommit(
                       RevisionOwner,
                       beatmap,
                       style,
                       _ => configuration,
                       () => environmentOverride ?? BmsGameplayLayoutEnvironment.Default)
                   ?? throw new InvalidOperationException("The compatibility BMS gameplay layout publication lost its commit admission.");
        }

        public BmsGameplayLayoutLane GetLaneForObject(BmsHitObject hitObject)
        {
            ArgumentNullException.ThrowIfNull(hitObject);

            // LaneIndex has already been transformed exactly once by the BMS mod applicator. It is the final object
            // target and must agree with keysound, input action and skin lookup identity in this same snapshot.
            return Current.GetLaneByLogicalIndex(hitObject.LaneIndex);
        }

        internal static bool TryPrepareExact(
            GameplaySkinLayoutRevisionOwner owner,
            BmsBeatmap beatmap,
            BmsPlayfieldStyle style,
            ISkin skin,
            GameHost? host,
            ISafeArea? safeArea,
            [NotNullWhen(true)] out BmsGameplayLayoutSnapshot? snapshot)
        {
            snapshot = tryPrepareAndCommit(
                owner,
                beatmap,
                style,
                keymode => BmsGameplayLayoutConfiguration.FromSkin(skin, keymode),
                () => CreateProductionEnvironment(host, safeArea));
            return snapshot != null;
        }

        private static BmsGameplayLayoutSnapshot? tryPrepareAndCommit(
            GameplaySkinLayoutRevisionOwner owner,
            BmsBeatmap beatmap,
            BmsPlayfieldStyle style,
            Func<BmsKeymode, BmsGameplayLayoutConfiguration> configurationFactory,
            Func<BmsGameplayLayoutEnvironment> environmentFactory)
        {
            BmsKeymode keymode = beatmap.BmsInfo.Keymode;

            BmsGameplayLayoutSnapshot? candidate = null;
            GameplaySkinPreparedLayout prepared;

            try
            {
                prepared = owner.PreparePublication(layoutRevision =>
                {
                    // Both package geometry reads and topology/geometry solving happen after the owner has acquired the
                    // fresh prepare lease. Nothing outside this callback can observe or cache a partial candidate.
                    BmsGameplayLayoutConfiguration configuration = configurationFactory(keymode);
                    BmsGameplayLayoutEnvironment environment = environmentFactory();
                    var topologyOwner = new BmsGameplaySkinLaneTopologyRevisionOwner();
                    // Topology is parser keymode + presentation order only. Geometry is solved exactly once below; no
                    // provisional profile/lane geometry is allowed on this production prepare path.
                    BmsGameplaySkinLaneTopologyPublication topology = topologyOwner.Publish(keymode, style);

                    candidate = BmsGameplayLayoutSolver.Solve(
                        beatmap.BmsInfo.KeymodeResolution,
                        style,
                        configuration,
                        environment,
                        owner.PackageRevision,
                        topology,
                        layoutRevision);
                    return GameplaySkinLayoutPublication.Create(candidate);
                });
            }
            catch (GameplaySkinLayoutParticipantBarrierChangedException)
            {
                return null;
            }

            if (!owner.TryCommit(prepared))
                return null;

            if (candidate == null
                || !ReferenceEquals(candidate, owner.CurrentPublication?.GetAdapter<BmsGameplayLayoutSnapshot>())
                || !ReferenceEquals(candidate.Neutral, owner.Current))
                throw new InvalidOperationException("The exact BMS gameplay layout publication lost its C2 commit admission.");

            return candidate;
        }

        internal static BmsGameplayLayoutEnvironment CreateProductionEnvironment(GameHost? host, ISafeArea? safeArea)
        {
            var diagnostics = new List<GameplaySkinLayoutDiagnostic>();
            var window = host?.Window;

            if (window == null || window.Size.Width <= 0 || window.Size.Height <= 0)
            {
                diagnostics.Add(new GameplaySkinLayoutDiagnostic("bms.layout.environment-window-fallback"));
                return new BmsGameplayLayoutEnvironment(
                    GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                    GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                    16f / 9f,
                    1,
                    diagnostics: diagnostics);
            }

            float width = window.Size.Width;
            float height = window.Size.Height;
            float left = 0;
            float top = 0;
            float right = 0;
            float bottom = 0;

            if (safeArea != null)
            {
                var padding = safeArea.SafeAreaPadding.Value;
                left = Math.Clamp(padding.Left / width, 0, 0.45f);
                right = Math.Clamp(padding.Right / width, 0, 0.45f);
                top = Math.Clamp(padding.Top / height, 0, 0.45f);
                bottom = Math.Clamp(padding.Bottom / height, 0, 0.45f);
            }
            else
                diagnostics.Add(new GameplaySkinLayoutDiagnostic("bms.layout.environment-safe-area-fallback"));

            float dpiScale = window.Scale;

            if (!float.IsFinite(dpiScale) || dpiScale <= 0)
            {
                dpiScale = 1;
                diagnostics.Add(new GameplaySkinLayoutDiagnostic("bms.layout.environment-dpi-fallback"));
            }

            return new BmsGameplayLayoutEnvironment(
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(left, top, 1 - left - right, 1 - top - bottom),
                width / height,
                dpiScale,
                diagnostics: diagnostics);
        }
    }
}
