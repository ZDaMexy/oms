// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning;
using Realms;
using Realms.Schema;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public partial class SkinFilesystemStorageAuthorityOwnerTest
    {
        private const string realm_filename = "skin-authority-owner.realm";

        [Test]
        public void TestOwnerRoundTripsAsOpaqueValue()
        {
            runWithStorage(storage =>
            {
                Guid id = Guid.NewGuid();
                const string owner = "scanner:v1/Case-Sensitive+Opaque==";

                using (var realm = new RealmAccess(storage, realm_filename))
                {
                    realm.Write(r => r.Add(new SkinInfo("Managed folder", "OMS", typeof(LegacySkin).AssemblyQualifiedName)
                    {
                        ID = id,
                        FilesystemStoragePath = "chartskin/package",
                        FilesystemStorageAuthorityOwner = owner,
                    }));
                }

                using (var realm = new RealmAccess(storage, realm_filename))
                {
                    var stored = realm.Realm.Find<SkinInfo>(id)!;

                    Assert.Multiple(() =>
                    {
                        Assert.That(realm.Realm.Config.SchemaVersion, Is.EqualTo(57));
                        Assert.That(stored.FilesystemStoragePath, Is.EqualTo("chartskin/package"));
                        Assert.That(stored.FilesystemStorageAuthorityOwner, Is.EqualTo(owner));
                    });
                }
            });
        }

        [Test]
        public void TestSchema56MigrationLeavesExistingRecordUnowned()
        {
            runWithStorage(storage =>
            {
                Guid id = Guid.NewGuid();
                string legacyRealmPath = storage.GetFullPath(realm_filename, true);

                var oldConfiguration = new RealmConfiguration(legacyRealmPath)
                {
                    SchemaVersion = 56,
                    Schema = new RealmSchema.Builder
                    {
                        new ObjectSchema.Builder("Skin")
                        {
                            Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
                            Property.Primitive("Name", RealmValueType.String),
                            Property.Primitive("Creator", RealmValueType.String),
                            Property.Primitive("InstantiationInfo", RealmValueType.String),
                            Property.Primitive("Hash", RealmValueType.String),
                            Property.Primitive("Protected", RealmValueType.Bool),
                            Property.Primitive("FilesystemStoragePath", RealmValueType.String, isNullable: true),
                            Property.Primitive("IsExternalFilesystemStorage", RealmValueType.Bool),
                            Property.Primitive("DeletePending", RealmValueType.Bool),
                        }
                    }
                };

                using (var oldRealm = Realm.GetInstance(oldConfiguration))
                {
                    oldRealm.Write(() =>
                    {
                        dynamic skin = oldRealm.DynamicApi.CreateObject("Skin", (Guid?)id);
                        skin.Name = "Pre-owner managed folder";
                        skin.Creator = "OMS";
                        skin.InstantiationInfo = typeof(LegacySkin).AssemblyQualifiedName!;
                        skin.Hash = string.Empty;
                        skin.Protected = false;
                        skin.FilesystemStoragePath = "chartskin/pre-owner";
                        skin.IsExternalFilesystemStorage = false;
                        skin.DeletePending = false;
                    });
                }

                using (var realm = new RealmAccess(storage, realm_filename))
                {
                    var migrated = realm.Realm.All<SkinInfo>().Single(s => s.ID == id);

                    Assert.Multiple(() =>
                    {
                        Assert.That(realm.Realm.Config.SchemaVersion, Is.EqualTo(57));
                        Assert.That(migrated.Name, Is.EqualTo("Pre-owner managed folder"));
                        Assert.That(migrated.FilesystemStoragePath, Is.EqualTo("chartskin/pre-owner"));
                        Assert.That(migrated.FilesystemStorageAuthorityOwner, Is.Null,
                            "Schema migration must not claim records whose scanner authority is unknown.");
                    });
                }
            });
        }

        private static void runWithStorage([InstantHandle] Action<OsuStorage> action)
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost())
            {
                host.Run(new TestGame(() => action(new OsuStorage(host, host.Storage))));
            }
        }

        private partial class TestGame : Framework.Game
        {
            public TestGame([InstantHandle] Action action)
            {
                Scheduler.Add(() =>
                {
                    action();
                    Exit();
                });
            }
        }
    }
}
