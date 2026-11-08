// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning.Gameplay;

namespace osu.Tools.StandardSkinCapture;

public partial class TestSceneStandardSkinCapture
{
    private GameplaySkinEventSubscription? graphSubscription;
    private GameplaySkinSceneRuntimeNode[] graphMovies = [];
    private readonly List<object> graphScoreEdges = [];
    private readonly List<object> graphScoreBudgets = [];
    private int graphScoreEdgeLimit;
    private readonly List<object> graphFrames = [];
    private readonly Dictionary<string, (int Frame, bool Visible)> graphLastFrames = new();
    private readonly Dictionary<string, Task> graphScreenshots = new();

    private void StartGraphObservations()
    {
        if (settings.Ruleset != "bms" || IsFailureCase)
            return;
        graphMovies = ReadingNodes().Where(node => node.ContentDrawable.GetType().Name == "GameplaySkinRecordedAnimationDrawable"
            && SourceId(node).Contains(".cutin.", StringComparison.Ordinal)).ToArray();
        if (graphMovies.Length == 0)
            return; // Older author packages do not declare these consumers.
        Assert.That(graphMovies, Has.Length.EqualTo(6));
        AddGraphScoreBudget(player.ChildrenOfType<DrawableBmsRuleset>().Single().Objects);
        graphSubscription = eventStream.Subscribe();
        settings.Write("graph-score-history-budget.json", graphScoreBudgets);
        settings.Write("graph-notification-declarations.json", graphMovies.Select(node => new
        {
            id = SourceId(node),
            node.InstanceId,
            animation = ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source.AnimationId,
            crossing = ReadProperty<object>(ReadProperty<GameplaySkinPreparedSceneNode>(node, "PreparedNode").Source, "ScoreCrossingThresholds"),
        }).ToArray());
    }

    private void AddGraphScoreBudget(IEnumerable<HitObject> objects)
    {
        int rootObjects = 0;
        int judgementNodes = 0;
        foreach (HitObject hitObject in objects)
        {
            rootObjects = checked(rootObjects + 1);
            count(hitObject);
        }

        // One forward play can publish Accuracy and TotalScore changes per judgement,
        // then a frame-level statistics change. Include every native nested node (also
        // gauge-only hold ticks), plus one initial state synchronisation. Keep history
        // and add each observed play's budget if this scene observes another Player.
        int playLimit = checked(3 * judgementNodes + 1);
        graphScoreEdgeLimit = checked(graphScoreEdgeLimit + playLimit);
        graphScoreBudgets.Add(new { rootObjects, judgementNodes, playLimit, totalLimit = graphScoreEdgeLimit });

        void count(HitObject hitObject)
        {
            judgementNodes = checked(judgementNodes + 1);
            foreach (HitObject nested in hitObject.NestedHitObjects)
                count(nested);
        }
    }

    private void ObserveGraphNotifications()
    {
        if (graphSubscription == null)
            return;
        graphSubscription.DrainFrame(envelope =>
        {
            if (envelope.Payload is GameplaySkinScoreEventPayload payload && envelope.EventKind == GameplaySkinEventKind.ScoreChanged)
            {
                if (graphScoreEdges.Count >= graphScoreEdgeLimit)
                    throw new InvalidOperationException($"Synthetic Graph fixture exceeded its bounded real-score history ({graphScoreEdgeLimit} edges).");
                graphScoreEdges.Add(new { envelope.Epoch, envelope.Sequence, envelope.GameplayTime, score = payload.State.Score,
                    maximum = payload.State.MaximumScore, delivery = envelope.DeliveryKind.ToString() });
            }
        });
        if (player == null || !ReferenceEquals(Game.ScreenStack.CurrentScreen, player) || !runtime.IsSceneReady)
            return;
        foreach (GameplaySkinSceneRuntimeNode node in graphMovies)
        {
            var animation = (CompositeDrawable)node.ContentDrawable;
            int frame = ReadField<int>(animation, "appliedFrame");
            string id = SourceId(node);
            bool visible = animation.ChildrenOfType<Sprite>().Any(sprite =>
            {
                if (sprite.Texture == null || EffectiveAlpha(sprite) <= .001)
                    return false;
                object piece = ReadField<object>(sprite, "piece");
                return ReadProperty<int>(piece, "VertexCount") >= 3
                       && (ReadProperty<osuTK.Vector4>(piece, "Multiply").W > 0 || ReadProperty<osuTK.Vector4>(piece, "Add").W > 0);
            });
            if (!graphLastFrames.TryGetValue(id, out var previous) || previous != (frame, visible))
            {
                if (graphFrames.Count >= 4096)
                    throw new InvalidOperationException("Synthetic Graph fixture exceeded its bounded source-frame history.");
                graphLastFrames[id] = (frame, visible);
                graphFrames.Add(new { id, gameplayTime = ReadProperty<double>(events, "AuthoritativeGameplayTime"),
                    frame, visible, effectiveAlpha = EffectiveAlpha(animation), score = score.TotalScore.Value });
            }
            string name = id[(id.LastIndexOf('.') + 1)..];
            if (name == "update" || !visible || frame < 45 || frame > 100 || graphScreenshots.ContainsKey(name))
                continue;
            string captureName = "graph-cutin-" + name;
            RecordGameplay(captureName, checked((int)Math.Round(ReadProperty<double>(events, "AuthoritativeGameplayTime"))));
            graphScreenshots.Add(name, Capture(captureName));
        }
    }

    private void FinishGraphObservations()
    {
        if (graphSubscription == null)
            return;
        foreach (Task capture in graphScreenshots.Values)
            capture.GetAwaiter().GetResult();
        Assert.That(graphScoreEdges, Is.Not.Empty);
        bool enabled = runtime.Publication.Snapshot.Context.OptionValues.TryGetValue("graph-cutin", out var cutin)
                       && cutin.CanonicalValue == "true"
                       && runtime.Publication.Snapshot.Context.OptionValues.TryGetValue("graph-visible", out var graph)
                       && graph.CanonicalValue == "true";
        if (enabled)
            Assert.That(graphScreenshots.Keys, Does.Contain("aaa"), "The complete native FC reaches the highest rank and must show its actual source movie.");
        else
            Assert.That(graphScreenshots, Is.Empty, "Hidden Graph/cut-ins must not draw a notification.");
        settings.Write("graph-notification-observations.json", new
        {
            source = "Actual ScoreChanged envelopes and source-movie drawables; no score, event, clock or frame is injected",
            enabled,
            nativePersonalBest = completionState!.ChartPresentation?.PersonalBestScore,
            finalScore = score.TotalScore.Value,
            maximum = completionState!.Score.MaximumScore,
            scoreHistoryBudgets = graphScoreBudgets,
            scoreEdges = graphScoreEdges,
            frames = graphFrames,
            screenshots = graphScreenshots.Keys.ToArray(),
        });
        graphSubscription.Dispose();
        graphSubscription = null;
    }
}
