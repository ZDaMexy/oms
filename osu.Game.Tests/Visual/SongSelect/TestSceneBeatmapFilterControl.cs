// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Overlays;
using osu.Game.Beatmaps;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneBeatmapFilterControl : SongSelectComponentsTestScene
    {
        private FilterControl filterControl = null!;

        [Cached(typeof(INotificationOverlay))]
        private readonly INotificationOverlay notifications = new NotificationOverlay();

        protected override Anchor ComponentAnchor => Anchor.TopRight;
        protected override float InitialRelativeWidth => 0.7f;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = filterControl = new FilterControl
                {
                    State = { Value = Visibility.Visible },
                    RelativeSizeAxes = Axes.X,
                },
            };
        });

        [Test]
        public void TestSearch()
        {
            AddStep("search for text", () => filterControl.Search("test search"));
            AddAssert("search reaches criteria", () => filterControl.CreateCriteria().SearchTerms.Select(t => t.SearchTerm), () => Is.EquivalentTo(new[] { "test", "search" }));
            AddAssert("matching title remains", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(
                new BeatmapInfo(Ruleset.Value.Clone(), new BeatmapDifficulty(), new BeatmapMetadata { Title = "test search" }), filterControl.CreateCriteria()));
            AddAssert("unrelated title excluded", () => !BeatmapCarouselFilterMatching.CheckCriteriaMatch(
                new BeatmapInfo(Ruleset.Value.Clone(), new BeatmapDifficulty(), new BeatmapMetadata { Title = "another song" }), filterControl.CreateCriteria()));
            AddStep("clear search", () => filterControl.Search(string.Empty));
            AddAssert("search criteria cleared", () => filterControl.CreateCriteria().SearchTerms.Length == 0);
            AddAssert("clearing restores unrelated title", () => BeatmapCarouselFilterMatching.CheckCriteriaMatch(
                new BeatmapInfo(Ruleset.Value.Clone(), new BeatmapDifficulty(), new BeatmapMetadata { Title = "another song" }), filterControl.CreateCriteria()));
        }
    }
}
