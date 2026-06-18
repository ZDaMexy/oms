// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;

namespace osu.Game.Screens.Select
{
    public partial class FilterControl
    {
        /// <summary>
        /// A docked banner shown while a hierarchical group is expanded. It surfaces the active group path
        /// as a breadcrumb and lets the user collapse one level (via the back button or <see cref="GlobalAction.Back"/>)
        /// without scrolling through a large library to find the group header.
        /// </summary>
        /// <remarks>
        /// Reuses the visual pattern of <see cref="ScopedBeatmapSetDisplay"/> but is driven by the carousel's
        /// expanded-group state, independent of <see cref="ScopedBeatmapSet"/>. Scope takes priority for the
        /// back action, so this banner only handles <see cref="GlobalAction.Back"/> when no set is scoped.
        /// </remarks>
        public partial class GroupNavigationDisplay : OsuClickableContainer, IKeyBindingHandler<GlobalAction>
        {
            public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

            private readonly IBindable<GroupDefinition?> expandedGroup = new Bindable<GroupDefinition?>();

            private Container content = null!;
            private OsuTextFlowContainer text = null!;

            private const float transition_duration = 300;

            [Resolved]
            private ISongSelect? songSelect { get; set; }

            public GroupNavigationDisplay()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                CornerRadius = 8f;
                Masking = true;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Content.AutoSizeEasing = Easing.OutQuint;
                Content.AutoSizeDuration = transition_duration;

                AddRange(new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                    },
                    content = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        BypassAutoSizeAxes = Axes.Y,
                        Shear = -OsuGame.SHEAR,
                        Padding = new MarginPadding
                        {
                            Horizontal = 6,
                            Vertical = 2,
                        },
                        Children = new Drawable[]
                        {
                            text = new OsuTextFlowContainer(t => t.Font = OsuFont.Style.Body)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Colour = colourProvider.Content1,
                                Padding = new MarginPadding { Right = 80, Vertical = 5 }
                            },
                            new ShearedButton(80)
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Text = CommonStrings.Back,
                                RelativeSizeAxes = Axes.Y,
                                Height = 1,
                                Action = () => Action?.Invoke(),
                            }
                        }
                    },
                });

                if (songSelect != null)
                    expandedGroup.BindTo(songSelect.CurrentExpandedGroup);

                Action = () => songSelect?.CollapseExpandedGroupOneLevel();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                expandedGroup.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                if (expandedGroup.Value != null)
                {
                    content.BypassAutoSizeAxes = Axes.None;
                    text.Clear();

                    bool first = true;

                    foreach (var group in expandedGroup.Value.GetPathFromRoot())
                    {
                        if (!first)
                            text.AddText(@"  ›  ");

                        text.AddText(group.Title, t => t.Font = OsuFont.Style.Body.With(weight: FontWeight.Bold));
                        first = false;
                    }
                }
                else
                {
                    content.BypassAutoSizeAxes = Axes.Y;
                }
            }

            public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                // Scope (temporarily showing all difficulties of a set) takes priority for the back action,
                // so only collapse a group level when nothing is scoped.
                if (expandedGroup.Value != null && ScopedBeatmapSet.Value == null && e.Action == GlobalAction.Back && !e.Repeat)
                {
                    TriggerClick();
                    return true;
                }

                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
            {
            }
        }
    }
}
