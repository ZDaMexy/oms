// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase("oms-simple")]
        [TestCase("oms-complex")]
        [TestCase("aurora-study")]
        public void TestCanonicalProductsRetainActualPressedKeysWithoutRequiringOptionalFlashes(string package)
        {
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            Drawable bmsKey = null!;
            Sprite maniaReleased = null!;
            Sprite maniaPressed = null!;

            addSelectAuthoredProduct(package);
            AddStep("mount both real playfields with ordinary imported key artwork", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager);
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both package scene graphs are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("released and pressed mania artwork both belong to the imported author package", () =>
            {
                BmsHitTarget target = renderer.BmsDrawable.ChildrenOfType<BmsHitTarget>().Single(hit =>
                    hit.ResolvedMaterialKey.Target.LaneId?.Value == "bms.lane.key-1");
                Assert.That(target.SceneVisualGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                GameplaySkinSpecialisedSceneVisual keyVisual = target.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(visual => visual.Key.Equals(target.ResolvedMaterialKey));
                Assert.That(keyVisual.IsApplied, Is.True);
                Assert.That(keyVisual.Alpha, Is.EqualTo(1), "The publication visibility gate is independent of key brightness.");
                Assert.That(keyVisual.RuntimeNodes, Is.Empty, "The product key uses an ordinary package texture.");
                bmsKey = keyVisual.RootDrawables.Single();
                Assert.That(bmsKey, Is.InstanceOf<Sprite>());
                Assert.That(((Sprite)bmsKey).Texture, Is.Not.Null);
                var column = renderer.ManiaDrawable.Playfield.Stages[0].Columns[0];
                Assert.That(mania.MaterialSet.TryGet(column.ResolvedMaterialKey, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                Assert.That(entry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                LegacyKeyArea area = column.ChildrenOfType<LegacyKeyArea>().Single();
                Sprite[] keyDrawings = area.ChildrenOfType<Sprite>().ToArray();
                Assert.That(keyDrawings, Has.Length.EqualTo(2));
                maniaReleased = keyDrawings.Single(sprite => sprite.Alpha == 1);
                maniaPressed = keyDrawings.Single(sprite => sprite.Alpha == 0);
                Assert.That(maniaReleased.Texture, Is.Not.Null);
                Assert.That(maniaPressed.Texture, Is.Not.Null);
                Assert.That(maniaPressed.Texture, Is.Not.SameAs(maniaReleased.Texture), "The actual ordinary KeyImageD drawing must have its own pressed artwork.");
                Assert.That(bmsKey.Alpha, Is.EqualTo(0.65f));
                Assert.That(maniaReleased.Alpha, Is.EqualTo(1));
                Assert.That(maniaPressed.Alpha, Is.Zero);
                if (package != "oms-complex")
                {
                    Assert.That(bms.MaterialSet.Entries.Where(item => ReferenceEquals(item.Slot, GameplaySkinSlotCatalog.KeyFlash))
                                   .All(item => item.State == GameplaySkinResolvedMaterialState.Suppress), Is.True);
                    Assert.That(mania.MaterialSet.Entries.Where(item => ReferenceEquals(item.Slot, GameplaySkinSlotCatalog.KeyFlash))
                                   .All(item => item.State == GameplaySkinResolvedMaterialState.Suppress), Is.True);
                }
            });
            AddStep("press the real BMS and mania input producers", () =>
            {
                c6Input(renderer, true);
                clock.Sample(1_020);
            });
            AddUntilStep("both real key drawings show the pressed state", () =>
                bmsKey.Alpha == 1 && maniaReleased.Alpha == 0 && maniaPressed.Alpha == 1);
            AddStep("release both real inputs", () =>
            {
                c6Input(renderer, false);
                for (int time = 1_040; time <= 1_320; time += 20)
                    clock.Sample(time);
            });
            AddUntilStep("both real key drawings return to released state", () =>
                bmsKey.Alpha == 0.65f && maniaReleased.Alpha == 1 && maniaPressed.Alpha == 0);
            AddStep("release the complete imported skin fixture", () =>
            {
                Assert.That(bms.RuntimeFaults, Is.Empty);
                Assert.That(mania.RuntimeFaults, Is.Empty);
                renderer.Expire();
            });
        }

        private void addSelectAuthoredProduct(string package)
        {
            if (package != "aurora-study")
            {
                addSelectCanonicalProduct(package);
                return;
            }

            Task<Live<SkinInfo>>? import = null;
            Live<SkinInfo>? imported = null;
            AddStep("import the author exercise from its ordinary disposable copy", () =>
            {
                string copy = LocalStorage.GetFullPath($"aurora-study-{Guid.NewGuid():N}.osk");
                File.Copy(Path.Combine(AppContext.BaseDirectory, "SkinAuthoringSamples", "aurora-study.osk"), copy);
                File.SetAttributes(copy, FileAttributes.Normal);
                import = Task.Run(async () => await manager.Import(new ImportTask(copy)).ConfigureAwait(false));
            });
            AddUntilStep("the ordinary author exercise import completes", () => import?.IsCompleted == true);
            AddStep("select the ordinary author exercise record", () =>
            {
                imported = import!.GetAwaiter().GetResult();
                Assert.That(imported.PerformRead(info => info.Protected), Is.False);
                manager.CurrentSkinInfo.Value = imported;
            });
            AddUntilStep("the author exercise is current", () =>
                imported != null && manager.CurrentSkin.Value.SkinInfo.ID == imported.ID);
        }
    }
}
