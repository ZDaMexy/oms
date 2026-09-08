// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder)]
        public void TestRealComplexCandidateAndSettingsAuthorizationReachBothJudgementProducers(MaterialDiagnosticPackageSource source)
        {
            var selection = new C6ScriptSelection();
            FullSkinSettingsCallerHost caller = null!;
            SkinScriptSettings settings = null!;
            GameplaySkinScriptAuthorization authorization = null!;
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock gameplayClock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            if (source == MaterialDiagnosticPackageSource.OrdinaryRealm)
            {
                Task<Live<SkinInfo>>? import = null;
                AddStep("import a real archive of the five published candidate source files", () =>
                {
                    selection.Root = LocalStorage.GetFullPath($"c6-published-import-{Guid.NewGuid():N}");
                    import = Task.Run(async () =>
                    {
                        copyPublishedC6Candidate(selection.Root);
                        string archive = selection.Root + ".osk";
                        ZipFile.CreateFromDirectory(selection.Root, archive);
                        return await manager.Import(new ImportTask(archive)).ConfigureAwait(false);
                    });
                });
                AddUntilStep("the production skin importer completes", () => import?.IsCompleted == true);
                AddStep("select the exact imported skin", () =>
                {
                    selection.Candidate = import!.GetAwaiter().GetResult();
                    manager.CurrentSkinInfo.Value = selection.Candidate;
                });
                AddUntilStep("the imported candidate owns the current immutable publication", () =>
                    manager.CurrentSkinInfo.Value.ID == selection.Candidate.ID
                    && manager.CurrentSkin.Value.SkinInfo.ID == selection.Candidate.ID
                    && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            }
            else
                addSelectC6ScriptPackage(selection, source, copyPublishedC6Candidate);
            AddStep("open the actual Skin settings section", () =>
            {
                authorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("the actual script permission controls are loaded", () =>
            {
                settings ??= caller.Section.ChildrenOfType<SkinScriptSettings>().SingleOrDefault()!;
                return settings?.IsLoaded == true && settings.ChildrenOfType<SettingsButtonV2>().Count() == authorization.Requests.Count * 3;
            });
            AddStep("permanent network denial is visible and cannot be granted", () =>
            {
                Assert.That(authorization.RequiredSatisfied, Is.False);
                Assert.That(c6PermissionButton(settings, authorization, "network.read", 0).Enabled.Value, Is.False);
            });
            foreach (string id in new[] { "gameplay.snapshot.read", "gameplay.events.read", "scene.numeric.write", "math.random.read" })
            {
                AddStep($"grant {id} with its actual settings button", () => c6PermissionButton(settings, authorization, id, 0).TriggerClick());
                AddUntilStep($"{id} is persisted and effective", () => authorization.IsGranted(id)
                    && c6PermissionButton(settings, authorization, id, 0).Enabled.Value);
            }
            AddStep("mount both production consumers with real notes and engine clock", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager);
                renderer.ManiaDrawable.Beatmap.HitObjects.OfType<Note>().Single().StartTime = 2_000;
                gameplayClock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both actual candidate scene graphs become visible", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("advance the actual clock toward the first shared judgement", () =>
            {
                for (int time = 1_050; time <= 1_950; time += 50)
                    gameplayClock.Sample(time);
            });
            AddStep("hit the real BMS and mania objects at their judgement time", () =>
            {
                gameplayClock.Sample(2_000);
                c6Input(renderer, true);
                gameplayClock.Sample(2_020);
            });
            AddUntilStep("real judgements drive the authored momentum pulse", () =>
                c6CandidateNode(bms, "momentum.pulse").TransformDrawable.Rotation != 45
                && c6CandidateNode(mania, "momentum.pulse").TransformDrawable.Rotation != 45
                && c6CandidateNode(bms, "momentum.pulse").TransformDrawable.Scale.X > 1
                && c6CandidateNode(mania, "momentum.pulse").TransformDrawable.Scale.X > 1);
            AddStep("release the actual gameplay keys", () => c6Input(renderer, false));
            AddStep("measure the actual dual-host candidate frame path", () =>
            {
                int bmsHeap = bms.ScriptInstance!.Profiler.HeapBytes;
                int maniaHeap = mania.ScriptInstance!.Profiler.HeapBytes;
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                for (int frame = 1; frame <= 300; frame++)
                    gameplayClock.Sample(2_020 + frame * (1000.0 / 60));
                double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                TestContext.Progress.WriteLine($"C6 actual dual-host {source}: 300 frames, {elapsed:F3} ms, {allocated} thread allocated bytes; "
                                               + $"OS={Environment.OSVersion.VersionString}, logical CPUs={Environment.ProcessorCount}, 64-bit={Environment.Is64BitProcess}; "
                                               + $"VM heaps={bmsHeap}/{maniaHeap}, VM instructions={bms.ScriptInstance.Profiler.Instructions}/{mania.ScriptInstance.Profiler.Instructions}.");
                Assert.That(bms.ScriptInstance.Fault, Is.Null);
                Assert.That(mania.ScriptInstance.Fault, Is.Null);
                Assert.That(bms.ScriptInstance.Profiler.HeapBytes, Is.EqualTo(bmsHeap));
                Assert.That(mania.ScriptInstance.Profiler.HeapBytes, Is.EqualTo(maniaHeap));
                Assert.That(authorization.RuntimeReport.Instructions, Is.GreaterThan(0));
            });
            AddStep("revoke scene writes with the actual settings control during gameplay", () =>
                c6PermissionButton(settings, authorization, "scene.numeric.write", 2).TriggerClick());
            AddUntilStep("the durable token no longer permits scene writes", () => !authorization.IsGranted("scene.numeric.write"));
            AddUntilStep("both actual candidate scenes return to their declared base visuals", () =>
                c6CandidateNode(bms, "momentum.pulse").TransformDrawable.Rotation == 45
                && c6CandidateNode(mania, "momentum.pulse").TransformDrawable.Rotation == 45
                && c6CandidateNode(bms, "momentum.pulse").TransformDrawable.Scale.X == 1
                && c6CandidateNode(mania, "momentum.pulse").TransformDrawable.Scale.X == 1);
            AddStep("the candidate retains both ordinary gameplay visual fallbacks", () =>
            {
                Assert.That(bms.HostedSlots.Any(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Note && slot.AllowsProgrammaticVisual), Is.True);
                Assert.That(mania.HostedSlots.Any(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Note && slot.AllowsProgrammaticVisual), Is.True);
                Assert.That(c6PermissionButton(settings, authorization, "network.read", 0).Enabled.Value, Is.False);
            });
            AddStep("detach the real candidate and settings consumers", () =>
            {
                renderer.Expire();
                caller.Expire();
            });
            AddUntilStep("all candidate consumers leave the exact revision", () => renderer.Parent == null && caller.Parent == null);
        }

        private static SettingsButtonV2 c6PermissionButton(SkinScriptSettings settings, GameplaySkinScriptAuthorization authorization, string id, int action)
        {
            int row = authorization.Requests.Select((request, index) => (request, index)).Single(item => item.request.Id == id).index;
            return settings.ChildrenOfType<SettingsButtonV2>().ElementAt(row * 3 + action);
        }

        private static GameplaySkinSceneRuntimeNode c6CandidateNode(GameplaySkinSceneRuntimeHost scene, string id)
        {
            Assert.That(scene.TryGetRuntimeNode(id, out GameplaySkinSceneRuntimeNode? node), Is.True);
            return node!;
        }

        private static void copyPublishedC6Candidate(string root)
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(AppContext.BaseDirectory, "SkinC6Candidate");
            foreach (string filename in new[] { "skin.ini", "gameplay-skin.json", "gameplay-skin.scene.json", "gameplay-skin.script", "tile.png" })
                File.Copy(Path.Combine(source, filename), Path.Combine(root, filename));
        }
    }
}
