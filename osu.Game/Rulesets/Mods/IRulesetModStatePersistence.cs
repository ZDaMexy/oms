// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mods
{
    public interface IRulesetModStatePersistence : IDisposable
    {
        void ApplyStoredState(Dictionary<ModType, IReadOnlyList<Mod>> availableMods);

        IReadOnlyList<Mod>? GetStoredSelection(IReadOnlyDictionary<ModType, IReadOnlyList<Mod>> availableMods);

        void BindSelectedMods(IBindable<IReadOnlyList<Mod>> selectedMods);
    }

    public interface IPreserveSettingsWhenDisabled
    {
    }
}
