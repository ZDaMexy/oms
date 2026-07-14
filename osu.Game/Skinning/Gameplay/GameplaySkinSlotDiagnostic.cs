// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

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
    /// <param name="Slot">The process-local lookup value identifying the affected slot. It is excluded from serialisation.</param>
    /// <param name="ProviderName">The provider which produced the diagnostic.</param>
    /// <param name="Exception">The associated process-local exception, if any. It is excluded from serialisation.</param>
    public sealed record GameplaySkinSlotDiagnostic(
        GameplaySkinSlotDiagnosticCode Code,
        [property: JsonIgnore] object Slot,
        string ProviderName,
        [property: JsonIgnore] Exception? Exception = null)
    {
        /// <summary>
        /// The stable semantic slot ID, when resolution used a catalog descriptor.
        /// </summary>
        public string? SlotId { get; init; }

        /// <summary>
        /// Returns a persistence-safe summary without process-local lookup or exception content.
        /// </summary>
        public override string ToString() => SlotId == null ? Code.ToString() : $"{Code}: {SlotId}";
    }
}
