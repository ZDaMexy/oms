// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Production stage/deck consumer for one exact group from the immutable gameplay layout snapshot.
    /// </summary>
    public partial class BmsGameplayLayoutGroupContainer : Container
    {
        public GameplaySkinLaneGroupId GroupId { get; }

        public BmsGameplayLayoutSnapshot LayoutSnapshot { get; }

        internal BmsGameplayLayoutGroupContainer(GameplaySkinLaneGroupId groupId, BmsGameplayLayoutSnapshot layoutSnapshot)
        {
            GroupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            LayoutSnapshot = layoutSnapshot ?? throw new ArgumentNullException(nameof(layoutSnapshot));
            LayoutSnapshot.Neutral.GetGroup(GroupId);
        }
    }
}
