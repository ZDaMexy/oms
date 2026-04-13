// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.SongSelect;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Bms.Skinning
{
    public class BmsSkinTransformer : SkinTransformer
    {
        private readonly bool providesBuiltInFallbacks;

        public BmsSkinTransformer(ISkin skin)
            : base(skin)
        {
            providesBuiltInFallbacks = skin is OmsSkin;
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            Drawable? skinnedComponent = base.GetDrawableComponent(lookup);

            switch (lookup)
            {
                case SkinComponentLookup<HitResult> resultComponent when BmsHitResultDisplayNames.TryGetCustomDisplayName(resultComponent.Component, out _):
                    return new SkinnableBmsJudgement(resultComponent.Component);

                case BmsJudgementSkinLookup judgementLookup:
                    return skinnedComponent is IAnimatableJudgement ? skinnedComponent : providesBuiltInFallbacks ? new BmsJudgementPiece(judgementLookup.Result) : null;

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

                case BmsPlayfieldSkinLookup playfieldLookup:
                    return skinnedComponent ?? createBuiltInFallback(() => createDefaultPlayfieldComponent(playfieldLookup));

                case BmsLaneSkinLookup laneLookup:
                    return skinnedComponent ?? createBuiltInFallback(() => createDefaultLaneComponent(laneLookup));

                case BmsNoteSkinLookup noteLookup:
                    return skinnedComponent ?? createBuiltInFallback(() => createDefaultNoteComponent(noteLookup));

                case BmsLaneCoverSkinLookup laneCoverLookup:
                    return skinnedComponent is IBmsLaneCoverDisplay ? skinnedComponent : createBuiltInFallback(() => new DefaultBmsLaneCoverDisplay(laneCoverLookup.Position));

                case GlobalSkinnableContainerLookup containerLookup when containerLookup.Lookup == GlobalSkinnableContainers.MainHUDComponents && containerLookup.Ruleset?.ShortName == BmsRuleset.SHORT_NAME:
                    if (!hasBmsHudLayer(skinnedComponent))
                        return null;

                    Drawable gaugeBar = GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)) ?? new BmsGaugeBar();
                    ComboCounter comboCounter = (ComboCounter)(GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) ?? new BmsComboCounter());
                    Drawable hudLayout = GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)) ?? new DefaultBmsHudLayoutDisplay();

                    if (hudLayout is IBmsHudLayoutDisplay hudLayoutDisplay)
                        hudLayoutDisplay.SetComponents(skinnedComponent, gaugeBar, comboCounter);

                    return hudLayout;
            }

            return skinnedComponent;
        }

        private bool hasBmsHudLayer(Drawable? wrappedHud)
            => wrappedHud != null
               || providesBuiltInFallbacks
               || Skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)) is IBmsHudLayoutDisplay
               || Skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)) != null
               || Skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) is ComboCounter;

        private Drawable? createBuiltInFallback(System.Func<Drawable> createDrawable)
            => providesBuiltInFallbacks ? createDrawable() : null;

        private static Drawable createDefaultPlayfieldComponent(BmsPlayfieldSkinLookup lookup)
            => lookup.Element switch
            {
                BmsPlayfieldSkinElements.Backdrop => new DefaultBmsPlayfieldBackdropDisplay(),
                BmsPlayfieldSkinElements.Baseplate => new DefaultBmsPlayfieldBaseplateDisplay(),
                _ => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

        private static Drawable createDefaultLaneComponent(BmsLaneSkinLookup lookup)
            => lookup.Element switch
            {
                BmsLaneSkinElements.Background => new DefaultBmsLaneBackgroundDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                BmsLaneSkinElements.Divider => new DefaultBmsLaneDividerDisplay(lookup.IsScratch),
                BmsLaneSkinElements.HitTarget => new DefaultBmsHitTargetDisplay(lookup.IsScratch, BmsPlayfieldLayoutProfile.CreateDefault(lookup.Keymode, lookup.LaneCount)),
                BmsLaneSkinElements.BarLine => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = BmsDefaultPlayfieldPalette.GetBarLine(lookup.IsMajorBarLine),
                },
                _ => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

        private static Drawable createDefaultNoteComponent(BmsNoteSkinLookup lookup)
            => lookup.Element switch
            {
                BmsNoteSkinElements.Note => new DefaultBmsNoteDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                BmsNoteSkinElements.LongNoteHead => new DefaultBmsLongNoteHeadDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                BmsNoteSkinElements.LongNoteBody => new DefaultBmsLongNoteBodyDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                BmsNoteSkinElements.LongNoteTail => new DefaultBmsLongNoteTailDisplay(lookup.LaneIndex, lookup.IsScratch, lookup.Keymode),
                _ => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };
    }
}
