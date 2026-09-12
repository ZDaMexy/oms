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
        public void TestBothInstallationSkinsAreSelectableAndComplexSurvivesRestartWithoutImport()
        {
            SkinManager manager = null!;
            Skin complex = null!;
            AddStep("inspect installation skins without importing anything", () =>
            {
                manager = Game.Dependencies.Get<SkinManager>();
                complex = manager.BuiltInComplexSkin;
                Assert.That(manager.GetAllUsableSkins().Select(skin => skin.ID).Take(2),
                    Is.EqualTo(new[] { SkinInfo.OMS_SKIN, SkinInfo.OMS_COMPLEX_SKIN }));
                Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                Assert.That(manager.CanModify(complex.SkinInfo), Is.False);
                Assert.That(manager.CanDelete(complex.SkinInfo.ID), Is.False);
                Assert.That(manager.CanExport(complex.SkinInfo), Is.True);
                Assert.That(CanonicalSkinPackage.IsBuiltInSkin(complex), Is.True);
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(complex), Is.False);
                manager.SelectNextSkin();
            });
            AddUntilStep("cycle selects the built-in complex owner", () => ReferenceEquals(manager.CurrentSkin.Value, complex));
            AddAssert("complex has its own source identity", () => manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedBuiltIn);
            AddStep("cycle back to simple", () => manager.SelectPreviousSkin());
            AddUntilStep("simple remains the fallback", () => ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin));
            AddStep("select complex from configuration", () => manager.SetSkinFromConfiguration(SkinInfo.OMS_COMPLEX_SKIN.ToString()));
            AddUntilStep("the same complex instance remains reusable", () => ReferenceEquals(manager.CurrentSkin.Value, complex));
            AddUntilStep("configuration records complex", () => Game.LocalConfig.Get<string>(OsuSetting.Skin) == SkinInfo.OMS_COMPLEX_SKIN.ToString());
            AddStep("save selection", () => Game.LocalConfig.Save());
            AddStep("remove game", () => Remove(Game, true));
            AddStep("create game again", CreateGame);
            AddUntilStep("wait for restart", () => Game.IsLoaded);
            AddUntilStep("complex selected after restart without import", () =>
                Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID == SkinInfo.OMS_COMPLEX_SKIN);
            AddAssert("both choices remain protected installation products", () =>
                Game.Dependencies.Get<SkinManager>().GetAllUsableSkins().Take(2).All(skin => skin.PerformRead(info => info.Protected && info.Files.Count == 0)));
        }
    }
}
