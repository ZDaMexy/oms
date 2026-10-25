// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private bool captureObservedHostActivity;
        private readonly BindableBool captureHostActivity = new BindableBool();

        private void addGameplayControlsCaptureSteps()
        {
            BmsInputManager input = null!;
            bool initiallyCovered = false;
            double gestureBoundary = 0;
            double firstNoteTime = 0;
            AddUntilStep("wait for the complete real adjustment scene", () => renderer.GameplaySkinSceneRuntime!.IsSceneReady);
            AddStep("simulate host activity for isolated software input capture", () =>
            {
                captureObservedHostActivity = host.IsActive.Value;
                captureHostActivity.BindTo((Bindable<bool>)host.IsActive);
                captureHostActivity.Value = true;
            });
            AddStep("hold the actual Start operator", () =>
            {
                input = (BmsInputManager)renderer.GameplayInputManager!;
                initiallyCovered = renderer.SuddenActive || renderer.HiddenActive;
                firstNoteTime = renderer.Beatmap.HitObjects.OfType<BmsHitObject>().Min(note => note.StartTime);
                Assert.That(input.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True);
                File.WriteAllText(Path.Combine(outputDirectory, "controls-input-state.json"), JsonSerializer.Serialize(new
                {
                    playerType = player.GetType().Name,
                    observedHostActive = captureObservedHostActivity,
                    hostActivitySimulated = true,
                    hostActive = host.IsActive.Value,
                    inputHeld = input.PreStartHoldPressed.Value,
                    renderer.AdjustmentHeld,
                    source = renderer.GameplaySkinAdjustmentState,
                    snapshot = lightingState().Timing.AdjustmentState,
                }));
                Assert.That(renderer.AdjustmentHeld, Is.True);
            });
            AddUntilStep("the real Start state reaches the author", () => lightingState().Timing.AdjustmentState.Adjusting);
            captureAdjustmentSample("controls-start-held");
            AddStep("switch the actual speed mode with Start and EFFECT", () =>
            {
                Assert.That(input.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True);
                Assert.That(input.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True);
            });
            captureAdjustmentSample("controls-mode-switched");
            AddStep("turn the calibrated scratch adjustment consumer", () =>
                renderer.AdjustFromScratch(new OmsAxisMotion(OmsAction.Key1P_Scratch, OmsAxisMode.CircularPosition,
                    0.025, "author-capture-axis", null, 60)));
            captureAdjustmentSample("controls-scratch-adjusted");
            AddStep("release the used Start gesture", () =>
            {
                Assert.That(input.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True);
                gestureBoundary = Time.Current;
            });
            AddUntilStep("separate the held gesture from the next double tap", () => Time.Current - gestureBoundary > 500);
            doubleTap();
            AddAssert("the native cover combination changes without losing Lift", () =>
                (renderer.SuddenActive || renderer.HiddenActive) != initiallyCovered);
            captureAdjustmentSample("controls-double-toggled");
            AddStep("hold Start after the completed double tap", () => input.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
            captureAdjustmentSample("controls-toggled-held");
            AddStep("release the long observation hold", () =>
            {
                input.TriggerOmsActionReleased(OmsAction.UI_PreStartHold);
                gestureBoundary = Time.Current;
            });
            AddUntilStep("separate the second pair", () => Time.Current - gestureBoundary > 500);
            doubleTap();
            AddAssert("the original configured combination returns", () =>
                !initiallyCovered || renderer.SuddenActive || renderer.HiddenActive);
            AddStep("observe a new held panel", () => input.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
            captureAdjustmentSample("controls-restored-held");
            AddStep("finish the real operator interaction", () => input.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));
            AddUntilStep("release reaches the same author snapshot", () => !lightingState().Timing.AdjustmentState.Adjusting);
            captureAdjustmentSample("controls-released");
            AddUntilStep("the real song starts after the operator releases Start", () => gameplayClock.IsRunning
                && renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime >= 800);
            AddStep("hold the same Start operator during live gameplay", () => input.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
            captureAdjustmentSample("controls-live-gameplay-held");
            AddStep("release the live gameplay operator", () => input.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));
            AddUntilStep("hit the first note at its real gameplay time", () =>
            {
                double inputTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime;
                if (inputTime < firstNoteTime - 25)
                    return false;
                Assert.That(input.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True);
                File.WriteAllText(Path.Combine(outputDirectory, "controls-manual-hit.json"), JsonSerializer.Serialize(new
                {
                    firstNoteTime,
                    inputTime,
                    audioTime = gameplayClock.CurrentTime,
                    input.UseParentInput,
                    inputHeld = input.PreStartHoldPressed.Value,
                    hostActive = host.IsActive.Value,
                }));
                return true;
            });
            AddStep("release the ordinary lane input", () => input.TriggerOmsActionReleased(OmsAction.Key1P_1));

            void doubleTap()
            {
                AddStep("first short Start press", () => input.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
                AddStep("first short Start release", () => input.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));
                AddStep("second short Start press", () => input.TriggerOmsActionPressed(OmsAction.UI_PreStartHold));
                AddStep("second short Start release", () => input.TriggerOmsActionReleased(OmsAction.UI_PreStartHold));
            }
        }

        private void captureAdjustmentSample(string filename)
        {
            AddWaitStep("let the same native state reach prepared visuals", 3);
            AddStep("record actual readouts and framebuffer", () =>
            {
                GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
                GameplaySkinTimingStateSnapshot timing = lightingState().Timing;
                GameplaySkinAdjustmentState adjustment = timing.AdjustmentState;
                Assert.That(adjustment.IsAvailable, Is.True);
                Assert.That(adjustment.Adjusting, Is.EqualTo(renderer.AdjustmentHeld));
                Assert.That(adjustment.GreenNumber, Is.EqualTo(renderer.SpeedMetrics.Value.GreenNumber));
                Assert.That(adjustment.SuddenActive, Is.EqualTo(renderer.SuddenActive));
                Assert.That(adjustment.HiddenActive, Is.EqualTo(renderer.HiddenActive));
                GameplaySkinSceneRuntimeNode[] nodes = lightingNodes().Concat(renderer.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Where(visual => visual.IsApplied).SelectMany(visual => visual.RuntimeNodes)).Distinct().ToArray();
                var bindings = runtime.PreparedScene.Program.Bindings;
                var greenNodes = nodes.Where(node => bindings.Any(binding => binding.TargetNodeId == node.PreparedNode.Source.Id
                    && binding.Source == GameplaySkinSceneBindingSource.ScrollGreenNumber && binding.Property == GameplaySkinSceneProperty.Text)).ToArray();
                Assert.That(greenNodes, Is.Not.Empty, "The selected Omni must expose real green-number text consumers.");
                Assert.That(greenNodes.Length, Is.EqualTo(1 + (doublePlay ? 2 : 1) * 4),
                    "A readout belongs to each real stage, without inherited group or lane copies.");
                foreach (var node in greenNodes)
                    Assert.That(((GameplaySkinBitmapText)node.ContentDrawable).AuthorText,
                        Is.EqualTo(adjustment.GreenNumber.ToString(CultureInfo.InvariantCulture)));
                (GameplaySkinSceneBindingSource Source, double Expected)[] whiteSources =
                {
                    (GameplaySkinSceneBindingSource.CoverSuddenUnits, adjustment.SuddenUnits),
                    (GameplaySkinSceneBindingSource.CoverHiddenUnits, adjustment.HiddenUnits),
                    (GameplaySkinSceneBindingSource.LiftValue, timing.LiftState.Units),
                };
                foreach (var (source, expected) in whiteSources)
                {
                    var whiteNodes = nodes.Where(node => bindings.Any(binding => binding.TargetNodeId == node.PreparedNode.Source.Id
                        && binding.Source == source && binding.Property == GameplaySkinSceneProperty.Text)).ToArray();
                    Assert.That(whiteNodes, Is.Not.Empty, $"The selected Omni must expose the real {source} white-number consumer.");
                    Assert.That(whiteNodes.Length, Is.EqualTo((doublePlay ? 2 : 1) * (source == GameplaySkinSceneBindingSource.LiftValue ? 2 : 1)),
                        "Each stage owns its readout; hidden reversal branches still count, inherited groups and lanes do not.");
                    foreach (var node in whiteNodes)
                        Assert.That(((GameplaySkinBitmapText)node.ContentDrawable).AuthorText,
                            Is.EqualTo(expected.ToString("0", CultureInfo.InvariantCulture)));
                }
                Assert.That(runtime.RuntimeFaults, Is.Empty);
                File.WriteAllText(Path.Combine(outputDirectory, filename + ".json"), JsonSerializer.Serialize(new
                {
                    boundary = "normal-Player-Start-input-with-simulated-host-activity-and-native-calibrated-axis-consumer; not foreground-or-device acceptance",
                    playerType = player.GetType().Name,
                    observedHostActive = captureObservedHostActivity,
                    hostActivitySimulated = true,
                    hostActive = host.IsActive.Value,
                    timing,
                    score = lightingState().Score,
                    presentation = runtime.Publication.Snapshot.Context.PresentationStyleId,
                    budget = new
                    {
                        runtime.PreparedScene.PreparedNodeCount,
                        runtime.PreparedScene.ReservedRuntimeInstanceCount,
                        runtime.PreparedScene.ReservedTextGlyphs,
                        runtime.PreparedScene.ReservedTextGlyphPixels,
                    },
                    native = new
                    {
                        renderer.AdjustmentHeld,
                        mode = renderer.HiSpeedMode.Value.ToString(),
                        renderer.SuddenUnits,
                        renderer.HiddenUnits,
                        renderer.SuddenActive,
                        renderer.HiddenActive,
                        lift = renderer.Playfield.LiftUnits.Value,
                        metrics = renderer.SpeedMetrics.Value
                    },
                    liftState = timing.LiftState,
                    covers = renderer.ChildrenOfType<BmsLaneCover>().Select(cover => new
                    {
                        position = cover.CoverPosition.ToString(),
                        enabled = cover.CoverEnabled.Value,
                        units = cover.CoverPercent.Value,
                        opacity = cover.CoverOpacity.Value,
                        stages = cover.GameplaySkinStageFallbackVisuals.Select(stage => new
                        {
                            x = stage.ScreenSpaceDrawQuad.TopLeft.X,
                            y = stage.ScreenSpaceDrawQuad.TopLeft.Y,
                            stage.ScreenSpaceDrawQuad.Width,
                            stage.ScreenSpaceDrawQuad.Height,
                        }).ToArray(),
                    }).ToArray(),
                    readouts = nodes.Where(node => node.ContentDrawable is GameplaySkinBitmapText).Select(node => new
                    {
                        id = node.PreparedNode.Source.Id,
                        node.InstanceId,
                        sources = bindings.Where(binding => binding.TargetNodeId == node.PreparedNode.Source.Id && binding.Property == GameplaySkinSceneProperty.Text)
                            .Select(binding => binding.Source.ToString()).ToArray(),
                        text = ((GameplaySkinBitmapText)node.ContentDrawable).AuthorText,
                        displayed = node.ContentDrawable.IsPresent && node.ContentDrawable.DrawColourInfo.Colour.AverageColour.SRGB.A > 0,
                        bounds = new
                        {
                            x = node.ContentDrawable.ScreenSpaceDrawQuad.TopLeft.X,
                            y = node.ContentDrawable.ScreenSpaceDrawQuad.TopLeft.Y,
                            node.ContentDrawable.ScreenSpaceDrawQuad.Width,
                            node.ContentDrawable.ScreenSpaceDrawQuad.Height,
                        },
                    }).ToArray(),
                }));
                capture = saveCapture(filename + ".png");
            });
            AddUntilStep("wait for adjustment evidence", () => capture?.IsCompleted == true);
            AddStep("propagate adjustment capture failure", () => capture!.GetAwaiter().GetResult());
        }
    }
}
