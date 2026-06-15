// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Screens.Play.HUD.JudgementCounter
{
    /// <summary>
    /// Keeps track of judgements for a current play session, exposing bindable counts which can
    /// be used for display purposes.
    /// </summary>
    public partial class JudgementCountController : Component
    {
        [Resolved]
        private ScoreProcessor scoreProcessor { get; set; } = null!;

        private readonly Dictionary<HitResult, JudgementCount> results = new Dictionary<HitResult, JudgementCount>();

        public IEnumerable<JudgementCount> Counters => counters;

        private readonly List<JudgementCount> counters = new List<JudgementCount>();

        [BackgroundDependencyLoader]
        private void load(IBindable<RulesetInfo> ruleset)
        {
            // Due to weirdness in judgements, some results have the same name and should be aggregated for display purposes.
            // There's only one case of this right now ("slider end").
            foreach (var group in ruleset.Value.CreateInstance().GetHitResultsForDisplay().GroupBy(r => r.displayName))
            {
                var judgementCount = new JudgementCount
                {
                    DisplayName = group.Key,
                    Types = group.Select(r => r.result).ToArray(),
                    ResultCount = new BindableInt()
                };

                counters.Add(judgementCount);

                foreach (var r in group)
                    results[r.result] = judgementCount;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scoreProcessor.OnResetFromReplayFrame += updateAllCountsFromReplayFrame;
            scoreProcessor.NewJudgement += _ => syncCounts();
            scoreProcessor.JudgementReverted += _ => syncCounts();
        }

        private bool hasUpdatedCountsFromReplayFrame;

        private void updateAllCountsFromReplayFrame()
        {
            if (hasUpdatedCountsFromReplayFrame)
                return;

            foreach (var kvp in scoreProcessor.Statistics)
            {
                if (!results.TryGetValue(kvp.Key, out var count))
                    continue;

                count.ResultCount.Value = kvp.Value;
            }

            hasUpdatedCountsFromReplayFrame = true;
        }

        private void syncCounts()
        {
            // Sync every counter from the score processor statistics rather than incrementing only the
            // judged result's counter. Some rulesets expose counters whose HitResult is never an actual
            // judgement type but is maintained as a derived running statistic by the score processor
            // (e.g. BMS "combo break", incremented whenever a judgement breaks combo). An increment-on-
            // matching-type approach would leave those stuck at zero during live play and only populate
            // them on a replay-frame reset. Reading from statistics is authoritative for all counters
            // because the processor updates them before raising NewJudgement / JudgementReverted.
            foreach (var counter in counters)
                counter.ResultCount.Value = counter.Types.Sum(type => scoreProcessor.Statistics.GetValueOrDefault(type));
        }
    }
}
