// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Argon;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.Skinning.Oms;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.ClicksPerSecond;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Triangles;
using osu.Game.Tests.Visual;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
        private SkinManager? publicMaterialSkinManager;
        private readonly HashSet<string> publicMaterialExternalRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected override bool UseFreshStoragePerRun => true;

        [TearDownSteps]
        public void TearDownPublicMaterialFixture()
        {
            AddStep("shutdown public-material skin manager", () =>
            {
                publicMaterialSkinManager?.ShutdownManagedFolderMutations();
                publicMaterialSkinManager = null;
                deletePublicMaterialExternalRoots();
            });
        }

        [TearDown]
        public void CleanUpPublicMaterialExternalRoots() => deletePublicMaterialExternalRoots();

        [TestCase(ManiaPublicMaterialPackageSource.OrdinaryRealm)]
        [TestCase(ManiaPublicMaterialPackageSource.ManagedFolder)]
        [TestCase(ManiaPublicMaterialPackageSource.ExternalFolder)]
        public void TestPublicCommonMaterialDrivesExactProductionNoteHoldAndKeyFromCurrentRevision(ManiaPublicMaterialPackageSource source)
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            string packageRoot = string.Empty;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            SkinCurrentRevision revision = null!;
            GameplaySkinEventSubscription eventSubscription = null!;
            var observedEvents = new List<GameplaySkinEventEnvelope>();
            long noteObjectId = -1;
            long holdObjectId = -1;
            long barLineObjectId = -1;
            long completedEpoch = -1;
            SkinCurrentRevisionSourceKind expectedRevisionSource = source switch
            {
                ManiaPublicMaterialPackageSource.OrdinaryRealm => SkinCurrentRevisionSourceKind.RealmPackage,
                ManiaPublicMaterialPackageSource.ManagedFolder => SkinCurrentRevisionSourceKind.ManagedFolder,
                ManiaPublicMaterialPackageSource.ExternalFolder => SkinCurrentRevisionSourceKind.ExternalFolder,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
            };
            GameplaySkinPackageSourceKind expectedPackageSource = source switch
            {
                ManiaPublicMaterialPackageSource.OrdinaryRealm => GameplaySkinPackageSourceKind.RealmPackage,
                ManiaPublicMaterialPackageSource.ManagedFolder => GameplaySkinPackageSourceKind.ManagedFolder,
                ManiaPublicMaterialPackageSource.ExternalFolder => GameplaySkinPackageSourceKind.ExternalFolder,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
            };

            AddStep("create isolated public-material skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });

            switch (source)
            {
                case ManiaPublicMaterialPackageSource.OrdinaryRealm:
                    AddStep("create and select ordinary public-material package", () =>
                    {
                        packageRoot = LocalStorage.GetFullPath($"realm-mania-public-material-{Guid.NewGuid():N}");
                        writePublicManiaMaterialPackage(packageRoot);
                        candidate = createPublicMaterialRealmCandidate(packageRoot);
                        publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                case ManiaPublicMaterialPackageSource.ManagedFolder:
                    AddStep("create and select managed public-material package", () =>
                    {
                        (packageRoot, candidate) = createPublicMaterialManagedCandidate();
                        publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                case ManiaPublicMaterialPackageSource.ExternalFolder:
                    AddStep("create and register external public-material package", () =>
                    {
                        packageRoot = createPublicMaterialExternalPackage();
                        registrationTask = publicMaterialSkinManager!.RegisterExternalFolderAsync(packageRoot);
                    });
                    AddUntilStep("wait for external public-material registration", () => registrationTask?.IsCompleted == true);
                    AddStep("query external public-material package", () =>
                    {
                        Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                        dropdownTask = publicMaterialSkinManager!.GetAllUsableSkinsAsync();
                    });
                    AddUntilStep("wait for external public-material candidate", () => dropdownTask?.IsCompleted == true);
                    AddStep("select external public-material package", () =>
                    {
                        candidate = dropdownTask!.GetAwaiter().GetResult()
                                                 .Single(record => record.PerformRead(info =>
                                                     info.IsExternalFilesystemStorage
                                                     && string.Equals(info.FilesystemStoragePath, packageRoot, StringComparison.OrdinalIgnoreCase)));
                        publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }

            AddUntilStep("wait for exact public-material current revision", () =>
                candidate != null
                && publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && publicMaterialSkinManager.CurrentRevision.SourceKind == expectedRevisionSource
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount exact mania production renderer", () =>
            {
                revision = publicMaterialSkinManager!.CurrentRevision;
                Add(renderer = new CurrentRevisionManiaMaterialHost(publicMaterialSkinManager, mods: new Mod[] { new ManiaModHidden() }));
            });
            AddUntilStep("wait for exact mania provider", () => renderer.Provider.IsLoaded);
            AddUntilStep("wait for exact mania ruleset", () => renderer.Drawable.IsLoaded);
            AddUntilStep("wait for exact mania stage", () => renderer.Drawable.Playfield.Stages.Single().LoadState >= LoadState.Ready);
            AddUntilStep("wait for exact mania first column", () => renderer.FirstColumn.LoadState >= LoadState.Ready);
            AddUntilStep("wait for exact mania prepared key surface", () => renderer.SurfaceReady);
            AddStep("capture production mania scene host", () =>
                sceneHost = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for bounded mania scene creation", () => sceneHost.PendingCreationCount == 0);
            AddStep("mount real mania HUD owner", () => renderer.AddProductionHud());
            AddUntilStep("wait for real mania HUD owner", () => renderer.Hud?.IsLoaded == true);
            AddStep("mount real core gameplay HUD owners", () => renderer.AddProductionCoreHud());
            AddUntilStep("wait for exact core gameplay HUD registrations", () =>
                renderer.CoreHud?.IsLoaded == true
                && renderer.CoreHud.GameplaySkinGaugeOwners.Count > 0
                && hasRequiredTextHudOwners(renderer.CoreHud));
            AddStep("attach read-only mania event consumer", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinEventStream stream = renderer.Drawable.GameplaySkinEventStream;
                eventSubscription = stream.Subscribe();
                eventSubscription.DrainFrame(observedEvents.Add);
                GameplaySkinStateEventPayload snapshotPayload = (GameplaySkinStateEventPayload)observedEvents.Single().Payload;

                Assert.Multiple(() =>
                {
                    Assert.That(renderer.Drawable.GameplaySkinEventRuntime.Publication, Is.SameAs(publication));
                    Assert.That(getPrivateField<ScoreProcessor>(renderer.Drawable.GameplaySkinEventRuntime, "scoreProcessor"),
                        Is.SameAs(renderer.ScoreProcessor));
                    Assert.That(getPrivateField<HealthProcessor>(renderer.Drawable.GameplaySkinEventRuntime, "healthProcessor"),
                        Is.SameAs(renderer.HealthProcessor));
                    Assert.That(stream.CurrentRevision, Is.EqualTo(publication.EventRevision));
                    Assert.That(observedEvents, Has.Count.EqualTo(1));
                    Assert.That(observedEvents[0].DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                    Assert.That(observedEvents[0].Revision, Is.EqualTo(publication.EventRevision));
                    Assert.That(publication.Snapshot.BgaViewports, Is.Empty,
                        "Mania must not invent a C3 BGA content viewport.");
                    Assert.That(snapshotPayload.State.BgaViewports, Is.Empty,
                        "The Mania event stream must not manufacture idle BGA content for a runtime-not-applicable slot.");
                });
            });
            AddStep("add real mania note and hold", () => renderer.AddProductionObjects(1_100));
            AddUntilStep("wait for real mania pooled objects", () => renderer.ObjectsReady);
            AddUntilStep("wait for exact mania note hold and key", () => renderer.Ready);
            AddStep("capture sole engine-owned scene object identities", () =>
            {
                noteObjectId = renderer.NoteDrawable!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single().BoundObjectId ?? -1;
                holdObjectId = renderer.HoldDrawable!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                                       .Single(visual => visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteBody).BoundObjectId ?? -1;
                barLineObjectId = renderer.BarLineDrawable!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single().BoundObjectId ?? -1;

                Assert.That(new[] { noteObjectId, holdObjectId, barLineObjectId }, Is.All.GreaterThanOrEqualTo(0));
                Assert.That(new[] { noteObjectId, holdObjectId, barLineObjectId }.Distinct().Count(), Is.EqualTo(3));
            });
            AddStep("assert real pooled note owns its ready specialised scene", () => Assert.Multiple(() =>
            {
                DrawableNote note = renderer.NoteDrawable!;
                GameplaySkinSpecialisedSceneVisual visual = getPrivateField<GameplaySkinSpecialisedSceneVisual>(note, "specialisedSceneVisual");
                SkinnableDrawable wrapper = getPrivateField<SkinnableDrawable>(note, "headPiece");
                Assert.That(((PoolableDrawable)note).IsInPool, Is.True);
                Assert.That(isDescendantOf(note, renderer.FirstColumn.HitObjectContainer), Is.True);
                Assert.That(note.SceneVisualGate.IsReplacementReady, Is.True);
                Assert.That(note.AppliedSceneNodeIds, Is.EqualTo(new[] { "node.note" }));
                Assert.That(visual.IsApplied, Is.True);
                Assert.That(visual.BoundObjectId, Is.EqualTo(noteObjectId),
                    "A real pooled note must bind its author scene to the root producer's sole stable object ID.");
                Assert.That(visual.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null), Is.True);
                Assert.That(wrapper.Alpha, Is.Zero);
            }));
            AddStep("press the real mania lane", () =>
            {
                ManiaAction action = renderer.FirstColumn.Action.Value;
                ManiaInputManager[] inputManagers = renderer.Drawable.ChildrenOfType<ManiaInputManager>().ToArray();
                Assert.That(inputManagers, Is.Not.Empty);

                foreach (ManiaInputManager inputManager in inputManagers)
                    inputManager.KeyBindingContainer.TriggerPressed(action);
            });
            AddUntilStep("wait for real mania judgement producer edge", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.JudgementApplied);
            });
            AddStep("release the real mania lane", () =>
            {
                ManiaAction action = renderer.FirstColumn.Action.Value;

                foreach (ManiaInputManager inputManager in renderer.Drawable.ChildrenOfType<ManiaInputManager>())
                    inputManager.KeyBindingContainer.TriggerReleased(action);
            });
            AddStep("assert shared document declarations reached one exact production material set", () =>
            {
                GameplaySkinDocument document = revision.Owner.GameplaySkinDocument;
                GameplaySkinDocumentEntry[] declarations = document.Sections
                                                                     .Where(section => section.Family == GameplaySkinSlotCatalogFamily.Common
                                                                                       && section.Version == GameplaySkinSlotCatalog.COMMON_VERSION)
                                                                     .SelectMany(section => section.Entries)
                                                                     .ToArray();
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinResolvedMaterialSet materialSet = publication.MaterialSet;
                GameplaySkinLaneTopologyGroup group = publication.Snapshot.Context.Topology.GroupsInLogicalOrder.Single();
                GameplaySkinLaneTopologyEntry lane = group.LanesInLogicalOrder[0];
                GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);
                GameplaySkinSlotDescriptor[] slots =
                {
                    GameplaySkinSlotCatalog.Note,
                    GameplaySkinSlotCatalog.LongNoteHead,
                    GameplaySkinSlotCatalog.LongNoteBody,
                    GameplaySkinSlotCatalog.LongNoteTail,
                    GameplaySkinSlotCatalog.KeyVisual,
                };
                GameplaySkinResolvedMaterialEntry[] entries = slots.Select(slot =>
                {
                    Assert.That(materialSet.TryGet(new GameplaySkinResolvedMaterialKey(slot, target), out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    return entry!;
                }).ToArray();
                var laneSurfaceKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneSurface, target);
                var keyFlashKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyFlash, target);
                GameplaySkinLaneTopologyEntry suppressedLane = group.LanesInLogicalOrder[1];
                var suppressedKeyVisualKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.KeyVisual,
                    GameplaySkinResolvedMaterialTarget.ForLane(group, suppressedLane));
                var suppressedKeyFlashKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.KeyFlash,
                    GameplaySkinResolvedMaterialTarget.ForLane(group, suppressedLane));
                var barLineKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.BarLine,
                    GameplaySkinResolvedMaterialTarget.ForGroup(group));
                Assert.That(materialSet.TryGet(laneSurfaceKey, out GameplaySkinResolvedMaterialEntry? laneSurfaceEntry), Is.True);
                Assert.That(materialSet.TryGet(keyFlashKey, out GameplaySkinResolvedMaterialEntry? keyFlashEntry), Is.True);
                Assert.That(materialSet.TryGet(suppressedKeyVisualKey, out GameplaySkinResolvedMaterialEntry? suppressedKeyVisualEntry), Is.True);
                Assert.That(materialSet.TryGet(suppressedKeyFlashKey, out GameplaySkinResolvedMaterialEntry? suppressedKeyFlashEntry), Is.True);
                Assert.That(materialSet.TryGet(barLineKey, out GameplaySkinResolvedMaterialEntry? barLineEntry), Is.True);
                DrawableNote note = renderer.NoteDrawable!;
                DrawableHoldNote hold = renderer.HoldDrawable!;
                DrawableBarLine barLine = renderer.BarLineDrawable!;
                Column column = renderer.FirstColumn;
                Stage stage = renderer.Drawable.Playfield.Stages.Single();
                Column suppressedColumn = stage.Columns[1];
                SkinnableDrawable noteWrapper = getPrivateField<SkinnableDrawable>(note, "headPiece");
                SkinnableDrawable headWrapper = getPrivateField<SkinnableDrawable>(hold.Head, "headPiece");
                SkinnableDrawable bodyWrapper = getPrivateField<SkinnableDrawable>(hold, "bodyPiece");
                SkinnableDrawable tailWrapper = getPrivateField<SkinnableDrawable>(hold.Tail, "headPiece");
                SkinnableDrawable keyWrapper = getPrivateField<SkinnableDrawable>(column, "keyArea");
                LegacyNotePiece notePiece = (LegacyNotePiece)noteWrapper.Drawable;
                LegacyHoldNoteHeadPiece headPiece = (LegacyHoldNoteHeadPiece)headWrapper.Drawable;
                LegacyBodyPiece bodyPiece = (LegacyBodyPiece)bodyWrapper.Drawable;
                LegacyHoldNoteTailPiece tailPiece = (LegacyHoldNoteTailPiece)tailWrapper.Drawable;
                LegacyKeyArea keyArea = (LegacyKeyArea)keyWrapper.Drawable;
                SkinnableDrawable suppressedKeyWrapper = getPrivateField<SkinnableDrawable>(suppressedColumn, "keyArea");
                SkinnableDrawable barLineWrapper = getPrivateField<SkinnableDrawable>(barLine, "programmaticVisual");
                SkinnableDrawable stageBackgroundWrapper = getPrivateField<SkinnableDrawable>(stage, "stageBackground");
                SkinnableDrawable stageForegroundWrapper = getPrivateField<SkinnableDrawable>(stage, "stageForeground");
                Drawable judgementWrapper = getPrivateField<Drawable>(stage, "judgements");
                IManiaGameplaySkinProgrammaticVisualPartProvider firstBackgroundParts =
                    (IManiaGameplaySkinProgrammaticVisualPartProvider)getPrivateField<SkinnableDrawable>(column, "columnBackground").Drawable;
                IManiaGameplaySkinProgrammaticVisualPartProvider secondBackgroundParts =
                    (IManiaGameplaySkinProgrammaticVisualPartProvider)getPrivateField<SkinnableDrawable>(suppressedColumn, "columnBackground").Drawable;
                IManiaGameplaySkinProgrammaticVisualPartProvider firstTargetParts =
                    (IManiaGameplaySkinProgrammaticVisualPartProvider)column.HitObjectArea.HitTarget.Drawable;
                IManiaGameplaySkinProgrammaticVisualPartProvider secondTargetParts =
                    (IManiaGameplaySkinProgrammaticVisualPartProvider)suppressedColumn.HitObjectArea.HitTarget.Drawable;
                Drawable[] stageShellOwners =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)stageBackgroundWrapper.Drawable)
                    .GameplaySkinProgrammaticVisualParts
                    .Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.StageBackground))
                    .Select(part => part.Owner)
                    .ToArray();
                Drawable[] playfieldBackdropOwners =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)stageBackgroundWrapper.Drawable)
                    .GameplaySkinProgrammaticVisualParts
                    .Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop))
                    .Select(part => part.Owner)
                    .ToArray();
                Drawable firstLaneSurfaceOwner = firstBackgroundParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneSurface)).Owner;
                Drawable secondLaneSurfaceOwner = secondBackgroundParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneSurface)).Owner;
                Drawable firstLaneDividerOwner = firstBackgroundParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneDivider)).Owner;
                Drawable secondLaneDividerOwner = secondBackgroundParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneDivider)).Owner;
                Drawable firstHitTargetOwner = firstTargetParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.HitTarget)).Owner;
                Drawable secondHitTargetOwner = secondTargetParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.HitTarget)).Owner;
                Drawable[] judgementLineOwners = stage.Columns.Select(stageColumn =>
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)stageColumn.HitObjectArea.HitTarget.Drawable)
                    .GameplaySkinProgrammaticVisualParts.Single(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.JudgementLine)).Owner).ToArray();
                Drawable firstKeyFlashOwner = firstTargetParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.KeyFlash)).Owner;
                Drawable secondKeyFlashOwner = secondTargetParts.GameplaySkinProgrammaticVisualParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.KeyFlash)).Owner;
                Drawable[] comboWrappers = renderer.Hud!.Children
                                                   .Where(child => child is OmsManiaComboCounter or LegacyManiaComboCounter)
                                                   .ToArray();
                ManiaGameplaySkinNoteMaterial noteMaterial = entries[0].GetMaterial<ManiaGameplaySkinNoteMaterial>();
                ManiaGameplaySkinNoteMaterial headMaterial = entries[1].GetMaterial<ManiaGameplaySkinNoteMaterial>();
                ManiaGameplaySkinBodyMaterial bodyMaterial = entries[2].GetMaterial<ManiaGameplaySkinBodyMaterial>();
                ManiaGameplaySkinNoteMaterial tailMaterial = entries[3].GetMaterial<ManiaGameplaySkinNoteMaterial>();
                ManiaGameplaySkinKeyMaterial keyMaterial = entries[4].GetMaterial<ManiaGameplaySkinKeyMaterial>();
                GameplaySkinPreparedScene preparedScene = publication.PreparedScene;
                GameplaySkinSceneRuntimeLayers sceneLayers = sceneHost.Layers;
                GameplaySkinEventEnvelope judgementEdge = observedEvents.Last(envelope =>
                    envelope.EventKind == GameplaySkinEventKind.JudgementApplied
                    && envelope.LaneId?.Value == "mania.lane.column-1");
                GameplaySkinResolvedMaterialEntry authoredGlobalTextHud = materialSet.Entries.Single(candidateEntry =>
                    ReferenceEquals(candidateEntry.Slot, GameplaySkinSlotCatalog.TextHud)
                    && candidateEntry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global);
                GameplaySkinResolvedMaterialEntry[] authoredEntries = materialSet.Entries
                    .Where(candidateEntry => candidateEntry.State == GameplaySkinResolvedMaterialState.Provide
                                             && candidateEntry.Source.Kind == GameplaySkinResolvedMaterialSourceKind.SelectedPackage)
                    .ToArray();
                var specialisedSlots = new HashSet<GameplaySkinSlotDescriptor>
                {
                    GameplaySkinSlotCatalog.Note,
                    GameplaySkinSlotCatalog.LongNoteHead,
                    GameplaySkinSlotCatalog.LongNoteBody,
                    GameplaySkinSlotCatalog.LongNoteTail,
                    GameplaySkinSlotCatalog.KeyVisual,
                    GameplaySkinSlotCatalog.BarLine,
                    GameplaySkinSlotCatalog.HitExplosion,
                    GameplaySkinSlotCatalog.LaneCoverFill,
                    GameplaySkinSlotCatalog.LaneCoverDecoration,
                };
                GameplaySkinSceneHostedSlot[] specialisedGates = entries.Select(candidateEntry =>
                {
                    Assert.That(sceneHost.TryGetVisualGate(candidateEntry.Key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    return gate!;
                }).ToArray();
                Assert.That(sceneHost.TryGetVisualGate(laneSurfaceKey, out GameplaySkinSceneHostedSlot? laneSurfaceGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(keyFlashKey, out GameplaySkinSceneHostedSlot? keyFlashGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(suppressedKeyVisualKey, out GameplaySkinSceneHostedSlot? suppressedKeyVisualGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(suppressedKeyFlashKey, out GameplaySkinSceneHostedSlot? suppressedKeyFlashGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(barLineKey, out GameplaySkinSceneHostedSlot? barLineGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(authoredGlobalTextHud.Key, out GameplaySkinSceneHostedSlot? globalTextGate), Is.True);
                Assert.That(sceneHost.TryGetHostedDrawable(keyFlashKey, out Drawable? keyFlashDrawable), Is.True);
                GameplaySkinSlotDescriptor[] supportedSlots = ManiaGameplaySkinMaterialResolver.RuntimeCapabilities.Support.Values
                    .Select(support => support.Descriptor)
                    .ToArray();
                GameplaySkinSlotDescriptor[] notApplicableSlots =
                {
                    GameplaySkinSlotCatalog.Mine,
                    GameplaySkinSlotCatalog.BgaViewport,
                    GameplaySkinSlotCatalog.BgaFrame,
                    GameplaySkinSlotCatalog.Turntable,
                    GameplaySkinSlotCatalog.Laser,
                };
                GameplaySkinSceneHostedSlot[] authoredGates = authoredEntries.Select(candidateEntry =>
                {
                    Assert.That(sceneHost.TryGetVisualGate(candidateEntry.Key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    return gate!;
                }).ToArray();
                GameplaySkinSpecialisedSceneVisual noteSceneVisual = note.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(candidateVisual => candidateVisual.Key.Equals(entries[0].Key));
                GameplaySkinSpecialisedSceneVisual headSceneVisual = hold.Head.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(candidateVisual => candidateVisual.Key.Equals(entries[1].Key));
                GameplaySkinSpecialisedSceneVisual bodySceneVisual = hold.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(candidateVisual => candidateVisual.Key.Equals(entries[2].Key));
                GameplaySkinSpecialisedSceneVisual tailSceneVisual = hold.Tail.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(candidateVisual => candidateVisual.Key.Equals(entries[3].Key));
                GameplaySkinSpecialisedSceneVisual barLineSceneVisual = barLine.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(candidateVisual => candidateVisual.Key.Equals(barLineKey));
                long[] spawnedObjectIds = observedEvents
                    .Where(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectSpawned)
                    .Select(envelope => ((GameplaySkinObjectEventPayload)envelope.Payload).State.ObjectId)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(document.HasFatalDiagnostics, Is.False);
                    Assert.That(declarations, Has.Length.EqualTo(26));
                    Assert.That(declarations.Where(candidateEntry => candidateEntry.Operation == GameplaySkinDocumentOperation.Provide)
                                            .Select(candidateEntry => candidateEntry.Descriptor)
                                            .Distinct(), Is.EquivalentTo(supportedSlots),
                        "The exact selected package must author a real Provide target for every supported Mania public slot.");
                    Assert.That(declarations.All(entry => entry.Presence == GameplaySkinDocumentDeclarationPresence.Declared
                                                          && entry.Validity == GameplaySkinDocumentValueValidity.Valid), Is.True);
                    Assert.That(declarations.Count(entry => entry.Operation == GameplaySkinDocumentOperation.Suppress), Is.EqualTo(2));
                    Assert.That(declarations.Where(entry => entry.Target.Kind == GameplaySkinDocumentTargetKind.Lane
                                                             && entry.Target.LaneId?.Value == "mania.lane.column-1")
                                            .All(entry => entry.Target.GroupId?.Value == "mania.group.stage-1"
                                                          && entry.Target.GroupLogicalIndex == 0
                                                          && entry.Target.GroupVisualIndex == 0
                                                          && entry.Target.GlobalLogicalIndex == 0
                                                          && entry.Target.GlobalVisualIndex == 0
                                                          && entry.Target.GroupLocalLogicalIndex == 0
                                                          && entry.Target.GroupLocalVisualIndex == 0), Is.True);
                    Assert.That(declarations.Where(entry => entry.Target.LaneId?.Value == "mania.lane.column-2")
                                            .All(entry => entry.Operation == GameplaySkinDocumentOperation.Suppress), Is.True);
                    Assert.That(declarations.Where(entry => entry.Target.Kind == GameplaySkinDocumentTargetKind.Stage)
                                            .All(entry => entry.Target.GroupId?.Value == "mania.group.stage-1"
                                                          && entry.Target.GroupLogicalIndex == 0
                                                          && entry.Target.GroupVisualIndex == 0), Is.True);
                    Assert.That(declarations.Single(entry => ReferenceEquals(entry.Descriptor, GameplaySkinSlotCatalog.BarLine)).Target.GroupId?.Value,
                        Is.EqualTo("mania.group.stage-1"));
                    Assert.That(publication.Snapshot, Is.SameAs(renderer.Drawable.LayoutSnapshot));
                    Assert.That(materialSet.Snapshot, Is.SameAs(publication.Snapshot));
                    Assert.That(materialSet.PackageRevision.SourceKind, Is.EqualTo(expectedPackageSource));
                    Assert.That(materialSet.PackageRevision.RecordId, Is.EqualTo(revision.RecordId));
                    Assert.That(materialSet.PackageRevision.ContentRevision, Is.EqualTo(revision.ContentRevision));
                    Assert.That(materialSet.PackageRevision.Generation, Is.EqualTo(revision.Generation));
                    Assert.That(materialSet.Entries, Has.Count.EqualTo(
                        supportedSlots.Sum(descriptor =>
                            GameplaySkinPublicSlotMaterialTargets.Enumerate(descriptor, publication.Snapshot).Count)));
                    Assert.That(materialSet.Entries.Select(entry => entry.Slot).Distinct(), Is.EquivalentTo(supportedSlots));
                    Assert.That(ManiaGameplaySkinMaterialResolver.RuntimeCapabilities.Support, Has.Count.EqualTo(23));
                    Assert.That(GameplaySkinRuntimeSupportProfile.Mania.Decisions, Has.Count.EqualTo(28));
                    Assert.That(notApplicableSlots.All(descriptor =>
                    {
                        Assert.That(GameplaySkinRuntimeSupportProfile.Mania.TryGetDecision(descriptor, out GameplaySkinRuntimeSupportDecision? decision), Is.True);
                        return decision!.Kind == GameplaySkinRuntimeSupportDecisionKind.NotApplicable;
                    }), Is.True);
                    Assert.That(notApplicableSlots.All(descriptor => !materialSet.Entries.Any(entry => ReferenceEquals(entry.Slot, descriptor))), Is.True,
                        "A Mania-not-applicable slot must not acquire a material fallback.");
                    Assert.That(notApplicableSlots.All(descriptor => !sceneHost.HostedSlots.Any(hosted => ReferenceEquals(hosted.Key.Slot, descriptor))), Is.True,
                        "A Mania-not-applicable slot must not acquire a mounted scene host.");
                    Assert.That(notApplicableSlots.All(descriptor => !sceneHost.RuntimeCapabilities.TryGet(descriptor, out _)), Is.True,
                        "A Mania-not-applicable slot must not be advertised by the production runtime.");
                    Assert.That(authoredEntries.Select(candidateEntry => candidateEntry.Slot).Distinct(),
                        Is.EquivalentTo(supportedSlots));
                    Assert.That(authoredGates.All(gate => gate.SuppressesProgrammaticVisual), Is.True);
                    Assert.That(authoredEntries.Where(candidateEntry => !specialisedSlots.Contains(candidateEntry.Slot))
                                               .All(candidateEntry => isHostedInMountedSceneLayer(sceneHost, candidateEntry)), Is.True,
                        "Every authored generic root/lane/effect/HUD key must own one drawable in a mounted shared layer.");
                    Assert.That(entries.All(entry => entry.State == GameplaySkinResolvedMaterialState.Provide
                                                     && entry.Source.Kind == GameplaySkinResolvedMaterialSourceKind.SelectedPackage
                                                     && entry.Source.StableId == "selected-common"
                                                     && entry.Source.ContentRevision == document.Identity.ContentRevision), Is.True,
                        "Every declared component must resolve from the shared selected-common authority, not legacy or fallback lookup.");
                    Assert.That(renderer.Drawable.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(renderer.Drawable.Playfield.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(renderer.Drawable.Playfield.Stages.Single().ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(column.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(note.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(hold.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(hold.Head.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(hold.Tail.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(note.ResolvedMaterialKey, Is.EqualTo(entries[0].Key));
                    Assert.That(hold.Head.ResolvedMaterialKey, Is.EqualTo(entries[1].Key));
                    Assert.That(hold.ResolvedMaterialKey, Is.EqualTo(entries[2].Key));
                    Assert.That(hold.Tail.ResolvedMaterialKey, Is.EqualTo(entries[3].Key));
                    Assert.That(column.ResolvedMaterialKey, Is.EqualTo(entries[4].Key));
                    Assert.That(notePiece.UsesPreparedMaterial, Is.True);
                    Assert.That(headPiece.UsesPreparedMaterial, Is.True);
                    Assert.That(bodyPiece.UsesPreparedMaterial, Is.True);
                    Assert.That(tailPiece.UsesPreparedMaterial, Is.True);
                    Assert.That(keyArea.UsesPreparedMaterial, Is.True);
                    Assert.That(containsTexture(notePiece, noteMaterial.Animation.Frames[0]), Is.True);
                    Assert.That(containsTexture(headPiece, headMaterial.Animation.Frames[0]), Is.True);
                    Assert.That(containsTexture(bodyPiece, bodyMaterial.Body.Frames[0]), Is.True);
                    Assert.That(containsTexture(tailPiece, tailMaterial.Animation.Frames[0]), Is.True);
                    Assert.That(containsTexture(keyArea, keyMaterial.UpTexture), Is.True);
                    Assert.That(note.ChildrenOfType<DefaultNotePiece>(), Is.Empty);
                    Assert.That(hold.ChildrenOfType<DefaultNotePiece>(), Is.Empty);
                    Assert.That(hold.ChildrenOfType<DefaultBodyPiece>(), Is.Empty);
                    Assert.That(column.ChildrenOfType<DefaultKeyArea>(), Is.Empty);
                    Assert.That(preparedScene.HasAuthorScene, Is.True);
                    Assert.That(preparedScene.Snapshot, Is.SameAs(publication.Snapshot));
                    Assert.That(preparedScene.MaterialSet, Is.SameAs(materialSet));
                    Assert.That(preparedScene.PackageRevision, Is.SameAs(publication.Snapshot.Context.PackageRevision));
                    Assert.That(preparedScene.EventContractId, Is.EqualTo(GameplaySkinSceneContracts.EVENT_CONTRACT_ID));
                    Assert.That(preparedScene.Roots.Single().Slot, Is.Null,
                        "The author-scene dispatcher must remain neutral and cannot claim a second public-slot subtree.");
                    Assert.That(preparedScene.Roots.Single().Children.Any(child => ReferenceEquals(child.Slot, GameplaySkinSlotCatalog.LaneSurface)), Is.True);
                    Assert.That(sceneHost.Publication, Is.SameAs(publication));
                    Assert.That(sceneHost.EventStream, Is.SameAs(renderer.Drawable.GameplaySkinEventStream));
                    Assert.That(sceneHost.HostedSlots, Has.Count.EqualTo(materialSet.Entries.Count));
                    Assert.That(new[] { sceneLayers.Background, sceneLayers.Underlay, sceneLayers.Object, sceneLayers.GameplayEffects }
                                    .All(layer => isDescendantOf(layer, renderer.Drawable.PlayfieldAdjustmentContainer)), Is.True);
                    Assert.That(new[] { sceneLayers.Overlay, sceneLayers.HudForeground }
                                    .All(layer => ReferenceEquals(layer.Parent, renderer.Drawable.Overlays)), Is.True);
                    Assert.That(new[]
                    {
                        sceneLayers.Background,
                        sceneLayers.Underlay,
                        sceneLayers.Object,
                        sceneLayers.GameplayEffects,
                        sceneLayers.Overlay,
                        sceneLayers.HudForeground,
                    }.All(layer => layer.Alpha == 1), Is.True,
                        "Every prepared scene stratum must be visibly mounted before any author node replaces production content.");
                    Assert.That(sceneLayers.Overlay.Depth, Is.LessThan(0));
                    Assert.That(sceneLayers.HudForeground.Depth, Is.LessThan(sceneLayers.Overlay.Depth),
                        "Stage foreground/BGA frame/decoration and HUD must remain above engine-owned overlay content.");
                    Assert.That(specialisedGates.All(gate => gate.Route == GameplaySkinSceneHostRoute.Specialised
                                                             && gate.RoutedNodes.Count == 1
                                                             && gate.IsReplacementReady), Is.True);
                    Assert.That(entries.All(candidateEntry => !sceneHost.TryGetHostedDrawable(candidateEntry.Key, out _)), Is.True,
                        "Typed Note/LN/KeyVisual consumers and the shared scene host must never draw the same exact slots twice.");
                    Assert.That(barLineGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(barLineGate.IsReplacementReady, Is.True);
                    Assert.That(sceneHost.TryGetHostedDrawable(barLineKey, out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.note", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.long-note-head", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.long-note-body", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.long-note-tail", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.key-visual", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.bar-line", out _), Is.False,
                        "A real pooled Mania BarLine must own its routed scene node; the shared root must not draw a duplicate.");
                    Assert.That(laneSurfaceEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(laneSurfaceEntry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(laneSurfaceGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Scene));
                    Assert.That(laneSurfaceGate.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.lane-surface", out GameplaySkinSceneRuntimeNode? laneSurfaceNode), Is.True);
                    Assert.That(((Sprite)laneSurfaceNode!.ContentDrawable).Texture,
                        Is.SameAs(laneSurfaceEntry.GetMaterial<GameplaySkinPublicSlotMaterial>().Texture));
                    Assert.That(column.BackgroundContainer.Alpha, Is.GreaterThan(0),
                        "The composite column wrapper must not be shared by multiple public-slot gates.");
                    Assert.That(firstLaneSurfaceOwner.Alpha, Is.Zero,
                        "A selected exact LaneSurface scene must replace the independently gateable native lane surface.");
                    Assert.That(firstLaneDividerOwner.Alpha, Is.Zero,
                        "A selected exact LaneDivider must replace its native divider without owning the lane surface wrapper.");
                    Assert.That(firstHitTargetOwner.Alpha, Is.Zero,
                        "A selected exact HitTarget must replace only its exact legacy lane slice.");
                    Assert.That(judgementLineOwners, Is.Not.Empty);
                    Assert.That(judgementLineOwners.All(owner => owner.Alpha == 0), Is.True,
                        "The stage JudgementLine gate must hide each independently owned native lane segment.");
                    Assert.That(firstKeyFlashOwner.Alpha, Is.Zero,
                        "A selected exact KeyFlash must replace the native per-lane light without hiding another component.");
                    Assert.That(stageBackgroundWrapper.Alpha, Is.GreaterThan(0),
                        "Stage shell gates must not hide the aggregate legacy wrapper which also owns lane/target fallbacks.");
                    Assert.That(stageShellOwners, Is.Not.Empty);
                    Assert.That(stageShellOwners.All(owner => owner.Alpha == 0), Is.True,
                        "The exact stage-background owners must be hidden independently of their aggregate legacy parent.");
                    Assert.That(playfieldBackdropOwners, Is.Not.Empty);
                    Assert.That(playfieldBackdropOwners.All(owner => owner.Alpha == 0), Is.True,
                        "The exact playfield-backdrop owner must use its own public-slot gate.");
                    Assert.That(stageForegroundWrapper.Alpha, Is.Zero);
                    Assert.That(judgementWrapper.Alpha, Is.Zero,
                        "A selected exact judgement host must replace the real per-stage judgement container.");
                    Assert.That(comboWrappers, Has.Length.EqualTo(1));
                    Assert.That(comboWrappers.Single().Alpha, Is.Zero,
                        "A selected exact ComboDisplay must replace the real stage-local mania combo owner.");
                    Assert.That(column.HitObjectArea.HitTarget.Alpha, Is.GreaterThan(0));
                    Assert.That(column.HitObjectArea.Explosions.Alpha, Is.GreaterThan(0),
                        "The bounded explosion pool stays mounted; each leased specialised explosion gates only its own native fallback.");
                    Assert.That(suppressedColumn.BackgroundContainer.Alpha, Is.GreaterThan(0));
                    Assert.That(suppressedColumn.HitObjectArea.HitTarget.Alpha, Is.GreaterThan(0));
                    Assert.That(secondLaneSurfaceOwner.Alpha, Is.GreaterThan(0));
                    Assert.That(secondLaneDividerOwner.Alpha, Is.GreaterThan(0));
                    Assert.That(secondHitTargetOwner.Alpha, Is.GreaterThan(0));
                    Assert.That(secondKeyFlashOwner.Alpha, Is.Zero,
                        "A legal lane-local KeyFlash Suppress must hide only its exact native light.");
                    Assert.That(suppressedColumn.HitObjectArea.Explosions.Alpha, Is.GreaterThan(0),
                        "A neighbouring programmatic lane must not be hidden by another lane's exact author gate.");
                    Assert.That(keyFlashEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(keyFlashEntry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(keyFlashGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(keyFlashGate.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(keyFlashDrawable!.Alpha, Is.Zero,
                        "The selected KeyFlash host must stay idle until the real input event stream activates it.");
                    Assert.That(suppressedKeyVisualEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(suppressedKeyVisualGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(suppressedKeyVisualGate.IsReplacementReady, Is.True);
                    Assert.That(suppressedKeyWrapper.Alpha, Is.Zero);
                    Assert.That(suppressedColumn.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                                                .Any(visual => visual.Key.Equals(suppressedKeyVisualKey)), Is.False);
                    Assert.That(suppressedKeyFlashEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(suppressedKeyFlashGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(sceneHost.TryGetHostedDrawable(suppressedKeyFlashKey, out _), Is.False);
                    Assert.That(authoredGlobalTextHud.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(globalTextGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(globalTextGate.IsReplacementReady, Is.True);
                    Assert.That(sceneHost.TryGetHostedDrawable(authoredGlobalTextHud.Key, out _), Is.True);
                    Assert.That(renderer.CoreHud!.GameplaySkinGaugeOwners.All(owner => owner.Alpha == 0), Is.True,
                        "A ready single-stage gauge replacement must hide the real core health owner.");
                    Assert.That(renderer.CoreHud.GameplaySkinTextOwners.All(owner => owner.Alpha == 0), Is.True,
                        "A ready global TextHud replacement must hide every real serialisable data/text HUD owner.");
                    assertRequiredTextHudOwners(renderer.CoreHud);
                    Assert.That(judgementEdge.Revision, Is.EqualTo(publication.EventRevision));
                    Assert.That(judgementEdge.LaneId?.Value, Is.EqualTo("mania.lane.column-1"));
                    Assert.That(judgementEdge.GroupId?.Value, Is.EqualTo("mania.group.stage-1"));
                    Assert.That(double.IsFinite(judgementEdge.GameplayTime), Is.True);
                    Assert.That(renderer.ScoreProcessor.TotalScore.Value, Is.GreaterThan(0));
                    Assert.That(renderer.ScoreProcessor.Combo.Value, Is.GreaterThan(0));
                    Assert.That(renderer.HealthProcessor.Health.Value, Is.GreaterThan(0.5));
                    Assert.That(((PoolableDrawable)note).IsInPool, Is.True);
                    Assert.That(((PoolableDrawable)hold).IsInPool, Is.True);
                    Assert.That(((PoolableDrawable)barLine).IsInPool, Is.True);
                    Assert.That(isDescendantOf(hold, column.HitObjectContainer), Is.True);
                    Assert.That(isDescendantOf(barLine, renderer.Drawable.Playfield.Stages.Single().HitObjectContainer), Is.True,
                        "Mania BarLine must retain the stage-local scrolling container after specialised scene routing.");
                    Assert.That(containsTexture(barLine, barLineEntry!.GetMaterial<GameplaySkinPublicSlotMaterial>().Texture!), Is.True);
                    Assert.That(new[] { noteWrapper, headWrapper, bodyWrapper, tailWrapper, keyWrapper, barLineWrapper }
                                    .All(wrapper => wrapper.Alpha == 0), Is.True,
                        "An exact ready specialised replacement must hide every matching legacy/programmatic wrapper.");
                    Assert.That(hold.Head.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single().IsApplied, Is.True);
                    Assert.That(hold.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                                    .Single(visual => visual.Key.Equals(entries[2].Key)).IsApplied, Is.True);
                    Assert.That(hold.Tail.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single().IsApplied, Is.True);
                    Assert.That(column.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                                      .Single(visual => visual.Key.Equals(entries[4].Key)).IsApplied, Is.True);
                    Assert.That(barLine.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single().IsApplied, Is.True);
                    Assert.That(noteObjectId, Is.GreaterThanOrEqualTo(0));
                    Assert.That(holdObjectId, Is.GreaterThanOrEqualTo(0));
                    Assert.That(headSceneVisual.BoundObjectId, Is.EqualTo(holdObjectId));
                    Assert.That(tailSceneVisual.BoundObjectId, Is.EqualTo(holdObjectId));
                    Assert.That(barLineObjectId, Is.GreaterThanOrEqualTo(0));
                    Assert.That(new[] { noteObjectId, holdObjectId, barLineObjectId }.Distinct().Count(), Is.EqualTo(3));
                    Assert.That(spawnedObjectIds, Does.Contain(noteObjectId));
                    Assert.That(spawnedObjectIds, Does.Contain(holdObjectId));
                    Assert.That(spawnedObjectIds, Does.Contain(barLineObjectId));
                });

                assertSpecialisedSceneConsumer(note, materialSet, entries[0].Key, specialisedGates[0], "node.note");
                assertSpecialisedSceneConsumer(hold.Head, materialSet, entries[1].Key, specialisedGates[1], "node.long-note-head");
                assertSpecialisedSceneConsumer(hold, materialSet, entries[2].Key, specialisedGates[2], "node.long-note-body");
                assertSpecialisedSceneConsumer(hold.Tail, materialSet, entries[3].Key, specialisedGates[3], "node.long-note-tail");
                assertSpecialisedSceneConsumer(column, materialSet, entries[4].Key, specialisedGates[4], "node.key-visual");
                assertSpecialisedSceneConsumer(barLine, materialSet, barLineKey, barLineGate!, "node.bar-line");
                assertSceneConsumer(
                    suppressedColumn,
                    materialSet,
                    suppressedKeyVisualKey,
                    suppressedKeyVisualGate!,
                    GameplaySkinSceneHostRoute.Suppressed);
            });

            AddStep("remove judged tap before long-note seek", () =>
                Assert.That(renderer.RemoveProductionNote(), Is.True));
            AddStep("advance real mania clock to long-note head", () => renderer.SetRulesetTime(renderer.Hold.StartTime));
            AddUntilStep("rebind real pooled long note after seek", () =>
                renderer.RefreshProductionHoldDrawable()
                && renderer.HoldDrawable!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                           .Single(visual => visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteBody).BoundObjectId == holdObjectId);
            AddStep("press real mania long note", () =>
            {
                ManiaAction action = renderer.FirstColumn.Action.Value;

                foreach (ManiaInputManager inputManager in renderer.Drawable.ChildrenOfType<ManiaInputManager>())
                    inputManager.KeyBindingContainer.TriggerPressed(action);
            });
            AddUntilStep("wait for real mania long-note holding edge", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectStateChanged
                                                      && envelope.Payload is GameplaySkinObjectEventPayload
                                                      {
                                                          State.ObjectId: var objectId,
                                                          State.State: GameplaySkinObjectState.Holding,
                                                      }
                                                      && objectId == holdObjectId);
            });
            AddStep("assert long-note progress uses envelope gameplay time", () =>
            {
                GameplaySkinEventEnvelope holding = observedEvents.Last(envelope =>
                    envelope.EventKind == GameplaySkinEventKind.ObjectStateChanged
                    && envelope.Payload is GameplaySkinObjectEventPayload
                    {
                        State.ObjectId: var objectId,
                        State.State: GameplaySkinObjectState.Holding,
                    }
                    && objectId == holdObjectId);
                GameplaySkinObjectStateSnapshot state = ((GameplaySkinObjectEventPayload)holding.Payload).State;
                double expected = Math.Clamp(
                    (holding.GameplayTime - state.StartTime) / (state.EndTime - state.StartTime),
                    0,
                    1);
                Assert.That(state.Progress, Is.EqualTo(expected).Within(1e-9),
                    "Object progress and the envelope timestamp must be sampled from the same authoritative gameplay clock.");
            });
            AddStep("advance real mania clock to long-note tail", () => renderer.SetRulesetTime(renderer.Hold.EndTime));
            AddUntilStep("rebind real pooled long note at tail", () => renderer.RefreshProductionHoldDrawable());
            AddStep("release real mania long note", () =>
            {
                ManiaAction action = renderer.FirstColumn.Action.Value;

                foreach (ManiaInputManager inputManager in renderer.Drawable.ChildrenOfType<ManiaInputManager>())
                    inputManager.KeyBindingContainer.TriggerReleased(action);
            });
            AddUntilStep("wait for real mania long-note completed edge", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectStateChanged
                                                      && envelope.Payload is GameplaySkinObjectEventPayload
                                                      {
                                                          State.ObjectId: var objectId,
                                                          State.State: GameplaySkinObjectState.Completed,
                                                          State.Progress: 1,
                                                      }
                                                      && objectId == holdObjectId);
            });
            AddStep("capture completed event epoch", () =>
                completedEpoch = observedEvents.Last(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectStateChanged
                                                                 && envelope.Payload is GameplaySkinObjectEventPayload
                                                                 {
                                                                     State.ObjectId: var objectId,
                                                                     State.State: GameplaySkinObjectState.Completed,
                                                                 }
                                                                 && objectId == holdObjectId).Epoch);
            AddStep("rewind completed long note to its midpoint", () =>
                renderer.SetRulesetTime(renderer.Hold.StartTime + renderer.Hold.Duration / 2));
            AddUntilStep("wait for authoritative rewind reset", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.StateReset
                                                      && envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset
                                                      && envelope.Epoch > completedEpoch);
            });
            AddStep("assert rewind reset recomputes terminal object progress", () =>
            {
                GameplaySkinEventEnvelope reset = observedEvents.Last(envelope => envelope.EventKind == GameplaySkinEventKind.StateReset
                                                                        && envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset
                                                                        && envelope.Epoch > completedEpoch);
                GameplaySkinStateEventPayload payload = (GameplaySkinStateEventPayload)reset.Payload;
                GameplaySkinObjectStateSnapshot[] matchingStates = payload.State.ActiveObjects.Where(obj => obj.ObjectId == holdObjectId).ToArray();
                Assert.That(matchingStates, Has.Length.EqualTo(1),
                    "Reset must rebuild the active long note directly from the production drawable snapshot source.");
                GameplaySkinObjectStateSnapshot state = matchingStates[0];
                double expected = Math.Clamp((reset.GameplayTime - state.StartTime) / (state.EndTime - state.StartTime), 0, 1);

                Assert.Multiple(() =>
                {
                    Assert.That(payload.ResetReason, Is.EqualTo(GameplaySkinEventResetReason.Rewind));
                    Assert.That(reset.GameplayTime, Is.EqualTo(renderer.Hold.StartTime + renderer.Hold.Duration / 2).Within(1e-9));
                    Assert.That(state.ObjectId, Is.EqualTo(holdObjectId));
                    Assert.That(state.Kind, Is.EqualTo(GameplaySkinObjectKind.LongNote));
                    Assert.That(state.State, Is.Not.EqualTo(GameplaySkinObjectState.Completed),
                        "A reset before the authoritative tail cannot retain terminal completed state.");
                    Assert.That(state.Progress, Is.EqualTo(expected).Within(1e-9),
                        $"reset={reset.GameplayTime}, start={state.StartTime}, end={state.EndTime}, state={state.State}");
                    Assert.That(state.Progress, Is.LessThan(1),
                        "A reset before the tail must not retain terminal completed progress.");
                    Assert.That(payload.State.Timing.Bpm, Is.EqualTo(240).Within(1e-9));
                    Assert.That(payload.State.Timing.Beat, Is.EqualTo(3).Within(1e-9));
                    Assert.That(payload.State.Timing.BarIndex, Is.EqualTo(1),
                        "The real Mania rewind must sample the second timing/signature segment without resetting cumulative beat/bar identity.");
                });
            });
            AddUntilStep("wait for legal post-reset hold resynchronisation", () =>
                renderer.RefreshProductionHoldDrawable()
                && renderer.HoldDrawable!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                           .Single(visual => visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteBody).BoundObjectId == holdObjectId);
            AddStep("assert reset remains the sole complete resynchronisation authority", () =>
            {
                GameplaySkinEventEnvelope reset = observedEvents.Last(envelope => envelope.EventKind == GameplaySkinEventKind.StateReset
                                                                                && envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Reset
                                                                                && envelope.Epoch > completedEpoch);
                Assert.That(renderer.HoldDrawable!.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                                            .Single(visual => visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteBody).BoundObjectId,
                    Is.EqualTo(holdObjectId),
                    "The real pooled owner must rebind the same engine ID without requiring a duplicate synthetic spawn edge after Reset.");
                Assert.That(renderer.Drawable.GameplaySkinEventStream.CurrentEpoch, Is.EqualTo(reset.Epoch),
                    "The rebound real owner must remain attached to the reset epoch rather than reviving the retired publication state.");
            });
            bool drainUntil(GameplaySkinEventKind kind)
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == kind);
            }

            AddUntilStep("wait for real mania object spawn event", () => drainUntil(GameplaySkinEventKind.ObjectSpawned));
            AddUntilStep("wait for real mania object state event", () => drainUntil(GameplaySkinEventKind.ObjectStateChanged));
            AddUntilStep("wait for real mania judgement event", () => drainUntil(GameplaySkinEventKind.JudgementApplied));
            AddUntilStep("wait for real mania score event", () => drainUntil(GameplaySkinEventKind.ScoreChanged));
            AddUntilStep("wait for real mania combo event", () => drainUntil(GameplaySkinEventKind.ComboChanged));
            AddUntilStep("wait for real mania gauge event", () => drainUntil(GameplaySkinEventKind.GaugeChanged));
            AddStep("remove real pooled hold through production playfield", () =>
                Assert.That(renderer.RemoveProductionHold(), Is.True));
            AddUntilStep("wait for real mania object despawn edge", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectDespawned);
            });
            AddStep("assert complete production event envelope coverage", () => Assert.Multiple(() =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinEventEnvelope snapshot = observedEvents.Single(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Snapshot);
                Assert.That(snapshot.Payload, Is.TypeOf<GameplaySkinStateEventPayload>());
                Assert.That(observedEvents.Where(envelope => envelope.DeliveryKind == GameplaySkinEventDeliveryKind.Edge)
                                          .All(envelope => envelope.Revision == publication.EventRevision
                                                           && double.IsFinite(envelope.GameplayTime)), Is.True);
                Assert.That(observedEvents.Select(envelope => envelope.EventKind), Does.Contain(GameplaySkinEventKind.ObjectSpawned));
                Assert.That(observedEvents.Select(envelope => envelope.EventKind), Does.Contain(GameplaySkinEventKind.ObjectStateChanged));
                Assert.That(observedEvents.Select(envelope => envelope.EventKind), Does.Contain(GameplaySkinEventKind.ObjectDespawned));
                Assert.That(observedEvents.Select(envelope => envelope.EventKind), Does.Contain(GameplaySkinEventKind.JudgementApplied));
                Assert.That(observedEvents.Select(envelope => envelope.EventKind), Does.Contain(GameplaySkinEventKind.ScoreChanged));
                Assert.That(observedEvents.Select(envelope => envelope.EventKind), Does.Contain(GameplaySkinEventKind.ComboChanged));
                Assert.That(observedEvents.Select(envelope => envelope.EventKind), Does.Contain(GameplaySkinEventKind.GaugeChanged));
            }));
            AddStep("detach public-material renderer", () =>
            {
                eventSubscription.Dispose();
                renderer.Expire();
            });
            AddUntilStep("wait for public-material renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestDualStagePartialBarLineAuthoringKeepsExactNativeOwnersIndependent()
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            DrawableBarLine[] barLines = null!;

            AddStep("create isolated dual-stage partial-author skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });
            AddStep("create and select dual-stage partial bar-line package", () =>
            {
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-dual-partial-{Guid.NewGuid():N}");
                writeDualStagePartialBarLinePackage(packageRoot);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for dual-stage partial exact revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount exact dual-stage partial-author renderer", () =>
            {
                Add(renderer = new CurrentRevisionManiaMaterialHost(
                    publicMaterialSkinManager!,
                    dualStage: true,
                    includeStageActivationObjects: true));
            });
            AddUntilStep("wait for exact dual-stage partial-author ruleset", () => renderer.Drawable.IsLoaded);
            AddUntilStep("wait for exact dual-stage partial-author topology", () => renderer.Drawable.Playfield.Stages.Count == 2);
            AddStep("add one real shared bar line", () =>
            {
                renderer.AddProductionBarLine(1_150);
                renderer.SetRulesetTime(1_150);
            });
            AddUntilStep("wait for exact dual-stage partial-author stages", () =>
                renderer.Drawable.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
            AddStep("capture mounted dual-stage scene host", () =>
                sceneHost = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for real stage-local bar-line owners", () =>
            {
                barLines = renderer.Drawable.Playfield.Stages
                                   .SelectMany(stage => stage.HitObjectContainer.Objects.OfType<DrawableBarLine>())
                                   .Where(line => ReferenceEquals(line.HitObject, renderer.BarLine))
                                   .ToArray();
                return barLines.Length == 2;
            });
            AddUntilStep("wait for bounded dual-stage scene preparation", () => sceneHost.PendingCreationCount == 0);
            AddStep("mount real dual-stage mania HUD owners", () => renderer.AddProductionHud());
            AddUntilStep("wait for both stage-local combo owners", () =>
                renderer.Hud?.IsLoaded == true
                && renderer.Hud.Children.Count(child => child is OmsManiaComboCounter or LegacyManiaComboCounter) == 2);
            AddStep("mount real dual-stage core HUD owners", () => renderer.AddProductionCoreHud());
            AddUntilStep("wait for dual-stage core HUD registrations", () =>
                renderer.CoreHud?.IsLoaded == true
                && renderer.CoreHud.GameplaySkinGaugeOwners.Count > 0
                && renderer.CoreHud.GameplaySkinTextOwners.Count > 0);
            AddStep("press suppressed lane on stage two", () =>
            {
                ManiaAction action = renderer.Drawable.Playfield.Stages[1].Columns[0].Action.Value;
                ManiaInputManager[] inputManagers = renderer.Drawable.ChildrenOfType<ManiaInputManager>().ToArray();
                Assert.That(inputManagers, Is.Not.Empty);

                foreach (ManiaInputManager inputManager in inputManagers)
                    inputManager.KeyBindingContainer.TriggerPressed(action);
            });
            AddWaitStep("allow suppressed native key-flash animation", 5);
            AddStep("release suppressed lane on stage two", () =>
            {
                ManiaAction action = renderer.Drawable.Playfield.Stages[1].Columns[0].Action.Value;

                foreach (ManiaInputManager inputManager in renderer.Drawable.ChildrenOfType<ManiaInputManager>())
                    inputManager.KeyBindingContainer.TriggerReleased(action);
            });
            AddWaitStep("allow suppressed native key-flash release", 5);
            AddStep("assert partial authoring cannot cross stage gate ownership", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinLaneTopologyGroup firstGroup = publication.Snapshot.Context.Topology.GroupsInLogicalOrder[0];
                GameplaySkinLaneTopologyGroup secondGroup = publication.Snapshot.Context.Topology.GroupsInLogicalOrder[1];
                var firstKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.BarLine,
                    GameplaySkinResolvedMaterialTarget.ForGroup(firstGroup));
                var secondKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.BarLine,
                    GameplaySkinResolvedMaterialTarget.ForGroup(secondGroup));
                GameplaySkinLaneTopologyEntry secondStageFirstLane = secondGroup.LanesInLogicalOrder[0];
                var secondStageLaneKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LaneSurface,
                    GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondStageFirstLane));
                GameplaySkinLaneTopologyEntry secondStageSecondLane = secondGroup.LanesInLogicalOrder[1];
                var secondStageNeighbourLaneKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LaneSurface,
                    GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondStageSecondLane));
                var secondStageKeyFlashKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.KeyFlash,
                    GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondStageFirstLane));
                var secondStageTargetKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.JudgementLine,
                    GameplaySkinResolvedMaterialTarget.ForStage(secondGroup));
                var firstStageJudgementKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.JudgementDisplay,
                    GameplaySkinResolvedMaterialTarget.ForStage(firstGroup));
                var secondStageJudgementKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.JudgementDisplay,
                    GameplaySkinResolvedMaterialTarget.ForStage(secondGroup));
                var firstStageComboKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.ComboDisplay,
                    GameplaySkinResolvedMaterialTarget.ForStage(firstGroup));
                var secondStageComboKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.ComboDisplay,
                    GameplaySkinResolvedMaterialTarget.ForStage(secondGroup));
                var firstStageGaugeKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.GaugeVisual,
                    GameplaySkinResolvedMaterialTarget.ForStage(firstGroup));
                var secondStageGaugeKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.GaugeVisual,
                    GameplaySkinResolvedMaterialTarget.ForStage(secondGroup));
                var globalTextKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.TextHud,
                    GameplaySkinResolvedMaterialTarget.Global);
                var firstStageTextKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.TextHud,
                    GameplaySkinResolvedMaterialTarget.ForStage(firstGroup));
                var secondStageTextKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.TextHud,
                    GameplaySkinResolvedMaterialTarget.ForStage(secondGroup));
                DrawableBarLine firstLine = barLines.Single(line => isDescendantOf(line, renderer.Drawable.Playfield.Stages[0].HitObjectContainer));
                DrawableBarLine secondLine = barLines.Single(line => isDescendantOf(line, renderer.Drawable.Playfield.Stages[1].HitObjectContainer));
                SkinnableDrawable firstWrapper = getPrivateField<SkinnableDrawable>(firstLine, "programmaticVisual");
                SkinnableDrawable secondWrapper = getPrivateField<SkinnableDrawable>(secondLine, "programmaticVisual");
                Drawable firstStageJudgements = getPrivateField<Drawable>(renderer.Drawable.Playfield.Stages[0], "judgements");
                Drawable secondStageJudgements = getPrivateField<Drawable>(renderer.Drawable.Playfield.Stages[1], "judgements");
                Drawable[] comboOwners = renderer.Hud!.Children
                                                  .Where(child => child is OmsManiaComboCounter or LegacyManiaComboCounter)
                                                  .ToArray();
                SkinnableDrawable secondStageBackground = getPrivateField<SkinnableDrawable>(renderer.Drawable.Playfield.Stages[1], "stageBackground");
                ManiaGameplaySkinProgrammaticVisualPart[] secondStageParts =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)secondStageBackground.Drawable)
                    .GameplaySkinProgrammaticVisualParts.ToArray();
                Drawable secondStageLaneSurfaceOwner = secondStageParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneSurface)
                    && part.GroupLocalLaneIndex == 0).Owner;
                Drawable secondStageNeighbourSurfaceOwner = secondStageParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneSurface)
                    && part.GroupLocalLaneIndex == 1).Owner;
                Drawable secondStageLaneDividerOwner = secondStageParts.Single(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneDivider)
                    && part.GroupLocalLaneIndex == 0).Owner;
                Drawable secondStageKeyFlashOwner =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)getPrivateField<SkinnableDrawable>(
                        renderer.Drawable.Playfield.Stages[1].Columns[0], "columnBackground").Drawable)
                    .GameplaySkinProgrammaticVisualParts.Single(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.KeyFlash)).Owner;
                Drawable firstStageKeyFlashOwner =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)getPrivateField<SkinnableDrawable>(
                        renderer.Drawable.Playfield.Stages[0].Columns[0], "columnBackground").Drawable)
                    .GameplaySkinProgrammaticVisualParts.Single(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.KeyFlash)).Owner;
                Assert.That(publication.MaterialSet.TryGet(firstKey, out GameplaySkinResolvedMaterialEntry? firstEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondKey, out GameplaySkinResolvedMaterialEntry? secondEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondStageLaneKey, out GameplaySkinResolvedMaterialEntry? secondStageLaneEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondStageNeighbourLaneKey, out GameplaySkinResolvedMaterialEntry? secondStageNeighbourLaneEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondStageKeyFlashKey, out GameplaySkinResolvedMaterialEntry? secondStageKeyFlashEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondStageTargetKey, out _), Is.True);
                Assert.That(publication.MaterialSet.TryGet(firstStageJudgementKey, out GameplaySkinResolvedMaterialEntry? firstStageJudgementEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondStageJudgementKey, out GameplaySkinResolvedMaterialEntry? secondStageJudgementEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(firstStageComboKey, out GameplaySkinResolvedMaterialEntry? firstStageComboEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondStageComboKey, out GameplaySkinResolvedMaterialEntry? secondStageComboEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(firstStageGaugeKey, out GameplaySkinResolvedMaterialEntry? firstStageGaugeEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(secondStageGaugeKey, out GameplaySkinResolvedMaterialEntry? secondStageGaugeEntry), Is.True);
                Assert.That(publication.MaterialSet.TryGet(globalTextKey, out GameplaySkinResolvedMaterialEntry? globalTextEntry), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(firstKey, out GameplaySkinSceneHostedSlot? firstGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(secondKey, out GameplaySkinSceneHostedSlot? secondGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(firstStageJudgementKey, out GameplaySkinSceneHostedSlot? firstStageJudgementGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(secondStageJudgementKey, out GameplaySkinSceneHostedSlot? secondStageJudgementGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(secondStageLaneKey, out GameplaySkinSceneHostedSlot? secondStageLaneGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(secondStageKeyFlashKey, out GameplaySkinSceneHostedSlot? secondStageKeyFlashGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(firstStageComboKey, out GameplaySkinSceneHostedSlot? firstStageComboGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(secondStageComboKey, out GameplaySkinSceneHostedSlot? secondStageComboGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(firstStageGaugeKey, out GameplaySkinSceneHostedSlot? firstStageGaugeGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(secondStageGaugeKey, out GameplaySkinSceneHostedSlot? secondStageGaugeGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(globalTextKey, out GameplaySkinSceneHostedSlot? globalTextGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(firstStageTextKey, out GameplaySkinSceneHostedSlot? firstStageTextGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(secondStageTextKey, out GameplaySkinSceneHostedSlot? secondStageTextGate), Is.True);

                GameplaySkinHudProgrammaticVisualPartition[] firstGaugePartitions = renderer.CoreHud!.GameplaySkinGaugePartitions
                    .Where(partition => partition.StageKey.Equals(firstStageGaugeKey)).ToArray();
                GameplaySkinHudProgrammaticVisualPartition[] secondGaugePartitions = renderer.CoreHud.GameplaySkinGaugePartitions
                    .Where(partition => partition.StageKey.Equals(secondStageGaugeKey)).ToArray();
                GameplaySkinHudProgrammaticVisualPartition[] firstTextPartitions = renderer.CoreHud.GameplaySkinTextPartitions
                    .Where(partition => partition.StageKey.Equals(firstStageTextKey)).ToArray();
                GameplaySkinHudProgrammaticVisualPartition[] secondTextPartitions = renderer.CoreHud.GameplaySkinTextPartitions
                    .Where(partition => partition.StageKey.Equals(secondStageTextKey)).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(firstEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                    Assert.That(secondEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(firstKey.Target.GroupId?.Value, Is.EqualTo("mania.group.stage-1"));
                    Assert.That(firstKey.Target.GroupLogicalIndex, Is.EqualTo(0));
                    Assert.That(firstKey.Target.GroupVisualIndex, Is.EqualTo(0));
                    Assert.That(secondKey.Target.GroupId?.Value, Is.EqualTo("mania.group.stage-2"));
                    Assert.That(secondKey.Target.GroupLogicalIndex, Is.EqualTo(1));
                    Assert.That(secondKey.Target.GroupVisualIndex, Is.EqualTo(1));
                    Assert.That(secondStageTargetKey.Target.GroupId?.Value, Is.EqualTo("mania.group.stage-2"));
                    Assert.That(secondStageTargetKey.Target.GroupLogicalIndex, Is.EqualTo(1));
                    Assert.That(secondStageTargetKey.Target.GroupVisualIndex, Is.EqualTo(1));
                    Assert.That(secondStageLaneKey.Target.GroupId?.Value, Is.EqualTo("mania.group.stage-2"));
                    Assert.That(secondStageLaneKey.Target.LaneId?.Value, Is.EqualTo("mania.lane.column-5"));
                    Assert.That(secondStageLaneKey.Target.GroupLogicalIndex, Is.EqualTo(1));
                    Assert.That(secondStageLaneKey.Target.GroupVisualIndex, Is.EqualTo(1));
                    Assert.That(secondStageLaneKey.Target.GlobalLogicalIndex, Is.EqualTo(4));
                    Assert.That(secondStageLaneKey.Target.GlobalVisualIndex, Is.EqualTo(4));
                    Assert.That(secondStageLaneKey.Target.GroupLocalLogicalIndex, Is.Zero);
                    Assert.That(secondStageLaneKey.Target.GroupLocalVisualIndex, Is.Zero);
                    Assert.That(secondStageLaneEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(secondStageLaneGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(secondStageLaneGate.IsReplacementReady, Is.True);
                    Assert.That(secondStageLaneSurfaceOwner.Alpha, Is.Zero);
                    Assert.That(secondStageNeighbourLaneEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                    Assert.That(secondStageNeighbourSurfaceOwner.Alpha, Is.GreaterThan(0));
                    Assert.That(secondStageLaneDividerOwner.Alpha, Is.GreaterThan(0),
                        "A lane-surface Provide must not hide the same lane's independent divider owner.");
                    Assert.That(secondStageKeyFlashEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(secondStageKeyFlashGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(secondStageKeyFlashOwner.Alpha, Is.Zero);
                    Assert.That(firstStageKeyFlashOwner.Alpha, Is.GreaterThan(0),
                        "A stage-two lane Suppress must not affect the neighbouring stage's native flash owner.");
                    Assert.That(firstGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(firstGate.RoutedNodes, Is.Empty);
                    Assert.That(firstGate.IsReplacementReady, Is.False);
                    Assert.That(firstWrapper.Alpha, Is.GreaterThan(0));
                    Assert.That(firstLine.AppliedSceneNodeIds, Is.Empty);
                    Assert.That(secondGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(secondGate.RoutedNodes.Select(node => node.InstanceId),
                        Is.EqualTo(new[] { "node.stage-2-bar-line" }));
                    Assert.That(secondGate.IsReplacementReady, Is.True);
                    Assert.That(secondWrapper.Alpha, Is.Zero);
                    Assert.That(secondLine.AppliedSceneNodeIds, Is.EqualTo(new[] { "node.stage-2-bar-line" }));
                    Assert.That(secondLine.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Single().IsApplied, Is.True);
                    Assert.That(firstLine.ResolvedMaterialKey, Is.EqualTo(firstKey));
                    Assert.That(secondLine.ResolvedMaterialKey, Is.EqualTo(secondKey));
                    Assert.That(ReferenceEquals(firstWrapper, secondWrapper), Is.False,
                        "A shared BarLine gameplay object must retain one independent native wrapper and gate per stage usage.");
                    Assert.That(firstStageJudgementEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                    Assert.That(firstStageJudgementGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(firstStageJudgements.Alpha, Is.GreaterThan(0));
                    Assert.That(secondStageJudgementEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(secondStageJudgementGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(secondStageJudgementGate.IsReplacementReady, Is.True);
                    Assert.That(secondStageJudgements.Alpha, Is.Zero);
                    Assert.That(ReferenceEquals(firstStageJudgements, secondStageJudgements), Is.False,
                        "A stage-local Suppress gate must never hide or restore the neighbouring stage's native owner.");
                    Assert.That(comboOwners, Has.Length.EqualTo(2));
                    Assert.That(renderer.CoreHud!.GameplaySkinComboPartitions, Is.Empty,
                        "Ruleset-owned stage combos must not be cloned or registered again by the shared HUD adapter.");
                    Assert.That(firstStageComboEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                    Assert.That(firstStageComboGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(comboOwners[0].Alpha, Is.GreaterThan(0));
                    Assert.That(secondStageComboEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(secondStageComboGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(secondStageComboGate.IsReplacementReady, Is.True);
                    Assert.That(comboOwners[1].Alpha, Is.Zero);
                    Assert.That(ReferenceEquals(comboOwners[0], comboOwners[1]), Is.False,
                        "A partial dual-stage combo gate must own independent native wrappers and never cross stages.");
                    Assert.That(firstStageGaugeEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                    Assert.That(firstStageGaugeGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(secondStageGaugeEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(secondStageGaugeGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(secondStageGaugeGate.IsReplacementReady, Is.True);
                    Assert.That(firstGaugePartitions, Is.Not.Empty);
                    Assert.That(secondGaugePartitions, Has.Length.EqualTo(firstGaugePartitions.Length));
                    Assert.That(firstGaugePartitions.All(partition => partition.Owner.Alpha > 0), Is.True);
                    Assert.That(secondGaugePartitions.All(partition => partition.Owner.Alpha == 0), Is.True,
                        "A stage-two gauge replacement must hide only its non-overlapping real health-display partitions.");
                    Assert.That(globalTextEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                    Assert.That(globalTextGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(firstStageTextGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(secondStageTextGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(firstTextPartitions, Is.Not.Empty);
                    Assert.That(secondTextPartitions, Has.Length.EqualTo(firstTextPartitions.Length));
                    Assert.That(firstTextPartitions.All(partition => partition.Owner.Alpha > 0), Is.True);
                    Assert.That(secondTextPartitions.All(partition => partition.Owner.Alpha == 0), Is.True,
                        "A stage-two TextHud suppression must not hide the neighbouring stage's real text partitions.");
                    assertExactHudPartitions(renderer.CoreHud, sceneHost, publication.Snapshot, GameplaySkinSlotCatalog.GaugeVisual,
                        renderer.CoreHud.GameplaySkinGaugePartitions);
                    assertExactHudPartitions(renderer.CoreHud, sceneHost, publication.Snapshot, GameplaySkinSlotCatalog.TextHud,
                        renderer.CoreHud.GameplaySkinTextPartitions);
                });
            });
            AddStep("detach dual-stage partial-author renderer", () => renderer.Expire());
            AddUntilStep("wait for dual-stage partial-author detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestUnpartitionedCustomColumnAndHitTargetCannotBypassExactAuthorGates()
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;

            AddStep("create isolated custom-fallback skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });
            AddStep("create and select custom-fallback exact package", () =>
            {
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-custom-fallback-{Guid.NewGuid():N}");
                writeDualStageCustomFallbackPackage(packageRoot);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for custom-fallback exact revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount real dual-stage custom-fallback renderer", () =>
            {
                Add(renderer = new CurrentRevisionManiaMaterialHost(
                    publicMaterialSkinManager!,
                    dualStage: true,
                    includeStageActivationObjects: true,
                    useUnpartitionedCustomVisuals: true));
            });
            AddUntilStep("wait for custom-fallback production topology", () =>
                renderer.Drawable.IsLoaded
                && renderer.Drawable.Playfield.Stages.Count == 2
                && renderer.Drawable.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
            AddStep("capture mounted custom-fallback scene host", () =>
                sceneHost = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for custom-fallback scene readiness", () => sceneHost.PendingCreationCount == 0);
            AddStep("publish custom-fallback scene readiness", () => sceneHost.ProcessFrame());
            AddStep("assert exact custom fallback gates and partial isolation", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinLaneTopologyGroup firstGroup = publication.Snapshot.Context.Topology.GroupsInLogicalOrder[0];
                GameplaySkinLaneTopologyGroup secondGroup = publication.Snapshot.Context.Topology.GroupsInLogicalOrder[1];
                GameplaySkinLaneTopologyEntry secondFirstLane = secondGroup.LanesInLogicalOrder[0];
                GameplaySkinLaneTopologyEntry secondSecondLane = secondGroup.LanesInLogicalOrder[1];

                var laneSurfaceKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LaneSurface,
                    GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondFirstLane));
                var laneDividerKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LaneDivider,
                    GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondFirstLane));
                var hitTargetKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.HitTarget,
                    GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondFirstLane));
                var keyFlashKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.KeyFlash,
                    GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondSecondLane));
                var judgementLineKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.JudgementLine,
                    GameplaySkinResolvedMaterialTarget.ForStage(secondGroup));

                assertGate(laneSurfaceKey, GameplaySkinSceneHostRoute.Semantic);
                assertGate(laneDividerKey, GameplaySkinSceneHostRoute.Semantic);
                assertGate(hitTargetKey, GameplaySkinSceneHostRoute.Semantic);
                assertGate(keyFlashKey, GameplaySkinSceneHostRoute.Suppressed);
                assertGate(judgementLineKey, GameplaySkinSceneHostRoute.Semantic);

                Column firstStageColumn = renderer.Drawable.Playfield.Stages[0].Columns[0];
                Column secondStageFirstColumn = renderer.Drawable.Playfield.Stages[1].Columns[0];
                Column secondStageSecondColumn = renderer.Drawable.Playfield.Stages[1].Columns[1];
                Column secondStageUnauthoredColumn = renderer.Drawable.Playfield.Stages[1].Columns[2];
                SkinnableDrawable firstStageBackground = getPrivateField<SkinnableDrawable>(firstStageColumn, "columnBackground");
                SkinnableDrawable secondFirstBackground = getPrivateField<SkinnableDrawable>(secondStageFirstColumn, "columnBackground");
                SkinnableDrawable secondSecondBackground = getPrivateField<SkinnableDrawable>(secondStageSecondColumn, "columnBackground");
                SkinnableDrawable secondUnauthoredBackground = getPrivateField<SkinnableDrawable>(secondStageUnauthoredColumn, "columnBackground");
                SkinnableDrawable firstStageHitTarget = firstStageColumn.HitObjectArea.HitTarget;
                SkinnableDrawable secondFirstHitTarget = secondStageFirstColumn.HitObjectArea.HitTarget;
                SkinnableDrawable secondSecondHitTarget = secondStageSecondColumn.HitObjectArea.HitTarget;
                SkinnableDrawable secondUnauthoredHitTarget = secondStageUnauthoredColumn.HitObjectArea.HitTarget;
                ManiaGameplaySkinProgrammaticVisualPart[] secondFirstBackgroundParts =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)secondFirstBackground.Drawable)
                    .GameplaySkinProgrammaticVisualParts.ToArray();
                ManiaGameplaySkinProgrammaticVisualPart[] secondSecondBackgroundParts =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)secondSecondBackground.Drawable)
                    .GameplaySkinProgrammaticVisualParts.ToArray();
                ManiaGameplaySkinProgrammaticVisualPart[] secondFirstTargetParts =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)secondFirstHitTarget.Drawable)
                    .GameplaySkinProgrammaticVisualParts.ToArray();
                ManiaGameplaySkinProgrammaticVisualPart[] secondSecondTargetParts =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)secondSecondHitTarget.Drawable)
                    .GameplaySkinProgrammaticVisualParts.ToArray();
                ManiaGameplaySkinProgrammaticVisualPart[] secondUnauthoredTargetParts =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)secondUnauthoredHitTarget.Drawable)
                    .GameplaySkinProgrammaticVisualParts.ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(firstStageBackground.Drawable, Is.TypeOf<UnpartitionedCustomColumnBackground>());
                    Assert.That(firstStageHitTarget.Drawable, Is.TypeOf<UnpartitionedCustomHitTarget>());
                    Assert.That(secondFirstBackground.Drawable, Is.TypeOf<DefaultColumnBackground>(),
                        "A selected declaration must close only the indivisible custom column component onto its typed fallback.");
                    Assert.That(secondSecondBackground.Drawable, Is.TypeOf<DefaultColumnBackground>());
                    Assert.That(secondUnauthoredBackground.Drawable, Is.TypeOf<UnpartitionedCustomColumnBackground>(),
                        "A lane with no selected declaration must retain the user's custom component unchanged.");
                    Assert.That(secondFirstHitTarget.Drawable, Is.TypeOf<DefaultHitTarget>());
                    Assert.That(secondSecondHitTarget.Drawable, Is.TypeOf<DefaultHitTarget>());
                    Assert.That(secondUnauthoredHitTarget.Drawable, Is.TypeOf<DefaultHitTarget>(),
                        "The stage-scoped JudgementLine declaration requires an independently gateable target fallback in every stage-two lane.");
                    Assert.That(firstStageBackground.Alpha, Is.GreaterThan(0));
                    Assert.That(firstStageHitTarget.Alpha, Is.GreaterThan(0));
                    Assert.That(secondFirstBackground.Alpha, Is.GreaterThan(0));
                    Assert.That(secondSecondBackground.Alpha, Is.GreaterThan(0));
                    Assert.That(secondUnauthoredBackground.Alpha, Is.GreaterThan(0));
                    Assert.That(secondFirstHitTarget.Alpha, Is.GreaterThan(0));
                    Assert.That(secondSecondHitTarget.Alpha, Is.GreaterThan(0));
                    Assert.That(secondUnauthoredHitTarget.Alpha, Is.GreaterThan(0));
                    Assert.That(partOwner(secondFirstBackgroundParts, GameplaySkinSlotCatalog.LaneSurface).Alpha, Is.Zero);
                    Assert.That(secondFirstBackgroundParts.Any(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneDivider)), Is.False,
                        "The author divider has no native default owner and must remain solely on the shared semantic layer.");
                    Assert.That(partOwner(secondFirstBackgroundParts, GameplaySkinSlotCatalog.KeyFlash).Alpha, Is.GreaterThan(0),
                        "LaneSurface/LaneDivider authoring must not hide the sibling KeyFlash fallback.");
                    Assert.That(partOwner(secondSecondBackgroundParts, GameplaySkinSlotCatalog.LaneSurface).Alpha, Is.GreaterThan(0),
                        "KeyFlash suppression must not hide the sibling LaneSurface fallback.");
                    Assert.That(partOwner(secondSecondBackgroundParts, GameplaySkinSlotCatalog.KeyFlash).Alpha, Is.Zero);
                    Assert.That(partOwner(secondFirstTargetParts, GameplaySkinSlotCatalog.HitTarget).Alpha, Is.Zero);
                    Assert.That(partOwner(secondFirstTargetParts, GameplaySkinSlotCatalog.JudgementLine).Alpha, Is.Zero);
                    Assert.That(partOwner(secondSecondTargetParts, GameplaySkinSlotCatalog.HitTarget).Alpha, Is.GreaterThan(0),
                        "Stage JudgementLine authoring must not hide the sibling lane HitTarget fallback.");
                    Assert.That(partOwner(secondSecondTargetParts, GameplaySkinSlotCatalog.JudgementLine).Alpha, Is.Zero);
                    Assert.That(partOwner(secondUnauthoredTargetParts, GameplaySkinSlotCatalog.HitTarget).Alpha, Is.GreaterThan(0));
                    Assert.That(partOwner(secondUnauthoredTargetParts, GameplaySkinSlotCatalog.JudgementLine).Alpha, Is.Zero);
                    Assert.That(renderer.Drawable.Playfield.Stages[1].ChildrenOfType<UnpartitionedCustomHitTarget>(), Is.Empty,
                        "No animated custom target child may remain mounted behind a selected stage declaration.");
                });

                ((UnpartitionedCustomColumnBackground)firstStageBackground.Drawable).ForceVisibleAfterGate();
                ((UnpartitionedCustomHitTarget)firstStageHitTarget.Drawable).ForceVisibleAfterGate();
                Assert.Multiple(() =>
                {
                    Assert.That(firstStageBackground.Drawable.Alpha, Is.EqualTo(1).Within(0.0001f));
                    Assert.That(firstStageBackground.Drawable.X, Is.Not.Zero);
                    Assert.That(firstStageBackground.Alpha, Is.GreaterThan(0),
                        "A fully programmatic lane must preserve its animated custom fallback.");
                    Assert.That(firstStageHitTarget.Drawable.Alpha, Is.EqualTo(1).Within(0.0001f));
                    Assert.That(firstStageHitTarget.Drawable.X, Is.Not.Zero);
                    Assert.That(firstStageHitTarget.Alpha, Is.GreaterThan(0));
                });

                void assertGate(GameplaySkinResolvedMaterialKey key, GameplaySkinSceneHostRoute route)
                {
                    Assert.That(publication.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    Assert.Multiple(() =>
                    {
                        Assert.That(entry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                        Assert.That(gate!.Route, Is.EqualTo(route));
                        Assert.That(gate.IsReplacementReady, Is.True);
                    });
                }

                static Drawable partOwner(
                    IEnumerable<ManiaGameplaySkinProgrammaticVisualPart> parts,
                    GameplaySkinSlotDescriptor slot)
                    => parts.Single(part => ReferenceEquals(part.Slot, slot)).Owner;
            });
            AddStep("prove provider registration failure rolls back transactionally", () =>
            {
                Column column = renderer.Drawable.Playfield.Stages[0].Columns[0];
                MethodInfo method = typeof(Column).GetMethod("registerProviderProgrammaticVisualParts", BindingFlags.Instance | BindingFlags.NonPublic)!;
                List<IDisposable> registrations = getPrivateField<List<IDisposable>>(column, "programmaticVisualPartRegistrations");
                HashSet<Drawable> owners = getPrivateField<HashSet<Drawable>>(column, "registeredProgrammaticVisualPartOwners");
                int registrationCount = registrations.Count;
                int ownerCount = owners.Count;
                var provider = new FaultingProgrammaticPartProvider();

                TargetInvocationException? exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(column, new object[] { provider }));

                Assert.Multiple(() =>
                {
                    Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(registrations, Has.Count.EqualTo(registrationCount));
                    Assert.That(owners, Has.Count.EqualTo(ownerCount));
                    Assert.That(owners, Does.Not.Contain(provider.ValidOwner));
                    Assert.That(provider.ValidOwner.Alpha, Is.EqualTo(1).Within(0.0001f));
                });

                provider.UseValidPartsOnly();
                Assert.DoesNotThrow(() => method.Invoke(column, new object[] { provider }));
                Assert.Multiple(() =>
                {
                    Assert.That(registrations, Has.Count.EqualTo(registrationCount + 1));
                    Assert.That(owners, Has.Count.EqualTo(ownerCount + 1));
                    Assert.That(owners, Does.Contain(provider.ValidOwner));
                });
            });
            AddStep("detach custom-fallback renderer", () => renderer.Expire());
            AddUntilStep("wait for custom-fallback detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestDualStageGaugeAndGlobalTextSuppressHideOnlyTheirRealCoreHudOwners()
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;

            AddStep("create isolated dual-stage HUD suppress skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });
            AddStep("create and select dual-stage HUD suppress package", () =>
            {
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-dual-hud-suppress-{Guid.NewGuid():N}");
                writeDualStageHudSuppressPackage(packageRoot);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for dual-stage HUD suppress exact revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount exact dual-stage HUD suppress renderer", () =>
                Add(renderer = new CurrentRevisionManiaMaterialHost(publicMaterialSkinManager!, dualStage: true)));
            AddUntilStep("wait for exact dual-stage HUD suppress ruleset", () => renderer.Drawable.IsLoaded);
            AddUntilStep("wait for exact dual-stage HUD suppress topology", () => renderer.Drawable.Playfield.Stages.Count == 2);
            AddStep("activate both real HUD suppress stages", () => renderer.AddProductionBarLine(1_150));
            AddUntilStep("wait for exact dual-stage HUD suppress stages", () =>
                renderer.Drawable.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
            AddStep("capture dual-stage HUD suppress scene host", () =>
                sceneHost = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for dual-stage HUD suppress scene readiness", () => sceneHost.PendingCreationCount == 0);
            AddStep("mount real dual-stage core HUD owners", () => renderer.AddProductionCoreHud());
            AddUntilStep("wait for suppressed core HUD registrations", () =>
                renderer.CoreHud?.IsLoaded == true
                && renderer.CoreHud.GameplaySkinGaugeOwners.Count > 0
                && renderer.CoreHud.GameplaySkinTextOwners.Count > 0);
            AddStep("assert all-stage gauge and text suppression", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinResolvedMaterialKey[] gaugeKeys = publication.Snapshot.Context.Topology.GroupsInLogicalOrder
                    .Select(group => new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.GaugeVisual,
                        GameplaySkinResolvedMaterialTarget.ForStage(group)))
                    .ToArray();
                var textKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.TextHud,
                    GameplaySkinResolvedMaterialTarget.Global);
                GameplaySkinResolvedMaterialKey[] stageTextKeys = publication.Snapshot.Context.Topology.GroupsInLogicalOrder
                    .Select(group => new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.TextHud,
                        GameplaySkinResolvedMaterialTarget.ForStage(group)))
                    .ToArray();
                GameplaySkinResolvedMaterialEntry entryFor(GameplaySkinResolvedMaterialKey key)
                    => publication.MaterialSet.Entries.Single(entry => entry.Key.Equals(key));

                Assert.Multiple(() =>
                {
                    Assert.That(gaugeKeys, Has.Length.EqualTo(2));
                    Assert.That(gaugeKeys.Select(key => entryFor(key).State),
                        Is.All.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(gaugeKeys.Select(key =>
                    {
                        Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                        return gate!.Route;
                    }), Is.All.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(renderer.CoreHud!.GameplaySkinGaugeOwners.All(owner => owner.Alpha == 0), Is.True,
                        "Every exact stage gauge suppression must hide only its corresponding real health-display partition.");
                    Assert.That(entryFor(textKey).State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(sceneHost.TryGetVisualGate(textKey, out GameplaySkinSceneHostedSlot? textGate), Is.True);
                    Assert.That(textGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(stageTextKeys.Select(key => entryFor(key).State),
                        Is.All.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(renderer.CoreHud.GameplaySkinTextOwners.All(owner => owner.Alpha == 0), Is.True,
                        "Global and stage TextHud suppression must hide every exact real score/accuracy/progress/hit-error partition.");
                    assertExactHudPartitions(renderer.CoreHud, sceneHost, publication.Snapshot, GameplaySkinSlotCatalog.GaugeVisual,
                        renderer.CoreHud.GameplaySkinGaugePartitions);
                    assertExactHudPartitions(renderer.CoreHud, sceneHost, publication.Snapshot, GameplaySkinSlotCatalog.TextHud,
                        renderer.CoreHud.GameplaySkinTextPartitions);
                    Assert.That(renderer.CoreHud.GameplaySkinHudResidualPartitions
                                                  .Where(partition => ReferenceEquals(partition.Slot, GameplaySkinSlotCatalog.GaugeVisual)
                                                                      || ReferenceEquals(partition.Slot, GameplaySkinSlotCatalog.TextHud))
                                                  .All(partition => partition.Owner.Alpha == 0), Is.True,
                        "Once every exact stage (or global TextHud) is suppressed, no compatibility HUD remainder may stay visible.");
                });
            });
            AddStep("detach dual-stage HUD suppress renderer", () => renderer.Expire());
            AddUntilStep("wait for dual-stage HUD suppress detach", () => renderer.Parent == null);
        }

        [TestCase(-1)]
        [TestCase(0)]
        public void TestDualStageCoreHudPartitionsRetainFullFallbackOrHideOnlyAuthoredFirstStage(int authoredStageIndex)
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;

            AddStep("create isolated dual-stage HUD partition skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });
            AddStep("create and select dual-stage HUD partition package", () =>
            {
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-dual-hud-partition-{Guid.NewGuid():N}");
                writeDualStageHudPartitionPackage(packageRoot, authoredStageIndex);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for dual-stage HUD partition exact revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount exact dual-stage HUD partition renderer", () =>
                Add(renderer = new CurrentRevisionManiaMaterialHost(publicMaterialSkinManager!, dualStage: true)));
            AddUntilStep("wait for exact dual-stage HUD partition ruleset", () => renderer.Drawable.IsLoaded);
            AddUntilStep("wait for exact dual-stage HUD partition stages", () =>
                renderer.Drawable.Playfield.Stages.Count == 2
                && renderer.Drawable.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
            AddStep("capture dual-stage HUD partition scene host", () =>
                sceneHost = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for dual-stage HUD partition scene readiness", () => sceneHost.PendingCreationCount == 0);
            AddStep("mount exact dual-stage core HUD partitions", () => renderer.AddProductionCoreHud());
            AddUntilStep("wait for exact dual-stage core HUD partitions", () =>
                renderer.CoreHud?.IsLoaded == true
                && (authoredStageIndex < 0
                    ? renderer.CoreHud.ChildrenOfType<DefaultHealthDisplay>().Any()
                      && renderer.CoreHud.ChildrenOfType<DefaultScoreCounter>().Any()
                    : renderer.CoreHud.GameplaySkinGaugePartitions.Count > 0
                      && renderer.CoreHud.GameplaySkinTextPartitions.Count > 0));
            AddStep("assert exact non-overlapping HUD partition authority", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinLaneTopologyGroup[] groups = publication.Snapshot.Context.Topology.GroupsInLogicalOrder.ToArray();
                var globalTextKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.TextHud,
                    GameplaySkinResolvedMaterialTarget.Global);
                HUDOverlay coreHud = renderer.CoreHud!;

                Assert.That(groups, Has.Length.EqualTo(2));

                if (authoredStageIndex < 0)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(coreHud.GameplaySkinGaugePartitions, Is.Empty);
                        Assert.That(coreHud.GameplaySkinTextPartitions, Is.Empty);
                        Assert.That(coreHud.GameplaySkinHudResidualPartitions, Is.Empty);
                        Assert.That(coreHud.ChildrenOfType<DefaultHealthDisplay>().Single().Alpha, Is.GreaterThan(0));
                        Assert.That(coreHud.ChildrenOfType<DefaultScoreCounter>().Single().Alpha, Is.GreaterThan(0));
                    });
                    return;
                }

                assertExactHudPartitions(coreHud, sceneHost, publication.Snapshot, GameplaySkinSlotCatalog.GaugeVisual,
                    coreHud.GameplaySkinGaugePartitions);
                assertExactHudPartitions(coreHud, sceneHost, publication.Snapshot, GameplaySkinSlotCatalog.TextHud,
                    coreHud.GameplaySkinTextPartitions);

                for (int stageIndex = 0; stageIndex < groups.Length; stageIndex++)
                {
                    var gaugeKey = new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.GaugeVisual,
                        GameplaySkinResolvedMaterialTarget.ForStage(groups[stageIndex]));
                    var textKey = new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.TextHud,
                        GameplaySkinResolvedMaterialTarget.ForStage(groups[stageIndex]));
                    bool authored = stageIndex == authoredStageIndex;
                    GameplaySkinHudProgrammaticVisualPartition[] gaugePartitions = coreHud.GameplaySkinGaugePartitions
                        .Where(partition => partition.StageKey.Equals(gaugeKey)).ToArray();
                    GameplaySkinHudProgrammaticVisualPartition[] textPartitions = coreHud.GameplaySkinTextPartitions
                        .Where(partition => partition.StageKey.Equals(textKey)).ToArray();

                    Assert.That(sceneHost.TryGetVisualGate(gaugeKey, out GameplaySkinSceneHostedSlot? gaugeGate), Is.True);
                    Assert.That(sceneHost.TryGetVisualGate(textKey, out GameplaySkinSceneHostedSlot? textGate), Is.True);
                    Assert.That(gaugePartitions, Is.Not.Empty);
                    Assert.That(textPartitions, Is.Not.Empty);
                    Assert.That(gaugeGate!.Route, Is.EqualTo(authored ? GameplaySkinSceneHostRoute.Suppressed : GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(textGate!.Route, Is.EqualTo(authored ? GameplaySkinSceneHostRoute.Suppressed : GameplaySkinSceneHostRoute.Programmatic));
                    Assert.That(gaugePartitions.All(partition => authored ? partition.Owner.Alpha == 0 : partition.Owner.Alpha > 0), Is.True);
                    Assert.That(textPartitions.All(partition => authored ? partition.Owner.Alpha == 0 : partition.Owner.Alpha > 0), Is.True);
                }

                Assert.That(sceneHost.TryGetVisualGate(globalTextKey, out GameplaySkinSceneHostedSlot? globalTextGate), Is.True);
                Assert.That(globalTextGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));

            });
            AddStep("detach dual-stage HUD partition renderer", () => renderer.Expire());
            AddUntilStep("wait for dual-stage HUD partition detach", () => renderer.Parent == null);
        }

        [TestCase("stage.background", false)]
        [TestCase("playfield.backdrop", false)]
        [TestCase("playfield.baseplate", false)]
        [TestCase("stage.background", true)]
        [TestCase("playfield.backdrop", true)]
        [TestCase("playfield.baseplate", true)]
        public void TestStageShellSlotsReplaceOnlyTheExactNativeShell(string slotId, bool dualStage)
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionManiaMaterialHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;

            AddStep("create isolated stage-shell skin manager", () =>
            {
                Directory.CreateDirectory(LocalStorage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY));
                publicMaterialSkinManager = new SkinManager(LocalStorage, Realm, gameHost, Resources, Audio, Scheduler);
            });
            AddStep("create and select one exact stage-shell package", () =>
            {
                string packageRoot = LocalStorage.GetFullPath($"realm-mania-stage-shell-{Guid.NewGuid():N}");
                writeStageShellPackage(packageRoot, slotId, dualStage);
                candidate = createPublicMaterialRealmCandidate(packageRoot);
                publicMaterialSkinManager!.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for stage-shell exact revision", () =>
                publicMaterialSkinManager!.CurrentSkinInfo.Value.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && publicMaterialSkinManager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(publicMaterialSkinManager.CurrentRevision.Owner, publicMaterialSkinManager.CurrentSkin.Value));
            AddStep("mount exact stage-shell renderer", () =>
            {
                Add(renderer = new CurrentRevisionManiaMaterialHost(publicMaterialSkinManager!, dualStage, new Mod[] { new ManiaModHidden() }));
                renderer.AddProductionBarLine(1_150);
            });
            AddUntilStep("wait for exact stage-shell production tree", () =>
                renderer.Drawable.IsLoaded
                && renderer.Drawable.Playfield.Stages.Count == (dualStage ? 2 : 1)
                && renderer.Drawable.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
            AddStep("capture mounted stage-shell scene host", () =>
                sceneHost = renderer.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddStep("run first production scene frame", () => sceneHost.ProcessFrame());
            AddUntilStep("wait for bounded stage-shell semantic host", () => sceneHost.PendingCreationCount == 0);
            AddStep("assert only the authored stage native shell is replaced", () =>
            {
                Assert.That(GameplaySkinSlotCatalog.TryGet(slotId, out GameplaySkinSlotDescriptor? slot), Is.True);
                GameplaySkinLayoutPublication publication = renderer.Drawable.LayoutRevisionOwner.CurrentPublication!;
                int authoredStageIndex = dualStage ? 1 : 0;
                GameplaySkinLaneTopologyGroup group = publication.Snapshot.Context.Topology.GroupsInLogicalOrder[authoredStageIndex];
                var key = new GameplaySkinResolvedMaterialKey(slot!, GameplaySkinResolvedMaterialTarget.ForStage(group));
                Stage stage = renderer.Drawable.Playfield.Stages[authoredStageIndex];
                SkinnableDrawable wrapper = getPrivateField<SkinnableDrawable>(stage, "stageBackground");
                ManiaGameplaySkinProgrammaticVisualPart[] parts =
                    ((IManiaGameplaySkinProgrammaticVisualPartProvider)wrapper.Drawable).GameplaySkinProgrammaticVisualParts.ToArray();
                Drawable[] shellOwners = parts.Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.StageBackground))
                                              .Select(part => part.Owner).ToArray();
                Drawable[] backdropOwners = parts.Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop))
                                                 .Select(part => part.Owner).ToArray();
                Drawable[] baseplateOwners = parts.Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBaseplate))
                                                  .Select(part => part.Owner).ToArray();
                Drawable[] independentLaneAndTargetOwners = parts.Where(part =>
                    ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneSurface)
                    || ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneDivider)
                    || ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.HitTarget)
                    || ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.JudgementLine))
                                                                 .Select(part => part.Owner).ToArray();

                Assert.That(publication.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                Assert.That(sceneHost.TryGetHostedDrawable(key, out Drawable? authored), Is.True);
                GameplaySkinSceneLayer expectedLayer = ReferenceEquals(slot, GameplaySkinSlotCatalog.PlayfieldBaseplate)
                    ? GameplaySkinSceneLayer.Underlay
                    : GameplaySkinSceneLayer.Background;

                Assert.Multiple(() =>
                {
                    Assert.That(entry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(entry.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(gate.PreparedRect, Is.EqualTo(publication.PreparedScene.HostedSlots.Single(route => route.Key.Equals(key)).Rect));
                    Assert.That(gate.Layer, Is.EqualTo(expectedLayer));
                    Assert.That(authored!.Alpha, Is.GreaterThan(0));
                    Assert.That(isDescendantOf(authored, sceneHost.Layers.Get(expectedLayer)), Is.True);
                    Assert.That(wrapper.Alpha, Is.GreaterThan(0),
                        "The aggregate legacy background wrapper must remain visible for independently owned lane and target fallback parts.");
                    Assert.That(shellOwners, Is.Not.Empty);
                    Assert.That(backdropOwners, Is.Not.Empty);
                    Assert.That(baseplateOwners, Is.Not.Empty);
                    Assert.That(shellOwners.All(owner => owner.Alpha == (ReferenceEquals(slot, GameplaySkinSlotCatalog.StageBackground) ? 0 : 1)), Is.True,
                        "StageBackground must not share one native gate owner with PlayfieldBackdrop or Baseplate.");
                    Assert.That(backdropOwners.All(owner => owner.Alpha == (ReferenceEquals(slot, GameplaySkinSlotCatalog.PlayfieldBackdrop) ? 0 : 1)), Is.True,
                        "PlayfieldBackdrop must not share one native gate owner with StageBackground or Baseplate.");
                    Assert.That(baseplateOwners.All(owner => owner.Alpha == (ReferenceEquals(slot, GameplaySkinSlotCatalog.PlayfieldBaseplate) ? 0 : 1)), Is.True,
                        "A baseplate replacement must affect only the exact native baseplate owner.");
                    Assert.That(independentLaneAndTargetOwners, Is.Not.Empty);
                    Assert.That(independentLaneAndTargetOwners.All(owner => owner.Alpha > 0), Is.True,
                        "A stage-shell Provide must not suppress neighbouring lane/divider/hit-target/judgement-line fallback owners.");
                });

                if (dualStage)
                {
                    SkinnableDrawable otherWrapper = getPrivateField<SkinnableDrawable>(renderer.Drawable.Playfield.Stages[0], "stageBackground");
                    Drawable[] otherShellOwners = ((IManiaGameplaySkinProgrammaticVisualPartProvider)otherWrapper.Drawable)
                                                  .GameplaySkinProgrammaticVisualParts
                                                  .Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.StageBackground))
                                                  .Select(part => part.Owner).ToArray();
                    Drawable[] otherBaseplateOwners = ((IManiaGameplaySkinProgrammaticVisualPartProvider)otherWrapper.Drawable)
                                                      .GameplaySkinProgrammaticVisualParts
                                                      .Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBaseplate))
                                                      .Select(part => part.Owner).ToArray();
                    Drawable[] otherBackdropOwners = ((IManiaGameplaySkinProgrammaticVisualPartProvider)otherWrapper.Drawable)
                                                     .GameplaySkinProgrammaticVisualParts
                                                     .Where(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop))
                                                     .Select(part => part.Owner).ToArray();
                    Assert.Multiple(() =>
                    {
                        Assert.That(otherShellOwners.All(owner => owner.Alpha > 0), Is.True,
                            "A stage-two shell replacement must not cross into the neighbouring native stage.");
                        Assert.That(otherBackdropOwners, Is.Not.Empty);
                        Assert.That(otherBackdropOwners.All(owner => owner.Alpha > 0), Is.True,
                            "A stage-two backdrop replacement must not cross into the neighbouring native stage.");
                        Assert.That(otherBaseplateOwners, Is.Not.Empty);
                        Assert.That(otherBaseplateOwners.All(owner => owner.Alpha > 0), Is.True,
                            "A stage-two baseplate replacement must not cross into the neighbouring native stage.");
                    });
                }
            });
            AddStep("detach stage-shell renderer", () => renderer.Expire());
            AddUntilStep("wait for stage-shell detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestExactRulesetProviderPublishesOneSnapshotForProductionTree()
        {
            GameplaySkinEventSubscription eventSubscription = null!;
            var observedEvents = new List<GameplaySkinEventEnvelope>();
            BarLine sharedBarLine = null!;
            DrawableBarLine[] sharedBarLineDrawables = null!;

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
                                                                     && drawableRuleset.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
            AddStep("attach exact dual-stage event consumer", () =>
            {
                eventSubscription = drawableRuleset.GameplaySkinEventStream.Subscribe();
                eventSubscription.DrainFrame(observedEvents.Add);
                Assert.That(observedEvents.Single().DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
            });
            AddStep("press first real column on stage two", () =>
            {
                Stage secondStage = drawableRuleset.Playfield.Stages[1];
                ManiaInputManager[] inputManagers = drawableRuleset.ChildrenOfType<ManiaInputManager>().ToArray();
                Assert.That(inputManagers, Is.Not.Empty);

                foreach (ManiaInputManager inputManager in inputManagers)
                    inputManager.KeyBindingContainer.TriggerPressed(secondStage.Columns[0].Action.Value);
            });
            AddUntilStep("stage-two input edge has global stable target", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.InputPressed
                                                      && envelope.GroupId?.Value == "mania.group.stage-2"
                                                      && envelope.LaneId?.Value == "mania.lane.column-5");
            });
            AddStep("release stage-two column", () =>
            {
                Stage secondStage = drawableRuleset.Playfield.Stages[1];

                foreach (ManiaInputManager inputManager in drawableRuleset.ChildrenOfType<ManiaInputManager>())
                    inputManager.KeyBindingContainer.TriggerReleased(secondStage.Columns[0].Action.Value);
            });
            AddAssert("provider package is exact", () => drawableRuleset.LayoutSnapshot.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility);
            AddAssert("owner publication adapter and root share one exact reference", () =>
                ReferenceEquals(drawableRuleset.LayoutRevisionOwner.CurrentPublication?.Snapshot, drawableRuleset.LayoutSnapshot)
                && ReferenceEquals(
                    drawableRuleset.LayoutRevisionOwner.CurrentPublication?.GetAdapter<ManiaGameplaySkinLayout>(),
                    drawableRuleset.LayoutAdapter)
                && ReferenceEquals(drawableRuleset.LayoutAdapter.Snapshot, drawableRuleset.LayoutSnapshot)
                && ReferenceEquals(drawableRuleset.LayoutRevisionOwner.CurrentPublication?.MaterialSet.Snapshot, drawableRuleset.LayoutSnapshot)
                && drawableRuleset.LayoutRevisionOwner.CurrentPublication?.MaterialSet.IsEmpty == false
                && drawableRuleset.LayoutRevisionOwner.CurrentPublication?.MaterialSet.Entries.Count
                == GameplaySkinSlotCatalog.Common.Where(GameplaySkinRuntimeSupportProfile.Mania.IsSupported).Sum(descriptor =>
                    GameplaySkinPublicSlotMaterialTargets.Enumerate(descriptor, drawableRuleset.LayoutSnapshot).Count));
            AddStep("prepare is background", () => Assert.That(drawableRuleset.LayoutRevisionOwner.LastPrepareWasUpdateThread, Is.False));
            AddStep("commit is update thread", () => Assert.That(drawableRuleset.LayoutRevisionOwner.LastCommitWasUpdateThread, Is.True));
            GameplaySkinLayoutPublication firstPublication = null!;
            AddStep("same root double prepare fails closed", () =>
            {
                firstPublication = drawableRuleset.LayoutRevisionOwner.CurrentPublication!;
                Assert.That(
                    () => productionRuleset.PrepareGameplaySkinLayout(
                        productionBeatmap,
                        productionContent.CapturedDependencies,
                        CancellationToken.None),
                    Throws.InvalidOperationException.With.Message.Contains("exactly one immutable layout"));
            });
            AddAssert("failed double prepare retains exact publication", () =>
                ReferenceEquals(firstPublication, drawableRuleset.LayoutRevisionOwner.CurrentPublication)
                && firstPublication.Snapshot.Context.LayoutRevision == drawableRuleset.LayoutSnapshot.Context.LayoutRevision);
            AddAssert("native dual vector retained", () => drawableRuleset.LayoutSnapshot.Context.NativeContextId == "stages-4-5");
            AddAssert("root playfield stages and columns share exact snapshot", () =>
                ReferenceEquals(drawableRuleset.LayoutSnapshot, drawableRuleset.Playfield.LayoutSnapshot)
                && ReferenceEquals(drawableRuleset.ResolvedMaterialSet, drawableRuleset.Playfield.ResolvedMaterialSet)
                && drawableRuleset.Playfield.Stages.All(stage =>
                    ReferenceEquals(stage.LayoutSnapshot, drawableRuleset.LayoutSnapshot)
                    && ReferenceEquals(stage.ResolvedMaterialSet, drawableRuleset.ResolvedMaterialSet)
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
                        || Math.Abs(stageBounds.Left - expectedStageLeft) > tolerance
                        || Math.Abs(stageBounds.Width - expectedStageWidth) > tolerance)
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
                            || Math.Abs(columnBounds.Left - expectedColumnLeft) > tolerance
                            || Math.Abs(columnBounds.Width - expectedColumnWidth) > tolerance)
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

                productionContent.Add(new HudDependenciesContainer(drawableRuleset.ScrollingInfo, scoreProcessor, drawableRuleset)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = productionHud,
                });
            });
            AddUntilStep("production mania HUD loaded", () => productionHud.IsLoaded);
            AddAssert("HUD wrapper shares exact snapshot", () => ReferenceEquals(productionHud.LayoutSnapshot, drawableRuleset.LayoutSnapshot));
            AddAssert("HUD wrapper shares exact material publication", () =>
                ReferenceEquals(productionHud.ResolvedMaterialSet, drawableRuleset.ResolvedMaterialSet));
            AddAssert("combo geometry comes from exact snapshot surface", () =>
            {
                Drawable[] combos = productionHud.Children
                                                 .Where(child => child is OmsManiaComboCounter or LegacyManiaComboCounter)
                                                 .ToArray();
                GameplaySkinLayoutSnapshot snapshot = drawableRuleset.LayoutSnapshot;
                GameplaySkinLayoutRect surface = snapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect;

                return combos.Length == snapshot.Context.Topology.GroupsInLogicalOrder.Count
                       && combos.Select((combo, stageIndex) => (combo, stageIndex)).All(pair =>
                       {
                           GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder[pair.stageIndex];
                           return pair.combo.RelativePositionAxes == Axes.Both
                                  && Math.Abs(pair.combo.X - (snapshot.GetGroup(group.Identity.Id).Rect.Left
                                                               + snapshot.GetGroup(group.Identity.Id).Rect.Width / 2)) < 0.001f
                                  && Math.Abs(pair.combo.Y - (surface.Top + surface.Height / 2)) < 0.001f;
                       });
            });
            AddStep("add production note hold and barline", () =>
            {
                var note = new Note { Column = 0, StartTime = Time.Current + 1000 };
                note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                drawableRuleset.Playfield.Add(productionNote = new DrawableNote(note));

                var hold = new HoldNote { Column = 8, StartTime = Time.Current + 1200, Duration = 1000 };
                hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                drawableRuleset.Playfield.Add(productionHold = new DrawableHoldNote(hold));
                drawableRuleset.Playfield.Add(sharedBarLine = new BarLine { StartTime = Time.Current + 500 });
            });
            AddUntilStep("production note hold and barline loaded", () =>
            {
                sharedBarLineDrawables = this.ChildrenOfType<DrawableBarLine>()
                                             .Where(line => ReferenceEquals(line.HitObject, sharedBarLine))
                                             .ToArray();

                return productionNote.IsLoaded
                       && productionHold.IsLoaded
                       && sharedBarLineDrawables.Length == 2
                       && sharedBarLineDrawables.All(line => line.IsLoaded);
            });
            AddStep("objects and barline share exact snapshot", () => Assert.Multiple(() =>
            {
                Assert.That(productionNote.LayoutSnapshot, Is.SameAs(drawableRuleset.LayoutSnapshot), "production note");
                Assert.That(productionHold.LayoutSnapshot, Is.SameAs(drawableRuleset.LayoutSnapshot), "production hold");
                Assert.That(sharedBarLineDrawables.All(line => ReferenceEquals(line.StageLayoutSnapshot, drawableRuleset.LayoutSnapshot)),
                    Is.True,
                    "per-stage production bar line usages");
            }));
            AddStep("shared barline uses two exact pooled stage-local specialised consumers", () =>
            {
                GameplaySkinLayoutPublication publication = drawableRuleset.LayoutRevisionOwner.CurrentPublication!;
                GameplaySkinSceneRuntimeHost sceneHost = drawableRuleset.GameplaySkinSceneRuntime;

                Assert.Multiple(() =>
                {
                    Assert.That(sceneHost.Publication, Is.SameAs(publication));
                    Assert.That(sceneHost.EventStream, Is.SameAs(drawableRuleset.GameplaySkinEventStream));
                    Assert.That(sharedBarLineDrawables, Has.Length.EqualTo(2));
                });

                for (int stageIndex = 0; stageIndex < drawableRuleset.Playfield.Stages.Count; stageIndex++)
                {
                    Stage stage = drawableRuleset.Playfield.Stages[stageIndex];
                    DrawableBarLine line = sharedBarLineDrawables.Single(candidate => isDescendantOf(candidate, stage.HitObjectContainer));
                    GameplaySkinLaneTopologyGroup group = publication.Snapshot.Context.Topology.GroupsInLogicalOrder[stageIndex];
                    var key = new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.BarLine,
                        GameplaySkinResolvedMaterialTarget.ForGroup(group));

                    Assert.That(publication.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);

                    Assert.Multiple(() =>
                    {
                        Assert.That(entry!.Material, Is.TypeOf<GameplaySkinPublicSlotMaterial>());
                        Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                        Assert.That(gate.RoutedNodes, Is.Empty);
                        Assert.That(gate.IsReplacementReady, Is.False);
                        Assert.That(sceneHost.TryGetHostedDrawable(key, out _), Is.False,
                            "A shared root overlay must never replace either real stage-local BarLine usage.");
                        Assert.That(((PoolableDrawable)line).IsInPool, Is.True);
                        Assert.That(isDescendantOf(line, stage.HitObjectContainer), Is.True);
                        Assert.That(line.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>(), Is.Empty);
                        Assert.That(line.ChildrenOfType<SkinnableDrawable>().Single().Alpha, Is.GreaterThan(0),
                            "A ProgrammaticFallback material must keep the real engine BarLine visible.");
                    });

                    assertSpecialisedSceneConsumer(line, publication.MaterialSet, key, gate!);
                }
            });
            AddUntilStep("shared barline produces one spawn edge per real stage usage", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Count(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectSpawned
                                                        && envelope.Payload is GameplaySkinObjectEventPayload
                                                        {
                                                            State.Kind: GameplaySkinObjectKind.BarLine
                                                        }) == 2;
            });
            AddStep("assert shared barline stage identities are distinct and exact", () =>
            {
                GameplaySkinEventEnvelope[] spawns = observedEvents.Where(envelope =>
                    envelope.EventKind == GameplaySkinEventKind.ObjectSpawned
                    && envelope.Payload is GameplaySkinObjectEventPayload
                    {
                        State.Kind: GameplaySkinObjectKind.BarLine
                    }).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(spawns.Select(envelope => envelope.GroupId?.Value),
                        Is.EquivalentTo(new[] { "mania.group.stage-1", "mania.group.stage-2" }));
                    Assert.That(spawns.Select(envelope => ((GameplaySkinObjectEventPayload)envelope.Payload).State.ObjectId).Distinct().Count(),
                        Is.EqualTo(2),
                        "One shared BarLine hit object has two production stage usages and therefore requires two stable event object IDs.");
                    Assert.That(spawns.All(envelope => envelope.LaneId == null
                                                       && ((GameplaySkinObjectEventPayload)envelope.Payload).State.LaneId == null), Is.True);
                    Assert.That(spawns.All(envelope => envelope.Revision == firstPublication.EventRevision), Is.True);
                });
            });
            AddUntilStep("shared barline produces matching despawn edges", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Count(envelope => envelope.EventKind == GameplaySkinEventKind.ObjectDespawned
                                                        && envelope.Payload is GameplaySkinObjectEventPayload
                                                        {
                                                            State.Kind: GameplaySkinObjectKind.BarLine
                                                        }) == 2;
            });
            AddStep("assert shared barline despawns preserve per-stage object identity", () =>
            {
                GameplaySkinObjectStateSnapshot[] spawns = observedEvents.Where(envelope =>
                        envelope.EventKind == GameplaySkinEventKind.ObjectSpawned
                        && envelope.Payload is GameplaySkinObjectEventPayload
                        {
                            State.Kind: GameplaySkinObjectKind.BarLine
                        })
                    .Select(envelope => ((GameplaySkinObjectEventPayload)envelope.Payload).State)
                    .ToArray();
                GameplaySkinObjectStateSnapshot[] despawns = observedEvents.Where(envelope =>
                        envelope.EventKind == GameplaySkinEventKind.ObjectDespawned
                        && envelope.Payload is GameplaySkinObjectEventPayload
                        {
                            State.Kind: GameplaySkinObjectKind.BarLine
                        })
                    .Select(envelope => ((GameplaySkinObjectEventPayload)envelope.Payload).State)
                    .ToArray();

                Assert.That(despawns.Select(state => (state.GroupId, state.ObjectId)),
                    Is.EquivalentTo(spawns.Select(state => (state.GroupId, state.ObjectId))));
            });
            AddAssert("real note and hold consume exact prepared material set", () =>
                ReferenceEquals(productionNote.ResolvedMaterialSet, firstPublication.MaterialSet)
                && productionNote.ResolvedMaterialKey?.Target.LaneId?.Equals(drawableRuleset.Playfield.GetColumn(productionNote.HitObject.Column).LayoutLaneId) == true
                && ReferenceEquals(productionHold.ResolvedMaterialSet, firstPublication.MaterialSet)
                && productionHold.ResolvedMaterialKey?.Slot == GameplaySkinSlotCatalog.LongNoteBody
                && productionHold.ResolvedMaterialKey?.Target.LaneId?.Equals(drawableRuleset.Playfield.GetColumn(productionHold.HitObject.Column).LayoutLaneId) == true
                && ReferenceEquals(productionHold.Head.ResolvedMaterialSet, firstPublication.MaterialSet)
                && productionHold.Head.ResolvedMaterialKey?.Slot == GameplaySkinSlotCatalog.LongNoteHead
                && ReferenceEquals(productionHold.Tail.ResolvedMaterialSet, firstPublication.MaterialSet)
                && productionHold.Tail.ResolvedMaterialKey?.Slot == GameplaySkinSlotCatalog.LongNoteTail);
            AddStep("hit targets and core adjustment share exact snapshot", () =>
            {
                HitPositionPaddedContainer[] targets = drawableRuleset.Playfield.Stages
                                                                    .SelectMany(stage => stage.ChildrenOfType<HitPositionPaddedContainer>())
                                                                    .ToArray();
                ManiaPlayfieldAdjustmentContainer[] adjustments = drawableRuleset.ChildrenOfType<ManiaPlayfieldAdjustmentContainer>().ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(targets, Is.Not.Empty);
                    Assert.That(targets.All(target => ReferenceEquals(target.LayoutSnapshot, drawableRuleset.LayoutSnapshot)),
                        Is.True,
                        "stage-local hit-target padding");
                    Assert.That(adjustments, Is.Not.Empty);
                    Assert.That(adjustments.All(core => ReferenceEquals(core.LayoutSnapshot, drawableRuleset.LayoutSnapshot)),
                        Is.True,
                        "root adjustment layout");
                    Assert.That(adjustments.All(core => ReferenceEquals(core.ResolvedMaterialSet, drawableRuleset.ResolvedMaterialSet)),
                        Is.True,
                        "root adjustment material");
                });
            });

            AddStep("publish real stage judgement", () =>
            {
                drawableRuleset.Playfield.Stages[0].OnNewResult(productionNote,
                    new JudgementResult(productionNote.HitObject, new ManiaJudgement()) { Type = HitResult.Perfect });
            });
            AddUntilStep("production judgement loaded", () => drawableRuleset.ChildrenOfType<OmsManiaJudgementPiece>().Any()
                                                                   || drawableRuleset.ChildrenOfType<LegacyManiaJudgementPiece>().Any()
                                                                   || drawableRuleset.ChildrenOfType<DefaultManiaJudgementPiece>().Any());
            AddAssert("judgement shares exact snapshot", () =>
                drawableRuleset.ChildrenOfType<OmsManiaJudgementPiece>().All(piece => ReferenceEquals(piece.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                && drawableRuleset.ChildrenOfType<LegacyManiaJudgementPiece>().All(piece => ReferenceEquals(piece.LayoutSnapshot, drawableRuleset.LayoutSnapshot))
                && drawableRuleset.ChildrenOfType<DefaultManiaJudgementPiece>().All(piece => ReferenceEquals(piece.LayoutSnapshot, drawableRuleset.LayoutSnapshot)));

            AddStep("change direction setting after publication", () => config.SetValue(ManiaRulesetSetting.ScrollDirection, ManiaScrollingDirection.Up));
            AddAssert("root keeps published direction", () => drawableRuleset.LayoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Down
                                                                  && drawableRuleset.PublishedDirection == ScrollingDirection.Down);
            AddStep("detach exact gameplay root", () =>
            {
                eventSubscription.Dispose();
                host.Expire();
            });
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
                       && Math.Abs(piece.X - 0.5f) < 0.001f
                       && Math.Abs(piece.Y - expectedY) < 0.001f;
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
                                                                               && invalidRuleset.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready));
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
            AddAssert("single-stage special key uses its exact stable material target", () =>
            {
                GameplaySkinLaneTopologyGroup group = invalidRuleset.LayoutSnapshot.Context.Topology.GroupsInLogicalOrder.Single();
                GameplaySkinLaneTopologyEntry specialLane = group.LanesInLogicalOrder.Single(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey);
                Column specialColumn = invalidRuleset.Playfield.Stages.Single().Columns[specialLane.GroupLocalLogicalIndex];
                GameplaySkinResolvedMaterialTarget? target = specialColumn.ResolvedMaterialKey?.Target;

                return ReferenceEquals(specialColumn.ResolvedMaterialSet, invalidRuleset.LayoutRevisionOwner.CurrentPublication!.MaterialSet)
                       && target != null
                       && target.LaneId?.Equals(specialLane.Identity.Id) == true
                       && target.GroupId?.Equals(group.Identity.Id) == true
                       && target.GroupLogicalIndex == group.LogicalIndex
                       && target.GroupVisualIndex == group.VisualIndex
                       && target.GlobalLogicalIndex == specialLane.GlobalLogicalIndex
                       && target.GlobalVisualIndex == specialLane.GlobalVisualIndex
                       && target.GroupLocalLogicalIndex == specialLane.GroupLocalLogicalIndex
                       && target.GroupLocalVisualIndex == specialLane.GroupLocalVisualIndex;
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
                        || Math.Abs(flowBounds.Left - stageBounds.Left) > 1
                        || Math.Abs(flowBounds.Width - stageBounds.Width) > 1
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
                        || Math.Abs(piece.X - 0.5f) > 0.001f
                        || Math.Abs(pieceCentreX - stageBounds.Centre.X) > 0.5f
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
                                                                            && argonRuleset.Playfield.Stages.All(stage => stage.LoadState >= LoadState.Ready
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
                            || Math.Abs(layoutLane.Rect.Width - expectedWidth) > 0.001f)
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
                                                                           column.ChildrenOfType<LegacyKeyArea>().Any(key => key.IsLoaded && key.UsesPreparedMaterial)
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
                        LegacyKeyArea keyArea = column.ChildrenOfType<LegacyKeyArea>().Single(key => key.UsesPreparedMaterial);
                        OmsColumnBackground background = column.ChildrenOfType<OmsColumnBackground>().Single();
                        string expectedFallback = topologyLane.Identity.Role == GameplaySkinLaneRole.SpecialKey
                            ? "S"
                            : Math.Min(localIndex, stage.Columns.Length - 1 - localIndex) % 2 == 0 ? "1" : "2";

                        if (!ReferenceEquals(column.LayoutSnapshot, omsRuleset.LayoutSnapshot)
                            || topologyLane.GroupLocalLogicalIndex != localIndex
                            || !column.LayoutLaneId.Equals(topologyLane.Identity.Id)
                             || keyArea.ResolvedFallbackColumnIndex != expectedFallback
                            || !ReferenceEquals(column.ResolvedMaterialSet, omsRuleset.LayoutRevisionOwner.CurrentPublication!.MaterialSet)
                            || column.ResolvedMaterialKey?.Target.LaneId?.Equals(topologyLane.Identity.Id) != true
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
                               && ReferenceEquals(note.ResolvedMaterialSet, drawableRuleset.LayoutRevisionOwner.CurrentPublication!.MaterialSet)
                               && note.ResolvedMaterialKey?.Target.LaneId?.Equals(targetColumn.LayoutLaneId) == true
                               && drawableRuleset.LayoutSnapshot.GetLane(note.LayoutLaneId).TopologyEntry.GlobalLogicalIndex == note.HitObject.Column;
                    }));
            AddStep("detach modded gameplay root", () => host.Expire());
            AddUntilStep("modded gameplay root detached", () => host.Parent == null);
        }

        private Live<SkinInfo> createPublicMaterialRealmCandidate(string sourceRoot)
        {
            var info = new SkinInfo("mania public material Realm package", "OMS tests", typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
            var fileStore = new RealmFileStore(Realm, LocalStorage);

            Realm.Write(realm =>
            {
                realm.Add(info);

                foreach (string path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    using Stream stream = File.OpenRead(path);
                    RealmFile file = fileStore.Add(stream, realm);
                    info.Files.Add(new RealmNamedFileUsage(file, Path.GetRelativePath(sourceRoot, path)));
                }
            });

            return publicMaterialSkinManager!.Query(skin => skin.ID == info.ID);
        }

        private (string PackageRoot, Live<SkinInfo> Candidate) createPublicMaterialManagedCandidate()
        {
            string relativePath = $"chartskin/mania-public-material-{Guid.NewGuid():N}";
            string packageRoot = LocalStorage.GetFullPath(relativePath);
            writePublicManiaMaterialPackage(packageRoot);
            var info = new SkinInfo("mania public material managed folder", "OMS tests", typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
            {
                FilesystemStoragePath = relativePath,
                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
            };

            Realm.Write(realm => realm.Add(info));
            return (packageRoot, publicMaterialSkinManager!.Query(skin => skin.ID == info.ID));
        }

        private string createPublicMaterialExternalPackage()
        {
            string packageRoot = Path.Combine(Path.GetTempPath(), $"oms-mania-public-material-{Guid.NewGuid():N}");
            publicMaterialExternalRoots.Add(packageRoot);
            writePublicManiaMaterialPackage(packageRoot);
            return packageRoot;
        }

        private void deletePublicMaterialExternalRoots()
        {
            foreach (string packageRoot in publicMaterialExternalRoots.ToArray())
            {
                if (Directory.Exists(packageRoot))
                    Directory.Delete(packageRoot, recursive: true);
            }

            publicMaterialExternalRoots.Clear();
        }

        private static void writePublicManiaMaterialPackage(string packageRoot)
        {
            string publicResources = Path.Combine(packageRoot, "public");
            Directory.CreateDirectory(publicResources);
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: mania public common production material\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 4\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 lane=mania.lane.column-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "object.note: resource Provide \"public/note\"\n" +
                "object.long-note.head: resource Provide \"public/head\"\n" +
                "object.long-note.body: resource Provide \"public/body\"\n" +
                 "object.long-note.tail: resource Provide \"public/tail\"\n" +
                 "playfield.key: resource Provide \"public/key\"\n" +
                 "playfield.lane-surface: resource Provide \"public/note\"\n" +
                 "effect.key-flash: resource Provide \"public/note\"\n" +
                 "effect.hit-explosion: resource Provide \"public/note\"\n" +
                 "playfield.hit-target: resource Provide \"public/note\"\n" +
                 "playfield.lane-divider: resource Provide \"public/note\"\n" +
                 "decoration: resource Provide \"public/note\"\n" +
                 "Target: Lane ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 lane=mania.lane.column-2 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                 "playfield.key: resource Suppress\n" +
                 "effect.key-flash: resource Suppress\n" +
                 "Target: Group ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 group-logical=0 group-visual=0\n" +
                 "playfield.bar-line: resource Provide \"public/bar\"\n" +
                 "Target: Stage ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 group-logical=0 group-visual=0\n" +
                 "playfield.judgement-line: resource Provide \"public/note\"\n" +
                 "playfield.lane-cover.fill: resource Provide \"public/note\"\n" +
                 "hud.judgement: resource Provide \"public/note\"\n" +
                 "hud.combo: resource Provide \"public/note\"\n" +
                 "hud.gauge: resource Provide \"public/note\"\n" +
                 "hud.text: resource Provide \"public/note\"\n" +
                 "stage.background: resource Provide \"public/note\"\n" +
                 "stage.foreground: resource Provide \"public/note\"\n" +
                 "playfield.backdrop: resource Provide \"public/note\"\n" +
                 "playfield.baseplate: resource Provide \"public/note\"\n" +
                 "playfield.lane-cover.decoration: resource Provide \"public/note\"\n" +
                 "Target: Global ruleset=mania keymode=4k stage-mode=single\n" +
                 "hud.text: resource Provide \"public/note\"\n");

            File.WriteAllBytes(Path.Combine(publicResources, "note.png"), createPublicMaterialPng(11, 13, new Rgba32(230, 40, 90, 255)));
            File.WriteAllBytes(Path.Combine(publicResources, "head.png"), createPublicMaterialPng(12, 14, new Rgba32(40, 190, 235, 255)));
            File.WriteAllBytes(Path.Combine(publicResources, "body.png"), createPublicMaterialPng(13, 15, new Rgba32(245, 205, 40, 255)));
            File.WriteAllBytes(Path.Combine(publicResources, "tail.png"), createPublicMaterialPng(14, 16, new Rgba32(105, 225, 80, 255)));
            File.WriteAllBytes(Path.Combine(publicResources, "key.png"), createPublicMaterialPng(15, 17, new Rgba32(170, 85, 230, 255)));
            File.WriteAllBytes(Path.Combine(publicResources, "bar.png"), createPublicMaterialPng(16, 18, new Rgba32(75, 155, 250, 255)));
            writeLegacyScoreFont(packageRoot);
            writePublicManiaScene(packageRoot);
        }

        private static void writeDualStagePartialBarLinePackage(string packageRoot)
        {
            string publicResources = Path.Combine(packageRoot, "public");
            Directory.CreateDirectory(publicResources);
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: mania dual-stage partial bar-line\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 9\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 lane=mania.lane.column-5 group-logical=1 group-visual=1 global-logical=4 global-visual=4 group-local-logical=0 group-local-visual=0\n" +
                "playfield.lane-surface: resource Provide \"public/bar\"\n" +
                "effect.key-flash: resource Suppress\n" +
                "Target: Group ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 group-logical=1 group-visual=1\n" +
                "playfield.bar-line: resource Provide \"public/bar\"\n" +
                "Target: Stage ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 group-logical=1 group-visual=1\n" +
                "hud.judgement: resource Suppress\n" +
                "hud.combo: resource Suppress\n" +
                "hud.gauge: resource Provide \"public/bar\"\n" +
                "hud.text: resource Suppress\n");
            File.WriteAllBytes(Path.Combine(publicResources, "bar.png"), createPublicMaterialPng(16, 18, new Rgba32(75, 155, 250, 255)));
            File.WriteAllBytes(Path.Combine(packageRoot, "mania-key1.png"), createPublicMaterialPng(16, 18, new Rgba32(75, 155, 250, 255)));
            writeLegacyScoreFont(packageRoot);
            File.WriteAllText(
                Path.Combine(packageRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-manifest.v1",
                  "scene": "gameplay-skin.scene.json",
                  "sceneContract": "oms-gameplay-skin-scene.v1",
                  "eventContract": "oms-gameplay-skin-event.v1",
                  "resources": [
                    { "id": "texture.bar-line", "type": "texture", "path": "public/bar.png" }
                  ]
                }
                """);
            File.WriteAllText(
                Path.Combine(packageRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-scene.v1",
                  "root": {
                    "id": "node.stage-2-bar-line",
                    "type": "sprite",
                    "target": { "kind": "group", "id": "mania.group.stage-2", "index": 1 },
                    "slot": "playfield.bar-line",
                    "resource": "texture.bar-line",
                    "blend": "alpha",
                    "properties": { "opacity": 1.0, "visible": true },
                    "effects": [],
                    "children": []
                  },
                  "tracks": [],
                  "stateMachines": [],
                  "bindings": [],
                  "templates": [],
                  "instances": []
                }
                """);
        }

        private static void writeDualStageCustomFallbackPackage(string packageRoot)
        {
            string publicResources = Path.Combine(packageRoot, "public");
            Directory.CreateDirectory(publicResources);
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: mania dual-stage custom fallback\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 9\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 lane=mania.lane.column-5 group-logical=1 group-visual=1 global-logical=4 global-visual=4 group-local-logical=0 group-local-visual=0\n" +
                "playfield.lane-surface: resource Provide \"public/custom\"\n" +
                "playfield.lane-divider: resource Provide \"public/custom\"\n" +
                "playfield.hit-target: resource Provide \"public/custom\"\n" +
                "Target: Lane ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 lane=mania.lane.column-6 group-logical=1 group-visual=1 global-logical=5 global-visual=5 group-local-logical=1 group-local-visual=1\n" +
                "effect.key-flash: resource Suppress\n" +
                "Target: Stage ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 group-logical=1 group-visual=1\n" +
                "playfield.judgement-line: resource Provide \"public/custom\"\n");
            File.WriteAllBytes(Path.Combine(publicResources, "custom.png"), createPublicMaterialPng(24, 24, new Rgba32(45, 215, 160, 255)));
            File.WriteAllBytes(Path.Combine(packageRoot, "mania-key1.png"), createPublicMaterialPng(16, 16, new Rgba32(255, 255, 255, 255)));
        }

        private static void writeDualStageHudSuppressPackage(string packageRoot)
        {
            Directory.CreateDirectory(packageRoot);
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: mania dual-stage HUD suppress\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 9\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Stage ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-1 group-logical=0 group-visual=0\n" +
                "hud.gauge: resource Suppress\n" +
                "hud.text: resource Suppress\n" +
                "Target: Stage ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 group-logical=1 group-visual=1\n" +
                "hud.gauge: resource Suppress\n" +
                "hud.text: resource Suppress\n" +
                "Target: Global ruleset=mania keymode=any stage-mode=dual\n" +
                "hud.text: resource Suppress\n");
            writeLegacyScoreFont(packageRoot);
        }

        private static void writeDualStageHudPartitionPackage(string packageRoot, int authoredStageIndex)
        {
            Directory.CreateDirectory(packageRoot);
            string declarations = string.Empty;

            if (authoredStageIndex >= 0)
            {
                int oneBasedStage = authoredStageIndex + 1;
                declarations =
                    "\n[GameplaySkin.Common:1]\n" +
                    $"Target: Stage ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-{oneBasedStage} group-logical={authoredStageIndex} group-visual={authoredStageIndex}\n" +
                    "hud.gauge: resource Suppress\n" +
                    "hud.text: resource Suppress\n";
            }

            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: mania dual-stage HUD partition\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 9\n" +
                declarations);
            writeLegacyScoreFont(packageRoot);
        }

        private static void writeStageShellPackage(string packageRoot, string slotId, bool dualStage)
        {
            string publicResources = Path.Combine(packageRoot, "public");
            Directory.CreateDirectory(publicResources);
            int stageIndex = dualStage ? 1 : 0;
            int oneBasedStage = stageIndex + 1;
            File.WriteAllText(
                Path.Combine(packageRoot, "skin.ini"),
                "[General]\n" +
                "Name: mania exact stage shell\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Mania]\n" +
                $"Keys: {(dualStage ? 9 : 4)}\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                $"Target: Stage ruleset=mania keymode=any stage-mode={(dualStage ? "dual" : "single")} group=mania.group.stage-{oneBasedStage} group-logical={stageIndex} group-visual={stageIndex}\n" +
                $"{slotId}: resource Provide \"public/shell\"\n");
            File.WriteAllBytes(Path.Combine(publicResources, "shell.png"), createPublicMaterialPng(32, 32, new Rgba32(220, 90, 45, 255)));
            File.WriteAllBytes(Path.Combine(packageRoot, "mania-key1.png"), createPublicMaterialPng(16, 16, new Rgba32(255, 255, 255, 255)));
            writeLegacyScoreFont(packageRoot);
        }

        private static void writeLegacyScoreFont(string packageRoot)
        {
            byte[] glyph = createPublicMaterialPng(8, 12, new Rgba32(255, 255, 255, 255));

            for (int digit = 0; digit <= 9; digit++)
                File.WriteAllBytes(Path.Combine(packageRoot, $"score-{digit}.png"), glyph);

            File.WriteAllBytes(Path.Combine(packageRoot, "score-x.png"), glyph);
            File.WriteAllBytes(Path.Combine(packageRoot, "score-percent.png"), glyph);
            File.WriteAllBytes(Path.Combine(packageRoot, "score-dot.png"), glyph);
            writeGameplayHudLayout(packageRoot);
        }

        private static void writeGameplayHudLayout(string packageRoot)
        {
            var layout = new SkinLayoutInfo();
            layout.Update(null, new[]
            {
                new SerialisedDrawableInfo(new DefaultHealthDisplay()),
                new SerialisedDrawableInfo(new DefaultScoreCounter()),
                new SerialisedDrawableInfo(new DefaultAccuracyCounter()),
                new SerialisedDrawableInfo(new DefaultSongProgress()),
                new SerialisedDrawableInfo(new TrianglesPerformancePointsCounter()),
                new SerialisedDrawableInfo(new DefaultKeyCounterDisplay()),
                new SerialisedDrawableInfo(new BPMCounter()),
                new SerialisedDrawableInfo(new ClicksPerSecondCounter()),
                new SerialisedDrawableInfo(new TrianglesUnstableRateCounter()),
                new SerialisedDrawableInfo(new JudgementCounterDisplay()),
                new SerialisedDrawableInfo(new DefaultRankDisplay()),
            });
            File.WriteAllText(
                Path.Combine(packageRoot, $"{GlobalSkinnableContainers.MainHUDComponents}.json"),
                JsonConvert.SerializeObject(layout));
        }

        private static void writePublicManiaScene(string packageRoot)
        {
            File.WriteAllText(
                Path.Combine(packageRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-manifest.v1",
                  "scene": "gameplay-skin.scene.json",
                  "sceneContract": "oms-gameplay-skin-scene.v1",
                  "eventContract": "oms-gameplay-skin-event.v1",
                  "resources": [
                    { "id": "texture.note", "type": "texture", "path": "public/note.png" },
                    { "id": "texture.long-note-head", "type": "texture", "path": "public/head.png" },
                    { "id": "texture.long-note-body", "type": "texture", "path": "public/body.png" },
                    { "id": "texture.long-note-tail", "type": "texture", "path": "public/tail.png" },
                    { "id": "texture.key-visual", "type": "texture", "path": "public/key.png" }
                  ]
                }
                """);
            File.WriteAllText(
                Path.Combine(packageRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-scene.v1",
                  "root": {
                    "id": "node.root",
                    "type": "container",
                    "target": { "kind": "global" },
                    "blend": "inherit",
                    "properties": {},
                    "effects": [],
                    "children": [
                      {
                        "id": "node.lane-surface",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "playfield.lane-surface",
                        "blend": "alpha",
                        "properties": { "opacity": 0.5, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.note",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "object.note",
                        "resource": "texture.note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.75, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-head",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "object.long-note.head",
                        "resource": "texture.long-note-head",
                        "blend": "alpha",
                        "properties": { "opacity": 0.7, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-body",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "object.long-note.body",
                        "resource": "texture.long-note-body",
                        "blend": "alpha",
                        "properties": { "opacity": 0.65, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-tail",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "object.long-note.tail",
                        "resource": "texture.long-note-tail",
                        "blend": "alpha",
                        "properties": { "opacity": 0.6, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.key-visual",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "playfield.key",
                        "resource": "texture.key-visual",
                        "blend": "alpha",
                        "properties": { "opacity": 0.55, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.hit-explosion",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "mania.lane.column-1", "index": 0 },
                        "slot": "effect.hit-explosion",
                        "resource": "texture.note",
                        "blend": "additive",
                        "properties": { "opacity": 0.9, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.bar-line",
                        "type": "sprite",
                        "target": { "kind": "group", "id": "mania.group.stage-1", "index": 0 },
                        "slot": "playfield.bar-line",
                        "blend": "alpha",
                        "properties": { "opacity": 0.5, "visible": true },
                        "effects": [],
                        "children": []
                      }
                    ]
                  },
                  "tracks": [],
                  "stateMachines": [],
                  "bindings": [],
                  "templates": [],
                  "instances": []
                }
                """);
        }

        private static byte[] createPublicMaterialPng(int width, int height, Rgba32 colour)
        {
            using var image = new Image<Rgba32>(width, height, colour);
            using var output = new MemoryStream();
            image.SaveAsPng(output);
            return output.ToArray();
        }

        private static bool containsTexture(Drawable root, Texture texture)
            => root.ChildrenOfType<Sprite>().Any(sprite => ReferenceEquals(sprite.Texture, texture));

        private static T getPrivateField<T>(object instance, string fieldName)
        {
            Type? type = instance.GetType();

            while (type != null)
            {
                FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null)
                    return (T)field.GetValue(instance)!;

                type = type.BaseType;
            }

            throw new InvalidOperationException($"{instance.GetType().Name} has no private field named {fieldName}.");
        }

        private static void assertSpecialisedSceneConsumer(
            Drawable consumer,
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinResolvedMaterialKey key,
            GameplaySkinSceneHostedSlot gate,
            params string[] expectedNodeIds)
            => assertSceneConsumer(
                consumer,
                materialSet,
                key,
                gate,
                GameplaySkinSceneHostRoute.Specialised,
                expectedNodeIds);

        private static void assertSceneConsumer(
            Drawable consumer,
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinResolvedMaterialKey key,
            GameplaySkinSceneHostedSlot gate,
            GameplaySkinSceneHostRoute expectedRoute,
            params string[] expectedNodeIds)
        {
            Type? contract = consumer.GetType().GetInterfaces()
                                     .SingleOrDefault(type => type.Name == "IGameplaySkinSpecialisedSceneConsumer");
            Assert.That(contract, Is.Not.Null,
                $"{consumer.GetType().Name} must consume specialised scene nodes through the shared immutable contract.");

            object? read(string propertyName)
            {
                var property = contract!.GetProperty(propertyName);
                Assert.That(property, Is.Not.Null,
                    $"The specialised scene consumer contract must expose {propertyName}.");
                return property!.GetValue(consumer);
            }

            string[]? appliedNodeIds = ((IEnumerable<string>?)read("AppliedSceneNodeIds"))?.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(read("ResolvedMaterialSet"), Is.SameAs(materialSet));
                Assert.That(read("ResolvedMaterialKey"), Is.EqualTo(key));
                Assert.That(read("SceneVisualGate"), Is.SameAs(gate));
                Assert.That(appliedNodeIds, Is.EqualTo(expectedNodeIds));
                Assert.That(appliedNodeIds, Is.Unique);
                Assert.That(gate.Route, Is.EqualTo(expectedRoute));
                Assert.That(gate.RoutedNodes.Select(node => node.InstanceId), Is.EqualTo(expectedNodeIds));
            });
        }

        private static bool isDescendantOf(Drawable drawable, CompositeDrawable expectedAncestor)
        {
            Drawable? current = drawable.Parent;

            while (current != null)
            {
                if (ReferenceEquals(current, expectedAncestor))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        private static bool isHostedInMountedSceneLayer(
            GameplaySkinSceneRuntimeHost sceneHost,
            GameplaySkinResolvedMaterialEntry entry)
        {
            if (!sceneHost.TryGetHostedDrawable(entry.Key, out Drawable? drawable) || drawable == null)
                return false;

            GameplaySkinSceneRuntimeLayers layers = sceneHost.Layers;
            return new[]
            {
                layers.Background,
                layers.Underlay,
                layers.Object,
                layers.GameplayEffects,
                layers.Overlay,
                layers.HudForeground,
            }.Any(layer => isDescendantOf(drawable, layer));
        }

        private static void assertExactHudPartitions(
            HUDOverlay hud,
            GameplaySkinSceneRuntimeHost sceneHost,
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinSlotDescriptor slot,
            IReadOnlyList<GameplaySkinHudProgrammaticVisualPartition> partitions)
        {
            int stageCount = snapshot.GroupsInLogicalOrder.Count;
            Assert.That(partitions, Is.Not.Empty);
            Assert.That(partitions.Count % stageCount, Is.Zero);

            GameplaySkinLayoutRect screen = snapshot.Context.ScreenBounds;
            GameplaySkinHudProgrammaticVisualResidual[] residuals = hud.GameplaySkinHudResidualPartitions
                .Where(partition => ReferenceEquals(partition.Slot, slot))
                .ToArray();

            for (int sourceIndex = 0; sourceIndex < partitions.Count / stageCount; sourceIndex++)
            {
                GameplaySkinHudProgrammaticVisualPartition[] sourcePartitions = partitions
                    .Where(partition => partition.SourceIndex == sourceIndex)
                    .OrderBy(partition => partition.RelativeStart)
                    .ToArray();

                Assert.That(sourcePartitions, Has.Length.EqualTo(stageCount));

                foreach (GameplaySkinLayoutGroup group in snapshot.GroupsInLogicalOrder)
                {
                    var stageKey = new GameplaySkinResolvedMaterialKey(slot, GameplaySkinResolvedMaterialTarget.ForStage(group.TopologyGroup));
                    GameplaySkinHudProgrammaticVisualPartition partition = sourcePartitions.Single(candidate => candidate.StageKey.Equals(stageKey));
                    Assert.That(sceneHost.TryGetVisualGate(stageKey, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    GameplaySkinLayoutRect exactSurface = gate!.PreparedRect;
                    Assert.That(partition.RelativeStart,
                        Is.EqualTo((exactSurface.Left - screen.Left) / screen.Width).Within(0.0001f));
                    Assert.That(partition.RelativeWidth,
                        Is.EqualTo(exactSurface.Width / screen.Width).Within(0.0001f));
                }

                Assert.That(sourcePartitions.All(partition => partition.Owner.Masking), Is.True);
                Assert.That(sourcePartitions.Select(partition => partition.Owner).Distinct().Count(), Is.EqualTo(stageCount));

                var segments = sourcePartitions.Select(partition => (partition.RelativeStart, partition.RelativeWidth))
                                               .Concat(residuals.Where(partition => partition.SourceIndex == sourceIndex)
                                                                .Select(partition => (partition.RelativeStart, partition.RelativeWidth)))
                                               .OrderBy(segment => segment.RelativeStart)
                                               .ToArray();
                Assert.That(segments, Is.Not.Empty);
                Assert.That(segments[0].RelativeStart, Is.EqualTo(0).Within(0.0001f));

                for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
                {
                    Assert.That(segments[segmentIndex].RelativeStart,
                        Is.EqualTo(segments[segmentIndex - 1].RelativeStart + segments[segmentIndex - 1].RelativeWidth).Within(0.0001f),
                        "Exact C3 stage spans plus residual screen/gap clips must reproduce the unsplit HUD visual without overlap.");
                }

                Assert.That(segments[^1].RelativeStart + segments[^1].RelativeWidth, Is.EqualTo(1).Within(0.0001f));
            }
        }

        private static readonly Type[] required_text_hud_owner_types =
        {
            typeof(DefaultScoreCounter),
            typeof(DefaultAccuracyCounter),
            typeof(DefaultSongProgress),
            typeof(TrianglesPerformancePointsCounter),
            typeof(DefaultKeyCounterDisplay),
            typeof(BPMCounter),
            typeof(ClicksPerSecondCounter),
            typeof(TrianglesUnstableRateCounter),
            typeof(JudgementCounterDisplay),
            typeof(DefaultRankDisplay),
        };

        private static bool hasRequiredTextHudOwners(HUDOverlay hud)
        {
            Type[] actual = hud.GameplaySkinTextPartitions.Select(partition => partition.Visual.GetType()).Distinct().ToArray();
            return required_text_hud_owner_types.All(actual.Contains);
        }

        private static void assertRequiredTextHudOwners(HUDOverlay hud)
        {
            Type[] actual = hud.GameplaySkinTextPartitions.Select(partition => partition.Visual.GetType()).Distinct().ToArray();

            Assert.That(actual, Is.SupersetOf(required_text_hud_owner_types),
                "TextHud must own every real score/stat/text component; only non-data chrome may fall back to Decoration.");
        }

        private sealed class UnpartitionedManiaVisualSkin : SkinTransformer
        {
            public UnpartitionedManiaVisualSkin(ISkin skin)
                : base(skin)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is ManiaSkinComponentLookup maniaLookup
                    ? maniaLookup.Component switch
                    {
                        ManiaSkinComponents.ColumnBackground => new UnpartitionedCustomColumnBackground(),
                        ManiaSkinComponents.HitTarget => new UnpartitionedCustomHitTarget(),
                        _ => null,
                    }
                    : null;
        }

        private abstract partial class UnpartitionedCustomVisual : CompositeDrawable
        {
            protected UnpartitionedCustomVisual(Color4 colour)
            {
                RelativeSizeAxes = Axes.Both;
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                };
            }

            public void ForceVisibleAfterGate()
            {
                ClearTransforms();
                Alpha = 0;
                X = 0;
                this.FadeTo(1, 100);
                this.MoveToX(17, 100);
                FinishTransforms(true);
            }
        }

        private sealed partial class UnpartitionedCustomColumnBackground : UnpartitionedCustomVisual
        {
            public UnpartitionedCustomColumnBackground()
                : base(new Color4(40, 180, 230, 255))
            {
            }
        }

        private sealed partial class UnpartitionedCustomHitTarget : UnpartitionedCustomVisual
        {
            public UnpartitionedCustomHitTarget()
                : base(new Color4(235, 105, 45, 255))
            {
            }
        }

        private sealed partial class FaultingProgrammaticPartProvider : CompositeDrawable, IManiaGameplaySkinProgrammaticVisualPartProvider
        {
            private readonly Drawable invalidOwner = new Container();

            public Drawable ValidOwner { get; } = new Container();

            public IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> GameplaySkinProgrammaticVisualParts { get; private set; }

            public FaultingProgrammaticPartProvider()
            {
                GameplaySkinProgrammaticVisualParts = new[]
                {
                    new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.LaneSurface, ValidOwner),
                    new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.Decoration, invalidOwner),
                };
            }

            public void UseValidPartsOnly()
            {
                GameplaySkinProgrammaticVisualParts = new[]
                {
                    new ManiaGameplaySkinProgrammaticVisualPart(GameplaySkinSlotCatalog.LaneSurface, ValidOwner),
                };
            }
        }

        private sealed partial class CurrentRevisionManiaMaterialHost : SkinProvidingContainer
        {
            [Cached]
            private readonly SkinManager skinManager;
            private readonly GameplayClockContainer gameplayClock;
            private readonly GameplayState gameplayState;
            private readonly List<Note> stageActivationNotes = new List<Note>();

            public Note Note { get; private set; } = null!;

            public HoldNote Hold { get; private set; } = null!;

            public BarLine BarLine { get; private set; } = null!;

            public DrawableManiaRuleset Drawable { get; }

            public RulesetSkinProvidingContainer Provider { get; }

            public ScoreProcessor ScoreProcessor { get; }

            public HealthProcessor HealthProcessor { get; }

            public DrawableNote? NoteDrawable { get; private set; }

            public DrawableHoldNote? HoldDrawable { get; private set; }

            public DrawableBarLine? BarLineDrawable { get; private set; }

            public ManiaGameplayHudComponentsContainer? Hud { get; private set; }

            public HUDOverlay? CoreHud { get; private set; }

            public Column FirstColumn => Drawable.Playfield.Stages.Single().Columns[0];

            public bool SurfaceReady
                => Provider.IsLoaded
                   && Drawable.IsLoaded
                   && Drawable.LayoutRevisionOwner.CurrentPublication != null
                   && Drawable.Playfield.Stages.Single().LoadState >= LoadState.Ready
                   && FirstColumn.LoadState >= LoadState.Ready
                   && getPrivateField<SkinnableDrawable>(FirstColumn, "keyArea").Drawable
                       is LegacyKeyArea { UsesPreparedMaterial: true };

            public bool Ready
            {
                get
                {
                    if (!ObjectsReady)
                        return false;

                    DrawableNote note = NoteDrawable!;
                    DrawableHoldNote hold = HoldDrawable!;

                    return SurfaceReady
                           && getPrivateField<SkinnableDrawable>(note, "headPiece").Drawable is LegacyNotePiece notePiece
                           && notePiece.GetType() == typeof(LegacyNotePiece)
                           && notePiece.UsesPreparedMaterial
                           && getPrivateField<SkinnableDrawable>(hold.Head, "headPiece").Drawable
                               is LegacyHoldNoteHeadPiece { UsesPreparedMaterial: true }
                           && getPrivateField<SkinnableDrawable>(hold, "bodyPiece").Drawable
                               is LegacyBodyPiece { UsesPreparedMaterial: true }
                           && getPrivateField<SkinnableDrawable>(hold.Tail, "headPiece").Drawable
                               is LegacyHoldNoteTailPiece { UsesPreparedMaterial: true }
                           && getPrivateField<SkinnableDrawable>(FirstColumn, "keyArea").Drawable
                               is LegacyKeyArea { UsesPreparedMaterial: true };
                }
            }

            public bool ObjectsReady
            {
                get
                {
                    DrawableNote? note = NoteDrawable ??= Drawable.ChildrenOfType<DrawableNote>()
                                                                   .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, Note));
                    DrawableHoldNote? hold = HoldDrawable ??= Drawable.ChildrenOfType<DrawableHoldNote>()
                                                                          .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, Hold));
                    DrawableBarLine? barLine = BarLineDrawable ??= Drawable.ChildrenOfType<DrawableBarLine>()
                                                                              .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, BarLine));

                    return note?.IsLoaded == true
                           && hold?.IsLoaded == true
                           && barLine?.IsLoaded == true
                           && hold.Head.IsLoaded
                           && hold.Tail.IsLoaded;
                }
            }

            public CurrentRevisionManiaMaterialHost(
                SkinManager skinManager,
                bool dualStage = false,
                IReadOnlyList<Mod>? mods = null,
                bool includeStageActivationObjects = false,
                bool useUnpartitionedCustomVisuals = false)
                : base(useUnpartitionedCustomVisuals
                    ? new UnpartitionedManiaVisualSkin(skinManager.CurrentSkin.Value)
                    : skinManager.CurrentSkin.Value)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;

                var ruleset = new ManiaRuleset();
                ScoreProcessor = ruleset.CreateScoreProcessor();
                HealthProcessor = ruleset.CreateHealthProcessor(0);
                gameplayClock = new GameplayClockContainer(new TrackVirtual(60_000), applyOffsets: false, requireDecoupling: false);
                gameplayClock.Seek(1_000);
                gameplayClock.SoftUnpause();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                beatmap.ControlPointInfo.Add(0, new TimingControlPoint
                {
                    BeatLength = 500,
                    TimeSignature = TimeSignature.SimpleQuadruple,
                });
                beatmap.ControlPointInfo.Add(1_400, new TimingControlPoint
                {
                    BeatLength = 250,
                    TimeSignature = TimeSignature.SimpleTriple,
                });

                if (dualStage)
                    beatmap.Stages.Add(new StageDefinition(5));

                if (includeStageActivationObjects)
                {
                    int firstColumn = 0;

                    foreach (StageDefinition stage in beatmap.Stages)
                    {
                        var note = new Note
                        {
                            Column = firstColumn,
                            StartTime = 1_150,
                        };
                        note.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());
                        beatmap.HitObjects.Add(note);
                        firstColumn += stage.Columns;
                    }
                }

                ScoreProcessor.ApplyBeatmap(beatmap);
                HealthProcessor.ApplyBeatmap(beatmap);
                HealthProcessor.Health.Value = 0.5;
                gameplayState = new GameplayState(
                    beatmap,
                    ruleset,
                    scoreProcessor: ScoreProcessor,
                    healthProcessor: HealthProcessor);

                Drawable = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap, mods);
                Drawable.NewResult += applyResultToProductionProcessors;
                Provider = new RulesetSkinProvidingContainer(
                    ruleset,
                    beatmap,
                    null,
                    prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Drawable,
                };
                InternalChild = gameplayClock.WithChild(Provider);

            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(ScoreProcessor);
                dependencies.CacheAs(HealthProcessor);
                dependencies.CacheAs(gameplayClock);
                dependencies.CacheAs(gameplayState);
                return dependencies;
            }

            public void AddProductionObjects(double startTime)
            {
                Note = new Note
                {
                    Column = 0,
                    StartTime = startTime,
                };
                Hold = new HoldNote
                {
                    Column = 0,
                    StartTime = startTime + 100,
                    Duration = 500,
                };
                Note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                Hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                BarLine = new BarLine { StartTime = startTime + 50 };
                Drawable.Beatmap.HitObjects.Add(Note);
                Drawable.Beatmap.HitObjects.Add(Hold);
                ScoreProcessor.ApplyBeatmap(Drawable.Beatmap);
                HealthProcessor.ApplyBeatmap(Drawable.Beatmap);
                Drawable.Playfield.Add(Note);
                Drawable.Playfield.Add(Hold);
                Drawable.Playfield.Add(BarLine);
            }

            public void ActivateStages()
            {
                if (stageActivationNotes.Count != 0)
                    return;

                int firstColumn = 0;

                foreach (StageDefinition stage in ((ManiaBeatmap)Drawable.Beatmap).Stages)
                {
                    var note = new Note
                    {
                        Column = firstColumn,
                        StartTime = 1_150,
                    };
                    note.ApplyDefaults(Drawable.Beatmap.ControlPointInfo, new BeatmapDifficulty());
                    stageActivationNotes.Add(note);
                    Drawable.Playfield.Add(note);
                    firstColumn += stage.Columns;
                }
            }

            public bool RemoveProductionHold() => Drawable.Playfield.Remove(Hold);

            public bool RemoveProductionNote() => Drawable.Playfield.Remove(Note);

            public bool RefreshProductionHoldDrawable()
            {
                HoldDrawable = FirstColumn.HitObjectContainer.Objects.OfType<DrawableHoldNote>()
                                          .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, Hold));
                return HoldDrawable?.IsLoaded == true;
            }

            public void SetRulesetTime(double time)
            {
                gameplayClock.Seek(time);
            }

            public void AddProductionBarLine(double startTime)
            {
                BarLine = new BarLine { StartTime = startTime };
                Drawable.Playfield.Add(BarLine);
            }

            public void AddProductionHud()
            {
                Hud = (ManiaGameplayHudComponentsContainer)Provider.GetDrawableComponent(
                    new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new ManiaRuleset().RulesetInfo))!;

                foreach (Drawable child in Hud.Children.Where(child => child is not OmsManiaComboCounter && child is not LegacyManiaComboCounter).ToArray())
                    Hud.Remove(child, false);

                Provider.Add(new HudDependenciesContainer(Drawable.ScrollingInfo, ScoreProcessor, Drawable)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Hud,
                });
            }

            public void AddProductionCoreHud()
            {
                if (CoreHud != null)
                    throw new InvalidOperationException("The production core HUD has already been mounted.");

                CoreHud = new HUDOverlay(Drawable, Array.Empty<Mod>(), new PlayerConfiguration())
                {
                    RelativeSizeAxes = Axes.Both,
                    AlwaysPresent = true,
                };
                Provider.Add(CoreHud);
            }

            private void applyResultToProductionProcessors(JudgementResult result)
            {
                ScoreProcessor.ApplyResult(result);
                HealthProcessor.ApplyResult(result);
            }

            protected override void Dispose(bool isDisposing)
            {
                if (isDisposing)
                    Drawable.NewResult -= applyResultToProductionProcessors;

                base.Dispose(isDisposing);
            }
        }

        public enum ManiaPublicMaterialPackageSource
        {
            OrdinaryRealm,
            ManagedFolder,
            ExternalFolder,
        }

        private partial class HudDependenciesContainer : Container
        {
            private readonly IScrollingInfo scrollingInfo;
            private readonly ScoreProcessor scoreProcessor;
            private readonly DrawableRuleset drawableRuleset;

            public HudDependenciesContainer(IScrollingInfo scrollingInfo, ScoreProcessor scoreProcessor, DrawableRuleset drawableRuleset)
            {
                this.scrollingInfo = scrollingInfo;
                this.scoreProcessor = scoreProcessor;
                this.drawableRuleset = drawableRuleset;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(scrollingInfo);
                dependencies.CacheAs(scoreProcessor);
                dependencies.CacheAs(drawableRuleset);
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
