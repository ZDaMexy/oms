// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinEventEnvelopeTest
    {
        [Test]
        public void TestV1ApiVersionIsStable()
        {
            Assert.That(GameplaySkinEventApiVersions.V1, Is.EqualTo(1));
        }

        [Test]
        public void TestCreatesImmutableEnvelopeWithFiniteNegativeGameplayTime()
        {
            var payload = new TestPayload(GameplaySkinEventDeliveryKind.Snapshot);
            GameplaySkinEventEnvelope envelope = GameplaySkinEventEnvelope.Create(
                GameplaySkinEventApiVersions.V1,
                7,
                12,
                -125.5,
                4,
                payload);

            Assert.Multiple(() =>
            {
                Assert.That(envelope.ApiVersion, Is.EqualTo(GameplaySkinEventApiVersions.V1));
                Assert.That(envelope.Epoch, Is.EqualTo(7));
                Assert.That(envelope.Sequence, Is.EqualTo(12));
                Assert.That(envelope.GameplayTime, Is.EqualTo(-125.5));
                Assert.That(envelope.LayoutRevision, Is.EqualTo(4));
                Assert.That(envelope.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                Assert.That(envelope.Payload, Is.SameAs(payload));
            });
        }

        [Test]
        public void TestRejectsInvalidEnvelopeValues()
        {
            TestPayload payload = snapshotPayload();

            Assert.Multiple(() =>
            {
                Assert.That(() => create(payload, apiVersion: 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(payload, apiVersion: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(payload, epoch: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(payload, sequence: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(payload, layoutRevision: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(payload, gameplayTime: double.NaN), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(payload, gameplayTime: double.PositiveInfinity), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(payload, gameplayTime: double.NegativeInfinity), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => create(null!), Throws.ArgumentNullException);
            });
        }

        [Test]
        public void TestPayloadRejectsUnsupportedDeliveryKind()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => new TestPayload(GameplaySkinEventDeliveryKind.Unspecified), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => new TestPayload((GameplaySkinEventDeliveryKind)99), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestPublicSurfaceIsEngineOwnedAndRulesetNeutral()
        {
            Type envelopeType = typeof(GameplaySkinEventEnvelope);
            Type payloadType = typeof(GameplaySkinEventPayload);
            string[] propertyTypeNames = envelopeType.GetProperties()
                                                     .Append(payloadType.GetProperty(nameof(GameplaySkinEventPayload.DeliveryKind))!)
                                                     .Select(property => property.PropertyType.FullName ?? string.Empty)
                                                     .ToArray();
            ConstructorInfo[] payloadConstructors = payloadType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.Multiple(() =>
            {
                Assert.That(envelopeType.IsSealed, Is.True);
                Assert.That(envelopeType.IsGenericType, Is.False);
                Assert.That(payloadType.IsAbstract, Is.True);
                Assert.That(envelopeType.GetConstructors(), Is.Empty);
                Assert.That(envelopeType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly), Is.Empty);
                Assert.That(envelopeType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(payloadType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(payloadConstructors, Has.Length.EqualTo(1));
                Assert.That(payloadConstructors.Single().IsAssembly, Is.True);
                Assert.That(propertyTypeNames.Any(name => name.Contains("Drawable", StringComparison.Ordinal)
                                                          || name.Contains("HitObject", StringComparison.Ordinal)
                                                          || name.Contains("Bindable", StringComparison.Ordinal)
                                                          || name.Contains("Rulesets.Bms", StringComparison.Ordinal)
                                                          || name.Contains("Rulesets.Mania", StringComparison.Ordinal)), Is.False);
            });
        }

        private static GameplaySkinEventEnvelope create(
            GameplaySkinEventPayload payload,
            int apiVersion = GameplaySkinEventApiVersions.V1,
            long epoch = 0,
            long sequence = 0,
            double gameplayTime = 0,
            long layoutRevision = 0)
            => GameplaySkinEventEnvelope.Create(apiVersion, epoch, sequence, gameplayTime, layoutRevision, payload);

        internal sealed class TestPayload : GameplaySkinEventPayload
        {
            public TestPayload(GameplaySkinEventDeliveryKind deliveryKind)
                : base(deliveryKind)
            {
            }
        }

        private static TestPayload snapshotPayload() => new TestPayload(GameplaySkinEventDeliveryKind.Snapshot);
    }
}
