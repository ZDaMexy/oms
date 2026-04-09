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

        public IList<BmsChannelEvent> ChannelEvents { get; } = new List<BmsChannelEvent>();

        public IList<BmsObjectEvent> ObjectEvents { get; } = new List<BmsObjectEvent>();

        public IList<BmsLongNoteEvent> LongNoteEvents { get; } = new List<BmsLongNoteEvent>();

        public IList<BmsBpmChangeEvent> BpmChangeEvents { get; } = new List<BmsBpmChangeEvent>();

        public IList<BmsStopEvent> StopEvents { get; } = new List<BmsStopEvent>();

        public IList<string> Warnings { get; } = new List<string>();
    }
}
