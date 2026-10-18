// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    public partial class TestSceneManiaGameplaySkinLayoutProduction
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestAuthoredKeysReceiveNativeInputAndDrawBelowTheScrollingMask(bool capturePixels)
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            Column column = null!;
            ManiaInputManager nativeInput = null!;
            Drawable visual = null!;
            Task<Image<Rgba32>?>? capture = null;

            AddStep("pixel evidence requires an actual renderer", () =>
            {
                if (capturePixels && gameHost is HeadlessGameHost)
                    Assert.Ignore("Framebuffer evidence requires the desktop runner; the other case still exercises native input and the scene consumer.");
            });
            AddStep("create an explicit authored keyboard below the native field", () =>
            {
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-authored-key-area-{Guid.NewGuid():N}");
                writePublicManiaMaterialPackage(packageRoot);
                JObject manifest = JObject.Parse(File.ReadAllText(Path.Combine(packageRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME)));
                manifest["layouts"] = JArray.Parse("""
                                                    [{"id":"layout.keys","ruleset":"mania","keymode":"4k","presentation":"any",
                                                      "keyAreaHeight":0.15,"stages":[{"rect":[0.15,0.1,0.4,0.45],
                                                        "lanes":[[0.02,0.2],[0.26,0.2],[0.5,0.2],[0.74,0.2]]}]}]
                                                    """);
                File.WriteAllText(Path.Combine(packageRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME), manifest.ToString());
                JObject scene = JObject.Parse(File.ReadAllText(Path.Combine(packageRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME)));
                JObject key = (JObject)scene["root"]!["children"]!.Single(n => n["id"]!.Value<string>() == "node.key-visual");
                key["properties"] = JObject.Parse("""{"opacity":1,"visible":false}""");
                scene["bindings"] = new JArray(new JObject { ["id"] = "binding.key.pressed", ["source"] = "input.pressed", ["target"] = "node.key-visual", ["property"] = "visible" });
                File.WriteAllText(Path.Combine(packageRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME), scene.ToString());
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for the immutable selected package", () => publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount a real mania ruleset and its input manager", () => Add(renderer = new CurrentRevisionManiaMaterialHost(publicMaterialSkinManager!)));
            AddUntilStep("wait for the real column and prepared key scene", () => renderer.Drawable.IsLoaded
                && renderer.Drawable.Playfield.Stages.Count == 1 && renderer.FirstColumn.IsLoaded
                && renderer.FirstColumn.PreparedKeyVisual?.IsApplied == true);
            AddStep("the native owner uses the authored keyboard band", () =>
            {
                column = renderer.FirstColumn;
                // RootDrawables are stable ownership wrappers; the actual visible binding controls the node's transform.
                visual = column.PreparedKeyVisual!.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "node.key-visual").TransformDrawable;
                // DrawableRuleset also owns a separate resume-overlay input manager. Use this column's actual
                // cached manager, exactly as Column's production touch input does.
                nativeInput = column.Dependencies.Get<ManiaInputManager>();
                Assert.That(column.UsesSeparateKeyArea, Is.True);
                GameplaySkinLayoutRect key = column.LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.KEY_VISUAL_SURFACE_PREFIX + column.LayoutLaneId.Value).Rect;
                Assert.That(key.Top, Is.GreaterThanOrEqualTo(column.LayoutSnapshot.GetLane(column.LayoutLaneId).Rect.Bottom));
            });
            AddUntilStep("the unpressed native action leaves its actual visual hidden", () => visual.Alpha == 0
                && !nativeInput.KeyBindingContainer.PressedActions.Contains(column.Action.Value));
            AddStep("press the actual native mania action", () =>
            {
                nativeInput.KeyBindingContainer.TriggerPressed(column.Action.Value);
            });
            AddUntilStep("native input lights the exact author key", () => visual.Alpha > 0 && visual.IsPresent
                && nativeInput.KeyBindingContainer.PressedActions.Contains(column.Action.Value)
                && renderer.Drawable.GameplaySkinSceneRuntime.RuntimeFaults.Count == 0);
            if (capturePixels)
            {
                AddStep("capture the actual rendered keyboard outside the scroll mask", () => capture = gameHost.TakeScreenshotAsync());
                AddUntilStep("wait for the render thread capture", () => capture?.IsCompleted == true);
                AddStep("the native keyboard is visible in the actual framebuffer", () =>
                {
                    using Image<Rgba32> pixels = capture!.GetAwaiter().GetResult().AsNonNull();
                    var centre = visual.ScreenSpaceDrawQuad.Centre;
                    Rgba32 pixel = pixels[(int)centre.X, (int)centre.Y];
                    Assert.Multiple(() =>
                    {
                        Assert.That(pixel.R, Is.EqualTo(170).Within(3));
                        Assert.That(pixel.G, Is.EqualTo(85).Within(3));
                        Assert.That(pixel.B, Is.EqualTo(230).Within(3));
                    });
                });
            }
            AddStep("release the actual native mania action", () =>
            {
                nativeInput.KeyBindingContainer.TriggerReleased(column.Action.Value);
            });
            AddUntilStep("the exact key follows the real release", () => visual.Alpha == 0
                && !nativeInput.KeyBindingContainer.PressedActions.Contains(column.Action.Value));
            AddStep("detach the authored keyboard", () => renderer.Expire());
            AddUntilStep("wait for exact detach", () => renderer.Parent == null);
        }
    }
}
