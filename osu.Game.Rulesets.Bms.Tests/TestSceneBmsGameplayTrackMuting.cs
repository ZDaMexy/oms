// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    /// <summary>
    /// Regression coverage for the BMS song-select-preview-playing-during-gameplay bug: a BMS chart's imported audio file
    /// (a #PREVIEW file or a large unreferenced audio file detected at import) is only meant to be a song-select preview.
    /// BMS gameplay audio is entirely keysound-driven, so the beatmap track must NOT be heard during gameplay — otherwise
    /// the preview is heard over the keysounds at the start of the chart. <see cref="BmsRuleset"/> opts out of playing the
    /// beatmap track via <see cref="Ruleset.PlayBeatmapTrackDuringGameplay"/>, and <see cref="MasterGameplayClockContainer"/>
    /// honours that by muting the track (it still drives the gameplay clock, so timing is unaffected).
    /// </summary>
    [HeadlessTest]
    public partial class TestSceneBmsGameplayTrackMuting : OsuTestScene
    {
        private OsuConfigManager localConfig = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(localConfig = new OsuConfigManager(LocalStorage));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset audio offset", () => localConfig.SetValue(OsuSetting.AudioOffset, 0.0));
        }

        [Test]
        public void TestBmsBeatmapTrackMutedDuringGameplay()
        {
            ClockBackedTestWorkingBeatmap working = null!;
            MasterGameplayClockContainer gameplayClockContainer = null!;

            AddStep("create container for bms beatmap", () =>
            {
                working = new ClockBackedTestWorkingBeatmap(new BmsRuleset().RulesetInfo, new FramedClock(new ManualClock()), Audio);
                Child = gameplayClockContainer = new MasterGameplayClockContainer(working, 0);
                gameplayClockContainer.Reset(startClock: true);
            });

            // The beatmap track still drives the gameplay clock (so keysound timing is unaffected), but it is muted so the
            // imported song-select preview/music file is never heard over the keysounds.
            AddUntilStep("track started", () => working.Track.IsRunning);
            AddAssert("beatmap track muted", () => working.Track.AggregateVolume.Value, () => Is.EqualTo(0));
        }

        [Test]
        public void TestStandardBeatmapTrackAudibleDuringGameplay()
        {
            ClockBackedTestWorkingBeatmap working = null!;
            MasterGameplayClockContainer gameplayClockContainer = null!;

            // Positive control: a ruleset that does NOT opt out keeps the beatmap track audible. This proves the assertion
            // above genuinely discriminates (the mute is BMS-specific, not applied to every beatmap).
            AddStep("create container for standard beatmap", () =>
            {
                working = new ClockBackedTestWorkingBeatmap(new TrackPlayingRuleset().RulesetInfo, new FramedClock(new ManualClock()), Audio);
                Child = gameplayClockContainer = new MasterGameplayClockContainer(working, 0);
                gameplayClockContainer.Reset(startClock: true);
            });

            AddUntilStep("track started", () => working.Track.IsRunning);
            AddAssert("beatmap track audible", () => working.Track.AggregateVolume.Value, () => Is.EqualTo(1));
        }

        protected override void Dispose(bool isDisposing)
        {
            localConfig?.Dispose();
            base.Dispose(isDisposing);
        }

        /// <summary>
        /// A minimal ruleset that keeps the default <see cref="Ruleset.PlayBeatmapTrackDuringGameplay"/> (true).
        /// </summary>
        private class TrackPlayingRuleset : Ruleset
        {
            public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();

            public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null) => throw new NotImplementedException();

            public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => throw new NotImplementedException();

            public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => throw new NotImplementedException();

            public override string Description => "track-playing test ruleset";
            public override string ShortName => "test-track-playing";
        }
    }
}
