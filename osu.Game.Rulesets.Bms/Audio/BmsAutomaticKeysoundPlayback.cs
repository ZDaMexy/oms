// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Bms.Audio
{
    /// <summary>
    /// Audio-only projection of this gameplay instance. Judgement and drawable lifetime never advance this cursor.
    /// </summary>
    internal sealed class BmsAutomaticKeysoundPlayback
    {
        private readonly IReadOnlyList<Event> events;
        private int nextEvent;

        public BmsAutomaticKeysoundPlayback(IBeatmap beatmap)
        {
            // OrderBy is stable: simultaneous BGM and playable events retain the converter's order, including
            // separate occurrences of the same WAV slot. Do not use the armed lane timeline (it includes tails).
            events = beatmap is BmsConvertedManiaBeatmap converted ? converted.AutomaticKeysounds : CreateEvents(beatmap);
        }

        internal static Event[] CreateEvents(IBeatmap beatmap)
            => beatmap.HitObjects.Select(createEvent).OfType<Event>().OrderBy(e => e.Time).ToArray();

        internal IEnumerable<ISampleInfo> Samples => events.Select(e => e.Sample);

        public void Seek(double time)
        {
            int low = 0;
            int high = events.Count;

            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (events[mid].Time < time)
                    low = mid + 1;
                else
                    high = mid;
            }

            nextEvent = low;
        }

        public bool TryDequeue(double time, [NotNullWhen(true)] out Event? keysound)
        {
            if (nextEvent == events.Count || events[nextEvent].Time > time)
            {
                keysound = null;
                return false;
            }

            keysound = events[nextEvent++];
            return true;
        }

        private static Event? createEvent(HitObject hitObject)
        {
            // Only top-level heads are projected; a hold's nested head must not duplicate its parent's sample.
            return hitObject switch
            {
                BmsHitObject { KeysoundSample: not null } note when note is not BmsHoldNoteTailEvent
                    => new Event(note.StartTime, note.KeysoundSample, note.KeysoundId, null),
                BmsBgmEvent { KeysoundSample: not null } bgm
                    => new Event(bgm.StartTime, bgm.KeysoundSample, bgm.KeysoundId, null),
                IHasManiaKeysound { KeysoundSample: not null } converted
                    => new Event(hitObject.StartTime, converted.KeysoundSample, converted.KeysoundCutGroup,
                        hitObject is Note or HoldNote ? ((ManiaHitObject)hitObject).Column : null),
                _ => null,
            };
        }

        internal sealed record Event(double Time, ISampleInfo Sample, int? CutGroup, int? ManiaColumn);
    }
}
