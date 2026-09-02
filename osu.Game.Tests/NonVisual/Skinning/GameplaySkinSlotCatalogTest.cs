// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinSlotCatalogTest
    {
        private const string canonical_contract_sha256 = "28f282d31eeb9097fa8184729b72f7b59d9635bab11c0dd459648325ec65b96d";

        private const string catalog_block_begin = "<!-- GAMEPLAY-SKIN-CATALOG:BEGIN -->";
        private const string catalog_block_end = "<!-- GAMEPLAY-SKIN-CATALOG:END -->";

        private static IEnumerable<GameplaySkinSlotDescriptor> suppressibleSlots =>
            GameplaySkinSlotCatalog.All.Where(slot => slot.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Allowed);

        private static IEnumerable<GameplaySkinSlotDescriptor> nonSuppressibleSlots =>
            GameplaySkinSlotCatalog.All.Where(slot => slot.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Forbidden);

        [Test]
        public void TestCatalogSnapshotAndStableIds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinSlotCatalog.All, Is.Not.Empty);
                Assert.That(GameplaySkinSlotCatalog.All.Select(slot => slot.Id).Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(GameplaySkinSlotCatalog.All.Count));

                foreach (GameplaySkinSlotDescriptor slot in GameplaySkinSlotCatalog.All)
                {
                    Assert.That(slot.Id, Does.Match("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$"));
                    Assert.That(slot.ToString(), Is.EqualTo(slot.Id));
                    Assert.That(slot.StableName, Does.Match("^[A-Z][A-Za-z0-9]*$"));
                    Assert.That(slot.DiagnosticId, Does.Match("^OMS-SKIN-SLOT-[0-9]{3}$"));
                    Assert.That(slot.CatalogVersion, Is.Positive);
                    Assert.That(slot.AllowedScopes, Is.Not.EqualTo(GameplaySkinSlotScope.None));
                    Assert.That(slot.DefaultSemantics, Is.EqualTo(GameplaySkinSlotDefaultSemantics.InheritToLowerAuthorityThenCanonicalFallback));
                    Assert.That(slot.Applicability.Keymodes, Is.Not.EqualTo(GameplaySkinKeymodeApplicability.None));
                }

                Assert.That(GameplaySkinSlotCatalog.All.Select(slot => slot.StableName), Is.Unique);
                Assert.That(GameplaySkinSlotCatalog.All.Select(slot => slot.DiagnosticId), Is.Unique);
            });
        }

        [Test]
        public void TestCommonAndVersionedBmsCatalogAreOneAuthority()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinSlotCatalog.COMMON_VERSION, Is.EqualTo(1));
                Assert.That(GameplaySkinSlotCatalog.BMS_EXTENSION_VERSION, Is.EqualTo(1));
                Assert.That(GameplaySkinSlotCatalog.Common, Is.Not.Empty);
                Assert.That(GameplaySkinSlotCatalog.BmsExtension, Is.Not.Empty);
                Assert.That(GameplaySkinSlotCatalog.Common, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.CatalogFamily == GameplaySkinSlotCatalogFamily.Common));
                Assert.That(GameplaySkinSlotCatalog.BmsExtension, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.CatalogFamily == GameplaySkinSlotCatalogFamily.Bms));
                Assert.That(GameplaySkinSlotCatalog.Common.Concat(GameplaySkinSlotCatalog.BmsExtension), Is.EquivalentTo(GameplaySkinSlotCatalog.All));
                Assert.That(GameplaySkinSlotCatalog.IsSupportedVersion(GameplaySkinSlotCatalogFamily.Common, 1), Is.True);
                Assert.That(GameplaySkinSlotCatalog.IsSupportedVersion(GameplaySkinSlotCatalogFamily.Bms, 1), Is.True);
                Assert.That(GameplaySkinSlotCatalog.IsSupportedVersion(GameplaySkinSlotCatalogFamily.Bms, 2), Is.False);
                Assert.That(GameplaySkinSlotCatalog.Turntable.Applicability.Rulesets, Is.EqualTo(GameplaySkinRulesetApplicability.Bms));
                Assert.That(GameplaySkinSlotCatalog.Turntable.Applicability.Keymodes,
                    Is.EqualTo(GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K | GameplaySkinKeymodeApplicability.Bms14K));
            });
        }

        [Test]
        public void TestCompletePublicMetadataSnapshot()
        {
            const GameplaySkinKeymodeApplicability all_keymodes =
                GameplaySkinKeymodeApplicability.Mania | GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K
                | GameplaySkinKeymodeApplicability.Bms9K | GameplaySkinKeymodeApplicability.Bms14K;

            GameplaySkinSlotDescriptor[] required = GameplaySkinSlotCatalog.All
                .Where(slot => slot.Classification == GameplaySkinSlotClassification.Required).ToArray();
            GameplaySkinSlotDescriptor[] recommended = GameplaySkinSlotCatalog.All
                .Where(slot => slot.Classification == GameplaySkinSlotClassification.Recommended).ToArray();
            GameplaySkinSlotDescriptor[] optional = GameplaySkinSlotCatalog.All
                .Where(slot => slot.Classification == GameplaySkinSlotClassification.Optional).ToArray();
            GameplaySkinSlotDescriptor[] bms = GameplaySkinSlotCatalog.BmsExtension.ToArray();
            GameplaySkinSlotDescriptor[] common = GameplaySkinSlotCatalog.Common.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(required, Is.Not.Empty);
                Assert.That(recommended, Is.Not.Empty);
                Assert.That(optional, Is.Not.Empty);
                Assert.That(required, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Forbidden));
                Assert.That(recommended, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Forbidden && slot.Requirement == SkinSlotRequirement.Optional));
                Assert.That(optional, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Allowed && slot.Requirement == SkinSlotRequirement.Optional));
                Assert.That(GameplaySkinSlotCatalog.All, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.ValueType == GameplaySkinSlotValueType.Resource
                    && slot.DefaultSemantics == GameplaySkinSlotDefaultSemantics.InheritToLowerAuthorityThenCanonicalFallback));
                Assert.That(common, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.CatalogVersion == GameplaySkinSlotCatalog.COMMON_VERSION
                    && slot.Applicability.Rulesets == (GameplaySkinRulesetApplicability.Mania | GameplaySkinRulesetApplicability.Bms)
                    && slot.Applicability.Keymodes == all_keymodes
                    && slot.Applicability.MinimumKeyCount == 1
                    && slot.Applicability.MaximumKeyCount == 20));
                Assert.That(bms, Is.All.Matches<GameplaySkinSlotDescriptor>(slot =>
                    slot.CatalogVersion == GameplaySkinSlotCatalog.BMS_EXTENSION_VERSION
                    && slot.Applicability.Rulesets == GameplaySkinRulesetApplicability.Bms
                    && slot.Applicability.Keymodes == (GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K | GameplaySkinKeymodeApplicability.Bms14K)
                    && slot.Applicability.LaneRoles == GameplaySkinLaneRoleApplicability.Scratch
                    && slot.Applicability.MinimumKeyCount == 5
                    && slot.Applicability.MaximumKeyCount == 14));
                Assert.That(GameplaySkinSlotCatalog.All.Select(slot => slot.DiagnosticId),
                    Is.EqualTo(Enumerable.Range(1, GameplaySkinSlotCatalog.All.Count).Select(index => $"OMS-SKIN-SLOT-{index:000}")));
                Assert.That(GameplaySkinSlotCatalog.Note.AllowedScopes, Is.EqualTo(GameplaySkinSlotScope.Lane));
                Assert.That(GameplaySkinSlotCatalog.HitTarget.AllowedScopes, Is.EqualTo(GameplaySkinSlotScope.Lane));
                Assert.That(GameplaySkinSlotCatalog.LaneDivider.AllowedScopes, Is.EqualTo(GameplaySkinSlotScope.Lane));
                Assert.That(GameplaySkinSlotCatalog.HitTarget.Classification, Is.EqualTo(GameplaySkinSlotClassification.Recommended));
                Assert.That(GameplaySkinSlotCatalog.LaneDivider.Classification, Is.EqualTo(GameplaySkinSlotClassification.Recommended));
                Assert.That(GameplaySkinSlotCatalog.BarLine.AllowedScopes, Is.EqualTo(GameplaySkinSlotScope.Group));
                Assert.That(GameplaySkinSlotCatalog.BgaViewport.AllowedScopes, Is.EqualTo(GameplaySkinSlotScope.Global));
                Assert.That(GameplaySkinSlotCatalog.StageBackground.AllowedScopes, Is.EqualTo(GameplaySkinSlotScope.Stage));
                Assert.That(GameplaySkinSlotCatalog.Decoration.AllowedScopes,
                    Is.EqualTo(GameplaySkinSlotScope.Global | GameplaySkinSlotScope.Stage | GameplaySkinSlotScope.Group | GameplaySkinSlotScope.Lane));
            });
        }

        [Test]
        public void TestCanonicalContractDigestAndGeneratedDocumentation()
        {
            string contract = GameplaySkinSlotCatalogDocumentation.GenerateCanonicalContract();
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contract))).ToLowerInvariant();
            string documentPath = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..",
                "doc_md", "other", "GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md"));
            string document = File.ReadAllText(documentPath).Replace("\r\n", "\n", StringComparison.Ordinal);
            int begin = document.IndexOf(catalog_block_begin, StringComparison.Ordinal);
            int end = document.IndexOf(catalog_block_end, StringComparison.Ordinal);

            Assert.Multiple(() =>
            {
                Assert.That(digest, Is.EqualTo(canonical_contract_sha256));
                Assert.That(begin, Is.GreaterThanOrEqualTo(0));
                Assert.That(end, Is.GreaterThan(begin));
            });

            string generatedBlock = document[(begin + catalog_block_begin.Length)..end].Trim();
            Assert.That(generatedBlock, Is.EqualTo(GameplaySkinSlotCatalogDocumentation.GenerateMarkdownTable()));
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
        public void TestDescriptorRejectsRecommendedSuppressionEligibility()
        {
            var applicability = new GameplaySkinSlotApplicability(
                GameplaySkinRulesetApplicability.Mania,
                GameplaySkinStageApplicability.Single,
                GameplaySkinLaneRoleApplicability.None,
                GameplaySkinKeymodeApplicability.Mania,
                1,
                10);

            Assert.That(() => new GameplaySkinSlotDescriptor(
                "stage.recommended",
                "Recommended",
                GameplaySkinSlotCatalogFamily.Common,
                GameplaySkinSlotCatalog.COMMON_VERSION,
                GameplaySkinSlotScope.Stage,
                GameplaySkinSlotValueType.Resource,
                GameplaySkinSlotClassification.Recommended,
                GameplaySkinSlotDefaultSemantics.InheritToLowerAuthorityThenCanonicalFallback,
                GameplaySkinSlotSuppressEligibility.Allowed,
                applicability,
                "OMS-SKIN-SLOT-999"), Throws.ArgumentException);
        }

        [Test]
        public void TestDefaultRequirementIsCritical()
        {
            Assert.That(default(SkinSlotRequirement), Is.EqualTo(SkinSlotRequirement.Critical));
        }

        [TestCaseSource(nameof(nonSuppressibleSlots))]
        public void TestCatalogForbiddenSuppressFallsBackToOmsSimple(GameplaySkinSlotDescriptor descriptor)
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

        [TestCaseSource(nameof(suppressibleSlots))]
        public void TestCatalogAllowedSuppressStopsFallback(GameplaySkinSlotDescriptor descriptor)
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
                Assert.That(GameplaySkinSlotCatalog.LongNoteTail.Classification, Is.EqualTo(GameplaySkinSlotClassification.Optional));
                Assert.That(GameplaySkinSlotCatalog.LongNoteTail.SuppressEligibility, Is.EqualTo(GameplaySkinSlotSuppressEligibility.Allowed));
                Assert.That(GameplaySkinSlotCatalog.StageBackground.Classification, Is.EqualTo(GameplaySkinSlotClassification.Recommended));
                Assert.That(GameplaySkinSlotCatalog.StageBackground.Requirement, Is.EqualTo(SkinSlotRequirement.Optional));
                Assert.That(GameplaySkinSlotCatalog.StageBackground.SuppressEligibility, Is.EqualTo(GameplaySkinSlotSuppressEligibility.Forbidden));
            });
        }

        [Test]
        public void TestRuntimeCapabilityCannotChangeCatalogSuppressEligibility()
        {
            GameplaySkinRuntimeSlotSupport note = GameplaySkinRuntimeSlotSupport.Create(
                GameplaySkinSlotCatalog.Note,
                GameplaySkinRuntimeSlotCapability.Provide);
            GameplaySkinRuntimeSlotSupport tail = GameplaySkinRuntimeSlotSupport.Create(
                GameplaySkinSlotCatalog.LongNoteTail,
                GameplaySkinRuntimeSlotCapability.Provide | GameplaySkinRuntimeSlotCapability.Suppress);
            GameplaySkinRuntimeCapabilitySet capabilities = GameplaySkinRuntimeCapabilitySet.Create(new[] { note, tail });

            Assert.Multiple(() =>
            {
                Assert.That(capabilities.TryGet(GameplaySkinSlotCatalog.Note, out GameplaySkinRuntimeSlotSupport? noteSupport), Is.True);
                Assert.That(noteSupport, Is.SameAs(note));
                Assert.That(capabilities.TryGet(GameplaySkinSlotCatalog.LongNoteTail, out GameplaySkinRuntimeSlotSupport? tailSupport), Is.True);
                Assert.That(tailSupport, Is.SameAs(tail));
                Assert.That(GameplaySkinSlotCatalog.Note.SuppressEligibility, Is.EqualTo(GameplaySkinSlotSuppressEligibility.Forbidden));
                Assert.That(() => GameplaySkinRuntimeSlotSupport.Create(
                    GameplaySkinSlotCatalog.Note,
                    GameplaySkinRuntimeSlotCapability.Suppress), Throws.ArgumentException);
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

        [Test]
        public void TestProviderIdentityIsPathFreeAndPersistenceSafe()
        {
            const string absolute_path = @"C:\Users\author\private-skin\note.png";
            const string author_content = "private author description";

            var context = new TestLookup("private lookup context", SkinSlotRequirement.Optional);
            var failure = new InvalidOperationException($"Failed to read {absolute_path}: {author_content}");
            var broken = new TestProvider(absolute_path, _ => throw failure);
            var fallback = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("fallback")));
            var authorNamedWinner = new TestProvider(author_content, _ => SkinSlotResult<TestComponent>.Provide(new TestComponent("selected")));

            GameplaySkinSlotResolution<TestComponent> failed = GameplaySkinSlotResolver.Resolve(
                GameplaySkinSlotCatalog.Note, context, new[] { broken, fallback });
            GameplaySkinSlotResolution<TestComponent> won = GameplaySkinSlotResolver.Resolve(
                GameplaySkinSlotCatalog.Note, context, new[] { authorNamedWinner, fallback });
            string serialisedDiagnostic = JsonConvert.SerializeObject(failed.Diagnostics[0]);

            Assert.Multiple(() =>
            {
                Assert.That(failed.Result.Value.Name, Is.EqualTo("fallback"));
                Assert.That(failed.Diagnostics[0].ProviderName, Is.EqualTo("redacted-provider"));
                Assert.That(failed.Diagnostics[0].Exception, Is.SameAs(failure));
                Assert.That(serialisedDiagnostic, Does.Contain("redacted-provider"));
                Assert.That(serialisedDiagnostic, Does.Not.Contain(absolute_path));
                Assert.That(serialisedDiagnostic, Does.Not.Contain("private-skin"));
                Assert.That(serialisedDiagnostic, Does.Not.Contain("Users"));
                Assert.That(serialisedDiagnostic, Does.Not.Contain(author_content));
                Assert.That(serialisedDiagnostic, Does.Not.Contain(context.Context));
                Assert.That(serialisedDiagnostic, Does.Not.Contain(failure.Message));
                Assert.That(won.Result.Value.Name, Is.EqualTo("selected"));
                Assert.That(won.ProviderName, Is.EqualTo("redacted-provider"));
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
