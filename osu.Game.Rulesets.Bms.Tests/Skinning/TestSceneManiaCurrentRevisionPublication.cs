// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneManiaCurrentRevisionPublication : PlayerTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new ManiaBeatmap(new StageDefinition(4))
        {
            HitObjects =
            {
                new Note
                {
                    Column = 0,
                    StartTime = 60_000,
                },
            },
            Difficulty = { CircleSize = 4 },
            BeatmapInfo = { Ruleset = ruleset },
        };

        [Test]
        public void TestRealManiaPlayerRejectsCurrentRevisionReloadBeforePrepareAndKeepsExactPair()
        {
            RulesetSkinProvidingContainer gameplayHost = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            Task<SkinCurrentRevisionReloadResult> reload = null!;
            int prepareCalls = 0;

            AddUntilStep("wait for real mania player gameplay host", () =>
            {
                gameplayHost = Player.ChildrenOfType<RulesetSkinProvidingContainer>().SingleOrDefault()!;
                return Player.DrawableRuleset is DrawableManiaRuleset { IsLoaded: true }
                       && gameplayHost?.IsLoaded == true;
            });
            AddStep("capture exact pair before first mania hit object", () =>
            {
                revisionA = skinManager.CurrentRevision;
                ownerA = skinManager.CurrentSkin.Value;
                selectionA = skinManager.CurrentSkinInfo.Value;
                skinManager.CurrentRevisionPrepareStarted = () => prepareCalls++;

                Assert.Multiple(() =>
                {
                    Assert.That(Player.DrawableRuleset, Is.TypeOf<DrawableManiaRuleset>());
                    Assert.That(gameplayHost, Is.Not.Null);
                    Assert.That(gameplayHost.IsLoaded, Is.True);
                    Assert.That(Player.DrawableRuleset.FrameStableClock.CurrentTime, Is.LessThan(60_000));
                    Assert.That(revisionA.Owner, Is.SameAs(ownerA));
                });
            });
            AddStep("request reload through manager while real mania host is attached", () =>
                reload = skinManager.ReloadCurrentRevisionAsync());
            AddStep("assert live rejection happened before prepare and preserved A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(reload.IsCompletedSuccessfully, Is.True);
                    Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.LiveGameplayActive));
                    Assert.That(prepareCalls, Is.Zero);
                    Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(skinManager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                });

                skinManager.CurrentRevisionPrepareStarted = () => { };
            });
        }
    }
}
