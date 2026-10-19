// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsPlayerLayoutPreparationFailure : OsuGameTestScene
    {
        [Cached]
        private readonly PreparationProbe preparation = new PreparationProbe();

        [TestCase(GameplaySkinLayoutPreparationResult.Rejected, 1)]
        [TestCase(GameplaySkinLayoutPreparationResult.Retry, 8)]
        public void TestFailedLayoutReturnsToMenuWithoutGameplayOrLeakedSubtree(GameplaySkinLayoutPreparationResult result, int expectedAttempts)
        {
            ProbePlayer player = null!;
            ProbeLoader loader = null!;
            SkinCurrentRevision revision = null!;
            int participantCount = 0;
            int scoreCount = 0;
            int resultsShown = 0;

            AddStep("select the controlled BMS layout preparation result", () =>
            {
                preparation.Result = result;
                preparation.Attempts = 0;
                preparation.Owner = null;
                preparation.UnattachedChild = null;

                var ruleset = new LayoutFailureBmsRuleset();
                // RulesetInfo equality uses ShortName. Cross the other supported mode so the real game accepts
                // this test BMS instantiation instead of retaining an equal, previously selected BMS instance.
                Game.Ruleset.Value = new ManiaRuleset().RulesetInfo;
                Game.Ruleset.Value = ruleset.RulesetInfo;
                var decoded = new BmsBeatmapDecoder().DecodeText("#TITLE Failed layout entry\n#BPM 120\n#WAV01 note.wav\n#03011:01\n", "failed-layout-entry.bme");
                Game.Beatmap.Value = CreateWorkingBeatmap(new BmsDecodedBeatmap(decoded)
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                });
                Game.SelectedMods.Value = Array.Empty<Mod>();
                revision = Game.Dependencies.Get<SkinManager>().CurrentRevision;
                participantCount = revision.ParticipantLeaseCount;
                scoreCount = Game.Realm.Run(realm => realm.All<ScoreInfo>().Count());
                DismissAnyNotifications();
            });
            AddStep("enter the actual player loader", () => Game.ScreenStack.Push(loader = new ProbeLoader(() =>
            {
                player = new ProbePlayer();
                player.OnShowingResults += () => resultsShown++;
                return player;
            })));
            AddUntilStep("failed player load completes normally", () => loader.CompletedPlayerLoad);
            AddUntilStep("loader returns to the menu and reclaims the player", () =>
                Game.ScreenStack.CurrentScreen is MainMenu && player.Disposed);
            AddUntilStep("layout failure is reported with a retry notification", () =>
                Game.Notifications.AllNotifications.OfType<SimpleErrorNotification>()
                    .Any(notification => notification.Text == PlayerLoaderOmsStrings.GameplaySkinLayoutPreparationFailed));
            AddUntilStep("failed layout owner and gameplay participants are released", () =>
                preparation.Owner?.IsDisposed == true && revision.ParticipantLeaseCount == participantCount);
            AddAssert("rejected renderer subtree is reclaimed without being loaded", () =>
                preparation.UnattachedChild is { Disposed: true, IsLoaded: false });
            AddStep("no incomplete gameplay or score is published", () => Assert.Multiple(() =>
            {
                Assert.That(preparation.Attempts, Is.EqualTo(expectedAttempts));
                Assert.That(preparation.Owner!.CurrentPublication, Is.Null);
                Assert.That(player.LoadedBeatmapSuccessfully, Is.False);
                Assert.That(player.ImportedScores, Is.Zero);
                Assert.That(resultsShown, Is.Zero);
                Assert.That(Game.Realm.Run(realm => realm.All<ScoreInfo>().Count()), Is.EqualTo(scoreCount));
            }));
        }

        private sealed class PreparationProbe
        {
            public GameplaySkinLayoutPreparationResult Result;
            public int Attempts;
            public GameplaySkinLayoutRevisionOwner? Owner;
            public DisposalProbe? UnattachedChild;
        }

        public sealed class LayoutFailureBmsRuleset : BmsRuleset, IGameplaySkinLayoutPreparer
        {
            GameplaySkinLayoutPreparationResult IGameplaySkinLayoutPreparer.PrepareGameplaySkinLayout(
                IBeatmap beatmap,
                IReadOnlyDependencyContainer dependencies,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var probe = dependencies.Get<PreparationProbe>();
                if (Interlocked.Increment(ref probe.Attempts) == 1)
                {
                    probe.Owner = dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                    // The production renderer already exists, but its subtree must stay unloaded until preparation
                    // succeeds. This child proves ownership survives rejection before that subtree is attached.
                    dependencies.Get<DrawableRuleset>().FrameStableComponents.Add(probe.UnattachedChild = new DisposalProbe());
                }
                return probe.Result;
            }
        }

        private partial class DisposalProbe : Drawable
        {
            public bool Disposed => IsDisposed;
        }

        private partial class ProbePlayer : BmsSoloPlayer
        {
            public bool Disposed => IsDisposed;
            public int ImportedScores { get; private set; }

            protected override Task ImportScore(Score score)
            {
                ImportedScores++;
                return Task.CompletedTask;
            }
        }

        private partial class ProbeLoader : PlayerLoader
        {
            public bool CompletedPlayerLoad { get; private set; }
            protected override double PlayerPushDelay => 0;
            protected override bool ReadyForGameplay => true;

            public ProbeLoader(Func<Player> createPlayer)
                : base(createPlayer)
            {
            }

            protected override void OnPlayerLoaded() => CompletedPlayerLoad = true;
        }
    }
}
