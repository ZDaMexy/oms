// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Skinning.Pages
{
    /// <summary>Read-only actual selection scope; never publishes or persists a global skin selection.</summary>
    internal partial class SkinSongSelectPageHost : CompositeDrawable
    {
        private readonly IBindable<WorkingBeatmap> working;
        private readonly IBindable<RulesetInfo> ruleset;
        private readonly IBindable<IReadOnlyList<Mod>> mods;
        private readonly SkinSongSelectPageThemeSource source;
        private readonly object preparationGate = new object();
        private CancellationTokenSource? cancellation;
        private SkinSettingsPreview? pending;
        private SkinSettingsPreview? preview;
        private SkinSongSelectPageTheme? theme;
        private ModSettingChangeTracker? settingsTracker;
        private Guid? targetId;
        private int generation;
        private bool stopping;
        private bool suspended;
        private bool requested;

        [Resolved]
        private SkinManager manager { get; set; } = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        internal event Action? AvailabilityChanged;
        internal bool Available => preview?.Skin.PreparedPages?.Document.SongSelect != null;
        internal bool ShowingSkinPage => theme != null;
        internal SkinSelectionScope? Scope { get; private set; }
        internal Skin? PageSkin => preview?.Skin;
        internal string? PreparationError { get; private set; }

        internal SkinSongSelectPageHost(IBindable<WorkingBeatmap> working, IBindable<RulesetInfo> ruleset,
                                       IBindable<IReadOnlyList<Mod>> mods, SkinSongSelectPageThemeSource source)
        {
            this.working = working.GetBoundCopy();
            this.ruleset = ruleset.GetBoundCopy();
            this.mods = mods.GetBoundCopy();
            this.source = source;
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            working.BindValueChanged(_ => reconsider());
            ruleset.BindValueChanged(_ => reconsider());
            mods.BindValueChanged(change =>
            {
                settingsTracker?.Dispose();
                settingsTracker = new ModSettingChangeTracker(change.NewValue);
                settingsTracker.SettingChanged += _ => reconsider();
                reconsider();
            }, true);
            manager.SourceChanged += sourceChanged;
            if (manager.SelectionPreferences != null)
                manager.SelectionPreferences.Changed += assignmentsChanged;
        }

        private void sourceChanged() => Schedule(() => reconsider(force: true));
        private void assignmentsChanged() => Schedule(() => reconsider());

        private void reconsider(bool force = false)
        {
            if (stopping || suspended)
                return;
            string mode = ruleset.Value.ShortName;
            SkinSelectionScope? scope = mode is "bms" or "mania"
                ? new SkinSelectionScope(mode, !working.IsDefault && working.Value.BeatmapInfo.AllowGameplayWithRuleset(ruleset.Value, allowConversion: true)
                    ? ruleset.Value.CreateInstance().GetGameplaySkinKeyCount(working.Value.BeatmapInfo, mods.Value)
                    : null)
                : null;
            Live<SkinInfo>? target = scope is SkinSelectionScope actual ? manager.GetConfiguredSkin(actual) : null;
            if (!force && scope == Scope && target?.ID == targetId && (preview != null || cancellation != null))
                return;
            cancelPending();
            clearPreview();
            Scope = scope;
            targetId = target?.ID;
            PreparationError = null;
            AvailabilityChanged?.Invoke();
            if (target == null)
                return;
            cancellation = new CancellationTokenSource();
            _ = prepareAsync(target, generation, cancellation.Token);
        }

        private async Task prepareAsync(Live<SkinInfo> target, int sequence, CancellationToken token)
        {
            try
            {
                SkinSettingsPreview prepared = await manager.PrepareSettingsSkinAsync(target, token).ConfigureAwait(false);
                lock (preparationGate)
                {
                    if (stopping || token.IsCancellationRequested || sequence != generation)
                    {
                        prepared.Dispose();
                        return;
                    }
                    pending = prepared;
                }
                gameHost.UpdateThread.Scheduler.Add(() => finishPreparation(prepared, sequence, token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
                                              or InvalidOperationException or GameplaySkinScenePreparationException or GameplaySkinScriptException)
            {
                Logger.Error(error, "Failed to prepare the actual selection's optional SongSelect page.");
                gameHost.UpdateThread.Scheduler.Add(() =>
                {
                    if (!stopping && !token.IsCancellationRequested && sequence == generation)
                    {
                        PreparationError = "此皮肤的选曲外观未能读取，已保留原界面。";
                        AvailabilityChanged?.Invoke();
                    }
                });
            }
        }

        private void finishPreparation(SkinSettingsPreview prepared, int sequence, CancellationToken token)
        {
            lock (preparationGate)
            {
                if (!ReferenceEquals(pending, prepared))
                    return;
                pending = null;
            }
            if (stopping || suspended || token.IsCancellationRequested || sequence != generation)
            {
                prepared.Dispose();
                return;
            }
            preview = prepared;
            applyRequestedView();
            AvailabilityChanged?.Invoke();
        }

        internal void ShowSkinPage(bool show)
        {
            requested = show;
            applyRequestedView();
            AvailabilityChanged?.Invoke();
        }

        private void applyRequestedView()
        {
            if (!requested || !Available || suspended)
            {
                clearTheme();
                return;
            }
            if (theme != null)
                return;
            theme = new SkinSongSelectPageTheme(preview!.Skin, preview.Skin.PreparedPages!, manager);
            source.SetTheme(theme);
        }

        private void clearTheme()
        {
            // Synchronous subscribers include pooled/hidden rows. Their sprite references must clear before disposal.
            source.SetTheme(null);
            theme?.Dispose();
            theme = null;
        }

        private void clearPreview()
        {
            clearTheme();
            preview?.Dispose();
            preview = null;
        }

        private void cancelPending()
        {
            SkinSettingsPreview? abandoned;
            lock (preparationGate)
            {
                generation++;
                abandoned = pending;
                pending = null;
            }
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            abandoned?.Dispose();
        }

        internal void Suspend()
        {
            suspended = true;
            cancelPending();
            clearPreview();
            AvailabilityChanged?.Invoke();
        }

        internal void ResumePreparation()
        {
            suspended = false;
            if (LoadState >= LoadState.Ready)
                reconsider(force: true);
        }

        protected override void Dispose(bool isDisposing)
        {
            lock (preparationGate)
                stopping = true;
            cancelPending();
            clearPreview();
            if (manager != null)
            {
                manager.SourceChanged -= sourceChanged;
                if (manager.SelectionPreferences != null)
                    manager.SelectionPreferences.Changed -= assignmentsChanged;
            }
            settingsTracker?.Dispose();
            working.UnbindAll();
            ruleset.UnbindAll();
            mods.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
