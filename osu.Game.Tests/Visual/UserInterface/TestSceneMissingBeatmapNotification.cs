// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Database;
using osu.Game.IO.Archives;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneMissingBeatmapNotification : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new Container
            {
                Width = 280,
                AutoSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = new MissingBeatmapNotification(CreateAPIBeatmapSet(Ruleset.Value).Beatmaps.First(), "deadbeef", new TestArchiveReader())
            };
        }

        private class TestArchiveReader : ArchiveReader
        {
            public TestArchiveReader()
                : base("test_archive")
            {
            }

            public override Stream GetStream(string name) => new MemoryStream();

            public override IEnumerable<string> Filenames => new[] { "test_file.osr" };

            public override void Dispose()
            {
            }
        }
    }
}
