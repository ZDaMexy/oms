// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Imports a local author's package into disposable storage and captures the actual autoplay Player.
    /// The synthetic chart exercises complete playback; it does not replace real-song or device acceptance.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBmsAuthorSkinCapture : OsuGameTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private Task<Live<SkinInfo>>? import;
        private Task? capture;
        private string outputDirectory = null!;
        private Player player = null!;
        private GameplayClockContainer gameplayClock = null!;
        private DrawableBmsRuleset renderer = null!;
        private ScoreProcessor score = null!;
        private BmsPlayfieldStyle style;
        private BmsLongNoteMode longNoteMode;
        private bool doublePlay;
        private bool fiveKey;
        private bool freezeHitPhase;
        private double? frozenHitPhase;
        private int sampledHitFrames;
        private int sampledEffects;
        private int sampledEffectStarts;
        private double lastSampledPhase;

        public override bool AutomaticallyRunFirstStep => false;

        protected override double TimePerAction => 50;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void requireIsolation(ExactVisualTestIsolation? isolation)
        {
            if (host is not HeadlessGameHost && isolation == null)
                throw new InvalidOperationException("Author capture requires the disposable --exact-test runner.");
        }

        [Test]
        public void CaptureImportedAuthorPackageThroughCompletePlayer()
        {
            bool captureBurst = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_HIT_PHASE") == "1";
            bool captureFullCombo = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_FULLCOMBO") == "1";
            bool captureNoteGeometry = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_NOTE_GEOMETRY") == "1";
            double completionOrigin = 0;
            double completionSeekTime = 0;
            double completionDuration = 0;
            int[] completionFrames = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_FC_FRAMES") is string frameList
                ? [.. frameList.Split(',').Select(int.Parse)] : [32, 60, 90, 120, 145];
            AddStep("require an isolated desktop and a local author package", () =>
            {
                if (host is HeadlessGameHost)
                    Assert.Ignore("Pixel evidence requires the isolated desktop --exact-test runner.");

                string? package = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_PACKAGE");
                string? output = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY");
                if (string.IsNullOrEmpty(package) || string.IsNullOrEmpty(output))
                    Assert.Ignore("Set OMS_AUTHOR_SKIN_PACKAGE and OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY to run this opt-in capture.");

                outputDirectory = Path.GetFullPath(output!);
                Directory.CreateDirectory(outputDirectory);
                doublePlay = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_KEYMODE") == "14K";
                fiveKey = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_KEYMODE") == "5K";
                longNoteMode = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_LONG_NOTE_MODE") switch
                {
                    null or "cn" => BmsLongNoteMode.CN,
                    "ln" => BmsLongNoteMode.LN,
                    "hcn" => BmsLongNoteMode.HCN,
                    _ => throw new InvalidOperationException("The author capture long-note mode must be ln, cn or hcn."),
                };
                style = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_PRESENTATION") == "p2"
                    ? BmsPlayfieldStyle.P2 : BmsPlayfieldStyle.P1;
                if (doublePlay)
                    style = BmsPlayfieldStyle.Center;
                var frameworkConfig = Game.Dependencies.Get<FrameworkConfigManager>();
                frameworkConfig.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
                frameworkConfig.SetValue(FrameworkSetting.WindowedSize, new System.Drawing.Size(1920, 1080));
                import = Game.Dependencies.Get<SkinManager>().Import(new PreservingImportTask(File.OpenRead(package!), Path.GetFileName(package!)));
                // The full local art library can exceed UntilStepButton's fixed ten-second deadline.
                // This opt-in isolated host has its own watchdog; do not abandon its Realm writer mid-import.
                import.GetAwaiter().GetResult();
            });
            AddUntilStep("wait for the normal package importer", () => import?.IsCompleted == true);
            AddStep("select the imported package and a repeatable BMS chart", () =>
            {
                var ruleset = new BmsRuleset();
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(createChart(ruleset, doublePlay, fiveKey));
                Mod autoplay = ruleset.GetAutoplayMod()!;
                Mod[] selectedMods = longNoteMode switch
                {
                    BmsLongNoteMode.LN => [autoplay],
                    BmsLongNoteMode.CN => [autoplay, new BmsModChargeNote()],
                    BmsLongNoteMode.HCN => [autoplay, new BmsModHellChargeNote()],
                    _ => throw new InvalidOperationException("The author capture long-note mode is invalid."),
                };
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_COVER") == "1")
                    selectedMods = [.. selectedMods, new BmsModSudden()];
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_LIFT") == "1")
                    selectedMods = [.. selectedMods, new BmsModLift()];
                Game.SelectedMods.Value = selectedMods;
                DismissAnyNotifications();
            });
            AddStep("select the author package after BMS restores its mode preference", () =>
                Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value = import!.GetAwaiter().GetResult());
            AddUntilStep("wait for the exact imported skin selection", () =>
            {
                var skins = Game.Dependencies.Get<SkinManager>();
                Assert.That(skins.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                return skins.CurrentSkin.Value.SkinInfo.ID == import!.GetAwaiter().GetResult().ID;
            });
            AddStep("enter the real replay player", () =>
            {
                var preferences = Game.Dependencies.Get<SkinOptionPreferences>();
                preferences.SetOverride(import!.GetAwaiter().GetResult().ID, "bms", "layout", style == BmsPlayfieldStyle.P2 ? "p2" : "p1");
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_OPTIONS") is string overrides)
                    foreach (var choice in System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(overrides)!)
                        preferences.SetOverride(import.GetAwaiter().GetResult().ID, "bms", choice.Key, choice.Value);
                var ruleset = new BmsRuleset();
                Game.ScreenStack.Push(new PlayerLoader(() => player = new ReplayPlayer(ruleset.GetAutoplayMod()!.CreateScoreFromReplayData)));
            });
            AddUntilStep("wait for the committed author scene in Player", () =>
            {
                DismissAnyNotifications();
                return player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player)
                                      && player.LoadedBeatmapSuccessfully
                                      && player.ChildrenOfType<DrawableBmsRuleset>().SingleOrDefault()?.IsLoaded == true;
            });
            AddStep("verify the real package publication and hide test chrome", () =>
            {
                renderer = player.ChildrenOfType<DrawableBmsRuleset>().Single();
                gameplayClock = player.ChildrenOfType<GameplayClockContainer>().Single();
                score = renderer.Dependencies.Get<ScoreProcessor>();
                Assert.That(renderer.LongNoteMode, Is.EqualTo(longNoteMode));
                Assert.That(((BmsBeatmap)renderer.Beatmap).HitObjects.OfType<BmsHoldNote>().Select(hold => hold.LongNoteMode),
                    Is.Not.Empty.And.All.EqualTo(longNoteMode), "The capture must exercise the requested native long-note mode.");
                var skins = Game.Dependencies.Get<SkinManager>();
                var owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                Assert.That(owner.PackageRevision.RetainsExact(skins.CurrentRevision), Is.True);
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.CurrentSkin.Value), Is.False);
                Assert.That(renderer.GameplaySkinSceneRuntime, Is.Not.Null);
                Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_BEAM_LAYER") is string beamCheck)
                    recordBeamLayer(beamCheck == "verify");
                File.WriteAllText(Path.Combine(outputDirectory, "materials.json"), System.Text.Json.JsonSerializer.Serialize(
                    renderer.GameplaySkinSceneRuntime.MaterialSet.Entries.Select(entry => new
                    {
                        slot = entry.Slot.Id,
                        lane = entry.Target.LaneId?.Value,
                        state = entry.State.ToString(),
                        selected = entry.Source.IsSelectedDocumentDeclaration,
                    })));
                Assert.That(renderer.GameplaySkinSceneRuntime.MaterialSet.Diagnostics, Is.Empty,
                    "A component fallback must not be reported as a successful author-package capture.");
                GameplaySkinOptionValues publishedOptions = owner.CurrentPublication!.Snapshot.Context.OptionValues;
                File.WriteAllText(Path.Combine(outputDirectory, "options.json"), System.Text.Json.JsonSerializer.Serialize(
                    publishedOptions.Values.ToDictionary(pair => pair.Key, pair => pair.Value.CanonicalValue)));
                if (Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_OPTIONS") is string requestedOptions)
                    foreach (var requested in System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(requestedOptions)!)
                    {
                        GameplaySkinOptionDefinition definition = skins.CurrentSkin.Value.PreparedGameplaySkinPackage!.Manifest!.Options.Single(option => option.Id == requested.Key);
                        Assert.That(definition.TryValidateValue(requested.Value, out GameplaySkinOptionValue expected), Is.True, requested.Key);
                        Assert.That(publishedOptions.TryGetValue(requested.Key, out GameplaySkinOptionValue actual), Is.True, requested.Key);
                        Assert.That(actual.CanonicalValue, Is.EqualTo(expected.CanonicalValue), requested.Key);
                    }
                if (owner.CurrentPublication!.Snapshot.Context.OptionValues.TryGetValue("frame", out _))
                {
                    GameplaySkinResolvedMaterialEntry[] information = renderer.GameplaySkinSceneRuntime.MaterialSet.Entries
                        .Where(entry => entry.State == GameplaySkinResolvedMaterialState.Provide
                                        && entry.Material is GameplaySkinPublicSlotMaterial { ResourceName: { } resource }
                                        && resource.StartsWith("markers/frame-", StringComparison.Ordinal)
                                        && resource.EndsWith("-information", StringComparison.Ordinal)).ToArray();
                    Assert.That(information, Has.Length.EqualTo(1));
                    Assert.That(information[0].Target.Kind, Is.EqualTo(GameplaySkinResolvedMaterialTargetKind.Global),
                        "Frame information must not be inherited and redrawn inside stages, groups or lanes.");
                }

                OsuGameBase? outer = null;
                for (Drawable? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
                    if (ancestor is OsuGameBase candidate) { outer = candidate; break; }
                Assert.That(outer, Is.Not.Null);
                var content = (Container<Drawable>)typeof(OsuGameBase)
                    .GetProperty("Content", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.GetValue(outer)!;
                AlwaysPresent = true;
                Remove(Game, false);
                foreach (Drawable chrome in content.Children)
                {
                    chrome.Alpha = 0;
                    chrome.AlwaysPresent = true;
                }
                content.Add(Game);
                content.Add(new HitPhaseProbe(captureHitPhase) { AlwaysPresent = true });
                Game.Notifications.Hide();
                InputManager.MoveMouseTo(player.ScreenSpaceDrawQuad.Centre);
                freezeHitPhase = captureBurst;
            });

            if (captureBurst)
            {
                int phaseAttempts = 0;
                AddUntilStep("freeze an actual judgement effect at its middle frame", () =>
                {
                    if (++phaseAttempts == 45)
                        File.WriteAllText(Path.Combine(outputDirectory, "hit-probe-diagnostic.json"), System.Text.Json.JsonSerializer.Serialize(
                            new { sampledHitFrames, sampledEffects, sampledEffectStarts, lastSampledPhase, time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime }));
                    return frozenHitPhase.HasValue;
                });
                AddStep("capture the original effect beyond its lane and judgement line", () =>
                {
                    string prefix = doublePlay ? "14k" : style.ToString().ToLowerInvariant();
                    File.WriteAllText(Path.Combine(outputDirectory, "hit-phase.json"),
                        System.Text.Json.JsonSerializer.Serialize(new { gameplayTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime, phase = frozenHitPhase }));
                    capture = saveCapture($"{prefix}-hit-middle.png");
                });
                AddUntilStep("wait for the judgement frame PNG", () => capture?.IsCompleted == true);
                AddStep("verify the frozen note geometry and resume the same complete chart", () =>
                {
                    capture!.GetAwaiter().GetResult();
                    if (captureNoteGeometry)
                        recordAndAssertNoteGeometry("hit");
                    gameplayClock.Start();
                });
            }

            // At 3700ms the first hold is active and the following ordinary notes are already in their
            // real drawable lifetime. Earlier sampling can accidentally assert against an unbound pooled note.
            foreach (double time in new[] { 2600d, 3700d, 4200d, 6800d, 10100d })
            {
                double captureTime = time;
                AddUntilStep($"play naturally to {captureTime:0}ms", () =>
                {
                    if (renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime < captureTime)
                        return false;
                    // Freeze in the observing step. Waiting another visual step can advance past the real tail.
                    if (captureNoteGeometry && captureTime == 3700)
                        gameplayClock.Stop();
                    return true;
                });
                AddStep("verify no author scene faults", () => Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty));
                if (captureNoteGeometry && captureTime == 3700)
                    AddStep("record the actual holding note resources", () => recordAndAssertNoteGeometry("holding"));
                AddStep("capture the actual desktop framebuffer", () => capture = saveCapture($"{(doublePlay ? "14k" : style.ToString().ToLowerInvariant())}-{captureTime:00000}.png"));
                AddUntilStep("wait for PNG write", () => capture?.IsCompleted == true);
                AddStep("propagate capture failures", () => capture!.GetAwaiter().GetResult());
                if (captureNoteGeometry && captureTime == 3700)
                    AddStep("resume after the held-note capture", () => gameplayClock.Start());
            }

            AddUntilStep("finish the complete synthetic chart", () => score.HasCompleted.Value);
            if (captureFullCombo)
            {
                AddStep("retain the actual full-combo completion and freeze its outro", () =>
                {
                    Assert.That(score.IsFullCombo, Is.True);
                    using GameplaySkinEventSubscription subscription = renderer.GameplaySkinEventStream.Subscribe();
                    GameplaySkinCompletionStateSnapshot? completion = null;
                    subscription.DrainFrame(envelope => completion = ((GameplaySkinStateEventPayload)envelope.Payload).State.Completion);
                    Assert.That(completion?.IsFullCombo, Is.True);
                    completionOrigin = completion!.Value.AppliedTime;
                    completionDuration = renderer.GameplaySkinSceneRuntime!.GetCompletionOutroDuration(true);
                    Assert.That(completionDuration, Is.InRange(2500, 3500));
                    double[] recordedDurations = [.. renderer.GameplaySkinSceneRuntime.PreparedScene.RecordedAnimations.Values
                        .Where(program => program.Trigger == "gameplay.complete").Select(program => program.Duration).Distinct()];
                    if (recordedDurations.Length != 0)
                        Assert.That(recordedDurations, Is.EqualTo(new[] { completionDuration }));
                    gameplayClock.Stop();
                });
                foreach (int frame in completionFrames)
                {
                    int sourceFrame = frame;
                    AddStep($"seek the real completed replay to full-combo frame {sourceFrame}", () =>
                    {
                        completionSeekTime = completionOrigin + (sourceFrame - 1) * 1000.0 / 60 + 0.01;
                        Assert.That(completionSeekTime - completionOrigin, Is.InRange(0, completionDuration - 0.001));
                        gameplayClock.Seek(completionSeekTime);
                        gameplayClock.Stop();
                    });
                    AddUntilStep("wait for the same authoritative completion frame", () =>
                        Math.Abs(renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime - completionSeekTime) < 0.1);
                    AddStep("capture the original full-combo frame in the real Player", () =>
                    {
                        Assert.That(ReferenceEquals(Game.ScreenStack.CurrentScreen, player), Is.True);
                        Assert.That(renderer.GameplaySkinSceneRuntime!.RuntimeFaults, Is.Empty);
                        string prefix = doublePlay ? "14k" : style.ToString().ToLowerInvariant();
                        string filename = $"{prefix}-full-combo-{sourceFrame:000}";
                        File.WriteAllText(Path.Combine(outputDirectory, filename + ".json"), System.Text.Json.JsonSerializer.Serialize(
                            new { sourceFrame, completionOrigin, gameplayTime = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime }));
                        capture = saveCapture(filename + ".png");
                    });
                    AddUntilStep("wait for the full-combo frame PNG", () => capture?.IsCompleted == true);
                    AddStep("propagate the full-combo capture result", () => capture!.GetAwaiter().GetResult());
                }
                AddStep("resume the remaining transparent completion tail", () => gameplayClock.Start());
                AddUntilStep("the real Player leaves for results after the bounded outro", () => !ReferenceEquals(Game.ScreenStack.CurrentScreen, player));
            }
            AddAssert("autoplay produced real EX score and judgements", () => score.TotalScore.Value > 0 && score.HighestCombo.Value > 0);
            AddAssert("the author scene survived complete playback", () => renderer.GameplaySkinSceneRuntime!.RuntimeFaults.Count == 0);
        }

        private void captureHitPhase()
        {
            if (!freezeHitPhase || renderer?.GameplaySkinEventRuntime == null)
                return;

            sampledHitFrames++;
            double now = renderer.GameplaySkinEventRuntime.AuthoritativeGameplayTime;
            foreach (BmsPoolableHitExplosion effect in renderer.Playfield.Lanes.SelectMany(lane => lane.ActiveHitExplosions))
            {
                sampledEffects++;
                double? start = effect.SpecialisedSceneVisual?.RuntimeNodes.Select(node => node.JudgementAnimationStart)
                                      .FirstOrDefault(time => time.HasValue);
                if (start.HasValue)
                {
                    sampledEffectStarts++;
                    lastSampledPhase = now - start.Value;
                }
                if (!start.HasValue || now - start.Value < 50 || now - start.Value > 100)
                    continue;

                frozenHitPhase = now - start.Value;
                freezeHitPhase = false;
                gameplayClock.Stop();
                break;
            }
        }

        private sealed partial class HitPhaseProbe : Drawable
        {
            private readonly Action sample;

            public HitPhaseProbe(Action sample)
            {
                this.sample = sample;
            }

            protected override void Update()
            {
                base.Update();
                sample();
            }
        }

        private async Task saveCapture(string filename)
        {
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            if (pixels == null || pixels.Width < 640 || pixels.Height < 360)
                throw new InvalidOperationException("Desktop capture did not return a usable framebuffer.");
            await pixels.SaveAsPngAsync(Path.Combine(outputDirectory, filename)).ConfigureAwait(false);
        }

        private void recordBeamLayer(bool verify)
        {
            GameplaySkinSceneRuntimeHost runtime = renderer.GameplaySkinSceneRuntime!;
            GameplaySkinSceneHostedSlot[] beams = [.. runtime.HostedSlots.Where(slot => slot.RoutedNodes.Any(node =>
                node.Source.Id.StartsWith("b.b", StringComparison.Ordinal)))];
            File.WriteAllText(Path.Combine(outputDirectory, "beam-layer.json"), System.Text.Json.JsonSerializer.Serialize(beams.Select(beam => new
            {
                lane = beam.Key.Target.LaneId?.Value,
                slot = beam.Key.Slot.Id,
                layer = beam.Layer.ToString(),
            })));
            if (!verify)
                return;
            Assert.That(beams.Length, Is.EqualTo(runtime.Publication.Snapshot.LanesInLogicalOrder.Count));
            foreach (GameplaySkinSceneHostedSlot beam in beams)
            {
                Assert.That(beam.Key.Slot, Is.SameAs(GameplaySkinSlotCatalog.LaneSurface));
                Assert.That(beam.Layer, Is.EqualTo(GameplaySkinSceneLayer.Underlay));
                Assert.That(runtime.TryGetHostedDrawable(beam.Key, out Drawable? drawable), Is.True);
                Assert.That(drawable!.Parent, Is.SameAs(runtime.Layers.Underlay));
            }
        }

        private void recordAndAssertNoteGeometry(string phase)
        {
            bool hasHoldingNote = false;
            using GameplaySkinEventSubscription subscription = renderer.GameplaySkinEventStream.Subscribe();
            subscription.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinStateEventPayload state)
                {
                    hasHoldingNote = state.State.ActiveObjects.Any(obj => obj.State == GameplaySkinObjectState.Holding);
                    File.WriteAllText(Path.Combine(outputDirectory, $"notes-{phase}-state.json"), System.Text.Json.JsonSerializer.Serialize(new
                    {
                        time = renderer.GameplaySkinEventRuntime!.AuthoritativeGameplayTime,
                        objects = state.State.ActiveObjects.Select(obj => new
                        {
                            obj.ObjectId,
                            kind = obj.Kind.ToString(),
                            mode = obj.LongNoteMode.ToString(),
                            state = obj.State.ToString(),
                            obj.StartTime,
                            obj.EndTime,
                        }).ToArray(),
                    }));
                }
            });
            if (phase == "holding")
                Assert.That(hasHoldingNote, Is.True, "The long-note screenshot must sample a real hold, rather than its scheduled successor.");
            BmsAsyncNoteDrawable[] notes = [.. renderer.ChildrenOfType<BmsAsyncNoteDrawable>().Where(note => note.Lookup.UsesResolvedMaterial)];
            File.WriteAllText(Path.Combine(outputDirectory, $"notes-{phase}-geometry.json"), System.Text.Json.JsonSerializer.Serialize(notes.Select(note => new
            {
                element = note.Lookup.Element.ToString(),
                lane = note.Lookup.LaneIndex,
                route = note.SceneVisualGate.Route.ToString(),
                replacementReady = note.SceneVisualGate.IsReplacementReady,
                routed = note.SceneVisualGate.RoutedNodes.Select(node => node.Source.Id).ToArray(),
                applied = note.SpecialisedSceneVisual?.IsApplied,
                objectId = note.SpecialisedSceneVisual?.BoundObjectId,
                appliedNodes = note.AppliedSceneNodeIds,
                nodes = note.SpecialisedSceneVisual?.RuntimeNodes.Select(node => new
                {
                    id = node.PreparedNode.Source.Id,
                    rootHeight = node.RootDrawable.DrawHeight,
                    contentHeight = node.ContentDrawable.ScreenSpaceDrawQuad.Height,
                    alpha = node.ContentDrawable.Alpha,
                    texture = node.ContentDrawable is Framework.Graphics.Sprites.Sprite sprite
                        ? renderer.GameplaySkinSceneRuntime!.PreparedScene.Resources.FirstOrDefault(resource => ReferenceEquals(resource.Texture, sprite.Texture))?.Id
                        : null,
                }).ToArray(),
            })));

            BmsAsyncNoteDrawable[] activeNotes = [.. notes.Where(note => note.SpecialisedSceneVisual?.IsApplied == true)];
            Assert.That(activeNotes.Any(note => note.Lookup.Element == BmsNoteSkinElements.Note), Is.True,
                "The actual native note must consume its authored scene, rather than only the texture fallback.");
            foreach (BmsAsyncNoteDrawable note in activeNotes)
            {
                Assert.That(note.AppliedSceneNodeIds, Is.Not.Empty);
                Assert.That(note.SceneVisualGate.SuppressesProgrammaticVisual, Is.True);
                if (note.Lookup.Element is not (BmsNoteSkinElements.Note or BmsNoteSkinElements.LongNoteHead or BmsNoteSkinElements.LongNoteTail))
                    continue;
                float expectedHeight = note.Lookup.Element == BmsNoteSkinElements.Note ? 15 : 24;
                foreach (GameplaySkinSceneRuntimeNode node in note.SpecialisedSceneVisual!.RuntimeNodes)
                    Assert.That(node.ContentDrawable.ScreenSpaceDrawQuad.Height, Is.EqualTo(expectedHeight).Within(0.05),
                        "The authored source-pixel height must reach the real native pooled drawable.");
            }
        }

        private static IBeatmap createChart(BmsRuleset ruleset, bool doublePlay, bool fiveKey)
        {
            var text = new StringBuilder("#TITLE SPARKLE SHOWER / OMS author capture\n#ARTIST Offline reference chart\n#DIFFICULTY 3\n#PLAYLEVEL 10\n#BPM 150\n#LNTYPE 1\n#BPM01 180\n#00408:01\n#WAV01 note.wav\n");
            if (doublePlay)
                text.Append("#PLAYER 3\n");
            string[] channels = doublePlay
                ? ["11", "12", "13", "14", "15", "18", "19", "16", "21", "22", "23", "24", "25", "28", "29", "26"]
                : fiveKey ? ["11", "12", "13", "14", "15", "16"]
                : ["11", "12", "13", "14", "15", "18", "19", "16"];
            for (int measure = 1; measure <= 8; measure++)
            {
                for (int lane = 0; lane < channels.Length; lane++)
                {
                    string channel = channels[lane];
                    if (measure % 2 == 0)
                        channel = (channel[0] == '1' ? "5" : "6") + channel[1];
                    text.Append('#').Append(measure.ToString("000")).Append(channel).Append(':')
                        .Append(measure % 2 == 0 ? "01000100" : lane % 2 == 0 ? "01000001" : "00010000").Append('\n');
                }
            }
            var decoded = new BmsBeatmapDecoder().DecodeText(text.ToString(), fiveKey ? "author-capture.bms" : "author-capture.bme");
            return new BmsDecodedBeatmap(decoded) { BeatmapInfo = { Ruleset = ruleset.RulesetInfo } };
        }

        private sealed class PreservingImportTask : ImportTask
        {
            public PreservingImportTask(Stream stream, string filename)
                : base(stream, filename)
            {
            }

            public override void DeleteFile()
            {
                // The reference package is the author's deliverable; importing into disposable storage retains it.
            }
        }
    }
}
