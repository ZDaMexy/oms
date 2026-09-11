// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        private static IEnumerable<TestCaseData> authoredInformationCases()
        {
            foreach (string package in new[] { "oms-simple", "oms-complex", "aurora-study" })
                foreach (bool dual in new[] { false, true })
                    foreach (int viewport in Enumerable.Range(0, 3))
                        yield return new TestCaseData(package, dual, viewport);
        }

        [TestCaseSource(nameof(authoredInformationCases))]
        public void TestAuthoredTopInformationUsesActualScreenBoundsAndGameplayValues(string package, bool dual, int viewport)
        {
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            string prefix = package == "oms-complex" ? "astral.console" : "still.hud";
            float bmsProgress = 0, maniaProgress = 0;

            addSelectAuthoredProduct(package);
            AddStep("mount ordinary complete skins with the narrowest or widest mania stage", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager, maniaStageColumns: dual ? new[] { 10, 10 } : new[] { 1 })
                {
                    RelativeSizeAxes = Axes.None,
                    Size = viewport switch
                    {
                        0 => new Vector2(1280, 720),
                        1 => new Vector2(640, 480),
                        2 => new Vector2(1720, 720),
                        _ => throw new ArgumentOutOfRangeException(nameof(viewport)),
                    },
                    Scale = new Vector2(viewport == 1 ? 1.5f : 1)
                };
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both authored information bars and actual fonts have loaded", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return new[] { bms, mania }.All(scene => scene.IsSceneReady
                    && scene.TryGetRuntimeNode(prefix + ".score", out GameplaySkinSceneRuntimeNode? score)
                    && score!.ContentDrawable.IsLoaded && score.ContentDrawable.DrawWidth > 0);
            });
            AddStep("before playing every necessary field is visible in the top bar", () =>
            {
                assertAuthoredInformation(bms, prefix);
                assertAuthoredInformation(mania, prefix);
                Assert.That(c6CandidateNode(bms, prefix + ".progress").TransformDrawable.Width, Is.Zero);
                Assert.That(c6CandidateNode(mania, prefix + ".progress").TransformDrawable.Width, Is.Zero);
                if (package == "oms-complex")
                {
                    Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!.RequiredSatisfied, Is.False);
                    Assert.That(c6CandidateNode(bms, prefix + ".judgement").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(c6CandidateNode(mania, prefix + ".judgement").TransformDrawable.Alpha, Is.Zero);
                }
            });
            AddStep("advance the actual playfields toward the first notes", () =>
            {
                for (int time = 1_050; time <= 1_950; time += 50)
                    clock.Sample(time);
            });
            AddUntilStep("both real first-key notes are loaded and ready for input", () =>
                renderer.BmsDrawable.ChildrenOfType<DrawableBmsHitObject>().Any(note => note.IsLoaded && note.IsPresent
                    && note.HandleUserInput && note.HitObject is BmsHitObject { LaneIndex: 1, StartTime: 2_000 })
                && renderer.ManiaDrawable.ChildrenOfType<DrawableNote>().Any(note => note.IsLoaded && note.IsPresent
                    && note.HitObject.Column == 0 && note.HitObject.StartTime == 2_000));
            AddStep("judge real notes through the BMS and mania input producers", () =>
            {
                clock.Sample(2_000);
                c6Input(renderer, true);
                clock.Sample(2_020);
            });
            AddUntilStep("both top bars display score and combo from actual successful input", () =>
                authoredInformationValue(bms, prefix + ".score") != "0"
                && authoredInformationValue(mania, prefix + ".score") != "0"
                && authoredInformationValue(bms, prefix + ".combo") != "0"
                && authoredInformationValue(mania, prefix + ".combo") != "0");
            AddStep("longer score text still fits and matches the live read-only state", () =>
            {
                assertAuthoredInformation(bms, prefix);
                assertAuthoredInformation(mania, prefix);
                if (package == "oms-complex")
                {
                    Assert.That(c6CandidateNode(bms, prefix + ".judgement").TransformDrawable.Alpha, Is.EqualTo(1));
                    Assert.That(c6CandidateNode(mania, prefix + ".judgement").TransformDrawable.Alpha, Is.EqualTo(1));
                }
                c6Input(renderer, false);
                for (int time = 2_040; time <= 3_500; time += 20)
                    clock.Sample(time);
            });
            AddStep("misses and elapsed playable duration reach the same complete information bar", () =>
            {
                assertAuthoredInformation(bms, prefix);
                assertAuthoredInformation(mania, prefix);
                Assert.That(authoredInformationSnapshot(bms).Score.Accuracy, Is.LessThan(1));
                Assert.That(authoredInformationSnapshot(mania).Score.Accuracy, Is.LessThan(1));
                if (package == "oms-complex")
                {
                    foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                    {
                        Assert.That(authoredInformationSnapshot(scene).CurrentJudgements.Any(judgement =>
                            judgement.Scope == GameplaySkinJudgementScope.Global && judgement.Judgement.Grade == GameplaySkinJudgementGrade.Miss), Is.True);
                        Assert.That(authoredInformationValue(scene, prefix + ".judgement"), Is.EqualTo("miss"));
                        Assert.That(c6CandidateNode(scene, prefix + ".judgement").TransformDrawable.Alpha, Is.EqualTo(1), "A genuine current Miss remains visible without optional script permission.");
                    }
                }
                bmsProgress = c6CandidateNode(bms, prefix + ".progress").TransformDrawable.Width;
                maniaProgress = c6CandidateNode(mania, prefix + ".progress").TransformDrawable.Width;
                Assert.That(bmsProgress, Is.GreaterThan(0));
                Assert.That(maniaProgress, Is.GreaterThan(0));
                clock.Stop();
            });
            AddWaitStep("allow UI frames while both real playfields are paused", 3);
            AddStep("paused complete information remains visible and its progress does not advance", () =>
            {
                assertAuthoredInformation(bms, prefix);
                assertAuthoredInformation(mania, prefix);
                Assert.That(c6CandidateNode(bms, prefix + ".progress").TransformDrawable.Width, Is.EqualTo(bmsProgress));
                Assert.That(c6CandidateNode(mania, prefix + ".progress").TransformDrawable.Width, Is.EqualTo(maniaProgress));
                clock.SoftUnpause();
                for (int time = 3_520; time <= 6_000; time += 20)
                    clock.Sample(time);
            });
            AddStep("expired judgements leave no misleading miss label after all notes have finished", () =>
            {
                assertAuthoredInformation(bms, prefix);
                assertAuthoredInformation(mania, prefix);
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(authoredInformationSnapshot(scene).CurrentJudgements.Any(judgement => judgement.Scope == GameplaySkinJudgementScope.Global), Is.False);
                    if (package == "oms-complex")
                        Assert.That(c6CandidateNode(scene, prefix + ".judgement").TransformDrawable.Alpha, Is.Zero);
                }
                renderer.Expire();
            });
            AddUntilStep("the ordinary information consumers have detached", () => renderer.Parent == null);
        }

        private static void assertAuthoredInformation(GameplaySkinSceneRuntimeHost scene, string prefix)
        {
            Assert.That(scene.RuntimeFaults, Is.Empty);
            GameplaySkinEventStateSnapshot state = authoredInformationSnapshot(scene);
            Assert.That(authoredInformationValue(scene, prefix + ".score"), Is.EqualTo(state.Score.Score.ToString(CultureInfo.InvariantCulture)));
            Assert.That(authoredInformationValue(scene, prefix + ".accuracy"),
                Is.EqualTo((state.Score.Accuracy * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%"));
            Assert.That(authoredInformationValue(scene, prefix + ".combo"), Is.EqualTo(state.Score.Combo.ToString(CultureInfo.InvariantCulture)));
            Assert.That(authoredInformationValue(scene, prefix + ".bpm"), Is.EqualTo(state.Timing.Bpm.ToString("0.###", CultureInfo.InvariantCulture)));
            Assert.That(c6CandidateNode(scene, prefix + ".progress").TransformDrawable.Width, Is.EqualTo(state.Timing.Progress).Within(0.00001));

            GameplaySkinResolvedMaterialEntry global = scene.MaterialSet.Entries.Single(entry =>
                ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.TextHud) && entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global);
            Assert.That(global.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
            Assert.That(scene.MaterialSet.Entries.Where(entry => ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.TextHud)
                                                               && entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Stage)
                             .All(entry => entry.State == GameplaySkinResolvedMaterialState.Suppress), Is.True);
            Assert.That(scene.TryGetVisualGate(global.Key, out GameplaySkinSceneHostedSlot? gate), Is.True);
            Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Scene));
            Assert.That(gate.IsReplacementReady, Is.True);

            GameplaySkinSceneRuntimeNode panel = c6CandidateNode(scene, prefix + ".panel");
            var screen = panel.RootDrawable.ScreenSpaceDrawQuad.AABBFloat;
            var panelBounds = panel.ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
            Assert.That(panel.Rect, Is.EqualTo(scene.PreparedScene.Snapshot.Context.SafeBounds),
                "An explicit global child uses the safe screen rather than its parent's legacy HUD band.");
            Assert.That(panelBounds.Top, Is.InRange(screen.Top, screen.Top + screen.Height * 0.02f));
            Assert.That(panelBounds.Bottom, Is.LessThanOrEqualTo(screen.Top + screen.Height * 0.10f));

            string[] fields = { "score", "accuracy", "combo", "bpm" };
            for (int index = 0; index < fields.Length; index++)
            {
                GameplaySkinSceneRuntimeNode node = c6CandidateNode(scene, prefix + "." + fields[index]);
                SpriteText text = (SpriteText)node.ContentDrawable;
                var bounds = text.ScreenSpaceDrawQuad.AABBFloat;
                var label = c6CandidateNode(scene, prefix + "." + fields[index] + "-label").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                Assert.That(node.Rect, Is.EqualTo(scene.PreparedScene.Snapshot.Context.SafeBounds));
                Assert.That(text.IsPresent, Is.True);
                Assert.That(bounds.Width, Is.GreaterThan(0));
                Assert.That(bounds.Height, Is.GreaterThan(0));
                Assert.That(bounds.Left, Is.GreaterThanOrEqualTo(panelBounds.Left - 1), fields[index]);
                Assert.That(bounds.Right, Is.LessThanOrEqualTo(panelBounds.Right + 1), fields[index]);
                Assert.That(bounds.Top, Is.GreaterThanOrEqualTo(panelBounds.Top - 1), fields[index]);
                Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(panelBounds.Bottom + 1), fields[index]);
                Assert.That(label.Bottom, Is.LessThanOrEqualTo(bounds.Top + 1), fields[index] + " label overlaps its value");
                if (index + 1 < fields.Length)
                {
                    float nextLeft = c6CandidateNode(scene, prefix + "." + fields[index + 1]).ContentDrawable.ScreenSpaceDrawQuad.AABBFloat.Left;
                    Assert.That(bounds.Right, Is.LessThan(nextLeft), fields[index] + " overlaps the next value");
                }
            }
            if (prefix == "astral.console")
            {
                bool hasCurrentJudgement = state.CurrentJudgements.Any(judgement => judgement.Scope == GameplaySkinJudgementScope.Global);
                GameplaySkinSceneRuntimeNode judgementNode = c6CandidateNode(scene, prefix + ".judgement");
                Assert.That(judgementNode.TransformDrawable.Alpha, Is.EqualTo(hasCurrentJudgement ? 1 : 0));
                if (hasCurrentJudgement)
                {
                    GameplaySkinCurrentJudgementStateSnapshot current = state.CurrentJudgements.Single(judgement => judgement.Scope == GameplaySkinJudgementScope.Global);
                    Assert.That(authoredInformationValue(scene, prefix + ".judgement"), Is.EqualTo(current.Judgement.Grade.ToString().ToLowerInvariant()));
                }
                foreach (string field in new[] { "brand", "status", "judgement" })
                {
                    SpriteText text = (SpriteText)c6CandidateNode(scene, prefix + "." + field).ContentDrawable;
                    var bounds = text.ScreenSpaceDrawQuad.AABBFloat;
                    Assert.That(text.Text.ToString(), Is.Not.Empty, field);
                    if (field != "judgement" || hasCurrentJudgement)
                        Assert.That(text.IsPresent, Is.True, field);
                    Assert.That(bounds.Width, Is.GreaterThan(0), field);
                    Assert.That(bounds.Height, Is.GreaterThan(0), field);
                    Assert.That(bounds.Left, Is.GreaterThanOrEqualTo(panelBounds.Left - 1), field);
                    Assert.That(bounds.Right, Is.LessThanOrEqualTo(panelBounds.Right + 1), field);
                    Assert.That(bounds.Top, Is.GreaterThanOrEqualTo(panelBounds.Top - 1), field);
                    Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(panelBounds.Bottom + 1), field);
                }

                var brand = c6CandidateNode(scene, prefix + ".brand").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var status = c6CandidateNode(scene, prefix + ".status").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var score = c6CandidateNode(scene, prefix + ".score").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var scoreLabel = c6CandidateNode(scene, prefix + ".score-label").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var bpm = c6CandidateNode(scene, prefix + ".bpm").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var bpmLabel = c6CandidateNode(scene, prefix + ".bpm-label").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var judgement = c6CandidateNode(scene, prefix + ".judgement").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                Assert.That(brand.Bottom, Is.LessThanOrEqualTo(status.Top + 1), "The brand overlaps the current playing or paused state.");
                Assert.That(Math.Max(brand.Right, status.Right), Is.LessThan(Math.Min(score.Left, scoreLabel.Left)), "The brand or state overlaps the score column.");
                Assert.That(Math.Max(bpm.Right, bpmLabel.Right), Is.LessThan(judgement.Left), "The tempo overlaps the rightmost judgement.");
            }
            var progress = c6CandidateNode(scene, prefix + ".progress").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
            var track = c6CandidateNode(scene, prefix + ".progress-track").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
            if (state.Timing.Progress == 0)
                Assert.That(progress.Width, Is.Zero, "Before the first object the bar has no residual visible progress line.");
            Assert.That(progress.Width, Is.EqualTo(track.Width * state.Timing.Progress).Within(1));
            Assert.That(track.Top, Is.GreaterThanOrEqualTo(panelBounds.Bottom - 1));
            Assert.That(track.Bottom, Is.LessThanOrEqualTo(screen.Top + screen.Height * 0.10f));
            Assert.That(track.Left, Is.GreaterThanOrEqualTo(screen.Left));
            Assert.That(track.Right, Is.LessThanOrEqualTo(screen.Right));
        }

        private static string authoredInformationValue(GameplaySkinSceneRuntimeHost scene, string id)
            => ((SpriteText)c6CandidateNode(scene, id).ContentDrawable).Text.ToString();

        private static GameplaySkinEventStateSnapshot authoredInformationSnapshot(GameplaySkinSceneRuntimeHost scene)
        {
            GameplaySkinEventStateSnapshot? state = null;
            using GameplaySkinEventSubscription observer = scene.EventStream.Subscribe();
            observer.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinStateEventPayload snapshot)
                    state = snapshot.State;
            });
            Assert.That(state, Is.Not.Null, "A newly attached ordinary observer receives the real complete gameplay state.");
            return state!;
        }
    }
}
