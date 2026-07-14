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
        public static GameplaySkinSlotResolution<TComponent> Resolve<TSlot, TComponent>(
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

            if (descriptor != null && requirement != descriptor.Requirement)
                throw new ArgumentException("A catalogued gameplay skin slot must use its descriptor requirement.", nameof(requirement));

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

            return resolve(lookup, descriptor.Requirement, providers, validator, descriptor.Id);
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
                        "<null>",
                        new InvalidOperationException("The gameplay skin slot provider chain contained a null provider."),
                        slotId));
                    continue;
                }

                string providerName = provider.GetType().Name;
                SkinSlotResult<TComponent> result;

                try
                {
                    string suppliedName = provider.Name;

                    if (!string.IsNullOrWhiteSpace(suppliedName))
                        providerName = suppliedName;

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
            => new GameplaySkinSlotResolution<T>(result, providerName, diagnostics.Count == 0 ? Array.Empty<GameplaySkinSlotDiagnostic>() : diagnostics.ToArray());

        private static GameplaySkinSlotDiagnostic createDiagnostic(
            GameplaySkinSlotDiagnosticCode code,
            object slot,
            string providerName,
            Exception? exception,
            string? slotId)
            => new GameplaySkinSlotDiagnostic(code, slot, providerName, exception)
            {
                SlotId = slotId,
            };
    }
}
