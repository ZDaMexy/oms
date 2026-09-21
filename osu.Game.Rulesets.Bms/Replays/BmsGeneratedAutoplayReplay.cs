// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Replays;

namespace osu.Game.Rulesets.Bms.Replays
{
    // Transient provenance: a freshly generated autoplay is a new run, not a saved replay without version data.
    // Saved replays are deserialised as Replay; their score data owns the historical TOTAL rules.
    internal sealed class BmsGeneratedAutoplayReplay : Replay
    {
        public BmsGeneratedAutoplayReplay(Replay generated)
        {
            Frames = generated.Frames;
        }
    }
}
