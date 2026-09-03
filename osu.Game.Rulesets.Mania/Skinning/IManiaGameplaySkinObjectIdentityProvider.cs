// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Objects;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Read-only access to the sole engine-owned object identity and event producer for a mania gameplay root.
    /// Pooled drawables consume this seam; they never allocate a parallel skin-facing identity.
    /// </summary>
    internal interface IManiaGameplaySkinObjectIdentityProvider
    {
        long GetObjectId(HitObject hitObject, GameplaySkinLaneGroupId? usageGroupId = null);

        void PublishLongNoteState(long objectId, GameplaySkinObjectState state);
    }
}
