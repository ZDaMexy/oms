// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests
{
    public partial class TestScenePlayfieldCoveringContainer : OsuTestScene
    {
        private readonly ScrollingTestContainer scrollingContainer;
        private readonly PlayfieldCoveringWrapper cover;
        private readonly Box content;

        public TestScenePlayfieldCoveringContainer()
        {
            Child = scrollingContainer = new ScrollingTestContainer(ScrollingDirection.Down)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(300, 500),
                Child = cover = new PlayfieldCoveringWrapper(content = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Orange
                })
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };
        }

        [Test]
        public void TestScrollingDownwards()
        {
            AddStep("set down scroll", () => scrollingContainer.Direction = ScrollingDirection.Down);
            AddStep("set coverage = 0.5", () => cover.Coverage.Value = 0.5f);
            AddStep("set coverage = 0.8f", () => cover.Coverage.Value = 0.8f);
            AddStep("set coverage = 0.2f", () => cover.Coverage.Value = 0.2f);
        }

        [Test]
        public void TestScrollingUpwards()
        {
            AddStep("set up scroll", () => scrollingContainer.Direction = ScrollingDirection.Up);
            AddStep("set coverage = 0.5", () => cover.Coverage.Value = 0.5f);
            AddStep("set coverage = 0.8f", () => cover.Coverage.Value = 0.8f);
            AddStep("set coverage = 0.2f", () => cover.Coverage.Value = 0.2f);
        }

        [Test]
        public void TestGameplaySkinSceneUsesTheNativeCoverageDirectionAndSeparateVisualLayer()
        {
            AddStep("add author fill and decoration probes", () =>
            {
                cover.GameplaySkinFillSceneOwner.Add(new Box { RelativeSizeAxes = Axes.Both });
                cover.GameplaySkinDecorationSceneOwner.Add(new Box { RelativeSizeAxes = Axes.Both });
            });
            AddStep("set down along-scroll with no coverage", () =>
            {
                scrollingContainer.Direction = ScrollingDirection.Down;
                cover.Direction = CoverExpandDirection.AlongScroll;
                cover.Coverage.Value = 0;
            });
            AddUntilStep("author scene is fully clipped at zero", () => Math.Abs(cover.GameplaySkinSceneCoverageHeight) < 0.001f);
            AddAssert("down-scroll transform is shared", () => cover.GameplaySkinSceneRotation == 180 && cover.GameplaySkinSceneScale == Vector2.One);
            AddAssert("author layer cannot alter content mask", () =>
                hasAncestor<BufferedContainer>(content)
                && !hasAncestor<BufferedContainer>(cover.GameplaySkinFillSceneOwner)
                && !hasAncestor<BufferedContainer>(cover.GameplaySkinDecorationSceneOwner));

            AddStep("set coverage = 0.5", () => cover.Coverage.Value = 0.5f);
            AddUntilStep("author clip follows native coverage", () => Math.Abs(cover.GameplaySkinSceneCoverageHeight - 0.5f) < 0.01f);
            AddStep("set coverage = 1", () => cover.Coverage.Value = 1);
            AddUntilStep("author clip follows full native coverage", () => Math.Abs(cover.GameplaySkinSceneCoverageHeight - 1) < 0.01f);
            AddStep("set up scroll", () => scrollingContainer.Direction = ScrollingDirection.Up);
            AddAssert("up-scroll transform is shared", () => cover.GameplaySkinSceneRotation == 0);
            AddStep("restore half coverage while scrolling up", () => cover.Coverage.Value = 0.5f);
            AddUntilStep("up-scroll author clip follows native coverage", () => Math.Abs(cover.GameplaySkinSceneCoverageHeight - 0.5f) < 0.01f);
            AddStep("expand against scroll", () => cover.Direction = CoverExpandDirection.AgainstScroll);
            AddAssert("against-scroll transform is shared", () => cover.GameplaySkinSceneScale == new Vector2(1, -1));
            AddStep("return coverage to zero", () => cover.Coverage.Value = 0);
            AddUntilStep("author scene closes with native cover", () => cover.GameplaySkinSceneCoverageHeight < 0.01f);
        }

        private static bool hasAncestor<T>(Drawable drawable)
            where T : Drawable
        {
            Drawable? current = drawable.Parent;

            while (current != null)
            {
                if (current is T)
                    return true;

                current = current.Parent;
            }

            return false;
        }
    }
}
