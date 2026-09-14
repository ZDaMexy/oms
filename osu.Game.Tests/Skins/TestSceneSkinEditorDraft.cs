// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Skinning.Windows;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [HeadlessTest]
    [Platform("Win")]
    public partial class TestSceneSkinEditorDraft : OsuTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private SkinManager manager = null!;
        private Skin? draft;
        private Live<SkinInfo>? candidate;
        private Task? import;
        private MemoryStream? sourceArchive;
        private string packageDirectory = string.Empty;

        protected override bool UseFreshStoragePerRun => true;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create isolated manager", () =>
            {
                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                draft = null;
                candidate = null;
                import = null;
                sourceArchive = null;
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("dispose draft and manager", () =>
            {
                draft?.Dispose();
                sourceArchive?.Dispose();
                manager.ShutdownManagedFolderMutations();
            });
        }

        [TestCase("default")]
        [TestCase("realm")]
        [TestCase("folder")]
        public void TestDraftPreservesResourcesAndDoesNotPublish(string sourceKind)
        {
            Skin original = null!;
            SkinCurrentRevision revision = null!;
            MemoryStream originalArchive = null!;
            Task<Skin> draftTask = null!;
            AddStep("prepare source", () =>
            {
                if (sourceKind == "default")
                    return;

                packageDirectory = LocalStorage.GetFullPath("chartskin/editor-source");
                Directory.CreateDirectory(Path.Combine(packageDirectory, "scripts"));
                File.WriteAllText(Path.Combine(packageDirectory, "skin.ini"), "[General]\nName: Editor source\nAuthor: OMS tests\n");
                File.WriteAllText(Path.Combine(packageDirectory, "scripts", "author-data.json"), "{\"preserve\":true}");

                if (sourceKind == "folder")
                {
                    var scanner = new SkinManagedFolderScanner(Realm, new WindowsSkinManagedFolderDiscoverySource(LocalStorage), manager.ManagedFolderOperationCoordinator);
                    Assert.That(scanner.Scan().IsSuccess, Is.True);
                    candidate = manager.Query(info => info.FilesystemStoragePath == "chartskin/editor-source");
                    manager.CurrentSkinInfo.Value = candidate;
                }
                else
                {
                    sourceArchive = new MemoryStream();
                    using (var zip = new ZipArchive(sourceArchive, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        foreach (string file in Directory.GetFiles(packageDirectory, "*", SearchOption.AllDirectories))
                        {
                            using Stream input = File.OpenRead(file);
                            using Stream output = zip.CreateEntry(Path.GetRelativePath(packageDirectory, file).Replace('\\', '/')).Open();
                            input.CopyTo(output);
                        }
                    }
                    sourceArchive.Position = 0;
                    import = importAndSelect();
                }
            });
            AddUntilStep("source selected", () => sourceKind == "default"
                || (import == null || import.IsCompletedSuccessfully) && candidate != null
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);

            AddStep("create independent editable copy", () =>
            {
                original = manager.CurrentSkin.Value;
                revision = manager.CurrentRevision;
                originalArchive = new MemoryStream();
                original.WriteEditorArchive(originalArchive, new SkinInfo { Name = "source" });

                // A draft of a selected folder must use its captured revision, not reopen changed author files.
                if (sourceKind == "folder")
                    File.WriteAllText(Path.Combine(packageDirectory, "scripts", "author-data.json"), "changed after publication");

                draftTask = manager.CreateEditorDraftAsync();
            });
            AddUntilStep("editable copy prepared", () => draftTask.IsCompleted);
            AddStep("verify independent editable copy", () =>
            {
                draft = draftTask.GetAwaiter().GetResult();
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(original));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                    Assert.That(draft.SkinInfo.ID, Is.Not.EqualTo(original.SkinInfo.ID));
                    Assert.That(draft.SkinInfo.IsManaged, Is.True);
                    Assert.That(draft.SkinInfo.Value.Protected, Is.False);
                    Assert.That(draft.SkinInfo.Value.FilesystemStoragePath, Is.Null.Or.Empty);
                    Assert.That(draft.Name, Does.Contain("modified"));
                });

                originalArchive.Position = 0;
                using var entries = new ZipArchive(originalArchive, ZipArchiveMode.Read);
                foreach (ZipArchiveEntry entry in entries.Entries.Where(entry => entry.FullName != "skininfo.json" && entry.FullName != "skin.ini"))
                {
                    using Stream input = entry.Open();
                    using var bytes = new MemoryStream();
                    input.CopyTo(bytes);
                    Assert.That(draft.TryCaptureGameplaySkinResource(entry.FullName, 64 * 1024 * 1024, out byte[] copied), Is.True, entry.FullName);
                    Assert.That(copied, Is.EqualTo(bytes.ToArray()), entry.FullName);
                }

                using var edit = new MemoryStream(Encoding.UTF8.GetBytes("edited draft resource"));
                draft.SkinInfo.PerformWrite(info => manager.AddFile(info, edit, "editor-data.txt", info.Realm!));
                manager.Save(draft);
                Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                Assert.That(draft.TryCaptureGameplaySkinResource("editor-data.txt", 1024, out byte[] edited), Is.True);
                Assert.That(Encoding.UTF8.GetString(edited), Is.EqualTo("edited draft resource"));
                Assert.Throws<InvalidOperationException>(() => manager.Save(original));
                originalArchive.Dispose();
            });
        }

        private async Task importAndSelect()
        {
            candidate = await manager.Import(new ImportTask(sourceArchive!, "Editor source.osk"), new ImportParameters { ImportImmediately = true });
            Schedule(() => manager.CurrentSkinInfo.Value = candidate!);
        }
    }
}
