// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Tools.StandardSkinCapture;

public partial class TestSceneStandardSkinCapture
{
    private static bool IsLongNoteTransitionCase => CaptureSettings.Current.ChartVariant == "long-note-transitions";

    private GameplaySkinEventSubscription? longNoteSubscription;
    private readonly List<LongNoteEdge> longNoteEdges = new List<LongNoteEdge>();
    private readonly Dictionary<long, (long Epoch, GameplaySkinObjectState State, double? Origin, GameplaySkinObjectState? Previous, double? Transition)> lastLongNoteStates = new();
    private readonly Dictionary<GameplaySkinSpecialisedSceneVisual, int> longNoteVisualIds = new();
    private readonly Dictionary<GameplaySkinSpecialisedSceneVisual, long?> lastLongNoteLeases = new();
    private readonly List<LongNoteLease> longNoteLeases = new();

    private sealed record LongNoteEdge(long Epoch, long Sequence, double GameplayTime, string Delivery, string Kind, GameplaySkinObjectStateSnapshot State)
    {
        public string StateName => State.State.ToString();
        public string? PreviousStateName => State.PreviousState?.ToString();
    }
    private sealed record LongNoteLease(int VisualId, long? ObjectId, double GameplayTime, string Lane, bool Applied);
    private sealed record ReplayInputPoint(double Time, int Action, bool Held);

    private StringBuilder CreateTransitionBmsText(string[] channels)
    {
        var text = new StringBuilder("#TITLE Standard native long-note transitions\n#ARTIST Offline synthetic input verification\n#DIFFICULTY 3\n#PLAYLEVEL 10\n#BPM 120\n#LNTYPE 1\n#WAV01 note.wav\n"
                                     + $"#BMP01 {BgaCaptureWorkingBeatmap.ImageFilename}\n#00104:01\n");
        if (settings.Keys == 14)
            text.Append("#PLAYER 3\n");
        var rows = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        void signal(int time, string channel)
        {
            int measure = time / 2000;
            int position = time % 2000 / 250;
            string key = measure.ToString("000") + channel;
            if (!rows.TryGetValue(key, out string[]? cells))
                rows.Add(key, cells = Enumerable.Repeat("00", 8).ToArray());
            if (cells[position] != "00")
                throw new InvalidOperationException("The native fixture contains overlapping source notes.");
            cells[position] = "01";
        }
        foreach ((int start, int end, string[] lanes) in new[]
                 {
                     (4000, 8000, new[] { "11", "12", "16" }),
                     (10000, 14000, new[] { "13", "14", "16" }),
                     (16000, 20000, new[] { "11", "12", "16" }),
                     (22000, 24000, new[] { "11", "12", "16" }),
                 })
            foreach (string lane in lanes)
            {
                string longChannel = "5" + lane[1];
                signal(start, longChannel);
                signal(end, longChannel);
            }
        foreach (string channel in channels.Except(new[] { "11", "12", "13", "14", "16" }, StringComparer.Ordinal))
            for (int time = 2000; time <= 28000; time += 250)
                signal(time, channel);
        foreach ((string key, string[] cells) in rows)
            text.Append('#').Append(key).Append(':').Append(string.Concat(cells)).Append('\n');
        return text;
    }

    private static void FillTransitionManiaChart(ManiaBeatmap beatmap)
    {
        foreach ((int start, int end) in new[] { (4000, 8000), (10000, 14000), (16000, 20000), (22000, 24000) })
            for (int column = 0; column < 3; column++)
                beatmap.HitObjects.Add(new HoldNote { Column = column, StartTime = start, Duration = end - start });
        for (int column = 3; column < beatmap.TotalColumns; column++)
            for (int time = 2000; time <= 28000; time += 250)
                beatmap.HitObjects.Add(new Note { Column = column, StartTime = time });
        beatmap.HitObjects.Sort((left, right) => left.StartTime.CompareTo(right.StartTime));
    }

    private Score CreateTransitionReplay(IBeatmap beatmap, IReadOnlyList<Mod> mods)
    {
        if (mods.Any(mod => mod is ModAutoplay or ModNoFail))
            throw new InvalidOperationException("Transition verification requires ordinary replay input, without Autoplay or NoFail.");
        var points = new List<ReplayInputPoint>();
        var holds = new List<object>();
        void add(double start, double end, int action, bool longNote)
        {
            if (!longNote)
            {
                points.Add(new ReplayInputPoint(start, action, true));
                points.Add(new ReplayInputPoint(start + 20, action, false));
                return;
            }
            holds.Add(new { start, end, action });
            switch (start)
            {
                case 4000:
                    points.AddRange(new[] { new ReplayInputPoint(start, action, true), new ReplayInputPoint(5200, action, false),
                        new ReplayInputPoint(6400, action, true), new ReplayInputPoint(end, action, false) });
                    break;
                case 10000:
                    points.AddRange(new[] { new ReplayInputPoint(11600, action, true), new ReplayInputPoint(end, action, false) });
                    break;
                case 16000:
                    points.AddRange(new[] { new ReplayInputPoint(start, action, true), new ReplayInputPoint(end - 40, action, false) });
                    break;
                case 22000:
                    points.AddRange(new[] { new ReplayInputPoint(start, action, true), new ReplayInputPoint(end, action, false) });
                    break;
                default:
                    throw new InvalidOperationException("An unexpected long note entered the transition fixture.");
            }
        }
        if (settings.Ruleset == "bms")
        {
            foreach (BmsHitObject hitObject in beatmap.HitObjects.OfType<BmsHitObject>())
                add(hitObject.StartTime, hitObject is BmsHoldNote hold ? hold.StartTime + hold.Duration : hitObject.StartTime,
                    (int)BmsActionExtensions.GetLaneAction(hitObject.LaneIndex, hitObject.IsScratch), hitObject is BmsHoldNote);
        }
        else
        {
            foreach (ManiaHitObject hitObject in beatmap.HitObjects.OfType<ManiaHitObject>())
                add(hitObject.StartTime, hitObject is HoldNote hold ? hold.StartTime + hold.Duration : hitObject.StartTime,
                    (int)ManiaAction.Key1 + hitObject.Column, hitObject is HoldNote);
        }
        if (holds.Count != 12)
            throw new InvalidOperationException("The converted fixture must contain the twelve planned native holds.");
        var actions = new SortedSet<int>();
        var frames = new List<ReplayFrame> { makeFrame(0) };
        foreach (var group in points.GroupBy(point => point.Time).OrderBy(group => group.Key))
        {
            foreach (ReplayInputPoint point in group.OrderBy(point => point.Held))
            {
                bool changed = point.Held ? actions.Add(point.Action) : actions.Remove(point.Action);
                if (!changed)
                    throw new InvalidOperationException("The fixture's replay has an unpaired press/release.");
            }
            frames.Add(makeFrame(group.Key));
        }
        if (actions.Count != 0)
            throw new InvalidOperationException("The fixture replay leaves a key held.");
        frames.Add(makeFrame(28200));
        Score result = ruleset.GetAutoplayMod()!.CreateScoreFromReplayData(beatmap, mods);
        // The original generator is used only for ordinary score/user metadata. Its generated-autoplay
        // replay marker and every generated frame are replaced by the real input fixture below.
        result.Replay = new Replay { Frames = frames };
        result.ScoreInfo.User.Username = "OMS native input fixture";
        settings.Write("native-input-sequence.json", new
        {
            source = "ReplayPlayer -> native framed replay input handler -> real ruleset judgements; no skin event injection",
            replayType = result.Replay.GetType().FullName,
            selectedMods = mods.Select(mod => mod.Acronym).ToArray(),
            holds,
            edges = points.OrderBy(point => point.Time).ThenBy(point => point.Action).ToArray(),
            frameCount = frames.Count,
            sourceTimes = "Milliseconds on the real replay clock; press4000/release5200/regrab6400, head10000 omitted until11600, early tail19960, fresh owner22000.",
        });
        return result;

        ReplayFrame makeFrame(double time) => settings.Ruleset == "bms"
            ? new BmsReplayFrame(time, actions.Select(action => (BmsAction)action).ToArray())
            : new ManiaReplayFrame(time, actions.Select(action => (ManiaAction)action).ToArray());
    }

    private void StartLongNoteObservations()
    {
        if (longNoteSubscription != null)
            throw new InvalidOperationException("The native observation subscription was already attached.");
        longNoteSubscription = eventStream.Subscribe();
        DrainLongNoteObservations();
    }

    protected override void Update()
    {
        base.Update();
        DrainLongNoteObservations();
    }

    private void DrainLongNoteObservations()
    {
        longNoteSubscription?.DrainFrame(envelope =>
        {
            if (envelope.Payload is GameplaySkinObjectEventPayload payload)
                record(payload.State);
            else if (envelope.Payload is GameplaySkinStateEventPayload snapshot)
                foreach (GameplaySkinObjectStateSnapshot state in snapshot.State.ActiveObjects)
                    record(state);

            void record(GameplaySkinObjectStateSnapshot state)
            {
                if (state.Kind != GameplaySkinObjectKind.LongNote)
                    return;
                var signature = (envelope.Epoch, state.State, state.StateChangedAt, state.PreviousState, state.LastLongNoteTransitionAt);
                if (lastLongNoteStates.GetValueOrDefault(state.ObjectId) == signature)
                    return;
                lastLongNoteStates[state.ObjectId] = signature;
                if (longNoteEdges.Count >= 512)
                    throw new InvalidOperationException("The bounded fixture unexpectedly exceeded its native transition history.");
                longNoteEdges.Add(new LongNoteEdge(envelope.Epoch, envelope.Sequence, envelope.GameplayTime,
                    envelope.DeliveryKind.ToString(), envelope.EventKind.ToString(), state));
            }
        });
        if (longNoteSubscription == null || player == null)
            return;
        foreach (GameplaySkinSpecialisedSceneVisual visual in player.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                     .Where(visual => visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteEffect))
        {
            if (!longNoteVisualIds.ContainsKey(visual))
                longNoteVisualIds.Add(visual, longNoteVisualIds.Count + 1);
        }
        // A returned pooled visual leaves the live player subtree. Observe already discovered native
        // owners as well, so its actual OnFree becomes a release receipt instead of a missing edge.
        foreach ((GameplaySkinSpecialisedSceneVisual visual, int identifier) in longNoteVisualIds)
        {
            long? binding = visual.IsApplied ? visual.BoundObjectId : null;
            if (lastLongNoteLeases.TryGetValue(visual, out long? previous) && previous == binding)
                continue;
            lastLongNoteLeases[visual] = binding;
            longNoteLeases.Add(new LongNoteLease(identifier, binding, ReadProperty<double>(events, "AuthoritativeGameplayTime"),
                visual.Key.Target.LaneId?.Value ?? "", visual.IsApplied));
        }
    }

    private void AddLongNoteTransitionCaptures()
    {
        foreach ((int time, string label) in new[] { (4600, "holding"), (5400, "released"), (6600, "regrab"),
                     (8100, "tail"), (10600, "head-miss"), (11800, "head-miss-regrab"),
                     (20050, "early-tail"), (22300, "pooled-reuse"), (24600, "terminal-cleanup") })
        {
            int sample = time;
            string name = "long-note-" + label;
            AddUntilStep("natural input fixture reaches " + label, () => ReadProperty<double>(events, "AuthoritativeGameplayTime") >= sample);
            AddStep("record exact native long-note state and animation", () =>
            {
                Assert.That(ReferenceEquals(Game.ScreenStack.CurrentScreen, player), Is.True);
                Assert.That(runtime.RuntimeFaults, Is.Empty);
                DrainLongNoteObservations();
                RecordGameplay(name, sample);
                var visuals = player.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Where(visual => visual.IsApplied && visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteEffect).ToArray();
                string heldRole = settings.Ruleset == "bms" && settings.LongNoteMode == "hcn" ? "hcn" : "cn";
                bool hellCharge = settings.Ruleset == "bms" && settings.LongNoteMode == "hcn";
                if (label is "holding" or "pooled-reuse")
                    Assert.That(visuals.Any(visual => HasVisibleLongNotePieces(visual, heldRole)), Is.True,
                        "The original sustained effect must have real textured pieces on a native hold owner.");
                if (label == "released")
                    Assert.That(visuals.Any(visual => HasVisibleLongNotePieces(visual, "break")), Is.EqualTo(hellCharge));
                if (label == "regrab")
                    Assert.That(visuals.Any(visual => HasVisibleLongNotePieces(visual, heldRole)),
                        Is.EqualTo(hellCharge || settings.Ruleset == "mania"));
                if (label == "head-miss")
                {
                    Assert.That(visuals.Any(visual => HasVisibleLongNotePieces(visual, "break")), Is.EqualTo(hellCharge));
                    Assert.That(visuals.Any(visual => HasVisibleLongNotePieces(visual, "base")), Is.False,
                        "A direct head miss must not invent a base transition burst.");
                }
                if (label == "early-tail")
                    Assert.That(visuals.Any(visual => HasVisibleLongNotePieces(visual, "base")), Is.True,
                        "An actual early tail release must retain the original bounded one-shot burst.");
                if (label == "terminal-cleanup")
                    Assert.That(visuals, Is.Empty, "Completed native holds must release their bounded fixed-line visuals.");
                settings.Write(name + "-native.json", new
                {
                    actualGameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime"),
                    publicStateSemantics = LongNotePublicStateSemantics(),
                    states = ReadState().ActiveObjects.Where(state => state.Kind == GameplaySkinObjectKind.LongNote).ToArray(),
                    edges = longNoteEdges.ToArray(),
                    leases = longNoteLeases.ToArray(),
                    visuals = visuals.Select(visual => new
                    {
                        visualId = longNoteVisualIds[visual],
                        visual.BoundObjectId,
                        visual.IsApplied,
                        lane = visual.Key.Target.LaneId?.Value,
                        visual.RequestedLongNoteOutroDuration,
                        nodes = visual.RuntimeNodes.Select(NodeEvidence).ToArray(),
                        animations = ObserveLongNoteAnimations(visual),
                    }).ToArray(),
                });
                screenshot = Capture(name);
            });
            AddUntilStep("wait for long-note framebuffer", () => screenshot?.IsCompleted == true);
            AddStep("propagate long-note framebuffer errors", () => screenshot!.GetAwaiter().GetResult());
        }
    }

    private object[] ObserveLongNoteAnimations(GameplaySkinSpecialisedSceneVisual visual)
        => visual.ChildrenOfType<CompositeDrawable>()
            .Where(drawable => drawable.GetType().FullName == "osu.Game.Skinning.Gameplay.GameplaySkinRecordedAnimationDrawable")
            .Select(animation =>
            {
                var program = ReadProperty<GameplaySkinRecordedAnimationProgram>(animation, "Program");
                return (object)new
                {
                    animationIds = runtime.PreparedScene.RecordedAnimations.Where(pair => ReferenceEquals(pair.Value, program)).Select(pair => pair.Key).ToArray(),
                    program.Trigger,
                    program.Loop,
                    program.Duration,
                    appliedFrame = ReadField<int>(animation, "appliedFrame"),
                    effectiveAlpha = EffectiveAlpha(animation),
                    visibleTexturedPieces = animation.ChildrenOfType<Sprite>().Count(sprite => sprite.IsPresent && sprite.Texture != null && EffectiveAlpha(sprite) > .001),
                };
            }).ToArray();

    private bool HasVisibleLongNotePieces(GameplaySkinSpecialisedSceneVisual visual, string role)
    {
        foreach (CompositeDrawable animation in visual.ChildrenOfType<CompositeDrawable>()
                     .Where(drawable => drawable.GetType().FullName == "osu.Game.Skinning.Gameplay.GameplaySkinRecordedAnimationDrawable"))
        {
            var program = ReadProperty<GameplaySkinRecordedAnimationProgram>(animation, "Program");
            if (!runtime.PreparedScene.RecordedAnimations.Any(pair => ReferenceEquals(pair.Value, program)
                    && pair.Key.EndsWith("." + role + ".clip", StringComparison.Ordinal))
                || ReadField<int>(animation, "appliedFrame") < 0 || EffectiveAlpha(animation) <= .001)
                continue;
            foreach (Sprite sprite in animation.ChildrenOfType<Sprite>().Where(sprite => sprite.IsPresent && sprite.Texture != null && EffectiveAlpha(sprite) > .001))
            {
                object piece = ReadField<object>(sprite, "piece");
                if (ReadProperty<int>(piece, "VertexCount") >= 3 && (ReadProperty<osuTK.Vector4>(piece, "Multiply").W > 0
                    || ReadProperty<osuTK.Vector4>(piece, "Add").W > 0))
                    return true;
            }
        }
        return false;
    }

    private void ValidateLongNoteTransitions()
    {
        DrainLongNoteObservations();
        bool regrabAllowed = settings.Ruleset == "mania" || settings.LongNoteMode == "hcn";
        GameplaySkinObjectState releasedState = settings.Ruleset == "mania" ? GameplaySkinObjectState.Visible : GameplaySkinObjectState.Missed;
        var objectHistories = longNoteEdges.GroupBy(edge => edge.State.ObjectId).ToArray();
        Assert.That(objectHistories, Has.Length.EqualTo(12), "Every planned native hold must retain its own observed identity.");
        foreach (int start in new[] { 4000, 10000, 16000, 22000 })
            Assert.That(objectHistories.Count(history => history.First().State.StartTime == start), Is.EqualTo(3));

        foreach (var history in objectHistories)
        {
            LongNoteEdge[] edges = history.ToArray();
            double start = edges[0].State.StartTime;
            double end = start switch { 4000 => 8000, 10000 => 14000, 16000 => 20000, 22000 => 24000, _ => throw new InvalidOperationException("Unexpected native hold.") };
            Assert.That(edges.All(edge => edge.State.StartTime == start && edge.State.EndTime == end), Is.True,
                "Input and visual retirement must never rewrite the original chart interval.");
            Assert.That(edges.Select(edge => edge.State.LaneId).Distinct().Count(), Is.EqualTo(1));
            foreach (LongNoteEdge edge in edges.Where(edge => edge.State.State is not (GameplaySkinObjectState.Completed or GameplaySkinObjectState.Despawned)))
                Assert.That(edge.State.Progress, Is.EqualTo(Math.Clamp((edge.GameplayTime - start) / (end - start), 0, 1)).Within(0.000001),
                    "Continuous body and parent Hit/Missed states retain elapsed chart progress.");

            if (start == 4000)
            {
                requireEdge(edge => edge.State.State == GameplaySkinObjectState.Holding && edge.State.LastLongNoteTransitionAt >= 4000
                                    && edge.State.LastLongNoteTransitionAt < 5000, "The real head press must enter Holding.");
                LongNoteEdge release = requireEdge(edge => edge.State.State == releasedState && edge.State.PreviousState == GameplaySkinObjectState.Holding
                    && edge.State.LastLongNoteTransitionAt >= 5100 && edge.State.LastLongNoteTransitionAt < 6000,
                    "Early release must retain the ruleset's actual body state and release origin.");
                Assert.That(release.State.Progress, Is.LessThan(1));
                Assert.That(edges.Any(edge => edge.State.State == GameplaySkinObjectState.Holding && edge.State.StateChangedAt >= 6300), Is.EqualTo(regrabAllowed));
                if (regrabAllowed)
                    requireEdge(edge => edge.State.State == GameplaySkinObjectState.Holding && edge.State.PreviousState == releasedState
                                        && edge.State.LastLongNoteTransitionAt >= 6300 && edge.State.LastLongNoteTransitionAt < 7000,
                        "An allowed re-grab must come from the real released state.");
                else
                    Assert.That(edges.Where(edge => edge.State.State == GameplaySkinObjectState.Completed)
                                     .All(edge => edge.State.LastLongNoteTransitionAt == release.State.LastLongNoteTransitionAt), Is.True,
                        "Later lifecycle retirement must not restart the release burst.");
            }
            else if (start == 10000)
            {
                LongNoteEdge[] beforeRegrab = edges.Where(edge => edge.GameplayTime < 11500).ToArray();
                Assert.That(beforeRegrab.All(edge => edge.State.LastLongNoteTransitionAt == null), Is.True,
                    "An untouched head miss has no held-transition flash.");
                if (settings.Ruleset == "bms")
                    requireEdge(edge => edge.GameplayTime < 11500 && edge.State.State == GameplaySkinObjectState.Missed,
                        "The BMS native missed head must reach its continuous body state.");
                Assert.That(edges.Any(edge => edge.State.State == GameplaySkinObjectState.Holding && edge.State.StateChangedAt >= 11500), Is.EqualTo(regrabAllowed));
            }
            else if (start == 16000)
            {
                LongNoteEdge release = requireEdge(edge => edge.State.PreviousState == GameplaySkinObjectState.Holding
                    && edge.State.StateChangedAt >= 19900 && edge.State.StateChangedAt < end
                    && edge.State.LastLongNoteTransitionAt >= 19900 && edge.State.LastLongNoteTransitionAt < end
                    && (settings.Ruleset == "bms" ? edge.State.State == GameplaySkinObjectState.Hit
                        : edge.State.State is GameplaySkinObjectState.Visible or GameplaySkinObjectState.Completed),
                    "The actual early tail must leave Holding before the unchanged chart end.");
                if (settings.Ruleset == "bms" || release.State.State == GameplaySkinObjectState.Visible)
                    Assert.That(release.State.Progress, Is.LessThan(1), "A successful early release is not 100% elapsed chart time.");
                Assert.That(edges.Where(edge => edge.State.State == GameplaySkinObjectState.Completed)
                                 .All(edge => edge.State.LastLongNoteTransitionAt == release.State.LastLongNoteTransitionAt), Is.True,
                    "Lifecycle completion retains the early release origin even when it arrives after the chart end.");
            }
            else
                requireEdge(edge => edge.State.State == GameplaySkinObjectState.Holding && edge.State.LastLongNoteTransitionAt >= 22000,
                    "The reused native owner must start from the new object's real press.");

            LongNoteEdge completed = requireEdge(edge => edge.Kind == nameof(GameplaySkinEventKind.ObjectStateChanged)
                && edge.State.State == GameplaySkinObjectState.Completed, "The native owner must eventually complete its lifecycle.");
            Assert.That(completed.State.Progress, Is.EqualTo(1), "This is the existing direct lifecycle edge, not the earlier body judgement.");
            requireEdge(edge => edge.State.State == GameplaySkinObjectState.Despawned && edge.Sequence > completed.Sequence,
                "Every completed native owner must actually despawn.");

            LongNoteEdge requireEdge(Func<LongNoteEdge, bool> predicate, string message)
            {
                LongNoteEdge? edge = edges.FirstOrDefault(predicate);
                Assert.That(edge, Is.Not.Null, $"Object {history.Key}: {message}");
                return edge!;
            }
        }

        // Mania's missed head does not replace its continuous Visible body state with Missed.
        // Prove the actual miss using the score processor's genuine head results instead.
        if (settings.Ruleset == "mania")
            Assert.That(score.HitEvents.Where(hit => hit.HitObject is HeadNote && hit.HitObject.StartTime == 10000 && hit.Result == HitResult.Miss)
                             .Select(hit => ((HeadNote)hit.HitObject).Column), Is.EquivalentTo(new[] { 0, 1, 2 }));
        Assert.That(longNoteLeases.Where(lease => lease.ObjectId.HasValue).GroupBy(lease => lease.VisualId)
            .Any(group => group.Select(lease => lease.ObjectId).Distinct().Count() > 1), Is.True, "The fixture must exercise actual native effect pool reuse.");
        Assert.That(longNoteLeases.GroupBy(lease => lease.VisualId).All(group => !group.Last().Applied && !group.Last().ObjectId.HasValue), Is.True,
            "Every observed native effect lease must have a real release receipt.");
        Assert.That(longNoteVisualIds.Keys.All(visual => !visual.IsApplied), Is.True);
        Assert.That(ReadState().ActiveObjects.Any(state => state.Kind == GameplaySkinObjectKind.LongNote), Is.False);
        settings.Write("long-note-transitions-complete.json", new
        {
            nativeReplayInput = true,
            injectedSkinEvents = false,
            settings.LongNoteMode,
            settings.Ruleset,
            regrabAllowed,
            publicStateSemantics = LongNotePublicStateSemantics(),
            edges = longNoteEdges.ToArray(),
            leases = longNoteLeases.ToArray(),
            nativeHeadAndTailResults = score.HitEvents.Where(hit => hit.HitObject is HeadNote or TailNote or BmsHoldNoteHead or BmsHoldNoteTailEvent)
                .Select(hit => new { objectType = hit.HitObject.GetType().Name, hit.HitObject.StartTime,
                    result = hit.Result.ToString(), hit.TimeOffset, appliedTime = hit.HitObject.StartTime + hit.TimeOffset,
                    lane = hit.HitObject is BmsHitObject bms ? bms.LaneIndex : ((ManiaHitObject)hit.HitObject).Column }).ToArray(),
            score.IsFullCombo,
            actualHits = score.HitEvents.Count,
            actualMisses = score.Statistics.Where(pair => pair.Key.ToString().Contains("Miss", StringComparison.Ordinal)).ToArray(),
            runtimeFaults = runtime.RuntimeFaults.Count,
        });
    }

    private object LongNotePublicStateSemantics() => new
    {
        releasedBody = settings.Ruleset == "mania" ? "Visible" : "Missed",
        earlyTail = settings.Ruleset == "mania" ? "Native Holding exit followed by the existing Completed lifecycle edge"
            : "Parent Hit with actual elapsed chart progress, followed later by Completed and Despawned",
        completed = "Existing native lifecycle edge; it is not the time of an earlier head/tail judgement",
        chartInterval = "Original StartTime/EndTime are unchanged; continuous body progress follows the authoritative gameplay clock",
    };
}
