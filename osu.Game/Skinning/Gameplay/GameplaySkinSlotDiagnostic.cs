// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A machine-readable reason why gameplay slot resolution continued past a provider.
    /// </summary>
    public enum GameplaySkinSlotDiagnosticCode
    {
        /// <summary>
        /// The provider threw while looking up or constructing the slot value.
        /// </summary>
        ProviderFailed = 0,

        /// <summary>
        /// The provider returned a value which failed validation.
        /// </summary>
        ProvidedValueRejected = 1,

        /// <summary>
        /// Validation of a provided value threw an exception.
        /// </summary>
        ProvidedValueValidationFailed = 2,

        /// <summary>
        /// A provider attempted to suppress a critical slot.
        /// </summary>
        CriticalSuppressionRejected = 3,

        /// <summary>
        /// A provider returned an unknown result state.
        /// </summary>
        InvalidResult = 4,
    }

    /// <summary>
    /// A structured gameplay skin slot resolution diagnostic.
    /// </summary>
    /// <param name="Code">The stable diagnostic code.</param>
    /// <param name="Slot">The lookup value identifying the affected slot.</param>
    /// <param name="ProviderName">The provider which produced the diagnostic.</param>
    /// <param name="Exception">The associated exception, if any.</param>
    public sealed record GameplaySkinSlotDiagnostic(
        GameplaySkinSlotDiagnosticCode Code,
        object Slot,
        string ProviderName,
        Exception? Exception = null);
}
