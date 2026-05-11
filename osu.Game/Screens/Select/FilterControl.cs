// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select.Filter;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public sealed partial class FilterControl : OverlayContainer
    {
        // taken from draw visualiser. used for carousel alignment purposes.
        public const float HEIGHT_FROM_SCREEN_TOP = 141 - corner_radius;

        private const float corner_radius = 10;
        private const string bms_ruleset_short_name = "bms";

        public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

        private SongSelectSearchTextBox searchTextBox = null!;
        private readonly BindableBool showConvertedBeatmaps = new BindableBool();
        private FillFlowContainer<Drawable> rulesetSpecificFiltersHost = null!;
        private Container standardFiltersContainer = null!;
        private Container bmsFiltersContainer = null!;
        private Drawable standardFilters = null!;
        private Drawable bmsFilters = null!;
        private ShearedToggleButton standardShowConvertedBeatmapsButton = null!;
        private ShearedToggleButton bmsShowConvertedBeatmapsButton = null!;
        private DifficultyRangeSlider difficultyRangeSlider = null!;
        private BmsCompositionFilterControl bmsCompositionFilter = null!;
        private readonly List<BmsKeyCountToggleButton> bmsKeyCountButtons = new List<BmsKeyCountToggleButton>();
        private ShearedDropdown<SortMode> sortDropdown = null!;
        private ShearedDropdown<GroupMode> groupDropdown = null!;
        private CollectionDropdown collectionDropdown = null!;
        private SortMode? sortModeBeforeLockedGrouping;

        /// <summary>
        /// An optional method which can force certain criteria adjustments.
        /// </summary>
        public Action<FilterCriteria>? ApplyRequiredCriteria { get; set; }

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IBindable<APIUser> localUser = null!;
        private readonly IBindableList<int> localUserFavouriteBeatmapSets = new BindableList<int>();

        public LocalisableString StatusText
        {
            get => searchTextBox.StatusText;
            set => searchTextBox.StatusText = value;
        }

        public event Action<FilterCriteria>? CriteriaChanged;

        private FilterCriteria currentCriteria = null!;

        private IDisposable? collectionsSubscription;

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Shear = OsuGame.SHEAR;
            Margin = new MarginPadding { Top = -corner_radius, Right = -40 };

            standardFilters = createStandardFilters();
            bmsFilters = createBmsFilters();

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = corner_radius,
                    Masking = true,
                    Child = new WedgeBackground
                    {
                        Anchor = Anchor.TopRight,
                        Scale = new Vector2(-1, 1),
                    }
                },
                new ReverseChildIDFillFlowContainer<Drawable>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, 5f),
                    Padding = new MarginPadding { Top = corner_radius + 5, Bottom = 2, Right = 40f, Left = 2f },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Shear = -OsuGame.SHEAR,
                            Child = searchTextBox = new SongSelectSearchTextBox
                            {
                                RelativeSizeAxes = Axes.X,
                                HoldFocus = true,
                                ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                            },
                        },
                        rulesetSpecificFiltersHost = new FillFlowContainer<Drawable>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                standardFiltersContainer = createRulesetSpecificFiltersContainer(standardFilters),
                                bmsFiltersContainer = createRulesetSpecificFiltersContainer(bmsFilters),
                            },
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            Shear = -OsuGame.SHEAR,
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            ColumnDimensions = new[]
                            {
                                new Dimension(maxSize: 180),
                                new Dimension(GridSizeMode.Absolute, 5),
                                new Dimension(maxSize: 180),
                                new Dimension(GridSizeMode.Absolute, 5),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new[]
                                {
                                    sortDropdown = new ShearedDropdown<SortMode>(SongSelectStrings.Sort)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Items = Array.Empty<SortMode>(),
                                    },
                                    Empty(),
                                    groupDropdown = new ShearedDropdown<GroupMode>(SongSelectStrings.Group)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Items = Array.Empty<GroupMode>(),
                                    },
                                    Empty(),
                                    collectionDropdown = new CollectionDropdown
                                    {
                                        RelativeSizeAxes = Axes.X,
                                    },
                                }
                            }
                        },
                        new ScopedBeatmapSetDisplay
                        {
                            ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                        }
                    },
                }
            };

            updateRulesetSpecificFilters();

            localUser = api.LocalUser.GetBoundCopy();
            localUserFavouriteBeatmapSets.BindTo(api.LocalUserState.FavouriteBeatmapSets);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            difficultyRangeSlider.LowerBound = config.GetBindable<double>(OsuSetting.DisplayStarsMinimum);
            difficultyRangeSlider.UpperBound = config.GetBindable<double>(OsuSetting.DisplayStarsMaximum);
            config.BindWith(OsuSetting.ShowConvertedBeatmaps, showConvertedBeatmaps);
            config.BindWith(OsuSetting.SongSelectSortingMode, sortDropdown.Current);
            config.BindWith(OsuSetting.SongSelectGroupMode, groupDropdown.Current);

            standardShowConvertedBeatmapsButton.Active.BindTo(showConvertedBeatmaps);
            bmsShowConvertedBeatmapsButton.Active.BindTo(showConvertedBeatmaps);

            updateRulesetSpecificFilters();

            updateAvailableSortingModes();
            updateAvailableGroupingModes();
            updateSortDropdownState();

            ruleset.BindValueChanged(_ =>
            {
                updateRulesetSpecificFilters();
                bool sortSelectionChanged = updateAvailableSortingModes();
                bool groupSelectionChanged = updateAvailableGroupingModes();
                updateSortDropdownState();

                if (!sortSelectionChanged && !groupSelectionChanged)
                    updateCriteria();
            });
            mods.BindValueChanged(m =>
            {
                // The following is a note carried from old song select and may not be a valid reason anymore:
                // // Mods are updated once by the mod select overlay when song select is entered,
                // // regardless of if there are any mods or any changes have taken place.
                // // Updating the criteria here so early triggers a re-ordering of panels on song select, via... some mechanism.
                // // Todo: Investigate/fix and potentially remove this.
                // TODO: this might be simply removable with the new song select & carousel code.
                if (m.NewValue.SequenceEqual(m.OldValue))
                    return;

                var rulesetCriteria = currentCriteria.RulesetCriteria;
                if (rulesetCriteria?.FilterMayChangeFromMods(m) == true)
                    updateCriteria();
            });

            searchTextBox.Current.BindValueChanged(_ => updateCriteria());
            difficultyRangeSlider.LowerBound.BindValueChanged(_ => updateCriteria());
            difficultyRangeSlider.UpperBound.BindValueChanged(_ => updateCriteria());
            showConvertedBeatmaps.BindValueChanged(_ => updateCriteria());

            foreach (var segment in bmsCompositionFilter.Segments)
            {
                segment.Enabled.BindValueChanged(_ => updateCriteria());
                segment.UpperBound.BindValueChanged(_ => updateCriteria());
            }

            foreach (var button in bmsKeyCountButtons)
                button.Active.BindValueChanged(_ => updateCriteria());

            sortDropdown.Current.BindValueChanged(_ => updateCriteria());
            groupDropdown.Current.BindValueChanged(_ =>
            {
                updateSortDropdownState();
                updateCriteria();
            });
            collectionDropdown.Current.BindValueChanged(v =>
            {
                // The hope would be that this never arrives here, but due to bindings receiving changes before
                // local ValueChanged events, that's not the case (see https://github.com/ppy/osu-framework/pull/1545).
                if (v.NewValue is ManageCollectionsFilterMenuItem || v.OldValue is ManageCollectionsFilterMenuItem)
                    return;

                updateCriteria();
            });
            collectionsSubscription = realm.RegisterForNotifications(r => r.All<BeatmapCollection>(), (collections, changeSet) =>
            {
                if (changeSet != null && groupDropdown.Current.Value == GroupMode.Collections)
                    updateCriteria();
            });

            localUser.BindValueChanged(_ => updateCriteria());
            localUserFavouriteBeatmapSets.BindCollectionChanged((_, _) => updateCriteria());
            ScopedBeatmapSet.BindValueChanged(_ => updateCriteria(clearScopedSet: false));

            updateCriteria();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            collectionsSubscription?.Dispose();
        }

        /// <summary>
        /// Creates a <see cref="FilterCriteria"/> based on the current state of the controls.
        /// </summary>
        public FilterCriteria CreateCriteria()
        {
            string query = searchTextBox.Current.Value;
            bool isValidUser = localUser.Value.Id > 1;

            var criteria = new FilterCriteria
            {
                SelectedBeatmapSet = ScopedBeatmapSet.Value,
                Sort = sortDropdown.Current.Value,
                Group = groupDropdown.Current.Value,
                AllowConvertedBeatmaps = showConvertedBeatmaps.Value,
                Ruleset = ruleset.Value,
                Mods = mods.Value,
                CollectionBeatmapMD5Hashes = collectionDropdown.Current.Value?.Collection?.PerformRead(c => c.BeatmapMD5Hashes).ToImmutableHashSet(),
                LocalUserId = isValidUser ? localUser.Value.Id : null,
                LocalUserUsername = isValidUser ? localUser.Value.Username : null,
            };

            bool isBmsRuleset = usingBmsSpecificFilters();
            string effectiveQuery = query;

            if (!isBmsRuleset)
            {
                if (!difficultyRangeSlider.LowerBound.IsDefault)
                    criteria.UserStarDifficulty.Min = difficultyRangeSlider.LowerBound.Value;

                if (!difficultyRangeSlider.UpperBound.IsDefault)
                    criteria.UserStarDifficulty.Max = difficultyRangeSlider.UpperBound.Value;
            }
            else
            {
                effectiveQuery = appendQuery(query, createBmsVisualFilterQuery());
            }

            criteria.RulesetCriteria = ruleset.Value.CreateInstance().CreateRulesetFilterCriteria();

            FilterQueryParser.ApplyQueries(criteria, effectiveQuery);

            ApplyRequiredCriteria?.Invoke(criteria);

            return criteria;
        }

        private void updateCriteria(bool clearScopedSet = true)
        {
            if (clearScopedSet && ScopedBeatmapSet.Value != null)
            {
                songSelect?.UnscopeBeatmapSet();
                // because `ScopedBeatmapSet` has a value change callback bound to it that calls `updateCriteria()` again,
                // we can just do nothing other than clear it to avoid extra work and duplicated `CriteriaChanged` invocations
                return;
            }

            currentCriteria = CreateCriteria();
            CriteriaChanged?.Invoke(currentCriteria);
        }

        /// <summary>
        /// Set the query to the search text box.
        /// </summary>
        /// <param name="query">The string to search.</param>
        public void Search(string query)
        {
            searchTextBox.Current.Value = query;
        }

        private Drawable createStandardFilters()
        {
            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Shear = -OsuGame.SHEAR,
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Content = new[]
                {
                    new[]
                    {
                        difficultyRangeSlider = new DifficultyRangeSlider
                        {
                            RelativeSizeAxes = Axes.X,
                            MinRange = 0.1f,
                        },
                        Empty(),
                        standardShowConvertedBeatmapsButton = createShowConvertedBeatmapsButton(),
                    },
                }
            };
        }

        private Drawable createBmsFilters()
        {
            return new FillFlowContainer<Drawable>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0f, 5f),
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Shear = -OsuGame.SHEAR,
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 5),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new[]
                            {
                                bmsCompositionFilter = new BmsCompositionFilterControl
                                {
                                    RelativeSizeAxes = Axes.X,
                                },
                                Empty(),
                                bmsShowConvertedBeatmapsButton = createShowConvertedBeatmapsButton(),
                            },
                        }
                    },
                    new FillFlowContainer<Drawable>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Shear = -OsuGame.SHEAR,
                        Spacing = new Vector2(5f, 0f),
                        Children = new Drawable[]
                        {
                            createBmsKeyCountButton(5),
                            createBmsKeyCountButton(7),
                            createBmsKeyCountButton(9),
                            createBmsKeyCountButton(14),
                        }
                    },
                }
            };
        }

        private ShearedToggleButton createShowConvertedBeatmapsButton() => new ShearedToggleButton
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = UserInterfaceStrings.ShowConverts,
            Height = 30f,
        };

        private static Container createRulesetSpecificFiltersContainer(Drawable child) => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Masking = true,
            Child = child,
        };

        private BmsKeyCountToggleButton createBmsKeyCountButton(int keyCount)
        {
            var button = new BmsKeyCountToggleButton(keyCount)
            {
                Active =
                {
                    Value = true,
                    Default = true,
                }
            };

            bmsKeyCountButtons.Add(button);
            return button;
        }

        private void updateRulesetSpecificFilters()
        {
            bool useBmsFilters = usingBmsSpecificFilters();

            setRulesetSpecificFilterVisibility(standardFiltersContainer, !useBmsFilters);
            setRulesetSpecificFilterVisibility(bmsFiltersContainer, useBmsFilters);
        }

        private static void setRulesetSpecificFilterVisibility(Container container, bool visible)
        {
            container.Alpha = visible ? 1 : 0;

            if (visible)
            {
                container.AutoSizeAxes = Axes.Y;
            }
            else
            {
                container.AutoSizeAxes = Axes.None;
                container.Height = 0;
            }
        }

        private bool usingBmsSpecificFilters() => ruleset.Value.ShortName == bms_ruleset_short_name;

        private IEnumerable<int> getSelectedBmsKeyCounts() => bmsKeyCountButtons.Where(button => button.Active.Value).Select(button => button.KeyCount);

        private string createBmsVisualFilterQuery()
        {
            var queryParts = new List<string>();
            var selectedKeyCounts = getSelectedBmsKeyCounts().ToArray();

            if (selectedKeyCounts.Length != bmsKeyCountButtons.Count)
                queryParts.Add($"keys={(selectedKeyCounts.Length == 0 ? "0" : string.Join(',', selectedKeyCounts))}");

            appendUpperBoundQuery(queryParts, bmsCompositionFilter.RegularSegment);
            appendUpperBoundQuery(queryParts, bmsCompositionFilter.LongNoteSegment);
            appendUpperBoundQuery(queryParts, bmsCompositionFilter.ScratchSegment);

            return string.Join(' ', queryParts);
        }

        private static void appendUpperBoundQuery(List<string> queryParts, BmsCompositionFilterControl.BmsCompositionSegment segment)
        {
            if (!segment.Enabled.Value || segment.UpperBound.IsDefault)
                return;

            queryParts.Add($"{segment.CompositionKey}<={segment.UpperBound.Value:0.#}");
        }

        private static string appendQuery(string textQuery, string visualQuery)
        {
            if (string.IsNullOrWhiteSpace(visualQuery))
                return textQuery;

            if (string.IsNullOrWhiteSpace(textQuery))
                return visualQuery;

            return $"{textQuery} {visualQuery}";
        }

        private bool updateAvailableGroupingModes()
        {
            var availableModes = ruleset.Value.CreateInstance().GetAvailableSongSelectGroupModes().ToArray();
            groupDropdown.Items = availableModes;

            if (availableModes.Contains(groupDropdown.Current.Value))
                return false;

            groupDropdown.Current.Value = availableModes.First();
            return true;
        }

        private bool updateAvailableSortingModes()
        {
            var availableModes = ruleset.Value.CreateInstance().GetAvailableSongSelectSortModes().ToArray();
            sortDropdown.Items = availableModes;

            if (availableModes.Contains(sortDropdown.Current.Value))
                return false;

            sortDropdown.Current.Value = availableModes.First();
            return true;
        }

        private void updateSortDropdownState()
        {
            // DifficultyTable grouping uses hierarchical groups (table → level) for structure,
            // but still allows user to choose how beatmaps within each level are sorted.
            sortDropdown.Current.Disabled = false;

            if (sortModeBeforeLockedGrouping.HasValue)
            {
                sortDropdown.Current.Value = sortModeBeforeLockedGrouping.Value;
                sortModeBeforeLockedGrouping = null;
            }
        }

        protected override void PopIn()
        {
            this.MoveToX(0, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeIn(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(150, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeOut(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        internal partial class SongSelectSearchTextBox : ShearedFilterTextBox
        {
            public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

            protected override InnerSearchTextBox CreateInnerTextBox() => new InnerTextBox
            {
                ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
            };

            private partial class InnerTextBox : InnerFilterTextBox
            {
                public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

                public override bool HandleLeftRightArrows => false;

                public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
                {
                    if (e.Action == GlobalAction.Back && ScopedBeatmapSet.Value != null)
                        return false;

                    return base.OnPressed(e);
                }

                public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
                {
                    // Conflicts with default group navigation keys (shift-left shift-right).
                    if (e.Action == PlatformAction.SelectBackwardChar || e.Action == PlatformAction.SelectForwardChar)
                        return false;

                    // the "cut" platform key binding (shift-delete) conflicts with the beatmap deletion action.
                    if (e.Action == PlatformAction.Cut && e.ShiftPressed && e.CurrentState.Keyboard.Keys.IsPressed(Key.Delete))
                        return false;

                    return base.OnPressed(e);
                }
            }
        }

        public partial class BmsCompositionFilterControl : CompositeDrawable
        {
            private const float track_height = 30f;
            private const double default_upper_bound = 15;

            public BmsCompositionSegment RegularSegment { get; } = new BmsCompositionSegment("RC", "rc", default_upper_bound, new Color4(255, 119, 86, 255));
            public BmsCompositionSegment LongNoteSegment { get; } = new BmsCompositionSegment("LN", "ln", default_upper_bound, new Color4(94, 190, 255, 255));
            public BmsCompositionSegment ScratchSegment { get; } = new BmsCompositionSegment("SCR", "scr", default_upper_bound, new Color4(255, 212, 92, 255));

            public IEnumerable<BmsCompositionSegment> Segments => segments;

            private readonly BmsCompositionSegment[] segments;
            private readonly Dictionary<BmsCompositionSegment, BmsCompositionSegmentDrawable> segmentDrawables = new Dictionary<BmsCompositionSegment, BmsCompositionSegmentDrawable>();
            private readonly Dictionary<BmsCompositionSegment, BmsCompositionHandle> segmentHandles = new Dictionary<BmsCompositionSegment, BmsCompositionHandle>();

            private readonly Box trackBackground;
            private readonly Container segmentLayer;
            private readonly Container handleLayer;
            private readonly CompositionValueTextBox valueEditor;

            private BmsCompositionSegment? editingSegment;
            private bool layoutInvalid = true;
            private float lastDrawWidth = -1;

            public BmsCompositionFilterControl()
            {
                RelativeSizeAxes = Axes.X;
                Height = track_height;

                segments = new[]
                {
                    RegularSegment,
                    LongNoteSegment,
                    ScratchSegment,
                };

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Shear = OsuGame.SHEAR,
                        Masking = true,
                        CornerRadius = 5,
                        BorderThickness = 2,
                        BorderColour = Color4.White.Opacity(0.08f),
                        Children = new Drawable[]
                        {
                            trackBackground = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            segmentLayer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        }
                    },
                    handleLayer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    valueEditor = new CompositionValueTextBox
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.Centre,
                        Width = 64,
                        Height = 28,
                        Alpha = 0,
                        AlwaysPresent = true,
                        CommitAction = commitEditor,
                        FocusLostAction = hideEditor,
                    },
                };

                foreach (var segment in segments)
                {
                    var drawable = new BmsCompositionSegmentDrawable(segment)
                    {
                        EditRequested = beginEditing,
                    };

                    segmentLayer.Add(drawable);
                    segmentDrawables.Add(segment, drawable);

                    var handle = new BmsCompositionHandle
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopCentre,
                        Clicked = () => beginEditing(segment),
                        Dragged = progress => dragSegment(segment, progress),
                    };

                    handleLayer.Add(handle);
                    segmentHandles.Add(segment, handle);

                    segment.Enabled.BindValueChanged(_ => requestLayout());
                    segment.UpperBound.BindValueChanged(_ => requestLayout());
                }
            }

            [BackgroundDependencyLoader(true)]
            private void load(OverlayColourProvider? colourProvider)
            {
                trackBackground.Colour = colourProvider?.Background5 ?? Color4.Black.Opacity(0.5f);
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (layoutInvalid || Math.Abs(lastDrawWidth - DrawWidth) > 0.01f)
                    updateLayout();
            }

            private void beginEditing(BmsCompositionSegment segment)
            {
                editingSegment = segment;
                valueEditor.Current.Value = Math.Round(segment.UpperBound.Value).ToString("0");
                positionEditor(segment);
                valueEditor.Alpha = 1;
                valueEditor.SelectAll();
                GetContainingFocusManager()?.ChangeFocus(valueEditor);
            }

            private void commitEditor()
            {
                if (editingSegment == null)
                    return;

                if (double.TryParse(valueEditor.Current.Value, out double parsedValue))
                    setUpperBound(editingSegment, parsedValue);

                hideEditor();
            }

            private void hideEditor()
            {
                if (valueEditor.Alpha == 0)
                    return;

                editingSegment = null;
                valueEditor.Alpha = 0;
            }

            private void dragSegment(BmsCompositionSegment segment, float progress)
            {
                hideEditor();

                double previousTotal = getPreviousTotal(segment);
                double desiredEnd = Math.Clamp(progress * 100d, previousTotal, previousTotal + getMaximumAvailable(segment));

                setUpperBound(segment, desiredEnd - previousTotal);
            }

            private void setUpperBound(BmsCompositionSegment segment, double value)
            {
                double clamped = Math.Clamp(Math.Round(value), 0, getMaximumAvailable(segment));

                if (Math.Abs(segment.UpperBound.Value - clamped) > 0.01d)
                    segment.UpperBound.Value = clamped;
            }

            private double getPreviousTotal(BmsCompositionSegment segment)
            {
                double total = 0;

                foreach (var candidate in segments)
                {
                    if (candidate == segment)
                        break;

                    total += candidate.UpperBound.Value;
                }

                return total;
            }

            private double getMaximumAvailable(BmsCompositionSegment segment) => 100 - segments.Where(candidate => candidate != segment).Sum(candidate => candidate.UpperBound.Value);

            private void requestLayout() => layoutInvalid = true;

            private void updateLayout()
            {
                lastDrawWidth = DrawWidth;
                layoutInvalid = false;

                double previousTotal = 0;

                foreach (var segment in segments)
                {
                    float segmentX = (float)(previousTotal / 100d * DrawWidth);
                    float segmentWidth = (float)(segment.UpperBound.Value / 100d * DrawWidth);

                    var drawable = segmentDrawables[segment];
                    drawable.X = segmentX;
                    drawable.Width = segmentWidth;
                    drawable.Height = DrawHeight;
                    drawable.UpdateDisplay(segmentWidth);

                    previousTotal += segment.UpperBound.Value;

                    var handle = segmentHandles[segment];
                    handle.Position = new Vector2((float)(previousTotal / 100d * DrawWidth), DrawHeight / 2f);
                }

                if (editingSegment != null)
                    positionEditor(editingSegment);
            }

            private void positionEditor(BmsCompositionSegment segment)
            {
                double previousTotal = getPreviousTotal(segment);
                double centrePercentage = previousTotal + segment.UpperBound.Value / 2;
                float centreX = (float)(centrePercentage / 100d * DrawWidth);
                float halfEditorWidth = valueEditor.Width / 2f;

                if (DrawWidth > 0)
                    centreX = Math.Clamp(centreX, halfEditorWidth, DrawWidth - halfEditorWidth);

                valueEditor.Position = new Vector2(centreX, DrawHeight / 2f);
            }

            public sealed class BmsCompositionSegment
            {
                public string Label { get; }

                public string CompositionKey { get; }

                public Color4 AccentColour { get; }

                public BindableBool Enabled { get; } = new BindableBool
                {
                    Value = true,
                    Default = true,
                };

                public BindableDouble UpperBound { get; }

                public BmsCompositionSegment(string label, string compositionKey, double defaultUpperBound, Color4 accentColour)
                {
                    Label = label;
                    CompositionKey = compositionKey;
                    AccentColour = accentColour;

                    UpperBound = new BindableDouble(defaultUpperBound)
                    {
                        Default = defaultUpperBound,
                        Value = defaultUpperBound,
                        MinValue = 0,
                        MaxValue = 100,
                        Precision = 1,
                    };
                }
            }

            private partial class BmsCompositionSegmentDrawable : CompositeDrawable, IHasTooltip
            {
                private readonly BmsCompositionSegment segment;
                private readonly Box fill;
                private readonly Box separator;
                private readonly Container content;
                private readonly Container toggleContainer;
                private readonly Box toggleFill;
                private readonly OsuSpriteText labelText;
                private readonly OsuSpriteText valueText;

                public Action<BmsCompositionSegment>? EditRequested { get; init; }

                public LocalisableString TooltipText => segment.Enabled.Value
                    ? $"{segment.Label} <= {segment.UpperBound.Value:0.#}%"
                    : $"{segment.Label} disabled (saved {segment.UpperBound.Value:0.#}%)";

                public BmsCompositionSegmentDrawable(BmsCompositionSegment segment)
                {
                    this.segment = segment;

                    RelativeSizeAxes = Axes.Y;
                    Masking = true;
                    AlwaysPresent = true;

                    InternalChildren = new Drawable[]
                    {
                        new OsuClickableContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Action = () => EditRequested?.Invoke(segment),
                            Children = new Drawable[]
                            {
                                fill = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                separator = new Box
                                {
                                    RelativeSizeAxes = Axes.Y,
                                    Width = 1,
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Alpha = 0.18f,
                                },
                                content = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Shear = -OsuGame.SHEAR,
                                    Children = new Drawable[]
                                    {
                                        new OsuClickableContainer
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Position = new Vector2(5f, 0f),
                                            Size = new Vector2(18f, 18f),
                                            Action = () => segment.Enabled.Value = !segment.Enabled.Value,
                                            Child = toggleContainer = new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Masking = true,
                                                CornerRadius = 4,
                                                BorderThickness = 1.5f,
                                                Child = toggleFill = new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Alpha = 0,
                                                },
                                            },
                                        },
                                        labelText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Position = new Vector2(29f, 0f),
                                            Font = OsuFont.TorusAlternate.With(size: 14, weight: FontWeight.Bold),
                                            Text = segment.Label,
                                        },
                                        valueText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Font = OsuFont.TorusAlternate.With(size: 14, weight: FontWeight.Bold),
                                        },
                                    }
                                },
                            }
                        },
                    };

                    segment.Enabled.BindValueChanged(_ => updateState());
                    segment.UpperBound.BindValueChanged(_ => updateState());
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    updateState();
                }

                public void UpdateDisplay(float width)
                {
                    Alpha = width > 0 ? 1 : 0.25f;
                    labelText.Alpha = width >= 44 ? 1 : 0;
                    valueText.Alpha = width >= 84 ? 1 : 0;
                    toggleContainer.Alpha = width >= 24 ? 1 : 0;
                }

                private void updateState()
                {
                    bool enabled = segment.Enabled.Value;
                    Color4 baseColour = enabled ? segment.AccentColour : segment.AccentColour.Darken(0.5f);

                    fill.Colour = baseColour;
                    fill.Alpha = enabled ? 0.78f : 0.22f;
                    separator.Colour = Color4.White;
                    toggleContainer.BorderColour = Color4.White.Opacity(enabled ? 0.9f : 0.25f);
                    toggleFill.Colour = Color4.White;
                    toggleFill.Alpha = enabled ? 0.8f : 0.06f;
                    labelText.Colour = Color4.White.Opacity(enabled ? 1f : 0.55f);
                    valueText.Colour = Color4.White.Opacity(enabled ? 1f : 0.45f);
                    valueText.Text = $"{segment.UpperBound.Value:0}%";
                }
            }

            private partial class BmsCompositionHandle : CompositeDrawable
            {
                public Action? Clicked { get; init; }

                public Action<float>? Dragged { get; init; }

                private readonly ShearedNub nub;

                public BmsCompositionHandle()
                {
                    Size = new Vector2(ShearedNub.EXPANDED_SIZE, track_height);

                    InternalChild = nub = new ShearedNub
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Current = { Value = true },
                    };
                }

                protected override bool OnHover(HoverEvent e)
                {
                    nub.Glowing = true;
                    return true;
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    nub.Glowing = false;
                    base.OnHoverLost(e);
                }

                protected override bool OnClick(ClickEvent e)
                {
                    Clicked?.Invoke();
                    return true;
                }

                protected override bool OnDragStart(DragStartEvent e)
                {
                    nub.Glowing = true;
                    return true;
                }

                protected override void OnDrag(DragEvent e)
                {
                    if (Dragged == null || Parent == null || Parent.DrawWidth <= 0)
                        return;

                    float progress = Math.Clamp(Parent.ToLocalSpace(e.ScreenSpaceMousePosition).X / Parent.DrawWidth, 0, 1);
                    Dragged(progress);
                }

                protected override void OnDragEnd(DragEndEvent e)
                {
                    nub.Glowing = IsHovered;
                    base.OnDragEnd(e);
                }
            }

            private partial class CompositionValueTextBox : OsuNumberBox
            {
                public Action? CommitAction { get; init; }

                public Action? FocusLostAction { get; init; }

                public CompositionValueTextBox()
                {
                    LengthLimit = 3;
                    ReleaseFocusOnCommit = true;
                    SelectAllOnFocus = true;
                }

                protected override void OnTextCommitted(bool textChanged)
                {
                    base.OnTextCommitted(textChanged);
                    CommitAction?.Invoke();
                }

                protected override void OnFocusLost(FocusLostEvent e)
                {
                    base.OnFocusLost(e);
                    FocusLostAction?.Invoke();
                }
            }
        }

        public partial class BmsKeyCountToggleButton : ShearedToggleButton
        {
            public int KeyCount { get; }

            public BmsKeyCountToggleButton(int keyCount)
                : base(width: 58)
            {
                KeyCount = keyCount;
                Height = 30f;
                Text = $"{keyCount}K";
            }
        }
    }
}
