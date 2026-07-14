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
            ArgumentNullException.ThrowIfNull(providers);

            if (requirement is not SkinSlotRequirement.Critical and not SkinSlotRequirement.Optional)
                throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unknown gameplay skin slot requirement.");

            var diagnostics = new List<GameplaySkinSlotDiagnostic>();

            foreach (var provider in providers)
            {
                if (provider == null)
                {
                    diagnostics.Add(new GameplaySkinSlotDiagnostic(
                        GameplaySkinSlotDiagnosticCode.ProviderFailed,
                        slot,
                        "<null>",
                        new InvalidOperationException("The gameplay skin slot provider chain contained a null provider.")));
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
                    diagnostics.Add(new GameplaySkinSlotDiagnostic(GameplaySkinSlotDiagnosticCode.ProviderFailed, slot, providerName, exception));
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
                                diagnostics.Add(new GameplaySkinSlotDiagnostic(GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed, slot, providerName, exception));
                                continue;
                            }

                            if (!isValid)
                            {
                                diagnostics.Add(new GameplaySkinSlotDiagnostic(GameplaySkinSlotDiagnosticCode.ProvidedValueRejected, slot, providerName));
                                continue;
                            }
                        }

                        return complete(result, providerName, diagnostics);

                    case SkinSlotResultKind.Suppress:
                        if (requirement == SkinSlotRequirement.Optional)
                            return complete(result, providerName, diagnostics);

                        diagnostics.Add(new GameplaySkinSlotDiagnostic(GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected, slot, providerName));
                        continue;

                    default:
                        diagnostics.Add(new GameplaySkinSlotDiagnostic(GameplaySkinSlotDiagnosticCode.InvalidResult, slot, providerName));
                        continue;
                }
            }

            return complete(SkinSlotResult<TComponent>.Inherit, null, diagnostics);
        }

        private static GameplaySkinSlotResolution<T> complete<T>(SkinSlotResult<T> result, string? providerName, List<GameplaySkinSlotDiagnostic> diagnostics)
            where T : notnull
            => new GameplaySkinSlotResolution<T>(result, providerName, diagnostics.Count == 0 ? Array.Empty<GameplaySkinSlotDiagnostic>() : diagnostics.ToArray());
    }
}
