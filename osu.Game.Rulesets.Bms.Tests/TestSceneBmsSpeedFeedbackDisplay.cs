// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsSpeedFeedbackDisplay : OsuTestScene
    {
        private readonly Bindable<BmsGameplayFeedbackState> gameplayFeedbackState = new Bindable<BmsGameplayFeedbackState>();
        private readonly BindableList<BmsJudgementTimingFeedback> recentJudgementFeedbacks = new BindableList<BmsJudgementTimingFeedback>();

        private DefaultBmsSpeedFeedbackDisplay display = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            gameplayFeedbackState.Value = createState();
            recentJudgementFeedbacks.Clear();

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(20),
                Child = new Container
                {
                    Width = 220,
                    AutoSizeAxes = Axes.Y,
                    Child = display = new DefaultBmsSpeedFeedbackDisplay(gameplayFeedbackState, recentJudgementFeedbacks),
                }
            };
        });

        [Test]
        public void TestDisplayShowsGreenNumberVisibleTimeAndTarget()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddAssert("green number shown", () => tryGetText("GN 208") != null);
            AddAssert("visible time shown", () => tryGetText("346ms | WN 350") != null);
            AddAssert("target detail shown", () => tryGetText("NHS 8.0 | SUD 1/3") != null);
            AddAssert("pacemaker shown", () => tryGetText("PAC AAA -1") != null);
            AddAssert("live run status shown", () => tryGetText("LIVE PERFECT") != null);
            AddAssert("green number uses accent colour", () => hasSingleColour(getText("GN 208"), BmsDefaultHudPalette.SpeedFeedbackAccent));
        }

        [Test]
        public void TestDisplayShowsPacemakerAsMetWhenOnPace()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("reach aaa pace", () => gameplayFeedbackState.Value = createState(exScorePacemakerInfo: BmsExScorePacemakerInfo.Create(BmsDjLevel.AAA, 2, 1, 2)));
            AddAssert("pacemaker line updated", () => tryGetText("PAC AAA +0") != null);
            AddAssert("pacemaker uses accent colour", () => hasSingleColour(getText("PAC AAA +0"), BmsDefaultHudPalette.SpeedFeedbackAccent));
        }

        [Test]
        public void TestDisplayShowsDjLevelAndExPercentage()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddAssert("initial dj line shown", () => tryGetText("DJ C | EX 1/2 50.00%") != null);
            AddStep("raise live ex progress", () => gameplayFeedbackState.Value = createState(exScoreProgressInfo: BmsExScoreProgressInfo.Create(8, 9)));
            AddAssert("dj line updated", () => tryGetText("DJ AAA | EX 8/9 88.89%") != null);
        }

        [Test]
        public void TestDisplayShowsCompactJudgementSummary()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddAssert("initial primary summary shown", () => tryGetText("PGR 0 GR 0 GD 0") != null);
            AddAssert("initial secondary summary shown", () => tryGetText("BD 0 PR 0 EP 0") != null);
            AddStep("set compact judgement counts", () => gameplayFeedbackState.Value = createState(judgementCounts: new BmsJudgementCounts(12, 3, 1, 0, 2, 4)));
            AddAssert("primary summary updated", () => tryGetText("PGR 12 GR 3 GD 1") != null);
            AddAssert("secondary summary updated", () => tryGetText("BD 0 PR 2 EP 4") != null);
        }

        [Test]
        public void TestDisplayShowsLiveRunStatus()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddAssert("perfect status shown", () => tryGetText("LIVE PERFECT") != null);
            AddAssert("perfect status uses perfect accent", () => hasSingleColour(getText("LIVE PERFECT"), BmsDefaultResultsPalette.LampPerfectAccent));

            AddStep("lose perfect but keep fc", () => gameplayFeedbackState.Value = createState(judgementCounts: new BmsJudgementCounts(12, 1, 0, 0, 0, 0)));
            AddAssert("full combo status shown", () => tryGetText("LIVE FC | GR 1") != null);
            AddAssert("full combo status uses fc accent", () => hasSingleColour(getText("LIVE FC | GR 1"), BmsDefaultResultsPalette.LampFullComboAccent));

            AddStep("break full combo", () => gameplayFeedbackState.Value = createState(judgementCounts: new BmsJudgementCounts(12, 1, 1, 0, 0, 0)));
            AddAssert("lost status shown", () => tryGetText("FC LOST | GD 1") != null);
            AddAssert("lost status uses warning accent", () => hasSingleColour(getText("FC LOST | GD 1"), BmsDefaultHudPalette.SpeedFeedbackWarning));
        }

        [Test]
        public void TestDisplayShowsCyclePositionForMultiTargetState()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("move to hidden target", () => gameplayFeedbackState.Value = createState(activeAdjustmentTarget: BmsGameplayAdjustmentTarget.Hidden, activeAdjustmentTargetIndex: 1));
            AddAssert("cycle detail shown", () => tryGetText("NHS 8.0 | HID 2/3") != null);
        }

        [Test]
        public void TestDisplayShowsNoTargetStateWhenNoTargetIsActive()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("clear active target", () => gameplayFeedbackState.Value = createState(activeAdjustmentTarget: null, enabledAdjustmentTargetCount: 0, activeAdjustmentTargetIndex: -1));
            AddAssert("no-target detail shown", () => tryGetText("NHS 8.0 | NONE") != null);
            AddAssert("old target detail removed", () => tryGetText("NHS 8.0 | SUD 1/3") == null);
        }

        [Test]
        public void TestDisplayShowsSingleTargetAsLocked()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("reduce to hidden only", () => gameplayFeedbackState.Value = createState(activeAdjustmentTarget: BmsGameplayAdjustmentTarget.Hidden, enabledAdjustmentTargetCount: 1, activeAdjustmentTargetIndex: 0));
            AddAssert("single-target detail shown", () => tryGetText("NHS 8.0 | HID ONLY") != null);
        }

        [Test]
        public void TestDisplayShowsTemporaryOverrideAsHoldState()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("activate temporary hidden override", () => gameplayFeedbackState.Value = createState(activeAdjustmentTarget: BmsGameplayAdjustmentTarget.Hidden, activeAdjustmentTargetIndex: 1, isAdjustmentTargetTemporarilyOverridden: true));
            AddAssert("hold detail shown", () => tryGetText("NHS 8.0 | HID HOLD") != null);
            AddAssert("cycle detail replaced while held", () => tryGetText("NHS 8.0 | HID 2/3") == null);
        }

        [Test]
        public void TestDisplayShowsHiSpeedModeSpecificFormatting()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("switch to floating mode metrics", () => gameplayFeedbackState.Value = createState(speedMetrics: BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Floating, 2.50, 0.8, suddenUnits: 350, hiddenUnits: 200, liftUnits: 150)));
            AddAssert("floating detail shown", () => tryGetText("FHS 2.50 | SUD 1/3") != null);
        }

        [Test]
        public void TestDisplayShowsLatestJudgementWithFastTiming()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("set fast judgement feedback", () => gameplayFeedbackState.Value = createState(latestJudgementFeedback: new BmsJudgementTimingFeedback(HitResult.Perfect, -3.2, true)));
            AddAssert("judgement feedback shown", () => tryGetText("PGREAT | FAST 3.2ms") != null);
        }

        [Test]
        public void TestDisplayShowsEmptyPoorWithoutTimingDirection()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("set empty poor feedback", () => gameplayFeedbackState.Value = createState(latestJudgementFeedback: new BmsJudgementTimingFeedback(HitResult.Ok, 0, false)));
            AddAssert("empty poor shown without timing", () => tryGetText("EPOOR") != null);
            AddAssert("timing suffix omitted", () => tryGetText("EPOOR | SLOW 0ms") == null);
        }

        [Test]
        public void TestJudgementFeedbackExpiresAfterDelay()
        {
            double feedbackShownAt = 0;

            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("set transient judgement feedback", () =>
            {
                gameplayFeedbackState.Value = createState(latestJudgementFeedback: new BmsJudgementTimingFeedback(HitResult.Perfect, -3.2, true));
                feedbackShownAt = display.Time.Current;
            });
            AddAssert("judgement shown immediately", () => tryGetText("PGREAT | FAST 3.2ms") != null);
            AddUntilStep("wait for expiry", () => display.Time.Current - feedbackShownAt >= 1300);
            AddAssert("judgement feedback expired", () => tryGetText("PGREAT | FAST 3.2ms") == null);
        }

        [Test]
        public void TestRepeatedIdenticalJudgementFeedbackRefreshesExpiry()
        {
            double initialFeedbackAt = 0;
            double refreshedFeedbackAt = 0;

            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("set initial judgement feedback", () =>
            {
                gameplayFeedbackState.Value = createState(latestJudgementFeedback: new BmsJudgementTimingFeedback(HitResult.Perfect, -3.2, true, 1));
                initialFeedbackAt = display.Time.Current;
            });
            AddUntilStep("wait before refresh", () => display.Time.Current - initialFeedbackAt >= 700);
            AddStep("refresh with identical feedback", () =>
            {
                gameplayFeedbackState.Value = createState(latestJudgementFeedback: new BmsJudgementTimingFeedback(HitResult.Perfect, -3.2, true, 2));
                refreshedFeedbackAt = display.Time.Current;
            });
            AddUntilStep("wait past original expiry window", () => display.Time.Current - initialFeedbackAt >= 1300);
            AddAssert("judgement still visible after refresh", () => tryGetText("PGREAT | FAST 3.2ms") != null);
            AddUntilStep("wait for refreshed expiry", () => display.Time.Current - refreshedFeedbackAt >= 1300);
            AddAssert("judgement expires after refreshed window", () => tryGetText("PGREAT | FAST 3.2ms") == null);
        }

        [Test]
        public void TestPacemakerChangesDoNotRefreshJudgementExpiry()
        {
            double feedbackShownAt = 0;
            var latestFeedback = new BmsJudgementTimingFeedback(HitResult.Perfect, -3.2, true, 1);

            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("set transient judgement feedback", () =>
            {
                gameplayFeedbackState.Value = createState(latestJudgementFeedback: latestFeedback);
                feedbackShownAt = display.Time.Current;
            });
            AddUntilStep("wait before pacemaker update", () => display.Time.Current - feedbackShownAt >= 700);
            AddStep("change pacemaker only", () => gameplayFeedbackState.Value = createState(latestJudgementFeedback: latestFeedback, exScorePacemakerInfo: BmsExScorePacemakerInfo.Create(BmsDjLevel.AAA, 2, 1, 2)));
            AddAssert("pacemaker updated", () => tryGetText("PAC AAA +0") != null);
            AddUntilStep("wait past original expiry window", () => display.Time.Current - feedbackShownAt >= 1300);
            AddAssert("judgement expires on original schedule", () => tryGetText("PGREAT | FAST 3.2ms") == null);
        }

        [Test]
        public void TestDisplayShowsTimingSparklineForRecentTimedFeedback()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("add recent timing feedback", () =>
            {
                recentJudgementFeedbacks.Add(new BmsJudgementTimingFeedback(HitResult.Great, -8.5, true, 1));
                recentJudgementFeedbacks.Add(new BmsJudgementTimingFeedback(HitResult.Perfect, 3.2, true, 2));
            });
            AddAssert("timing sparkline markers shown", () => getTimingMarkerCount() == 2);
        }

        [Test]
        public void TestTimingSparklineRangeFollowsGameplayFeedbackState()
        {
            float initialMarkerX = 0;

            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("add one late timing feedback", () => recentJudgementFeedbacks.Add(new BmsJudgementTimingFeedback(HitResult.Perfect, 9, true, 1)));
            AddAssert("single timing marker shown", () => getTimingMarkerCount() == 1);
            AddStep("record initial marker position", () => initialMarkerX = getSingleTimingMarker().ScreenSpaceDrawQuad.Centre.X);
            AddStep("shrink visual range via gameplay feedback state", () => gameplayFeedbackState.Value = createState(timingFeedbackVisualRange: 9));
            AddAssert("marker shifts right for smaller range", () => getSingleTimingMarker().ScreenSpaceDrawQuad.Centre.X > initialMarkerX + 10);
        }

        [Test]
        public void TestDisplayShowsWarningStateWhenVisibleLaneIsFullyCovered()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddStep("cover entire lane", () => gameplayFeedbackState.Value = createState(speedMetrics: BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 0.8, suddenUnits: 1000, hiddenUnits: 0, liftUnits: 150)));
            AddAssert("warning gn shown", () => tryGetText("GN ---") != null);
            AddAssert("warning time shown", () => tryGetText("0ms | WN 1000") != null);
            AddAssert("warning colour applied", () => hasSingleColour(getText("GN ---"), BmsDefaultHudPalette.SpeedFeedbackWarning));
        }

        private static BmsGameplayFeedbackState createState(BmsScrollSpeedMetrics? speedMetrics = null, BmsGameplayAdjustmentTarget? activeAdjustmentTarget = BmsGameplayAdjustmentTarget.Sudden,
                                    int enabledAdjustmentTargetCount = 3, int activeAdjustmentTargetIndex = 0, bool isAdjustmentTargetTemporarilyOverridden = false,
                                    BmsJudgementTimingFeedback? latestJudgementFeedback = null, BmsJudgementCounts judgementCounts = default,
                                    BmsExScoreProgressInfo? exScoreProgressInfo = null,
                                    BmsExScorePacemakerInfo? exScorePacemakerInfo = null,
                                    double timingFeedbackVisualRange = 18)
            => new BmsGameplayFeedbackState(
                speedMetrics ?? BmsScrollSpeedMetrics.FromRuntime(BmsHiSpeedMode.Normal, 8.0, 0.8, suddenUnits: 350, hiddenUnits: 200, liftUnits: 150),
                activeAdjustmentTarget,
                enabledAdjustmentTargetCount,
                activeAdjustmentTargetIndex,
                isAdjustmentTargetTemporarilyOverridden,
                latestJudgementFeedback,
                judgementCounts,
                exScoreProgressInfo ?? BmsExScoreProgressInfo.Create(1, 2),
            exScorePacemakerInfo ?? BmsExScorePacemakerInfo.Create(BmsDjLevel.AAA, 1, 1, 2),
            timingFeedbackVisualRange);

        private OsuSpriteText getText(string text)
            => tryGetText(text) ?? throw new AssertionException($"Could not find speed feedback text '{text}'.");

        private OsuSpriteText? tryGetText(string text)
            => display.ChildrenOfType<OsuSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);

        private int getTimingMarkerCount()
            => display.ChildrenOfType<Drawable>().Count(drawable => drawable.Name == "timing-offset-marker");

        private Drawable getSingleTimingMarker()
            => display.ChildrenOfType<Drawable>().Single(drawable => drawable.Name == "timing-offset-marker");

        private static bool hasSingleColour(Drawable drawable, Color4 expected)
            => drawable.Colour.TopLeft.SRGB == expected
               && drawable.Colour.TopRight.SRGB == expected
               && drawable.Colour.BottomLeft.SRGB == expected
               && drawable.Colour.BottomRight.SRGB == expected;
    }
}
