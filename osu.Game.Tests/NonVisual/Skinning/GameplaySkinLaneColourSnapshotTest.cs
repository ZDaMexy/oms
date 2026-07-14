// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinLaneColourSnapshotTest
    {
        [Test]
        public void TestClosedFieldCatalogUsesLaneSemantics()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinLaneColourFieldCatalog.All.Select(field => field.Id), Is.EqualTo(new[]
                {
                    "playfield.lane.background-colour",
                    "playfield.lane.light-colour",
                }));
                Assert.That(GameplaySkinLaneColourFieldCatalog.TryGet(
                    "playfield.lane.background-colour", out GameplaySkinLaneColourField? background), Is.True);
                Assert.That(background, Is.SameAs(GameplaySkinLaneColourFieldCatalog.LaneBackground));
                Assert.That(GameplaySkinLaneColourFieldCatalog.TryGet("Playfield.Lane.Background-Colour", out _), Is.False);
                Assert.That(GameplaySkinLaneColourFieldCatalog.TryGet("future.colour", out _), Is.False);
                Assert.That(GameplaySkinLaneColourFieldCatalog.TryGet(null, out _), Is.False);
                Assert.That(typeof(GameplaySkinLaneColourFieldCatalog).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                                     .SelectMany(method => method.GetParameters()),
                    Has.None.Property(nameof(ParameterInfo.ParameterType)).EqualTo(typeof(string)));
            });
        }

        [Test]
        public void TestMissingAndExplicitAlphaZeroRemainDistinct()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneColourSnapshot snapshot = GameplaySkinLaneColourSnapshot.Create(topology, new[]
            {
                GameplaySkinLaneColourDeclaration.Create(
                    firstLane,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground,
                    new Color4(1, 2, 3, 0)),
            });

            GameplaySkinConfigurationDeclaration<Color4> declared =
                snapshot.GetDeclaration(firstLane, GameplaySkinLaneColourFieldCatalog.LaneBackground);
            GameplaySkinConfigurationDeclaration<Color4> absent =
                snapshot.GetDeclaration(firstLane, GameplaySkinLaneColourFieldCatalog.LaneLight);

            Assert.Multiple(() =>
            {
                Assert.That(declared.IsDeclared, Is.True);
                Assert.That(declared.Value, Is.EqualTo(new Color4(1, 2, 3, 0)));
                Assert.That(absent.IsDeclared, Is.False);
                Assert.That(() => absent.Value, Throws.InvalidOperationException);
            });
        }

        [Test]
        public void TestSnapshotCopiesAndOrdersDeclarationsByLogicalLaneThenField()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneId secondLane = topology.LanesInLogicalOrder[1].Identity.Id;
            var input = new[]
            {
                GameplaySkinLaneColourDeclaration.Create(secondLane, GameplaySkinLaneColourFieldCatalog.LaneLight, Color4.Blue),
                GameplaySkinLaneColourDeclaration.Create(firstLane, GameplaySkinLaneColourFieldCatalog.LaneLight, Color4.Green),
                GameplaySkinLaneColourDeclaration.Create(firstLane, GameplaySkinLaneColourFieldCatalog.LaneBackground, Color4.Red),
            };

            GameplaySkinLaneColourSnapshot snapshot = GameplaySkinLaneColourSnapshot.Create(topology, input);
            input[0] = GameplaySkinLaneColourDeclaration.Create(firstLane, GameplaySkinLaneColourFieldCatalog.LaneBackground, Color4.White);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Declarations.Select(declaration => (declaration.LaneId.Value, declaration.Field.Id)), Is.EqualTo(new[]
                {
                    ("test.lane.first", "playfield.lane.background-colour"),
                    ("test.lane.first", "playfield.lane.light-colour"),
                    ("test.lane.second", "playfield.lane.light-colour"),
                }));
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneColourFieldCatalog.LaneLight).Value, Is.EqualTo(Color4.Blue));
                Assert.That(snapshot.Topology, Is.SameAs(topology));
            });
        }

        [Test]
        public void TestEquivalentLaneIdUsesOrdinalLookup()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneColourSnapshot snapshot = GameplaySkinLaneColourSnapshot.Create(topology, new[]
            {
                GameplaySkinLaneColourDeclaration.Create(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground,
                    Color4.Red),
            });

            Assert.That(snapshot.GetDeclaration(
                GameplaySkinLaneId.Create("test.lane.first"),
                GameplaySkinLaneColourFieldCatalog.LaneBackground).Value, Is.EqualTo(Color4.Red));
        }

        [Test]
        public void TestInvalidSnapshotInputsFailClosed()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId lane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneColourDeclaration declaration = GameplaySkinLaneColourDeclaration.Create(
                lane, GameplaySkinLaneColourFieldCatalog.LaneBackground, Color4.Red);
            GameplaySkinLaneColourDeclaration[] containsNull = { null! };
            var unknownField = new GameplaySkinLaneColourField("test.unknown-colour");

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneColourDeclaration.Create(null!, GameplaySkinLaneColourFieldCatalog.LaneBackground, Color4.Red),
                    Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneColourDeclaration.Create(lane, null!, Color4.Red), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneColourDeclaration.Create(lane, unknownField, Color4.Red), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneColourSnapshot.Create(null!, Array.Empty<GameplaySkinLaneColourDeclaration>()),
                    Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneColourSnapshot.Create(topology, null!), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneColourSnapshot.Create(topology, containsNull), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneColourSnapshot.Create(topology, new[] { declaration, declaration }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneColourSnapshot.Create(topology, new[]
                {
                    GameplaySkinLaneColourDeclaration.Create(
                        GameplaySkinLaneId.Create("test.lane.outside"),
                        GameplaySkinLaneColourFieldCatalog.LaneBackground,
                        Color4.Red),
                }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestInvalidQueriesFailClosed()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneColourSnapshot snapshot = GameplaySkinLaneColourSnapshot.Create(
                topology, Array.Empty<GameplaySkinLaneColourDeclaration>());
            var unknownField = new GameplaySkinLaneColourField("test.unknown-colour");

            Assert.Multiple(() =>
            {
                Assert.That(() => snapshot.GetDeclaration(null!, GameplaySkinLaneColourFieldCatalog.LaneBackground), Throws.ArgumentNullException);
                Assert.That(() => snapshot.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, null!), Throws.ArgumentNullException);
                Assert.That(() => snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("test.lane.outside"), GameplaySkinLaneColourFieldCatalog.LaneBackground), Throws.ArgumentException);
                Assert.That(() => snapshot.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, unknownField), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestClosedImmutableSurfaceAndSafeStrings()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneColourDeclaration declaration = GameplaySkinLaneColourDeclaration.Create(
                topology.LanesInLogicalOrder[0].Identity.Id,
                GameplaySkinLaneColourFieldCatalog.LaneBackground,
                new Color4(123, 45, 67, 89));
            GameplaySkinLaneColourSnapshot snapshot = GameplaySkinLaneColourSnapshot.Create(topology, new[] { declaration });

            Assert.Multiple(() =>
            {
                Assert.That(typeof(GameplaySkinLaneColourField).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneColourDeclaration).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneColourSnapshot).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneColourField).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneColourDeclaration).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneColourSnapshot).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneColourSnapshot).GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(() => ((IList<GameplaySkinLaneColourField>)GameplaySkinLaneColourFieldCatalog.All)[0] =
                    GameplaySkinLaneColourFieldCatalog.LaneLight, Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IList<GameplaySkinLaneColourDeclaration>)snapshot.Declarations)[0] = declaration,
                    Throws.TypeOf<NotSupportedException>());
                Assert.That(declaration.ToString(), Does.Not.Contain("123").And.Not.Contain("45").And.Not.Contain("67").And.Not.Contain("89"));
                Assert.That(snapshot.ToString(), Is.EqualTo(nameof(GameplaySkinLaneColourSnapshot)));
                Assert.That(GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot>.Declared(snapshot).ToString(), Is.EqualTo("Declared"));
            });
        }

        private static GameplaySkinLaneTopologySnapshot createTopology()
        {
            GameplaySkinLaneGroupIdentity group = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group.main"), GameplaySkinLaneSide.Neutral);
            var lanes = new[]
            {
                GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane.first"), group, GameplaySkinLaneRole.Key),
                    0, 0, 1, 1),
                GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane.second"), group, GameplaySkinLaneRole.Key),
                    1, 1, 0, 0),
            };

            return GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(group, 0, 0, lanes),
            });
        }
    }
}
