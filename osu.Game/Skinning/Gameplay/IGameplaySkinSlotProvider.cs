// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A provider participating in ordered gameplay skin slot resolution.
    /// </summary>
    /// <typeparam name="TSlot">The lookup type identifying a gameplay slot and its context.</typeparam>
    /// <typeparam name="TComponent">The value type supplied by the slot.</typeparam>
    public interface IGameplaySkinSlotProvider<in TSlot, TComponent>
        where TSlot : notnull
        where TComponent : notnull
    {
        /// <summary>
        /// A stable name used in fallback diagnostics.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Resolves <paramref name="slot"/> from this provider.
        /// </summary>
        /// <remarks>
        /// A provider must complete construction and its own basic validation before returning <see cref="SkinSlotResultKind.Provide"/>.
        /// The resolver never disposes returned values: lifetime and drawable parenting remain an explicit contract between the provider and the eventual consumer.
        /// Providers must therefore retain and later reclaim any candidate rejected by an additional resolver validator.
        /// </remarks>
        SkinSlotResult<TComponent> GetSlot(TSlot slot);
    }
}
