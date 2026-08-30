// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// One BMS-owned native context paired with its engine-issued neutral lane-topology publication.
    /// </summary>
    /// <remarks>
    /// The keymode is the topology continuity authority. The applied style is current presentation metadata and may change
    /// between publications. This internal process-local wrapper is not a complete gameplay layout context or wire ABI.
    /// </remarks>
    internal sealed class BmsGameplaySkinLaneTopologyPublication
    {
        public BmsKeymode Keymode { get; }

        public BmsPlayfieldStyle AppliedStyle { get; }

        public GameplaySkinLaneTopologyPublication Publication { get; }

        internal BmsGameplaySkinLaneTopologyPublication(
            BmsGameplaySkinLaneTopologyProjection projection,
            GameplaySkinLaneTopologyPublication publication)
        {
            ArgumentNullException.ThrowIfNull(projection);
            ArgumentNullException.ThrowIfNull(publication);

            if (!ReferenceEquals(projection.Topology, publication.Topology))
                throw new ArgumentException("The BMS native context must wrap the exact topology issued by the shared revision owner.", nameof(publication));

            Keymode = projection.Keymode;
            AppliedStyle = projection.AppliedStyle;
            Publication = publication;
        }

        public override string ToString() => $"Bms:{Keymode}:{AppliedStyle}:Revision{Publication.Revision}";
    }

    /// <summary>
    /// Issues consecutive BMS lane-topology publications for one gameplay attachment.
    /// </summary>
    /// <remarks>
    /// A keymode change starts a different native topology context and is rejected by this owner. Presentation-style changes
    /// remain valid only when the existing neutral topology transition contract also accepts them. Projection, native-context,
    /// neutral-transition and revision-overflow rejection never replaces <see cref="Current"/> or consumes a revision. This owner
    /// does not connect production layout or rendering.
    /// </remarks>
    internal sealed class BmsGameplaySkinLaneTopologyRevisionOwner
    {
        private readonly GameplaySkinLaneTopologyRevisionOwner<BmsKeymode> revisionOwner =
            new GameplaySkinLaneTopologyRevisionOwner<BmsKeymode>((previous, current) => previous == current);

        public BmsGameplaySkinLaneTopologyPublication? Current { get; private set; }

        public BmsGameplaySkinLaneTopologyPublication Publish(BmsKeymode keymode, BmsPlayfieldStyle style)
            => publish(BmsGameplaySkinLaneTopologyFactory.Create(keymode, style));

        public BmsGameplaySkinLaneTopologyPublication Publish(BmsLaneLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            return publish(BmsGameplaySkinLaneTopologyFactory.Create(layout));
        }

        private BmsGameplaySkinLaneTopologyPublication publish(BmsGameplaySkinLaneTopologyProjection projection)
        {
            GameplaySkinLaneTopologyPublication publication = revisionOwner.Publish(projection.Keymode, projection.Topology);
            var result = new BmsGameplaySkinLaneTopologyPublication(projection, publication);

            Current = result;
            return result;
        }
    }
}
