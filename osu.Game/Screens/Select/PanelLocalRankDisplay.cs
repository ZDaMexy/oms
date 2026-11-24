// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osuTK;
using Realms;

namespace osu.Game.Screens.Select
{
    public partial class PanelLocalRankDisplay : CompositeDrawable
    {
        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                bool changed = beatmap?.ID != value?.ID || beatmap?.Hash != value?.Hash;
                beatmap = value;

                if (changed && IsLoaded)
                    updateSubscription();
            }
        }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IBindable<APIUser> localUser = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        private IDisposable? scoreSubscription;

        private readonly UpdateableRank updateable;

        public bool HasRank => updateable.Rank != null;

        public SongSelectPanelAccent? PanelAccent { get; private set; }

        public PanelLocalRankDisplay(BeatmapInfo? beatmap = null)
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = updateable = new UpdateableRank(animate: false)
            {
                Size = new Vector2(40, 20),
                Alpha = 0,
            };

            Beatmap = beatmap;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api) => localUser = api.LocalUser.GetBoundCopy();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateSubscription());
            mods.BindValueChanged(_ => updateSubscription());
            localUser.BindValueChanged(_ => updateSubscription(), true);
        }

        private void updateSubscription()
        {
            scoreSubscription?.Dispose();
            scoreSubscription = null;
            clearDisplay();

            if (beatmap == null)
                return;

            Guid beatmapId = beatmap.ID;
            string beatmapHash = beatmap.Hash;

            // Legacy rows can have an empty hash. Keep their query bounded to the selected difficulty.
            scoreSubscription = realm.RegisterForNotifications(r => string.IsNullOrEmpty(beatmapHash)
                    ? r.All<ScoreInfo>().Filter($"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0 AND "
                                                + $"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.Hash)} == {nameof(ScoreInfo.BeatmapHash)} AND "
                                                + $"{nameof(ScoreInfo.DeletePending)} == false", beatmapId)
                    : r.All<ScoreInfo>().Where(score => score.BeatmapHash == beatmapHash && !score.DeletePending),
                (scores, _) => localScoresChanged(scores, beatmapId));
        }

        private void localScoresChanged(IRealmCollection<ScoreInfo> scores, Guid beatmapId)
        {
            var rulesetInstance = ruleset.Value.CreateInstance();
            var scoreDisplayBucket = rulesetInstance.GetScoreDisplayBucket(mods.Value);
            int userId = localUser.Value.Id;

            // Identical local chart files may have separate identities. Check the link after the indexed lookup.
            // Modification notifications matter here because a score can regain its chart link after a rescan.
            ScoreInfo? topScore = scores.AsEnumerable()
                                        .Where(score => score.BeatmapInfo?.ID == beatmapId
                                                        && ruleset.Value.Equals(score.Ruleset)
                                                        && score.RealmUser is { } user
                                                        && (user.OnlineID == userId || user.OnlineID <= 1))
                                        .FilterToScoreDisplayBucket(rulesetInstance, scoreDisplayBucket)
                                        .MaxBy(info => (info.TotalScore, -info.Date.UtcDateTime.Ticks));

            PanelAccent = topScore != null ? rulesetInstance.GetSongSelectPanelAccent(topScore) : null;
            updateable.Rank = topScore?.Rank;
            updateable.Alpha = topScore != null ? 1 : 0;
        }

        private void clearDisplay()
        {
            PanelAccent = null;
            updateable.Rank = null;
            updateable.Alpha = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            scoreSubscription?.Dispose();
        }
    }
}
