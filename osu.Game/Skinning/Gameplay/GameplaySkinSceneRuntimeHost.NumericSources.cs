// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning.Gameplay
{
    internal sealed class GameplaySkinNumericPropertyDefault
    {
        internal double Number { get; init; }
        internal Texture? Texture { get; init; }
        internal GameplaySkinProjectiveQuad? Quad { get; init; }
        internal Axes RelativePositionAxes { get; init; }
        internal Axes RelativeSizeAxes { get; init; }
        internal bool WasApplied { get; set; }
    }

    public partial class GameplaySkinSceneRuntimeHost
    {
        private GameplaySkinNumericSourceRuntime? numericSourceRuntime;
        private Func<GameplaySkinNumericOperand, double?>? numericSourceReader;

        private void evaluateNumericSources()
        {
            if (PreparedScene.Program.NumericSources.Declarations.Count == 0)
                return;
            numericSourceRuntime ??= new GameplaySkinNumericSourceRuntime(PreparedScene.Program.NumericSources.Declarations);
            numericSourceReader ??= readNumericSource;
            numericSourceRuntime.Evaluate(numericSourceReader);
        }

        private bool tryNumericValue(string id, out double value)
        {
            if (numericSourceRuntime == null)
            {
                value = double.NaN;
                return false;
            }
            return numericSourceRuntime.TryGetValue(PreparedScene.Program.NumericSources.IndexOf(id), out value);
        }

        private void initialiseNumericDefaults(GameplaySkinSceneRuntimeNode node)
        {
            foreach (GameplaySkinPreparedSceneBinding binding in PreparedScene.Program.Bindings)
                if (binding.ValueId != null && binding.TargetNodeId == node.PreparedNode.Source.Id)
                    captureNumericDefault(node, binding.Property);
            foreach (GameplaySkinPreparedSceneTrack track in PreparedScene.Program.Tracks)
                if (track.ClockValueId != null && track.TargetNodeId == node.PreparedNode.Source.Id)
                    captureNumericDefault(node, track.Property);
        }

        private static void captureNumericDefault(GameplaySkinSceneRuntimeNode node, GameplaySkinSceneProperty property)
        {
            node.NumericPropertyDefaults ??= new Dictionary<GameplaySkinSceneProperty, GameplaySkinNumericPropertyDefault>();
            if (node.NumericPropertyDefaults.ContainsKey(property))
                return;
            node.NumericPropertyDefaults.Add(property, new GameplaySkinNumericPropertyDefault
            {
                Texture = (node.ContentDrawable as Sprite)?.Texture,
                Quad = (node.ContentDrawable as GameplaySkinProjectiveSprite)?.AppliedQuad,
                RelativePositionAxes = node.TransformDrawable.RelativePositionAxes,
                RelativeSizeAxes = node.TransformDrawable.RelativeSizeAxes,
                Number = property switch
                {
                    GameplaySkinSceneProperty.Brightness => getNumber(node.PreparedNode.Source.Properties, "brightness", 1),
                    GameplaySkinSceneProperty.Opacity => node.TransformDrawable.Alpha,
                    GameplaySkinSceneProperty.X => node.TransformDrawable.X,
                    GameplaySkinSceneProperty.Y => node.TransformDrawable.Y,
                    GameplaySkinSceneProperty.Width => node.TransformDrawable.Width,
                    GameplaySkinSceneProperty.Height => node.TransformDrawable.Height,
                    GameplaySkinSceneProperty.ScaleX => node.TransformDrawable.Scale.X,
                    GameplaySkinSceneProperty.ScaleY => node.TransformDrawable.Scale.Y,
                    GameplaySkinSceneProperty.Rotation => node.TransformDrawable.Rotation,
                    GameplaySkinSceneProperty.Z => node.ContentDrawable.Depth,
                    GameplaySkinSceneProperty.FontSize => ((SpriteText)node.ContentDrawable).Font.Size,
                    GameplaySkinSceneProperty.CornerRadius => ((Container)node.ContentDrawable).CornerRadius,
                    GameplaySkinSceneProperty.RevealX => ((Container)node.ContentDrawable).Width,
                    GameplaySkinSceneProperty.Text or GameplaySkinSceneProperty.Resource or GameplaySkinSceneProperty.ProjectiveQuad => 0,
                    _ => throw new InvalidOperationException("A numeric source can only drive a numeric, text, or numeric-clock track property."),
                },
            });
        }

        private bool canApplyNumericValue(string id, GameplaySkinSceneRuntimeNode node, GameplaySkinSceneProperty property, out double value)
        {
            GameplaySkinNumericPropertyDefault original = node.NumericPropertyDefaults![property];
            if (tryNumericValue(id, out value))
            {
                original.WasApplied = true;
                return true;
            }
            // Text must never turn an absent calculation into a fabricated zero or retain a previous score.
            if (property == GameplaySkinSceneProperty.Text)
                applyRuntimeStringProperty(node, property, string.Empty);
            else if (original.WasApplied)
            {
                if (property == GameplaySkinSceneProperty.Resource)
                    ((Sprite)node.ContentDrawable).Texture = original.Texture;
                else if (property == GameplaySkinSceneProperty.ProjectiveQuad)
                {
                    var projective = (GameplaySkinProjectiveSprite)node.ContentDrawable;
                    if (original.Quad == null)
                        projective.ClearQuad();
                    else
                        projective.ApplyQuad(original.Quad);
                }
                else
                {
                    applyRuntimeNumberProperty(node, property, original.Number);
                    node.TransformDrawable.RelativePositionAxes = original.RelativePositionAxes;
                    node.TransformDrawable.RelativeSizeAxes = original.RelativeSizeAxes;
                }
            }
            original.WasApplied = false;
            return false;
        }

        private double? readNumericSource(GameplaySkinNumericOperand operand) => operand.Source switch
        {
            GameplaySkinSceneBindingSource.Option => Publication.Snapshot.Context.OptionValues.TryGetValue(operand.OptionId!, out GameplaySkinOptionValue option)
                ? option.NumberValue : 0,
            GameplaySkinSceneBindingSource.ScoreValue => scoreState.Score,
            GameplaySkinSceneBindingSource.ScoreMaximum => scoreState.MaximumScore,
            GameplaySkinSceneBindingSource.ScoreProgress => scoreState.MaximumScore > 0 ? Math.Clamp((double)scoreState.Score / scoreState.MaximumScore, 0, 1) : 0,
            GameplaySkinSceneBindingSource.ScoreAccuracy => scoreState.Accuracy,
            GameplaySkinSceneBindingSource.ScorePersonalBest => chartPresentation?.PersonalBestScore ?? 0,
            GameplaySkinSceneBindingSource.ScorePersonalBestProgress => scoreState.MaximumScore > 0 && chartPresentation?.PersonalBestScore is long best
                ? Math.Clamp((double)best / scoreState.MaximumScore, 0, 1) : 0,
            GameplaySkinSceneBindingSource.ScorePacemakerNext => pacemakerNext.Value,
            GameplaySkinSceneBindingSource.ScorePacemakerNextProgress => pacemakerNext.Progress,
            GameplaySkinSceneBindingSource.ScoreRankAaa => rankValue(8),
            GameplaySkinSceneBindingSource.ScoreRankAa => rankValue(7),
            GameplaySkinSceneBindingSource.ScoreRankA => rankValue(6),
            GameplaySkinSceneBindingSource.ScoreRankAaaProgress => rankAvailable ? rankValue(8) / scoreState.MaximumScore : 0,
            GameplaySkinSceneBindingSource.ScoreRankAaProgress => rankAvailable ? rankValue(7) / scoreState.MaximumScore : 0,
            GameplaySkinSceneBindingSource.ScoreRankAProgress => rankAvailable ? rankValue(6) / scoreState.MaximumScore : 0,
            GameplaySkinSceneBindingSource.ComboValue => scoreState.Combo,
            GameplaySkinSceneBindingSource.ComboMaximum => scoreState.MaxCombo,
            GameplaySkinSceneBindingSource.ComboBreaks => scoreState.Statistics.ComboBreak,
            GameplaySkinSceneBindingSource.GaugeValue => scoreState.Gauge,
            GameplaySkinSceneBindingSource.GaugeMaximum => scoreState.GaugeState.Maximum,
            GameplaySkinSceneBindingSource.GaugeClearThreshold => scoreState.GaugeState.ClearThreshold,
            GameplaySkinSceneBindingSource.JudgementCountPerfect => scoreState.Statistics.Perfect,
            GameplaySkinSceneBindingSource.JudgementCountGreat => scoreState.Statistics.Great,
            GameplaySkinSceneBindingSource.JudgementCountGood => scoreState.Statistics.Good,
            GameplaySkinSceneBindingSource.JudgementCountOk => scoreState.Statistics.Ok,
            GameplaySkinSceneBindingSource.JudgementCountMeh => scoreState.Statistics.Meh,
            GameplaySkinSceneBindingSource.JudgementCountMiss => scoreState.Statistics.Miss,
            GameplaySkinSceneBindingSource.JudgementCountFast => scoreState.Statistics.Fast,
            GameplaySkinSceneBindingSource.JudgementCountSlow => scoreState.Statistics.Slow,
            _ => throw new InvalidOperationException("A numeric declaration may only read validated global number sources."),
        };
    }
}
