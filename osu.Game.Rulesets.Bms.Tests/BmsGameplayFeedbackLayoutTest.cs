// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsGameplayFeedbackLayoutTest
    {
        [Test]
        public void TestDefaultHudAppliesSharedGameplayFeedbackPositionContract()
        {
            var gameplayFeedback = new TestGameplayFeedbackDisplay();

            DefaultBmsHudLayoutDisplay.ApplyGameplayFeedbackDefaults(gameplayFeedback);

            Assert.Multiple(() =>
            {
                Assert.That(gameplayFeedback.Anchor, Is.EqualTo(Anchor.TopCentre));
                Assert.That(gameplayFeedback.Origin, Is.EqualTo(Anchor.TopCentre));
                Assert.That(gameplayFeedback.Position, Is.EqualTo(BmsGameplayFeedbackLayout.DefaultGameplayFeedbackPosition));
                Assert.That(gameplayFeedback.UsesFixedAnchor, Is.True);
            });
        }

        private sealed partial class TestGameplayFeedbackDisplay : Drawable, ISerialisableDrawable
        {
            public bool UsesFixedAnchor { get; set; }
        }
    }
}
