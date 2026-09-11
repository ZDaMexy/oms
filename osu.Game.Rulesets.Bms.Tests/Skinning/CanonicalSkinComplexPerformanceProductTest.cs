// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestCanonicalComplexActualBothRulesetPerformancePermissionPauseSeekAndRetry()
        {
            FullSkinSettingsCallerHost caller = null!;
            SkinScriptSettings settings = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock clock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            GameplaySkinEventSubscription bmsEvents = null!;
            GameplaySkinEventSubscription maniaEvents = null!;
            GameplaySkinEventSubscription earlyReleaseEvents = null!;
            var bmsJudgements = new List<GameplaySkinJudgementStateSnapshot>();
            var maniaJudgements = new List<GameplaySkinJudgementStateSnapshot>();
            var earlyReleaseStates = new List<GameplaySkinObjectStateSnapshot>();
            long earlyReleasedObjectId = -1;
            long earlyReleaseSequence = -1;
            Task<SkinCurrentRevisionReloadResult>? reload = null;
            float bmsPulse = 0, maniaPulse = 0, bmsRotation = 0, maniaRotation = 0;
            long epoch = 0;
            GameplaySkinScriptInstance firstBmsScript = null!;
            GameplaySkinScriptInstance firstManiaScript = null!;

            // This helper copies the retained distributable before ordinary import can consume its input.
            addSelectCanonicalProduct("oms-complex");
            AddStep("open the imported complete skin's actual permission controls", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Assert.That(manager.CurrentSkin.Value.SkinInfo.PerformRead(info => info.Protected), Is.False);
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("the complete skin's settings are loaded", () =>
            {
                settings ??= caller.Section.ChildrenOfType<SkinScriptSettings>().SingleOrDefault()!;
                return settings?.IsLoaded == true && settings.ChildrenOfType<SettingsButtonV2>().Count() == authorization.Requests.Count * 3;
            });
            AddStep("mount the actual two playfields before permission is given", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager);
                renderer.ManiaDrawable.Beatmap.HitObjects.OfType<Note>().Single().StartTime = 2_000;
                clock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both complete scene graphs and ordinary materials are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("unapproved extras retain every required playing part and the full console", () =>
            {
                Assert.That(authorization.RequiredSatisfied, Is.False);
                Assert.That(c6PermissionButton(settings, authorization, "network.read", 0).Enabled.Value, Is.False);
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    assertCanonicalProductScene(scene, "oms-complex");
                    Assert.That(c6CandidateNode(scene, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(1));
                    Assert.That(c6CandidateNode(scene, "astral.console.score").ContentDrawable, Is.InstanceOf<SpriteText>());
                    Assert.That(((SpriteText)c6CandidateNode(scene, "astral.console.bpm").ContentDrawable).Text.ToString(), Is.Not.Empty);
                    Assert.That(((SpriteText)c6CandidateNode(scene, "astral.console.accuracy").ContentDrawable).Text.ToString(), Does.EndWith("%"));
                    Assert.That(c6CandidateNode(scene, "astral.console.progress").TransformDrawable.Width, Is.InRange(0f, 1f));
                }
                bmsEvents = bms.EventStream.Subscribe();
                maniaEvents = mania.EventStream.Subscribe();
                reload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("reload is rejected while the actual pre-start preview tree is attached", () => reload?.IsCompleted == true);
            AddStep("preview rejection retains the displayed immutable package", () =>
                Assert.That(reload!.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.LiveGameplayActive)));
            foreach (string id in new[] { "gameplay.snapshot.read", "gameplay.events.read", "scene.numeric.write" })
            {
                AddStep($"allow {id} through the actual settings button", () => c6PermissionButton(settings, authorization, id, 0).TriggerClick());
                AddUntilStep($"{id} is persisted", () => authorization.IsGranted(id));
            }
            AddStep("advance the real clock toward the first notes", () =>
            {
                for (int time = 1_050; time <= 1_950; time += 50)
                    clock.Sample(time);
                Assert.That(c6CandidateNode(bms, "astral.orbit-0").TransformDrawable.Rotation, Is.Not.Zero);
                Assert.That(c6CandidateNode(mania, "astral.orbit-0").TransformDrawable.Rotation, Is.Not.Zero);
            });
            AddStep("judge real BMS and mania notes with their production input producers", () =>
            {
                clock.Sample(2_000);
                c6Input(renderer, true);
                clock.Sample(2_020);
            });
            AddUntilStep("recent real judgements change both composed performances", () =>
                c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X > 1
                && c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X > 1
                && c6CandidateNode(bms, "astral.meter-0-0").TransformDrawable.Scale.Y > 0.12f
                && c6CandidateNode(mania, "astral.meter-0-0").TransformDrawable.Scale.Y > 0.12f);
            AddStep("confirm the authored scene consumed genuine successful note judgements", () =>
            {
                drainCanonicalComplexJudgements(bmsEvents, bmsJudgements);
                drainCanonicalComplexJudgements(maniaEvents, maniaJudgements);
                Assert.That(bmsJudgements.Any(state => state.ObjectId.HasValue && state.Grade > GameplaySkinJudgementGrade.Miss), Is.True);
                Assert.That(maniaJudgements.Any(state => state.ObjectId.HasValue && state.Grade > GameplaySkinJudgementGrade.Miss), Is.True);
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(((SpriteText)c6CandidateNode(scene, "astral.console.judgement").ContentDrawable).Text.ToString(), Is.Not.Empty);
                    Assert.That(scene.ScriptInstance!.Fault, Is.Null);
                    Assert.That(scene.ScriptInstance.Profiler.Instructions, Is.GreaterThan(0));
                    assertCanonicalProductScene(scene, "oms-complex");
                }
                firstBmsScript = bms.ScriptInstance!;
                firstManiaScript = mania.ScriptInstance!;
                epoch = bms.CurrentEpoch;

                GameplaySkinObjectStateSnapshot held = authoredInformationSnapshot(bms).ActiveObjects.Single(state => state.Kind == GameplaySkinObjectKind.LongNote);
                Assert.That(held.State, Is.EqualTo(GameplaySkinObjectState.Holding));
                Assert.That(held.StartTime, Is.EqualTo(2_000));
                Assert.That(held.EndTime, Is.EqualTo(3_000));
                Assert.That(held.Progress, Is.EqualTo(0.02).Within(0.000001));
                earlyReleasedObjectId = held.ObjectId;
                earlyReleaseEvents = bms.EventStream.Subscribe();
                c6Input(renderer, false);
            });
            AddUntilStep("early LN release and its body update reach the authored scene in the same history", () =>
            {
                earlyReleaseEvents.DrainFrame(envelope =>
                {
                    if (envelope.Payload is GameplaySkinObjectEventPayload obj && obj.State.ObjectId == earlyReleasedObjectId)
                    {
                        earlyReleaseStates.Add(obj.State);
                        earlyReleaseSequence = envelope.Sequence;
                    }
                });
                Assert.That(bms.CurrentEpoch, Is.EqualTo(epoch), "An early LN release must not rebuild unrelated performance history.");
                return earlyReleaseStates.Count(state => state.State == GameplaySkinObjectState.Missed) >= 2
                       && bms.LastSequence >= earlyReleaseSequence;
            });
            AddStep("the genuine early release retains clock progress and the accumulated performance", () =>
            {
                GameplaySkinObjectStateSnapshot missed = authoredInformationSnapshot(bms).ActiveObjects.Single(state => state.ObjectId == earlyReleasedObjectId);
                Assert.That(missed.State, Is.EqualTo(GameplaySkinObjectState.Missed));
                Assert.That(missed.Progress, Is.EqualTo(0.02).Within(0.000001));
                Assert.That(earlyReleaseStates.Where(state => state.State == GameplaySkinObjectState.Missed).All(state => Math.Abs(state.Progress - 0.02) < 0.000001), Is.True,
                    "Both the terminal judgement and the following body state must use elapsed chart time, not an invented 100% progress.");
                Assert.That(bms.CurrentEpoch, Is.EqualTo(epoch));
                Assert.That(bms.ScriptInstance, Is.SameAs(firstBmsScript));
                Assert.That(c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X, Is.GreaterThan(1));
                earlyReleaseEvents.Dispose();

                // The real release can add a judgement to the composition. Take the pause baseline only after
                // that edge and its subsequent body-state projection have reached the actual scene consumer.
                bmsPulse = c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X;
                maniaPulse = c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X;
                bmsRotation = c6CandidateNode(bms, "astral.orbit-0").TransformDrawable.Rotation;
                maniaRotation = c6CandidateNode(mania, "astral.orbit-0").TransformDrawable.Rotation;
                reload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("reload is rejected during the actual performance", () => reload?.IsCompleted == true);
            AddStep("gameplay reload rejection is explicit", () =>
                Assert.That(reload!.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.LiveGameplayActive)));
            AddStep("pause the actual gameplay clock and render its frozen scene", () =>
            {
                logPauseProjection("before Stop");
                Assert.That(c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(bmsPulse), "BMS changed before Stop; inspect the recorded epoch and reset reason.");
                Assert.That(c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(maniaPulse), "mania changed before Stop; inspect the recorded epoch and reset reason.");
                long bmsEpochBeforePause = bms.CurrentEpoch;
                long maniaEpochBeforePause = mania.CurrentEpoch;
                clock.Stop();
                for (int frame = 0; frame < 20; frame++)
                {
                    clock.Sample(2_020);
                    if (frame == 0 || bms.CurrentEpoch != bmsEpochBeforePause || mania.CurrentEpoch != maniaEpochBeforePause)
                        logPauseProjection($"paused frame {frame}");
                }
                Assert.That(bms.ScriptInstance!.Fault, Is.Null);
                Assert.That(mania.ScriptInstance!.Fault, Is.Null);
                Assert.That(bms.CurrentEpoch, Is.EqualTo(bmsEpochBeforePause), "A pause at the unchanged gameplay time must not reset BMS history.");
                Assert.That(mania.CurrentEpoch, Is.EqualTo(maniaEpochBeforePause), "A pause at the unchanged gameplay time must not reset mania history.");
                Assert.That(c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(bmsPulse));
                Assert.That(c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(maniaPulse));
                Assert.That(c6CandidateNode(bms, "astral.orbit-0").TransformDrawable.Rotation, Is.EqualTo(bmsRotation));
                Assert.That(c6CandidateNode(mania, "astral.orbit-0").TransformDrawable.Rotation, Is.EqualTo(maniaRotation));
            });
            AddStep("revoke scene writes through settings while both playfields remain paused", () =>
                c6PermissionButton(settings, authorization, "scene.numeric.write", 2).TriggerClick());
            AddUntilStep("paused revocation removes optional output from both playfields", () =>
                !authorization.IsGranted("scene.numeric.write")
                && c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X == 1
                && c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X == 1
                && c6CandidateNode(bms, "astral.meter-0-0").TransformDrawable.Scale.Y == 0.15f
                && c6CandidateNode(mania, "astral.meter-0-0").TransformDrawable.Scale.Y == 0.15f);
            AddStep("the paused revoked skin still owns all essential parts", () =>
            {
                assertCanonicalProductScene(bms, "oms-complex");
                assertCanonicalProductScene(mania, "oms-complex");
                clock.Seek(1_000);
                clock.SoftUnpause();
                clock.Sample(1_000);
            });
            AddUntilStep("backward seek resets the real scene epochs without restoring permission", () =>
                bms.CurrentEpoch > epoch && !authorization.RequiredSatisfied
                && c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X == 1
                && c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X == 1);
            AddStep("allow the same optional performance again", () =>
                c6PermissionButton(settings, authorization, "scene.numeric.write", 0).TriggerClick());
            AddUntilStep("the new permission is durable", () => authorization.RequiredSatisfied);
            AddStep("retry through the real gameplay reset API", () =>
            {
                epoch = bms.CurrentEpoch;
                clock.Reset(1_000);
                clock.SoftUnpause();
                clock.Sample(1_000);
            });
            AddUntilStep("retry creates fresh per-playfield history", () =>
                bms.CurrentEpoch > epoch && bms.ScriptInstance != null && mania.ScriptInstance != null
                && !ReferenceEquals(bms.ScriptInstance, firstBmsScript) && !ReferenceEquals(mania.ScriptInstance, firstManiaScript));
            AddStep("fresh history has no retained recent-hit pulse", () =>
            {
                for (int time = 1_050; time <= 1_950; time += 50)
                    clock.Sample(time);
                Assert.That(c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(1));
                Assert.That(c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(1));
                clock.Sample(2_000);
                c6Input(renderer, true);
                clock.Sample(2_020);
            });
            AddUntilStep("new actual judgements recreate both optional performances after retry", () =>
                c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X > 1
                && c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X > 1);
            AddStep("sample sustained full-package frames and recent-hit decay", () =>
            {
                c6Input(renderer, false);
                int bmsHeap = bms.ScriptInstance!.Profiler.HeapBytes;
                int maniaHeap = mania.ScriptInstance!.Profiler.HeapBytes;
                long before = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                for (int frame = 1; frame <= 300; frame++)
                    clock.Sample(2_020 + frame * (1000.0 / 60));
                double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                TestContext.Progress.WriteLine($"C7 oms-complex actual BMS+mania: 300 frames, {elapsed:F3} ms, {allocated} thread bytes; "
                    + $"OS={Environment.OSVersion.VersionString}, logical CPUs={Environment.ProcessorCount}, 64-bit={Environment.Is64BitProcess}; heaps={bmsHeap}/{maniaHeap}.");
                Assert.That(bms.ScriptInstance.Profiler.HeapBytes, Is.EqualTo(bmsHeap));
                Assert.That(mania.ScriptInstance.Profiler.HeapBytes, Is.EqualTo(maniaHeap));
                Assert.That(c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(1));
                Assert.That(c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(1));
                assertCanonicalProductScene(bms, "oms-complex");
                assertCanonicalProductScene(mania, "oms-complex");
            });
            AddStep("deny extra writes and retry basic gameplay", () =>
                c6PermissionButton(settings, authorization, "scene.numeric.write", 1).TriggerClick());
            AddUntilStep("the refusal is persisted", () => !authorization.RequiredSatisfied);
            AddStep("restart with denied extras", () =>
            {
                epoch = bms.CurrentEpoch;
                clock.Reset(1_000);
                clock.SoftUnpause();
                clock.Sample(1_000);
            });
            AddUntilStep("the denied retry owns a fresh scene epoch", () => bms.CurrentEpoch > epoch);
            AddStep("actual judgements continue without the optional script", () =>
            {
                bmsJudgements.Clear();
                maniaJudgements.Clear();
                drainCanonicalComplexJudgements(bmsEvents, bmsJudgements);
                drainCanonicalComplexJudgements(maniaEvents, maniaJudgements);
                bmsJudgements.Clear();
                maniaJudgements.Clear();
                for (int time = 1_050; time <= 2_000; time += 50)
                    clock.Sample(time);
                c6Input(renderer, true);
                clock.Sample(2_020);
                c6Input(renderer, false);
            });
            AddStep("refusal preserves successful judgements and complete basic visuals", () =>
            {
                drainCanonicalComplexJudgements(bmsEvents, bmsJudgements);
                drainCanonicalComplexJudgements(maniaEvents, maniaJudgements);
                Assert.That(bmsJudgements.Any(state => state.ObjectId.HasValue && state.Grade > GameplaySkinJudgementGrade.Miss), Is.True);
                Assert.That(maniaJudgements.Any(state => state.ObjectId.HasValue && state.Grade > GameplaySkinJudgementGrade.Miss), Is.True);
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    assertCanonicalProductScene(scene, "oms-complex");
                    Assert.That(c6CandidateNode(scene, "astral.prism-0").TransformDrawable.Scale.X, Is.EqualTo(1));
                }
                bmsEvents.Dispose();
                maniaEvents.Dispose();
                renderer.Expire();
                caller.Expire();
            });
            AddUntilStep("the actual performance and settings release their current skin", () => renderer.Parent == null && caller.Parent == null);

            void logPauseProjection(string step)
            {
                TestContext.Progress.WriteLine($"C7 {step}: clock={clock.CurrentTime:R}; "
                    + $"BMS epoch={bms.CurrentEpoch}, time={bms.LastGameplayTime:R}, scale={c6CandidateNode(bms, "astral.prism-0").TransformDrawable.Scale.X:R}, script={bms.ScriptStatus}, fault={bms.ScriptInstance?.Fault}; "
                    + $"mania epoch={mania.CurrentEpoch}, time={mania.LastGameplayTime:R}, scale={c6CandidateNode(mania, "astral.prism-0").TransformDrawable.Scale.X:R}, script={mania.ScriptStatus}, fault={mania.ScriptInstance?.Fault}.");
                trace(bmsEvents, "BMS");
                trace(maniaEvents, "mania");

                void trace(GameplaySkinEventSubscription events, string ruleset)
                    => events.DrainFrame(envelope =>
                    {
                        if (envelope.Payload is GameplaySkinStateEventPayload state)
                            TestContext.Progress.WriteLine($"C7 {ruleset} state: epoch={envelope.Epoch}, time={envelope.GameplayTime:R}, kind={envelope.EventKind}, reset={state.ResetReason}.");
                        else if (envelope.Payload is GameplaySkinLifecycleEventPayload)
                            TestContext.Progress.WriteLine($"C7 {ruleset} lifecycle: epoch={envelope.Epoch}, time={envelope.GameplayTime:R}, kind={envelope.EventKind}.");
                    });
            }
        }

        private static void drainCanonicalComplexJudgements(GameplaySkinEventSubscription events, List<GameplaySkinJudgementStateSnapshot> judgements)
            => events.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinJudgementEventPayload judgement)
                    judgements.Add(judgement.State);
            });
    }
}
