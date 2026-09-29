// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
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
            var objects = getParticipatingHitObjects();
            bool found = false;

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] is not DrawableBmsHitObject candidate || !canParticipateInLocking(candidate))
                    continue;

                if (found)
                    return time < candidate.HitObject.StartTime;

                found = ReferenceEquals(candidate, hitObject);
            }

            return true;
        }

        public void HandleHit(DrawableBmsHitObject hitObject)
        {
            var objects = getParticipatingHitObjects();

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] is not DrawableBmsHitObject candidate || !canParticipateInLocking(candidate))
                    continue;

                if (candidate.HitObject.StartTime >= hitObject.HitObject.StartTime)
                    break;

                if (candidate.Judged)
                    continue;

                candidate.MissForcefully();
            }
        }

        private IReadOnlyList<DrawableHitObject> getParticipatingHitObjects()
        {
            var aliveObjects = hitObjectContainer.OrderedAliveObjects;

            for (int i = 0; i < aliveObjects.Count; i++)
            {
                if (aliveObjects[i] is DrawableBmsHitObject hitObject && canParticipateInLocking(hitObject))
                    return aliveObjects;
            }

            return hitObjectContainer.OrderedObjects;
        }

        private static bool canParticipateInLocking(DrawableBmsHitObject hitObject) => hitObject.AcceptsPlayerInput;
    }
}
