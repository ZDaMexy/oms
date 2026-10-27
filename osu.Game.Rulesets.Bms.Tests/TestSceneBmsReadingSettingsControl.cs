// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsReadingSettingsControl : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        protected override bool UseFreshStoragePerRun => true;

        private BmsRulesetConfigManager config = null!;
        private BmsReadingSettingsControl control = null!;

        [TearDownSteps]
        public void DisposeReadingConfiguration()
            => AddStep("dispose reading settings", () =>
            {
                Clear();
                config?.Dispose();
            });

        [Test]
        public void TestCoverControlsSaveCombinationDisplayValuesAndUnifiedRememberPreference()
        {
            createControl();
            AddStep("select all three reading aids and hide the covers", () =>
            {
                checkBox("使用 Sudden").Current.Value = true;
                checkBox("使用 Hidden").Current.Value = true;
                checkBox("使用 Lift").Current.Value = true;
                checkBox("显示挡板（游玩中双击 Start 切换）").Current.Value = false;
                slider("Sudden 白字").Current.Value = 321;
                slider("Hidden 白字").Current.Value = 234;
                slider("Lift 白字").Current.Value = 123;
                slider("Sudden 不透明度").Current.Value = 800;
                slider("Hidden 不透明度").Current.Value = 600;
                checkBox("记住游玩中的读谱调整").Current.Value = false;
            });
            AddAssert("the actual ruleset configuration stores the complete reading selection", () =>
                config.CaptureReadingSettings() == new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Normal, 8, 2.5, 2.5,
                    true, true, true, false, 321, 234, 123, 800, 600));
            AddAssert("there is one unified remember control and its preference is saved", () =>
                control.ChildrenOfType<FormCheckBox>().Count(check => check.Caption.ToString().Contains("记住")) == 1
                && !config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value);
            AddStep("show the saved cover combination again", () => checkBox("显示挡板（游玩中双击 Start 切换）").Current.Value = true);
            AddAssert("showing covers preserves both selected covers and the independent Lift", () =>
            {
                var reading = config.CaptureReadingSettings();
                return reading.LaneCoversEnabled && reading.SuddenEnabled && reading.HiddenEnabled && reading.LiftEnabled
                       && reading.SuddenUnits == 321 && reading.HiddenUnits == 234 && reading.LiftUnits == 123;
            });
        }

        [Test]
        public void TestHiSpeedControlKeepsTheThreeModeValuesIndependent()
        {
            createControl();
            AddStep("set independent mode speeds", () => config.SaveReadingSettings(config.CaptureReadingSettings() with
            {
                NormalHiSpeed = 9,
                FloatingHiSpeed = 1.75,
                ClassicHiSpeed = 3.25,
            }));
            AddStep("switch to Floating in the visible control", () => modeControl().Current.Value = BmsHiSpeedMode.Floating);
            AddAssert("the shared speed control shows the stored Floating speed", () => speedControl().Current.Value == 1.75);
            AddStep("edit Floating", () => speedControl().Current.Value = 2.25);
            AddStep("switch to Classic", () => modeControl().Current.Value = BmsHiSpeedMode.Classic);
            AddAssert("Classic retains its speed", () => speedControl().Current.Value == 3.25);
            AddStep("switch to Normal", () => modeControl().Current.Value = BmsHiSpeedMode.Normal);
            AddAssert("Normal and edited Floating remain independent", () => speedControl().Current.Value == 9
                && config.GetBindable<double>(BmsRulesetSetting.FloatingHiSpeed).Value == 2.25);
            AddAssert("the mode hint names the actual EFFECT binding", () => modeControl().HintText.ToString().Contains("游玩：挡板／调速"));
        }

        private void createControl()
        {
            AddStep("open BMS reading settings", () =>
            {
                config = (BmsRulesetConfigManager)new BmsRuleset().CreateConfig(null)!;
                Child = new Container
                {
                    Width = 600,
                    AutoSizeAxes = Axes.Y,
                    Child = control = new BmsReadingSettingsControl(config),
                };
            });
            AddUntilStep("the reading controls are loaded", () => control.IsLoaded
                && control.ChildrenOfType<FormCheckBox>().Any(check => check.IsLoaded && check.Caption.ToString() == "记住游玩中的读谱调整"));
        }

        private FormCheckBox checkBox(string caption) => control.ChildrenOfType<FormCheckBox>().Single(check => check.Caption.ToString() == caption);

        private FormSliderBar<float> slider(string caption) => control.ChildrenOfType<FormSliderBar<float>>().Single(slider => slider.Caption.ToString() == caption);

        private FormEnumDropdown<BmsHiSpeedMode> modeControl() => control.ChildrenOfType<FormEnumDropdown<BmsHiSpeedMode>>().Single();

        private FormSliderBar<double> speedControl() => control.ChildrenOfType<FormSliderBar<double>>().Single();
    }
}
