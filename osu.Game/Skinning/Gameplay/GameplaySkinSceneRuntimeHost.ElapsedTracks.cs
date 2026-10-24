// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning.Gameplay
{
    internal sealed class GameplaySkinElapsedTrackProperty
    {
        internal double Number { get; init; }
        internal Texture? Texture { get; init; }
        internal GameplaySkinProjectiveQuad? Quad { get; init; }
        internal bool WasApplied { get; set; }
    }

    public partial class GameplaySkinSceneRuntimeHost
    {
        private void initialiseElapsedTrackDefaults(GameplaySkinSceneRuntimeNode node)
        {
            var tracks = PreparedScene.Program.Tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                GameplaySkinPreparedSceneTrack track = tracks[i];
                if (!requiresElapsedClockAvailability(track.ClockSource) || track.TargetNodeId != node.PreparedNode.Source.Id)
                    continue;
                node.ElapsedTrackProperties ??= new Dictionary<GameplaySkinSceneProperty, GameplaySkinElapsedTrackProperty>();
                if (node.ElapsedTrackProperties.ContainsKey(track.Property))
                    continue;
                node.ElapsedTrackProperties.Add(track.Property, new GameplaySkinElapsedTrackProperty
                {
                    Texture = node.ContentDrawable is Sprite sprite ? sprite.Texture : null,
                    Quad = (node.ContentDrawable as GameplaySkinProjectiveSprite)?.AppliedQuad,
                    Number = track.Property switch
                    {
                        GameplaySkinSceneProperty.Brightness => getNumber(node.PreparedNode.Source.Properties, "brightness", 1),
                        GameplaySkinSceneProperty.Opacity => node.TransformDrawable.Alpha,
                        GameplaySkinSceneProperty.X => node.TransformDrawable.X,
                        GameplaySkinSceneProperty.Y => node.TransformDrawable.Y,
                        GameplaySkinSceneProperty.Width => node.TransformDrawable.Width,
                        GameplaySkinSceneProperty.Height => node.TransformDrawable.Height,
                        GameplaySkinSceneProperty.ScaleX => node.TransformDrawable.Scale.X,
                        GameplaySkinSceneProperty.ScaleY => node.TransformDrawable.Scale.Y,
                        GameplaySkinSceneProperty.Rotation => node.TransformDrawable.Rotation,
                        GameplaySkinSceneProperty.Z => node.ContentDrawable.Depth,
                        GameplaySkinSceneProperty.FontSize => ((SpriteText)node.ContentDrawable).Font.Size,
                        GameplaySkinSceneProperty.CornerRadius => ((Container)node.ContentDrawable).CornerRadius,
                        GameplaySkinSceneProperty.RevealX => ((Container)node.ContentDrawable).Width,
                        GameplaySkinSceneProperty.Resource or GameplaySkinSceneProperty.ProjectiveQuad => 0,
                        _ => throw new InvalidOperationException("An elapsed track must animate a prepared numeric, resource or projective property."),
                    },
                });
            }
        }

        private bool canSampleElapsedTrack(GameplaySkinPreparedSceneTrack track, GameplaySkinSceneRuntimeNode node)
        {
            if (!requiresElapsedClockAvailability(track.ClockSource))
                return true;
            GameplaySkinElapsedTrackProperty original = node.ElapsedTrackProperties![track.Property];
            bool available = track.ClockSource switch
            {
                GameplaySkinSceneBindingSource.TimingElapsed => timingState.StartTime.HasValue,
                GameplaySkinSceneBindingSource.PresentationElapsed => timingState.Preparation.Available || timingState.StartTime.HasValue,
                GameplaySkinSceneBindingSource.PreparationReadyElapsed => timingState.Preparation.Ready,
                _ => timingState.Preparation.Available,
            };
            if (available)
            {
                original.WasApplied = true;
                return true;
            }
            if (!original.WasApplied)
                return false;
            original.WasApplied = false;
            if (track.Property == GameplaySkinSceneProperty.Resource)
                ((Sprite)node.ContentDrawable).Texture = original.Texture;
            else if (track.Property == GameplaySkinSceneProperty.ProjectiveQuad)
            {
                var projective = (GameplaySkinProjectiveSprite)node.ContentDrawable;
                if (original.Quad == null)
                    projective.ClearQuad();
                else
                    projective.ApplyQuad(original.Quad);
            }
            else
                applyRuntimeNumberProperty(node, track.Property, original.Number);
            return false;
        }

        private static bool requiresElapsedClockAvailability(GameplaySkinSceneBindingSource source) =>
            source is GameplaySkinSceneBindingSource.TimingElapsed or GameplaySkinSceneBindingSource.PresentationElapsed or GameplaySkinSceneBindingSource.PreparationElapsed
                or GameplaySkinSceneBindingSource.PreparationRemaining or GameplaySkinSceneBindingSource.PreparationReadyElapsed;
    }
}
