// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinDocumentSkinIntegrationTest
    {
        [Test]
        public void TestExactConfigurationIsTokenizedOnceAndSharedWithLegacyAdapters()
        {
            const string configuration = """
                                         [General] // legacy header comment remains compatible
                                         Name: Shared Token Skin // ignored: forged name

                                         [Mania]
                                         Keys: 5 // ignored: 9
                                         NoteImage0: compatibility-note // ignored: forged-resource

                                         [GameplaySkin.Common:1]
                                         Target: Global ruleset=any keymode=any stage-mode=any
                                         bga.frame: resource Provide "c4/frame.png"
                                         """;

            byte[] exactBytes = new byte[] { 0xef, 0xbb, 0xbf }
                                .Concat(Encoding.UTF8.GetBytes(configuration))
                                .ToArray();
            var store = new MutableConfigurationStore(exactBytes);
            using var skin = new TestLegacySkin(store);

            GameplaySkinDocument document = skin.GameplaySkinDocument;
            string expectedRevision = Convert.ToHexString(SHA256.HashData(exactBytes)).ToLowerInvariant();
            GameplaySkinLegacyLine retainedName = document.LegacySections.Single(section => section.Name == "General").Lines
                                                                .Single(line => line.Kind == GameplaySkinLegacyLineKind.Field);
            GameplaySkinConfigurationDeclaration<string> noteResource = skin.ParsedManiaConfigurations.Single()
                .GetAcceptedLaneResource(LegacyManiaSkinLaneResourceField.Note, 0);

            Assert.Multiple(() =>
            {
                Assert.That(store.ConfigurationOpenCount, Is.EqualTo(1));
                Assert.That(document.Identity.ContentRevision, Is.EqualTo(expectedRevision));
                Assert.That(document.Sections, Has.Count.EqualTo(1));
                Assert.That(document.Sections.Single().Entries.Single().Descriptor, Is.SameAs(GameplaySkinSlotCatalog.BgaFrame));
                Assert.That(document.LegacySections.Select(section => section.Name), Does.Contain("General"));
                Assert.That(document.LegacySections.Select(section => section.Name), Does.Contain("Mania"));
                Assert.That(retainedName.NormalizedText, Does.Contain("ignored: forged name"));
                Assert.That(retainedName.Key, Is.EqualTo("Name"));
                Assert.That(retainedName.Value, Is.EqualTo("Shared Token Skin"));
                Assert.That(skin.Configuration.SkinInfo.Name, Is.EqualTo("Shared Token Skin"));
                Assert.That(skin.ParsedManiaConfigurations.Single().Keys, Is.EqualTo(5));
                Assert.That(noteResource.IsDeclared, Is.True);
                Assert.That(noteResource.Value, Is.EqualTo("compatibility-note"));
            });

            // Changing the backing store after construction cannot alter the exact immutable document or either
            // compatibility adapter. No consumer is allowed to reopen the author configuration.
            store.Replace(Encoding.UTF8.GetBytes("[General]\nName: Mutated\n"));

            Assert.Multiple(() =>
            {
                Assert.That(store.ConfigurationOpenCount, Is.EqualTo(1));
                Assert.That(skin.GameplaySkinDocument, Is.SameAs(document));
                Assert.That(skin.GameplaySkinDocument.Identity.ContentRevision, Is.EqualTo(expectedRevision));
                Assert.That(skin.Configuration.SkinInfo.Name, Is.EqualTo("Shared Token Skin"));
                Assert.That(skin.ParsedManiaConfigurations.Single().Keys, Is.EqualTo(5));
            });
        }

        private sealed class TestLegacySkin : LegacySkin
        {
            public IReadOnlyList<LegacyManiaSkinConfiguration> ParsedManiaConfigurations
                => GetParsedManiaConfigurationsForGameplaySkinCompatibility();

            public TestLegacySkin(IResourceStore<byte[]> store)
                : base(new SkinInfo(), null, store)
            {
            }
        }

        private sealed class MutableConfigurationStore : IResourceStore<byte[]>
        {
            private byte[] configuration;

            public int ConfigurationOpenCount { get; private set; }

            public MutableConfigurationStore(byte[] configuration)
            {
                this.configuration = configuration;
            }

            public void Replace(byte[] replacement) => configuration = replacement;

            public byte[] Get(string name) => name == "skin.ini" ? configuration.ToArray() : null!;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(name));

            public Stream? GetStream(string name)
            {
                if (name != "skin.ini")
                    return null;

                ConfigurationOpenCount++;
                return new MemoryStream(configuration.ToArray(), writable: false);
            }

            public IEnumerable<string> GetAvailableResources() => new[] { "skin.ini" };

            public void Dispose()
            {
            }
        }
    }
}
