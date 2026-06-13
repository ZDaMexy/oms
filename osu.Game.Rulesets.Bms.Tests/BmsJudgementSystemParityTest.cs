// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    /// <summary>
    /// Locks the per-judge-family timing-window contract so any future parity change is reviewable rather than silent.
    /// Window numbers come from <c>doc_md/other/IIDX_REFERENCE_AUDIT.md</c> (the "判定与 timing" baseline) where the
    /// audit is authoritative; values the audit explicitly declines to pin (IIDX empty-POOR, beatoraja BAD asymmetry)
    /// are marked PLACEHOLDER below and are the inputs to P1-C slice 2 (sourcing), not settled parity yet.
    /// </summary>
    [TestFixture]
    public class BmsJudgementSystemParityTest
    {
        private static BmsJudgementSystem atRank(BmsJudgementSystem system, BmsJudgeRank rank)
        {
            system.SetDifficulty(rank.ToOverallDifficulty());
            return system;
        }

        // ---- IIDX: fixed windows, rank-invariant (audit: PGREAT ±16.67 / GREAT ±33.33 / GOOD ±116.67 / BAD ±250) ----

        [Test]
        public void TestIidxWindowsMatchAuditBaseline()
        {
            var system = new IidxJudgementSystem();

            Assert.That(system.Windows, Is.EqualTo(new[] { 16.67, 33.33, 116.67, 250d, 250d }));
        }

        [Test]
        public void TestIidxWindowsAreRankInvariant()
        {
            // IIDX does not derive its windows from a judge rank; SetDifficulty must be a no-op.
            var normal = new IidxJudgementSystem().Windows.ToArray();
            var veryHard = atRank(new IidxJudgementSystem(), BmsJudgeRank.VeryHard).Windows.ToArray();

            Assert.That(veryHard, Is.EqualTo(normal));
        }

        [Test]
        public void TestIidxEvaluateRespectsWindowBoundaries()
        {
            var system = new IidxJudgementSystem();

            Assert.Multiple(() =>
            {
                Assert.That(system.Evaluate(16.67), Is.EqualTo(HitResult.Perfect));
                Assert.That(system.Evaluate(-16.67), Is.EqualTo(HitResult.Perfect));
                Assert.That(system.Evaluate(16.7), Is.EqualTo(HitResult.Great));
                Assert.That(system.Evaluate(33.33), Is.EqualTo(HitResult.Great));
                Assert.That(system.Evaluate(33.4), Is.EqualTo(HitResult.Good));
                Assert.That(system.Evaluate(116.67), Is.EqualTo(HitResult.Good));
                Assert.That(system.Evaluate(116.7), Is.EqualTo(HitResult.Meh));
                Assert.That(system.Evaluate(250), Is.EqualTo(HitResult.Meh));
                Assert.That(system.Evaluate(250.1), Is.EqualTo(HitResult.None));
            });
        }

        [Test]
        public void TestIidxEmptyPoorWindowsAreDocumentedHeuristic()
        {
            // Resolved (P1-C slice 2): IIDX is closed-source and publishes no exact empty/excessive POOR window; the
            // audit (doc_md/other/IIDX_REFERENCE_AUDIT.md) only states it can fire before OR after the note. So these
            // stay an explicit OMS heuristic (fires up to 500ms before / 150ms after a note), not a parity claim.
            var system = new IidxJudgementSystem();

            Assert.Multiple(() =>
            {
                Assert.That(system.GetExcessivePoorEarlyWindow(), Is.EqualTo(500));
                Assert.That(system.GetExcessivePoorLateWindow(), Is.EqualTo(150));
            });
        }

        // ---- LR2: four judge-rank tiers, symmetric, excessive POOR before the note only (audit table) ----

        [TestCase(BmsJudgeRank.VeryEasy, 21, 60, 120)] // LR2 has no distinct VERY EASY tier; it shares EASY windows.
        [TestCase(BmsJudgeRank.Easy, 21, 60, 120)]
        [TestCase(BmsJudgeRank.Normal, 18, 40, 100)]
        [TestCase(BmsJudgeRank.Hard, 15, 30, 60)]
        [TestCase(BmsJudgeRank.VeryHard, 8, 24, 40)]
        public void TestLr2WindowsMatchAuditTier(BmsJudgeRank rank, double pgreat, double great, double good)
        {
            var system = atRank(new Lr2JudgementSystem(), rank);

            Assert.That(system.Windows, Is.EqualTo(new[] { pgreat, great, good, 200d, 200d }));
        }

        [Test]
        public void TestLr2ExcessivePoorIsBeforeNoteOnly()
        {
            // Audit: LR2's excessive POOR only occurs before the note (early window wide, late window zero).
            var system = new Lr2JudgementSystem();

            Assert.Multiple(() =>
            {
                Assert.That(system.GetExcessivePoorEarlyWindow(), Is.EqualTo(1000));
                Assert.That(system.GetExcessivePoorLateWindow(), Is.EqualTo(0));

                Assert.That(system.CanTriggerExcessivePoor(-500), Is.True);
                Assert.That(system.CanTriggerExcessivePoor(-1000), Is.True);
                Assert.That(system.CanTriggerExcessivePoor(-1000.001), Is.False);
                Assert.That(system.CanTriggerExcessivePoor(0), Is.True);
                Assert.That(system.CanTriggerExcessivePoor(0.5), Is.False, "late presses must not count as LR2 excessive POOR");
            });
        }

        [Test]
        public void TestLr2LongNoteReleaseWidensMissOnly()
        {
            // Release windows reuse the note windows except a more lenient MISS edge (release_miss_lenience = 1.2).
            var system = new Lr2JudgementSystem();

            Assert.Multiple(() =>
            {
                Assert.That(system.LongNoteReleaseWindows.Take(4), Is.EqualTo(system.Windows.Take(4)));
                Assert.That(system.LongNoteReleaseWindows[4], Is.EqualTo(240).Within(1e-6)); // 200 * 1.2
            });
        }

        // ---- beatoraja: integer-truncation scaled tiers + early/late asymmetric BAD + widened scratch/release ----

        [TestCase(BmsJudgeRank.VeryHard, 25)]
        [TestCase(BmsJudgeRank.Hard, 50)]
        [TestCase(BmsJudgeRank.Normal, 75)]
        [TestCase(BmsJudgeRank.Easy, 100)]
        [TestCase(BmsJudgeRank.VeryEasy, 125)]
        public void TestBeatorajaScalesByIntegerTruncation(BmsJudgeRank rank, int scale)
        {
            var system = atRank(new BeatorajaJudgementSystem(), rank);

            // Base note windows {20, 60, 150} scaled by the rank percentage and integer-truncated.
            Assert.Multiple(() =>
            {
                Assert.That(system.Windows[0], Is.EqualTo(System.Math.Truncate(20 * scale / 100.0)));
                Assert.That(system.Windows[1], Is.EqualTo(System.Math.Truncate(60 * scale / 100.0)));
                Assert.That(system.Windows[2], Is.EqualTo(System.Math.Truncate(150 * scale / 100.0)));
            });
        }

        [Test]
        public void TestBeatorajaBadWindowIsEarlyLateAsymmetric()
        {
            // Sourced from beatoraja JudgeProperty.SEVENKEYS (exch-bms2/beatoraja): note BAD = {-280000, 220000} µs,
            // i.e. early 280ms / late 220ms (the EARLY bound is wider). At NORMAL (scale 75) -> early 210 / late 165.
            var system = atRank(new BeatorajaJudgementSystem(), BmsJudgeRank.Normal);

            Assert.Multiple(() =>
            {
                Assert.That(system.GetEarlyWindow(HitResult.Meh), Is.EqualTo(210));
                Assert.That(system.GetLateWindow(HitResult.Meh), Is.EqualTo(165));

                // An early BAD reaches further than a late BAD of the same magnitude.
                Assert.That(system.Evaluate(-210), Is.EqualTo(HitResult.Meh));
                Assert.That(system.Evaluate(-211), Is.EqualTo(HitResult.None));
                Assert.That(system.Evaluate(165), Is.EqualTo(HitResult.Meh));
                Assert.That(system.Evaluate(166), Is.EqualTo(HitResult.None));

                // Same magnitude, opposite side: an early press is still BAD where a late press is already gone.
                Assert.That(system.Evaluate(-200), Is.EqualTo(HitResult.Meh));
                Assert.That(system.Evaluate(200), Is.EqualTo(HitResult.None));
            });
        }

        [Test]
        public void TestBeatorajaPerfectGreatGoodAreSymmetric()
        {
            var system = atRank(new BeatorajaJudgementSystem(), BmsJudgeRank.Normal);

            Assert.Multiple(() =>
            {
                Assert.That(system.Evaluate(system.Windows[0]), Is.EqualTo(HitResult.Perfect));
                Assert.That(system.Evaluate(-system.Windows[0]), Is.EqualTo(HitResult.Perfect));
                Assert.That(system.Evaluate(system.Windows[2]), Is.EqualTo(HitResult.Good));
                Assert.That(system.Evaluate(-system.Windows[2]), Is.EqualTo(HitResult.Good));
            });
        }

        [Test]
        public void TestBeatorajaWidensScratchAndRelease()
        {
            // beatoraja gives scratch notes and long-note releases extra leniency over a regular note.
            var system = atRank(new BeatorajaJudgementSystem(), BmsJudgeRank.Normal);

            Assert.Multiple(() =>
            {
                Assert.That(system.ScratchWindows[0], Is.GreaterThan(system.Windows[0]));
                Assert.That(system.ScratchWindows[2], Is.GreaterThan(system.Windows[2]));
                Assert.That(system.LongNoteReleaseWindows[0], Is.GreaterThan(system.Windows[0]));
                Assert.That(system.LongScratchReleaseWindows[0], Is.GreaterThanOrEqualTo(system.LongNoteReleaseWindows[0]));
            });
        }

        // ---- OD: structural contract (windows scale with OverallDifficulty; before-note-only excessive POOR) ----

        [Test]
        public void TestOdWindowsTightenWithOverallDifficulty()
        {
            double easyPerfect = atRank(new OsuOdJudgementSystem(), BmsJudgeRank.Easy).Windows[0];
            double veryHardPerfect = atRank(new OsuOdJudgementSystem(), BmsJudgeRank.VeryHard).Windows[0];

            Assert.That(veryHardPerfect, Is.LessThan(easyPerfect));
        }

        [Test]
        public void TestOdWindowsAreAscendingAndExcessivePoorBeforeNoteOnly()
        {
            var system = new OsuOdJudgementSystem();
            var windows = system.Windows.ToArray();

            Assert.Multiple(() =>
            {
                for (int i = 1; i < windows.Length; i++)
                    Assert.That(windows[i], Is.GreaterThanOrEqualTo(windows[i - 1]), $"window {i} must not be tighter than {i - 1}");

                Assert.That(system.GetExcessivePoorEarlyWindow(), Is.EqualTo(500));
                Assert.That(system.GetExcessivePoorLateWindow(), Is.EqualTo(0));
            });
        }

        // ---- cross-family: every judge family supports excessive POOR and shares the inclusive boundary convention ----

        [TestCase(BmsJudgeMode.OD)]
        [TestCase(BmsJudgeMode.Beatoraja)]
        [TestCase(BmsJudgeMode.LR2)]
        [TestCase(BmsJudgeMode.IIDX)]
        public void TestEveryFamilySupportsExcessivePoor(BmsJudgeMode judgeMode)
        {
            var windows = new BmsTimingWindows { JudgementSystem = judgeMode.CreateJudgementSystem() };

            Assert.That(windows.SupportsExcessivePoor, Is.True);
        }

        [TestCase(BmsJudgeMode.OD)]
        [TestCase(BmsJudgeMode.Beatoraja)]
        [TestCase(BmsJudgeMode.LR2)]
        [TestCase(BmsJudgeMode.IIDX)]
        public void TestEveryFamilyTreatsPerfectBoundaryAsInclusive(BmsJudgeMode judgeMode)
        {
            // The shared boundary convention is "<= window + BoundaryEpsilon"; an offset sitting exactly on the
            // PGREAT edge must resolve to Perfect in every family, including beatoraja's bespoke Evaluate branch.
            var system = judgeMode.CreateJudgementSystem();
            double perfectEdge = system.Windows[0];

            Assert.That(system.Evaluate(perfectEdge), Is.EqualTo(HitResult.Perfect));
            Assert.That(system.Evaluate(-perfectEdge), Is.EqualTo(HitResult.Perfect));
        }
    }
}
