// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestLateAuthorizationNotificationPreservesAlreadyConsumedRealEngineEvents()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            GameplaySkinScriptInstance freshBms = null!;
            GameplaySkinScriptInstance freshMania = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            Task? permission = null;
            var notificationEntered = new ManualResetEventSlim();
            var releaseNotification = new ManualResetEventSlim();
            Action delayNotification = () =>
            {
                notificationEntered.Set();
                Assert.That(releaseNotification.Wait(TimeSpan.FromSeconds(30)), Is.True, "The test must release the delayed worker notification.");
            };
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                string path = Path.Combine(root, "gameplay-skin.script");
                File.WriteAllText(path, File.ReadAllText(path).Replace("required scene.numeric.write",
                    "required scene.numeric.write\noptional math.random.read", StringComparison.Ordinal));
            });
            AddStep("grant the actual event script", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                permission = grantC6Script(authorization);
            });
            AddUntilStep("initial grants complete", () => permission?.IsCompleted == true);
            AddStep("subscribe the controlled worker delay before actual host observers attach", () =>
            {
                permission!.GetAwaiter().GetResult();
                authorization.Changed += delayNotification;
                renderer = new ExactLayoutJourneyHost(manager);
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both actual host observers attach", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("publish changed rights while delaying only their notification", () =>
                permission = authorization.SetAsync("math.random.read", GameplaySkinScriptAuthorizationChoice.Granted));
            AddUntilStep("the atomic decision is visible before its observer notification", () =>
                notificationEntered.IsSet && authorization.IsGranted("math.random.read"));
            AddStep("active gameplay consumes fresh input under the new decision", () =>
            {
                c6Input(renderer, true);
                clock.Sample(1_000);
                freshBms = bms.ScriptInstance!;
                freshMania = mania.ScriptInstance!;
                Assert.That(c6Rotation(bms), Is.EqualTo(15));
                Assert.That(c6Rotation(mania), Is.EqualTo(15));
            });
            AddStep("release the now-late authorization notification", () => releaseNotification.Set());
            AddUntilStep("the worker finishes its already-applied decision", () => permission?.IsCompleted == true);
            AddWaitStep("allow the real game-host notification scheduler to run", 2);
            AddStep("a late notification cannot erase either freshly consumed event", () =>
            {
                permission!.GetAwaiter().GetResult();
                Assert.That(bms.ScriptInstance, Is.SameAs(freshBms));
                Assert.That(mania.ScriptInstance, Is.SameAs(freshMania));
                Assert.That(c6Rotation(bms), Is.EqualTo(15));
                Assert.That(c6Rotation(mania), Is.EqualTo(15));
                authorization.Changed -= delayNotification;
                notificationEntered.Dispose();
                releaseNotification.Dispose();
                c6Input(renderer, false);
                renderer.Expire();
            });
            AddUntilStep("both delayed-notification consumers leave their revision", () => renderer.Parent == null);
        }

        [Test]
        public void TestOptionalSnapshotGrantAndRevocationControlActualGameplayTickDelivery()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            Task? permission = null;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                File.WriteAllText(Path.Combine(root, "gameplay-skin.script"), """
                    oms-script 1
                    optional gameplay.snapshot.read
                    deny gameplay.events.read
                    required scene.numeric.write
                    state callbacks 0
                    target node.glow rotation
                    add callbacks callbacks 1
                    set node.glow rotation callbacks
                    halt
                    """);
            });
            AddStep("grant scene writes without either gameplay read channel", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                permission = authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
            });
            AddUntilStep("scene grant completes", () => permission?.IsCompleted == true);
            AddStep("mount both actual clock consumers", () =>
            {
                permission!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager);
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both author scenes are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("clock progression cannot leak through unauthorized callbacks", () =>
            {
                clock.Sample(1_101);
                Assert.That(bms.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(mania.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(c6Rotation(bms), Is.Zero);
                Assert.That(c6Rotation(mania), Is.Zero);
            });
            AddStep("grant the optional snapshot clock channel", () => permission = authorization.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted));
            AddUntilStep("snapshot grant completes", () => permission?.IsCompleted == true);
            AddStep("authorized gameplay ticks now reach both scripts", () =>
            {
                permission!.GetAwaiter().GetResult();
                clock.Sample(1_201);
                Assert.That(bms.ScriptInstance!.Profiler.Callbacks, Is.GreaterThan(0));
                Assert.That(mania.ScriptInstance!.Profiler.Callbacks, Is.GreaterThan(0));
                Assert.That(c6Rotation(bms), Is.GreaterThan(0));
                Assert.That(c6Rotation(mania), Is.GreaterThan(0));
            });
            AddStep("revoke snapshot access with gameplay mounted", () => permission = authorization.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.NotDecided));
            AddUntilStep("snapshot revocation completes", () => permission?.IsCompleted == true);
            AddStep("clock progression no longer reaches either author program", () =>
            {
                permission!.GetAwaiter().GetResult();
                clock.Sample(1_301);
                Assert.That(bms.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(mania.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(c6Rotation(bms), Is.Zero);
                Assert.That(c6Rotation(mania), Is.Zero);
                Assert.That(bms.ScriptStatus, Is.EqualTo("running"));
                Assert.That(mania.ScriptStatus, Is.EqualTo("running"));
                renderer.Expire();
            });
            AddUntilStep("both read-channel consumers release their revision", () => renderer.Parent == null);
        }

        [Test]
        public void TestSettingsRevocationDisablesBothPausedScriptTreesWithoutResumingOrReloading()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinScriptSettings settings = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            GameplaySkinScriptInstance oldBms = null!;
            GameplaySkinScriptInstance oldMania = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            Task? permission = null;
            long bmsSequence = 0;
            long maniaSequence = 0;
            long bmsCallbacks = 0;
            long maniaCallbacks = 0;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, writeC6ScriptPackage);
            AddStep("grant the exact package before real gameplay attaches", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                permission = grantC6Script(authorization);
            });
            AddUntilStep("all requested grants complete", () => permission?.IsCompleted == true);
            AddStep("mount both actual gameplay trees and the real settings caller", () =>
            {
                permission!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager);
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("both actual scripts and settings controls are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                settings ??= caller.Section.ChildrenOfType<SkinScriptSettings>().SingleOrDefault()!;
                return bms.IsSceneReady && mania.IsSceneReady && settings?.IsLoaded == true
                       && settings.ChildrenOfType<SettingsButtonV2>().Count() == authorization.Requests.Count * 3;
            });
            AddStep("actual input produces both script overlays", () =>
            {
                c6Input(renderer, true);
                clock.Sample(1_000);
            });
            AddUntilStep("both script overlays are visible", () => c6Rotation(bms) == 15 && c6Rotation(mania) == 15);
            AddStep("pause the actual gameplay clock and freeze both native subtrees", () =>
            {
                clock.Stop();
                oldBms = bms.ScriptInstance!;
                oldMania = mania.ScriptInstance!;
                bmsSequence = bms.LastSequence;
                maniaSequence = mania.LastSequence;
                bmsCallbacks = oldBms.Profiler.Callbacks;
                maniaCallbacks = oldMania.Profiler.Callbacks;
            });
            AddStep("revoke writes with the real settings button while still paused", () =>
                c6PermissionButton(settings, authorization, "scene.numeric.write", 2).TriggerClick());
            AddUntilStep("both old scripts and overlays lose authority without a gameplay traversal", () =>
                !authorization.IsGranted("scene.numeric.write")
                && !oldBms.IsEnabled && !oldMania.IsEnabled
                && c6Rotation(bms) == 0 && c6Rotation(mania) == 0);
            AddStep("revocation did not resume clocks, consume events, execute authors, or reload", () =>
            {
                Assert.That(clock.IsPaused.Value, Is.True);
                Assert.That(bms.LastSequence, Is.EqualTo(bmsSequence));
                Assert.That(mania.LastSequence, Is.EqualTo(maniaSequence));
                Assert.That(oldBms.Profiler.Callbacks, Is.EqualTo(bmsCallbacks));
                Assert.That(oldMania.Profiler.Callbacks, Is.EqualTo(maniaCallbacks));
                Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization, Is.SameAs(authorization));
            });
            AddStep("regrant writes while the gameplay trees remain paused", () =>
                permission = authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted));
            AddUntilStep("both paused hosts admit new authorized instances", () => permission?.IsCompleted == true
                && bms.ScriptInstance?.IsEnabled == true && mania.ScriptInstance?.IsEnabled == true);
            AddStep("rapidly revoke and regrant the same permission", () =>
            {
                permission!.GetAwaiter().GetResult();
                oldBms = bms.ScriptInstance!;
                oldMania = mania.ScriptInstance!;
                permission = Task.WhenAll(
                    authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.NotDecided),
                    authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted));
            });
            AddUntilStep("returning to the same rights never preserves a revoked script instance", () =>
                permission?.IsCompleted == true && authorization.RequiredSatisfied
                && !oldBms.IsEnabled && !oldMania.IsEnabled
                && bms.ScriptInstance?.IsEnabled == true && mania.ScriptInstance?.IsEnabled == true
                && !ReferenceEquals(bms.ScriptInstance, oldBms) && !ReferenceEquals(mania.ScriptInstance, oldMania));
            AddStep("the safety notification still did not resume gameplay or deliver callbacks", () =>
            {
                permission!.GetAwaiter().GetResult();
                Assert.That(clock.IsPaused.Value, Is.True);
                Assert.That(bms.LastSequence, Is.EqualTo(bmsSequence));
                Assert.That(mania.LastSequence, Is.EqualTo(maniaSequence));
                Assert.That(bms.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(mania.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                renderer.Expire();
                caller.Expire();
            });
            AddUntilStep("paused consumers leave the exact revision", () => renderer.Parent == null && caller.Parent == null);
        }

        [Test]
        public void TestScriptEqualTimeTickFollowsBothActualJudgementProducers()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            Task? authorization = null;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                File.WriteAllText(Path.Combine(root, "gameplay-skin.script"), """
                    oms-script 1
                    required gameplay.snapshot.read
                    required gameplay.events.read
                    required scene.numeric.write
                    state kind 0
                    state condition 0
                    state time 0
                    state judgements 0
                    target node.glow rotation
                    read kind event-kind
                    eq condition kind 40
                    when condition judged
                    eq condition kind -1
                    when condition tick
                    halt
                    judged:
                    add judgements judgements 1
                    halt
                    tick:
                    read time time
                    eq condition time 2000
                    when condition project
                    halt
                    project:
                    set node.glow rotation judgements
                    halt
                    """);
            });
            AddStep("grant the actual package", () => authorization = grantC6Script(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!));
            AddUntilStep("grant completes", () => authorization?.IsCompleted == true);
            AddStep("mount both actual note producers", () =>
            {
                authorization!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager);
                renderer.ManiaDrawable.Beatmap.HitObjects.OfType<Note>().Single().StartTime = 2_000;
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both actual scenes are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("reach the exact judgement timestamp before input is produced", () =>
            {
                for (int time = 1_050; time <= 2_000; time += 50)
                    clock.Sample(time);
            });
            AddStep("produce both real judgements after the same-time scene traversal", () =>
            {
                c6Input(renderer, true);
                clock.Sample(2_001);
            });
            AddAssert("the 2000ms tick includes the BMS judgement at 2000ms", () => c6Rotation(bms) == 1);
            AddAssert("the 2000ms tick includes the mania judgement at 2000ms", () => c6Rotation(mania) == 1);
            AddStep("detach the actual note producers", () =>
            {
                c6Input(renderer, false);
                renderer.Expire();
            });
            AddUntilStep("both producers release their exact revision", () => renderer.Parent == null);
        }

        [Test]
        public void TestOptionalEventGrantAndRevocationControlActualCallbackDelivery()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            Task? permission = null;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                File.WriteAllText(Path.Combine(root, "gameplay-skin.script"), """
                    oms-script 1
                    required gameplay.snapshot.read
                    optional gameplay.events.read
                    required scene.numeric.write
                    state callbacks 0
                    target node.glow rotation
                    add callbacks callbacks 1
                    set node.glow rotation callbacks
                    halt
                    """);
            });
            AddStep("grant only required snapshots and scene writes", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                permission = grantC6RequiredWithoutEvents(authorization);
            });
            AddUntilStep("required grants complete", () => permission?.IsCompleted == true);
            AddStep("mount both actual producers at a frozen gameplay time", () =>
            {
                permission!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager);
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both optional-event scripts are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("publish real input edges without event authorization", () =>
            {
                c6Input(renderer, true);
                // Mania projects its actual pressed state during update; an entire press/release between
                // traversals intentionally produces no edge in that existing engine contract.
                clock.Sample(1_000);
                c6Input(renderer, false);
                clock.Sample(1_000);
                Assert.That(bms.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(mania.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(c6Rotation(bms), Is.Zero);
                Assert.That(c6Rotation(mania), Is.Zero);
            });
            AddStep("grant optional event delivery", () => permission = authorization.SetAsync("gameplay.events.read", GameplaySkinScriptAuthorizationChoice.Granted));
            AddUntilStep("event grant completes", () => permission?.IsCompleted == true);
            AddStep("the same real input now reaches both scripts", () =>
            {
                permission!.GetAwaiter().GetResult();
                c6Input(renderer, true);
                clock.Sample(1_000);
                c6Input(renderer, false);
                clock.Sample(1_000);
                Assert.That(bms.ScriptInstance!.Profiler.Callbacks, Is.GreaterThan(0));
                Assert.That(mania.ScriptInstance!.Profiler.Callbacks, Is.GreaterThan(0));
                Assert.That(c6Rotation(bms), Is.GreaterThan(0));
                Assert.That(c6Rotation(mania), Is.GreaterThan(0));
            });
            AddStep("revoke optional events while gameplay stays mounted", () => permission = authorization.SetAsync("gameplay.events.read", GameplaySkinScriptAuthorizationChoice.NotDecided));
            AddUntilStep("event revocation completes", () => permission?.IsCompleted == true);
            AddStep("old scripts lose actual event delivery", () =>
            {
                permission!.GetAwaiter().GetResult();
                c6Input(renderer, true);
                clock.Sample(1_000);
                c6Input(renderer, false);
                clock.Sample(1_000);
                Assert.That(bms.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(mania.ScriptInstance!.Profiler.Callbacks, Is.Zero);
                Assert.That(c6Rotation(bms), Is.Zero);
                Assert.That(c6Rotation(mania), Is.Zero);
                Assert.That(bms.ScriptStatus, Is.EqualTo("running"));
                Assert.That(mania.ScriptStatus, Is.EqualTo("running"));
            });
            AddStep("detach the actual permission consumers", () => renderer.Expire());
            AddUntilStep("both permission consumers release their revision", () => renderer.Parent == null);
        }

        private static async Task grantC6RequiredWithoutEvents(GameplaySkinScriptAuthorization authorization)
        {
            Assert.That(await authorization.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted).ConfigureAwait(false), Is.True);
            Assert.That(await authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted).ConfigureAwait(false), Is.True);
        }
    }
}
