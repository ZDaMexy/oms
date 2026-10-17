// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        private const string projection_chart = "#TITLE Native projection\n#BPM 120\n#LNTYPE 1\n#WAV01 note.wav\n#00151:01000100\n#00811:01\n";

        [TestCase(BmsLongNoteMode.LN, GameplaySkinLongNoteMode.Standard, 13, 23, 37)]
        [TestCase(BmsLongNoteMode.CN, GameplaySkinLongNoteMode.Charge, 17, 29, 41)]
        [TestCase(BmsLongNoteMode.HCN, GameplaySkinLongNoteMode.HellCharge, 19, 31, 43)]
        public void TestNativeLongNoteAppearanceSelectsModeHoldingReleaseAndHcnRegrabTextures(
            BmsLongNoteMode mode, GameplaySkinLongNoteMode expectedMode, int idleWidth, int holdingWidth, int releasedWidth)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            DrawableBmsHoldNote hold = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            GameplaySkinSceneRuntimeNode body = null!;
            GameplaySkinEventSubscription subscription = null!;
            var events = new List<GameplaySkinEventEnvelope>();
            long objectId = -1;

            AddStep("select typed native-body author package", () =>
            {
                (_, candidate) = createCandidate(writeReadOnlyProjectionPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("typed package revision committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                                                                 && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount actual pooled long note with real mode mods", () =>
            {
                Mod[] mods = mode switch
                {
                    BmsLongNoteMode.LN => Array.Empty<Mod>(),
                    BmsLongNoteMode.CN => new Mod[] { new BmsModChargeNote() },
                    BmsLongNoteMode.HCN => new Mod[] { new BmsModHellChargeNote() },
                    _ => throw new ArgumentOutOfRangeException(nameof(mode)),
                };
                var fixture = new ExactBmsProductionFixture(projection_chart, "projection.bms", BmsPlayfieldStyle.P1, mods,
                    keymodeOverride: BmsKeymode.Key5K);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                Assert.That(renderer.BmsBeatmap.BmsInfo.Keymode, Is.EqualTo(BmsKeymode.Key5K));
                renderer.ShowBms();
            });
            AddUntilStep("real long-note body owns its specialised scene clone", () =>
            {
                if (!renderer.BmsReady || renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady != true)
                    return false;

                hold = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().SingleOrDefault()!;
                if (hold == null)
                    return false;

                BmsAsyncNoteDrawable? surface = hold.ChildrenOfType<BmsAsyncNoteDrawable>()
                                                   .SingleOrDefault(candidateSurface => candidateSurface.Lookup.Element == BmsNoteSkinElements.LongNoteBody);
                if (surface?.SpecialisedSceneVisual?.RuntimeNodes.Count != 1)
                    return false;

                body = surface.SpecialisedSceneVisual.RuntimeNodes.Single();
                sceneHost = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                return body.ContentDrawable is Sprite;
            });
            AddStep("native body consumes actual object mode before the head", () =>
            {
                Assert.That(((BmsHoldNote)hold.HitObject).LongNoteMode, Is.EqualTo(mode));
                sceneHost.ProcessFrame();
                assertBodyTexture(idleWidth);
                objectId = renderer.BmsDrawable.GetGameplaySkinObjectId(hold.HitObject);
                subscription = renderer.BmsDrawable.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(events.Add);
                Assert.That(((GameplaySkinStateEventPayload)events.Single().Payload).State.ActiveObjects
                    .Single(obj => obj.ObjectId == objectId).LongNoteMode, Is.EqualTo(expectedMode));
            });
            AddStep("apply a real head press on the pooled owner", () =>
            {
                renderer.AdvanceBmsTo(hold.HitObject.StartTime);
                Assert.That(hold.TryApplyHeadPress(HitResult.Perfect), Is.True);
                renderer.AdvanceBmsTo(hold.HitObject.StartTime + 1);
            });
            AddUntilStep("typed holding variant reaches the native sprite", () =>
            {
                sceneHost.ProcessFrame();
                return hold.BodyState.Value == BmsLongNoteBodyState.Holding && ((Sprite)body.ContentDrawable).Texture!.Width == holdingWidth;
            });
            AddStep("release the actual long-note input", () =>
            {
                renderer.AdvanceBmsTo(hold.HitObject.StartTime + 30);
                hold.OnReleased(new KeyBindingReleaseEvent<BmsAction>(new InputState(), BmsAction.Key1));
            });
            AddUntilStep("typed released variant reaches the native sprite", () =>
            {
                sceneHost.ProcessFrame();
                return hold.BodyState.Value == BmsLongNoteBodyState.Broken && ((Sprite)body.ContentDrawable).Texture!.Width == releasedWidth;
            });

            if (mode == BmsLongNoteMode.HCN)
            {
                AddStep("re-grab the actual HCN body", () =>
                {
                    renderer.AdvanceBmsTo(hold.HitObject.StartTime + 70);
                    Assert.That(hold.TryApplyLateBodyPress(), Is.True);
                });
                AddUntilStep("same pooled HCN sprite returns to holding variant", () =>
                {
                    sceneHost.ProcessFrame();
                    return hold.BodyState.Value == BmsLongNoteBodyState.Holding && ((Sprite)body.ContentDrawable).Texture!.Width == holdingWidth;
                });
            }

            AddStep("native object edges retain typed mode and scene stays healthy", () =>
            {
                subscription.DrainFrame(events.Add);
                GameplaySkinObjectStateSnapshot[] states = events.Where(envelope => envelope.Payload is GameplaySkinObjectEventPayload obj && obj.State.ObjectId == objectId)
                    .Select(envelope => ((GameplaySkinObjectEventPayload)envelope.Payload).State).ToArray();
                Assert.That(states, Is.Not.Empty);
                Assert.That(states.Select(state => state.LongNoteMode), Is.All.EqualTo(expectedMode));
                Assert.That(sceneHost.RuntimeFaults, Is.Empty);
                subscription.Dispose();
            });

            void assertBodyTexture(int width) => Assert.That(((Sprite)body.ContentDrawable).Texture!.Width, Is.EqualTo(width));
        }

        [Test]
        public void TestNativeGaugeClearStateChangePublishesWithoutNumericHealthChange()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            DrawableBmsHoldNote hold = null!;
            GameplaySkinEventSubscription subscription = null!;
            var events = new List<GameplaySkinEventEnvelope>();
            double unchangedHealth = -1;

            AddStep("select actual gauge projection author package", () =>
            {
                (_, candidate) = createCandidate(writeReadOnlyProjectionPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("gauge package revision committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                                                                 && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount actual Assist Easy processor and renderer", () =>
            {
                var fixture = new ExactBmsProductionFixture(projection_chart, "gauge-projection.bms", BmsPlayfieldStyle.P1,
                    keymodeOverride: BmsKeymode.Key5K);
                Add(renderer = new ExactLayoutJourneyHost(manager, bmsRulesetOverride: new GaugeProjectionRuleset(), exactBmsFixture: fixture));
                Assert.That(renderer.BmsBeatmap.BmsInfo.Keymode, Is.EqualTo(BmsKeymode.Key5K));
                renderer.ShowBms();
            });
            AddUntilStep("actual gauge HUD and pooled hold loaded", () =>
            {
                if (!renderer.BmsReady || renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady != true)
                    return false;

                hold = renderer.BmsDrawable.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().SingleOrDefault()!;
                return hold?.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Count() == 1;
            });
            AddStep("judge actual head and establish an uncleared numeric gauge", () =>
            {
                renderer.AdvanceBmsTo(hold.HitObject.StartTime);
                Assert.That(hold.TryApplyHeadPress(HitResult.Perfect), Is.True);
                renderer.ProjectionGauge.Health.Value = 0.7;
                unchangedHealth = renderer.ProjectionGauge.Health.Value;
                Assert.That(renderer.ProjectionGauge.JudgedHits, Is.GreaterThan(0));
                Assert.That(renderer.ProjectionGauge.IsClear, Is.False);
                renderer.AdvanceBmsTo(hold.HitObject.StartTime + 1);
                subscription = renderer.BmsDrawable.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(_ => { });
            });
            AddStep("switch actual gauge rule family while keeping Health.Value", () =>
            {
                renderer.ProjectionGauge.SetGaugeRulesFamily(BmsGaugeRulesFamily.IIDX);
                Assert.That(renderer.ProjectionGauge.Health.Value, Is.EqualTo(unchangedHealth));
            });
            AddUntilStep("existing GaugeChanged carries actual new clear semantics", () =>
            {
                subscription.DrainFrame(events.Add);
                return events.Any(envelope => envelope.EventKind == GameplaySkinEventKind.GaugeChanged
                    && ((GameplaySkinScoreEventPayload)envelope.Payload).State.GaugeState.ClearState == GameplaySkinGaugeClearState.Clear);
            });
            AddStep("actual author HUD receives clear state without health movement", () =>
            {
                GameplaySkinSceneRuntimeHost scene = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                scene.ProcessFrame();
                Assert.That(scene.TryGetRuntimeNode("projection.gauge-clear", out GameplaySkinSceneRuntimeNode? clear), Is.True);
                Assert.That(scene.TryGetRuntimeNode("projection.gauge-type", out GameplaySkinSceneRuntimeNode? type), Is.True);
                GameplaySkinScoreStateSnapshot state = events.Where(envelope => envelope.EventKind == GameplaySkinEventKind.GaugeChanged)
                    .Select(envelope => ((GameplaySkinScoreEventPayload)envelope.Payload).State).Last();
                Assert.Multiple(() =>
                {
                    Assert.That(state.Gauge, Is.EqualTo(unchangedHealth));
                    Assert.That(state.GaugeState, Is.EqualTo(((IGameplaySkinGaugeStateSource)renderer.ProjectionGauge).GameplaySkinGaugeState));
                    Assert.That(state.GaugeState.ClearThreshold, Is.EqualTo(0.6));
                    Assert.That(((SpriteText)clear!.ContentDrawable).Text.ToString(), Is.EqualTo("clear"));
                    Assert.That(((SpriteText)type!.ContentDrawable).Text.ToString(), Is.EqualTo("assist-easy"));
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                });
                subscription.Dispose();
            });
        }

        private static void writeReadOnlyProjectionPackage(string root)
        {
            // The shared fixture provides both exact 5K lane materials and the selected package's 5K Global
            // hud.text material consumed by projection.hud below. Sparse charts must retain that same keymode.
            writeLongNoteIdentityFiveKeyPackage(root);
            string manifestPath = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var cases = new JArray();
            (string Key, int Width)[] appearances =
            {
                ("ln", 13), ("cn", 17), ("hcn", 19),
                ("ln-holding", 23), ("cn-holding", 29), ("hcn-holding", 31),
                ("ln-released", 37), ("cn-released", 41), ("hcn-released", 43),
            };
            foreach ((string key, int width) in appearances)
            {
                string resource = "projection." + key;
                string path = "notes/projection-" + key + ".png";
                using (var image = new Image<Rgba32>(width, 13, new Rgba32(80, 190, 230, 255)))
                using (Stream output = File.Create(Path.Combine(root, path)))
                    image.SaveAsPng(output);

                ((JArray)manifest["resources"]!).Add(new JObject { ["id"] = resource, ["type"] = "texture", ["path"] = path });
                cases.Add(new JObject { ["id"] = "case." + key, ["key"] = key, ["resource"] = resource });
            }

            File.WriteAllText(manifestPath, manifest.ToString());
            string scenePath = Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME);
            JObject scene = JObject.Parse(File.ReadAllText(scenePath));
            scene["stateMachines"] = new JArray();
            scene["variants"] = new JArray(new JObject
            {
                ["id"] = "projection.appearance",
                ["target"] = "node.long-note-body",
                ["property"] = "resource",
                ["source"] = "object.long-note-appearance",
                ["default"] = "texture.public-note",
                ["cases"] = cases,
            });
            ((JArray)scene["root"]!["children"]!).Add(JObject.Parse("""
                {"id":"projection.hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
                  {"id":"projection.gauge-type","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"","font-size":20},"effects":[],"children":[]},
                  {"id":"projection.gauge-clear","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"","font-size":20},"effects":[],"children":[]}]}
                """));
            scene["bindings"] = new JArray(
                new JObject { ["id"] = "projection.bind-type", ["target"] = "projection.gauge-type", ["property"] = "text", ["source"] = "gauge.type" },
                new JObject { ["id"] = "projection.bind-clear", ["target"] = "projection.gauge-clear", ["property"] = "text", ["source"] = "gauge.clear-state" });
            File.WriteAllText(scenePath, scene.ToString());
        }

        private sealed partial class ExactLayoutJourneyHost
        {
            public BmsGaugeProcessor ProjectionGauge => (BmsGaugeProcessor)healthProcessor;
        }

        private sealed class GaugeProjectionRuleset : BmsRuleset
        {
            public override HealthProcessor CreateHealthProcessor(double drainStartTime) => new BmsGaugeProcessor(drainStartTime, BmsGaugeType.AssistEasy);
        }
    }
}
