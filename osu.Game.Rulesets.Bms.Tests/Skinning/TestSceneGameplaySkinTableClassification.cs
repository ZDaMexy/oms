// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneGameplaySkinTableClassification : OsuGameTestScene
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestPlayerReadsPersistedTableMetadataAcrossRealConversion(bool mania)
        {
            Player player = null!;
            var bmsRuleset = new BmsRuleset();
            Ruleset selectedRuleset = mania ? new ManiaRuleset() : bmsRuleset;
            const string classification = "Satellite sl4 / Insane ★8";
            AddStep("load indexed BMS working source and select actual ruleset", () =>
            {
                var source = new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("""
                    #TITLE Indexed source
                    #ARTIST Test Artist
                    #BPM 120
                    #DIFFICULTY 4
                    #PLAYLEVEL 12
                    #WAV01 missing.wav
                    #00111:01000100
                    #00219:00010001
                    """, "indexed-source.bme"))
                {
                    BeatmapInfo = new BeatmapInfo(bmsRuleset.RulesetInfo)
                    {
                        Metadata = new BeatmapMetadata
                        {
                            RulesetDataJson = """
                                {"difficulty_table_entries":[
                                  {"TableName":"Insane","LevelLabel":"★8","Level":8,"TableSortOrder":1},
                                  {"TableName":"Satellite","LevelLabel":"sl4","Level":4,"TableSortOrder":0}]}
                                """,
                        },
                    },
                };
                // Conversion deliberately reconstructs chart metadata; the working source remains authoritative
                // for the independent persisted table classification.
                var converted = (BmsBeatmap)bmsRuleset.CreateBeatmapConverter(source).Convert();
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassificationWithNames(converted.BeatmapInfo.Metadata), Is.Empty);
                Game.Beatmap.Value = CreateWorkingBeatmap(source);
                Assert.That(BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassificationWithNames(Game.Beatmap.Value.Metadata), Is.EqualTo(classification));
                var skins = Game.Dependencies.Get<SkinManager>();
                skins.CurrentSkinInfo.Value = skins.DefaultOmsSkin.SkinInfo;
                Game.Ruleset.Value = selectedRuleset.RulesetInfo;
                Game.SelectedMods.Value = new[] { selectedRuleset.GetAutoplayMod()! };
                DismissAnyNotifications();
            });
            AddStep("enter actual player", () => Game.ScreenStack.Push(new PlayerLoader(() =>
                player = new ReplayPlayer(selectedRuleset.GetAutoplayMod()!.CreateScoreFromReplayData))));
            AddUntilStep("wait for committed player scene", () =>
            {
                DismissAnyNotifications();
                return player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                                      && player.IsLoaded && player.LoadedBeatmapSuccessfully
                                      && player.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Any(scene => scene.IsSceneReady);
            });
            AddStep("complete skin snapshot keeps table classification separate from author level", () =>
            {
                GameplaySkinEventRuntimeHost host = player.ChildrenOfType<GameplaySkinEventRuntimeHost>().Single();
                using var subscription = host.EventStream.Subscribe();
                GameplaySkinSongInformation? information = null;
                subscription.DrainFrame(envelope =>
                {
                    if (envelope.Payload is GameplaySkinStateEventPayload state)
                        information = state.State.SongInformation;
                });
                Assert.That(information, Is.Not.Null);
                Assert.That(information!.TableClassification, Is.EqualTo(classification));
                Assert.That(information.Level, Is.EqualTo(mania ? string.Empty : "12"));
            });
        }
    }
}
