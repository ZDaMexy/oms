// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// A component which can trigger the most appropriate hit sound for a given point in time, based on the state of a <see cref="HitObjectContainer"/>
    /// </summary>
    public partial class GameplaySampleTriggerSource : CompositeDrawable
    {
        /// <summary>
        /// The number of concurrent samples allowed to be played concurrently so that it feels better when spam-pressing a key.
        /// </summary>
        private const int max_concurrent_hitsounds = OsuGameBase.SAMPLE_CONCURRENCY;

        private readonly HitObjectContainer hitObjectContainer;

        private int nextHitSoundIndex;

        private readonly Container<SkinnableSound> hitSounds;

        private HitObjectLifetimeEntry? mostValidObject;

        private IReadOnlyList<HitObjectLifetimeEntry>? orderedEntries;
        private IReadOnlyList<DrawableHitObject>? orderedAliveObjects;
        private int nextEntryIndex;
        private int nextAliveIndex;
        private HitObjectLifetimeEntry? firstEntry;
        private HitObjectLifetimeEntry? lastEntry;

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        protected readonly AudioContainer AudioContainer;

        public GameplaySampleTriggerSource(HitObjectContainer hitObjectContainer)
        {
            this.hitObjectContainer = hitObjectContainer;

            InternalChild = AudioContainer = new AudioContainer
            {
                Child = hitSounds = new Container<SkinnableSound>
                {
                    Name = "concurrent sample pool",
                    ChildrenEnumerable = Enumerable.Range(0, max_concurrent_hitsounds).Select(_ => new PausableSkinnableSound
                    {
                        MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME
                    })
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Ruleset loading installs all hit objects before child LoadComplete. Build the full-chart lookup here,
            // alongside other sample preparation, rather than sorting/subscribing the whole column on its first press.
            refreshLookupEntries();
        }

        /// <summary>
        /// Play the most appropriate hit sound for the current point in time.
        /// </summary>
        public virtual void Play()
        {
            HitObject? nextObject = GetMostValidObject();

            if (nextObject == null || nextObject.Samples.Count == 0)
                return;

            var samples = nextObject.Samples
                                    .Cast<ISampleInfo>()
                                    .ToArray();

            PlaySamples(samples);
        }

        protected virtual void PlaySamples(ISampleInfo[] samples) => Schedule(() =>
        {
            var hitSound = GetNextSample();
            ApplySampleInfo(hitSound, samples);
            hitSound.Play();
        });

        protected virtual void ApplySampleInfo(SkinnableSound hitSound, ISampleInfo[] samples)
        {
            hitSound.Samples = samples;
        }

        public void StopAllPlayback() => Schedule(() =>
        {
            foreach (var sound in hitSounds)
                sound.Stop();
        });

        protected override void Update()
        {
            base.Update();

            if (gameplayClock?.IsRewinding == true)
            {
                mostValidObject = null;
                nextEntryIndex = nextAliveIndex = 0;
            }
        }

        protected HitObject? GetMostValidObject()
        {
            refreshLookupEntries();

            if (mostValidObject == null || isAlreadyHit(mostValidObject))
            {
                // Preserve alive-first selection, including silent sample-only objects. The cursors only skip
                // already-judged entries; they reset on rewind or an authoritative membership/order change.
                // In particular a finished column no longer re-scans its entire history on every empty strike.
                var candidate = nextAliveEntry() ?? nextUnjudgedEntry();

                // In the case there are no non-judged objects, the last hit object should be used instead.
                if (candidate == null)
                {
                    mostValidObject = lastEntry;
                }
                else
                {
                    if (isCloseEnoughToCurrentTime(candidate.HitObject))
                    {
                        mostValidObject = candidate;
                    }
                    else
                    {
                        mostValidObject ??= firstEntry;
                    }
                }
            }

            if (mostValidObject == null)
                return null;

            // If the fallback has been judged then we want the sample from the object itself.
            if (isAlreadyHit(mostValidObject))
                return mostValidObject.HitObject;

            // Else we want the earliest valid nested.
            // In cases of nested objects, they will always have earlier sample data than their parent object.
            return getNextNested(mostValidObject.HitObject, getReferenceTime(), null) ?? mostValidObject.HitObject;
        }

        private void refreshLookupEntries()
        {
            var entries = hitObjectContainer.OrderedEntries;

            if (!ReferenceEquals(entries, orderedEntries))
            {
                orderedEntries = entries;
                nextEntryIndex = 0;
                mostValidObject = null;

                // Retain the original collection's first/last fallback, including equal-time insertion order.
                // The collection is only enumerated when objects are added, removed or their start times change.
                firstEntry = lastEntry = null;
                foreach (var entry in hitObjectContainer.Entries)
                {
                    firstEntry ??= entry;
                    lastEntry = entry;
                }
            }

            var alive = hitObjectContainer.OrderedAliveObjects;

            if (!ReferenceEquals(alive, orderedAliveObjects))
            {
                orderedAliveObjects = alive;
                nextAliveIndex = 0;
            }
        }

        private HitObjectLifetimeEntry? nextAliveEntry()
        {
            while (nextAliveIndex < orderedAliveObjects!.Count)
            {
                HitObjectLifetimeEntry entry = orderedAliveObjects[nextAliveIndex].Entry!;
                if (!isAlreadyHit(entry))
                    return entry;

                nextAliveIndex++;
            }

            return null;
        }

        private HitObjectLifetimeEntry? nextUnjudgedEntry()
        {
            while (nextEntryIndex < orderedEntries!.Count)
            {
                HitObjectLifetimeEntry entry = orderedEntries[nextEntryIndex];
                if (!isAlreadyHit(entry))
                    return entry;

                nextEntryIndex++;
            }

            return null;
        }

        private bool isAlreadyHit(HitObjectLifetimeEntry h) => h.AllJudged;
        private bool isCloseEnoughToCurrentTime(HitObject h) => getReferenceTime() >= h.StartTime - h.HitWindows.WindowFor(HitResult.Miss) * 2;

        private double getReferenceTime() => gameplayClock?.CurrentTime ?? Clock.CurrentTime;

        private static HitObject? getNextNested(HitObject hitObject, double time, HitObject? candidate)
        {
            foreach (var h in hitObject.NestedHitObjects)
            {
                if (h.GetEndTime() > time && (candidate == null || h.GetEndTime() < candidate.GetEndTime()))
                    candidate = h;

                candidate = getNextNested(h, time, candidate);
            }

            return candidate;
        }

        protected SkinnableSound GetNextSample()
        {
            SkinnableSound hitSound = hitSounds[nextHitSoundIndex];

            // round robin over available samples to allow for concurrent playback.
            nextHitSoundIndex = (nextHitSoundIndex + 1) % max_concurrent_hitsounds;

            return hitSound;
        }
    }
}
