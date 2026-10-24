// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.Visual.Skinning
{
    /// <summary>
    /// Evidence from the same live author nodes in the BMS and mania Player capture fixtures.
    /// This records visual state; independent frame/geometry review supplies the acceptance criteria.
    /// </summary>
    internal static class GameplaySkinOptionPresentationCapture
    {
        internal static object Capture(GameplaySkinSceneRuntimeHost runtime, GameplaySkinSceneRuntimeNode[] nodes)
        {
            string[] optionIds = ["judgement-position", "judgement-display", "pacemaker-percent", "graph-target"];
            var program = runtime.PreparedScene.Program;
            var tracks = program.Tracks.Where(track => track.ClockSource == GameplaySkinSceneBindingSource.Option
                                                       && optionIds.Contains(track.ClockOptionId)).ToArray();
            var bindings = program.Bindings.Where(binding => binding.Source == GameplaySkinSceneBindingSource.Option
                                                             && optionIds.Contains(binding.OptionId)).ToArray();
            var variants = program.Variants.Where(variant => variant.Source == GameplaySkinSceneBindingSource.Option
                                                             && optionIds.Contains(variant.OptionId)).ToArray();
            HashSet<string> targets = tracks.Select(track => track.TargetNodeId)
                                            .Concat(bindings.Select(binding => binding.TargetNodeId))
                                            .Concat(variants.Select(variant => variant.TargetNodeId)).ToHashSet(StringComparer.Ordinal);
            var roots = nodes.Where(node => targets.Contains(node.PreparedNode.Source.Id)).ToArray();
            return new
            {
                options = runtime.Publication.Snapshot.Context.OptionValues.Values
                                 .Where(pair => optionIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value.CanonicalValue),
                tracks = tracks.Select(track => new
                {
                    track.Id,
                    track.TargetNodeId,
                    track.ClockOptionId,
                    property = track.Property.ToString(),
                    track.Loop,
                    keyframes = track.Keyframes.Select(key => new { key.Time, key.Value.NumberValue }).ToArray(),
                }).ToArray(),
                nodes = nodes.Where(node => roots.Any(root => isWithin(node.RootDrawable, root.RootDrawable))).Select(node => new
                {
                    node = node.PreparedNode.Source.Id,
                    node.InstanceId,
                    slot = node.PreparedNode.OwningSlotKey?.Slot.Id,
                    target = node.PreparedNode.OwningSlotKey?.Target.ToString(),
                    text = (node.ContentDrawable as SpriteText)?.Text.ToString(),
                    root = geometry(node.RootDrawable),
                    transform = geometry(node.TransformDrawable),
                    content = geometry(node.ContentDrawable),
                    ancestors = ancestors(node.RootDrawable).Select(geometry).ToArray(),
                }).ToArray(),
            };
        }

        private static bool isWithin(Drawable drawable, Drawable root)
        {
            for (Drawable? current = drawable; current != null; current = current.Parent)
                if (ReferenceEquals(current, root))
                    return true;
            return false;
        }

        private static IEnumerable<Drawable> ancestors(Drawable drawable)
        {
            for (Drawable? parent = drawable.Parent; parent != null; parent = parent.Parent)
                yield return parent;
        }

        private static object geometry(Drawable drawable)
        {
            var bounds = drawable.ScreenSpaceDrawQuad.AABBFloat;
            return new
            {
                type = drawable.GetType().Name,
                drawable.Alpha,
                drawable.IsPresent,
                drawable.X,
                drawable.Y,
                drawable.Width,
                drawable.Height,
                relativePositionAxes = drawable.RelativePositionAxes.ToString(),
                scale = new { drawable.Scale.X, drawable.Scale.Y },
                bounds = new { bounds.Left, bounds.Top, bounds.Width, bounds.Height },
            };
        }
    }
}
