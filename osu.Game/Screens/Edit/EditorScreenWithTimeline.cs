// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Screens.Edit
{
    [Cached]
    public abstract partial class EditorScreenWithTimeline : EditorScreen
    {
        public TimelineArea TimelineArea { get; private set; } = null!;

        public Container MainContent { get; private set; } = null!;

        private LoadingSpinner spinner = null!;
        private Container timelineContent = null!;
        private PendingAsyncDrawableOwnership<Drawable>? pendingMainContentOwnership;
        private PendingAsyncDrawableOwnership<TimelineArea>? pendingTimelineOwnership;

        protected EditorScreenWithTimeline(EditorScreenMode type)
            : base(type)
        {
        }

        [BackgroundDependencyLoader(true)]
        private void load()
        {
            // Grid with only two rows.
            // First is the timeline area, which should be allowed to expand as required.
            // Second is the main editor content, including the playfield and side toolbars (but not the bottom).
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new Container
                        {
                            Name = "Timeline",
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new GridContainer
                                {
                                    Name = "Timeline content",
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            timelineContent = new Container
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                        },
                                    },
                                    RowDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.Absolute, 90),
                                    }
                                }
                            }
                        },
                    },
                    new Drawable[]
                    {
                        MainContent = new Container
                        {
                            Name = "Main content",
                            RelativeSizeAxes = Axes.Both,
                            Depth = float.MaxValue,
                            Child = spinner = new LoadingSpinner(true)
                            {
                                State = { Value = Visibility.Visible },
                            },
                        },
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Drawable mainContent = CreateMainContent();
            var mainOwnership = new PendingAsyncDrawableOwnership<Drawable>(mainContent);
            pendingMainContentOwnership = mainOwnership;

            try
            {
                mainOwnership.Attach(LoadComponentAsync(mainOwnership.Loadable, loaded =>
                {
                    if (!ReferenceEquals(pendingMainContentOwnership, mainOwnership)
                        || !mainOwnership.TryTransfer(loaded, out Drawable? ownedContent))
                    {
                        return;
                    }

                    pendingMainContentOwnership = null;
                    try
                    {
                        spinner.State.Value = Visibility.Hidden;

                        MainContent.Add(ownedContent!);
                        ownedContent.FadeInFromZero(300, Easing.OutQuint);
                        beginTimelineLoad();
                    }
                    catch
                    {
                        if (ownedContent!.Parent == null)
                            ownedContent.Dispose();

                        throw;
                    }
                    finally
                    {
                        mainOwnership.CompleteTransfer();
                    }
                }), Scheduler);
            }
            catch
            {
                if (ReferenceEquals(pendingMainContentOwnership, mainOwnership))
                    pendingMainContentOwnership = null;

                mainOwnership.ReclaimUnstarted();
                throw;
            }
        }

        private void beginTimelineLoad()
        {
            var timeline = new TimelineArea(CreateTimelineContent());
            TimelineArea = timeline;
            var ownership = new PendingAsyncDrawableOwnership<TimelineArea>(timeline);
            pendingTimelineOwnership = ownership;

            try
            {
                ownership.Attach(LoadComponentAsync(ownership.Loadable, loaded =>
                {
                    if (!ReferenceEquals(pendingTimelineOwnership, ownership)
                        || !ownership.TryTransfer(loaded, out TimelineArea? owned))
                    {
                        return;
                    }

                    pendingTimelineOwnership = null;

                    try
                    {
                        ConfigureTimeline(owned!);
                        timelineContent.Add(owned);
                    }
                    catch
                    {
                        if (owned!.Parent == null)
                            owned.Dispose();

                        throw;
                    }
                    finally
                    {
                        ownership.CompleteTransfer();
                    }
                }), Scheduler);
            }
            catch
            {
                if (ReferenceEquals(pendingTimelineOwnership, ownership))
                    pendingTimelineOwnership = null;

                ownership.ReclaimUnstarted();
                throw;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            PendingAsyncDrawableOwnership<Drawable>? mainOwnership = pendingMainContentOwnership;
            pendingMainContentOwnership = null;
            mainOwnership?.Cancel();

            PendingAsyncDrawableOwnership<TimelineArea>? timelineOwnership = pendingTimelineOwnership;
            pendingTimelineOwnership = null;
            timelineOwnership?.Cancel();

            base.Dispose(isDisposing);
            mainOwnership?.JoinAfterParentDisposal();
            timelineOwnership?.JoinAfterParentDisposal();
        }

        protected virtual void ConfigureTimeline(TimelineArea timelineArea)
        {
        }

        protected abstract Drawable CreateMainContent();

        protected virtual Drawable CreateTimelineContent() => new Container();
    }
}
