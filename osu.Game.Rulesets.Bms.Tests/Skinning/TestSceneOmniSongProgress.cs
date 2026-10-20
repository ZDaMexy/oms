// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests;
using osu.Game.Tests.Visual;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Opt-in checks of the private, ordinarily imported Omni package against independent source geometry.
    /// No artwork, chart files or player data are included in this fixture.
    /// </summary>
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneOmniSongProgress : OsuGameTestScene
    {
        private static readonly JsonSerializerOptions receipt_options = new() { WriteIndented = true };

        private static readonly string[] all_frames =
        [
            "iidx27", "iidx28", "iidx29", "iidx30", "iidx31", "iidx32", "iidx33",
            "bpl-apina", "bpl-gamepanic", "bpl-gigo", "bpl-leisureland", "bpl-round1", "bpl-silkhat", "bpl-supernova-hd", "bpl-tradz",
        ];

        [Resolved]
        private GameHost host { get; set; } = null!;

        private SkinManager skins = null!;
        private Live<SkinInfo> imported = null!;
        private JsonElement reference;
        private ProgressPlayer player = null!;
        private ProgressLoader loader = null!;
        private DrawableRuleset renderer = null!;
        private GameplaySkinSceneRuntimeHost scene = null!;
        private GameplaySkinLayoutRevisionOwner owner = null!;
        private SkinCurrentRevision revision = null!;
        private int participantCount;
        private double firstHit;
        private double lastHit;
        private LayoutGeometry geometry = null!;
        private Cursor[] cursors = [];
        private Task? capture;
        private string? outputDirectory;
        private readonly List<Sample> samples = [];

        public override bool AutomaticallyRunFirstStep => false;

        protected override double TimePerAction => 20;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void requireIsolation(ExactVisualTestIsolation? isolation)
        {
            if (host is not HeadlessGameHost && isolation == null)
                throw new InvalidOperationException("Omni progress checks require the disposable exact-test runner.");
        }

        private static IEnumerable<TestCaseData> frameCases()
        {
            foreach (string frame in selectedFrames())
                yield return new TestCaseData(frame).SetName("OmniSongProgress_" + frame);
        }

        private static string[] selectedFrames()
        {
            string requested = Environment.GetEnvironmentVariable("OMS_OMNI_PROGRESS_FRAMES") ?? "iidx33";
            string[] frames = requested == "all" ? all_frames : requested.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (frames.Length == 0 || frames.Distinct(StringComparer.Ordinal).Count() != frames.Length || frames.Except(all_frames, StringComparer.Ordinal).Any())
                throw new ArgumentException("OMS_OMNI_PROGRESS_FRAMES must be all or a comma-separated selection of supported Frame IDs.");
            return frames;
        }

        [TestCaseSource(nameof(frameCases))]
        public void TestImportedOmniSongProgress(string frame)
        {
            PlayCase[] contexts = playCases().ToArray();
            string? requestedContexts = Environment.GetEnvironmentVariable("OMS_OMNI_PROGRESS_CONTEXTS");
            if (!string.IsNullOrWhiteSpace(requestedContexts) && requestedContexts != "all")
            {
                string[] requested = requestedContexts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (requested.Length == 0 || requested.Distinct(StringComparer.Ordinal).Count() != requested.Length
                    || requested.Except(contexts.Select(context => context.Id), StringComparer.Ordinal).Any())
                    throw new ArgumentException("OMS_OMNI_PROGRESS_CONTEXTS must select supported mode-key-count-side IDs.");
                contexts = contexts.Where(context => requested.Contains(context.Id, StringComparer.Ordinal)).ToArray();
            }
            string lifecycleFrame = selectedFrames().Contains("iidx33", StringComparer.Ordinal) ? "iidx33" : selectedFrames()[0];

            AddStep("require private package and independent AFP geometry", () =>
            {
                string? package = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_PACKAGE");
                string? geometryPath = Environment.GetEnvironmentVariable("OMS_OMNI_PROGRESS_GEOMETRY");
                if (string.IsNullOrWhiteSpace(package) || string.IsNullOrWhiteSpace(geometryPath))
                    Assert.Ignore("Set OMS_AUTHOR_SKIN_PACKAGE and OMS_OMNI_PROGRESS_GEOMETRY for the opt-in Omni check.");

                Assert.That(Path.GetPathRoot(Path.GetFullPath(package!)), Is.Not.EqualTo("D:\\").IgnoreCase,
                    "The source HDD is not an author-package input.");
                Assert.That(Path.GetPathRoot(Path.GetFullPath(geometryPath!)), Is.Not.EqualTo("D:\\").IgnoreCase);

                using JsonDocument source = JsonDocument.Parse(File.ReadAllBytes(geometryPath!));
                Assert.That(source.RootElement.GetProperty("contract").GetString(), Is.EqualTo("oms-iidx-music-progress-reference.v1"));
                reference = source.RootElement.GetProperty("frames").GetProperty(frame).Clone();
                foreach (string layout in new[] { "p1", "p2", "dp" })
                    readGeometry(layout);
                using (ZipArchive archive = ZipFile.OpenRead(package!))
                {
                    ZipArchiveEntry? cursor = archive.GetEntry("art/common/music-progress-cursor.png");
                    Assert.That(cursor, Is.Not.Null, "The normally imported package must retain the original shared cursor artwork.");
                    using Stream cursorBytes = cursor!.Open();
                    Assert.That(Convert.ToHexString(SHA256.HashData(cursorBytes)).ToLowerInvariant(),
                        Is.EqualTo(source.RootElement.GetProperty("texture").GetProperty("sha256").GetString()));
                }

                samples.Clear();
                string? output = Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY");
                outputDirectory = string.IsNullOrWhiteSpace(output) ? null : Path.Combine(Path.GetFullPath(output), frame);
                if (outputDirectory != null)
                    Directory.CreateDirectory(outputDirectory);
                if (host is not HeadlessGameHost)
                {
                    Assert.That(outputDirectory, Is.Not.Null, "Native evidence requires an output directory.");
                    var framework = Game.Dependencies.Get<FrameworkConfigManager>();
                    framework.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
                    framework.SetValue(FrameworkSetting.WindowedSize, new System.Drawing.Size(1920, 1080));
                }

                skins = Game.Dependencies.Get<SkinManager>();
                // A complete private library can exceed the UntilStep deadline. The isolated runner owns its watchdog.
                imported = skins.Import(new PreservingImportTask(File.OpenRead(package!), Path.GetFileName(package!))).GetAwaiter().GetResult();
                Assert.That(imported, Is.Not.Null);
            });

            foreach (PlayCase playCase in contexts)
            {
                PlayCase current = playCase;
                addEnter(frame, current);
                addPosition(current, 0, "start");
                addPosition(current, 0.5, "middle");
                if (frame == lifecycleFrame && current.DeepLifecycle)
                    addPauseAndRestart(current);
                addPosition(current, 1, "end");
                addExit(current);
            }
            AddAssert("all requested key-count and side combinations were checked", () =>
                samples.Count(sample => sample.Phase == "end"), () => Is.EqualTo(contexts.Length));
        }

        private void addEnter(string frame, PlayCase playCase)
        {
            AddStep("select " + playCase.Id + " with " + frame, () =>
            {
                Ruleset ruleset = playCase.Mania ? new ManiaRuleset() : new BmsRuleset();
                Game.Ruleset.Value = ruleset.RulesetInfo;
                Game.Beatmap.Value = CreateWorkingBeatmap(playCase.Mania ? createManiaChart(ruleset, playCase.Keys) : createBmsChart(ruleset, playCase.Keys));
                Game.SelectedMods.Value = [ruleset.GetAutoplayMod()!];
                skins.SelectionPreferences!.SetSkin(new SkinSelectionScope(playCase.Mode), imported.ID);
                skins.SelectionPreferences.SetSkin(new SkinSelectionScope(playCase.Mode, playCase.Keys), imported.ID);
                skins.CurrentSkinInfo.Value = imported;
                var preferences = Game.Dependencies.Get<SkinOptionPreferences>();
                preferences.SetOverride(imported.ID, playCase.Mode, "frame", frame);
                preferences.SetOverride(imported.ID, playCase.Mode, "layout", playCase.Layout == "p2" ? "p2" : "p1");
                geometry = readGeometry(playCase.Layout);
                DismissAnyNotifications();
            });
            AddUntilStep("wait for the normally imported exact skin", () =>
                skins.CurrentSkin.Value.SkinInfo.ID == imported.ID && skins.LastSelectionRejectionReason == SkinSelectionRejectionReason.None);
            AddStep("enter the real PlayerLoader for " + playCase.Id, () =>
            {
                revision = skins.CurrentRevision;
                participantCount = revision.ParticipantLeaseCount;
                Ruleset ruleset = playCase.Mania ? new ManiaRuleset() : new BmsRuleset();
                ModAutoplay autoplay = ruleset.GetAutoplayMod()!;
                Game.ScreenStack.Push(loader = new ProgressLoader(() => player = new ProgressPlayer(autoplay.CreateScoreFromReplayData)));
            });
            addWaitForPlayer();
            AddStep("inspect exact committed cursor instances for " + playCase.Id, () =>
            {
                bindPlayer(playCase);
                Assert.That(owner.CurrentPublication!.Snapshot.Context.OptionValues.TryGetValue("frame", out GameplaySkinOptionValue selected), Is.True);
                Assert.That(selected.CanonicalValue, Is.EqualTo(frame));
            });
        }

        private void addWaitForPlayer()
        {
            AddUntilStep("wait for the real loaded Player and committed scene", () =>
            {
                DismissAnyNotifications();
                return player != null && ReferenceEquals(Game.ScreenStack.CurrentScreen, player) && player.LoadedBeatmapSuccessfully
                    && player.Alpha == 1 && player.GameplayClock.Alpha == 1
                    && player.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()?.IsSceneReady == true;
            });
        }

        private void bindPlayer(PlayCase playCase)
        {
            renderer = player.ChildrenOfType<DrawableRuleset>().Single();
            scene = renderer.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
            owner = renderer.Dependencies.Get<GameplaySkinLayoutRevisionOwner>();
            Assert.That(owner.PackageRevision.RetainsExact(skins.CurrentRevision), Is.True);
            Assert.That(CanonicalSkinPackage.IsCanonicalSkin(skins.CurrentSkin.Value), Is.False);
            Assert.That(scene.RuntimeFaults, Is.Empty);
            Assert.That(scene.MaterialSet.Diagnostics, Is.Empty);
            Assert.That(owner.CurrentPublication!.Snapshot.Context.RulesetId, Is.EqualTo(playCase.Mode));
            Assert.That(owner.CurrentPublication.Snapshot.Context.Topology.GroupsInLogicalOrder.Count, Is.EqualTo(playCase.Dual ? 2 : 1));
            Assert.That(owner.CurrentPublication.Snapshot.Context.Topology.LanesInLogicalOrder.Count,
                Is.EqualTo(playCase.Keys + (playCase.Mania ? 0 : playCase.Dual ? 2 : 1)));
            (firstHit, lastHit) = BeatmapExtensions.CalculatePlayableBounds(renderer.Objects);
            Assert.That(lastHit, Is.GreaterThan(firstHit));

            GameplaySkinPreparedSceneNode[] all = walk(scene.PreparedScene.Roots).ToArray();
            GameplaySkinPreparedSceneNode[] prepared = all.Where(node => node.Source.Id.Contains(".music-progress.", StringComparison.Ordinal)
                && node.Source.Id.EndsWith(".cursor", StringComparison.Ordinal)).ToArray();
            Assert.That(prepared, Has.Length.EqualTo(geometry.Cursors.Length), "All source cursor array entries must survive exact layout expansion, including DP duplicates.");
            cursors = geometry.Cursors.Select((expected, index) =>
            {
                GameplaySkinPreparedSceneNode node = prepared.Single(candidate => candidate.Source.Id.EndsWith($".music-progress.o{index}.cursor", StringComparison.Ordinal));
                string trackId = node.Source.Id[..^".cursor".Length] + ".track";
                GameplaySkinPreparedSceneNode track = all.Single(candidate => candidate.Source.Id == trackId);
                Assert.That(scene.TryGetRuntimeNode(node.InstanceId, out GameplaySkinSceneRuntimeNode? runtime), Is.True);
                Assert.That(scene.TryGetRuntimeNode(track.InstanceId, out GameplaySkinSceneRuntimeNode? trackRuntime), Is.True);
                Assert.That(scene.PreparedScene.Program.Bindings.Count(binding => binding.TargetNodeId == node.Source.Id
                    && binding.Property == GameplaySkinSceneProperty.Y && binding.Source == GameplaySkinSceneBindingSource.TimingProgress), Is.EqualTo(1));
                Assert.That(runtime!.ContentDrawable, Is.TypeOf<Sprite>());
                Assert.That(((Sprite)runtime.ContentDrawable).Texture, Is.Not.Null);
                Assert.That(node.Resource?.Id, Is.EqualTo("omni.music-progress.cursor"));
                Assert.That(node.Source.Blend, Is.EqualTo(GameplaySkinSceneBlendMode.Additive));
                Assert.That(((Sprite)runtime.ContentDrawable).Texture, Is.SameAs(node.ResolvedTexture));
                Assert.That(runtime.RootDrawable.Parent, Is.SameAs(trackRuntime!.TransformDrawable));
                return new Cursor(expected, runtime, trackRuntime!);
            }).ToArray();
        }

        private void addPosition(PlayCase playCase, double progress, string phase)
        {
            double destination = 0;
            AddStep($"seek {playCase.Id} to {progress:P0}", () =>
            {
                destination = progress switch { 0 => firstHit - 500, 1 => lastHit + 100, _ => firstHit + (lastHit - firstHit) * progress };
                player.GameplayClock.Stop();
                player.GameplayClock.Seek(destination);
            });
            AddUntilStep("wait for the same authoritative timing and cursor binding", () =>
                Math.Abs(scene.LastGameplayTime - destination) < 0.05 && Math.Abs(snapshot().Timing.Progress - progress) < 0.00001
                && cursors.All(cursor => Math.Abs(cursor.Node.TransformDrawable.Y - progress) < 0.00001));
            AddStep("compare actual cursor bounds with independent AFP geometry", () => assertSample(playCase, phase, progress));
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OMS_AUTHOR_SKIN_CAPTURE_DIRECTORY")))
            {
                AddStep("capture native progress pixels when a real renderer is present", () =>
                    capture = host is HeadlessGameHost ? Task.CompletedTask : saveCapture(playCase.Id + "-" + phase + ".png"));
                AddUntilStep("wait for native framebuffer", () => capture?.IsCompleted == true);
                AddStep("propagate native capture errors", () => capture!.GetAwaiter().GetResult());
            }
        }

        private void addPauseAndRestart(PlayCase playCase)
        {
            double pausedProgress = 0;
            double resumeTime = 0;
            ProgressPlayer previous = null!;
            GameplaySkinLayoutRevisionOwner previousOwner = null!;
            AddStep("resume through the real replay pause action", () =>
            {
                resumeTime = player.GameplayClock.CurrentTime;
                toggleReplayPause();
            });
            AddUntilStep("the production track advances naturally after resume", () => player.GameplayClock.IsRunning && player.GameplayClock.CurrentTime > resumeTime + 20);
            AddStep("pause through the real replay pause action", toggleReplayPause);
            AddUntilStep("the paused authoritative timing has reached the scene", () =>
                player.GameplayClock.IsPaused.Value && Math.Abs(scene.LastGameplayTime - player.GameplayClock.CurrentTime) < 0.05);
            AddStep("retain the actual paused progress", () =>
            {
                pausedProgress = snapshot().Timing.Progress;
                Assert.That(pausedProgress, Is.GreaterThan(0.5).And.LessThan(1));
                assertSample(playCase, "paused", pausedProgress);
            });
            AddWaitStep("allow UI frames while gameplay is paused", 3);
            AddStep("pause preserves timing and cursor bounds", () => assertSample(playCase, "pause-held", pausedProgress));
            AddStep("restart through the real PlayerLoader factory", () =>
            {
                previous = player;
                previousOwner = owner;
                Assert.That(previous.Restart(), Is.True);
                previous.RemoveAndDisposeImmediately();
            });
            AddUntilStep("the previous retry root has retired", () => previousOwner.IsDisposed && previous.Parent == null);
            AddUntilStep("the loader creates a distinct retry Player", () => !ReferenceEquals(player, previous));
            addWaitForPlayer();
            AddStep("retry begins with a new owner and no residual cursor progress", () =>
            {
                bindPlayer(playCase);
                Assert.That(owner, Is.Not.SameAs(previousOwner));
                Assert.That(snapshot().Timing.Progress, Is.Zero);
                assertSample(playCase, "retry", 0);
            });
        }

        private void toggleReplayPause()
            => Assert.That(player.OnPressed(new KeyBindingPressEvent<GlobalAction>(InputManager.CurrentState, GlobalAction.TogglePauseReplay)), Is.True);

        private void addExit(PlayCase playCase)
        {
            ProgressPlayer exited = null!;
            ProgressLoader exitedLoader = null!;
            GameplaySkinLayoutRevisionOwner retired = null!;
            AddStep("leave " + playCase.Id + " through the screen stack", () =>
            {
                exited = player;
                exitedLoader = loader;
                retired = owner;
                exited.Exit();
            });
            AddUntilStep("return to the real main menu", () => Game.ScreenStack.CurrentScreen is MainMenu);
            AddStep("dispose the retained previous gameplay and loader trees", () =>
            {
                exited.RemoveAndDisposeImmediately();
                exitedLoader.RemoveAndDisposeImmediately();
            });
            AddUntilStep("wait for the exact layout owner and all participant leases to retire", () =>
                retired.IsDisposed && exited.Parent == null && revision.ParticipantLeaseCount == participantCount);
        }

        private void assertSample(PlayCase playCase, string phase, double progress)
        {
            Assert.That(scene.RuntimeFaults, Is.Empty);
            Assert.That(snapshot().Timing.Progress, Is.EqualTo(progress).Within(0.00001));
            double expectedProgress = Math.Clamp((player.GameplayClock.CurrentTime - firstHit) / (lastHit - firstHit), 0, 1);
            Assert.That(progress, Is.EqualTo(expectedProgress).Within(0.00001), "The package consumes the actual gameplay clock and playable bounds.");
            var bounds = new List<double[]>();
            foreach (Cursor cursor in cursors)
            {
                Assert.That(cursor.Node.TransformDrawable.Y, Is.EqualTo(progress).Within(0.00001));
                Assert.That(cursor.Node.ContentDrawable.IsPresent, Is.True);
                double[] actual = canvasBounds(cursor.Node);
                double[] expected = cursor.Geometry.Start.Zip(cursor.Geometry.End, (start, end) => start + (end - start) * progress).ToArray();
                for (int coordinate = 0; coordinate < 4; coordinate++)
                    Assert.That(actual[coordinate], Is.EqualTo(expected[coordinate]).Within(0.15), $"{playCase.Id}/{phase}/{cursor.Geometry.Id}: source bbox component {coordinate}");
                double[] track = canvasBounds(cursor.Track);
                Assert.That(track[0], Is.EqualTo(cursor.Geometry.Start[0]).Within(0.15));
                Assert.That(track[1], Is.EqualTo(cursor.Geometry.Start[1]).Within(0.15));
                Assert.That(track[2], Is.EqualTo(cursor.Geometry.Start[2]).Within(0.15));
                Assert.That(track[3], Is.EqualTo(cursor.Geometry.End[1] - cursor.Geometry.Start[1]).Within(0.15));
                bounds.Add(actual);
            }
            samples.Add(new Sample(playCase.Id, phase, player.GameplayClock.CurrentTime, progress, bounds.ToArray()));
            if (outputDirectory != null)
                File.WriteAllText(Path.Combine(outputDirectory, "progress.json"), JsonSerializer.Serialize(samples, receipt_options));
        }

        private double[] canvasBounds(GameplaySkinSceneRuntimeNode node)
        {
            Container canvas = scene.Layers.Get(node.PreparedNode.Layer);
            Vector2 topLeft = canvas.ToLocalSpace(node.ContentDrawable.ScreenSpaceDrawQuad.TopLeft);
            Vector2 bottomRight = canvas.ToLocalSpace(node.ContentDrawable.ScreenSpaceDrawQuad.BottomRight);
            return [topLeft.X / canvas.DrawWidth * geometry.Canvas[0], topLeft.Y / canvas.DrawHeight * geometry.Canvas[1],
                (bottomRight.X - topLeft.X) / canvas.DrawWidth * geometry.Canvas[0], (bottomRight.Y - topLeft.Y) / canvas.DrawHeight * geometry.Canvas[1]];
        }

        private GameplaySkinEventStateSnapshot snapshot()
        {
            GameplaySkinEventStateSnapshot? state = null;
            using GameplaySkinEventSubscription observer = scene.EventStream.Subscribe();
            observer.DrainFrame(envelope =>
            {
                if (envelope.Payload is GameplaySkinStateEventPayload complete)
                    state = complete.State;
            });
            Assert.That(state, Is.Not.Null, "A late observer must receive the actual complete Player state.");
            return state!;
        }

        private LayoutGeometry readGeometry(string layout)
        {
            JsonElement source = reference.GetProperty(layout);
            double[] canvas = source.GetProperty("canvas").EnumerateArray().Select(value => value.GetDouble()).ToArray();
            Assert.That(canvas, Is.EqualTo(new[] { 1920d, 1080d }));
            CursorGeometry[] expected = source.GetProperty("cursors").EnumerateArray().Select(cursor => new CursorGeometry(
                cursor.GetProperty("id").GetString()!,
                cursor.GetProperty("start").EnumerateArray().Select(value => value.GetDouble()).ToArray(),
                cursor.GetProperty("end").EnumerateArray().Select(value => value.GetDouble()).ToArray())).ToArray();
            Assert.That(expected, Has.Length.EqualTo(layout == "dp" ? 2 : 1));
            foreach (CursorGeometry cursor in expected)
            {
                Assert.That(cursor.Start, Has.Length.EqualTo(4));
                Assert.That(cursor.End, Has.Length.EqualTo(4));
                Assert.That(cursor.Start.Concat(cursor.End).All(double.IsFinite), Is.True);
                Assert.That(cursor.End[1], Is.GreaterThan(cursor.Start[1]));
            }
            return new LayoutGeometry(canvas, expected);
        }

        private async Task saveCapture(string filename)
        {
            using Image<Rgba32> image = await host.TakeScreenshotAsync().ConfigureAwait(false);
            await image.SaveAsPngAsync(Path.Combine(outputDirectory!, filename)).ConfigureAwait(false);
        }

        private static IEnumerable<GameplaySkinPreparedSceneNode> walk(IEnumerable<GameplaySkinPreparedSceneNode> roots)
        {
            foreach (GameplaySkinPreparedSceneNode node in roots)
            {
                yield return node;
                foreach (GameplaySkinPreparedSceneNode descendant in walk(node.Children))
                    yield return descendant;
            }
        }

        private static IEnumerable<PlayCase> playCases()
        {
            foreach (int keys in new[] { 5, 7 })
                foreach (string side in new[] { "p1", "p2" })
                    yield return new PlayCase(false, keys, side);
            yield return new PlayCase(false, 14, "dp");
            foreach (int keys in new[] { 5, 6, 7, 8 })
                foreach (string side in new[] { "p1", "p2" })
                    yield return new PlayCase(true, keys, side);
            foreach (int keys in new[] { 14, 16 })
                yield return new PlayCase(true, keys, "dp");
        }

        private static IBeatmap createBmsChart(Ruleset ruleset, int keys)
        {
            var text = new StringBuilder("#TITLE Omni song progress\n#BPM 120\n#LNTYPE 1\n#WAV01 note.wav\n");
            if (keys == 14)
                text.Append("#PLAYER 3\n");
            string[] channels = keys switch
            {
                5 => ["11", "12", "13", "14", "15", "16"],
                7 => ["11", "12", "13", "14", "15", "18", "19", "16"],
                14 => ["11", "12", "13", "14", "15", "18", "19", "16", "21", "22", "23", "24", "25", "28", "29", "26"],
                _ => throw new ArgumentOutOfRangeException(nameof(keys)),
            };
            foreach (string channel in channels)
                text.Append("#001").Append(channel).Append(":01\n#002").Append(channel).Append(":01\n");
            text.Append("#00451:0101\n");
            return new BmsDecodedBeatmap(new BmsBeatmapDecoder().DecodeText(text.ToString(), keys == 5 ? "progress.bms" : "progress.bme"))
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };
        }

        private static IBeatmap createManiaChart(Ruleset ruleset, int keys)
        {
            int columns = keys > 8 ? keys / 2 : keys;
            var beatmap = new ManiaBeatmap(new StageDefinition(columns)) { BeatmapInfo = { Ruleset = ruleset.RulesetInfo } };
            if (keys > 8)
                beatmap.Stages.Add(new StageDefinition(columns));
            beatmap.Difficulty.CircleSize = keys;
            for (int column = 0; column < keys; column++)
            {
                beatmap.HitObjects.Add(new Note { Column = column, StartTime = 2000 });
                beatmap.HitObjects.Add(new Note { Column = column, StartTime = 4000 });
                beatmap.HitObjects.Add(new HoldNote { Column = column, StartTime = 8000, Duration = 1000 });
            }
            beatmap.HitObjects.Sort((left, right) => left.StartTime.CompareTo(right.StartTime));
            return beatmap;
        }

        private sealed record PlayCase(bool Mania, int Keys, string Layout)
        {
            public string Mode => Mania ? "mania" : "bms";
            public string Id => $"{Mode}-{Keys}k-{Layout}";
            public bool Dual => Keys > 8;
            public bool DeepLifecycle => Id is "bms-7k-p1" or "mania-7k-p2" or "mania-16k-dp";
        }

        private sealed record CursorGeometry(string Id, double[] Start, double[] End);
        private sealed record LayoutGeometry(double[] Canvas, CursorGeometry[] Cursors);
        private sealed record Cursor(CursorGeometry Geometry, GameplaySkinSceneRuntimeNode Node, GameplaySkinSceneRuntimeNode Track);
        private sealed record Sample(string Context, string Phase, double Time, double Progress, double[][] Bounds);

        private partial class ProgressPlayer : ReplayPlayer
        {
            public GameplayClockContainer GameplayClock => GameplayClockContainer;

            public ProgressPlayer(Func<IBeatmap, IReadOnlyList<Mod>, Score> createScore)
                : base(createScore, new PlayerConfiguration { ShowResults = false })
            {
                // Preserve the production clock and start/reset path, then hold the initial frame for explicit seeks.
                OnGameplayStarted += () => GameplayClockContainer.Stop();
            }
        }

        private partial class ProgressLoader : PlayerLoader
        {
            protected override double PlayerPushDelay => 0;
            protected override bool ReadyForGameplay => true;

            public ProgressLoader(Func<Player> createPlayer)
                : base(createPlayer)
            {
            }
        }

        private sealed class PreservingImportTask : ImportTask
        {
            public PreservingImportTask(Stream stream, string filename)
                : base(stream, filename)
            {
            }

            public override void DeleteFile()
            {
            }
        }
    }
}
