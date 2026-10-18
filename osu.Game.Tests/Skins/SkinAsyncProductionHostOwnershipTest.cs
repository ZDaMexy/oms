// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Drawables;
using osu.Game.Tests.Visual;
using osu.Game.Tests.NonVisual.Skinning;
using osuTK;

namespace osu.Game.Tests.Skins
{
    [HeadlessTest]
    [TestFixture]
    public partial class SkinAsyncProductionHostOwnershipTest : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private bool originalShowStoryboard;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("disable storyboard before host load", () =>
            {
                originalShowStoryboard = config.Get<bool>(OsuSetting.ShowStoryboard);
                config.SetValue(OsuSetting.ShowStoryboard, false);
            });
        }

        [TearDownSteps]
        public void TearDownSteps() =>
            AddStep("restore storyboard setting", () => config.SetValue(OsuSetting.ShowStoryboard, originalShowStoryboard));

        [Test]
        public void TestDimmableStoryboardDisposeDuringAsyncSkinLoadReclaimsParticipantAndRetries()
        {
            DimmableStoryboard first = null!;
            DimmableStoryboard retry = null!;
            int participantBaseline = 0;

            AddStep("mount production dimmable storyboard", () => Add(first = createHost()));
            AddUntilStep("wait for disabled host", () => first.IsLoaded);
            AddStep("capture participant baseline", () =>
                participantBaseline = skinManager.CurrentRevision.ParticipantLeaseCount);
            AddStep("start async skin storyboard and dispose before callback", () =>
            {
                config.SetValue(OsuSetting.ShowStoryboard, true);
                Remove(first, disposeImmediately: true);
            });
            AddUntilStep("provisional storyboard participant reclaimed", () =>
                first.Parent == null
                && skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
            AddStep("prepare retry host with storyboard disabled", () =>
            {
                config.SetValue(OsuSetting.ShowStoryboard, false);
                Add(retry = createHost());
            });
            AddUntilStep("wait for retry host", () => retry.IsLoaded);
            AddStep("start retry", () => config.SetValue(OsuSetting.ShowStoryboard, true));
            AddUntilStep("retry owns loaded skin storyboard", () =>
                retry.ChildrenOfType<DrawableStoryboard>().SingleOrDefault()?.IsLoaded == true
                && skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline + 1);
            AddStep("dispose retry host", () => Remove(retry, disposeImmediately: true));
            AddUntilStep("retry participant detached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
        }

        [TestCase("normal")]
        [TestCase("participant-shutdown")]
        [TestCase("worker-shutdown")]
        [TestCase("dispose")]
        public void TestTerminalSkinnableOwnerCannotBeRoutedByALateSiblingHudCallback(string terminal)
        {
            Container root = null!;
            Container partitions = null!;
            ManualSkinnableContainer first = null!;
            ManualSkinnableContainer second = null!;
            TrackingHudSource firstSource = null!;
            TrackingHudSource secondSource = null!;
            Container firstContent = null!;
            Container secondContent = null!;
            GameplaySkinHudProgrammaticVisualAdapter adapter = null!;
            GameplaySkinSceneRuntimeHost runtime = null!;
            GameplaySkinEventStream stream = null!;
            Texture texture = null!;
            SkinCurrentRevision revision = null!;
            Task shutdown = Task.CompletedTask;
            int participantBaseline = 0;
            int siblingPublications = 0;
            var heldCallbacks = new Scheduler();

            AddStep("mount two actual skinnable HUD owners", () =>
            {
                revision = skinManager.CurrentRevision;
                participantBaseline = revision.ParticipantLeaseCount;
                Add(root = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        first = new ManualSkinnableContainer(),
                        second = new ManualSkinnableContainer(),
                        partitions = new Container { RelativeSizeAxes = Axes.Both },
                    },
                });
            });
            AddUntilStep("both real revision participants finish loading", () => first.IsLoaded && second.IsLoaded
                && revision.WorkDetached.IsCompleted && revision.ParticipantLeaseCount == participantBaseline + 2);
            AddStep("publish the first source and hold the real sibling callback", () =>
            {
                first.Reload(firstContent = new Container { Child = firstSource = new TrackingHudSource() });
                second.ContentLoadCallbackScheduler = heldCallbacks;
                second.OnComponentsLoaded += _ => siblingPublications++;
                second.Reload(secondContent = new Container { Child = secondSource = new TrackingHudSource() });
            });
            AddUntilStep("first source is ready while sibling is queued", () => first.ComponentsLoaded
                && second.PendingContentLoadTask?.IsCompleted == true && heldCallbacks.HasPendingTasks);
            AddStep("subscribe the real prepared HUD adapter before terminal retirement", () =>
            {
                texture = new DummyRenderer().CreateTexture(2, 2);
                GameplaySkinLayoutPublication publication = createTextRoutingPublication(texture);
                Assert.That(publication.PreparedScene.HudPlan.GetRole(GameplaySkinPreparedHudRole.Text).RequiresRouting, Is.True);
                stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
                runtime = new GameplaySkinSceneRuntimeHost(publication, stream);
                adapter = new GameplaySkinHudProgrammaticVisualAdapter(runtime, new[] { first, second }, partitions);
                Assert.That(second.ComponentsLoaded, Is.False);
                Assert.That(partitions.Children, Is.Empty);

                if (terminal == "dispose")
                    Assert.That(root.Remove(first, disposeImmediately: true), Is.True);
                else if (terminal == "participant-shutdown")
                    requestParticipantShutdown(first);
                else if (terminal == "worker-shutdown")
                    shutdown = Task.Run(() => requestParticipantShutdown(first));
            });
            AddUntilStep("the actual terminal callback completes before the sibling publishes", () => shutdown.IsCompleted);
            AddStep("late sibling callback cannot claim the terminal owner's stale components", () =>
            {
                shutdown.GetAwaiter().GetResult();
                if (terminal != "normal")
                {
                    Assert.Throws<NotSupportedException>(() => first.Remove(firstSource, disposeImmediately: false),
                        "Remove remains strict; the adapter must not call it for an unavailable owner.");
                }
                // Before the fix this real callback enters the HUD adapter and attempts Remove on first's null
                // content, reproducing the exact full-run exception rather than only checking a readiness flag.
                Assert.DoesNotThrow(() => heldCallbacks.Update());
                heldCallbacks.Update();
            });
            AddUntilStep("the sibling publication releases its exact asynchronous work", () => second.ComponentsLoaded
                && siblingPublications == 1 && revision.WorkDetached.IsCompleted);
            AddStep("assert native source lifetime and real HUD routing", () =>
            {
                if (terminal == "normal")
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(first.ComponentsLoaded, Is.True);
                        Assert.That(adapter.TextPartitions, Has.Count.EqualTo(2));
                        Assert.That(partitions.Children, Is.Not.Empty);
                        Assert.That(first.Components, Is.Empty);
                        Assert.That(second.Components, Is.Empty);
                        Assert.That(firstSource.Parent, Is.SameAs(secondSource.Parent));
                        Assert.That(firstSource.Parent, Is.Not.SameAs(firstContent));
                        Assert.That(firstSource.DisposeCount, Is.Zero);
                        Assert.That(secondSource.DisposeCount, Is.Zero);
                    });
                }
                else
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(first.ComponentsLoaded, Is.False);
                        Assert.That(second.ComponentsLoaded, Is.True);
                        Assert.That(adapter.TextPartitions, Is.Empty);
                        Assert.That(partitions.Children, Is.Empty);
                        Assert.That(second.Components.Single(), Is.SameAs(secondSource));
                        Assert.That(secondSource.Parent, Is.SameAs(secondContent));
                        Assert.That(secondSource.DisposeCount, Is.Zero);
                    });
                    if (terminal != "dispose")
                    {
                        Assert.That(first.Components.Single(), Is.SameAs(firstSource));
                        Assert.That(firstSource.Parent, Is.SameAs(firstContent), "Readiness revocation must not steal or dispose the retained visual.");
                        Assert.That(firstSource.DisposeCount, Is.Zero);
                    }
                }

                adapter.Dispose();
                Assert.That(Remove(root, disposeImmediately: true), Is.True);
                runtime.Dispose();
                stream.Dispose();
                texture.Dispose();
            });
            AddUntilStep("both real visual participants detach without leaked work", () => revision.WorkDetached.IsCompleted
                && revision.ParticipantLeaseCount == participantBaseline && firstSource.DisposeCount == 1 && secondSource.DisposeCount == 1);
        }

        private static void requestParticipantShutdown(SkinnableContainer owner)
        {
            // Invoke the exact terminal callback registered with SkinManager, without shutting down the scene's
            // unrelated test services. Content, readiness and generation are never forged by this fixture.
            typeof(SkinReloadableDrawable).GetMethod("requestRevisionParticipantShutdown", BindingFlags.Instance | BindingFlags.NonPublic)!
                                         .Invoke(owner, null);
        }

        private static GameplaySkinLayoutPublication createTextRoutingPublication(Texture texture)
        {
            GameplaySkinLayoutPublication basis = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(texture, false);
            GameplaySkinSlotDescriptor slot = GameplaySkinSlotCatalog.TextHud;
            GameplaySkinResolvedMaterialSourceIdentity source = GameplaySkinResolvedMaterialSourceIdentity.CreateSelectedDocument("selected-hud", "hud-revision");
            GameplaySkinPublicSlotMaterial material = GameplaySkinPublicSlotMaterial.FromPreparedResource(slot, "test/hud", texture);
            GameplaySkinResolvedMaterialSet materials = GameplaySkinResolvedMaterialSet.Create(
                basis.Snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(basis.Snapshot),
                new[]
                {
                    GameplaySkinResolvedMaterialEntry.Provide(slot, GameplaySkinResolvedMaterialTarget.Global, source, material),
                    GameplaySkinResolvedMaterialEntry.Provide(slot,
                        GameplaySkinResolvedMaterialTarget.ForStage(basis.Snapshot.Context.Topology.GroupsInLogicalOrder.Single()), source, material),
                });
            return GameplaySkinLayoutPublication.Create(basis.Adapter, materials, GameplaySkinPreparedScene.CreateEmpty(basis.Snapshot, materials));
        }

        private sealed partial class ManualSkinnableContainer : SkinnableContainer
        {
            public ManualSkinnableContainer()
                : base(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))
            {
                RelativeSizeAxes = Axes.Both;
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                // This fixture provides each real Reload(Container) explicitly to hold only the sibling callback.
            }
        }

        private sealed partial class TrackingHudSource : Container, ISerialisableDrawable, IGameplaySkinHudProgrammaticVisualSource
        {
            public bool UsesFixedAnchor { get; set; }

            public GameplaySkinPreparedHudRole GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;

            public int DisposeCount { get; private set; }

            protected override void Dispose(bool isDisposing)
            {
                DisposeCount++;
                base.Dispose(isDisposing);
            }
        }

        private static DimmableStoryboard createHost() =>
            new DimmableStoryboard(createSkinStoryboard(), Array.Empty<Mod>())
            {
                RelativeSizeAxes = Axes.Both,
            };

        private static Storyboard createSkinStoryboard()
        {
            var storyboard = new Storyboard { UseSkinSprites = true };
            var sprite = new StoryboardSprite("Menu/fountain-star", Anchor.Centre, Vector2.Zero);
            sprite.Commands.AddAlpha(Easing.None, 0, 60_000, 1, 1);
            storyboard.Layers.Last().Add(sprite);
            return storyboard;
        }
    }
}
