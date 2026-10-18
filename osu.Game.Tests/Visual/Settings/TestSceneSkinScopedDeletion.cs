// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.Bms;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>Uses the real OsuGame selection subscriber so protected projections cannot rewrite saved scopes.</summary>
    [HeadlessTest]
    [Platform("Win")]
    public partial class TestSceneSkinScopedDeletion : OsuGameTestScene
    {
        private SkinManager skins => Game.Dependencies.Get<SkinManager>();
        private SkinSelectionPreferences preferences => skins.SelectionPreferences!;
        private readonly SkinSelectionScope scope = new SkinSelectionScope("bms", 7);
        private readonly SkinSelectionScope mode = new SkinSelectionScope("bms");
        private Live<SkinInfo> modeDefault = null!;
        private Live<SkinInfo> keySkin = null!;
        private Guid keySkinId;
        private string savedAssignments = string.Empty;
        private string managedDirectory = string.Empty;

        [TestCase(false)]
        [TestCase(true)]
        public void TestDeletingCurrentKeySkinPreservesAssignmentsAndFollowsModeDefault(bool managed)
        {
            Task<bool> deletion = null!;
            arrange(managed);
            AddStep("delete the current key-count skin through the manager", () => deletion = skins.DeleteSkinAsync(keySkinId));
            AddUntilStep("current deletion and protected transition complete", () => deletion.IsCompleted);
            AddStep("stored key skin remains distinct from the protected runtime projection", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deletion.GetAwaiter().GetResult(), Is.True);
                    Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(skins.CurrentSkin.Value, Is.SameAs(skins.DefaultOmsSkin));
                    Assert.That(skins.CurrentSkinScope, Is.EqualTo(scope));
                    Assert.That(preferences.GetConfiguredSkinId(scope), Is.EqualTo(keySkinId));
                    Assert.That(preferences.GetConfiguredSkinId(mode), Is.EqualTo(modeDefault.ID));
                    Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo(savedAssignments));
                    Assert.That(skins.GetConfiguredSkin(scope).ID, Is.EqualTo(modeDefault.ID));
                    Assert.That(skins.GetConfiguredSkin(new SkinSelectionScope("bms", 5)).ID, Is.EqualTo(modeDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("mania")), Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(skins.IsProjectingProtectedSkinTransition, Is.False);
                });
                if (managed)
                {
                    Assert.That(skins.Query(info => info.ID == keySkinId), Is.Null);
                    Assert.That(Directory.Exists(managedDirectory), Is.False);
                }
                else
                    Assert.That(keySkin.PerformRead(info => info.DeletePending), Is.True);
            });
        }

        [Test]
        public void TestFailedRealmDeleteKeepsAssignmentsDuringFallbackAndRestoresExactOriginal()
        {
            Task<bool> deletion = null!;
            Skin original = null!;
            SkinCurrentRevision originalRevision = null!;
            string? assignmentsAtFallback = null;
            Guid? modeAtFallback = null;
            bool protectedPairObserved = false;
            arrange(managed: false);
            AddStep("fail the existing Realm commit after the real protected fallback", () =>
            {
                original = skins.CurrentSkin.Value;
                originalRevision = skins.CurrentRevision;
                skins.CurrentRealmPackageDeleteBeforeRealmCommit = () =>
                {
                    protectedPairObserved = skins.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                                            && ReferenceEquals(skins.CurrentSkin.Value, skins.DefaultOmsSkin);
                    assignmentsAtFallback = Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides);
                    modeAtFallback = preferences.GetConfiguredSkinId(mode);
                    throw new IOException("test-only failure before the ordinary deletion Realm commit");
                };
                deletion = skins.DeleteSkinAsync(keySkinId);
            });
            AddUntilStep("failed deletion restores its exact original pair", () => deletion.IsCompleted);
            AddStep("fallback and rollback never replace the player's saved choice", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(deletion.GetAwaiter().GetResult(), Is.False);
                        Assert.That(protectedPairObserved, Is.True);
                        Assert.That(assignmentsAtFallback, Is.EqualTo(savedAssignments));
                        Assert.That(modeAtFallback, Is.EqualTo(modeDefault.ID));
                        Assert.That(skins.CurrentSkin.Value, Is.SameAs(original));
                        Assert.That(skins.CurrentRevision, Is.SameAs(originalRevision));
                        Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(keySkinId));
                        Assert.That(keySkin.PerformRead(info => info.DeletePending), Is.False);
                        Assert.That(preferences.GetConfiguredSkinId(scope), Is.EqualTo(keySkinId));
                        Assert.That(preferences.GetConfiguredSkinId(mode), Is.EqualTo(modeDefault.ID));
                        Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo(savedAssignments));
                        Assert.That(skins.GetConfiguredSkin(scope).ID, Is.EqualTo(keySkinId));
                        Assert.That(skins.IsProjectingProtectedSkinTransition, Is.False);
                    });
                }
                finally
                {
                    skins.CurrentRealmPackageDeleteBeforeRealmCommit = () => { };
                }
            });
        }

        private void arrange(bool managed)
        {
            Task<Live<SkinInfo>?> importing = null!;
            Task<SkinManagedFolderScanResult>? scanning = null;
            Task<bool> selection = null!;
            AddStep("import the independent BMS mode default", () => importing = skins.Import(new ImportTask(archive("Deletion mode default A"), "mode-a.osk")));
            AddUntilStep("mode default import completes", () => importing.IsCompleted);
            AddStep("prepare the distinct per-key package", () =>
            {
                modeDefault = importing.GetAwaiter().GetResult()!;
                if (managed)
                {
                    managedDirectory = Game.Storage.GetFullPath("chartskin/scoped-delete-b");
                    Directory.CreateDirectory(managedDirectory);
                    File.WriteAllText(Path.Combine(managedDirectory, "skin.ini"), skinIni("Deletion key skin B"), new UTF8Encoding(false));
                    scanning = Game.RefreshManagedSkinFoldersAsync();
                }
                else
                    importing = skins.Import(new ImportTask(archive("Deletion key skin B"), "key-b.osk"));
            });
            AddUntilStep("key package import or actual game discovery completes", () => managed ? scanning!.IsCompleted : importing.IsCompleted);
            AddStep("assign mode A and key B without changing the mode default", () =>
            {
                if (managed)
                {
                    Assert.That(scanning!.GetAwaiter().GetResult().IsSuccess, Is.True);
                    keySkin = skins.Query(info => info.FilesystemStoragePath == "chartskin/scoped-delete-b");
                    Assert.That(keySkin, Is.Not.Null);
                }
                else
                    keySkin = importing.GetAwaiter().GetResult()!;
                keySkinId = keySkin.ID;
                Game.Ruleset.Value = new BmsRuleset().RulesetInfo;
                preferences.SetSkin(mode, modeDefault.ID);
                preferences.SetSkin(new SkinSelectionScope("mania"), SkinInfo.OMS_SKIN);
                preferences.SetSkin(scope, keySkinId);
                preferences.SetSkin(new SkinSelectionScope("bms", 14), keySkinId);
                savedAssignments = Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides);
                selection = skins.ApplySkinScopeAsync(scope);
            });
            AddUntilStep("real scoped selection has completed", () => selection.IsCompleted);
            AddStep("verify current B and separate stored mode A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.GetAwaiter().GetResult(), Is.True);
                    Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(keySkinId));
                    Assert.That(skins.CurrentSkinScope, Is.EqualTo(scope));
                    Assert.That(preferences.GetConfiguredSkinId(scope), Is.EqualTo(keySkinId));
                    Assert.That(preferences.GetConfiguredSkinId(mode), Is.EqualTo(modeDefault.ID));
                });
                DismissAnyNotifications();
            });
        }

        private static string skinIni(string name)
            => $"[General]\nName: {name}\nAuthor: OMS tests\nVersion: 2.7\n\n[Bms]\nKeymode: 7K\n";

        private static MemoryStream archive(string name)
        {
            var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            using (var writer = new StreamWriter(zip.CreateEntry("skin.ini").Open(), new UTF8Encoding(false)))
                writer.Write(skinIni(name));
            output.Position = 0;
            return output;
        }
    }
}
