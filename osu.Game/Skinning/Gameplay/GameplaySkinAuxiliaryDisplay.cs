// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Skinning.Gameplay
{
    public readonly record struct GameplaySkinAuxiliaryDisplaySettings(bool InitiallyOpen, bool Concentration, float Brightness);

    /// <summary>Explicit author admission for one independent auxiliary canvas. All controls read frozen root options.</summary>
    public sealed class GameplaySkinAuxiliaryDisplayDefinition
    {
        public string EnabledOption { get; }
        public string ConcentrationOption { get; }
        public string BrightnessOption { get; }

        internal GameplaySkinAuxiliaryDisplayDefinition(string enabledOption, string concentrationOption, string brightnessOption)
        {
            EnabledOption = enabledOption;
            ConcentrationOption = concentrationOption;
            BrightnessOption = brightnessOption;
        }

        public bool TryResolve(GameplaySkinOptionValues options, out GameplaySkinAuxiliaryDisplaySettings settings)
        {
            settings = default;
            // Equal author scopes are checked by the codec. Missing values mean the complete feature is not
            // applicable to this root, rather than permission to inherit values from another skin or mode.
            if (!options.TryGetValue(EnabledOption, out GameplaySkinOptionValue enabled)
                || !options.TryGetValue(ConcentrationOption, out GameplaySkinOptionValue concentration)
                || !options.TryGetValue(BrightnessOption, out GameplaySkinOptionValue brightness))
                return false;
            settings = new GameplaySkinAuxiliaryDisplaySettings(enabled.BooleanValue, concentration.BooleanValue, (float)brightness.NumberValue);
            return true;
        }
    }
}
