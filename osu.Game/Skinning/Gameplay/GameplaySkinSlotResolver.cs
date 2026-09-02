// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Resolves gameplay skin slots without changing the nullable <see cref="ISkin"/> compatibility ABI.
    /// </summary>
    public static class GameplaySkinSlotResolver
    {
        private const string redacted_provider_name = "redacted-provider";

        public const int VERSION = 1;

        public const string CONTRACT_ID = "oms-gameplay-skin-resolver.v1";

        /// <summary>
        /// Resolves a slot from <paramref name="providers"/> in enumeration order.
        /// </summary>
        /// <remarks>
        /// Provider failures and rejected values behave as <see cref="SkinSlotResultKind.Inherit"/> and produce diagnostics.
        /// Suppression ends resolution only for <see cref="SkinSlotRequirement.Optional"/> slots.
        /// The optional validator is a failure-isolation boundary, not a lifecycle owner; rejected values are not disposed because providers may cache or share them.
        /// This overload remains for uncatalogued compatibility lookups. New catalogued gameplay resolution must use the descriptor overload;
        /// wrapping a catalog ID in an unrelated lookup type does not make this compatibility entry point a slot-authority validator.
        /// </remarks>
        internal static GameplaySkinSlotResolution<TComponent> Resolve<TSlot, TComponent>(
            TSlot slot,
            SkinSlotRequirement requirement,
            IEnumerable<IGameplaySkinSlotProvider<TSlot, TComponent>> providers,
            Func<TComponent, bool>? validator = null)
            where TSlot : notnull
            where TComponent : notnull
        {
            ArgumentNullException.ThrowIfNull(slot);

            if (requirement is not SkinSlotRequirement.Critical and not SkinSlotRequirement.Optional)
                throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unknown gameplay skin slot requirement.");

            GameplaySkinSlotDescriptor? descriptor = slot switch
            {
                GameplaySkinSlotDescriptor directDescriptor => directDescriptor,
                IGameplaySkinSlotLookup cataloguedLookup => cataloguedLookup.Descriptor,
                _ => null,
            };

            if (descriptor != null)
            {
                if (requirement != descriptor.Requirement)
                    throw new ArgumentException("A catalogued gameplay skin slot must use its descriptor requirement.", nameof(requirement));

                // Requirement is a pre-C4 compatibility projection and deliberately keeps recommended presentation
                // slots classified as Optional. Suppression authority comes only from the versioned public catalog.
                requirement = descriptor.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Allowed
                    ? SkinSlotRequirement.Optional
                    : SkinSlotRequirement.Critical;
            }

            return resolve(slot, requirement, providers, validator, descriptor?.Id);
        }

        /// <summary>
        /// Resolves a catalogued semantic slot while keeping lane, keymode and other ruleset context in a separate lookup value.
        /// </summary>
        /// <remarks>
        /// The descriptor is the sole requirement authority, preventing a critical slot from being accidentally resolved as optional.
        /// </remarks>
        public static GameplaySkinSlotResolution<TComponent> Resolve<TLookup, TComponent>(
            GameplaySkinSlotDescriptor descriptor,
            TLookup context,
            IEnumerable<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<TLookup>, TComponent>> providers,
            Func<TComponent, bool>? validator = null)
            where TLookup : notnull
            where TComponent : notnull
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(context);

            var lookup = new GameplaySkinSlotLookup<TLookup>(descriptor, context);

            SkinSlotRequirement suppressionRequirement = descriptor.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Allowed
                ? SkinSlotRequirement.Optional
                : SkinSlotRequirement.Critical;

            return resolve(lookup, suppressionRequirement, providers, validator, descriptor.Id);
        }

        private static GameplaySkinSlotResolution<TComponent> resolve<TSlot, TComponent>(
            TSlot slot,
            SkinSlotRequirement requirement,
            IEnumerable<IGameplaySkinSlotProvider<TSlot, TComponent>> providers,
            Func<TComponent, bool>? validator,
            string? slotId)
            where TSlot : notnull
            where TComponent : notnull
        {
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(providers);

            if (requirement is not SkinSlotRequirement.Critical and not SkinSlotRequirement.Optional)
                throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unknown gameplay skin slot requirement.");

            var diagnostics = new List<GameplaySkinSlotDiagnostic>();

            foreach (var provider in providers)
            {
                if (provider == null)
                {
                    diagnostics.Add(createDiagnostic(
                        GameplaySkinSlotDiagnosticCode.ProviderFailed,
                        slot,
                        redacted_provider_name,
                        new InvalidOperationException("The gameplay skin slot provider chain contained a null provider."),
                        slotId));
                    continue;
                }

                string providerName = redacted_provider_name;
                SkinSlotResult<TComponent> result;

                try
                {
                    providerName = sanitiseProviderName(provider.Name);

                    result = provider.GetSlot(slot);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    diagnostics.Add(createDiagnostic(GameplaySkinSlotDiagnosticCode.ProviderFailed, slot, providerName, exception, slotId));
                    continue;
                }

                switch (result.Kind)
                {
                    case SkinSlotResultKind.Inherit:
                        continue;

                    case SkinSlotResultKind.Provide:
                        if (validator != null)
                        {
                            bool isValid;

                            try
                            {
                                isValid = validator(result.Value);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                diagnostics.Add(createDiagnostic(GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed, slot, providerName, exception, slotId));
                                continue;
                            }

                            if (!isValid)
                            {
                                diagnostics.Add(createDiagnostic(GameplaySkinSlotDiagnosticCode.ProvidedValueRejected, slot, providerName, null, slotId));
                                continue;
                            }
                        }

                        return complete(result, providerName, diagnostics);

                    case SkinSlotResultKind.Suppress:
                        if (requirement == SkinSlotRequirement.Optional)
                            return complete(result, providerName, diagnostics);

                        diagnostics.Add(createDiagnostic(GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected, slot, providerName, null, slotId));
                        continue;

                    default:
                        diagnostics.Add(createDiagnostic(GameplaySkinSlotDiagnosticCode.InvalidResult, slot, providerName, null, slotId));
                        continue;
                }
            }

            return complete(SkinSlotResult<TComponent>.Inherit, null, diagnostics);
        }

        private static GameplaySkinSlotResolution<T> complete<T>(SkinSlotResult<T> result, string? providerName, List<GameplaySkinSlotDiagnostic> diagnostics)
            where T : notnull
            => new GameplaySkinSlotResolution<T>(
                result,
                providerName == null ? null : sanitiseProviderName(providerName),
                diagnostics.Count == 0 ? Array.Empty<GameplaySkinSlotDiagnostic>() : diagnostics.ToArray());

        private static GameplaySkinSlotDiagnostic createDiagnostic(
            GameplaySkinSlotDiagnosticCode code,
            object slot,
            string providerName,
            Exception? exception,
            string? slotId)
            => new GameplaySkinSlotDiagnostic(code, slot, sanitiseProviderName(providerName), exception)
            {
                SlotId = slotId,
            };

        /// <summary>
        /// Restricts the persistence-visible provider identity to a stable, path-free ASCII token.
        /// Author-controlled display names, paths and runtime type names are deliberately not used as fallbacks.
        /// </summary>
        private static string sanitiseProviderName(string? providerName)
        {
            if (string.IsNullOrEmpty(providerName) || providerName.Length > 128 || !isLowerAsciiLetter(providerName[0]))
                return redacted_provider_name;

            char previous = '\0';

            foreach (char character in providerName)
            {
                if (!isLowerAsciiLetter(character) && !char.IsAsciiDigit(character) && character is not '-' and not '.')
                    return redacted_provider_name;

                if (character == '.' && previous == '.')
                    return redacted_provider_name;

                previous = character;
            }

            return previous is '.' or '-' ? redacted_provider_name : providerName;
        }

        private static bool isLowerAsciiLetter(char character) => character is >= 'a' and <= 'z';
    }
}
