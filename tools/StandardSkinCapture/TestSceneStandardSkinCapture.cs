// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Tools.StandardSkinCapture;

/// <summary>Opt-in evidence through the normal package importer and actual scored Player.</summary>
[TestFixture]
public partial class TestSceneStandardSkinCapture : OsuGameTestScene
{
    [Resolved]
    private GameHost host { get; set; } = null!;

    private CaptureSettings settings = null!;
    private Task<Live<SkinInfo>> import = null!;
    private Task<bool> selection = null!;
    private Ruleset ruleset = null!;
    private ReplayPlayer player = null!;
    private GameplaySkinLayoutRevisionOwner owner = null!;
    private GameplaySkinSceneRuntimeHost runtime = null!;
    private GameplaySkinEventRuntimeHost events = null!;
    private GameplaySkinEventStream eventStream = null!;
    private ScoreProcessor score = null!;
    private GameplayClockContainer clock = null!;
    private Task? screenshot;
    private ResultsScreen results = null!;
    private readonly BindableBool hostActivity = new BindableBool();
    private bool observedHostActivity;
    private bool activityBound;
    private readonly List<string> captured = new List<string>();
    private GameplaySkinEventStateSnapshot? completionState;
    private string? bgaFixtureDirectory;
    private BgaCaptureWorkingBeatmap? bgaWorking;

    public override bool AutomaticallyRunFirstStep => false;
    protected override double TimePerAction => 50;

    [BackgroundDependencyLoader(permitNulls: true)]
    private void requireIsolation(ExactVisualTestIsolation? isolation)
    {
        if (host is HeadlessGameHost || isolation == null)
            throw new InvalidOperationException("Standard skin capture requires the isolated desktop runner.");
    }

    [Test]
    public void ImportPlayCaptureAndReachNaturalResults()
    {
        AddStep("verify the exact package and configure the isolated window", () =>
        {
            settings = CaptureSettings.Current;
            settings.VerifyPackage();
            Assert.That(UseOnlineAPI, Is.False);
            var framework = Game.Dependencies.Get<FrameworkConfigManager>();
            framework.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
            framework.SetValue(FrameworkSetting.WindowedSize, new System.Drawing.Size(1920, 1080));
            observedHostActivity = host.IsActive.Value;
            hostActivity.BindTo((Bindable<bool>)host.IsActive);
            activityBound = true;
            hostActivity.Value = true;
            import = Game.Dependencies.Get<SkinManager>().Import(new PreservingImportTask(File.OpenRead(settings.Package), Path.GetFileName(settings.Package)));
            // The exact runner owns the watchdog. Keep the normal importer alive until it finishes.
            import.GetAwaiter().GetResult();
        });
        AddStep("create a synthetic chart in the requested native ruleset", () =>
        {
            ruleset = settings.Ruleset == "bms" ? new BmsRuleset() : new ManiaRuleset();
            Game.Ruleset.Value = ruleset.RulesetInfo;
            IBeatmap beatmap = settings.Ruleset == "bms" ? CreateBmsChart((BmsRuleset)ruleset) : CreateManiaChart((ManiaRuleset)ruleset);
            Game.Beatmap.Value = settings.Ruleset == "bms"
                ? bgaWorking = new BgaCaptureWorkingBeatmap(beatmap, Clock, Audio, bgaFixtureDirectory!)
                : CreateWorkingBeatmap(beatmap);
            Mod autoplay = ruleset.GetAutoplayMod()!;
            Game.SelectedMods.Value = settings.Ruleset == "bms" ? settings.LongNoteMode switch
            {
                "ln" => new[] { autoplay },
                "cn" => new Mod[] { autoplay, new BmsModChargeNote() },
                "hcn" => new Mod[] { autoplay, new BmsModHellChargeNote() },
                _ => throw new InvalidOperationException("Unvalidated long-note mode."),
            } : new[] { autoplay };
            settings.Write("chart.json", new
            {
                source = settings.Ruleset == "bms" ? "Synthetic BMS fixture file through BmsBeatmapDecoder" : "Synthetic ManiaBeatmap with explicit native StageDefinition",
                rawWrapperObjects = beatmap.HitObjects.Count,
                rawWrapperType = beatmap.GetType().FullName,
                ruleset = ruleset.RulesetInfo.ShortName,
                settings.Keys,
                settings.LongNoteMode,
                settings.ChartVariant,
                mods = Game.SelectedMods.Value.Select(mod => mod.Acronym).ToArray(),
                physicalInput = false,
                realSongAcceptance = false,
                hostActivitySimulated = true,
                observedHostActivity,
            });
            DismissAnyNotifications();
            selection = Game.Dependencies.Get<SkinManager>().SelectSkinForScopeAsync(new SkinSelectionScope(settings.Ruleset), import.Result);
        });
        AddUntilStep("wait for committed skin selection", () => selection.IsCompleted);
        AddStep("verify selection and apply this case's explicit author choices", () =>
        {
            Assert.That(selection.GetAwaiter().GetResult(), Is.True);
            var skins = Game.Dependencies.Get<SkinManager>();
            Assert.That(skins.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(import.Result.ID));
            Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(import.Result.ID));
            Assert.That(skins.CurrentSkinScope, Is.EqualTo(new SkinSelectionScope(settings.Ruleset)));
            Assert.That(skins.SelectionPreferences!.GetConfiguredSkinId(new SkinSelectionScope(settings.Ruleset)), Is.EqualTo(import.Result.ID));
            var preferences = Game.Dependencies.Get<SkinOptionPreferences>();
            preferences.SetOverride(import.Result.ID, settings.Ruleset, "layout", settings.Presentation == "p2" ? "p2" : "p1");
            foreach (KeyValuePair<string, string> choice in settings.Options)
                preferences.SetOverride(import.Result.ID, settings.Ruleset, choice.Key, choice.Value);
            settings.Write("skin-selection.json", new { importedId = import.Result.ID, selectedId = skins.CurrentSkinInfo.Value.ID, scope = skins.CurrentSkinScope.ToString() });
            Game.ScreenStack.Push(new PlayerLoader(() => player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)));
        });
        AddUntilStep("the real Player and exact authored scene are ready", () =>
        {
            DismissAnyNotifications();
            if (player == null || !ReferenceEquals(Game.ScreenStack.CurrentScreen, player) || !player.LoadedBeatmapSuccessfully)
                return false;
            DrawableRuleset? renderer = settings.Ruleset == "bms"
                ? player.ChildrenOfType<DrawableBmsRuleset>().SingleOrDefault()
                : player.ChildrenOfType<DrawableManiaRuleset>().SingleOrDefault();
            if (renderer?.IsLoaded != true)
                return false;
            runtime = renderer.Dependencies.Get<GameplaySkinSceneRuntimeHost>();
            if (!runtime.IsSceneReady)
                return false;
            owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
            events = renderer.Dependencies.Get<GameplaySkinEventRuntimeHost>();
            eventStream = events.EventStream;
            score = renderer.Dependencies.Get<ScoreProcessor>();
            clock = player.ChildrenOfType<GameplayClockContainer>().Single();
            return true;
        });
        AddStep("verify actual publication and expose its framebuffer", () =>
        {
            DrawableRuleset renderer = settings.Ruleset == "bms"
                ? player.ChildrenOfType<DrawableBmsRuleset>().Single()
                : player.ChildrenOfType<DrawableManiaRuleset>().Single();
            int convertedHitObjects = renderer.Objects.Count();
            Assert.That(convertedHitObjects, Is.GreaterThan(0), "The actual Player must contain native converted hit objects.");
            bool noFail = renderer.Mods.OfType<ModNoFail>().Any();
            Assert.That(noFail, Is.False, "A successful complete-play capture may not conceal replay failure with NoFail.");
            string? actualLongNoteMode = null;
            string[] nativeHoldModes = Array.Empty<string>();
            if (renderer is DrawableBmsRuleset bmsRenderer)
            {
                BmsLongNoteMode expectedMode = Enum.Parse<BmsLongNoteMode>(settings.LongNoteMode, true);
                Assert.That(bmsRenderer.LongNoteMode, Is.EqualTo(expectedMode));
                BmsLongNoteMode[] holdModes = bmsRenderer.Objects.OfType<BmsHoldNote>()
                    .Select(hold => ReadProperty<BmsLongNoteMode>(hold, "LongNoteMode")).ToArray();
                Assert.That(holdModes, Is.Not.Empty.And.All.EqualTo(expectedMode));
                actualLongNoteMode = bmsRenderer.LongNoteMode.ToString().ToLowerInvariant();
                nativeHoldModes = holdModes.Select(mode => mode.ToString().ToLowerInvariant()).ToArray();
            }
            settings.Write("player-chart.json", new
            {
                source = "Actual DrawableRuleset.Objects after native beatmap conversion",
                nativeRenderer = renderer.GetType().FullName,
                convertedHitObjects,
                settings.Ruleset,
                settings.Keys,
                settings.ChartVariant,
                requestedLongNoteMode = settings.LongNoteMode,
                actualLongNoteMode,
                nativeHoldModes,
                actualMods = renderer.Mods.Select(mod => new { mod.Acronym, type = mod.GetType().FullName }).ToArray(),
                noFail,
                metadata = new
                {
                    workingTitle = Game.Beatmap.Value.Metadata.Title,
                    workingArtist = Game.Beatmap.Value.Metadata.Artist,
                    nativeTitle = player.GameplayState.Beatmap.BeatmapInfo.Metadata.Title,
                    nativeArtist = player.GameplayState.Beatmap.BeatmapInfo.Metadata.Artist,
                },
            });
            var skins = Game.Dependencies.Get<SkinManager>();
            Assert.That(owner.PackageRevision.RetainsExactSource(skins.CurrentSkin.Value), Is.True);
            Assert.That(owner.PackageRevision.RecordId, Is.EqualTo(import.Result.ID));
            Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.CurrentSkin.Value), Is.False);
            Assert.That(ReferenceEquals(owner.CurrentPublication, runtime.Publication), Is.True);
            Assert.That(runtime.MaterialSet.Diagnostics, Is.Empty);
            Assert.That(runtime.RuntimeFaults, Is.Empty);
            int expectedLanes = settings.Ruleset == "bms" ? settings.Keys + (settings.Keys == 14 ? 2 : 1) : settings.Keys;
            Assert.That(runtime.Publication.Snapshot.Context.Topology.LanesInLogicalOrder.Count, Is.EqualTo(expectedLanes));
            var options = runtime.Publication.Snapshot.Context.OptionValues;
            Assert.That(runtime.Publication.Snapshot.Context.RulesetId, Is.EqualTo(settings.Ruleset));
            string expectedKeymode = settings.Ruleset == "mania" && settings.Keys >= 14
                ? $"{settings.Keys / 2}k-{settings.Keys / 2}k" : $"{settings.Keys}k";
            Assert.That(runtime.Publication.Snapshot.Context.KeymodeId, Is.EqualTo(expectedKeymode));
            if (settings.Keys < 14)
            {
                Assert.That(options.TryGetValue("layout", out GameplaySkinOptionValue layout), Is.True);
                Assert.That(layout.CanonicalValue, Is.EqualTo(settings.Presentation));
                Assert.That(runtime.Publication.Snapshot.Context.PresentationStyleId, Is.EqualTo(settings.Presentation));
            }
            foreach (KeyValuePair<string, string> requested in settings.Options)
            {
                Assert.That(options.TryGetValue(requested.Key, out GameplaySkinOptionValue value), Is.True, requested.Key);
                Assert.That(value.CanonicalValue, Is.EqualTo(requested.Value), requested.Key);
            }
            if (options.TryGetValue("frame", out _))
            {
                var information = runtime.MaterialSet.Entries.Where(entry => entry.TryGetMaterial<GameplaySkinPublicSlotMaterial>(out var material)
                    && material.ResourceName is { } name
                    && name.StartsWith("markers/frame-", StringComparison.Ordinal) && name.EndsWith("-information", StringComparison.Ordinal)).ToArray();
                Assert.That(information, Has.Length.EqualTo(1));
                Assert.That(information[0].Target.Kind, Is.EqualTo(GameplaySkinResolvedMaterialTargetKind.Global));
            }
            settings.Write("publication.json", new
            {
                exactImportedOwner = true,
                revision = new { owner.PackageRevision.Generation, owner.PackageRevision.RecordId, owner.PackageRevision.ContentRevision, sourceKind = owner.PackageRevision.SourceKind.ToString() },
                context = new { runtime.Publication.Snapshot.Context.RulesetId, runtime.Publication.Snapshot.Context.KeymodeId, runtime.Publication.Snapshot.Context.PresentationStyleId },
                options = options.Values.ToDictionary(pair => pair.Key, pair => pair.Value.CanonicalValue),
                lanes = runtime.Publication.Snapshot.LanesInLogicalOrder.Select(lane => new { id = lane.TopologyEntry.Identity.Id.Value, rect = Rect(lane.Rect) }).ToArray(),
                materials = runtime.MaterialSet.Entries.Select(entry => new
                {
                    slot = entry.Slot.Id,
                    target = entry.Target.Kind.ToString(),
                    lane = entry.Target.LaneId?.Value,
                    state = entry.State.ToString(),
                    suppressed = entry.State == GameplaySkinResolvedMaterialState.Suppress,
                    sourceKind = entry.Source.Kind.ToString(),
                    entry.Source.StableId,
                    entry.Source.ContentRevision,
                    selectedDeclaration = ReadProperty<bool>(entry.Source, "IsSelectedDocumentDeclaration"),
                    resource = entry.TryGetMaterial<GameplaySkinPublicSlotMaterial>(out var material) ? material.ResourceName : null,
                }).ToArray(),
                bindings = runtime.PreparedScene.Program.Bindings.Select(binding => new
                {
                    binding.Id,
                    target = binding.TargetNodeId,
                    source = binding.Source.ToString(),
                    property = binding.Property.ToString(),
                    binding.OptionId,
                }).ToArray(),
                tracks = runtime.PreparedScene.Program.Tracks.Select(track => new { track.Id, target = track.TargetNodeId, clock = track.ClockSource.ToString(), property = track.Property.ToString() }).ToArray(),
            });
            ExposeGameFramebuffer();
        });
        foreach (int time in new[] { 2600, 3700, 6800, 10100 })
        {
            int sample = time;
            AddUntilStep("natural play reaches " + sample + " ms", () => ReadProperty<double>(events, "AuthoritativeGameplayTime") >= sample);
            AddStep("capture actual gameplay and bound data", () =>
            {
                Assert.That(runtime.RuntimeFaults, Is.Empty);
                Assert.That(ReferenceEquals(Game.ScreenStack.CurrentScreen, player), Is.True);
                if (settings.ChartVariant == "combo-stress" && sample == 10100)
                    Assert.That(score.Combo.Value, Is.GreaterThanOrEqualTo(1000), "The actual scored Player must reach four-digit combo before this capture.");
                string name = $"gameplay-{sample:00000}";
                RecordGameplay(name, sample);
                screenshot = Capture(name);
            });
            AddUntilStep("wait for framebuffer write", () => screenshot?.IsCompleted == true);
            AddStep("propagate capture failures", () => screenshot!.GetAwaiter().GetResult());
        }
        AddUntilStep("finish scoring the complete chart", () => score.HasCompleted.Value);
        AddStep("retain real completion and score", () =>
        {
            Assert.That(score.TotalScore.Value, Is.GreaterThan(0));
            Assert.That(score.HighestCombo.Value, Is.GreaterThan(0));
            if (settings.ChartVariant == "combo-stress")
                Assert.That(score.Combo.Value, Is.GreaterThanOrEqualTo(1000));
            Assert.That(score.HitEvents, Is.Not.Empty);
            Assert.That(player.Score.ScoreInfo.Passed, Is.True, "The replay must complete without a native failure.");
            Assert.That(player.GameplayState.HasFailed, Is.False);
            Assert.That(score.IsFullCombo, Is.True, "The outro case requires an actual scored full combo.");
            Assert.That(runtime.RuntimeFaults, Is.Empty);
            completionState = ReadState();
            Assert.That(completionState.Completion?.IsFullCombo, Is.True);
            settings.Write("score-complete.json", new
            {
                score = score.TotalScore.Value,
                combo = score.HighestCombo.Value,
                hits = score.HitEvents.Count,
                score.IsFullCombo,
                statistics = score.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                state = completionState,
            });
        });
        AddUntilStep("natural full-combo outro is visible after 300 ms", () =>
        {
            Assert.That(ReferenceEquals(Game.ScreenStack.CurrentScreen, player), Is.True,
                "Player exited before a naturally visible full-combo outro could be captured.");
            GameplaySkinCompletionStateSnapshot? completion = ReadState().Completion;
            return completion.HasValue && completion.Value.IsFullCombo
                   && ReadProperty<double>(events, "AuthoritativeGameplayTime") - completion.Value.AppliedTime >= 300
                   && ObserveCompletionAnimations().HasVisiblePieces;
        });
        AddStep("capture the actual full-combo outro without changing its clock", () =>
        {
            Assert.That(ReferenceEquals(Game.ScreenStack.CurrentScreen, player), Is.True);
            Assert.That(score.IsFullCombo, Is.True);
            GameplaySkinCompletionStateSnapshot completion = ReadState().Completion
                ?? throw new InvalidOperationException("The native completion event was not retained.");
            Assert.That(completion.IsFullCombo, Is.True);
            var observation = ObserveCompletionAnimations();
            Assert.That(observation.HasVisiblePieces, Is.True);
            double gameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime");
            settings.Write("full-combo-observation.json", new
            {
                completion.AppliedTime,
                completion.IsFullCombo,
                requestedElapsed = 300,
                actualGameplayTime = gameplayTime,
                actualElapsed = gameplayTime - completion.AppliedTime,
                clock.IsRunning,
                actualPlayerStillCurrent = true,
                animations = observation.Animations,
                clockChangedForCapture = false,
            });
            RecordGameplay("full-combo-outro", checked((int)Math.Round(completion.AppliedTime + 300)));
            screenshot = Capture("full-combo-outro");
        });
        AddUntilStep("wait for full-combo framebuffer", () => screenshot?.IsCompleted == true);
        AddStep("propagate full-combo capture failures", () => screenshot!.GetAwaiter().GetResult());
        AddUntilStep("natural transition reaches ordinary Results", () =>
        {
            results = Game.ScreenStack.CurrentScreen as ResultsScreen ?? null!;
            return results is { IsLoaded: true, Score: not null };
        });
        AddWaitStep("let ordinary Results finish its entry", 40);
        AddStep("capture native Results with its real score", () =>
        {
            Assert.That(results.Score!.TotalScore, Is.EqualTo(score.TotalScore.Value));
            Assert.That(results.Score.Passed, Is.True);
            Assert.That(results.Score.Mods.OfType<ModNoFail>(), Is.Empty);
            settings.Write("results.json", new
            {
                screen = results.GetType().FullName,
                ruleset = results.Score.Ruleset.ShortName,
                results.Score.TotalScore,
                results.Score.MaxCombo,
                results.Score.Passed,
                actualMods = results.Score.Mods.Select(mod => new { mod.Acronym, type = mod.GetType().FullName }).ToArray(),
                noFail = false,
                statistics = results.Score.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            });
            screenshot = Capture("native-results");
        });
        AddUntilStep("wait for Results framebuffer", () => screenshot?.IsCompleted == true);
        AddStep("finish the exact-package evidence receipt", () =>
        {
            screenshot!.GetAwaiter().GetResult();
            settings.VerifyPackage();
            settings.Write("completed.json", new
            {
                success = true,
                settings.PackageSha256,
                settings.Ruleset,
                settings.Keys,
                settings.Presentation,
                settings.ChartVariant,
                screenshots = captured.ToArray(),
                normalImport = true,
                actualPlayer = player.GetType().FullName,
                naturalResults = results.GetType().FullName,
                completeScoredChart = true,
                fullComboOutroCaptured = true,
                humanOrDeviceAcceptance = false,
                originalGameEquivalence = false,
            });
            RestoreHostActivity();
        });
    }

    private void ExposeGameFramebuffer()
    {
        OsuGameBase? outer = null;
        for (Drawable? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            if (ancestor is OsuGameBase game) { outer = game; break; }
        Assert.That(outer, Is.Not.Null);
        var content = (Container<Drawable>)typeof(OsuGameBase).GetProperty("Content", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.GetValue(outer)!;
        AlwaysPresent = true;
        Remove(Game, false);
        foreach (Drawable chrome in content.Children) { chrome.Alpha = 0; chrome.AlwaysPresent = true; }
        content.Add(Game);
        Game.Notifications.Hide();
    }

    private GameplaySkinEventStateSnapshot ReadState()
    {
        GameplaySkinEventStateSnapshot? state = null;
        using GameplaySkinEventSubscription subscription = eventStream.Subscribe();
        subscription.DrainFrame(envelope =>
        {
            if (envelope.Payload is GameplaySkinStateEventPayload snapshot)
                state = snapshot.State;
        });
        return state ?? throw new InvalidOperationException("No native retained gameplay state was published.");
    }

    private void RecordGameplay(string name, int requestedTime)
    {
        GameplaySkinEventStateSnapshot state = ReadState();
        object? bga = settings.Ruleset == "bms" ? RecordBga(state) : null;
        var nodes = new List<object>();
        MethodInfo lookup = typeof(GameplaySkinSceneRuntimeHost).GetMethod("TryGetRuntimeNode", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (GameplaySkinPreparedSceneNode prepared in runtime.PreparedScene.Roots.SelectMany(Walk))
        {
            object?[] arguments = [prepared.InstanceId, null];
            if ((bool)lookup.Invoke(runtime, arguments)!)
                nodes.Add(NodeEvidence((GameplaySkinSceneRuntimeNode)arguments[1]!));
        }
        var nativeObjects = player.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>().Where(visual => visual.IsApplied).Select(visual => new
        {
            visual.BoundObjectId,
            visual.IsApplied,
            nodes = visual.RuntimeNodes.Select(NodeEvidence).ToArray(),
        }).ToArray();
        settings.Write(name + ".json", new
        {
            settings.ChartVariant,
            requestedTime,
            actualGameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime"),
            runtime.LastGameplayTime,
            clock.CurrentTime,
            clock.IsRunning,
            state,
            bga,
            nativeScore = score.TotalScore.Value,
            nativeCombo = score.HighestCombo.Value,
            nativeCurrentCombo = score.Combo.Value,
            nativeHitCount = score.HitEvents.Count,
            statistics = score.Statistics.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            nodes,
            nativeObjects,
            runtimeFaults = runtime.RuntimeFaults.Count,
            captureTiming = "State sampled on the update thread immediately before requesting the next actual framebuffer; not a frozen clock.",
        });
    }

    private object RecordBga(GameplaySkinEventStateSnapshot state)
    {
        BgaCaptureWorkingBeatmap working = bgaWorking ?? throw new InvalidOperationException("BMS BGA fixture storage is missing.");
        var renderer = player.ChildrenOfType<DrawableBmsRuleset>().Single();
        var beatmap = (BmsBeatmap)renderer.Beatmap;
        BmsBgaPlayer bgaPlayer = player.ChildrenOfType<BmsBgaPlayer>().Single();
        object baseLayer = ReadField<object>(bgaPlayer, "baseLayer");
        int activeIndex = ReadField<int>(baseLayer, "activeIndex");
        BmsBgaTimelineEntry[] entries = ReadField<BmsBgaTimelineEntry[]>(baseLayer, "entries");
        Assert.That(activeIndex, Is.InRange(0, entries.Length - 1), "The real base layer must have selected a BMS channel-04 event.");
        BmsBgaTimelineEntry active = entries[activeIndex];
        int imageId = beatmap.BmsInfo.BitmapTable.Single(pair => pair.Value == active.AssetFile).Key;
        Assert.That(imageId, Is.EqualTo(1));
        Assert.That(active.AssetFile, Is.EqualTo(BgaCaptureWorkingBeatmap.ImageFilename));
        Assert.That(active.IsVideo, Is.False);
        Assert.That(working.ImageReads, Is.GreaterThan(0));
        Sprite imageSprite = ReadField<Sprite>(baseLayer, "imageSprite");
        Assert.That(imageSprite.Texture, Is.Not.Null, "A live native image texture is required; a background or timeline declaration is insufficient.");
        Assert.That(imageSprite.Alpha, Is.GreaterThan(0));
        Assert.That(imageSprite.Texture!.DisplayWidth, Is.EqualTo(BgaCaptureWorkingBeatmap.ImageWidth));
        Assert.That(imageSprite.Texture.DisplayHeight, Is.EqualTo(BgaCaptureWorkingBeatmap.ImageHeight));
        Assert.That(state.BgaViewports, Is.Not.Empty);
        Assert.That(state.BgaViewports.All(viewport => viewport.ContentRevision > 0
            && viewport.ContentState == GameplaySkinBgaContentState.Playing), Is.True);
        var views = player.ChildrenOfType<BmsBgaPanel>().Single().ChildrenOfType<BufferedContainerView<Drawable>>()
            .Select((view, index) =>
            {
                var quad = view.ScreenSpaceDrawQuad.AABBFloat;
                return new { observedIndex = index, effectiveAlpha = EffectiveAlpha(view), geometry = new { quad.X, quad.Y, quad.Width, quad.Height } };
            }).ToArray();
        Assert.That(views, Is.Not.Empty);
        return new
        {
            generatedAcceptancePattern = true,
            working.ImagePath,
            working.ImageReads,
            actualImageId = imageId,
            imageToken = imageId.ToString("00"),
            active.AssetFile,
            active.StartTime,
            layer = active.Layer.ToString(),
            activeIndex,
            contentState = ReadProperty<GameplaySkinBgaContentState>(bgaPlayer, "ContentState").ToString(),
            contentRevision = ReadProperty<long>(bgaPlayer, "ContentRevision"),
            texture = new { imageSprite.Texture.DisplayWidth, imageSprite.Texture.DisplayHeight, imageSprite.Alpha },
            viewports = state.BgaViewports.Select(viewport => new { viewport.ViewportIndex, viewport.ContentRevision,
                state = viewport.ContentState.ToString(), rect = Rect(viewport.Viewport) }).ToArray(),
            actualViews = views,
        };
    }

    private (bool HasVisiblePieces, object[] Animations) ObserveCompletionAnimations()
    {
        var observations = new List<object>();
        bool hasVisiblePieces = false;
        foreach (CompositeDrawable animation in player.ChildrenOfType<CompositeDrawable>()
                     .Where(drawable => drawable.GetType().FullName == "osu.Game.Skinning.Gameplay.GameplaySkinRecordedAnimationDrawable"))
        {
            var program = ReadProperty<GameplaySkinRecordedAnimationProgram>(animation, "Program");
            if (program.Trigger != "gameplay.complete")
                continue;
            int appliedFrame = ReadField<int>(animation, "appliedFrame");
            var visiblePieces = new List<object>();
            foreach (Sprite sprite in animation.ChildrenOfType<Sprite>().Where(sprite => sprite.IsPresent && sprite.Texture != null && EffectiveAlpha(sprite) > 0.001))
            {
                object piece = ReadField<object>(sprite, "piece");
                int vertices = ReadProperty<int>(piece, "VertexCount");
                float multiplyAlpha = ReadProperty<osuTK.Vector4>(piece, "Multiply").W;
                float addAlpha = ReadProperty<osuTK.Vector4>(piece, "Add").W;
                if (vertices < 3 || multiplyAlpha <= 0 && addAlpha <= 0)
                    continue;
                visiblePieces.Add(new { resource = ReadProperty<string?>(piece, "Resource"), vertices, multiplyAlpha, addAlpha,
                    effectiveAlpha = EffectiveAlpha(sprite) });
            }
            hasVisiblePieces |= appliedFrame >= 0 && visiblePieces.Count > 0;
            observations.Add(new
            {
                animationIds = runtime.PreparedScene.RecordedAnimations.Where(pair => ReferenceEquals(pair.Value, program)).Select(pair => pair.Key).ToArray(),
                appliedFrame,
                effectiveAlpha = EffectiveAlpha(animation),
                visiblePieceCount = visiblePieces.Count,
                visiblePieces,
            });
        }
        return (hasVisiblePieces, observations.ToArray());
    }

    private object NodeEvidence(GameplaySkinSceneRuntimeNode node)
    {
        // Read-only inspection of internal lineage; no state or resource is replaced by the probe.
        GameplaySkinPreparedSceneNode prepared = ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode");
        var quad = node.ContentDrawable.ScreenSpaceDrawQuad.AABBFloat;
        return new
        {
            id = prepared.Source.Id,
            node.InstanceId,
            resource = prepared.Source.ResourceId,
            resolvedResource = prepared.Resource?.Id,
            textureResources = node.ContentDrawable is Sprite sprite
                ? runtime.PreparedScene.Resources.Where(resource => resource.Texture != null && ReferenceEquals(resource.Texture, sprite.Texture)).Select(resource => resource.Id).ToArray()
                : Array.Empty<string>(),
            type = node.ContentDrawable.GetType().FullName,
            text = node.ContentDrawable is SpriteText text ? text.Text.ToString() : null,
            authorText = node.ContentDrawable.GetType().FullName == "osu.Game.Skinning.Gameplay.GameplaySkinBitmapText"
                ? ReadProperty<string>(node.ContentDrawable, "AuthorText") : null,
            node.TransformDrawable.Alpha,
            effectiveAlpha = EffectiveAlpha(node.ContentDrawable),
            geometry = new { quad.X, quad.Y, quad.Width, quad.Height },
        };
    }

    private static T ReadProperty<T>(object instance, string name)
        => (T)(instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   ?? throw new MissingMemberException(instance.GetType().FullName, name)).GetValue(instance)!;

    private static T ReadField<T>(object instance, string name)
        => (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   ?? throw new MissingFieldException(instance.GetType().FullName, name)).GetValue(instance)!;

    private static double EffectiveAlpha(Drawable drawable)
    {
        double alpha = 1;
        for (Drawable? current = drawable; current != null; current = current.Parent)
            alpha *= current.Alpha;
        return alpha;
    }

    private static object Rect(GameplaySkinLayoutRect rect) => new { rect.X, rect.Y, rect.Width, rect.Height };

    private static IEnumerable<GameplaySkinPreparedSceneNode> Walk(GameplaySkinPreparedSceneNode root)
    {
        yield return root;
        foreach (GameplaySkinPreparedSceneNode child in root.Children.SelectMany(Walk))
            yield return child;
    }

    private async Task Capture(string name)
    {
        using Image<Rgba32>? image = await host.TakeScreenshotAsync().ConfigureAwait(false);
        if (image == null || image.Width < 640 || image.Height < 360)
            throw new InvalidOperationException("The desktop framebuffer was unavailable or unexpectedly small.");
        string filename = name + ".png";
        await image.SaveAsPngAsync(Path.Combine(settings.Output, filename)).ConfigureAwait(false);
        captured.Add(filename);
    }

    private IBeatmap CreateBmsChart(BmsRuleset bms)
    {
        bgaFixtureDirectory = BgaCaptureWorkingBeatmap.GenerateImage(settings.Output);
        var text = new StringBuilder("#TITLE Standard Model / OMS capture\n#ARTIST Offline synthetic verification\n#DIFFICULTY 3\n#PLAYLEVEL 10\n#BPM 150\n#LNTYPE 1\n#BPM01 180\n#00408:01\n#WAV01 note.wav\n"
            + $"#BMP01 {BgaCaptureWorkingBeatmap.ImageFilename}\n#00104:01\n");
        if (settings.Keys == 14)
            text.Append("#PLAYER 3\n");
        string[] channels = settings.Keys switch
        {
            5 => ["11", "12", "13", "14", "15", "16"],
            7 => ["11", "12", "13", "14", "15", "18", "19", "16"],
            14 => ["11", "12", "13", "14", "15", "18", "19", "16", "21", "22", "23", "24", "25", "28", "29", "26"],
            _ => throw new InvalidOperationException("Unvalidated BMS key count."),
        };
        for (int measure = 1; measure <= 8; measure++)
            for (int lane = 0; lane < channels.Length; lane++)
            {
                string channel = channels[lane];
                if (measure % 2 == 0)
                    channel = (channel[0] == '1' ? "5" : "6") + channel[1];
                text.Append('#').Append(measure.ToString("000")).Append(channel).Append(':')
                    .Append(measure % 2 == 0 ? "01000100" : lane % 2 == 0 ? "01000001" : "00010000").Append('\n');
            }
        string chartPath = Path.Combine(bgaFixtureDirectory, settings.Keys == 5 ? "standard-capture.bms" : "standard-capture.bme");
        using (var chartFile = new StreamWriter(new FileStream(chartPath, FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(false)))
            chartFile.Write(text.ToString());
        using FileStream chart = File.OpenRead(chartPath);
        var decoded = new BmsBeatmapDecoder().Decode(chart, chartPath);
        settings.Write("bga-fixture.json", new
        {
            generatedAcceptancePattern = true,
            noOriginalArtwork = true,
            imageId = 1,
            imageToken = "01",
            image = BgaCaptureWorkingBeatmap.ImageFilename,
            imageSha256 = CaptureSettings.Hash(Path.Combine(bgaFixtureDirectory, BgaCaptureWorkingBeatmap.ImageFilename)),
            width = BgaCaptureWorkingBeatmap.ImageWidth,
            height = BgaCaptureWorkingBeatmap.ImageHeight,
            chartPath,
            chartSha256 = CaptureSettings.Hash(chartPath),
            declaration = "#BMP01 " + BgaCaptureWorkingBeatmap.ImageFilename,
            trigger = "#00104:01",
            resourcePath = "BmsBgaPlayer.WorkingBeatmapFileStore -> WorkingBeatmap.GetStream -> read-only fixture FileStream",
        });
        var beatmap = new BmsDecodedBeatmap(decoded) { BeatmapInfo = { Ruleset = bms.RulesetInfo } };
        beatmap.BeatmapInfo.Metadata.Title = decoded.BeatmapInfo.Title;
        beatmap.BeatmapInfo.Metadata.Artist = decoded.BeatmapInfo.Artist;
        return beatmap;
    }

    private ManiaBeatmap CreateManiaChart(ManiaRuleset mania)
    {
        int columns = settings.Keys >= 14 ? settings.Keys / 2 : settings.Keys;
        var beatmap = new ManiaBeatmap(new StageDefinition(columns)) { BeatmapInfo = { Ruleset = mania.RulesetInfo } };
        beatmap.BeatmapInfo.Metadata.Title = "Standard Model / OMS capture";
        beatmap.BeatmapInfo.Metadata.Artist = "Offline synthetic verification";
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        if (settings.Keys >= 14)
            beatmap.Stages.Add(new StageDefinition(columns));
        beatmap.Difficulty.CircleSize = settings.Keys;
        if (settings.ChartVariant == "combo-stress")
        {
            for (int row = 0; row < 64; row++)
                for (int lane = 0; lane < beatmap.TotalColumns; lane++)
                    beatmap.HitObjects.Add(row < 63
                        ? new Note { Column = lane, StartTime = 2000 + row * 125 + lane * 0.2 }
                        : new HoldNote { Column = lane, StartTime = 2000 + row * 125 + lane * 0.2, Duration = 700 });
            return beatmap;
        }
        for (int row = 0; row < 8; row++)
            for (int lane = 0; lane < beatmap.TotalColumns; lane++)
                beatmap.HitObjects.Add(row % 2 == 0
                    ? new Note { Column = lane, StartTime = 2000 + row * 1500 + lane * 30 }
                    : new HoldNote { Column = lane, StartTime = 2000 + row * 1500 + lane * 30, Duration = 700 });
        return beatmap;
    }

    private void RestoreHostActivity()
    {
        if (!activityBound)
            return;
        hostActivity.Value = observedHostActivity;
        hostActivity.UnbindAll();
        activityBound = false;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            RestoreHostActivity();
        base.Dispose(isDisposing);
    }

    private sealed class PreservingImportTask : ImportTask
    {
        public PreservingImportTask(Stream stream, string filename) : base(stream, filename) { }
        public override void DeleteFile() { }
    }
}
