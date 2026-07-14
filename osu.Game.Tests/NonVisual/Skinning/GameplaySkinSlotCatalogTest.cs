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
    public sealed class GameplaySkinSlotCatalogTest
    {
        private static readonly string[] expected_critical_ids =
        {
            "playfield.lane-surface",
            "playfield.judgement-line",
            "object.note",
            "object.long-note.head",
            "object.long-note.body",
            "object.mine",
            "playfield.lane-cover.fill",
        };

        private static readonly string[] expected_optional_ids =
        {
            "object.long-note.tail",
            "playfield.key",
            "effect.key-flash",
            "effect.hit-explosion",
            "hud.judgement",
            "hud.combo",
            "hud.gauge",
            "hud.text",
            "playfield.bar-line",
            "stage.background",
            "stage.foreground",
            "playfield.backdrop",
            "playfield.baseplate",
            "playfield.lane-cover.decoration",
            "playfield.turntable",
            "playfield.laser",
            "bga.viewport",
            "bga.frame",
            "decoration",
        };

        private static IEnumerable<GameplaySkinSlotDescriptor> criticalSlots =>
            GameplaySkinSlotCatalog.All.Where(slot => slot.Requirement == SkinSlotRequirement.Critical);

        private static IEnumerable<GameplaySkinSlotDescriptor> optionalSlots =>
            GameplaySkinSlotCatalog.All.Where(slot => slot.Requirement == SkinSlotRequirement.Optional);

        [Test]
        public void TestCatalogSnapshotAndStableIds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinSlotCatalog.All, Has.Count.EqualTo(expected_critical_ids.Length + expected_optional_ids.Length));
                Assert.That(criticalSlots.Select(slot => slot.Id), Is.EquivalentTo(expected_critical_ids));
                Assert.That(optionalSlots.Select(slot => slot.Id), Is.EquivalentTo(expected_optional_ids));
                Assert.That(GameplaySkinSlotCatalog.All.Select(slot => slot.Id).Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(GameplaySkinSlotCatalog.All.Count));

                foreach (GameplaySkinSlotDescriptor slot in GameplaySkinSlotCatalog.All)
                {
                    Assert.That(slot.Id, Does.Match("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$"));
                    Assert.That(slot.ToString(), Is.EqualTo(slot.Id));
                }
            });
        }

        [Test]
        public void TestCatalogIsReadOnlyAndOrdinal()
        {
            foreach (GameplaySkinSlotDescriptor slot in GameplaySkinSlotCatalog.All)
            {
                Assert.That(GameplaySkinSlotCatalog.TryGet(slot.Id, out GameplaySkinSlotDescriptor? resolved), Is.True);
                Assert.That(resolved, Is.SameAs(slot));
            }

            bool foundUnknown = GameplaySkinSlotCatalog.TryGet("object.future", out GameplaySkinSlotDescriptor? unknownDescriptor);

            Assert.Multiple(() =>
            {
                Assert.That(foundUnknown, Is.False);
                Assert.That(unknownDescriptor, Is.Null);
                Assert.That(GameplaySkinSlotCatalog.TryGet("Object.Note", out _), Is.False);
                Assert.That(GameplaySkinSlotCatalog.TryGet("object.note ", out _), Is.False);
                Assert.That(GameplaySkinSlotCatalog.TryGet(string.Empty, out _), Is.False);
                Assert.That(GameplaySkinSlotCatalog.TryGet(null, out _), Is.False);
                Assert.That(GameplaySkinSlotCatalog.All, Is.Not.InstanceOf<GameplaySkinSlotDescriptor[]>());
                Assert.That(() => ((IList<GameplaySkinSlotDescriptor>)GameplaySkinSlotCatalog.All).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestDescriptorRejectsInvalidIdOrRequirement()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => new GameplaySkinSlotDescriptor(null!, SkinSlotRequirement.Critical), Throws.ArgumentNullException);
                Assert.That(() => new GameplaySkinSlotDescriptor(string.Empty, SkinSlotRequirement.Critical), Throws.ArgumentException);
                Assert.That(() => new GameplaySkinSlotDescriptor("Object.note", SkinSlotRequirement.Critical), Throws.ArgumentException);
                Assert.That(() => new GameplaySkinSlotDescriptor("object..note", SkinSlotRequirement.Critical), Throws.ArgumentException);
                Assert.That(() => new GameplaySkinSlotDescriptor("object.note-", SkinSlotRequirement.Critical), Throws.ArgumentException);
                Assert.That(() => new GameplaySkinSlotDescriptor("1object.note", SkinSlotRequirement.Critical), Throws.ArgumentException);
                Assert.That(() => new GameplaySkinSlotDescriptor("object.note", (SkinSlotRequirement)99), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestDefaultRequirementIsCritical()
        {
            Assert.That(default(SkinSlotRequirement), Is.EqualTo(SkinSlotRequirement.Critical));
        }

        [TestCaseSource(nameof(criticalSlots))]
        public void TestCriticalSuppressFallsBackToOmsSimple(GameplaySkinSlotDescriptor descriptor)
        {
            var lookup = new TestLookup("private context must not become the stable ID", SkinSlotRequirement.Optional);
            var expected = new TestComponent("oms-simple");
            var selected = new TestProvider("selected", _ => SkinSlotResult<TestComponent>.Suppress);
            var fallback = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(expected));

            GameplaySkinSlotResolution<TestComponent> resolution = GameplaySkinSlotResolver.Resolve(descriptor, lookup, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Provide));
                Assert.That(resolution.Result.Value, Is.SameAs(expected));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics[0].Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected));
                Assert.That(resolution.Diagnostics[0].Slot, Is.TypeOf<GameplaySkinSlotLookup<TestLookup>>());
                Assert.That(((GameplaySkinSlotLookup<TestLookup>)resolution.Diagnostics[0].Slot).Descriptor, Is.SameAs(descriptor));
                Assert.That(((GameplaySkinSlotLookup<TestLookup>)resolution.Diagnostics[0].Slot).Context, Is.SameAs(lookup));
                Assert.That(resolution.Diagnostics[0].SlotId, Is.EqualTo(descriptor.Id));
                Assert.That(resolution.Diagnostics[0].SlotId, Does.Not.Contain(lookup.Context));
                Assert.That(resolution.Diagnostics[0].ToString(), Does.Not.Contain(lookup.Context));
                Assert.That(selected.QueryCount, Is.EqualTo(1));
                Assert.That(fallback.QueryCount, Is.EqualTo(1));
            });
        }

        [TestCaseSource(nameof(optionalSlots))]
        public void TestOptionalSuppressStopsFallback(GameplaySkinSlotDescriptor descriptor)
        {
            var lookup = new TestLookup("optional context", SkinSlotRequirement.Critical);
            var selected = new TestProvider("selected", _ => SkinSlotResult<TestComponent>.Suppress);
            var fallback = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("fallback")));

            GameplaySkinSlotResolution<TestComponent> resolution = GameplaySkinSlotResolver.Resolve(descriptor, lookup, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Suppress));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected"));
                Assert.That(resolution.Diagnostics, Is.Empty);
                Assert.That(selected.QueryCount, Is.EqualTo(1));
                Assert.That(fallback.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestConditionalCoreAndLongNoteTailClassification()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinSlotCatalog.LaneCoverFill.Requirement, Is.EqualTo(SkinSlotRequirement.Critical));
                Assert.That(GameplaySkinSlotCatalog.LongNoteHead.Requirement, Is.EqualTo(SkinSlotRequirement.Critical));
                Assert.That(GameplaySkinSlotCatalog.LongNoteBody.Requirement, Is.EqualTo(SkinSlotRequirement.Critical));
                Assert.That(GameplaySkinSlotCatalog.LongNoteTail.Requirement, Is.EqualTo(SkinSlotRequirement.Optional));
            });
        }

        [Test]
        public void TestProviderReceivesDescriptorSeparatelyFromSharedContext()
        {
            var context = new TestLookup("same context", SkinSlotRequirement.Optional);
            var provider = new TestProvider("selected", lookup => SkinSlotResult<TestComponent>.Provide(new TestComponent(lookup.Descriptor.Id)));

            GameplaySkinSlotResolution<TestComponent> note = GameplaySkinSlotResolver.Resolve(GameplaySkinSlotCatalog.Note, context, new[] { provider });
            GameplaySkinSlotResolution<TestComponent> mine = GameplaySkinSlotResolver.Resolve(GameplaySkinSlotCatalog.Mine, context, new[] { provider });

            Assert.Multiple(() =>
            {
                Assert.That(note.Result.Value.Name, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(mine.Result.Value.Name, Is.EqualTo(GameplaySkinSlotCatalog.Mine.Id));
                Assert.That(provider.SeenDescriptors, Is.EqualTo(new[] { GameplaySkinSlotCatalog.Note, GameplaySkinSlotCatalog.Mine }));
                Assert.That(provider.SeenContexts, Is.All.SameAs(context));
            });
        }

        [Test]
        public void TestRawResolverCannotOverrideCatalogRequirement()
        {
            var selected = new RawDescriptorProvider("selected", _ => SkinSlotResult<TestComponent>.Suppress);
            var context = new TestLookup("catalogued raw context", SkinSlotRequirement.Optional);
            var cataloguedLookup = new GameplaySkinSlotLookup<TestLookup>(GameplaySkinSlotCatalog.Note, context);
            var cataloguedProvider = new TestProvider("selected", _ => SkinSlotResult<TestComponent>.Suppress);

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => GameplaySkinSlotResolver.Resolve(GameplaySkinSlotCatalog.Note, SkinSlotRequirement.Optional, new[] { selected }),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinSlotResolver.Resolve(cataloguedLookup, SkinSlotRequirement.Optional, new[] { cataloguedProvider }),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinSlotResolver.Resolve(GameplaySkinSlotCatalog.Note, (SkinSlotRequirement)99, new[] { selected }),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestStableSlotIdFlowsThroughFailureDiagnostics()
        {
            var context = new TestLookup("private diagnostic context", SkinSlotRequirement.Optional);
            var expected = new TestComponent("oms-simple");
            var providerFailure = new InvalidOperationException("provider failed");
            var broken = new TestProvider("broken", _ => throw providerFailure);
            var damaged = new TestProvider("damaged", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("damaged")));
            var fallback = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(expected));

            GameplaySkinSlotResolution<TestComponent> failed = GameplaySkinSlotResolver.Resolve(
                GameplaySkinSlotCatalog.Note, context, new[] { broken, fallback });
            GameplaySkinSlotResolution<TestComponent> rejected = GameplaySkinSlotResolver.Resolve(
                GameplaySkinSlotCatalog.Note, context, new[] { damaged, fallback }, component => component.Name != "damaged");
            string serialisedFailure = JsonConvert.SerializeObject(failed.Diagnostics[0]);

            Assert.Multiple(() =>
            {
                Assert.That(failed.Result.Value, Is.SameAs(expected));
                Assert.That(failed.Diagnostics[0].Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
                Assert.That(failed.Diagnostics[0].SlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(failed.Diagnostics[0].Exception, Is.SameAs(providerFailure));
                Assert.That(failed.Diagnostics[0].ToString(), Does.Not.Contain(context.Context));
                Assert.That(serialisedFailure, Does.Contain(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(serialisedFailure, Does.Not.Contain(context.Context));
                Assert.That(serialisedFailure, Does.Not.Contain(providerFailure.Message));

                Assert.That(rejected.Result.Value, Is.SameAs(expected));
                Assert.That(rejected.Diagnostics[0].Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProvidedValueRejected));
                Assert.That(rejected.Diagnostics[0].SlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(rejected.Diagnostics[0].ToString(), Does.Not.Contain(context.Context));
            });
        }

        private sealed record TestLookup(string Context, SkinSlotRequirement SuggestedRequirement);

        private sealed record TestComponent(string Name);

        private sealed class TestProvider : IGameplaySkinSlotProvider<GameplaySkinSlotLookup<TestLookup>, TestComponent>
        {
            private readonly Func<GameplaySkinSlotLookup<TestLookup>, SkinSlotResult<TestComponent>> getSlot;

            public string Name { get; }

            public int QueryCount { get; private set; }

            public List<GameplaySkinSlotDescriptor> SeenDescriptors { get; } = new List<GameplaySkinSlotDescriptor>();

            public List<TestLookup> SeenContexts { get; } = new List<TestLookup>();

            public TestProvider(string name, Func<GameplaySkinSlotLookup<TestLookup>, SkinSlotResult<TestComponent>> getSlot)
            {
                Name = name;
                this.getSlot = getSlot;
            }

            public SkinSlotResult<TestComponent> GetSlot(GameplaySkinSlotLookup<TestLookup> slot)
            {
                QueryCount++;
                SeenDescriptors.Add(slot.Descriptor);
                SeenContexts.Add(slot.Context);
                return getSlot(slot);
            }
        }

        private sealed class RawDescriptorProvider : IGameplaySkinSlotProvider<GameplaySkinSlotDescriptor, TestComponent>
        {
            private readonly Func<GameplaySkinSlotDescriptor, SkinSlotResult<TestComponent>> getSlot;

            public string Name { get; }

            public RawDescriptorProvider(string name, Func<GameplaySkinSlotDescriptor, SkinSlotResult<TestComponent>> getSlot)
            {
                Name = name;
                this.getSlot = getSlot;
            }

            public SkinSlotResult<TestComponent> GetSlot(GameplaySkinSlotDescriptor slot) => getSlot(slot);
        }
    }
}
