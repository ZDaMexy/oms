// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private string? autoAssistCaptureMode;
        private BmsInputManager autoAssistCaptureInput = null!;
        private BmsHitObject autoAssistCaptureKey = null!;
        private BmsHitObject autoAssistCaptureScratch = null!;
        private BmsHoldNote autoAssistCaptureHold = null!;
        private readonly Dictionary<long, BmsHitObject> autoAssistCaptureOwners = [];
        private bool autoAssistCaptureInputEnabled;
        private bool autoAssistCaptureKeyDispatched;
        private double? autoAssistCaptureKeyTime;
        private bool autoAssistCaptureKeyPressed;
        private bool autoAssistCaptureKeyReleased;

        private static string? readAutoAssistCaptureMode()
            => Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_AUTO_ASSIST") switch
            {
                null or "" => null,
                "visible" => "visible",
                "hidden" => "hidden",
                "untinted" => "untinted",
                _ => throw new InvalidOperationException("OMS_AUTHOR_SKIN_CAPTURE_AUTO_ASSIST must be visible, hidden or untinted."),
            };

        private Mod[] configureAutoAssistCapture(Mod[] mods)
            => [.. mods, new BmsModAutoScratch
            {
                ScratchVisibility = { Value = autoAssistCaptureMode == "hidden" ? AutoScratchVisibility.Hidden : AutoScratchVisibility.Visible },
                TintScratchNotes = { Value = autoAssistCaptureMode != "untinted" },
            }];

        private static IBeatmap createAutoAssistChart(BmsRuleset ruleset, bool doublePlay, bool fiveKey)
        {
            var chart = new StringBuilder("#TITLE AUTO SCRATCH / OMS native author capture\n#ARTIST Offline reference chart\n#DIFFICULTY 3\n#PLAYLEVEL 5\n#BPM 150\n#LNTYPE 1\n#WAV01 note.wav\n");
            if (doublePlay)
                chart.Append("#PLAYER 3\n");
            for (int deck = 0; deck < (doublePlay ? 2 : 1); deck++)
            {
                string regular = deck == 0 ? "1" : "2";
                string hold = deck == 0 ? "5" : "6";
                chart.Append("#001").Append(regular).Append("6:01000100\n");
                chart.Append("#001").Append(regular).Append("1:00010000\n");
                chart.Append("#002").Append(hold).Append("6:01000100\n");
                chart.Append("#002").Append(hold).Append("1:01000100\n");
                chart.Append("#003").Append(regular).Append("6:01\n");
                chart.Append("#004").Append(regular).Append("1:01\n");
            }
            return new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(chart.ToString(), fiveKey ? "auto-assist-capture.bms" : "auto-assist-capture.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
        }

        private void addAutoAssistCaptureSteps()
        {
            AddStep("prepare the normal Player and actual AUTO SCRATCH objects", () =>
            {
                Assert.That(player, Is.TypeOf<BmsSoloPlayer>());
                Assert.That(player.GameplayState.Mods.OfType<BmsModAutoplay>(), Is.Empty);
                Assert.That(player.GameplayState.Mods.OfType<BmsModAutoNote>(), Is.Empty);
                BmsModAutoScratch mod = player.GameplayState.Mods.OfType<BmsModAutoScratch>().Single();
                Assert.That(mod.ScratchVisibility.Value, Is.EqualTo(autoAssistCaptureMode == "hidden" ? AutoScratchVisibility.Hidden : AutoScratchVisibility.Visible));
                Assert.That(mod.TintScratchNotes.Value, Is.EqualTo(autoAssistCaptureMode != "untinted"));
                Assert.That(renderer.HasReplayLoaded.Value, Is.False);
                autoAssistCaptureInput = (BmsInputManager)renderer.GameplayInputManager!;
                Assert.That(autoAssistCaptureInput.UseParentInput, Is.True);
                captureObservedHostActivity = host.IsActive.Value;
                captureHostActivity.BindTo((Bindable<bool>)host.IsActive);
                captureHostActivity.Value = true;
                BmsGameplayLayoutLane keyLane = renderer.Playfield.Lanes.Single(lane => lane.LayoutLane.Action == BmsAction.Key1).LayoutSnapshotLane!;
                BmsGameplayLayoutLane scratchLane = renderer.Playfield.Lanes.Single(lane => lane.LayoutLane.Action == BmsAction.Scratch1).LayoutSnapshotLane!;
                BmsHitObject[] objects = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().ToArray();
                foreach (BmsHitObject obj in objects)
                {
                    autoAssistCaptureOwners.Add(renderer.GetGameplaySkinObjectId(obj), obj);
                    Assert.That(obj.AutoPlay, Is.EqualTo(obj.IsScratch));
                    Assert.That(obj.CountsForScore, Is.EqualTo(!obj.IsScratch));
                }
                autoAssistCaptureScratch = objects.First(obj => obj is not BmsHoldNote && obj.LaneIndex == scratchLane.LogicalIndex);
                autoAssistCaptureHold = objects.OfType<BmsHoldNote>().First(obj => obj.LaneIndex == scratchLane.LogicalIndex);
                autoAssistCaptureKey = objects.First(obj => obj is not BmsHoldNote && obj.LaneIndex == keyLane.LogicalIndex);
                autoAssistCaptureInputEnabled = true;
                File.WriteAllText(Path.Combine(outputDirectory, "auto-assist-plan.json"), JsonSerializer.Serialize(new
                {
                    boundary = "Actual normal Player with native Auto Scratch; one software key pulse and recorded software clock stops; not physical-device or foreground acceptance",
                    mode = autoAssistCaptureMode,
                    longNoteMode = longNoteMode.ToString(),
                    style = style.ToString(),
                    doublePlay,
                    fiveKey,
                    observedHostActive = captureObservedHostActivity,
                    hostActivitySimulated = true,
                    mods = player.GameplayState.Mods.Select(item => item.Acronym).ToArray(),
                    scratchTime = autoAssistCaptureScratch.StartTime,
                    holdStart = autoAssistCaptureHold.StartTime,
                    holdEnd = autoAssistCaptureHold.EndTime,
                    scoringKeyTime = autoAssistCaptureKey.StartTime,
                }));
            });
            addAutoAssistFrame("short", () => autoAssistCaptureScratch, () => autoAssistCaptureScratch.StartTime - 80, false);
            AddStep("resume toward the actual scoring key and long scratch", () => gameplayClock.Start());
            addAutoAssistFrame("long-before", () => autoAssistCaptureHold, () => autoAssistCaptureHold.StartTime - 80, false);
            AddStep("resume into the actual automatically held long scratch", () => gameplayClock.Start());
            addAutoAssistFrame("long-holding", () => autoAssistCaptureHold, () => autoAssistCaptureHold.StartTime + 50, true);
            AddStep("retain the real scoring input and finish the same chart", () =>
            {
                Assert.That(autoAssistCaptureKeyDispatched && autoAssistCaptureKeyPressed && autoAssistCaptureKeyReleased, Is.True);
                Assert.That(score.HitEvents.Any(hit => ReferenceEquals(hit.HitObject, autoAssistCaptureKey) && hit.Result is HitResult.Perfect or HitResult.Great), Is.True);
                Assert.That(score.TotalScore.Value, Is.GreaterThan(0));
                autoAssistCaptureInputEnabled = false;
                gameplayClock.Start();
            });
        }

        private void sampleAutoAssistCaptureInput()
        {
            if (!autoAssistCaptureInputEnabled || autoAssistCaptureKeyDispatched || !gameplayClock.IsRunning)
                return;
            double time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
            if (time < autoAssistCaptureKey.StartTime - 5)
                return;
            autoAssistCaptureKeyDispatched = true;
            autoAssistCaptureKeyTime = time;
            autoAssistCaptureKeyPressed = autoAssistCaptureInput.TriggerOmsActionPressed(OmsAction.Key1P_1);
            autoAssistCaptureKeyReleased = autoAssistCaptureInput.TriggerOmsActionReleased(OmsAction.Key1P_1);
        }

        private void addAutoAssistFrame(string phase, Func<BmsHitObject> target, Func<double> sampleTime, bool holding)
        {
            string name = "auto-assist-" + autoAssistCaptureMode + "-" + phase;
            AddUntilStep("freeze the real " + phase + " scratch frame", () =>
            {
                if (!gameplayClock.IsRunning || renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime < sampleTime())
                    return false;
                gameplayClock.Stop();
                return true;
            });
            AddWaitStep("let the same object state reach the native author clones", 3);
            AddStep("record AUTO SCRATCH native readiness", () => writeAutoAssistEvidence(name + "-ready", target(), holding, false));
            AddUntilStep("wait for the actual AUTO SCRATCH scene branches", () => autoAssistNativeReady(target()));
            AddStep("record the real AUTO SCRATCH frame and native framebuffer", () =>
            {
                writeAutoAssistEvidence(name, target(), holding, false);
                capture = saveCapture(name + ".png");
            });
            AddUntilStep("wait for the native AUTO SCRATCH screenshot", () => capture?.IsCompleted == true);
            AddStep("validate the captured actual AUTO SCRATCH consumers", () =>
            {
                capture!.GetAwaiter().GetResult();
                writeAutoAssistEvidence(name + "-verified", target(), holding, true);
            });
        }

        private bool autoAssistNativeReady(BmsHitObject target)
        {
            long id = renderer.GetGameplaySkinObjectId(target);
            if (!lightingState().ActiveObjects.Any(obj => obj.ObjectId == id))
                return false;
            BmsAsyncNoteDrawable[] notes = autoAssistNativeNotes(id);
            BmsNoteSkinElements[] required = target is BmsHoldNote
                ? [BmsNoteSkinElements.LongNoteHead, BmsNoteSkinElements.LongNoteBody, BmsNoteSkinElements.LongNoteTail]
                : [BmsNoteSkinElements.Note];
            if (!required.All(element => notes.Any(note => note.Lookup.Element == element)))
                return false;
            // The specialised scene is the displayed material. Its suppressed fallback Drawable may remain
            // unloaded even while the actual scene sprites are already visible and consuming this exact object.
            // Hidden native hosts still have read-only scene state; invisible texture children need not load.
            if (autoAssistCaptureMode == "hidden")
                return true;
            foreach (BmsAsyncNoteDrawable note in notes)
            {
                GameplaySkinSceneRuntimeNode[] visible = [.. note.SpecialisedSceneVisual!.RuntimeNodes.Where(node =>
                    node.ContentDrawable is Sprite && autoAssistBranchAlpha(node.ContentDrawable, note) > 0)];
                if (!note.IsLoaded || visible.Length == 0
                    || visible.Any(node => !node.ContentDrawable.IsLoaded || ((Sprite)node.ContentDrawable).Texture == null))
                    return false;
            }
            return true;
        }

        private BmsAsyncNoteDrawable[] autoAssistNativeNotes(long id)
            => [.. renderer.ChildrenOfType<BmsAsyncNoteDrawable>().Where(note => note.Lookup.UsesResolvedMaterial
                && note.SpecialisedSceneVisual is { IsApplied: true } visual && visual.BoundObjectId == id)];

        private void writeAutoAssistEvidence(string name, BmsHitObject target, bool holding, bool assertValues)
        {
            GameplaySkinEventStateSnapshot state = lightingState();
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            long targetId = renderer.GetGameplaySkinObjectId(target);
            BmsModAutoScratch mod = player.GameplayState.Mods.OfType<BmsModAutoScratch>().Single();
            var hosts = new List<object>();
            foreach (GameplaySkinObjectStateSnapshot obj in state.ActiveObjects.Where(obj => obj.Kind is GameplaySkinObjectKind.Note or GameplaySkinObjectKind.LongNote))
            {
                BmsHitObject owner = autoAssistCaptureOwners[obj.ObjectId];
                GameplaySkinObjectAutoAssist expected = owner is { IsScratch: true, AutoPlay: true, CountsForScore: false }
                    ? GameplaySkinObjectAutoAssist.Scratch : GameplaySkinObjectAutoAssist.None;
                if (assertValues)
                {
                    Assert.That(obj.AutoAssist, Is.EqualTo(expected));
                    Assert.That(obj.LaneId, Is.EqualTo(renderer.LayoutSnapshot.GetLaneByLogicalIndex(owner.LaneIndex).LaneId));
                }
                foreach (BmsAsyncNoteDrawable note in autoAssistNativeNotes(obj.ObjectId))
                {
                    var variants = new List<object>();
                    int assistVariants = 0;
                    GameplaySkinSpecialisedSceneVisual visual = note.SpecialisedSceneVisual!;
                    foreach (GameplaySkinPreparedSceneVariant variant in runtime.PreparedScene.Program.Variants.Where(variant => variant.Source is
                                 GameplaySkinSceneBindingSource.ObjectAutoAssist or GameplaySkinSceneBindingSource.ObjectLongNoteMode or GameplaySkinSceneBindingSource.ObjectLongNoteAppearance))
                        foreach (GameplaySkinSceneRuntimeNode node in visual.RuntimeNodes.Where(node => node.PreparedNode.Source.Id == variant.TargetNodeId))
                        {
                            if (variant.Source == GameplaySkinSceneBindingSource.ObjectAutoAssist)
                                assistVariants++;
                            string mode = obj.LongNoteMode switch { GameplaySkinLongNoteMode.Standard => "ln", GameplaySkinLongNoteMode.Charge => "cn", GameplaySkinLongNoteMode.HellCharge => "hcn", _ => "none" };
                            string key = variant.Source == GameplaySkinSceneBindingSource.ObjectAutoAssist ? expected == GameplaySkinObjectAutoAssist.Scratch ? "scratch" : "none"
                                : variant.Source == GameplaySkinSceneBindingSource.ObjectLongNoteAppearance && mode != "none"
                                    ? obj.State == GameplaySkinObjectState.Holding ? mode + "-holding" : obj.State == GameplaySkinObjectState.Missed ? mode + "-released" : mode
                                    : mode;
                            GameplaySkinPreparedSceneValue selected = variant.SelectValue(key);
                            if (assertValues && variant.Property == GameplaySkinSceneProperty.Visible)
                                Assert.That(node.TransformDrawable.Alpha, Is.EqualTo(selected.BooleanValue ? 1 : 0));
                            if (assertValues && variant.Property == GameplaySkinSceneProperty.Resource)
                                Assert.That(((Sprite)node.ContentDrawable).Texture, Is.SameAs(selected.Resource!.Texture));
                            variants.Add(new
                            {
                                variant.Id,
                                target = node.PreparedNode.Source.Id,
                                source = variant.Source.ToString(),
                                key,
                                property = variant.Property.ToString(),
                                expectedResource = selected.Resource?.Id,
                                expectedVisible = variant.Property == GameplaySkinSceneProperty.Visible ? selected.BooleanValue : (bool?)null,
                                localAlpha = node.TransformDrawable.Alpha
                            });
                        }
                    var textures = visual.RuntimeNodes.Where(node => node.ContentDrawable is Sprite).Select(node => new
                    {
                        node.InstanceId,
                        id = node.PreparedNode.Source.Id,
                        loaded = node.ContentDrawable.IsLoaded,
                        branchAlpha = autoAssistBranchAlpha(node.ContentDrawable, note),
                        effectiveAlpha = lightingAlpha(node.ContentDrawable),
                        resources = runtime.PreparedScene.Resources.Where(resource => ReferenceEquals(resource.Texture, ((Sprite)node.ContentDrawable).Texture))
                            .Select(resource => new { resource.Id, resource.ContentRevision }).ToArray(),
                        geometry = lightingGeometry(node.ContentDrawable),
                    }).ToArray();
                    bool hasAutoArt = textures.Any(texture => texture.branchAlpha > 0 && texture.resources.Any(resource => resource.Id.StartsWith("as.t", StringComparison.Ordinal)));
                    if (assertValues)
                    {
                        Assert.That(note.SceneVisualGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                        Assert.That(note.SceneVisualGate.SuppressesProgrammaticVisual, Is.True);
                        Assert.That(visual.BoundObjectId, Is.EqualTo(obj.ObjectId));
                        if (expected == GameplaySkinObjectAutoAssist.Scratch)
                            Assert.That(assistVariants, Is.GreaterThan(0), "The native AUTO SCRATCH branch must actually consume object.auto-assist.");
                        Assert.That(hasAutoArt, Is.EqualTo(expected == GameplaySkinObjectAutoAssist.Scratch), "Only a real assisted scratch may select original AUTO SCRATCH art.");
                        Assert.That(note.Alpha, Is.EqualTo(expected == GameplaySkinObjectAutoAssist.Scratch && autoAssistCaptureMode == "hidden" ? 0 : 1));
                        Assert.That(note.Colour.TopLeft.SRGB, Is.EqualTo(expected == GameplaySkinObjectAutoAssist.Scratch && mod.TintScratchNotes.Value ? (Color4)mod.ScratchTintColour.Value : Color4.White));
                    }
                    hosts.Add(new
                    {
                        obj.ObjectId,
                        lane = obj.LaneId?.Value,
                        element = note.Lookup.Element.ToString(),
                        route = note.SceneVisualGate.Route.ToString(),
                        note.IsLoaded,
                        fallbackKind = note.Drawable?.GetType().Name,
                        fallbackLoaded = note.Drawable?.IsLoaded,
                        fallbackLoadPending = note.PendingLoadTask != null,
                        sceneLoaded = visual.IsLoaded,
                        nativeAlpha = note.Alpha,
                        nativeColour = note.Colour.TopLeft.SRGB.ToString(),
                        nativeGeometry = lightingGeometry(note),
                        hasAutoArt,
                        appliedNodes = note.AppliedSceneNodeIds,
                        variants,
                        textures
                    });
                }
            }
            File.WriteAllText(Path.Combine(outputDirectory, name + ".json"), JsonSerializer.Serialize(new
            {
                boundary = "Actual native normal Player and Auto Scratch; host activity simulated; the software gameplay clock is stopped for these frames",
                mode = autoAssistCaptureMode,
                longNoteMode = longNoteMode.ToString(),
                targetId,
                holding,
                time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                audioTime = gameplayClock.CurrentTime,
                gameplayClock.IsRunning,
                streamEpoch = renderer.GameplaySkinEventStream.CurrentEpoch,
                consumerEpoch = runtime.CurrentEpoch,
                runtime.LastSequence,
                mod = new { visibility = mod.ScratchVisibility.Value.ToString(), tint = mod.TintScratchNotes.Value, colour = mod.ScratchTintColour.Value.ToString() },
                objects = state.ActiveObjects.Select(obj =>
                {
                    autoAssistCaptureOwners.TryGetValue(obj.ObjectId, out BmsHitObject? owner);
                    return new
                    {
                        obj.ObjectId,
                        kind = obj.Kind.ToString(),
                        obj.StartTime,
                        obj.EndTime,
                        state = obj.State.ToString(),
                        mode = obj.LongNoteMode.ToString(),
                        autoAssist = obj.AutoAssist.ToString(),
                        lane = obj.LaneId?.Value,
                        sourceScratch = owner?.IsScratch,
                        sourceAutoPlay = owner?.AutoPlay,
                        sourceCountsForScore = owner?.CountsForScore
                    };
                }).ToArray(),
                input = new
                {
                    action = OmsAction.Key1P_1.ToString(),
                    time = autoAssistCaptureKeyTime,
                    pressed = autoAssistCaptureKeyPressed,
                    released = autoAssistCaptureKeyReleased
                },
                actualEx = score.TotalScore.Value,
                scoringKeyHits = score.HitEvents.Where(hit => ReferenceEquals(hit.HitObject, autoAssistCaptureKey))
                    .Select(hit => new { result = hit.Result.ToString(), hit.TimeOffset }).ToArray(),
                hosts,
                faults = runtime.RuntimeFaults,
            }));
            if (!assertValues)
                return;
            Assert.That(runtime.RuntimeFaults, Is.Empty);
            GameplaySkinObjectStateSnapshot sampled = state.ActiveObjects.Single(obj => obj.ObjectId == targetId);
            Assert.That(sampled.AutoAssist, Is.EqualTo(GameplaySkinObjectAutoAssist.Scratch));
            Assert.That(autoAssistNativeNotes(targetId), Is.Not.Empty);
            if (holding)
                Assert.That(sampled.State, Is.EqualTo(GameplaySkinObjectState.Holding));
        }

        private static float autoAssistBranchAlpha(Drawable drawable, BmsAsyncNoteDrawable native)
        {
            float alpha = 1;
            Drawable? current = drawable;
            for (; current != null && !ReferenceEquals(current, native); current = current.Parent)
                alpha *= current.Alpha;
            return ReferenceEquals(current, native) ? alpha : 0;
        }
    }
}
