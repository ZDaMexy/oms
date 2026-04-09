// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsHitTargetState : OsuTestScene
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        private TestBmsHitTarget regularTarget = null!;
        private TestBmsHitTarget scratchTarget = null!;
        private TestableDrawableBmsRuleset drawableRuleset = null!;

        [SetUp]
        public void Setup() => Schedule(() =>
        {
            var layoutProfile = BmsPlayfieldLayoutProfile.CreateDefault(BmsKeymode.Key7K, 8);

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.DarkGray,
                    },
                    new Container
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        RelativeSizeAxes = Axes.X,
                        Width = 0.55f,
                        Height = 32,
                        Padding = new MarginPadding { Top = 8 },
                        Children = new Drawable[]
                        {
                            regularTarget = new TestBmsHitTarget(new BmsLaneSkinLookup(BmsLaneSkinElements.HitTarget, 1, 8, false, BmsKeymode.Key7K), layoutProfile)
                            {
                                RelativePositionAxes = Axes.X,
                                Width = 0.45f,
                            },
                            scratchTarget = new TestBmsHitTarget(new BmsLaneSkinLookup(BmsLaneSkinElements.HitTarget, 0, 8, true, BmsKeymode.Key7K), layoutProfile)
                            {
                                RelativePositionAxes = Axes.X,
                                X = 0.55f,
                                Width = 0.45f,
                            },
                        }
                    },
                    drawableRuleset = new TestableDrawableBmsRuleset(new BmsRuleset(), createPlayableBeatmap())
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                }
            };
        });

        [Test]
        public void TestPressedStateVisualResponds()
        {
            AddUntilStep("regular target loaded", () => regularTarget?.IsLoaded == true);

            AddStep("press regular target", () => regularTarget.IsPressed.Value = true);
            AddAssert("pressed overlay visible", () => regularTarget.PressedOverlayAlpha, () => Is.GreaterThan(0f));

            AddStep("release regular target", () => regularTarget.IsPressed.Value = false);
            AddAssert("pressed overlay hidden", () => regularTarget.PressedOverlayAlpha, () => Is.EqualTo(0f));
        }

        [Test]
        public void TestFocusStateVisualResponds()
        {
            AddUntilStep("scratch target loaded", () => scratchTarget?.IsLoaded == true);

            AddStep("focus scratch target", () => scratchTarget.IsFocused.Value = true);
            AddAssert("focus edge visible", () => scratchTarget.FocusEdgeAlpha, () => Is.GreaterThan(0f));

            AddStep("unfocus scratch target", () => scratchTarget.IsFocused.Value = false);
            AddAssert("focus edge hidden", () => scratchTarget.FocusEdgeAlpha, () => Is.EqualTo(0f));
        }

        [Test]
        public void TestLaneHitTargetTracksRegularActionPresses()
        {
            AddUntilStep("drawable ruleset loaded", () => drawableRuleset?.IsLoaded == true);

            AddStep("press key1 action", () => Assert.That(drawableRuleset.InputManager.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddUntilStep("key1 hit target pressed", () => drawableRuleset.Playfield.Lanes[1].HitTarget.IsPressed.Value);

            AddStep("release key1 action", () => Assert.That(drawableRuleset.InputManager.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddUntilStep("key1 hit target released", () => !drawableRuleset.Playfield.Lanes[1].HitTarget.IsPressed.Value);
        }

        [Test]
        public void TestLaneHitTargetTracksScratchActionPresses()
        {
            AddUntilStep("drawable ruleset loaded", () => drawableRuleset?.IsLoaded == true);

            AddStep("press scratch action", () => Assert.That(drawableRuleset.InputManager.TriggerOmsActionPressed(OmsAction.Key1P_Scratch), Is.True));
            AddUntilStep("scratch hit target pressed", () => drawableRuleset.Playfield.Lanes[0].HitTarget.IsPressed.Value);

            AddStep("release scratch action", () => Assert.That(drawableRuleset.InputManager.TriggerOmsActionReleased(OmsAction.Key1P_Scratch), Is.True));
            AddUntilStep("scratch hit target released", () => !drawableRuleset.Playfield.Lanes[0].HitTarget.IsPressed.Value);
        }

        private BmsBeatmap createPlayableBeatmap()
        {
            const string text = @"
#TITLE HitTarget State Stub
#BPM 120
#RANK 2
#00101:AA00
#WAVAA bgm.wav
#WAVBB key1.wav
#WAVDD scratch.wav
#00111:BB00
#00116:DD00
";

            var decodedChart = decoder.DecodeText(text, "hit-target-state-stub.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }

        private partial class TestBmsHitTarget : BmsHitTarget
        {
            public TestBmsHitTarget(BmsLaneSkinLookup lookup, BmsPlayfieldLayoutProfile layoutProfile)
                : base(lookup, layoutProfile)
            {
            }

            public new float PressedOverlayAlpha => base.PressedOverlayAlpha;

            public new float FocusEdgeAlpha => base.FocusEdgeAlpha;
        }

        private sealed partial class TestableDrawableBmsRuleset : DrawableBmsRuleset
        {
            public BmsInputManager InputManager => (BmsInputManager)KeyBindingInputManager;

            public TestableDrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap)
                : base(ruleset, beatmap)
            {
            }
        }
    }
}
