// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Ranking.Statistics;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class StatisticItemContainerTest
    {
        [Test]
        public void TestNamedStatisticItemKeepsGenericShell()
        {
            var container = new TestStatisticItemContainer(new StatisticItem("Named item", static () => new Container()));

            Assert.Multiple(() =>
            {
                Assert.That(container.Shell.Masking, Is.True);
                Assert.That(container.Shell.CornerRadius, Is.EqualTo(6));
            });
        }

        [Test]
        public void TestUnnamedStatisticItemOmitsGenericShell()
        {
            var container = new TestStatisticItemContainer(new StatisticItem(string.Empty, static () => new Container()));

            Assert.Multiple(() =>
            {
                Assert.That(container.Shell.Masking, Is.False);
                Assert.That(container.Shell.CornerRadius, Is.Zero);
            });
        }

        private partial class TestStatisticItemContainer : StatisticItemContainer
        {
            public TestStatisticItemContainer(StatisticItem item)
                : base(item)
            {
            }

            public Container Shell => (Container)InternalChild;
        }
    }
}