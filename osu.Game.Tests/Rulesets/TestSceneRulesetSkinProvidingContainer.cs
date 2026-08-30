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
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
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

        [Test]
        public void TestGameplayLayoutOwnerUsesExactProviderPackageRevision()
        {
            setupProviderStep();

            AddAssert("exact package token cached once", () =>
                requester.PackageRevision != null
                && requester.LayoutOwner != null
                && ReferenceEquals(requester.PackageRevision, requester.LayoutOwner.PackageRevision)
                && requester.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility);
        }

        [Test]
        public void TestRejectedGameplayLayoutDoesNotAttachDelayedRenderer()
        {
            RejectingLayoutRuleset ruleset = null;
            LoadProbe delayedRenderer = null;

            AddStep("mount rejecting gameplay layout provider", () =>
            {
                ruleset = new RejectingLayoutRuleset();
                Child = new RulesetSkinProvidingContainer(
                    ruleset,
                    Beatmap.Value.Beatmap,
                    Beatmap.Value.Skin,
                    prepareGameplaySkinLayout: true)
                {
                    Child = delayedRenderer = new LoadProbe(),
                };
            });
            AddWaitStep("allow provider background load", 5);
            AddAssert("preparer called once", () => ruleset.PrepareCount == 1);
            AddAssert("delayed renderer never loaded", () => !delayedRenderer.IsLoaded);
        }

        [Test]
        public void TestTransientGameplayLayoutAdmissionRetriesBeforeAttachingRenderer()
        {
            RetryingLayoutRuleset ruleset = null;
            LoadProbe delayedRenderer = null;

            AddStep("mount transient gameplay layout provider", () =>
            {
                ruleset = new RetryingLayoutRuleset();
                Child = new RulesetSkinProvidingContainer(
                    ruleset,
                    Beatmap.Value.Beatmap,
                    Beatmap.Value.Skin,
                    prepareGameplaySkinLayout: true)
                {
                    Child = delayedRenderer = new LoadProbe(),
                };
            });
            AddUntilStep("renderer attached after fresh barrier", () => delayedRenderer.IsLoaded);
            AddAssert("each retry was a full preparer call", () => ruleset.PrepareCount == 3);
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

            public GameplaySkinPackageRevision PackageRevision { get; private set; }

            public GameplaySkinLayoutRevisionOwner LayoutOwner { get; private set; }

            public event Action OnLoadAsync;

            [BackgroundDependencyLoader]
            private void load(
                ISkinSource skin,
                GameplaySkinPackageRevision packageRevision,
                GameplaySkinLayoutRevisionOwner layoutOwner)
            {
                this.skin = skin;
                PackageRevision = packageRevision;
                LayoutOwner = layoutOwner;

                OnLoadAsync?.Invoke();
            }

            public Drawable GetDrawableComponent(ISkinComponentLookup lookup) => skin.GetDrawableComponent(lookup);

            public Texture GetTexture(string componentName, WrapMode wrapModeS = default, WrapMode wrapModeT = default) => skin.GetTexture(componentName);

            public ISample GetSample(ISampleInfo sampleInfo) => skin.GetSample(sampleInfo);

            public IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => skin.GetConfig<TLookup, TValue>(lookup);

            public IEnumerable<ISkin> AllSources => skin.AllSources;
        }

        private sealed class RejectingLayoutRuleset : TestSceneRulesetDependencies.TestRuleset, IGameplaySkinLayoutPreparer
        {
            public int PrepareCount { get; private set; }

            public GameplaySkinLayoutPreparationResult PrepareGameplaySkinLayout(IBeatmap beatmap, IReadOnlyDependencyContainer dependencies)
            {
                PrepareCount++;
                return GameplaySkinLayoutPreparationResult.Rejected;
            }
        }

        private sealed class RetryingLayoutRuleset : TestSceneRulesetDependencies.TestRuleset, IGameplaySkinLayoutPreparer
        {
            public int PrepareCount { get; private set; }

            public GameplaySkinLayoutPreparationResult PrepareGameplaySkinLayout(IBeatmap beatmap, IReadOnlyDependencyContainer dependencies)
                => ++PrepareCount < 3
                    ? GameplaySkinLayoutPreparationResult.Retry
                    : GameplaySkinLayoutPreparationResult.Prepared;
        }

        private sealed partial class LoadProbe : Drawable
        {
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
