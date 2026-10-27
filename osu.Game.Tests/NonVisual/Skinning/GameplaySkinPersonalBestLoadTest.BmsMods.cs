// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public partial class GameplaySkinPersonalBestLoadTest
    {
        private BmsNativeHost bmsLoaded = null!;

        [TestCase("score.personal-best", "scratch-tint")]
        [TestCase("score.pacemaker-next", "scratch-tint")]
        [TestCase("score.pacemaker-next", "note-tint")]
        [TestCase("score.personal-best", "covers-to-none")]
        [TestCase("score.pacemaker-next", "none-to-covers")]
        [TestCase("score.personal-best", "explicit-default")]
        public void TestBmsQueryReusesExistingRealmScoresAcrossDisplayOnlyChoices(string consumer, string difference)
        {
            Mod[] current = bmsCurrentMods(difference);
            Guid savedId = default;
            string persistedMods = string.Empty;
            useBmsBeatmap();
            AddStep("persist a real BMS scored model before changing display-only preferences", () =>
            {
                Mod[] previous = difference switch
                {
                    "scratch-tint" => new Mod[] { new BmsModAutoScratch() },
                    "note-tint" => new Mod[] { new BmsModAutoNote() },
                    "covers-to-none" => coverChoices(),
                    _ => current,
                };
                if (difference == "none-to-covers")
                    previous = Array.Empty<Mod>();
                ScoreInfo played = createBmsScoredModel(previous, HitResult.Great);
                if (difference == "explicit-default")
                {
                    APIMod[] oldSettings = played.APIMods;
                    oldSettings.Single(mod => mod.Acronym == "ASCR").Settings.Add("scratch_visibility", 0);
                    played.APIMods = oldSettings;
                }
                expected = played.TotalScore;
                savedId = save(played);
                persistedMods = database!.Run(r => r.Find<ScoreInfo>(savedId)!.ModsJson);
                Assert.That(expected, Is.GreaterThan(0));
            });
            mountBms(consumer, current);
            AddStep("the production query supplies the existing PB and NEXT without rewriting its mods", () =>
            {
                GameplaySkinEventStateSnapshot state = readBmsState();
                Assert.That(state.ChartPresentation!.PersonalBestScore, Is.EqualTo(expected));
                Assert.That(state.ChartPresentation.UsesExScore, Is.True);
                Assert.That(GameplaySkinPacemakerNext.Calculate(true, state.Score.MaximumScore, state.ChartPresentation.PersonalBestScore).Available, Is.True);
                Assert.That(database!.Run(r => r.Find<ScoreInfo>(savedId)!.ModsJson), Is.EqualTo(persistedMods));
                database.Dispose();
            });
            AddRepeatStep("advance after the actual Realm has been closed", () => bmsLoaded.ClockHost.Sample(1500), 3);
            AddStep("reset and late attachment retain the same once-loaded target", () =>
            {
                bmsLoaded.Events.RequestReset(GameplaySkinEventResetReason.ConsumerRebuilt);
                bmsLoaded.ClockHost.Sample(1500);
                Assert.That(readBmsState().ChartPresentation!.PersonalBestScore, Is.EqualTo(expected));
            });
        }

        [TestCase("assist-presence")]
        [TestCase("assist-kind")]
        [TestCase("assist-visibility")]
        [TestCase("judge")]
        [TestCase("gauge")]
        [TestCase("cn")]
        [TestCase("hcn")]
        [TestCase("mirror")]
        [TestCase("random-seed")]
        [TestCase("unknown-mod")]
        [TestCase("unknown-assist-setting")]
        [TestCase("unknown-cover-setting")]
        public void TestBmsHigherSavedScoreWithIncompatibleGameplayOrUnknownSettingsCannotReplacePb(string difference)
        {
            Mod[] current = bmsCurrentMods(difference);
            useBmsBeatmap();
            AddStep("persist a valid PB and a genuinely higher incompatible BMS model", () =>
            {
                ScoreInfo valid = createBmsScoredModel(current, HitResult.Great);
                expected = valid.TotalScore;
                save(valid);
                Mod[] incompatible = difference switch
                {
                    "assist-presence" => Array.Empty<Mod>(),
                    "assist-kind" => new Mod[] { new BmsModAutoNote() },
                    "assist-visibility" => new Mod[] { new BmsModAutoScratch { ScratchVisibility = { Value = AutoScratchVisibility.Hidden } } },
                    "judge" => new Mod[] { new BmsModAutoScratch(), new BmsModJudgeIidx() },
                    "gauge" => new Mod[] { new BmsModAutoScratch(), new BmsModGaugeHard() },
                    "cn" => new Mod[] { new BmsModAutoScratch(), new BmsModChargeNote() },
                    "hcn" => new Mod[] { new BmsModAutoScratch(), new BmsModHellChargeNote() },
                    "mirror" => new Mod[] { new BmsModAutoScratch(), new BmsModMirror() },
                    "random-seed" => new Mod[] { new BmsModAutoScratch(), new BmsModRandom { Seed = { Value = 456 } } },
                    _ => current,
                };
                ScoreInfo higher = createBmsScoredModel(incompatible, HitResult.Perfect);
                Assert.That(higher.TotalScore, Is.GreaterThan(expected));
                if (difference.StartsWith("unknown-", StringComparison.Ordinal))
                {
                    APIMod[] previous = higher.APIMods;
                    switch (difference)
                    {
                        case "unknown-mod":
                            previous = previous.Append(new APIMod { Acronym = "FUTURE" }).ToArray();
                            break;
                        case "unknown-assist-setting":
                            previous.Single(mod => mod.Acronym == "ASCR").Settings.Add("future_rule", 1);
                            break;
                        case "unknown-cover-setting":
                            previous = previous.Append(new APIMod { Acronym = "SUD", Settings = { ["cover_percent"] = 700, ["future_rule"] = 1 } }).ToArray();
                            break;
                    }
                    higher.APIMods = previous;
                }
                save(higher);
            });
            mountBms("score.pacemaker-next", current);
            AddAssert("only the compatible lower PB supplies the NEXT comparison", () => readBmsState().ChartPresentation!.PersonalBestScore == expected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBmsDisplayProjectionDoesNotBypassRulesVersionOrResultStatistics(bool missingStatistics)
        {
            Mod[] current = bmsCurrentMods("scratch-tint");
            useBmsBeatmap();
            AddStep("persist current valid data and a higher visually equivalent but obsolete model", () =>
            {
                ScoreInfo valid = createBmsScoredModel(current, HitResult.Great);
                expected = valid.TotalScore;
                save(valid);
                ScoreInfo old = createBmsScoredModel(new Mod[] { new BmsModAutoScratch() }, HitResult.Perfect);
                BmsScoreInfoData data = old.GetRulesetData<BmsScoreInfoData>()!;
                if (missingStatistics)
                    data.FinalGauge = null;
                else
                    data.Version = BmsScoreInfoData.TOTAL_RULES_VERSION - 1;
                old.SetRulesetData(data);
                save(old);
            });
            mountBms("score.personal-best", current);
            AddAssert("the existing BMS saved-score qualification remains mandatory", () => readBmsState().ChartPresentation!.PersonalBestScore == expected);
        }

        [Test]
        public void TestNonBmsPersonalBestStillMatchesTheCompleteApiModSettingsExactly()
        {
            AddStep("save a mania model carrying a different exact setting payload", () =>
            {
                ScoreInfo played = createScoredModel(HitResult.Perfect);
                APIMod[] oldSettings = played.APIMods;
                oldSettings[0].Settings.Add("tint_scratch_notes", false);
                played.APIMods = oldSettings;
                save(played);
            });
            mount("score.personal-best-available");
            AddAssert("the BMS query policy is not silently applied to mania", () => readPersonalBest() == null);
        }

        private void useBmsBeatmap()
            => AddStep("use a BMS ruleset identity in the real isolated score database", () => database!.Write(r =>
            {
                BeatmapInfo stored = r.Find<BeatmapInfo>(scoreBeatmap.ID)!;
                stored.Ruleset = r.Find<RulesetInfo>(BmsRuleset.SHORT_NAME)!;
                scoreBeatmap = stored.Detach();
            }));

        private static Mod[] bmsCurrentMods(string difference)
            => difference switch
            {
                "scratch-tint" => new Mod[] { new BmsModAutoScratch { TintScratchNotes = { Value = false } } },
                "note-tint" => new Mod[] { new BmsModAutoNote { TintNotes = { Value = false } } },
                "covers-to-none" => Array.Empty<Mod>(),
                "none-to-covers" => coverChoices(),
                "random-seed" => new Mod[] { new BmsModAutoScratch(), new BmsModRandom { Seed = { Value = 123 } } },
                _ => new Mod[] { new BmsModAutoScratch() },
            };

        private static Mod[] coverChoices()
            => new Mod[]
            {
                new BmsModSudden { CoverPercent = { Value = 700 }, CoverOpacity = { Value = 200 }, RememberGameplayChanges = { Value = false } },
                new BmsModHidden { CoverPercent = { Value = 100 }, RememberGameplayChanges = { Value = false } },
                new BmsModLift { LiftUnits = { Value = 333 }, RememberGameplayChanges = { Value = false } },
            };

        private ScoreInfo createBmsScoredModel(Mod[] mods, HitResult result)
        {
            BmsBeatmap beatmap = bmsChart(scoreBeatmap);
            foreach (BmsHitObject note in beatmap.HitObjects.OfType<BmsHitObject>())
                note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            using var processor = new BmsScoreProcessor();
            processor.Mods.Value = mods;
            processor.ApplyBeatmap(beatmap);
            var played = new ScoreInfo(scoreBeatmap, scoreBeatmap.Ruleset)
            {
                User = API.LocalUser.Value,
                Hash = Guid.NewGuid().ToString("N"),
                Mods = mods,
            };
            using var recorder = new BmsReplayRecorder(new Score { ScoreInfo = played });
            foreach (BmsHitObject note in beatmap.HitObjects.OfType<BmsHitObject>().Where(note => note.CountsForScore))
                processor.ApplyResult(new JudgementResult(note, note.Judgement) { Type = result });
            processor.PopulateScore(played);
            new BmsRuleset().PrepareScoreInfoForResults(played, beatmap);
            Assert.That(played.GetRulesetData<BmsScoreInfoData>(), Is.Not.Null);
            Assert.That(played.GetRulesetData<BmsScoreInfoData>()!.Version, Is.EqualTo(BmsScoreInfoData.TOTAL_RULES_VERSION));
            Assert.That(played.GetRulesetData<BmsScoreInfoData>()!.HasResultStatistics, Is.True);
            return played;
        }

        private static BmsBeatmap bmsChart(BeatmapInfo info)
            => new BmsBeatmap
            {
                BeatmapInfo = info,
                HitObjects =
                {
                    new BmsHitObject { StartTime = 1000, LaneIndex = 1, Keymode = BmsKeymode.Key7K },
                    new BmsHitObject { StartTime = 2000, LaneIndex = 0, IsScratch = true, Keymode = BmsKeymode.Key7K },
                    new BmsHitObject { StartTime = 3000, LaneIndex = 2, Keymode = BmsKeymode.Key7K },
                    new BmsHitObject { StartTime = 4000, LaneIndex = 0, IsScratch = true, Keymode = BmsKeymode.Key7K },
                },
            };

        private void mountBms(string consumer, Mod[] mods)
        {
            AddStep("mount BMS scoring with its query-only mod projection and actual BMS publication", () => Child = bmsLoaded = new BmsNativeHost(database!, scoreBeatmap, consumer, mods));
            AddUntilStep("the real BMS Realm loading chain completes", () => bmsLoaded.Events?.IsLoaded == true);
        }

        private GameplaySkinEventStateSnapshot readBmsState()
        {
            using GameplaySkinEventSubscription subscription = bmsLoaded.Events.EventStream.Subscribe();
            Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
            return ((GameplaySkinStateEventPayload)envelope!.Payload).State;
        }

        private partial class BmsNativeHost : Container
        {
            [Cached]
            private readonly RealmAccess database;

            [Cached(typeof(ScoreProcessor))]
            private readonly BmsScoreProcessor score = new BmsScoreProcessor();

            private readonly BeatmapInfo info;
            private readonly string consumer;
            private readonly Mod[] mods;
            private GameplaySkinLayoutPublication? publication;
            public NativeClock ClockHost { get; } = new NativeClock();
            public GameplaySkinEventRuntimeHost Events { get; private set; } = null!;

            public BmsNativeHost(RealmAccess database, BeatmapInfo info, string consumer, Mod[] mods)
            {
                this.database = database;
                this.info = info;
                this.consumer = consumer;
                this.mods = mods;
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                BmsBeatmap beatmap = bmsChart(info);
                foreach (BmsHitObject note in beatmap.HitObjects.OfType<BmsHitObject>())
                    note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                score.Mods.Value = mods;
                score.ApplyBeatmap(beatmap);
                publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(renderer.WhitePixel, true, bindingSource: consumer, rulesetId: "bms");
                Events = new GameplaySkinEventRuntimeHost(publication, beatmap, null, null, usesExScore: true, scoreBeatmap: info,
                    validSavedScore: saved => saved.GetRulesetData<BmsScoreInfoData>() is { Version: BmsScoreInfoData.TOTAL_RULES_VERSION, HasResultStatistics: true },
                    personalBestModProjection: BmsPersonalBestModComparison.Normalize);
                ClockHost.Child = Events;
                Child = ClockHost;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                score.Dispose();
                publication?.Dispose();
            }
        }
    }
}
