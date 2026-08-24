// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mania;
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

        private sealed partial class HandoffTestLoader : PlayerLoader
        {
            internal bool HandoffPrepared { get; private set; }

            internal HandoffTestLoader(System.Func<Player> createPlayer)
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
