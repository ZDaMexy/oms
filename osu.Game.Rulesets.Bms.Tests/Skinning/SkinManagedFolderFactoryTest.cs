// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Text;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class SkinManagedFolderFactoryTest
    {
        private const string canonical_type = "osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms";

        [TestCase("")]
        [TestCase("osu.Game.Skinning.LegacySkin, osu.Game")]
        [TestCase("osu.Game.Skinning.TrianglesSkin, osu.Game")]
        [TestCase("osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms ")]
        [TestCase("OSU.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms")]
        [TestCase("osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
        [TestCase("missing.Type, missing.Assembly")]
        public void TestAllowlistRejectsEveryNonCanonicalType(string instantiationInfo)
        {
            Assert.That(SkinManagedFolderFactory.IsInstantiationInfoAllowed(instantiationInfo), Is.False);
        }

        [Test]
        public void TestCanonicalFactoryCreatesExactBmsSkin()
        {
            SkinPackageRevisionCapsule capsule = createCapsule("[Bms]\nKeymode: 7K\nPlayfieldWidth: 0.42\n");
            var info = new SkinInfo
            {
                Name = "captured",
                InstantiationInfo = canonical_type,
                FilesystemStoragePath = "chartskin/captured",
            };

            SkinManagedFolderFactoryResult result = SkinManagedFolderFactory.Create(info, new TestResourceProvider(), capsule);
            using Skin skin = result.Skin!;

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderFactoryRejectionReason.None));
                Assert.That(skin, Is.TypeOf<BmsLegacySkin>());
                Assert.That(((BmsLegacySkin)skin).GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.PlayfieldWidth, BmsKeymode.Key7K)?.Value, Is.EqualTo(0.42f));
                Assert.That(skin.SkinInfo.IsManaged, Is.False);
            });
        }

        [Test]
        public void TestRejectedFactoryDisposesTransferredCapsule()
        {
            SkinPackageRevisionCapsule capsule = createCapsule("[Bms]\nKeymode: 7K\n");
            var info = new SkinInfo
            {
                Name = "rejected",
                InstantiationInfo = "missing.Type, missing.Assembly",
                FilesystemStoragePath = "chartskin/rejected",
            };

            SkinManagedFolderFactoryResult result = SkinManagedFolderFactory.Create(info, new TestResourceProvider(), capsule);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderFactoryRejectionReason.InstantiationInfoNotAllowed));
                Assert.That(() => capsule.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
            });
        }

        [Test]
        public void TestMissingConfigurationRejectsAndDisposesTransferredCapsule()
        {
            SkinPackageRevisionCapsuleCreationResult creation = SkinPackageRevisionCapsuleFactory.Create(new[]
            {
                SkinPackageCapturedEntry.CreateFile("notes/note.png", new byte[] { 1, 2, 3 }),
            });
            SkinPackageRevisionCapsule capsule = creation.Capsule!;
            var info = new SkinInfo
            {
                Name = "missing configuration",
                InstantiationInfo = canonical_type,
                FilesystemStoragePath = "chartskin/missing-configuration",
            };

            SkinManagedFolderFactoryResult result = SkinManagedFolderFactory.Create(info, new TestResourceProvider(), capsule);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinManagedFolderFactoryRejectionReason.RequiredConfigurationMissing));
                Assert.That(() => capsule.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
            });
        }

        [Test]
        public void TestFilesystemRecordCannotUseHistoricalCreateInstanceFallback()
        {
            var folder = new SkinInfo
            {
                Name = "folder",
                InstantiationInfo = "missing.Type, missing.Assembly",
                FilesystemStoragePath = "chartskin/folder",
            };

            Assert.That(
                () => folder.CreateInstance(new TestResourceProvider()),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TestRealmRecordKeepsHistoricalCreateInstanceFallback()
        {
            var realmPackage = new SkinInfo
            {
                Name = "legacy missing type",
                InstantiationInfo = "missing.Type, missing.Assembly",
            };

            using Skin skin = realmPackage.CreateInstance(new TestResourceProviderWithFiles());
            Assert.That(skin, Is.TypeOf<TrianglesSkin>());
        }

        private static SkinPackageRevisionCapsule createCapsule(string ini)
        {
            SkinPackageRevisionCapsuleCreationResult creation = SkinPackageRevisionCapsuleFactory.Create(new[]
            {
                SkinPackageCapturedEntry.CreateFile("skin.ini", Encoding.UTF8.GetBytes(ini)),
            });

            Assert.That(creation.Capsule, Is.Not.Null);
            return creation.Capsule!;
        }

        private sealed class TestResourceProvider : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files => throw new AssertionException("The exact factory must not access Realm files.");
            public IResourceStore<byte[]> Resources { get; } = new ResourceStore<byte[]>();
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;
        }

        private sealed class TestResourceProviderWithFiles : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files { get; } = new ResourceStore<byte[]>();
            public IResourceStore<byte[]> Resources { get; } = new ResourceStore<byte[]>();
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;
        }
    }
}
