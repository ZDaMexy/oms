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
using osu.Framework.Graphics.Effects;
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
        private static readonly LocalisableString bms_composition_label = "谱面构成";
        private static readonly LocalisableString bms_key_count_label = "键数";

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
        private void load(IAPIProvider api, BeatmapManager beatmapManager, RealmAccess realmAccess)
        {
            // Ensure ruleset-specific one-time setup (e.g. stats backfill initialisation) runs before
            // the first filter operation fires in SongSelect.LoadComplete(). Also re-runs if the user
            // switches rulesets while on song select.
            ruleset.BindValueChanged(r => r.NewValue?.CreateInstance().OnSongSelectSetup(beatmapManager, realmAccess, () =>
            {
                // Called from the background backfill task every ~100 computations.
                // Schedule on the game thread so updateCriteria() runs safely.
                if (!IsDisposed)
                    Scheduler.AddOnce(() => updateCriteria());
            }), true);
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

            foreach (var row in bmsCompositionFilter.Rows)
            {
                row.Enabled.BindValueChanged(_ => updateCriteria());
                row.LowerBound.BindValueChanged(_ => updateCriteria());
                row.UpperBound.BindValueChanged(_ => updateCriteria());
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
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 5),
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 5),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new[]
                            {
                                createRulesetFilterLabel(bms_composition_label),
                                Empty(),
                                bmsCompositionFilter = new BmsCompositionFilterControl
                                {
                                    RelativeSizeAxes = Axes.X,
                                },
                                Empty(),
                                bmsShowConvertedBeatmapsButton = createShowConvertedBeatmapsButton(),
                            },
                        }
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Shear = -OsuGame.SHEAR,
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 5),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new[]
                            {
                                createRulesetFilterLabel(bms_key_count_label),
                                Empty(),
                                new FillFlowContainer<Drawable>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5f, 0f),
                                    Children = new Drawable[]
                                    {
                                        createBmsKeyCountButton(5),
                                        createBmsKeyCountButton(7),
                                        createBmsKeyCountButton(9),
                                        createBmsKeyCountButton(14),
                                    }
                                },
                            },
                        }
                    },
                }
            };
        }

        private static Drawable createRulesetFilterLabel(LocalisableString text) => new RulesetFilterLabel(text);

        // A non-static label component so it can resolve OverlayColourProvider and match the
        // visual weight of the sort/group/collection dropdowns (Background3 background).
        private partial class RulesetFilterLabel : CompositeDrawable
        {
            private readonly LocalisableString labelText;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public RulesetFilterLabel(LocalisableString labelText)
            {
                this.labelText = labelText;
                AutoSizeAxes = Axes.X;
                Height = ShearedNub.HEIGHT;
                Masking = true;
                CornerRadius = 5f;
                Shear = OsuGame.SHEAR;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = labelText,
                        Shear = -OsuGame.SHEAR,
                        Margin = new MarginPadding { Horizontal = 12, Vertical = 5 },
                        Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                    },
                };
            }
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

            appendRangeQuery(queryParts, bmsCompositionFilter.RegularRow);
            appendRangeQuery(queryParts, bmsCompositionFilter.LongNoteRow);
            appendRangeQuery(queryParts, bmsCompositionFilter.ScratchRow);

            return string.Join(' ', queryParts);
        }

        private static void appendRangeQuery(List<string> queryParts, BmsCompositionFilterControl.BmsCompositionRow row)
        {
            if (!row.Enabled.Value)
                return;

            if (row.LowerBound.Value > 0)
                queryParts.Add($"{row.QueryKey}>={row.LowerBound.Value:0.#}");

            if (row.UpperBound.Value < 100)
                queryParts.Add($"{row.QueryKey}<={row.UpperBound.Value:0.#}");
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

        internal partial class SongSelectSearchTextBox : ShearedFilterTextBox, IHasCustomTooltip<bool>
        {
            public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            // Show the hint tooltip only when the search box is empty.
            bool IHasCustomTooltip<bool>.TooltipContent => string.IsNullOrEmpty(Current.Value);

            // Pass colourProvider from this (song-select DI scope) to the tooltip
            // so the tooltip does not need to resolve it from the global tooltip-layer scope.
            ITooltip<bool> IHasCustomTooltip<bool>.GetCustomTooltip() => new SearchHintTooltip(colourProvider);

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

        /// <summary>
        /// Custom tooltip shown when hovering over the empty search box.
        /// Displays all supported search syntax fields and operators.
        /// </summary>
        private partial class SearchHintTooltip : VisibilityContainer, ITooltip<bool>
        {
            private bool shouldShow;
            private readonly OverlayColourProvider colourProvider;

            // OverlayColourProvider is passed from SongSelectSearchTextBox (song-select DI scope)
            // because the tooltip layer sits at a global level where it is not registered.
            public SearchHintTooltip(OverlayColourProvider colourProvider)
            {
                this.colourProvider = colourProvider;
                AutoSizeAxes = Axes.Both;
                Alpha = 0;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Masking = true;
                CornerRadius = 7f;
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = Color4.Black.Opacity(0.3f),
                    Radius = 10f,
                };

                // BMS section accent: blue to match the RC colour in the composition filter.
                var bmsAccent = new Color4(94, 190, 255, 255);

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                        Alpha = 0.93f,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(12f),
                        Spacing = new Vector2(0f, 7f),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = "搜索语法",
                                Font = OsuFont.Torus.With(size: 13f, weight: FontWeight.Bold),
                                Colour = colourProvider.Content1,
                            },
                            createSection(colourProvider, "数字字段  ( = != < > <= >= )",
                                ("stars / sr", "星级"),
                                ("bpm", "BPM"),
                                ("length", "时长（秒）"),
                                ("ar  /  od  /  hp  /  cs", "AR · OD · 扣血 · 圆圈大小")),
                            createSection(colourProvider, "文本字段  ( =包含  !=排除 )",
                                ("artist  /  title  /  diff", "曲目 / 标题 / 难度名"),
                                ("creator / mapper", "谱师"),
                                ("source  /  tag", "来源 / 标签"),
                                ("status", "ranked · loved · pending · graveyard"),
                                ("played", "true / false  （是否已游玩）")),
                            createBmsSection(colourProvider, bmsAccent),
                        }
                    },
                };
            }

            private static Drawable createSection(
                OverlayColourProvider cp,
                string header,
                params (string keyword, string description)[] rows)
            {
                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, 4f),
                };

                flow.Add(new OsuSpriteText
                {
                    Text = header,
                    Font = OsuFont.Torus.With(size: 11f, weight: FontWeight.SemiBold),
                    Colour = cp.Content2,
                });

                foreach (var (keyword, description) in rows)
                    flow.Add(makeRow(keyword, cp.Content1, description, cp.Content2, OsuFont.Torus.With(size: 12f, weight: FontWeight.SemiBold)));

                return flow;
            }

            private static Drawable createBmsSection(OverlayColourProvider cp, Color4 accent)
            {
                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, 4f),
                };

                // Header row with coloured left-bar marker.
                flow.Add(new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6f, 0f),
                    Children = new Drawable[]
                    {
                        new Box { Width = 3f, RelativeSizeAxes = Axes.Y, Colour = accent },
                        new OsuSpriteText
                        {
                            Text = "BMS 专属字段",
                            Font = OsuFont.Torus.With(size: 11f, weight: FontWeight.SemiBold),
                            Colour = accent,
                        },
                    }
                });

                var bmsRows = new[]
                {
                    ("keys  /  key", "键数，多选用逗号  keys=5,7,9"),
                    ("rc  /  regular", "单点比例 %  （0–100）"),
                    ("ln", "长条比例 %"),
                    ("scr  /  scratch", "转盘比例 %"),
                };

                foreach (var (keyword, description) in bmsRows)
                    flow.Add(makeRow(keyword, accent, description, cp.Content2, OsuFont.Torus.With(size: 12f, weight: FontWeight.SemiBold)));

                return flow;
            }

            // A two-column keyword/description row using a fixed-width Container for alignment.
            // Avoids GridContainer + AutoSizeAxes.Both which can be unstable with absolute column dimensions.
            private static Drawable makeRow(
                string keyword, Color4 keywordColour,
                string description, Color4 descriptionColour,
                FontUsage font)
            {
                return new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Width = 160f,
                            AutoSizeAxes = Axes.Y,
                            Child = new OsuSpriteText
                            {
                                Text = $"  {keyword}",
                                Font = font,
                                Colour = keywordColour,
                            },
                        },
                        new OsuSpriteText
                        {
                            Text = description,
                            Font = font.With(weight: FontWeight.Regular),
                            Colour = descriptionColour,
                        },
                    },
                };
            }

            public void SetContent(bool show)
            {
                shouldShow = show;

                if (!show)
                    this.FadeOut(120);
                else if (IsPresent)
                    this.FadeIn(200, Easing.OutQuint);
            }

            protected override void PopIn()
            {
                if (shouldShow)
                    this.FadeIn(300, Easing.OutQuint);
            }

            protected override void PopOut() => this.FadeOut(250, Easing.OutQuint);

            public void Move(Vector2 pos) => Position = pos;
        }

        public partial class BmsCompositionFilterControl : CompositeDrawable
        {
            private const float row_height = 28f;
            private const float row_spacing = 3f;
            private const float button_width = 56f;
            private const float button_gap = 5f;

            // Half the nub container width; used to inset handle positions so they
            // don't overlap the toggle buttons or neighbouring UI at the 0%/100% extremes.
            private const float handle_half_width = ShearedNub.EXPANDED_SIZE / 2f;

            public BmsCompositionRow RegularRow { get; } = new BmsCompositionRow("RC", "rc", new Color4(94, 190, 255, 255));
            public BmsCompositionRow LongNoteRow { get; } = new BmsCompositionRow("LN", "ln", new Color4(255, 212, 92, 255));
            public BmsCompositionRow ScratchRow { get; } = new BmsCompositionRow("SCR", "scr", new Color4(255, 119, 86, 255));

            public IEnumerable<BmsCompositionRow> Rows { get; }

            private readonly Dictionary<BmsCompositionRow, BmsCompositionRangeSlider> rowSliders = new Dictionary<BmsCompositionRow, BmsCompositionRangeSlider>();

            internal Drawable GetMinHandleDrawable(BmsCompositionRow row) => rowSliders[row].MinHandle;
            internal Drawable GetMaxHandleDrawable(BmsCompositionRow row) => rowSliders[row].MaxHandle;

            internal Vector2 GetTrackScreenSpacePosition(BmsCompositionRow row, float progress)
                => rowSliders[row].GetTrackScreenSpacePosition(progress);

            public BmsCompositionFilterControl()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                var rows = new[] { RegularRow, LongNoteRow, ScratchRow };
                Rows = rows;

                var flowContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, row_spacing),
                };

                foreach (var row in rows)
                {
                    var slider = new BmsCompositionRangeSlider(row);
                    rowSliders[row] = slider;

                    flowContainer.Add(new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = row_height,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, button_width),
                            new Dimension(GridSizeMode.Absolute, button_gap),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new BmsCompositionRowButton(row),
                                Empty(),
                                slider,
                            }
                        }
                    });
                }

                InternalChild = flowContainer;
            }

            public sealed class BmsCompositionRow
            {
                public string Label { get; }
                public string QueryKey { get; }
                public Color4 AccentColour { get; }

                public BindableBool Enabled { get; } = new BindableBool(false);
                public BindableDouble LowerBound { get; } = new BindableDouble(0) { MinValue = 0, MaxValue = 100, Precision = 1 };
                public BindableDouble UpperBound { get; } = new BindableDouble(100) { MinValue = 0, MaxValue = 100, Precision = 1 };

                public BmsCompositionRow(string label, string queryKey, Color4 accentColour)
                {
                    Label = label;
                    QueryKey = queryKey;
                    AccentColour = accentColour;
                }
            }

            private partial class BmsCompositionRangeSlider : CompositeDrawable
            {
                private readonly BmsCompositionRow row;
                private readonly Box fill;
                private bool layoutInvalid = true;
                private float lastDrawWidth = -1;

                public BmsCompositionHandle MinHandle { get; }
                public BmsCompositionHandle MaxHandle { get; }

                /// <summary>
                /// Returns the screen-space position corresponding to a logical 0..1 progress
                /// within the inset track range.
                /// </summary>
                public Vector2 GetTrackScreenSpacePosition(float progress)
                {
                    float trackWidth = Math.Max(0, DrawWidth - handle_half_width * 2);
                    float x = handle_half_width + progress * trackWidth;
                    return new Vector2(
                        ScreenSpaceDrawQuad.TopLeft.X + x,
                        ScreenSpaceDrawQuad.Centre.Y);
                }

                public BmsCompositionRangeSlider(BmsCompositionRow row)
                {
                    this.row = row;
                    RelativeSizeAxes = Axes.Both;

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
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black.Opacity(0.5f),
                                },
                                fill = new Box
                                {
                                    RelativeSizeAxes = Axes.Y,
                                    X = 0,
                                    Width = 0,
                                },
                            }
                        },
                        MinHandle = new BmsCompositionHandle(row, row.LowerBound)
                        {
                            Origin = Anchor.Centre,
                            Dragged = dragMin,
                        },
                        MaxHandle = new BmsCompositionHandle(row, row.UpperBound)
                        {
                            Origin = Anchor.Centre,
                            Dragged = dragMax,
                        },
                    };

                    row.Enabled.BindValueChanged(_ => requestLayout());
                    row.LowerBound.BindValueChanged(_ => requestLayout());
                    row.UpperBound.BindValueChanged(_ => requestLayout());
                }

                protected override void UpdateAfterChildren()
                {
                    base.UpdateAfterChildren();

                    if (layoutInvalid || Math.Abs(lastDrawWidth - DrawWidth) > 0.01f)
                        updateLayout();
                }

                private void updateLayout()
                {
                    lastDrawWidth = DrawWidth;
                    layoutInvalid = false;

                    float w = DrawWidth;
                    float h = DrawHeight;
                    float trackWidth = Math.Max(0, w - handle_half_width * 2);
                    bool enabled = row.Enabled.Value;
                    Color4 accentColour = row.AccentColour;

                    float minX = handle_half_width + (float)(row.LowerBound.Value / 100.0 * trackWidth);
                    float maxX = handle_half_width + (float)(row.UpperBound.Value / 100.0 * trackWidth);

                    fill.X = minX;
                    fill.Width = Math.Max(0, maxX - minX);
                    fill.Colour = enabled ? accentColour.Opacity(0.74f) : accentColour.Darken(0.5f).Opacity(0.18f);

                    MinHandle.Position = new Vector2(minX, h / 2);
                    MaxHandle.Position = new Vector2(maxX, h / 2);
                }

                /// <summary>
                /// Converts a raw 0..1 drag progress (across the full slider width) to the
                /// logical 0..1 progress within the inset track range.
                /// </summary>
                private float toLogicalProgress(float rawProgress)
                {
                    if (DrawWidth <= 0) return 0;

                    float inset = handle_half_width / DrawWidth;
                    float range = 1 - 2 * inset;
                    return range > 0 ? Math.Clamp((rawProgress - inset) / range, 0, 1) : 0;
                }

                private void dragMin(float rawProgress)
                {
                    row.Enabled.Value = true;
                    double newVal = Math.Round(Math.Clamp(toLogicalProgress(rawProgress) * 100.0, 0, row.UpperBound.Value - 1));
                    row.LowerBound.Value = newVal;
                }

                private void dragMax(float rawProgress)
                {
                    row.Enabled.Value = true;
                    double newVal = Math.Round(Math.Clamp(toLogicalProgress(rawProgress) * 100.0, row.LowerBound.Value + 1, 100));
                    row.UpperBound.Value = newVal;
                }

                private void requestLayout() => layoutInvalid = true;
            }

            private partial class BmsCompositionHandle : CompositeDrawable
            {
                public Action<float>? Dragged { get; init; }

                private readonly BmsCompositionRow row;
                private readonly BindableDouble boundValue;
                private readonly ShearedNub nub;
                private OsuSpriteText valueText = null!;

                public BmsCompositionHandle(BmsCompositionRow row, BindableDouble boundValue)
                {
                    this.row = row;
                    this.boundValue = boundValue;
                    Size = new Vector2(ShearedNub.EXPANDED_SIZE, row_height);

                    InternalChildren = new Drawable[]
                    {
                        nub = new ShearedNub
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Scale = new Vector2(0.72f),
                            Current = { Value = true },
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Shear = -OsuGame.SHEAR,
                            Child = valueText = new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Font = OsuFont.TorusAlternate.With(size: 10, weight: FontWeight.Bold),
                                Colour = Color4.White,
                            },
                        },
                    };
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    row.Enabled.BindValueChanged(_ => updateNubState(), true);
                    boundValue.BindValueChanged(v => valueText.Text = $"{v.NewValue:0}%", true);
                }

                private void updateNubState()
                {
                    bool enabled = row.Enabled.Value;
                    Color4 colour = enabled ? row.AccentColour : row.AccentColour.Darken(0.6f);
                    nub.AccentColour = colour;
                    nub.GlowColour = colour;
                    nub.GlowingAccentColour = colour.Lighten(0.3f);
                    nub.Current.Value = enabled;
                    nub.Alpha = enabled ? 1 : 0.65f;
                    valueText.Alpha = enabled ? 1 : 0.7f;
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

            private partial class BmsCompositionRowButton : ShearedToggleButton
            {
                private readonly BmsCompositionRow row;

                public BmsCompositionRowButton(BmsCompositionRow row)
                    : base(width: button_width)
                {
                    this.row = row;
                    Height = row_height;
                    TextSize = 13;
                    Active.BindTo(row.Enabled);
                    Text = row.Label;
                }

                protected override void UpdateActiveState()
                {
                    if (Active.Value)
                    {
                        DarkerColour = row.AccentColour.Darken(0.1f);
                        LighterColour = row.AccentColour.Lighten(0.1f);
                        TextColour = OsuColour.ForegroundTextColourFor(row.AccentColour);
                    }
                    else
                    {
                        // Inactive: match the visual weight of the section labels (Background3).
                        // Background3 responds correctly to ShearedButton's Lighten(0.2f) hover effect.
                        DarkerColour = ColourProvider.Background3;
                        LighterColour = ColourProvider.Background1;
                        TextColour = ColourProvider.Content2;
                    }
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

            protected override void UpdateActiveState()
            {
                if (Active.Value)
                {
                    // Active: default ShearedToggleButton highlight colours.
                    base.UpdateActiveState();
                }
                else
                {
                    // Inactive: match Background3 so hover Lighten(0.2f) produces a visible change.
                    DarkerColour = ColourProvider.Background3;
                    LighterColour = ColourProvider.Background1;
                    TextColour = ColourProvider.Content2;
                }
            }
        }
    }
}
