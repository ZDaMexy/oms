// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class TestSceneBmsSupplementalBindingSettingsSection
    {
        [Test]
        public async System.Threading.Tasks.Task TestDeviceDiscoveryRunsOffCallingThread()
        {
            int callingThreadId = Environment.CurrentManagedThreadId;
            int providerThreadId = 0;

            var section = new BmsSupplementalBindingSettingsSection(new BmsRuleset(), () =>
            {
                providerThreadId = Environment.CurrentManagedThreadId;
                return Array.Empty<OmsHidDeviceInfo>();
            });

            var devices = await section.enumerateDetectedDevicesAsync().ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(devices, Is.Empty);
                Assert.That(providerThreadId, Is.Not.EqualTo(0));
                Assert.That(providerThreadId, Is.Not.EqualTo(callingThreadId));
            });
        }
    }
}
