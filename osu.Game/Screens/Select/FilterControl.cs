// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
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

        // The sole conversion target in OMS (BMS → mania). "Converts only" is only meaningful here.
        private const string mania_ruleset_short_name = "mania";
        private static readonly LocalisableString bms_composition_label = "谱面构成";
        private static readonly LocalisableString bms_key_count_label = "键数";

        public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

        private SongSelectSearchTextBox searchTextBox = null!;
        private readonly Bindable<ConvertedBeatmapsDisplay> convertedBeatmapsDisplay = new Bindable<ConvertedBeatmapsDisplay>();
        private FillFlowContainer<Drawable> rulesetSpecificFiltersHost = null!;
        private Container standardFiltersContainer = null!;
        private Container bmsFiltersContainer = null!;
        private Drawable standardFilters = null!;
        private Drawable bmsFilters = null!;
        private ConvertedBeatmapsDisplayButton standardShowConvertedBeatmapsButton = null!;
        private ShearedToggleButton bmsShowConvertedBeatmapsButton = null!;
        private DifficultyRangeSlider difficultyRangeSlider = null!;
        private BmsCompositionFilterControl bmsCompositionFilter = null!;
        private readonly List<BmsKeyCountToggleButton> bmsKeyCountButtons = new List<BmsKeyCountToggleButton>();
        private ShearedDropdown<SortMode> sortDropdown = null!;
        private ShearedDropdown<GroupMode> groupDropdown = null!;
        private CollectionDropdown collectionDropdown = null!;
        private SortMode? sortModeBeforeLockedGrouping;

        // BMS-only "display level" control: persisted user preference, plus a guard used while we
        // programmatically force the dropdown to a locked value (so the preference is not overwritten).
        private GridContainer sortGroupRow = null!;
        private ShearedDropdown<DisplayLevel> displayLevelDropdown = null!;
        private readonly Bindable<DisplayLevel> displayLevelSetting = new Bindable<DisplayLevel>();
        private bool suppressDisplayLevelSettingWrite;

        // The display-level dropdown sits as a 4th column between group and collection, but only for the BMS ruleset;
        // other rulesets collapse that column (and its gap) to zero width so the row matches the original layout.
        private static Dimension[] createSortGroupColumns(bool showDisplayLevel) => new[]
        {
            new Dimension(maxSize: 180),
            new Dimension(GridSizeMode.Absolute, 5),
            new Dimension(maxSize: 180),
            new Dimension(GridSizeMode.Absolute, 5),
            showDisplayLevel ? new Dimension(maxSize: 180) : new Dimension(GridSizeMode.Absolute, 0),
            new Dimension(GridSizeMode.Absolute, showDisplayLevel ? 5 : 0),
            new Dimension(),
        };

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
        private void load(IAPIProvider api, BeatmapManager beatmapManager, RealmAccess realmAccess, Storage storage, INotificationOverlay notifications)
        {
            // Ensure ruleset-specific one-time setup (e.g. stats backfill initialisation) runs before
            // the first filter operation fires in SongSelect.LoadComplete(). Also re-runs if the user
            // switches rulesets while on song select.
            ruleset.BindValueChanged(r => r.NewValue?.CreateInstance().OnSongSelectSetup(beatmapManager, realmAccess, storage, notifications, () =>
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
                        sortGroupRow = new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            Shear = -OsuGame.SHEAR,
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            // The display-level column (index 4) + its gap (index 5) are BMS-only; collapsed to 0 for other rulesets.
                            ColumnDimensions = createSortGroupColumns(showDisplayLevel: false),
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
                                    displayLevelDropdown = new ShearedDropdown<DisplayLevel>(OmsSongSelectStrings.DisplayLevel)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Items = new[] { DisplayLevel.Songs, DisplayLevel.Difficulties },
                                        Alpha = 0,
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
                        },
                        new GroupNavigationDisplay
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
            config.BindWith(OsuSetting.ConvertedBeatmapsDisplay, convertedBeatmapsDisplay);
            config.BindWith(OsuSetting.SongSelectSortingMode, sortDropdown.Current);
            config.BindWith(OsuSetting.SongSelectGroupMode, groupDropdown.Current);
            config.BindWith(OsuSetting.BmsSongSelectDisplayLevel, displayLevelSetting);

            // The standard (mania) layout exposes the full tri-state (Hidden / Shown / ConvertedOnly).
            standardShowConvertedBeatmapsButton.Current.BindTo(convertedBeatmapsDisplay);

            // The BMS layout keeps a plain two-state toggle: BMS has no converts, so "converts only" is meaningless there.
            // Mirror the shared setting onto it (active whenever converts aren't hidden) and let toggling it set Shown/Hidden,
            // guarding against the echo so the two stay in sync without feedback.
            convertedBeatmapsDisplay.BindValueChanged(e => bmsShowConvertedBeatmapsButton.Active.Value = e.NewValue != ConvertedBeatmapsDisplay.Hidden, true);
            bmsShowConvertedBeatmapsButton.Active.BindValueChanged(e =>
            {
                bool convertsAllowed = convertedBeatmapsDisplay.Value != ConvertedBeatmapsDisplay.Hidden;
                if (e.NewValue == convertsAllowed)
                    return;

                convertedBeatmapsDisplay.Value = e.NewValue ? ConvertedBeatmapsDisplay.Shown : ConvertedBeatmapsDisplay.Hidden;
            });

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
                updateDisplayLevelDropdownState();

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
            convertedBeatmapsDisplay.BindValueChanged(_ => updateCriteria());

            foreach (var row in bmsCompositionFilter.Rows)
            {
                row.Enabled.BindValueChanged(_ => updateCriteria());
                row.UpperBound.BindValueChanged(_ => updateCriteria());
            }

            foreach (var button in bmsKeyCountButtons)
                button.Active.BindValueChanged(_ => updateCriteria());

            sortDropdown.Current.BindValueChanged(_ =>
            {
                updateDisplayLevelDropdownState();
                updateCriteria();
            });
            groupDropdown.Current.BindValueChanged(_ =>
            {
                updateSortDropdownState();
                updateDisplayLevelDropdownState();
                updateCriteria();
            });
            displayLevelSetting.BindValueChanged(_ =>
            {
                // Reflect the persisted preference into the dropdown unless the current grouping/sort
                // forces it standalone (in which case the dropdown is locked to "Difficulties").
                if (!displayLevelForcedStandalone())
                {
                    suppressDisplayLevelSettingWrite = true;
                    displayLevelDropdown.Current.Value = displayLevelSetting.Value;
                    suppressDisplayLevelSettingWrite = false;
                }

                updateCriteria();
            });
            displayLevelDropdown.Current.BindValueChanged(_ =>
            {
                // Only genuine user interaction (dropdown enabled, not a programmatic sync) updates the preference.
                if (suppressDisplayLevelSettingWrite || displayLevelDropdown.Current.Disabled)
                    return;

                displayLevelSetting.Value = displayLevelDropdown.Current.Value;
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

            updateDisplayLevelDropdownState();
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

            var convertedDisplay = convertedBeatmapsDisplay.Value;

            // "Converts only" is meaningful only for a ruleset that can actually have converts (mania, the BMS→mania
            // target). For any other ruleset it would hide every native chart and empty the list, so degrade it to "shown".
            if (convertedDisplay == ConvertedBeatmapsDisplay.ConvertedOnly && ruleset.Value.ShortName != mania_ruleset_short_name)
                convertedDisplay = ConvertedBeatmapsDisplay.Shown;

            var criteria = new FilterCriteria
            {
                SelectedBeatmapSet = ScopedBeatmapSet.Value,
                Sort = sortDropdown.Current.Value,
                Group = groupDropdown.Current.Value,
                ConvertedBeatmaps = convertedDisplay,
                Ruleset = ruleset.Value,
                Mods = mods.Value,
                CollectionBeatmapMD5Hashes = collectionDropdown.Current.Value?.Collection?.PerformRead(c => c.BeatmapMD5Hashes).ToImmutableHashSet(),
                LocalUserId = isValidUser ? localUser.Value.Id : null,
                LocalUserUsername = isValidUser ? localUser.Value.Username : null,
            };

            bool isBmsRuleset = usingBmsSpecificFilters();
            string effectiveQuery = query;

            // Display level is BMS-only; other rulesets defer to the default grouping heuristic.
            criteria.DisplayLevel = isBmsRuleset ? displayLevelSetting.Value : null;

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
                        standardShowConvertedBeatmapsButton = new ConvertedBeatmapsDisplayButton
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
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

            // The display-level dropdown is a BMS-only peer of sort/group; reveal it (and reclaim its column) only for BMS.
            sortGroupRow.ColumnDimensions = createSortGroupColumns(useBmsFilters);
            displayLevelDropdown.Alpha = useBmsFilters ? 1 : 0;
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

            if (row.UpperBound.Value < 100)
                queryParts.Add($"{row.QueryKey}<={row.UpperBound.Value.ToString("0.#", CultureInfo.InvariantCulture)}");
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

        /// <summary>
        /// Whether the current grouping/sort forces difficulties to be shown standalone, in which case
        /// the display-level dropdown is locked to "Difficulties". Mirrors
        /// <see cref="BeatmapCarouselFilterGrouping.GroupingForcesStandaloneDifficulties"/> using the live control state.
        /// </summary>
        private bool displayLevelForcedStandalone()
        {
            var rulesetInstance = ruleset.Value.CreateInstance();
            var group = groupDropdown.Current.Value;
            var sort = sortDropdown.Current.Value;

            return rulesetInstance.IsSongSelectGroupingHierarchical(group)
                   || group == GroupMode.Difficulty
                   || sort == SortMode.Difficulty
                   || group == GroupMode.RankAchieved
                   || (sort == SortMode.LastPlayed && group == GroupMode.LastPlayed);
        }

        private void updateDisplayLevelDropdownState()
        {
            bool forced = displayLevelForcedStandalone();

            suppressDisplayLevelSettingWrite = true;

            // Clear Disabled before mutating Value (a disabled bindable throws on set), then re-lock if needed.
            displayLevelDropdown.Current.Disabled = false;

            if (forced)
            {
                displayLevelDropdown.Current.Value = DisplayLevel.Difficulties;
                displayLevelDropdown.Current.Disabled = true;
            }
            else
            {
                displayLevelDropdown.Current.Value = displayLevelSetting.Value;
            }

            suppressDisplayLevelSettingWrite = false;
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
                    ("rc  /  rice", "单点比例 %  （0–100）"),
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

        public partial class BmsCompositionFilterControl : CompositeDrawable, IHasTooltip
        {
            private const float row_height = 28;
            private const float handle_half_width = 8;
            public BmsCompositionRow RegularRow { get; } = new BmsCompositionRow("RC", "rc", new Color4(94, 190, 255, 255));
            public BmsCompositionRow LongNoteRow { get; } = new BmsCompositionRow("LN", "ln", new Color4(255, 212, 92, 255));
            public BmsCompositionRow ScratchRow { get; } = new BmsCompositionRow("SCR", "scr", new Color4(255, 119, 86, 255));
            public IEnumerable<BmsCompositionRow> Rows { get; }
            private readonly Container track;
            private readonly Dictionary<BmsCompositionRow, SegmentButton> entriesByRow = new Dictionary<BmsCompositionRow, SegmentButton>();
            private readonly Dictionary<BmsCompositionRow, SegmentButton> segments = new Dictionary<BmsCompositionRow, SegmentButton>();
            private readonly Dictionary<BmsCompositionRow, BoundaryHandle> handles = new Dictionary<BmsCompositionRow, BoundaryHandle>();

            public LocalisableString TooltipText => Rows.All(r => r.Enabled.Value)
                ? Rows.Sum(r => r.UpperBound.Value) < 100
                    ? "三项上限全部启用且合计不足 100%：有构成统计的谱面均不匹配。可关闭一项限制。"
                    : "三项上限全部启用且合计 100%：只匹配这一精确配比。可关闭一项限制。"
                : "RC 单点 / LN 长条 / SCR 皿：点击编辑最大占比，拖动分界调整。空白是可分配额度，不是谱面成分。";

            internal Drawable GetMaxHandleDrawable(BmsCompositionRow row) => handles[row];
            internal Drawable GetSegmentButton(BmsCompositionRow row) => entriesByRow[row];
            internal Vector2 GetTrackScreenSpacePosition(float progress)
                => track.ToScreenSpace(new Vector2(handle_half_width + progress * Math.Max(0, track.DrawWidth - 2 * handle_half_width), row_height / 2));

            public BmsCompositionFilterControl()
            {
                RelativeSizeAxes = Axes.X;
                Height = row_height;
                Rows = new[] { RegularRow, LongNoteRow, ScratchRow };
                var entries = new FillFlowContainer
                {
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.X,
                    RelativeSizeAxes = Axes.Y,
                    Spacing = new Vector2(2, 0),
                };
                track = new Container { RelativeSizeAxes = Axes.Both };
                track.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.4f) });
                foreach (var row in Rows)
                {
                    entries.Add(entriesByRow[row] = new SegmentButton(this, row) { Width = 34 });
                    track.Add(segments[row] = new SegmentButton(this, row));
                }
                foreach (var row in Rows)
                {
                    track.Add(handles[row] = new BoundaryHandle(this, row));
                    row.UpperBound.BindValueChanged(_ => updateBudget());
                }
                updateBudget();
                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Absolute, 5), new Dimension() },
                    Content = new[] { new Drawable[] { entries, Empty(), track } },
                };
            }

            private void updateBudget()
            {
                foreach (var row in Rows)
                    row.UpperBound.MaxValue = 100 - Rows.Where(other => other != row).Sum(other => other.UpperBound.Value);
            }

            internal void SetUpperBound(BmsCompositionRow row, double value)
            {
                row.UpperBound.Value = value;
                row.Enabled.Value = true;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();
                float width = Math.Max(0, track.DrawWidth - 2 * handle_half_width);
                float start = handle_half_width;
                foreach (var row in Rows)
                {
                    var segment = segments[row];
                    segment.X = start;
                    segment.Width = (float)(row.UpperBound.Value / 100 * width);
                    start += segment.Width;
                    handles[row].Position = new Vector2(start, row_height / 2);
                }
            }

            public sealed class BmsCompositionRow
            {
                public string Label { get; }
                public string QueryKey { get; }
                public Color4 AccentColour { get; }
                public BindableBool Enabled { get; } = new BindableBool(false);
                public BindableDouble UpperBound { get; } = new BindableDouble(30) { MinValue = 0, MaxValue = 100, Precision = 1 };

                public BmsCompositionRow(string label, string queryKey, Color4 accentColour)
                {
                    Label = label;
                    QueryKey = queryKey;
                    AccentColour = accentColour;
                }
            }

            private partial class SegmentButton : OsuClickableContainer, IHasPopover, IHasTooltip
            {
                private readonly BmsCompositionFilterControl owner;
                private readonly BmsCompositionRow row;
                private readonly Box fill;
                private readonly OsuSpriteText text;
                public override LocalisableString TooltipText => $"{row.Label} 最大 {row.UpperBound.Value:0}%（{(row.Enabled.Value ? "已启用" : "未启用")}），点击编辑。{owner.TooltipText}";

                public SegmentButton(BmsCompositionFilterControl owner, BmsCompositionRow row)
                {
                    this.owner = owner;
                    this.row = row;
                    Height = row_height;
                    Masking = true;
                    Action = this.ShowPopover;
                    Children = new Drawable[]
                    {
                        fill = new Box { RelativeSizeAxes = Axes.Both },
                        text = new OsuSpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.Default.With(size: 11) },
                    };
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    row.Enabled.BindValueChanged(_ => fill.Colour = row.Enabled.Value ? row.AccentColour.Opacity(0.75f) : row.AccentColour.Darken(0.65f), true);
                    row.UpperBound.BindValueChanged(_ => updateText());
                }

                protected override void UpdateAfterChildren()
                {
                    base.UpdateAfterChildren();
                    updateText();
                }

                private void updateText() => text.Text = DrawWidth >= 72 ? $"{row.Label} {row.UpperBound.Value:0}%" : row.Label;

                public Popover GetPopover() => new CompositionEditor(owner, row);
            }

            private partial class CompositionEditor : OsuPopover
            {
                public CompositionEditor(BmsCompositionFilterControl owner, BmsCompositionRow row)
                {
                    var input = new OsuTextBox { Width = 200, Height = 35, Text = row.UpperBound.Value.ToString("0", CultureInfo.InvariantCulture) };
                    var enabled = new ShearedToggleButton(200) { Text = $"启用 {row.Label} 上限", Height = 30 };
                    enabled.Active.BindTo(row.Enabled);
                    var message = new OsuSpriteText { Font = OsuFont.Default.With(size: 12) };
                    input.OnCommit += (_, _) =>
                    {
                        if (!double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                        {
                            message.Text = "请输入 0–100 的数字";
                            return;
                        }
                        owner.SetUpperBound(row, value);
                        input.Text = row.UpperBound.Value.ToString("0", CultureInfo.InvariantCulture);
                        message.Text = $"已设为 {row.UpperBound.Value:0}%（超出额度时自动限制）";
                    };
                    Child = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText { Text = $"{row.Label} 最大占比（%）", Font = OsuFont.Default.With(size: 16) },
                            input,
                            enabled,
                            message,
                            new OsuSpriteText { Text = "对已有构成统计的谱面，三项全开时：", Font = OsuFont.Default.With(size: 12) },
                            new OsuSpriteText { Text = "合计不足 100% 无匹配；", Font = OsuFont.Default.With(size: 12) },
                            new OsuSpriteText { Text = "合计 100% 只匹配精确配比。可关闭一项。", Font = OsuFont.Default.With(size: 12) },
                        },
                    };
                }
            }

            private partial class BoundaryHandle : CompositeDrawable, IHasTooltip
            {
                private readonly BmsCompositionFilterControl owner;
                private readonly BmsCompositionRow row;
                public LocalisableString TooltipText => $"拖动调整 {row.Label} 最大占比：{row.UpperBound.Value:0}%";
                public BoundaryHandle(BmsCompositionFilterControl owner, BmsCompositionRow row)
                {
                    this.owner = owner;
                    this.row = row;
                    Origin = Anchor.Centre;
                    Size = new Vector2(12, row_height);
                    InternalChild = new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(3, 22), Colour = row.AccentColour };
                }
                protected override bool OnDragStart(DragStartEvent e) => e.Button == MouseButton.Left;
                protected override void OnDrag(DragEvent e)
                {
                    float width = owner.track.DrawWidth - 2 * handle_half_width;
                    if (width <= 0)
                        return;
                    double preceding = owner.Rows.TakeWhile(r => r != row).Sum(r => r.UpperBound.Value);
                    double end = (owner.track.ToLocalSpace(e.ScreenSpaceMousePosition).X - handle_half_width) / width * 100;
                    owner.SetUpperBound(row, Math.Round(end - preceding));
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
