// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public static class GameplaySkinContentBudgets
    {
        public const int MAX_SELECTED_RESOURCES = 384;
        public const string CONTRACT_ID = "oms-gameplay-skin-content.v1";
        public const int MAX_CATALOG_BYTES = 4 * 1024 * 1024;
        public const int MAX_CHOICES_PER_MODULE = 1024;
        public const int MAX_TOTAL_CHOICES = 4096;
        public const int MAX_VARIANTS_PER_CHOICE = 64;
        public const int MAX_CAPTURED_FILES = 8192;
        public const long MAX_CAPTURED_BYTES = 512L * 1024 * 1024;
        public const long MAX_STRUCTURED_BYTES = 64L * 1024 * 1024;
        public const int MAX_INI_BYTES = 1024 * 1024;
        // Three independently selected animated surfaces plus the frame and completion effect are real
        // consumers of this bounded content budget. Ordinary packages retain their existing 64MP limit.
        public const long MAX_SELECTED_TEXTURE_PIXELS = 96L * 1024 * 1024;
        public const long MAX_SELECTED_DECODED_TEXTURE_BYTES = MAX_SELECTED_TEXTURE_PIXELS * 4;
    }

    public sealed class GameplaySkinContentCatalog
    {
        public IReadOnlyList<GameplaySkinContentModule> Modules { get; }

        internal GameplaySkinContentCatalog(IEnumerable<GameplaySkinContentModule> modules)
            => Modules = Array.AsReadOnly(modules.ToArray());
    }

    public sealed class GameplaySkinContentModule
    {
        public GameplaySkinOptionDefinition Option { get; }
        public IReadOnlyList<GameplaySkinContentChoice> Choices { get; }

        internal GameplaySkinContentModule(GameplaySkinOptionDefinition option, IEnumerable<GameplaySkinContentChoice> choices)
        {
            Option = option;
            Choices = Array.AsReadOnly(choices.ToArray());
        }
    }

    public sealed class GameplaySkinContentChoice
    {
        public string Value { get; }
        public string Label { get; }
        public string? PreviewPath { get; }
        public string? ManifestFile { get; }
        public string? SceneFile { get; }
        public string? IniFile { get; }
        public bool OrderedVariants { get; }
        public IReadOnlyList<GameplaySkinContentVariant> Variants { get; }

        internal GameplaySkinContentChoice(string value, string label, string? manifest, string? scene, string? ini,
                                           IEnumerable<GameplaySkinContentVariant>? variants = null, string? previewPath = null, bool orderedVariants = false)
        {
            Value = value;
            Label = label;
            PreviewPath = previewPath;
            ManifestFile = manifest;
            SceneFile = scene;
            IniFile = ini;
            OrderedVariants = orderedVariants;
            Variants = Array.AsReadOnly(variants?.ToArray() ?? []);
        }
    }

    public sealed class GameplaySkinContentVariant
    {
        public IReadOnlyDictionary<string, string> When { get; }
        public string? ManifestFile => Contribution.ManifestFile;
        public string? SceneFile => Contribution.SceneFile;
        public string? IniFile => Contribution.IniFile;
        internal GameplaySkinContentChoice Contribution { get; }

        internal GameplaySkinContentVariant(Dictionary<string, string> when, string? manifest, string? scene, string? ini)
        {
            When = new ReadOnlyDictionary<string, string>(when);
            Contribution = new GameplaySkinContentChoice("variant", "Variant", manifest, scene, ini);
        }
    }
}
