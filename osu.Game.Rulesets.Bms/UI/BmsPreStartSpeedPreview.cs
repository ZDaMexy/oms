// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsPreStartSpeedPreview : CompositeDrawable
    {
        private const double minimum_travel_duration = 120;

        private readonly IBindable<BmsScrollSpeedMetrics> speedMetrics;
        private readonly StopwatchClock animationClock = new StopwatchClock();
        private readonly PreviewNote[] previewNotes;
        private readonly float noteHeight;

        private double activationStartTime;

        public int LaneIndex { get; }

        public bool IsPreviewVisible => IsPreviewActive && Alpha > 0;

        public bool IsPreviewActive { get; private set; }

        public bool IsPreviewPaused { get; private set; }

        public float PrimaryNoteProgress => previewNotes[0].Progress;

        public BmsPreStartSpeedPreview(BmsLaneLayout.Lane lane, BmsKeymode keymode, IBindable<BmsScrollSpeedMetrics> speedMetrics, float noteHeight)
        {
            LaneIndex = lane.LaneIndex;
            this.noteHeight = Math.Max(1, noteHeight);

            this.speedMetrics = speedMetrics.GetBoundCopy();

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            Clock = new FramedClock(animationClock);

            InternalChildren = previewNotes = new[]
            {
                new PreviewNote(0, lane, keymode),
                new PreviewNote(0.5, lane, keymode),
            };

            this.speedMetrics.BindValueChanged(_ => updatePreviewTransforms(), true);
        }

        protected override void Update()
        {
            base.Update();

            if (!IsPreviewActive)
                return;

            updatePreviewTransforms();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
        }

        public void UpdateState(bool active, bool paused)
        {
            bool wasActive = IsPreviewActive;

            IsPreviewActive = active;
            IsPreviewPaused = active && paused;

            if (!IsPreviewActive)
            {
                animationClock.Stop();
                Alpha = 0;
                return;
            }

            if (!wasActive)
                activationStartTime = animationClock.CurrentTime;

            if (IsPreviewPaused)
                animationClock.Stop();
            else
                animationClock.Start();

            Alpha = 1;
            updatePreviewTransforms();
        }

        private void updatePreviewTransforms()
        {
            double travelDuration = Math.Max(minimum_travel_duration, speedMetrics.Value.VisibleLaneTime);
            double elapsed = Math.Max(0, animationClock.CurrentTime - activationStartTime);
            float availableHeight = DrawHeight;

            foreach (var note in previewNotes)
            {
                double cycleProgress = (elapsed / travelDuration + note.PhaseOffset) % 1;
                note.ApplyProgress((float)cycleProgress, noteHeight, availableHeight);
            }
        }

        private sealed partial class PreviewNote : CompositeDrawable
        {
            public double PhaseOffset { get; }

            public float Progress { get; private set; }

            public PreviewNote(double phaseOffset, BmsLaneLayout.Lane lane, BmsKeymode keymode)
            {
                PhaseOffset = phaseOffset;

                RelativeSizeAxes = Axes.X;
                Width = 1;
                Masking = true;

                InternalChild = new SkinnableDrawable(new BmsNoteSkinLookup(BmsNoteSkinElements.Note, lane.LaneIndex, lane.IsScratch, keymode))
                {
                    RelativeSizeAxes = Axes.Both,
                    CentreComponent = false,
                };
            }

            public void ApplyProgress(float progress, float noteHeight, float availableHeight)
            {
                Progress = progress;
                Height = noteHeight;

                float clampedHeight = Math.Max(noteHeight, 1);
                float endY = Math.Max(availableHeight - clampedHeight, 0);
                Y = -clampedHeight + (endY + clampedHeight) * progress;
            }
        }
    }
}
