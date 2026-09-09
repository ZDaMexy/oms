// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase("canonical")]
        [TestCase("oms-simple")]
        [TestCase("oms-complex")]
        [TestCase("third-party")]
        public void TestCanonicalProductsSupplyDecodedManiaHitAndHoldSounds(string package)
        {
            addSelectCanonicalProduct(package);
            SkinProvidingContainer provider = null!;
            var sounds = new List<(string Name, PausableSkinnableSound Sound)>();
            AddStep("load actual hit and hold sounds through the selected skin and required fallback", () =>
            {
                Add(provider = new SkinProvidingContainer(manager));
                foreach (string bank in HitSampleInfo.ALL_BANKS)
                    foreach (string sample in new[] { HitSampleInfo.HIT_NORMAL, HitSampleInfo.HIT_CLAP, HitSampleInfo.HIT_FINISH, HitSampleInfo.HIT_WHISTLE, "sliderslide", "sliderwhistle" })
                    {
                        var sound = new PausableSkinnableSound(new HitSampleInfo(sample, bank));
                        sounds.Add(($"{bank}-{sample}", sound));
                        provider.Add(sound);
                    }
            });
            AddUntilStep("every supported mania sound decodes to playable audio", () => sounds.All(sample => sample.Sound.Length > 0));
            AddStep("the ordinary package resource owns every decoded sample", () =>
            {
                foreach ((string name, PausableSkinnableSound sound) in sounds)
                    Assert.That(sound.ChildrenOfType<DrawableSample>().Single().Name, Is.EqualTo(name));
            });
            AddStep("release sound consumers", () => provider.Expire());
            AddUntilStep("all sound consumers detach", () => provider.Parent == null);
        }

        private static IEnumerable<TestCaseData> canonicalBmsCases()
        {
            foreach (string package in new[] { "canonical", "oms-simple", "oms-complex", "third-party" })
                foreach (BmsKeymode keymode in Enum.GetValues<BmsKeymode>())
                {
                    if (keymode is not (BmsKeymode.Key5K or BmsKeymode.Key7K or BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms or BmsKeymode.Key14K))
                        continue;
                    var styles = keymode is BmsKeymode.Key5K or BmsKeymode.Key7K
                        ? new[] { BmsPlayfieldStyle.P1, BmsPlayfieldStyle.P2, BmsPlayfieldStyle.Center, BmsPlayfieldStyle.CenterRightScratch }
                        : new[] { BmsPlayfieldStyle.Center };
                    foreach (BmsPlayfieldStyle style in styles)
                        foreach (int viewport in Enumerable.Range(0, 3))
                            yield return new TestCaseData(package, keymode, style, viewport);
                }
        }

        [TestCaseSource(nameof(canonicalBmsCases))]
        public void TestCanonicalProductsBmsAllModesStylesAndScreens(string package, BmsKeymode keymode, BmsPlayfieldStyle style, int viewport)
        {
            addSelectCanonicalProduct(package);
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            AddStep("mount complete skin with the actual BMS decoder and renderer", () =>
            {
                var fixture = new ExactBmsProductionFixture(createC5MatrixChart(keymode),
                    keymode == BmsKeymode.Key9K_Pms ? "canonical.pms" : "canonical.bms", style,
                    initialGameplayTime: 2_000, keymodeOverride: keymode);
                renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture);
                setCanonicalViewport(renderer, viewport);
                Add(renderer);
                renderer.ShowBms();
            });
            AddUntilStep("the full skin reaches actual lanes and background viewport", () => renderer.BmsReady);
            AddStep("mount the actual gameplay information", () => renderer.AddProductionCoreHud());
            AddUntilStep("all bounded skin visuals are ready", () =>
            {
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()!;
                return scene?.IsSceneReady == true && renderer.CoreHud?.IsLoaded == true;
            });
            AddStep("all supported parts have a file-backed result", () => assertCanonicalProductScene(scene, package));
            AddStep("the skin and all consumers share the committed publication", () =>
            {
                Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
                Assert.That(scene.PreparedScene.Snapshot, Is.SameAs(renderer.BmsLayoutProbe.Publication!.Snapshot));
                Assert.That(scene.MaterialSet, Is.SameAs(renderer.BmsLayoutProbe.Publication.MaterialSet));
            });
            AddStep("detach complete BMS playfield", () => renderer.Expire());
            AddUntilStep("all skin consumers detached", () => renderer.Parent == null);
        }

        private static IEnumerable<TestCaseData> canonicalManiaCases()
        {
            foreach (string package in new[] { "canonical", "oms-simple", "oms-complex", "third-party" })
                foreach (int columns in Enumerable.Range(1, 10))
                    foreach (bool dual in new[] { false, true })
                        foreach (int viewport in Enumerable.Range(0, 3))
                            yield return new TestCaseData(package, columns, dual, viewport);
        }

        [TestCaseSource(nameof(canonicalManiaCases))]
        public void TestCanonicalProductsManiaAllStageSizesAndScreens(string package, int columns, bool dual, int viewport)
        {
            addSelectCanonicalProduct(package);
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            AddStep("mount full skin with notes and holds in every mania column", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager, maniaStageColumns: dual ? new[] { columns, columns } : new[] { columns });
                setCanonicalViewport(renderer, viewport);
                Add(renderer);
                renderer.ShowMania();
            });
            AddUntilStep("all mania stages have one current material publication", () => renderer.ManiaReady);
            AddStep("mount the actual mania gameplay information", () => renderer.AddProductionCoreHud(mania: true));
            AddUntilStep("the complete mania skin is ready", () =>
            {
                scene ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()!;
                return scene?.IsSceneReady == true
                       && renderer.CoreHud?.IsLoaded == true
                       && renderer.CoreHud.GameplaySkinGaugePartitions.Count > 0
                       && renderer.CoreHud.GameplaySkinTextPartitions.Count > 0;
            });
            AddStep("required mania visuals and author suppression survive", () =>
            {
                assertCanonicalProductScene(scene, package);
                Assert.That(scene.PreparedScene.Snapshot.Context.Topology.LanesInLogicalOrder, Has.Count.EqualTo(columns * (dual ? 2 : 1)));
                Assert.That(scene.PreparedScene.Snapshot, Is.SameAs(renderer.ManiaLayoutProbe.Publication!.Snapshot));
                Assert.That(scene.MaterialSet, Is.SameAs(renderer.ManiaLayoutProbe.Publication.MaterialSet));
                Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
                assertCanonicalManiaCoreHud(renderer.CoreHud!, scene, dual ? 2 : 1);
            });
            AddStep("detach complete mania playfield", () => renderer.Expire());
            AddUntilStep("all mania consumers detached", () => renderer.Parent == null);
        }

        private void addSelectCanonicalProduct(string package)
        {
            Task<Live<SkinInfo>>? import = null;
            Live<SkinInfo>? imported = null;
            if (package == "canonical")
            {
                AddStep("select the validated installation default", () =>
                {
                    Assert.That(manager.IsGameplaySkinInstallationAvailable, Is.True, manager.GameplaySkinInstallationRepairMessage);
                    Assert.That(CanonicalSkinPackage.IsCanonicalSkin(manager.DefaultOmsSkin), Is.True);
                    manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo;
                });
                return;
            }

            AddStep("import the deliverable using the ordinary player importer", () =>
            {
                string archive = package == "third-party"
                    ? createCanonicalThirdPartyFixture()
                    : createCanonicalImportCopy(package);
                import = Task.Run(async () => await manager.Import(new ImportTask(archive)).ConfigureAwait(false));
            });
            AddUntilStep("ordinary import completes", () => import?.IsCompleted == true);
            AddStep("select the ordinary user-owned record", () =>
            {
                imported = import!.GetAwaiter().GetResult();
                Assert.That(imported.PerformRead(info => info.Protected), Is.False);
                manager.CurrentSkinInfo.Value = imported;
            });
            AddUntilStep("the selected package owns the current skin", () =>
                imported != null && manager.CurrentSkin.Value.SkinInfo.ID == imported.ID);
        }

        private static void assertCanonicalManiaCoreHud(HUDOverlay hud, GameplaySkinSceneRuntimeHost scene, int stageCount)
        {
            Assert.That(hud.GameplaySkinGaugePartitions.Any(partition => partition.Visual is DefaultHealthDisplay), Is.True);
            Assert.That(hud.GameplaySkinTextPartitions.Any(partition => partition.Visual is DefaultScoreCounter), Is.True);
            Assert.That(hud.GameplaySkinTextPartitions.Any(partition => partition.Visual is DefaultAccuracyCounter), Is.True);
            Assert.That(hud.GameplaySkinTextPartitions.Any(partition => partition.Visual is DefaultSongProgress), Is.True);

            foreach (var partitions in new[] { hud.GameplaySkinGaugePartitions, hud.GameplaySkinTextPartitions })
            {
                Assert.That(partitions.Select(partition => partition.StageKey).Distinct().Count(), Is.EqualTo(stageCount));
                foreach (GameplaySkinHudProgrammaticVisualPartition partition in partitions)
                {
                    Assert.That(scene.TryGetVisualGate(partition.StageKey, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    if (ReferenceEquals(partition.StageKey.Slot, GameplaySkinSlotCatalog.TextHud))
                    {
                        Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed),
                            "The full packages explicitly suppress duplicate narrow stage information while retaining the global readout.");
                        Assert.That(scene.TryGetHostedDrawable(partition.StageKey, out _), Is.False);
                    }
                    else
                    {
                        Assert.That(gate!.IsReplacementReady, Is.True);
                        Assert.That(gate.Route, Is.Not.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                        Assert.That(scene.TryGetHostedDrawable(partition.StageKey, out Drawable? replacement), Is.True);
                        Assert.That(replacement?.Parent, Is.Not.Null);
                    }
                    Assert.That(partition.Owner.Alpha, Is.Zero,
                        "A complete ready skin must replace the actual corresponding health and information owners.");
                    Assert.That(partition.ControllingKeys, Does.Contain(partition.StageKey));
                    Assert.That(scene.MaterialSet.Entries.Any(entry => entry.Key.Equals(partition.StageKey)), Is.True);
                }
            }

            Assert.That(hud.GameplaySkinTextPartitions.All(partition =>
                partition.ControllingKeys.Any(key => ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.TextHud)
                                                     && key.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global)), Is.True);
            GameplaySkinResolvedMaterialKey globalText = scene.MaterialSet.Entries.Single(entry =>
                ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.TextHud)
                && entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global).Key;
            Assert.That(scene.TryGetVisualGate(globalText, out GameplaySkinSceneHostedSlot? globalGate), Is.True);
            Assert.That(globalGate!.IsReplacementReady, Is.True);
            Assert.That(scene.TryGetHostedDrawable(globalText, out Drawable? globalReadout), Is.True);
            Assert.That(globalReadout!.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString().Contains('%')), Is.True,
                "The actual global readout must replace the hidden native accuracy display with visible percentage text.");
            Assert.That(hud.GameplaySkinHudResidualPartitions
                           .Where(partition => ReferenceEquals(partition.Slot, GameplaySkinSlotCatalog.GaugeVisual)
                                               || ReferenceEquals(partition.Slot, GameplaySkinSlotCatalog.TextHud))
                           .All(partition => partition.Owner.Alpha == 0), Is.True,
                "The old information surface must not remain visible in the gaps beside the authored stage surfaces.");
        }

        private string createCanonicalImportCopy(string package)
        {
            string archive = LocalStorage.GetFullPath($"{package}-{Guid.NewGuid():N}.osk");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Skins", "Canonical", package + ".osk"), archive);
            File.SetAttributes(archive, FileAttributes.Normal);
            return archive;
        }

        private string createCanonicalThirdPartyFixture()
        {
            string archive = LocalStorage.GetFullPath($"third-party-{Guid.NewGuid():N}.osk");
            using var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write);
            using var zip = new ZipArchive(output, ZipArchiveMode.Create);
            using var writer = new StreamWriter(zip.CreateEntry("skin.ini").Open(), new UTF8Encoding(false));
            writer.Write("[General]\nName: Third-party missing parts\nAuthor: Independent fixture\nVersion: latest\n"
                         + "[GameplaySkin.Common:1]\n"
                         + "Target: Global ruleset=any keymode=any stage-mode=any\n"
                         + "decoration: resource Suppress\n");
            return archive;
        }

        private static void setCanonicalViewport(ExactLayoutJourneyHost renderer, int viewport)
        {
            renderer.RelativeSizeAxes = Axes.None;
            renderer.Size = viewport switch
            {
                0 => new Vector2(1280, 720),
                1 => new Vector2(1024, 768),
                2 => new Vector2(1720, 720),
                _ => throw new ArgumentOutOfRangeException(nameof(viewport)),
            };
            renderer.Scale = new Vector2(viewport == 1 ? 1.25f : 1);
        }

        private static void assertCanonicalProductScene(GameplaySkinSceneRuntimeHost scene, string package)
        {
            Assert.That(scene.RuntimeFaults, Is.Empty);
            Assert.That(scene.MaterialSet.Entries, Is.Not.Empty);
            Assert.That(scene.MaterialSet.Entries.Any(entry => entry.Source.Kind == GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback), Is.False,
                "Complete skin and missing user parts must resolve to package content.");
            Assert.That(scene.HostedSlots.Any(slot => slot.Route == GameplaySkinSceneHostRoute.Programmatic), Is.False);
            foreach (GameplaySkinResolvedMaterialEntry entry in scene.MaterialSet.Entries)
            {
                if (entry.Slot.Requirement == SkinSlotRequirement.Critical)
                    Assert.That(entry.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide), entry.Slot.Id);
            }
            if (package == "third-party")
            {
                Assert.That(scene.MaterialSet.Entries.Any(entry => entry.Source.Kind == GameplaySkinResolvedMaterialSourceKind.CanonicalPackage), Is.True);
                Assert.That(scene.MaterialSet.Entries.Single(entry => entry.Slot == GameplaySkinSlotCatalog.Decoration
                    && entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global).State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
            }
            Assert.That(scene.ScriptInstance?.Fault, Is.Null);
        }
    }
}
