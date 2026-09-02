// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinDocumentCodecTest
    {
        private static readonly GameplaySkinDocumentIdentity unbound =
            GameplaySkinDocumentIdentity.CreateUnboundPackageParse("content.revision-a");

        private static readonly GameplaySkinDocumentTarget lane_target = GameplaySkinDocumentTarget.ForLane(
            GameplaySkinDocumentRulesetSelector.Any,
            GameplaySkinDocumentTarget.ANY_KEYMODE,
            GameplaySkinDocumentStageModeSelector.Any,
            GameplaySkinLaneGroupId.Create("bms.primary"),
            GameplaySkinLaneId.Create("bms.primary.key-1"),
            0,
            0,
            1,
            1,
            0,
            0);

        [Test]
        public void TestAbsentDeclaredEmptyInvalidValidInheritAndSuppressRemainDistinct()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.Common:1]
                Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
                object.note: resource Provide "notes/note.png"
                object.long-note.head: resource Provide ""
                object.long-note.body: resource Inherit
                object.long-note.tail: resource Suppress
                playfield.key: colour Provide "wrong-type"
                """);

            GameplaySkinDocumentEntry absent = document.GetEntry(GameplaySkinSlotCatalog.Mine, lane_target);
            GameplaySkinDocumentEntry provided = document.GetEntry(GameplaySkinSlotCatalog.Note, lane_target);
            GameplaySkinDocumentEntry empty = document.GetEntry(GameplaySkinSlotCatalog.LongNoteHead, lane_target);
            GameplaySkinDocumentEntry inherited = document.GetEntry(GameplaySkinSlotCatalog.LongNoteBody, lane_target);
            GameplaySkinDocumentEntry suppressed = document.GetEntry(GameplaySkinSlotCatalog.LongNoteTail, lane_target);
            GameplaySkinDocumentEntry invalid = document.GetEntry(GameplaySkinSlotCatalog.KeyVisual, lane_target);
            GameplaySkinDocument roundTrip = decode(GameplaySkinDocumentCodec.Encode(document));

            Assert.Multiple(() =>
            {
                Assert.That(absent.Presence, Is.EqualTo(GameplaySkinDocumentDeclarationPresence.Absent));
                Assert.That(absent.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.None));
                Assert.That(provided.Presence, Is.EqualTo(GameplaySkinDocumentDeclarationPresence.Declared));
                Assert.That(provided.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Valid));
                Assert.That(provided.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Provide));
                Assert.That(provided.Value, Is.EqualTo("notes/note.png"));
                Assert.That(empty.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Empty));
                Assert.That(empty.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Provide));
                Assert.That(empty.Value, Is.Empty);
                Assert.That(inherited.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Valid));
                Assert.That(inherited.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Inherit));
                Assert.That(suppressed.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Valid));
                Assert.That(suppressed.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Suppress));
                Assert.That(invalid.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidValueType));
                Assert.That(roundTrip.Sections.Single().Entries.Select(entry => (entry.Presence, entry.Validity, entry.Operation, entry.Value)),
                    Is.EqualTo(document.Sections.Single().Entries.Select(entry => (entry.Presence, entry.Validity, entry.Operation, entry.Value))));
                Assert.That(roundTrip.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(document.Diagnostics.Select(diagnostic => diagnostic.Code)));
                Assert.That(() => ((IList<GameplaySkinDocumentSection>)document.Sections).Clear(), Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IList<GameplaySkinDocumentEntry>)document.Sections.Single().Entries).Clear(), Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IList<GameplaySkinCodecDiagnostic>)document.Diagnostics).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestWhitespaceCommentsEscapingCaseAndDuplicatesAreDeterministic()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.Common:1]
                   Target : Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0 ; target comment
                object.note : resource Provide "notes/a\"#;b.png" # outside comment
                object.note: resource Provide "notes/duplicate.png"
                Object.Note: resource Inherit
                """);

            GameplaySkinDocumentEntry duplicate = document.GetEntry(GameplaySkinSlotCatalog.Note, lane_target);

            Assert.Multiple(() =>
            {
                Assert.That(document.Sections.Single().Entries[0].Value, Is.EqualTo("notes/a\"#;b.png"));
                Assert.That(duplicate.Value, Is.EqualTo("notes/duplicate.png"));
                Assert.That(duplicate.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(document.Diagnostics.Count(diagnostic => diagnostic.Code == GameplaySkinCodecDiagnosticCode.DuplicateDeclaration), Is.EqualTo(1));
                Assert.That(document.Diagnostics.Count(diagnostic => diagnostic.Code == GameplaySkinCodecDiagnosticCode.UnknownSlot), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestSectionAndOperationCaseAreStrict()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.common:1]
                Target: Global ruleset=any keymode=any stage-mode=any
                bga.frame: resource Inherit
                [GameplaySkin.Common:1]
                Target: Global ruleset=any keymode=any stage-mode=any
                bga.frame: resource inherit
                """);

            Assert.Multiple(() =>
            {
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.UnknownExtension));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidState));
                Assert.That(document.Sections.Single().Entries.Single().Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
            });
        }

        [Test]
        public void TestUnknownExtensionVersionFieldScopeTypeAndIndexFailClosed()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.Common:2]
                Target: Global ruleset=any keymode=any stage-mode=any
                object.note: resource Inherit
                [GameplaySkin.Future:1]
                Future: Value
                [GameplaySkin.Common:1]
                no-colon
                Target: Global ruleset=any keymode=any stage-mode=any
                object.note: colour Provide "wrong"
                Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=-1 global-visual=1 group-local-logical=0 group-local-visual=0
                object.note: resource Inherit
                object.long-note.tail: resource Provide "bad\q"
                """);

            Assert.Multiple(() =>
            {
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.UnsupportedVersion));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.UnknownExtension));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.UnknownField));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidTargetScope));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidValueType));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidTargetIndex));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidEscape));
                Assert.That(document.HasFatalDiagnostics, Is.True);
                Assert.That(document.Sections.Single().Entries, Is.All.Matches<GameplaySkinDocumentEntry>(entry => entry.Validity == GameplaySkinDocumentValueValidity.Invalid));
            });
        }

        [Test]
        public void TestExplicitBmsExtensionAndExactTargetCoordinates()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.Bms:1]
                Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.primary lane=bms.primary.scratch group-logical=0 group-visual=1 global-logical=0 global-visual=7 group-local-logical=0 group-local-visual=7
                playfield.turntable: resource Provide "scratch/turntable.png"
                object.note: resource Inherit
                """);

            GameplaySkinDocumentEntry turntable = document.Sections.Single().Entries[0];

            Assert.Multiple(() =>
            {
                Assert.That(document.Sections.Single().Family, Is.EqualTo(GameplaySkinSlotCatalogFamily.Bms));
                Assert.That(document.Sections.Single().Version, Is.EqualTo(GameplaySkinSlotCatalog.BMS_EXTENSION_VERSION));
                Assert.That(turntable.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Valid));
                Assert.That(turntable.Target.GroupId, Is.EqualTo(GameplaySkinLaneGroupId.Create("bms.primary")));
                Assert.That(turntable.Target.LaneId, Is.EqualTo(GameplaySkinLaneId.Create("bms.primary.scratch")));
                Assert.That(turntable.Target.GroupLogicalIndex, Is.Zero);
                Assert.That(turntable.Target.GroupVisualIndex, Is.EqualTo(1));
                Assert.That(turntable.Target.GlobalLogicalIndex, Is.Zero);
                Assert.That(turntable.Target.GlobalVisualIndex, Is.EqualTo(7));
                Assert.That(turntable.Target.GroupLocalLogicalIndex, Is.Zero);
                Assert.That(turntable.Target.GroupLocalVisualIndex, Is.EqualTo(7));
                Assert.That(document.Sections.Single().Entries[1].Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.ExtensionSlotMismatch));
            });
        }

        [Test]
        public void TestStageAndGroupTargetsRequireStableGroupIdentityAndBothIndices()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.Common:1]
                Target: Stage ruleset=mania keymode=5k stage-mode=single group=mania.primary group-logical=0 group-visual=1
                stage.background: resource Inherit
                Target: Group ruleset=mania keymode=5k stage-mode=single group=mania.primary group-logical=0 group-visual=1
                playfield.bar-line: resource Provide "bar.png"
                Target: Stage ruleset=mania keymode=5k stage-mode=single stage=0
                stage.foreground: resource Inherit
                """);

            Assert.Multiple(() =>
            {
                Assert.That(document.Sections.Single().Entries[0].Target.Kind, Is.EqualTo(GameplaySkinDocumentTargetKind.Stage));
                Assert.That(document.Sections.Single().Entries[1].Target.Kind, Is.EqualTo(GameplaySkinDocumentTargetKind.Group));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidTargetIndex));
                Assert.That(document.Sections.Single().Entries[2].Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
            });
        }

        [Test]
        public void TestOnlyClassificationOptionalCanSuppress()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.Common:1]
                Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
                object.note: resource Suppress
                object.long-note.tail: resource Suppress
                Target: Stage ruleset=any keymode=any stage-mode=any group=bms.primary group-logical=0 group-visual=0
                stage.background: resource Suppress
                """);

            GameplaySkinDocumentEntry note = document.Sections.Single().Entries[0];
            GameplaySkinDocumentEntry tail = document.Sections.Single().Entries[1];
            GameplaySkinDocumentEntry recommended = document.Sections.Single().Entries[2];

            Assert.Multiple(() =>
            {
                Assert.That(note.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Suppress));
                Assert.That(note.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(tail.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Valid));
                Assert.That(recommended.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Suppress));
                Assert.That(recommended.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(document.Diagnostics.Count(diagnostic => diagnostic.Code == GameplaySkinCodecDiagnosticCode.SuppressionForbidden), Is.EqualTo(2));
            });
        }

        [Test]
        public void TestLegacySectionsAreTokenizedOnceRetainedAndRoundTrip()
        {
            const string source = """
                                  [General]
                                  Name: portable author skin
                                  UnknownLegacy: remains
                                  [Bms]
                                  Keymode: 7K
                                  NoteImage1: notes/one
                                  ; legacy comment
                                  malformed legacy line
                                  [GameplaySkin.Common:1]
                                  Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
                                  object.note: resource Provide "notes/one"
                                  """;

            GameplaySkinDocument first = decode(source);
            GameplaySkinDocument second = decode(GameplaySkinDocumentCodec.Encode(first));
            GameplaySkinLegacySection bms = first.LegacySections.Single(section => section.Name == "Bms");

            Assert.Multiple(() =>
            {
                Assert.That(first.Diagnostics, Is.Empty);
                Assert.That(first.LegacySections.Select(section => section.Name), Is.EqualTo(new[] { "General", "Bms" }));
                Assert.That(bms.Lines.Select(line => line.Kind), Is.EqualTo(new[]
                {
                    GameplaySkinLegacyLineKind.Field,
                    GameplaySkinLegacyLineKind.Field,
                    GameplaySkinLegacyLineKind.Comment,
                    GameplaySkinLegacyLineKind.Unparsed,
                }));
                Assert.That(bms.Lines[1].Key, Is.EqualTo("NoteImage1"));
                Assert.That(bms.Lines[1].Value, Is.EqualTo("notes/one"));
                Assert.That(second.LegacySections.Select(section => section.Name), Is.EqualTo(first.LegacySections.Select(section => section.Name)));
                Assert.That(second.Sections.Single().Entries.Single().Value, Is.EqualTo("notes/one"));
                Assert.That(() => ((IList<GameplaySkinLegacySection>)first.LegacySections).Clear(), Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IList<GameplaySkinLegacyLine>)bms.Lines).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestWithIdentityRetainsExactRevisionAndDoesNotRetokenize()
        {
            GameplaySkinDocument parsed = decode(
                """
                [GameplaySkin.Common:1]
                Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
                object.note: resource Inherit
                """);
            var sourceId = new Guid("dca12f44-178d-4ee6-9f16-af80fe2f7e17");
            GameplaySkinDocumentIdentity boundIdentity = GameplaySkinDocumentIdentity.CreateBound(
                GameplaySkinDocumentSourceKind.ManagedFolder,
                sourceId,
                "content.revision-a",
                19,
                23,
                29);
            GameplaySkinDocument bound = parsed.WithIdentity(boundIdentity);

            Assert.Multiple(() =>
            {
                Assert.That(bound.Identity, Is.SameAs(boundIdentity));
                Assert.That(bound.Identity.SourceId, Is.EqualTo(sourceId));
                Assert.That(bound.Identity.ContentRevision, Is.EqualTo("content.revision-a"));
                Assert.That(bound.Identity.PackageRevision, Is.EqualTo(19));
                Assert.That(bound.Identity.CurrentRevision, Is.EqualTo(23));
                Assert.That(bound.Identity.LayoutRevision, Is.EqualTo(29));
                Assert.That(bound.Sections, Is.SameAs(parsed.Sections));
                Assert.That(bound.LegacySections, Is.SameAs(parsed.LegacySections));
                Assert.That(bound.Diagnostics, Is.SameAs(parsed.Diagnostics));
                Assert.That(bound.Identity.ToString(), Does.Not.Contain(sourceId.ToString()));
                Assert.That(bound.Identity.ToString(), Does.Not.Contain("content.revision-a"));
                Assert.That(() => parsed.WithIdentity(GameplaySkinDocumentIdentity.CreateBound(
                    GameplaySkinDocumentSourceKind.ManagedFolder,
                    sourceId,
                    "different.content",
                    19,
                    23,
                    29)), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestDiagnosticsAreStableAndRedacted()
        {
            const string private_content = "C:\\Users\\author\\secret\\note.png";
            GameplaySkinDocument document = decode(
                $"""
                 [GameplaySkin.Common:1]
                 Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
                 object.note: resource Provide "{private_content}"
                 object.note: resource Provide "duplicate"
                 """);

            Assert.Multiple(() =>
            {
                Assert.That(document.Diagnostics, Is.Not.Empty);
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Id), Is.All.Matches<string>(id => id.StartsWith("OMS-SKIN-CODEC-", StringComparison.Ordinal)));
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.ToString()), Has.None.Contains(private_content));
                Assert.That(document.ToString(), Does.Not.Contain(private_content));
                Assert.That(GameplaySkinDocumentCodec.CONTRACT_ID, Is.EqualTo("oms-gameplay-skin-codec.v1"));
                Assert.That(GameplaySkinSlotCatalog.CONTRACT_ID, Is.EqualTo("oms-gameplay-skin-catalog.v1"));
            });
        }

        [Test]
        public void TestInvalidUtf8ProducesStableDocumentFailure()
        {
            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(
                new byte[] { 0xff, 0xfe, 0xfd },
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("invalid.content"));

            Assert.Multiple(() =>
            {
                Assert.That(document.Sections, Is.Empty);
                Assert.That(document.LegacySections, Is.Empty);
                Assert.That(document.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinCodecDiagnosticCode.InvalidUtf8));
                Assert.That(document.Diagnostics.Single().Id, Is.EqualTo("OMS-SKIN-CODEC-001"));
                Assert.That(document.HasFatalDiagnostics, Is.True);
            });
        }

        [Test]
        public void TestMalformedDeclarationStillClaimsExactDuplicateKey()
        {
            GameplaySkinDocument document = decode(
                """
                [GameplaySkin.Common:1]
                Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
                object.note: resource Provide "notes/bad\q.png"
                object.note: resource Provide "notes/later-valid.png"
                """);

            GameplaySkinDocumentEntry winner = document.GetEntry(GameplaySkinSlotCatalog.Note, lane_target);

            Assert.Multiple(() =>
            {
                Assert.That(document.Sections.Single().Entries, Has.Count.EqualTo(2));
                Assert.That(document.Sections.Single().Entries[0].Descriptor, Is.SameAs(GameplaySkinSlotCatalog.Note));
                Assert.That(document.Sections.Single().Entries[1].Descriptor, Is.SameAs(GameplaySkinSlotCatalog.Note));
                Assert.That(document.Sections.Single().Entries[0].Target, Is.EqualTo(document.Sections.Single().Entries[1].Target));
                Assert.That(document.Sections.Single().Entries[0].Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(winner.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(winner.Value, Is.EqualTo("notes/later-valid.png"));
                Assert.That(document.Diagnostics.Count(diagnostic => diagnostic.Code == GameplaySkinCodecDiagnosticCode.InvalidEscape), Is.EqualTo(1));
                Assert.That(document.Diagnostics.Count(diagnostic => diagnostic.Code == GameplaySkinCodecDiagnosticCode.DuplicateDeclaration), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestSingleLeadingUtf8BomIsAcceptedButEmbeddedBomIsFatal()
        {
            const string source = "[General]\nName: BOM Skin\n[GameplaySkin.Common:1]\n"
                                  + "Target: Global ruleset=any keymode=any stage-mode=any\n"
                                  + "bga.frame: resource Inherit\n";
            byte[] sourceBytes = Encoding.UTF8.GetBytes(source);
            byte[] withBom = new byte[sourceBytes.Length + 3];
            withBom[0] = 0xef;
            withBom[1] = 0xbb;
            withBom[2] = 0xbf;
            sourceBytes.CopyTo(withBom, 3);
            GameplaySkinDocument accepted = GameplaySkinDocumentCodec.Decode(
                withBom,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("bom.exact-content"));
            GameplaySkinDocument embedded = decode(
                "[General]\nName: Before\n\ufeff[GameplaySkin.Common:1]\n"
                + "Target: Global ruleset=any keymode=any stage-mode=any\n"
                + "bga.frame: resource Inherit\n");

            Assert.Multiple(() =>
            {
                Assert.That(accepted.Diagnostics, Is.Empty);
                Assert.That(accepted.LegacySections.Select(section => section.Name), Does.Contain("General"));
                Assert.That(accepted.Sections.Single().Entries.Single().Descriptor, Is.SameAs(GameplaySkinSlotCatalog.BgaFrame));
                Assert.That(GameplaySkinDocumentCodec.Encode(accepted), Does.StartWith("[General]"));
                Assert.That(embedded.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.UnexpectedBom));
                Assert.That(embedded.HasFatalDiagnostics, Is.True);
            });
        }

        [Test]
        public void TestWireIntegersAreCanonicalAsciiAndCultureIndependent()
        {
            string[] invalidHeaders =
            {
                "[GameplaySkin.Common:+1]",
                "[GameplaySkin.Common:01]",
                "[GameplaySkin.Common:１]",
                "[GameplaySkin.Common:999999999999999999999999]",
            };
            string[] invalidIndices = { "+0", "00", "０", "999999999999999999999999" };

            foreach (string header in invalidHeaders)
            {
                GameplaySkinDocument document = decode(
                    $"{header}\nTarget: Global ruleset=any keymode=any stage-mode=any\nbga.frame: resource Inherit\n");
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.UnsupportedVersion));
                Assert.That(document.HasFatalDiagnostics, Is.True);
            }

            foreach (string index in invalidIndices)
            {
                GameplaySkinDocument document = decode(
                    "[GameplaySkin.Common:1]\n"
                    + $"Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical={index} group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0\n"
                    + "object.note: resource Inherit\n");
                Assert.That(document.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.InvalidTargetIndex));
                Assert.That(document.Sections.Single().Entries.Single().Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
            }
        }

        [Test]
        public void TestMalformedGameplayHeaderAndTargetFailClosedWithoutReusingPreviousTarget()
        {
            GameplaySkinDocument malformedHeader = decode(
                "[GameplaySkin.Common:1\n"
                + "Target: Global ruleset=any keymode=any stage-mode=any\n"
                + "bga.frame: resource Provide \"must-not-become-legacy\"\n");
            GameplaySkinDocument malformedSuffix = decode(
                "[GameplaySkin.Common:1]junk\n"
                + "Target: Global ruleset=any keymode=any stage-mode=any\n"
                + "bga.frame: resource Inherit\n");
            GameplaySkinDocument badTarget = decode(
                "[GameplaySkin.Common:1]\n"
                + "Target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0\n"
                + "object.note: resource Inherit\n"
                + "target: Lane ruleset=any keymode=any stage-mode=any group=bms.primary lane=bms.primary.key-2 group-logical=0 group-visual=0 global-logical=2 global-visual=2 group-local-logical=1 group-local-visual=1\n"
                + "object.long-note.head: resource Provide \"must-not-bind-to-first-target\"\n");

            Assert.Multiple(() =>
            {
                Assert.That(malformedHeader.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.MalformedSectionHeader));
                Assert.That(malformedSuffix.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.MalformedSectionHeader));
                Assert.That(malformedHeader.HasFatalDiagnostics, Is.True);
                Assert.That(malformedSuffix.HasFatalDiagnostics, Is.True);
                Assert.That(malformedHeader.LegacySections, Is.Empty);
                Assert.That(malformedSuffix.LegacySections, Is.Empty);
                Assert.That(badTarget.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.UnknownField));
                Assert.That(badTarget.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinCodecDiagnosticCode.MissingTarget));
                Assert.That(badTarget.GetEntry(GameplaySkinSlotCatalog.LongNoteHead, lane_target).Presence,
                    Is.EqualTo(GameplaySkinDocumentDeclarationPresence.Absent));
            });
        }

        private static GameplaySkinDocument decode(string source) => GameplaySkinDocumentCodec.Decode(source, unbound);
    }
}
