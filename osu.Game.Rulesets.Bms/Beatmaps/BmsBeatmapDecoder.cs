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
        private const string switch_branching_warning = "Switch branching is not fully supported. OMS deterministically executes the #CASE 1 branch (or the #DEF branch) inside #SWITCH blocks.";
        private const int unknown_channel = -1;
        private const int bgm_channel = 0x01;

        private static readonly HashSet<string> control_directive_keys = new HashSet<string>
        {
            "RANDOM", "SETRANDOM", "IF", "ELSEIF", "ELSE", "ENDIF", "ENDRANDOM",
            "SWITCH", "SETSWITCH", "CASE", "SKIP", "DEF", "ENDSW",
        };

        private static readonly HashSet<string> if_chain_terminators = new HashSet<string> { "ELSEIF", "ELSE", "ENDIF" };

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
            int sourceLineOrder = 0;

            foreach (string rawLine in preprocessConditionalDirectives(content, decodedChart))
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('#'))
                    continue;

                if (handleControlDirective(line, decodedChart))
                    continue;

                if (tryParseChannelLine(line, out int measureIndex, out int channel, out string rawChannelToken, out string payload))
                {
                    handleChannelLine(decodedChart, measureLengthControlPoints, playableChannels, measureIndex, channel, rawChannelToken, payload, sourceLineOrder++);
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

            var effectiveLines = new List<string>();
            int index = 0;
            collectEffectiveLines(lines, decodedChart, ref index, effectiveLines, stopDirectives: null);
            return effectiveLines;
        }

        /// <summary>
        /// Appends branch-selected lines into <paramref name="output"/>, expanding nested #RANDOM / #SWITCH blocks.
        /// Returns without consuming when a directive contained in <paramref name="stopDirectives"/> is encountered
        /// (returning that key), or null at end of input.
        /// </summary>
        private static string? collectEffectiveLines(IReadOnlyList<string> lines, BmsDecodedChart decodedChart, ref int index, List<string> output, ISet<string>? stopDirectives)
        {
            while (index < lines.Count)
            {
                string trimmedLine = lines[index].Trim();

                if (tryGetControlDirective(trimmedLine, out string directiveKey, out string directiveValue))
                {
                    if (stopDirectives != null && stopDirectives.Contains(directiveKey))
                        return directiveKey;

                    switch (directiveKey)
                    {
                        case "RANDOM":
                        case "SETRANDOM":
                            index++;
                            collectRandomBlock(lines, decodedChart, ref index, output, isSet: directiveKey == "SETRANDOM", directiveValue);
                            continue;

                        case "SWITCH":
                        case "SETSWITCH":
                            index++;
                            collectSwitchBlock(lines, decodedChart, ref index, output, isSet: directiveKey == "SETSWITCH", directiveValue);
                            continue;

                        default:
                            decodedChart.Warnings.Add($@"Ignoring stray #{directiveKey} directive encountered outside a control block.");
                            index++;
                            continue;
                    }
                }

                output.Add(lines[index]);
                index++;
            }

            return null;
        }

        private static void collectRandomBlock(IReadOnlyList<string> lines, BmsDecodedChart decodedChart, ref int index, List<string> output, bool isSet, string value)
        {
            int selectedValue = resolveSelectedBranchValue(decodedChart, isSet, value, random_branching_warning);
            bool matchedAnyBranch = false;
            bool branchTakenInChain = false;

            while (index < lines.Count)
            {
                string trimmedLine = lines[index].Trim();

                if (!tryGetControlDirective(trimmedLine, out string directiveKey, out string directiveValue))
                {
                    // A line that sits directly inside #RANDOM but outside any #IF chain stays unconditionally.
                    output.Add(lines[index]);
                    index++;
                    continue;
                }

                if (directiveKey == "ENDRANDOM")
                {
                    index++;
                    break;
                }

                if (directiveKey == "IF" || directiveKey == "ELSEIF" || directiveKey == "ELSE")
                {
                    index++;

                    bool conditionMatches;

                    if (directiveKey == "ELSE")
                        conditionMatches = !branchTakenInChain;
                    else if (int.TryParse(directiveValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int branchValue))
                        conditionMatches = !branchTakenInChain && branchValue == selectedValue;
                    else
                    {
                        decodedChart.Warnings.Add($@"Failed to parse #{directiveKey} value '{directiveValue}'.");
                        conditionMatches = false;
                    }

                    var branchBody = new List<string>();
                    string? terminator = collectEffectiveLines(lines, decodedChart, ref index, branchBody, if_chain_terminators);

                    if (conditionMatches)
                    {
                        matchedAnyBranch = true;
                        branchTakenInChain = true;
                        output.AddRange(branchBody);
                    }

                    if (terminator == "ENDIF")
                    {
                        index++;
                        branchTakenInChain = false;
                    }
                    else if (terminator == null)
                        break;

                    // #ELSEIF / #ELSE terminators are intentionally left unconsumed so the loop continues the chain.
                    continue;
                }

                decodedChart.Warnings.Add($@"Ignoring unexpected #{directiveKey} directive inside #RANDOM block.");
                index++;
            }

            if (!isSet && !matchedAnyBranch)
                decodedChart.Warnings.Add(random_branch_missing_if1_warning);
        }

        private static void collectSwitchBlock(IReadOnlyList<string> lines, BmsDecodedChart decodedChart, ref int index, List<string> output, bool isSet, string value)
        {
            int selectedValue = resolveSelectedBranchValue(decodedChart, isSet, value, switch_branching_warning);
            var segments = collectSwitchSegments(lines, decodedChart, ref index);

            int startSegment = findSwitchStartSegment(segments, selectedValue);

            if (startSegment < 0)
                return;

            // C-style fall-through: execute from the selected segment until a #SKIP-terminated segment.
            for (int i = startSegment; i < segments.Count; i++)
            {
                int nestedIndex = 0;
                collectEffectiveLines(segments[i].Lines, decodedChart, ref nestedIndex, output, stopDirectives: null);

                if (segments[i].EndsWithSkip)
                    break;
            }
        }

        private static List<SwitchSegment> collectSwitchSegments(IReadOnlyList<string> lines, BmsDecodedChart decodedChart, ref int index)
        {
            var segments = new List<SwitchSegment>();
            SwitchSegment? currentSegment = null;
            int nestedSwitchDepth = 0;

            while (index < lines.Count)
            {
                string rawLine = lines[index];
                string trimmedLine = rawLine.Trim();

                if (tryGetControlDirective(trimmedLine, out string directiveKey, out string directiveValue))
                {
                    if (nestedSwitchDepth == 0)
                    {
                        if (directiveKey == "ENDSW")
                        {
                            index++;
                            break;
                        }

                        if (directiveKey == "CASE")
                        {
                            currentSegment = new SwitchSegment(parseControlValue(decodedChart, "CASE", directiveValue), isDefault: false);
                            segments.Add(currentSegment);
                            index++;
                            continue;
                        }

                        if (directiveKey == "DEF")
                        {
                            currentSegment = new SwitchSegment(null, isDefault: true);
                            segments.Add(currentSegment);
                            index++;
                            continue;
                        }

                        if (directiveKey == "SKIP")
                        {
                            if (currentSegment != null)
                                currentSegment.EndsWithSkip = true;

                            index++;
                            continue;
                        }
                    }

                    // Track nested #SWITCH depth so a nested switch's #CASE / #ENDSW are captured raw (expanded later),
                    // instead of being mistaken for this switch's boundaries.
                    if (directiveKey == "SWITCH" || directiveKey == "SETSWITCH")
                        nestedSwitchDepth++;
                    else if (directiveKey == "ENDSW")
                        nestedSwitchDepth = Math.Max(0, nestedSwitchDepth - 1);
                }

                currentSegment?.Lines.Add(rawLine);
                index++;
            }

            return segments;
        }

        private static int findSwitchStartSegment(List<SwitchSegment> segments, int selectedValue)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (!segments[i].IsDefault && segments[i].Value == selectedValue)
                    return i;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].IsDefault)
                    return i;
            }

            return -1;
        }

        private static int resolveSelectedBranchValue(BmsDecodedChart decodedChart, bool isSet, string value, string nonSetWarning)
        {
            if (!isSet)
            {
                decodedChart.Warnings.Add(nonSetWarning);
                return 1;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pinnedValue))
                return pinnedValue;

            decodedChart.Warnings.Add($@"Failed to parse pinned control value '{value}'; defaulting to branch 1.");
            return 1;
        }

        private static int? parseControlValue(BmsDecodedChart decodedChart, string directiveKey, string value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
                return parsedValue;

            decodedChart.Warnings.Add($@"Failed to parse #{directiveKey} value '{value}'.");
            return null;
        }

        private sealed class SwitchSegment
        {
            public int? Value { get; }

            public bool IsDefault { get; }

            public bool EndsWithSkip { get; set; }

            public List<string> Lines { get; } = new List<string>();

            public SwitchSegment(int? value, bool isDefault)
            {
                Value = value;
                IsDefault = isDefault;
            }
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
            if (tryGetControlDirective(line, out string directiveKey, out _))
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

                case "POORBGA":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int poorBgaMode)
                        && Enum.IsDefined(typeof(BmsPoorBgaMode), poorBgaMode))
                        beatmapInfo.PoorBgaMode = (BmsPoorBgaMode)poorBgaMode;
                    else
                        decodedChart.Warnings.Add($@"Failed to parse #POORBGA value '{value}'.");
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

            beatmapInfo.UnknownHeaders[key] = value;
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

            if (key.Length == 8 && key.StartsWith("SCROLL", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(6), out int scrollIndex))
            {
                if (tryParseDouble(value, out double scrollValue))
                    beatmapInfo.ScrollTable[scrollIndex] = scrollValue;
                else
                    decodedChart.Warnings.Add($@"Failed to parse SCROLL value '{value}' for #{key}.");

                return true;
            }

            if (key.Length == 5 && key.StartsWith("BGA", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(3), out int bgaDefinitionIndex))
            {
                if (tryParseBgaDefinition(value, out string bitmapReference, out int sourceX1, out int sourceY1, out int sourceX2, out int sourceY2, out int destinationX, out int destinationY))
                    beatmapInfo.BgaDefinitions[bgaDefinitionIndex] = new BmsBgaDefinition(bgaDefinitionIndex, bitmapReference, sourceX1, sourceY1, sourceX2, sourceY2, destinationX, destinationY);
                else
                    decodedChart.Warnings.Add($@"Failed to parse #BGA definition value '{value}' for #{key}.");

                return true;
            }

            if (key.Length == 6 && key.StartsWith("@BGA", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(4), out int atBgaDefinitionIndex))
            {
                if (tryParseAtBgaDefinition(value, out string bitmapReference, out int sourceX, out int sourceY, out int width, out int height, out int destinationX, out int destinationY))
                    beatmapInfo.AtBgaDefinitions[atBgaDefinitionIndex] = new BmsAtBgaDefinition(atBgaDefinitionIndex, bitmapReference, sourceX, sourceY, width, height, destinationX, destinationY);
                else
                    decodedChart.Warnings.Add($@"Failed to parse #@BGA definition value '{value}' for #{key}.");

                return true;
            }

            if (key.Length == 6 && key.StartsWith("ARGB", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(4), out int argbDefinitionIndex))
            {
                if (tryParseArgbComponents(value, out int alpha, out int red, out int green, out int blue))
                    beatmapInfo.ArgbDefinitions[argbDefinitionIndex] = new BmsArgbDefinition(argbDefinitionIndex, alpha, red, green, blue);
                else
                    decodedChart.Warnings.Add($@"Failed to parse #ARGB definition value '{value}' for #{key}.");

                return true;
            }

            if (key.Length == 7 && key.StartsWith("SWBGA", StringComparison.OrdinalIgnoreCase) && TryParseBase36(key.AsSpan(5), out int swBgaDefinitionIndex))
            {
                if (tryParseSwBgaDefinition(value, out int frameDurationMilliseconds, out int totalDurationMilliseconds, out int lineChannel, out bool loop, out int alpha, out int red, out int green, out int blue, out string pattern))
                    beatmapInfo.SwBgaDefinitions[swBgaDefinitionIndex] = new BmsSwBgaDefinition(swBgaDefinitionIndex, frameDurationMilliseconds, totalDurationMilliseconds, lineChannel, loop, alpha, red, green, blue, pattern);
                else
                    decodedChart.Warnings.Add($@"Failed to parse #SWBGA definition value '{value}' for #{key}.");

                return true;
            }

            return false;
        }

        private static void postProcessChannelEvents(BmsDecodedChart decodedChart)
        {
            var orderedEvents = new List<BmsChannelEvent>(decodedChart.RawChannelEvents);
            orderedEvents.Sort(compareChannelEvents);
            var effectiveEvents = compoundDuplicateChannelEvents(orderedEvents);

            var pendingLnObjHeads = new Dictionary<int, List<int>>();
            var pendingLnType1Heads = new Dictionary<int, BmsObjectEvent>();
            var pendingLnType2Segments = new Dictionary<int, List<BmsObjectEvent>>();
            var consumedLnObjHeadIndices = new HashSet<int>();

            foreach (var channelEvent in effectiveEvents)
            {
                switch (channelEvent.Channel)
                {
                    case 0x01:
                        handleObjectEvent(decodedChart, channelEvent, autoPlay: true);
                        break;

                    case 0x03:
                        handleInlineBpmEvent(decodedChart, channelEvent);
                        break;

                    case 0x04:
                    case 0x06:
                    case 0x07:
                    case 0x0A:
                        handleBgaEvent(decodedChart, channelEvent);
                        break;

                    case 0x08:
                        handleExtendedBpmEvent(decodedChart, channelEvent);
                        break;

                    case 0x09:
                        handleStopEvent(decodedChart, channelEvent);
                        break;

                    default:
                        if (isScrollChannel(channelEvent))
                        {
                            handleScrollEvent(decodedChart, channelEvent);
                            break;
                        }

                        if (isInvisibleObjectChannel(channelEvent.Channel))
                        {
                            handleInvisibleObjectEvent(decodedChart, channelEvent);
                            break;
                        }

                        if (isMineChannel(channelEvent.Channel))
                        {
                            handleMineEvent(decodedChart, channelEvent);
                            break;
                        }

                        if (isPlayableNoteChannel(channelEvent.Channel, decodedChart.BeatmapInfo.Keymode))
                        {
                            handlePlayableNoteEvent(decodedChart, channelEvent, pendingLnObjHeads, consumedLnObjHeadIndices);
                            break;
                        }

                        if (isLongNoteChannel(channelEvent.Channel, decodedChart.BeatmapInfo.Keymode))
                        {
                            handleLongNoteChannelEvent(decodedChart, channelEvent, pendingLnType1Heads, pendingLnType2Segments);
                            break;
                        }

                        break;
                }
            }

            flushPendingLongNoteChannels(decodedChart, pendingLnType1Heads, pendingLnType2Segments);
            removeConsumedLnObjHeads(decodedChart, consumedLnObjHeadIndices);
        }

        private static void removeConsumedLnObjHeads(BmsDecodedChart decodedChart, ISet<int> consumedLnObjHeadIndices)
        {
            if (consumedLnObjHeadIndices.Count == 0)
                return;

            var objectEvents = decodedChart.ObjectEvents;
            var retained = new List<BmsObjectEvent>(Math.Max(0, objectEvents.Count - consumedLnObjHeadIndices.Count));

            for (int i = 0; i < objectEvents.Count; i++)
            {
                if (!consumedLnObjHeadIndices.Contains(i))
                    retained.Add(objectEvents[i]);
            }

            objectEvents.Clear();

            foreach (var objectEvent in retained)
                objectEvents.Add(objectEvent);
        }

        private static List<BmsChannelEvent> compoundDuplicateChannelEvents(IReadOnlyList<BmsChannelEvent> orderedEvents)
        {
            var effectiveEvents = new List<BmsChannelEvent>(orderedEvents.Count);

            foreach (var channelEvent in orderedEvents)
            {
                if (effectiveEvents.Count > 0 && isDuplicateChannelCollision(effectiveEvents[^1], channelEvent))
                {
                    if (channelEvent.RawValue == "00")
                        continue;

                    effectiveEvents[^1] = channelEvent;
                    continue;
                }

                effectiveEvents.Add(channelEvent);
            }

            return effectiveEvents;
        }

        private static bool isDuplicateChannelCollision(BmsChannelEvent left, BmsChannelEvent right)
            // BGM (channel 01) is the spec-mandated exception to channel compounding: multiple #xxx01 lines that
            // land on the same position are independent simultaneous keysound layers and must all survive.
            => left.Channel != bgm_channel
               && left.MeasureIndex == right.MeasureIndex
               && left.FractionWithinMeasure == right.FractionWithinMeasure
               && hasSameChannelIdentity(left, right);

        private static bool hasSameChannelIdentity(BmsChannelEvent left, BmsChannelEvent right)
        {
            if (left.Channel != right.Channel)
                return false;

            if (left.Channel != unknown_channel)
                return true;

            return string.Equals(left.RawChannelToken, right.RawChannelToken, StringComparison.OrdinalIgnoreCase);
        }

        private static int compareChannelEvents(BmsChannelEvent left, BmsChannelEvent right)
        {
            int measureComparison = left.MeasureIndex.CompareTo(right.MeasureIndex);

            if (measureComparison != 0)
                return measureComparison;

            int fractionComparison = left.FractionWithinMeasure.CompareTo(right.FractionWithinMeasure);

            if (fractionComparison != 0)
                return fractionComparison;

            int channelComparison = left.Channel.CompareTo(right.Channel);

            if (channelComparison != 0)
                return channelComparison;

            if (left.Channel == unknown_channel)
            {
                int rawChannelTokenComparison = StringComparer.OrdinalIgnoreCase.Compare(left.RawChannelToken, right.RawChannelToken);

                if (rawChannelTokenComparison != 0)
                    return rawChannelTokenComparison;
            }

            return left.SourceLineOrder.CompareTo(right.SourceLineOrder);
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

        private static void handleBgaEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int bitmapId))
            {
                decodedChart.Warnings.Add($@"Failed to parse BGA token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            decodedChart.BgaEvents.Add(new BmsBgaEvent(
                channelEvent.MeasureIndex,
                channelEvent.FractionWithinMeasure,
                channelEvent.Channel,
                bitmapId,
                getBgaLayer(channelEvent.Channel)));
        }

        private static void handleScrollEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int scrollIndex))
            {
                decodedChart.Warnings.Add($@"Failed to parse SCROLL table index '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            if (!decodedChart.BeatmapInfo.ScrollTable.TryGetValue(scrollIndex, out double scrollValue))
            {
                decodedChart.Warnings.Add($@"Missing SCROLL definition for token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            if (!double.IsFinite(scrollValue))
            {
                decodedChart.Warnings.Add($@"Ignoring non-finite SCROLL value '{scrollValue}' for token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}.");
                return;
            }

            decodedChart.ScrollEvents.Add(new BmsScrollEvent(
                channelEvent.MeasureIndex,
                channelEvent.FractionWithinMeasure,
                scrollIndex,
                scrollValue));
        }

        private static void handleInvisibleObjectEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int objectId))
            {
                decodedChart.Warnings.Add($@"Failed to parse invisible object token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            decodedChart.InvisibleObjectEvents.Add(new BmsInvisibleObjectEvent(
                channelEvent.MeasureIndex,
                channelEvent.FractionWithinMeasure,
                channelEvent.Channel,
                objectId));
        }

        private static void handleMineEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int damageValue))
            {
                decodedChart.Warnings.Add($@"Failed to parse mine token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            decodedChart.MineEvents.Add(new BmsMineEvent(
                channelEvent.MeasureIndex,
                channelEvent.FractionWithinMeasure,
                channelEvent.Channel,
                damageValue));
        }

        private static void handlePlayableNoteEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent, IDictionary<int, List<int>> pendingLnObjHeads, ISet<int> consumedLnObjHeadIndices)
        {
            if (!TryParseBase36(channelEvent.RawValue, out int objectId))
            {
                decodedChart.Warnings.Add($@"Failed to parse object token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            int laneChannel = channelEvent.Channel;

            if (decodedChart.BeatmapInfo.LongNoteObjectId == objectId)
            {
                if (!tryPopPendingLnObjHead(laneChannel, pendingLnObjHeads, out int headIndex))
                {
                    decodedChart.Warnings.Add($@"Encountered LNOBJ tail at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2} without a matching head.");
                    return;
                }

                // Mark the head index for a single deferred removal pass instead of an O(n) scan per LNOBJ tail.
                var headEvent = decodedChart.ObjectEvents[headIndex];
                consumedLnObjHeadIndices.Add(headIndex);
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

            int noteIndex = decodedChart.ObjectEvents.Count;
            var noteEvent = new BmsObjectEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, laneChannel, objectId, autoPlay: false);
            decodedChart.ObjectEvents.Add(noteEvent);

            if (!pendingLnObjHeads.TryGetValue(laneChannel, out var laneEvents))
            {
                laneEvents = new List<int>();
                pendingLnObjHeads[laneChannel] = laneEvents;
            }

            laneEvents.Add(noteIndex);
        }

        private static void handleLongNoteChannelEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent, IDictionary<int, BmsObjectEvent> pendingLnType1Heads, IDictionary<int, List<BmsObjectEvent>> pendingLnType2Segments)
        {
            switch (decodedChart.BeatmapInfo.LongNoteType)
            {
                case 1:
                    handleLnType1ChannelEvent(decodedChart, channelEvent, pendingLnType1Heads);
                    return;

                case 2:
                    handleLnType2ChannelEvent(decodedChart, channelEvent, pendingLnType2Segments);
                    return;
            }

            if (channelEvent.RawValue == "00")
                return;

            decodedChart.Warnings.Add($@"Ignoring LN channel event at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2} because only #LNTYPE 1 and #LNTYPE 2 are currently supported.");
        }

        private static void handleLnType1ChannelEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent, IDictionary<int, BmsObjectEvent> pendingLnType1Heads)
        {
            if (channelEvent.RawValue == "00")
                return;

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

        private static void handleLnType2ChannelEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent, IDictionary<int, List<BmsObjectEvent>> pendingLnType2Segments)
        {
            int laneChannel = channelEvent.Channel - 0x40;

            if (channelEvent.RawValue == "00")
            {
                if (!pendingLnType2Segments.TryGetValue(laneChannel, out var laneEvents) || laneEvents.Count == 0)
                    return;

                pendingLnType2Segments.Remove(laneChannel);

                var headEvent = laneEvents[0];
                var tailEvent = laneEvents[^1];

                decodedChart.LongNoteEvents.Add(new BmsLongNoteEvent(
                    headEvent.MeasureIndex,
                    headEvent.FractionWithinMeasure,
                    channelEvent.MeasureIndex,
                    channelEvent.FractionWithinMeasure,
                    laneChannel,
                    headEvent.ObjectId,
                    tailEvent.ObjectId,
                    BmsLongNoteEncoding.LnType2));
                return;
            }

            if (!TryParseBase36(channelEvent.RawValue, out int objectId))
            {
                decodedChart.Warnings.Add($@"Failed to parse object token '{channelEvent.RawValue}' at measure {channelEvent.MeasureIndex:000}, channel {channelEvent.Channel:X2}.");
                return;
            }

            var laneEvent = new BmsObjectEvent(channelEvent.MeasureIndex, channelEvent.FractionWithinMeasure, laneChannel, objectId, autoPlay: false);

            if (!pendingLnType2Segments.TryGetValue(laneChannel, out var laneEventsForChannel))
            {
                laneEventsForChannel = new List<BmsObjectEvent>();
                pendingLnType2Segments[laneChannel] = laneEventsForChannel;
            }

            laneEventsForChannel.Add(laneEvent);
        }

        private static void flushPendingLongNoteChannels(BmsDecodedChart decodedChart, IDictionary<int, BmsObjectEvent> pendingLnType1Heads, IDictionary<int, List<BmsObjectEvent>> pendingLnType2Segments)
        {
            foreach (var pair in pendingLnType1Heads)
            {
                decodedChart.Warnings.Add($@"Unclosed LNTYPE 1 long note head at measure {pair.Value.MeasureIndex:000}, channel {pair.Key:X2}. Keeping it as a normal note.");
                decodedChart.ObjectEvents.Add(pair.Value);
            }

            foreach (var pair in pendingLnType2Segments)
            {
                if (pair.Value.Count == 0)
                    continue;

                decodedChart.Warnings.Add($@"Unclosed LNTYPE 2 long note starting at measure {pair.Value[0].MeasureIndex:000}, channel {pair.Key:X2}. Keeping collected segment objects as normal notes.");

                foreach (var laneEvent in pair.Value)
                    decodedChart.ObjectEvents.Add(laneEvent);
            }
        }

        private static void handleInlineBpmEvent(BmsDecodedChart decodedChart, BmsChannelEvent channelEvent)
        {
            if (!int.TryParse(channelEvent.RawValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bpmValue) || bpmValue == 0)
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

            if (!decodedChart.BeatmapInfo.ExtendedBpmTable.TryGetValue(bpmIndex, out double bpmValue) || bpmValue == 0)
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

        private static bool tryPopPendingLnObjHead(int laneChannel, IDictionary<int, List<int>> pendingLnObjHeads, out int headIndex)
        {
            if (pendingLnObjHeads.TryGetValue(laneChannel, out var laneEvents) && laneEvents.Count > 0)
            {
                int lastIndex = laneEvents.Count - 1;
                headIndex = laneEvents[lastIndex];
                laneEvents.RemoveAt(lastIndex);

                if (laneEvents.Count == 0)
                    pendingLnObjHeads.Remove(laneChannel);

                return true;
            }

            headIndex = -1;
            return false;
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

        private static bool isInvisibleObjectChannel(int channel)
            => channel is >= 0x31 and <= 0x3F || channel is >= 0x41 and <= 0x4F;

        private static bool isScrollChannel(BmsChannelEvent channelEvent)
            => channelEvent.Channel == unknown_channel && channelEvent.RawChannelToken.Equals("SC", StringComparison.OrdinalIgnoreCase);

        private static bool isMineChannel(int channel)
            => channel is >= 0xD1 and <= 0xD9 || channel is >= 0xE1 and <= 0xE9;

        private static BmsBgaLayer getBgaLayer(int channel)
            => channel switch
            {
                0x04 => BmsBgaLayer.Base,
                0x06 => BmsBgaLayer.Poor,
                0x07 => BmsBgaLayer.Layer,
                0x0A => BmsBgaLayer.Layer2,
                _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, @"Unsupported BGA channel."),
            };

        private static void handleChannelLine(BmsDecodedChart decodedChart, List<BmsMeasureLengthControlPoint> measureLengthControlPoints, ISet<int> playableChannels, int measureIndex, int channel, string rawChannelToken, string payload, int sourceLineOrder)
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
                bool isExplicitRest = token == "00";

                if (isExplicitRest && !shouldPreserveExplicitRestToken(channel))
                    continue;

                decodedChart.RawChannelEvents.Add(new BmsChannelEvent(measureIndex, channel, rawChannelToken, (double)i / sliceCount, token, sourceLineOrder));

                if (!isExplicitRest && TryNormalizePlayableChannel(channel, out int normalizedChannel))
                    playableChannels.Add(normalizedChannel);
            }
        }

        private static bool shouldPreserveExplicitRestToken(int channel)
            => channel is >= 0x51 and <= 0x8C;

        private static BmsKeymode detectKeymode(string? filePath, ISet<int> playableChannels)
        {
            string extension = Path.GetExtension(filePath ?? string.Empty);

            if (extension.Equals(".pms", StringComparison.OrdinalIgnoreCase))
                return BmsKeymode.Key9K_Pms;

            if (containsAny(playableChannels, 0x21, 0x29))
                return BmsKeymode.Key14K;

            // Channel 0x17 only exists on the 9-key BMS layout, so a sparse chart that touches this lane
            // must stay on the 9K path even when not all nine lanes appear in the file.
            if (playableChannels.Contains(0x17) && !extension.Equals(".bme", StringComparison.OrdinalIgnoreCase))
                return BmsKeymode.Key9K_Bms;

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

        private static bool tryParseBgaDefinition(string value, out string bitmapReference, out int sourceX1, out int sourceY1, out int sourceX2, out int sourceY2, out int destinationX, out int destinationY)
        {
            bitmapReference = string.Empty;
            sourceX1 = 0;
            sourceY1 = 0;
            sourceX2 = 0;
            sourceY2 = 0;
            destinationX = 0;
            destinationY = 0;

            string[] parts = splitWhitespaceArguments(value);

            if (parts.Length != 7)
                return false;

            bitmapReference = parts[0];

            return bitmapReference.Length > 0
                   && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceX1)
                   && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceY1)
                   && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceX2)
                   && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceY2)
                   && int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out destinationX)
                   && int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out destinationY);
        }

        private static bool tryParseAtBgaDefinition(string value, out string bitmapReference, out int sourceX, out int sourceY, out int width, out int height, out int destinationX, out int destinationY)
        {
            bitmapReference = string.Empty;
            sourceX = 0;
            sourceY = 0;
            width = 0;
            height = 0;
            destinationX = 0;
            destinationY = 0;

            string[] parts = splitWhitespaceArguments(value);

            if (parts.Length != 7)
                return false;

            bitmapReference = parts[0];

            return bitmapReference.Length > 0
                   && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceX)
                   && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceY)
                   && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
                   && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
                   && int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out destinationX)
                   && int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out destinationY);
        }

        private static bool tryParseArgbComponents(string value, out int alpha, out int red, out int green, out int blue)
        {
            alpha = 0;
            red = 0;
            green = 0;
            blue = 0;

            string[] parts = splitArgbArguments(value);

            if (parts.Length != 4)
                return false;

            return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out alpha)
                   && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out red)
                   && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out green)
                   && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out blue)
                   && alpha is >= 0 and <= 255
                   && red is >= 0 and <= 255
                   && green is >= 0 and <= 255
                   && blue is >= 0 and <= 255;
        }

        private static bool tryParseSwBgaDefinition(string value, out int frameDurationMilliseconds, out int totalDurationMilliseconds, out int lineChannel, out bool loop, out int alpha, out int red, out int green, out int blue, out string pattern)
        {
            frameDurationMilliseconds = 0;
            totalDurationMilliseconds = 0;
            lineChannel = 0;
            loop = false;
            alpha = 0;
            red = 0;
            green = 0;
            blue = 0;
            pattern = string.Empty;

            int separatorIndex = indexOfFirstWhitespace(value);

            if (separatorIndex < 0)
                return false;

            string settings = value[..separatorIndex].Trim();
            pattern = value[(separatorIndex + 1)..].Trim();

            if (pattern.Length == 0 || pattern.Length % 2 != 0)
                return false;

            string[] settingParts = settings.Split(':');

            if (settingParts.Length != 5)
                return false;

            if (!int.TryParse(settingParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out frameDurationMilliseconds) || frameDurationMilliseconds <= 0)
                return false;

            if (!int.TryParse(settingParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out totalDurationMilliseconds) || totalDurationMilliseconds < 0)
                return false;

            if (!int.TryParse(settingParts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lineChannel))
                return false;

            if (!int.TryParse(settingParts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int loopValue) || loopValue is < 0 or > 1)
                return false;

            if (!tryParseArgbComponents(settingParts[4], out alpha, out red, out green, out blue))
                return false;

            loop = loopValue == 1;
            return true;
        }

        private static string[] splitWhitespaceArguments(string value)
            => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        private static string[] splitArgbArguments(string value)
            => value.Replace(',', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        private static int indexOfFirstWhitespace(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                    return i;
            }

            return -1;
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

        private static bool tryParseChannelLine(string line, out int measureIndex, out int channel, out string rawChannelToken, out string payload)
        {
            measureIndex = default;
            channel = default;
            rawChannelToken = string.Empty;
            payload = string.Empty;

            if (line.Length < 7 || line[0] != '#')
                return false;

            int separatorIndex = line.IndexOf(':');

            if (separatorIndex != 6)
                return false;

            if (!int.TryParse(line.AsSpan(1, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out measureIndex))
                return false;

            rawChannelToken = line.Substring(4, 2);

            payload = line[(separatorIndex + 1)..].Trim();

            if (!int.TryParse(rawChannelToken, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out channel))
                channel = unknown_channel;

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

        private static bool tryGetControlDirective(string line, out string key, out string value)
        {
            splitDirective(line, out key, out value);

            return control_directive_keys.Contains(key);
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
