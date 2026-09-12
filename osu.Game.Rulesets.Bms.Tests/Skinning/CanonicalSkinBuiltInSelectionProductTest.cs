// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestBuiltInComplexPlaysBmsAndManiaWithoutImport()
        {
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;

            AddStep("choose the bundled complex skin directly", () =>
            {
                Assert.That(CanonicalSkinPackage.IsBuiltInSkin(manager.BuiltInComplexSkin), Is.True);
                Assert.That(manager.BuiltInComplexSkin.SkinInfo.PerformRead(info => info.Protected), Is.True);
                manager.CurrentSkinInfo.Value = manager.BuiltInComplexSkin.SkinInfo;
            });
            AddUntilStep("the built-in instance is the selected package", () =>
                ReferenceEquals(manager.CurrentSkin.Value, manager.BuiltInComplexSkin));
            AddStep("enter the actual BMS and mania playfields", () =>
            {
                // The explicit stage fixture schedules its first mania note at 2,000 ms, matching the BMS chart.
                renderer = new ExactLayoutJourneyHost(manager, maniaStageColumns: new[] { 4 });
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both complete package scenes are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("both playfields consume the selected built-in publication", () =>
            {
                assertCanonicalProductScene(bms, "oms-complex");
                assertCanonicalProductScene(mania, "oms-complex");
                Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.BuiltInComplexSkin));
                Assert.That(bms.PreparedScene.Snapshot, Is.SameAs(renderer.BmsLayoutProbe.Publication!.Snapshot));
                Assert.That(mania.PreparedScene.Snapshot, Is.SameAs(renderer.ManiaLayoutProbe.Publication!.Snapshot));
                Assert.That(bms.TryGetRuntimeNode("astral.console.score", out _), Is.True);
                Assert.That(mania.TryGetRuntimeNode("astral.console.score", out _), Is.True);
                for (int time = 1_050; time <= 1_950; time += 50)
                    clock.Sample(time);
            });
            AddUntilStep("real notes in both rulesets are ready for player input", () =>
                renderer.BmsDrawable.ChildrenOfType<DrawableBmsHitObject>().Any(note => note.IsLoaded && note.IsPresent
                    && note.HandleUserInput && note.HitObject is BmsHitObject { LaneIndex: 1, StartTime: 2_000 })
                && renderer.ManiaDrawable.ChildrenOfType<DrawableNote>().Any(note => note.IsLoaded && note.IsPresent
                    && note.HandleUserInput && note.HitObject.Column == 0 && note.HitObject.StartTime == 2_000));
            AddStep("play the first notes through both real input producers", () =>
            {
                clock.Sample(2_000);
                c6Input(renderer, true);
                clock.Sample(2_020);
            });
            AddUntilStep("both built-in score displays show successful play", () =>
                authoredInformationSnapshot(bms).Score.Score > 0 && authoredInformationSnapshot(mania).Score.Score > 0
                && authoredInformationValue(bms, "astral.console.score") != "0"
                && authoredInformationValue(mania, "astral.console.score") != "0");
            AddStep("release input and leave both playfields", () =>
            {
                c6Input(renderer, false);
                Assert.That(bms.RuntimeFaults, Is.Empty);
                Assert.That(mania.RuntimeFaults, Is.Empty);
                renderer.Expire();
            });
            AddUntilStep("both built-in gameplay consumers detach", () => renderer.Parent == null);
        }
    }
}
