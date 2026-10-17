// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Tests.Visual.Skinning
{
    [HeadlessTest]
    public partial class TestSceneGameplaySkinHudCapture : OsuTestScene
    {
        [TestCase(1)]
        [TestCase(2)]
        public void TestOffscreenHudOwnerRendersBeforeFullCanvasPartitionViews(int partitionCount)
        {
            Container foreground = null!;
            BufferedContainer capture = null!;
            Container[] clips = Array.Empty<Container>();

            AddStep("mount the actual HUD capture and partition view factories", () =>
            {
                Type captureType = typeof(GameplaySkinHudProgrammaticVisualAdapter)
                    .GetNestedType("GameplaySkinHudCaptureSurface", BindingFlags.NonPublic)!;
                capture = (BufferedContainer)Activator.CreateInstance(captureType, nonPublic: true)!;
                capture.Child = new Box { RelativeSizeAxes = Axes.Both };

                MethodInfo createClip = typeof(GameplaySkinHudProgrammaticVisualAdapter)
                    .GetMethod("createClip", BindingFlags.NonPublic | BindingFlags.Static)!;
                clips = Enumerable.Range(0, partitionCount)
                    .Select(index => (Container)createClip.Invoke(null, new object[]
                    {
                        capture, (float)index / partitionCount, 1f / partitionCount,
                    })!)
                    .ToArray();

                Child = foreground = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(600, 300),
                    Masking = true,
                    Children = new Drawable[] { capture }.Concat(clips).ToArray(),
                };
            });
            AddUntilStep("offscreen source and every foreground view are loaded", () => capture.IsLoaded
                && clips.All(clip => clip.ChildrenOfType<BufferedContainerView<Drawable>>().Single().IsLoaded));
            AddStep("a visible fallback cannot read a geometrically culled owner", assertCaptureAndViews);
            AddStep("hide the fallback partitions when their author replacements become ready", () =>
            {
                foreach (Container clip in clips)
                    clip.Alpha = 0;
            });
            AddStep("restore the same native fallback views", () =>
            {
                foreach (Container clip in clips)
                    clip.Alpha = 1;
            });
            AddStep("restored views retain their source and exact partition geometry", assertCaptureAndViews);

            void assertCaptureAndViews()
            {
                // Perform the same post-update masking pass even on a headless host, where no GPU draw follows.
                foreground.UpdateSubTreeMasking();
                Assert.Multiple(() =>
                {
                    Assert.That(capture.ScreenSpaceDrawQuad.AABBFloat.Left,
                        Is.GreaterThanOrEqualTo(foreground.ScreenSpaceDrawQuad.AABBFloat.Right),
                        "The real capture remains outside the visible HUD rather than being moved onto the screen.");
                    Assert.That(isMaskedAway(capture), Is.False,
                        "The owner must get a first BufferedDrawNode.Draw before a visible view reads MainBuffer.");
                    Assert.That(foreground.Children[0], Is.SameAs(capture),
                        "The source precedes all foreground views in the actual depth-sorted draw order.");
                    Assert.That(capture.DrawSize, Is.EqualTo(new Vector2(600, 300)));
                    Assert.That(capture.Alpha, Is.EqualTo(1));
                });

                for (int index = 0; index < clips.Length; index++)
                {
                    Container clip = clips[index];
                    BufferedContainerView<Drawable> view = clip.ChildrenOfType<BufferedContainerView<Drawable>>().Single();
                    Vector2 clipStart = foreground.ToLocalSpace(clip.ScreenSpaceDrawQuad.TopLeft);
                    Vector2 canvasStart = foreground.ToLocalSpace(view.ScreenSpaceDrawQuad.TopLeft);
                    Vector2 canvasEnd = foreground.ToLocalSpace(view.ScreenSpaceDrawQuad.BottomRight);
                    Assert.Multiple(() =>
                    {
                        Assert.That(clip.Masking, Is.True);
                        Assert.That(clip.Alpha, Is.EqualTo(1), "Native fallback remains visible until its replacement is ready.");
                        Assert.That(isMaskedAway(clip), Is.False);
                        Assert.That(clipStart.X, Is.EqualTo(600f * index / partitionCount).Within(0.001));
                        Assert.That(clipStart.Y, Is.EqualTo(0).Within(0.001));
                        Assert.That(clip.DrawWidth, Is.EqualTo(600f / partitionCount).Within(0.001));
                        Assert.That(clip.DrawHeight, Is.EqualTo(300).Within(0.001));
                        Assert.That(view.IsPresent, Is.True);
                        Assert.That(isMaskedAway(view), Is.False);
                        Assert.That(view.DrawSize, Is.EqualTo(new Vector2(600, 300)),
                            "Each clipped view samples the complete HUD canvas, not a zero-sized or half-width copy.");
                        Assert.That(canvasStart.X, Is.EqualTo(0).Within(0.001));
                        Assert.That(canvasStart.Y, Is.EqualTo(0).Within(0.001));
                        Assert.That(canvasEnd.X, Is.EqualTo(600).Within(0.001));
                        Assert.That(canvasEnd.Y, Is.EqualTo(300).Within(0.001));
                    });
                }
            }
        }

        private static bool isMaskedAway(Drawable drawable)
            => (bool)typeof(Drawable).GetProperty("IsMaskedAway", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(drawable)!;
    }
}
