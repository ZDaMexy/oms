// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    [HeadlessTest]
    [Platform("Win")]
    public partial class TestSceneManagedSkinFolderRefresh : OsuGameTestScene
    {
        private Task<SkinManagedFolderScanResult> refresh = null!;
        private Guid skinId;

        public override void SetUpSteps()
        {
            AddStep("start a fresh installation", () =>
            {
                RecycleLocalStorage(false);
                CreateGame();
            });
            AddUntilStep("game loaded", () => Game.IsLoaded);
        }

        [Test]
        public void TestDiscoverUpdateRemoveAndRestoreWithoutRestart()
        {
            AddStep("place a skin in the fixed folder", () =>
            {
                string directory = LocalStorage.GetFullPath("chartskin/refresh-author");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "skin.ini"), "[General]\nName: Refresh author\nAuthor: OMS tests\n");
                refresh = Game.RefreshManagedSkinFoldersAsync();
            });
            AddUntilStep("new skin discovered", () => refresh.IsCompleted);
            AddAssert("refresh succeeded", () => refresh.GetAwaiter().GetResult().IsSuccess);
            AddAssert("new skin is selectable", () => Game.Realm.Run(r =>
            {
                SkinInfo record = r.All<SkinInfo>().Single(s => s.FilesystemStoragePath == "chartskin/refresh-author");
                skinId = record.ID;
                return record.Name == "Refresh author" && !record.DeletePending;
            }));

            AddStep("edit skin metadata and refresh", () =>
            {
                File.WriteAllText(LocalStorage.GetFullPath("chartskin/refresh-author/skin.ini"), "[General]\nName: Updated author\nAuthor: OMS tests\n");
                refresh = Game.RefreshManagedSkinFoldersAsync();
            });
            AddUntilStep("edited skin scanned", () => refresh.IsCompleted);
            AddAssert("existing identity receives edited name", () => refresh.GetAwaiter().GetResult().IsSuccess
                && Game.Realm.Run(r => r.Find<SkinInfo>(skinId)!.Name == "Updated author"));

            AddStep("move skin out of the folder and refresh", () =>
            {
                Directory.Move(LocalStorage.GetFullPath("chartskin/refresh-author"), LocalStorage.GetFullPath("removed-author"));
                refresh = Game.RefreshManagedSkinFoldersAsync();
            });
            AddUntilStep("missing skin scanned", () => refresh.IsCompleted);
            AddAssert("missing skin is removed from choices", () => refresh.GetAwaiter().GetResult().IsSuccess
                && Game.Realm.Run(r => r.Find<SkinInfo>(skinId)!.DeletePending));

            AddStep("return skin to the folder and refresh", () =>
            {
                Directory.Move(LocalStorage.GetFullPath("removed-author"), LocalStorage.GetFullPath("chartskin/refresh-author"));
                refresh = Game.RefreshManagedSkinFoldersAsync();
            });
            AddUntilStep("returned skin scanned", () => refresh.IsCompleted);
            AddAssert("returned skin retains its identity", () => refresh.GetAwaiter().GetResult().IsSuccess
                && Game.Realm.Run(r => !r.Find<SkinInfo>(skinId)!.DeletePending));
        }
    }
}
