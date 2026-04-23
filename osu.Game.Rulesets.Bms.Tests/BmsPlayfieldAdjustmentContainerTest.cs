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
        public void TestUsesIdentityTransform()
        {
            var container = new BmsPlayfieldAdjustmentContainer();

            Assert.Multiple(() =>
            {
                Assert.That(container.AppliedScale.X, Is.EqualTo(1.0f).Within(0.0001f));
                Assert.That(container.AppliedScale.Y, Is.EqualTo(1.0f).Within(0.0001f));
                Assert.That(container.AppliedHorizontalOffset, Is.EqualTo(0.0f).Within(0.0001f));
            });
        }
    }
}
