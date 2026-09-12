// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    public partial class TestSceneBuiltInOmsSkins : OsuGameTestScene
    {
        [Test]
        public void TestOnlySimpleIsBuiltInAndRetiredComplexSelectionReturnsToSimple()
        {
            SkinManager manager = null!;
            AddStep("inspect the sole installation skin without importing anything", () =>
            {
                manager = Game.Dependencies.Get<SkinManager>();
                Assert.That(manager.GetAllUsableSkins().Select(skin => skin.ID), Is.EqualTo(new[] { SkinInfo.OMS_SKIN }));
                Assert.That(manager.CanModify(manager.DefaultOmsSkin.SkinInfo), Is.False);
                Assert.That(manager.CanDelete(SkinInfo.OMS_SKIN), Is.False);
                Assert.That(manager.CanExport(manager.DefaultOmsSkin.SkinInfo), Is.True);
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(manager.DefaultOmsSkin), Is.True);
                manager.SelectNextSkin();
                manager.SelectPreviousSkin();
            });
            AddUntilStep("cycling retains simple", () => ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin));
            AddStep("restore a retired complex selection", () => manager.SetSkinFromConfiguration(SkinInfo.OMS_COMPLEX_SKIN.ToString()));
            AddUntilStep("retired selection resolves to simple", () => manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
            AddUntilStep("configuration records the replacement", () => Game.LocalConfig.Get<string>(OsuSetting.Skin) == SkinInfo.OMS_SKIN.ToString());
            AddStep("save selection", () => Game.LocalConfig.Save());
            AddStep("remove game", () => Remove(Game, true));
            AddStep("create game again", CreateGame);
            AddUntilStep("wait for restart", () => Game.IsLoaded);
            AddUntilStep("simple selected after restart without import", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
        }
    }
}
