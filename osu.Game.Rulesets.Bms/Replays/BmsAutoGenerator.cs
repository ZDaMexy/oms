// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Bms.Replays
{
    internal class BmsAutoGenerator : AutoGenerator<BmsReplayFrame>
    {
        public const double RELEASE_DELAY = 20;

        public new BmsBeatmap Beatmap => (BmsBeatmap)base.Beatmap;

        private readonly List<BmsHitObject> playableObjects;

        public BmsAutoGenerator(BmsBeatmap beatmap)
            : base(beatmap)
        {
            playableObjects = beatmap.HitObjects.OfType<BmsHitObject>().ToList();
        }

        protected override void GenerateFrames()
        {
            if (playableObjects.Count == 0)
                return;

            var pointGroups = generateActionPoints().GroupBy(point => point.Time).OrderBy(group => group.First().Time);
            var actions = new List<BmsAction>();

            foreach (var group in pointGroups)
            {
                foreach (var point in group)
                {
                    switch (point)
                    {
                        case HitPoint:
                            actions.Add(point.Action);
                            break;

                        case ReleasePoint:
                            actions.Remove(point.Action);
                            break;
                    }
                }

                Frames.Add(new BmsReplayFrame(group.First().Time, actions.ToArray()));
            }
        }

        private IEnumerable<IActionPoint> generateActionPoints()
        {
            for (int i = 0; i < playableObjects.Count; i++)
            {
                var currentObject = playableObjects[i];
                var action = BmsActionExtensions.GetLaneAction(currentObject.LaneIndex, currentObject.IsScratch);
                var nextObject = GetNextObject(i) as BmsHitObject;
                double releaseTime = calculateReleaseTime(currentObject, nextObject);

                yield return new HitPoint { Time = currentObject.StartTime, Action = action };
                yield return new ReleasePoint { Time = releaseTime, Action = action };
            }
        }

        private static double calculateReleaseTime(BmsHitObject currentObject, BmsHitObject? nextObject)
        {
            double endTime = currentObject.GetEndTime();
            double releaseDelay = RELEASE_DELAY;

            if (currentObject is BmsHoldNote holdNote)
            {
                if (holdNote.Duration > 0)
                    return endTime;

                releaseDelay = 1;
            }

            bool canDelayKeyUpFully = nextObject == null || nextObject.StartTime > endTime + releaseDelay;

            return endTime + (canDelayKeyUpFully ? releaseDelay : (nextObject.AsNonNull().StartTime - endTime) * 0.9);
        }

        protected override HitObject? GetNextObject(int currentIndex)
        {
            var currentObject = playableObjects[currentIndex];

            for (int i = currentIndex + 1; i < playableObjects.Count; i++)
            {
                var nextObject = playableObjects[i];

                if (nextObject.LaneIndex == currentObject.LaneIndex && nextObject.IsScratch == currentObject.IsScratch)
                    return nextObject;
            }

            return null;
        }

        private interface IActionPoint
        {
            double Time { get; set; }

            BmsAction Action { get; set; }
        }

        private struct HitPoint : IActionPoint
        {
            public double Time { get; set; }

            public BmsAction Action { get; set; }
        }

        private struct ReleasePoint : IActionPoint
        {
            public double Time { get; set; }

            public BmsAction Action { get; set; }
        }
    }
}
