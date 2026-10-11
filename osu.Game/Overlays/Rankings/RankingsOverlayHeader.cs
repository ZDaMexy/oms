// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Online.IR;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Users;

namespace osu.Game.Overlays.Rankings
{
    public partial class RankingsOverlayHeader : TabControlOverlayHeader<RankingsScope>
    {
        public Bindable<RulesetInfo> Ruleset => rulesetSelector.Current;

        public Bindable<CountryCode> Country => countryFilter.Current;

        private OverlayRulesetSelector rulesetSelector = null!;
        private CountryFilter countryFilter = null!;
        private OmsMetricTabs? omsMetrics;
        private bool updatingOmsMetric;
        public event Action<string>? OmsMetricChanged;

        public void EnableOms()
        {
            TabControlContainer.Remove(TabControl, true);
            TabControlContainer.Remove(rulesetSelector, true);
            countryFilter.Hide();
            TabControlContainer.Add(omsMetrics = new OmsMetricTabs());
            TabControlContainer.Add(rulesetSelector = new OmsRulesetSelector
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
            });
            omsMetrics.Current.BindValueChanged(change =>
            {
                if (!updatingOmsMetric) OmsMetricChanged?.Invoke(omsMetrics.MetricFor(change.NewValue));
            });
        }

        public void SetOmsRoute(OmsPageRoute route)
        {
            if (omsMetrics == null) return;
            updatingOmsMetric = true;
            omsMetrics.SetMetrics(OmsPlayerPageContent.PlayerMetrics(route.Ruleset), route.Metric!);
            updatingOmsMetric = false;
        }

        protected override OverlayTitle CreateTitle() => new RankingsTitle();

        protected override Drawable CreateTabControlContent() => rulesetSelector = new OverlayRulesetSelector();

        protected override Drawable CreateContent() => countryFilter = new CountryFilter();

        protected override Drawable CreateBackground() => new OverlayHeaderBackground("Headers/rankings");

        private partial class RankingsTitle : OverlayTitle
        {
            public RankingsTitle()
            {
                Title = PageTitleStrings.MainRankingControllerDefault;
                Description = NamedOverlayComponentStrings.RankingsDescription;
                Icon = OsuIcon.Ranking;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (omsMetrics != null) return;

            Current.BindValueChanged(scope =>
            {
                rulesetSelector.FadeTo(showRulesetSelector(scope.NewValue) ? 1 : 0, 200, Easing.OutQuint);
            }, true);

            bool showRulesetSelector(RankingsScope scope)
            {
                switch (scope)
                {
                    case RankingsScope.Performance:
                    case RankingsScope.Score:
                    case RankingsScope.Country:
                    case RankingsScope.Playlists:
                        return true;

                    default:
                        return false;
                }
            }
        }

        private partial class OmsRulesetSelector : OverlayRulesetSelector
        {
            protected override bool LegacyOnly => false;

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Clear();
                foreach (var ruleset in Rulesets.AvailableRulesets.Where(ruleset => ruleset.ShortName is "bms" or "mania"))
                    AddItem(ruleset);
            }
        }

        private partial class OmsMetricTabs : TabControlOverlayHeader<LocalisableString>.OverlayHeaderTabControl
        {
            private (string Code, string Label)[] metrics = Array.Empty<(string, string)>();

            public void SetMetrics(IEnumerable<(string Code, string Label)> choices, string selected)
            {
                var next = choices.ToArray();
                if (!metrics.SequenceEqual(next))
                {
                    Clear();
                    metrics = next;
                    foreach (var metric in metrics) AddItem(metric.Label);
                }
                Current.Value = metrics.Single(metric => metric.Code == selected).Label;
            }

            public string MetricFor(LocalisableString label) => metrics.Single(metric => label == metric.Label).Code;
        }
    }
}
