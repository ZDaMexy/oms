// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Argon;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.Skinning.Oms;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    [HeadlessTest]
    public partial class TestSceneManiaGameplaySkinLayoutProduction : OsuTestScene, IStorageResourceProvider
    {
        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        private Drawable host = null!;
        private RulesetSkinProvidingContainer skinProvider = null!;
        private ExactDependencyProbeContainer productionContent = null!;
        private ManiaGameplayHudComponentsContainer productionHud = null!;
        private DrawableManiaRuleset drawableRuleset = null!;
        private ManiaRuleset productionRuleset = null!;
        private ManiaBeatmap productionBeatmap = null!;
        private ManiaRulesetConfigManager config = null!;
        private ScoreProcessor scoreProcessor = null!;
        private DrawableNote productionNote = null!;
        private DrawableHoldNote productionHold = null!;

        [Test]
        public void TestExactRulesetProviderPublishesOneSnapshotForProductionTree()
        {
            AddStep("create exact dual-stage gameplay root", () =>
            {
                var ruleset = productionRuleset = new ManiaRuleset();
                scoreProcessor = ruleset.CreateScoreProcessor();
                config = (ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(ruleset).AsNonNull();
                config.SetValue(ManiaRulesetSetting.ScrollDirection, ManiaScrollingDirection.Down);

                var beatmap = productionBeatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.Stages.Add(new StageDefinition(5));

                beatmap.HitObjects.Add(new Note
                {
                    Column = 0,
                    StartTime = 1000,
                });
                beatmap.HitObjects.Add(new HoldNote
                {
                    Column = 8,
                    StartTime = 1200,
                    Duration = 1000,
                });

                foreach (ManiaHitObject hitObject in beatmap.HitObjects)
                    hitObject.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());

                drawableRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Add(host = skinProvider = new RulesetSkinProvidingContainer(ruleset, beatmap, null, prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = productionContent = new ExactDependencyProbeContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = drawableRuleset,
                    },
                });
            });

            AddUntilStep("production gameplay tree loaded", () => drawableRuleset.IsLoaded
                                                                    && drawableRuleset.Playfield.Stages.All(stage => stage.IsLoaded));
            AddAssert("provider package is exact", () => drawableRuleset.LayoutSnapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility);
            AddAssert("owner publication adapter and root share one exact reference", () =>
                ReferenceEquals(drawableRuleset.LayoutRevisionOwner.CurrentPublication?.Snapshot, drawableRuleset.LayoutSnapshot)
                && ReferenceEquals(
                    drawableRuleset.LayoutRevisionOwner.CurrentPublication?.GetAdapter<ManiaGameplaySkinLayout>(),
                    drawableRuleset.LayoutAdapter)
                && ReferenceEquals(drawableRuleset.LayoutAdapter.Snapshot, drawableRuleset.LayoutSnapshot));
            AddStep("prepare is background", () => Assert.That(drawableRuleset.LayoutRevisionOwner.LastPrepareWasUpdateThread, Is.False));
            AddStep("commit is update thread", () => Assert.That(drawableRuleset.LayoutRevisionOwner.LastCommitWasUpdateThread, Is.True));
            GameplaySkinLayoutPublication firstPublication = null!;
            AddStep("same root double prepare fails closed", () =>
            {
                firstPublication = drawableRuleset.LayoutRevisionOwner.CurrentPublication!;
                Assert.That(
                    () => productionRuleset.PrepareGameplaySkinLayout(productionBeatmap, productionContent.CapturedDependencies),
                    Throws.InvalidOperationException.With.Message.Contains("exactly one immutable layout"));
            });
            AddAssert("failed double prepare retains exact publication", () =>
                ReferenceEquals(firstPublication, drawableRuleset.LayoutRevisionOwner.CurrentPublication)
                && firstPublication.Snapshot.Context.LayoutRevision == drawableRuleset.LayoutSnapshot.Context.LayoutRevision);
            AddAssert("native dual vector retained", () => drawableRuleset.LayoutSnapshot.Context.NativeContextId == "stages-4-5");
            AddAssert("root playfield stages and columns share exact snapshot", () =>
                ReferenceEquals(drawableRuleset.LayoutSnapshot, drawableRuleset.Playfield.LayoutSnapshot)
                && drawableRuleset.Playfield.Stages.All(stage =>
                    ReferenceEquals(stage.LayoutSnapshot, drawableRuleset.LayoutSnapshot)
                    && stage.ChildrenOfType<ColumnFlow<Column>>().Any()
                    && stage.ChildrenOfType<ColumnFlow<Column>>().All(flow => ReferenceEquals(flow.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                    && stage.Columns.All(column => ReferenceEquals(column.LayoutSnapshot, drawableRuleset.LayoutSnapshot))));
            AddAssert("real stage and column quads project the exact snapshot", () =>
            {
                const float tolerance = 1f;
                GameplaySkinLayoutSnapshot snapshot = drawableRuleset.LayoutSnapshot;
                GameplaySkinLayoutRect screen = snapshot.Context.ScreenBounds;
                var playfieldBounds = drawableRuleset.Playfield.ScreenSpaceDrawQuad.AABBFloat;

                for (int stageIndex = 0; stageIndex < drawableRuleset.Playfield.Stages.Count; stageIndex++)
                {
                    Stage stage = drawableRuleset.Playfield.Stages[stageIndex];
                    GameplaySkinLayoutGroup group = snapshot.GroupsInLogicalOrder[stageIndex];
                    var stageBounds = stage.ScreenSpaceDrawQuad.AABBFloat;
                    float expectedStageLeft = playfieldBounds.Left + (group.Rect.Left - screen.Left) / screen.Width * playfieldBounds.Width;
                    float expectedStageWidth = group.Rect.Width / screen.Width * playfieldBounds.Width;

                    if (stageBounds.Width <= 1
                        || System.Math.Abs(stageBounds.Left - expectedStageLeft) > tolerance
                        || System.Math.Abs(stageBounds.Width - expectedStageWidth) > tolerance)
                    {
                        return false;
                    }

                    foreach (GameplaySkinLaneTopologyEntry topologyLane in group.TopologyGroup.LanesInLogicalOrder)
                    {
                        Column column = stage.Columns[topologyLane.GroupLocalLogicalIndex];
                        GameplaySkinLayoutRect lane = snapshot.GetLane(topologyLane.Identity.Id).Rect;
                        var columnBounds = column.ScreenSpaceDrawQuad.AABBFloat;
                        float expectedColumnLeft = stageBounds.Left + (lane.Left - group.Rect.Left) / group.Rect.Width * stageBounds.Width;
                        float expectedColumnWidth = lane.Width / group.Rect.Width * stageBounds.Width;

                        if (columnBounds.Width <= 1
                            || System.Math.Abs(columnBounds.Left - expectedColumnLeft) > tolerance
                            || System.Math.Abs(columnBounds.Width - expectedColumnWidth) > tolerance)
                        {
                            return false;
                        }
                    }
                }

                return true;
            });
            AddStep("attach production mania HUD", () =>
            {
                productionHud = (ManiaGameplayHudComponentsContainer)skinProvider.GetDrawableComponent(
                    new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new ManiaRuleset().RulesetInfo))!;

                foreach (Drawable child in productionHud.Children.Where(child => child is not OmsManiaComboCounter && child is not LegacyManiaComboCounter).ToArray())
                    productionHud.Remove(child, false);

                productionContent.Add(new HudDependenciesContainer(drawableRuleset.ScrollingInfo, scoreProcessor)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = productionHud,
                });
            });
            AddUntilStep("production mania HUD loaded", () => productionHud.IsLoaded);
            AddAssert("HUD wrapper shares exact snapshot", () => ReferenceEquals(productionHud.LayoutSnapshot, drawableRuleset.LayoutSnapshot));
            AddAssert("combo geometry comes from exact snapshot surface", () =>
            {
                Drawable combo = productionHud.Children.Single(child => child is OmsManiaComboCounter or LegacyManiaComboCounter);
                GameplaySkinLayoutRect surface = drawableRuleset.LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect;
                return combo.RelativePositionAxes == Axes.Both
                       && System.Math.Abs(combo.X - (surface.Left + surface.Width / 2)) < 0.001f
                       && System.Math.Abs(combo.Y - (surface.Top + surface.Height / 2)) < 0.001f;
            });
            AddStep("add production note hold and barline", () =>
            {
                var note = new Note { Column = 0, StartTime = Time.Current + 1000 };
                note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                drawableRuleset.Playfield.Add(productionNote = new DrawableNote(note));

                var hold = new HoldNote { Column = 8, StartTime = Time.Current + 1200, Duration = 1000 };
                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                drawableRuleset.Playfield.Add(productionHold = new DrawableHoldNote(hold));
                drawableRuleset.Playfield.Add(new BarLine { StartTime = Time.Current + 500 });
            });
            AddUntilStep("production note hold and barline loaded", () => this.ChildrenOfType<DrawableNote>().Any()
                                                                            && this.ChildrenOfType<DrawableHoldNote>().Any()
                                                                            && this.ChildrenOfType<DrawableBarLine>().Any());
            AddAssert("objects and barline share exact snapshot", () =>
                this.ChildrenOfType<DrawableNote>().All(note => ReferenceEquals(note.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                && this.ChildrenOfType<DrawableHoldNote>().All(hold => ReferenceEquals(hold.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                && this.ChildrenOfType<DrawableBarLine>().All(line => ReferenceEquals(line.StageLayoutSnapshot, drawableRuleset.LayoutSnapshot)));
            AddAssert("hit targets and core adjustment share exact snapshot", () =>
                this.ChildrenOfType<HitPositionPaddedContainer>().Any()
                && this.ChildrenOfType<HitPositionPaddedContainer>().All(target => ReferenceEquals(target.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                && this.ChildrenOfType<ManiaPlayfieldAdjustmentContainer>().All(core => ReferenceEquals(core.LayoutSnapshot, drawableRuleset.LayoutSnapshot)));

            AddStep("publish real stage judgement", () =>
            {
                drawableRuleset.Playfield.Stages[0].OnNewResult(productionNote,
                    new JudgementResult(productionNote.HitObject, new ManiaJudgement()) { Type = HitResult.Perfect });
            });
            AddUntilStep("production judgement loaded", () => this.ChildrenOfType<OmsManiaJudgementPiece>().Any()
                                                                   || this.ChildrenOfType<LegacyManiaJudgementPiece>().Any()
                                                                   || this.ChildrenOfType<DefaultManiaJudgementPiece>().Any());
            AddAssert("judgement shares exact snapshot", () =>
                this.ChildrenOfType<OmsManiaJudgementPiece>().All(piece => ReferenceEquals(piece.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                && this.ChildrenOfType<LegacyManiaJudgementPiece>().All(piece => ReferenceEquals(piece.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                && this.ChildrenOfType<DefaultManiaJudgementPiece>().All(piece => ReferenceEquals(piece.LayoutSnapshot, drawableRuleset.LayoutSnapshot)));

            AddStep("change direction setting after publication", () => config.SetValue(ManiaRulesetSetting.ScrollDirection, ManiaScrollingDirection.Up));
            AddAssert("root keeps published direction", () => drawableRuleset.LayoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Down
                                                                  && drawableRuleset.PublishedDirection == ScrollingDirection.Down);
            AddStep("detach exact gameplay root", () => host.Expire());
            AddUntilStep("exact gameplay root detached", () => host.Parent == null);
        }

        [Test]
        public void TestArgonJudgementConsumesExactProductionSnapshot()
        {
            Drawable argonHost = null!;
            DrawableManiaRuleset argonRuleset = null!;
            DrawableNote argonNote = null!;
            Note note = null!;

            AddStep("create exact Argon gameplay root", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.Stages.Add(new StageDefinition(5));
                note = new Note
                {
                    Column = 0,
                    StartTime = Time.Current + 5000,
                };
                note.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());
                beatmap.HitObjects.Add(note);

                argonRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Add(argonHost = new RulesetSkinProvidingContainer(ruleset, beatmap, new ArgonSkin(this), prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = argonRuleset,
                });
            });

            AddUntilStep("Argon production note loaded", () => argonRuleset.IsLoaded
                                                                && (argonNote = this.ChildrenOfType<DrawableNote>()
                                                                                      .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, note))!) != null);
            AddStep("publish Argon judgement", () => argonRuleset.Playfield.Stages[0].OnNewResult(argonNote,
                new JudgementResult(argonNote.HitObject, new ManiaJudgement()) { Type = HitResult.Perfect }));
            AddUntilStep("Argon judgement loaded", () => this.ChildrenOfType<ArgonJudgementPiece>().Any(piece => piece.IsLoaded));
            AddAssert("Argon judgement shares exact snapshot and surface centre", () =>
            {
                ArgonJudgementPiece piece = this.ChildrenOfType<ArgonJudgementPiece>().Single();
                GameplaySkinLayoutGroup group = argonRuleset.LayoutSnapshot.GroupsInLogicalOrder[0];
                GameplaySkinLayoutRect judgement = argonRuleset.LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.JUDGEMENT_SURFACE).Rect;
                float expectedY = (judgement.Top + judgement.Height / 2 - group.Rect.Top) / group.Rect.Height;
                return argonRuleset.LayoutSnapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility
                       && ReferenceEquals(piece.LayoutSnapshot, argonRuleset.LayoutSnapshot)
                       && piece.RelativePositionAxes == Axes.Both
                       && System.Math.Abs(piece.X - 0.5f) < 0.001f
                       && System.Math.Abs(piece.Y - expectedY) < 0.001f;
            });
            AddStep("detach Argon gameplay root", () => argonHost.Expire());
            AddUntilStep("Argon gameplay root detached", () => argonHost.Parent == null);
        }

        [Test]
        public void TestInvalidGeometryFallsBackInsideOneExactProductionPublication()
        {
            Drawable invalidHost = null!;
            DrawableManiaRuleset invalidRuleset = null!;

            AddStep("create exact invalid-geometry gameplay root", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };

                invalidRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Add(invalidHost = new RulesetSkinProvidingContainer(
                    ruleset,
                    beatmap,
                    new InvalidGeometrySkin(),
                    prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = invalidRuleset,
                });
            });

            AddUntilStep("invalid-geometry production root loaded", () => invalidRuleset.IsLoaded
                                                                               && invalidRuleset.Playfield.Stages.All(stage => stage.IsLoaded));
            AddAssert("invalid fields retain one exact complete fallback snapshot", () =>
            {
                GameplaySkinLayoutSnapshot snapshot = invalidRuleset.LayoutSnapshot;
                string[] diagnostics = snapshot.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();

                return snapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility
                       && diagnostics.Contains("mania.layout.column-width-fallback")
                       && diagnostics.Contains("mania.layout.hit-position-fallback")
                       && diagnostics.Contains("mania.layout.stage-padding-top-fallback")
                       && diagnostics.Contains("mania.layout.barline-height-fallback")
                       && diagnostics.Contains("mania.layout.combo-position-fallback")
                       && snapshot.GroupsInLogicalOrder.All(group => isFinitePositiveAndContained(snapshot.Context.SafeBounds, group.Rect))
                       && snapshot.LanesInLogicalOrder.All(lane => isFinitePositiveAndContained(snapshot.Context.SafeBounds, lane.Rect))
                       && snapshot.Surfaces.All(surface => isFinitePositiveAndContained(snapshot.Context.SafeBounds, surface.Rect))
                       && invalidRuleset.Playfield.Stages.All(stage => ReferenceEquals(stage.LayoutSnapshot, snapshot)
                                                                        && stage.Columns.All(column => ReferenceEquals(column.LayoutSnapshot, snapshot)));
            });
            AddStep("detach invalid-geometry gameplay root", () => invalidHost.Expire());
            AddUntilStep("invalid-geometry gameplay root detached", () => invalidHost.Parent == null);
        }

        [Test]
        public void TestLegacyStageBackgroundProjectsTheExactProductionLanes()
        {
            Drawable legacyHost = null!;
            DrawableManiaRuleset legacyRuleset = null!;

            AddStep("create exact legacy dual-stage gameplay root", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.Stages.Add(new StageDefinition(5));

                legacyRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Add(legacyHost = new RulesetSkinProvidingContainer(
                    ruleset,
                    beatmap,
                    new DefaultLegacySkin(this),
                    prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = legacyRuleset,
                });
            });

            AddUntilStep("legacy stage backgrounds loaded", () => legacyRuleset.IsLoaded
                                                                        && legacyRuleset.Playfield.Stages.All(stage =>
                                                                            stage.ChildrenOfType<ColumnFlow<Drawable>>().SingleOrDefault()?.IsLoaded == true));
            AddAssert("legacy backgrounds project the exact non-degenerate lane quads", () =>
            {
                foreach (Stage stage in legacyRuleset.Playfield.Stages)
                {
                    ColumnFlow<Drawable> flow = stage.ChildrenOfType<ColumnFlow<Drawable>>().Single();
                    var stageBounds = stage.ScreenSpaceDrawQuad.AABBFloat;
                    var flowBounds = flow.ScreenSpaceDrawQuad.AABBFloat;

                    if (!ReferenceEquals(flow.LayoutSnapshot, legacyRuleset.LayoutSnapshot)
                        || flowBounds.Width <= 1
                        || System.Math.Abs(flowBounds.Left - stageBounds.Left) > 1
                        || System.Math.Abs(flowBounds.Width - stageBounds.Width) > 1
                        || flow.Content.Any(column => column.ScreenSpaceDrawQuad.AABBFloat.Width <= 1))
                    {
                        return false;
                    }
                }

                return true;
            });
            AddStep("detach legacy gameplay root", () => legacyHost.Expire());
            AddUntilStep("legacy gameplay root detached", () => legacyHost.Parent == null);
        }

        [Test]
        public void TestDualStageJudgementsStayCentredInsideTheirExactGroups()
        {
            Drawable argonHost = null!;
            DrawableManiaRuleset argonRuleset = null!;
            Note firstStageNote = null!;
            Note secondStageNote = null!;
            DrawableNote firstStageDrawable = null!;
            DrawableNote secondStageDrawable = null!;

            AddStep("create exact Argon 4+5 judgement root", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.Stages.Add(new StageDefinition(5));

                firstStageNote = new Note { Column = 0, StartTime = Time.Current + 5000 };
                secondStageNote = new Note { Column = 8, StartTime = Time.Current + 5000 };

                foreach (Note note in new[] { firstStageNote, secondStageNote })
                {
                    note.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());
                    beatmap.HitObjects.Add(note);
                }

                argonRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Add(argonHost = new RulesetSkinProvidingContainer(ruleset, beatmap, new ArgonSkin(this), prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = argonRuleset,
                });
            });

            AddUntilStep("both stage notes loaded", () => argonRuleset.IsLoaded
                                                           && (firstStageDrawable = this.ChildrenOfType<DrawableNote>()
                                                                                     .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, firstStageNote))!) != null
                                                           && (secondStageDrawable = this.ChildrenOfType<DrawableNote>()
                                                                                      .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, secondStageNote))!) != null);
            AddStep("publish one judgement in each stage", () =>
            {
                argonRuleset.Playfield.Stages[0].OnNewResult(firstStageDrawable,
                    new JudgementResult(firstStageDrawable.HitObject, new ManiaJudgement()) { Type = HitResult.Perfect });
                argonRuleset.Playfield.Stages[1].OnNewResult(secondStageDrawable,
                    new JudgementResult(secondStageDrawable.HitObject, new ManiaJudgement()) { Type = HitResult.Perfect });
            });
            AddUntilStep("both stage judgements loaded", () => argonRuleset.Playfield.Stages.All(stage =>
                stage.ChildrenOfType<ArgonJudgementPiece>().Count(piece => piece.IsLoaded) == 1));
            AddAssert("each judgement uses the same snapshot and its own group centre", () =>
            {
                foreach (Stage stage in argonRuleset.Playfield.Stages)
                {
                    ArgonJudgementPiece piece = stage.ChildrenOfType<ArgonJudgementPiece>().Single();
                    var stageBounds = stage.ScreenSpaceDrawQuad.AABBFloat;
                    float pieceCentreX = piece.ScreenSpaceDrawQuad.Centre.X;

                    if (!ReferenceEquals(piece.LayoutSnapshot, argonRuleset.LayoutSnapshot)
                        || piece.RelativePositionAxes != Axes.Both
                        || System.Math.Abs(piece.X - 0.5f) > 0.001f
                        || System.Math.Abs(pieceCentreX - stageBounds.Centre.X) > 0.5f
                        || pieceCentreX < stageBounds.Left
                        || pieceCentreX > stageBounds.Right)
                        return false;
                }

                return true;
            });
            AddStep("detach dual judgement root", () => argonHost.Expire());
            AddUntilStep("dual judgement root detached", () => argonHost.Parent == null);
        }

        [TestCase(4, 5)]
        [TestCase(5, 4)]
        public void TestArgonDualStageUsesStageLocalTopologyForSpecialRoleAndWidth(int firstStageColumns, int secondStageColumns)
        {
            Drawable argonHost = null!;
            DrawableManiaRuleset argonRuleset = null!;

            AddStep($"create exact Argon {firstStageColumns}+{secondStageColumns} root", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(firstStageColumns))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.Stages.Add(new StageDefinition(secondStageColumns));

                argonRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Add(argonHost = new RulesetSkinProvidingContainer(ruleset, beatmap, new ArgonSkin(this), prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = argonRuleset,
                });
            });

            AddUntilStep("all Argon stages and columns loaded", () => argonRuleset.IsLoaded
                                                                            && argonRuleset.Playfield.Stages.All(stage => stage.IsLoaded
                                                                                && stage.Columns.All(column => column.IsLoaded)));
            AddAssert("native vector and exact publication retained", () =>
                argonRuleset.LayoutSnapshot.Context.NativeContextId == $"stages-{firstStageColumns}-{secondStageColumns}"
                && argonRuleset.LayoutSnapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility);
            AddAssert("stage-local role lane id and width agree with topology", () =>
            {
                int globalIndex = 0;

                for (int stageIndex = 0; stageIndex < argonRuleset.Playfield.Stages.Count; stageIndex++)
                {
                    Stage stage = argonRuleset.Playfield.Stages[stageIndex];
                    GameplaySkinLaneTopologyGroup topologyGroup = argonRuleset.LayoutSnapshot.Context.Topology.GroupsInLogicalOrder[stageIndex];
                    float normalWidth = topologyGroup.LanesInLogicalOrder
                                                     .Where(lane => lane.Identity.Role == GameplaySkinLaneRole.Key)
                                                     .Select(lane => argonRuleset.LayoutSnapshot.GetLane(lane.Identity.Id).Rect.Width)
                                                     .First();

                    for (int localIndex = 0; localIndex < stage.Columns.Length; localIndex++, globalIndex++)
                    {
                        Column column = stage.Columns[localIndex];
                        GameplaySkinLaneTopologyEntry topologyLane = topologyGroup.LanesInLogicalOrder[localIndex];
                        GameplaySkinLayoutLane layoutLane = argonRuleset.LayoutSnapshot.GetLane(topologyLane.Identity.Id);
                        bool expectedSpecial = stage.Definition.IsSpecialColumn(localIndex);
                        float expectedWidth = normalWidth * (expectedSpecial ? 2 : 1);

                        if (!ReferenceEquals(column.LayoutSnapshot, argonRuleset.LayoutSnapshot)
                            || topologyLane.GlobalLogicalIndex != globalIndex
                            || topologyLane.GroupLocalLogicalIndex != localIndex
                            || topologyLane.Identity.Id.Value != $"mania.lane.column-{globalIndex + 1}"
                            || topologyLane.Identity.Role != (expectedSpecial ? GameplaySkinLaneRole.SpecialKey : GameplaySkinLaneRole.Key)
                            || column.IsSpecial != expectedSpecial
                            || !column.LayoutLaneId.Equals(topologyLane.Identity.Id)
                            || System.Math.Abs(layoutLane.Rect.Width - expectedWidth) > 0.001f)
                            return false;
                    }
                }

                GameplaySkinLaneTopologyGroup secondGroup = argonRuleset.LayoutSnapshot.Context.Topology.GroupsInLogicalOrder[1];
                return secondGroup.LanesInLogicalOrder.All(lane => lane.GroupLocalLogicalIndex >= 0
                                                                   && lane.GroupLocalLogicalIndex < secondStageColumns)
                       && (secondStageColumns % 2 == 0
                           ? secondGroup.LanesInLogicalOrder.All(lane => lane.Identity.Role == GameplaySkinLaneRole.Key)
                           : secondGroup.LanesInLogicalOrder[secondStageColumns / 2].Identity.Role == GameplaySkinLaneRole.SpecialKey);
            });
            AddStep("detach Argon dual-stage root", () => argonHost.Expire());
            AddUntilStep("Argon dual-stage root detached", () => argonHost.Parent == null);
        }

        [TestCase(4, 5)]
        [TestCase(5, 4)]
        public void TestOmsDualStageUsesStageLocalFallbackAssetsAndLastColumn(int firstStageColumns, int secondStageColumns)
        {
            Drawable omsHost = null!;
            DrawableManiaRuleset omsRuleset = null!;

            AddStep($"create exact OMS {firstStageColumns}+{secondStageColumns} root", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(firstStageColumns))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.Stages.Add(new StageDefinition(secondStageColumns));

                omsRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                Add(omsHost = new RulesetSkinProvidingContainer(ruleset, beatmap, null, prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = omsRuleset,
                });
            });

            AddUntilStep("all OMS column surfaces loaded", () => omsRuleset.IsLoaded
                                                                       && omsRuleset.Playfield.Stages.All(stage => stage.Columns.All(column =>
                                                                           column.ChildrenOfType<OmsKeyArea>().Any(key => key.IsLoaded)
                                                                           && column.ChildrenOfType<OmsColumnBackground>().Any(background => background.IsLoaded))));
            AddAssert("OMS fallback and edge identity are stage local", () =>
            {
                for (int stageIndex = 0; stageIndex < omsRuleset.Playfield.Stages.Count; stageIndex++)
                {
                    Stage stage = omsRuleset.Playfield.Stages[stageIndex];
                    GameplaySkinLaneTopologyGroup topologyGroup = omsRuleset.LayoutSnapshot.Context.Topology.GroupsInLogicalOrder[stageIndex];

                    for (int localIndex = 0; localIndex < stage.Columns.Length; localIndex++)
                    {
                        Column column = stage.Columns[localIndex];
                        GameplaySkinLaneTopologyEntry topologyLane = topologyGroup.LanesInLogicalOrder[localIndex];
                        OmsKeyArea keyArea = column.ChildrenOfType<OmsKeyArea>().Single();
                        OmsColumnBackground background = column.ChildrenOfType<OmsColumnBackground>().Single();
                        string expectedFallback = topologyLane.Identity.Role == GameplaySkinLaneRole.SpecialKey
                            ? "S"
                            : System.Math.Min(localIndex, stage.Columns.Length - 1 - localIndex) % 2 == 0 ? "1" : "2";

                        if (!ReferenceEquals(column.LayoutSnapshot, omsRuleset.LayoutSnapshot)
                            || topologyLane.GroupLocalLogicalIndex != localIndex
                            || !column.LayoutLaneId.Equals(topologyLane.Identity.Id)
                            || keyArea.ResolvedFallbackColumnIndex != expectedFallback
                            || background.ResolvedFallbackColumnIndex != expectedFallback
                            || background.IsStageLastColumn != (localIndex == stage.Columns.Length - 1))
                            return false;
                    }
                }

                return omsRuleset.LayoutSnapshot.Context.NativeContextId == $"stages-{firstStageColumns}-{secondStageColumns}"
                       && omsRuleset.LayoutSnapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility;
            });
            AddStep("detach OMS dual-stage root", () => omsHost.Expire());
            AddUntilStep("OMS dual-stage root detached", () => omsHost.Parent == null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestGameplayModRetargetsObjectsWithoutChangingFixedLaneIdentity(bool random)
        {
            string[] baselineLaneIds = null!;
            ManiaHitObject[] objects = null!;
            int[] originalColumns = null!;

            AddStep($"create exact {(random ? "random" : "mirror")} gameplay root", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.Stages.Add(new StageDefinition(5));

                objects = Enumerable.Range(0, beatmap.TotalColumns)
                                    .Select(column => (ManiaHitObject)new Note
                                    {
                                        Column = column,
                                        StartTime = Time.Current + 5000 + column * 10,
                                    }).ToArray();
                originalColumns = objects.Select(hitObject => hitObject.Column).ToArray();

                foreach (ManiaHitObject hitObject in objects)
                {
                    hitObject.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());
                    beatmap.HitObjects.Add(hitObject);
                }

                baselineLaneIds = ManiaGameplaySkinLaneTopologyFactory.Create(beatmap).LanesInLogicalOrder
                                                                         .Select(lane => lane.Identity.Id.Value)
                                                                         .ToArray();

                Mod mod;

                if (random)
                {
                    var randomMod = new ManiaModRandom();
                    randomMod.Seed.Value = 1337;
                    randomMod.ApplyToBeatmap(beatmap);
                    mod = randomMod;
                }
                else
                {
                    var mirrorMod = new ManiaModMirror();
                    mirrorMod.ApplyToBeatmap(beatmap);
                    mod = mirrorMod;
                }

                drawableRuleset = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap, new[] { mod });
                Add(host = new RulesetSkinProvidingContainer(ruleset, beatmap, null, prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = drawableRuleset,
                });
            });

            AddUntilStep("modded production objects loaded", () => drawableRuleset.IsLoaded
                                                                    && this.ChildrenOfType<DrawableNote>().Count(note => objects.Contains(note.HitObject)) == objects.Length);
            AddAssert("mod changes at least one object target", () => objects.Where((hitObject, index) => hitObject.Column != originalColumns[index]).Any());
            AddAssert("fixed topology lane ids survive mod", () =>
                drawableRuleset.LayoutSnapshot.Context.Topology.LanesInLogicalOrder
                               .Select(lane => lane.Identity.Id.Value)
                               .SequenceEqual(baselineLaneIds));
            AddAssert("every modded object skin target is its real column lane id", () =>
                this.ChildrenOfType<DrawableNote>()
                    .Where(note => objects.Contains(note.HitObject))
                    .All(note =>
                    {
                        Column targetColumn = drawableRuleset.Playfield.GetColumn(note.HitObject.Column);
                        return ReferenceEquals(note.LayoutSnapshot, drawableRuleset.LayoutSnapshot)
                               && ReferenceEquals(targetColumn.LayoutSnapshot, drawableRuleset.LayoutSnapshot)
                               && note.LayoutLaneId.Equals(targetColumn.LayoutLaneId)
                               && drawableRuleset.LayoutSnapshot.GetLane(note.LayoutLaneId).TopologyEntry.GlobalLogicalIndex == note.HitObject.Column;
                    }));
            AddStep("detach modded gameplay root", () => host.Expire());
            AddUntilStep("modded gameplay root detached", () => host.Parent == null);
        }

        private partial class HudDependenciesContainer : Container
        {
            private readonly IScrollingInfo scrollingInfo;
            private readonly ScoreProcessor scoreProcessor;

            public HudDependenciesContainer(IScrollingInfo scrollingInfo, ScoreProcessor scoreProcessor)
            {
                this.scrollingInfo = scrollingInfo;
                this.scoreProcessor = scoreProcessor;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(scrollingInfo);
                dependencies.CacheAs(scoreProcessor);
                return dependencies;
            }
        }

        private partial class ExactDependencyProbeContainer : Container
        {
            public IReadOnlyDependencyContainer CapturedDependencies { get; private set; } = null!;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
                => CapturedDependencies = base.CreateChildDependencies(parent);
        }

        private static bool isFinitePositiveAndContained(GameplaySkinLayoutRect bounds, GameplaySkinLayoutRect rect)
            => float.IsFinite(rect.X)
               && float.IsFinite(rect.Y)
               && float.IsFinite(rect.Width)
               && float.IsFinite(rect.Height)
               && rect.Width > 0
               && rect.Height > 0
               && bounds.Contains(rect);

        private sealed class InvalidGeometrySkin : ISkin
        {
            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
            {
                if (lookup is not ManiaSkinConfigurationLookup maniaLookup || typeof(TValue) != typeof(float))
                    return null;

                float? value = maniaLookup.Lookup switch
                {
                    LegacyManiaSkinConfigurationLookups.ColumnWidth when maniaLookup.ColumnIndex == 0 => float.NaN,
                    LegacyManiaSkinConfigurationLookups.HitPosition => float.PositiveInfinity,
                    LegacyManiaSkinConfigurationLookups.StagePaddingTop => -1,
                    LegacyManiaSkinConfigurationLookups.BarLineHeight => float.NegativeInfinity,
                    LegacyManiaSkinConfigurationLookups.ComboPosition => float.NaN,
                    _ => null,
                };

                return value.HasValue ? (IBindable<TValue>)(object)new Bindable<float>(value.Value) : null;
            }
        }

        public IRenderer Renderer => gameHost.Renderer;
        public AudioManager AudioManager => Audio;
        public IResourceStore<byte[]> Files => null!;
        public new IResourceStore<byte[]> Resources => base.Resources;
        public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => gameHost.CreateTextureLoaderStore(underlyingStore);
        RealmAccess IStorageResourceProvider.RealmAccess => null!;
    }
}
