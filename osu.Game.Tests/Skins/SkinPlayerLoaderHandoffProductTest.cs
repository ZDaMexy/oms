// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Layout;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Tests.Skins
{
    [HeadlessTest]
    [TestFixture]
    public partial class SkinPlayerLoaderHandoffProductTest : ScreenTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notificationOverlay;

        [Cached]
        private readonly VolumeOverlay volumeOverlay;

        [Cached]
        private readonly OsuLogo logo;

        [Cached(typeof(BatteryInfo))]
        private readonly LocalBatteryInfo batteryInfo = new LocalBatteryInfo();

        [Cached]
        private readonly LeaderboardManager leaderboardManager;

        private readonly ChangelogOverlay changelogOverlay;

        public SkinPlayerLoaderHandoffProductTest()
        {
            AddRange(new Drawable[]
            {
                leaderboardManager = new LeaderboardManager(),
                notificationOverlay = new NotificationOverlay(),
                volumeOverlay = new VolumeOverlay(),
                changelogOverlay = new ChangelogOverlay(),
                logo = new OsuLogo
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Scale = new Vector2(0.5f),
                },
            });
        }

        [Test]
        public void TestExitDuringDelayedHandoffDisposesPlayerExactlyOnceAndDetachesRevision()
        {
            HandoffTestLoader loader = null!;
            TrackingPlayer player = null!;
            int participantBaseline = 0;

            AddStep("capture revision participant baseline", () =>
                participantBaseline = skinManager.CurrentRevision.ParticipantLeaseCount);
            AddStep("load production player loader", () =>
            {
                Ruleset.Value = new ManiaRuleset().RulesetInfo;
                SelectedMods.Value = Array.Empty<Mod>();
                Beatmap.Value = CreateWorkingBeatmap(new ManiaRuleset().RulesetInfo);
                LoadScreen(loader = new HandoffTestLoader(() => player = new TrackingPlayer()));
            });
            AddUntilStep("wait for loader current", () => loader.IsCurrentScreen());
            AddUntilStep("wait for exact delayed handoff ownership", () => loader.HandoffPrepared);
            AddAssert("player not pushed before handoff commit", () => player.Parent == null);
            AddStep("exit during handoff boundary", () => loader.Exit());
            AddUntilStep("wait for loader exit", () => !loader.IsCurrentScreen());
            AddUntilStep("handoff player disposed exactly once", () => player.DisposeCount == 1);
            AddUntilStep("live and temporary participants detached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
            AddAssert("no double dispose", () => player.DisposeCount, () => Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReadyPlayerWaitsForBackgroundOwnershipBeforePush(bool bms)
        {
            ImmediateTestLoader loader = null!;
            OwnershipGatedPlayer player = null!;
            double readyTime = 0;
            ModAutoplay autoplay = null!;

            AddStep("load player through production loader", () =>
            {
                Ruleset ruleset = bms ? new BmsRuleset() : new ManiaRuleset();
                Ruleset.Value = ruleset.RulesetInfo;
                autoplay = ruleset.GetAutoplayMod()!;
                SelectedMods.Value = new Mod[] { autoplay };
                Beatmap.Value = bms
                    ? CreateWorkingBeatmap(new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(
                        "#TITLE Loader ownership test\n#BPM 120\n#WAV01 note.wav\n#01011:01\n#01118:01\n", "loader-ownership.bme"))
                    {
                        BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    })
                    : CreateWorkingBeatmap(ruleset.RulesetInfo);
                LoadScreen(loader = new ImmediateTestLoader(() => player = new OwnershipGatedPlayer(autoplay)));
            });
            AddUntilStep("background player ready but still owned by load wrapper", () => player?.WrapperOwnsReadyPlayer.IsSet == true);
            AddStep("capture pending ownership time", () => readyTime = Clock.CurrentTime);
            AddUntilStep("wait beyond normal push debounce", () => Clock.CurrentTime - readyTime > 1000);
            AddStep("loader must retain screen until ownership transfer", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(loader.IsCurrentScreen(), Is.True, "A Ready player must not be pushed while its background wrapper still owns it.");
                        Assert.That(player.LoadState, Is.EqualTo(LoadState.Ready));
                        Assert.That(player.Parent, Is.Not.Null);
                        Assert.That(loader.LoadCompleted, Is.False);
                    });
                }
                finally
                {
                    player.ReleaseWrapper.Set();
                }
            });
            AddUntilStep("load callback finished", () => loader.LoadCompleted);
            AddAssert("native beatmap loaded successfully", () => player.LoadedBeatmapSuccessfully);
            AddUntilStep("ownership transferred and player entered", () => player.IsCurrentScreen() && player.IsLoaded);
            AddAssert("live stack now owns player", () => ReferenceEquals(player.Parent, Stack));
            AddStep("exit first play", () => player.Exit());
            AddUntilStep("first player disposed", () => player.DisposeCount == 1);
            AddStep("open same beatmap again", () =>
            {
                player = null!;
                LoadScreen(loader = new ImmediateTestLoader(() => player = new OwnershipGatedPlayer(autoplay)));
            });
            AddUntilStep("second ready player still owned by wrapper", () => player?.WrapperOwnsReadyPlayer.IsSet == true);
            AddStep("release second ownership", () => player.ReleaseWrapper.Set());
            AddUntilStep("second player entered", () => player.IsCurrentScreen() && player.IsLoaded);
            AddAssert("second player owned by live stack", () => ReferenceEquals(player.Parent, Stack));
        }

        private sealed partial class ImmediateTestLoader : PlayerLoader
        {
            internal bool LoadCompleted { get; private set; }

            internal ImmediateTestLoader(Func<Player> createPlayer)
                : base(createPlayer)
            {
            }

            protected override double PlayerPushDelay => 0;

            protected override bool ReadyForGameplay => true;

            protected override void OnPlayerLoaded()
            {
                base.OnPlayerLoaded();
                LoadCompleted = true;
            }
        }

        private sealed partial class OwnershipGatedPlayer : ReplayPlayer
        {
            internal readonly ManualResetEventSlim WrapperOwnsReadyPlayer = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim ReleaseWrapper = new ManualResetEventSlim();
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            internal OwnershipGatedPlayer(ModAutoplay autoplay)
                : base(autoplay.CreateScoreFromReplayData, new PlayerConfiguration { AllowPause = false, ShowResults = false })
            {
            }

            protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
            {
                bool result = base.OnInvalidate(invalidation, source);

                // The framework marks the child Ready before the async wrapper has finished loading and handed
                // it back on the update thread. Hold that real boundary beyond the normal push debounce.
                if (!ThreadSafety.IsUpdateThread && LoadState == LoadState.Ready && Parent != null
                    && (invalidation & Invalidation.Parent) != 0 && !WrapperOwnsReadyPlayer.IsSet)
                {
                    WrapperOwnsReadyPlayer.Set();
                    if (!ReleaseWrapper.Wait(TimeSpan.FromSeconds(15)))
                        throw new TimeoutException("The pending player wrapper ownership was not released.");
                }

                return result;
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
                WrapperOwnsReadyPlayer.Dispose();
                ReleaseWrapper.Dispose();
            }
        }

        private sealed partial class HandoffTestLoader : PlayerLoader
        {
            internal bool HandoffPrepared { get; private set; }

            internal HandoffTestLoader(Func<Player> createPlayer)
                : base(createPlayer)
            {
            }

            protected override double PlayerPushDelay => 0;

            protected override double PlayerHandoffDelay => 60_000;

            protected override bool ReadyForGameplay => true;

            protected override void OnPlayerHandoffPrepared(Player player)
            {
                base.OnPlayerHandoffPrepared(player);
                HandoffPrepared = true;
            }
        }

        private sealed partial class TrackingPlayer : TestPlayer
        {
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            internal TrackingPlayer()
                : base(false, false)
            {
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed class LocalBatteryInfo : BatteryInfo
        {
            public override bool OnBattery => false;

            public override double? ChargeLevel => null;
        }
    }
}
