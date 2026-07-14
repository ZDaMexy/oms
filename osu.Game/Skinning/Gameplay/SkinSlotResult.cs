// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The result of resolving one gameplay skin slot from one provider.
    /// </summary>
    public enum SkinSlotResultKind
    {
        /// <summary>
        /// This provider does not override the slot. Resolution should continue with the next provider.
        /// </summary>
        Inherit = 0,

        /// <summary>
        /// This provider supplies a value for the slot.
        /// </summary>
        Provide = 1,

        /// <summary>
        /// This provider explicitly disables an optional visual slot.
        /// </summary>
        Suppress = 2,
    }

    /// <summary>
    /// An explicit three-state gameplay skin slot result.
    /// </summary>
    /// <typeparam name="T">The type of value supplied by the slot.</typeparam>
    public readonly struct SkinSlotResult<T>
        where T : notnull
    {
        /// <summary>
        /// The result state. The default value is <see cref="SkinSlotResultKind.Inherit"/>.
        /// </summary>
        public SkinSlotResultKind Kind { get; }

        private readonly T? value;

        /// <summary>
        /// The provided value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="SkinSlotResultKind.Provide"/>.</exception>
        public T Value => Kind == SkinSlotResultKind.Provide
            ? value!
            : throw new InvalidOperationException($"A {Kind} skin slot result has no provided value.");

        /// <summary>
        /// Returns an inherit result.
        /// </summary>
        public static SkinSlotResult<T> Inherit => default;

        /// <summary>
        /// Returns a suppress result. The resolver will only honour this for optional slots.
        /// </summary>
        public static SkinSlotResult<T> Suppress => new SkinSlotResult<T>(SkinSlotResultKind.Suppress, default);

        /// <summary>
        /// Returns a provide result containing <paramref name="value"/>.
        /// Providers should only return this after constructing and validating the value; the resolver's validator is an additional failure-isolation boundary.
        /// </summary>
        public static SkinSlotResult<T> Provide(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new SkinSlotResult<T>(SkinSlotResultKind.Provide, value);
        }

        private SkinSlotResult(SkinSlotResultKind kind, T? value)
        {
            Kind = kind;
            this.value = value;
        }
    }
}
