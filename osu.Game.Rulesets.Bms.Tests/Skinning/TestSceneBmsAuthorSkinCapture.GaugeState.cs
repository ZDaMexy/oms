// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private void recordGaugeStateEvidence(string filename)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            var native = (BmsGaugeProcessor)player.GameplayState.HealthProcessor;
            GameplaySkinGaugeStateSnapshot snapshot = lightingState().Score.GaugeState;
            string layout = doublePlay ? "dp" : style == BmsPlayfieldStyle.P2 ? "p2" : "p1";
            string prefix = "gx." + layout + ".";
            GameplaySkinSceneRuntimeNode[] nodes = lightingNodes().Where(node => node.PreparedNode.Source.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            File.WriteAllText(Path.Combine(outputDirectory, filename + ".gauge-ready.json"), JsonSerializer.Serialize(new
            {
                layout,
                native = new
                {
                    type = native.GaugeType.ToString(),
                    rules = native.GaugeRulesFamily.ToString(),
                    native.Health.Value,
                    native.CurrentMaximumGauge,
                    native.CurrentClearThreshold,
                    native.IsClear
                },
                snapshot,
                nodes = nodes.Select(node => new
                {
                    node.InstanceId,
                    id = node.PreparedNode.Source.Id,
                    scope = node.RuntimeScopeId,
                    node.TransformDrawable.X,
                    alpha = lightingAlpha(node.ContentDrawable),
                    node.ContentDrawable.IsLoaded,
                    geometry = lightingGeometry(node.ContentDrawable)
                }).ToArray(),
                faults = runtime.RuntimeFaults.Select(fault => fault.Code.ToString()).ToArray(),
            }));
            string[] thresholdTargets = runtime.PreparedScene.Program.Bindings.Where(binding =>
                binding.Source == GameplaySkinSceneBindingSource.GaugeClearThreshold && binding.Property == GameplaySkinSceneProperty.X)
                .Select(binding => binding.TargetNodeId).ToArray();
            GameplaySkinSceneRuntimeNode[] thresholds = nodes.Where(node => thresholdTargets.Contains(node.PreparedNode.Source.Id)).ToArray();
            Assert.That(thresholds, Is.Not.Empty, "The final imported author must consume the actual clear threshold.");
            Assert.That(runtime.RuntimeFaults, Is.Empty);
            Assert.That(snapshot.ClearThreshold, Is.EqualTo(native.CurrentClearThreshold).Within(0.000001));
            bool proportional = native.GaugeType is BmsGaugeType.Normal or BmsGaugeType.Easy or BmsGaugeType.AssistEasy;
            bool reverse = !doublePlay && style == BmsPlayfieldStyle.P2;

            var evidence = thresholds.Select(line =>
            {
                GameplaySkinSceneRuntimeNode canvas = nodes.Single(node => node.PreparedNode.Source.Id == prefix + "fill"
                    && node.RuntimeScopeId == line.RuntimeScopeId);
                Assert.That(line.TransformDrawable.X, Is.EqualTo(snapshot.ClearThreshold).Within(0.000001));
                float alpha = lightingAlpha(line.ContentDrawable);
                Assert.That(alpha > 0, Is.EqualTo(proportional), "Survival gauges must not display a misleading 0% clear line.");
                double? expectedX = null;
                double? actualX = null;
                if (proportional)
                {
                    Assert.That(line.ContentDrawable.IsLoaded, Is.True);
                    Assert.That(canvas.ContentDrawable.IsLoaded, Is.True);
                    var bar = canvas.ContentDrawable.ScreenSpaceDrawQuad;
                    expectedX = bar.TopLeft.X + (bar.TopRight.X - bar.TopLeft.X) * (reverse ? 1 - snapshot.ClearThreshold : snapshot.ClearThreshold);
                    actualX = line.ContentDrawable.ScreenSpaceDrawQuad.Centre.X;
                    Assert.That(actualX.Value, Is.EqualTo(expectedX.Value).Within(1), "P2 mirrors the marker inside the original gauge canvas.");
                }

                GameplaySkinSceneRuntimeNode[] statuses = nodes.Where(node => node.RuntimeScopeId == line.RuntimeScopeId
                    && (node.PreparedNode.Source.Id == prefix + "clear" || node.PreparedNode.Source.Id == prefix + "unclear")).ToArray();
                Assert.That(statuses, Has.Length.EqualTo(2));
                GameplaySkinSceneRuntimeNode[] visible = statuses.Where(node => lightingAlpha(node.ContentDrawable) > 0).ToArray();
                Assert.That(visible, Has.Length.EqualTo(1));
                Assert.That(visible[0].ContentDrawable.IsLoaded, Is.True);
                string text = ((SpriteText)visible[0].ContentDrawable).Text.ToString();
                Assert.That(text, Is.EqualTo(snapshot.IsClear ? "CLEAR" : "NOT CLEAR"));
                GameplaySkinSceneRuntimeNode hazard = nodes.Single(node => node.RuntimeScopeId == line.RuntimeScopeId
                    && node.PreparedNode.Source.Id == prefix + "hazard");
                float hazardAlpha = lightingAlpha(hazard.ContentDrawable);
                Assert.That(hazardAlpha > 0, Is.EqualTo(native.GaugeType == BmsGaugeType.Hazard));
                string? hazardText = null;
                if (hazardAlpha > 0)
                {
                    Assert.That(hazard.ContentDrawable.IsLoaded, Is.True);
                    hazardText = ((SpriteText)hazard.ContentDrawable).Text.ToString();
                    Assert.That(hazardText, Is.EqualTo("HAZARD / SURVIVAL"));
                }
                return new
                {
                    line.InstanceId,
                    scope = line.RuntimeScopeId,
                    localX = line.TransformDrawable.X,
                    alpha,
                    expectedX,
                    actualX,
                    text,
                    hazardAlpha,
                    hazardText,
                    canvas = lightingGeometry(canvas.ContentDrawable),
                    marker = lightingGeometry(line.ContentDrawable),
                };
            }).ToArray();

            File.WriteAllText(Path.Combine(outputDirectory, filename + ".gauge.json"), JsonSerializer.Serialize(new
            {
                boundary = "Actual isolated Player, real Mods and natural autoplay results; no health/score injection or seek.",
                time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                layout,
                reverse,
                native = new
                {
                    type = native.GaugeType.ToString(),
                    rules = native.GaugeRulesFamily.ToString(),
                    native.Health.Value,
                    native.CurrentMaximumGauge,
                    native.CurrentClearThreshold,
                    native.IsClear
                },
                snapshot,
                markers = evidence,
                faults = runtime.RuntimeFaults.Select(fault => fault.Code.ToString()).ToArray(),
            }));
        }
    }
}
