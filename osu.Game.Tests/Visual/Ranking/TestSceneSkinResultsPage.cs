// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Pages;
using osu.Game.Tests.NonVisual.Skinning;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>Ordinary import, real per-key selection and the actual results screen; no player's storage is opened.</summary>
    [HeadlessTest]
    public partial class TestSceneSkinResultsPage : OsuGameTestScene
    {
        private SkinManager skins => Game.Dependencies.Get<SkinManager>();
        private Live<SkinInfo> keyed = null!;
        private Live<SkinInfo> unrelated = null!;
        private SkinManagedFolderOperationCoordinator.Lease? heldMutation;

        [TearDownSteps]
        public override void TearDownSteps()
        {
            AddStep("release any held package read reservation", () =>
            {
                heldMutation?.Dispose();
                heldMutation = null;
            });
            base.TearDownSteps();
        }

        [TestCase(false, 7)]
        [TestCase(false, 14)]
        [TestCase(true, 7)]
        public void TestActualResultsUsesItsScoreScopeAndKeepsNativePageAndActions(bool bms, int keys)
        {
            Ruleset ruleset = bms ? new BmsRuleset() : new ManiaRuleset();
            var score = createScore(ruleset, keys, "First real score", true, 37);
            PageResultsScreen screen = null!;
            Skin originalOwner = null!;
            SkinCurrentRevision originalRevision = null!;
            CollectionButton collection = null!;
            arrangeSkins(ruleset, keys, true);
            AddStep("retain the unrelated global owner", () =>
            {
                originalOwner = skins.CurrentSkin.Value;
                originalRevision = skins.CurrentRevision;
            });
            PushAndConfirm(() => screen = new PageResultsScreen(score));
            AddUntilStep("the actual score's keyed package is prepared", () => screen.SkinPageAvailable);
            AddStep("native results remain the default and original actions remain reachable", () =>
            {
                Assert.That(screen.ShowingSkinPage, Is.False);
                Assert.That(screen.ChildrenOfType<SkinResultsPageDisplay>(), Is.Empty);
                Assert.That(screen.ResultsPageSkin!.SkinInfo.ID, Is.EqualTo(keyed.ID));
                Assert.That(screen.SelectedScore.Value, Is.SameAs(score));
                Assert.That(screen.ChildrenOfType<ScorePanel>(), Is.Not.Empty);
                collection = screen.ChildrenOfType<CollectionButton>().Single();
                Assert.That(screen.ChildrenOfType<ReplayDownloadButton>(), Is.Not.Empty);
            });
            AddStep("switch through the visible page button", () =>
            {
                var button = screen.ChildrenOfType<RoundedButton>().Single(button => button.Text.ToString() == "皮肤界面");
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("the real skin page and its pixels are mounted", () => screen.ShowingSkinPage
                && screen.ChildrenOfType<SkinResultsPageDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("page reads this score's real metadata and judgement count", () =>
                screen.ChildrenOfType<SkinResultsPageDisplay>().Single().ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "First real score")
                && screen.ChildrenOfType<SkinResultsPageDisplay>().Single().ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "37"));
            AddAssert("captured image material was actually loaded", () => screen.ChildrenOfType<SkinResultsPageDisplay>().Single()
                .ChildrenOfType<Sprite>().Any(sprite => sprite.Texture?.Width == 4 && sprite.Texture.Height == 3));
            AddAssert("passed background alone is visible", () => background(screen, "frame").Alpha == 1 && background(screen, "failed").Alpha == 0);
            AddStep("live refresh cannot publish across the mounted page", () =>
            {
                Task<SkinCurrentRevisionReloadResult> reload = skins.ReloadCurrentRevisionAsync();
                Assert.That(reload.IsCompleted, Is.True);
                Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.ParticipantRejected));
                Assert.That(skins.CurrentSkin.Value, Is.SameAs(originalOwner));
                Assert.That(skins.CurrentRevision, Is.SameAs(originalRevision));
            });
            AddStep("return through the original page button", () =>
            {
                var button = screen.ChildrenOfType<RoundedButton>().Single(button => button.Text.ToString() == "原界面");
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("skin visuals are detached", () => !screen.ShowingSkinPage && !screen.ChildrenOfType<SkinResultsPageDisplay>().Any());
            AddAssert("native selection, collection and global skin are unchanged", () =>
                ReferenceEquals(screen.SelectedScore.Value, score)
                && ReferenceEquals(screen.ChildrenOfType<CollectionButton>().Single(), collection)
                && ReferenceEquals(skins.CurrentSkin.Value, originalOwner)
                && skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope(ruleset.ShortName, keys)) == keyed.ID);
            AddStep("select another actual score from the same native result list", () =>
                screen.SelectOtherScore(createScore(ruleset, keys, "Second real score", false, 19)));
            AddUntilStep("new score has a fresh prepared page", () => screen.SkinPageAvailable && !screen.ShowingSkinPage);
            AddStep("open the new score's skin page", () => screen.ShowSkinResultsPage(true));
            AddUntilStep("new score pixels loaded", () => screen.ChildrenOfType<SkinResultsPageDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("failed background and counts do not retain the previous score", () => background(screen, "frame").Alpha == 0
                && background(screen, "failed").Alpha == 1
                && screen.ChildrenOfType<SkinResultsPageDisplay>().Single().ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "Second real score")
                && screen.ChildrenOfType<SkinResultsPageDisplay>().Single().ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "19"));
            AddAssert("showing results has not rewritten scoring", () => score.Statistics[HitResult.Perfect] == 37 && score.TotalScore == 24680);
        }

        [Test]
        public void TestAnUndeclaredPageCannotBorrowTheUnrelatedGlobalPage()
        {
            var ruleset = new ManiaRuleset();
            PageResultsScreen screen = null!;
            arrangeSkins(ruleset, 7, false);
            PushAndConfirm(() => screen = new PageResultsScreen(createScore(ruleset, 7, "No custom results", true, 5)));
            AddUntilStep("the exact unskinned target was inspected", () => screen.ResultsPageSkin?.SkinInfo.ID == keyed.ID);
            AddAssert("only the native page is available", () => !screen.SkinPageAvailable && !screen.ShowingSkinPage
                && !screen.ChildrenOfType<SkinResultsPageDisplay>().Any()
                && screen.ChildrenOfType<RoundedButton>().Where(button => button.Text.ToString() == "皮肤界面").All(button => button.Alpha == 0));
            AddAssert("the unrelated global page is still untouched", () => skins.CurrentSkin.Value.SkinInfo.ID == unrelated.ID);
        }

        [Test]
        public void TestBadOptionalPageJsonReportsFailureAndRetainsNativeResults()
        {
            var ruleset = new ManiaRuleset();
            var score = createScore(ruleset, 7, "Invalid page fixture", true, 5);
            PageResultsScreen screen = null!;
            arrangeSkins(ruleset, 7, true, invalidPage: true);
            PushAndConfirm(() => screen = new PageResultsScreen(score));
            AddUntilStep("the actual host reports its invalid JSON", () => screen.ChildrenOfType<SkinResultsPageHost>().Single().PreparationError != null);
            AddAssert("native results and their score remain available", () => !screen.SkinPageAvailable && !screen.ShowingSkinPage
                && ReferenceEquals(screen.SelectedScore.Value, score) && screen.ChildrenOfType<ScorePanel>().Any());
            AddAssert("the player sees the preparation failure", () => screen.ChildrenOfType<OsuSpriteText>()
                .Any(text => text.Text.ToString() == "此皮肤的结算页未能读取，已保留原界面。" && text.Alpha == 1));
        }

        [Test]
        public void TestBadGameplayScriptWithValidResultsReportsFailureAndRetainsNativeResults()
        {
            var ruleset = new ManiaRuleset();
            var score = createScore(ruleset, 7, "Invalid gameplay script fixture", true, 5);
            PageResultsScreen screen = null!;
            Skin originalOwner = null!;
            arrangeSkins(ruleset, 7, true, invalidScript: true);
            AddStep("retain the unrelated global owner before opening actual results", () => originalOwner = skins.CurrentSkin.Value);
            PushAndConfirm(() => screen = new PageResultsScreen(score));
            AddUntilStep("the actual results host reports the gameplay script failure", () =>
                screen.ChildrenOfType<SkinResultsPageHost>().Single().PreparationError != null);
            AddAssert("the actual score selected its scoped author while native results remain visible", () =>
                screen.ChildrenOfType<SkinResultsPageHost>().Single().Scope == new SkinSelectionScope("mania", 7)
                && !screen.SkinPageAvailable && !screen.ShowingSkinPage && screen.NativeResultsVisible
                && ReferenceEquals(screen.SelectedScore.Value, score) && screen.ChildrenOfType<ScorePanel>().Any()
                && !screen.ChildrenOfType<SkinResultsPageDisplay>().Any());
            AddAssert("the player sees the explicit preparation failure", () => screen.ChildrenOfType<OsuSpriteText>()
                .Any(text => text.Text.ToString() == "此皮肤的结算页未能读取，已保留原界面。" && text.Alpha == 1));
            AddAssert("the failure preserves the global skin and original result actions", () =>
                ReferenceEquals(skins.CurrentSkin.Value, originalOwner)
                && screen.ChildrenOfType<ReplayDownloadButton>().Any()
                && screen.ChildrenOfType<CollectionButton>().Any());
        }

        [Test]
        public void TestCancelledActualResultsLoadCanDisposeBeforeItsHostExists()
        {
            PageResultsScreen screen = null!;
            Task loading = null!;
            bool attached = false;
            AddStep("cancel a real results load before dispatch", () =>
            {
                screen = new PageResultsScreen(createScore(new ManiaRuleset(), 7, "Cancelled load fixture", true, 1));
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                loading = LoadComponentAsync(screen, _ => attached = true, cancellation.Token);
            });
            AddUntilStep("the cancelled load finishes", () => loading.IsCompleted);
            AddStep("dispose the unmounted results owner", () =>
            {
                // The pinned framework returns its cleanup continuation, which completes successfully after
                // cancellation. The canceled operation must instead be proven by no callback and no loaded tree.
                Assert.That(loading.IsCompletedSuccessfully, Is.True);
                Assert.That(attached, Is.False);
                Assert.That(screen.IsLoaded, Is.False);
                Assert.That(screen.Parent, Is.Null);
                Assert.That(screen.ChildrenOfType<SkinResultsPageHost>(), Is.Empty);
                Assert.DoesNotThrow(screen.Dispose);
            });
        }

        [Test]
        public void TestAStalePreparingScoreCannotReplaceTheNewScoresModeDefault()
        {
            var ruleset = new ManiaRuleset();
            PageResultsScreen screen = null!;
            arrangeSkins(ruleset, 7, true);
            AddStep("hold only independent package reads", () => heldMutation = skins.ManagedFolderOperationCoordinator.EnterMutation());
            PushAndConfirm(() => screen = new PageResultsScreen(createScore(ruleset, 7, "Old held score", true, 7)));
            AddUntilStep("the old keyed scope started preparing", () => screen.ChildrenOfType<SkinResultsPageHost>().Single().Scope == new SkinSelectionScope("mania", 7));
            AddStep("select a different-key score while the old read is held", () =>
                screen.SelectOtherScore(createScore(ruleset, 14, "New mode score", true, 14)));
            AddUntilStep("the replacement score's exact scope is selected", () =>
                screen.ChildrenOfType<SkinResultsPageHost>().Single().Scope == new SkinSelectionScope("mania", 14));
            AddAssert("the replacement scope resolves the real mode default", () =>
                skins.GetConfiguredSkin(new SkinSelectionScope("mania", 14)).ID == unrelated.ID);
            AddStep("release the reservation shared by independent package reads", () =>
            {
                heldMutation!.Dispose();
                heldMutation = null;
            });
            AddUntilStep("the new score uses the real mode default", () =>
                (screen.ResultsPageSkin?.SkinInfo.ID, screen.SkinPageAvailable, screen.ChildrenOfType<SkinResultsPageHost>().Single().PreparationError),
                () => Is.EqualTo(((Guid?)unrelated.ID, true, (string?)null)));
            AddStep("open the current score's page", () => screen.ShowSkinResultsPage(true));
            AddUntilStep("only the new score's page mounts", () => screen.ChildrenOfType<SkinResultsPageDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("no stale package or score was revived", () => screen.ResultsPageSkin!.SkinInfo.ID == unrelated.ID
                && screen.ChildrenOfType<SkinResultsPageDisplay>().Single().ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "New mode score")
                && screen.ChildrenOfType<SkinResultsPageHost>().Single().Scope == new SkinSelectionScope("mania", 14));
        }

        private void arrangeSkins(Ruleset ruleset, int keys, bool withPage, bool invalidPage = false, bool invalidScript = false)
        {
            Task<Live<SkinInfo>?> importing = null!;
            AddStep("import a real scoped author package", () => importing = skins.Import(new ImportTask(archive("Results keyed", withPage, invalidPage, invalidScript), "results-keyed.osk")));
            AddUntilStep("keyed package imported", () => importing.IsCompleted);
            AddStep("import an unrelated global author package", () =>
            {
                keyed = importing.GetAwaiter().GetResult()!;
                importing = skins.Import(new ImportTask(archive("Results unrelated", true), "results-unrelated.osk"));
            });
            AddUntilStep("unrelated package imported", () => importing.IsCompleted);
            AddStep("assign modes and a distinct real key override", () =>
            {
                unrelated = importing.GetAwaiter().GetResult()!;
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope("bms"), unrelated.ID);
                skins.SelectionPreferences.SetSkin(new SkinSelectionScope("mania"), unrelated.ID);
                skins.SelectionPreferences.SetSkin(new SkinSelectionScope(ruleset.ShortName, keys), keyed.ID);
                skins.CurrentSkinInfo.Value = unrelated;
                Game.Ruleset.Value = ruleset.ShortName == "bms" ? new ManiaRuleset().RulesetInfo : new BmsRuleset().RulesetInfo;
            });
            AddUntilStep("the unrelated global skin settles", () => skins.CurrentSkin.Value.SkinInfo.ID == unrelated.ID && !skins.CurrentSkin.Disabled);
        }

        private static ScoreInfo createScore(Ruleset ruleset, int keys, string title, bool passed, int count)
        {
            var score = TestResources.CreateTestScoreInfo(ruleset.RulesetInfo);
            score.Mods = Array.Empty<Mod>();
            score.BeatmapInfo!.Difficulty.CircleSize = keys;
            score.BeatmapInfo.Metadata.Title = title;
            score.TotalScore = 24680;
            score.Passed = passed;
            score.Statistics[HitResult.Perfect] = count;
            return score;
        }

        private static Container background(PageResultsScreen screen, string name)
            => screen.ChildrenOfType<SkinResultsPageDisplay>().Single().ChildrenOfType<Container>().Single(container => container.Name == name);

        private static MemoryStream archive(string name, bool withPage, bool invalidPage = false, bool invalidScript = false)
        {
            var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                write("skin.ini", Encoding.UTF8.GetBytes($"[General]\nName: {name}\nAuthor: OMS isolated tests\nVersion: latest\n"));
                if (withPage)
                {
                    JObject document = SkinPageCodecTest.PageJson();
                    var elements = (JArray)document["results"]!["elements"]!;
                    elements[0]!["visibleWhen"] = new JObject { ["source"] = "score.passed", ["value"] = true };
                    JObject failed = (JObject)elements[0]!.DeepClone();
                    failed["id"] = "failed";
                    failed["visibleWhen"]!["value"] = false;
                    elements.Insert(1, failed);
                    foreach (var (path, bytes) in SkinPageCodecTest.PageFiles(document))
                        write(path, invalidPage && path == SkinPageContracts.FILE_NAME ? Encoding.UTF8.GetBytes("{") : bytes);
                    if (invalidScript)
                        Assert.That(SkinPageCodec.Decode(SkinPageCodecTest.DocumentBytes(document)).Results, Is.Not.Null);
                }
                if (invalidScript)
                {
                    var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>(), GameplaySkinSceneContracts.SCRIPT_FILE_NAME);
                    const string scene = """
                        {"contract":"oms-gameplay-skin-scene.v1","root":{"id":"results.fixture.root","type":"container","target":{"kind":"global"},
                         "blend":"inherit","properties":{},"effects":[],"children":[]},
                         "tracks":[],"stateMachines":[],"bindings":[],"variants":[],"templates":[],"instances":[]}
                        """;
                    string encodedManifest = GameplaySkinSceneCodec.EncodeManifest(manifest);
                    Assert.That(GameplaySkinSceneCodec.DecodeManifest(encodedManifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
                    Assert.That(GameplaySkinSceneCodec.DecodeScene(scene, manifest).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
                    write(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, Encoding.UTF8.GetBytes(encodedManifest));
                    write(GameplaySkinSceneContracts.SCENE_FILE_NAME, Encoding.UTF8.GetBytes(scene));
                    write(GameplaySkinSceneContracts.SCRIPT_FILE_NAME, Encoding.UTF8.GetBytes("oms-script 1\njump missing-label\nhalt\n"));
                }
                void write(string path, byte[] bytes)
                {
                    using Stream destination = zip.CreateEntry(path).Open();
                    destination.Write(bytes);
                }
            }
            output.Position = 0;
            return output;
        }

        private partial class PageResultsScreen : ResultsScreen
        {
            public bool NativeResultsVisible => VerticalScrollContent.Alpha == 1;

            public PageResultsScreen(ScoreInfo score)
                : base(score)
            {
            }

            public void SelectOtherScore(ScoreInfo score)
            {
                ScorePanelList.AddScore(score);
                SelectedScore.Value = score;
            }
        }
    }
}
