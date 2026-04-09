// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaHitExplosionPreset
    {
        public string ExplosionImage { get; }

        public IReadOnlyList<float> ExplosionWidths { get; }

        private OmsManiaHitExplosionPreset(string explosionImage, IReadOnlyList<float> explosionWidths)
        {
            ExplosionImage = explosionImage;
            ExplosionWidths = explosionWidths;
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaHitExplosionPreset> presets = new Dictionary<int, OmsManiaHitExplosionPreset>
        {
            [4] = new OmsManiaHitExplosionPreset("lightingN", new[] { 69f, 69f, 69f, 69f }),
            [5] = new OmsManiaHitExplosionPreset("lightingN", new[] { 46f, 40f, 46f, 40f, 46f }),
            [6] = new OmsManiaHitExplosionPreset("lightingN", new[] { 40f, 40f, 40f, 40f, 40f, 40f }),
            [7] = new OmsManiaHitExplosionPreset("lightingN", new[] { 47f, 47f, 47f, 47f, 47f, 47f, 47f }),
            [8] = new OmsManiaHitExplosionPreset("lightingN", new[] { 43f, 36f, 36f, 36f, 36f, 36f, 36f, 36f }),
            [9] = new OmsManiaHitExplosionPreset("lightingN", new[] { 34f, 34f, 34f, 34f, 34f, 34f, 34f, 34f, 34f }),
        };

        public static OmsManiaHitExplosionPreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);

        public float GetExplosionScale(int columnIndex)
            => ExplosionWidths[columnIndex] / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE;
    }
}
