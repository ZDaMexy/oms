// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaKeyAssetPreset
    {
        public IReadOnlyList<string> KeyImages { get; }

        public IReadOnlyList<string> KeyDownImages { get; }

        private OmsManiaKeyAssetPreset(IReadOnlyList<string> keyImages, IReadOnlyList<string> keyDownImages)
        {
            KeyImages = keyImages;
            KeyDownImages = keyDownImages;
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaKeyAssetPreset> presets = new Dictionary<int, OmsManiaKeyAssetPreset>
        {
            [4] = new OmsManiaKeyAssetPreset(
                keyImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1" },
                keyDownImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1" }),
            [5] = new OmsManiaKeyAssetPreset(
                keyImages: new[] { "mania-key1", "mania-key2", "mania-key1", "mania-key2", "mania-key1" },
                keyDownImages: new[] { "mania-key1D", "mania-key2D", "mania-key1D", "mania-key2D", "mania-key1D" }),
            [6] = new OmsManiaKeyAssetPreset(
                keyImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1" },
                keyDownImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1" }),
            [7] = new OmsManiaKeyAssetPreset(
                keyImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1" },
                keyDownImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1" }),
            [8] = new OmsManiaKeyAssetPreset(
                keyImages: new[] { "7k\\0", "7k\\1", "7k\\2", "7k\\3", "7k\\4", "7k\\5", "7k\\6", "7k\\7" },
                keyDownImages: new[] { "7k\\0p", "7k\\1p", "7k\\2p", "7k\\3p", "7k\\4p", "7k\\5p", "7k\\6p", "7k\\7p" }),
            [9] = new OmsManiaKeyAssetPreset(
                keyImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1" },
                keyDownImages: new[] { "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1", "4k\\1" }),
        };

        public static OmsManiaKeyAssetPreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);

        public string GetKeyImage(int columnIndex)
            => KeyImages[columnIndex];

        public string GetKeyDownImage(int columnIndex)
            => KeyDownImages[columnIndex];
    }
}
