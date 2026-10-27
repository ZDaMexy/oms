// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Scoring;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Skinning.Pages
{
    /// <summary>
    /// Resolves the selected score's real gameplay scope without changing the current skin. This owner joins pending
    /// reads on cancellation and detaches drawables before releasing the exact package lease.
    /// </summary>
    internal partial class SkinResultsPageHost : CompositeDrawable
    {
        private readonly IBindable<ScoreInfo?> selectedScore;
        private readonly Func<Drawable> createStatistics;
        private readonly object preparationGate = new object();
        private CancellationTokenSource? preparationCancellation;
        private SkinSettingsPreview? pendingPreview;
        private SkinSettingsPreview? preview;
        private SkinResultsPageDisplay? display;
        private Bindable<long>? displayedScore;
        private int generation;
        private bool stopping;
        private bool resumeSkinPage;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        public event Action? AvailabilityChanged;

        public bool Available => preview?.Skin.PreparedPages?.Document.Results != null;
        public bool ShowingSkinPage => display != null;
        internal Skin? PageSkin => preview?.Skin;
        internal SkinSelectionScope? Scope { get; private set; }
        internal string? PreparationError { get; private set; }

        public SkinResultsPageHost(IBindable<ScoreInfo?> score, Func<Drawable> createStatistics, MarginPadding padding = default)
        {
            selectedScore = score.GetBoundCopy();
            this.createStatistics = createStatistics;
            RelativeSizeAxes = Axes.Both;
            Padding = padding;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            selectedScore.BindValueChanged(_ => prepareScore(), true);
        }

        private void prepareScore()
        {
            CancelPendingPreparation();
            ShowSkinPage(false);
            displayedScore?.UnbindAll();
            displayedScore = null;
            preview?.Dispose();
            preview = null;
            Scope = null;
            PreparationError = null;
            AvailabilityChanged?.Invoke();
            ScoreInfo? score = selectedScore.Value;
            if (score == null || score.Ruleset.ShortName is not ("bms" or "mania"))
                return;

            var ruleset = score.Ruleset.CreateInstance();
            int? keys = score.BeatmapInfo == null ? null : ruleset.GetGameplaySkinKeyCount(score.BeatmapInfo, score.Mods);
            var scope = new SkinSelectionScope(score.Ruleset.ShortName, keys);
            Scope = scope;
            var target = skinManager.GetConfiguredSkin(scope);
            var cancellation = new CancellationTokenSource();
            preparationCancellation = cancellation;
            int sequence = generation;
            _ = prepareAsync(target, score, sequence, cancellation.Token);
        }

        private async Task prepareAsync(Live<SkinInfo> target, ScoreInfo score, int sequence, CancellationToken token)
        {
            try
            {
                SkinSettingsPreview prepared = await skinManager.PrepareSettingsSkinAsync(target, token).ConfigureAwait(false);
                lock (preparationGate)
                {
                    if (stopping || token.IsCancellationRequested || sequence != generation)
                    {
                        prepared.Dispose();
                        return;
                    }
                    pendingPreview = prepared;
                }
                // The game's scheduler remains reachable if this results screen is suspended or disposed.
                gameHost.UpdateThread.Scheduler.Add(() => finishPreparation(prepared, score, sequence, token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException
                                              or GameplaySkinScenePreparationException or GameplaySkinScriptException)
            {
                Logger.Error(error, "Failed to prepare the selected score's optional skin results page.");
                gameHost.UpdateThread.Scheduler.Add(() =>
                {
                    if (!IsDisposed && !token.IsCancellationRequested && sequence == generation)
                    {
                        PreparationError = "此皮肤的结算页未能读取，已保留原界面。";
                        AvailabilityChanged?.Invoke();
                    }
                });
            }
        }

        private void finishPreparation(SkinSettingsPreview prepared, ScoreInfo score, int sequence, CancellationToken token)
        {
            lock (preparationGate)
            {
                if (!ReferenceEquals(pendingPreview, prepared))
                    return;
                pendingPreview = null;
            }
            if (IsDisposed || stopping || token.IsCancellationRequested || sequence != generation || !ReferenceEquals(selectedScore.Value, score))
            {
                prepared.Dispose();
                return;
            }
            preview = prepared;
            displayedScore = scoreManager.GetBindableTotalScore(score);
            displayedScore.BindValueChanged(_ => display?.SetData(SkinResultsPageData.FromScore(score, displayedScore.Value)));
            AvailabilityChanged?.Invoke();
        }

        public void ShowSkinPage(bool show)
        {
            if (display != null)
            {
                if (show)
                    return;
                if (!RemoveInternal(display, disposeImmediately: true))
                    throw new InvalidOperationException("The old results page could not be detached.");
                display = null;
            }
            if (!show || preview?.Skin.PreparedPages is not SkinPagePreparedPackage package || package.Document.Results == null)
                return;
            ScoreInfo score = selectedScore.Value!;
            display = new SkinResultsPageDisplay(preview.Skin, package,
                SkinResultsPageData.FromScore(score, displayedScore!.Value), createStatistics);
            AddInternal(display);
        }

        public void CancelPendingPreparation()
        {
            SkinSettingsPreview? pending;
            lock (preparationGate)
            {
                generation++;
                pending = pendingPreview;
                pendingPreview = null;
            }
            preparationCancellation?.Cancel();
            preparationCancellation?.Dispose();
            preparationCancellation = null;
            pending?.Dispose();
        }

        /// <summary>Only a cancelled initial read needs restarting on screen resume; a retained page remains exact.</summary>
        public void ResumePreparation()
        {
            if (preview == null && preparationCancellation == null)
                prepareScore();
            if (resumeSkinPage && Available)
                ShowSkinPage(true);
            resumeSkinPage = false;
        }

        public void Suspend()
        {
            resumeSkinPage = ShowingSkinPage;
            ShowSkinPage(false);
            CancelPendingPreparation();
        }

        protected override void Dispose(bool isDisposing)
        {
            lock (preparationGate)
                stopping = true;
            CancelPendingPreparation();
            ShowSkinPage(false);
            selectedScore.UnbindAll();
            displayedScore?.UnbindAll();
            displayedScore = null;
            base.Dispose(isDisposing);
            preview?.Dispose();
            preview = null;
        }
    }
}
