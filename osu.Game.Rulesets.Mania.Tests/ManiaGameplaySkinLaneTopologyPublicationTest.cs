// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public sealed class ManiaGameplaySkinLaneTopologyPublicationTest
    {
        [Test]
        public void TestIndependentFourKeyRebuildPublishesNextRevision()
        {
            var owner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication previous = owner.Publish(createBeatmap(4));
            ManiaGameplaySkinLaneTopologyPublication current = owner.Publish(createBeatmap(4));

            Assert.Multiple(() =>
            {
                Assert.That(previous.Publication.Revision, Is.Zero);
                Assert.That(current.Publication.Revision, Is.EqualTo(1));
                Assert.That(current.StageColumnCounts, Is.EqualTo(new[] { 4 }));
                Assert.That(owner.Current, Is.SameAs(current));
                Assert.That(previous.Publication.Topology, Is.Not.SameAs(current.Publication.Topology));
                Assert.That(
                    previous.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id),
                    Is.EqualTo(current.Publication.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id)));
            });
        }

        [Test]
        public void TestFourToFiveKeyChangeIsRejectedAtomically()
        {
            var owner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication accepted = owner.Publish(createBeatmap(4));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => owner.Publish(createBeatmap(5)))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.ParamName, Is.EqualTo("nativeContext"));
                Assert.That(owner.Current, Is.SameAs(accepted));
                Assert.That(owner.Current!.StageColumnCounts, Is.EqualTo(new[] { 4 }));
                Assert.That(owner.Current.Publication.Revision, Is.Zero);
                Assert.That(owner.Current.Publication.Topology, Is.SameAs(accepted.Publication.Topology));
            });

            ManiaGameplaySkinLaneTopologyPublication current = owner.Publish(createBeatmap(4));

            Assert.That(current.Publication.Revision, Is.EqualTo(1));
        }

        [Test]
        public void TestDualStageReorderWithSameTotalColumnsIsRejectedAtomically()
        {
            var owner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication accepted = owner.Publish(createBeatmap(4, 5));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => owner.Publish(createBeatmap(5, 4)))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.ParamName, Is.EqualTo("nativeContext"));
                Assert.That(owner.Current, Is.SameAs(accepted));
                Assert.That(owner.Current!.StageColumnCounts, Is.EqualTo(new[] { 4, 5 }));
                Assert.That(owner.Current.Publication.Revision, Is.Zero);
                Assert.That(owner.Current.Publication.Topology, Is.SameAs(accepted.Publication.Topology));
            });

            ManiaGameplaySkinLaneTopologyPublication current = owner.Publish(createBeatmap(4, 5));

            Assert.That(current.Publication.Revision, Is.EqualTo(1));
        }

        [Test]
        public void TestPublishedProjectionDoesNotDriftAfterBeatmapMutation()
        {
            ManiaBeatmap beatmap = createBeatmap(4, 5);
            var owner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication publication = owner.Publish(beatmap);

            beatmap.Stages.Clear();
            beatmap.Stages.Add(new StageDefinition(7));

            Assert.Multiple(() =>
            {
                Assert.That(publication.StageColumnCounts, Is.EqualTo(new[] { 4, 5 }));
                Assert.That(publication.Publication.Topology.GroupsInLogicalOrder.Select(group => group.LanesInLogicalOrder.Count),
                    Is.EqualTo(new[] { 4, 5 }));
                Assert.That(owner.Current, Is.SameAs(publication));
                Assert.That(owner.Current!.Publication.Revision, Is.Zero);
            });
        }

        [Test]
        public void TestInvalidProvisionalBeatmapDoesNotAdvanceRevision()
        {
            var owner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication accepted = owner.Publish(createBeatmap(4));
            ManiaBeatmap invalid = createBeatmap(4);
            invalid.Stages.Clear();

            Assert.That(() => owner.Publish(invalid), Throws.ArgumentException);

            Assert.Multiple(() =>
            {
                Assert.That(owner.Current, Is.SameAs(accepted));
                Assert.That(owner.Current!.Publication.Revision, Is.Zero);
            });

            ManiaGameplaySkinLaneTopologyPublication current = owner.Publish(createBeatmap(4));

            Assert.That(current.Publication.Revision, Is.EqualTo(1));
        }

        [Test]
        public void TestProjectionPublicationAndOwnerRemainInternalAndImmutable()
        {
            ManiaBeatmap beatmap = createBeatmap(4, 5);
            ManiaGameplaySkinLaneTopologyProjection projection = ManiaGameplaySkinLaneTopologyFactory.CreateProjection(beatmap);
            var owner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication publication = owner.Publish(beatmap);
            var projectionStageCounts = (IList<int>)projection.StageColumnCounts;
            var publicationStageCounts = (IList<int>)publication.StageColumnCounts;
            Type[] surfaceTypes =
            {
                typeof(ManiaGameplaySkinLaneTopologyProjection),
                typeof(ManiaGameplaySkinLaneTopologyPublication),
                typeof(ManiaGameplaySkinLaneTopologyRevisionOwner),
            };

            Assert.Multiple(() =>
            {
                Assert.That(surfaceTypes.All(type => !type.IsPublic && type.IsSealed), Is.True);
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).All(property => property.SetMethod == null || !property.SetMethod.IsPublic), Is.True);
                Assert.That(() => projectionStageCounts[0] = 7, Throws.TypeOf<NotSupportedException>());
                Assert.That(() => publicationStageCounts[0] = 7, Throws.TypeOf<NotSupportedException>());
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("Drawable"));
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("ISkin"));
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("Texture"));
                Assert.That(projection.ToString(), Is.EqualTo(nameof(ManiaGameplaySkinLaneTopologyProjection)));
                Assert.That(publication.ToString(), Is.EqualTo("Mania:Revision0"));
                Assert.That(owner.ToString(), Is.EqualTo(nameof(ManiaGameplaySkinLaneTopologyRevisionOwner)));
            });
        }

        [Test]
        public void TestInvalidProjectionOrWrapperConstructionFailsClosed()
        {
            ManiaGameplaySkinLaneTopologyProjection projection = ManiaGameplaySkinLaneTopologyFactory.CreateProjection(createBeatmap(4));
            GameplaySkinLaneTopologySnapshot fiveKeyTopology = ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(5));
            var sharedOwner = new GameplaySkinLaneTopologyRevisionOwner<IReadOnlyList<int>>((previous, current) => previous.SequenceEqual(current));
            GameplaySkinLaneTopologyPublication publication = sharedOwner.Publish(projection.StageColumnCounts, projection.Topology);
            var otherSharedOwner = new GameplaySkinLaneTopologyRevisionOwner<IReadOnlyList<int>>((previous, current) => previous.SequenceEqual(current));
            GameplaySkinLaneTopologyPublication otherPublication = otherSharedOwner.Publish(new[] { 5 }, fiveKeyTopology);

            Assert.Multiple(() =>
            {
                Assert.That(() => new ManiaGameplaySkinLaneTopologyRevisionOwner().Publish(null!), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyProjection.Create(null!), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyProjection.Create(Array.Empty<int>()), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyProjection.Create(new[] { 4, 5, 6 }), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyProjection.Create(new[] { 0 }), Throws.ArgumentException);
                Assert.That(() => new ManiaGameplaySkinLaneTopologyPublication(null!, publication), Throws.ArgumentNullException);
                Assert.That(() => new ManiaGameplaySkinLaneTopologyPublication(projection, null!), Throws.ArgumentNullException);
                Assert.That(() => new ManiaGameplaySkinLaneTopologyPublication(projection, otherPublication), Throws.ArgumentException);
            });
        }

        private static ManiaBeatmap createBeatmap(params int[] stageColumns)
        {
            if (stageColumns.Length == 0)
                throw new ArgumentException("At least one stage is required.", nameof(stageColumns));

            var beatmap = new ManiaBeatmap(new StageDefinition(stageColumns[0]));

            foreach (int columns in stageColumns.Skip(1))
                beatmap.Stages.Add(new StageDefinition(columns));

            return beatmap;
        }
    }
}
