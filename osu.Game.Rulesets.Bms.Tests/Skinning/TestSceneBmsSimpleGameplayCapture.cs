// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Saves pixels from a desktop host running the actual PlayerLoader, canonical package and BMS renderer.
    /// The synthetic offline chart supplies repeatable notes; it is not a capture of a user's song or BGA.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBmsSimpleGameplayCapture : OsuGameTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private Task? capture;

        public override bool AutomaticallyRunFirstStep => false;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void requireIsolation(ExactVisualTestIsolation? isolation)
        {
            if (host is not HeadlessGameHost && isolation == null)
                throw new InvalidOperationException("Gameplay capture requires the disposable --exact-test runner.");
        }

        [Test]
        public void CaptureCanonicalSimplePlayer()
        {
            Player player = null!;
            var ruleset = new BmsRuleset();
            AddStep("require a real desktop renderer", () =>
            {
                if (host is HeadlessGameHost)
                    Assert.Ignore("Pixel evidence requires the isolated desktop --exact-test runner.");
            });
            AddStep("select installed simple and a repeatable 7K chart", () =>
            {
                var skins = Game.Dependencies.Get<SkinManager>();
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.DefaultOmsSkin), Is.True);
                skins.CurrentSkinInfo.Value = skins.DefaultOmsSkin.SkinInfo;
                var configs = Game.Dependencies.Get<IRulesetConfigCache>();
                ((BmsRulesetConfigManager)configs.GetConfigFor(ruleset)!).SetValue(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.P1);
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(createChart(ruleset));
                Game.SelectedMods.Value = new[] { ruleset.GetAutoplayMod()! };
                DismissAnyNotifications();
            });
            AddStep("enter real autoplay player", () => Game.ScreenStack.Push(new PlayerLoader(() =>
                player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData))));
            AddUntilStep("wait for actual player and committed skin", () =>
            {
                DismissAnyNotifications();
                return player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                                      && player.IsLoaded && player.LoadedBeatmapSuccessfully
                                      && player.ChildrenOfType<DrawableBmsRuleset>().SingleOrDefault()?.IsLoaded == true;
            });
            AddStep("verify renderer owns the installed package", () =>
            {
                var skins = Game.Dependencies.Get<SkinManager>();
                DrawableBmsRuleset renderer = player.ChildrenOfType<DrawableBmsRuleset>().Single();
                var owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                Assert.That(owner.CurrentPublication, Is.Not.Null);
                Assert.That(owner.PackageRevision.RetainsExact(skins.CurrentRevision), Is.True);
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.CurrentSkin.Value), Is.True);
            });
            AddWaitStep("allow live notes and draw frames", 12);
            AddStep("capture real desktop framebuffer", () => capture = saveCapture());
            AddUntilStep("wait for PNG write", () => capture?.IsCompleted == true);
            AddStep("propagate capture failures", () => capture!.GetAwaiter().GetResult());
        }

        private async Task saveCapture()
        {
            string path = Path.GetFullPath(Environment.GetEnvironmentVariable("OMS_SIMPLE_CAPTURE_PATH")
                                           ?? Path.Combine("artifacts", "simple-gameplay-capture.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            if (pixels == null || pixels.Width < 640 || pixels.Height < 360)
                throw new InvalidOperationException("Desktop capture did not return a usable framebuffer.");
            await pixels.SaveAsPngAsync(path).ConfigureAwait(false);
        }

        private static IBeatmap createChart(BmsRuleset ruleset)
        {
            var text = new StringBuilder("#TITLE Simple information / 実機検証\n#ARTIST OMS Offline Studio\n#DIFFICULTY 4\n#PLAYLEVEL 12\n#BPM 138\n#BPM01 172\n#00808:01\n#01603:78\n#WAV01 note.wav\n");
            string[] channels = { "11", "12", "13", "14", "15", "18", "19", "16" };
            for (int measure = 1; measure <= 32; measure++)
                for (int lane = 0; lane < channels.Length; lane++)
                    text.Append('#').Append(measure.ToString("000")).Append(channels[lane]).Append(':')
                        .Append(lane % 2 == 0 ? "0100000001000000" : "0000010000000100").Append('\n');
            var decoded = new BmsBeatmapDecoder().DecodeText(text.ToString(), "simple-gameplay-capture.bme");
            return new BmsDecodedBeatmap(decoded) { BeatmapInfo = { Ruleset = ruleset.RulesetInfo } };
        }
    }
}
