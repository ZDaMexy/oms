// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
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

        // F2: true when any hold note on this lane is in Holding state; drives the hold light display.
        public readonly BindableBool AnyHolding = new BindableBool();

        public BmsLaneLayout.Lane LayoutLane { get; private set; }

        public BmsHitTarget HitTarget { get; }

        public Container PreviewContainer => hitObjectArea.PreviewContainer;

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

        private IReadOnlyList<BmsLaneKeysoundEntry> keysoundTimeline = Array.Empty<BmsLaneKeysoundEntry>();

        /// <summary>
        /// Supplies the time-ordered keysound assignments for this lane so empty (note-less) key presses can play
        /// the keysound currently armed on the lane (built at conversion time by BmsBeatmap.GetLaneKeysoundTimeline).
        /// </summary>
        internal void SetKeysoundTimeline(IReadOnlyList<BmsLaneKeysoundEntry>? timeline)
            => keysoundTimeline = timeline ?? Array.Empty<BmsLaneKeysoundEntry>();

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
                new SkinnableDrawable(createLookup(BmsLaneSkinElements.KeyFlash))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                },
                new SkinnableDrawable(createLookup(BmsLaneSkinElements.HitLighting))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                },
                new SkinnableDrawable(createLookup(BmsLaneSkinElements.HoldLight))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                },
                new SkinnableDrawable(createLookup(BmsLaneSkinElements.MineHit))
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

        protected override void Update()
        {
            base.Update();

            if (inputManager == null)
                return;

            bool isPressed = inputManager.KeyBindingContainer.PressedActions.Contains(Action.Value);

            if (HitTarget.IsPressed.Value != isPressed)
                HitTarget.IsPressed.Value = isPressed;
        }

        public void TriggerHitLighting()
        {
            foreach (var child in InternalChildren)
            {
                if (child is SkinnableDrawable sd && sd.Drawable is DefaultBmsHitLightingDisplay hl)
                {
                    hl.Flash();
                    return;
                }
            }
        }

        public void TriggerMineHit()
        {
            foreach (var child in InternalChildren)
            {
                if (child is SkinnableDrawable sd && sd.Drawable is DefaultBmsMineHitDisplay mh)
                {
                    mh.Flash();
                    return;
                }
            }
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            base.OnNewDrawableHitObject(drawableHitObject);

            if (drawableHitObject is not DrawableBmsHitObject bmsHitObject)
                return;

            bmsHitObject.CheckHittable = hitPolicy.IsHittable;
            bmsHitObject.OnUserPressedSuccessfully = hitPolicy.HandleHit;

            if (drawableHitObject is DrawableBmsHoldNote holdNote)
                trackHoldState(holdNote);
        }

        private int activeHoldCount;

        private void trackHoldState(DrawableBmsHoldNote holdNote)
        {
            holdNote.BodyState.BindValueChanged(e =>
            {
                if (e.NewValue == BmsLongNoteBodyState.Holding)
                {
                    if (activeHoldCount++ == 0)
                        AnyHolding.Value = true;
                }
                else if (e.OldValue == BmsLongNoteBodyState.Holding)
                {
                    if (--activeHoldCount <= 0)
                    {
                        activeHoldCount = 0;
                        AnyHolding.Value = false;
                    }
                }
            });
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

        private void playCurrentLaneKeysound()
        {
            if (keysoundStore == null || keysoundTimeline.Count == 0)
                return;

            // Autoplay / auto-scratch / auto-note: the notes in this lane are auto-played and already sound their own
            // keysound through the note auto-apply path (DrawableBmsHitObject.PlaySamples). The autoplay replay still
            // synthesises a key press per note, and because an auto note does not accept input the press falls through
            // to this lane handler. Sounding the lane's armed keysound on top would play every note TWICE — diverging
            // from an actual 100%-perfect play, where the note consumes the press so the lane never sounds. Leave the
            // keysound to the notes when this lane is auto-driven; genuine empty presses only happen on a player lane
            // (a hit note there consumes the press before it can reach here).
            if (laneHasAutoPlayNote())
                return;

            var entry = resolveArmedKeysound(Time.Current);

            if (entry != null)
                keysoundStore.Play(entry.Value.Sample, 0, entry.Value.KeysoundId);
        }

        // True when this lane currently hosts an auto-played note (autoplay mod, or auto-scratch / auto-note). Such a
        // lane is driven entirely by the replay: every press lines up with an auto note that sounds itself, so the
        // lane's own armed-keysound playback would only duplicate it. Player lanes never match (their notes accept
        // input and consume the press on a hit), so the empty-press keysound there is unaffected.
        private bool laneHasAutoPlayNote()
        {
            foreach (var aliveObject in HitObjectContainer.AliveObjects)
            {
                if (aliveObject is DrawableBmsHitObject bmsObject && !bmsObject.AcceptsPlayerInput)
                    return true;
            }

            return false;
        }

        private BmsLaneKeysoundEntry? resolveArmedKeysound(double time)
        {
            // Binary search for the most recent assignment at-or-before `time`. Before the first entry, fall back to
            // the first so the lane is never silent and the opening press previews the first keysound.
            int low = 0;
            int high = keysoundTimeline.Count - 1;
            int resolved = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);

                if (keysoundTimeline[mid].Time <= time)
                {
                    resolved = mid;
                    low = mid + 1;
                }
                else
                    high = mid - 1;
            }

            return keysoundTimeline[resolved < 0 ? 0 : resolved];
        }

        private bool shouldTriggerEmptyPoor()
        {
            double currentTime = Time.Current;
            bool foundCandidate = false;
            bool supportsExcessivePoor = false;
            bool canTriggerSupportedEmptyPoor = false;
            bool hasFutureUnjudgedCandidate = false;

            void inspectCandidate(DrawableBmsHitObject hitObject)
            {
                if (!hitObject.AcceptsPlayerInput)
                    return;

                foundCandidate = true;

                if (hitObject.HitObject.HitWindows is BmsTimingWindows timingWindows && timingWindows.SupportsExcessivePoor)
                {
                    supportsExcessivePoor = true;
                    canTriggerSupportedEmptyPoor |= timingWindows.CanTriggerExcessivePoor(currentTime - hitObject.HitObject.StartTime);
                    return;
                }

                hasFutureUnjudgedCandidate |= !hitObject.Judged && hitObject.HitObject.StartTime > currentTime;
            }

            foreach (var hitObject in HitObjectContainer.AliveObjects.OfType<DrawableBmsHitObject>())
            {
                inspectCandidate(hitObject);

                if (supportsExcessivePoor && canTriggerSupportedEmptyPoor)
                    return true;
            }

            foreach (var hitObject in HitObjectContainer.Objects.OfType<DrawableBmsHitObject>())
            {
                inspectCandidate(hitObject);

                if (supportsExcessivePoor && canTriggerSupportedEmptyPoor)
                    return true;
            }

            if (!foundCandidate)
                return false;

            return supportsExcessivePoor ? canTriggerSupportedEmptyPoor : hasFutureUnjudgedCandidate;
        }

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
