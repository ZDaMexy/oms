// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Immutable prepared animation frames. Texture/resource lookup is complete before this material is published.
    /// </summary>
    internal sealed class ManiaGameplaySkinAnimationMaterial
    {
        public IReadOnlyList<Texture> Frames { get; }

        public double FrameLength { get; }

        public bool Loop { get; }

        public ManiaGameplaySkinAnimationMaterial(IEnumerable<Texture> frames, double frameLength, bool loop = true)
        {
            ArgumentNullException.ThrowIfNull(frames);
            Texture[] copied = frames.ToArray();

            if (copied.Length == 0 || copied.Any(texture => texture == null))
                throw new ArgumentException("A prepared mania animation requires at least one valid texture.", nameof(frames));

            if (!double.IsFinite(frameLength) || frameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameLength));

            Frames = Array.AsReadOnly(copied);
            FrameLength = frameLength;
            Loop = loop;
        }

        public Drawable CreateDrawable()
        {
            if (Frames.Count == 1)
                return new Sprite { Texture = Frames[0] };

            var animation = new LegacySkinExtensions.SkinnableTextureAnimation
            {
                DefaultFrameLength = FrameLength,
                Loop = Loop,
            };

            foreach (Texture frame in Frames)
                animation.AddFrame(frame);

            return animation;
        }
    }

    internal interface IManiaGameplaySkinMaterial
    {
        Drawable CreateDrawable();
    }

    internal sealed class ManiaGameplaySkinNoteMaterial : IManiaGameplaySkinMaterial
    {
        public ManiaSkinComponents Component { get; }

        public ManiaGameplaySkinAnimationMaterial Animation { get; }

        public float? WidthForNoteHeightScale { get; }

        public ManiaGameplaySkinNoteMaterial(
            ManiaSkinComponents component,
            ManiaGameplaySkinAnimationMaterial animation,
            float? widthForNoteHeightScale)
        {
            if (component is not ManiaSkinComponents.Note
                and not ManiaSkinComponents.HoldNoteHead
                and not ManiaSkinComponents.HoldNoteTail)
            {
                throw new ArgumentOutOfRangeException(nameof(component));
            }

            Component = component;
            Animation = animation ?? throw new ArgumentNullException(nameof(animation));

            if (widthForNoteHeightScale.HasValue
                && (!float.IsFinite(widthForNoteHeightScale.Value) || widthForNoteHeightScale.Value <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(widthForNoteHeightScale));
            }

            WidthForNoteHeightScale = widthForNoteHeightScale;
        }

        public Drawable CreateDrawable() => Component switch
        {
            ManiaSkinComponents.Note => new LegacyNotePiece(this),
            ManiaSkinComponents.HoldNoteHead => new LegacyHoldNoteHeadPiece(this),
            ManiaSkinComponents.HoldNoteTail => new LegacyHoldNoteTailPiece(this),
            _ => throw new InvalidOperationException("The prepared note material has an unknown component."),
        };
    }

    internal sealed class ManiaGameplaySkinBodyMaterial : IManiaGameplaySkinMaterial
    {
        public ManiaGameplaySkinAnimationMaterial Body { get; }

        public ManiaGameplaySkinAnimationMaterial? Light { get; }

        public float LightScale { get; }

        public LegacyNoteBodyStyle? BodyStyle { get; }

        public ManiaGameplaySkinBodyMaterial(
            ManiaGameplaySkinAnimationMaterial body,
            ManiaGameplaySkinAnimationMaterial? light,
            float lightScale,
            LegacyNoteBodyStyle? bodyStyle)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Light = light;

            if (!float.IsFinite(lightScale) || lightScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(lightScale));

            LightScale = lightScale;
            BodyStyle = bodyStyle;
        }

        public Drawable CreateDrawable() => new LegacyBodyPiece(this);
    }

    internal sealed class ManiaGameplaySkinKeyMaterial : IManiaGameplaySkinMaterial
    {
        public Texture UpTexture { get; }

        public Texture DownTexture { get; }

        public bool KeysUnderNotes { get; }

        public ManiaGameplaySkinKeyMaterial(Texture upTexture, Texture downTexture, bool keysUnderNotes)
        {
            UpTexture = upTexture ?? throw new ArgumentNullException(nameof(upTexture));
            DownTexture = downTexture ?? throw new ArgumentNullException(nameof(downTexture));
            KeysUnderNotes = keysUnderNotes;
        }

        public Drawable CreateDrawable() => new LegacyKeyArea(this);
    }

    internal sealed class ManiaGameplaySkinProgrammaticMaterial : IManiaGameplaySkinMaterial
    {
        private readonly ManiaSkinComponents component;

        public ManiaGameplaySkinProgrammaticMaterial(ManiaSkinComponents component)
        {
            if (!ManiaGameplaySkinMaterialMapping.TryGetDescriptor(component, out _))
                throw new ArgumentOutOfRangeException(nameof(component));

            this.component = component;
        }

        public Drawable CreateDrawable() => component switch
        {
            ManiaSkinComponents.Note => new DefaultNotePiece(),
            ManiaSkinComponents.HoldNoteHead => new DefaultNotePiece(),
            ManiaSkinComponents.HoldNoteTail => new DefaultNotePiece(),
            ManiaSkinComponents.HoldNoteBody => new DefaultBodyPiece { RelativeSizeAxes = Axes.Both },
            ManiaSkinComponents.KeyArea => new DefaultKeyArea(),
            _ => throw new InvalidOperationException("The prepared mania component has no programmatic fallback."),
        };
    }

    /// <summary>
    /// Explicit marker used when an optional resolved slot is suppressed. It carries state through the lookup without
    /// overloading <see cref="Drawable.Empty"/> or a missing drawable as the suppression signal.
    /// </summary>
    internal partial class ManiaSuppressedSkinComponentMarker : Drawable
    {
    }

    /// <summary>
    /// Explicit no-visual marker for the legacy hit-target z-order contract. This is not a C4 Suppress result.
    /// </summary>
    internal partial class ManiaLegacyHitTargetOrderingMarker : Drawable
    {
    }

    internal static class ManiaGameplaySkinMaterialMapping
    {
        public static bool TryGetDescriptor(ManiaSkinComponents component, out GameplaySkinSlotDescriptor? descriptor)
        {
            descriptor = component switch
            {
                ManiaSkinComponents.Note => GameplaySkinSlotCatalog.Note,
                ManiaSkinComponents.HoldNoteHead => GameplaySkinSlotCatalog.LongNoteHead,
                ManiaSkinComponents.HoldNoteTail => GameplaySkinSlotCatalog.LongNoteTail,
                ManiaSkinComponents.HoldNoteBody => GameplaySkinSlotCatalog.LongNoteBody,
                ManiaSkinComponents.KeyArea => GameplaySkinSlotCatalog.KeyVisual,
                _ => null,
            };

            return descriptor != null;
        }
    }

    internal sealed class ManiaGameplaySkinMaterialContext
    {
        public GameplaySkinResolvedMaterialSet MaterialSet { get; }

        public GameplaySkinResolvedMaterialTarget Target { get; }

        public bool UsesResolvedMaterial => !MaterialSet.ContractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty);

        public ManiaGameplaySkinMaterialContext(
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinResolvedMaterialTarget target)
        {
            MaterialSet = materialSet ?? throw new ArgumentNullException(nameof(materialSet));
            Target = target ?? throw new ArgumentNullException(nameof(target));

            // The material-set constructor has already checked the copied stable IDs and every explicit index against
            // this exact snapshot. Exact C4 contexts are total: every migrated renderer slot must be present before a
            // drawable can attach, so no consumer can fall through to a post-commit resource lookup.
            if (UsesResolvedMaterial)
            {
                foreach (ManiaSkinComponents component in new[]
                         {
                             ManiaSkinComponents.Note,
                             ManiaSkinComponents.HoldNoteHead,
                             ManiaSkinComponents.HoldNoteBody,
                             ManiaSkinComponents.HoldNoteTail,
                             ManiaSkinComponents.KeyArea,
                         })
                {
                    if (!MaterialSet.TryGet(GetKey(component), out _))
                        throw new ArgumentException("The exact mania material context is incomplete for its stable lane target.", nameof(materialSet));
                }
            }
        }

        public GameplaySkinResolvedMaterialKey GetKey(ManiaSkinComponents component)
        {
            if (!ManiaGameplaySkinMaterialMapping.TryGetDescriptor(component, out GameplaySkinSlotDescriptor? descriptor))
                throw new ArgumentOutOfRangeException(nameof(component), component, "The mania component is not part of the C4 material surface.");

            return new GameplaySkinResolvedMaterialKey(descriptor!, Target);
        }
    }

    internal static class ManiaGameplaySkinResolvedDrawableFactory
    {
        public static bool TryCreate(ManiaSkinComponentLookup lookup, out Drawable? drawable)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            if (lookup.ResolvedMaterialSet == null || lookup.ResolvedMaterialKey == null)
            {
                drawable = null;
                return false;
            }

            if (!lookup.ResolvedMaterialSet.TryGet(lookup.ResolvedMaterialKey, out GameplaySkinResolvedMaterialEntry? entry))
                throw new InvalidOperationException("The exact mania material publication does not contain its requested component key.");

            if (entry.State == GameplaySkinResolvedMaterialState.Suppress)
            {
                drawable = new ManiaSuppressedSkinComponentMarker();
                return true;
            }

            IManiaGameplaySkinMaterial material = entry.GetMaterial<IManiaGameplaySkinMaterial>();
            drawable = material.CreateDrawable();
            return true;
        }
    }
}
