// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using System.Linq;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    /// <summary>
    /// Explicit OMS entry point for routing the built-in candidate skin through mania skinning.
    /// </summary>
    public class ManiaOmsSkinTransformer : ManiaLegacySkinTransformer
    {
        private readonly ManiaBeatmap beatmap;

        public ManiaOmsSkinTransformer(ISkin skin, IBeatmap beatmap)
            : base(skin, beatmap)
        {
            this.beatmap = (ManiaBeatmap)beatmap;
        }

        public override Drawable GetDrawableComponent(ISkinComponentLookup lookup)
        {
            if (lookup is GlobalSkinnableContainerLookup containerLookup)
            {
                if (containerLookup.Ruleset == null)
                    return base.GetDrawableComponent(lookup);

                if (!IsProvidingLegacyResources)
                    return null!;

                switch (containerLookup.Lookup)
                {
                    case GlobalSkinnableContainers.MainHUDComponents:
                        return new DefaultSkinComponentsContainer(container =>
                        {
                            var combo = container.ChildrenOfType<OmsManiaComboCounter>().FirstOrDefault();
                            var spectatorList = container.OfType<SpectatorList>().FirstOrDefault();
                            var leaderboard = container.OfType<DrawableGameplayLeaderboard>().FirstOrDefault();

                            if (combo != null)
                            {
                                combo.Anchor = Anchor.TopCentre;
                                combo.Origin = Anchor.Centre;
                                combo.Y = this.GetManiaSkinConfig<float>(LegacyManiaSkinConfigurationLookups.ComboPosition)?.Value ?? 0;
                            }

                            if (spectatorList != null)
                            {
                                spectatorList.Anchor = Anchor.BottomLeft;
                                spectatorList.Origin = Anchor.BottomLeft;
                                spectatorList.Position = new Vector2(10, -10);
                            }

                            if (leaderboard != null)
                            {
                                leaderboard.Anchor = Anchor.CentreLeft;
                                leaderboard.Origin = Anchor.CentreLeft;
                                leaderboard.X = 10;
                            }

                            foreach (var drawable in container.OfType<ISerialisableDrawable>())
                                drawable.UsesFixedAnchor = true;
                        })
                        {
                            new OmsManiaComboCounter(),
                            new SpectatorList(),
                            new DrawableGameplayLeaderboard(),
                        };
                }

                return null!;
            }

            if (lookup is SkinComponentLookup<HitResult> resultComponent)
                return getOmsJudgement(resultComponent.Component) ?? base.GetDrawableComponent(lookup);

            if (lookup is ManiaSkinComponentLookup maniaComponent)
            {
                switch (maniaComponent.Component)
                {
                    case ManiaSkinComponents.StageBackground:
                        return new OmsStageBackground();

                    case ManiaSkinComponents.StageForeground:
                        return new OmsStageForeground();

                    case ManiaSkinComponents.ColumnBackground:
                        return new OmsColumnBackground();

                    case ManiaSkinComponents.KeyArea:
                        return new OmsKeyArea();

                    case ManiaSkinComponents.Note:
                        return new OmsNotePiece();

                    case ManiaSkinComponents.HoldNoteHead:
                        return new OmsHoldNoteHeadPiece();

                    case ManiaSkinComponents.HoldNoteTail:
                        return new OmsHoldNoteTailPiece();

                    case ManiaSkinComponents.HoldNoteBody:
                        return new OmsHoldNoteBodyPiece();

                    case ManiaSkinComponents.HitTarget:
                        return new OmsHitTarget();

                    case ManiaSkinComponents.HitExplosion:
                        return new OmsHitExplosion();

                    case ManiaSkinComponents.BarLine:
                        return new OmsBarLine();
                }
            }

            return base.GetDrawableComponent(lookup);
        }

        private Drawable? getOmsJudgement(HitResult result)
        {
            string? filename = result switch
            {
                HitResult.Perfect => OmsManiaJudgementAssetPreset.Shared.Hit300gImage,
                HitResult.Great => OmsManiaJudgementAssetPreset.Shared.Hit300Image,
                HitResult.Good => OmsManiaJudgementAssetPreset.Shared.Hit200Image,
                HitResult.Ok => OmsManiaJudgementAssetPreset.Shared.Hit100Image,
                HitResult.Meh => OmsManiaJudgementAssetPreset.Shared.Hit50Image,
                HitResult.Miss => OmsManiaJudgementAssetPreset.Shared.Hit0Image,
                _ => null,
            };

            if (filename == null)
                return null;

            var animation = this.GetAnimation(filename, true, true, frameLength: 1000 / 20d);
            return animation == null ? null : new OmsManiaJudgementPiece(result, animation);
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is ManiaSkinConfigurationLookup maniaLookup)
            {
                var sharedAssetPreset = OmsManiaShellAssetPreset.Shared;
                var sharedJudgementPreset = OmsManiaJudgementAssetPreset.Shared;
                var sharedHoldNoteBodyPreset = OmsManiaHoldNoteBodyPreset.Shared;

                switch (maniaLookup.Lookup)
                {
                    case LegacyManiaSkinConfigurationLookups.ColumnLineColour:
                        return SkinUtils.As<TValue>(new Bindable<osuTK.Graphics.Color4>(OmsManiaColumnColourPreset.ForStageColumns(4)?.ColumnLineColour ?? osuTK.Graphics.Color4.White));

                    case LegacyManiaSkinConfigurationLookups.JudgementLineColour:
                        return SkinUtils.As<TValue>(new Bindable<osuTK.Graphics.Color4>(OmsManiaColumnColourPreset.ForStageColumns(4)?.JudgementLineColour ?? osuTK.Graphics.Color4.White));

                    case LegacyManiaSkinConfigurationLookups.LeftStageImage:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedAssetPreset.LeftStageImage));

                    case LegacyManiaSkinConfigurationLookups.RightStageImage:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedAssetPreset.RightStageImage));

                    case LegacyManiaSkinConfigurationLookups.BottomStageImage:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedAssetPreset.BottomStageImage));

                    case LegacyManiaSkinConfigurationLookups.HitTargetImage:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedAssetPreset.HitTargetImage));

                    case LegacyManiaSkinConfigurationLookups.LightImage:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedAssetPreset.LightImage));

                    case LegacyManiaSkinConfigurationLookups.Hit300g:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedJudgementPreset.Hit300gImage));

                    case LegacyManiaSkinConfigurationLookups.Hit300:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedJudgementPreset.Hit300Image));

                    case LegacyManiaSkinConfigurationLookups.Hit200:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedJudgementPreset.Hit200Image));

                    case LegacyManiaSkinConfigurationLookups.Hit100:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedJudgementPreset.Hit100Image));

                    case LegacyManiaSkinConfigurationLookups.Hit50:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedJudgementPreset.Hit50Image));

                    case LegacyManiaSkinConfigurationLookups.Hit0:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedJudgementPreset.Hit0Image));

                    case LegacyManiaSkinConfigurationLookups.KeysUnderNotes:
                        return SkinUtils.As<TValue>(new Bindable<bool>(sharedAssetPreset.KeysUnderNotes));

                    case LegacyManiaSkinConfigurationLookups.HoldNoteLightImage:
                        return SkinUtils.As<TValue>(new Bindable<string>(sharedHoldNoteBodyPreset.LightImage));

                    case LegacyManiaSkinConfigurationLookups.HoldNoteLightScale:
                        return SkinUtils.As<TValue>(new Bindable<float>(sharedHoldNoteBodyPreset.LightScale));

                    case LegacyManiaSkinConfigurationLookups.NoteBodyStyle:
                        return SkinUtils.As<TValue>(new Bindable<LegacyNoteBodyStyle>(sharedHoldNoteBodyPreset.BodyStyle));
                }

                if (maniaLookup.ColumnIndex is int columnIndex && tryGetStageColumnInfo(columnIndex, out int stageColumns, out int localColumnIndex))
                {
                    var columnPreset = OmsManiaLayoutPreset.ForStageColumns(stageColumns);

                    if (columnPreset != null)
                    {
                        switch (maniaLookup.Lookup)
                        {
                            case LegacyManiaSkinConfigurationLookups.ColumnWidth:
                                return SkinUtils.As<TValue>(new Bindable<float>(columnPreset.GetColumnWidth(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale:
                                return SkinUtils.As<TValue>(new Bindable<float>(columnPreset.NoteHeightReferenceWidth));

                            case LegacyManiaSkinConfigurationLookups.LeftColumnSpacing:
                                return SkinUtils.As<TValue>(new Bindable<float>(columnPreset.GetLeftColumnSpacing(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.RightColumnSpacing:
                                return SkinUtils.As<TValue>(new Bindable<float>(columnPreset.GetRightColumnSpacing(localColumnIndex)));
                        }
                    }

                    var shellPreset = OmsManiaShellPreset.ForStageColumns(stageColumns);

                    if (shellPreset != null)
                    {
                        switch (maniaLookup.Lookup)
                        {
                            case LegacyManiaSkinConfigurationLookups.LeftLineWidth:
                                return SkinUtils.As<TValue>(new Bindable<float>(shellPreset.GetLeftLineWidth(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.RightLineWidth:
                                return SkinUtils.As<TValue>(new Bindable<float>(shellPreset.GetRightLineWidth(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.LightPosition:
                                return SkinUtils.As<TValue>(new Bindable<float>(shellPreset.LightPosition));

                            case LegacyManiaSkinConfigurationLookups.ShowJudgementLine:
                                return SkinUtils.As<TValue>(new Bindable<bool>(shellPreset.ShowJudgementLine));

                            case LegacyManiaSkinConfigurationLookups.LightFramePerSecond:
                                return SkinUtils.As<TValue>(new Bindable<int>(shellPreset.LightFramePerSecond));
                        }
                    }

                    var keyAssetPreset = OmsManiaKeyAssetPreset.ForStageColumns(stageColumns);

                    if (keyAssetPreset != null)
                    {
                        switch (maniaLookup.Lookup)
                        {
                            case LegacyManiaSkinConfigurationLookups.KeyImage:
                                return SkinUtils.As<TValue>(new Bindable<string>(keyAssetPreset.GetKeyImage(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.KeyImageDown:
                                return SkinUtils.As<TValue>(new Bindable<string>(keyAssetPreset.GetKeyDownImage(localColumnIndex)));
                        }
                    }

                    var noteAssetPreset = OmsManiaNoteAssetPreset.ForStageColumns(stageColumns);

                    if (noteAssetPreset != null)
                    {
                        switch (maniaLookup.Lookup)
                        {
                            case LegacyManiaSkinConfigurationLookups.NoteImage:
                                return SkinUtils.As<TValue>(new Bindable<string>(noteAssetPreset.GetNoteImage(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage:
                                return SkinUtils.As<TValue>(new Bindable<string>(noteAssetPreset.GetHoldHeadImage(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.HoldNoteTailImage:
                                return SkinUtils.As<TValue>(new Bindable<string>(noteAssetPreset.GetHoldTailImage(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage:
                                return SkinUtils.As<TValue>(new Bindable<string>(noteAssetPreset.GetHoldBodyImage(localColumnIndex)));
                        }
                    }

                    var hitExplosionPreset = OmsManiaHitExplosionPreset.ForStageColumns(stageColumns);

                    if (hitExplosionPreset != null)
                    {
                        switch (maniaLookup.Lookup)
                        {
                            case LegacyManiaSkinConfigurationLookups.ExplosionImage:
                                return SkinUtils.As<TValue>(new Bindable<string>(hitExplosionPreset.ExplosionImage));

                            case LegacyManiaSkinConfigurationLookups.ExplosionScale:
                                return SkinUtils.As<TValue>(new Bindable<float>(hitExplosionPreset.GetExplosionScale(localColumnIndex)));
                        }
                    }

                    var colourPreset = OmsManiaColumnColourPreset.ForStageColumns(stageColumns);

                    if (colourPreset != null)
                    {
                        switch (maniaLookup.Lookup)
                        {
                            case LegacyManiaSkinConfigurationLookups.ColumnLineColour:
                                return SkinUtils.As<TValue>(new Bindable<osuTK.Graphics.Color4>(colourPreset.ColumnLineColour));

                            case LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour:
                                return SkinUtils.As<TValue>(new Bindable<osuTK.Graphics.Color4>(colourPreset.GetColumnBackgroundColour(localColumnIndex)));

                            case LegacyManiaSkinConfigurationLookups.ColumnLightColour:
                                return SkinUtils.As<TValue>(new Bindable<osuTK.Graphics.Color4>(colourPreset.GetColumnLightColour(localColumnIndex)));
                        }
                    }
                }

                var sharedPreset = getSharedPreset(OmsManiaLayoutPreset.ForStageColumns);

                if (sharedPreset != null)
                {
                    switch (maniaLookup.Lookup)
                    {
                        case LegacyManiaSkinConfigurationLookups.HitPosition:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedPreset.HitPosition));

                        case LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedPreset.NoteHeightReferenceWidth));

                        case LegacyManiaSkinConfigurationLookups.StagePaddingTop:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedPreset.StagePaddingTop));

                        case LegacyManiaSkinConfigurationLookups.StagePaddingBottom:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedPreset.StagePaddingBottom));
                    }
                }

                var sharedShellPreset = getSharedPreset(OmsManiaShellPreset.ForStageColumns);

                if (sharedShellPreset != null)
                {
                    switch (maniaLookup.Lookup)
                    {
                        case LegacyManiaSkinConfigurationLookups.LightPosition:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedShellPreset.LightPosition));

                        case LegacyManiaSkinConfigurationLookups.ShowJudgementLine:
                            return SkinUtils.As<TValue>(new Bindable<bool>(sharedShellPreset.ShowJudgementLine));

                        case LegacyManiaSkinConfigurationLookups.LightFramePerSecond:
                            return SkinUtils.As<TValue>(new Bindable<int>(sharedShellPreset.LightFramePerSecond));
                    }
                }

                var sharedJudgementPositionPreset = getSharedPreset(OmsManiaJudgementPositionPreset.ForStageColumns);

                if (sharedJudgementPositionPreset != null)
                {
                    switch (maniaLookup.Lookup)
                    {
                        case LegacyManiaSkinConfigurationLookups.ComboPosition:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedJudgementPositionPreset.ComboPosition));

                        case LegacyManiaSkinConfigurationLookups.ScorePosition:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedJudgementPositionPreset.ScorePosition));
                    }
                }

                var sharedBarLinePreset = getSharedPreset(OmsManiaBarLinePreset.ForStageColumns);

                if (sharedBarLinePreset != null)
                {
                    switch (maniaLookup.Lookup)
                    {
                        case LegacyManiaSkinConfigurationLookups.BarLineHeight:
                            return SkinUtils.As<TValue>(new Bindable<float>(sharedBarLinePreset.BarLineHeight));

                        case LegacyManiaSkinConfigurationLookups.BarLineColour:
                            return SkinUtils.As<TValue>(new Bindable<osuTK.Graphics.Color4>(sharedBarLinePreset.BarLineColour));
                    }
                }
            }

            return base.GetConfig<TLookup, TValue>(lookup);
        }

        private TPreset? getSharedPreset<TPreset>(System.Func<int, TPreset?> getPreset)
            where TPreset : class
        {
            return getPreset(beatmap.Stages[0].Columns);
        }

        private bool tryGetStageColumnInfo(int columnIndex, out int stageColumns, out int localColumnIndex)
        {
            foreach (var stage in beatmap.Stages)
            {
                if (columnIndex < stage.Columns)
                {
                    localColumnIndex = columnIndex;
                    stageColumns = stage.Columns;
                    return true;
                }

                columnIndex -= stage.Columns;
            }

            stageColumns = 0;
            localColumnIndex = 0;
            return false;
        }
    }
}
