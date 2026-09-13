// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Graphics.Sprites;
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
        public void TestBuiltInSimplePlaysBmsAndManiaWithoutImport()
        {
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;

            AddStep("choose the bundled simple skin directly", () =>
            {
                Assert.That(CanonicalSkinPackage.IsBuiltInSkin(manager.DefaultOmsSkin), Is.True);
                Assert.That(manager.DefaultOmsSkin.SkinInfo.PerformRead(info => info.Protected), Is.True);
                manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo;
            });
            AddUntilStep("the built-in instance is the selected package", () =>
                ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin));
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
                assertCanonicalProductScene(bms, "canonical");
                assertCanonicalProductScene(mania, "canonical");
                Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.DefaultOmsSkin));
                Assert.That(bms.PreparedScene.Snapshot, Is.SameAs(renderer.BmsLayoutProbe.Publication!.Snapshot));
                Assert.That(mania.PreparedScene.Snapshot, Is.SameAs(renderer.ManiaLayoutProbe.Publication!.Snapshot));
                Assert.That(bms.TryGetRuntimeNode("still.hud.score", out _), Is.True);
                Assert.That(mania.TryGetRuntimeNode("still.hud.score", out _), Is.True);
                foreach (string id in new[] { "still.judgement", "still.combo", "still.gauge" })
                {
                    var stage = bms.PreparedScene.Roots.Single(node => node.Source.Id == id);
                    Assert.That(stage.ResolvedTarget.Kind, Is.EqualTo(GameplaySkinSceneTargetKind.Stage));
                    Assert.That(bms.TryGetRuntimeNode(stage.InstanceId, out _), Is.True);
                    Assert.That(stage.Children.All(child => child.Rect.Equals(stage.Rect)), Is.True,
                        "Material template children must stay in their HUD surface, not cover the playfield.");
                    Assert.That(mania.PreparedScene.Roots.Any(node => node.Source.Id == id), Is.False);
                }
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
                && authoredInformationValue(bms, "still.hud.score") != "0"
                && authoredInformationValue(mania, "still.hud.score") != "0");
            AddStep("release input and leave both playfields", () =>
            {
                var gauge = bms.PreparedScene.Roots.Single(node => node.Source.Id == "still.gauge");
                var value = gauge.Children.Single(node => node.Source.Id == "still.gauge.value");
                var reveal = gauge.Children.Single(node => node.Source.Id == "still.gauge.reveal");
                Assert.That(bms.TryGetRuntimeNode(reveal.InstanceId, out var revealNode), Is.True);
                Assert.That(bms.TryGetRuntimeNode(reveal.Children.Single().InstanceId, out var fillNode), Is.True);
                Assert.That(fillNode!.RootDrawable.Parent, Is.SameAs(revealNode!.ContentDrawable));
                Assert.That(bms.TryGetRuntimeNode(value.InstanceId, out var gaugeText), Is.True);
                Assert.That(((SpriteText)gaugeText!.ContentDrawable).Text.ToString(), Does.EndWith("%"));
                var judgement = bms.PreparedScene.Roots.Single(node => node.Source.Id == "still.judgement");
                var result = judgement.Children.Single();
                Assert.That(bms.TryGetRuntimeNode(result.InstanceId, out var resultText), Is.True);
                Assert.That(((SpriteText)resultText!.ContentDrawable).Text.ToString(), Is.EqualTo("PERFECT"));
                c6Input(renderer, false);
                Assert.That(bms.RuntimeFaults, Is.Empty);
                Assert.That(mania.RuntimeFaults, Is.Empty);
                renderer.Expire();
            });
            AddUntilStep("both built-in gameplay consumers detach", () => renderer.Parent == null);
        }
    }
}
