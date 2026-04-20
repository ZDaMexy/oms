// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using osu.Game.Rulesets.Bms.Difficulty;
using Ude;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Decodes a textual BMS file into BMS-specific metadata and raw channel events.
    /// </summary>
    public class BmsBeatmapDecoder
    {
        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);
        private const string random_branching_warning = "Random branching is not fully supported. OMS only executes the #IF 1 branch inside #RANDOM blocks.";
        private const string random_branch_missing_if1_warning = "Random branching is not fully supported. OMS skipped a #RANDOM block because no #IF 1 branch was found.";

        static BmsBeatmapDecoder()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public BmsDecodedChart Decode(Stream stream, string? filePath = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            return Decode(memoryStream.ToArray(), filePath);
        }

        public BmsDecodedChart Decode(byte[] data, string? filePath = null)
        {
            ArgumentNullException.ThrowIfNull(data);

            return DecodeText(decodeText(data), filePath);
        }

        public BmsDecodedChart DecodeText(string content, string? filePath = null)
        {
            ArgumentNullException.ThrowIfNull(content);

            var decodedChart = new BmsDecodedChart();
            var measureLengthControlPoints = new List<BmsMeasureLengthControlPoint>();
            var playableChannels = new HashSet<int>();

            foreach (string rawLine in preprocessConditionalDirectives(content, decodedChart))
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('#'))
                    continue;

                if (handleControlDirective(line, decodedChart))
                    continue;

                if (tryParseChannelLine(line, out int measureIndex, out int channel, out string payload))
                {
                    handleChannelLine(decodedChart, measureLengthControlPoints, playableChannels, measureIndex, channel, payload);
                    continue;
                }

                handleHeaderLine(decodedChart, line);
            }

            decodedChart.BeatmapInfo.SetMeasureLengthControlPoints(measureLengthControlPoints);
            decodedChart.BeatmapInfo.Keymode = detectKeymode(filePath, playableChannels);
            postProcessChannelEvents(decodedChart);

            return decodedChart;
        }

        private static List<string> preprocessConditionalDirectives(string content, BmsDecodedChart decodedChart)
        {
            var lines = new List<string>();

            using (var reader = new StringReader(content))
            {
                string? line;

                while ((line = reader.ReadLine()) != null)
                    lines.Add(line);
            }

            int index = 0;
            return collectEffectiveLines(lines, decodedChart, ref index, stopAtEndIf: false);
        }

        private static List<string> collectEffectiveLines(IReadOnlyList<string> lines, BmsDecodedChart decodedChart, ref int index, bool stopAtEndIf)
        {
            var effectiveLines = new List<string>();

            while (index < lines.Count)
            {
                string currentLine = lines[index];
                string trimmedLine = currentLine.Trim();

                if (tryGetConditionalDirective(trimmedLine, out string directiveKey, out _))
                {
                    if (stopAtEndIf && directiveKey == "ENDIF")
                    {
                        index++;
                        break;
                    }

                    switch (directiveKey)
                    {
                        case "RANDOM":
                            index++;
                            effectiveLines.AddRange(collectRandomBranch(lines, decodedChart, ref index));
                            continue;

                        case "IF":
                            decodedChart.Warnings.Add(@"Ignoring stray #IF block encountered outside #RANDOM.");
                            index++;
                            collectEffectiveLines(lines, decodedChart, ref index, stopAtEndIf: true);
                            continue;

                        case "ENDIF":
                        case "ENDRANDOM":
                            decodedChart.Warnings.Add($@"Ignoring stray #{directiveKey} directive encountered outside #RANDOM.");
                            index++;
                            continue;
                    }
                }

                effectiveLines.Add(currentLine);
                index++;
            }

            return effectiveLines;
        }

        private static List<string> collectRandomBranch(IReadOnlyList<string> lines, BmsDecodedChart decodedChart, ref int index)
        {
            var effectiveLines = new List<string>();
            bool foundIf1Branch = false;

            decodedChart.Warnings.Add(random_branching_warning);

            while (index < lines.Count)
            {
                string trimmedLine = lines[index].Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    index++;
                    continue;
                }

                if (!tryGetConditionalDirective(trimmedLine, out string directiveKey, out string directiveValue))
                    break;

                if (directiveKey == "ENDRANDOM")
                {
                    index++;
                    break;
                }

                if (directiveKey != "IF")
                    break;

                index++;

                var branchLines = collectEffectiveLines(lines, decodedChart, ref index, stopAtEndIf: true);

                if (!int.TryParse(directiveValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int branchValue))
                {
                    decodedChart.Warnings.Add($@"Failed to parse #IF value '{directiveValue}'.");
                    continue;
                }

                if (branchValue != 1)
                    continue;

                foundIf1Branch = true;
                effectiveLines.AddRange(branchLines);
            }

            if (!foundIf1Branch)
                decodedChart.Warnings.Add(random_branch_missing_if1_warning);

            return effectiveLines;
        }

        private static string decodeText(byte[] data)
        {
            // UTF-8 is self-synchronising: if strict decoding succeeds, the data IS valid UTF-8.
            // Try it first to avoid heuristic charset detection misidentifying modern UTF-8 BMS files
            // (e.g. Ude may report "windows-1252" for a UTF-8 file with few multi-byte characters).
            try
            {
                return stripBom(strict_utf8.GetString(data));
            }
            catch (DecoderFallbackException)
            {
                // Not valid UTF-8 — fall through to heuristic detection.
            }

            var detectedEncoding = detectEncoding(data);

            if (detectedEncoding != null)
                return stripBom(detectedEncoding.GetString(data));

            // Final fallback: Shift_JIS is the most common legacy encoding for BMS files.
            return stripBom(Encoding.GetEncoding(@"shift_jis").GetString(data));
        }

        private static string stripBom(string text)
            => text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;

        private const float minimum_charset_confidence = 0.5f;

        private static Encoding? detectEncoding(byte[] data)
        {
            var detector = new CharsetDetector();
            detector.Feed(data, 0, data.Length);
            detector.DataEnd();

            if (string.IsNullOrWhiteSpace(detector.Charset))
                return null;

            // Reject low-confidence detections to reduce the risk of mojibake for short BMS files
            // where Ude cannot gather enough statistical data (e.g. a file with only a few CJK characters
            // in the title while the rest is ASCII channel data).
            if (detector.Confidence < minimum_charset_confidence)
                return null;

            try
            {
                return Encoding.GetEncoding(detector.Charset);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool handleControlDirective(string line, BmsDecodedChart decodedChart)
        {
            if (tryGetConditionalDirective(line, out string directiveKey, out _))
            {
                decodedChart.Warnings.Add($@"Ignoring unexpected #{directiveKey} directive after conditional preprocessing.");
                return true;
            }

            return false;
        }

        private static void handleHeaderLine(BmsDecodedChart decodedChart, string line)
        {
            splitDirective(line, out string key, out string value);

            if (string.IsNullOrEmpty(key))
                return;

            var beatmapInfo = decodedChart.BeatmapInfo;

            switch (key)
            {
                case "TITLE":
                    beatmapInfo.Title = value;
                    return;

                case "SUBTITLE":
                    beatmapInfo.Subtitle = value;
                    return;

                case "SUBARTIST":
                    beatmapInfo.SubArtist = value;
                    return;

                case "ARTIST":
                    beatmapInfo.Artist = value;
                    return;

                case "COMMENT":
                    beatmapInfo.Comment = value;
                    return;

                case "GENRE":
                    beatmapInfo.Genre = value;
                    return;

                case "BPM":
                    if (tryParseDouble(value, out double bpm))
                        beatmapInfo.InitialBpm = bpm;
                    else
                        decodedChart.Warnings.Add($@"Failed to parse #BPM value '{value}'.");
                    return;

                case "PLAYLEVEL":
                    beatmapInfo.PlayLevel = value;
                    return;

                case "DIFFICULTY":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int difficulty))
                        beatmapInfo.HeaderDifficulty = difficulty;
                    else
                        decodedChart.Warnings.Add($@"Failed to parse #DIFFICULTY value '{value}'.");
                    return;

                case "RANK":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rank))
                        beatmapInfo.Rank = rank;
                    else
                        decodedChart.Warnings.Add($@"Failed to parse #RANK value '{value}'.");
                    return;

                case "TOTAL":
                    if (tryParseDouble(value, out double total))
                        beatmapInfo.Total = total;
                    else
                        decodedChart.Warnings.Add($@"Failed to parse #TOTAL value '{value}'.");
                    return;

                case "STAGEFILE":
                    beatmapInfo.StageFile = value;
                    return;

                case "BANNER":
                    beatmapInfo.BannerFile = value;
                    return;

                case "BACKBMP":
                    beatmapInfo.BackgroundFile = value;
                    return;

                case "PREVIEW":
                    beatmapInfo.PreviewFile = value;
                    return;

                case "LNOBJ":
                    if (TryParseBase36(value, out int longNoteObjectId))
                        beatmapInfo.LongNoteObjectId = longNoteObjectId;
                    else
                        decodedChart.Warnings.Add($@"Failed to parse #LNOBJ value '{value}'.");
                    return;

                case "LNTYPE":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int longNoteType))
                        beatmapInfo.LongNoteType = longNoteType;
                    else
                        decodedChart.Warnings.Add($@"Failed to parse #LNTYPE value '{value}'.");
                    return;
            }

            if (tryHandleIndexedHeader(decodedChart, key, value))
                return;
        }

        private static bool tryHandleIndexedHeader(BmsDecodedChart decodedChart, string key, string value)
        {
            var beatmapInfo = decodedChart.BeatmapInfo;

            if (key.Length == 5 && key.StartsWith("WAV", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(3), out int wavIndex))
            {
                beatmapInfo.KeysoundTable[wavIndex] = value;
                return true;
            }

            if (key.Length == 5 && key.StartsWith("BMP", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(3), out int bmpIndex))
            {
                beatmapInfo.BitmapTable[bmpIndex] = value;
                return true;
            }

            if (key.Length == 5 && key.StartsWith("BPM", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(3), out int bpmIndex))
            {
                if (tryParseDouble(value, out double bpmValue))
                    beatmapInfo.ExtendedBpmTable[bpmIndex] = bpmValue;
                else
                    decodedChart.Warnings.Add($@"Failed to parse indexed BPM value '{value}' for #{key}.");

                return true;
            }

            if (key.Length == 6 && key.StartsWith("STOP", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(4), out int stopIndex))
            {
                if (tryParseDouble(value, out double stopValue))
                    beatmapInfo.StopTable[stopIndex] = stopValue;
                else
                    decodedChart.Warnings.Add($@"Failed to parse STOP value '{value}' for #{key}.");

                return true;
            }

            return false;
        }

        private static void postProcessChannelEvents(BmsDecodedChart decodedChart)
        {
            var orderedEvents = new List<BmsChannelEvent>(decodedChart.ChannelEvents);
            orderedEvents.Sort(compareChannelEvents);

            var pendingLnObjHeads = new Dictionary<int, List<BmsObjectEvent>>();
            var pendingLnType1Heads = new Dictionary<int, BmsObjectEvent>();

            foreach (var channelEvent in orderedEvents)
            {
                switch (channelEvent.Channel)
                {
                    case 0x01:
                        handleObjectEvent(decodedChart, channelEvent, autoPlay: true);
                        break;

                    case 0x03:
                        handleInlineBpmEvent(decodedChart, channelEvent);
                        break;

                    case 0x08:
                        handleExtendedBpmEvent(decodedChart, channelEvent);
                        break;

                    case 0x09:
                        handleStopEvent(decodedChart, channelEvent);
                        break;

                    default:
                        if (isPlayableNoteChannel(channelEvent.Channel, decodedChart.BeatmapInfo.Keymode))
                        {
                            handlePlayableNoteEvent(decodedChart, channelEvent, pendingLnObjHeads);
                            break;
                        }

                        if (isLongNoteChannel(channelEvent.Channel, decodedChart.BeatmapInfo.Keymode))
                        {
                            handleLongNoteChannelEvent(decodedChart, channelEvent, pendingLnType1Heads);
                            break;
                        }

                        break;
                }
            }

            flushPendingLongNoteChannels(decodedChart, pendingLnType1Heads);
        }

        private static int compareChannelEvents(BmsChannelEvent left, BmsChannelEvent right)
        {
            int measureComparison = left.MeasureIndex.CompareTo(right.MeasureIndex);

            if (measureComparison != 0)
                return measureComparison;

            int fractionComparison = left.FractionWithinMeasure.CompareTo(right.FractionWithinMeasure);

            if (fractionComparison != 0)
                return fractionComparison;

            return left.Channel.CompareTo(right.Channel);
        }

        private static void handleObjectEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent, bool autoPlay)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int objectId))
            {
                decodedChart.Warnings.Add($@"Failed to parse object token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            decodedChart.ObjectEvents.Add(new BmsObjectEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, channelEvent.Channel, objectId, autoPlay));
        }

        private static void handlePlayableNoteEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent, IDictionary<int, List<BmsObjectEvent>> pendingLnObjHeads)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int objectId))
            {
                decodedChart.Warnings.Add($@"Failed to parse object token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            int laneChannel = channelEvent.Channel;

            if (decodedChart.BeatmapInfo.LongNoteObjectId == objectId)
            {
                if (!tryPopPendingLnObjHead(laneChannel, pendingLnObjHeads, out var headEvent))
                {
                    decodedChart.Warnings.Add($@"Encountered LNOBJ tail at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2} without a matching head.");
                    return;
                }

                tryRemoveObjectEvent(decodedChart.ObjectEvents, headEvent);
                decodedChart.LongNoteEvents.Add(new BmsLongNoteEvent(
                    headEvent.MeasureIndex,
                    headEvent.FractionWithinMeasure,
                    channelEvent.MeasureIndex,
                    channelEvent.FractionWithinMeasure,
                    laneChannel,
                    headEvent.ObjectId,
                    objectId,
                    BmsLongNoteEncoding.LnObj));
                return;
            }

            var noteEvent = new BmsObjectEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, laneChannel, objectId, autoPlay: false);
            decodedChart.ObjectEvents.Add(noteEvent);

            if (!pendingLnObjHeads.TryGetValue(laneChannel, out var laneEvents))
            {
                laneEvents = new List<BmsObjectEvent>();
                pendingLnObjHeads[laneChannel] = laneEvents;
            }

            laneEvents.Add(noteEvent);
        }

        private static void handleLongNoteChannelEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent, IDictionary<int, BmsObjectEvent> pendingLnType1Heads)
        {
            if (decodedChart.BeatmapInfo.LongNoteType != 1)
            {
                decodedChart.Warnings.Add($@"Ignoring LN channel event at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2} because only #LNTYPE 1 is currently supported.");
                return;
            }

            if (!TryParseBase36(channelEvent.RawValue, out int objectId))
            {
                decodedChart.Warnings.Add($@"Failed to parse object token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            int laneChannel = channelEvent.Channel - 0x40;
            var laneEvent = new BmsObjectEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, laneChannel, objectId, autoPlay: false);

            if (pendingLnType1Heads.TryGetValue(laneChannel, out var headEvent))
            {
                pendingLnType1Heads.Remove(laneChannel);
                decodedChart.LongNoteEvents.Add(new BmsLongNoteEvent(
                    headEvent.MeasureIndex,
                    headEvent.FractionWithinMeasure,
                    laneEvent.MeasureIndex,
                    laneEvent.FractionWithinMeasure,
                    laneChannel,
                    headEvent.ObjectId,
                    laneEvent.ObjectId,
                    BmsLongNoteEncoding.LnType1));
                return;
            }

            pendingLnType1Heads[laneChannel] = laneEvent;
        }

        private static void flushPendingLongNoteChannels(BmsDecodedChart decodedChart, IDictionary<int, BmsObjectEvent> pendingLnType1Heads)
        {
            foreach (var pair in pendingLnType1Heads)
            {
                decodedChart.Warnings.Add($@"Unclosed LNTYPE 1 long note head at measure {pair.Value.MeasureIndex:000}, channel {pair.Key:X2}. Keeping it as a normal note.");
                decodedChart.ObjectEvents.Add(pair.Value);
            }
        }

        private static void handleInlineBpmEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!int.TryParse(channelEvent.RawValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bpmValue) || bpmValue <= 0)
            {
                decodedChart.Warnings.Add($@"Failed to parse inline BPM value '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            decodedChart.BpmChangeEvents.Add(new BmsBpmChangeEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, channelEvent.Channel, bpmValue, bpmValue));
        }

        private static void handleExtendedBpmEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int bpmIndex))
            {
                decodedChart.Warnings.Add($@"Failed to parse BPM table index '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            if (!decodedChart.BeatmapInfo.ExtendedBpmTable.TryGetValue(bpmIndex, out double bpmValue) || bpmValue <= 0)
            {
                decodedChart.Warnings.Add($@"Missing indexed BPM definition for token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            decodedChart.BpmChangeEvents.Add(new BmsBpmChangeEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, channelEvent.Channel, bpmIndex, bpmValue));
        }

        private static void handleStopEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int stopIndex))
            {
                decodedChart.Warnings.Add($@"Failed to parse STOP table index '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            if (!decodedChart.BeatmapInfo.StopTable.TryGetValue(stopIndex, out double stopValue))
            {
                decodedChart.Warnings.Add($@"Missing STOP definition for token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            decodedChart.StopEvents.Add(new BmsStopEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, stopIndex, stopValue));
        }

        private static bool tryPopPendingLnObjHead(int laneChannel, IDictionary<int, List<BmsObjectEvent>> pendingLnObjHeads, out BmsObjectEvent headEvent)
        {
            if (pendingLnObjHeads.TryGetValue(laneChannel, out var laneEvents) && laneEvents.Count > 0)
            {
                int lastIndex = laneEvents.Count - 1;
                headEvent = laneEvents[lastIndex];
                laneEvents.RemoveAt(lastIndex);

                if (laneEvents.Count == 0)
                    pendingLnObjHeads.Remove(laneChannel);

                return true;
            }

            headEvent = default;
            return false;
        }

        private static void tryRemoveObjectEvent(IList<BmsObjectEvent> objectEvents, BmsObjectEvent targetEvent)
        {
            for (int i = objectEvents.Count - 1; i >= 0; i--)
            {
                if (objectEvents[i].Equals(targetEvent))
                {
                    objectEvents.RemoveAt(i);
                    return;
                }
            }
        }

        private static bool isPlayableNoteChannel(int channel, BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => channel is >= 0x11 and <= 0x16,
                BmsKeymode.Key7K => channel is >= 0x11 and <= 0x16 || channel is 0x18 or 0x19,
                BmsKeymode.Key14K => channel is >= 0x11 and <= 0x16 || channel is 0x18 or 0x19 || channel is >= 0x21 and <= 0x26 || channel is 0x28 or 0x29,
                BmsKeymode.Key9K_Bms => channel is >= 0x11 and <= 0x19,
                BmsKeymode.Key9K_Pms => channel is >= 0x11 and <= 0x19,
                _ => false,
            };

        private static bool isLongNoteChannel(int channel, BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => channel is >= 0x51 and <= 0x56,
                BmsKeymode.Key7K => channel is >= 0x51 and <= 0x56 || channel is 0x58 or 0x59,
                BmsKeymode.Key14K => channel is >= 0x51 and <= 0x56 || channel is 0x58 or 0x59 || channel is >= 0x61 and <= 0x66 || channel is 0x68 or 0x69,
                BmsKeymode.Key9K_Bms => channel is >= 0x51 and <= 0x59,
                BmsKeymode.Key9K_Pms => channel is >= 0x51 and <= 0x59,
                _ => false,
            };

        private static void handleChannelLine(BmsDecodedChart decodedChart, List<BmsMeasureLengthControlPoint> measureLengthControlPoints, ISet<int> playableChannels, int measureIndex, int channel, string payload)
        {
            if (channel == 0x02)
            {
                if (tryParseDouble(payload, out double multiplier))
                    measureLengthControlPoints.Add(new BmsMeasureLengthControlPoint(measureIndex, multiplier));
                else
                    decodedChart.Warnings.Add($@"Failed to parse channel 02 measure length '{payload}' at measure {measureIndex:000}.");

                return;
            }

            if (payload.Length == 0)
                return;

            if (payload.Length % 2 != 0)
            {
                decodedChart.Warnings.Add($@"Ignoring malformed channel payload '{payload}' at measure {measureIndex:000}, channel {channel:X2}.");
                return;
            }

            int sliceCount = payload.Length / 2;

            for (int i = 0; i < sliceCount; i++)
            {
                string token = payload.Substring(i * 2, 2);

                if (token == "00")
                    continue;

                decodedChart.ChannelEvents.Add(new BmsChannelEvent(measureIndex, channel, (double)i / sliceCount, token));

                if (TryNormalizePlayableChannel(channel, out int normalizedChannel))
                    playableChannels.Add(normalizedChannel);
            }
        }

        private static BmsKeymode detectKeymode(string? filePath, ISet<int> playableChannels)
        {
            string extension = Path.GetExtension(filePath ?? string.Empty);

            if (extension.Equals(".pms", StringComparison.OrdinalIgnoreCase))
                return BmsKeymode.Key9K_Pms;

            if (containsAny(playableChannels, 0x21, 0x29))
                return BmsKeymode.Key14K;

            bool hasAllNineButtons = containsAll(playableChannels, 0x11, 0x19);

            if (hasAllNineButtons && !extension.Equals(".bme", StringComparison.OrdinalIgnoreCase))
                return BmsKeymode.Key9K_Bms;

            if (playableChannels.Contains(0x18) || playableChannels.Contains(0x19) || extension.Equals(".bme", StringComparison.OrdinalIgnoreCase))
                return BmsKeymode.Key7K;

            if (containsAny(playableChannels, 0x11, 0x16))
                return BmsKeymode.Key5K;

            return BmsKeymode.Key7K;
        }

        private static bool containsAll(ISet<int> values, int startInclusive, int endInclusive)
        {
            for (int i = startInclusive; i <= endInclusive; i++)
            {
                if (!values.Contains(i))
                    return false;
            }

            return true;
        }

        private static bool containsAny(ISet<int> values, int startInclusive, int endInclusive)
        {
            for (int i = startInclusive; i <= endInclusive; i++)
            {
                if (values.Contains(i))
                    return true;
            }

            return false;
        }

        private static bool TryNormalizePlayableChannel(int channel, out int normalizedChannel)
        {
            if (channel is >= 0x11 and <= 0x19)
            {
                normalizedChannel = channel;
                return true;
            }

            if (channel is >= 0x21 and <= 0x29)
            {
                normalizedChannel = channel;
                return true;
            }

            if (channel is >= 0x51 and <= 0x59)
            {
                normalizedChannel = channel - 0x40;
                return true;
            }

            if (channel is >= 0x61 and <= 0x69)
            {
                normalizedChannel = channel - 0x40;
                return true;
            }

            normalizedChannel = default;
            return false;
        }

        private static bool tryParseChannelLine(string line, out int measureIndex, out int channel, out string payload)
        {
            measureIndex = default;
            channel = default;
            payload = string.Empty;

            if (line.Length < 7 || line[0] != '#')
                return false;

            int separatorIndex = line.IndexOf(':');

            if (separatorIndex != 6)
                return false;

            if (!int.TryParse(line.AsSpan(1, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out measureIndex))
                return false;

            if (!int.TryParse(line.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out channel))
                return false;

            payload = line[(separatorIndex + 1)..].Trim();
            return true;
        }

        private static void splitDirective(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;

            if (line.Length == 0 || line[0] != '#')
                return;

            int separatorIndex = -1;

            for (int i = 1; i < line.Length; i++)
            {
                if (char.IsWhiteSpace(line[i]))
                {
                    separatorIndex = i;
                    break;
                }
            }

            if (separatorIndex < 0)
            {
                key = line[1..].Trim().ToUpperInvariant();
                return;
            }

            key = line[1..separatorIndex].Trim().ToUpperInvariant();
            value = line[(separatorIndex + 1)..].Trim();
        }

        private static bool tryGetConditionalDirective(string line, out string key, out string value)
        {
            splitDirective(line, out key, out value);

            return key is "RANDOM" or "IF" or "ENDIF" or "ENDRANDOM";
        }

        private static bool tryParseDouble(string value, out double result)
            => double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);

        internal static bool TryParseBase36(string value, out int result)
            => TryParseBase36(value.AsSpan(), out result);

        internal static bool TryParseBase36(ReadOnlySpan<char> value, out int result)
        {
            result = 0;

            foreach (char character in value)
            {
                int digit = character switch
                {
                    >= '0' and <= '9' => character - '0',
                    >= 'A' and <= 'Z' => character - 'A' + 10,
                    >= 'a' and <= 'z' => character - 'a' + 10,
                    _ => -1,
                };

                if (digit < 0)
                    return false;

                result = (result * 36) + digit;
            }

            return value.Length > 0;
        }
    }
}
