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
        public void TestAuthoredInformationUsesSafeLayoutAndGameplayValues(string package, bool dual, int viewport)
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
                Assert.That(bms.RuntimeFaults, Is.Empty, "BMS information scene failed while loading");
                Assert.That(mania.RuntimeFaults, Is.Empty, "mania information scene failed while loading");
                return new[] { bms, mania }.All(scene => scene.IsSceneReady
                    && scene.TryGetRuntimeNode(authoredInformationId(scene, prefix + ".score"), out GameplaySkinSceneRuntimeNode? score)
                    && score!.ContentDrawable.IsLoaded && score.ContentDrawable.DrawWidth > 0);
            });
            AddStep("before playing every necessary field is visible in its authored information area", () =>
            {
                assertAuthoredInformation(bms, prefix, package == "oms-simple");
                assertAuthoredInformation(mania, prefix, package == "oms-simple");
                Assert.That(authoredInformationNode(bms, prefix + ".progress").TransformDrawable.Width, Is.Zero);
                Assert.That(authoredInformationNode(mania, prefix + ".progress").TransformDrawable.Width, Is.Zero);
                if (package == "oms-complex")
                {
                    Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!.RequiredSatisfied, Is.False);
                    Assert.That(authoredInformationNode(bms, prefix + ".judgement").TransformDrawable.Alpha, Is.Zero);
                    Assert.That(authoredInformationNode(mania, prefix + ".judgement").TransformDrawable.Alpha, Is.Zero);
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
            AddUntilStep("both information areas display score from actual successful input", () =>
                authoredInformationValue(bms, prefix + ".score") != "0"
                && authoredInformationValue(mania, prefix + ".score") != "0"
                && (package == "oms-simple" || (authoredInformationValue(bms, prefix + ".combo") != "0"
                    && authoredInformationValue(mania, prefix + ".combo") != "0")));
            AddStep("longer score text still fits and matches the live read-only state", () =>
            {
                assertAuthoredInformation(bms, prefix, package == "oms-simple");
                assertAuthoredInformation(mania, prefix, package == "oms-simple");
                if (package == "oms-complex")
                {
                    Assert.That(authoredInformationNode(bms, prefix + ".judgement").TransformDrawable.Alpha, Is.EqualTo(1));
                    Assert.That(authoredInformationNode(mania, prefix + ".judgement").TransformDrawable.Alpha, Is.EqualTo(1));
                }
                c6Input(renderer, false);
                for (int time = 2_040; time <= 3_500; time += 20)
                    clock.Sample(time);
            });
            AddStep("misses and elapsed playable duration reach the same complete information bar", () =>
            {
                assertAuthoredInformation(bms, prefix, package == "oms-simple");
                assertAuthoredInformation(mania, prefix, package == "oms-simple");
                if (package == "oms-simple")
                {
                    GameplaySkinJudgementStatistics statistics = authoredInformationSnapshot(bms).Score.Statistics;
                    Assert.That(statistics.Perfect + statistics.Great + statistics.Good, Is.GreaterThan(0), "Real successful input reaches the live judgement counts.");
                    Assert.That(statistics.Miss, Is.GreaterThan(0), "Unplayed notes reach the live miss count.");
                }
                Assert.That(authoredInformationSnapshot(bms).Score.Accuracy, Is.LessThan(1));
                Assert.That(authoredInformationSnapshot(mania).Score.Accuracy, Is.LessThan(1));
                if (package == "oms-complex")
                {
                    foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                    {
                        Assert.That(authoredInformationSnapshot(scene).CurrentJudgements.Any(judgement =>
                            judgement.Scope == GameplaySkinJudgementScope.Global && judgement.Judgement.Grade == GameplaySkinJudgementGrade.Miss), Is.True);
                        Assert.That(authoredInformationValue(scene, prefix + ".judgement"), Is.EqualTo("miss"));
                        Assert.That(authoredInformationNode(scene, prefix + ".judgement").TransformDrawable.Alpha, Is.EqualTo(1), "A genuine current Miss remains visible without optional script permission.");
                    }
                }
                bmsProgress = authoredInformationNode(bms, prefix + ".progress").TransformDrawable.Width;
                maniaProgress = authoredInformationNode(mania, prefix + ".progress").TransformDrawable.Width;
                Assert.That(bmsProgress, Is.GreaterThan(0));
                Assert.That(maniaProgress, Is.GreaterThan(0));
                clock.Stop();
            });
            AddWaitStep("allow UI frames while both real playfields are paused", 3);
            AddStep("paused complete information remains visible and its progress does not advance", () =>
            {
                assertAuthoredInformation(bms, prefix, package == "oms-simple");
                assertAuthoredInformation(mania, prefix, package == "oms-simple");
                Assert.That(authoredInformationNode(bms, prefix + ".progress").TransformDrawable.Width, Is.EqualTo(bmsProgress));
                Assert.That(authoredInformationNode(mania, prefix + ".progress").TransformDrawable.Width, Is.EqualTo(maniaProgress));
                clock.SoftUnpause();
                for (int time = 3_520; time <= 6_000; time += 20)
                    clock.Sample(time);
            });
            AddStep("expired judgements leave no misleading miss label after all notes have finished", () =>
            {
                assertAuthoredInformation(bms, prefix, package == "oms-simple");
                assertAuthoredInformation(mania, prefix, package == "oms-simple");
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(authoredInformationSnapshot(scene).CurrentJudgements.Any(judgement => judgement.Scope == GameplaySkinJudgementScope.Global), Is.False);
                    if (package == "oms-complex")
                        Assert.That(authoredInformationNode(scene, prefix + ".judgement").TransformDrawable.Alpha, Is.Zero);
                }
                renderer.Expire();
            });
            AddUntilStep("the ordinary information consumers have detached", () => renderer.Parent == null);
        }

        private static void assertAuthoredInformation(GameplaySkinSceneRuntimeHost scene, string prefix, bool compact)
        {
            Assert.That(scene.RuntimeFaults, Is.Empty);
            GameplaySkinEventStateSnapshot state = authoredInformationSnapshot(scene);
            Assert.That(authoredInformationValue(scene, prefix + ".score"), Is.EqualTo(state.Score.Score.ToString(CultureInfo.InvariantCulture)));
            Assert.That(authoredInformationValue(scene, prefix + ".accuracy"),
                Is.EqualTo((state.Score.Accuracy * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%"));
            if (compact)
                Assert.That(scene.TryGetRuntimeNode(authoredInformationId(scene, prefix + ".combo"), out _), Is.False, "The information band must not duplicate the lane combo.");
            else
                Assert.That(authoredInformationValue(scene, prefix + ".combo"), Is.EqualTo(state.Score.Combo.ToString(CultureInfo.InvariantCulture)));
            Assert.That(authoredInformationValue(scene, prefix + ".bpm"), Is.EqualTo(state.Timing.Bpm.ToString("0.###", CultureInfo.InvariantCulture)));
            Assert.That(authoredInformationNode(scene, prefix + ".progress").TransformDrawable.Width, Is.EqualTo(state.Timing.Progress).Within(0.00001));

            GameplaySkinResolvedMaterialEntry global = scene.MaterialSet.Entries.Single(entry =>
                ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.TextHud) && entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global);
            Assert.That(global.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
            Assert.That(scene.MaterialSet.Entries.Where(entry => ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.TextHud)
                                                               && entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Stage)
                             .All(entry => entry.State == GameplaySkinResolvedMaterialState.Suppress), Is.True);
            Assert.That(scene.TryGetVisualGate(global.Key, out GameplaySkinSceneHostedSlot? gate), Is.True);
            Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Scene));
            Assert.That(gate.IsReplacementReady, Is.True);

            if (compact && scene.PreparedScene.Snapshot.Context.RulesetId == "bms")
            {
                assertBmsInstrumentInformation(scene, state);
                return;
            }

            GameplaySkinSceneRuntimeNode panel = authoredInformationNode(scene, prefix + ".panel");
            var screen = scene.ScreenSpaceDrawQuad.AABBFloat;
            var panelBounds = panel.ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
            if (compact)
            {
                string ruleset = scene.PreparedScene.Snapshot.Context.RulesetId;
                Assert.That(panel.Rect, Is.EqualTo(scene.PreparedScene.Snapshot.GetSurface(ruleset + ".hud").Rect));
                Assert.That(panel.Rect.Intersects(scene.PreparedScene.Snapshot.GetSurface(ruleset + ".playfield").Rect), Is.False,
                    "Information must remain outside the real falling-note area in both rulesets.");
                Assert.That(panelBounds.Bottom, Is.LessThanOrEqualTo(screen.Bottom + 1));
                Assert.That(panelBounds.Top, Is.GreaterThanOrEqualTo(screen.Top - 1));
            }
            else
            {
                Assert.That(panel.Rect, Is.EqualTo(scene.PreparedScene.Snapshot.Context.SafeBounds));
                Assert.That(panelBounds.Top, Is.InRange(screen.Top, screen.Top + screen.Height * 0.02f));
                Assert.That(panelBounds.Bottom, Is.LessThanOrEqualTo(screen.Top + screen.Height * 0.10f));
            }

            string[] fields = compact ? new[] { "score", "accuracy", "bpm" } : new[] { "score", "accuracy", "combo", "bpm" };
            for (int index = 0; index < fields.Length; index++)
            {
                if (compact)
                    panelBounds = authoredInformationNode(scene, prefix + (index == 0 ? ".panel" : "." + fields[index] + "-panel"))
                        .ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                GameplaySkinSceneRuntimeNode node = authoredInformationNode(scene, prefix + "." + fields[index]);
                SpriteText text = (SpriteText)node.ContentDrawable;
                var bounds = text.ScreenSpaceDrawQuad.AABBFloat;
                var label = authoredInformationNode(scene, prefix + "." + fields[index] + "-label").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                Assert.That(node.Rect, Is.EqualTo(panel.Rect));
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
                    float nextLeft = authoredInformationNode(scene, prefix + "." + fields[index + 1]).ContentDrawable.ScreenSpaceDrawQuad.AABBFloat.Left;
                    Assert.That(bounds.Right, Is.LessThan(nextLeft), fields[index] + " overlaps the next value");
                }
            }
            if (prefix == "astral.console")
            {
                bool hasCurrentJudgement = state.CurrentJudgements.Any(judgement => judgement.Scope == GameplaySkinJudgementScope.Global);
                GameplaySkinSceneRuntimeNode judgementNode = authoredInformationNode(scene, prefix + ".judgement");
                Assert.That(judgementNode.TransformDrawable.Alpha, Is.EqualTo(hasCurrentJudgement ? 1 : 0));
                if (hasCurrentJudgement)
                {
                    GameplaySkinCurrentJudgementStateSnapshot current = state.CurrentJudgements.Single(judgement => judgement.Scope == GameplaySkinJudgementScope.Global);
                    Assert.That(authoredInformationValue(scene, prefix + ".judgement"), Is.EqualTo(current.Judgement.Grade.ToString().ToLowerInvariant()));
                }
                foreach (string field in new[] { "brand", "status", "judgement" })
                {
                    SpriteText text = (SpriteText)authoredInformationNode(scene, prefix + "." + field).ContentDrawable;
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

                var brand = authoredInformationNode(scene, prefix + ".brand").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var status = authoredInformationNode(scene, prefix + ".status").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var score = authoredInformationNode(scene, prefix + ".score").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var scoreLabel = authoredInformationNode(scene, prefix + ".score-label").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var bpm = authoredInformationNode(scene, prefix + ".bpm").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var bpmLabel = authoredInformationNode(scene, prefix + ".bpm-label").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                var judgement = authoredInformationNode(scene, prefix + ".judgement").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                Assert.That(brand.Bottom, Is.LessThanOrEqualTo(status.Top + 1), "The brand overlaps the current playing or paused state.");
                Assert.That(Math.Max(brand.Right, status.Right), Is.LessThan(Math.Min(score.Left, scoreLabel.Left)), "The brand or state overlaps the score column.");
                Assert.That(Math.Max(bpm.Right, bpmLabel.Right), Is.LessThan(judgement.Left), "The tempo overlaps the rightmost judgement.");
            }
            var progress = authoredInformationNode(scene, prefix + ".progress").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
            var track = authoredInformationNode(scene, prefix + ".progress-track").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
            if (state.Timing.Progress == 0)
                Assert.That(progress.Width, Is.Zero, "Before the first object the bar has no residual visible progress line.");
            Assert.That(progress.Width, Is.EqualTo(track.Width * state.Timing.Progress).Within(1));
            Assert.That(track.Top, Is.GreaterThanOrEqualTo(panelBounds.Bottom - 1));
            Assert.That(track.Bottom, Is.LessThanOrEqualTo(compact ? screen.Bottom + 1 : screen.Top + screen.Height * 0.10f));
            Assert.That(track.Left, Is.GreaterThanOrEqualTo(screen.Left));
            Assert.That(track.Right, Is.LessThanOrEqualTo(screen.Right));
        }

        private static string authoredInformationId(GameplaySkinSceneRuntimeHost scene, string id)
            => id.StartsWith("still.hud", StringComparison.Ordinal) && !scene.TryGetRuntimeNode(id, out _)
                ? "still." + scene.PreparedScene.Snapshot.Context.RulesetId + "-information/global/"
                  + (scene.PreparedScene.Snapshot.Context.RulesetId == "bms" ? id.Replace("still.hud", "still.bms-hud", StringComparison.Ordinal) : id)
                : id;

        private static GameplaySkinSceneRuntimeNode authoredInformationNode(GameplaySkinSceneRuntimeHost scene, string id)
            => c6CandidateNode(scene, authoredInformationId(scene, id));

        private static void assertBmsInstrumentInformation(GameplaySkinSceneRuntimeHost scene, GameplaySkinEventStateSnapshot state)
        {
            GameplaySkinLayoutSnapshot layout = scene.PreparedScene.Snapshot;
            foreach (string region in new[] { "player", "song", "judgements", "tempo" })
            {
                GameplaySkinSceneRuntimeNode panel = authoredInformationNode(scene, "still.hud." + region + ".panel");
                Assert.That(panel.Rect, Is.EqualTo(layout.GetSurface("information." + region).Rect), region);
                Assert.That(panel.Rect.Intersects(layout.GetSurface("bms.playfield").Rect), Is.False, region);
                Assert.That(layout.BgaViewports.Any(bga => bga.Intersects(panel.Rect)), Is.False, region);
            }
            Assert.That(state.SongInformation, Is.Not.Null);
            GameplaySkinSongInformation song = state.SongInformation!;
            var expected = new Dictionary<string, string>
            {
                ["title"] = song.Title,
                ["artist"] = song.Artist,
                ["difficulty"] = song.Difficulty,
                ["level"] = song.Level,
                ["table-classification"] = song.TableClassification,
                ["bpm-min"] = song.MinimumBpm.ToString("0.###", CultureInfo.InvariantCulture),
                ["bpm-max"] = song.MaximumBpm.ToString("0.###", CultureInfo.InvariantCulture),
                ["hispeed"] = state.Timing.ScrollSpeed.ToString("0.00", CultureInfo.InvariantCulture),
                ["count-perfect"] = state.Score.Statistics.Perfect.ToString(CultureInfo.InvariantCulture),
                ["count-great"] = state.Score.Statistics.Great.ToString(CultureInfo.InvariantCulture),
                ["count-good"] = state.Score.Statistics.Good.ToString(CultureInfo.InvariantCulture),
                ["count-meh"] = state.Score.Statistics.Meh.ToString(CultureInfo.InvariantCulture),
                ["count-miss"] = state.Score.Statistics.Miss.ToString(CultureInfo.InvariantCulture),
                ["count-ok"] = state.Score.Statistics.Ok.ToString(CultureInfo.InvariantCulture),
                ["count-combo-breaks"] = state.Score.Statistics.ComboBreak.ToString(CultureInfo.InvariantCulture),
            };
            foreach ((string field, string value) in expected)
                Assert.That(authoredInformationValue(scene, "still.hud." + field), Is.EqualTo(value), field);
            Assert.That(state.Timing.ScrollSpeed, Is.GreaterThan(0));
            var screen = scene.ScreenSpaceDrawQuad.AABBFloat;
            foreach (string field in expected.Keys.Concat(new[] { "score", "accuracy", "bpm" }))
            {
                SpriteText text = (SpriteText)authoredInformationNode(scene, "still.hud." + field).ContentDrawable;
                if (text.Text.ToString().Length == 0)
                    continue;
                Assert.That(text.IsPresent, Is.True, field);
                var bounds = text.ScreenSpaceDrawQuad.AABBFloat;
                Assert.That(bounds.Left, Is.GreaterThanOrEqualTo(screen.Left - 1), field);
                Assert.That(bounds.Right, Is.LessThanOrEqualTo(screen.Right + 1), field);
                Assert.That(bounds.Top, Is.GreaterThanOrEqualTo(screen.Top - 1), field);
                Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(screen.Bottom + 1), field);
                if (field is "title" or "artist" or "table-classification" or "difficulty" or "level")
                {
                    var songBounds = authoredInformationNode(scene, "still.hud.song.panel").ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
                    Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(songBounds.Bottom), field + " must remain above the header's lower edge");
                }
            }
        }

        private static string authoredInformationValue(GameplaySkinSceneRuntimeHost scene, string id)
            => ((SpriteText)authoredInformationNode(scene, id).ContentDrawable).Text.ToString();

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
