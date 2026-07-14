// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinCapabilityNegotiatorTest
    {
        private static readonly string[] expected_hard_denied_ids =
        {
            "gameplay.mutation",
            "gameplay.input.inject",
            "gameplay.input.write",
            "gameplay.lane-order.write",
            "gameplay.lane-action.write",
            "gameplay.layout.write",
            "gameplay.judgement-line.write",
            "gameplay.lane-cover.write",
            "gameplay.scroll.write",
            "gameplay.timing.write",
            "gameplay.clock.write",
            "gameplay.judgement.write",
            "gameplay.score.write",
            "gameplay.combo.write",
            "gameplay.gauge.write",
            "gameplay.chart.write",
            "gameplay.beatmap.write",
            "gameplay.bga-timeline.write",
            "gameplay.bga-playback.write",
            "gameplay.bga-seek.write",
            "storage.realm.access",
            "storage.configuration.write",
            "host.network.request",
            "host.filesystem.arbitrary",
            "host.reflection",
            "host.process.spawn",
            "host.thread.create",
            "host.native-library.load",
        };

        private static IEnumerable<GameplaySkinCapabilityId> hardDeniedCapabilities => GameplaySkinCapabilityHardDenyCatalog.All;

        [TestCase("visual")]
        [TestCase("visual.scene")]
        [TestCase("gameplay.event-family-1.read")]
        public void TestCapabilityIdAcceptsCanonicalValue(string value)
        {
            GameplaySkinCapabilityId capabilityId = GameplaySkinCapabilityId.Create(value);

            Assert.Multiple(() =>
            {
                Assert.That(capabilityId.Value, Is.EqualTo(value));
                Assert.That(capabilityId.ToString(), Is.EqualTo(value));
            });
        }

        [Test]
        public void TestCapabilityIdRejectsNull()
        {
            Assert.That(() => GameplaySkinCapabilityId.Create(null!), Throws.ArgumentNullException);
        }

        [TestCase("")]
        [TestCase("Visual.scene")]
        [TestCase("1visual.scene")]
        [TestCase("visual.")]
        [TestCase(".visual")]
        [TestCase("visual..scene")]
        [TestCase("visual-")]
        [TestCase("visual scene")]
        [TestCase("visual_scene")]
        [TestCase("visual/scene")]
        [TestCase("visual\\scene")]
        [TestCase("视觉.scene")]
        public void TestCapabilityIdRejectsMalformedValue(string value)
        {
            Assert.That(() => GameplaySkinCapabilityId.Create(value), Throws.ArgumentException);
        }

        [Test]
        public void TestCapabilityIdUsesStrongOrdinalValueSemantics()
        {
            GameplaySkinCapabilityId first = id("test.visual-basic");
            GameplaySkinCapabilityId equivalent = id("test.visual-basic");
            GameplaySkinCapabilityId other = id("test.visual-other");

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(equivalent));
                Assert.That(first == equivalent, Is.True);
                Assert.That(first != other, Is.True);
                Assert.That(first.GetHashCode(), Is.EqualTo(equivalent.GetHashCode()));
                Assert.That(new HashSet<GameplaySkinCapabilityId> { first }.Contains(equivalent), Is.True);
            });
        }

        [Test]
        public void TestRequestIsSortedUniqueReadOnlyAndDefensive()
        {
            GameplaySkinCapabilityId first = id("test.capability-a");
            GameplaySkinCapabilityId second = id("test.capability-b");
            var source = new List<GameplaySkinCapabilityId> { second, first };

            GameplaySkinCapabilityRequest request = GameplaySkinCapabilityRequest.Create(source);
            source.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(request.CapabilityIds, Is.EqualTo(new[] { first, second }));
                Assert.That(request.CapabilityIds, Is.Not.InstanceOf<GameplaySkinCapabilityId[]>());
                Assert.That(() => ((IList<GameplaySkinCapabilityId>)request.CapabilityIds).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestRequestRejectsNullCollectionEntryAndDuplicate()
        {
            GameplaySkinCapabilityId capabilityId = id("test.capability");

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinCapabilityRequest.Create(null!), Throws.ArgumentNullException);
                Assert.That(
                    () => GameplaySkinCapabilityRequest.Create(new GameplaySkinCapabilityId[] { capabilityId, null! }),
                    Throws.ArgumentNullException);
                Assert.That(
                    () => GameplaySkinCapabilityRequest.Create(new[] { capabilityId, id("test.capability") }),
                    Throws.ArgumentException);
            });
        }

        [Test]
        public void TestEmptyRequestProducesEmptyNegotiation()
        {
            GameplaySkinCapabilityNegotiation result = negotiate(
                GameplaySkinCapabilityRequest.Create(Array.Empty<GameplaySkinCapabilityId>()),
                Array.Empty<GameplaySkinCapabilityDefinition>(),
                Array.Empty<string>(),
                Array.Empty<GameplaySkinCapabilityId>());

            Assert.Multiple(() =>
            {
                Assert.That(result.GrantedCapabilityIds, Is.Empty);
                Assert.That(result.Diagnostics, Is.Empty);
                Assert.That(result.ToString(), Is.EqualTo("Granted=0, Denied=0"));
            });
        }

        [Test]
        public void TestKnownAvailableCapabilityNeedsNoUnrequestedAuthorization()
        {
            GameplaySkinCapabilityId capabilityId = id("test.visual-basic");
            GameplaySkinCapabilityDefinition definition = automatic(capabilityId, "feature.visual-basic");

            GameplaySkinCapabilityNegotiation result = negotiate(request(capabilityId), new[] { definition }, new[] { "feature.visual-basic" });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsGranted(capabilityId), Is.True);
                Assert.That(result.GrantedCapabilityIds, Is.EqualTo(new[] { capabilityId }));
                Assert.That(result.Diagnostics, Is.Empty);
            });
        }

        [Test]
        public void TestUnavailableHostFeatureDeniesEvenWithStaleAuthorization()
        {
            GameplaySkinCapabilityId capabilityId = id("test.visual-authorised");
            GameplaySkinCapabilityDefinition definition = perSkin(capabilityId, "feature.visual-authorised");

            GameplaySkinCapabilityNegotiation result = negotiate(
                request(capabilityId), new[] { definition }, Array.Empty<string>(), new[] { capabilityId });

            assertSingleDenial(result, capabilityId, GameplaySkinCapabilityDiagnosticCode.HostFeatureUnavailable);
        }

        [TestCase(false, GameplaySkinCapabilityDiagnosticCode.PerSkinAuthorizationRequired)]
        [TestCase(true, null)]
        public void TestPerSkinCapabilityRequiresCurrentAuthorization(
            bool authorised,
            GameplaySkinCapabilityDiagnosticCode? expectedDiagnostic)
        {
            GameplaySkinCapabilityId capabilityId = id("test.visual-authorised");
            GameplaySkinCapabilityDefinition definition = perSkin(capabilityId, "feature.visual-authorised");
            GameplaySkinCapabilityId[] authorisations = authorised ? new[] { capabilityId } : Array.Empty<GameplaySkinCapabilityId>();

            GameplaySkinCapabilityNegotiation result = negotiate(
                request(capabilityId), new[] { definition }, new[] { "feature.visual-authorised" }, authorisations);

            if (expectedDiagnostic.HasValue)
                assertSingleDenial(result, capabilityId, expectedDiagnostic.Value);
            else
                Assert.That(result.IsGranted(capabilityId), Is.True);
        }

        [Test]
        public void TestUnknownCapabilityCannotBeCreatedByAvailabilityOrAuthorization()
        {
            GameplaySkinCapabilityId unknown = id("test.unknown");

            GameplaySkinCapabilityNegotiation result = negotiate(
                request(unknown), Array.Empty<GameplaySkinCapabilityDefinition>(), new[] { "test.unknown" }, new[] { unknown });

            assertSingleDenial(result, unknown, GameplaySkinCapabilityDiagnosticCode.UnknownCapability);
        }

        [Test]
        public void TestUnrequestedAvailableAndAuthorizedCapabilityIsNotGranted()
        {
            GameplaySkinCapabilityId capabilityId = id("test.visual-authorised");
            GameplaySkinCapabilityDefinition definition = perSkin(capabilityId, "feature.visual-authorised");

            GameplaySkinCapabilityNegotiation result = negotiate(
                GameplaySkinCapabilityRequest.Create(Array.Empty<GameplaySkinCapabilityId>()),
                new[] { definition },
                new[] { "feature.visual-authorised" },
                new[] { capabilityId });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsGranted(capabilityId), Is.False);
                Assert.That(result.GrantedCapabilityIds, Is.Empty);
                Assert.That(result.Diagnostics, Is.Empty);
            });
        }

        [TestCaseSource(nameof(hardDeniedCapabilities))]
        public void TestHardDeniedAuthorityCannotBeOverridden(GameplaySkinCapabilityId capabilityId)
        {
            var fakeAllowlistEntry = automatic(capabilityId, "feature.fake-supported");

            GameplaySkinCapabilityNegotiation result = negotiate(
                request(capabilityId),
                new[] { fakeAllowlistEntry },
                new[] { "feature.fake-supported" },
                new[] { capabilityId });

            assertSingleDenial(result, capabilityId, GameplaySkinCapabilityDiagnosticCode.HardDeniedAuthority);
        }

        [TestCase("gameplay.input.write")]
        [TestCase("gameplay.input.write.raw")]
        [TestCase("gameplay.input-write")]
        [TestCase("gameplay.replay.mutate")]
        [TestCase("gameplay.future.mutation")]
        [TestCase("gameplay.mutation.extra")]
        [TestCase("gameplay.score.update")]
        [TestCase("gameplay.clock.seek")]
        [TestCase("storage.realm.read")]
        [TestCase("storage.realm.write")]
        [TestCase("storage.configuration.profile.write")]
        [TestCase("host.network.open")]
        [TestCase("host.filesystem.arbitrary.read")]
        [TestCase("host.reflection.metadata")]
        [TestCase("host.process.inspect")]
        [TestCase("host.thread.pool")]
        [TestCase("host.native-library.open")]
        public void TestReservedAuthorityFamilyCannotBeGrantedThroughNearAlias(string value)
        {
            GameplaySkinCapabilityId capabilityId = id(value);
            var fakeAllowlistEntry = automatic(capabilityId, "feature.fake-supported");

            GameplaySkinCapabilityNegotiation result = negotiate(
                request(capabilityId),
                new[] { fakeAllowlistEntry },
                new[] { "feature.fake-supported" },
                new[] { capabilityId });

            assertSingleDenial(result, capabilityId, GameplaySkinCapabilityDiagnosticCode.HardDeniedAuthority);
        }

        [TestCase("gameplay.snapshot.read")]
        [TestCase("gameplay.event.read")]
        [TestCase("gameplay.lifecycle.reset.read")]
        [TestCase("gameplay.lifecycle.pause.read")]
        [TestCase("gameplay.object.create.read")]
        [TestCase("gameplay.event.trigger.read")]
        [TestCase("gameplay.event.seek.read")]
        [TestCase("gameplay.event.score-update.read")]
        [TestCase("visual.scene.update")]
        [TestCase("package.resource.read")]
        [TestCase("host.filesystem.package.read")]
        public void TestReadOnlyFixtureTokenIsNotMisclassifiedAsReservedAuthority(string value)
        {
            GameplaySkinCapabilityId capabilityId = id(value);
            var fakeAllowlistEntry = automatic(capabilityId, "feature.fake-supported");

            GameplaySkinCapabilityNegotiation result = negotiate(
                request(capabilityId), new[] { fakeAllowlistEntry }, new[] { "feature.fake-supported" });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsGranted(capabilityId), Is.True);
                Assert.That(result.Diagnostics, Is.Empty);
            });
        }

        [Test]
        public void TestHardDenyCatalogSnapshotIsUniqueAndReadOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinCapabilityHardDenyCatalog.All.Select(capabilityId => capabilityId.Value), Is.EqualTo(expected_hard_denied_ids));
                Assert.That(GameplaySkinCapabilityHardDenyCatalog.All.Distinct().Count(), Is.EqualTo(expected_hard_denied_ids.Length));
                Assert.That(GameplaySkinCapabilityHardDenyCatalog.All, Is.Not.InstanceOf<GameplaySkinCapabilityId[]>());
                Assert.That(() => ((IList<GameplaySkinCapabilityId>)GameplaySkinCapabilityHardDenyCatalog.All).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestMixedRequestKeepsIndependentDecisionsInOrdinalOrder()
        {
            GameplaySkinCapabilityId granted = id("test.capability-c");
            GameplaySkinCapabilityId unavailable = id("test.capability-b");
            GameplaySkinCapabilityId unknown = id("test.capability-a");

            GameplaySkinCapabilityNegotiation result = negotiate(
                request(granted, unknown, unavailable),
                new[]
                {
                    automatic(granted, "feature.granted"),
                    automatic(unavailable, "feature.unavailable"),
                },
                new[] { "feature.granted" });

            Assert.Multiple(() =>
            {
                Assert.That(result.GrantedCapabilityIds, Is.EqualTo(new[] { granted }));
                Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.CapabilityId), Is.EqualTo(new[] { unknown, unavailable }));
                Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[]
                {
                    GameplaySkinCapabilityDiagnosticCode.UnknownCapability,
                    GameplaySkinCapabilityDiagnosticCode.HostFeatureUnavailable,
                }));
            });
        }

        [Test]
        public void TestNegotiationSnapshotsMutableInputs()
        {
            GameplaySkinCapabilityId capabilityId = id("test.visual-basic");
            var definitions = new List<GameplaySkinCapabilityDefinition> { automatic(capabilityId, "feature.visual-basic") };
            var features = new List<string> { "feature.visual-basic" };
            var authorisations = new List<GameplaySkinCapabilityId>();

            GameplaySkinCapabilityNegotiation result = negotiate(request(capabilityId), definitions, features, authorisations);
            definitions.Clear();
            features.Clear();
            authorisations.Add(capabilityId);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsGranted(capabilityId), Is.True);
                Assert.That(result.Diagnostics, Is.Empty);
                Assert.That(() => ((IList<GameplaySkinCapabilityId>)result.GrantedCapabilityIds).Clear(), Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IList<GameplaySkinCapabilityDiagnostic>)result.Diagnostics).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestDefinitionFeatureAndAuthorizationOrderingAndDuplicatesHaveSetSemantics()
        {
            GameplaySkinCapabilityId first = id("test.capability-a");
            GameplaySkinCapabilityId second = id("test.capability-b");
            GameplaySkinCapabilityRequest capabilityRequest = request(second, first);
            GameplaySkinCapabilityDefinition firstDefinition = perSkin(first, "feature.shared");
            GameplaySkinCapabilityDefinition secondDefinition = automatic(second, "feature.shared");

            GameplaySkinCapabilityNegotiation forward = negotiate(
                capabilityRequest,
                new[] { firstDefinition, secondDefinition },
                new[] { "feature.shared", "feature.shared" },
                new[] { first, first });
            GameplaySkinCapabilityNegotiation reverse = negotiate(
                capabilityRequest,
                new[] { secondDefinition, firstDefinition },
                new[] { "feature.shared" },
                new[] { first });

            Assert.Multiple(() =>
            {
                Assert.That(forward.GrantedCapabilityIds, Is.EqualTo(new[] { first, second }));
                Assert.That(reverse.GrantedCapabilityIds, Is.EqualTo(forward.GrantedCapabilityIds));
                Assert.That(forward.Diagnostics, Is.Empty);
                Assert.That(reverse.Diagnostics, Is.Empty);
            });
        }

        [Test]
        public void TestNegotiationSnapshotRejectsContradictionsAndHardDeniedGrants()
        {
            GameplaySkinCapabilityId capabilityId = id("test.capability");
            GameplaySkinCapabilityId hardDenied = id("gameplay.input.write");
            var unknownDiagnostic = new GameplaySkinCapabilityDiagnostic(GameplaySkinCapabilityDiagnosticCode.UnknownCapability, capabilityId);
            var hardDeniedDiagnostic = new GameplaySkinCapabilityDiagnostic(GameplaySkinCapabilityDiagnosticCode.HardDeniedAuthority, hardDenied);

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => new GameplaySkinCapabilityNegotiation(new[] { capabilityId }, new[] { unknownDiagnostic }),
                    Throws.ArgumentException);
                Assert.That(
                    () => new GameplaySkinCapabilityNegotiation(
                        Array.Empty<GameplaySkinCapabilityId>(), new[] { unknownDiagnostic, unknownDiagnostic }),
                    Throws.ArgumentException);
                Assert.That(
                    () => new GameplaySkinCapabilityNegotiation(new[] { hardDenied }, Array.Empty<GameplaySkinCapabilityDiagnostic>()),
                    Throws.ArgumentException);
                Assert.That(
                    () => new GameplaySkinCapabilityNegotiation(
                        Array.Empty<GameplaySkinCapabilityId>(),
                        new[] { new GameplaySkinCapabilityDiagnostic(GameplaySkinCapabilityDiagnosticCode.HardDeniedAuthority, capabilityId) }),
                    Throws.ArgumentException);
                Assert.That(
                    () => new GameplaySkinCapabilityNegotiation(
                        Array.Empty<GameplaySkinCapabilityId>(),
                        new[] { new GameplaySkinCapabilityDiagnostic(GameplaySkinCapabilityDiagnosticCode.UnknownCapability, hardDenied) }),
                    Throws.ArgumentException);
                Assert.That(
                    () => new GameplaySkinCapabilityNegotiation(Array.Empty<GameplaySkinCapabilityId>(), new[] { hardDeniedDiagnostic }),
                    Throws.Nothing);
            });
        }

        [Test]
        public void TestReNegotiationAppliesRevocationAndFeatureRemovalWithoutMutatingOldSnapshot()
        {
            GameplaySkinCapabilityId capabilityId = id("test.visual-authorised");
            GameplaySkinCapabilityRequest capabilityRequest = request(capabilityId);
            GameplaySkinCapabilityDefinition definition = perSkin(capabilityId, "feature.visual-authorised");

            GameplaySkinCapabilityNegotiation granted = negotiate(
                capabilityRequest, new[] { definition }, new[] { "feature.visual-authorised" }, new[] { capabilityId });
            GameplaySkinCapabilityNegotiation revoked = negotiate(
                capabilityRequest, new[] { definition }, new[] { "feature.visual-authorised" });
            GameplaySkinCapabilityNegotiation featureRemoved = negotiate(
                capabilityRequest, new[] { definition }, Array.Empty<string>(), new[] { capabilityId });

            Assert.Multiple(() =>
            {
                Assert.That(granted.IsGranted(capabilityId), Is.True);
                Assert.That(granted.Diagnostics, Is.Empty);
                assertSingleDenial(revoked, capabilityId, GameplaySkinCapabilityDiagnosticCode.PerSkinAuthorizationRequired);
                assertSingleDenial(featureRemoved, capabilityId, GameplaySkinCapabilityDiagnosticCode.HostFeatureUnavailable);
            });
        }

        [Test]
        public void TestDefinitionAndNegotiatorInputsFailClosed()
        {
            GameplaySkinCapabilityId capabilityId = id("test.capability");
            GameplaySkinCapabilityDefinition definition = automatic(capabilityId, "feature.capability");

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => new GameplaySkinCapabilityDefinition(null!, "feature.capability", GameplaySkinCapabilityAccessPolicy.NoAdditionalAuthorization),
                    Throws.ArgumentNullException);
                Assert.That(
                    () => new GameplaySkinCapabilityDefinition(capabilityId, "Feature.capability", GameplaySkinCapabilityAccessPolicy.NoAdditionalAuthorization),
                    Throws.ArgumentException);
                Assert.That(
                    () => new GameplaySkinCapabilityDefinition(capabilityId, "feature.capability", GameplaySkinCapabilityAccessPolicy.Unspecified),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(
                    () => new GameplaySkinCapabilityDefinition(capabilityId, "feature.capability", (GameplaySkinCapabilityAccessPolicy)99),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(
                    () => negotiate(request(capabilityId), new[] { definition, automatic(id("test.capability"), "feature.other") }, new[] { "feature.capability" }),
                    Throws.ArgumentException);
                Assert.That(
                    () => negotiate(request(capabilityId), new GameplaySkinCapabilityDefinition[] { definition, null! }, new[] { "feature.capability" }),
                    Throws.ArgumentNullException);
                Assert.That(
                    () => negotiate(request(capabilityId), new[] { definition }, new string[] { "feature.capability", null! }),
                    Throws.ArgumentNullException);
                Assert.That(
                    () => negotiate(request(capabilityId), new[] { definition }, new[] { "Feature.capability" }),
                    Throws.ArgumentException);
                Assert.That(
                    () => negotiate(request(capabilityId), new[] { definition }, new[] { "feature.capability" }, new GameplaySkinCapabilityId[] { null! }),
                    Throws.ArgumentNullException);
            });
        }

        [Test]
        public void TestDiagnosticContainsOnlyStableCodeAndCapabilityId()
        {
            GameplaySkinCapabilityId capabilityId = id("test.private-token");
            GameplaySkinCapabilityNegotiation result = negotiate(
                request(capabilityId), Array.Empty<GameplaySkinCapabilityDefinition>(), Array.Empty<string>());
            GameplaySkinCapabilityDiagnostic diagnostic = result.Diagnostics.Single();
            string serialised = JsonConvert.SerializeObject(diagnostic);

            Assert.Multiple(() =>
            {
                Assert.That(diagnostic.ToString(), Is.EqualTo($"UnknownCapability: {capabilityId}"));
                Assert.That(serialised, Does.Contain("\"Code\":"));
                Assert.That(serialised, Does.Contain(capabilityId.Value));
                Assert.That(serialised, Does.Not.Contain("Package"));
                Assert.That(serialised, Does.Not.Contain("Path"));
                Assert.That(serialised, Does.Not.Contain("Exception"));
                Assert.That(typeof(GameplaySkinCapabilityDiagnostic).GetProperties().Select(property => property.Name),
                    Is.EquivalentTo(new[] { "Code", "CapabilityId" }));
            });
        }

        [Test]
        public void TestPublicSurfaceIsImmutableDecisionOnlyAndDefersActivationPolicy()
        {
            Type[] publicTypes =
            {
                typeof(GameplaySkinCapabilityId),
                typeof(GameplaySkinCapabilityRequest),
                typeof(GameplaySkinCapabilityDiagnostic),
                typeof(GameplaySkinCapabilityNegotiation),
            };
            string[] propertyTypeNames = publicTypes.SelectMany(type => type.GetProperties())
                                                    .Select(property => property.PropertyType.FullName ?? property.PropertyType.Name)
                                                    .ToArray();
            string[] propertyNames = publicTypes.SelectMany(type => type.GetProperties())
                                                .Select(property => property.Name)
                                                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(publicTypes, Is.All.Matches<Type>(type => type.IsSealed));
                Assert.That(publicTypes.SelectMany(type => type.GetProperties()).Select(property => property.SetMethod), Is.All.Null);
                Assert.That(typeof(GameplaySkinCapabilityRequest).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinCapabilityDiagnostic).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinCapabilityNegotiation).GetConstructors(), Is.Empty);
                Assert.That(
                    typeof(GameplaySkinCapabilityRequest).GetMethod(
                        "Create", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public),
                    Is.Null);
                Assert.That(
                    typeof(GameplaySkinCapabilityRequest).GetMethod(
                        "Create", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic),
                    Is.Not.Null);
                Assert.That(typeof(GameplaySkinCapabilityDefinition).IsNotPublic, Is.True);
                Assert.That(typeof(GameplaySkinCapabilityAccessPolicy).IsNotPublic, Is.True);
                Assert.That(typeof(GameplaySkinCapabilityNegotiator).IsNotPublic, Is.True);
                Assert.That(typeof(GameplaySkinCapabilityHardDenyCatalog).IsNotPublic, Is.True);
                Assert.That(propertyNames.Intersect(new[]
                {
                    "Package",
                    "Path",
                    "Exception",
                    "Service",
                    "Handle",
                    "Delegate",
                    "Requirement",
                    "Required",
                    "Optional",
                    "CanActivate",
                    "IsActive",
                }), Is.Empty);
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Drawable"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("HitObject"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Judgement"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Score"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Gauge"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Realm"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Bindable"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Clock"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Bms"));
                Assert.That(propertyTypeNames, Is.All.Not.Contains("Mania"));
            });
        }

        private static GameplaySkinCapabilityId id(string value) => GameplaySkinCapabilityId.Create(value);

        private static GameplaySkinCapabilityRequest request(params GameplaySkinCapabilityId[] capabilityIds)
            => GameplaySkinCapabilityRequest.Create(capabilityIds);

        private static GameplaySkinCapabilityDefinition automatic(GameplaySkinCapabilityId capabilityId, string featureId)
            => new GameplaySkinCapabilityDefinition(
                capabilityId,
                featureId,
                GameplaySkinCapabilityAccessPolicy.NoAdditionalAuthorization);

        private static GameplaySkinCapabilityDefinition perSkin(GameplaySkinCapabilityId capabilityId, string featureId)
            => new GameplaySkinCapabilityDefinition(
                capabilityId,
                featureId,
                GameplaySkinCapabilityAccessPolicy.PerSkinAuthorization);

        private static GameplaySkinCapabilityNegotiation negotiate(
            GameplaySkinCapabilityRequest capabilityRequest,
            IEnumerable<GameplaySkinCapabilityDefinition> definitions,
            IEnumerable<string> availableFeatures,
            IEnumerable<GameplaySkinCapabilityId>? authorisations = null)
            => GameplaySkinCapabilityNegotiator.Negotiate(
                capabilityRequest,
                definitions,
                availableFeatures,
                authorisations ?? Array.Empty<GameplaySkinCapabilityId>());

        private static void assertSingleDenial(
            GameplaySkinCapabilityNegotiation result,
            GameplaySkinCapabilityId capabilityId,
            GameplaySkinCapabilityDiagnosticCode expectedCode)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.GrantedCapabilityIds, Is.Empty);
                Assert.That(result.IsGranted(capabilityId), Is.False);
                Assert.That(result.IsGranted(null), Is.False);
                Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(result.Diagnostics[0].CapabilityId, Is.EqualTo(capabilityId));
                Assert.That(result.Diagnostics[0].Code, Is.EqualTo(expectedCode));
            });
        }
    }
}
