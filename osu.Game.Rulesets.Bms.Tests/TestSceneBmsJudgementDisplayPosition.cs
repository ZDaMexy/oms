// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsJudgementDisplayPosition : OsuTestScene
    {
        private ScrollingTestContainer scrollingContainer = null!;
        private TestDrawableBmsJudgement perfectJudgement = null!;
        private TestDrawableBmsJudgement poorJudgement = null!;
        private TestDrawableBmsJudgement emptyPoorJudgement = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = scrollingContainer = new ScrollingTestContainer(ScrollingDirection.Up)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        perfectJudgement = createJudgement(HitResult.Perfect, 0.3f),
                        poorJudgement = createJudgement(HitResult.Miss, 0.5f),
                        emptyPoorJudgement = createJudgement(HitResult.Ok, 0.7f),
                    }
                }
            };
        });

        [Test]
        public void TestPoorAndEmptyPoorStayAlignedForUpScroll()
        {
            AddUntilStep("judgements loaded", () => perfectJudgement?.IsLoaded == true && poorJudgement?.IsLoaded == true && emptyPoorJudgement?.IsLoaded == true);
            playAnimations();
            AddWaitStep("advance animation", 5);
            assertAligned("up scroll");
        }

        [Test]
        public void TestPoorAndEmptyPoorStayAlignedForDownScroll()
        {
            AddUntilStep("judgements loaded", () => perfectJudgement?.IsLoaded == true && poorJudgement?.IsLoaded == true && emptyPoorJudgement?.IsLoaded == true);
            AddStep("switch to down scroll", () => scrollingContainer.Direction = ScrollingDirection.Down);
            AddWaitStep("apply direction", 1);
            playAnimations();
            AddWaitStep("advance animation", 5);
            assertAligned("down scroll");
        }

        private void playAnimations()
            => AddStep("play judgement animations", () =>
            {
                perfectJudgement.PlayInnerAnimation();
                poorJudgement.PlayInnerAnimation();
                emptyPoorJudgement.PlayInnerAnimation();
            });

        private void assertAligned(string label)
        {
            AddAssert($"poor stays on the shared judgement baseline ({label})", () => Math.Abs(perfectJudgement.BodyY - poorJudgement.BodyY) <= 0.1f);
            AddAssert($"empty poor stays on the shared judgement baseline ({label})", () => Math.Abs(perfectJudgement.BodyY - emptyPoorJudgement.BodyY) <= 0.1f);
            AddAssert($"poor uses the same body layer contract ({label})", () => poorJudgement.BodyAnchor == perfectJudgement.BodyAnchor && poorJudgement.BodyOrigin == perfectJudgement.BodyOrigin);
            AddAssert($"empty poor uses the same body layer contract ({label})", () => emptyPoorJudgement.BodyAnchor == perfectJudgement.BodyAnchor && emptyPoorJudgement.BodyOrigin == perfectJudgement.BodyOrigin);
        }

        private static TestDrawableBmsJudgement createJudgement(HitResult result, float x)
            => new TestDrawableBmsJudgement(result)
            {
                RelativePositionAxes = Axes.Both,
                X = x,
            };

        private sealed partial class TestDrawableBmsJudgement : DrawableBmsJudgement
        {
            public float BodyY => JudgementBody?.Y ?? 0;

            public Anchor BodyAnchor => JudgementBody?.Anchor ?? Anchor.Centre;

            public Anchor BodyOrigin => JudgementBody?.Origin ?? Anchor.Centre;

            public TestDrawableBmsJudgement(HitResult result)
            {
                Apply(createResult(result), null);
            }

            public void PlayInnerAnimation()
            {
                if (JudgementBody?.Drawable is IAnimatableJudgement animatable)
                    animatable.PlayAnimation();
            }

            private static JudgementResult createResult(HitResult result)
            {
                var hitObject = new BmsHitObject();

                return new JudgementResult(hitObject, hitObject.CreateJudgement())
                {
                    Type = result,
                };
            }
        }
    }
}
