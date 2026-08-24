// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneEditorScreenModes : EditorTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        [Test]
        public void TestSwitchScreensInstantaneously()
        {
            AddStep("switch between all screens at once", () =>
            {
                foreach (var screen in Enum.GetValues(typeof(EditorScreenMode)).Cast<EditorScreenMode>())
                    Editor.Mode.Value = screen;
            });
        }

        [Test]
        public void TestCancelledComposeAndTimelineLoadsReleaseRevisionParticipants()
        {
            int participantBaseline = 0;

            AddStep("switch away from compose", () => Editor.Mode.Value = EditorScreenMode.SongSetup);
            AddUntilStep("song setup ready", () => Editor.ReadyForUse);
            AddStep("remove cached compose", () => Editor.ReloadComposeScreen());
            AddUntilStep("cached compose detached", () => !Editor.ChildrenOfType<ComposeScreen>().Any());
            AddStep("capture non-compose participant baseline", () =>
                participantBaseline = skinManager.CurrentRevision.ParticipantLeaseCount);
            AddStep("cancel compose screen before outer callback", () =>
            {
                Editor.Mode.Value = EditorScreenMode.Compose;
                Editor.Mode.Value = EditorScreenMode.Verify;
            });
            AddUntilStep("latest verify screen ready", () =>
                Editor.Mode.Value == EditorScreenMode.Verify && Editor.ReadyForUse);
            AddUntilStep("outer provisional participants reclaimed", () =>
                !Editor.ChildrenOfType<ComposeScreen>().Any()
                && skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
            AddStep("start a real compose host", () => Editor.Mode.Value = EditorScreenMode.Compose);
            AddUntilStep("compose screen begins loading children", () =>
                Editor.ChildrenOfType<ComposeScreen>().SingleOrDefault()?.IsLoaded == true);
            AddStep("dispose compose and cancel replacement", () =>
            {
                Editor.ReloadComposeScreen();
                Editor.Mode.Value = EditorScreenMode.Verify;
            });
            AddUntilStep("replacement cancelled and latest screen ready", () =>
                Editor.Mode.Value == EditorScreenMode.Verify
                && Editor.ReadyForUse
                && !Editor.ChildrenOfType<ComposeScreen>().Any());
            AddUntilStep("nested main and timeline participants reclaimed", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
        }
    }
}
