// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Tests.Visual.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneLocalPlayerExit : PlayerTestScene
    {
        private bool bms;

        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => bms ? new BmsRuleset() : new ManiaRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
            => bms
                ? new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText("#TITLE Local exit\n#BPM 120\n#03011:01\n", "exit.bme"))
                {
                    BeatmapInfo = { Ruleset = ruleset },
                }
                : new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset },
                    Difficulty = { CircleSize = 4 },
                    HitObjects = { new Note { StartTime = 60_000 } },
                };

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(false, false);

        [TestCase(false)]
        [TestCase(true)]
        public void TestOfflineQuitDoesNotCloneAnUnusedLongReplay(bool useBms)
        {
            CreateTest(() => AddStep("choose the local gameplay", () => bms = useBms));
            const int frame_count = 131_072;
            long allocated = 0;

            AddStep("represent a long recorded session", () =>
            {
                ReplayFrame frame = bms ? new BmsReplayFrame(0) : new ManiaReplayFrame(0);
                Player.Score.Replay.Frames.Clear();
                for (int i = 0; i < frame_count; i++)
                    Player.Score.Replay.Frames.Add(frame);
            });
            AddStep("quit through the actual submitting player", () =>
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                Player.Exit();
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            });
            AddAssert("a frozen legacy online path never copies the replay for a no-op submission", () =>
            {
                TestContext.Progress.WriteLine($"offline-quit: mode={(bms ? "bms" : "mania")} frames={frame_count} allocated={allocated}");
                return allocated < 256 * 1024;
            });
            AddAssert("the original replay remains available", () => Player.Score.Replay.Frames.Count >= frame_count);
            AddUntilStep("the player has exited", () => !Player.IsCurrentScreen());
        }
    }
}
