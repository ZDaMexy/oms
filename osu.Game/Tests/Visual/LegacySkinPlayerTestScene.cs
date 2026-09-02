// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual
{
    [TestFixture]
    public abstract partial class LegacySkinPlayerTestScene : PlayerTestScene
    {
        protected LegacySkin LegacySkin { get; private set; }

        private ISkinSource legacySkinSource;

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new SkinProvidingPlayer(legacySkinSource);

        [BackgroundDependencyLoader]
        private void load(SkinManager skins)
        {
            LegacySkin = new DefaultLegacySkin(skins);
            // Keep the deliberately injected legacy layer first, while retaining the SkinManager's exact selected
            // owner behind it. Production gameplay publication is bound to that current owner; omitting it would
            // create an impossible source vector which no real RulesetSkinProvidingContainer can publish.
            legacySkinSource = new FixedSkinSource(LegacySkin, skins.CurrentSkin.Value);
        }

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();
            addResetTargetsStep();
        }

        [TearDownSteps]
        public override void TearDownSteps()
        {
            addResetTargetsStep();
            base.TearDownSteps();
        }

        private void addResetTargetsStep()
        {
            AddStep("reset targets", () => this.ChildrenOfType<SkinnableContainer>().ForEach(t =>
            {
                LegacySkin.ResetDrawableTarget(t);
                t.Reload();
            }));

            AddUntilStep("wait for components to load", () => this.ChildrenOfType<SkinnableContainer>().All(t => t.ComponentsLoaded));
        }

        public partial class SkinProvidingPlayer : TestPlayer
        {
            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            public SkinProvidingPlayer(ISkinSource skinSource)
            {
                this.skinSource = skinSource;
            }
        }

        private sealed partial class FixedSkinSource : SkinProvidingContainer
        {
            public FixedSkinSource(params ISkin[] sources)
            {
                SetSources(sources);
            }
        }
    }
}
