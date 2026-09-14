// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    public partial class TestSceneStartupRuleset : OsuGameTestScene
    {
        protected override TestOsuGame CreateTestGame()
        {
            // Must be done in this function due to the RecycleLocalStorage call just before.
            var config = DebugUtils.IsDebugBuild
                ? new DevelopmentOsuConfigManager(LocalStorage)
                : new OsuConfigManager(LocalStorage);

            config.SetValue(OsuSetting.Ruleset, "mania");
            config.Save();

            return base.CreateTestGame();
        }

        [Test]
        public void TestRulesetConsumed()
        {
            AddUntilStep("ruleset correct", () => Game.Ruleset.Value.ShortName == "mania");
        }

        [Test]
        public void TestRulesetSpecificSkinPreferences()
        {
            Guid bmsSkinId = Guid.NewGuid();
            Guid maniaSkinId = Guid.NewGuid();

            AddStep("create skin records and configure preferences", () =>
            {
                Game.Realm.Write(realm =>
                {
                    realm.Add(new SkinInfo("BMS preference", "OMS tests", typeof(LegacySkin).AssemblyQualifiedName)
                    {
                        ID = bmsSkinId,
                    });
                    realm.Add(new SkinInfo("mania preference", "OMS tests", typeof(LegacySkin).AssemblyQualifiedName)
                    {
                        ID = maniaSkinId,
                    });
                });

                Game.LocalConfig.SetValue(OsuSetting.SkinBms, bmsSkinId.ToString());
                Game.LocalConfig.SetValue(OsuSetting.SkinMania, maniaSkinId.ToString());
            });

            AddStep("switch to BMS", () => Game.Ruleset.Value = Game.Dependencies.Get<RulesetStore>().AvailableRulesets.Single(r => r.ShortName == "bms"));
            AddUntilStep("BMS preference applied", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID == bmsSkinId);

            AddStep("switch to mania", () => Game.Ruleset.Value = Game.Dependencies.Get<RulesetStore>().AvailableRulesets.Single(r => r.ShortName == "mania"));
            AddUntilStep("mania preference applied", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID == maniaSkinId);

            AddAssert("preferences remain independent", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinBms), Is.EqualTo(bmsSkinId.ToString()));
                    Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinMania), Is.EqualTo(maniaSkinId.ToString()));
                });

                return true;
            });
        }
    }
}
