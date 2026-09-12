// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public partial class BuiltInSkinColdStartTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task TestConfiguredBuiltInSkinDuringHostBootstrap(bool complex)
        {
            Guid selected = complex ? SkinInfo.OMS_COMPLEX_SKIN : SkinInfo.OMS_SKIN;
            using var host = new ConfiguredHost(selected);
            using var game = new ColdStartGame(selected);
            await Task.Factory.StartNew(() => host.Run(game), TaskCreationOptions.LongRunning)
                      .WaitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            Assert.That(game.SelectionRestored, Is.True);
        }

        private class ConfiguredHost : CleanRunHeadlessGameHost
        {
            private readonly Guid selected;

            public ConfiguredHost(Guid selected)
            {
                this.selected = selected;
            }

            protected override void SetupForRun()
            {
                base.SetupForRun();
                using var config = new OsuConfigManager(Storage);
                config.SetValue(OsuSetting.Skin, selected.ToString());
                config.Save();
            }
        }

        private partial class ColdStartGame : OsuGameTestScene.TestOsuGame
        {
            private readonly Guid selected;

            public bool SelectionRestored { get; private set; }

            public ColdStartGame(Guid selected)
                : base(null!, new DummyAPIAccess())
            {
                this.selected = selected;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Schedule(() =>
                {
                    var manager = Dependencies.Get<SkinManager>();
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(selected));
                    Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(selected));
                    Assert.That(LocalConfig.Get<string>(OsuSetting.Skin), Is.EqualTo(selected.ToString()));
                    SelectionRestored = true;
                    Host.Exit();
                });
            }
        }
    }
}
