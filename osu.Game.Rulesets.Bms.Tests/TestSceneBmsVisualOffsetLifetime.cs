// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsVisualOffsetLifetime : OsuTestScene
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestVisualFadeFollowsOffsetAndLiveChanges(bool mine)
        {
            ManualClock clock = null!;
            FramedClock framedClock = null!;
            DrawableBmsRuleset ruleset = null!;
            Drawable visual = null!;

            AddStep("create visual with delayed arrival", () =>
            {
                ruleset = new DrawableBmsRuleset(new BmsRuleset(), new BmsBeatmap());
                ruleset.Playfield.VisualOffset.Value = -500;
                clock = new ManualClock { CurrentTime = 900 };
                framedClock = new FramedClock(clock);

                if (mine)
                {
                    var hitObject = new BmsMine { StartTime = 1000 };
                    hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    visual = new DrawableBmsMine(hitObject);
                }
                else
                {
                    var hitObject = new BmsBarLine { StartTime = 1000 };
                    hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    var barLine = new DrawableBmsBarLine();
                    barLine.Apply(hitObject);
                    visual = barLine;
                }

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = framedClock,
                    CachedDependencies = new (Type, object)[] { (typeof(DrawableBmsRuleset), ruleset) },
                    Child = visual,
                };
            });
            AddUntilStep("visual loaded", () => visual.IsLoaded);
            AddStep("pass original arrival", () => seek(1200));
            AddAssert("still visible before delayed arrival", () => visual.Alpha, () => Is.EqualTo(1));
            AddAssert("lifetime covers delayed fade", () => visual.LifetimeEnd, () => Is.EqualTo(1650));
            AddStep("reach delayed fade", () => seek(1575));
            AddAssert("half faded at delayed arrival plus 75ms", () => visual.Alpha, () => Is.EqualTo(0.5f).Within(0.001));
            AddStep("rewind to live adjustment point", () => seek(1200));
            AddStep("advance visual offset", () =>
            {
                ruleset.Playfield.VisualOffset.Value = 0;
                seek(1200);
            });
            AddAssert("past visual is hidden", () => visual.Alpha, () => Is.Zero);
            AddStep("move visual back before arrival", () =>
            {
                ruleset.Playfield.VisualOffset.Value = -500;
                seek(1200);
            });
            AddAssert("live offset can restore visual", () => visual.Alpha, () => Is.EqualTo(1));

            void seek(double time)
            {
                clock.CurrentTime = time;
                framedClock.ProcessFrame();
                Child.UpdateSubTree();
            }
        }
    }
}
