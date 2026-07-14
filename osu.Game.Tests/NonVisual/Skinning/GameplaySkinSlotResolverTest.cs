// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinSlotResolverTest
    {
        [Test]
        public void TestThreeStateResultContract()
        {
            var component = new TestComponent("provided");

            SkinSlotResult<TestComponent> defaultResult = default;
            SkinSlotResult<TestComponent> provided = SkinSlotResult<TestComponent>.Provide(component);

            Assert.Multiple(() =>
            {
                Assert.That(defaultResult.Kind, Is.EqualTo(SkinSlotResultKind.Inherit));
                Assert.That(SkinSlotResult<TestComponent>.Inherit.Kind, Is.EqualTo(SkinSlotResultKind.Inherit));
                Assert.That(provided.Kind, Is.EqualTo(SkinSlotResultKind.Provide));
                Assert.That(provided.Value, Is.SameAs(component));
                Assert.That(SkinSlotResult<TestComponent>.Suppress.Kind, Is.EqualTo(SkinSlotResultKind.Suppress));
                Assert.That(() => { _ = defaultResult.Value; }, Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => { _ = SkinSlotResult<TestComponent>.Suppress.Value; }, Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => defaultResult.ToString(), Throws.Nothing);
                Assert.That(() => SkinSlotResult<TestComponent>.Suppress.ToString(), Throws.Nothing);
                Assert.That(() => SkinSlotResult<TestComponent>.Provide(null!), Throws.ArgumentNullException);
            });
        }

        [Test]
        public void TestProvideStopsFallback()
        {
            var expected = new TestComponent("selected");
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Provide(expected));
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("fallback")));

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Note, SkinSlotRequirement.Critical, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Provide));
                Assert.That(resolution.Result.Value, Is.SameAs(expected));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected"));
                Assert.That(selected.QueryCount, Is.EqualTo(1));
                Assert.That(fallback.QueryCount, Is.Zero);
                Assert.That(resolution.Diagnostics, Is.Empty);
            });
        }

        [Test]
        public void TestInheritContinuesFallback()
        {
            var expected = new TestComponent("oms-simple");
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Inherit);
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(expected));

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Note, SkinSlotRequirement.Critical, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value, Is.SameAs(expected));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(selected.QueryCount, Is.EqualTo(1));
                Assert.That(fallback.QueryCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestAllProvidersInheritReturnsInherit()
        {
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Inherit);
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Inherit);

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Note, SkinSlotRequirement.Critical, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Inherit));
                Assert.That(resolution.ProviderName, Is.Null);
                Assert.That(selected.QueryCount, Is.EqualTo(1));
                Assert.That(fallback.QueryCount, Is.EqualTo(1));
                Assert.That(resolution.Diagnostics, Is.Empty);
            });
        }

        [Test]
        public void TestOptionalSuppressStopsFallback()
        {
            var beatmap = new TestProvider<TestComponent>("beatmap-local", _ => SkinSlotResult<TestComponent>.Inherit);
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Suppress);
            var rulesetResources = new TestProvider<TestComponent>("ruleset-resources", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("ruleset")));
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("fallback")));

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Combo, SkinSlotRequirement.Optional, new[] { beatmap, selected, rulesetResources, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Suppress));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected"));
                Assert.That(beatmap.QueryCount, Is.EqualTo(1));
                Assert.That(selected.QueryCount, Is.EqualTo(1));
                Assert.That(rulesetResources.QueryCount, Is.Zero);
                Assert.That(fallback.QueryCount, Is.Zero);
                Assert.That(resolution.Diagnostics, Is.Empty);
            });
        }

        [Test]
        public void TestCriticalSuppressFallsBackToOmsSimple()
        {
            var expected = new TestComponent("oms-simple");
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Suppress);
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(expected));

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Note, SkinSlotRequirement.Critical, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value, Is.SameAs(expected));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics[0].Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected));
                Assert.That(resolution.Diagnostics[0].ProviderName, Is.EqualTo("selected"));
                Assert.That(resolution.Diagnostics[0].Slot, Is.EqualTo(TestSlot.Note));
                Assert.That(resolution.Diagnostics[0].SlotId, Is.Null);
            });
        }

        [Test]
        public void TestProviderConstructionFailureFallsBackWithDiagnostic()
        {
            var failure = new InvalidDataException("broken resource");
            var expected = new TestComponent("oms-simple");
            var broken = new TestProvider<TestComponent>("broken", _ => throw failure);
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(expected));

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Note, SkinSlotRequirement.Critical, new[] { broken, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value, Is.SameAs(expected));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics[0].Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
                Assert.That(resolution.Diagnostics[0].Exception, Is.SameAs(failure));
            });
        }

        [Test]
        public void TestCancellationIsNotConvertedToFallback()
        {
            var cancellation = new OperationCanceledException();
            var selected = new TestProvider<TestComponent>("selected", _ => throw cancellation);
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("fallback")));

            var thrown = Assert.Throws<OperationCanceledException>(() =>
                GameplaySkinSlotResolver.Resolve(TestSlot.Note, SkinSlotRequirement.Critical, new[] { selected, fallback }));

            Assert.Multiple(() =>
            {
                Assert.That(thrown, Is.SameAs(cancellation));
                Assert.That(fallback.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestDamagedProvideFallsBackWithDiagnostic()
        {
            var expected = new TestComponent("oms-simple");
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("damaged")));
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(expected));

            var resolution = GameplaySkinSlotResolver.Resolve(
                TestSlot.Note,
                SkinSlotRequirement.Critical,
                new[] { selected, fallback },
                component => component.Name != "damaged");

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value, Is.SameAs(expected));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics[0].Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProvidedValueRejected));
            });
        }

        [Test]
        public void TestValidatorFailureFallsBackWithDiagnostic()
        {
            var failure = new InvalidDataException("validation failed");
            var expected = new TestComponent("oms-simple");
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("selected")));
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(expected));

            var resolution = GameplaySkinSlotResolver.Resolve(
                TestSlot.Note,
                SkinSlotRequirement.Critical,
                new[] { selected, fallback },
                component => component == expected ? true : throw failure);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value, Is.SameAs(expected));
                Assert.That(resolution.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics[0].Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed));
                Assert.That(resolution.Diagnostics[0].Exception, Is.SameAs(failure));
            });
        }

        [Test]
        public void TestFallbackIsPerComponent()
        {
            var selectedNote = new TestComponent("selected note");
            var fallbackNote = new TestComponent("fallback note");
            var fallbackLongNote = new TestComponent("fallback long note");
            var selected = new TestProvider<TestComponent>("selected", slot => slot == TestSlot.Note
                ? SkinSlotResult<TestComponent>.Provide(selectedNote)
                : SkinSlotResult<TestComponent>.Inherit);
            var fallback = new TestProvider<TestComponent>("oms-simple", slot => SkinSlotResult<TestComponent>.Provide(slot == TestSlot.Note ? fallbackNote : fallbackLongNote));

            var noteResolution = GameplaySkinSlotResolver.Resolve(TestSlot.Note, SkinSlotRequirement.Critical, new[] { selected, fallback });
            var longNoteResolution = GameplaySkinSlotResolver.Resolve(TestSlot.LongNote, SkinSlotRequirement.Critical, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(noteResolution.Result.Value, Is.SameAs(selectedNote));
                Assert.That(noteResolution.ProviderName, Is.EqualTo("selected"));
                Assert.That(longNoteResolution.Result.Value, Is.SameAs(fallbackLongNote));
                Assert.That(longNoteResolution.ProviderName, Is.EqualTo("oms-simple"));
            });
        }

        [Test]
        public void TestBeatmapProviderCannotBePiercedByLowerSuppress()
        {
            var beatmapValue = new TestComponent("beatmap-local");
            var beatmap = new TestProvider<TestComponent>("beatmap-local", _ => SkinSlotResult<TestComponent>.Provide(beatmapValue));
            var selected = new TestProvider<TestComponent>("selected", _ => SkinSlotResult<TestComponent>.Suppress);
            var rulesetResources = new TestProvider<TestComponent>("ruleset-resources", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("ruleset")));
            var fallback = new TestProvider<TestComponent>("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("fallback")));

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Combo, SkinSlotRequirement.Optional, new[] { beatmap, selected, rulesetResources, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value, Is.SameAs(beatmapValue));
                Assert.That(resolution.ProviderName, Is.EqualTo("beatmap-local"));
                Assert.That(selected.QueryCount, Is.Zero);
                Assert.That(rulesetResources.QueryCount, Is.Zero);
                Assert.That(fallback.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestDrawableEmptyHasNoSuppressMeaning()
        {
            using Drawable empty = Drawable.Empty();
            var selected = new TestProvider<Drawable>("selected", _ => SkinSlotResult<Drawable>.Provide(empty));
            var fallback = new TestProvider<Drawable>("oms-simple", _ => SkinSlotResult<Drawable>.Provide(new Container()));

            var resolution = GameplaySkinSlotResolver.Resolve(TestSlot.Combo, SkinSlotRequirement.Optional, new[] { selected, fallback });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Provide));
                Assert.That(resolution.Result.Value, Is.SameAs(empty));
                Assert.That(fallback.QueryCount, Is.Zero);
            });
        }

        private enum TestSlot
        {
            Note,
            LongNote,
            Combo,
        }

        private sealed record TestComponent(string Name);

        private sealed class TestProvider<T> : IGameplaySkinSlotProvider<TestSlot, T>
            where T : notnull
        {
            private readonly Func<TestSlot, SkinSlotResult<T>> getSlot;

            public string Name { get; }

            public int QueryCount { get; private set; }

            public TestProvider(string name, Func<TestSlot, SkinSlotResult<T>> getSlot)
            {
                Name = name;
                this.getSlot = getSlot;
            }

            public SkinSlotResult<T> GetSlot(TestSlot slot)
            {
                QueryCount++;
                return getSlot(slot);
            }
        }
    }
}
