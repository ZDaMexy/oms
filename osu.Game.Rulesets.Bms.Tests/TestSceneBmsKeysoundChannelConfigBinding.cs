// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsKeysoundChannelConfigBinding : OsuTestScene
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly BmsRuleset ruleset = new BmsRuleset();

        private BmsRulesetConfigManager config = null!;
        private DrawableBmsRuleset drawableRuleset = null!;

        [Test]
        public void TestKeysoundConcurrentChannelsAppliedOnLoad()
        {
            const int configuredChannels = 48;

            setupScene(configuredChannels);

            AddAssert("shared store target matches config", () => drawableRuleset.Playfield.KeysoundStore.ConcurrentChannels, () => Is.EqualTo(configuredChannels));
            AddAssert("shared store pool grows to config", () => drawableRuleset.Playfield.KeysoundStore.ActualConcurrentChannels, () => Is.EqualTo(configuredChannels));
        }

        [Test]
        public void TestKeysoundConcurrentChannelsTrackLiveConfigChanges()
        {
            const int initialChannels = 24;
            const int grownChannels = 64;
            const int shrunkChannels = 12;

            setupScene(initialChannels);

            AddStep("grow keysound channels", () => config.SetValue(BmsRulesetSetting.KeysoundConcurrentChannels, grownChannels));
            AddUntilStep("shared store target grows", () => drawableRuleset.Playfield.KeysoundStore.ConcurrentChannels == grownChannels);
            AddUntilStep("shared store pool grows", () => drawableRuleset.Playfield.KeysoundStore.ActualConcurrentChannels == grownChannels);

            AddStep("shrink keysound channels", () => config.SetValue(BmsRulesetSetting.KeysoundConcurrentChannels, shrunkChannels));
            AddUntilStep("shared store target shrinks", () => drawableRuleset.Playfield.KeysoundStore.ConcurrentChannels == shrunkChannels);
            AddUntilStep("shared store pool shrinks when idle", () => drawableRuleset.Playfield.KeysoundStore.ActualConcurrentChannels == shrunkChannels);
        }

        private void setupScene(int keysoundConcurrentChannels)
        {
            AddStep("configure keysound channels", () =>
            {
                config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(ruleset)!;
                config.SetValue(BmsRulesetSetting.KeysoundConcurrentChannels, keysoundConcurrentChannels);

                Child = drawableRuleset = new DrawableBmsRuleset(ruleset, createPlayableBeatmap())
                {
                    RelativeSizeAxes = Axes.Both,
                };
            });

            AddUntilStep("drawable ruleset loaded", () => drawableRuleset?.IsLoaded == true);
        }

        private BmsBeatmap createPlayableBeatmap()
        {
            const string text = @"
#TITLE Keysound Channel Config Stub
#BPM 120
#RANK 2
#00101:AA00
#WAVAA bgm.wav
#WAVBB key1.wav
#WAVCC key2.wav
#00111:BB00
#00112:CC00
";

            var decodedChart = decoder.DecodeText(text, "keysound-channel-config-stub.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), ruleset).Convert();
        }
    }
}
