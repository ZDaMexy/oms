// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Tools.StandardSkinCapture;

public partial class TestSceneStandardSkinCapture
{
    private static bool IsAutoScratchCase => CaptureSettings.Current.Assist == "scratch";

    private Score CreateAutoScratchReplay(IBeatmap beatmap, IReadOnlyList<Mod> mods)
    {
        if (mods.OfType<BmsModAutoScratch>().Count() != 1 || mods.Any(mod => mod is ModAutoplay or ModNoFail))
            throw new InvalidOperationException("ASCR verification requires the actual Auto Scratch Mod and ordinary replay input.");
        Score result = ruleset.GetAutoplayMod()!.CreateScoreFromReplayData(beatmap, mods);
        var original = result.Replay.Frames.Cast<BmsReplayFrame>().ToArray();
        var replay = new Replay();
        replay.Frames.Add(new BmsReplayFrame(0));
        foreach (BmsReplayFrame frame in original)
            replay.Frames.Add(new BmsReplayFrame(frame.Time, frame.Actions.Where(action => action is not (BmsAction.Scratch1 or BmsAction.Scratch2
                or BmsAction.Scratch1Motion or BmsAction.Scratch2Motion)).ToArray()));
        result.Replay = replay;
        result.ScoreInfo.User.Username = "OMS native ASCR fixture";
        settings.Write("native-input-sequence.json", new
        {
            source = "Ordinary ReplayPlayer key input; real BmsModAutoScratch owns scratch actions and score eligibility",
            replayType = replay.GetType().FullName,
            actualMods = mods.Select(mod => mod.Acronym).ToArray(),
            removedScratchInputs = original.Sum(frame => frame.Actions.Count(action => action is BmsAction.Scratch1 or BmsAction.Scratch2
                or BmsAction.Scratch1Motion or BmsAction.Scratch2Motion)),
            frames = replay.Frames.Cast<BmsReplayFrame>().Select(frame => new { frame.Time, actions = frame.Actions.Select(action => action.ToString()).ToArray() }).ToArray(),
            tint = "The real ASCR Mod tint is explicitly disabled so the two author-display cases compare the original source colours.",
            authorRoleInjected = false,
        });
        return result;
    }

    private void RecordAutoScratchPublication()
    {
        DrawableBmsRuleset renderer = player.ChildrenOfType<DrawableBmsRuleset>().Single();
        Assert.That(renderer.Mods.OfType<BmsModAutoScratch>().Count(), Is.EqualTo(1));
        Assert.That(renderer.Mods.OfType<ModAutoplay>(), Is.Empty);
        Assert.That(renderer.Mods.OfType<ModNoFail>(), Is.Empty);
        BmsHitObject[] objects = renderer.Objects.OfType<BmsHitObject>().ToArray();
        BmsHitObject[] scratches = objects.Where(note => note.IsScratch).ToArray();
        BmsHitObject[] keys = objects.Where(note => !note.IsScratch).ToArray();
        Assert.That(scratches, Is.Not.Empty);
        Assert.That(keys, Is.Not.Empty);
        Assert.That(scratches.All(note => note.AutoPlay && !note.CountsForScore), Is.True);
        Assert.That(keys.All(note => !note.AutoPlay && note.CountsForScore), Is.True);
        foreach (BmsHoldNote hold in scratches.OfType<BmsHoldNote>())
        {
            Assert.That(((BmsHitObjectJudgement)hold.Head!.Judgement).CountsForScore, Is.False);
            Assert.That(((BmsHoldNoteTailJudgement)hold.Tail!.Judgement).CountsForScore, Is.False);
            Assert.That(hold.BodyTicks.All(tick => !tick.CountsForGauge), Is.True);
        }
        settings.Write("auto-scratch-qualification.json", new
        {
            nativeMod = renderer.Mods.OfType<BmsModAutoScratch>().Single().GetType().FullName,
            authorChangesEligibility = false,
            objects = objects.Select(note => new
            {
                note.StartTime,
                note.LaneIndex,
                note.IsScratch,
                note.AutoPlay,
                note.CountsForScore,
                longNote = note is BmsHoldNote,
                headScores = note is BmsHoldNote hold && ((BmsHitObjectJudgement)hold.Head!.Judgement).CountsForScore,
                tailScores = note is BmsHoldNote tailHold && ((BmsHoldNoteTailJudgement)tailHold.Tail!.Judgement).CountsForScore,
            }).ToArray(),
        });
    }

    private void RecordAutoScratchVisibility(string name)
    {
        Assert.That(runtime.Publication.Snapshot.Context.OptionValues.TryGetValue("auto-scratch-display", out GameplaySkinOptionValue option), Is.True);
        bool shown = option.CanonicalValue switch
        {
            "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException("The ASCR display option must be a native author toggle."),
        };
        GameplaySkinEventStateSnapshot snapshot = ReadState();
        var states = snapshot.ActiveObjects.ToDictionary(state => state.ObjectId);
        var observations = player.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
            .Where(visual => visual.IsApplied && visual.BoundObjectId.HasValue && states.ContainsKey(visual.BoundObjectId.Value)
                             && (visual.Key.Slot == GameplaySkinSlotCatalog.Note || visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteHead
                                 || visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteBody || visual.Key.Slot == GameplaySkinSlotCatalog.LongNoteTail))
            .Select(visual => new
            {
                visual.BoundObjectId,
                slot = visual.Key.Slot.Id,
                state = states[visual.BoundObjectId!.Value],
                role = states[visual.BoundObjectId.Value].AutoAssist.ToString(),
                hasVisibleTexture = visual.RuntimeNodes.Any(node => node.ContentDrawable is Sprite { Texture: not null } sprite && EffectiveAlpha(sprite) > .001),
                nodes = visual.RuntimeNodes.Select(NodeEvidence).ToArray(),
            }).ToArray();
        var scratch = observations.Where(observation => observation.state.AutoAssist == GameplaySkinObjectAutoAssist.Scratch).ToArray();
        var keys = observations.Where(observation => observation.state.AutoAssist == GameplaySkinObjectAutoAssist.None).ToArray();
        Assert.That(scratch, Is.Not.Empty, "A true native assisted scratch must be present at the capture.");
        Assert.That(keys, Is.Not.Empty, "An ordinary scored key must be present at the same capture.");
        Assert.That(scratch.Any(observation => observation.hasVisibleTexture), Is.EqualTo(shown));
        Assert.That(keys.Any(observation => observation.hasVisibleTexture), Is.True);
        settings.Write(name + "-auto-scratch.json", new
        {
            displayOption = option.CanonicalValue,
            actualGameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime"),
            exactNativeScratchObjects = scratch.Select(observation => observation.BoundObjectId).Distinct().Count(),
            exactOrdinaryKeyObjects = keys.Select(observation => observation.BoundObjectId).Distinct().Count(),
            observations,
            score.TotalScore.Value,
            score.IsFullCombo,
            eligibility = "The original native Mod owns AutoPlay and CountsForScore; only authored sprite visibility differs.",
        });
    }
}
