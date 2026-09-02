// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public sealed class BmsGameplaySkinLaneResourceResolutionTest
    {
        private readonly List<TestComponentOwner> componentOwners = new List<TestComponentOwner>();

        [TearDown]
        public void TearDown()
        {
            foreach (TestComponentOwner owner in componentOwners)
                owner.Dispose();

            componentOwners.Clear();
        }

        [TestCase(BmsKeymode.Key5K, "selected.bms-role-override,selected.mania-full-keys-6,selected.mania-key-only-keys-5")]
        [TestCase(BmsKeymode.Key7K, "selected.bms-role-override,selected.mania-full-keys-8,selected.mania-key-only-keys-7")]
        [TestCase(BmsKeymode.Key9K_Bms, "selected.bms-role-override,selected.mania-full-keys-9")]
        [TestCase(BmsKeymode.Key9K_Pms, "selected.bms-role-override,selected.mania-full-keys-9")]
        [TestCase(BmsKeymode.Key14K, "selected.bms-role-override,selected.mania-full-keys-16,selected.mania-deck-keys-8,selected.mania-key-only-keys-14")]
        public void TestFactoryPreservesSelectedCandidateOrderAndExcludesCanonicalMarker(BmsKeymode keymode, string expectedNames)
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(keymode);
            using var owner = new TestComponentOwner(materialize);
            IReadOnlyList<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TestComponent>> providers =
                BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(plan, owner);

            Assert.Multiple(() =>
            {
                Assert.That(providers.Select(provider => provider.Name), Is.EqualTo(expectedNames.Split(',')));
                Assert.That(providers.Select(provider => provider.Name), Has.None.EqualTo("oms-simple"));
                Assert.That(providers, Is.InstanceOf<IReadOnlyList<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TestComponent>>>());
            });
        }

        [Test]
        public void TestAbsentFieldDoesNotMaterializeAndFallsThroughToCanonical()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nKeyImage1: declared-other-field\n");
            int materializeCount = 0;
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    materializeCount++;
                    return materialize(reference);
                },
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("canonical"));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics, Is.Empty);
                Assert.That(materializeCount, Is.Zero);
                Assert.That(canonical.QueryCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestExplicitEmptyAndMissingResourcesFallThroughPerField()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1:\n",
                maniaIni:
                    "[Mania]\nKeys: 8\nNoteImage1: missing\n" +
                    "[Mania]\nKeys: 7\nNoteImage0: valid\n");
            var references = new List<BmsGameplaySkinLaneResourceReference>();
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    references.Add(reference);

                    if (reference.ResourceName.Length == 0)
                        throw new InvalidDataException("empty declaration");

                    if (reference.ResourceName == "missing")
                        throw new FileNotFoundException("missing package resource");

                    return materialize(reference);
                },
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("valid"));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.mania-key-only-keys-7"));
                Assert.That(references.Select(reference => reference.ResourceName), Is.EqualTo(new[] { string.Empty, "missing", "valid" }));
                Assert.That(resolution.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[]
                {
                    GameplaySkinSlotDiagnosticCode.ProviderFailed,
                    GameplaySkinSlotDiagnosticCode.ProviderFailed,
                }));
                Assert.That(resolution.Diagnostics.Select(diagnostic => diagnostic.ProviderName), Is.EqualTo(new[]
                {
                    "selected.bms-role-override",
                    "selected.mania-full-keys-8",
                }));
                Assert.That(canonical.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestSameResourceNameCanResolveDifferentlyBySource()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: shared-name\n",
                maniaIni: "[Mania]\nKeys: 8\nNoteImage1: shared-name\n");
            var references = new List<BmsGameplaySkinLaneResourceReference>();

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    references.Add(reference);

                    if (reference.Source == BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride)
                        throw new FileNotFoundException("missing from this source layer");

                    return materialize(reference);
                });

            Assert.Multiple(() =>
            {
                Assert.That(references.Select(reference => reference.ResourceName), Is.EqualTo(new[] { "shared-name", "shared-name" }));
                Assert.That(references.Select(reference => reference.Source), Is.EqualTo(new[]
                {
                    BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                    BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                }));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.mania-full-keys-8"));
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
            });
        }

        [TestCase("object.note.resource", "NoteImage1")]
        [TestCase("object.long-note.head.resource", "NoteImage1H")]
        [TestCase("object.long-note.body.resource", "NoteImage1L")]
        [TestCase("object.long-note.tail.resource", "NoteImage1T")]
        public void TestEveryDeclaredFieldFallsThroughFromBrokenBmsToFullMania(string fieldId, string lookup)
        {
            Assert.That(GameplaySkinLaneResourceFieldCatalog.TryGet(fieldId, out GameplaySkinLaneResourceField? field), Is.True);

            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: $"[Bms]\nKeymode: 7K\n{lookup}: broken-bms\n",
                maniaIni: $"[Mania]\nKeys: 8\n{lookup}: valid-mania\n");

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                field!,
                reference => reference.ResourceName == "broken-bms"
                    ? throw new InvalidDataException(reference.ResourceName)
                    : materialize(reference));

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("valid-mania"));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.mania-full-keys-8"));
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
                Assert.That(resolution.Diagnostics.Single().ProviderName, Is.EqualTo("selected.bms-role-override"));
                Assert.That(resolution.Diagnostics.Single().SlotId, Is.EqualTo(field!.Slot.Id));
            });
        }

        [Test]
        public void TestAdditionalValidatorRejectionFallsThrough()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: rejected\n",
                maniaIni: "[Mania]\nKeys: 8\nNoteImage1: accepted\n");

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                materialize,
                validator: component => component.Name != "rejected");

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("accepted"));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.mania-full-keys-8"));
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProvidedValueRejected));
                Assert.That(resolution.Diagnostics.Single().ProviderName, Is.EqualTo("selected.bms-role-override"));
            });
        }

        [Test]
        public void TestAdditionalValidatorFailureFallsThrough()
        {
            var failure = new InvalidDataException("validation failed");
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: throws\n",
                maniaIni: "[Mania]\nKeys: 8\nNoteImage1: accepted\n");

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                materialize,
                validator: component => component.Name == "throws" ? throw failure : true);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("accepted"));
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed));
                Assert.That(resolution.Diagnostics.Single().Exception, Is.SameAs(failure));
            });
        }

        [TestCase(false, GameplaySkinSlotDiagnosticCode.ProvidedValueRejected)]
        [TestCase(true, GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed)]
        public void TestRevisionOwnerRetainsRejectedAndWinningComponentsUntilDisposed(
            bool validatorThrows,
            GameplaySkinSlotDiagnosticCode expectedDiagnostic)
        {
            var failure = new InvalidDataException("validation failed");
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: rejected\n",
                maniaIni: "[Mania]\nKeys: 8\nNoteImage1: accepted\n");
            var owner = new TestComponentOwner(materialize);
            var context = new BmsGameplaySkinLaneResourceContext(plan.Topology, lane("bms.lane.key-1"), GameplaySkinLaneResourceFieldCatalog.Note);
            var providers = BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(plan, owner).ToList();
            providers.Add(new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical"))));

            GameplaySkinSlotResolution<TestComponent> resolution = GameplaySkinSlotResolver.Resolve(
                GameplaySkinLaneResourceFieldCatalog.Note.Slot,
                context,
                providers,
                component => component.Name != "rejected"
                    ? true
                    : validatorThrows
                        ? throw failure
                        : false);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("accepted"));
                Assert.That(owner.Components.Select(component => component.Name), Is.EqualTo(new[] { "rejected", "accepted" }));
                Assert.That(owner.Components.Select(component => component.DisposeCount), Is.All.Zero);
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(expectedDiagnostic));
                Assert.That(resolution.Diagnostics.Single().Exception, validatorThrows ? Is.SameAs(failure) : Is.Null);
            });

            owner.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(owner.Components.Select(component => component.DisposeCount), Is.All.EqualTo(1));
                Assert.That(() => owner.Materialize(resolution.Result.Value.Reference!), Throws.InstanceOf<ObjectDisposedException>());
            });

            owner.Dispose();
            Assert.That(owner.Components.Select(component => component.DisposeCount), Is.All.EqualTo(1));
        }

        [Test]
        public void TestRevisionOwnerScopesDoNotCrossReloadBoundary()
        {
            BmsGameplaySkinConfigurationCandidatePlan oldPlan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: old-revision\n");
            BmsGameplaySkinConfigurationCandidatePlan newPlan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: new-revision\n");
            using var oldOwner = new TestComponentOwner(materialize);
            using var newOwner = new TestComponentOwner(materialize);

            var oldContext = new BmsGameplaySkinLaneResourceContext(oldPlan.Topology, lane("bms.lane.key-1"), GameplaySkinLaneResourceFieldCatalog.Note);
            var newContext = new BmsGameplaySkinLaneResourceContext(newPlan.Topology, lane("bms.lane.key-1"), GameplaySkinLaneResourceFieldCatalog.Note);
            GameplaySkinSlotResolution<TestComponent> oldResolution = GameplaySkinSlotResolver.Resolve(
                GameplaySkinLaneResourceFieldCatalog.Note.Slot,
                oldContext,
                BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(oldPlan, oldOwner));
            GameplaySkinSlotResolution<TestComponent> newResolution = GameplaySkinSlotResolver.Resolve(
                GameplaySkinLaneResourceFieldCatalog.Note.Slot,
                newContext,
                BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(newPlan, newOwner));

            Assert.Multiple(() =>
            {
                Assert.That(oldResolution.Result.Value.Name, Is.EqualTo("old-revision"));
                Assert.That(newResolution.Result.Value.Name, Is.EqualTo("new-revision"));
                Assert.That(oldOwner.Components.Single().DisposeCount, Is.Zero);
                Assert.That(newOwner.Components.Single().DisposeCount, Is.Zero);
            });

            oldOwner.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(oldOwner.Components.Single().DisposeCount, Is.EqualTo(1));
                Assert.That(newOwner.Components.Single().DisposeCount, Is.Zero);
            });

            newOwner.Dispose();
            Assert.That(newOwner.Components.Single().DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestFailedProvisionalRevisionDoesNotDisposeActiveOwner()
        {
            BmsGameplaySkinConfigurationCandidatePlan activePlan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: active\n");
            BmsGameplaySkinConfigurationCandidatePlan provisionalPlan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: invalid-provisional\n");
            using var activeOwner = new TestComponentOwner(materialize);
            using var provisionalOwner = new TestComponentOwner(materialize);
            var activeContext = new BmsGameplaySkinLaneResourceContext(activePlan.Topology, lane("bms.lane.key-1"), GameplaySkinLaneResourceFieldCatalog.Note);
            var provisionalContext = new BmsGameplaySkinLaneResourceContext(provisionalPlan.Topology, lane("bms.lane.key-1"), GameplaySkinLaneResourceFieldCatalog.Note);

            GameplaySkinSlotResolution<TestComponent> activeResolution = GameplaySkinSlotResolver.Resolve(
                GameplaySkinLaneResourceFieldCatalog.Note.Slot,
                activeContext,
                BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(activePlan, activeOwner));
            GameplaySkinSlotResolution<TestComponent> provisionalResolution = GameplaySkinSlotResolver.Resolve(
                GameplaySkinLaneResourceFieldCatalog.Note.Slot,
                provisionalContext,
                BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(provisionalPlan, provisionalOwner),
                _ => false);

            Assert.Multiple(() =>
            {
                Assert.That(activeResolution.Result.Value.Name, Is.EqualTo("active"));
                Assert.That(provisionalResolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Inherit));
                Assert.That(provisionalResolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProvidedValueRejected));
                Assert.That(activeOwner.Components.Single().DisposeCount, Is.Zero);
                Assert.That(provisionalOwner.Components.Single().DisposeCount, Is.Zero);
            });

            provisionalOwner.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(provisionalOwner.Components.Single().DisposeCount, Is.EqualTo(1));
                Assert.That(activeOwner.Components.Single().DisposeCount, Is.Zero);
                Assert.That(activeResolution.Result.Value.DisposeCount, Is.Zero);
            });
        }

        [Test]
        public void TestHighPrioritySelectedResourceStopsLowerLayers()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: selected\n",
                maniaIni: "[Mania]\nKeys: 8\nNoteImage1: lower\n");
            var references = new List<BmsGameplaySkinLaneResourceReference>();
            var ruleset = new TestProvider("ruleset-resources", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("ruleset")));
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    references.Add(reference);
                    return materialize(reference);
                },
                ruleset: new[] { ruleset },
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("selected"));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.bms-role-override"));
                Assert.That(references, Has.Count.EqualTo(1));
                Assert.That(ruleset.QueryCount, Is.Zero);
                Assert.That(canonical.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestFourteenKeyResolvesEachHostedNoteFieldIndependently()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key14K,
                maniaIni:
                    "[Mania]\nKeys: 8\nNoteImage0: deck-note\n" +
                    "[Mania]\nKeys: 14\nNoteImage7T: key-only-tail\n");
            GameplaySkinLaneId key8 = lane("bms.lane.key-8");

            GameplaySkinSlotResolution<TestComponent> note = resolve(
                plan, key8, GameplaySkinLaneResourceFieldCatalog.Note, materialize);
            GameplaySkinSlotResolution<TestComponent> tail = resolve(
                plan, key8, GameplaySkinLaneResourceFieldCatalog.LongNoteTail, materialize);

            Assert.Multiple(() =>
            {
                Assert.That(note.Result.Value.Name, Is.EqualTo("deck-note"));
                Assert.That(note.Result.Value.Reference!.Source, Is.EqualTo(BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck));
                Assert.That(note.Result.Value.Reference.ManiaKeys, Is.EqualTo(8));
                Assert.That(note.ProviderName, Is.EqualTo("selected.mania-deck-keys-8"));

                Assert.That(tail.Result.Value.Name, Is.EqualTo("key-only-tail"));
                Assert.That(tail.Result.Value.Reference!.Source, Is.EqualTo(BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly));
                Assert.That(tail.Result.Value.Reference.ManiaKeys, Is.EqualTo(14));
                Assert.That(tail.ProviderName, Is.EqualTo("selected.mania-key-only-keys-14"));
            });
        }

        [Test]
        public void TestFourteenKeyInvalidFullFallsThroughToDeck()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key14K,
                maniaIni:
                    "[Mania]\nKeys: 16\nNoteImage8: broken-full\n" +
                    "[Mania]\nKeys: 8\nNoteImage0: valid-deck\n");

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-8"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference => reference.ResourceName == "broken-full"
                    ? throw new InvalidDataException(reference.ResourceName)
                    : materialize(reference));

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("valid-deck"));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.mania-deck-keys-8"));
                Assert.That(resolution.Diagnostics.Single().ProviderName, Is.EqualTo("selected.mania-full-keys-16"));
            });
        }

        [TestCase("object.note.resource", "NoteImage0", "NoteImage7")]
        [TestCase("object.long-note.head.resource", "NoteImage0H", "NoteImage7H")]
        [TestCase("object.long-note.body.resource", "NoteImage0L", "NoteImage7L")]
        [TestCase("object.long-note.tail.resource", "NoteImage0T", "NoteImage7T")]
        public void TestFourteenKeyInvalidDeckFallsThroughToKeyOnlyPerField(
            string fieldId,
            string deckLookup,
            string keyOnlyLookup)
        {
            Assert.That(GameplaySkinLaneResourceFieldCatalog.TryGet(fieldId, out GameplaySkinLaneResourceField? field), Is.True);

            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key14K,
                maniaIni:
                    $"[Mania]\nKeys: 8\n{deckLookup}: broken-deck\n" +
                    $"[Mania]\nKeys: 14\n{keyOnlyLookup}: valid-key-only\n");

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-8"),
                field!,
                reference => reference.ResourceName == "broken-deck"
                    ? throw new InvalidDataException(reference.ResourceName)
                    : materialize(reference));

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("valid-key-only"));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.mania-key-only-keys-14"));
                Assert.That(resolution.Diagnostics.Single().ProviderName, Is.EqualTo("selected.mania-deck-keys-8"));
                Assert.That(resolution.Diagnostics.Single().SlotId, Is.EqualTo(field!.Slot.Id));
            });
        }

        [Test]
        public void TestFourteenKeyScratchSkipsKeyOnlyAndFallsToCanonical()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key14K,
                maniaIni:
                    "[Mania]\nKeys: 8\nNoteImage7: broken-deck-scratch\n" +
                    "[Mania]\nKeys: 14\nNoteImage13: ordinary-key-only\n");
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical-scratch")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.scratch-2"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference => throw new InvalidDataException(reference.ResourceName),
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("canonical-scratch"));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics[0].ProviderName, Is.EqualTo("selected.mania-deck-keys-8"));
                Assert.That(canonical.QueryCount, Is.EqualTo(1));
            });
        }

        [TestCase(BmsKeymode.Key5K, 5)]
        [TestCase(BmsKeymode.Key7K, 7)]
        public void TestSinglePlayScratchDoesNotUseKeyOnlyBucket(BmsKeymode keymode, int keyOnlyKeys)
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                keymode,
                maniaIni: $"[Mania]\nKeys: {keyOnlyKeys}\nNoteImage0: ordinary-key\n");
            int materializeCount = 0;

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.scratch-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    materializeCount++;
                    return materialize(reference);
                });

            Assert.Multiple(() =>
            {
                Assert.That(materializeCount, Is.Zero);
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics, Is.Empty);
            });
        }

        [TestCase(BmsKeymode.Key5K, 6, 5)]
        [TestCase(BmsKeymode.Key7K, 8, 7)]
        public void TestSinglePlayScratchFailureSkipsKeyOnlyBucketAndFallsToCanonical(
            BmsKeymode keymode,
            int fullVisualKeys,
            int keyOnlyKeys)
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                keymode,
                maniaIni:
                    $"[Mania]\nKeys: {fullVisualKeys}\nNoteImage0: broken-full-scratch\n" +
                    $"[Mania]\nKeys: {keyOnlyKeys}\nNoteImage0: ordinary-key-only\n");
            var references = new List<BmsGameplaySkinLaneResourceReference>();

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.scratch-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    references.Add(reference);
                    throw new InvalidDataException(reference.ResourceName);
                });

            Assert.Multiple(() =>
            {
                Assert.That(references.Select(reference => reference.Source), Is.EqualTo(new[]
                {
                    BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                }));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics.Select(diagnostic => diagnostic.ProviderName), Is.EqualTo(new[]
                {
                    $"selected.mania-full-keys-{fullVisualKeys}",
                }));
            });
        }

        [TestCase(BmsKeymode.Key9K_Bms)]
        [TestCase(BmsKeymode.Key9K_Pms)]
        public void TestNineKeyValidatesKeysNineOnlyOnce(BmsKeymode keymode)
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                keymode,
                maniaIni: "[Mania]\nKeys: 9\nNoteImage0: broken\n");
            int materializeCount = 0;

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    materializeCount++;
                    throw new InvalidDataException(reference.ResourceName);
                });

            Assert.Multiple(() =>
            {
                Assert.That(materializeCount, Is.EqualTo(1));
                Assert.That(resolution.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics[0].ProviderName, Is.EqualTo("selected.mania-full-keys-9"));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
            });
        }

        [Test]
        public void TestLegacyBeatmapCompatibilityProvideStopsSelectedRulesetAndCanonicalLayers()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: selected\n");
            int materializeCount = 0;
            var beatmap = new TestProvider("legacy-beatmap-compatibility", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("beatmap")));
            var ruleset = new TestProvider("ruleset-resources", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("ruleset")));
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    materializeCount++;
                    return materialize(reference);
                },
                beatmap: new[] { beatmap },
                ruleset: new[] { ruleset },
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("beatmap"));
                Assert.That(resolution.ProviderName, Is.EqualTo("legacy-beatmap-compatibility"));
                Assert.That(beatmap.QueryCount, Is.EqualTo(1));
                Assert.That(materializeCount, Is.Zero);
                Assert.That(ruleset.QueryCount, Is.Zero);
                Assert.That(canonical.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestLegacyBeatmapCompatibilityOptionalTailSuppressionStopsDeclaredSelectedResource()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1T: selected-tail\n");
            int materializeCount = 0;
            var beatmap = new TestProvider("legacy-beatmap-compatibility", _ => SkinSlotResult<TestComponent>.Suppress);

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
                reference =>
                {
                    materializeCount++;
                    return materialize(reference);
                },
                beatmap: new[] { beatmap });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Suppress));
                Assert.That(resolution.ProviderName, Is.EqualTo("legacy-beatmap-compatibility"));
                Assert.That(beatmap.QueryCount, Is.EqualTo(1));
                Assert.That(materializeCount, Is.Zero);
            });
        }

        [Test]
        public void TestRulesetResourcesRemainAboveCanonicalFallback()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: broken-selected\n");
            var ruleset = new TestProvider("ruleset-resources", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("ruleset")));
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference => throw new InvalidDataException(reference.ResourceName),
                ruleset: new[] { ruleset },
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("ruleset"));
                Assert.That(resolution.ProviderName, Is.EqualTo("ruleset-resources"));
                Assert.That(resolution.Diagnostics.Single().ProviderName, Is.EqualTo("selected.bms-role-override"));
                Assert.That(ruleset.QueryCount, Is.EqualTo(1));
                Assert.That(canonical.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestCriticalSuppressionIsRejectedBeforeSelectedFallback()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: selected-note\n");
            var beatmap = new TestProvider("legacy-beatmap-compatibility", _ => SkinSlotResult<TestComponent>.Suppress);

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                materialize,
                beatmap: new[] { beatmap });

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("selected-note"));
                Assert.That(resolution.ProviderName, Is.EqualTo("selected.bms-role-override"));
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected));
            });
        }

        [Test]
        public void TestOptionalTailSuppressionStopsBeforeCanonicalFallback()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(BmsKeymode.Key7K);
            var ruleset = new TestProvider("ruleset-resources", _ => SkinSlotResult<TestComponent>.Suppress);
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
                materialize,
                ruleset: new[] { ruleset },
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Suppress));
                Assert.That(resolution.ProviderName, Is.EqualTo("ruleset-resources"));
                Assert.That(canonical.QueryCount, Is.Zero);
            });
        }

        [TestCase("object.note.resource", SkinSlotRequirement.Critical)]
        [TestCase("object.long-note.head.resource", SkinSlotRequirement.Critical)]
        [TestCase("object.long-note.body.resource", SkinSlotRequirement.Critical)]
        [TestCase("object.long-note.tail.resource", SkinSlotRequirement.Optional)]
        public void TestEveryFieldUsesItsCataloguedRequirement(string fieldId, SkinSlotRequirement requirement)
        {
            Assert.That(GameplaySkinLaneResourceFieldCatalog.TryGet(fieldId, out GameplaySkinLaneResourceField? field), Is.True);

            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(BmsKeymode.Key7K);
            var beatmap = new TestProvider("legacy-beatmap-compatibility", _ => SkinSlotResult<TestComponent>.Suppress);
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                field!,
                materialize,
                beatmap: new[] { beatmap },
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(field!.Slot.Requirement, Is.EqualTo(requirement));

                if (requirement == SkinSlotRequirement.Critical)
                {
                    Assert.That(resolution.Result.Value.Name, Is.EqualTo("canonical"));
                    Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected));
                    Assert.That(canonical.QueryCount, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Suppress));
                    Assert.That(resolution.Diagnostics, Is.Empty);
                    Assert.That(canonical.QueryCount, Is.Zero);
                }
            });
        }

        [Test]
        public void TestNullMaterializedComponentFallsThroughWithDiagnostic()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: null-component\n");

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                _ => null!);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Value.Name, Is.EqualTo("canonical"));
                Assert.That(resolution.ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
                Assert.That(resolution.Diagnostics.Single().ProviderName, Is.EqualTo("selected.bms-role-override"));
            });
        }

        [Test]
        public void TestCanonicalFailureEndsInInheritWithDiagnostics()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: broken-selected\n");
            var canonical = new TestProvider("oms-simple", _ => throw new InvalidDataException("canonical package failure"));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference => throw new InvalidDataException(reference.ResourceName),
                canonical: canonical);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Inherit));
                Assert.That(resolution.ProviderName, Is.Null);
                Assert.That(resolution.Diagnostics.Select(diagnostic => diagnostic.ProviderName), Is.EqualTo(new[]
                {
                    "selected.bms-role-override",
                    "oms-simple",
                }));
                Assert.That(resolution.Diagnostics.Select(diagnostic => diagnostic.Code), Is.All.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
            });
        }

        [TestCase(false, GameplaySkinSlotDiagnosticCode.ProvidedValueRejected)]
        [TestCase(true, GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed)]
        public void TestCanonicalValidationFailureEndsInInherit(
            bool validatorThrows,
            GameplaySkinSlotDiagnosticCode expectedDiagnostic)
        {
            var failure = new InvalidDataException("canonical validation failed");
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(BmsKeymode.Key7K);
            using var canonicalComponent = new TestComponent("canonical");
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(canonicalComponent));

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                materialize,
                canonical: canonical,
                validator: _ => validatorThrows ? throw failure : false);

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Inherit));
                Assert.That(resolution.ProviderName, Is.Null);
                Assert.That(resolution.Diagnostics.Single().ProviderName, Is.EqualTo("oms-simple"));
                Assert.That(resolution.Diagnostics.Single().Code, Is.EqualTo(expectedDiagnostic));
                Assert.That(resolution.Diagnostics.Single().Exception, validatorThrows ? Is.SameAs(failure) : Is.Null);
                Assert.That(canonicalComponent.DisposeCount, Is.Zero);
            });
        }

        [Test]
        public void TestOperationCancellationIsNotConvertedToFallback()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: "[Bms]\nKeymode: 7K\nNoteImage1: cancel\n");
            var canonical = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical")));

            Assert.Multiple(() =>
            {
                Assert.That(() => resolve(
                    plan,
                    lane("bms.lane.key-1"),
                    GameplaySkinLaneResourceFieldCatalog.Note,
                    _ => throw new OperationCanceledException(),
                    canonical: canonical), Throws.InstanceOf<OperationCanceledException>());
                Assert.That(canonical.QueryCount, Is.Zero);
            });
        }

        [Test]
        public void TestDiagnosticsAndSafeStringsDoNotExposeResourceName()
        {
            const string private_resource = @"C:\Users\private\skin\note";
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                bmsIni: $"[Bms]\nKeymode: 7K\nNoteImage1: {private_resource}\n");
            BmsGameplaySkinLaneResourceReference? capturedReference = null;

            GameplaySkinSlotResolution<TestComponent> resolution = resolve(
                plan,
                lane("bms.lane.key-1"),
                GameplaySkinLaneResourceFieldCatalog.Note,
                reference =>
                {
                    capturedReference = reference;
                    throw new FileNotFoundException(private_resource);
                });
            string json = JsonConvert.SerializeObject(resolution.Diagnostics);

            Assert.Multiple(() =>
            {
                Assert.That(capturedReference, Is.Not.Null);
                Assert.That(capturedReference!.ToString(), Does.Not.Contain(private_resource));
                Assert.That(resolution.Diagnostics.Single().ToString(), Does.Not.Contain(private_resource));
                Assert.That(resolution.Diagnostics.Single().Slot.ToString(), Does.Not.Contain(private_resource));
                Assert.That(json, Does.Not.Contain(private_resource));
                Assert.That(json, Does.Not.Contain("C:\\Users"));
                Assert.That(resolution.Diagnostics.Single().SlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
            });
        }

        [Test]
        public void TestInvalidFactoryContextAndLookupInputsFailClosed()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(BmsKeymode.Key7K);
            GameplaySkinLaneId key1 = lane("bms.lane.key-1");
            var context = new BmsGameplaySkinLaneResourceContext(plan.Topology, key1, GameplaySkinLaneResourceFieldCatalog.Note);
            var unknownField = new GameplaySkinLaneResourceField("test.unknown-field", GameplaySkinSlotCatalog.Note);
            var otherPlan = createPlan(BmsKeymode.Key7K);
            var otherContext = new BmsGameplaySkinLaneResourceContext(otherPlan.Topology, key1, GameplaySkinLaneResourceFieldCatalog.Note);
            using var owner = new TestComponentOwner(materialize);
            IReadOnlyList<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TestComponent>> providers =
                BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(plan, owner);

            Assert.Multiple(() =>
            {
                Assert.That(() => BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(null!, owner), Throws.ArgumentNullException);
                Assert.That(
                    () => BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(
                        plan,
                        (IBmsGameplaySkinLaneResourceComponentOwner<TestComponent>)null!),
                    Throws.ArgumentNullException);
                Assert.That(() => new BmsGameplaySkinLaneResourceContext(null!, key1, GameplaySkinLaneResourceFieldCatalog.Note), Throws.ArgumentNullException);
                Assert.That(() => new BmsGameplaySkinLaneResourceContext(plan.Topology, null!, GameplaySkinLaneResourceFieldCatalog.Note), Throws.ArgumentNullException);
                Assert.That(() => new BmsGameplaySkinLaneResourceContext(plan.Topology, key1, null!), Throws.ArgumentNullException);
                Assert.That(() => new BmsGameplaySkinLaneResourceContext(plan.Topology, key1, unknownField), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinLaneResourceContext(plan.Topology, key1, GameplaySkinLaneResourceFieldCatalog.Key), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinLaneResourceContext(plan.Topology, key1, GameplaySkinLaneResourceFieldCatalog.KeyPressed), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinLaneResourceContext(plan.Topology, lane("bms.lane.outside"), GameplaySkinLaneResourceFieldCatalog.Note), Throws.ArgumentException);
                Assert.That(() => providers[0].GetSlot(new GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>(GameplaySkinSlotCatalog.LongNoteHead, context)), Throws.ArgumentException);
                Assert.That(() => providers[0].GetSlot(new GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>(GameplaySkinSlotCatalog.Note, otherContext)), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestAdapterSurfaceRemainsInternalImmutableAndRuntimeNeutral()
        {
            Type[] surfaceTypes =
            {
                typeof(BmsGameplaySkinLaneResourceContext),
                typeof(BmsGameplaySkinLaneResourceReference),
                typeof(IBmsGameplaySkinLaneResourceComponentOwner<>),
                typeof(BmsGameplaySkinLaneResourceCandidateProviderFactory),
            };

            Assert.Multiple(() =>
            {
                Assert.That(surfaceTypes.All(type => !type.IsPublic), Is.True);
                Assert.That(typeof(BmsGameplaySkinLaneResourceContext).IsSealed, Is.True);
                Assert.That(typeof(BmsGameplaySkinLaneResourceReference).IsSealed, Is.True);
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).All(property => property.SetMethod == null), Is.True);
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("Drawable"));
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("Texture"));
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType.FullName), Has.None.Contains("ISkin"));
                Assert.That(surfaceTypes.SelectMany(type => type.GetProperties()).Select(property => property.PropertyType), Has.None.AssignableTo<Delegate>());
            });
        }

        private GameplaySkinSlotResolution<TestComponent> resolve(
            BmsGameplaySkinConfigurationCandidatePlan plan,
            GameplaySkinLaneId laneId,
            GameplaySkinLaneResourceField field,
            Func<BmsGameplaySkinLaneResourceReference, TestComponent> materializer,
            IEnumerable<TestProvider>? beatmap = null,
            IEnumerable<TestProvider>? ruleset = null,
            TestProvider? canonical = null,
            Func<TestComponent, bool>? validator = null)
        {
            var context = new BmsGameplaySkinLaneResourceContext(plan.Topology, laneId, field);
            var providers = new List<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TestComponent>>();
            var owner = new TestComponentOwner(materializer);
            componentOwners.Add(owner);

            if (beatmap != null)
                providers.AddRange(beatmap);

            providers.AddRange(BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(plan, owner));

            if (ruleset != null)
                providers.AddRange(ruleset);

            providers.Add(canonical ?? new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("canonical"))));

            return GameplaySkinSlotResolver.Resolve(field.Slot, context, providers, validator);
        }

        private static BmsGameplaySkinConfigurationCandidatePlan createPlan(
            BmsKeymode keymode,
            string bmsIni = "",
            string maniaIni = "")
        {
            BmsPlayfieldStyle style = keymode is BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms or BmsKeymode.Key14K
                ? BmsPlayfieldStyle.Center
                : BmsPlayfieldStyle.P1;

            return BmsGameplaySkinConfigurationCandidateFactory.Create(
                BmsLaneLayout.CreateForKeymode(keymode, style: style),
                decodeBms(bmsIni),
                decodeMania(maniaIni));
        }

        private static TestComponent materialize(BmsGameplaySkinLaneResourceReference reference)
            => new TestComponent(reference.ResourceName, reference);

        private static GameplaySkinLaneId lane(string id) => GameplaySkinLaneId.Create(id);

        private static IReadOnlyList<BmsSkinConfiguration> decodeBms(string skinIni)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(skinIni);
            return decoder.Configurations;
        }

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decodeMania(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }

        private sealed class TestComponent : IDisposable
        {
            public string Name { get; }

            public BmsGameplaySkinLaneResourceReference? Reference { get; }

            public int DisposeCount { get; private set; }

            public TestComponent(string name, BmsGameplaySkinLaneResourceReference? reference = null)
            {
                Name = name;
                Reference = reference;
            }

            public void Dispose() => DisposeCount++;
        }

        private sealed class TestComponentOwner : IBmsGameplaySkinLaneResourceComponentOwner<TestComponent>
        {
            private readonly Func<BmsGameplaySkinLaneResourceReference, TestComponent> materializer;
            private bool disposed;

            public IReadOnlyList<TestComponent> Components => components;

            private readonly List<TestComponent> components = new List<TestComponent>();

            public TestComponentOwner(Func<BmsGameplaySkinLaneResourceReference, TestComponent> materializer)
            {
                this.materializer = materializer;
            }

            public TestComponent Materialize(BmsGameplaySkinLaneResourceReference reference)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                TestComponent component = materializer(reference);

                if (component != null)
                    components.Add(component);

                return component!;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;

                foreach (TestComponent component in components.Distinct())
                    component.Dispose();
            }
        }

        private sealed class TestProvider : IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, TestComponent>
        {
            private readonly Func<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, SkinSlotResult<TestComponent>> getSlot;

            public string Name { get; }

            public int QueryCount { get; private set; }

            public TestProvider(
                string name,
                Func<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, SkinSlotResult<TestComponent>> getSlot)
            {
                Name = name;
                this.getSlot = getSlot;
            }

            public SkinSlotResult<TestComponent> GetSlot(GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext> slot)
            {
                QueryCount++;
                return getSlot(slot);
            }
        }
    }
}
