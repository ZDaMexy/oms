// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsBgaSharedContent
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestSeekOrResetCannotReplayAnOldPoorFlash(bool reset)
        {
            BmsBgaPlaybackSession session = null!;
            GameplayClockContainer gameplay = null!;
            var source = new BgaSeekClock { Rate = 0 };

            AddStep("mount the real BGA session under the gameplay discontinuity owner", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K } };
                var working = new BgaWorkingBeatmap(beatmap);
                session = new BmsBgaPlaybackSession(new[]
                {
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "base.png", false),
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Poor, "poor.png", false),
                }, BmsPoorBgaMode.Default);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(working)),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Child = gameplay = new GameplayClockContainer(source, applyOffsets: false, requireDecoupling: false)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = session,
                    },
                };
                session.Clock = gameplay;
            });
            AddUntilStep("the BGA player is loaded", () => session?.Player?.IsLoaded == true);
            AddStep("start gameplay", () => gameplay.Start());
            AddUntilStep("gameplay is running", () => gameplay.IsRunning);
            AddStep("advance to the real miss time", () => source.CurrentTime = 1000);
            AddUntilStep("BGA reaches the miss time", () => Math.Abs(session.Player!.Time.Current - 1000) < 0.1);
            AddStep("trigger the original miss", () => session.NotifyMiss());
            AddUntilStep("POOR is visibly active", () => poorAlpha(session.Player!) == 1);
            AddStep(reset ? "reset the gameplay epoch" : "seek before the old miss", () =>
            {
                if (reset)
                    gameplay.Reset(0, startClock: true);
                else
                    gameplay.Seek(0);
            });
            AddUntilStep("BGA clock returns to the beginning", () => Math.Abs(session.Player!.Time.Current) < 0.1);
            AddStep("cross the old miss time without a new miss", () => source.CurrentTime = 1000);
            AddUntilStep("BGA reaches the same timestamp in the new epoch", () => Math.Abs(session.Player!.Time.Current - 1000) < 0.1);
            AddAssert("the previous epoch's POOR cannot reappear", () => poorAlpha(session.Player!) == 0);
            AddStep("a genuine new miss still triggers POOR", () => session.NotifyMiss());
            AddUntilStep("the new miss is displayed", () => poorAlpha(session.Player!) == 1);
        }

        [Test]
        public void TestPendingVideoRetriesImmediatelyAfterBackwardSeekInsideTheSameEntry()
        {
            BmsBgaPlaybackSession session = null!;
            GameplayClockContainer gameplay = null!;
            var source = new BgaSeekClock { Rate = 0 };
            int attempts = 0;
            int attemptsBeforeSeek = 0;

            AddStep("mount a single pending-video entry under the actual gameplay clock", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K } };
                session = new BmsBgaPlaybackSession(new[]
                {
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "pending.mp4", true),
                }, BmsPoorBgaMode.Default);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(new BgaWorkingBeatmap(beatmap))),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Child = gameplay = new GameplayClockContainer(source, applyOffsets: false, requireDecoupling: false)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = session,
                    },
                };
                session.Clock = gameplay;
            });
            AddUntilStep("the single-entry BGA player has loaded", () => session?.Player?.IsLoaded == true);
            AddStep("supply a deterministic pending transcode resolver", () =>
            {
                object layer = typeof(BmsBgaPlayer).GetField("baseLayer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Player!)!;
                ((IDictionary)layer.GetType().GetField("videosByFile", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(layer)!).Clear();
                layer.GetType().GetMethod("SetResolvers")!.Invoke(layer, new object?[]
                {
                    (Func<string, Texture?>)(_ => null),
                    (Func<string, (Video?, bool)>)(_ => { attempts++; return (null, true); }),
                    null,
                });
                gameplay.Start();
            });
            AddUntilStep("the pending-video clock is running", () => gameplay.IsRunning);
            AddStep("advance beyond the initial retry window", () => source.CurrentTime = 5000);
            AddUntilStep("the pending source is checked at five seconds", () => attempts > 0 && Math.Abs(session.Player!.Time.Current - 5000) < 0.1);
            AddStep("seek backwards without selecting a different video entry", () =>
            {
                attemptsBeforeSeek = attempts;
                gameplay.Seek(1000);
            });
            AddUntilStep("the same video entry is at its new playback time", () => Math.Abs(session.Player!.Time.Current - 1000) < 0.1);
            AddWaitStep("allow the next presentation update", 2);
            AddAssert("the pending cache is rechecked in the new epoch", () => attempts > attemptsBeforeSeek);
            AddAssert("the content timeline retains its original entry", () => session.ContentRevision == 1
                && session.ContentState == GameplaySkinBgaContentState.Playing);
        }

        private static float poorAlpha(BmsBgaPlayer player)
            => ((Drawable)typeof(BmsBgaPlayer).GetField("poorLayer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(player)!).Alpha;

        private class BgaSeekClock : ManualClock, IAdjustableClock
        {
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;

            public bool Seek(double position)
            {
                CurrentTime = position;
                return true;
            }

            public void Reset()
            {
                IsRunning = false;
                CurrentTime = 0;
            }

            public void ResetSpeedAdjustments() => Rate = 0;
        }
    }
}
