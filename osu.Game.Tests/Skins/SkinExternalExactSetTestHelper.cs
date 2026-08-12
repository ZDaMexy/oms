// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Framework.Platform;

namespace osu.Game.Tests.Skins
{
    internal static class SkinExternalExactSetTestHelper
    {
        public static Guid AddServiceOwnedRecord(
            RealmAccess realm,
            Storage storage,
            string childName)
        {
            string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                storage.GetFullPath(Path.Combine("external-exact-set", childName))));
            Directory.CreateDirectory(path);
            File.WriteAllText(
                Path.Combine(path, "skin.ini"),
                "[General]\nName: External exact set\nAuthor: OMS tests\n");
            Guid recordId = Guid.NewGuid();

            realm.Write(r => r.Add(new SkinInfo("External exact set", "OMS tests")
            {
                ID = recordId,
                FilesystemStoragePath = path,
                IsExternalFilesystemStorage = true,
                FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
            }));
            return recordId;
        }

        public static void DriftDeclaration(RealmAccess realm, Guid recordId)
        {
            realm.Write(r =>
            {
                SkinInfo record = r.Find<SkinInfo>(recordId)!;
                record.FilesystemStoragePath += Path.DirectorySeparatorChar;
            });
        }
    }
}
