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
        public BmsSkinTransformer(ISkin skin)
            : base(skin)
        {
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            Drawable? skinnedComponent = base.GetDrawableComponent(lookup);

            switch (lookup)
            {
                case SkinComponentLookup<HitResult> resultComponent when BmsHitResultDisplayNames.TryGetCustomDisplayName(resultComponent.Component, out _):
                    return new SkinnableBmsJudgement(resultComponent.Component);

                case BmsJudgementSkinLookup judgementLookup:
                    return skinnedComponent is IAnimatableJudgement ? skinnedComponent : new BmsJudgementPiece(judgementLookup.Result);

                case BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout }:
                    return skinnedComponent is IBmsHudLayoutDisplay ? skinnedComponent : new DefaultBmsHudLayoutDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeBar }:
                    return skinnedComponent ?? new BmsGaugeBar();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter }:
                    return skinnedComponent is ComboCounter ? skinnedComponent : new BmsComboCounter();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ClearLamp }:
                    return skinnedComponent is IBmsClearLampDisplay ? skinnedComponent : new DefaultBmsClearLampDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeHistoryPanel }:
                    return skinnedComponent is IBmsGaugeHistoryPanelDisplay ? skinnedComponent : new DefaultBmsGaugeHistoryPanelDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeHistory }:
                    return skinnedComponent is IBmsGaugeHistoryDisplay ? skinnedComponent : new DefaultBmsGaugeHistoryDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ResultsSummaryPanel }:
                    return skinnedComponent is IBmsResultsSummaryPanelDisplay ? skinnedComponent : new DefaultBmsResultsSummaryPanelDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.ResultsSummary }:
                    return skinnedComponent is IBmsResultsSummaryDisplay ? skinnedComponent : new DefaultBmsResultsSummaryDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.NoteDistributionPanel }:
                    return skinnedComponent is IBmsNoteDistributionPanelDisplay
                        ? skinnedComponent
                        : new DefaultBmsNoteDistributionPanelDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.NoteDistribution }:
                    return skinnedComponent is IBmsNoteDistributionDisplay
                        ? skinnedComponent
                        : new DefaultBmsNoteDistributionDisplay();

                case BmsSkinComponentLookup { Component: BmsSkinComponents.StaticBackgroundLayer }:
                    return skinnedComponent is IBmsBackgroundLayerDisplay ? skinnedComponent : new DefaultBmsBackgroundLayerDisplay();

                case BmsPlayfieldSkinLookup playfieldLookup:
                    return skinnedComponent ?? createDefaultPlayfieldComponent(playfieldLookup);

                case BmsLaneSkinLookup laneLookup:
                    return skinnedComponent ?? createDefaultLaneComponent(laneLookup);

                case BmsNoteSkinLookup noteLookup:
                    return skinnedComponent ?? createDefaultNoteComponent(noteLookup);

                case BmsLaneCoverSkinLookup laneCoverLookup:
                    return skinnedComponent is IBmsLaneCoverDisplay ? skinnedComponent : new DefaultBmsLaneCoverDisplay(laneCoverLookup.Position);

                case GlobalSkinnableContainerLookup containerLookup when containerLookup.Lookup == GlobalSkinnableContainers.MainHUDComponents && containerLookup.Ruleset?.ShortName == BmsRuleset.SHORT_NAME:
                    Drawable gaugeBar = GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar)) ?? new BmsGaugeBar();
                    ComboCounter comboCounter = (ComboCounter)(GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) ?? new BmsComboCounter());
                    Drawable hudLayout = GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout)) ?? new DefaultBmsHudLayoutDisplay();

                    if (hudLayout is IBmsHudLayoutDisplay hudLayoutDisplay)
                        hudLayoutDisplay.SetComponents(skinnedComponent, gaugeBar, comboCounter);

                    return hudLayout;
            }

            return skinnedComponent;
        }

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
                BmsLaneSkinElements.Background => new DefaultBmsLaneBackgroundDisplay(lookup.LaneIndex, lookup.IsScratch),
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
                BmsNoteSkinElements.Note => new DefaultBmsNoteDisplay(lookup.IsScratch),
                BmsNoteSkinElements.LongNoteHead => new DefaultBmsLongNoteHeadDisplay(lookup.IsScratch),
                BmsNoteSkinElements.LongNoteBody => new DefaultBmsLongNoteBodyDisplay(lookup.IsScratch),
                BmsNoteSkinElements.LongNoteTail => new DefaultBmsLongNoteTailDisplay(lookup.IsScratch),
                _ => new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };
    }
}
