// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinOptionKind
    {
        Choice,
        Toggle,
        Number,
    }

    public enum GameplaySkinOptionEffect
    {
        None,
        Presentation,
    }

    public static class GameplaySkinOptionBudgets
    {
        public const int MAX_OPTIONS = 32;
        public const int MAX_CHOICES = 32;
        public const int MAX_LABEL_LENGTH = 128;
        public const int MAX_APPLICABILITY_TOKENS = 32;
        public const double MAX_ABSOLUTE_NUMBER = 1_000_000;
        public const int MAX_NUMBER_STEPS = 10_000;
    }

    public sealed class GameplaySkinOptionChoice
    {
        public string Value { get; }
        public string Label { get; }

        internal GameplaySkinOptionChoice(string value, string label)
        {
            Value = value;
            Label = label;
        }
    }

    /// <summary>One validated author declaration. Values are canonical strings only at the preferences boundary.</summary>
    public sealed class GameplaySkinOptionDefinition
    {
        public string Id { get; }
        public string Label { get; }
        public GameplaySkinOptionKind Kind { get; }
        public string DefaultValue { get; }
        public IReadOnlyList<GameplaySkinOptionChoice> Choices { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public double Step { get; }
        public IReadOnlyList<string> Rulesets { get; }
        public IReadOnlyList<string> Keymodes { get; }
        public GameplaySkinOptionEffect Effect { get; }

        /// <summary>A bounded external content directory, rather than an inline runtime variant list.</summary>
        public bool IsContentCatalog { get; }

        internal GameplaySkinOptionDefinition(string id, string label, GameplaySkinOptionKind kind, string defaultValue,
                                             IEnumerable<GameplaySkinOptionChoice> choices, double minimum, double maximum, double step,
                                             IEnumerable<string> rulesets, IEnumerable<string> keymodes, GameplaySkinOptionEffect effect,
                                             bool isContentCatalog = false)
        {
            Id = id;
            Label = label;
            Kind = kind;
            DefaultValue = defaultValue;
            Choices = Array.AsReadOnly(choices.ToArray());
            Minimum = minimum;
            Maximum = maximum;
            Step = step;
            Rulesets = Array.AsReadOnly(rulesets.ToArray());
            Keymodes = Array.AsReadOnly(keymodes.ToArray());
            Effect = effect;
            IsContentCatalog = isContentCatalog;
        }

        public bool AppliesTo(string rulesetId, string keymodeId)
            => (Rulesets.Count == 0 || Rulesets.Contains(rulesetId, StringComparer.Ordinal))
               && (Keymodes.Count == 0 || Keymodes.Contains(keymodeId, StringComparer.Ordinal));

        public bool TryValidateValue(string value, out GameplaySkinOptionValue validated)
        {
            validated = default;
            switch (Kind)
            {
                case GameplaySkinOptionKind.Choice when Choices.Any(choice => choice.Value == value):
                    validated = GameplaySkinOptionValue.FromChoice(value);
                    return true;

                case GameplaySkinOptionKind.Toggle when value is "true" or "false":
                    validated = GameplaySkinOptionValue.FromBoolean(value == "true");
                    return true;

                case GameplaySkinOptionKind.Number:
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                        || !double.IsFinite(number) || number < Minimum || number > Maximum)
                        return false;

                    double steps = (number - Minimum) / Step;
                    if (Math.Abs(steps - Math.Round(steps)) > 0.0000001)
                        return false;

                    // Store the declared step value, not accumulated slider floating point error.
                    double canonicalNumber = Math.Clamp(Minimum + Math.Round(steps) * Step, Minimum, Maximum);
                    validated = GameplaySkinOptionValue.FromNumber(canonicalNumber == 0 ? 0 : canonicalNumber);
                    return true;

                default:
                    return false;
            }
        }
    }

    public readonly struct GameplaySkinOptionValue
    {
        public GameplaySkinOptionKind Kind { get; }
        public string? StringValue { get; }
        public bool BooleanValue { get; }
        public double NumberValue { get; }
        public string CanonicalValue { get; }

        private GameplaySkinOptionValue(GameplaySkinOptionKind kind, string? text, bool boolean, double number, string canonical)
        {
            Kind = kind;
            StringValue = text;
            BooleanValue = boolean;
            NumberValue = number;
            CanonicalValue = canonical;
        }

        internal static GameplaySkinOptionValue FromChoice(string value) => new(GameplaySkinOptionKind.Choice, value, false, 0, value);
        internal static GameplaySkinOptionValue FromBoolean(bool value) => new(GameplaySkinOptionKind.Toggle, null, value, value ? 1 : 0, value ? "true" : "false");
        internal static GameplaySkinOptionValue FromNumber(double value) => new(GameplaySkinOptionKind.Number, null, false, value, value.ToString("G17", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Frozen choices for one gameplay root. A preferences change cannot mutate an existing layout or renderer.
    /// </summary>
    public sealed class GameplaySkinOptionValues
    {
        public static GameplaySkinOptionValues Empty { get; } = new GameplaySkinOptionValues([], null);

        public IReadOnlyDictionary<string, GameplaySkinOptionValue> Values { get; }
        public string? Presentation { get; }

        private GameplaySkinOptionValues(Dictionary<string, GameplaySkinOptionValue> values, string? presentation)
        {
            Values = new ReadOnlyDictionary<string, GameplaySkinOptionValue>(values);
            Presentation = presentation;
        }

        public bool TryGetValue(string id, out GameplaySkinOptionValue value) => Values.TryGetValue(id, out value);

        public static GameplaySkinOptionValues Resolve(IReadOnlyList<GameplaySkinOptionDefinition> definitions,
                                                       IReadOnlyDictionary<string, string>? overrides, string rulesetId, string keymodeId)
        {
            ArgumentNullException.ThrowIfNull(definitions);
            var values = new Dictionary<string, GameplaySkinOptionValue>(StringComparer.Ordinal);
            string? presentation = null;
            foreach (GameplaySkinOptionDefinition definition in definitions)
            {
                if (!definition.AppliesTo(rulesetId, keymodeId))
                    continue;

                if (overrides == null || !overrides.TryGetValue(definition.Id, out string? raw) || !definition.TryValidateValue(raw, out var value))
                {
                    if (!definition.TryValidateValue(definition.DefaultValue, out value))
                        throw new InvalidOperationException("A prepared option must have a valid author default.");
                }

                values.Add(definition.Id, value);
                if (definition.Effect == GameplaySkinOptionEffect.Presentation)
                    presentation = value.StringValue;
            }
            return values.Count == 0 ? Empty : new GameplaySkinOptionValues(values, presentation);
        }
    }
}
