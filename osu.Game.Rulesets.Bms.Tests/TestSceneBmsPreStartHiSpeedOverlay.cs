// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsPreStartHiSpeedOverlay : OsuTestScene
    {
        private readonly Bindable<BmsHiSpeedMode> hiSpeedMode = new Bindable<BmsHiSpeedMode>();
        private readonly BindableDouble hiSpeedValue = new BindableDouble();
        private readonly List<int> adjustmentDirections = new List<int>();

        private BmsPreStartHiSpeedOverlay overlay = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            hiSpeedMode.Value = BmsHiSpeedMode.Normal;
            hiSpeedValue.Value = 8;
            adjustmentDirections.Clear();

            Child = overlay = new BmsPreStartHiSpeedOverlay(8, hiSpeedMode, hiSpeedValue, direction =>
            {
                adjustmentDirections.Add(direction);
                return true;
            });
        });

        [Test]
        public void TestOverlayUpdatesModeAndValueTextForTriModeSurface()
        {
            AddUntilStep("overlay loaded", () => overlay.IsLoaded);
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddAssert("normal mode text shown", () => tryGetText("Normal Hi-Speed") != null);
            AddAssert("normal value text shown", () => tryGetText("8.0") != null);

            AddStep("switch to floating mode", () =>
            {
                hiSpeedMode.Value = BmsHiSpeedMode.Floating;
                hiSpeedValue.Value = 2.5;
            });
            AddAssert("floating mode text shown", () => tryGetText("Floating Hi-Speed") != null);
            AddAssert("floating value text shown", () => tryGetText("2.50") != null);

            AddStep("switch to classic mode", () =>
            {
                hiSpeedMode.Value = BmsHiSpeedMode.Classic;
                hiSpeedValue.Value = 6.25;
            });
            AddAssert("classic mode text shown", () => tryGetText("Classic Hi-Speed") != null);
            AddAssert("classic value text shown", () => tryGetText("6.25") != null);
        }

        [Test]
        public void TestOverlayOnlyRoutesHiSpeedAdjustmentsWhileVisible()
        {
            AddUntilStep("overlay loaded", () => overlay.IsLoaded);

            AddAssert("hidden overlay blocks hi-speed adjustment", () => !overlay.TryHandleActionPress(BmsAction.Key1));
            AddAssert("hidden overlay does not invoke callback", () => adjustmentDirections.Count == 0);

            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddAssert("non adjustment action ignored", () => !overlay.TryHandleActionPress(BmsAction.LaneCoverFocus));
            AddAssert("callback still untouched after non adjustment action", () => adjustmentDirections.Count == 0);

            AddAssert("odd lane increases hi-speed", () => overlay.TryHandleActionPress(BmsAction.Key1));
            AddAssert("positive direction routed", () => adjustmentDirections.Last() == 1);

            AddAssert("even lane decreases hi-speed", () => overlay.TryHandleActionPress(BmsAction.Key2));
            AddAssert("negative direction routed", () => adjustmentDirections.Last() == -1);

            AddStep("hide overlay", () => overlay.Hide());
            AddUntilStep("overlay hidden", () => overlay.State.Value == Visibility.Hidden);
            AddAssert("hidden overlay blocks later adjustment", () => !overlay.TryHandleActionPress(BmsAction.Key1));
            AddAssert("callback count unchanged after hiding", () => adjustmentDirections.Count == 2);
        }

        private OsuSpriteText? tryGetText(string text)
            => overlay.ChildrenOfType<OsuSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);
    }
}
