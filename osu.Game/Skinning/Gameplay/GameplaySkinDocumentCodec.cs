// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The single shared tokenizer/codec for C4 gameplay authoring sections and retained legacy sections.
    /// </summary>
    public static class GameplaySkinDocumentCodec
    {
        public const int VERSION = 1;
        public const string CONTRACT_ID = "oms-gameplay-skin-codec.v1";

        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);

        public static GameplaySkinDocument Decode(ReadOnlyMemory<byte> content, GameplaySkinDocumentIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(identity);

            string text;

            try
            {
                ReadOnlySpan<byte> bytes = content.Span;

                if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
                    bytes = bytes[3..];

                text = strict_utf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return new GameplaySkinDocument(
                    identity,
                    Array.Empty<GameplaySkinDocumentSection>(),
                    Array.Empty<GameplaySkinLegacySection>(),
                    Array.AsReadOnly(new[] { new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidUtf8, 0) }),
                    Array.Empty<string>());
            }

            return decodeText(text, identity);
        }

        public static GameplaySkinDocument Decode(string content, GameplaySkinDocumentIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(content);
            return Decode(strict_utf8.GetBytes(content), identity);
        }

        /// <summary>
        /// Encodes the normalized, immutable token stream. No filesystem or package source is read.
        /// </summary>
        public static string Encode(GameplaySkinDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            return string.Join("\n", document.NormalizedSourceLines);
        }

        public static byte[] EncodeUtf8(GameplaySkinDocument document) => strict_utf8.GetBytes(Encode(document));

        private static GameplaySkinDocument decodeText(string text, GameplaySkinDocumentIdentity identity)
        {
            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            var sections = new List<SectionBuilder>();
            var legacySections = new List<LegacySectionBuilder>();
            var diagnostics = new List<GameplaySkinCodecDiagnostic>();
            var declarations = new HashSet<DeclarationKey>();
            SectionBuilder? currentSection = null;
            LegacySectionBuilder? currentLegacySection = null;
            bool unsupportedGameplaySection = false;

            for (int index = 0; index < lines.Length; index++)
            {
                int lineNumber = index + 1;
                string normalizedLine = lines[index].TrimEnd();

                if (normalizedLine.Contains('\ufeff'))
                    diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.UnexpectedBom, lineNumber));

                string gameplayHeaderCandidate = stripComment(normalizedLine).Trim();

                // C4 authoring headers use the shared codec's whitespace/comment rules. Legacy headers deliberately
                // retain LegacyDecoder semantics: no leading-whitespace normalisation, but an inline // comment after
                // a closing bracket is stripped before header recognition. This preserves old skins while still
                // ensuring that every consumer reads one token stream.
                if (looksLikeGameplaySection(gameplayHeaderCandidate))
                {
                    currentSection = null;
                    currentLegacySection = null;
                    unsupportedGameplaySection = false;

                    if (!tryParseSectionHeader(gameplayHeaderCandidate, out string? gameplaySectionName))
                    {
                        diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.MalformedSectionHeader, lineNumber));
                        unsupportedGameplaySection = true;
                        continue;
                    }

                    if (!gameplaySectionName.StartsWith("GameplaySkin.", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.MalformedSectionHeader, lineNumber));
                        unsupportedGameplaySection = true;
                        continue;
                    }

                    if (tryParseGameplaySection(gameplaySectionName, lineNumber, diagnostics, out GameplaySkinSlotCatalogFamily family, out int version, out bool gameplayLike))
                    {
                        currentSection = new SectionBuilder(family, version);
                        sections.Add(currentSection);
                    }
                    else if (gameplayLike)
                    {
                        unsupportedGameplaySection = true;
                    }
                    continue;
                }

                string legacyHeaderCandidate = stripLegacyInlineComment(normalizedLine).TrimEnd();

                if (tryParseLegacySectionHeader(legacyHeaderCandidate, out string? legacySectionName))
                {
                    currentSection = null;
                    unsupportedGameplaySection = false;
                    currentLegacySection = new LegacySectionBuilder(legacySectionName, lineNumber);
                    legacySections.Add(currentLegacySection);
                    continue;
                }

                if (currentSection != null)
                {
                    parseGameplayLine(normalizedLine, lineNumber, currentSection, diagnostics, declarations);
                    continue;
                }

                if (unsupportedGameplaySection)
                    continue;

                currentLegacySection ??= createPreamble(legacySections);
                currentLegacySection.Lines.Add(parseLegacyLine(normalizedLine, lineNumber, currentLegacySection.Name));
            }

            GameplaySkinDocumentSection[] immutableSections = sections
                .Select(section => new GameplaySkinDocumentSection(section.Family, section.Version, section.Entries.ToArray()))
                .ToArray();
            GameplaySkinLegacySection[] immutableLegacySections = legacySections
                .Where(section => section.HeaderLineNumber > 0 || section.Lines.Count > 0)
                .Select(section => new GameplaySkinLegacySection(section.Name, section.HeaderLineNumber, section.Lines.ToArray()))
                .ToArray();

            return new GameplaySkinDocument(
                identity,
                Array.AsReadOnly(immutableSections),
                Array.AsReadOnly(immutableLegacySections),
                Array.AsReadOnly(diagnostics.ToArray()),
                Array.AsReadOnly(lines.Select(line => line.TrimEnd()).ToArray()));
        }

        private static bool tryParseSectionHeader(string line, out string sectionName)
        {
            if (line.Length >= 2 && line[0] == '[' && line[^1] == ']')
            {
                sectionName = line[1..^1];
                return true;
            }

            sectionName = string.Empty;
            return false;
        }

        private static bool looksLikeGameplaySection(string line)
        {
            if (line.Length < 2 || line[0] != '[')
                return false;

            return line.AsSpan(1).TrimStart().StartsWith("GameplaySkin.".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool tryParseLegacySectionHeader(string line, out string sectionName)
        {
            if (line.Length >= 2 && line[0] == '[' && line[^1] == ']')
            {
                sectionName = line[1..^1];
                return true;
            }

            sectionName = string.Empty;
            return false;
        }

        private static bool tryParseGameplaySection(
            string name,
            int lineNumber,
            List<GameplaySkinCodecDiagnostic> diagnostics,
            out GameplaySkinSlotCatalogFamily family,
            out int version,
            out bool gameplayLike)
        {
            family = default;
            version = 0;
            gameplayLike = name.StartsWith("GameplaySkin.", StringComparison.OrdinalIgnoreCase);

            if (!gameplayLike)
                return false;

            if (!name.StartsWith("GameplaySkin.", StringComparison.Ordinal))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.UnknownExtension, lineNumber));
                return false;
            }

            int separator = name.LastIndexOf(':');

            if (separator < 0 || !tryParseCanonicalNonNegativeInt(name.AsSpan(separator + 1), out version))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.UnsupportedVersion, lineNumber));
                return false;
            }

            string familyToken = name["GameplaySkin.".Length..separator];

            family = familyToken switch
            {
                "Common" => GameplaySkinSlotCatalogFamily.Common,
                "Bms" => GameplaySkinSlotCatalogFamily.Bms,
                _ => (GameplaySkinSlotCatalogFamily)(-1),
            };

            if (!Enum.IsDefined(family))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.UnknownExtension, lineNumber));
                return false;
            }

            if (!GameplaySkinSlotCatalog.IsSupportedVersion(family, version))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.UnsupportedVersion, lineNumber));
                return false;
            }

            return true;
        }

        private static void parseGameplayLine(
            string line,
            int lineNumber,
            SectionBuilder section,
            List<GameplaySkinCodecDiagnostic> diagnostics,
            HashSet<DeclarationKey> declarations)
        {
            string content = stripComment(line).Trim();

            if (content.Length == 0)
                return;

            int separator = findOutsideQuotes(content, ':');

            if (separator <= 0)
            {
                if (content.StartsWith("Target", StringComparison.OrdinalIgnoreCase))
                    section.CurrentTarget = null;

                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.UnknownField, lineNumber));
                section.Entries.Add(invalidEntry(section.CurrentTarget ?? GameplaySkinDocumentTarget.Global, lineNumber));
                return;
            }

            string field = content[..separator].Trim();
            string value = content[(separator + 1)..].Trim();

            if (field.StartsWith("Target", StringComparison.OrdinalIgnoreCase))
            {
                section.CurrentTarget = null;

                if (field != "Target")
                {
                    diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.UnknownField, lineNumber));
                    return;
                }

                section.CurrentTarget = parseTarget(value, lineNumber, diagnostics);
                return;
            }

            parseEntry(field, value, lineNumber, section, diagnostics, declarations);
        }

        private static GameplaySkinDocumentTarget? parseTarget(
            string value,
            int lineNumber,
            List<GameplaySkinCodecDiagnostic> diagnostics)
        {
            if (!tryTokenize(value, out List<Token> tokens, out GameplaySkinCodecDiagnosticCode tokenError))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(tokenError, lineNumber));
                return null;
            }

            if (tokens.Count == 0)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetScope, lineNumber));
                return null;
            }

            GameplaySkinDocumentTargetKind kind = tokens[0].Value switch
            {
                "Global" => GameplaySkinDocumentTargetKind.Global,
                "Stage" => GameplaySkinDocumentTargetKind.Stage,
                "Group" => GameplaySkinDocumentTargetKind.Group,
                "Lane" => GameplaySkinDocumentTargetKind.Lane,
                _ => (GameplaySkinDocumentTargetKind)(-1),
            };

            if (!Enum.IsDefined(kind))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetScope, lineNumber));
                return null;
            }

            var properties = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Token token in tokens.Skip(1))
            {
                int equals = token.Value.IndexOf('=');

                if (equals <= 0 || equals == token.Value.Length - 1 || !properties.TryAdd(token.Value[..equals], token.Value[(equals + 1)..]))
                {
                    diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetIndex, lineNumber));
                    return null;
                }
            }

            if (!tryParseSelectors(
                    properties,
                    out GameplaySkinDocumentRulesetSelector rulesetSelector,
                    out string keymodeSelector,
                    out GameplaySkinDocumentStageModeSelector stageModeSelector))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetContext, lineNumber));
                return null;
            }

            try
            {
                if (kind == GameplaySkinDocumentTargetKind.Global)
                {
                    if (properties.Count != 3)
                        throw new InvalidDataException();

                    return GameplaySkinDocumentTarget.ForGlobal(rulesetSelector, keymodeSelector, stageModeSelector);
                }

                GameplaySkinLaneGroupId groupId = GameplaySkinLaneGroupId.Create(requireProperty(properties, "group"));
                int groupLogical = parseIndex(properties, "group-logical");
                int groupVisual = parseIndex(properties, "group-visual");

                if (kind == GameplaySkinDocumentTargetKind.Stage || kind == GameplaySkinDocumentTargetKind.Group)
                {
                    if (properties.Count != 6)
                        throw new InvalidDataException();

                    return kind == GameplaySkinDocumentTargetKind.Stage
                        ? GameplaySkinDocumentTarget.ForStage(
                            rulesetSelector, keymodeSelector, stageModeSelector, groupId, groupLogical, groupVisual)
                        : GameplaySkinDocumentTarget.ForGroup(
                            rulesetSelector, keymodeSelector, stageModeSelector, groupId, groupLogical, groupVisual);
                }

                if (properties.Count != 11)
                    throw new InvalidDataException();

                return GameplaySkinDocumentTarget.ForLane(
                    rulesetSelector,
                    keymodeSelector,
                    stageModeSelector,
                    groupId,
                    GameplaySkinLaneId.Create(requireProperty(properties, "lane")),
                    groupLogical,
                    groupVisual,
                    parseIndex(properties, "global-logical"),
                    parseIndex(properties, "global-visual"),
                    parseIndex(properties, "group-local-logical"),
                    parseIndex(properties, "group-local-visual"));
            }
            catch (ArgumentException)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetIdentity, lineNumber));
            }
            catch (InvalidDataException)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetIndex, lineNumber));
            }

            return null;
        }

        private static string requireProperty(Dictionary<string, string> properties, string name)
        {
            if (!properties.TryGetValue(name, out string? value))
                throw new InvalidDataException();

            return value;
        }

        private static int parseIndex(Dictionary<string, string> properties, string name)
        {
            if (!properties.TryGetValue(name, out string? value)
                || !tryParseCanonicalNonNegativeInt(value, out int index))
                throw new InvalidDataException();

            return index;
        }

        private static bool tryParseSelectors(
            IReadOnlyDictionary<string, string> properties,
            out GameplaySkinDocumentRulesetSelector rulesetSelector,
            out string keymodeSelector,
            out GameplaySkinDocumentStageModeSelector stageModeSelector)
        {
            rulesetSelector = default;
            keymodeSelector = string.Empty;
            stageModeSelector = default;

            if (!properties.TryGetValue("ruleset", out string? ruleset)
                || !properties.TryGetValue("keymode", out string? parsedKeymodeSelector)
                || !properties.TryGetValue("stage-mode", out string? stageMode))
            {
                return false;
            }

            keymodeSelector = parsedKeymodeSelector;

            rulesetSelector = ruleset switch
            {
                "any" => GameplaySkinDocumentRulesetSelector.Any,
                "mania" => GameplaySkinDocumentRulesetSelector.Mania,
                "bms" => GameplaySkinDocumentRulesetSelector.Bms,
                _ => (GameplaySkinDocumentRulesetSelector)(-1),
            };
            stageModeSelector = stageMode switch
            {
                "any" => GameplaySkinDocumentStageModeSelector.Any,
                "single" => GameplaySkinDocumentStageModeSelector.Single,
                "dual" => GameplaySkinDocumentStageModeSelector.Dual,
                _ => (GameplaySkinDocumentStageModeSelector)(-1),
            };

            if (!Enum.IsDefined(rulesetSelector)
                || !Enum.IsDefined(stageModeSelector)
                || keymodeSelector.Length == 0)
            {
                return false;
            }

            try
            {
                _ = GameplaySkinDocumentTarget.ForGlobal(rulesetSelector, keymodeSelector, stageModeSelector);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool tryParseCanonicalNonNegativeInt(ReadOnlySpan<char> token, out int value)
        {
            value = 0;

            if (token.IsEmpty || token.Length > 1 && token[0] == '0')
                return false;

            foreach (char character in token)
            {
                if (character is not (>= '0' and <= '9'))
                    return false;

                try
                {
                    value = checked(value * 10 + character - '0');
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            return true;
        }

        private static void parseEntry(
            string field,
            string value,
            int lineNumber,
            SectionBuilder section,
            List<GameplaySkinCodecDiagnostic> diagnostics,
            HashSet<DeclarationKey> declarations)
        {
            GameplaySkinDocumentTarget target = section.CurrentTarget ?? GameplaySkinDocumentTarget.Global;
            bool invalid = false;

            if (section.CurrentTarget == null)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.MissingTarget, lineNumber));
                invalid = true;
            }


            if (!GameplaySkinSlotCatalog.TryGet(field, out var descriptor))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(
                    field.Contains('.', StringComparison.Ordinal) ? GameplaySkinCodecDiagnosticCode.UnknownSlot : GameplaySkinCodecDiagnosticCode.UnknownField,
                    lineNumber));
                invalid = true;
            }
            else
            {
                if (descriptor.CatalogFamily != section.Family || descriptor.CatalogVersion != section.Version)
                {
                    diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.ExtensionSlotMismatch, lineNumber, descriptor.Id));
                    invalid = true;
                }

                GameplaySkinSlotScope targetScope = target.Kind switch
                {
                    GameplaySkinDocumentTargetKind.Global => GameplaySkinSlotScope.Global,
                    GameplaySkinDocumentTargetKind.Stage => GameplaySkinSlotScope.Stage,
                    GameplaySkinDocumentTargetKind.Group => GameplaySkinSlotScope.Group,
                    GameplaySkinDocumentTargetKind.Lane => GameplaySkinSlotScope.Lane,
                    _ => GameplaySkinSlotScope.None,
                };

                if ((descriptor.AllowedScopes & targetScope) == 0)
                {
                    diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetScope, lineNumber, descriptor.Id));
                    invalid = true;
                }

                if (GameplaySkinSlotApplicabilityValidator.ValidateDeclaration(descriptor, target)
                    != GameplaySkinSlotApplicabilityResult.Applicable)
                {
                    diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidTargetApplicability, lineNumber, descriptor.Id));
                    invalid = true;
                }
            }

            bool declarationAdded = descriptor == null || declarations.Add(new DeclarationKey(descriptor, target));

            if (descriptor != null && !declarationAdded)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.DuplicateDeclaration, lineNumber, descriptor.Id));
                invalid = true;
            }

            if (!tryTokenize(value, out List<Token> tokens, out GameplaySkinCodecDiagnosticCode tokenError))
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(tokenError, lineNumber, descriptor?.Id));
                section.Entries.Add(new GameplaySkinDocumentEntry(
                    GameplaySkinDocumentDeclarationPresence.Declared,
                    GameplaySkinDocumentValueValidity.Invalid,
                    GameplaySkinDocumentOperation.None,
                    descriptor,
                    field,
                    null,
                    target,
                    null,
                    lineNumber));
                return;
            }

            GameplaySkinSlotValueType? declaredType = tokens.Count > 0 ? parseValueType(tokens[0].Value) : null;

            if (declaredType == null || descriptor != null && declaredType != descriptor.ValueType)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidValueType, lineNumber, descriptor?.Id));
                invalid = true;
            }

            GameplaySkinDocumentOperation operation = tokens.Count > 1 ? parseOperation(tokens[1].Value) : GameplaySkinDocumentOperation.None;

            if (operation == GameplaySkinDocumentOperation.None)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidState, lineNumber, descriptor?.Id));
                invalid = true;
            }

            string? providedValue = null;

            if (operation == GameplaySkinDocumentOperation.Provide)
            {
                if (tokens.Count != 3 || !tokens[2].WasQuoted)
                {
                    diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.MissingValue, lineNumber, descriptor?.Id));
                    invalid = true;
                }
                else
                {
                    providedValue = tokens[2].Value;
                }
            }
            else if (tokens.Count != 2)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.InvalidState, lineNumber, descriptor?.Id));
                invalid = true;
            }

            if (descriptor != null
                && operation == GameplaySkinDocumentOperation.Suppress
                && descriptor.SuppressEligibility != GameplaySkinSlotSuppressEligibility.Allowed)
            {
                diagnostics.Add(new GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode.SuppressionForbidden, lineNumber, descriptor.Id));
                invalid = true;
            }

            GameplaySkinDocumentValueValidity validity = invalid
                ? GameplaySkinDocumentValueValidity.Invalid
                : operation == GameplaySkinDocumentOperation.Provide && providedValue?.Length == 0
                    ? GameplaySkinDocumentValueValidity.Empty
                    : GameplaySkinDocumentValueValidity.Valid;

            section.Entries.Add(new GameplaySkinDocumentEntry(
                GameplaySkinDocumentDeclarationPresence.Declared,
                validity,
                operation,
                descriptor,
                field,
                declaredType,
                target,
                providedValue,
                lineNumber));
        }

        private static GameplaySkinSlotValueType? parseValueType(string value)
            => value switch
            {
                "resource" => GameplaySkinSlotValueType.Resource,
                "colour" => GameplaySkinSlotValueType.Colour,
                "number" => GameplaySkinSlotValueType.Number,
                "boolean" => GameplaySkinSlotValueType.Boolean,
                "text" => GameplaySkinSlotValueType.Text,
                _ => null,
            };

        private static GameplaySkinDocumentOperation parseOperation(string value)
            => value switch
            {
                "Provide" => GameplaySkinDocumentOperation.Provide,
                "Inherit" => GameplaySkinDocumentOperation.Inherit,
                "Suppress" => GameplaySkinDocumentOperation.Suppress,
                _ => GameplaySkinDocumentOperation.None,
            };

        private static GameplaySkinDocumentEntry invalidEntry(GameplaySkinDocumentTarget target, int lineNumber)
            => new GameplaySkinDocumentEntry(
                GameplaySkinDocumentDeclarationPresence.Declared,
                GameplaySkinDocumentValueValidity.Invalid,
                GameplaySkinDocumentOperation.None,
                null,
                null,
                null,
                target,
                null,
                lineNumber);

        private static bool tryTokenize(string value, out List<Token> tokens, out GameplaySkinCodecDiagnosticCode error)
        {
            tokens = new List<Token>();
            error = GameplaySkinCodecDiagnosticCode.InvalidState;
            int index = 0;

            while (index < value.Length)
            {
                while (index < value.Length && char.IsWhiteSpace(value[index]))
                    index++;

                if (index >= value.Length)
                    break;

                if (value[index] == '"')
                {
                    index++;
                    var result = new StringBuilder();
                    bool closed = false;

                    while (index < value.Length)
                    {
                        char character = value[index++];

                        if (character == '"')
                        {
                            closed = true;
                            break;
                        }

                        if (character != '\\')
                        {
                            result.Append(character);
                            continue;
                        }

                        if (index >= value.Length)
                        {
                            error = GameplaySkinCodecDiagnosticCode.InvalidEscape;
                            return false;
                        }

                        char escaped = value[index++];

                        result.Append(escaped switch
                        {
                            '\\' => '\\',
                            '"' => '"',
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            _ => '\0',
                        });

                        if (result[^1] == '\0')
                        {
                            error = GameplaySkinCodecDiagnosticCode.InvalidEscape;
                            return false;
                        }
                    }

                    if (!closed)
                    {
                        error = GameplaySkinCodecDiagnosticCode.InvalidEscape;
                        return false;
                    }

                    tokens.Add(new Token(result.ToString(), true));
                }
                else
                {
                    int start = index;

                    while (index < value.Length && !char.IsWhiteSpace(value[index]))
                        index++;

                    tokens.Add(new Token(value[start..index], false));
                }
            }

            return true;
        }

        private static string stripComment(string value)
        {
            bool quoted = false;
            bool escaped = false;

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (quoted && character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (!quoted && character is '#' or ';')
                    return value[..index];
            }

            return value;
        }

        private static string stripLegacyInlineComment(string value)
        {
            int index = value.IndexOf("//", StringComparison.Ordinal);
            return index > 0 ? value[..index] : value;
        }

        private static int findOutsideQuotes(string value, char sought)
        {
            bool quoted = false;
            bool escaped = false;

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (quoted && character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (!quoted && character == sought)
                    return index;
            }

            return -1;
        }

        private static GameplaySkinLegacyLine parseLegacyLine(string line, int lineNumber, string sectionName)
        {
            // Retain the exact normalized source text for round-trip while deriving legacy semantics once here.
            // LegacyDecoder strips // comments everywhere except [Metadata], trims the end, and splits only on the
            // first colon. Core, mania and BMS consumers receive these immutable semantic tokens and never split the
            // source line again.
            string semantic = sectionName == "Metadata" ? line : stripLegacyInlineComment(line);
            semantic = semantic.TrimEnd();
            string trimmed = semantic.Trim();

            if (trimmed.Length == 0)
                return new GameplaySkinLegacyLine(GameplaySkinLegacyLineKind.Blank, lineNumber, line, null, null, null);

            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed[0] is '#' or ';')
                return new GameplaySkinLegacyLine(GameplaySkinLegacyLineKind.Comment, lineNumber, line, null, null, null);

            int separatorIndex = semantic.IndexOf(':');

            if (separatorIndex > 0)
            {
                return new GameplaySkinLegacyLine(
                    GameplaySkinLegacyLineKind.Field,
                    lineNumber,
                    line,
                    semantic[..separatorIndex].Trim(),
                    semantic[(separatorIndex + 1)..].Trim(),
                    ':');
            }

            return new GameplaySkinLegacyLine(
                GameplaySkinLegacyLineKind.Unparsed,
                lineNumber,
                line,
                trimmed,
                string.Empty,
                null);
        }

        private static LegacySectionBuilder createPreamble(List<LegacySectionBuilder> sections)
        {
            var preamble = new LegacySectionBuilder(string.Empty, 0);
            sections.Add(preamble);
            return preamble;
        }

        private sealed class SectionBuilder
        {
            public GameplaySkinSlotCatalogFamily Family { get; }

            public int Version { get; }

            public List<GameplaySkinDocumentEntry> Entries { get; } = new List<GameplaySkinDocumentEntry>();

            public GameplaySkinDocumentTarget? CurrentTarget { get; set; }

            public SectionBuilder(GameplaySkinSlotCatalogFamily family, int version)
            {
                Family = family;
                Version = version;
            }
        }

        private sealed class LegacySectionBuilder
        {
            public string Name { get; }

            public int HeaderLineNumber { get; }

            public List<GameplaySkinLegacyLine> Lines { get; } = new List<GameplaySkinLegacyLine>();

            public LegacySectionBuilder(string name, int headerLineNumber)
            {
                Name = name;
                HeaderLineNumber = headerLineNumber;
            }
        }

        private readonly record struct Token(string Value, bool WasQuoted);

        private readonly record struct DeclarationKey(GameplaySkinSlotDescriptor Descriptor, GameplaySkinDocumentTarget Target);
    }
}
