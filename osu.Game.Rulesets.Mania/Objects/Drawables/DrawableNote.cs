// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// Visualises a <see cref="Note"/> hit object.
    /// </summary>
    public partial class DrawableNote : DrawableManiaHitObject<Note>, IKeyBindingHandler<ManiaAction>
    {
        [Resolved]
        private OsuColour colours { get; set; }

        [Resolved(canBeNull: true)]
        private IBeatmap beatmap { get; set; }

        // Optional shared keysound store, present only when a converted chart (e.g. BMS) hosts one (cached under the
        // IManiaKeysoundStore interface so this drawable needs no reference to the source ruleset assembly). A converted
        // KEY note (IHasManiaKeysound) routes its keysound through it, which is what lets it stay a fully pooled
        // DrawableNote instead of a per-note non-pooled drawable on dense charts (J6 / P1-J #10).
        [Resolved(canBeNull: true)]
        private IManiaKeysoundStore keysoundStore { get; set; }

        private readonly Bindable<bool> configTimingBasedNoteColouring = new Bindable<bool>();

        protected virtual ManiaSkinComponents Component => ManiaSkinComponents.Note;

        private Drawable headPiece;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; private set; }

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey { get; private set; }

        public DrawableNote()
            : this(null)
        {
        }

        public DrawableNote(Note hitObject)
            : base(hitObject)
        {
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ManiaRulesetConfigManager rulesetConfig, ManiaGameplaySkinMaterialContext materialContext)
        {
            rulesetConfig?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouring, configTimingBasedNoteColouring);

            ManiaSkinComponentLookup lookup;

            if (materialContext?.UsesResolvedMaterial == true)
            {
                lookup = new ManiaSkinComponentLookup(Component, materialContext);
                ResolvedMaterialSet = materialContext.MaterialSet;
                ResolvedMaterialKey = materialContext.GetKey(Component);
            }
            else
                lookup = new ManiaSkinComponentLookup(Component);

            AddInternal(headPiece = new SkinnableDrawable(lookup, _ => new DefaultNotePiece())
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            configTimingBasedNoteColouring.BindValueChanged(_ => updateSnapColour());
            StartTimeBindable.BindValueChanged(_ => updateSnapColour(), true);
        }

        protected override void OnApply()
        {
            base.OnApply();
            updateSnapColour();
        }

        protected override void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            base.OnDirectionChanged(e);

            headPiece.Anchor = headPiece.Origin = e.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            Debug.Assert(HitObject.HitWindows != null);

            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);
            ApplyResult(result);
        }

        /// <summary>
        /// Some objects in mania may want to limit the max result.
        /// </summary>
        protected virtual HitResult GetCappedResult(HitResult result) => result;

        public override void PlaySamples()
        {
            // A converted chart (e.g. BMS) may host a shared keysound store; a converted KEY note then routes its
            // keysound through it (bounded pool, per-WAV cut, pause/seek aware) instead of mania's per-object one-shot.
            // This is what keeps the converted note a fully pooled DrawableNote rather than a per-note non-pooled
            // drawable on dense charts (J6 / P1-J #10). Normal mania notes — and any context without a hosted store —
            // fall through to the base one-shot path unchanged.
            if (keysoundStore != null && HitObject is IHasManiaKeysound keysound && keysound.KeysoundSample != null)
            {
                keysoundStore.Play(keysound.KeysoundSample, CalculateSamplePlaybackBalance(SamplePlaybackPosition), keysound.KeysoundCutGroup);
                return;
            }

            base.PlaySamples();
        }

        public virtual bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            if (CheckHittable?.Invoke(this, Time.Current) == false)
                return false;

            return UpdateResult(true);
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }

        private void updateSnapColour()
        {
            if (beatmap == null || HitObject == null) return;

            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(HitObject.StartTime);

            Colour = configTimingBasedNoteColouring.Value ? BindableBeatDivisor.GetColourFor(snapDivisor, colours) : Color4.White;
        }
    }
}
