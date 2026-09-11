// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Enters the real PlayerLoader under a nested game, without OsuTestScene's optional ruleset dependency scope.
    /// The production RulesetConfigCache must be the sole upstream source of the final ruleset configuration.
    /// </summary>
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsPlayerSkinEntry : OsuGameTestScene
    {
        private TextWriter diagnosticOutput = TextWriter.Null;

        [SetUp]
        public void SetUpDiagnostics()
        {
            diagnosticOutput = TestContext.Out;
        }

        [TearDown]
        public void TearDownDiagnostics() => Logger.NewEntry -= logEntry;

        private void logEntry(LogEntry entry)
        {
            if (entry.Level == LogLevel.Error || entry.Exception != null)
                diagnosticOutput.WriteLine($"Actual game entry error: {entry.Message}{Environment.NewLine}{entry.Exception}");
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestRealPlayerAndAutoplayPreviewUseFinalConfigurationAndRetry(bool mania, bool autoplayPreview)
        {
            Ruleset ruleset = mania ? new ManiaRuleset() : new BmsRuleset();
            Player player = null!;
            Player firstPlayer = null!;
            GameplaySkinLayoutSnapshot firstSnapshot = null!;
            IRulesetConfigCache configs = null!;

            AddStep("set actual game configuration without a concrete parent config", () =>
            {
                // Subscribe after the nested game establishes its logging environment, before loading Player.
                Logger.NewEntry += logEntry;
                // Do not override CreateRuleset(): doing so would add DrawableRulesetDependencies to the outer
                // test scene and make the production player's missing upstream config appear to work.
                Assert.That(Dependencies.Get<BmsRulesetConfigManager>(), Is.Null);
                Assert.That(Dependencies.Get<ManiaRulesetConfigManager>(), Is.Null);
                Assert.That(Game.Dependencies.Get<BmsRulesetConfigManager>(), Is.Null);
                Assert.That(Game.Dependencies.Get<ManiaRulesetConfigManager>(), Is.Null);
                configs = Game.Dependencies.Get<IRulesetConfigCache>();
                Assert.That(configs, Is.TypeOf<RulesetConfigCache>());

                if (mania)
                    ((ManiaRulesetConfigManager)configs.GetConfigFor(ruleset)!).SetValue(ManiaRulesetSetting.ScrollDirection, ManiaScrollingDirection.Up);
                else
                    ((BmsRulesetConfigManager)configs.GetConfigFor(ruleset)!).SetValue(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.P2);

                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(createEntryBeatmap(ruleset, mania));
                Game.SelectedMods.Value = autoplayPreview ? new[] { ruleset.GetAutoplayMod()! } : Array.Empty<Mod>();
                DismissAnyNotifications();
            });

            AddStep("enter the real loading screen", () => Game.ScreenStack.Push(new PlayerLoader(() =>
            {
                // This is the same factory choice used by SoloSongSelect for normal play and Ctrl+Enter preview.
                player = autoplayPreview
                    ? new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)
                    : mania ? new SoloPlayer() : new BmsSoloPlayer();
                return player;
            })));

            addUntilPlayerReady(() => player);
            AddStep("check final configuration and exact published layout", () =>
            {
                firstPlayer = player;
                firstSnapshot = assertCurrentEntry(player, ruleset, configs, mania, autoplayPreview);
            });
            AddStep("retry through the actual parent loader", () => Assert.That(player.Restart(true), Is.True));
            AddUntilStep("loader constructs the replacement player", () => player != firstPlayer);
            addUntilPlayerReady(() => player);
            AddStep("retry retains settings and creates a fresh exact layout", () =>
                Assert.That(assertCurrentEntry(player, ruleset, configs, mania, autoplayPreview), Is.Not.SameAs(firstSnapshot)));
            AddStep("exit through the actual gameplay exit action", () => player.ChildrenOfType<HotkeyExitOverlay>().Single().Action());
            AddUntilStep("return from player and loader to the menu", () => Game.ScreenStack.CurrentScreen is MainMenu);
        }

        private void addUntilPlayerReady(Func<Player> getPlayer)
        {
            AddUntilStep("actual player and ruleset finish loading", () =>
            {
                DismissAnyNotifications();
                Player player = getPlayer();
                return player != null
                       && ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                       && player.IsLoaded
                       && player.LoadedBeatmapSuccessfully
                       && player.ChildrenOfType<DrawableRuleset>().SingleOrDefault()?.IsLoaded == true;
            });
        }

        private GameplaySkinLayoutSnapshot assertCurrentEntry(Player player, Ruleset ruleset, IRulesetConfigCache configs, bool mania, bool autoplayPreview)
        {
            Assert.That(player.Dependencies.Get<BmsRulesetConfigManager>(), Is.Null);
            Assert.That(player.Dependencies.Get<ManiaRulesetConfigManager>(), Is.Null);
            Assert.That(player, autoplayPreview ? Is.TypeOf<ReplayPlayer>() : mania ? Is.TypeOf<SoloPlayer>() : Is.TypeOf<BmsSoloPlayer>());

            DrawableRuleset drawable = player.ChildrenOfType<DrawableRuleset>().Single();
            var owner = drawable.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
            var skins = Game.Dependencies.Get<SkinManager>();
            Assert.That(owner.CurrentPublication, Is.Not.Null);
            Assert.That(owner.PackageRevision.RetainsExact(skins.CurrentRevision), Is.True);
            GameplaySkinLayoutSnapshot snapshot = owner.CurrentPublication!.Snapshot;

            if (mania)
            {
                var renderer = (DrawableManiaRuleset)drawable;
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.Dependencies.Get<ManiaRulesetConfigManager>(), Is.SameAs(configs.GetConfigFor(ruleset)));
                    Assert.That(renderer.LayoutSnapshot, Is.SameAs(snapshot));
                    Assert.That(snapshot.Context.ScrollDirection, Is.EqualTo(GameplaySkinScrollDirection.Up));
                    Assert.That(renderer.PublishedDirection, Is.EqualTo(ScrollingDirection.Up));
                    Assert.That(renderer.Playfield.Stages.All(stage => stage.IsLoaded && stage.Columns.All(column => column.IsLoaded)), Is.True);
                });
            }
            else
            {
                var renderer = (DrawableBmsRuleset)drawable;
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.Dependencies.Get<BmsRulesetConfigManager>(), Is.SameAs(configs.GetConfigFor(ruleset)));
                    Assert.That(renderer.LayoutSnapshot.Neutral, Is.SameAs(snapshot));
                    Assert.That(renderer.LayoutSnapshot.Style, Is.EqualTo(BmsPlayfieldStyle.P2));
                    Assert.That(renderer.Playfield.LayoutSnapshot, Is.SameAs(renderer.LayoutSnapshot));
                    Assert.That(renderer.Playfield.Lanes.All(lane => lane.IsLoaded), Is.True);
                    Assert.That(renderer.HudLayoutSnapshot, Is.SameAs(renderer.LayoutSnapshot));
                    Assert.That(renderer.BgaLayoutSnapshot, Is.SameAs(renderer.LayoutSnapshot));
                });
            }

            var reload = skins.ReloadCurrentRevisionAsync();
            Assert.That(reload.IsCompletedSuccessfully, Is.True);
            Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.LiveGameplayActive));
            Assert.That(owner.CurrentPublication.Snapshot, Is.SameAs(snapshot));
            return snapshot;
        }

        private static IBeatmap createEntryBeatmap(Ruleset ruleset, bool mania)
        {
            if (mania)
            {
                return new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    Difficulty = { CircleSize = 4 },
                    HitObjects = { new Note { Column = 0, StartTime = 60_000 } },
                };
            }

            var decoded = new BmsBeatmapDecoder().DecodeText("#TITLE Actual player skin entry\n#BPM 120\n#WAV01 note.wav\n#03011:01\n#03016:01\n", "actual-player-skin-entry.bme");
            return new BmsDecodedBeatmap(decoded)
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
        }
    }
}
