// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneDifficultyCacheInvalidation : OsuTestScene
    {
        protected override Ruleset CreateRuleset() => new ManiaRuleset();

        [Test]
        public void TestFirstCalculationCannotOverwriteNewerDifficulty()
        {
            ControlledDifficultyCache cache = null!;
            IBindable<StarDifficulty> difficulty = null!;

            AddStep("start a difficulty calculation and invalidate the old chart", () =>
            {
                Add(cache = new ControlledDifficultyCache());
                var original = new BeatmapInfo(Ruleset.Value);
                var updated = new BeatmapInfo(Ruleset.Value) { ID = original.ID };

                difficulty = cache.GetBindableDifficulty(original);
                cache.Invalidate(original, updated);
            });

            AddUntilStep("the new calculation publishes", () => difficulty.Value.Stars, () => Is.EqualTo(5));
            AddAssert("the initial computation was cancelled by invalidation", () => cache.FirstCancellationToken.IsCancellationRequested);
            AddStep("the stale calculator returns despite cancellation", () => cache.FirstResult.SetResult(new StarDifficulty(2, 0)));
            AddWaitStep("allow the earlier result to return to the update thread", 2);
            AddAssert("the newer stars remain visible", () => difficulty.Value.Stars, () => Is.EqualTo(5));
        }

        private partial class ControlledDifficultyCache : BeatmapDifficultyCache
        {
            public readonly TaskCompletionSource<StarDifficulty?> FirstResult = new TaskCompletionSource<StarDifficulty?>(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken FirstCancellationToken { get; private set; }

            private bool hasFirstRequest;

            public override Task<StarDifficulty?> GetDifficultyAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null,
                                                                     IEnumerable<Mod>? mods = null, CancellationToken cancellationToken = default, int computationDelay = 0)
            {
                if (hasFirstRequest)
                    return Task.FromResult<StarDifficulty?>(new StarDifficulty(5, 0));

                hasFirstRequest = true;
                FirstCancellationToken = cancellationToken;
                return FirstResult.Task;
            }
        }
    }
}
