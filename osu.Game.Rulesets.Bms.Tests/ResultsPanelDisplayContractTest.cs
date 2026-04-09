// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.SongSelect;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Ranking.Statistics;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class ResultsPanelDisplayContractTest
    {
        [TestCase(typeof(DefaultBmsResultsSummaryPanelDisplay))]
        [TestCase(typeof(DefaultBmsGaugeHistoryPanelDisplay))]
        [TestCase(typeof(DefaultBmsNoteDistributionPanelDisplay))]
        public void TestUsesSharedStatefulResultsPanelContract(Type panelType)
        {
            Assert.That(panelType.BaseType, Is.Not.Null);
            Assert.That(panelType.BaseType!.IsGenericType, Is.True);
            Assert.That(panelType.BaseType!.GetGenericTypeDefinition(), Is.EqualTo(typeof(DefaultResultsPanelDisplay<>)));
        }
    }
}