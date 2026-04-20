// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsUserSkinFallbackSemantics : OsuTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Test]
        public void TestUserSkinWithoutBmsComboAllowsLaterSourceFallback()
        {
            RulesetSkinProvidingContainer provider = null!;
            var ruleset = new BmsRuleset();
            var fallbackSkin = new TestBmsSkin(comboCounterComponent: new TestComboCounter());

            AddStep("load provider chain", () =>
            {
                var beatmap = new BmsBeatmap
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(new SkinProvidingContainer(fallbackSkin)
                {
                    Child = new SkinProvidingContainer(new NonBmsUserSkin())
                    {
                        Child = provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                        {
                            Child = new Container(),
                        },
                    },
                });
            });

            AddUntilStep("provider loaded", () => provider.IsLoaded);
            AddAssert("later source combo counter used", () => provider.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)), () => Is.SameAs(fallbackSkin.ComboCounterComponent));
        }

        [Test]
        public void TestUserSkinWithoutBmsHudLayerAllowsLaterSourceFallback()
        {
            RulesetSkinProvidingContainer provider = null!;
            Drawable resolvedHud = null!;
            var ruleset = new BmsRuleset();
            var fallbackSkin = new TestBmsSkin(hudLayoutComponent: new TestHudLayoutDisplay(), comboCounterComponent: new TestComboCounter());

            AddStep("load provider chain", () =>
            {
                var beatmap = new BmsBeatmap
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(new SkinProvidingContainer(fallbackSkin)
                {
                    Child = new SkinProvidingContainer(new NonBmsUserSkin())
                    {
                        Child = provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                        {
                            Child = new Container(),
                        },
                    },
                });
            });

            AddUntilStep("provider loaded", () => provider.IsLoaded);
            AddStep("resolve ruleset HUD", () => resolvedHud = provider.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!);

            AddAssert("later source HUD layout used", () => resolvedHud, () => Is.SameAs(fallbackSkin.HudLayoutComponent));
            AddAssert("later source combo counter kept inside HUD", () => ((Container)resolvedHud).Children.OfType<TestComboCounter>().Any());
        }

        [Test]
        public void TestManiaOnlyLegacyUserSkinFallsBackToOmsBmsHudLayer()
        {
            Drawable host = null!;
            RulesetSkinProvidingContainer provider = null!;
            Drawable resolvedHud = null!;
            ManiaOnlyLegacyUserSkin userSkin = null!;
            var ruleset = new BmsRuleset();

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("create mania-only legacy skin", () => userSkin = new ManiaOnlyLegacyUserSkin(renderer));
            AddAssert("user skin exposes mania key texture", () => userSkin.GetTexture("mania-key1") != null);

            AddStep("load provider chain", () =>
            {
                var beatmap = new BmsBeatmap
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(userSkin)
                {
                    Child = provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = new Container(),
                    },
                });
            });

            AddUntilStep("provider loaded", () => provider.IsLoaded);
            AddAssert("OMS BMS combo counter used", () => provider.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)), () => Is.TypeOf<BmsComboCounter>());
            AddStep("resolve ruleset HUD", () => resolvedHud = provider.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!);
            AddAssert("OMS BMS hud layout used", () => resolvedHud, () => Is.TypeOf<DefaultBmsHudLayoutDisplay>());
            AddAssert("OMS BMS combo kept inside HUD", () => ((Container)resolvedHud).Children.OfType<BmsComboCounter>().Any());
            AddStep("clear provider chain", () => host.Expire());
        }

        [Test]
        public void TestMixedLayerUserSkinUsesItsOwnBmsHudLayer()
        {
            Drawable host = null!;
            RulesetSkinProvidingContainer provider = null!;
            Drawable resolvedHud = null!;
            MixedLayerLegacyUserSkin userSkin = null!;
            var ruleset = new BmsRuleset();

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("create mixed-layer legacy+BMS skin", () => userSkin = new MixedLayerLegacyUserSkin(renderer));
            AddAssert("user skin exposes legacy mania assets", () => userSkin.GetTexture("mania-key1") != null && userSkin.GetTexture("mania-note1") != null);
            AddAssert("user skin exposes BMS combo counter", () => userSkin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)), () => Is.SameAs(userSkin.ComboCounterComponent));

            AddStep("load provider chain", () =>
            {
                var beatmap = new BmsBeatmap
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(userSkin)
                {
                    Child = provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = new Container(),
                    },
                });
            });

            AddUntilStep("provider loaded", () => provider.IsLoaded);
            AddAssert("mixed-layer combo counter used", () => provider.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)), () => Is.SameAs(userSkin.ComboCounterComponent));
            AddAssert("mixed-layer HUD layout used", () => provider.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)), () => Is.SameAs(userSkin.HudLayoutComponent));
            AddStep("resolve ruleset HUD", () => resolvedHud = provider.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!);
            AddAssert("ruleset HUD resolves from mixed-layer skin", () => resolvedHud, () => Is.SameAs(userSkin.HudLayoutComponent));
            AddAssert("mixed-layer combo counter kept inside HUD", () => ((Container)resolvedHud).Children.OfType<TestComboCounter>().Any(drawable => ReferenceEquals(drawable, userSkin.ComboCounterComponent)));
            AddStep("clear provider chain", () => host.Expire());
        }

        private sealed class NonBmsUserSkin : Skin
        {
            private readonly Drawable globalComponent = new Container();

            public NonBmsUserSkin()
                : base(new SkinInfo(name: nameof(NonBmsUserSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is GlobalSkinnableContainerLookup { Ruleset: null, Lookup: GlobalSkinnableContainers.MainHUDComponents } ? globalComponent : null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                => null;
        }

        private sealed class TestBmsSkin : Skin
        {
            public readonly Drawable? HudLayoutComponent;
            public readonly Drawable? ComboCounterComponent;

            public TestBmsSkin(Drawable? hudLayoutComponent = null, Drawable? comboCounterComponent = null)
                : base(new SkinInfo(name: nameof(TestBmsSkin)), null)
            {
                HudLayoutComponent = hudLayoutComponent;
                ComboCounterComponent = comboCounterComponent;
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup switch
                {
                    BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout } => HudLayoutComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter } => ComboCounterComponent,
                    _ => null,
                };

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                => null;
        }

        private sealed class ManiaOnlyLegacyUserSkin : LegacySkin
        {
            private readonly IRenderer renderer;

            public ManiaOnlyLegacyUserSkin(IRenderer renderer)
                : base(new SkinInfo(), null, null, string.Empty)
            {
                this.renderer = renderer;
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName is "mania-key1" or "mania-key1D"
                    ? renderer.WhitePixel
                    : base.GetTexture(componentName, wrapModeS, wrapModeT);
        }

        private sealed class MixedLayerLegacyUserSkin : LegacySkin
        {
            private readonly IRenderer renderer;

            public readonly TestHudLayoutDisplay HudLayoutComponent = new TestHudLayoutDisplay();
            public readonly TestComboCounter ComboCounterComponent = new TestComboCounter();

            public MixedLayerLegacyUserSkin(IRenderer renderer)
                : base(new SkinInfo(name: nameof(MixedLayerLegacyUserSkin)), null, null, string.Empty)
            {
                this.renderer = renderer;
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup switch
                {
                    BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout } => HudLayoutComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter } => ComboCounterComponent,
                    _ => base.GetDrawableComponent(lookup),
                };

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName is "mania-key1" or "mania-key1D" or "mania-note1"
                    ? renderer.WhitePixel
                    : base.GetTexture(componentName, wrapModeS, wrapModeT);
        }

        private sealed partial class TestHudLayoutDisplay : Container, IBmsHudLayoutDisplayWithGameplayFeedback
        {
            public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
                => SetComponents(wrappedHud, gaugeBar, comboCounter, null!);

            public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter, Drawable gameplayFeedback)
            {
                Clear();

                if (wrappedHud != null)
                    Add(wrappedHud);

                Add(gaugeBar);
                Add(comboCounter);

                if (gameplayFeedback != null)
                    Add(gameplayFeedback);
            }
        }

        private sealed partial class TestComboCounter : DefaultComboCounter
        {
        }
    }
}
