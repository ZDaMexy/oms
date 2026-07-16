// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.SongSelect;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    public class BmsSkinTransformer : SkinTransformer
    {
        private readonly bool providesBuiltInFallbacks;
        private readonly BmsManagedPackageNoteProvider? managedPackageNoteProvider;
        private readonly ConcurrentDictionary<string, byte> emittedGameplaySkinDiagnostics = new ConcurrentDictionary<string, byte>();

        public BmsSkinTransformer(ISkin skin)
            : base(skin)
        {
            providesBuiltInFallbacks = skin is OmsSkin;

            if (skin is BmsLegacySkin bmsLegacySkin)
                managedPackageNoteProvider = new BmsManagedPackageNoteProvider(bmsLegacySkin);
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            Drawable? skinnedComponent = base.GetDrawableComponent(lookup);

            switch (lookup)
            {
                case SkinComponentLookup<HitResult> resultComponent when BmsHitResultDisplayNames.TryGetCustomDisplayName(resultComponent.Component, out _):
                    return GetDrawableComponent(new BmsJudgementSkinLookup(resultComponent.Component))
                           ?? new BmsJudgementPiece(resultComponent.Component);

                case BmsJudgementSkinLookup judgementLookup:
                    if (skinnedComponent is IAnimatableJudgement)
                        return skinnedComponent;

                    if (!BmsHitResultDisplayNames.TryGetCustomDisplayName(judgementLookup.Result, out _))
                    {
                        Drawable? wrappedJudgement = Skin.GetDrawableComponent(new SkinComponentLookup<HitResult>(judgementLookup.Result));

                        if (wrappedJudgement != null)
                            return wrappedJudgement;
                    }

                    return providesBuiltInFallbacks ? new BmsJudgementPiece(judgementLookup.Result) : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout }:
                    return skinnedComponent is IBmsHudLayoutDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsHudLayoutDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeBar }:
                    return skinnedComponent ?? createBuiltInFallback(() => new BmsGaugeBar());

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter }:
                    return skinnedComponent is ComboCounter ? skinnedComponent : providesBuiltInFallbacks ? new BmsComboCounter() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ClearLamp }:
                    return skinnedComponent is IBmsClearLampDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsClearLampDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeHistoryPanel }:
                    return skinnedComponent is IBmsGaugeHistoryPanelDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsGaugeHistoryPanelDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeHistory }:
                    return skinnedComponent is IBmsGaugeHistoryDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsGaugeHistoryDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ResultsSummaryPanel }:
                    return skinnedComponent is IBmsResultsSummaryPanelDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsResultsSummaryPanelDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ResultsSummary }:
                    return skinnedComponent is IBmsResultsSummaryDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsResultsSummaryDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.NoteDistributionPanel }:
                    return skinnedComponent is IBmsNoteDistributionPanelDisplay
                        ? skinnedComponent
                        : providesBuiltInFallbacks ? new DefaultBmsNoteDistributionPanelDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.NoteDistribution }:
                    return skinnedComponent is IBmsNoteDistributionDisplay
                        ? skinnedComponent
                        : providesBuiltInFallbacks ? new DefaultBmsNoteDistributionDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.StaticBackgroundLayer }:
                    return skinnedComponent is IBmsBackgroundLayerDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsBackgroundLayerDisplay() : null;

                case BmsSkinComponentLookup { Component: BmsSkinComponents.BgaPanel }:
                    return skinnedComponent is IBmsBgaPanelDisplay ? skinnedComponent : providesBuiltInFallbacks ? new DefaultBmsBgaPanelDisplay() : null;

                case BmsPlayfieldSkinLookup playfieldLookup:
                    return skinnedComponent ?? createBuiltInFallback(() => createDefaultPlayfieldComponent(playfieldLookup));

                case BmsLaneSkinLookup laneLookup:
                    return skinnedComponent ?? createBuiltInFallback(() => createDefaultLaneComponent(laneLookup));

                case BmsNoteSkinLookup { Element: BmsNoteSkinElements.Note or BmsNoteSkinElements.LongNoteHead or BmsNoteSkinElements.LongNoteTail } noteLookup:
                    if (skinnedComponent != null)
                        return skinnedComponent;

                    if (managedPackageNoteProvider != null)
                    {
                        if (managedPackageNoteProvider.ClaimsDeclaration(noteLookup))
                        {
                            GameplaySkinSlotResolution<BmsSourceBoundNoteMaterial> resolution = managedPackageNoteProvider.Resolve(noteLookup);
                            emitDiagnosticsOnce(resolution.Diagnostics, noteLookup);

                            if (resolution.Result.Kind == SkinSlotResultKind.Provide)
                                return resolution.Result.Value.CreateDrawable();

                            // This exact source declared the native slot and has now failed the strict package gate.
                            // Do not retry through its mutable legacy config view or a lower same-named texture.
                            return null;
                        }
                    }

                    BmsSkinConfigurationLookups exactSourceImageLookup = noteLookup.Element switch
                    {
                        BmsNoteSkinElements.Note => BmsSkinConfigurationLookups.NoteImage,
                        BmsNoteSkinElements.LongNoteHead => BmsSkinConfigurationLookups.HoldNoteHeadImage,
                        BmsNoteSkinElements.LongNoteTail => BmsSkinConfigurationLookups.HoldNoteTailImage,
                        _ => throw new System.ArgumentOutOfRangeException(nameof(noteLookup), noteLookup.Element, "Unsupported exact-source BMS note element."),
                    };
                    string? exactSourceImage = Skin.GetBmsSkinConfig<string>(
                        exactSourceImageLookup,
                        noteLookup.Keymode,
                        noteLookup.LaneIndex,
                        noteLookup.IsScratch)?.Value;

                    if (!string.IsNullOrEmpty(exactSourceImage))
                    {
                        var texture = Skin.GetTexture(exactSourceImage);

                        if (texture != null)
                        {
                            return new Sprite
                            {
                                RelativeSizeAxes = Axes.Both,
                                Texture = texture,
                            };
                        }
                    }

                    return createBuiltInFallback(() => createDefaultNoteComponent(noteLookup, allowAggregateTextureOverride: false));

                case BmsNoteSkinLookup noteLookup:
                    return skinnedComponent ?? createBuiltInFallback(() => createDefaultNoteComponent(noteLookup, allowAggregateTextureOverride: true));

                case BmsLaneCoverSkinLookup laneCoverLookup:
                    return skinnedComponent is IBmsLaneCoverDisplay ? skinnedComponent : createBuiltInFallback(() => new DefaultBmsLaneCoverDisplay(laneCoverLookup.Position));

                case GlobalSkinnableContainerLookup containerLookup when containerLookup.Lookup == GlobalSkinnableContainers.MainHUDComponents && containerLookup.Ruleset?.ShortName == BmsRuleset.SHORT_NAME:
                    if (!hasBmsHudLayer(skinnedComponent))
                        return null;

                    // Remove the upstream default combo counter and gameplay leaderboard from the wrapped HUD so they are
                    // not part of the BMS default-skin configuration at all (BMS shows its own combo; OMS is offline-first).
                    stripDefaultHudElements(skinnedComponent);

                    Drawable gaugeBar = GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)) ?? new BmsGaugeBar();
                    ComboCounter comboCounter = (ComboCounter)(GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) ?? new BmsComboCounter());
                    Drawable hudLayout = GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)) ?? new DefaultBmsHudLayoutDisplay();

                    if (hudLayout is IBmsHudLayoutDisplay hudLayoutDisplay)
                        hudLayoutDisplay.SetComponents(skinnedComponent, gaugeBar, comboCounter);

                    return hudLayout;
            }

            return skinnedComponent;
        }

        // Strips the wrapped global HUD's default combo counter (duplicates the BMS combo) and gameplay leaderboard
        // (offline-first) so neither is ever part of the BMS HUD configuration. NOTE the legacy default combo is
        // `LegacyDefaultComboCounter` (a CompositeDrawable, NOT a ComboCounter) — both are matched. The BMS combo is added
        // separately and is not inside the wrapped HUD, so removing every combo here is safe. Graceful for skins without them.
        private static void stripDefaultHudElements(Drawable? wrappedHud)
        {
            if (wrappedHud is not Container container)
                return;

            foreach (var drawable in container.Children.Where(child => child is ComboCounter or LegacyDefaultComboCounter or DrawableGameplayLeaderboard).ToArray())
                container.Remove(drawable, true);
        }

        private bool hasBmsHudLayer(Drawable? wrappedHud)
            => wrappedHud != null
               || providesBuiltInFallbacks
               || Skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)) is IBmsHudLayoutDisplay
               || Skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)) != null
               || Skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) is ComboCounter;

        private Drawable? createBuiltInFallback(System.Func<Drawable> createDrawable)
            => providesBuiltInFallbacks ? createDrawable() : null;

        private void emitDiagnosticsOnce(IReadOnlyList<GameplaySkinSlotDiagnostic> diagnostics, BmsNoteSkinLookup lookup)
        {
            foreach (GameplaySkinSlotDiagnostic diagnostic in diagnostics)
            {
                string context = $"{lookup.Keymode}:{lookup.LaneIndex}:{lookup.IsScratch}";
                string key = $"{diagnostic.Code}:{diagnostic.SlotId}:{diagnostic.ProviderName}:{context}";

                if (!emittedGameplaySkinDiagnostics.TryAdd(key, 0))
                    continue;

                Logger.Log(
                    $"Gameplay skin component fallback: code={diagnostic.Code}; slot={diagnostic.SlotId ?? "unknown"}; provider={diagnostic.ProviderName}; context={context}.",
                    LoggingTarget.Runtime,
                    LogLevel.Verbose);
            }
        }

        private static Drawable createDefaultPlayfieldComponent(BmsPlayfieldSkinLookup lookup)
            => lookup.Element switch
            {
                BmsPlayfieldSkinElements.Backdrop => new DefaultBmsPlayfieldBackdropDisplay(lookup.Keymode),
                BmsPlayfieldSkinElements.Baseplate => new DefaultBmsPlayfieldBaseplateDisplay(lookup.Keymode),
                _ => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

        private static Drawable createDefaultLaneComponent(BmsLaneSkinLookup lookup)
            => lookup.Element switch
            {
                BmsLaneSkinElements.Background => new DefaultBmsLaneBackgroundDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                BmsLaneSkinElements.Divider => new DefaultBmsLaneDividerDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                BmsLaneSkinElements.HitTarget => new DefaultBmsHitTargetDisplay(lookup.IsScratch, lookup.Keymode, BmsPlayfieldLayoutProfile.CreateDefault(lookup.Keymode, lookup.LaneCount)),
                BmsLaneSkinElements.BarLine => new DefaultBmsBarLineDisplay(lookup.IsMajorBarLine, lookup.Keymode),
                _ => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

        private static Drawable createDefaultNoteComponent(BmsNoteSkinLookup lookup, bool allowAggregateTextureOverride)
            => lookup.Element switch
            {
                BmsNoteSkinElements.Note => new DefaultBmsNoteDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode, allowAggregateTextureOverride),
                BmsNoteSkinElements.LongNoteHead => new DefaultBmsLongNoteHeadDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode, allowAggregateTextureOverride),
                BmsNoteSkinElements.LongNoteBody => new DefaultBmsLongNoteBodyDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                BmsNoteSkinElements.LongNoteTail => new DefaultBmsLongNoteTailDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode, allowAggregateTextureOverride),
                _ => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };
    }
}
