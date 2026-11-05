// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning.Gameplay;

namespace osu.Tools.StandardSkinCapture;

public partial class TestSceneStandardSkinCapture
{
    private static bool IsPersonalBestCase => CaptureSettings.Current.ChartVariant == "graph-pb";
    private int personalBestPlayCount;
    private CaptureReadingPlayer personalBestWarmup = null!;
    private ScoreInfo personalBestSaved = null!;
    private BeatmapInfo personalBestBeatmap = null!;

    private void ImportPersonalBestBeatmap(IBeatmap beatmap)
    {
        var result = new BmsFolderImporter(Game.Storage, Game.Realm)
            .Import(new ImportTask(bgaFixtureDirectory!)).GetAwaiter().GetResult();
        Assert.That(result.ImportedBeatmapSet, Is.Not.Null);
        Assert.That(result.SkippedBeatmapFiles, Is.Empty);
        personalBestBeatmap = result.ImportedBeatmapSet!.PerformRead(set => set.Beatmaps.Single().Detach());
        string chartPath = Path.Combine(bgaFixtureDirectory!, settings.Keys == 5 ? "standard-capture.bms" : "standard-capture.bme");
        Assert.That(personalBestBeatmap.Hash, Is.EqualTo(CaptureSettings.Hash(chartPath)));
        Assert.That(Game.ScoreManager.Query(saved => saved.BeatmapHash == personalBestBeatmap.Hash), Is.Null,
            "The two-play fixture must start with no history for its freshly imported synthetic chart.");
        beatmap.BeatmapInfo = personalBestBeatmap;
        settings.Write("personal-best-chart-import.json", new
        {
            importer = typeof(BmsFolderImporter).FullName,
            normalManagedImport = true,
            personalBestBeatmap.ID,
            personalBestBeatmap.Hash,
            personalBestBeatmap.MD5Hash,
            personalBestBeatmap.LocalFilePath,
            chartPath,
            sourceChartSha256 = CaptureSettings.Hash(chartPath),
            warnings = result.ImportWarnings.Select(item => new { item.BeatmapFile, item.WarningCount }).ToArray(),
            isolatedTestStorage = true,
            userDataRead = false,
        });
    }

    private void AddPersonalBestWarmupSteps()
    {
        AddUntilStep("the first ordinary Player is ready for the native PB warmup", () =>
        {
            DismissAnyNotifications();
            if (player is not CaptureReadingPlayer reading || !ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                || !player.LoadedBeatmapSuccessfully || !reading.NativeRuleset.IsLoaded)
                return false;
            runtime = reading.NativeRuleset.Dependencies.Get<GameplaySkinSceneRuntimeHost>();
            if (!runtime.IsSceneReady)
                return false;
            personalBestWarmup = reading;
            events = reading.NativeRuleset.Dependencies.Get<GameplaySkinEventRuntimeHost>();
            eventStream = events.EventStream;
            score = reading.NativeRuleset.Dependencies.Get<ScoreProcessor>();
            clock = player.ChildrenOfType<GameplayClockContainer>().Single();
            return true;
        });
        AddUntilStep("warmup preparation naturally starts chart time", () => !ReadState().Timing.Preparation.Active && clock.IsRunning);
        AddStep("schedule the real warmup inputs with two omitted short-note chords", () =>
        {
            Assert.That(ReadState().ChartPresentation?.PersonalBestScore, Is.Null);
            Assert.That(personalBestWarmup.OmittedChords, Has.Count.EqualTo(2));
            settings.Write("personal-best-warmup-input-plan.json", personalBestWarmup.StartSyntheticChartInput());
            settings.Write("personal-best-warmup-omissions.json", new
            {
                personalBestWarmup.ExpectedPersonalBestMisses,
                chords = personalBestWarmup.OmittedChords,
                selection = "First two complete nonempty press-to-empty-release intervals from the native input plan",
                nativeShortNotesOnly = true,
                scoreOrJudgementRewritten = false,
            });
        });
        foreach (int sample in new[] { 4000, 8000, 11000 })
            AddUntilStep("warmup naturally reaches " + sample + " ms", () => ReadProperty<double>(events, "AuthoritativeGameplayTime") >= sample);
        AddUntilStep("the warmup actually scores its entire chart", () => score.HasCompleted.Value);
        AddStep("retain the actual lower completed warmup score", () =>
        {
            Assert.That(player.GameplayState.HasFailed, Is.False);
            Assert.That(score.IsFullCombo, Is.False);
            Assert.That(score.Statistics.GetValueOrDefault(HitResult.Miss), Is.EqualTo(personalBestWarmup.ExpectedPersonalBestMisses),
                "Exactly the native short notes in the first two omitted chords should miss.");
            Assert.That(score.TotalScore.Value, Is.GreaterThan(0).And.LessThan(ReadState().Score.MaximumScore));
            AssertNativePersonalBestInputComplete(personalBestWarmup);
            settings.Write("personal-best-warmup-score.json", new
            {
                nativeScore = score.TotalScore.Value,
                maximum = ReadState().Score.MaximumScore,
                nativeCombo = score.HighestCombo.Value,
                score.IsFullCombo,
                statistics = score.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                omittedChords = personalBestWarmup.OmittedChords,
                expectedMisses = personalBestWarmup.ExpectedPersonalBestMisses,
                deliveredInputs = personalBestWarmup.ChartInputObservations.ToArray(),
                recordedReplayFrames = personalBestWarmup.Score.Replay.Frames.Count,
                injectedScore = false,
                inputWasAttachedAsReplay = false,
            });
        });
        AddUntilStep("the warmup naturally reaches Results after native score import", () =>
            Game.ScreenStack.CurrentScreen is ResultsScreen { IsLoaded: true, Score: not null });
        AddStep("verify the native saved score and restart through the real PlayerLoader", () =>
        {
            var firstResults = (ResultsScreen)Game.ScreenStack.CurrentScreen;
            ScoreInfo actual = firstResults.Score!;
            personalBestSaved = Game.ScoreManager.Query(saved => saved.ID == actual.ID)
                ?? throw new InvalidOperationException("Native Player did not save the completed warmup score.");
            AssertPersonalBestStoredIdentity(personalBestSaved, actual);
            Assert.That(personalBestSaved.TotalScore, Is.EqualTo(score.TotalScore.Value));
            Assert.That(personalBestSaved.Statistics.GetValueOrDefault(HitResult.Miss), Is.EqualTo(personalBestWarmup.ExpectedPersonalBestMisses));
            settings.Write("personal-best-warmup-saved.json", new
            {
                nativeResults = firstResults.GetType().FullName,
                nativePlayer = personalBestWarmup.GetType().FullName,
                nativeStorePath = "Player.ImportScore -> ScoreManager.Import -> ScoreImporter",
                score = PersonalBestScoreIdentity(personalBestSaved),
                currentLocalUserMatches = true,
                sameIsolatedStorage = true,
                noResultValuesRewritten = true,
            });
            Assert.That(personalBestWarmup.Restart(), Is.True, "Use the ordinary completed-player restart, without replacing the database or injecting PB.");
        });
        AddUntilStep("the restart creates a distinct second Player", () => player != null && !ReferenceEquals(player, personalBestWarmup));
    }

    private void AddPersonalBestReadingSteps()
    {
        AddUntilStep("second native preparation naturally starts chart time", () => !ReadState().Timing.Preparation.Active && clock.IsRunning);
        AddStep("verify the production PB query and schedule the second complete play", () =>
        {
            Assert.That(personalBestPlayCount, Is.EqualTo(2));
            // The converted BMS wrapper has a transient identity. Both the
            // production PB query and Player.ImportScore use the working map.
            Assert.That(player.Score.ScoreInfo.BeatmapInfo!.ID, Is.EqualTo(personalBestBeatmap.ID));
            Assert.That(ReadState().ChartPresentation?.PersonalBestScore, Is.EqualTo(personalBestSaved.TotalScore));
            Assert.That(player.Score.ScoreInfo.UserID, Is.EqualTo(personalBestSaved.UserID));
            Assert.That(player.Score.ScoreInfo.ModsJson, Is.EqualTo(personalBestSaved.ModsJson));
            GameplaySkinSceneRuntimeNode[] nodes = PersonalBestNodes();
            AssertPersonalBestVisible(nodes);
            settings.Write("personal-best-native-read.json", new
            {
                savedScore = PersonalBestScoreIdentity(personalBestSaved),
                nativeRead = ReadState().ChartPresentation,
                nativeQuery = "GameplaySkinEventRuntimeHost.preparePersonalBest",
                actualSecondPlayer = player.GetType().FullName,
                distinctPlayer = !ReferenceEquals(player, personalBestWarmup),
                sameBeatmapIdentity = player.Score.ScoreInfo.BeatmapInfo!.ID,
                convertedWrapperIdentity = player.GameplayState.Beatmap.BeatmapInfo.ID,
                currentUserMatches = player.Score.ScoreInfo.UserID == personalBestSaved.UserID,
                currentMods = player.Score.ScoreInfo.APIMods,
                sourceNodes = nodes.Select(NodeEvidence).ToArray(),
                injectedChartPresentation = false,
            });
            settings.Write("personal-best-second-input-plan.json", ((CaptureReadingPlayer)player).StartSyntheticChartInput());
        });
    }

    private void AddPersonalBestCrossingCapture()
    {
        AddUntilStep("the second actual score naturally exceeds the saved PB", () =>
            score.TotalScore.Value > personalBestSaved.TotalScore && ReadState().Score.Score > personalBestSaved.TotalScore
            && PersonalBestNodes().Any(node => SourceId(node).EndsWith(".best.difference", StringComparison.Ordinal)
                                               && EffectiveAlpha(node.ContentDrawable) > .01 && AuthorInteger(node) > 0));
        AddStep("capture real PB crossing, label and positive difference", () =>
        {
            Assert.That(Game.ScreenStack.CurrentScreen, Is.SameAs(player));
            GameplaySkinSceneRuntimeNode[] nodes = PersonalBestNodes();
            AssertPersonalBestVisible(nodes);
            long current = AuthorInteger(nodes.Single(node => SourceId(node).EndsWith(".current.value", StringComparison.Ordinal)));
            long difference = AuthorInteger(nodes.Single(node => SourceId(node).EndsWith(".best.difference", StringComparison.Ordinal)));
            Assert.That(difference, Is.EqualTo(current - personalBestSaved.TotalScore).And.GreaterThan(0));
            Assert.That(ReadState().ChartPresentation?.PersonalBestScore, Is.EqualTo(personalBestSaved.TotalScore));
            settings.Write("personal-best-crossing-native.json", new
            {
                savedScoreId = personalBestSaved.ID,
                savedTotal = personalBestSaved.TotalScore,
                actualScore = score.TotalScore.Value,
                boundCurrent = current,
                boundDifference = difference,
                nativeRead = ReadState().ChartPresentation,
                nodes = nodes.Select(NodeEvidence).ToArray(),
                sourceEventNotForced = true,
                numericalValuesInjected = false,
            });
            RecordGameplay("personal-best-crossing", checked((int)Math.Round(ReadProperty<double>(events, "AuthoritativeGameplayTime"))));
            screenshot = Capture("personal-best-crossing");
        });
        AddUntilStep("wait for real PB crossing framebuffer", () => screenshot?.IsCompleted == true);
        AddStep("propagate PB crossing framebuffer errors", () => screenshot!.GetAwaiter().GetResult());
    }

    private void ValidatePersonalBestCompletion()
    {
        var second = (CaptureReadingPlayer)player;
        AssertNativePersonalBestInputComplete(second);
        Assert.That(second.OmittedChords, Is.Empty, "The second play must retain the full original native input plan.");
        GameplaySkinEventStateSnapshot completed = completionState
            ?? throw new InvalidOperationException("The second Player did not retain its genuine completion snapshot.");
        ScoreInfo savedSecond = Game.ScoreManager.Query(saved => saved.ID == results.Score!.ID)
            ?? throw new InvalidOperationException("The second native completed score was not saved.");
        AssertPersonalBestStoredIdentity(savedSecond, results.Score!);
        Assert.That(savedSecond.ID, Is.Not.EqualTo(personalBestSaved.ID));
        Assert.That(savedSecond.TotalScore, Is.GreaterThan(personalBestSaved.TotalScore));
        Assert.That(completed.ChartPresentation?.PersonalBestScore, Is.EqualTo(personalBestSaved.TotalScore),
            "The active play must retain its loaded PB; saving the new best must not rewrite its comparison target.");
        settings.Write("personal-best-complete.json", new
        {
            settings.PackageSha256,
            first = PersonalBestScoreIdentity(personalBestSaved),
            second = PersonalBestScoreIdentity(savedSecond),
            nativeLoadedPersonalBest = completed.ChartPresentation?.PersonalBestScore,
            nativeReadMoment = "Actual second Player completion snapshot retained before its natural Results transition",
            deliveredSecondInputs = second.ChartInputObservations.ToArray(),
            secondReplayFrames = second.Score.Replay.Frames.Count,
            twoNaturallyCompletedPlays = true,
            firstScoreStoredByNativePlayer = true,
            secondScoreStoredByNativePlayer = true,
            realInputOmissionCreatedLowerPB = true,
            nativeQueryMatched = true,
            positiveDifferenceCaptured = captured.Contains("personal-best-crossing.png"),
            numericalValuesInjected = false,
        });
    }

    private void AssertPersonalBestStoredIdentity(ScoreInfo stored, ScoreInfo actual)
    {
        Assert.That(stored.ID, Is.EqualTo(actual.ID));
        Assert.That(stored.TotalScore, Is.EqualTo(actual.TotalScore));
        Assert.That(stored.Passed, Is.True);
        Assert.That(stored.Rank, Is.Not.EqualTo(ScoreRank.F));
        Assert.That(stored.TotalScoreVersion, Is.EqualTo(LegacyScoreEncoder.LATEST_VERSION));
        Assert.That(stored.MaximumStatistics, Is.Not.Empty);
        Assert.That(stored.GetRulesetData<BmsScoreInfoData>(), Is.Not.Null);
        Assert.That(stored.GetRulesetData<BmsScoreInfoData>()!.Version, Is.EqualTo(BmsScoreInfoData.TOTAL_RULES_VERSION));
        Assert.That(stored.GetRulesetData<BmsScoreInfoData>()!.HasResultStatistics, Is.True);
        Assert.That(stored.BeatmapInfo!.ID, Is.EqualTo(personalBestBeatmap.ID));
        Assert.That(stored.BeatmapHash, Is.EqualTo(personalBestBeatmap.Hash));
        Assert.That(stored.UserID, Is.EqualTo(Game.Dependencies.Get<IAPIProvider>().LocalUser.Value.Id));
        Assert.That(stored.Ruleset.ShortName, Is.EqualTo("bms"));
        Assert.That(stored.Mods.Any(mod => mod is ModAutoplay or ModNoFail), Is.False);
        Assert.That(stored.BackgroundReprocessingFailed || stored.DeletePending, Is.False);
    }

    private static object PersonalBestScoreIdentity(ScoreInfo saved) => new
    {
        saved.ID, saved.Hash, saved.TotalScore, saved.TotalScoreVersion, saved.MaxCombo, saved.Passed, saved.UserID,
        beatmapId = saved.BeatmapInfo!.ID, saved.BeatmapHash, ruleset = saved.Ruleset.ShortName,
        mods = saved.APIMods, statistics = saved.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
        maximumStatistics = saved.MaximumStatistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
        bms = saved.GetRulesetData<BmsScoreInfoData>(),
    };

    private static void AssertNativePersonalBestInputComplete(CaptureReadingPlayer actual)
    {
        Assert.That(actual.NativeRuleset.HasReplayLoaded.Value, Is.False);
        Assert.That(actual.NativeRuleset.Mods.Any(mod => mod is ModAutoplay or ModNoFail), Is.False);
        Assert.That(actual.DeliveredInputFrames, Is.EqualTo(actual.PlannedInputFrames));
        Assert.That(actual.HeldChartActions, Is.Empty);
        Assert.That(actual.ChartInputObservations.All(item => item.DeliveredTime >= item.IntendedTime
                                                            && item.DeliveredTime - item.IntendedTime <= 50), Is.True);
        Assert.That(actual.Score.Replay.Frames, Is.Not.Empty);
    }

    private GameplaySkinSceneRuntimeNode[] PersonalBestNodes()
        => ReadingNodes().Where(node => SourceId(node).Contains(".best.", StringComparison.Ordinal)
                                       || SourceId(node).EndsWith(".current.value", StringComparison.Ordinal)).ToArray();

    private static string SourceId(GameplaySkinSceneRuntimeNode node)
        => ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source.Id;

    private static long AuthorInteger(GameplaySkinSceneRuntimeNode node)
        => long.Parse(ReadProperty<string>(node.ContentDrawable, "AuthorText"), CultureInfo.InvariantCulture);

    private void AssertPersonalBestVisible(GameplaySkinSceneRuntimeNode[] nodes)
    {
        GameplaySkinSceneRuntimeNode label = nodes.Single(node => SourceId(node).EndsWith(".best.label", StringComparison.Ordinal));
        Assert.That(label.ContentDrawable, Is.TypeOf<Sprite>());
        Assert.That(((Sprite)label.ContentDrawable).Texture, Is.Not.Null);
        Assert.That(EffectiveAlpha(label.ContentDrawable), Is.GreaterThan(.01));
        GameplaySkinSceneRuntimeNode value = nodes.Single(node => SourceId(node).EndsWith(".best.value", StringComparison.Ordinal));
        Assert.That(AuthorInteger(value), Is.EqualTo(personalBestSaved.TotalScore));
        Assert.That(EffectiveAlpha(value.ContentDrawable), Is.GreaterThan(.01));
    }

    private partial class CaptureReadingPlayer
    {
        public sealed record OmittedNativeAction(string Action, int LaneIndex, bool IsScratch, double From, double Until, string HitObjectType);
        public sealed record OmittedNativeChord(int PressFrame, int ReleaseFrame, double From, double Until, OmittedNativeAction[] Actions)
        {
            public int ExpectedMisses => Actions.Length;
        }

        public bool OmitFirstTwoChordsForPersonalBest { get; init; }
        public IReadOnlyList<OmittedNativeChord> OmittedChords { get; private set; } = Array.Empty<OmittedNativeChord>();
        public int ExpectedPersonalBestMisses => OmittedChords.Sum(chord => chord.ExpectedMisses);

        private void OmitFirstTwoNativeChordsForPersonalBest(IBeatmap beatmap)
        {
            Assert.That(IsPersonalBestCase, Is.True);
            var omissions = new List<OmittedNativeChord>();
            int afterRelease = 0;
            for (int chord = 0; chord < 2; chord++)
            {
                int start = Array.FindIndex(inputPlan, afterRelease, frame => frame.Actions.Count > 0);
                Assert.That(start, Is.GreaterThanOrEqualTo(0));
                int end = Array.FindIndex(inputPlan, start + 1, frame => frame.Actions.Count == 0);
                Assert.That(end, Is.GreaterThan(start));
                double from = inputPlan[start].Time;
                double until = inputPlan[end].Time;
                Assert.That(until - from, Is.EqualTo(20), "Only the original complete 20 ms short-note press may be omitted.");
                BmsAction[] actions = inputPlan[start].Actions.ToArray();
                Assert.That(actions.Distinct().Count(), Is.EqualTo(actions.Length));
                Assert.That(inputPlan.Skip(start).Take(end - start).All(frame => frame.Actions.ToHashSet().SetEquals(actions)), Is.True,
                    "No different press or release may be hidden inside the selected chord interval.");
                BmsHitObject[] notes = beatmap.HitObjects.OfType<BmsHitObject>().Where(note => note.StartTime == from).ToArray();
                Assert.That(notes, Is.Not.Empty);
                Assert.That(notes.All(note => note.GetType() == typeof(BmsHitObject) && note.CountsForScore && !note.AutoPlay), Is.True,
                    "The warmup must omit ordinary native scored notes, never a long-note head or tail.");
                Assert.That(notes.Select(note => BmsActionExtensions.GetLaneAction(note.LaneIndex, note.IsScratch)), Is.EquivalentTo(actions));
                omissions.Add(new OmittedNativeChord(start, end, from, until, notes.Select(note => new OmittedNativeAction(
                    BmsActionExtensions.GetLaneAction(note.LaneIndex, note.IsScratch).ToString(), note.LaneIndex, note.IsScratch,
                    from, until, note.GetType().FullName!)).ToArray()));
                for (int index = start; index < end; index++)
                    inputPlan[index] = new BmsReplayFrame(inputPlan[index].Time, Array.Empty<BmsAction>());
                afterRelease = end + 1;
            }

            OmittedChords = omissions;
            int expected = CaptureSettings.Current.Keys switch
            {
                5 => 6,
                7 => 8,
                14 => 16,
                _ => throw new InvalidOperationException("Unvalidated native personal-best key count."),
            };
            Assert.That(ExpectedPersonalBestMisses, Is.EqualTo(expected));
        }
    }
}
