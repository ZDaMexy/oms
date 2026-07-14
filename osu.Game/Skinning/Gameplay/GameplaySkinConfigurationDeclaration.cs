// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Preserves whether a gameplay skin configuration value was explicitly declared by its source.
    /// </summary>
    /// <remarks>
    /// This is declaration provenance only. It does not validate, clone or freeze <typeparamref name="T"/>,
    /// and it is not a gameplay slot <c>Provide</c>, <c>Inherit</c> or <c>Suppress</c> decision.
    /// The default value is <see cref="Absent"/> so missing declarations cannot be confused with an
    /// explicitly declared default value such as <see langword="false"/>, zero or an empty string.
    /// This process-local carrier is not a serialisation or author-facing manifest ABI.
    /// </remarks>
    public readonly struct GameplaySkinConfigurationDeclaration<T>
        where T : notnull
    {
        private readonly T? value;

        /// <summary>
        /// Whether the source explicitly declared the value.
        /// </summary>
        public bool IsDeclared { get; }

        /// <summary>
        /// The explicitly declared value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the declaration is absent.</exception>
        public T Value => IsDeclared
            ? value!
            : throw new InvalidOperationException("An absent gameplay skin configuration declaration has no value.");

        /// <summary>
        /// Returns an absent declaration.
        /// </summary>
        public static GameplaySkinConfigurationDeclaration<T> Absent => default;

        /// <summary>
        /// Returns an explicit declaration containing <paramref name="value"/>.
        /// </summary>
        public static GameplaySkinConfigurationDeclaration<T> Declared(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new GameplaySkinConfigurationDeclaration<T>(value);
        }

        private GameplaySkinConfigurationDeclaration(T value)
        {
            this.value = value;
            IsDeclared = true;
        }

        /// <summary>
        /// Attempts to retrieve the explicitly declared value without collapsing absence into <typeparamref name="T"/>'s default value.
        /// </summary>
        public bool TryGetValue([MaybeNullWhen(false)] out T declaredValue)
        {
            declaredValue = value;
            return IsDeclared;
        }

        /// <summary>
        /// Returns declaration state only and never includes the value or a source path.
        /// </summary>
        public override string ToString() => IsDeclared ? "Declared" : "Absent";
    }
}
