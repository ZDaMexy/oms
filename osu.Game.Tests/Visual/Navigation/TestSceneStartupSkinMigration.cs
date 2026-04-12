// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    public partial class TestSceneStartupSkinMigration : OsuGameTestScene
    {
        private bool configSeeded;

        protected override TestOsuGame CreateTestGame()
        {
            if (!configSeeded)
            {
                var config = DebugUtils.IsDebugBuild
                    ? new DevelopmentOsuConfigManager(LocalStorage)
                    : new OsuConfigManager(LocalStorage);

                config.SetValue(OsuSetting.Skin, TrianglesSkin.CreateInfo().ID.ToString());
                config.Save();
                configSeeded = true;
            }

            return base.CreateTestGame();
        }

        [Test]
        public void TestProtectedBuiltInSkinMigratesToOmsOnStartup()
        {
            AddUntilStep("runtime skin migrated to OMS", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID == OmsSkin.CreateInfo().ID);
            AddAssert("config migrated to OMS", () => Game.LocalConfig.Get<string>(OsuSetting.Skin) == OmsSkin.CreateInfo().ID.ToString());

            AddStep("save migrated config", () => Game.LocalConfig.Save());
            AddStep("remove game", () => Remove(Game, true));
            AddStep("create game again", CreateGame);

            AddUntilStep("wait for reload", () => Game.IsLoaded);
            AddUntilStep("runtime skin stays OMS after reload", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID == OmsSkin.CreateInfo().ID);
            AddAssert("config stays OMS after reload", () => Game.LocalConfig.Get<string>(OsuSetting.Skin) == OmsSkin.CreateInfo().ID.ToString());
        }
    }
}
