// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JetBrains.Annotations;
using osu.Framework.Bindables;
using osu.Framework.IO.Stores;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// An OMS legacy skin that, on top of the core <see cref="LegacySkin"/> behaviour (general + mania sections),
    /// also parses the BMS-specific (<c>[General]</c> / <c>[Bms]</c>) sections of skin.ini and answers
    /// <see cref="BmsSkinConfigurationLookup"/> queries.
    /// </summary>
    /// <remarks>
    /// Lives in the ruleset assembly so it can reference <see cref="BmsSkinDecoder"/> (osu.Game must never depend on a
    /// ruleset). It extends core skin parsing purely via the <see cref="ParseConfigurationStream"/> hook — mania-section
    /// parsing (which lives in core <see cref="LegacySkin"/>) is preserved untouched, BMS parsing is layered on top.
    /// Intended to be reached by reflection (skin <c>InstantiationInfo</c>) so the core importer needs no compile-time
    /// ruleset reference.
    /// </remarks>
    public class BmsLegacySkin : LegacySkin
    {
        private readonly Dictionary<BmsKeymode, BmsSkinConfiguration> bmsConfigurations = new Dictionary<BmsKeymode, BmsSkinConfiguration>();

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public BmsLegacySkin(SkinInfo skin, IStorageResourceProvider resources)
            : base(skin, resources)
        {
        }

        /// <summary>
        /// Folder-backed construction (G1 visible-folder skins): skin.ini + textures are read directly from
        /// <paramref name="folderStore"/> — a store over a visible skin folder such as <c>chartskin/&lt;name&gt;</c> —
        /// instead of the realm hash-backed file store. Intended to be reached by reflection from the core skin
        /// instantiation path for skins whose <see cref="SkinInfo"/> carries a filesystem storage path; the empty realm
        /// <c>Files</c> list falls through to this store (the same fallback-store pattern <see cref="OmsSkin"/> uses).
        /// </summary>
        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public BmsLegacySkin(SkinInfo skin, IStorageResourceProvider resources, IResourceStore<byte[]> folderStore)
            : this(skin, resources, folderStore, @"skin.ini")
        {
        }

        protected BmsLegacySkin(SkinInfo skin, IStorageResourceProvider? resources, IResourceStore<byte[]>? fallbackStore, string configurationFilename = @"skin.ini")
            : base(skin, resources, fallbackStore, configurationFilename)
        {
        }

        protected override void ParseConfigurationStream(Stream stream)
        {
            // Snapshot the stream first: the base (mania) parse consumes its own reader, so BMS parsing reads a copy.
            // Field initialisers run before the base constructor, so bmsConfigurations is already set when the base
            // constructor calls this virtual during construction (same pattern as core mania parsing).
            using var copy = new MemoryStream();
            stream.Position = 0;
            stream.CopyTo(copy);

            // Reset the original stream so the base parser (LegacySkinDecoder for [General]/[Colours])
            // can read from the start — CopyTo leaves the stream positioned at the end.
            stream.Position = 0;
            base.ParseConfigurationStream(stream);

            copy.Position = 0;
            using var reader = new StreamReader(copy);
            var decoder = new BmsSkinDecoder();
            decoder.Parse(reader);

            foreach (var config in decoder.Configurations)
                bmsConfigurations[config.Keymode] = config;
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is BmsSkinConfigurationLookup bmsLookup)
                return lookupBms<TValue>(bmsLookup);

            return base.GetConfig<TLookup, TValue>(lookup);
        }

        private IBindable<TValue>? lookupBms<TValue>(BmsSkinConfigurationLookup lookup)
        {
            if (!bmsConfigurations.TryGetValue(lookup.Keymode, out var config))
                return null;

            // The typeof(TValue) guards keep SkinUtils.As (a hard cast) safe even if a caller queries the wrong type.
            if (typeof(TValue) == typeof(float) && config.Geometry.TryGetValue(lookup.Lookup, out float number))
                return SkinUtils.As<TValue>(new Bindable<float>(number));

            if (typeof(TValue) == typeof(Color4) && config.Colours.TryGetValue(lookup.Lookup, out var colour))
                return SkinUtils.As<TValue>(new Bindable<Color4>(colour));

            if (typeof(TValue) == typeof(string))
            {
                string? imageKey = resolveImageKey(lookup);

                if (imageKey != null && config.ImageLookups.TryGetValue(imageKey, out string? path))
                    return SkinUtils.As<TValue>(new Bindable<string>(path));
            }

            return null;
        }

        /// <summary>
        /// Maps a logical texture lookup to the full ini key the decoder stored it under. Per-lane keys embed the lane
        /// token (a digit, or <c>S</c> for scratch); global slots use the enum name verbatim.
        /// </summary>
        private static string? resolveImageKey(BmsSkinConfigurationLookup lookup)
        {
            // For 14K DP, the P2 scratch uses token "S2" (lane index >= 8); P1 scratch uses "S".
            bool isSecondScratch = lookup.IsScratch && lookup.Keymode == BmsKeymode.Key14K
                && lookup.LaneIndex.HasValue && lookup.LaneIndex.Value >= 8;
            string laneToken = lookup.IsScratch
                ? (isSecondScratch ? "S2" : "S")
                : lookup.LaneIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            bool hasLane = laneToken.Length > 0;

            switch (lookup.Lookup)
            {
                case BmsSkinConfigurationLookups.NoteImage:
                    return hasLane ? $"NoteImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.HoldNoteHeadImage:
                    return hasLane ? $"NoteImage{laneToken}H" : null;

                case BmsSkinConfigurationLookups.HoldNoteBodyImage:
                    return hasLane ? $"NoteImage{laneToken}L" : null;

                case BmsSkinConfigurationLookups.HoldNoteTailImage:
                    return hasLane ? $"NoteImage{laneToken}T" : null;

                case BmsSkinConfigurationLookups.KeyImage:
                    return hasLane ? $"KeyImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.KeyImageDown:
                    return hasLane ? $"KeyImage{laneToken}D" : null;

                case BmsSkinConfigurationLookups.LaneBackgroundImage:
                    return hasLane ? $"LaneBackgroundImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.LaneDividerImage:
                    return hasLane ? $"LaneDividerImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.KeyFlashImage:
                    return hasLane ? $"KeyFlashImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.HitLightingImage:
                    return hasLane ? $"HitLightingImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.HoldLightImage:
                    return hasLane ? $"HoldLightImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.MineHitImage:
                    return hasLane ? $"MineHitImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.HitTargetImage:
                case BmsSkinConfigurationLookups.StageLeftImage:
                case BmsSkinConfigurationLookups.StageRightImage:
                case BmsSkinConfigurationLookups.StageBottomImage:
                case BmsSkinConfigurationLookups.StageHintImage:
                case BmsSkinConfigurationLookups.PlayfieldBackdropImage:
                case BmsSkinConfigurationLookups.LaneCoverTopImage:
                case BmsSkinConfigurationLookups.LaneCoverBottomImage:
                    return lookup.Lookup.ToString();

                default:
                    return null;
            }
        }
    }
}
