// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.UI;
namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsPlayfieldAdjustmentContainerTest
    {
        [Test]
        public void TestAppliesProvidedAdjustmentValues()
        {
            var scale = new BindableDouble(1.15);
            var horizontalOffset = new BindableDouble(-0.08);
            var container = new BmsPlayfieldAdjustmentContainer(scale, horizontalOffset);

            Assert.Multiple(() =>
            {
                Assert.That(container.AppliedScale.X, Is.EqualTo(1.15f).Within(0.0001f));
                Assert.That(container.AppliedScale.Y, Is.EqualTo(1.15f).Within(0.0001f));
                Assert.That(container.AppliedHorizontalOffset, Is.EqualTo(-0.08f).Within(0.0001f));
            });
        }

        [Test]
        public void TestRespondsToAdjustmentValueChanges()
        {
            var scale = new BindableDouble(1.0);
            var horizontalOffset = new BindableDouble(0.0);
            var container = new BmsPlayfieldAdjustmentContainer(scale, horizontalOffset);

            scale.Value = 0.9;
            horizontalOffset.Value = 0.12;

            Assert.Multiple(() =>
            {
                Assert.That(container.AppliedScale.X, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(container.AppliedScale.Y, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(container.AppliedHorizontalOffset, Is.EqualTo(0.12f).Within(0.0001f));
            });
        }
    }
}
