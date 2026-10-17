// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        internal static GameplaySkinDocument CreateMaterialTemplateDocument(GameplaySkinResolvedMaterialSet materials, string revision)
        {
            var lines = new List<string> { "[GameplaySkin.Common:1]" };
            foreach (GameplaySkinResolvedMaterialEntry entry in materials.Entries)
            {
                var material = (GameplaySkinPublicSlotMaterial)entry.Material!;
                GameplaySkinResolvedMaterialTarget target = entry.Target;
                string coordinates = $"group={target.GroupId?.Value} group-logical={target.GroupLogicalIndex} group-visual={target.GroupVisualIndex}";
                lines.Add(target.Kind switch
                {
                    GameplaySkinResolvedMaterialTargetKind.Global => "Target: Global ruleset=any keymode=any stage-mode=any",
                    GameplaySkinResolvedMaterialTargetKind.Stage => "Target: Stage ruleset=any keymode=any stage-mode=any " + coordinates,
                    GameplaySkinResolvedMaterialTargetKind.Lane => "Target: Lane ruleset=any keymode=any stage-mode=any " + coordinates
                        + $" lane={target.LaneId!.Value} global-logical={target.GlobalLogicalIndex} global-visual={target.GlobalVisualIndex}"
                        + $" group-local-logical={target.GroupLocalLogicalIndex} group-local-visual={target.GroupLocalVisualIndex}",
                    _ => throw new InvalidOperationException("This fixture only authors public material-template scopes."),
                });
                lines.Add($"{entry.Slot.Id}: resource Provide \"{material.ResourceName}\"");
            }

            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(string.Join('\n', lines),
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse(revision)).BindToPublication(materials.Snapshot);
            Assert.That(document.Diagnostics, Is.Empty);
            return document;
        }
    }
}
