// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class DrawableManiaRuleset : DrawableScrollingRuleset<ManiaHitObject>
    {
        private const string bms_to_mania_drawable_factory_type = "osu.Game.Rulesets.Bms.Beatmaps.BmsToManiaDrawableRepresentationFactory";
        private const string bms_ruleset_assembly = "osu.Game.Rulesets.Bms";

        /// <summary>
        /// The minimum time range. This occurs at a <see cref="ManiaRulesetSetting.ScrollSpeed"/> of 40.
        /// </summary>
        public const double MIN_TIME_RANGE = 290;

        /// <summary>
        /// The maximum time range. This occurs with a <see cref="ManiaRulesetSetting.ScrollSpeed"/> of 1.
        /// </summary>
        public const double MAX_TIME_RANGE = 11485;

        public new ManiaPlayfield Playfield => (ManiaPlayfield)base.Playfield;

        public new ManiaBeatmap Beatmap => (ManiaBeatmap)base.Beatmap;

        public IEnumerable<BarLine> BarLines;

        public override bool RequiresPortraitOrientation => Beatmap.Stages.Count == 1 && mobileLayout.Value == ManiaMobileLayout.Portrait;

        protected override bool RelativeScaleBeatLengths => true;

        protected new ManiaRulesetConfigManager Config => (ManiaRulesetConfigManager)base.Config;

        private readonly BindableDouble configScrollSpeed = new BindableDouble();
        private readonly Bindable<ManiaMobileLayout> mobileLayout = new Bindable<ManiaMobileLayout>();
        private readonly Bindable<bool> touchOverlay = new Bindable<bool>();

        public double TargetTimeRange { get; protected set; }

        private double currentTimeRange;

        private static readonly Type? bms_drawable_factory_type = Type.GetType($"{bms_to_mania_drawable_factory_type}, {bms_ruleset_assembly}", throwOnError: false);
        private static readonly Func<ManiaHitObject, bool>? bms_can_create_drawable = createFactoryDelegate<Func<ManiaHitObject, bool>>("CanCreate");
        private static readonly Func<ManiaHitObject, DrawableHitObject<ManiaHitObject>?>? bms_create_drawable = createFactoryDelegate<Func<ManiaHitObject, DrawableHitObject<ManiaHitObject>?>>("Create");

        private static TDelegate? createFactoryDelegate<TDelegate>(string methodName) where TDelegate : Delegate
        {
            MethodInfo? method = bms_drawable_factory_type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

            return method == null ? null : (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
        }

        private const string bms_to_mania_keysound_store_factory_type = "osu.Game.Rulesets.Bms.Beatmaps.BmsToManiaKeysoundStoreFactory";

        private static readonly Type? bms_keysound_store_factory_type = Type.GetType($"{bms_to_mania_keysound_store_factory_type}, {bms_ruleset_assembly}", throwOnError: false);
        private static readonly Func<IBeatmap, bool>? bms_should_host_keysound_store = createKeysoundStoreFactoryDelegate<Func<IBeatmap, bool>>("ShouldHost");
        private static readonly Func<IRulesetConfigCache, Drawable>? bms_create_keysound_store = createKeysoundStoreFactoryDelegate<Func<IRulesetConfigCache, Drawable>>("Create");

        private static TDelegate? createKeysoundStoreFactoryDelegate<TDelegate>(string methodName) where TDelegate : Delegate
        {
            MethodInfo? method = bms_keysound_store_factory_type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

            return method == null ? null : (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
        }

        // Shared BMS keysound store hosted for converted-BMS mania playback (J6). Created + cached here so the converted
        // sample-only drawables (BGM / scratch) can resolve it, and added to the tree in load() so it resolves the
        // gameplay clock for pause / seek handling. Only created when the beatmap actually carries converted-BMS
        // keysounds, so normal mania play is unaffected; the BMS assembly being absent is a clean no-op.
        private Drawable? sharedKeysoundStore;

        private GameplaySkinLayoutPublication layoutPublication = null!;

        internal GameplaySkinLayoutRevisionOwner LayoutRevisionOwner { get; private set; } = null!;

        internal ManiaGameplaySkinLayout LayoutAdapter => layoutPublication.GetAdapter<ManiaGameplaySkinLayout>();

        /// <summary>
        /// The exact immutable layout snapshot shared by the complete mania gameplay tree.
        /// </summary>
        public GameplaySkinLayoutSnapshot LayoutSnapshot => layoutPublication.Snapshot;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => layoutPublication.MaterialSet;

        public ScrollingDirection PublishedDirection => Direction.Value;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            IReadOnlyDependencyContainer dependencies = base.CreateChildDependencies(parent);
            var wrapped = new DependencyContainer(dependencies);

            if (!dependencies.TryGet(out GameplaySkinLayoutRevisionOwner exactOwner))
            {
                throw new InvalidOperationException(
                    "A mania gameplay root requires an exact provider owner or an explicitly cached compatibility owner.");
            }

            LayoutRevisionOwner = exactOwner;
            layoutPublication = LayoutRevisionOwner.CurrentPublication!;

            if (layoutPublication == null)
            {
                if (LayoutRevisionOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                    throw new InvalidOperationException("An exact mania gameplay root must complete background layout preparation before child loading.");

                // Explicit isolation-only compatibility path for visual/unit hosts which do not mount a managed
                // RulesetSkinProvidingContainer. Exact production roots are prepared by the stateless ruleset hook.
                GameplaySkinScrollDirection layoutDirection = Config.Get<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection) == ManiaScrollingDirection.Up
                    ? GameplaySkinScrollDirection.Up
                    : GameplaySkinScrollDirection.Down;
                layoutPublication = ManiaGameplaySkinLayout.PrepareAndPublish(
                    Beatmap,
                    dependencies.Get<ISkinSource>(),
                    LayoutRevisionOwner,
                    dependencies.Get<GameHost>(),
                    layoutDirection);
            }

            ManiaGameplaySkinLayout adapter = layoutPublication.GetAdapter<ManiaGameplaySkinLayout>();
            string expectedNativeContext = $"stages-{string.Join("-", Beatmap.Stages.Select(stage => stage.Columns))}";

            if (!ReferenceEquals(layoutPublication, LayoutRevisionOwner.CurrentPublication)
                || !ReferenceEquals(adapter.Snapshot, layoutPublication.Snapshot)
                || !ReferenceEquals(layoutPublication.MaterialSet.Snapshot, layoutPublication.Snapshot)
                || !ReferenceEquals(layoutPublication.MaterialSet.PackageRevision, LayoutRevisionOwner.PackageRevision)
                || !ReferenceEquals(layoutPublication.Snapshot.Context.PackageRevision, LayoutRevisionOwner.PackageRevision)
                || layoutPublication.Snapshot.Context.RulesetId != "mania"
                || layoutPublication.Snapshot.Context.NativeContextId != expectedNativeContext)
            {
                throw new InvalidOperationException("The mania gameplay layout does not retain this root's exact package revision.");
            }

            wrapped.Cache(adapter);
            wrapped.Cache(layoutPublication.Snapshot);
            wrapped.Cache(layoutPublication.MaterialSet);
            wrapped.Cache(LayoutRevisionOwner);

            if (bms_should_host_keysound_store?.Invoke(Beatmap) == true && bms_create_keysound_store?.Invoke(dependencies.Get<IRulesetConfigCache>()) is Drawable store)
            {
                sharedKeysoundStore = store;

                // Cache under the store's runtime type (BmsKeysoundStore) so the BMS-assembly sample-only drawables
                // (BGM / scratch) resolve it; mania cannot name that type at compile time.
                wrapped.Cache(store);

                // Also cache under the mania-owned IManiaKeysoundStore interface so a pooled DrawableNote (a converted
                // KEY note) can route its keysound through the store without referencing the BMS assembly. This is what
                // lets converted KEY notes stay pooled instead of each becoming a non-pooled drawable (J6 / P1-J #10).
                if (store is IManiaKeysoundStore keysoundStore)
                    wrapped.CacheAs(keysoundStore);
            }

            return wrapped;
        }

        // Stores the current speed adjustment active in gameplay.
        private readonly Track speedAdjustmentTrack = new TrackVirtual(0);

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        public DrawableManiaRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            BarLines = new BarLineGenerator<BarLine>(Beatmap).BarLines;

            TimeRange.MinValue = 1;
            TimeRange.MaxValue = MAX_TIME_RANGE;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            foreach (var mod in Mods.OfType<IApplicableToTrack>())
                mod.ApplyToTrack(speedAdjustmentTrack);

            bool isForCurrentRuleset = Beatmap.BeatmapInfo.Ruleset.Equals(Ruleset.RulesetInfo);

            foreach (var p in ControlPoints)
            {
                // Mania doesn't care about global velocity
                p.Velocity = 1;
                p.BaseBeatLength *= Beatmap.Difficulty.SliderMultiplier;

                // For non-mania beatmap, speed changes should only happen through timing points
                if (!isForCurrentRuleset)
                    p.EffectPoint = new EffectControlPoint();
            }

            BarLines.ForEach(Playfield.Add);

            // Geometry and scrolling direction form one immutable publication. A configuration change is therefore
            // deliberately applied only to the next gameplay root rather than splitting this root across revisions.
            Direction.Value = LayoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? ScrollingDirection.Up
                : ScrollingDirection.Down;

            Config.BindWith(ManiaRulesetSetting.ScrollSpeed, configScrollSpeed);
            configScrollSpeed.BindValueChanged(speed =>
            {
                if (!AllowScrollSpeedAdjustment)
                    return;

                TargetTimeRange = ComputeScrollTime(speed.NewValue);
            });

            TimeRange.Value = TargetTimeRange = currentTimeRange = ComputeScrollTime(configScrollSpeed.Value);

            Config.BindWith(ManiaRulesetSetting.MobileLayout, mobileLayout);
            mobileLayout.BindValueChanged(_ => updateMobileLayout(), true);

            Config.BindWith(ManiaRulesetSetting.TouchOverlay, touchOverlay);
            touchOverlay.BindValueChanged(_ => updateMobileLayout(), true);

            // Host the shared converted-BMS keysound store under the gameplay clock so it resolves pause / seek.
            if (sharedKeysoundStore != null)
                AddInternal(sharedKeysoundStore);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            prewarmConvertedKeysounds();
        }

        // Mirrors BMS-native DrawableBmsRuleset.PrewarmKeysounds (J6 / P1-J): every converted keysound will sound during
        // the play, so preload them all (decoding into the shared sample store / BASS) at load instead of paying the
        // first-play decode cost mid-gameplay. Runs for PLAYER MODE too, not just autoplay: a converted chart's full
        // keysound set (~hundreds of distinct WAVs) otherwise cold-decodes DURING gameplay, and the resulting transient
        // large buffers / promotion bursts were measured triggering blocking gen2 full GCs (~220ms freezes) in the first
        // ~30s of play (P1-J, 2026-06-11 probe). Preloading everything before gameplay is also what LR2/beatoraja do.
        // Two sources are warmed: (1) the standard Samples / NodeSamples (a converted KEY note carries its keysound
        // there for the store-absent fallback, and a hold node carries the head keysound); (2) the
        // IHasManiaKeysound.KeysoundSample of every converted object — required because BGM / scratch deliberately
        // carry EMPTY Samples (so a key press's GameplaySampleTriggerSource can't fire them; P1-J #11), so their
        // keysound is reachable only via KeysoundSample. Warming the underlying decode also warms the store playback
        // path, since the store and the playfield sample pool share the same decoded sample-store cache. Gated to
        // converted-BMS charts (store hosted), so normal mania play is unaffected.
        private void prewarmConvertedKeysounds()
        {
            if (sharedKeysoundStore == null)
                return;

            foreach (var hitObject in Beatmap.HitObjects)
            {
                foreach (var sample in hitObject.Samples)
                    Playfield.PrepareSamplePool(sample);

                if (hitObject is HoldNote hold && hold.NodeSamples != null)
                {
                    foreach (var nodeSamples in hold.NodeSamples)
                        foreach (var sample in nodeSamples)
                            Playfield.PrepareSamplePool(sample);
                }

                // BGM / scratch carry their keysound only here (empty Samples; see above); KEY notes expose theirs too.
                // Warm it so the store's first play of each unique keysound doesn't hit a cold decode mid-gameplay.
                if (hitObject is IHasManiaKeysound keysound && keysound.KeysoundSample != null)
                    Playfield.PrepareSamplePool(keysound.KeysoundSample);
            }
        }

        private ManiaTouchInputArea? touchInputArea;

        private void updateMobileLayout()
        {
            if (touchOverlay.Value)
                KeyBindingInputManager.Add(touchInputArea = new ManiaTouchInputArea(this));
            else
            {
                if (touchInputArea != null)
                    KeyBindingInputManager.Remove(touchInputArea, true);

                touchInputArea = null;
            }
        }

        protected override void AdjustScrollSpeed(int amount) => configScrollSpeed.Value += amount;

        protected override void Update()
        {
            base.Update();
            updateTimeRange();
        }

        private void updateTimeRange()
        {
            GameplaySkinLayoutSurface playfieldSurface = LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.PLAYFIELD_SURFACE);
            GameplaySkinLayoutSurface hitTargetSurface = LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.HIT_TARGET_SURFACE);
            float resolvedScrollLength = LayoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Down
                ? hitTargetSurface.Rect.Top - playfieldSurface.Rect.Top
                : playfieldSurface.Rect.Bottom - hitTargetSurface.Rect.Bottom;
            float defaultScrollLength = 1 - LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION / 768f;

            // This scaling factor preserves the scroll speed as the scroll length varies from changes to the hit position.
            float scale = resolvedScrollLength / defaultScrollLength;

            // we're intentionally using the game host's update clock here to decouple the time range tween from the gameplay clock (which can be arbitrarily paused, or even rewinding)
            currentTimeRange = Interpolation.DampContinuously(currentTimeRange, TargetTimeRange, 50, gameHost.UpdateThread.Clock.ElapsedFrameTime);
            TimeRange.Value = currentTimeRange * speedAdjustmentTrack.AggregateTempo.Value * speedAdjustmentTrack.AggregateFrequency.Value * scale;
        }

        /// <summary>
        /// Computes a scroll time (in milliseconds) from a scroll speed in the range of 1-40.
        /// </summary>
        /// <param name="scrollSpeed">The scroll speed.</param>
        /// <returns>The scroll time.</returns>
        public static double ComputeScrollTime(double scrollSpeed) => MAX_TIME_RANGE / scrollSpeed;

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new ManiaPlayfieldAdjustmentContainer();

        protected override Playfield CreatePlayfield() => new ManiaPlayfield(Beatmap.Stages);

        public override int Variant => (int)(Beatmap.Stages.Count == 1 ? PlayfieldType.Single : PlayfieldType.Dual) + Beatmap.TotalColumns;

        protected override PassThroughInputManager CreateInputManager() => new ManiaInputManager(Ruleset.RulesetInfo, Variant);

        public override DrawableHitObject<ManiaHitObject>? CreateDrawableRepresentation(ManiaHitObject h)
        {
            if (h.GetType().Assembly.GetName().Name == bms_ruleset_assembly && tryCreateBmsDrawableRepresentation(h, out var drawableRepresentation))
                return drawableRepresentation;

            return null;
        }

        private static bool tryCreateBmsDrawableRepresentation(ManiaHitObject hitObject, out DrawableHitObject<ManiaHitObject>? drawableRepresentation)
        {
            drawableRepresentation = null;

            if (bms_can_create_drawable?.Invoke(hitObject) is not true)
                return false;

            if (bms_create_drawable?.Invoke(hitObject) is not DrawableHitObject<ManiaHitObject> createdDrawableRepresentation)
                return false;

            drawableRepresentation = createdDrawableRepresentation;
            return true;
        }

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new ManiaFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new ManiaReplayRecorder(score);

        protected override ResumeOverlay CreateResumeOverlay() => new DelayedResumeOverlay();

    }
}
