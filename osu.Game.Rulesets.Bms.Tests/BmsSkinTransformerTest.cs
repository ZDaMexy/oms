// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using osu.Framework.Audio;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.SongSelect;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsSkinTransformerTest
    {
        [Test]
        public void TestCreatesBmsSkinTransformer()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer, Is.TypeOf<BmsSkinTransformer>());
        }

        [Test]
        public void TestOmsSkinUsesSharedTransformerShell()
        {
            var ruleset = new BmsRuleset();
            var transformer = ruleset.CreateSkinTransformer(new OmsSkin(new TestStorageResourceProvider()), new BmsBeatmap());

            Assert.Multiple(() =>
            {
                Assert.That(transformer, Is.TypeOf<OmsSkinTransformer>());
                Assert.That(((ISkinTransformer)transformer!).Skin, Is.TypeOf<BmsSkinTransformer>());
                Assert.That(transformer.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents)), Is.TypeOf<DefaultSkinComponentsContainer>());
                Assert.That(transformer.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect)), Is.TypeOf<DefaultSkinComponentsContainer>());
                Assert.That(transformer.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Playfield, ruleset.RulesetInfo)), Is.TypeOf<DefaultSkinComponentsContainer>());
                Assert.That(transformer.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo)), Is.AssignableTo<IBmsHudLayoutDisplay>());
            });
        }

        [Test]
        public void TestRulesetHudIncludesGaugeBarAndComboCounter()
        {
            var ruleset = new BmsRuleset();
            var transformer = ruleset.CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo));
            var skinnableChildren = ((Container)drawable!).Children.OfType<Drawable>().ToArray();
            var gaugeBars = skinnableChildren.OfType<BmsGaugeBar>().ToArray();
            var comboCounters = skinnableChildren.OfType<ComboCounter>().ToArray();
            var speedFeedbackDisplays = skinnableChildren.Where(child => child is IBmsSpeedFeedbackDisplay).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsHudLayoutDisplay>());
                Assert.That(skinnableChildren.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(gaugeBars, Has.Length.EqualTo(1));
                Assert.That(comboCounters, Has.Length.EqualTo(1));
                Assert.That(speedFeedbackDisplays, Has.Length.EqualTo(1));
                Assert.That(comboCounters.Single(), Is.TypeOf<BmsComboCounter>());
            });
        }

        [Test]
        public void TestRulesetHudPreservesWrappedSkinContent()
        {
            var ruleset = new BmsRuleset();
            var skin = new TestSkin(rulesetHudComponent: new Container());
            var transformer = ruleset.CreateSkinTransformer(skin, new BmsBeatmap());
            var drawable = (Container)transformer!.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!;
            var gaugeBars = drawable.Children.OfType<BmsGaugeBar>().ToArray();
            var comboCounters = drawable.Children.OfType<ComboCounter>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.Children, Has.Some.SameAs(skin.RulesetHudComponent));
                Assert.That(gaugeBars, Has.Length.EqualTo(1));
                Assert.That(comboCounters, Has.Length.EqualTo(1));
                Assert.That(comboCounters.Single(), Is.TypeOf<BmsComboCounter>());
            });
        }

        [Test]
        public void TestGlobalHudFallsBackToWrappedSkin()
        {
            var ruleset = new BmsRuleset();
            var skin = new TestSkin();
            var transformer = ruleset.CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents)), Is.SameAs(skin.GlobalComponent));
        }

        [Test]
        public void TestHudLayoutFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)), Is.AssignableTo<IBmsHudLayoutDisplay>());
        }

        [Test]
        public void TestUserSkinWithoutBmsHudLayerReturnsNullToAllowLaterFallback()
        {
            var ruleset = new BmsRuleset();
            var transformer = ruleset.CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo)), Is.Null);
        }

        [Test]
        public void TestUserSkinWithoutHudLayoutReturnsNullToAllowLaterFallback()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)), Is.Null);
        }

        [Test]
        public void TestCustomHudLayoutFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(hudLayoutComponent: new TestHudLayoutDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)), Is.SameAs(skin.HudLayoutComponent));
        }

        [Test]
        public void TestRulesetHudUsesCustomHudLayout()
        {
            var ruleset = new BmsRuleset();
            var skin = new TestSkin(hudLayoutComponent: new TestHudLayoutDisplay());
            var transformer = ruleset.CreateSkinTransformer(skin, new BmsBeatmap());
            var drawable = (Container)transformer!.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!;
            var gaugeBars = drawable.Children.OfType<BmsGaugeBar>().ToArray();
            var comboCounters = drawable.Children.OfType<ComboCounter>().ToArray();
            var speedFeedbackDisplays = drawable.Children.OfType<Drawable>().Where(child => child is IBmsSpeedFeedbackDisplay).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.SameAs(skin.HudLayoutComponent));
                Assert.That(gaugeBars, Has.Length.EqualTo(1));
                Assert.That(comboCounters, Has.Length.EqualTo(1));
                Assert.That(speedFeedbackDisplays, Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestSpeedFeedbackFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.SpeedFeedback)), Is.AssignableTo<IBmsSpeedFeedbackDisplay>());
        }

        [Test]
        public void TestUserSkinWithoutSpeedFeedbackReturnsNullToAllowLaterFallback()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.SpeedFeedback)), Is.Null);
        }

        [Test]
        public void TestCustomSpeedFeedbackFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(speedFeedbackComponent: new TestSpeedFeedbackDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.SpeedFeedback)), Is.SameAs(skin.SpeedFeedbackComponent));
        }

        [Test]
        public void TestLegacyHudLayoutStillGetsGameplayFeedbackOverlay()
        {
            var ruleset = new BmsRuleset();
            var skin = new TestSkin(hudLayoutComponent: new LegacyTestHudLayoutDisplay(), speedFeedbackComponent: new TestSpeedFeedbackDisplay());
            var transformer = ruleset.CreateSkinTransformer(skin, new BmsBeatmap());
            var drawable = (Container)transformer!.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!;

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.Not.SameAs(skin.HudLayoutComponent));
                Assert.That(drawable.Children, Has.Some.SameAs(skin.HudLayoutComponent));
                Assert.That(drawable.Children.OfType<Drawable>().Any(child => child is IBmsSpeedFeedbackDisplay), Is.True);
            });
        }

        [TestCase(HitResult.Meh)]
        [TestCase(HitResult.Miss)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.ComboBreak)]
        public void TestRulesetResolvesCustomBmsJudgementDirectly(HitResult result)
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new SkinComponentLookup<HitResult>(result)), Is.TypeOf<BmsJudgementPiece>());
        }

        [TestCase(HitResult.Meh)]
        [TestCase(HitResult.Miss)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.ComboBreak)]
        public void TestBmsJudgementFallsBackToDefaultDisplay(HitResult result)
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsJudgementSkinLookup(result)), Is.TypeOf<BmsJudgementPiece>());
        }

        [TestCase(HitResult.Meh)]
        [TestCase(HitResult.Miss)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.ComboBreak)]
        public void TestUserSkinWithoutBmsJudgementReturnsNullToAllowLaterFallback(HitResult result)
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsJudgementSkinLookup(result)), Is.Null);
        }

        [TestCase(HitResult.Meh)]
        [TestCase(HitResult.Miss)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.ComboBreak)]
        public void TestCustomBmsJudgementFallsBackToWrappedSkin(HitResult result)
        {
            var skin = new TestSkin(judgementComponent: new TestJudgementDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsJudgementSkinLookup(result)), Is.SameAs(skin.JudgementComponent));
        }

        [TestCase(HitResult.Meh)]
        [TestCase(HitResult.Miss)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.ComboBreak)]
        public void TestRulesetBoundaryRedirectUsesCustomSkinJudgement(HitResult result)
        {
            var skin = new TestSkin(judgementComponent: new TestJudgementDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new SkinComponentLookup<HitResult>(result)), Is.SameAs(skin.JudgementComponent));
        }

        [Test]
        public void TestNonCustomBmsJudgementFallsBackToWrappedSkin()
        {
            var skin = new TestSkin();
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsJudgementSkinLookup(HitResult.Great)), Is.SameAs(skin.GreatJudgementComponent));
        }

        [Test]
        public void TestNonCustomJudgementFallsBackToWrappedSkin()
        {
            var skin = new TestSkin();
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new SkinComponentLookup<HitResult>(HitResult.Great)), Is.SameAs(skin.GreatJudgementComponent));
        }

        [Test]
        public void TestNoteDistributionFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.NoteDistribution)), Is.TypeOf<DefaultBmsNoteDistributionDisplay>());
        }

        [Test]
        public void TestNoteDistributionPanelFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.NoteDistributionPanel)), Is.TypeOf<DefaultBmsNoteDistributionPanelDisplay>());
        }

        [Test]
        public void TestPlayfieldBackdropFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Backdrop, BmsKeymode.Key7K, 8)), Is.TypeOf<DefaultBmsPlayfieldBackdropDisplay>());
        }

        [Test]
        public void TestPlayfieldBaseplateFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Baseplate, BmsKeymode.Key7K, 8)), Is.TypeOf<DefaultBmsPlayfieldBaseplateDisplay>());
        }

        [Test]
        public void TestCustomPlayfieldBackdropFallsBackToWrappedSkin()
        {
            var backdropComponent = new Container();
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(playfieldBackdropComponent: backdropComponent), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Backdrop, BmsKeymode.Key7K, 8)), Is.SameAs(backdropComponent));
        }

        [Test]
        public void TestCustomPlayfieldBaseplateFallsBackToWrappedSkin()
        {
            var baseplateComponent = new Container();
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(playfieldBaseplateComponent: baseplateComponent), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements.Baseplate, BmsKeymode.Key7K, 8)), Is.SameAs(baseplateComponent));
        }

        [Test]
        public void TestLaneBackgroundFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            var drawable = transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.Background, 0, 8, false, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLaneBackgroundDisplay>());
                Assert.That(((DefaultBmsLaneBackgroundDisplay)drawable!).LaneIndex, Is.EqualTo(0));
                Assert.That(((DefaultBmsLaneBackgroundDisplay)drawable).IsScratch, Is.False);
            });
        }

        [Test]
        public void TestScratchLaneBackgroundFallsBackToScratchDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.Background, 0, 8, true, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLaneBackgroundDisplay>());
                Assert.That(((DefaultBmsLaneBackgroundDisplay)drawable!).IsScratch, Is.True);
            });
        }

        [Test]
        public void TestLaneDividerFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.Divider, 0, 8, false, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLaneDividerDisplay>());
                Assert.That(((DefaultBmsLaneDividerDisplay)drawable!).IsScratch, Is.False);
            });
        }

        [Test]
        public void TestScratchLaneDividerFallsBackToScratchDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.Divider, 0, 8, true, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLaneDividerDisplay>());
                Assert.That(((DefaultBmsLaneDividerDisplay)drawable!).IsScratch, Is.True);
            });
        }

        [Test]
        public void TestNoteFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.Note, 1, false, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                Assert.That(((DefaultBmsNoteDisplay)drawable!).IsScratch, Is.False);
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.WhiteKeyNote);
            });
        }

        [Test]
        public void TestScratchNoteFallsBackToScratchDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.Note, 0, true, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                Assert.That(((DefaultBmsNoteDisplay)drawable!).IsScratch, Is.True);
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.ScratchNote);
            });
        }

        [Test]
        public void TestLongNoteHeadFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, 2, false, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLongNoteHeadDisplay>());
                Assert.That(((DefaultBmsLongNoteHeadDisplay)drawable!).IsScratch, Is.False);
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.CyanKeyNote);
            });
        }

        [Test]
        public void TestScratchLongNoteHeadFallsBackToScratchDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteHead, 0, true, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLongNoteHeadDisplay>());
                Assert.That(((DefaultBmsLongNoteHeadDisplay)drawable!).IsScratch, Is.True);
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.ScratchNote);
            });
        }

        [Test]
        public void TestLongNoteBodyFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, 4, false, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLongNoteBodyDisplay>());
                Assert.That(((DefaultBmsLongNoteBodyDisplay)drawable!).IsScratch, Is.False);
                Assert.That(drawable!.Width, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(drawable.Alpha, Is.EqualTo(0.8f).Within(0.0001f));
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.YellowKeyLongNoteBody);
            });
        }

        [Test]
        public void TestScratchLongNoteBodyFallsBackToScratchDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, 0, true, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLongNoteBodyDisplay>());
                Assert.That(((DefaultBmsLongNoteBodyDisplay)drawable!).IsScratch, Is.True);
                Assert.That(drawable!.Width, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(drawable.Alpha, Is.EqualTo(0.8f).Within(0.0001f));
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.ScratchLongNoteBody);
            });
        }

        [Test]
        public void TestLongNoteTailFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, 6, false, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLongNoteTailDisplay>());
                Assert.That(((DefaultBmsLongNoteTailDisplay)drawable!).IsScratch, Is.False);
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.CyanKeyNote);
            });
        }

        [Test]
        public void TestScratchLongNoteTailFallsBackToScratchDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteTail, 0, true, BmsKeymode.Key7K));

            Assert.Multiple(() =>
            {
                Assert.That(drawable, Is.TypeOf<DefaultBmsLongNoteTailDisplay>());
                Assert.That(((DefaultBmsLongNoteTailDisplay)drawable!).IsScratch, Is.True);
                assertSingleColour((Drawable)drawable!, BmsDefaultPlayfieldPalette.ScratchNote);
            });
        }

        [TestCase(1, 243, 243, 243)]
        [TestCase(2, 53, 234, 255)]
        [TestCase(4, 255, 222, 53)]
        [TestCase(6, 53, 234, 255)]
        [TestCase(7, 243, 243, 243)]
        public void TestSevenKeyFallbackUsesRequestedPerKeyColours(int laneIndex, byte red, byte green, byte blue)
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex, false, BmsKeymode.Key7K));

            Assert.That(drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
            assertSingleColour((Drawable)drawable!, new Color4(red, green, blue, 255));
        }

        [TestCase(BmsKeymode.Key5K, 1, false, 53, 234, 255)]
        [TestCase(BmsKeymode.Key5K, 2, false, 243, 243, 243)]
        [TestCase(BmsKeymode.Key9K_Bms, 0, false, 53, 234, 255)]
        [TestCase(BmsKeymode.Key9K_Bms, 1, false, 243, 243, 243)]
        [TestCase(BmsKeymode.Key14K, 1, false, 243, 243, 243)]
        [TestCase(BmsKeymode.Key14K, 2, false, 53, 234, 255)]
        [TestCase(BmsKeymode.Key14K, 7, false, 243, 243, 243)]
        [TestCase(BmsKeymode.Key14K, 8, false, 243, 243, 243)]
        [TestCase(BmsKeymode.Key14K, 9, false, 53, 234, 255)]
        [TestCase(BmsKeymode.Key14K, 10, false, 243, 243, 243)]
        [TestCase(BmsKeymode.Key14K, 14, false, 243, 243, 243)]
        [TestCase(BmsKeymode.Key14K, 15, true, 252, 0, 20)]
        public void TestNonSevenKeyFallbackUsesAlternatingOddEvenColours(BmsKeymode keymode, int laneIndex, bool isScratch, byte red, byte green, byte blue)
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());
            var drawable = transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex, isScratch, keymode));

            Assert.That(drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
            assertSingleColour((Drawable)drawable!, new Color4(red, green, blue, 255));
        }

        [Test]
        public void TestCustomLaneBackgroundFallsBackToWrappedSkin()
        {
            var laneBackgroundComponent = new Container();
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(laneBackgroundComponent: laneBackgroundComponent), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.Background, 0, 8, false, BmsKeymode.Key7K)), Is.SameAs(laneBackgroundComponent));
        }

        [Test]
        public void TestCustomLaneDividerFallsBackToWrappedSkin()
        {
            var laneDividerComponent = new Container();
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(laneDividerComponent: laneDividerComponent), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.Divider, 0, 8, false, BmsKeymode.Key7K)), Is.SameAs(laneDividerComponent));
        }

        [Test]
        public void TestCustomNoteFallsBackToWrappedSkin()
        {
            var noteComponent = new Container();
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(noteComponent: noteComponent), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.Note, 0, false)), Is.SameAs(noteComponent));
        }

        [Test]
        public void TestCustomLongNoteBodyFallsBackToWrappedSkin()
        {
            var noteComponent = new Container();
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(noteComponent: noteComponent), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.LongNoteBody, 0, false)), Is.SameAs(noteComponent));
        }

        [Test]
        public void TestUserSkinWithoutNoteOverrideReturnsNullToAllowLaterFallback()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsNoteSkinLookup(BmsNoteSkinElements.Note, 0, false)), Is.Null);
        }

        [Test]
        public void TestHitTargetFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.HitTarget, 0, 8, false, BmsKeymode.Key7K)), Is.TypeOf<DefaultBmsHitTargetDisplay>());
        }

        [Test]
        public void TestBarLineFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsLaneSkinLookup(BmsLaneSkinElements.BarLine, 0, 8, false, BmsKeymode.Key7K, true)), Is.TypeOf<Box>());
        }

        [Test]
        public void TestGaugeBarFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)), Is.TypeOf<BmsGaugeBar>());
        }

        [Test]
        public void TestUserSkinWithoutGaugeBarReturnsNullToAllowLaterFallback()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)), Is.Null);
        }

        [Test]
        public void TestCustomGaugeBarFallsBackToWrappedSkin()
        {
            var gaugeBarComponent = new Container();
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(gaugeBarComponent: gaugeBarComponent), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)), Is.SameAs(gaugeBarComponent));
        }

        [Test]
        public void TestComboCounterFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)), Is.TypeOf<BmsComboCounter>());
        }

        [Test]
        public void TestUserSkinWithoutComboCounterReturnsNullToAllowLaterFallback()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(new TestSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)), Is.Null);
        }

        [Test]
        public void TestCustomComboCounterFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(comboCounterComponent: new TestComboCounter());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)), Is.SameAs(skin.ComboCounterComponent));
        }

        [Test]
        public void TestClearLampFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ClearLamp)), Is.TypeOf<DefaultBmsClearLampDisplay>());
        }

        [Test]
        public void TestCustomClearLampFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(clearLampComponent: new TestClearLampDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ClearLamp)), Is.SameAs(skin.ClearLampComponent));
        }

        [Test]
        public void TestGaugeHistoryFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeHistory)), Is.TypeOf<DefaultBmsGaugeHistoryDisplay>());
        }

        [Test]
        public void TestGaugeHistoryPanelFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeHistoryPanel)), Is.TypeOf<DefaultBmsGaugeHistoryPanelDisplay>());
        }

        [Test]
        public void TestResultsSummaryFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ResultsSummary)), Is.TypeOf<DefaultBmsResultsSummaryDisplay>());
        }

        [Test]
        public void TestResultsSummaryPanelFallsBackToBmsDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ResultsSummaryPanel)), Is.TypeOf<DefaultBmsResultsSummaryPanelDisplay>());
        }

        [Test]
        public void TestCustomResultsSummaryFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(resultsSummaryComponent: new TestResultsSummaryDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ResultsSummary)), Is.SameAs(skin.ResultsSummaryComponent));
        }

        [Test]
        public void TestCustomResultsSummaryPanelFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(resultsSummaryPanelComponent: new TestResultsSummaryPanelDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ResultsSummaryPanel)), Is.SameAs(skin.ResultsSummaryPanelComponent));
        }

        [Test]
        public void TestCustomNoteDistributionPanelFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(noteDistributionPanelComponent: new TestNoteDistributionPanelDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.NoteDistributionPanel)), Is.SameAs(skin.NoteDistributionPanelComponent));
        }

        [Test]
        public void TestStaticBackgroundFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.StaticBackgroundLayer)), Is.TypeOf<DefaultBmsBackgroundLayerDisplay>());
        }

        [Test]
        public void TestLaneCoverFallsBackToDefaultDisplay()
        {
            var transformer = new BmsRuleset().CreateSkinTransformer(createOmsSkin(), new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsLaneCoverSkinLookup(BmsLaneCoverPosition.Sudden)), Is.TypeOf<DefaultBmsLaneCoverDisplay>());
        }

        [Test]
        public void TestCustomLaneCoverFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(laneCoverComponent: new TestLaneCoverDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsLaneCoverSkinLookup(BmsLaneCoverPosition.Sudden)), Is.SameAs(skin.LaneCoverComponent));
        }

        [Test]
        public void TestCustomStaticBackgroundFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(staticBackgroundComponent: new TestBackgroundLayerDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.StaticBackgroundLayer)), Is.SameAs(skin.StaticBackgroundComponent));
        }

        [Test]
        public void TestCustomGaugeHistoryFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(gaugeHistoryComponent: new TestGaugeHistoryDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeHistory)), Is.SameAs(skin.GaugeHistoryComponent));
        }

        [Test]
        public void TestCustomGaugeHistoryPanelFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(gaugeHistoryPanelComponent: new TestGaugeHistoryPanelDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeHistoryPanel)), Is.SameAs(skin.GaugeHistoryPanelComponent));
        }

        [Test]
        public void TestCustomNoteDistributionFallsBackToWrappedSkin()
        {
            var skin = new TestSkin(noteDistributionComponent: new TestNoteDistributionDisplay());
            var transformer = new BmsRuleset().CreateSkinTransformer(skin, new BmsBeatmap());

            Assert.That(transformer!.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.NoteDistribution)), Is.SameAs(skin.NoteDistributionComponent));
        }

        private static OmsSkin createOmsSkin() => new OmsSkin(new TestStorageResourceProvider());

        private static void assertSingleColour(Drawable drawable, Color4 expected)
        {
            Assert.That(drawable.Colour.TopLeft.SRGB, Is.EqualTo(expected));
            Assert.That(drawable.Colour.TopRight.SRGB, Is.EqualTo(expected));
            Assert.That(drawable.Colour.BottomLeft.SRGB, Is.EqualTo(expected));
            Assert.That(drawable.Colour.BottomRight.SRGB, Is.EqualTo(expected));
        }

        private sealed class TestStorageResourceProvider : IStorageResourceProvider
        {
            private readonly DllResourceStore resourceStore = new DllResourceStore(typeof(OmsSkin).Assembly);

            public IRenderer Renderer { get; } = new DummyRenderer();

            public AudioManager? AudioManager => null;

            public IResourceStore<byte[]> Files => resourceStore;

            public IResourceStore<byte[]> Resources => resourceStore;

            public RealmAccess RealmAccess => null!;

            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;
        }

        private sealed class TestSkin : ISkin
        {
            public readonly Drawable GlobalComponent = new Container();
            public readonly Drawable GreatJudgementComponent = new Container();
            public readonly Drawable? NoteDistributionComponent;
            public readonly Drawable? HudLayoutComponent;
            public readonly Drawable? SpeedFeedbackComponent;
            public readonly Drawable? NoteDistributionPanelComponent;
            public readonly Drawable? RulesetHudComponent;
            public readonly Drawable? GaugeBarComponent;
            public readonly Drawable? ComboCounterComponent;
            public readonly Drawable? ClearLampComponent;
            public readonly Drawable? GaugeHistoryPanelComponent;
            public readonly Drawable? GaugeHistoryComponent;
            public readonly Drawable? ResultsSummaryPanelComponent;
            public readonly Drawable? ResultsSummaryComponent;
            public readonly Drawable? PlayfieldBackdropComponent;
            public readonly Drawable? PlayfieldBaseplateComponent;
            public readonly Drawable? LaneBackgroundComponent;
            public readonly Drawable? LaneDividerComponent;
            public readonly Drawable? JudgementComponent;
            public readonly Drawable? NoteComponent;
            public readonly Drawable? LaneCoverComponent;
            public readonly Drawable? StaticBackgroundComponent;

            public TestSkin(Drawable? rulesetHudComponent = null, Drawable? hudLayoutComponent = null, Drawable? gaugeBarComponent = null, Drawable? comboCounterComponent = null, Drawable? speedFeedbackComponent = null, Drawable? clearLampComponent = null, Drawable? gaugeHistoryPanelComponent = null, Drawable? gaugeHistoryComponent = null, Drawable? resultsSummaryPanelComponent = null, Drawable? resultsSummaryComponent = null, Drawable? noteDistributionComponent = null, Drawable? noteDistributionPanelComponent = null, Drawable? playfieldBackdropComponent = null, Drawable? playfieldBaseplateComponent = null, Drawable? laneBackgroundComponent = null, Drawable? laneDividerComponent = null, Drawable? judgementComponent = null, Drawable? noteComponent = null, Drawable? laneCoverComponent = null, Drawable? staticBackgroundComponent = null)
            {
                RulesetHudComponent = rulesetHudComponent;
                HudLayoutComponent = hudLayoutComponent;
                GaugeBarComponent = gaugeBarComponent;
                ComboCounterComponent = comboCounterComponent;
                SpeedFeedbackComponent = speedFeedbackComponent;
                ClearLampComponent = clearLampComponent;
                GaugeHistoryPanelComponent = gaugeHistoryPanelComponent;
                GaugeHistoryComponent = gaugeHistoryComponent;
                ResultsSummaryPanelComponent = resultsSummaryPanelComponent;
                ResultsSummaryComponent = resultsSummaryComponent;
                NoteDistributionComponent = noteDistributionComponent;
                NoteDistributionPanelComponent = noteDistributionPanelComponent;
                PlayfieldBackdropComponent = playfieldBackdropComponent;
                PlayfieldBaseplateComponent = playfieldBaseplateComponent;
                LaneBackgroundComponent = laneBackgroundComponent;
                LaneDividerComponent = laneDividerComponent;
                JudgementComponent = judgementComponent;
                NoteComponent = noteComponent;
                LaneCoverComponent = laneCoverComponent;
                StaticBackgroundComponent = staticBackgroundComponent;
            }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup switch
                {
                    GlobalSkinnableContainerLookup { Ruleset: null, Lookup: GlobalSkinnableContainers.MainHUDComponents } => GlobalComponent,
                    GlobalSkinnableContainerLookup { Ruleset: { ShortName: BmsRuleset.SHORT_NAME }, Lookup: GlobalSkinnableContainers.MainHUDComponents } => RulesetHudComponent,
                    SkinComponentLookup<HitResult> { Component: HitResult.Great } => GreatJudgementComponent,
                    BmsJudgementSkinLookup => JudgementComponent,
                    BmsPlayfieldSkinLookup { Element: BmsPlayfieldSkinElements.Backdrop } => PlayfieldBackdropComponent,
                    BmsPlayfieldSkinLookup { Element: BmsPlayfieldSkinElements.Baseplate } => PlayfieldBaseplateComponent,
                    BmsLaneSkinLookup { Element: BmsLaneSkinElements.Background } => LaneBackgroundComponent,
                    BmsLaneSkinLookup { Element: BmsLaneSkinElements.Divider } => LaneDividerComponent,
                    BmsNoteSkinLookup => NoteComponent,
                    BmsLaneCoverSkinLookup => LaneCoverComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout } => HudLayoutComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeBar } => GaugeBarComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter } => ComboCounterComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.SpeedFeedback } => SpeedFeedbackComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ClearLamp } => ClearLampComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeHistoryPanel } => GaugeHistoryPanelComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeHistory } => GaugeHistoryComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ResultsSummaryPanel } => ResultsSummaryPanelComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ResultsSummary } => ResultsSummaryComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.NoteDistributionPanel } => NoteDistributionPanelComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.NoteDistribution } => NoteDistributionComponent,
                    BmsSkinComponentLookup { Component: BmsSkinComponents.StaticBackgroundLayer } => StaticBackgroundComponent,
                    _ => null,
                };

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
                => null;
        }

        private sealed partial class TestNoteDistributionDisplay : CompositeDrawable, IBmsNoteDistributionDisplay
        {
            public void SetData(BmsNoteDistributionData? data)
            {
            }
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

        private sealed partial class LegacyTestHudLayoutDisplay : Container, IBmsHudLayoutDisplay
        {
            public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
            {
                Clear();

                if (wrappedHud != null)
                    Add(wrappedHud);

                Add(gaugeBar);
                Add(comboCounter);
            }
        }

        private sealed partial class TestSpeedFeedbackDisplay : CompositeDrawable, IBmsSpeedFeedbackDisplay
        {
            public bool UsesFixedAnchor { get; set; }
        }

        private sealed partial class TestNoteDistributionPanelDisplay : CompositeDrawable, IBmsNoteDistributionPanelDisplay
        {
            public void SetState(BmsNoteDistributionPanelState? state)
            {
            }
        }

        private sealed partial class TestGaugeHistoryDisplay : CompositeDrawable, IBmsGaugeHistoryDisplay
        {
            public void SetHistory(BmsGaugeHistory? history)
            {
            }
        }

        private sealed partial class TestGaugeHistoryPanelDisplay : CompositeDrawable, IBmsGaugeHistoryPanelDisplay
        {
            public void SetHistory(BmsGaugeHistory? history)
            {
            }
        }

        private sealed partial class TestClearLampDisplay : CompositeDrawable, IBmsClearLampDisplay
        {
            public void SetClearLamp(BmsClearLampData? clearLamp)
            {
            }
        }

        private sealed partial class TestResultsSummaryDisplay : CompositeDrawable, IBmsResultsSummaryDisplay
        {
            public void SetSummary(BmsResultsSummaryData? summary)
            {
            }
        }

        private sealed partial class TestResultsSummaryPanelDisplay : CompositeDrawable, IBmsResultsSummaryPanelDisplay
        {
            public void SetSummary(BmsResultsSummaryData? summary)
            {
            }
        }

        private sealed partial class TestBackgroundLayerDisplay : CompositeDrawable, IBmsBackgroundLayerDisplay
        {
            public void SetDisplayedAssetName(string displayedAssetName)
            {
            }
        }

        private sealed partial class TestLaneCoverDisplay : CompositeDrawable, IBmsLaneCoverDisplay
        {
            public void SetFocused(bool isFocused)
            {
            }
        }

        private sealed partial class TestJudgementDisplay : CompositeDrawable, IAnimatableJudgement
        {
            public void PlayAnimation()
            {
            }

            public Drawable? GetAboveHitObjectsProxiedContent() => null;
        }

        private sealed partial class TestComboCounter : DefaultComboCounter
        {
        }
    }
}
