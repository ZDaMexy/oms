// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Intermediate representation produced by <see cref="BmsBeatmapDecoder"/>.
    /// </summary>
    public class BmsDecodedChart
    {
        public BmsBeatmapInfo BeatmapInfo { get; } = new BmsBeatmapInfo();

        public IList<BmsChannelEvent> RawChannelEvents { get; } = new List<BmsChannelEvent>();

        public IList<BmsChannelEvent> ChannelEvents => RawChannelEvents;

        public IList<BmsObjectEvent> ObjectEvents { get; } = new List<BmsObjectEvent>();

        public IList<BmsLongNoteEvent> LongNoteEvents { get; } = new List<BmsLongNoteEvent>();

        public IList<BmsScrollEvent> ScrollEvents { get; } = new List<BmsScrollEvent>();

        public IList<BmsBgaEvent> BgaEvents { get; } = new List<BmsBgaEvent>();

        public IList<BmsInvisibleObjectEvent> InvisibleObjectEvents { get; } = new List<BmsInvisibleObjectEvent>();

        public IList<BmsMineEvent> MineEvents { get; } = new List<BmsMineEvent>();

        public IList<BmsBpmChangeEvent> BpmChangeEvents { get; } = new List<BmsBpmChangeEvent>();

        public IList<BmsStopEvent> StopEvents { get; } = new List<BmsStopEvent>();

        public IList<string> Warnings { get; } = new List<string>();
    }
}
