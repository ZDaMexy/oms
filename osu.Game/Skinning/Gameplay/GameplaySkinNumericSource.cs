// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinNumericOperation
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Min,
        Max,
        Floor,
        Ceil,
        Float32,
        Multiply32,
        Divide32,
    }

    /// <summary>A finite literal, global numeric source, or earlier declaration. It never executes author code.</summary>
    public sealed class GameplaySkinNumericOperand
    {
        public double? Constant { get; }
        public string? SourceId { get; }
        internal GameplaySkinSceneBindingSource Source { get; }
        internal string? OptionId { get; }
        internal int ReferenceIndex { get; }
        internal int Depth { get; }
        internal bool IsInteger { get; }
        internal bool CanBeNegative { get; }
        internal double? ConstantResult { get; }

        internal GameplaySkinNumericOperand(double constant)
        {
            Constant = ConstantResult = constant;
            ReferenceIndex = -1;
            IsInteger = constant == Math.Truncate(constant);
            CanBeNegative = constant < 0;
        }

        internal GameplaySkinNumericOperand(string sourceId, bool integer, bool negative)
        {
            SourceId = sourceId;
            Source = GameplaySkinSceneVocabulary.ParseBindingSource(sourceId);
            OptionId = Source == GameplaySkinSceneBindingSource.Option ? sourceId[7..] : null;
            ReferenceIndex = -1;
            IsInteger = integer;
            CanBeNegative = negative;
        }

        internal GameplaySkinNumericOperand(string sourceId, int index, GameplaySkinNumericSource reference)
        {
            SourceId = sourceId;
            ReferenceIndex = index;
            Depth = reference.Depth;
            IsInteger = reference.IsInteger;
            CanBeNegative = reference.CanBeNegative;
            ConstantResult = reference.ConstantResult;
        }
    }

    /// <summary>An immutable, topologically ordered arithmetic declaration validated at the package boundary.</summary>
    public sealed class GameplaySkinNumericSource
    {
        public string Id { get; }
        public GameplaySkinNumericOperation Operation { get; }
        public GameplaySkinNumericOperand Left { get; }
        public GameplaySkinNumericOperand? Right { get; }
        internal int Depth { get; }
        internal bool IsInteger { get; }
        internal bool CanBeNegative { get; }
        internal double? ConstantResult { get; }

        internal GameplaySkinNumericSource(string id, GameplaySkinNumericOperation operation, GameplaySkinNumericOperand left,
                                           GameplaySkinNumericOperand? right)
        {
            Id = id;
            Operation = operation;
            Left = left;
            Right = right;
            Depth = 1 + Math.Max(left.Depth, right?.Depth ?? 0);
            if (left.ConstantResult is double a && (right == null || right.ConstantResult.HasValue)
                && GameplaySkinNumericSources.TryCalculate(operation, a, right?.ConstantResult ?? 0, out double result))
                ConstantResult = result;
            IsInteger = ConstantResult is double number ? number == Math.Truncate(number)
                : operation is GameplaySkinNumericOperation.Floor or GameplaySkinNumericOperation.Ceil
                  || operation is not (GameplaySkinNumericOperation.Divide or GameplaySkinNumericOperation.Divide32)
                     && left.IsInteger && right?.IsInteger == true;
            CanBeNegative = ConstantResult is double value ? value < 0 : operation switch
            {
                GameplaySkinNumericOperation.Add or GameplaySkinNumericOperation.Min => left.CanBeNegative || right!.CanBeNegative,
                GameplaySkinNumericOperation.Max => left.CanBeNegative && right!.CanBeNegative,
                GameplaySkinNumericOperation.Floor or GameplaySkinNumericOperation.Ceil or GameplaySkinNumericOperation.Float32 => left.CanBeNegative,
                GameplaySkinNumericOperation.Subtract => true,
                _ => left.CanBeNegative || right!.CanBeNegative,
            };
        }
    }

    internal static class GameplaySkinNumericSources
    {
        internal const int MAX_DECLARATIONS = 64;
        internal const int MAX_DEPTH = 16;

        // These facts belong to the publication as a whole. Target-local judgements, arrival clocks, booleans,
        // strings and elapsed clocks deliberately retain their existing direct binding/availability contracts.
        internal static bool IsGlobalSource(GameplaySkinSceneBindingSource source) => source is
            GameplaySkinSceneBindingSource.ScoreValue or GameplaySkinSceneBindingSource.ScoreMaximum
            or GameplaySkinSceneBindingSource.ScoreProgress or GameplaySkinSceneBindingSource.ScoreAccuracy
            or GameplaySkinSceneBindingSource.ScorePersonalBest or GameplaySkinSceneBindingSource.ScorePersonalBestProgress
            or GameplaySkinSceneBindingSource.ScorePacemakerNext or GameplaySkinSceneBindingSource.ScorePacemakerNextProgress
            or GameplaySkinSceneBindingSource.ScoreRankAaa or GameplaySkinSceneBindingSource.ScoreRankAa or GameplaySkinSceneBindingSource.ScoreRankA
            or GameplaySkinSceneBindingSource.ScoreRankAaaProgress or GameplaySkinSceneBindingSource.ScoreRankAaProgress or GameplaySkinSceneBindingSource.ScoreRankAProgress
            or GameplaySkinSceneBindingSource.ComboValue or GameplaySkinSceneBindingSource.ComboMaximum or GameplaySkinSceneBindingSource.ComboBreaks
            or GameplaySkinSceneBindingSource.GaugeValue or GameplaySkinSceneBindingSource.GaugeMaximum or GameplaySkinSceneBindingSource.GaugeClearThreshold
            or GameplaySkinSceneBindingSource.TimingBpmMinimum or GameplaySkinSceneBindingSource.TimingBpmMaximum
            or GameplaySkinSceneBindingSource.JudgementCountPerfect or GameplaySkinSceneBindingSource.JudgementCountGreat
            or GameplaySkinSceneBindingSource.JudgementCountGood or GameplaySkinSceneBindingSource.JudgementCountOk
            or GameplaySkinSceneBindingSource.JudgementCountMeh or GameplaySkinSceneBindingSource.JudgementCountMiss
            or GameplaySkinSceneBindingSource.JudgementCountFast or GameplaySkinSceneBindingSource.JudgementCountSlow;

        internal static bool IsIntegerSource(GameplaySkinSceneBindingSource source) => IsGlobalSource(source) && source is not
            (GameplaySkinSceneBindingSource.ScoreProgress or GameplaySkinSceneBindingSource.ScoreAccuracy
            or GameplaySkinSceneBindingSource.ScorePersonalBestProgress or GameplaySkinSceneBindingSource.ScorePacemakerNextProgress
            or GameplaySkinSceneBindingSource.ScoreRankAaaProgress or GameplaySkinSceneBindingSource.ScoreRankAaProgress or GameplaySkinSceneBindingSource.ScoreRankAProgress
            or GameplaySkinSceneBindingSource.GaugeValue or GameplaySkinSceneBindingSource.GaugeMaximum or GameplaySkinSceneBindingSource.GaugeClearThreshold
            or GameplaySkinSceneBindingSource.TimingBpmMinimum or GameplaySkinSceneBindingSource.TimingBpmMaximum);

        internal static bool TryCalculate(GameplaySkinNumericOperation operation, double left, double right, out double result)
        {
            if (operation is GameplaySkinNumericOperation.Multiply32 or GameplaySkinNumericOperation.Divide32
                && (!float.IsFinite((float)left) || !float.IsFinite((float)right)))
            {
                result = double.NaN;
                return false;
            }
            result = operation switch
            {
                GameplaySkinNumericOperation.Add => left + right,
                GameplaySkinNumericOperation.Subtract => left - right,
                GameplaySkinNumericOperation.Multiply => left * right,
                GameplaySkinNumericOperation.Divide => right == 0 ? double.NaN : left / right,
                GameplaySkinNumericOperation.Min => Math.Min(left, right),
                GameplaySkinNumericOperation.Max => Math.Max(left, right),
                GameplaySkinNumericOperation.Floor => Math.Floor(left),
                GameplaySkinNumericOperation.Ceil => Math.Ceiling(left),
                GameplaySkinNumericOperation.Float32 => (float)left,
                GameplaySkinNumericOperation.Multiply32 => (float)left * (float)right,
                GameplaySkinNumericOperation.Divide32 => (float)right == 0 ? double.NaN : (float)left / (float)right,
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
            return double.IsFinite(result);
        }
    }

    /// <summary>Fixed storage, one ordered evaluation per folded runtime frame, with transitive unavailability.</summary>
    internal sealed class GameplaySkinNumericSourceRuntime
    {
        private readonly IReadOnlyList<GameplaySkinNumericSource> declarations;
        private readonly double[] values;
        private readonly bool[] available;

        internal GameplaySkinNumericSourceRuntime(IReadOnlyList<GameplaySkinNumericSource> declarations)
        {
            this.declarations = declarations;
            values = new double[declarations.Count];
            available = new bool[declarations.Count];
        }

        internal void Evaluate(Func<GameplaySkinNumericOperand, double?> read)
        {
            for (int i = 0; i < declarations.Count; i++)
            {
                GameplaySkinNumericSource declaration = declarations[i];
                double? left = operand(declaration.Left, read);
                double? right = declaration.Right == null ? 0 : operand(declaration.Right, read);
                available[i] = left.HasValue && right.HasValue
                    && GameplaySkinNumericSources.TryCalculate(declaration.Operation, left.Value, right.Value, out values[i]);
                if (!available[i])
                    values[i] = double.NaN;
            }
        }

        internal bool TryGetValue(int index, out double value)
        {
            value = values[index];
            return available[index];
        }

        private double? operand(GameplaySkinNumericOperand source, Func<GameplaySkinNumericOperand, double?> read)
        {
            if (source.ReferenceIndex >= 0)
                return available[source.ReferenceIndex] ? values[source.ReferenceIndex] : null;
            double? result = source.Constant ?? read(source);
            return result is double value && double.IsFinite(value) ? value : null;
        }
    }
}
