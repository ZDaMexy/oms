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
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    public partial class TestSceneManiaAuthorSkinCapture
    {
        private const string capture_setting_prefix = "OMS_AUTHOR_SKIN_CAPTURE_";
        private static readonly string[] brightness_options = Environment.GetEnvironmentVariable(capture_setting_prefix + "COLUMN") == "1"
            ? ["lane-objects-brightness", "column-brightness"] : ["lane-objects-brightness"];
        private static readonly FieldInfo rgb_recorded_sprites = typeof(GameplaySkinRecordedAnimationDrawable)
            .GetField("sprites", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly HashSet<string> brightnessOptionsObserved = new(StringComparer.Ordinal);
        private readonly HashSet<string> brightnessSlotsObserved = new(StringComparer.Ordinal);
        private GameplaySkinSpecialisedSceneVisual? pendingExplosionRgb;
        private long? pendingExplosionObjectId;
        private Exception? explosionRgbFailure;

        private GameplaySkinOptionDefinition[] maniaSettingsDefinitions()
            => Game.Dependencies.Get<SkinManager>().CurrentSkin.Value.PreparedGameplaySkinPackage!.Manifest!.Options
                .Where(definition => definition.Rulesets.Count == 0 || definition.Rulesets.Contains("mania", StringComparer.Ordinal)).ToArray();

        private static bool hasDeclaredSettingsControls(SkinOptionsSettings controls, GameplaySkinOptionDefinition[] definitions)
            => controls.ChildrenOfType<FormDropdown<string>>().Count() == definitions.Count(definition => definition.Kind == GameplaySkinOptionKind.Choice
                    && definition.Choices.All(choice => choice.PreviewPath == null))
                && controls.ChildrenOfType<SkinOptionChoiceSettings>().Count() == definitions.Count(definition => definition.Kind == GameplaySkinOptionKind.Choice
                    && definition.Choices.Any(choice => choice.PreviewPath != null))
                && controls.ChildrenOfType<FormSliderBar<double>>().Count() == definitions.Count(definition => definition.Kind == GameplaySkinOptionKind.Number)
                && controls.ChildrenOfType<FormCheckBox>().Count() == definitions.Count(definition => definition.Kind == GameplaySkinOptionKind.Toggle);

        private void recordLiveHitExplosionRgb()
        {
            if (explosionRgbFailure != null || brightnessSlotsObserved.Contains(GameplaySkinSlotCatalog.HitExplosion.Id))
                return;
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinSpecialisedSceneVisual? visual = renderer.Playfield.Stages.SelectMany(stage => stage.Columns).SelectMany(column => column.ActiveHitExplosions)
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

        private sealed partial class LiveHitExplosionProbe : Drawable
        {
            private readonly Action sample;

            public LiveHitExplosionProbe(Action sample)
            {
                this.sample = sample;
            }

            protected override void Update()
            {
                base.Update();
                sample();
            }
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
            GameplaySkinSceneRuntimeNode[] nodes = openingNodes().Concat(visuals.SelectMany(visual => visual.RuntimeNodes)).Distinct().ToArray();
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
                DrawableBarLine? nativeBarLine = slot == GameplaySkinSlotCatalog.BarLine.Id
                    ? renderer.ChildrenOfType<DrawableBarLine>().Single(barLine => isWithin(node.RootDrawable, barLine)) : null;
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
            File.WriteAllText(Path.Combine(output, filename + ".rgb.json"), JsonSerializer.Serialize(new
            {
                purpose = "actual-Player-RGB-carriers-and-original-content",
                authoritativeGameplayTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                evidence,
                optionPresentation = GameplaySkinOptionPresentationCapture.Capture(runtime, nodes),
            }));
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
