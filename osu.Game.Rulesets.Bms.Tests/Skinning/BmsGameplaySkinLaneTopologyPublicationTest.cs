// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public sealed class BmsGameplaySkinLaneTopologyPublicationTest
    {
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, BmsPlayfieldStyle.P2, true)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.Center, BmsPlayfieldStyle.CenterRightScratch, true)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, BmsPlayfieldStyle.P2, true)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center, BmsPlayfieldStyle.CenterRightScratch, true)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, BmsPlayfieldStyle.Center, false)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2, BmsPlayfieldStyle.CenterRightScratch, false)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, BmsPlayfieldStyle.Center, false)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, BmsPlayfieldStyle.CenterRightScratch, false)]
        public void TestSinglePlayStyleTransitionPublishesNextRevision(
            BmsKeymode keymode,
            BmsPlayfieldStyle previousStyle,
            BmsPlayfieldStyle currentStyle,
            bool visualOrderChanges)
        {
            var owner = new BmsGameplaySkinLaneTopologyRevisionOwner();
            BmsGameplaySkinLaneTopologyPublication previous = owner.Publish(BmsLaneLayout.CreateForKeymode(keymode, style: previousStyle));
            BmsGameplaySkinLaneTopologyPublication current = owner.Publish(BmsLaneLayout.CreateForKeymode(keymode, style: currentStyle));

            Assert.Multiple(() =>
            {
                Assert.That(previous.Publication.Revision, Is.Zero);
                Assert.That(current.Publication.Revision, Is.EqualTo(1));
                Assert.That(current.Keymode, Is.EqualTo(keymode));
                Assert.That(current.AppliedStyle, Is.EqualTo(currentStyle));
                Assert.That(owner.Current, Is.SameAs(current));
                Assert.That(previous.Publication.Topology, Is.Not.SameAs(current.Publication.Topology));
                Assert.That(
                    previous.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id),
                    Is.EqualTo(current.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id)));
                Assert.That(
                    previous.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex),
                    Is.EqualTo(current.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex)));
                Assert.That(
                    previous.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex),
                    visualOrderChanges
                        ? Is.Not.EqualTo(current.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex))
                        : Is.EqualTo(current.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex)));
            });
        }

        [Test]
        public void TestNineKeyNativeContextChangeIsRejectedAtomically()
        {
            var owner = new BmsGameplaySkinLaneTopologyRevisionOwner();
            BmsGameplaySkinLaneTopologyPublication accepted = owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Bms));
            GameplaySkinLaneTopologySnapshot pmsTopology = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Pms)).Topology;

            Assert.Multiple(() =>
            {
                Assert.That(
                    accepted.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id),
                    Is.EqualTo(pmsTopology.LanesInLogicalOrder.Select(lane => lane.Identity.Id)));
                Assert.That(
                    accepted.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role),
                    Is.EqualTo(pmsTopology.LanesInLogicalOrder.Select(lane => lane.Identity.Role)));
                Assert.That(
                    () => GameplaySkinLaneTopologyTransitionValidator.Validate(accepted.Publication.Topology, pmsTopology),
                    Throws.Nothing,
                    "The native keymode gate, rather than the neutral topology validator, must close this blind spot.");
            });

            Assert.That(
                () => owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Pms)),
                Throws.TypeOf<ArgumentException>()
                      .With.Property(nameof(ArgumentException.ParamName)).EqualTo("nativeContext"));

            Assert.Multiple(() =>
            {
                Assert.That(owner.Current, Is.SameAs(accepted));
                Assert.That(owner.Current!.Keymode, Is.EqualTo(BmsKeymode.Key9K_Bms));
                Assert.That(owner.Current.Publication.Revision, Is.Zero);
                Assert.That(owner.Current.Publication.Topology, Is.SameAs(accepted.Publication.Topology));
            });

            BmsGameplaySkinLaneTopologyPublication current = owner.Publish(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Bms));

            Assert.That(current.Publication.Revision, Is.EqualTo(1));
        }

        [Test]
        public void TestKeyCountChangeIsRejectedAtomically()
        {
            var owner = new BmsGameplaySkinLaneTopologyRevisionOwner();
            BmsGameplaySkinLaneTopologyPublication accepted = owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key5K));

            Assert.That(
                () => owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K)),
                Throws.TypeOf<ArgumentException>()
                      .With.Property(nameof(ArgumentException.ParamName)).EqualTo("nativeContext"));

            Assert.Multiple(() =>
            {
                Assert.That(owner.Current, Is.SameAs(accepted));
                Assert.That(owner.Current!.Keymode, Is.EqualTo(BmsKeymode.Key5K));
                Assert.That(owner.Current.Publication.Revision, Is.Zero);
            });
        }

        [Test]
        public void TestFourteenKeyIndependentRebuildPublishesNextRevision()
        {
            var owner = new BmsGameplaySkinLaneTopologyRevisionOwner();
            BmsGameplaySkinLaneTopologyPublication previous = owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key14K));
            BmsGameplaySkinLaneTopologyPublication current = owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key14K));

            Assert.Multiple(() =>
            {
                Assert.That(previous.Publication.Revision, Is.Zero);
                Assert.That(current.Publication.Revision, Is.EqualTo(1));
                Assert.That(current.Keymode, Is.EqualTo(BmsKeymode.Key14K));
                Assert.That(current.AppliedStyle, Is.EqualTo(BmsPlayfieldStyle.Center));
                Assert.That(owner.Current, Is.SameAs(current));
                Assert.That(previous.Publication.Topology, Is.Not.SameAs(current.Publication.Topology));
                Assert.That(
                    previous.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id),
                    Is.EqualTo(current.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id)));
            });
        }

        [Test]
        public void TestInvalidProvisionalLayoutDoesNotAdvanceRevision()
        {
            var owner = new BmsGameplaySkinLaneTopologyRevisionOwner();
            BmsGameplaySkinLaneTopologyPublication accepted = owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K));
            BmsLaneLayout invalid = BmsLaneLayout.CreateForKeymode(
                BmsKeymode.Key7K,
                scratchLaneIndices: new[] { 7 }.ToHashSet());

            Assert.That(() => owner.Publish(invalid), Throws.ArgumentException);

            Assert.Multiple(() =>
            {
                Assert.That(owner.Current, Is.SameAs(accepted));
                Assert.That(owner.Current!.Publication.Revision, Is.Zero);
            });

            BmsGameplaySkinLaneTopologyPublication current = owner.Publish(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K, style: BmsPlayfieldStyle.P2));

            Assert.Multiple(() =>
            {
                Assert.That(current.Publication.Revision, Is.EqualTo(1));
                Assert.That(owner.Current, Is.SameAs(current));
            });
        }

        [Test]
        public void TestWrapperAndOwnerRemainInternalAndRuntimeNeutral()
        {
            var owner = new BmsGameplaySkinLaneTopologyRevisionOwner();
            BmsGameplaySkinLaneTopologyPublication publication = owner.Publish(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K));
            Type[] surfaceTypes =
            {
                typeof(BmsGameplaySkinLaneTopologyPublication),
                typeof(BmsGameplaySkinLaneTopologyRevisionOwner),
            };

            Assert.Multiple(() =>
            {
                Assert.That(surfaceTypes.All(type => !type.IsPublic), Is.True);
                Assert.That(typeof(BmsGameplaySkinLaneTopologyPublication).IsSealed, Is.True);
                Assert.That(typeof(BmsGameplaySkinLaneTopologyRevisionOwner).IsSealed, Is.True);
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).All(property => property.SetMethod == null || !property.SetMethod.IsPublic), Is.True);
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("Drawable"));
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("ISkin"));
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("Texture"));
                Assert.That(publication.ToString(), Is.EqualTo("Bms:Key7K:Center:Revision0"));
                Assert.That(publication.ToString(), Does.Not.Contain("\\"));
                Assert.That(publication.ToString(), Does.Not.Contain("/"));
            });
        }

        [Test]
        public void TestInvalidConstructionInputFailsClosed()
        {
            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K));
            var owner = new GameplaySkinLaneTopologyRevisionOwner<BmsKeymode>((previous, current) => previous == current);
            GameplaySkinLaneTopologyPublication publication = owner.Publish(BmsKeymode.Key7K, projection.Topology);
            GameplaySkinLaneTopologyPublication otherPublication = owner.Publish(
                BmsKeymode.Key7K,
                BmsGameplaySkinLaneTopologyFactory.Create(BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K, style: BmsPlayfieldStyle.P2)).Topology);

            Assert.Multiple(() =>
            {
                Assert.That(() => new BmsGameplaySkinLaneTopologyRevisionOwner().Publish(null!), Throws.ArgumentNullException);
                Assert.That(() => new BmsGameplaySkinLaneTopologyPublication(null!, publication), Throws.ArgumentNullException);
                Assert.That(() => new BmsGameplaySkinLaneTopologyPublication(projection, null!), Throws.ArgumentNullException);
                Assert.That(() => new BmsGameplaySkinLaneTopologyPublication(projection, otherPublication), Throws.ArgumentException);
            });
        }
    }
}
