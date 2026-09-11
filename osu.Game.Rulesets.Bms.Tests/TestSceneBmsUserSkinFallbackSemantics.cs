// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

        protected override bool UseFreshStoragePerRun => true;

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

            AddAssert("later source HUD layout used", () => ((BmsHudLayoutSnapshotCarrier)resolvedHud).Display, () => Is.SameAs(fallbackSkin.HudLayoutComponent));
            AddAssert("later source combo counter retained by HUD carrier", () => ((BmsHudLayoutSnapshotCarrier)resolvedHud).ComboCounter, () => Is.TypeOf<TestComboCounter>());
        }

        [Test]
        public void TestManiaOnlyLegacyUserSkinFallsBackToOmsBmsHudLayer()
        {
            CanonicalFallbackHudHost host = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            Task<Live<SkinInfo>> import = null!;
            Live<SkinInfo> imported = null!;
            MemoryStream archive = null!;
            var ruleset = new BmsRuleset();

            AddStep("import a real mania-only ordinary package", () =>
            {
                archive = new MemoryStream();
                using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
                {
                    using (var writer = new StreamWriter(zip.CreateEntry("skin.ini").Open(), new UTF8Encoding(false)))
                        writer.Write("[General]\nName: Mania-only fallback fixture\nAuthor: Independent author\nVersion: 2.7\n[Mania]\nKeys: 8\nKeyImage0: mania-key1\n");
                    using Stream imageStream = zip.CreateEntry("mania-key1.png").Open();
                    using var image = new Image<Rgba32>(3, 5, new Rgba32(60, 180, 230, 255));
                    image.SaveAsPng(imageStream);
                }
                archive.Position = 0;
                import = skinManager.Import(new ImportTask(archive, "Mania-only fallback fixture.osk"));
            });
            AddUntilStep("ordinary import completes", () => import.IsCompleted);
            AddStep("select the imported author package", () =>
            {
                imported = import.GetAwaiter().GetResult();
                archive.Dispose();
                skinManager.CurrentSkinInfo.Value = imported;
            });
            AddUntilStep("the requested package is the actual current owner", () =>
                skinManager.CurrentSkinInfo.Value.ID == imported.ID
                && skinManager.CurrentSkin.Value.SkinInfo.ID == imported.ID
                && ReferenceEquals(skinManager.CurrentRevision.Owner, skinManager.CurrentSkin.Value));
            AddStep("mount real BMS gameplay over the mania-only package", () =>
            {
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skinManager.DefaultOmsSkin), Is.True);
                Assert.That(skinManager.CurrentSkin.Value.GetTexture("mania-key1")!.Width, Is.EqualTo(3));
                Assert.That(skinManager.CurrentSkin.Value.GetTexture("mania-key1")!.Height, Is.EqualTo(5));
                var decoded = new BmsBeatmapDecoder().DecodeText(
                    "#TITLE Mania-only skin BMS fallback\n#BPM 120\n#WAV01 note.wav\n#00111:0101\n#00119:0001\n",
                    "mania-only-bms-fallback.bme");
                var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), ruleset).Convert();
                Assert.That(beatmap.HitObjects, Is.Not.Empty);
                Assert.That(beatmap.BmsInfo.Keymode, Is.EqualTo(BmsKeymode.Key7K));
                var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(ruleset)!;
                Add(host = new CanonicalFallbackHudHost(ruleset, beatmap, config));
            });
            AddUntilStep("the actual fallback HUD is ready", () =>
            {
                scene ??= host.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()!;
                return scene?.IsSceneReady == true && host.Drawable.HudMaterialSet != null;
            });
            AddUntilStep("the actual global information text has loaded and acquired visible dimensions", () =>
            {
                var globalText = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, GameplaySkinResolvedMaterialTarget.Global);
                if (!scene.TryGetHostedDrawable(globalText, out Drawable? visual))
                    return false;

                SpriteText? text = visual!.ChildrenOfType<SpriteText>().SingleOrDefault();
                return visual!.IsLoaded && text?.IsLoaded == true && text.DrawWidth > 0 && text.DrawHeight > 0;
            });
            AddStep("canonical resources supply every actual missing HUD role", () =>
            {
                var carrier = host.Drawable.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Single();
                Assert.That(carrier.Display, Is.TypeOf<DefaultBmsHudLayoutDisplay>());
                Assert.That(carrier.GaugeBar, Is.TypeOf<BmsGaugeBar>());
                Assert.That(carrier.ComboCounter, Is.TypeOf<BmsComboCounter>());
                Assert.That(((BmsGaugeBar)carrier.GaugeBar!).GameplaySkinStageFallbackVisuals.Single().Alpha, Is.Zero);
                Assert.That(((BmsComboCounter)carrier.ComboCounter!).GameplaySkinStageFallbackVisuals.Single().Alpha, Is.Zero);
                Assert.That(carrier.ResolvedMaterialSet, Is.SameAs(scene.MaterialSet));
                Assert.That(scene.RuntimeFaults, Is.Empty);
                Assert.That(scene.PreparedScene.Roots, Is.Empty,
                    "A package without a scene must not acquire another author's scene or private component tree.");
                foreach (GameplaySkinSlotDescriptor slot in new[] { GameplaySkinSlotCatalog.ComboDisplay, GameplaySkinSlotCatalog.GaugeVisual, GameplaySkinSlotCatalog.TextHud })
                {
                    GameplaySkinResolvedMaterialEntry[] entries = scene.MaterialSet.Entries.Where(entry => ReferenceEquals(entry.Slot, slot)).ToArray();
                    Assert.That(entries, Has.Length.EqualTo(ReferenceEquals(slot, GameplaySkinSlotCatalog.TextHud) ? 2 : 1), slot.Id);
                    foreach (GameplaySkinResolvedMaterialEntry entry in entries)
                    {
                        Assert.That(entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.CanonicalPackage), slot.Id);
                        Assert.That(scene.TryGetVisualGate(entry.Key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                        Assert.That(gate!.IsReplacementReady, Is.True, slot.Id);
                        Assert.That(gate.SuppressesProgrammaticVisual, Is.True);
                        bool suppressedStageText = ReferenceEquals(slot, GameplaySkinSlotCatalog.TextHud)
                                                   && entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Stage;
                        if (suppressedStageText)
                        {
                            Assert.That(entry.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                            Assert.That(gate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                            Assert.That(scene.TryGetHostedDrawable(entry.Key, out _), Is.False,
                                "The canonical package explicitly omits duplicate narrow stage text.");
                            continue;
                        }

                        Assert.That(entry.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                        Assert.That(gate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                        Assert.That(entry.Material, Is.TypeOf<GameplaySkinPublicSlotMaterial>());
                        var material = (GameplaySkinPublicSlotMaterial)entry.Material;
                        Assert.That(material.IsProgrammaticFallback, Is.False);
                        Assert.That(material.ResourceName, Is.EqualTo(ReferenceEquals(slot, GameplaySkinSlotCatalog.GaugeVisual) ? "bms/gauge" : "bms/hud"));
                        Assert.That(material.Texture, Is.Not.Null);
                        Assert.That(scene.TryGetHostedDrawable(entry.Key, out Drawable? visual), Is.True);
                        Assert.That(visual!.IsLoaded, Is.True);
                        Assert.That(visual.Parent, Is.SameAs(scene.Layers.Get(gate.Layer)));
                        Assert.That(visual.Parent!.Alpha, Is.GreaterThan(0));
                        Assert.That(visual.Alpha, Is.GreaterThan(0));
                        Assert.That(visual.Position, Is.EqualTo(new Vector2(gate.PreparedRect.X, gate.PreparedRect.Y)));
                        Assert.That(visual.Size, Is.EqualTo(new Vector2(gate.PreparedRect.Width, gate.PreparedRect.Height)));
                        Assert.That(visual.ScreenSpaceDrawQuad.AABBFloat.Width, Is.GreaterThan(0));
                        Assert.That(visual.ScreenSpaceDrawQuad.AABBFloat.Height, Is.GreaterThan(0));
                        Sprite sprite = visual.ChildrenOfType<Sprite>().Single();
                        Assert.That(sprite.Texture, Is.SameAs(material.Texture));
                        Assert.That(sprite.Alpha, Is.GreaterThan(0));
                        Assert.That(sprite.DrawWidth, Is.GreaterThan(0));
                        Assert.That(sprite.DrawHeight, Is.GreaterThan(0));

                        if (ReferenceEquals(slot, GameplaySkinSlotCatalog.GaugeVisual))
                            continue;

                        SpriteText text = visual.ChildrenOfType<SpriteText>().Single();
                        Assert.That(text.IsLoaded, Is.True);
                        Assert.That(text.Alpha, Is.GreaterThan(0));
                        Assert.That(text.DrawWidth, Is.GreaterThan(0));
                        Assert.That(text.DrawHeight, Is.GreaterThan(0));
                        Assert.That(text.Text.ToString(), Is.EqualTo(ReferenceEquals(slot, GameplaySkinSlotCatalog.TextHud)
                            ? "0 | 100.00% | 0x | 0%" : "0"));
                        if (ReferenceEquals(slot, GameplaySkinSlotCatalog.TextHud))
                        {
                            Assert.That(entry.Target.Kind, Is.EqualTo(GameplaySkinResolvedMaterialTargetKind.Global));
                            Assert.That(text.DrawWidth * text.Scale.X, Is.LessThanOrEqualTo(visual.DrawWidth + 0.01));
                            Assert.That(text.DrawHeight * text.Scale.Y, Is.LessThanOrEqualTo(visual.DrawHeight + 0.01));
                        }
                    }
                }
            });
            AddStep("detach gameplay before restoring the default", () => Remove(host, disposeImmediately: true));
            AddStep("restore the verified default", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddUntilStep("default publication completes", () => ReferenceEquals(skinManager.CurrentSkin.Value, skinManager.DefaultOmsSkin));
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
            AddAssert("ruleset HUD resolves from mixed-layer skin", () => ((BmsHudLayoutSnapshotCarrier)resolvedHud).Display, () => Is.SameAs(userSkin.HudLayoutComponent));
            AddAssert("mixed-layer combo retained by HUD carrier", () => ((BmsHudLayoutSnapshotCarrier)resolvedHud).ComboCounter, () => Is.SameAs(userSkin.ComboCounterComponent));
            AddStep("clear provider chain", () => host.Expire());
        }

        private sealed partial class CanonicalFallbackHudHost : CompositeDrawable
        {
            internal DrawableBmsRuleset Drawable { get; }

            internal CanonicalFallbackHudHost(BmsRuleset ruleset, BmsBeatmap beatmap, BmsRulesetConfigManager config)
            {
                RelativeSizeAxes = Axes.Both;
                HealthProcessor health = ruleset.CreateHealthProcessor(0);
                health.ApplyBeatmap(beatmap);
                ScoreProcessor score = ruleset.CreateScoreProcessor();
                score.ApplyBeatmap(beatmap);
                var gameplayState = new GameplayState(beatmap, ruleset, scoreProcessor: score, healthProcessor: health);
                var clock = new FramedClock(new ManualClock { CurrentTime = 1_500, IsRunning = false });
                clock.ProcessFrame();
                Drawable = (DrawableBmsRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Drawable.Clock = clock;
                InternalChild = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(BmsRulesetConfigManager), config) },
                    Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null, prepareGameplaySkinLayout: true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new DependencyProvidingContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            CachedDependencies = new (Type, object)[]
                            {
                                (typeof(GameplayState), gameplayState),
                                (typeof(HealthProcessor), health),
                                (typeof(ScoreProcessor), score),
                            },
                            Child = Drawable,
                        },
                    },
                };
            }
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

        private sealed partial class TestHudLayoutDisplay : Container, IBmsHudLayoutDisplay
        {
            public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

            public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
            {
                Clear();

                if (wrappedHud != null)
                    Add(wrappedHud);

                Add(gaugeBar);
                Add(comboCounter);
            }

            public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
                => LayoutSnapshot = snapshot;
        }

        private sealed partial class TestComboCounter : DefaultComboCounter
        {
        }
    }
}
