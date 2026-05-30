// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Database
{
    [HeadlessTest]
    public partial class BackgroundDataStoreProcessorRulesetVersionTest : OsuTestScene
    {
        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Test]
        public void TestDifficultyVersionRefreshUpdatesDetachedRulesetInfo()
        {
            RulesetInfo maniaRuleset = null!;
            int currentVersion = 0;

            AddStep("get mania ruleset", () =>
            {
                maniaRuleset = rulesets.AvailableRulesets.Single(r => r.ShortName == ManiaRuleset.SHORT_NAME);
                currentVersion = maniaRuleset.CreateInstance().CreateDifficultyCalculator(Beatmap.Default).Version;
            });

            AddStep("set stale difficulty versions", () =>
            {
                maniaRuleset.LastAppliedDifficultyVersion = -1;
                Realm.Write(r => r.Find<RulesetInfo>(maniaRuleset.ShortName)!.LastAppliedDifficultyVersion = -1);
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("wait for completion", () => processor.Completed);

            AddAssert("detached ruleset version refreshed", () => maniaRuleset.LastAppliedDifficultyVersion, () => Is.EqualTo(currentVersion));
            AddAssert("realm ruleset version refreshed", () => Realm.Run(r => r.Find<RulesetInfo>(maniaRuleset.ShortName)!.LastAppliedDifficultyVersion), () => Is.EqualTo(currentVersion));
        }

        public partial class TestBackgroundDataStoreProcessor : BackgroundDataStoreProcessor
        {
            public bool Completed => ProcessingTask.IsCompleted;
        }
    }
}
