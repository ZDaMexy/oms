// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Drawables;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Tests.Skins
{
    [HeadlessTest]
    [TestFixture]
    public partial class SkinAsyncProductionHostOwnershipTest : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private bool originalShowStoryboard;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("disable storyboard before host load", () =>
            {
                originalShowStoryboard = config.Get<bool>(OsuSetting.ShowStoryboard);
                config.SetValue(OsuSetting.ShowStoryboard, false);
            });
        }

        [TearDownSteps]
        public void TearDownSteps() =>
            AddStep("restore storyboard setting", () => config.SetValue(OsuSetting.ShowStoryboard, originalShowStoryboard));

        [Test]
        public void TestDimmableStoryboardDisposeDuringAsyncSkinLoadReclaimsParticipantAndRetries()
        {
            DimmableStoryboard first = null!;
            DimmableStoryboard retry = null!;
            int participantBaseline = 0;

            AddStep("mount production dimmable storyboard", () => Add(first = createHost()));
            AddUntilStep("wait for disabled host", () => first.IsLoaded);
            AddStep("capture participant baseline", () =>
                participantBaseline = skinManager.CurrentRevision.ParticipantLeaseCount);
            AddStep("start async skin storyboard and dispose before callback", () =>
            {
                config.SetValue(OsuSetting.ShowStoryboard, true);
                Remove(first, disposeImmediately: true);
            });
            AddUntilStep("provisional storyboard participant reclaimed", () =>
                first.Parent == null
                && skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
            AddStep("prepare retry host with storyboard disabled", () =>
            {
                config.SetValue(OsuSetting.ShowStoryboard, false);
                Add(retry = createHost());
            });
            AddUntilStep("wait for retry host", () => retry.IsLoaded);
            AddStep("start retry", () => config.SetValue(OsuSetting.ShowStoryboard, true));
            AddUntilStep("retry owns loaded skin storyboard", () =>
                retry.ChildrenOfType<DrawableStoryboard>().SingleOrDefault()?.IsLoaded == true
                && skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline + 1);
            AddStep("dispose retry host", () => Remove(retry, disposeImmediately: true));
            AddUntilStep("retry participant detached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
        }

        private static DimmableStoryboard createHost() =>
            new DimmableStoryboard(createSkinStoryboard(), Array.Empty<Mod>())
            {
                RelativeSizeAxes = Axes.Both,
            };

        private static Storyboard createSkinStoryboard()
        {
            var storyboard = new Storyboard { UseSkinSprites = true };
            var sprite = new StoryboardSprite("Menu/fountain-star", Anchor.Centre, Vector2.Zero);
            sprite.Commands.AddAlpha(Easing.None, 0, 60_000, 1, 1);
            storyboard.Layers.Last().Add(sprite);
            return storyboard;
        }
    }
}
