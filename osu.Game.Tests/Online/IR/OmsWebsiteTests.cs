// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.IR;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.Online.IR
{
    [TestFixture]
    public class OmsWebsiteTests
    {
        private const string origin = "https://ir.example.test:8443/";
        private static readonly string md5 = new string('a', 32);

        [TestCase("bms")]
        [TestCase("mania")]
        public void LocalChartUsesActualIdentityInsteadOfUpstreamIds(string ruleset)
        {
            BeatmapInfo chart = chartInfo(ruleset);
            chart.OnlineID = 123456;
            chart.OnlineMD5Hash = new string('b', 32);
            Assert.That(OmsWebsite.Chart(origin, chart), Is.EqualTo(origin + "beatmaps?ruleset=" + ruleset + "&md5=" + md5));
            chart.OnlineID = -1;
            Assert.That(OmsWebsite.Chart(origin, chart), Is.EqualTo(origin + "beatmaps?ruleset=" + ruleset + "&md5=" + md5));
            Assert.That(OmsWebsite.Chart("", chart), Is.Null);
            chart.MD5Hash = new string('z', 32);
            Assert.That(OmsWebsite.Chart(origin, chart), Is.Null);
        }

        [Test]
        public void BoardLinksPreserveSourceAndConditionScope()
        {
            string condition = new string('c', 64) + ":3372";
            Assert.That(OmsWebsite.ChartBoard(origin, md5, "bms", "comparable", condition, new[] { "oms", "lr2ir.v3.lr2" }, page: 3),
                Is.EqualTo(origin + "beatmaps?ruleset=bms&md5=" + md5 + "&mode=comparable&condition=" + Uri.EscapeDataString(condition)
                           + "&sources=lr2ir.v3.lr2%2Coms&page=3"));
            Assert.That(OmsWebsite.ChartBoard(origin, md5, "bms", "reference"), Does.Not.Contain("sources="));
            Assert.That(OmsWebsite.ChartBoard(origin, md5, "bms", "reference", sources: Array.Empty<string>()), Does.EndWith("&sources="));
            Assert.That(OmsWebsite.ChartBoard(origin, md5, "mania", group: new string('d', 64)),
                Is.EqualTo(origin + "beatmaps?ruleset=mania&md5=" + md5 + "&group=" + new string('d', 64)));
        }

        [Test]
        public void PublicIdentityAndQueryStayOnConfiguredOrigin()
        {
            Assert.That(OmsWebsite.Profile(origin, long.MaxValue, "mania", "mania_4k"),
                Is.EqualTo(origin + "users/9223372036854775807?ruleset=mania&keymode=mania_4k"));
            Assert.That(OmsWebsite.Profile(origin, 0, "bms"), Is.Null);
            Assert.That(OmsWebsite.Profile(origin, -1, "bms"), Is.Null);
            Assert.That(OmsWebsite.Rankings(origin, "bms", "pms_9k"), Is.EqualTo(origin + "rankings?ruleset=bms&keymode=pms_9k"));
            Assert.That(OmsWebsite.Rankings("", "bms"), Is.Null);
        }

        [TestCase(5, null, "bms_5k")]
        [TestCase(7, null, "bms_7k")]
        [TestCase(14, null, "bms_14k")]
        [TestCase(9, "chart.PMS", "pms_9k")]
        [TestCase(9, "chart.bms", "bms_9k")]
        [TestCase(9, null, null)]
        [TestCase(9, "chart.unknown", null)]
        public void BmsScopeUsesStoredKeysAndPmsAuthority(int keys, string? path, string? expected)
        {
            var chart = chartInfo("bms", keys);
            chart.LocalFilePath = path;
            Assert.That(new BmsRuleset().GetOmsWebsiteKeymode(chart), Is.EqualTo(expected));
        }

        [TestCase(4)]
        [TestCase(7)]
        [TestCase(10)]
        [TestCase(14)]
        [TestCase(18)]
        public void ManiaScopeKeepsActualColumnsAndDualStages(int columns)
        {
            var chart = chartInfo("mania", columns);
            var scope = OmsWebsite.CurrentScope(chart, new ManiaRuleset().RulesetInfo);
            Assert.That(scope, Is.EqualTo(("mania", "mania_" + columns + "k")));
        }

        private static BeatmapInfo chartInfo(string ruleset, int keys = 7) => new BeatmapInfo(new osu.Game.Rulesets.RulesetInfo { ShortName = ruleset })
        {
            MD5Hash = md5.ToUpperInvariant(),
            Difficulty = new BeatmapDifficulty { CircleSize = keys },
        };
    }
}
