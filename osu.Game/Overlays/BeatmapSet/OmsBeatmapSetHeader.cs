// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>Native chart header without the ppy picker, online cover or ruleset requests.</summary>
    public partial class OmsBeatmapSetHeader : BeatmapSetHeader
    {
        private OsuSpriteText rulesetLabel = null!;

        protected override Drawable CreateContent() => Empty();

        protected override Drawable CreateTabControlContent() => rulesetLabel = new OsuSpriteText
        {
            Font = OsuFont.GetFont(size: 16),
            Margin = new MarginPadding { Vertical = 16 },
        };

        protected override OverlayTitle CreateTitle() => new OmsChartTitle();

        public void SetRuleset(string ruleset) => rulesetLabel.Text = ruleset == "bms" ? "BMS" : "osu!mania";

        private partial class OmsChartTitle : OverlayTitle
        {
            public OmsChartTitle()
            {
                Title = "谱面信息";
                Description = "原谱详情、成绩与下载";
                Icon = OsuIcon.Beatmap;
            }
        }
    }
}
