// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class TestSceneBmsAuthorSkinCapture
    {
        private const string capture_setting_prefix = "OMS_AUTHOR_SKIN_CAPTURE_";
        private static readonly string[] brightness_options = Environment.GetEnvironmentVariable(capture_setting_prefix + "COLUMN") == "1"
            ? ["lane-objects-brightness", "column-brightness"] : ["lane-objects-brightness"];
        private static readonly FieldInfo rgb_recorded_sprites = typeof(GameplaySkinRecordedAnimationDrawable)
            .GetField("sprites", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly FieldInfo hud_score_state = typeof(GameplaySkinSceneRuntimeHost)
            .GetField("scoreState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly HashSet<string> brightnessOptionsObserved = new(StringComparer.Ordinal);
        private readonly HashSet<string> brightnessSlotsObserved = new(StringComparer.Ordinal);
        private GameplaySkinSpecialisedSceneVisual? pendingExplosionRgb;
        private long? pendingExplosionObjectId;
        private Exception? explosionRgbFailure;

        // Use the real reading settings and gameplay Mods, rather than emulating original-game controls.
        private Mod[] configureStartOptions(Mod[] original)
        {
            var mods = original.ToList();
            var config = (BmsRulesetConfigManager)Game.Dependencies.Get<IRulesetConfigCache>().GetConfigFor(new BmsRuleset())!;
            configureCover("COVER", BmsRulesetSetting.SuddenEnabled, BmsRulesetSetting.SuddenUnits, BmsRulesetSetting.SuddenOpacity);
            configureCover("HIDDEN", BmsRulesetSetting.HiddenEnabled, BmsRulesetSetting.HiddenUnits, BmsRulesetSetting.HiddenOpacity);
            config.SetValue(BmsRulesetSetting.LiftEnabled, captureFlag("LIFT"));
            config.SetValue(BmsRulesetSetting.LiftUnits, 250f);
            if (captureFlag("LIFT"))
                config.SetValue(BmsRulesetSetting.LiftUnits, (float)captureNumber("LIFT_VALUE", 250, 0, 1000, 1));
            else
                requireEnabled("LIFT", "LIFT_VALUE");
            config.SetValue(BmsRulesetSetting.LaneCoversEnabled, true);
            config.SetValue(BmsRulesetSetting.RememberReadingChanges, true);

            if (Environment.GetEnvironmentVariable(capture_setting_prefix + "GAUGE") is string gauge)
            {
                Mod? gaugeMod = gauge switch
                {
                    "normal" => null,
                    "easy" => new BmsModGaugeEasy(),
                    "a-easy" => new BmsModGaugeAssistEasy(),
                    "hard" => new BmsModGaugeHard(),
                    "ex-hard" => new BmsModGaugeExHard(),
                    "hazard" => new BmsModGaugeHazard(),
                    _ => throw new InvalidOperationException("The author capture gauge must be normal, easy, a-easy, hard, ex-hard or hazard."),
                };
                if (gaugeMod != null)
                    mods.Add(gaugeMod);
            }

            if (Environment.GetEnvironmentVariable(capture_setting_prefix + "GAUGE_RULES") is string gaugeRules)
            {
                Mod? rulesMod = gaugeRules switch
                {
                    "legacy" => null,
                    "beatoraja" => new BmsModGaugeRulesBeatoraja(),
                    "lr2" => new BmsModGaugeRulesLr2(),
                    "iidx" => new BmsModGaugeRulesIidx(),
                    _ => throw new InvalidOperationException("The author capture gauge rules must be legacy, beatoraja, lr2 or iidx."),
                };
                if (rulesMod != null)
                    mods.Add(rulesMod);
            }

            if (Environment.GetEnvironmentVariable(capture_setting_prefix + "HISPEED_MODE") is string speedMode)
            {
                (BmsHiSpeedMode mode, BmsRulesetSetting setting, double min, double max, double step) = speedMode switch
                {
                    "normal" => (BmsHiSpeedMode.Normal, BmsRulesetSetting.ScrollSpeed, 1, 20, 0.1),
                    "floating" => (BmsHiSpeedMode.Floating, BmsRulesetSetting.FloatingHiSpeed, 0.5, 10, 0.01),
                    "classic" => (BmsHiSpeedMode.Classic, BmsRulesetSetting.ClassicHiSpeed, 0.5, 10, 0.25),
                    _ => throw new InvalidOperationException("The author capture hi-speed mode must be normal, floating or classic."),
                };
                config.SetValue(BmsRulesetSetting.HiSpeedMode, mode);
                config.SetValue(setting, captureNumber("HISPEED_VALUE", config.Get<double>(setting), min, max, step));
            }
            else
                requireEnabled("HISPEED_MODE", "HISPEED_VALUE");
            Assert.That(mods.Any(mod => mod is BmsModLaneCover or BmsModLift), Is.False,
                "The author capture must configure reading controls through settings, without legacy reading Mods.");
            return mods.ToArray();

            void configureCover(string name, BmsRulesetSetting enabled, BmsRulesetSetting units, BmsRulesetSetting opacity)
            {
                config.SetValue(enabled, captureFlag(name));
                config.SetValue(units, 250f);
                config.SetValue(opacity, 1000f);
                if (!captureFlag(name))
                {
                    requireEnabled(name, name + "_VALUE", name + "_OPACITY");
                    return;
                }
                config.SetValue(units, (float)captureNumber(name + "_VALUE", 250, 0, 1000, 1));
                config.SetValue(opacity, (float)captureNumber(name + "_OPACITY", 1000, 0, 1000, 1));
            }
        }

        private static bool captureFlag(string name) => Environment.GetEnvironmentVariable(capture_setting_prefix + name) switch
        {
            null or "0" => false,
            "1" => true,
            _ => throw new InvalidOperationException(capture_setting_prefix + name + " must be 0 or 1."),
        };

        private static void requireEnabled(string owner, params string[] settings)
        {
            foreach (string setting in settings)
                if (Environment.GetEnvironmentVariable(capture_setting_prefix + setting) != null)
                    throw new InvalidOperationException(capture_setting_prefix + setting + " requires " + capture_setting_prefix + owner + ".");
        }

        private static double captureNumber(string name, double defaultValue, double min, double max, double step)
        {
            string? raw = Environment.GetEnvironmentVariable(capture_setting_prefix + name);
            if (raw == null)
                return defaultValue;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value)
                || value < min || value > max || Math.Abs((value - min) / step - Math.Round((value - min) / step)) > 0.000001)
                throw new InvalidOperationException($"{capture_setting_prefix}{name} must be {min}..{max} on step {step}.");
            return value;
        }

        private void recordStartOptions()
        {
            Mod[] actualMods = player.GameplayState.Mods.ToArray();
            var config = (BmsRulesetConfigManager)Game.Dependencies.Get<IRulesetConfigCache>().GetConfigFor(new BmsRuleset())!;
            BmsReadingSettingsSnapshot reading = config.CaptureReadingSettings();
            var nativeCovers = renderer.Playfield.CoverContainer.Children.OfType<BmsLaneCover>().ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(actualMods.Any(mod => mod is BmsModLaneCover or BmsModLift), Is.False);
                Assert.That(renderer.Mods.Any(mod => mod is BmsModLaneCover or BmsModLift), Is.False);
                Assert.That(Game.SelectedMods.Value.Any(mod => mod is BmsModLaneCover or BmsModLift), Is.False);
                Assert.That(reading.SuddenEnabled, Is.EqualTo(captureFlag("COVER")));
                Assert.That(reading.HiddenEnabled, Is.EqualTo(captureFlag("HIDDEN")));
                Assert.That(reading.LiftEnabled, Is.EqualTo(captureFlag("LIFT")));
                Assert.That(reading.LaneCoversEnabled, Is.True);
                Assert.That(config.Get<bool>(BmsRulesetSetting.RememberReadingChanges), Is.True);
                Assert.That(renderer.SuddenActive, Is.EqualTo(reading.SuddenEnabled));
                Assert.That(renderer.HiddenActive, Is.EqualTo(reading.HiddenEnabled));
                Assert.That(renderer.SuddenUnits, Is.EqualTo(reading.SuddenEnabled ? reading.SuddenUnits : 0));
                Assert.That(renderer.HiddenUnits, Is.EqualTo(reading.HiddenEnabled ? reading.HiddenUnits : 0));
                Assert.That(nativeCovers.Length, Is.EqualTo((reading.SuddenEnabled ? 1 : 0) + (reading.HiddenEnabled ? 1 : 0)));
            });
            foreach (BmsLaneCover native in nativeCovers)
            {
                bool sudden = native.CoverPosition == BmsLaneCoverPosition.Sudden;
                float units = sudden ? reading.SuddenUnits : reading.HiddenUnits;
                float opacity = sudden ? reading.SuddenOpacity : reading.HiddenOpacity;
                Assert.That(native.CoverEnabled.Value, Is.True);
                Assert.That(native.CoverPercent.Value, Is.EqualTo(units));
                Assert.That(native.CoverOpacity.Value, Is.EqualTo(opacity));
                Assert.That(coverDisplayAlpha(native), Is.EqualTo(opacity / 1000f).Within(0.00001));
                Container sceneHost = coverSceneHost(native);
                if (sceneHost.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Any(visual => visual.IsApplied))
                    Assert.That(sceneHost.Alpha, Is.EqualTo(opacity / 1000f).Within(0.00001),
                        "Authored cover parts must receive the same native opacity as the compatibility display.");
            }
            Assert.That(renderer.Playfield.LiftUnits.Value, Is.EqualTo(reading.LiftEnabled ? reading.LiftUnits : 0));
            BmsScrollSpeedMetrics metrics = renderer.SpeedMetrics.Value;
            Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(config.Get<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode)));
            Assert.That(metrics.HiSpeedMode, Is.EqualTo(renderer.HiSpeedMode.Value));
            Assert.That(metrics.ScrollSpeed, Is.EqualTo(renderer.SelectedHiSpeed.Value));
            if (Environment.GetEnvironmentVariable(capture_setting_prefix + "HISPEED_VALUE") is string requestedSpeed)
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(double.Parse(requestedSpeed, CultureInfo.InvariantCulture)).Within(0.000001));
            Assert.That(metrics.SuddenUnits, Is.EqualTo(nativeCovers.SingleOrDefault(cover => cover.CoverPosition == BmsLaneCoverPosition.Sudden)?.CoverPercent.Value ?? 0));
            Assert.That(metrics.HiddenUnits, Is.EqualTo(nativeCovers.SingleOrDefault(cover => cover.CoverPosition == BmsLaneCoverPosition.Hidden)?.CoverPercent.Value ?? 0));
            Assert.That(metrics.LiftUnits, Is.EqualTo(renderer.Playfield.LiftUnits.Value));
            var gauge = (BmsGaugeProcessor)player.GameplayState.HealthProcessor;
            Assert.That(gauge.GaugeType, Is.EqualTo(actualMods.OfType<BmsModGauge>().SingleOrDefault()?.GaugeType ?? BmsGaugeType.Normal));
            Assert.That(gauge.GaugeRulesFamily, Is.EqualTo(actualMods.OfType<BmsModGaugeRules>().SingleOrDefault()?.GaugeRulesFamily ?? BmsGaugeRulesFamily.Legacy));
            File.WriteAllText(Path.Combine(outputDirectory, "start-options.json"), JsonSerializer.Serialize(new
            {
                purpose = "actual-OMS-Player-configuration-not-original-menu-equivalence",
                requested = new[] { "COVER", "COVER_VALUE", "COVER_OPACITY", "HIDDEN", "HIDDEN_VALUE", "HIDDEN_OPACITY", "LIFT", "LIFT_VALUE",
                    "HISPEED_MODE", "HISPEED_VALUE", "GAUGE", "GAUGE_RULES" }.Where(name => Environment.GetEnvironmentVariable(capture_setting_prefix + name) != null)
                    .ToDictionary(name => name, name => Environment.GetEnvironmentVariable(capture_setting_prefix + name)),
                mods = actualMods.Select(mod => new { type = mod.GetType().Name, mod.Acronym }).ToArray(),
                covers = nativeCovers.Select(cover => new
                {
                    position = cover.CoverPosition.ToString(),
                    enabled = cover.CoverEnabled.Value,
                    value = cover.CoverPercent.Value,
                    opacity = cover.CoverOpacity.Value,
                    displayAlpha = coverDisplayAlpha(cover),
                    sceneHostAlpha = coverSceneHost(cover).Alpha
                }).ToArray(),
                lift = renderer.Playfield.LiftUnits.Value,
                configuration = new
                {
                    mode = config.Get<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode).ToString(),
                    normal = config.Get<double>(BmsRulesetSetting.ScrollSpeed),
                    floating = config.Get<double>(BmsRulesetSetting.FloatingHiSpeed),
                    classic = config.Get<double>(BmsRulesetSetting.ClassicHiSpeed),
                    readingSettings = reading,
                    rememberReadingChanges = config.Get<bool>(BmsRulesetSetting.RememberReadingChanges)
                },
                actualMode = renderer.HiSpeedMode.Value.ToString(),
                selectedValue = renderer.SelectedHiSpeed.Value,
                derivedMetrics = metrics,
                gauge = new
                {
                    type = gauge.GaugeType.ToString(),
                    rulesFamily = gauge.GaugeRulesFamily.ToString(),
                    gauge.Health.Value,
                    gauge.CurrentMaximumGauge,
                    gauge.CurrentClearThreshold
                },
                autoplayBoundary = "ASCR is not added: it is incompatible with the real full-autoplay capture.",
            }));
        }

        private static float coverDisplayAlpha(BmsLaneCover cover)
            => (float)typeof(BmsLaneCover).GetProperty("CoverDisplayAlpha", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cover)!;

        private static Container coverSceneHost(BmsLaneCover cover)
            => (Container)typeof(BmsLaneCover).GetField("sceneHost", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cover)!;

        private void recordLiveHitExplosionRgb()
        {
            if (explosionRgbFailure != null || brightnessSlotsObserved.Contains(GameplaySkinSlotCatalog.HitExplosion.Id))
                return;
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinSpecialisedSceneVisual? visual = renderer.Playfield.Lanes.SelectMany(lane => lane.ActiveHitExplosions)
                .Select(effect => effect.SpecialisedSceneVisual).FirstOrDefault(candidate => candidate is { IsApplied: true }
                    && candidate.RuntimeNodes.Any(node => runtime.PreparedScene.Program.Bindings.Any(binding =>
                        binding.Property == GameplaySkinSceneProperty.Brightness && binding.Source == GameplaySkinSceneBindingSource.Option
                        && binding.TargetNodeId == node.PreparedNode.Source.Id)));
            // Observe the same live lease after another complete Game update, rather than its OnApply boundary.
            if (visual == null || !ReferenceEquals(visual, pendingExplosionRgb) || visual.BoundObjectId != pendingExplosionObjectId)
            {
                pendingExplosionRgb = visual;
                pendingExplosionObjectId = visual?.BoundObjectId;
                return;
            }
            try
            {
                recordRgbEvidence("hit-explosion-live");
                Assert.That(brightnessSlotsObserved, Does.Contain(GameplaySkinSlotCatalog.HitExplosion.Id));
            }
            catch (Exception exception)
            {
                // The probe runs outside a test step. Preserve the failure for the final NUnit assertion step.
                explosionRgbFailure = exception;
            }
            pendingExplosionRgb = null;
            pendingExplosionObjectId = null;
        }

        private void recordRgbEvidence(string filename)
        {
            if (Environment.GetEnvironmentVariable(capture_setting_prefix + "START_OPTIONS") != "1")
                return;
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinPreparedSceneBinding[] bindings = runtime.PreparedScene.Program.Bindings.Where(binding =>
                binding.Property == GameplaySkinSceneProperty.Brightness && binding.Source == GameplaySkinSceneBindingSource.Option).ToArray();
            Assert.That(bindings.Select(binding => binding.OptionId).Distinct(), Is.EquivalentTo(brightness_options));
            GameplaySkinSpecialisedSceneVisual[] visuals = renderer.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Where(visual => visual.IsApplied).ToArray();
            GameplaySkinSceneRuntimeNode[] nodes = lightingNodes().Concat(visuals.SelectMany(visual => visual.RuntimeNodes)).Distinct().ToArray();
            var evidence = new List<object>();
            foreach (GameplaySkinSceneRuntimeNode node in nodes)
            {
                GameplaySkinPreparedSceneBinding? binding = bindings.SingleOrDefault(binding => binding.TargetNodeId == node.PreparedNode.Source.Id);
                if (binding == null)
                    continue;
                GameplaySkinOptionValue option = runtime.Publication.Snapshot.Context.OptionValues.Values[binding.OptionId!];
                Assert.That(option.Kind, Is.EqualTo(GameplaySkinOptionKind.Number));
                Color4 actual = node.RootDrawable.Colour.TopLeft.SRGB;
                Color4 original = authoredColour(node);
                Color4 content = node.ContentDrawable.Colour.TopLeft.SRGB;
                Assert.Multiple(() =>
                {
                    Assert.That(actual.R, Is.EqualTo(option.NumberValue).Within(0.00001), node.InstanceId);
                    Assert.That(actual.G, Is.EqualTo(option.NumberValue).Within(0.00001), node.InstanceId);
                    Assert.That(actual.B, Is.EqualTo(option.NumberValue).Within(0.00001), node.InstanceId);
                    Assert.That(actual.A, Is.EqualTo(1), node.InstanceId);
                    Assert.That(node.RootDrawable.Alpha, Is.EqualTo(1), "RGB must not replace native or authored visibility.");
                    Assert.That(content.R, Is.EqualTo(original.R).Within(0.00001), node.InstanceId + " original content red");
                    Assert.That(content.G, Is.EqualTo(original.G).Within(0.00001), node.InstanceId + " original content green");
                    Assert.That(content.B, Is.EqualTo(original.B).Within(0.00001), node.InstanceId + " original content blue");
                    Assert.That(content.A, Is.EqualTo(original.A).Within(0.00001), node.InstanceId + " original content colour alpha");
                });
                string slot = node.PreparedNode.OwningSlotKey!.Slot.Id;
                GameplaySkinSpecialisedSceneVisual? visual = visuals.SingleOrDefault(visual => visual.RuntimeNodes.Contains(node));
                DrawableBmsBarLine? nativeBarLine = slot == GameplaySkinSlotCatalog.BarLine.Id
                    ? renderer.ChildrenOfType<DrawableBmsBarLine>().Single(barLine => isWithin(node.RootDrawable, barLine)) : null;
                brightnessOptionsObserved.Add(binding.OptionId!);
                brightnessSlotsObserved.Add(slot);
                var bounds = node.RootDrawable.ScreenSpaceDrawQuad.AABBFloat;
                evidence.Add(new
                {
                    option = binding.OptionId,
                    requestedPublishedValue = option.CanonicalValue,
                    node = node.PreparedNode.Source.Id,
                    node.InstanceId,
                    slot,
                    target = node.PreparedNode.OwningSlotKey.Target.ToString(),
                    node.BoundObjectId,
                    specialised = visual != null,
                    specialisedAlpha = visual?.Alpha,
                    nativeBarLineAlpha = nativeBarLine?.Alpha,
                    rootRef = RuntimeHelpers.GetHashCode(node.RootDrawable),
                    contentRef = RuntimeHelpers.GetHashCode(node.ContentDrawable),
                    screenBounds = new { bounds.Left, bounds.Top, bounds.Width, bounds.Height },
                    preparedRect = new { node.Rect.X, node.Rect.Y, node.Rect.Width, node.Rect.Height },
                    transform = new
                    {
                        node.TransformDrawable.X,
                        node.TransformDrawable.Y,
                        node.TransformDrawable.Width,
                        node.TransformDrawable.Height,
                        relativePositionAxes = node.TransformDrawable.RelativePositionAxes.ToString(),
                        relativeSizeAxes = node.TransformDrawable.RelativeSizeAxes.ToString()
                    },
                    rootRgb = new[] { actual.R, actual.G, actual.B, actual.A },
                    contentRgba = new[] { content.R, content.G, content.B, content.A },
                    node.RootDrawable.Alpha,
                    transformAlpha = node.TransformDrawable.Alpha,
                    alphaAuthority = "original dynamic opacity/visibility remains on TransformDrawable and native ancestors; RGB is a separate opaque carrier",
                    artwork = nodes.Where(child => isWithin(child.RootDrawable, node.RootDrawable)).Select(child => rgbArtwork(runtime, child)).ToArray(),
                });
            }
            Assert.That(evidence, Is.Not.Empty, "Inspect live authored roots and applied native pooled visuals, not only option values.");
            Assert.That(runtime.RuntimeFaults, Is.Empty);
            File.WriteAllText(Path.Combine(outputDirectory, filename + ".rgb.json"), JsonSerializer.Serialize(new
            {
                purpose = "actual-Player-RGB-carriers-and-original-content",
                authoritativeGameplayTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                evidence,
                hud = startOptionHudEvidence(runtime, nodes),
                optionPresentation = GameplaySkinOptionPresentationCapture.Capture(runtime, nodes),
            }));
        }

        private object startOptionHudEvidence(GameplaySkinSceneRuntimeHost runtime, GameplaySkinSceneRuntimeNode[] nodes)
        {
            GameplaySkinScoreStateSnapshot runtimeScore = (GameplaySkinScoreStateSnapshot)hud_score_state.GetValue(runtime)!;
            GameplaySkinScoreStateSnapshot retainedScore = lightingState().Score;
            GameplaySkinPreparedSceneBinding[] bindings = runtime.PreparedScene.Program.Bindings.Where(binding =>
                binding.Property == GameplaySkinSceneProperty.Text
                && binding.Source is GameplaySkinSceneBindingSource.ScoreValue or GameplaySkinSceneBindingSource.GaugeValue).ToArray();
            BmsGaugeProcessor gauge = (BmsGaugeProcessor)player.GameplayState.HealthProcessor;
            HUDOverlay overlay = player.ChildrenOfType<HUDOverlay>().Single();
            return new
            {
                runtimeScore,
                retainedScore,
                nativeScore = new
                {
                    score.TotalScore.Value,
                    exScore = ((BmsScoreProcessor)score).CurrentExScore,
                    combo = score.Combo.Value,
                    statistics = score.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                },
                nativeGauge = new { gauge.Health.Value, gauge.CurrentMaximumGauge, type = gauge.GaugeType.ToString() },
                modDisplay = geometry(overlay.ModDisplay),
                topRightElements = geometry(overlay.TopRightElements),
                readouts = nodes.Where(node => node.ContentDrawable is GameplaySkinBitmapText
                    && bindings.Any(binding => binding.TargetNodeId == node.PreparedNode.Source.Id)).Select(node =>
                {
                    var text = (GameplaySkinBitmapText)node.ContentDrawable;
                    var ancestors = new List<object>();
                    for (Drawable? parent = node.RootDrawable.Parent; parent != null; parent = parent.Parent)
                        ancestors.Add(geometry(parent));
                    return new
                    {
                        node = node.PreparedNode.Source.Id,
                        node.InstanceId,
                        bindings = bindings.Where(binding => binding.TargetNodeId == node.PreparedNode.Source.Id)
                            .Select(binding => binding.Source.ToString()).ToArray(),
                        text.AuthorText,
                        displayedText = text.Text.ToString(),
                        fontSize = text.Font.Size,
                        naturalSize = new { text.NaturalSize.X, text.NaturalSize.Y },
                        root = geometry(node.RootDrawable),
                        transform = geometry(node.TransformDrawable),
                        content = geometry(text),
                        ancestors,
                    };
                }).ToArray(),
            };

            static object geometry(Drawable drawable)
            {
                var bounds = drawable.ScreenSpaceDrawQuad.AABBFloat;
                Color4 colour = drawable.Colour.TopLeft.SRGB;
                return new
                {
                    type = drawable.GetType().Name,
                    drawable.Name,
                    drawable.Alpha,
                    drawable.IsPresent,
                    drawable.X,
                    drawable.Y,
                    drawable.Width,
                    drawable.Height,
                    drawable.DrawWidth,
                    drawable.DrawHeight,
                    anchor = drawable.Anchor.ToString(),
                    origin = drawable.Origin.ToString(),
                    scale = new { drawable.Scale.X, drawable.Scale.Y },
                    masking = drawable is Container { Masking: true },
                    bounds = new { bounds.Left, bounds.Top, bounds.Width, bounds.Height },
                    rgba = new[] { colour.R, colour.G, colour.B, colour.A },
                };
            }
        }

        private void assertRgbEvidenceCoverage()
        {
            if (explosionRgbFailure != null)
                ExceptionDispatchInfo.Capture(explosionRgbFailure).Throw();
            Assert.That(brightnessOptionsObserved, Is.EquivalentTo(brightness_options));
            foreach (GameplaySkinSlotDescriptor slot in new[] { GameplaySkinSlotCatalog.Note, GameplaySkinSlotCatalog.LongNoteHead,
                         GameplaySkinSlotCatalog.LongNoteBody, GameplaySkinSlotCatalog.LongNoteTail, GameplaySkinSlotCatalog.LaneSurface,
                         GameplaySkinSlotCatalog.HitExplosion, GameplaySkinSlotCatalog.JudgementDisplay, GameplaySkinSlotCatalog.BarLine })
                Assert.That(brightnessSlotsObserved, Does.Contain(slot.Id), "The capture must inspect the actually applied material: " + slot.Id);
            if (Environment.GetEnvironmentVariable(capture_setting_prefix + "COLUMN") == "1")
                Assert.That(brightnessSlotsObserved, Does.Contain(GameplaySkinSlotCatalog.StageBackground.Id),
                    "Column brightness must reach an actual stage-background node.");
        }

        private static bool isWithin(Drawable drawable, Drawable root)
        {
            for (Drawable? current = drawable; current != null; current = current.Parent)
                if (ReferenceEquals(current, root))
                    return true;
            return false;
        }

        private static Color4 authoredColour(GameplaySkinSceneRuntimeNode node)
        {
            if (!node.PreparedNode.Source.Properties.TryGetValue("colour", out GameplaySkinScenePropertyValue? value))
                return Color4.White;
            string raw = value.StringValue!;
            return new Color4(byte.Parse(raw.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(raw.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(raw.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                raw.Length == 9 ? byte.Parse(raw.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : byte.MaxValue);
        }

        private static object rgbArtwork(GameplaySkinSceneRuntimeHost runtime, GameplaySkinSceneRuntimeNode node)
        {
            var textures = new List<object>();
            if (node.ContentDrawable is Sprite sprite)
            {
                IEnumerable<Texture?> allowed = new[] { node.PreparedNode.ResolvedTexture }
                    .Concat(runtime.PreparedScene.Program.Variants.Where(variant => variant.TargetNodeId == node.PreparedNode.Source.Id && variant.Property == GameplaySkinSceneProperty.Resource)
                        .SelectMany(variant => variant.Cases.Select(item => item.Texture).Prepend(variant.DefaultTexture)))
                    .Concat(runtime.PreparedScene.Program.Tracks.Where(track => track.TargetNodeId == node.PreparedNode.Source.Id && track.Property == GameplaySkinSceneProperty.Resource)
                        .SelectMany(track => track.Keyframes.Select(frame => frame.Value.Texture)))
                    .Concat(runtime.PreparedScene.Program.StateMachines.SelectMany(machine => machine.States).SelectMany(state => state.Assignments)
                        .Where(assignment => assignment.TargetNodeId == node.PreparedNode.Source.Id && assignment.Property == GameplaySkinSceneProperty.Resource)
                        .Select(assignment => assignment.Value.Texture));
                Assert.That(allowed.Any(texture => texture != null && ReferenceEquals(texture, sprite.Texture)), Is.True, node.InstanceId + " retains an exact authored texture");
                textures.Add(new
                {
                    actualRef = RuntimeHelpers.GetHashCode(sprite.Texture!),
                    resources = runtime.PreparedScene.Resources
                    .Where(resource => resource.Texture != null && ReferenceEquals(resource.Texture, sprite.Texture)).Select(resource => resource.Id).ToArray()
                });
            }
            int? recordedFrame = null;
            if (node.ContentDrawable is GameplaySkinRecordedAnimationDrawable animation)
            {
                recordedFrame = (int)opening_applied_frame.GetValue(animation)!;
                GameplaySkinRecordedPreparedFrame frame = animation.Program.GetPreparedFrame(recordedFrame.Value);
                Sprite[] pieces = ((IEnumerable<Sprite>)rgb_recorded_sprites.GetValue(animation)!).ToArray();
                Assert.That(pieces.Count(piece => piece.Alpha > 0), Is.EqualTo(frame.PieceCount));
                for (int i = 0; i < frame.PieceCount; i++)
                {
                    string? resourceId = frame.GetPiece(i).Resource;
                    if (resourceId != null)
                        Assert.That(pieces[i].Texture, Is.SameAs(runtime.PreparedScene.Resources.Single(resource => resource.Id == resourceId).Texture));
                    Assert.That(pieces[i].Alpha, Is.EqualTo(1));
                    textures.Add(new { piece = i, resource = resourceId, textureRef = pieces[i].Texture == null ? (int?)null : RuntimeHelpers.GetHashCode(pieces[i].Texture!) });
                }
            }
            Color4 colour = node.ContentDrawable.Colour.TopLeft.SRGB;
            var bounds = node.ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
            return new
            {
                node = node.PreparedNode.Source.Id,
                node.InstanceId,
                contentType = node.ContentDrawable.GetType().Name,
                screenBounds = new { bounds.Left, bounds.Top, bounds.Width, bounds.Height },
                preparedRect = new { node.Rect.X, node.Rect.Y, node.Rect.Width, node.Rect.Height },
                transform = new
                {
                    node.TransformDrawable.X,
                    node.TransformDrawable.Y,
                    node.TransformDrawable.Width,
                    node.TransformDrawable.Height,
                    relativePositionAxes = node.TransformDrawable.RelativePositionAxes.ToString(),
                    relativeSizeAxes = node.TransformDrawable.RelativeSizeAxes.ToString()
                },
                colour = new[] { colour.R, colour.G, colour.B, colour.A },
                contentAlpha = node.ContentDrawable.Alpha,
                transformAlpha = node.TransformDrawable.Alpha,
                recordedFrame,
                textures
            };
        }
    }
}
