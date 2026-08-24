// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestResultsApplauseSkippedCallbackReclaimsRevisionParticipantExactlyOnce()
        {
            string packageRoot = string.Empty;
            ResultsOwnershipHost resultsHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            BeatmapManager beatmapManager = null!;
            GatedPoolableSample provisional = null!;
            GatedStatisticsContainer provisionalStatistics = null!;
            SkinCurrentRevision revisionA = null!;
            int participantBaseline = 0;
            int retiredA = 0;
            int statisticsCreationCount = 0;
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var statisticsEntered = new ManualResetEventSlim();
            var statisticsRelease = new ManualResetEventSlim();

            AddStep("create and select results revision A", () =>
            {
                var candidate = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                packageRoot = candidate.PackageRoot;
                manager.CurrentSkinInfo.Value = candidate.Candidate;
            });
            AddUntilStep("wait for results revision A", () => manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount real results caller", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        Interlocked.Increment(ref retiredA);
                };

                beatmapManager = new BeatmapManager(LocalStorage, Realm, null, Audio, Resources, host, Beatmap.Default);
                Add(resultsHost = new ResultsOwnershipHost(manager, beatmapManager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for current results screen", () =>
                resultsHost.Screen.IsLoaded
                && resultsHost.Screen.IsCurrentScreen()
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("install gated production applause sample", () =>
            {
                participantBaseline = revisionA.ParticipantLeaseCount;
                resultsHost.Screen.RankApplauseSampleFactory = info =>
                    provisional = new GatedPoolableSample(info, entered, release);
                resultsHost.Screen.Panel.StatisticsContainerFactory = content =>
                {
                    Interlocked.Increment(ref statisticsCreationCount);
                    return provisionalStatistics = new GatedStatisticsContainer(content, statisticsEntered, statisticsRelease);
                };
                resultsHost.Screen.Panel.StatisticsItemsFactory = createBmsStatisticsItems;
                resultsHost.Screen.PlayApplause(ScoreRank.S);
                ScoreInfo statisticsScore = createBmsStatisticsScore("results-suspend");
                resultsHost.Screen.Panel.Score.UnbindFrom(resultsHost.Screen.SelectedScore);
                resultsHost.Screen.AddScorePanel(statisticsScore);
                resultsHost.Screen.SelectedScore.Value = statisticsScore;
                resultsHost.Screen.Panel.Show();
                resultsHost.Screen.Panel.Score.Value = statisticsScore;
            });
            AddUntilStep("wait for results provisional BDL gates", () =>
                entered.IsSet
                && Volatile.Read(ref statisticsCreationCount) == 1
                && provisionalStatistics != null
                && statisticsEntered.IsSet);
            AddUntilStep("wait for real BMS results participants", () =>
                provisionalStatistics.ContainsBmsResultParticipants
                && revisionA.ParticipantLeaseCount > participantBaseline);
            AddStep("leave results before callback", () =>
            {
                resultsHost.LeaveResults();
                release.Set();
                statisticsRelease.Set();
            });
            AddUntilStep("wait for skipped callback reclaim", () =>
                provisional.DisposeCount == 1
                && provisionalStatistics.DisposeCount == 1
                && revisionA.ParticipantLeaseCount == participantBaseline);
            AddStep("dispose results graph after reclaim", () =>
            {
                Assert.That(Remove(resultsHost, disposeImmediately: true), Is.True);
                Assert.That(provisional.DisposeCount, Is.EqualTo(1));
                Assert.That(provisionalStatistics.DisposeCount, Is.EqualTo(1));
            });
            AddStep("rewrite same results record to revision B", () =>
            {
                File.AppendAllText(Path.Combine(packageRoot, "skin.ini"), "\n// results-owner-B\n");
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for results revision B and A retirement", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && revisionA.ConsumersDetached.IsCompleted
                && revisionA.Retired.IsCompleted
                && Volatile.Read(ref retiredA) == 1);
            AddStep("assert results owner retired exactly once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(provisional.DisposeCount, Is.EqualTo(1));
                    Assert.That(provisionalStatistics.DisposeCount, Is.EqualTo(1));
                    Assert.That(retiredA, Is.EqualTo(1));
                });

                entered.Dispose();
                release.Dispose();
                statisticsEntered.Dispose();
                statisticsRelease.Dispose();
            });
        }

        [Test]
        public void TestBmsStatisticsScoreChangeAndDisposeReclaimProvisionalParticipantsExactlyOnce()
        {
            string packageRoot = string.Empty;
            StatisticsOwnershipHost statisticsHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            BeatmapManager beatmapManager = null!;
            GatedStatisticsContainer first = null!;
            GatedStatisticsContainer second = null!;
            SkinCurrentRevision revisionA = null!;
            int participantBaseline = 0;
            int retiredA = 0;
            int creationCount = 0;
            var firstEntered = new ManualResetEventSlim();
            var firstRelease = new ManualResetEventSlim();
            var secondEntered = new ManualResetEventSlim();
            var secondRelease = new ManualResetEventSlim();
            var heldCallbackScheduler = new Scheduler();

            AddStep("create and select statistics revision A", () =>
            {
                var candidate = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                packageRoot = candidate.PackageRoot;
                manager.CurrentSkinInfo.Value = candidate.Candidate;
            });
            AddUntilStep("wait for statistics revision A", () => manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount real BMS statistics panel", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        Interlocked.Increment(ref retiredA);
                };

                beatmapManager = new BeatmapManager(LocalStorage, Realm, null, Audio, Resources, host, Beatmap.Default);
                Add(statisticsHost = new StatisticsOwnershipHost(manager, beatmapManager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for statistics host", () => statisticsHost.Panel.IsLoaded && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("install two gated statistics generations", () =>
            {
                participantBaseline = revisionA.ParticipantLeaseCount;
                statisticsHost.Panel.StatisticsContainerFactory = content =>
                {
                    if (Interlocked.Increment(ref creationCount) == 1)
                        return first = new GatedStatisticsContainer(content, firstEntered, firstRelease);

                    return second = new GatedStatisticsContainer(content, secondEntered, secondRelease);
                };
                statisticsHost.Panel.Score.Value = createBmsStatisticsScore("first");
            });
            AddUntilStep("wait for first BMS statistics provisional", () =>
                first != null
                && firstEntered.IsSet
                && first.ContainsBmsResultParticipants
                && revisionA.ParticipantLeaseCount > participantBaseline);
            AddStep("change score while first generation loads", () =>
            {
                statisticsHost.Panel.Score.Value = null;
                firstRelease.Set();
            });
            AddUntilStep("wait for first statistics reclaim", () =>
                first.DisposeCount == 1
                && revisionA.ParticipantLeaseCount == participantBaseline);
            AddStep("start second generation with callback held", () =>
            {
                statisticsHost.Panel.StatisticsLoadCallbackScheduler = heldCallbackScheduler;
                statisticsHost.Panel.Score.Value = createBmsStatisticsScore("second");
            });
            AddUntilStep("wait for second BMS statistics provisional", () =>
                second != null
                && secondEntered.IsSet
                && second.ContainsBmsResultParticipants
                && revisionA.ParticipantLeaseCount > participantBaseline);
            AddStep("release second worker while holding callback", secondRelease.Set);
            AddUntilStep("wait for second prepared callback window", () =>
                statisticsHost.Panel.PendingStatisticsLoadTask?.IsCompleted == true
                && second.Parent?.Parent == null
                && second.DisposeCount == 0);
            AddStep("dispose statistics host before callback", () =>
            {
                Assert.That(Remove(statisticsHost, disposeImmediately: true), Is.True);
                heldCallbackScheduler.Update();
                Assert.Multiple(() =>
                {
                    Assert.That(first.DisposeCount, Is.EqualTo(1));
                    Assert.That(second.DisposeCount, Is.EqualTo(1));
                });
            });
            AddStep("rewrite same statistics record to revision B", () =>
            {
                File.AppendAllText(Path.Combine(packageRoot, "skin.ini"), "\n// statistics-owner-B\n");
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for statistics revision B and A retirement", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && revisionA.ConsumersDetached.IsCompleted
                && revisionA.Retired.IsCompleted
                && Volatile.Read(ref retiredA) == 1);
            AddStep("assert statistics owners retired exactly once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(first.DisposeCount, Is.EqualTo(1));
                    Assert.That(second.DisposeCount, Is.EqualTo(1));
                    Assert.That(retiredA, Is.EqualTo(1));
                });

                firstEntered.Dispose();
                firstRelease.Dispose();
                secondEntered.Dispose();
                secondRelease.Dispose();
            });
        }

        private static ScoreInfo createBmsStatisticsScore(string marker)
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BeatmapInfo(
                ruleset.RulesetInfo.Clone(),
                new BeatmapDifficulty { CircleSize = 7 },
                new BeatmapMetadata { Title = marker, Artist = "OMS" });
            var set = new BeatmapSetInfo();
            set.Beatmaps.Add(beatmap);
            beatmap.BeatmapSet = set;

            var score = new ScoreInfo
            {
                BeatmapInfo = beatmap,
                BeatmapHash = marker,
                Ruleset = ruleset.RulesetInfo,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1 },
                MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1 },
                Accuracy = 1,
            };

            // Keep the production gauge-history result participant reachable as well as the summary participant.
            score.HitEvents.Add(new HitEvent(0, 1, HitResult.Ok, new BmsEmptyPoorHitObject(), null, null));
            return score;
        }

        private static IEnumerable<StatisticItem> createBmsStatisticsItems(ScoreInfo score, IBeatmap playableBeatmap)
        {
            // These are the two real BMS results participant types produced by BmsRuleset. Keeping their constructors
            // independent of score arithmetic makes the ownership regression deterministic.
            yield return new StatisticItem(
                string.Empty,
                () => new SkinnableBmsResultsSummaryPanelDisplay(null));
            yield return new StatisticItem(
                string.Empty,
                () => new SkinnableBmsGaugeHistoryPanelDisplay(null));
        }

        private sealed partial class ResultsOwnershipHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            [Cached]
            private readonly BeatmapManager beatmapManager;

            private readonly OsuScreenStack stack = new OsuScreenStack();

            internal TestResultsScreen Screen { get; } = new TestResultsScreen();

            internal ResultsOwnershipHost(SkinManager skinManager, BeatmapManager beatmapManager)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;
                this.beatmapManager = beatmapManager;
                RelativeSizeAxes = Axes.Both;
                InternalChild = stack;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                stack.Push(Screen);
            }

            internal void LeaveResults() => stack.Push(new EmptyResultsScreen());
        }

        private sealed partial class TestResultsScreen : ResultsScreen
        {
            internal StatisticsPanel Panel => StatisticsPanel;

            internal void AddScorePanel(ScoreInfo score) => ScorePanelList.AddScore(score);

            internal TestResultsScreen()
                : base(null)
            {
            }
        }

        private sealed partial class EmptyResultsScreen : OsuScreen
        {
        }

        private sealed partial class GatedPoolableSample : PoolableSkinnableSample
        {
            private readonly ManualResetEventSlim entered;
            private readonly ManualResetEventSlim release;
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            internal GatedPoolableSample(ISampleInfo sampleInfo, ManualResetEventSlim entered, ManualResetEventSlim release)
                : base(sampleInfo)
            {
                this.entered = entered;
                this.release = release;
            }

            [BackgroundDependencyLoader]
            private void waitForGate()
            {
                entered.Set();

                if (!release.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Timed out waiting to release the results applause load gate.");
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class StatisticsOwnershipHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            [Cached]
            private readonly BeatmapManager beatmapManager;

            internal TestStatisticsPanel Panel { get; } = new TestStatisticsPanel
            {
                RelativeSizeAxes = Axes.Both,
                State = { Value = Visibility.Visible },
            };

            internal StatisticsOwnershipHost(SkinManager skinManager, BeatmapManager beatmapManager)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;
                this.beatmapManager = beatmapManager;
                RelativeSizeAxes = Axes.Both;
                InternalChild = Panel;
            }
        }

        private sealed partial class TestStatisticsPanel : StatisticsPanel
        {
            protected override IEnumerable<StatisticItem> CreateStatisticItems(ScoreInfo newScore, IBeatmap playableBeatmap)
                => createBmsStatisticsItems(newScore, playableBeatmap);
        }

        private sealed partial class GatedStatisticsContainer : Container<Drawable>
        {
            private readonly ManualResetEventSlim entered;
            private readonly ManualResetEventSlim release;
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            internal bool ContainsBmsResultParticipants =>
                this.ChildrenOfType<SkinnableBmsResultsSummaryPanelDisplay>().Any()
                && this.ChildrenOfType<SkinnableBmsGaugeHistoryPanelDisplay>().Any();

            internal GatedStatisticsContainer(
                Container<Drawable> content,
                ManualResetEventSlim entered,
                ManualResetEventSlim release)
            {
                this.entered = entered;
                this.release = release;
                RelativeSizeAxes = Axes.Both;
                Child = content;
            }

            [BackgroundDependencyLoader]
            private void waitForGate()
            {
                entered.Set();

                if (!release.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Timed out waiting to release the BMS statistics load gate.");
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }
    }
}
