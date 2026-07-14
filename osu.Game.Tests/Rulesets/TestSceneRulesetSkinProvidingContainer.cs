// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Tests.Testing;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Rulesets
{
    [HeadlessTest]
    public partial class TestSceneRulesetSkinProvidingContainer : OsuTestScene
    {
        private SkinRequester requester;

        protected override Ruleset CreateRuleset() => new TestSceneRulesetDependencies.TestRuleset();

        [Test]
        public void TestRulesetResources()
        {
            setupProviderStep();

            AddAssert("ruleset texture retrieved via skin", () => requester.GetTexture("test-image") != null);
            AddAssert("ruleset sample retrieved via skin", () => requester.GetSample(new SampleInfo("test-sample")) != null);
        }

        [Test]
        public void TestEarlyAddedSkinRequester()
        {
            Texture textureOnLoad = null;

            AddStep("setup provider", () =>
            {
                requester = new SkinRequester();
                requester.OnLoadAsync += () => textureOnLoad = requester.GetTexture("test-image");

                Child = new RulesetSkinProvidingContainer(CreateRuleset(), Beatmap.Value.Beatmap, Beatmap.Value.Skin)
                {
                    Child = requester
                };
            });

            AddAssert("requester got correct initial texture", () => textureOnLoad != null);
        }

        [Test]
        public void TestGameplayProviderAuthorityOrder()
        {
            NamedSkin beatmapSkin = null;
            NamedSkin selectedSkin = null;
            NamedSkin builtInSkin = null;

            AddStep("setup isolated provider chain", () =>
            {
                beatmapSkin = new NamedSkin("beatmap-local", false);
                selectedSkin = new NamedSkin("selected", false);
                builtInSkin = new NamedSkin("built-in", true);

                Child = new IsolatedSkinProvidingContainer(new[] { selectedSkin, builtInSkin })
                    .WithChild(new RulesetSkinProvidingContainer(CreateRuleset(), Beatmap.Value.Beatmap, beatmapSkin)
                        .WithChild(requester = new SkinRequester()));
            });

            AddAssert("authority order preserved", () =>
            {
                ISkin[] sources = requester.AllSources.ToArray();

                return sources.Length == 4
                       && sources[0] == beatmapSkin
                       && sources[1] == selectedSkin
                       && sources[2] is ResourceStoreBackedSkin
                       && sources[3] == builtInSkin;
            });
        }

        private void setupProviderStep()
        {
            AddStep("setup provider", () =>
            {
                Child = new RulesetSkinProvidingContainer(CreateRuleset(), Beatmap.Value.Beatmap, Beatmap.Value.Skin)
                    .WithChild(requester = new SkinRequester());
            });
        }

        private partial class SkinRequester : Drawable, ISkin
        {
            private ISkinSource skin;

            public event Action OnLoadAsync;

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                this.skin = skin;

                OnLoadAsync?.Invoke();
            }

            public Drawable GetDrawableComponent(ISkinComponentLookup lookup) => skin.GetDrawableComponent(lookup);

            public Texture GetTexture(string componentName, WrapMode wrapModeS = default, WrapMode wrapModeT = default) => skin.GetTexture(componentName);

            public ISample GetSample(ISampleInfo sampleInfo) => skin.GetSample(sampleInfo);

            public IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => skin.GetConfig<TLookup, TValue>(lookup);

            public IEnumerable<ISkin> AllSources => skin.AllSources;
        }

        private partial class IsolatedSkinProvidingContainer : SkinProvidingContainer
        {
            private readonly IEnumerable<ISkin> sources;

            protected override bool AllowFallingBackToParent => false;

            public IsolatedSkinProvidingContainer(IEnumerable<ISkin> sources)
            {
                this.sources = sources;
            }

            protected override void RefreshSources() => SetSources(sources);
        }

        private class NamedSkin : Skin
        {
            public NamedSkin(string name, bool isProtected)
                : base(new SkinInfo(name) { Protected = isProtected }, null, null, string.Empty)
            {
            }

            public override Drawable GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public override Texture GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }
    }
}
