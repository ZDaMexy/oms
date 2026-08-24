// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestReloadSuccessThenLateAttachedRealBmsRendererConsumesExactBResource()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            JourneyRendererHost renderer = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionB = null!;
            string expectedNoteHash = string.Empty;
            int participantLeasesBeforeRenderer = 0;

            addSelectRevisionA(context, external: false);
            AddStep("mount real reload caller before renderer exists", () =>
            {
                revisionA = manager.CurrentRevision;
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for late-renderer reload affordance", () =>
                caller.ReloadCurrentButton.IsLoaded && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write B with a uniquely sized ordinary-note resource", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(20, 210, 120, 255));
                byte[] exactNoteBytes = createRendererRealityPng(7, 11, new Rgba32(20, 210, 120, 255));
                File.WriteAllBytes(Path.Combine(context.PackageRoot, "notes", "note.png"), exactNoteBytes);
                expectedNoteHash = Convert.ToHexString(SHA256.HashData(exactNoteBytes));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for exact same-ID B publication", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && manager.CurrentRevision.RecordId == revisionA.RecordId
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("capture exact B capsule before late renderer attach", () =>
            {
                revisionB = manager.CurrentRevision;
                BmsManagedPackageSourceRevision sourceRevision =
                    ((BmsLegacySkin)revisionB.Owner).CaptureManagedPackageSourceRevision();
                BmsManagedPackageFileRevision noteFile = sourceRevision.Files.Single(file =>
                    string.Equals(file.PackageName, "notes/note.png", StringComparison.OrdinalIgnoreCase));
                participantLeasesBeforeRenderer = revisionB.ParticipantLeaseCount;

                Assert.Multiple(() =>
                {
                    Assert.That(revisionB.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(revisionB.ContentRevision, Is.EqualTo(sourceRevision.PackageContentRevision));
                    Assert.That(noteFile.ContentHash, Is.EqualTo(expectedNoteHash));
                    Assert.That(manager.CurrentRevision, Is.Not.SameAs(revisionA));
                });
            });
            AddStep("late attach production BMS note and LN renderer tree", () =>
                Add(renderer = new JourneyRendererHost(
                    manager,
                    Clock.CurrentTime + 60_000,
                    Clock.CurrentTime + 5_000)));
            AddUntilStep("wait for late renderer host", () => renderer.IsLoaded);
            AddStep("mount late production BMS provider", () => renderer.ShowBms());
            AddUntilStep("wait for late BMS async note artifacts", () => renderer.BmsArtifactsLoaded);
            AddStep("assert real BmsAsyncNoteDrawable consumed exact B visual", () =>
            {
                BmsAsyncNoteDrawable asyncNote = renderer.BmsOrdinary.ChildrenOfType<BmsAsyncNoteDrawable>().Single();
                BmsSourceBoundNoteDrawable artifact = (BmsSourceBoundNoteDrawable)renderer.BmsOrdinaryArtifact;
                Sprite visual = artifact.ChildrenOfType<Sprite>().Single();

                Assert.Multiple(() =>
                {
                    Assert.That(asyncNote.LoadState, Is.GreaterThanOrEqualTo(LoadState.Ready));
                    Assert.That(asyncNote.Drawable, Is.SameAs(artifact));
                    Assert.That(visual.Texture, Is.Not.Null);
                    Assert.That(visual.Texture!.Width, Is.EqualTo(7));
                    Assert.That(visual.Texture.Height, Is.EqualTo(11));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionB));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(revisionB.Owner));
                    Assert.That(revisionB.ParticipantLeaseCount, Is.GreaterThan(participantLeasesBeforeRenderer));
                });

                renderer.Expire();
            });
            AddUntilStep("wait for late renderer detach", () => renderer.Parent == null);
            AddStep("assert B remains current after renderer detach", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionB));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(revisionB.Owner));
                });
            });
        }

        private static byte[] createRendererRealityPng(int width, int height, Rgba32 colour)
        {
            using var image = new Image<Rgba32>(width, height, colour);
            using var output = new MemoryStream();
            image.SaveAsPng(output);
            return output.ToArray();
        }
    }
}
