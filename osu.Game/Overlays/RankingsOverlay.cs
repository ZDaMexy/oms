// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Overlays.Rankings;
using osu.Game.Users;
using osu.Game.Rulesets;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.IR;
using osu.Game.Overlays.Rankings.Tables;

namespace osu.Game.Overlays
{
    public partial class RankingsOverlay : TabbableOnlineOverlay<RankingsOverlayHeader, RankingsScope>, IOmsPageOverlay
    {
        protected Bindable<CountryCode> Country => Header.Country;

        private APIRequest lastRequest;

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> parentRuleset { get; set; }

        [Cached]
        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();
        private readonly bool useOmsService;
        private readonly OmsPlayerPageContent omsContent;
        private bool omsPageRequested;
        private bool resumeOmsPage;
        private bool synchronisingOmsHeader;
        private float omsRestoreScroll;

        [Resolved(canBeNull: true)]
        private OsuGame omsGame { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        public OmsPageRoute CurrentOmsRoute => useOmsService ? omsContent.Route : null;

        public float OmsScroll
        {
            get => (float)ScrollFlow.Current;
            set
            {
                omsRestoreScroll = value;
                if (IsLoaded) ScrollFlow.ScrollTo(value, false);
            }
        }

        public RankingsOverlay(bool useOmsService = false)
            : base(OverlayColourScheme.Green, !useOmsService)
        {
            this.useOmsService = useOmsService;
            if (useOmsService)
            {
                Header.EnableOms();
                Child = omsContent = new OmsPlayerPageContent
                {
                    Padding = new MarginPadding { Horizontal = HORIZONTAL_PADDING, Vertical = 20 },
                };
                omsContent.Rendered = () => ScheduleAfterChildren(() =>
                {
                    ScrollFlow.ScrollTo(omsRestoreScroll, false);
                    omsRestoreScroll = 0;
                });
                omsContent.RouteLoaded += route =>
                {
                    synchronisingOmsHeader = true;
                    Header.SetOmsRoute(route);
                    Header.Ruleset.Value = rulesets.GetRuleset(route.Ruleset);
                    synchronisingOmsHeader = false;
                };
                Header.OmsMetricChanged += omsContent.SelectMetric;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (useOmsService)
            {
                Loading.Hide();
                Header.Ruleset.BindValueChanged(change =>
                {
                    if (!synchronisingOmsHeader && change.NewValue != null && change.NewValue.ShortName != omsContent.Route.Ruleset)
                        omsContent.SelectRuleset(change.NewValue.ShortName);
                });
                return;
            }

            Header.Ruleset.BindTo(ruleset);

            Country.BindValueChanged(_ =>
            {
                // if a country is requested, force performance scope.
                if (!Country.IsDefault)
                    Header.Current.Value = RankingsScope.Performance;

                Scheduler.AddOnce(triggerTabChanged);
            });

            ruleset.BindValueChanged(_ =>
            {
                if (Header.Current.Value == RankingsScope.Playlists)
                    return;

                Scheduler.AddOnce(triggerTabChanged);
            });
        }

        private bool requiresRulesetUpdate = true;

        protected override void PopIn()
        {
            if (useOmsService)
            {
                base.PopIn();
                if (!omsPageRequested)
                    ShowOmsPage(omsGame?.CurrentOmsRoute(OmsPageKind.Rankings) ?? new OmsPageRoute { Kind = OmsPageKind.Rankings });
                else if (resumeOmsPage)
                {
                    resumeOmsPage = false;
                    omsContent.Resume(omsContent.Route);
                }
                return;
            }

            if (requiresRulesetUpdate)
            {
                ruleset.Value = parentRuleset.Value;
                requiresRulesetUpdate = false;
            }

            base.PopIn();
        }

        protected override void OnTabChanged(RankingsScope tab)
        {
            if (useOmsService) return;

            // country filtering is only valid for performance scope.
            if (Header.Current.Value != RankingsScope.Performance)
                Country.SetDefault();

            Scheduler.AddOnce(triggerTabChanged);
        }

        private void triggerTabChanged() => base.OnTabChanged(Header.Current.Value);

        protected override RankingsOverlayHeader CreateHeader() => new RankingsOverlayHeader();

        public void ShowCountry(CountryCode requested)
        {
            if (useOmsService || requested == default)
                return;

            Show();

            Country.Value = requested;
        }

        public void ShowOmsPage(OmsPageRoute route)
        {
            if (!useOmsService || route.Kind != OmsPageKind.Rankings)
                throw new ArgumentException("The OMS rankings overlay requires a player ranking route.", nameof(route));
            omsPageRequested = true;
            resumeOmsPage = false;
            omsRestoreScroll = 0;
            omsContent.Open(route);
            Show();
        }

        public void RestoreOmsPage(OmsPageRoute route, float scroll)
        {
            if (!useOmsService || route.Kind != OmsPageKind.Rankings)
                throw new ArgumentException("The OMS rankings overlay requires a player ranking route.", nameof(route));
            omsPageRequested = true;
            resumeOmsPage = false;
            omsRestoreScroll = scroll;
            omsContent.Resume(route);
            Show();
        }

        protected override void PopOut()
        {
            if (useOmsService)
            {
                omsContent.CancelOperation();
                resumeOmsPage = true;
            }
            base.PopOut();
        }

        protected override void PopOutComplete()
        {
            if (!useOmsService) base.PopOutComplete();
        }

        protected override void Update()
        {
            base.Update();
            if (useOmsService)
            {
                if (omsContent.IsLoading) Loading.Show();
                else Loading.Hide();
            }
        }

        protected override void CreateDisplayToLoad(RankingsScope tab)
        {
            lastRequest?.Cancel();

            if (Header.Current.Value == RankingsScope.Playlists)
            {
                LoadDisplay(new SpotlightsLayout
                {
                    Ruleset = { BindTarget = ruleset }
                });
                return;
            }

            var request = createScopedRequest();
            lastRequest = request;

            if (request == null)
            {
                LoadDisplay(Empty());
                return;
            }

            request.Success += () => Schedule(() => LoadDisplay(createTableFromResponse(request)));
            request.Failure += _ => Schedule(() => LoadDisplay(Empty()));

            api.Queue(request);
        }

        private APIRequest createScopedRequest()
        {
            switch (Header.Current.Value)
            {
                case RankingsScope.Performance:
                    return new GetUserRankingsRequest(ruleset.Value, countryCode: Country.Value);

                case RankingsScope.Country:
                    return new GetCountryRankingsRequest(ruleset.Value);

                case RankingsScope.Score:
                    return new GetUserRankingsRequest(ruleset.Value, UserRankingsType.Score);

                case RankingsScope.Kudosu:
                    return new GetKudosuRankingsRequest();
            }

            return null;
        }

        private Drawable createTableFromResponse(APIRequest request)
        {
            switch (request)
            {
                case GetUserRankingsRequest userRequest:
                    if (userRequest.Response == null)
                        return null;

                    switch (userRequest.Type)
                    {
                        case UserRankingsType.Performance:
                            return new PerformanceTable(1, userRequest.Response.Users);

                        case UserRankingsType.Score:
                            return new ScoresTable(1, userRequest.Response.Users);
                    }

                    return null;

                case GetCountryRankingsRequest countryRequest:
                {
                    if (countryRequest.Response == null)
                        return null;

                    return new CountriesTable(1, countryRequest.Response.Countries);
                }

                case GetKudosuRankingsRequest kudosuRequest:
                    if (kudosuRequest.Response == null)
                        return null;

                    return new KudosuTable(1, kudosuRequest.Response.Users);
            }

            return null;
        }

        protected override void Dispose(bool isDisposing)
        {
            lastRequest?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
