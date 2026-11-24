// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    public partial class TestSceneManiaPlayer : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("change direction to down", () => changeDirectionTo(ManiaScrollingDirection.Down));
            AddStep("change direction to up", () => changeDirectionTo(ManiaScrollingDirection.Up));
        }

        [Test]
        public void TestScrollSpeedChangesInOneUpdate()
        {
            Bindable<double> range = null!;
            double expectedRange = 0;
            int rangeChanges = 0;

            AddStep("change scroll speed while playing", () =>
            {
                var drawable = (DrawableManiaRuleset)Player.DrawableRuleset;
                var config = (ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset()).AsNonNull();
                double previousSpeed = config.Get<double>(ManiaRulesetSetting.ScrollSpeed);
                double newSpeed = previousSpeed == 20 ? 30 : 20;

                range = (Bindable<double>)drawable.ScrollingInfo.TimeRange.GetBoundCopy();
                expectedRange = range.Value * previousSpeed / newSpeed;
                range.BindValueChanged(_ => rangeChanges++);
                config.SetValue(ManiaRulesetSetting.ScrollSpeed, newSpeed);
            });

            AddUntilStep("new visible range is applied", () => range.Value, () => Is.EqualTo(expectedRange).Within(0.001));
            AddAssert("one change to the playable field", () => rangeChanges, () => Is.EqualTo(1));
            AddStep("stop observing visible range", () => range.UnbindAll());
        }

        private void changeDirectionTo(ManiaScrollingDirection direction)
        {
            var rulesetConfig = (ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(new ManiaRuleset()).AsNonNull();
            rulesetConfig.SetValue(ManiaRulesetSetting.ScrollDirection, direction);
        }
    }
}
