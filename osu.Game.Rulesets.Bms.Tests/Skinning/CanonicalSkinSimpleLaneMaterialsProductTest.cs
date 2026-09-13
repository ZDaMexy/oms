// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key9K_Pms, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center)]
        public void TestSimpleLaneContrastAndDividersUseTheActualAuthoredImages(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            addSelectCanonicalProduct("canonical");
            AddStep("mount the built-in simple skin in the actual BMS layout", () =>
            {
                var fixture = new ExactBmsProductionFixture(createC5MatrixChart(keymode),
                    keymode == BmsKeymode.Key9K_Pms ? "simple-lanes.pms" : "simple-lanes.bms", style,
                    initialGameplayTime: 2_000, keymodeOverride: keymode);
                renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture);
                setCanonicalViewport(renderer, 0);
                Add(renderer);
                renderer.ShowBms();
            });
            AddUntilStep("all authored lane images are loaded", () =>
            {
                if (!renderer.BmsReady)
                    return false;
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return scene.IsSceneReady;
            });
            AddStep("each real lane uses its matching package image without programmatic supplementation", () =>
            {
                assertCanonicalProductScene(scene, "canonical");
                foreach (BmsLane lane in renderer.BmsDrawable.Playfield.Lanes)
                {
                    var exact = lane.LayoutSnapshotLane!;
                    int key = keymode is BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms ? exact.LogicalIndex + 1 : exact.LogicalIndex;
                    if (keymode == BmsKeymode.Key14K && key > 7)
                        key -= 7;
                    string role = exact.IsScratch ? "scratch" : key % 2 == 0 ? "accent" : "white";
                    GameplaySkinResolvedMaterialTarget target = lane.HitTarget.ResolvedMaterialKey.Target;
                    var surface = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneSurface, target);
                    var divider = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneDivider, target);
                    Assert.That(scene.MaterialSet.TryGet(surface, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    bool selected = entry!.Source.Kind == GameplaySkinResolvedMaterialSourceKind.SelectedPackage;
                    assertPackagedSemanticSurface(scene, surface, selected, "bms/lane-" + role);
                    string dividerResource = exact.IsScratch ? "bms/divider-scratch"
                        : role == "accent" && keymode is not (BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms) ? "bms/divider-accent" : "bms/divider";
                    assertPackagedSemanticSurface(scene, divider, selected, dividerResource);
                    Assert.That(lane.GameplaySkinLaneSurfaceFallbackVisual.Alpha, Is.Zero);
                    Assert.That(lane.GameplaySkinLaneDividerFallbackVisual.Alpha, Is.Zero);
                }
            });
            AddStep("the shipped artwork has visible lane contrast and equally weighted separators", () =>
            {
                using ZipArchive archive = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, "Skins", "Canonical", "oms-simple.osk"));
                using Image<Rgba32> white = readSimpleLaneImage(archive, "bms/lane-white.png");
                using Image<Rgba32> accent = readSimpleLaneImage(archive, "bms/lane-accent.png");
                using Image<Rgba32> scratch = readSimpleLaneImage(archive, "bms/lane-scratch.png");
                Assert.That(white[white.Width / 2, white.Height / 2], Is.Not.EqualTo(accent[accent.Width / 2, accent.Height / 2]),
                    "White-key and black-key lanes must have different visible backgrounds.");
                using Image<Rgba32> divider = readSimpleLaneImage(archive, "bms/divider.png");
                using Image<Rgba32> scratchDivider = readSimpleLaneImage(archive, "bms/divider-scratch.png");
                using Image<Rgba32> accentDivider = readSimpleLaneImage(archive, "bms/divider-accent.png");
                int regularBand = Enumerable.Range(0, divider.Width).Count(x => divider[x, divider.Height / 2].A > 0);
                int scratchBand = Enumerable.Range(0, scratchDivider.Width).Count(x => scratchDivider[x, scratchDivider.Height / 2].A > 0);
                int accentBand = Enumerable.Range(0, accentDivider.Width).Count(x => accentDivider[x, accentDivider.Height / 2].A > 0);
                Assert.That(scratchBand, Is.LessThan(regularBand));
                Assert.That(scratchBand * 1.7037037 / scratchDivider.Width,
                    Is.EqualTo((double)regularBand / divider.Width).Within(1.0 / divider.Width),
                    "The wider scratch column must not make its separator visually heavier.");
                Assert.That(accentBand * 0.7777778 / accentDivider.Width,
                    Is.EqualTo((double)regularBand / divider.Width).Within(1.0 / divider.Width),
                    "The narrower black-key column must retain the same visible separator weight.");
            });
            AddStep("leave the simple playfield", () => renderer.Expire());
            AddUntilStep("the simple lane consumers detach", () => renderer.Parent == null);
        }

        private static Image<Rgba32> readSimpleLaneImage(ZipArchive archive, string path)
        {
            ZipArchiveEntry? entry = archive.GetEntry(path);
            Assert.That(entry, Is.Not.Null, path);
            using Stream source = entry!.Open();
            Image<Rgba32> image = Image.Load<Rgba32>(source);
            Assert.That(Enumerable.Range(0, image.Width).Any(x => image[x, image.Height / 2].A > 0), Is.True,
                $"{path} must contain visible artwork.");
            return image;
        }
    }
}
