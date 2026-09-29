// Copyright (c) OMS contributors. Licensed under the MIT Licence.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneHitObjectContainerOrdering : OsuTestScene
    {
        [Test]
        public void TestOrderedViewsFollowLifetimeSeekAndStableTies()
        {
            HitObjectContainer container = null;
            ManualClock manualClock = null;
            FramedClock clock = null;
            TestDrawable first = null;
            TestDrawable second = null;
            TestDrawable third = null;
            IReadOnlyList<DrawableHitObject> snapshot = null;

            AddStep("create loaded container", () =>
            {
                manualClock = new ManualClock { CurrentTime = 1000 };
                clock = new FramedClock(manualClock);
                clock.ProcessFrame();
                Child = container = new HitObjectContainer { Clock = clock };
                container.Add(first = new TestDrawable(1100));
                container.Add(second = new TestDrawable(1200));
                container.Add(third = new TestDrawable(1200));
            });
            AddUntilStep("all objects alive", () => container.AliveEntries.Count == 3);
            AddStep("check stable ordered snapshots", () =>
            {
                snapshot = container.OrderedAliveObjects;
                Assert.That(snapshot, Is.EqualTo(container.AliveEntries.Values.OrderBy(drawable => drawable.HitObject.StartTime)));
                Assert.That(container.OrderedObjects, Is.EqualTo(new[] { first, third, second }));
                Assert.That(container.AliveObjects, Is.SameAs(snapshot));
                Assert.That(container.OrderedAliveObjects, Is.SameAs(snapshot));
                Assert.That(container.Objects, Is.SameAs(container.OrderedObjects));
            });
            AddStep("edit active object's time", () => first.HitObject.StartTime = 1300);
            AddStep("check time invalidation", () =>
            {
                Assert.That(container.OrderedAliveObjects, Is.Not.SameAs(snapshot));
                Assert.That(container.OrderedAliveObjects, Is.EqualTo(container.AliveEntries.Values.OrderBy(drawable => drawable.HitObject.StartTime)));
                Assert.That(container.OrderedObjects, Is.EqualTo(new[] { third, second, first }));
                snapshot = container.OrderedAliveObjects;
                first.EndTime = 1500;
                first.RefreshStateTransforms();
                Assert.That(first.Entry.LifetimeEnd, Is.EqualTo(1500));
                Assert.That(second.Entry.LifetimeEnd, Is.EqualTo(double.MaxValue));
                Assert.That(third.Entry.LifetimeEnd, Is.EqualTo(double.MaxValue));
                manualClock.CurrentTime = 1600;
                clock.ProcessFrame();
            });
            AddUntilStep("one object leaves lifetime", () => container.AliveEntries.Count == 2);
            AddStep("check lifetime invalidation", () =>
            {
                Assert.That(container.OrderedAliveObjects, Is.Not.SameAs(snapshot));
                Assert.That(container.OrderedAliveObjects, Does.Not.Contain(first));
                // Non-pooled fixtures retain the inactive drawable in Objects, as before.
                Assert.That(container.OrderedObjects, Does.Contain(first));
                snapshot = container.OrderedAliveObjects;
                manualClock.CurrentTime = 1400;
                clock.ProcessFrame();
            });
            AddUntilStep("seek restores alive object", () => container.AliveEntries.Count == 3);
            AddStep("check restored order and removal", () =>
            {
                Assert.That(container.OrderedAliveObjects, Is.Not.SameAs(snapshot));
                Assert.That(container.OrderedAliveObjects, Is.EqualTo(container.AliveEntries.Values.OrderBy(drawable => drawable.HitObject.StartTime)));
                container.Remove(second);
                Assert.That(container.OrderedObjects, Is.EqualTo(new[] { third, first }));
                Assert.That(container.OrderedAliveObjects, Does.Not.Contain(second));
            });
        }

        private partial class TestDrawable : DrawableHitObject
        {
            public double EndTime { get; set; } = double.MaxValue;

            public TestDrawable(double time)
                : base(new HitObject { StartTime = time, HitWindows = HitWindows.Empty })
            {
            }

            // This fixture tests container membership, not judgement-driven expiration. Keep its explicit
            // boundary through load/skin state refreshes; a null HitWindows would expire every object at StartTime.
            protected override void UpdateHitStateTransforms(ArmedState state) => LifetimeEnd = EndTime;
        }
    }
}
