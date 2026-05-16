// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Bms.UI
{
    internal class BmsOrderedHitPolicy
    {
        private readonly HitObjectContainer hitObjectContainer;

        public BmsOrderedHitPolicy(HitObjectContainer hitObjectContainer)
        {
            this.hitObjectContainer = hitObjectContainer;
        }

        public bool IsHittable(DrawableHitObject hitObject, double time)
        {
            var bmsHitObject = (DrawableBmsHitObject)hitObject;
            var nextObject = getParticipatingHitObjects().GetNext(bmsHitObject);

            return nextObject == null || time < nextObject.HitObject.StartTime;
        }

        public void HandleHit(DrawableBmsHitObject hitObject)
        {
            foreach (var candidate in getParticipatingHitObjects())
            {
                if (candidate.HitObject.StartTime >= hitObject.HitObject.StartTime)
                    break;

                if (candidate.Judged)
                    continue;

                candidate.MissForcefully();
            }
        }

        private IEnumerable<DrawableBmsHitObject> getParticipatingHitObjects()
        {
            using (var aliveEnumerator = hitObjectContainer.AliveObjects.GetEnumerator())
            {
                while (aliveEnumerator.MoveNext())
                {
                    if (aliveEnumerator.Current is not DrawableBmsHitObject hitObject || !canParticipateInLocking(hitObject))
                        continue;

                    yield return hitObject;

                    while (aliveEnumerator.MoveNext())
                    {
                        if (aliveEnumerator.Current is DrawableBmsHitObject remainingHitObject && canParticipateInLocking(remainingHitObject))
                            yield return remainingHitObject;
                    }

                    yield break;
                }
            }

            foreach (var drawable in hitObjectContainer.Objects)
            {
                if (drawable is DrawableBmsHitObject hitObject && canParticipateInLocking(hitObject))
                    yield return hitObject;
            }
        }

        private static bool canParticipateInLocking(DrawableBmsHitObject hitObject) => hitObject.AcceptsPlayerInput;
    }
}
