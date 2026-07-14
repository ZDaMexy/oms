// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The outcome of resolving one gameplay skin slot through an ordered provider chain.
    /// </summary>
    public sealed class GameplaySkinSlotResolution<T>
        where T : notnull
    {
        /// <summary>
        /// The final three-state result.
        /// </summary>
        public SkinSlotResult<T> Result { get; }

        /// <summary>
        /// The provider which supplied or suppressed the slot, or <see langword="null"/> if all providers inherited.
        /// </summary>
        public string? ProviderName { get; }

        /// <summary>
        /// Diagnostics produced while falling through the provider chain.
        /// </summary>
        public IReadOnlyList<GameplaySkinSlotDiagnostic> Diagnostics { get; }

        internal GameplaySkinSlotResolution(SkinSlotResult<T> result, string? providerName, IReadOnlyList<GameplaySkinSlotDiagnostic> diagnostics)
        {
            Result = result;
            ProviderName = providerName;
            Diagnostics = diagnostics;
        }
    }
}
