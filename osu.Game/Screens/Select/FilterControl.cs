// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select.Filter;
using osuTK;
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
        private BmsCompositionRangeSlider regularCompositionRangeSlider = null!;
        private BmsCompositionRangeSlider longNoteCompositionRangeSlider = null!;
        private BmsCompositionRangeSlider scratchCompositionRangeSlider = null!;
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
            regularCompositionRangeSlider.LowerBound.BindValueChanged(_ => updateCriteria());
            regularCompositionRangeSlider.UpperBound.BindValueChanged(_ => updateCriteria());
            longNoteCompositionRangeSlider.LowerBound.BindValueChanged(_ => updateCriteria());
            longNoteCompositionRangeSlider.UpperBound.BindValueChanged(_ => updateCriteria());
            scratchCompositionRangeSlider.LowerBound.BindValueChanged(_ => updateCriteria());
            scratchCompositionRangeSlider.UpperBound.BindValueChanged(_ => updateCriteria());

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
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 5),
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 5),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new[]
                            {
                                regularCompositionRangeSlider = new BmsCompositionRangeSlider("RC", "rc"),
                                Empty(),
                                longNoteCompositionRangeSlider = new BmsCompositionRangeSlider("LN", "ln"),
                                Empty(),
                                scratchCompositionRangeSlider = new BmsCompositionRangeSlider("SCR", "scr"),
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

        private static FilterCriteria.OptionalRange<float> createOptionalRange(BmsCompositionRangeSlider slider) => new FilterCriteria.OptionalRange<float>
        {
            Min = slider.LowerBound.IsDefault ? null : (float)slider.LowerBound.Value,
            Max = slider.UpperBound.IsDefault ? null : (float)slider.UpperBound.Value,
            IsLowerInclusive = true,
            IsUpperInclusive = true,
        };

        private string createBmsVisualFilterQuery()
        {
            var queryParts = new List<string>();
            var selectedKeyCounts = getSelectedBmsKeyCounts().ToArray();

            if (selectedKeyCounts.Length != bmsKeyCountButtons.Count)
                queryParts.Add($"keys={(selectedKeyCounts.Length == 0 ? "0" : string.Join(',', selectedKeyCounts))}");

            appendRangeQuery(queryParts, "rc", regularCompositionRangeSlider);
            appendRangeQuery(queryParts, "ln", longNoteCompositionRangeSlider);
            appendRangeQuery(queryParts, "scr", scratchCompositionRangeSlider);

            return string.Join(' ', queryParts);
        }

        private static void appendRangeQuery(List<string> queryParts, string key, BmsCompositionRangeSlider slider)
        {
            var range = createOptionalRange(slider);

            if (range.Min != null)
                queryParts.Add($"{key}>={range.Min.Value:0.#}");

            if (range.Max != null)
                queryParts.Add($"{key}<={range.Max.Value:0.#}");
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

        public partial class BmsCompositionRangeSlider : ShearedRangeSlider
        {
            public string CompositionKey { get; }

            public BmsCompositionRangeSlider(string label, string compositionKey)
                : base(label)
            {
                CompositionKey = compositionKey;
                RelativeSizeAxes = Axes.X;
                MinRange = 0f;
                NubWidth = ShearedNub.HEIGHT * 1.05f;
                DefaultStringLowerBound = "0";
                DefaultStringUpperBound = "100";

                LowerBound = new BindableDouble(0)
                {
                    Default = 0,
                    Value = 0,
                    MinValue = 0,
                    MaxValue = 100,
                    Precision = 1,
                };

                UpperBound = new BindableDouble(100)
                {
                    Default = 100,
                    Value = 100,
                    MinValue = 0,
                    MaxValue = 100,
                    Precision = 1,
                };
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
