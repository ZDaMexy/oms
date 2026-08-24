// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Localisation;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class SkinExternalEditDisabledUiTest
    {
        [Test]
        public void ProductionMenuAffordanceIsExplicitlyUnavailable()
        {
            var item = new SkinEditor().CreateExternalEditMenuItem();

            Assert.Multiple(() =>
            {
                Assert.That(item.Text.ToString(), Is.EqualTo(SkinEditorStrings.EditExternallyUnavailable.ToString()));
                Assert.That(item.Action.Disabled, Is.True);
            });
        }

        [Test]
        public void OverlayGateHasStableDisabledDiagnostic()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExternalEditOverlay.IsSkinExternalEditingAvailable, Is.False);
                Assert.That(ExternalEditOverlay.EXTERNAL_EDITING_DISABLED_DIAGNOSTIC, Does.Contain("current revision protocol"));
                Assert.That(SkinEditorStrings.ExternalEditingUnavailable.ToString(), Is.Not.Empty);
                Assert.That(SkinEditorStrings.ExternalChangesNotActivated.ToString(), Is.Not.Empty);
            });
        }

        [Test]
        public void LegacyAuthoringHasOneStableFailClosedAuthority()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SkinAuthoringAvailability.LegacyEditorAvailable, Is.False);
                Assert.That(SkinEditorOverlay.IsLegacyAuthoringAvailable, Is.False);
                Assert.That(SkinSettingsStrings.SkinAuthoringUnavailable.ToString(), Is.Not.Empty);
            });
        }
    }
}
