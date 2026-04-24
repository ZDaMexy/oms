// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Bms.Mods
{
    internal interface IBmsGameplayAdjustmentMod
    {
        BindableBool RememberGameplayChanges { get; }
    }
}
