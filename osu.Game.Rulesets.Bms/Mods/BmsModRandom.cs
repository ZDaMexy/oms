// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModRandom : ModRandom, IApplicableToBeatmap
    {
        public override LocalisableString Description => BmsModStrings.RandomDescription;

        public override bool Ranked => true;

        public override bool RequiresConfiguration => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModMirror), typeof(BmsModRandom) };

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.RandomType), nameof(BmsModStrings.RandomTypeDescription))]
        public Bindable<BmsRandomMode> RandomMode { get; } = new Bindable<BmsRandomMode>(BmsRandomMode.Random);

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.CustomPattern), nameof(BmsModStrings.CustomPatternDescription))]
        public Bindable<string> CustomPattern { get; } = new Bindable<string>(string.Empty);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!RandomMode.IsDefault)
                    yield return (BmsModStrings.RandomType, RandomMode.Value.GetLocalisableDescription());

                if (Seed.Value.HasValue)
                    yield return (ModSettingsStrings.Seed, Seed.Value.Value.ToString());

                if (!string.IsNullOrWhiteSpace(CustomPattern.Value))
                    yield return (BmsModStrings.CustomPattern, CustomPattern.Value.Trim().ToUpperInvariant());
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= RNG.Next();
            BmsLaneRearrangement.ApplyRandom(beatmap, RandomMode.Value, Seed.Value, CustomPattern.Value);
        }
    }
}
