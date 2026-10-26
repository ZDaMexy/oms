// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Input;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneKeyBindingRow : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [TestCase(360)]
        [TestCase(600)]
        public void TestLongActionDescriptionLeavesBindingsVisibleAndRetainsFullHelp(int width)
        {
            KeyBindingRow row = null!;
            Container container = null!;

            AddStep("create a long action label with two bindings", () => Child = container = new Container
            {
                Width = width,
                AutoSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = row = new KeyBindingRow(LongAction.Adjust)
                {
                    KeyBindings =
                    {
                        new RealmKeyBinding(LongAction.Adjust, new KeyCombination(InputKey.W)),
                        new RealmKeyBinding(LongAction.Adjust, new KeyCombination(InputKey.Joystick1)),
                    }
                }
            });

            AddUntilStep("long label ends before the binding buttons", () => labelFits());
            AddAssert("hover help retains the full action explanation", () =>
                row.ChildrenOfType<TruncatingSpriteText>().Single().TooltipText == long_action_description);
            AddStep("narrow the settings row", () => container.Width -= 80);
            AddUntilStep("resized label still leaves both bindings visible", () => labelFits());
            AddStep("restore row width and add another binding", () =>
            {
                // At 280px, three 80px buttons plus the row padding leave no label area.
                container.Width = width;
                row.KeyBindings.Add(new RealmKeyBinding(LongAction.Adjust, new KeyCombination(InputKey.E)));
            });
            AddUntilStep("label also leaves room for the new binding", () => row.ChildrenOfType<KeyBindingRow.KeyButton>().Count() == 3 && labelFits());

            bool labelFits()
            {
                TruncatingSpriteText label = row.ChildrenOfType<TruncatingSpriteText>().Single();
                KeyBindingRow.KeyButton[] buttons = row.ChildrenOfType<KeyBindingRow.KeyButton>().ToArray();
                return buttons.Length >= 2 && label.HandlePositionalInput
                                           && label.ScreenSpaceDrawQuad.AABBFloat.Right <= buttons.Min(button => button.ScreenSpaceDrawQuad.AABBFloat.Left);
            }
        }

        private const string long_action_description = "Gameplay: Cover / Hi-Speed (press to choose Sudden, Hidden or Lift to adjust; hold Start and press to switch between Normal and Floating Hi-Speed)";

        private enum LongAction
        {
            [System.ComponentModel.Description(long_action_description)]
            Adjust,
        }

        [Test]
        public void TestChangesAfterConstruction()
        {
            KeyBindingRow row = null!;

            AddStep("create row", () => Child = new Container
            {
                Width = 500,
                AutoSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = row = new KeyBindingRow(GlobalAction.Back)
                {
                    Defaults = new[]
                    {
                        new KeyCombination(InputKey.Escape),
                        new KeyCombination(InputKey.ExtraMouseButton1)
                    }
                }
            });

            AddStep("change key bindings", () =>
            {
                row.KeyBindings.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Escape)));
                row.KeyBindings.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.ExtraMouseButton1)));
            });
            AddUntilStep("revert to default button not shown", () => row.ChildrenOfType<SettingsRevertToDefaultButton>().Single().Alpha, () => Is.Zero);

            AddStep("change key bindings", () =>
            {
                row.KeyBindings.Clear();
                row.KeyBindings.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.X)));
                row.KeyBindings.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Z)));
                row.KeyBindings.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.I)));
            });
            AddUntilStep("revert to default button not shown", () => row.ChildrenOfType<SettingsRevertToDefaultButton>().Single().Alpha, () => Is.Not.Zero);
        }
    }
}
