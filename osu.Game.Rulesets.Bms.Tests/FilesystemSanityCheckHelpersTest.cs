// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class FilesystemSanityCheckHelpersTest
    {
        [Test]
        public void TestIsSubDirectoryAllowsTrailingSeparatorOnParent()
        {
            withTemporaryDirectory((root, child) =>
            {
                Assert.That(FilesystemSanityCheckHelpers.IsSubDirectory(root + Path.DirectorySeparatorChar, child), Is.True);
            });
        }

        [Test]
        public void TestIsSubDirectoryAllowsSameDirectoryWithTrailingSeparator()
        {
            withTemporaryDirectory((root, _) =>
            {
                Assert.That(FilesystemSanityCheckHelpers.IsSubDirectory(root + Path.DirectorySeparatorChar, root), Is.True);
            });
        }

        private static void withTemporaryDirectory(Action<string, string> assertion)
        {
            string root = Path.Combine(Path.GetTempPath(), $"oms-filesystem-sanity-{Guid.NewGuid():N}");
            string child = Path.Combine(root, "child");

            Directory.CreateDirectory(child);

            try
            {
                assertion(root, child);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
