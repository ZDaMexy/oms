// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Linq;
using Humanizer;
using NUnit.Framework;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Tests.Visual.Navigation
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneGameplaySkinDiagnosticNotification : OsuGameTestScene
    {
        private const string diagnostic_batch = "Gameplay skin material diagnostic: count=2\n"
                                                + "code=OMS-SKIN-CODEC-021; slot=effect.hit-explosion; target=document; source=SelectedPackage; catalog=oms-gameplay-skin-catalog.v1; codec=oms-gameplay-skin-codec.v1; resolver=oms-gameplay-skin-resolver.v1\n"
                                                + "code=OMS-SKIN-CODEC-021; slot=object.note; target=document; source=SelectedPackage; catalog=oms-gameplay-skin-catalog.v1; codec=oms-gameplay-skin-codec.v1; resolver=oms-gameplay-skin-resolver.v1";

        private readonly ConcurrentQueue<LogEntry> logEntries = new ConcurrentQueue<LogEntry>();

        [SetUp]
        public void SetUpLogReceipt() => logEntries.Clear();

        [TearDown]
        public void TearDownLogReceipt() => Logger.NewEntry -= captureLog;

        private void captureLog(LogEntry entry) => logEntries.Enqueue(entry);

        [Test]
        public void TestSkinDiagnosticHasActionableNotificationAndCompleteLog()
        {
            AddStep("observe the real game logging and publish the full diagnostic batch", () =>
            {
                Logger.NewEntry += captureLog;
                Assert.That(diagnostic_batch.Length, Is.GreaterThan(256));
                Logger.Log(diagnostic_batch, LoggingTarget.Runtime, LogLevel.Important);
            });
            AddUntilStep("the real notification offers skin update and log export", () =>
                Game.Notifications.AllNotifications.OfType<SimpleErrorNotification>().Any(notification =>
                    notification.Text.ToString() == NotificationsStrings.GameplaySkinContentUnavailable.ToString()));
            AddUntilStep("the original complete important log was observed", () =>
                logEntries.Any(entry => entry.Message == diagnostic_batch
                                        && entry.Target == LoggingTarget.Runtime
                                        && entry.Level == LogLevel.Important
                                        && entry.Exception == null));
            AddStep("the player sees Chinese actions without author diagnostic internals", () =>
            {
                string text = Game.Notifications.AllNotifications.OfType<SimpleErrorNotification>()
                                  .Single(notification => notification.Text.ToString() == NotificationsStrings.GameplaySkinContentUnavailable.ToString()).Text.ToString();
                Assert.Multiple(() =>
                {
                    Assert.That(text, Is.EqualTo("当前皮肤的部分内容未能正常使用。请更新皮肤；如仍有问题，可在设置中“导出日志”并提供给作者。"));
                    Assert.That(text, Does.Not.Contain("count="));
                    Assert.That(text, Does.Not.Contain("code="));
                    Assert.That(text, Does.Not.Contain("slot="));
                    Assert.That(Game.Notifications.AllNotifications.Any(notification => notification.Text.ToString().StartsWith("Gameplay skin material diagnostic:", StringComparison.Ordinal)), Is.False);
                    Assert.That(logEntries.Count(entry => entry.Message == diagnostic_batch), Is.EqualTo(1));
                });
            });
        }

        [TestCase(LoggingTarget.Runtime, LogLevel.Important, "Ordinary important message remains readable.", TestName = "TestOrdinaryImportantNotificationRetainsOriginalText")]
        [TestCase(LoggingTarget.Database, LogLevel.Important, diagnostic_batch, TestName = "TestDiagnosticLikeDatabaseNotificationRetainsOriginalText")]
        [TestCase(LoggingTarget.Runtime, LogLevel.Error, diagnostic_batch, TestName = "TestDiagnosticLikeErrorNotificationRetainsOriginalText")]
        [TestCase(LoggingTarget.Runtime, LogLevel.Important, "Gameplay skin material diagnostic: unrelated ordinary message.", TestName = "TestDiagnosticLikeOtherPrefixNotificationRetainsOriginalText")]
        public void TestOtherGeneralLogNotificationsRetainTheirOriginalText(LoggingTarget target, LogLevel level, string originalMessage)
        {
            AddStep("observe the real game logging and publish an ordinary message", () =>
            {
                Logger.NewEntry += captureLog;
                Logger.Log(originalMessage, target, level);
            });
            AddUntilStep("the original message reaches the real notification", () =>
                Game.Notifications.AllNotifications.OfType<SimpleErrorNotification>().Any(notification =>
                    notification.Text.ToString() == originalMessage.Truncate(256)));
            AddUntilStep("the log retains its original complete message and level", () =>
                logEntries.Any(entry => entry.Message == originalMessage && entry.Target == target && entry.Level == level));
            AddAssert("other messages are not relabelled as skin failures", () =>
                Game.Notifications.AllNotifications.All(notification =>
                    notification.Text.ToString() != NotificationsStrings.GameplaySkinContentUnavailable.ToString()));
        }
    }
}
