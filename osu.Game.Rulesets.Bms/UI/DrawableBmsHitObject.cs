// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsHitObject : DrawableHitObject<HitObject>, IKeyBindingHandler<BmsAction>
    {
        public override bool DisplayResult => AcceptsPlayerInput;

        protected override double InitialLifetimeOffset => 2000;

        protected readonly Bindable<BmsAction> Action = new Bindable<BmsAction>();

        public Func<DrawableHitObject, double, bool>? CheckHittable;
        public Action<DrawableBmsHitObject>? OnUserPressedSuccessfully;

        internal bool AcceptsPlayerInput => SupportsPlayerInput(HitObject);

        [Resolved(CanBeNull = true)]
        private BmsKeysoundStore? keysoundStore { get; set; }

        private readonly Drawable? mainVisual;
        private Container? nestedHitObjectContainer;
        private bool autoAssistVisualsApplied;
        private bool autoAssistVisible = true;
        private bool autoAssistTintEnabled;
        private Color4 autoAssistTintColour = Color4.White;

        public override IEnumerable<HitSampleInfo> GetSamples()
        {
            IEnumerable<HitSampleInfo> samples = base.GetSamples();

            foreach (var keysoundSample in getKeysoundSamples())
                samples = samples.Append(keysoundSample);

            return samples;
        }

        public DrawableBmsHitObject(HitObject hitObject)
            : base(hitObject)
        {
            HandleUserInput = SupportsPlayerInput(hitObject);

            if (hitObject is BmsHitObject bmsHitObject)
                Action.Value = BmsActionExtensions.GetLaneAction(bmsHitObject.LaneIndex, bmsHitObject.IsScratch);

            if (hitObject is BmsBgmEvent)
            {
                Alpha = 0;
                return;
            }

            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Width = 1;
            Height = hitObject is BmsHoldNote ? 28 : 18;

            AddInternal(mainVisual = createMainVisual(hitObject));

            AddInternal(nestedHitObjectContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
            });
        }

        [BackgroundDependencyLoader(true)]
        private void load(IBindable<BmsAction>? action)
        {
            if (action is Bindable<BmsAction> bindable)
                Action.BindTo(bindable);
        }

        protected override void OnApply()
        {
            base.OnApply();
            HandleUserInput = SupportsPlayerInput(HitObject);
        }

        protected override void LoadSamples()
        {
            var samples = (keysoundStore == null ? GetSamples() : base.GetSamples()).Cast<ISampleInfo>().ToArray();

            if (samples.Length == 0)
                return;

            Samples.Samples = samples;
        }

        protected override void Update()
        {
            base.Update();

            if (autoAssistVisualsApplied)
                applyAutoAssistVisualState();
        }

        public override void PlaySamples()
        {
            if (keysoundStore != null)
            {
                var keysoundSamples = getKeysoundSamples().Cast<ISampleInfo>().ToArray();

                if (keysoundSamples.Length > 0)
                    keysoundStore.Play(keysoundSamples, CalculateSamplePlaybackBalance(SamplePlaybackPosition));
            }

            base.PlaySamples();
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (AcceptsPlayerInput)
            {
                if (userTriggered)
                {
                    var result = ResultForPlayerInput(HitObject, timeOffset);

                    if (result != HitResult.None)
                    {
                        ApplyResult(result);

                        if (result.IsHit())
                            OnUserPressedSuccessfully?.Invoke(this);
                    }

                    return;
                }

                if (!CanStillBeHitByPlayer(HitObject, timeOffset))
                    ApplyMinResult();

                return;
            }

            if (timeOffset < 0)
                return;

            if (ShouldAutoApplyMaxResult(HitObject))
                ApplyMaxResult();
            else
                ApplyMinResult();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            base.UpdateHitStateTransforms(state);

            switch (state)
            {
                case ArmedState.Hit:
                case ArmedState.Miss:
                    this.FadeOut(120).Expire();
                    break;
            }
        }

        private static Drawable createMainVisual(HitObject hitObject)
        {
            if (createLookup(hitObject) is BmsNoteSkinLookup lookup)
            {
                return new SkinnableDrawable(lookup)
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                };
            }

            return new Box
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        private static BmsNoteSkinLookup? createLookup(HitObject hitObject)
            => hitObject switch
            {
                BmsHoldNote holdNote => new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, holdNote.LaneIndex, holdNote.IsScratch, holdNote.Keymode),
                BmsHoldNoteHead head => new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, head.LaneIndex, head.IsScratch, head.Keymode),
                BmsHoldNoteTailEvent tail => new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, tail.LaneIndex, tail.IsScratch, tail.Keymode),
                BmsHitObject note => new BmsNoteSkinLookup(BmsNoteSkinElements.Note, note.LaneIndex, note.IsScratch, note.Keymode),
                _ => null,
            };

        internal static bool ShouldAutoApplyMaxResult(HitObject hitObject) => hitObject switch
        {
            BmsHoldNoteTailEvent => false,
            BmsHitObject { AutoPlay: true } => true,
            BmsBgmEvent => true,
            _ => false,
        };

        internal static bool SupportsPlayerInput(HitObject hitObject)
            => hitObject is BmsHitObject { AutoPlay: false } && hitObject is not BmsHoldNoteTailEvent;

        internal static HitResult ResultForPlayerInput(HitObject hitObject, double timeOffset)
        {
            if (!SupportsPlayerInput(hitObject) || hitObject.HitWindows == null)
                return HitResult.None;

            if (hitObject.HitWindows is BmsTimingWindows bmsTimingWindows)
                return bmsTimingWindows.Evaluate(timeOffset);

            return hitObject.HitWindows.ResultFor(timeOffset);
        }

        internal static bool CanStillBeHitByPlayer(HitObject hitObject, double timeOffset)
        {
            if (!SupportsPlayerInput(hitObject) || hitObject.HitWindows == null)
                return false;

            if (hitObject.HitWindows is BmsTimingWindows bmsTimingWindows)
                return timeOffset <= bmsTimingWindows.PoorWindow;

            return hitObject.HitWindows.CanBeHit(timeOffset);
        }

        public virtual bool OnPressed(KeyBindingPressEvent<BmsAction> e)
        {
            if (!AcceptsPlayerInput || e.Action != Action.Value)
                return false;

            if (CheckHittable?.Invoke(this, Time.Current) == false)
                return false;

            return UpdateResult(true);
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<BmsAction> e)
        {
        }

        internal virtual void HitForcefully()
        {
            if (!Judged)
                ApplyMaxResult();
        }

        internal virtual void MissForcefully()
        {
            if (!Judged)
                ApplyMinResult();
        }

        internal void ApplyAutoScratchVisuals(bool visible, bool tintEnabled, Color4 tintColour)
            => applyAutoAssistVisuals(appliesToScratch: true, visible, tintEnabled, tintColour);

        internal void ApplyAutoNoteVisuals(bool visible, bool tintEnabled, Color4 tintColour)
            => applyAutoAssistVisuals(appliesToScratch: false, visible, tintEnabled, tintColour);

        private void applyAutoAssistVisuals(bool appliesToScratch, bool visible, bool tintEnabled, Color4 tintColour)
        {
            if (mainVisual == null)
                return;

            if (HitObject is not BmsHitObject { AutoPlay: true, CountsForScore: false } bmsHitObject || bmsHitObject.IsScratch != appliesToScratch)
                return;

            autoAssistVisualsApplied = true;
            autoAssistVisible = visible;
            autoAssistTintEnabled = tintEnabled;
            autoAssistTintColour = tintColour;

            applyAutoAssistVisualState();
        }

        private void applyAutoAssistVisualState()
        {
            if (mainVisual == null)
                return;

            float alpha = autoAssistVisible ? 1 : 0;
            Color4 colour = autoAssistTintEnabled ? autoAssistTintColour : Color4.White;

            if (mainVisual is SkinnableDrawable skinnableDrawable && skinnableDrawable.IsLoaded)
            {
                skinnableDrawable.Alpha = 1;
                skinnableDrawable.Colour = Color4.White;
                skinnableDrawable.Drawable.Alpha = alpha;
                skinnableDrawable.Drawable.Colour = colour;
                return;
            }

            mainVisual.Alpha = alpha;
            mainVisual.Colour = colour;
        }

        private IEnumerable<BmsKeysoundSampleInfo> getKeysoundSamples()
        {
            switch (HitObject)
            {
                case BmsHoldNote { HeadKeysoundSample: not null } holdNote:
                    yield return holdNote.HeadKeysoundSample;
                    break;

                case BmsHitObject { KeysoundSample: not null } bmsHitObject:
                    yield return bmsHitObject.KeysoundSample;
                    break;

                case BmsBgmEvent { KeysoundSample: not null } bgmEvent:
                    yield return bgmEvent.KeysoundSample;
                    break;
            }
        }

        protected override void AddNestedHitObject(DrawableHitObject hitObject)
        {
            base.AddNestedHitObject(hitObject);
            nestedHitObjectContainer?.Add(hitObject);
        }

        protected override void ClearNestedHitObjects()
        {
            base.ClearNestedHitObjects();
            nestedHitObjectContainer?.Clear(false);
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
        {
            if (hitObject is BmsHoldNoteTailEvent tailEvent)
                return new DrawableBmsHitObject(tailEvent);

            return base.CreateNestedHitObject(hitObject);
        }
    }
}
