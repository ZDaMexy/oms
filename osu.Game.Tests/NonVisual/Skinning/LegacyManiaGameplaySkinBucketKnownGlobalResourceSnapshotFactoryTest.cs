// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactoryTest
    {
        [Test]
        public void TestMissingBucketIsAbsent()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "StageLeft: ignored-without-bucket\n");

            GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot> declaration =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(configurations, 4);

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Is.Empty);
                Assert.That(declaration.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestEmptyBucketDeclaresSnapshotWithAllResourcesAbsent()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SourceColumnCount, Is.EqualTo(4));
                assertAllResourcesAbsent(snapshot);
            });
        }

        [Test]
        public void TestAllExactKnownGlobalResourcesRemainDeclared()
        {
            Dictionary<string, string> expected = createAllResourceValues(string.Empty);
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                createResourceLines(expected));
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                assertAllResourcesEqual(snapshot, expected);
                Assert.That(configurations[0].ImageLookups, Is.EquivalentTo(expected));
            });
        }

        [Test]
        public void TestExplicitEmptyResourcesRemainDeclared()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "StageHint:\n" +
                "StageLeft:   \n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.HitTargetResource.IsDeclared, Is.True);
                Assert.That(snapshot.HitTargetResource.Value, Is.Empty);
                Assert.That(snapshot.LeftStageResource.IsDeclared, Is.True);
                Assert.That(snapshot.LeftStageResource.Value, Is.Empty);
                Assert.That(snapshot.RightStageResource.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestKnownResourceBeforeKeysIsAttributedToBucket()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "StageBottom: before-keys\n" +
                "Keys: 4\n");

            Assert.That(snapshot.BottomStageResource.Value, Is.EqualTo("before-keys"));
        }

        [Test]
        public void TestDuplicateKnownResourceUsesLastAcceptedValue()
        {
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                "LightingN: first\n" +
                "LightingN:\n");

            Assert.That(snapshot.ExplosionResource.Value, Is.Empty);
        }

        [Test]
        public void TestOnlyExactKnownGlobalKeysEnterClosedSnapshot()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "LightingNWidth: 1,2,3,4\n" +
                "LightingLWidth: 4,3,2,1\n" +
                "HitPosition: 402\n" +
                "Lightingn: compatibility-only\n" +
                "LightingNExtra: compatibility-only\n" +
                "Stageleft: compatibility-only\n" +
                "StagePaddingTop: compatibility-only\n" +
                "Hit300G: compatibility-only\n" +
                "Hit999: compatibility-only\n" +
                "lightingN: ignored\n" +
                "stageLeft: ignored\n" +
                "hit0: ignored\n");

            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                assertAllResourcesAbsent(snapshot);
                Assert.That(configurations[0].ImageLookups.Keys, Is.EquivalentTo(new[]
                {
                    "Lightingn",
                    "LightingNExtra",
                    "Stageleft",
                    "StagePaddingTop",
                    "Hit300G",
                    "Hit999",
                }));
                Assert.That(configurations[0].ExplosionWidth, Is.EqualTo(new[] { 1.6f, 3.2f, 4.8f, 6.4f }).Within(0.0001f));
                Assert.That(configurations[0].HoldNoteLightWidth, Is.EqualTo(new[] { 6.4f, 4.8f, 3.2f, 1.6f }).Within(0.0001f));
            });
        }

        [Test]
        public void TestSelectsOnlyExactDecodedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "StageLeft: four-key\n" +
                "[Mania]\n" +
                "Keys: 7\n" +
                "StageRight: seven-key\n");

            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot four =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(configurations, 4).Value;
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot seven =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(configurations, 7).Value;

            Assert.Multiple(() =>
            {
                Assert.That(four.LeftStageResource.Value, Is.EqualTo("four-key"));
                Assert.That(four.RightStageResource.IsDeclared, Is.False);
                Assert.That(seven.LeftStageResource.IsDeclared, Is.False);
                Assert.That(seven.RightStageResource.Value, Is.EqualTo("seven-key"));
            });
        }

        [Test]
        public void TestDiscardedDuplicateBucketDoesNotPolluteAcceptedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                "LightingN: accepted\n" +
                "Keys: 4\n" +
                "StageLeft: discarded\n");

            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(configurations, 4).Value;

            Assert.Multiple(() =>
            {
                Assert.That(configurations, Has.Count.EqualTo(1));
                Assert.That(snapshot.ExplosionResource.Value, Is.EqualTo("accepted"));
                Assert.That(snapshot.LeftStageResource.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestManualCompatibilityDictionaryMutationCannotForgeDecoderDeclaration()
        {
            var configuration = new LegacyManiaSkinConfiguration(4)
            {
                ImageLookups = createAllResourceValues("forged-"),
            };

            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(new[] { configuration }, 4).Value;

            assertAllResourcesAbsent(snapshot);
        }

        [Test]
        public void TestAcceptedValuesAreDetachedFromLaterCompatibilityDictionaryMutation()
        {
            Dictionary<string, string> expected = createAllResourceValues("accepted-");
            IReadOnlyList<LegacyManiaSkinConfiguration> configurations = decode(
                "[Mania]\n" +
                "Keys: 4\n" +
                createResourceLines(expected));

            configurations[0].ImageLookups["LightingN"] = "replaced";
            configurations[0].ImageLookups.Remove("StageLeft");
            configurations[0].ImageLookups = new Dictionary<string, string>
            {
                ["Hit300g"] = "forged",
                ["StageRight"] = "forged",
            };

            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot =
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(configurations, 4).Value;
            configurations[0].ImageLookups.Clear();

            assertAllResourcesEqual(snapshot, expected);
        }

        [Test]
        public void TestRejectsInvalidDecoderOutputOrColumnCount()
        {
            var duplicate = new[]
            {
                new LegacyManiaSkinConfiguration(4),
                new LegacyManiaSkinConfiguration(4),
            };
            LegacyManiaSkinConfiguration[] containsNull = { null! };

            Assert.Multiple(() =>
            {
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(null!, 4), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(containsNull, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(duplicate, 4), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSidecarRejectsUnknownCompositeOrNullResource()
        {
            var configuration = new LegacyManiaSkinConfiguration(4);
            configuration.AcceptKnownGlobalResource(LegacyManiaSkinKnownGlobalResourceField.LightingN, "accepted");

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.AcceptKnownGlobalResource(0, "value"), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptKnownGlobalResource(
                    LegacyManiaSkinKnownGlobalResourceField.LightingN | LegacyManiaSkinKnownGlobalResourceField.StageLeft,
                    "value"), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptKnownGlobalResource(
                    LegacyManiaSkinKnownGlobalResourceField.LightingN, null!), Throws.ArgumentNullException);
                Assert.That(configuration.AcceptedLightingNResource.Value, Is.EqualTo("accepted"));
                Assert.That(configuration.ImageLookups, Is.EquivalentTo(new Dictionary<string, string>
                {
                    ["LightingN"] = "accepted",
                }));
            });
        }

        [Test]
        public void TestPublicSurfaceRemainsClosedSourceSpecificAndImmutable()
        {
            Type snapshotType = typeof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot);
            string[] propertyNames = snapshotType.GetProperties().Select(property => property.Name).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(snapshotType.IsPublic && snapshotType.IsSealed, Is.True);
                Assert.That(snapshotType.GetConstructors(), Is.Empty);
                Assert.That(snapshotType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(propertyNames, Is.EquivalentTo(new[]
                {
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.SourceColumnCount),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.ExplosionResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.HoldNoteLightResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.LeftStageResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.RightStageResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.BottomStageResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.KeyFlashResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.HitTargetResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.MissJudgementResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.MehJudgementResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.OkJudgementResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.GoodJudgementResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.GreatJudgementResource),
                    nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.PerfectJudgementResource),
                }));
                Assert.That(snapshotType.GetProperties().Select(property => property.PropertyType),
                    Has.None.EqualTo(typeof(Dictionary<string, string>)));
                Assert.That(snapshotType.GetMethods().Select(method => method.GetParameters()).SelectMany(parameters => parameters),
                    Has.None.Property(nameof(System.Reflection.ParameterInfo.ParameterType)).EqualTo(typeof(string)));
                Assert.That(typeof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory).IsAbstract
                            && typeof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory).IsSealed, Is.True);
            });
        }

        [Test]
        public void TestSafeStringDoesNotExposeResourceNamesOrSourceKeys()
        {
            const string private_resource_name = "private-resource-name-987654";
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot = createSnapshot(
                "[Mania]\n" +
                "Keys: 4\n" +
                $"StageLeft: {private_resource_name}\n");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot)));
                Assert.That(snapshot.ToString(), Does.Not.Contain(private_resource_name));
                Assert.That(snapshot.ToString(), Does.Not.Contain("StageLeft"));
                Assert.That(snapshot.LeftStageResource.ToString(), Is.EqualTo("Declared"));
            });
        }

        private static LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot createSnapshot(string skinIni)
            => LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory.Create(decode(skinIni), 4).Value;

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }

        private static Dictionary<string, string> createAllResourceValues(string prefix) => new Dictionary<string, string>
        {
            ["LightingN"] = $"{prefix}lighting-normal",
            ["LightingL"] = $"{prefix}lighting-long-note",
            ["StageLeft"] = $"{prefix}stage-left:variant",
            ["StageRight"] = $"\"{prefix}stage-right\"",
            ["StageBottom"] = $"{prefix}stage-bottom",
            ["StageLight"] = $"{prefix}stage-light",
            ["StageHint"] = $"{prefix}stage-hint",
            ["Hit0"] = $"{prefix}hit-zero",
            ["Hit50"] = $"{prefix}hit-fifty",
            ["Hit100"] = $"{prefix}hit-one-hundred",
            ["Hit200"] = $"{prefix}hit-two-hundred",
            ["Hit300"] = $"{prefix}hit-three-hundred",
            ["Hit300g"] = $"{prefix}hit-geki",
        };

        private static string createResourceLines(IReadOnlyDictionary<string, string> resources)
        {
            var builder = new StringBuilder();

            foreach (KeyValuePair<string, string> resource in resources)
                builder.Append(resource.Key).Append(": ").Append(resource.Value).Append('\n');

            return builder.ToString();
        }

        private static void assertAllResourcesEqual(
            LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot,
            IReadOnlyDictionary<string, string> expected)
        {
            Assert.That(snapshot.ExplosionResource.Value, Is.EqualTo(expected["LightingN"]));
            Assert.That(snapshot.HoldNoteLightResource.Value, Is.EqualTo(expected["LightingL"]));
            Assert.That(snapshot.LeftStageResource.Value, Is.EqualTo(expected["StageLeft"]));
            Assert.That(snapshot.RightStageResource.Value, Is.EqualTo(expected["StageRight"]));
            Assert.That(snapshot.BottomStageResource.Value, Is.EqualTo(expected["StageBottom"]));
            Assert.That(snapshot.KeyFlashResource.Value, Is.EqualTo(expected["StageLight"]));
            Assert.That(snapshot.HitTargetResource.Value, Is.EqualTo(expected["StageHint"]));
            Assert.That(snapshot.MissJudgementResource.Value, Is.EqualTo(expected["Hit0"]));
            Assert.That(snapshot.MehJudgementResource.Value, Is.EqualTo(expected["Hit50"]));
            Assert.That(snapshot.OkJudgementResource.Value, Is.EqualTo(expected["Hit100"]));
            Assert.That(snapshot.GoodJudgementResource.Value, Is.EqualTo(expected["Hit200"]));
            Assert.That(snapshot.GreatJudgementResource.Value, Is.EqualTo(expected["Hit300"]));
            Assert.That(snapshot.PerfectJudgementResource.Value, Is.EqualTo(expected["Hit300g"]));
        }

        private static void assertAllResourcesAbsent(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot snapshot)
        {
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ExplosionResource.IsDeclared, Is.False);
                Assert.That(snapshot.HoldNoteLightResource.IsDeclared, Is.False);
                Assert.That(snapshot.LeftStageResource.IsDeclared, Is.False);
                Assert.That(snapshot.RightStageResource.IsDeclared, Is.False);
                Assert.That(snapshot.BottomStageResource.IsDeclared, Is.False);
                Assert.That(snapshot.KeyFlashResource.IsDeclared, Is.False);
                Assert.That(snapshot.HitTargetResource.IsDeclared, Is.False);
                Assert.That(snapshot.MissJudgementResource.IsDeclared, Is.False);
                Assert.That(snapshot.MehJudgementResource.IsDeclared, Is.False);
                Assert.That(snapshot.OkJudgementResource.IsDeclared, Is.False);
                Assert.That(snapshot.GoodJudgementResource.IsDeclared, Is.False);
                Assert.That(snapshot.GreatJudgementResource.IsDeclared, Is.False);
                Assert.That(snapshot.PerfectJudgementResource.IsDeclared, Is.False);
            });
        }
    }
}
