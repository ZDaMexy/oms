// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestRealAxisMotionRemainsC5OnlyAndDoesNotAddCallbacksToExistingC6Scripts()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            GameplaySkinEventSubscription observer = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            Task? permission = null;

            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                File.WriteAllText(Path.Combine(root, "gameplay-skin.script"), """
                    oms-script 1
                    deny gameplay.snapshot.read
                    required gameplay.events.read
                    required scene.numeric.write
                    state calls 0
                    target node.glow rotation
                    add calls calls 1
                    set node.glow rotation calls
                    halt
                    """);
            });
            AddStep("grant only the existing event callback and numeric write channels", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                permission = Task.WhenAll(
                    authorization.SetAsync("gameplay.events.read", GameplaySkinScriptAuthorizationChoice.Granted),
                    authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted));
            });
            AddUntilStep("event-only authorization completes", () => permission?.IsCompleted == true);
            AddStep("mount production gameplay with a real saved relative-axis binding", () =>
            {
                permission!.GetAwaiter().GetResult();
                Assert.That(authorization.RequiredSatisfied, Is.True);
                Assert.That(authorization.IsGranted("gameplay.snapshot.read"), Is.False, "No fixed-tick callbacks can obscure event callback counts.");
                renderer = new ExactLayoutJourneyHost(manager);
                OmsBmsBindingSettingsStorage.SaveSupplementalBindings(Realm, renderer.BmsDrawable.Variant, new[]
                {
                    new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)),
                });
                clock = renderer.AttachC6GameplayClock(1000);
                Add(new DependencyProvidingContainer
                {
                    CachedDependencies = new[] { (typeof(RealmAccess), (object)Realm) },
                    Child = renderer,
                });
            });
            AddUntilStep("actual BMS and mania author hosts are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                scene ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return scene.IsSceneReady && scene.ScriptInstance?.IsEnabled == true;
            });
            AddStep("settle the real loaded epoch before measuring callbacks", () =>
            {
                clock.Sample(1000);
                clock.Sample(1000);
                observer = renderer.BmsDrawable.GameplaySkinEventStream.Subscribe();
                observer.DrainFrame(_ => { });
            });
            AddStep("first accepted axis report preserves C5 motion without adding a C6 callback", () => assertInput(() =>
            {
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerMouseAxisDelta(OmsMouseAxis.X, 4), Is.True);
                Assert.That(renderer.BmsDrawable.GameplayInputManager.ScratchPresentationMotionAvailable, Is.True);
            }, motion: true));
            AddStep("continued signed displacement is folded without waking the old script", () => assertInput(() =>
                renderer.BmsDrawable.GameplayInputManager!.TriggerMouseAxisDelta(OmsMouseAxis.X, 6), motion: true));
            AddStep("the original real key-down still reaches the author callback", () => assertInput(() => c6Input(renderer, true), inputEdge: GameplaySkinEventKind.InputPressed));
            AddStep("the original real key-up still reaches the author callback", () => assertInput(() => c6Input(renderer, false), inputEdge: GameplaySkinEventKind.InputReleased));
            AddStep("detach the measured author consumers", () =>
            {
                observer.Dispose();
                renderer.Expire();
            });
            AddUntilStep("measured gameplay leaves its revision", () => renderer.Parent == null);

            void assertInput(Action input, bool motion = false, GameplaySkinEventKind? inputEdge = null)
            {
                long before = scene.ScriptInstance!.Profiler.Callbacks;
                input();
                // The native ruleset reconciles motion after the scene's pass. A second traversal at the same
                // authoritative time consumes that observation without advancing ticks or expiring the scratch.
                clock.Sample(1000);
                clock.Sample(1000);
                var events = new List<GameplaySkinEventEnvelope>();
                observer.DrainFrame(events.Add);
                if (motion)
                {
                    Assert.That(events.Any(edge => edge.EventKind == GameplaySkinEventKind.InputMotionChanged), Is.True,
                        "The real accepted input must reach the C5 stream; the test cannot pass by suppressing its producer.");
                    GameplaySkinInputEventPayload current = (GameplaySkinInputEventPayload)events.Last(edge => edge.EventKind == GameplaySkinEventKind.InputMotionChanged).Payload;
                    Assert.That(current.State.MotionCounts, Is.GreaterThan(0));
                }
                if (inputEdge.HasValue)
                    Assert.That(events.Any(edge => edge.EventKind == inputEdge.Value), Is.True, "The real legacy key edge remains observable.");
                int legacyEvents = events.Count(edge => edge.EventKind != GameplaySkinEventKind.InputMotionChanged);
                Assert.That(scene.ScriptInstance.Profiler.Callbacks - before, Is.EqualTo(legacyEvents),
                    "Every existing event still executes once, while directional motion executes zero C6 callbacks.");
                Assert.That(scene.ScriptStatus, Is.EqualTo("running"));
                Assert.That(scene.RuntimeFaults, Is.Empty);
            }
        }
    }
}
