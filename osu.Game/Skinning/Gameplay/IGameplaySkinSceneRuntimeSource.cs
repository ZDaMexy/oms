// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Narrow ruleset-to-core HUD seam for the exact committed gameplay-skin scene runtime.
    /// </summary>
    internal interface IGameplaySkinSceneRuntimeSource
    {
        GameplaySkinSceneRuntimeHost? GameplaySkinSceneRuntime { get; }
    }
}
