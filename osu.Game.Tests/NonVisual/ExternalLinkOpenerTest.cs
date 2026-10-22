// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online.Chat;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ExternalLinkOpenerTest
    {
        [TestCase("https://outside.example/", "", false)]
        [TestCase("https://outside.example/", null, false)]
        [TestCase("https://outside.example/", "invalid", false)]
        [TestCase("https://oms.zdamexy.work/ir", "https://oms.zdamexy.work", true)]
        [TestCase("https://OMS.ZDAMEXY.WORK:443/ir?x=1#chart", "https://oms.zdamexy.work/", true)]
        [TestCase("https://oms.zdamexy.work.evil.example/", "https://oms.zdamexy.work", false)]
        [TestCase("https://oms.zdamexy.work@evil.example/", "https://oms.zdamexy.work", false)]
        [TestCase("https://evil.example@oms.zdamexy.work/", "https://oms.zdamexy.work", false)]
        [TestCase("https://oms.zdamexy.work:8443/", "https://oms.zdamexy.work", false)]
        [TestCase("http://oms.zdamexy.work/", "https://oms.zdamexy.work", false)]
        [TestCase("https://sub.oms.zdamexy.work/", "https://oms.zdamexy.work", false)]
        [TestCase("file:///C:/test.txt", "file:///C:/", false)]
        [TestCase("/ir", "", false)]
        public void TestOnlyConfiguredHttpOriginIsTrusted(string url, string? origin, bool expected)
        {
            Assert.That(ExternalLinkOpener.IsTrustedOrigin(url, origin), Is.EqualTo(expected));
        }
    }
}
