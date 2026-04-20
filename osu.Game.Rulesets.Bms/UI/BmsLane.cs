// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsLane : ScrollingPlayfield, IKeyBindingHandler<BmsAction>
    {
        internal readonly Bindable<BmsAction> Action = new Bindable<BmsAction>();

        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsLaneLayout.Lane LayoutLane { get; private set; }

        public BmsHitTarget HitTarget { get; }

        public IBindable<double> ScrollLengthRatio => hitObjectArea.ScrollLengthRatio;

        protected BmsPlayfieldLayoutProfile LayoutProfile { get; private set; }

        private readonly BmsOrderedHitPolicy hitPolicy;
        private readonly int laneCount;
        private readonly BmsKeymode keymode;
        private readonly BmsHitObjectArea hitObjectArea;
        private readonly BindableFloat? liftUnits;

        [Resolved(canBeNull: true)]
        private BmsInputManager? inputManager { get; set; }

        [Resolved(CanBeNull = true)]
        private BmsKeysoundStore? keysoundStore { get; set; }

        private BmsKeysoundSampleInfo? currentLaneKeysound;

        public BmsLane(BmsLaneLayout.Lane lane, int laneCount, BmsKeymode keymode, BmsPlayfieldLayoutProfile layoutProfile, BindableFloat? liftUnits = null)
        {
            LayoutLane = lane;
            LaneIndex = lane.LaneIndex;
            IsScratch = lane.IsScratch;
            this.laneCount = laneCount;
            this.keymode = keymode;
            this.liftUnits = liftUnits;
            LayoutProfile = layoutProfile;
            Name = $"Lane {LaneIndex}";
            Action.Value = lane.Action;
            hitPolicy = new BmsOrderedHitPolicy(HitObjectContainer);

            RelativeSizeAxes = Axes.Both;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                new SkinnableDrawable(createLookup(BmsLaneSkinElements.Background))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                },
                new SkinnableDrawable(createLookup(BmsLaneSkinElements.Divider))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                },
                hitObjectArea = createHitObjectArea(),
            };

            HitTarget = hitObjectArea.HitTarget;
        }

        protected BmsLaneSkinLookup createLookup(BmsLaneSkinElements element, bool isMajorBarLine = true)
            => new BmsLaneSkinLookup(element, LaneIndex, laneCount, IsScratch, keymode, isMajorBarLine);

        protected virtual BmsHitTarget createHitTarget() => new BmsHitTarget(createLookup(BmsLaneSkinElements.HitTarget), LayoutProfile);

        protected virtual BmsHitObjectArea createHitObjectArea()
            => new BmsHitObjectArea(createHitTarget(), LayoutProfile, HitObjectContainer, liftUnits)
            {
                RelativeSizeAxes = Axes.Both,
            };

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBindable<BmsAction>>(Action);
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            NewResult += onNewResult;
        }

        protected override void Update()
        {
            base.Update();

            if (inputManager == null)
                return;

            bool isPressed = inputManager.KeyBindingContainer.PressedActions.Contains(Action.Value);

            if (HitTarget.IsPressed.Value != isPressed)
                HitTarget.IsPressed.Value = isPressed;
        }

        protected override void Dispose(bool isDisposing)
        {
            NewResult -= onNewResult;
            base.Dispose(isDisposing);
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            base.OnNewDrawableHitObject(drawableHitObject);

            if (drawableHitObject is not DrawableBmsHitObject bmsHitObject)
                return;

            bmsHitObject.CheckHittable = hitPolicy.IsHittable;
            bmsHitObject.OnUserPressedSuccessfully = hitPolicy.HandleHit;
        }

        private void onNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            updateLaneKeysound(judgedObject);

            if (!result.IsHit || judgedObject is not DrawableBmsHitObject bmsHitObject || !bmsHitObject.AcceptsPlayerInput)
                return;

            hitPolicy.HandleHit(bmsHitObject);
        }

        public virtual bool OnPressed(KeyBindingPressEvent<BmsAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            playCurrentLaneKeysound();

            if (!shouldTriggerEmptyPoor())
                return false;

            triggerEmptyPoor();
            return true;
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<BmsAction> e)
        {
        }

        public void ApplyLayoutProfile(BmsLaneLayout.Lane lane, BmsPlayfieldLayoutProfile layoutProfile)
        {
            LayoutLane = lane;
            LayoutProfile = layoutProfile;
            hitObjectArea.ApplyLayoutProfile(layoutProfile);

            foreach (var barLine in AllHitObjects.OfType<DrawableBmsBarLine>())
                barLine.ApplyLayoutProfile(layoutProfile);
        }

        private void triggerEmptyPoor()
        {
            var drawable = new DrawableBmsEmptyPoorHitObject(new BmsEmptyPoorHitObject
            {
                StartTime = Time.Current,
            })
            {
                Clock = Clock,
            };

            Add(drawable);
            drawable.ApplyEmptyPoor();
        }

        private void updateLaneKeysound(DrawableHitObject judgedObject)
        {
            BmsKeysoundSampleInfo? sample = judgedObject.HitObject switch
            {
                BmsHoldNote holdNote => holdNote.HeadKeysoundSample,
                BmsHitObject hitObject => hitObject.KeysoundSample,
                _ => null,
            };

            if (sample != null)
                currentLaneKeysound = sample;
        }

        private void playCurrentLaneKeysound()
        {
            if (keysoundStore == null || currentLaneKeysound == null)
                return;

            keysoundStore.Play(new ISampleInfo[] { currentLaneKeysound }, 0);
        }

        private bool shouldTriggerEmptyPoor()
        {
            double currentTime = Time.Current;
            DrawableBmsHitObject[] candidates = getEmptyPoorCandidates().ToArray();

            if (candidates.Length == 0)
                return false;

            if (candidates.Select(hitObject => hitObject.HitObject.HitWindows).OfType<BmsTimingWindows>().Any(timingWindows => timingWindows.SupportsExcessivePoor))
                return candidates.Any(hitObject => canTriggerExcessivePoor(hitObject, currentTime));

            return candidates.Any(hitObject => !hitObject.Judged && hitObject.HitObject.StartTime > currentTime);
        }

        private IEnumerable<DrawableBmsHitObject> getEmptyPoorCandidates()
        {
            var seen = new HashSet<DrawableBmsHitObject>();

            foreach (var hitObject in HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>())
            {
                if (hitObject.AcceptsPlayerInput && seen.Add(hitObject))
                    yield return hitObject;
            }

            foreach (var hitObject in HitObjectContainer.Objects.OfType<DrawableBmsHitObject>())
            {
                if (hitObject.AcceptsPlayerInput && seen.Add(hitObject))
                    yield return hitObject;
            }
        }

        private static bool canTriggerExcessivePoor(DrawableBmsHitObject hitObject, double currentTime)
            => hitObject.HitObject.HitWindows is BmsTimingWindows timingWindows
               && timingWindows.CanTriggerExcessivePoor(currentTime - hitObject.HitObject.StartTime);

        private sealed partial class DrawableBmsEmptyPoorHitObject : DrawableHitObject<BmsEmptyPoorHitObject>
        {
            public override bool DisplayResult => true;

            protected override double InitialLifetimeOffset => 0;

            public DrawableBmsEmptyPoorHitObject(BmsEmptyPoorHitObject hitObject)
                : base(hitObject)
            {
                Alpha = 0;
                HandleUserInput = false;
            }

            public void ApplyEmptyPoor() => ApplyResult(HitResult.Ok);

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
            }

            protected override void UpdateHitStateTransforms(ArmedState state)
            {
                base.UpdateHitStateTransforms(state);

                if (state != ArmedState.Idle)
                    this.Expire();
            }
        }
    }
}
