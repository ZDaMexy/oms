// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsPreStartSpeedPreview : CompositeDrawable
    {
        private const double minimum_travel_duration = 120;

        private readonly IBindable<BmsScrollSpeedMetrics> speedMetrics;
        private readonly StopwatchClock animationClock = new StopwatchClock();
        private readonly PreviewNote[] previewNotes;
        private readonly float noteHeight;
        private readonly BmsHitObjectArea? authoredSourceArea;

        private double activationStartTime;

        public int LaneIndex { get; }

        public bool IsPreviewVisible => IsPreviewActive && Alpha > 0;

        public bool IsPreviewActive { get; private set; }

        public bool IsPreviewPaused { get; private set; }

        public float PrimaryNoteProgress => previewNotes[0].Progress;

        internal float PrimaryNoteScreenSpaceHeight => previewNotes[0].ScreenSpaceDrawQuad.Height;

        public BmsGameplayLayoutSnapshot LayoutSnapshot { get; }

        public GameplaySkinResolvedMaterialSet? ResolvedMaterialSet { get; }

        public BmsPreStartSpeedPreview(
            BmsGameplayLayoutLane lane,
            BmsKeymode keymode,
            IBindable<BmsScrollSpeedMetrics> speedMetrics,
            BmsGameplayLayoutSnapshot layoutSnapshot,
            GameplaySkinResolvedMaterialSet? materialSet = null,
            BmsHitObjectArea? authoredSourceArea = null)
        {
            LaneIndex = lane.LogicalIndex;
            LayoutSnapshot = layoutSnapshot ?? throw new ArgumentNullException(nameof(layoutSnapshot));

            if (materialSet != null && !ReferenceEquals(materialSet.Snapshot, LayoutSnapshot.Neutral))
                throw new ArgumentException("A BMS pre-start material set must retain its exact layout.", nameof(materialSet));

            ResolvedMaterialSet = materialSet;
            this.authoredSourceArea = authoredSourceArea;

            noteHeight = LayoutSnapshot.ProjectVerticalProfileMetric(LayoutSnapshot.Profile.HitTargetHeight);

            this.speedMetrics = speedMetrics.GetBoundCopy();

            RelativeSizeAxes = Axes.Both;
            if (authoredSourceArea != null)
            {
                RelativePositionAxes = Axes.Both;
                Masking = true;
            }
            Alpha = 0;
            Clock = new FramedClock(animationClock);

            InternalChildren = previewNotes = new[]
            {
                new PreviewNote(0, lane, keymode, LayoutSnapshot, materialSet) { Height = noteHeight },
                new PreviewNote(0.5, lane, keymode, LayoutSnapshot, materialSet) { Height = noteHeight },
            };

            // A hidden authored preview may not update. A native mode change must still restore its two owners.
            this.speedMetrics.BindValueChanged(_ => updateVisibility());
        }

        protected override void Update()
        {
            base.Update();

            if (authoredSourceArea != null)
            {
                GameplaySkinLayoutRect bounds = LayoutSnapshot.PreStartPreviewRect;
                float offset = authoredSourceArea.AppliedOffsetRatio;
                Position = new osuTK.Vector2(bounds.X, bounds.Y + (authoredSourceArea.AppliedReverse ? bounds.Height * offset : 0));
                Size = new osuTK.Vector2(bounds.Width, bounds.Height * (1 - offset));
            }

            if (!IsPreviewActive)
                return;

            // An authored Normal preview is a read-only presentation of this same native driver.
            updateVisibility();

            updatePreviewTransforms();
        }

        public void UpdateState(bool active, bool paused)
        {
            bool wasActive = IsPreviewActive;

            IsPreviewActive = active;
            IsPreviewPaused = active && paused;

            if (!IsPreviewActive)
            {
                animationClock.Stop();
                updateVisibility();
                return;
            }

            if (!wasActive)
                activationStartTime = animationClock.CurrentTime;

            if (IsPreviewPaused)
                animationClock.Stop();
            else
                animationClock.Start();

            updateVisibility();
        }

        private void updateVisibility()
        {
            bool visible = IsPreviewActive && (authoredSourceArea == null || speedMetrics.Value.HiSpeedMode == BmsHiSpeedMode.Normal);
            Alpha = visible ? 1 : 0;

            foreach (PreviewNote note in previewNotes)
                note.SetUsageActive(visible);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                UpdateState(false, false);
                speedMetrics.UnbindAll();
            }

            base.Dispose(isDisposing);
        }

        private void updatePreviewTransforms()
        {
            double travelDuration = Math.Max(minimum_travel_duration, speedMetrics.Value.VisibleLaneTime);
            double elapsed = Math.Max(0, animationClock.CurrentTime - activationStartTime);
            // During parent layout and full Lift there may be no drawable interval yet.
            float availableHeight = Math.Max(0, DrawHeight);

            foreach (var note in previewNotes)
            {
                double cycleProgress = (elapsed / travelDuration + note.PhaseOffset) % 1;
                note.ApplyProgress((float)cycleProgress, noteHeight, availableHeight);
            }
        }

        private sealed partial class PreviewNote : CompositeDrawable
        {
            private readonly BmsAsyncNoteDrawable note;
            private bool usageActive;

            public double PhaseOffset { get; }

            public float Progress { get; private set; }

            public PreviewNote(
                double phaseOffset,
                BmsGameplayLayoutLane lane,
                BmsKeymode keymode,
                BmsGameplayLayoutSnapshot layoutSnapshot,
                GameplaySkinResolvedMaterialSet? materialSet)
            {
                PhaseOffset = phaseOffset;

                RelativeSizeAxes = Axes.Both;
                Width = 1;
                Masking = true;

                InternalChild = note = new BmsAsyncNoteDrawable(new BmsNoteSkinLookup(
                    BmsNoteSkinElements.Note,
                    lane.LogicalIndex,
                    lane.IsScratch,
                    keymode,
                    lane.LaneId,
                    layoutSnapshot,
                    materialSet))
                {
                    RelativeSizeAxes = Axes.Both,
                };
            }

            public void SetUsageActive(bool active)
            {
                if (usageActive == active)
                    return;

                usageActive = active;
                // Preview notes have no scored hitobject identity. Retain activation before asynchronous loading,
                // and only apply/free at visibility edges so repeated frames do not reset authored visual state.
                note.SetPooledUsageActive(active);
            }

            public void ApplyProgress(float progress, float noteHeight, float availableHeight)
            {
                Progress = progress;
                float drawHeight = Math.Clamp(noteHeight * availableHeight, 0, availableHeight);
                float endY = Math.Max(availableHeight - drawHeight, 0);
                Y = -drawHeight + (endY + drawHeight) * progress;
            }
        }
    }
}
