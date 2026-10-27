// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed partial class SkinResultsPageLifetimeTest
    {
        [Test]
        public void TestAnActualResultsScreenCanBeDisposedBeforeLoadingItsOptionalHost()
        {
            var screen = new UnloadedResultsScreen();
            Assert.DoesNotThrow(screen.Dispose);
            Assert.DoesNotThrow(screen.Dispose);
        }

        [Test]
        public void TestAnActualSongSelectCanBeDisposedBeforeLoadingItsOptionalHost()
        {
            var screen = new SoloSongSelect();
            Assert.DoesNotThrow(screen.Dispose);
            Assert.DoesNotThrow(screen.Dispose);
        }

        private partial class UnloadedResultsScreen : ResultsScreen
        {
            public UnloadedResultsScreen()
                : base(null)
            {
            }
        }
    }
}
