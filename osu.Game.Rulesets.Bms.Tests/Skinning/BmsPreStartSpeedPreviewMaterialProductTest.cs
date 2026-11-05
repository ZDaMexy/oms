// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestPreStartPreviewOwnsTwoAuthoredNoteVisualsAcrossVisibilityAndDisposal(bool activateBeforeLoad)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            BmsPreStartSpeedPreview preview = null!;
            BmsAsyncNoteDrawable[] notes = [];
            GameplaySkinSpecialisedSceneVisual[] visuals = [];
            Sprite[] sprites = [];
            var metrics = new Bindable<BmsScrollSpeedMetrics>(previewMetrics(BmsHiSpeedMode.Normal));
            int mountedInstanceCount = 0;
            float initialProgress = 0;
            float pausedProgress = 0;
            float[] pausedY = [];

            AddStep("select a real note template and authored Normal preview rectangle", () =>
            {
                (_, candidate) = createCandidate(root =>
                {
                    writeNoteMaterialTemplatePackage(root, true, "notes/public-note");
                    string manifestPath = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
                    JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                    manifest["layouts"] = JArray.Parse("""
                        [{"id":"preview.test","ruleset":"bms","keymode":"5k","presentation":"center-p1",
                          "normalSpeedPreviewRect":[0.19,0.1,0.02,0.6],
                          "stages":[{"rect":[0.1,0.1,0.4,0.6],
                          "lanes":[[0,0.16],[0.17,0.16],[0.34,0.16],[0.51,0.16],[0.68,0.16],[0.85,0.15]]}]}]
                        """);
                    File.WriteAllText(manifestPath, manifest.ToString());
                }, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("selected material publication committed", () => manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount the native five-key playfield with its exact publication", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, useFiveKeyBeatmap: true));
                renderer.ShowBms();
            });
            AddUntilStep("native playfield and authored scene loaded", () => renderer.BmsReady
                && renderer.BmsDrawable.GameplaySkinSceneRuntime?.IsSceneReady == true);
            AddStep("mount an independently owned native preview with the same exact note material", () =>
            {
                BmsPlayfield playfield = renderer.BmsDrawable.Playfield;
                scene = renderer.BmsDrawable.GameplaySkinSceneRuntime!;
                BmsLane lane = playfield.Lanes.Single(candidateLane => candidateLane.LayoutSnapshotLane!.LogicalIndex == 1);
                Assert.That(playfield.LayoutSnapshot.HasAuthoredNormalSpeedPreview, Is.True);
                preview = new BmsPreStartSpeedPreview(lane.LayoutSnapshotLane!, playfield.LayoutSnapshot.Keymode, metrics,
                    playfield.LayoutSnapshot, playfield.ResolvedMaterialSet, lane.HitObjectArea);
                notes = preview.ChildrenOfType<BmsAsyncNoteDrawable>().ToArray();
                Assert.That(notes, Has.Length.EqualTo(2));
                Assert.That(notes.All(note => !note.IsLoaded), Is.True);
                if (activateBeforeLoad)
                    preview.UpdateState(true, false);
                playfield.MountAuthoredSpeedPreview(preview);
            });
            AddUntilStep("both exact specialised note materials finish loading", () => preview.LoadState >= LoadState.Ready
                && notes.All(note => note.SpecialisedSceneVisual?.LoadState >= LoadState.Ready
                                     && note.Drawable is BmsSourceBoundNoteDrawable && note.Drawable.Parent != null));
            AddStep("activation before asynchronous load is retained and inactive owners remain free", () =>
            {
                visuals = notes.Select(note => note.SpecialisedSceneVisual!).ToArray();
                Assert.That(visuals.All(visual => visual.IsApplied == activateBeforeLoad), Is.True);
                Assert.That(visuals.All(visual => visual.BoundObjectId == null), Is.True,
                    "Preview notes must not impersonate scored chart objects.");
                preview.UpdateState(true, false);
                mountedInstanceCount = scene.RuntimeInstanceCount;
                sprites = visuals.Select(visual => (Sprite)visual.RuntimeNodes.Single().ContentDrawable).ToArray();
            });
            AddUntilStep("two actual source textures are visible inside the native preview", () =>
            {
                var bounds = preview.ScreenSpaceDrawQuad.AABBFloat;
                return visuals.All(visual => visual.IsApplied && visual.Alpha > 0)
                       && sprites.All(sprite => sprite.Texture != null && sprite.DrawColourInfo.Colour.AverageColour.Linear.A > 0
                                               && sprite.ScreenSpaceDrawQuad.AABBFloat.Width > 0
                                               && sprite.ScreenSpaceDrawQuad.AABBFloat.Height > 0)
                       && sprites.All(sprite =>
                       {
                           var noteBounds = sprite.ScreenSpaceDrawQuad.AABBFloat;
                           return noteBounds.Left >= bounds.Left - 0.01f && noteBounds.Right <= bounds.Right + 0.01f
                                  && noteBounds.Top >= bounds.Top && noteBounds.Bottom <= bounds.Bottom;
                       });
            });
            AddStep("assert source texture identity and no duplicate preview instances", () =>
            {
                foreach (var pair in notes.Zip(sprites))
                {
                    Assert.That(pair.First.ResolvedMaterialSet, Is.SameAs(scene.MaterialSet));
                    Assert.That(pair.First.SpecialisedSceneVisual!.RuntimeNodes.Single().PreparedNode.Source.Id, Is.EqualTo("typed-note.note"));
                    Assert.That(pair.Second.Texture!.Width, Is.EqualTo(40));
                    Assert.That(pair.Second.Texture.Height, Is.EqualTo(15));
                    Assert.That(pair.First.Drawable!.Parent!.Alpha, Is.Zero,
                        "The source-bound fallback is suppressed only while its actual specialised replacement owns the note.");
                }
                initialProgress = preview.PrimaryNoteProgress;
            });
            AddUntilStep("the existing native preview clock moves both prepared textures", () =>
                Math.Abs(preview.PrimaryNoteProgress - initialProgress) > 0.05f);
            AddStep("pause the same visible owners", () => preview.UpdateState(true, true));
            AddWaitStep("settle the existing framed preview clock", 2);
            AddStep("capture paused positions", () =>
            {
                pausedProgress = preview.PrimaryNoteProgress;
                pausedY = sprites.Select(sprite => sprite.ScreenSpaceDrawQuad.TopLeft.Y).ToArray();
            });
            AddWaitStep("paused preview remains mounted", 3);
            AddStep("pause does not free replace or restart the visual leases", () =>
            {
                Assert.That(preview.PrimaryNoteProgress, Is.EqualTo(pausedProgress).Within(0.0001));
                Assert.That(sprites.Select(sprite => sprite.ScreenSpaceDrawQuad.TopLeft.Y), Is.EqualTo(pausedY));
                Assert.That(visuals.All(visual => visual.IsApplied), Is.True);
                Assert.That(scene.RuntimeInstanceCount, Is.EqualTo(mountedInstanceCount));
                Assert.That(notes.Select(note => note.SpecialisedSceneVisual), Is.EqualTo(visuals));
            });
            AddStep("release Start and free both active leases", () => preview.UpdateState(false, false));
            AddAssert("both hidden notes are free", () => !preview.IsPreviewVisible && visuals.All(visual => !visual.IsApplied && visual.Alpha == 0));
            AddStep("reopen Start with the original two visual owners", () => preview.UpdateState(true, false));
            AddUntilStep("both prepared owners are reapplied", () => preview.IsPreviewVisible && visuals.All(visual => visual.IsApplied && visual.Alpha > 0));
            AddStep("switch the native reading metric to Floating", () => metrics.Value = previewMetrics(BmsHiSpeedMode.Floating));
            AddUntilStep("the authored Normal-only preview frees its hidden owners", () => !preview.IsPreviewVisible
                && visuals.All(visual => !visual.IsApplied && visual.Alpha == 0));
            AddStep("switch back while the preview itself is not updating", () => metrics.Value = previewMetrics(BmsHiSpeedMode.Normal));
            AddUntilStep("Normal resumes the same two prepared owners", () => preview.IsPreviewVisible && visuals.All(visual => visual.IsApplied));
            AddStep("retire the preview while its two owners are active", () => preview.Expire());
            AddUntilStep("preview owners detach and release their exact prepared instances", () => preview.Parent == null
                && visuals.All(visual => !visual.IsApplied && visual.Alpha == 0)
                && scene.RuntimeInstanceCount == mountedInstanceCount - 2);
            AddStep("disposed preview no longer responds to its former metric binding", () =>
            {
                metrics.Value = previewMetrics(BmsHiSpeedMode.Classic);
                Assert.That(preview.IsPreviewActive, Is.False);
                Assert.That(visuals.All(visual => !visual.IsApplied && visual.BoundObjectId == null), Is.True);
                Assert.That(scene.RuntimeFaults, Is.Empty);
                renderer.Expire();
            });
            AddUntilStep("native renderer detaches", () => renderer.Parent == null);
        }

        private static BmsScrollSpeedMetrics previewMetrics(BmsHiSpeedMode mode)
            => BmsScrollSpeedMetrics.FromTimeRange(mode, 1, 1200, 1, 0, 0, 0, 1);
    }
}
