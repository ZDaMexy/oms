// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestBothRulesetScriptFixedTicksStayDeterministicAcrossRenderStepsPauseSeekAndRetry()
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock gameplayClock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            Task? authorizationTask = null;
            float firstBms = 0;
            float firstMania = 0;
            long firstEpoch = 0;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                File.WriteAllText(Path.Combine(root, "gameplay-skin.script"), """
                    oms-script 1
                    required gameplay.snapshot.read
                    required gameplay.events.read
                    required scene.numeric.write
                    state kind 0
                    state tick 0
                    state ticks 0
                    target node.glow rotation
                    read kind event-kind
                    eq tick kind -1
                    when tick advance
                    halt
                    advance:
                    add ticks ticks 1
                    set node.glow rotation ticks
                    halt
                    """);
            });
            AddStep("grant the current author script", () => authorizationTask = grantC6Script(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!));
            AddUntilStep("authorization completes", () => authorizationTask?.IsCompleted == true);
            AddStep("mount both production providers under the actual gameplay clock", () =>
            {
                authorizationTask!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager);
                gameplayClock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both production script hosts attach", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("render twenty frames at ten millisecond steps", () =>
            {
                for (int i = 1; i <= 20; i++)
                    gameplayClock.Sample(1_000 + i * 10);
                // Advancing beyond 1200 seals all producer edges at 1200 before the equal-time script tick.
                gameplayClock.Sample(1_201);
                firstBms = c6Rotation(bms);
                firstMania = c6Rotation(mania);
                firstEpoch = bms.CurrentEpoch;
                Assert.That(firstBms, Is.EqualTo(12));
                Assert.That(firstMania, Is.EqualTo(12));
            });
            AddStep("pause the actual engine clock", () => gameplayClock.Stop());
            AddStep("render while paused", () =>
            {
                for (int i = 0; i < 20; i++)
                    gameplayClock.Sample(1_201);
                Assert.That(c6Rotation(bms), Is.EqualTo(firstBms));
                Assert.That(c6Rotation(mania), Is.EqualTo(firstMania));
            });
            AddStep("rewind through the real gameplay seek API", () =>
            {
                gameplayClock.Seek(1_000);
                // The native FrameStabilityContainer intentionally skips its whole tree while paused. Resume
                // traversal at the still-frozen source time so its real seek barrier can rebuild the scene.
                gameplayClock.SoftUnpause();
                gameplayClock.Sample(1_000);
            });
            AddUntilStep("seek resets both script epochs and authored overlays", () =>
                bms.CurrentEpoch > firstEpoch && c6Rotation(bms) == 0 && c6Rotation(mania) == 0);
            AddStep("render four frames at fifty millisecond steps", () =>
            {
                gameplayClock.SoftUnpause();
                for (int i = 1; i <= 4; i++)
                    gameplayClock.Sample(1_000 + i * 50);
                gameplayClock.Sample(1_201);
                Assert.That(c6Rotation(bms), Is.EqualTo(firstBms));
                Assert.That(c6Rotation(mania), Is.EqualTo(firstMania));
                firstEpoch = bms.CurrentEpoch;
            });
            AddStep("retry through the real gameplay reset API", () =>
            {
                gameplayClock.Reset(1_000);
                gameplayClock.Sample(1_000);
            });
            AddUntilStep("retry rebuilds both exact script instances from baseline", () =>
                bms.CurrentEpoch > firstEpoch && c6Rotation(bms) == 0 && c6Rotation(mania) == 0);
            AddStep("repeat a single bounded catch-up frame", () =>
            {
                gameplayClock.SoftUnpause();
                gameplayClock.Sample(1_200);
                gameplayClock.Sample(1_201);
                Assert.That(c6Rotation(bms), Is.EqualTo(firstBms));
                Assert.That(c6Rotation(mania), Is.EqualTo(firstMania));
                Assert.That(bms.ScriptInstance!.Fault, Is.Null);
                Assert.That(mania.ScriptInstance!.Fault, Is.Null);
            });
            AddStep("detach both deterministic production consumers", () => renderer.Expire());
            AddUntilStep("the clock and production providers detach", () => renderer.Parent == null);
        }

        [TestCase("loop", "InstructionLimit")]
        [TestCase("heap", "HeapLimit")]
        [TestCase("writes", "NodeLimit")]
        [TestCase("number", "NonFiniteNumber")]
        [TestCase("permission", "PermissionDenied")]
        public void TestScriptFaultFromRealBothRulesetEventRestoresSceneWithoutStoppingGameplay(string failure, string expectedCode)
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            Task? authorizationTask = null;
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.ManagedFolder, root =>
            {
                writeC6ScriptPackage(root);
                string failingBody = failure switch
                {
                    "loop" => "trap:\njump trap\n",
                    "heap" => "load value 1\nhalt\n",
                    "writes" => "trap:\nset node.glow rotation 60\njump trap\n",
                    "number" => "div value 1 0\nhalt\n",
                    "permission" => "random value\nhalt\n",
                    _ => throw new ArgumentOutOfRangeException(nameof(failure)),
                };
                File.WriteAllText(Path.Combine(root, "gameplay-skin.script"), """
                    oms-script 1
                    required gameplay.snapshot.read
                    required gameplay.events.read
                    required scene.numeric.write
                    optional math.random.read
                    state kind 0
                    state pressed 0
                    state value 0
                    heap 1
                    target node.glow rotation
                    read kind event-kind
                    eq pressed kind 20
                    when pressed fail
                    set node.glow rotation 45
                    halt
                    fail:
                    """ + "\n" + failingBody);
            });
            AddStep("grant the actual candidate before mounting gameplay", () =>
                authorizationTask = grantC6Script(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!));
            AddUntilStep("the authorization write completes", () => authorizationTask?.IsCompleted == true);
            AddStep("mount both real consumers of the author script", () =>
            {
                authorizationTask!.GetAwaiter().GetResult();
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
                renderer.ShowMania();
            });
            AddUntilStep("the script produces visible output in both hosts", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady && c6Rotation(bms) == 45 && c6Rotation(mania) == 45;
            });
            AddStep("send actual gameplay input into the failing script branch", () => c6Input(renderer, true));
            AddUntilStep("both runtimes fuse and remove the old script output", () =>
                bms.ScriptInstance?.Fault?.Code.ToString() == expectedCode
                && mania.ScriptInstance?.Fault?.Code.ToString() == expectedCode
                && c6Rotation(bms) == 0 && c6Rotation(mania) == 0);
            AddStep("the real gameplay producers continue after script failure", () => c6Input(renderer, false));
            AddStep("profiler and source-mapped fault remain inspectable", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(scene.ScriptInstance!.Profiler.Instructions, Is.GreaterThan(0));
                    Assert.That(scene.ScriptInstance.Profiler.MaximumCallbackInstructions, Is.LessThanOrEqualTo(GameplaySkinScriptInstance.MAX_CALLBACK_INSTRUCTIONS));
                    Assert.That(scene.ScriptInstance.Fault!.Line, Is.GreaterThan(0));
                    Assert.That(scene.ScriptStatus, Is.EqualTo("faulted"));
                    Assert.That(scene.RuntimeFaults, Is.Empty);
                    Assert.That(scene.HostedSlots.Any(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Note && slot.AllowsProgrammaticVisual), Is.True);
                }
            });
            AddStep("detach the faulted production consumers", () => renderer.Expire());
            AddUntilStep("faulted consumers release their revision leases", () => renderer.Parent == null);
        }

        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder)]
        public void TestScriptPackageRequiresAuthorizationAndRealBothRulesetInputStopsOnRevocation(MaterialDiagnosticPackageSource source)
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            Task? authorizationTask = null;

            addSelectC6ScriptPackage(selection, source, writeC6ScriptPackage);
            AddStep("mount the selected package in both real rulesets", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
                renderer.ShowMania();
            });
            AddUntilStep("both real scene renderers are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;

                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("ungranted scripts leave authored decoration and necessary notes usable", () =>
            {
                Assert.That(c6Rotation(bms), Is.Zero);
                Assert.That(c6Rotation(mania), Is.Zero);
                Assert.That(bms.HostedSlots.Any(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Note && slot.AllowsProgrammaticVisual), Is.True);
                Assert.That(mania.HostedSlots.Any(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Note && slot.AllowsProgrammaticVisual), Is.True);
            });
            AddStep("grant the exact selected skin requests", () => authorizationTask = grantC6Script(authorization));
            AddUntilStep("authorization is durable", () => authorizationTask?.IsCompleted == true);
            AddStep("observe successful authorization write", () => authorizationTask!.GetAwaiter().GetResult());
            AddStep("press through both actual input producers", () => c6Input(renderer, true));
            AddUntilStep("both scene nodes consume the first real input event", () => c6Rotation(bms) == 15 && c6Rotation(mania) == 15);
            AddStep("release both actual keys", () => c6Input(renderer, false));
            AddStep("press again to prove persistent per-host script state", () => c6Input(renderer, true));
            AddUntilStep("both script instances retain the previous event", () => c6Rotation(bms) == 30 && c6Rotation(mania) == 30);
            AddStep("revoke scene writes while the gameplay hosts remain mounted", () =>
                authorizationTask = authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Denied));
            AddUntilStep("revocation is durable", () => authorizationTask?.IsCompleted == true);
            AddStep("observe successful revocation write", () => authorizationTask!.GetAwaiter().GetResult());
            AddUntilStep("old script output is removed in both actual renderers", () => c6Rotation(bms) == 0 && c6Rotation(mania) == 0);
            AddStep("produce new gameplay events after revocation", () =>
            {
                c6Input(renderer, false);
                c6Input(renderer, true);
            });
            AddStep("revoked instances cannot regain authority from new events", () =>
            {
                Assert.That(c6Rotation(bms), Is.Zero);
                Assert.That(c6Rotation(mania), Is.Zero);
                Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
            });
            AddStep("release and detach the exact production trees", () =>
            {
                c6Input(renderer, false);
                renderer.Expire();
            });
            AddUntilStep("production trees release their revision leases", () => renderer.Parent == null);
        }

        private void addSelectC6ScriptPackage(C6ScriptSelection selection, MaterialDiagnosticPackageSource source, Action<string> writer)
        {
            Task<bool>? registration = null;
            Task<IList<Live<SkinInfo>>>? dropdown = null;

            switch (source)
            {
                case MaterialDiagnosticPackageSource.OrdinaryRealm:
                    AddStep("import an ordinary script author package", () =>
                    {
                        selection.Root = LocalStorage.GetFullPath($"realm-c6-script-{Guid.NewGuid():N}");
                        writer(selection.Root);
                        selection.Candidate = createRealmRevisionCandidate(selection.Root);
                        manager.CurrentSkinInfo.Value = selection.Candidate;
                    });
                    break;

                case MaterialDiagnosticPackageSource.ManagedFolder:
                    AddStep("select a managed script author package", () =>
                    {
                        (selection.Root, selection.Candidate) = createCandidate(writer, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                        manager.CurrentSkinInfo.Value = selection.Candidate;
                    });
                    break;

                case MaterialDiagnosticPackageSource.ExternalFolder:
                    AddStep("register an external script author package", () =>
                    {
                        selection.Root = createExternalPackage(writer);
                        registration = manager.RegisterExternalFolderAsync(selection.Root);
                    });
                    AddUntilStep("external registration completes", () => registration?.IsCompleted == true);
                    AddStep("query the registered skin through the production dropdown", () =>
                    {
                        Assert.That(registration!.GetAwaiter().GetResult(), Is.True);
                        dropdown = manager.GetAllUsableSkinsAsync();
                    });
                    AddUntilStep("the production dropdown query completes", () => dropdown?.IsCompleted == true);
                    AddStep("select the registered external script package", () =>
                    {
                        selection.Candidate = dropdown!.GetAwaiter().GetResult().Single(record => record.PerformRead(info =>
                            info.IsExternalFilesystemStorage && string.Equals(info.FilesystemStoragePath, selection.Root, StringComparison.OrdinalIgnoreCase)));
                        manager.CurrentSkinInfo.Value = selection.Candidate;
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }

            AddUntilStep("the exact script package becomes the current immutable revision", () =>
                selection.Candidate != null
                && manager.CurrentSkinInfo.Value.ID == selection.Candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == selection.Candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
        }

        private static async Task grantC6Script(GameplaySkinScriptAuthorization authorization)
        {
            Assert.That(await authorization.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted).ConfigureAwait(false), Is.True);
            Assert.That(await authorization.SetAsync("gameplay.events.read", GameplaySkinScriptAuthorizationChoice.Granted).ConfigureAwait(false), Is.True);
            Assert.That(await authorization.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted).ConfigureAwait(false), Is.True);
        }

        private static float c6Rotation(GameplaySkinSceneRuntimeHost scene)
        {
            Assert.That(scene.TryGetRuntimeNode("node.glow", out GameplaySkinSceneRuntimeNode? glow), Is.True);
            return glow!.TransformDrawable.Rotation;
        }

        private static void c6Input(ExactLayoutJourneyHost renderer, bool pressed)
        {
            if (pressed)
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True);
            else
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True);

            foreach (ManiaInputManager input in renderer.ManiaDrawable.ChildrenOfType<ManiaInputManager>())
            {
                var action = renderer.ManiaDrawable.Playfield.Stages[0].Columns[0].Action.Value;
                if (pressed)
                    input.KeyBindingContainer.TriggerPressed(action);
                else
                    input.KeyBindingContainer.TriggerReleased(action);
            }
        }

        private static void writeC6ScriptPackage(string root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "skin.ini"), """
                [General]
                Name: C6 script production candidate
                Author: OMS tests
                Version: 2.7
                [Bms]
                Keymode: 7K
                [GameplaySkin.Common:1]
                Target: Global ruleset=bms keymode=7k stage-mode=single
                decoration: resource Provide "glow"
                Target: Global ruleset=mania keymode=4k stage-mode=single
                decoration: resource Provide "glow"
                """);
            using (var image = new Image<Rgba32>(16, 16, new Rgba32(20, 220, 180, 255)))
                image.SaveAsPng(Path.Combine(root, "glow.png"));

            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["script"] = "gameplay-skin.script",
                ["resources"] = new JArray(new JObject { ["id"] = "texture.glow", ["type"] = "texture", ["path"] = "glow.png" }),
            }.ToString());
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = new JObject
                {
                    ["id"] = "node.glow",
                    ["type"] = "sprite",
                    ["target"] = new JObject { ["kind"] = "global" },
                    ["slot"] = "decoration",
                    ["resource"] = "texture.glow",
                    ["blend"] = "alpha",
                    ["properties"] = new JObject { ["x"] = 0.7, ["y"] = 0.1, ["width"] = 0.08, ["height"] = 0.08, ["rotation"] = 0 },
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(),
                },
                ["tracks"] = new JArray(),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray(),
                ["templates"] = new JArray(),
                ["instances"] = new JArray(),
            }.ToString());
            File.WriteAllText(Path.Combine(root, "gameplay-skin.script"), """
                oms-script 1
                required gameplay.snapshot.read
                required gameplay.events.read
                required scene.numeric.write
                state kind 0
                state pressed 0
                state spin 0
                state clock 0
                target node.glow rotation
                read clock time
                read kind event-kind
                eq pressed kind 20
                when pressed hit
                set node.glow rotation spin
                halt
                hit:
                add spin spin 15
                set node.glow rotation spin
                halt
                """);
        }

        private sealed class C6ScriptSelection
        {
            public string Root = string.Empty;
            public Live<SkinInfo> Candidate = null!;
        }

        private sealed partial class ExactLayoutJourneyHost
        {
            public C6GameplayTestClock AttachC6GameplayClock(double time, bool includeMania = true)
            {
                var clock = new C6GameplayTestClock(time)
                {
                    Children = includeMania ? new Drawable[] { BmsProvider, ManiaProvider } : new Drawable[] { BmsProvider },
                };
                providerHost.Add(clock);
                return clock;
            }
        }

        private partial class C6GameplayTestClock : GameplayClockContainer
        {
            private readonly C6AdjustableManualClock source;

            public C6GameplayTestClock(double time)
                : this(new C6AdjustableManualClock { CurrentTime = time, IsRunning = false })
            {
            }

            private C6GameplayTestClock(C6AdjustableManualClock source)
                : base(source, applyOffsets: false, requireDecoupling: false)
            {
                this.source = source;
                SoftUnpause();
            }

            public void Sample(double time)
            {
                source.CurrentTime = time;
                UpdateSubTree();
            }
        }

        private class C6AdjustableManualClock : ManualClock, IAdjustableClock
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
            public void ResetSpeedAdjustments() => Rate = 1;
        }
    }
}
