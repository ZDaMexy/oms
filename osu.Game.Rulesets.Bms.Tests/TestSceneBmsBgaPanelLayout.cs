// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsBgaPanelLayout : OsuTestScene
    {
        private DefaultBmsBgaPanelDisplay panel = null!;

        [Test]
        public void Test14KMirrorsBgaIntoFourCorners()
        {
            loadPanel(BmsKeymode.Key14K);
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("BGA mirrored into four corners", () => panel.ChildrenOfType<BmsBgaPlayer>().Count(), () => Is.EqualTo(4));
        }

        [Test]
        public void TestSinglePlayUsesOneCorner()
        {
            loadPanel(BmsKeymode.Key7K);
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("single BGA corner", () => panel.ChildrenOfType<BmsBgaPlayer>().Count(), () => Is.EqualTo(1));
        }

        [Test]
        public void TestCustomDisplayReceivesExactImmutableViewportSnapshot()
        {
            TestBgaPanelDisplay customDisplay = null!;
            BmsGameplayLayoutSnapshot expectedSnapshot = null!;

            AddStep("load custom BGA carrier", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key14K } };
                var layoutProvider = new BmsGameplayLayoutProvider(beatmap);
                expectedSnapshot = layoutProvider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());
                customDisplay = new TestBgaPanelDisplay();

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(BmsGameplayLayoutProvider), layoutProvider),
                    },
                    Child = new SkinProvidingContainer(new TestBgaSkin(customDisplay))
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new BmsBgaPanel(
                            new[] { new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "x.png", false) },
                            BmsPoorBgaMode.Default,
                            layoutProvider),
                    },
                };
            });

            AddUntilStep("custom BGA carrier initialised", () => customDisplay?.LayoutSnapshot != null);
            AddAssert("custom BGA retains exact typed snapshot", () => customDisplay.LayoutSnapshot, () => Is.SameAs(expectedSnapshot));
            AddAssert("custom BGA receives every solved viewport", () => customDisplay.LayoutSnapshot!.BgaViewports, () => Is.SameAs(expectedSnapshot.BgaViewports));
            AddAssert("custom BGA initialises snapshot exactly once", () => customDisplay.LayoutInitialisationCount, () => Is.EqualTo(1));
            AddAssert("custom BGA content callbacks observe snapshot first", () => customDisplay.AllCallbacksObservedSnapshot);
        }

        private void loadPanel(BmsKeymode keymode) => AddStep($"load {keymode} BGA panel", () =>
        {
            var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = keymode } };
            beatmap.HitObjects.Add(new BmsHitObject { StartTime = 0, LaneIndex = 1, Keymode = keymode });
            var layoutProvider = new BmsGameplayLayoutProvider(beatmap);
            layoutProvider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());

            var config = (BmsRulesetConfigManager)RulesetConfigs.GetConfigFor(new BmsRuleset())!;

            // This is an explicitly isolated visual preview. Its compatibility-labelled provider is never used by the
            // production gameplay root, which resolves the exact owner/package pair before mounting the BGA surface.
            panel = new DefaultBmsBgaPanelDisplay(layoutProvider);

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(GameplayState), new GameplayState(beatmap, new BmsRuleset())),
                    (typeof(BmsRulesetConfigManager), config),
                },
                Child = panel,
            };

            // A non-empty timeline makes the panel mount a BmsBgaPlayer per corner (one for single play, four for 14K).
            panel.SetBgaSource(new[] { new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "x.png", false) }, BmsPoorBgaMode.Default);
            panel.SetLayout(BmsBgaPlacement.TopRight);
        });

        private sealed class TestBgaSkin : Skin
        {
            private readonly Drawable bgaDisplay;

            public TestBgaSkin(Drawable bgaDisplay)
                : base(new SkinInfo(name: nameof(TestBgaSkin)), null)
            {
                this.bgaDisplay = bgaDisplay;
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is BmsSkinComponentLookup { Component: BmsSkinComponents.BgaPanel } ? bgaDisplay : null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed partial class TestBgaPanelDisplay : CompositeDrawable, IBmsBgaPanelDisplay, IBmsBgaPanelLayoutDisplay
        {
            public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

            public int LayoutInitialisationCount { get; private set; }

            public bool AllCallbacksObservedSnapshot { get; private set; } = true;

            public void SetBgaSource(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode)
            {
                AllCallbacksObservedSnapshot &= LayoutSnapshot != null;
            }

            public void SetLayout(BmsBgaPlacement placement)
            {
                AllCallbacksObservedSnapshot &= LayoutSnapshot != null;
            }

            public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
            {
                LayoutInitialisationCount++;
                LayoutSnapshot = snapshot;
            }

            public void NotifyMiss()
            {
            }
        }
    }
}
