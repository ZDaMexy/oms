// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Beatmaps
{
    public class DirectPlayableWorkingBeatmap : WorkingBeatmap
    {
        private readonly IBeatmap beatmap;
        private readonly IRulesetInfo playableRuleset;

        public DirectPlayableWorkingBeatmap(IBeatmap beatmap, IRulesetInfo playableRuleset)
            : base(beatmap.BeatmapInfo, null)
        {
            this.beatmap = beatmap;
            this.playableRuleset = playableRuleset;
        }

        protected override IBeatmap GetBeatmap() => beatmap;

        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
        {
            if (!string.Equals(ruleset.ShortName, playableRuleset.ShortName, StringComparison.Ordinal))
                throw new InvalidOperationException($"{nameof(DirectPlayableWorkingBeatmap)} only exposes the provided playable ruleset.");

            if (mods.Count > 0)
                throw new InvalidOperationException($"{nameof(DirectPlayableWorkingBeatmap)} does not support applying mods.");

            return beatmap;
        }

        public override Texture GetBackground() => throw new NotImplementedException();

        protected override Track GetBeatmapTrack() => throw new NotImplementedException();

        protected internal override ISkin GetSkin() => throw new NotImplementedException();

        public override Stream GetStream(string storagePath) => throw new NotImplementedException();
    }
}
