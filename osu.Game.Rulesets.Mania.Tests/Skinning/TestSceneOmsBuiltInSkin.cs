// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Argon;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.Skinning.Oms;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.ClicksPerSecond;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    [TestFixture]
    [HeadlessTest]
    public partial class TestSceneOmsBuiltInSkin : OsuTestScene
    {
        [Cached]
        private readonly GameplaySkinLayoutRevisionOwner compatibilityLayoutOwner = GameplaySkinLayoutRevisionOwner.CreateCompatibility();

        [Cached(Type = typeof(IScrollingInfo))]
        private readonly TestScrollingInfo scrollingInfo = new TestScrollingInfo();

        [Cached]
        private readonly ScoreProcessor scoreProcessor = new ScoreProcessor(new ManiaRuleset());

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private OmsSkin? historicalOmsSkinInstance;

        // These retained component regressions intentionally exercise the pre-C7 provider, not the product default.
        private OmsSkin historicalOmsSkin => historicalOmsSkinInstance ??= new OmsSkin(skinManager);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            historicalOmsSkinInstance?.Dispose();
        }

        public TestSceneOmsBuiltInSkin()
        {
            scrollingInfo.Direction.Value = ScrollingDirection.Down;
        }
        [SetUp]
        public void SetUp() => Schedule(removeAllUserSkins);

        [Test]
        public void TestOmsBuiltInSkinIsRegisteredAndProvidesResources()
        {
            Skin skin = null!;
            int selectableProtectedSkinCount = 0;

            AddStep("load OMS skin", () =>
            {
                var skins = skinManager.GetAllUsableSkins();
                selectableProtectedSkinCount = skins.Count(s => s.PerformRead(info => info.Protected));

                Assert.That(skins.First().ID, Is.EqualTo(SkinInfo.OMS_SKIN));

                var skinInfo = skins.Single(s => s.ID == SkinInfo.OMS_SKIN);
                skin = skinInfo.PerformRead(skinManager.GetSkin);
            });

            AddAssert("is OMS skin", () => CanonicalSkinPackage.IsCanonicalSkin(skin) && ReferenceEquals(skin, skinManager.DefaultOmsSkin));
            AddAssert("is protected", () => skin.SkinInfo.PerformRead(s => s.Protected));
            AddAssert("OMS is only selectable built-in skin", () => selectableProtectedSkinCount == 1);
            AddAssert("has the actual package stage texture", () => canonicalLegacyTexture(LegacyManiaSkinConfigurationLookups.LeftStageImage, null, 4) != null);
            AddAssert("has the actual package key texture", () => canonicalLegacyTexture(LegacyManiaSkinConfigurationLookups.KeyImage, 0, 4) != null);
            AddAssert("has ordinary authored gameplay declarations", () => skin.GameplaySkinDocument.Sections.SelectMany(section => section.Entries).Any(entry =>
                entry.Descriptor == GameplaySkinSlotCatalog.Note && entry.Operation == GameplaySkinDocumentOperation.Provide));
        }

        [Test]
        public void TestCanSelectOmsBuiltInSkin()
        {
            AddStep("select OMS skin", () =>
                skinManager.CurrentSkinInfo.Value = skinManager.GetAllUsableSkins().Single(s => s.ID == SkinInfo.OMS_SKIN));

            AddAssert("current skin is OMS", () => CanonicalSkinPackage.IsCanonicalSkin(skinManager.CurrentSkin.Value) && ReferenceEquals(skinManager.CurrentSkin.Value, skinManager.DefaultOmsSkin));
        }
        [Test]
        public void TestUsableSkinListContainsOmsThenUserSkins()
        {
            Live<SkinInfo> alphaSkin = null!;
            Live<SkinInfo> zuluSkin = null!;
            Live<SkinInfo>[] skins = null!;

            addImportUserSkin("Alpha Skin", imported => alphaSkin = imported);
            addImportUserSkin("Zulu Skin", imported => zuluSkin = imported);

            AddStep("query usable skins", () => skins = skinManager.GetAllUsableSkins().ToArray());

            AddAssert("OMS stays first in usable list", () => skins.First().ID == SkinInfo.OMS_SKIN);
            AddAssert("user skins follow in name order", () => skins.Skip(1).Select(s => s.ID).SequenceEqual(new[] { alphaSkin.ID, zuluSkin.ID }));
            AddAssert("upstream protected triangles skin is not exposed", () => skins.All(s => s.ID != TrianglesSkin.CreateInfo().ID));
            AddAssert("only OMS remains protected in usable list", () => skins.Count(s => s.PerformRead(info => info.Protected)) == 1);

            AddStep("clear user skins", removeAllUserSkins);
        }

        [Test]
        public void TestRandomSkinFallsBackToOmsWithoutUserSkins()
        {
            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("select random skin", () => skinManager.SelectRandomSkin());

            AddAssert("random selection falls back to OMS", () => skinManager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
            AddAssert("runtime skin remains OMS", () => CanonicalSkinPackage.IsCanonicalSkin(skinManager.CurrentSkin.Value) && ReferenceEquals(skinManager.CurrentSkin.Value, skinManager.DefaultOmsSkin));
        }

        [Test]
        public void TestRandomSkinSelectsOnlyAvailableUserSkin()
        {
            Live<SkinInfo> userSkin = null!;

            addImportUserSkin("Only User Skin", imported => userSkin = imported);
            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("select random skin", () => skinManager.SelectRandomSkin());

            AddUntilStep("random selection chooses user skin", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);
            AddAssert("runtime skin follows chosen user skin", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);

            AddStep("clear user skins", removeAllUserSkins);
        }

        [Test]
        public void TestSetSkinFromConfigurationSelectsUserSkin()
        {
            Live<SkinInfo> userSkin = null!;

            addImportUserSkin("Config User Skin", imported => userSkin = imported);
            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("set skin from user config", () => skinManager.SetSkinFromConfiguration(userSkin.ID.ToString()));
            AddUntilStep("wait for selected user package", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);

            AddAssert("current skin info follows user config", () => skinManager.CurrentSkinInfo.Value.ID == userSkin.ID);
            AddAssert("runtime skin follows user config", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);

            AddStep("clear user skins", removeAllUserSkins);
        }

        [Test]
        public void TestUnknownSkinConfigurationFallsBackToOms()
        {
            Live<SkinInfo> userSkin = null!;

            addImportUserSkin("Fallback User Skin", imported => userSkin = imported);
            AddStep("set current skin from user config", () => skinManager.SetSkinFromConfiguration(userSkin.ID.ToString()));
            AddUntilStep("wait for selected user package", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);
            AddUntilStep("user skin selected first", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);

            AddStep("set skin from invalid config string", () => skinManager.SetSkinFromConfiguration("not-a-guid"));
            AddAssert("invalid config falls back to OMS", () => skinManager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);

            AddStep("set skin from missing guid", () => skinManager.SetSkinFromConfiguration(Guid.NewGuid().ToString()));
            AddAssert("missing config falls back to OMS", () => skinManager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
            AddAssert("runtime skin remains OMS after fallback", () => CanonicalSkinPackage.IsCanonicalSkin(skinManager.CurrentSkin.Value) && ReferenceEquals(skinManager.CurrentSkin.Value, skinManager.DefaultOmsSkin));

            AddStep("clear user skins", removeAllUserSkins);
        }

        [Test]
        public void TestSelectNextSkinCyclesAcrossOmsAndUserSkins()
        {
            Live<SkinInfo> alphaSkin = null!;
            Live<SkinInfo> zuluSkin = null!;

            addImportUserSkin("Alpha Skin", imported => alphaSkin = imported);
            addImportUserSkin("Zulu Skin", imported => zuluSkin = imported);

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("select next skin", () => skinManager.SelectNextSkin());
            AddUntilStep("next selects first user skin", () => skinManager.CurrentSkin.Value.SkinInfo.ID == alphaSkin.ID);

            AddStep("select next skin again", () => skinManager.SelectNextSkin());
            AddUntilStep("next selects second user skin", () => skinManager.CurrentSkin.Value.SkinInfo.ID == zuluSkin.ID);

            AddStep("select next skin third time", () => skinManager.SelectNextSkin());
            AddAssert("next wraps back to OMS", () => skinManager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);

            AddStep("clear user skins", removeAllUserSkins);
        }

        [Test]
        public void TestSelectPreviousSkinCyclesAcrossOmsAndUserSkins()
        {
            Live<SkinInfo> alphaSkin = null!;
            Live<SkinInfo> zuluSkin = null!;

            addImportUserSkin("Alpha Skin", imported => alphaSkin = imported);
            addImportUserSkin("Zulu Skin", imported => zuluSkin = imported);

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("select previous skin", () => skinManager.SelectPreviousSkin());
            AddUntilStep("previous wraps to last user skin", () => skinManager.CurrentSkin.Value.SkinInfo.ID == zuluSkin.ID);

            AddStep("select previous skin again", () => skinManager.SelectPreviousSkin());
            AddUntilStep("previous selects first user skin", () => skinManager.CurrentSkin.Value.SkinInfo.ID == alphaSkin.ID);

            AddStep("select previous skin third time", () => skinManager.SelectPreviousSkin());
            AddAssert("previous wraps back to OMS", () => skinManager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);

            AddStep("clear user skins", removeAllUserSkins);
        }

        [Test]
        public void TestAllSourcesContainsOnlyOmsWhenOmsIsCurrent()
        {
            Guid[] sourceIds = Array.Empty<Guid>();

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("capture sources", () => sourceIds = skinManager.AllSources.OfType<Skin>().Select(s => s.SkinInfo.ID).ToArray());

            AddAssert("only OMS source is exposed", () => sourceIds.SequenceEqual(new[] { SkinInfo.OMS_SKIN }));
        }

        [Test]
        public void TestAllSourcesAddsOmsFallbackBehindUserSkin()
        {
            Live<SkinInfo> userSkin = null!;
            Guid[] sourceIds = Array.Empty<Guid>();

            addImportUserSkin("Source User Skin", imported => userSkin = imported);
            AddStep("set current skin from user config", () => skinManager.SetSkinFromConfiguration(userSkin.ID.ToString()));
            AddUntilStep("wait for selected user package", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);
            AddStep("capture sources", () => sourceIds = skinManager.AllSources.OfType<Skin>().Select(s => s.SkinInfo.ID).ToArray());

            AddAssert("user skin stays first source", () => sourceIds.FirstOrDefault() == userSkin.ID);
            AddAssert("OMS stays fallback source", () => sourceIds.SequenceEqual(new[] { userSkin.ID, SkinInfo.OMS_SKIN }));

            AddStep("clear user skins", removeAllUserSkins);
        }

        [Test]
        public void TestDeletingCurrentUserSkinFallsBackToOms()
        {
            MemoryStream archive = null!;
            Task<Live<SkinInfo>>? importTask = null;
            Live<SkinInfo> userSkin = null!;
            Task<bool>? deleteTask = null;

            AddStep("import ordinary Realm skin", () =>
            {
                archive = createOrdinaryRealmSkinArchive();
                importTask = skinManager.Import(new ImportTask(archive, $"mania-current-delete-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep("wait for ordinary Realm import", () => importTask?.IsCompleted == true);
            AddStep("select imported current skin", () =>
            {
                userSkin = importTask!.GetAwaiter().GetResult();
                skinManager.CurrentSkinInfo.Value = userSkin;
            });
            AddUntilStep("user skin selected first", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);

            AddStep("delete current user skin through current mutation protocol", () =>
                deleteTask = skinManager.DeleteSkinAsync(userSkin.ID));
            AddUntilStep("wait for current delete", () => deleteTask?.IsCompleted == true);
            AddAssert("current delete succeeded", () => deleteTask!.GetAwaiter().GetResult());
            AddUntilStep("current skin falls back to OMS", () => skinManager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
            AddAssert("runtime skin falls back to OMS", () => CanonicalSkinPackage.IsCanonicalSkin(skinManager.CurrentSkin.Value) && ReferenceEquals(skinManager.CurrentSkin.Value, skinManager.DefaultOmsSkin));
            AddAssert("deleted skin leaves usable list", () => skinManager.GetAllUsableSkins().All(s => s.ID != userSkin.ID));
            AddStep("dispose import archive", () => archive.Dispose());
        }

        [Test]
        public void TestDeletingNonCurrentUserSkinKeepsCurrentUserSkin()
        {
            Live<SkinInfo> currentUserSkin = null!;
            Live<SkinInfo> otherUserSkin = null!;

            addImportUserSkin("Current User Skin", imported => currentUserSkin = imported);
            addImportUserSkin("Other User Skin", imported => otherUserSkin = imported);

            AddStep("set current skin from user config", () => skinManager.SetSkinFromConfiguration(currentUserSkin.ID.ToString()));
            AddUntilStep("wait for selected user package", () => skinManager.CurrentSkin.Value.SkinInfo.ID == currentUserSkin.ID);
            AddAssert("current user skin selected first", () => skinManager.CurrentSkinInfo.Value.ID == currentUserSkin.ID);

            Task<bool>? deleteTask = null;
            AddStep("delete non-current user skin", () => deleteTask = skinManager.DeleteSkinAsync(otherUserSkin.ID));
            AddUntilStep("non-current deletion completes", () => deleteTask?.IsCompleted == true);
            AddAssert("non-current deletion succeeds", () => deleteTask!.GetAwaiter().GetResult());
            AddAssert("current user skin remains selected", () => skinManager.CurrentSkinInfo.Value.ID == currentUserSkin.ID);
            AddAssert("runtime skin remains current user skin", () => skinManager.CurrentSkin.Value.SkinInfo.ID == currentUserSkin.ID);
            AddAssert("deleted user skin leaves usable list", () => skinManager.GetAllUsableSkins().All(s => s.ID != otherUserSkin.ID));

            AddStep("clear user skins", removeAllUserSkins);
        }

        [TestCaseSource(nameof(upstreamProtectedSkinIds))]
        public void TestUpstreamProtectedSkinIdsFallbackToOms(string skinName, Guid skinId)
        {
            AddStep($"set skin from {skinName} id", () => skinManager.SetSkinFromConfiguration(skinId.ToString()));

            AddAssert("current skin info is OMS", () => skinManager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
            AddAssert("current skin instance is OMS", () => CanonicalSkinPackage.IsCanonicalSkin(skinManager.CurrentSkin.Value) && ReferenceEquals(skinManager.CurrentSkin.Value, skinManager.DefaultOmsSkin));
        }

        [TestCaseSource(nameof(upstreamProtectedSkinIds))]
        public void TestUpstreamBuiltInSkinsAreNotRegisteredInDatabase(string skinName, Guid skinId)
        {
            AddAssert($"{skinName} built-in is absent from realm", () => skinManager.Query(s => s.ID == skinId) == null);
        }

        [Test]
        public void TestLegacyBeatmapCompatibilityFallbackUsesOmsSkin()
        {
            Drawable host = null!;
            BeatmapSkinProvidingContainer provider = null!;

            AddStep("set current skin to triangles", () => skinManager.CurrentSkinInfo.Value = TrianglesSkin.CreateInfo().ToLiveUnmanaged());

            AddStep("setup ruleset provider", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new RulesetSkinProvidingContainer(ruleset, beatmap, new LegacyResourceBeatmapSkin(renderer))
                {
                    Child = new Container(),
                });

                provider = this.ChildrenOfType<BeatmapSkinProvidingContainer>().Single();
            });

            AddUntilStep("compatibility fallback available", () => provider.AllSources.Skip(1).FirstOrDefault() != null);
            AddAssert("compatibility fallback wraps OMS skin", () => ReferenceEquals(unwrapSkin(provider.AllSources.ElementAt(1)), skinManager.DefaultOmsSkin) && CanonicalSkinPackage.IsCanonicalSkin(skinManager.DefaultOmsSkin));
            AddStep("detach compatibility provider", () => host.Expire());
            AddUntilStep("wait for compatibility provider detach", () => host.Parent == null);
        }

        [Test]
        public void TestRulesetResourcesPrecedeOmsBuiltInFallback()
        {
            Drawable host = null!;
            RulesetSkinProvidingContainer provider = null!;
            string sourceOrder = string.Empty;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load ruleset provider with OMS fallback", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                {
                    Child = new Container(),
                });
            });

            AddUntilStep("ruleset provider sources loaded", () => provider.AllSources.Count() >= 2);
            AddStep("capture source order", () => sourceOrder = string.Join(" -> ", provider.AllSources.Select(describeSkinSource)));
            AddAssert("ruleset resources precede OMS fallback", () => sourceOrder, () => Is.EqualTo("ResourceStoreBackedSkin -> ValidatedCanonicalSkin"));

            AddStep("clear ruleset provider", () => host.Expire());
        }

        [Test]
        public void TestRulesetResourcesPrecedeOmsFallbackForLegacyUserSkin()
        {
            Drawable host = null!;
            RulesetSkinProvidingContainer provider = null!;
            string sourceOrder = string.Empty;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load ruleset provider with legacy user skin", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(new EmptyLegacyUserSkin())
                {
                    Child = provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = new Container(),
                    },
                });
            });

            AddUntilStep("wrapped ruleset provider sources loaded", () => provider.AllSources.Count() >= 3);
            AddStep("capture wrapped source order", () => sourceOrder = string.Join(" -> ", provider.AllSources.Select(describeSkinSource)));
            AddAssert("ruleset resources sit between legacy user and OMS fallback", () => sourceOrder, () => Is.EqualTo("EmptyLegacyUserSkin -> ResourceStoreBackedSkin -> ValidatedCanonicalSkin"));

            AddStep("clear wrapped ruleset provider", () => host.Expire());
        }

        [Test]
        public void TestBmsOnlyUserSkinFallsBackToCanonicalNoteMaterial()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;
            BmsOnlyUserSkin userSkin = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("create BMS-only user skin", () => userSkin = new BmsOnlyUserSkin());
            AddAssert("user skin exposes BMS combo counter", () => userSkin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) is TestBmsComboCounter);

            AddStep("load BMS-only user skin note host", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(userSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                });
            });

            AddUntilStep("column host loaded", () => columnHost.IsLoaded);
            AddStep("add note under BMS-only user skin", () => columnHost.Add(new TestDrawableNote(new Note
            {
                Column = 0,
                StartTime = Time.Current,
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }));

            AddUntilStep("ordinary note piece loaded through BMS-only fallback", () => host.ChildrenOfType<LegacyNotePiece>().Any(drawable => drawable.IsLoaded));
            AddAssert("note uses the canonical package resource", () => hasCanonicalTexture(host.ChildrenOfType<LegacyNotePiece>().Single(), LegacyManiaSkinConfigurationLookups.NoteImage));
            AddAssert("historical note provider is absent", () => !host.ChildrenOfType<OmsNotePiece>().Any());
            AddAssert("BMS combo counter not used in mania note path", () => !this.ChildrenOfType<TestBmsComboCounter>().Any());
            AddStep("clear BMS-only note host", () => host.Expire());
        }

        [Test]
        public void TestMixedLayerUserSkinUsesLegacyNotePathWithoutLeakingBmsLayer()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;
            MixedLayerLegacyUserSkin userSkin = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);
            AddStep("create mixed-layer legacy+BMS skin", () => userSkin = new MixedLayerLegacyUserSkin(renderer));
            AddAssert("user skin exposes legacy mania note assets", () => userSkin.GetTexture("mania-key1") != null && userSkin.GetTexture("mania-note1") != null);
            AddAssert("user skin exposes BMS combo counter", () => userSkin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) is TestBmsComboCounter);

            AddStep("load mixed-layer user skin note host", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(userSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                });
            });

            AddUntilStep("column host loaded", () => columnHost.IsLoaded);
            AddStep("add note under mixed-layer user skin", () => columnHost.Add(new TestDrawableNote(new Note
            {
                Column = 0,
                StartTime = Time.Current,
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }));

            AddUntilStep("legacy note piece loaded through mixed-layer skin", () => this.ChildrenOfType<LegacyNotePiece>().Any(drawable => drawable.IsLoaded));
            AddAssert("mixed-layer note keeps the selected author's actual texture", () => hasTexture(host.ChildrenOfType<LegacyNotePiece>().Single(), renderer.WhitePixel));
            AddAssert("OMS note piece not used when legacy note assets exist", () => !this.ChildrenOfType<OmsNotePiece>().Any());
            AddAssert("BMS combo counter not used in mania note path", () => !this.ChildrenOfType<TestBmsComboCounter>().Any());
            AddStep("clear mixed-layer note host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutNoteAssetsFallsBackToCanonicalNoteMaterial()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load key-only legacy user skin note host", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(new KeyOnlyLegacyUserSkin(renderer))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                });
            });

            AddUntilStep("column host loaded", () => columnHost.IsLoaded);
            AddStep("add note under key-only legacy user skin", () => columnHost.Add(new TestDrawableNote(new Note
            {
                Column = 0,
                StartTime = Time.Current,
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }));

            AddUntilStep("ordinary note piece fills missing author note", () => host.ChildrenOfType<LegacyNotePiece>().Any(drawable => drawable.IsLoaded));
            AddAssert("missing note uses the canonical package resource", () => hasCanonicalTexture(host.ChildrenOfType<LegacyNotePiece>().Single(), LegacyManiaSkinConfigurationLookups.NoteImage));
            AddAssert("historical note provider is absent", () => !host.ChildrenOfType<OmsNotePiece>().Any());
            AddStep("clear key-only legacy note host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutHoldBodyAssetsFallsBackToCanonicalHoldBodyMaterial()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load key-only legacy user skin hold host", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(new KeyOnlyLegacyUserSkin(renderer))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                });
            });

            AddUntilStep("hold column host loaded", () => columnHost.IsLoaded);
            AddStep("add hold note under key-only legacy user skin", () =>
            {
                var holdNote = new HoldNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                    Duration = 500,
                };

                holdNote.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                columnHost.Add(new TestDrawableHoldNote(holdNote)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("ordinary hold body fills missing author body", () => host.ChildrenOfType<LegacyBodyPiece>().Any(drawable => drawable.IsLoaded));
            AddAssert("missing hold body uses the canonical package resource", () => hasCanonicalTexture(host.ChildrenOfType<LegacyBodyPiece>().Single(), LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage));
            AddAssert("historical hold body provider is absent", () => !host.ChildrenOfType<OmsHoldNoteBodyPiece>().Any());
            AddStep("clear key-only legacy hold host", () => host.Expire());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLegacyUserSkinWithoutJudgementAssetsUsesCanonicalOrAuthorSuppression(bool suppress)
            => addCanonicalPublicFallbackTest(GameplaySkinSlotCatalog.JudgementDisplay, suppress);

        [TestCase(false)]
        [TestCase(true)]
        public void TestLegacyUserSkinWithoutHitExplosionAssetsKeepsOptionalSuppression(bool suppress)
            => addCanonicalPublicFallbackTest(GameplaySkinSlotCatalog.HitExplosion, suppress);

        [TestCase(false)]
        [TestCase(true)]
        public void TestLegacyUserSkinWithoutComboFontUsesCanonicalOrAuthorSuppression(bool suppress)
            => addCanonicalPublicFallbackTest(GameplaySkinSlotCatalog.ComboDisplay, suppress);

        [Test]
        public void TestLegacyUserSkinWithoutBarLineConfigUsesCanonicalMaterial()
            => addCanonicalPublicFallbackTest(GameplaySkinSlotCatalog.BarLine, false);

        [Test]
        public void TestOmsSkinUsesSharedTransformerShell()
        {
            ISkin transformedSkin = null!;
            ISkin wrappedSkin = null!;
            DefaultSkinComponentsContainer globalHudShell = null!;
            DefaultSkinComponentsContainer songSelectShell = null!;
            DefaultSkinComponentsContainer resultsShell = null!;
            DefaultSkinComponentsContainer playfieldShell = null!;
            DefaultSkinComponentsContainer hudComponents = null!;

            AddStep("create OMS transformer shell", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                transformedSkin = ruleset.CreateSkinTransformer(historicalOmsSkin, beatmap)!;
                wrappedSkin = ((ISkinTransformer)transformedSkin).Skin;
                globalHudShell = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))!;
                songSelectShell = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect))!;
                resultsShell = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results))!;
                playfieldShell = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Playfield, ruleset.RulesetInfo))!;
                hudComponents = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!;
            });

            AddAssert("uses OMS transformer shell", () => transformedSkin is OmsSkinTransformer);
            AddAssert("wraps explicit mania transformer", () => wrappedSkin is ManiaOmsSkinTransformer);
            AddAssert("provides global HUD shell", () => globalHudShell is DefaultSkinComponentsContainer);
            AddAssert("provides song select shell", () => songSelectShell is DefaultSkinComponentsContainer);
            AddAssert("provides results shell", () => resultsShell is DefaultSkinComponentsContainer);
            AddAssert("provides playfield shell", () => playfieldShell is DefaultSkinComponentsContainer);
            AddAssert("uses OMS combo counter", () => hudComponents.ChildrenOfType<OmsManiaComboCounter>().Any());
            AddAssert("uses OMS stage background", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.StageBackground)) is OmsStageBackground);
            AddAssert("uses OMS stage foreground", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.StageForeground)) is OmsStageForeground);
            AddAssert("uses OMS column background", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.ColumnBackground)) is OmsColumnBackground);
            AddAssert("uses OMS key area", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.KeyArea)) is OmsKeyArea);
            AddAssert("uses OMS note piece", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.Note)) is OmsNotePiece);
            AddAssert("uses OMS hold note head piece", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteHead)) is OmsHoldNoteHeadPiece);
            AddAssert("uses OMS hold note tail piece", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteTail)) is OmsHoldNoteTailPiece);
            AddAssert("uses OMS hold note body piece", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteBody)) is OmsHoldNoteBodyPiece);
            AddAssert("uses OMS hit target", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.HitTarget)) is OmsHitTarget);
            AddAssert("uses OMS judgement piece", () => transformedSkin.GetDrawableComponent(new SkinComponentLookup<HitResult>(HitResult.Great)) is OmsManiaJudgementPiece);
            AddAssert("uses OMS hit explosion", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.HitExplosion)) is OmsHitExplosion);
            AddAssert("uses OMS bar line", () => transformedSkin.GetDrawableComponent(new ManiaSkinComponentLookup(ManiaSkinComponents.BarLine)) is OmsBarLine);
        }

        [Test]
        public void TestOmsSkinProvidesEmbeddedGlobalLayoutMetadata()
        {
            Skin skin = null!;
            SkinLayoutInfo globalHudLayout = null!;
            SkinLayoutInfo songSelectLayout = null!;
            SkinLayoutInfo resultsLayout = null!;
            SkinLayoutInfo playfieldLayout = null!;
            SerialisedDrawableInfo[] maniaPlayfieldComponents = null!;

            AddStep("load OMS embedded layout metadata", () =>
            {
                skin = historicalOmsSkin;
                globalHudLayout = skin.LayoutInfos[GlobalSkinnableContainers.MainHUDComponents];
                songSelectLayout = skin.LayoutInfos[GlobalSkinnableContainers.SongSelect];
                resultsLayout = skin.LayoutInfos[GlobalSkinnableContainers.Results];
                playfieldLayout = skin.LayoutInfos[GlobalSkinnableContainers.Playfield];

                Assert.That(playfieldLayout.TryGetDrawableInfo(new ManiaRuleset().RulesetInfo, out var components), Is.True);
                maniaPlayfieldComponents = components!;
            });

            AddAssert("exposes four global layout targets", () => skin.LayoutInfos.Count == 4);
            AddAssert("global HUD layout contains song progress", () => globalHudLayout.AllDrawables.Select(i => i.Type).Contains(typeof(DefaultSongProgress)));
            AddAssert("global HUD layout contains Argon score counter", () => globalHudLayout.AllDrawables.Select(i => i.Type).Contains(typeof(ArgonScoreCounter)));
            AddAssert("global HUD layout contains judgement counter", () => globalHudLayout.AllDrawables.Select(i => i.Type).Contains(typeof(ArgonJudgementCounterDisplay)));
            AddAssert("song select layout stays intentionally empty", () => !songSelectLayout.AllDrawables.Any());
            AddAssert("results layout stays intentionally empty", () => !resultsLayout.AllDrawables.Any());
            AddAssert("mania playfield layout contains bar hit error meter", () => maniaPlayfieldComponents.Select(i => i.Type).Contains(typeof(BarHitErrorMeter)));
            AddAssert("mania playfield layout contains accuracy counter", () => maniaPlayfieldComponents.Select(i => i.Type).Contains(typeof(ArgonAccuracyCounter)));
            AddAssert("mania playfield layout contains combo counter", () => maniaPlayfieldComponents.Select(i => i.Type).Contains(typeof(ArgonComboCounter)));
            AddAssert("mania playfield layout contains pp counter", () => maniaPlayfieldComponents.Select(i => i.Type).Contains(typeof(ArgonPerformancePointsCounter)));
            AddAssert("mania playfield layout contains cps counter", () => maniaPlayfieldComponents.Select(i => i.Type).Contains(typeof(ClicksPerSecondCounter)));
        }

        [Test]
        public void TestOmsStageShellLoads()
        {
            Drawable host = null!;

            AddStep("load OMS shell components", () =>
            {
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
                };

                var transformedSkin = new ManiaRuleset().CreateSkinTransformer(historicalOmsSkin, beatmap)!;

                Add(host = new SkinProvidingContainer(transformedSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new OmsStageBackground
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            new OmsStageForeground
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            new ColumnTestContainer(0, ManiaAction.Key1)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Width = 0.5f,
                                Child = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new OmsColumnBackground
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                        },
                                        new OmsKeyArea
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                        },
                                        new OmsHitTarget
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                        },
                                    },
                                },
                            },
                        },
                    },
                });
            });

            AddUntilStep("stage background loaded", () => this.ChildrenOfType<OmsStageBackground>().Any(drawable => drawable.IsLoaded));
            AddAssert("stage foreground loaded", () => this.ChildrenOfType<OmsStageForeground>().Any(drawable => drawable.IsLoaded));
            AddAssert("column backgrounds loaded", () => this.ChildrenOfType<OmsColumnBackground>().Any(drawable => drawable.IsLoaded));
            AddAssert("key areas loaded", () => this.ChildrenOfType<OmsKeyArea>().Any(drawable => drawable.IsLoaded));
            AddAssert("hit targets loaded", () => this.ChildrenOfType<OmsHitTarget>().Any(drawable => drawable.IsLoaded));
            AddStep("clear stage host", () => host.Expire());
        }

        [Test]
        public void TestOmsHitExplosionLoads()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("load OMS hit explosion host", () =>
            {
                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });
            });

            AddStep("add poolable hit explosion", () => columnHost.Add(new PoolableHitExplosion
            {
                RelativeSizeAxes = Axes.Both,
            }));

            AddUntilStep("OMS hit explosion loaded", () => this.ChildrenOfType<OmsHitExplosion>().Any(drawable => drawable.IsLoaded));
            AddStep("clear hit explosion host", () => host.Expire());
        }

        [Test]
        public void TestOmsNotePieceLoads()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("load OMS note host", () =>
            {
                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                columnHost.Add(new TestDrawableNote(new Note
                {
                    Column = 0,
                    StartTime = Time.Current,
                })
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS note piece loaded", () => this.ChildrenOfType<OmsNotePiece>().Any(drawable => drawable.IsLoaded));
            AddStep("clear note host", () => host.Expire());
        }

        [Test]
        public void TestOmsNotePieceUsesStageLocalNoteHeightForMixedStages()
        {
            Drawable host = null!;
            ColumnTestContainer firstColumnHost = null!;
            ColumnTestContainer secondColumnHost = null!;
            OmsNotePiece firstNotePiece = null!;
            OmsNotePiece secondNotePiece = null!;
            float expectedHeightRatio = 0;

            AddStep("load mixed-stage OMS note host", () =>
            {
                var transformedSkin = createTransformedSkin(7, 6);

                expectedHeightRatio = getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 0)
                                      / getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 7);

                Add(host = new SkinProvidingContainer(transformedSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        Direction = FillDirection.Horizontal,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            firstColumnHost = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 7, layoutStageColumns: new[] { 7, 6 })
                            {
                                Width = 80,
                                Height = 200,
                            },
                            secondColumnHost = new ColumnTestContainer(7, ManiaAction.Key1, stageColumns: 6, layoutStageColumns: new[] { 7, 6 }, layoutStageIndex: 1)
                            {
                                Width = 80,
                                Height = 200,
                            },
                        },
                    }
                });
            });

            AddUntilStep("mixed note hosts loaded", () => firstColumnHost.IsLoaded && secondColumnHost.IsLoaded);

            AddStep("add first-stage note", () => firstColumnHost.Add(new TestDrawableNote(new Note
            {
                Column = 0,
                StartTime = Time.Current,
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }));

            AddStep("add second-stage note", () => secondColumnHost.Add(new TestDrawableNote(new Note
            {
                Column = 7,
                StartTime = Time.Current,
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }));

            AddUntilStep("mixed-stage note pieces loaded", () =>
            {
                var loadedFirstPiece = firstColumnHost.ChildrenOfType<OmsNotePiece>().FirstOrDefault(drawable => drawable.IsLoaded && drawable.DrawHeight > 0);
                var loadedSecondPiece = secondColumnHost.ChildrenOfType<OmsNotePiece>().FirstOrDefault(drawable => drawable.IsLoaded && drawable.DrawHeight > 0);

                if (loadedFirstPiece == null || loadedSecondPiece == null)
                    return false;

                firstNotePiece = loadedFirstPiece;
                secondNotePiece = loadedSecondPiece;
                return true;
            });

            AddAssert("first-stage note piece keeps shorter 7K profile", () => firstNotePiece.DrawHeight < secondNotePiece.DrawHeight);
            AddAssert("mixed-stage note pieces follow stage-local note height config", () => Math.Abs(firstNotePiece.DrawHeight / secondNotePiece.DrawHeight - expectedHeightRatio) < 0.01f);
            AddStep("clear mixed-stage note host", () => host.Expire());
        }

        [Test]
        public void TestOmsNotePieceUsesOwnedScrollingDisplayState()
        {
            Drawable host = null!;
            Container? directionContainer = null;

            AddStep("load OMS note direction host", () =>
            {
                var columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                {
                    RelativeSizeAxes = Axes.Both,
                };

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost,
                });

                columnHost.Add(new TestDrawableNote(new Note
                {
                    Column = 0,
                    StartTime = Time.Current,
                })
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS note loaded for direction test", () =>
            {
                var notePiece = this.ChildrenOfType<OmsNotePiece>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (notePiece == null)
                    return false;

                directionContainer = notePiece.ChildrenOfType<Container>().FirstOrDefault(drawable => drawable.Parent == notePiece);
                return directionContainer != null;
            });

            AddAssert("note defaults to downward anchor", () => directionContainer!.Anchor, () => Is.EqualTo(Anchor.BottomCentre));
            AddAssert("note defaults to explicit bottom origin", () => directionContainer!.Origin, () => Is.EqualTo(Anchor.BottomCentre));
            AddAssert("note defaults to downward scale", () => directionContainer!.Scale == Vector2.One);

            AddStep("set scrolling upward", () => scrollingInfo.Direction.Value = ScrollingDirection.Up);
            AddUntilStep("note flips for upward scroll", () => directionContainer?.Anchor == Anchor.TopCentre);
            AddAssert("note keeps explicit bottom origin when flipped", () => directionContainer!.Origin, () => Is.EqualTo(Anchor.BottomCentre));
            AddUntilStep("note uses upward display scale", () => directionContainer?.Scale == new Vector2(1, -1));

            AddStep("set scrolling downward", () => scrollingInfo.Direction.Value = ScrollingDirection.Down);
            AddUntilStep("note resets for downward scroll", () => directionContainer?.Anchor == Anchor.BottomCentre);
            AddUntilStep("note restores downward scale", () => directionContainer?.Scale == Vector2.One);

            AddStep("clear note direction host", () => host.Expire());
        }

        [Test]
        public void TestOmsHoldNoteHeadPieceLoads()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("load OMS hold note head host", () =>
            {
                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                columnHost.Add(new TestDrawableHoldNoteHead(new HeadNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                })
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS hold note head piece loaded", () => this.ChildrenOfType<OmsHoldNoteHeadPiece>().Any(drawable => drawable.IsLoaded));
            AddStep("clear hold note head host", () => host.Expire());
        }

        [Test]
        public void TestOmsHoldNoteTailPieceLoads()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("load OMS hold note tail host", () =>
            {
                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                columnHost.Add(new TestDrawableHoldNoteTail(new TailNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                })
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS hold note tail piece loaded", () => this.ChildrenOfType<OmsHoldNoteTailPiece>().Any(drawable => drawable.IsLoaded));
            AddStep("clear hold note tail host", () => host.Expire());
        }

        [Test]
        public void TestOmsHoldNoteTailUsesInvertedScrollingDirection()
        {
            Drawable host = null!;
            Container? directionContainer = null;

            AddStep("load OMS hold note tail direction host", () =>
            {
                var columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                {
                    RelativeSizeAxes = Axes.Both,
                };

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost,
                });

                columnHost.Add(new TestDrawableHoldNoteTail(new TailNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                })
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS hold note tail loaded for direction test", () =>
            {
                var tailPiece = this.ChildrenOfType<OmsHoldNoteTailPiece>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (tailPiece == null)
                    return false;

                directionContainer = tailPiece.ChildrenOfType<Container>().FirstOrDefault(drawable => drawable.Parent == tailPiece);
                return directionContainer != null;
            });

            AddAssert("hold tail defaults to downward inverted anchor", () => directionContainer!.Anchor, () => Is.EqualTo(Anchor.TopCentre));
            AddAssert("hold tail defaults to downward inverted scale", () => directionContainer!.Scale == new Vector2(1, -1));
            AddStep("set scrolling upward", () => scrollingInfo.Direction.Value = ScrollingDirection.Up);
            AddUntilStep("hold tail flips for upward scroll", () => directionContainer?.Anchor == Anchor.BottomCentre);
            AddUntilStep("hold tail resets scale for upward scroll", () => directionContainer?.Scale == Vector2.One);
            AddStep("set scrolling downward", () => scrollingInfo.Direction.Value = ScrollingDirection.Down);
            AddUntilStep("hold tail flips for downward scroll", () => directionContainer?.Anchor == Anchor.TopCentre);
            AddUntilStep("hold tail restores inverted scale", () => directionContainer?.Scale == new Vector2(1, -1));
            AddStep("clear hold tail direction host", () => host.Expire());
        }

        [Test]
        public void TestOmsHoldNoteBodyPieceLoads()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("load OMS hold note body host", () =>
            {
                var holdNote = new HoldNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                    Duration = 500,
                };

                holdNote.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                columnHost.Add(new TestDrawableHoldNote(holdNote)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS hold note body piece loaded", () => this.ChildrenOfType<OmsHoldNoteBodyPiece>().Any(drawable => drawable.IsLoaded));
            AddStep("clear hold note body host", () => host.Expire());
        }

        [Test]
        public void TestOmsHoldNoteBodyFollowsScrollingDirection()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;
            Drawable? bodyAnimation = null;

            AddStep("load OMS hold note body direction host", () =>
            {
                var holdNote = new HoldNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                    Duration = 500,
                };

                holdNote.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                columnHost.Add(new TestDrawableHoldNote(holdNote)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS hold note body piece loaded for direction test", () =>
            {
                var bodyPiece = this.ChildrenOfType<OmsHoldNoteBodyPiece>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (bodyPiece == null)
                    return false;

                bodyAnimation = bodyPiece.ChildrenOfType<TextureAnimation>().FirstOrDefault() as Drawable
                                ?? bodyPiece.ChildrenOfType<Sprite>().FirstOrDefault();

                return bodyAnimation != null;
            });

            AddAssert("hold body defaults to downward anchor", () => bodyAnimation!.Anchor, () => Is.EqualTo(Anchor.TopCentre));
            AddAssert("hold body defaults to OMS stretch scale", () => bodyAnimation!.Scale == Vector2.One);
            AddStep("set scrolling upward", () => scrollingInfo.Direction.Value = ScrollingDirection.Up);
            AddUntilStep("hold body flips for upward scroll", () => bodyAnimation?.Anchor == Anchor.BottomCentre);
            AddUntilStep("hold body flips scale for upward scroll", () => bodyAnimation?.Scale == new Vector2(1, -1));
            AddStep("set scrolling downward", () => scrollingInfo.Direction.Value = ScrollingDirection.Down);
            AddUntilStep("hold body flips for downward scroll", () => bodyAnimation?.Anchor == Anchor.TopCentre);
            AddUntilStep("hold body resets scale for downward scroll", () => bodyAnimation?.Scale == Vector2.One);
            AddStep("clear hold body direction host", () => host.Expire());
        }

        [Test]
        public void TestOmsHoldNoteBodyDoesNotAddLegacyHitLighting()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;
            TestDrawableHoldNote drawableHoldNote = null!;
            int initialInsetContainerCount = 0;

            AddStep("load OMS hold note light host", () =>
            {
                var holdNote = new HoldNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                    Duration = 500,
                };

                holdNote.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                columnHost.Add(drawableHoldNote = new TestDrawableHoldNote(holdNote)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            AddUntilStep("OMS hold note body piece loaded for light test", () => this.ChildrenOfType<OmsHoldNoteBodyPiece>().Any(drawable => drawable.IsLoaded));
            AddStep("capture baseline inset container count", () => initialInsetContainerCount = columnHost.ChildrenOfType<HitTargetInsetContainer>().Count());
            AddStep("force hold note holding state", () => drawableHoldNote.ForceHoldingState(true));
            AddUntilStep("forced holding state applied", () => drawableHoldNote.IsHolding.Value);
            AddAssert("hold body adds no legacy hit-light container", () => columnHost.ChildrenOfType<HitTargetInsetContainer>().Count() == initialInsetContainerCount);
            AddStep("clear hold note light host", () => host.Expire());
        }

        [Test]
        public void TestOmsHoldNoteBodyDoesNotApplyLegacyMissFade()
        {
            Drawable host = null!;
            TestDrawableHoldNote drawableHoldNote = null!;
            Drawable? bodyAnimation = null;
            Color4 initialBodyColour = default;
            Color4 initialHeadColour = default;
            Color4 initialTailColour = default;

            AddStep("load OMS hold note miss-fade host", () =>
            {
                var holdNote = new HoldNote
                {
                    Column = 0,
                    StartTime = Time.Current,
                    Duration = 500,
                };

                holdNote.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = drawableHoldNote = new TestDrawableHoldNote(holdNote)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                    },
                });
            });

            AddUntilStep("OMS hold note body piece loaded for miss-fade test", () =>
            {
                var bodyPiece = this.ChildrenOfType<OmsHoldNoteBodyPiece>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (bodyPiece == null)
                    return false;

                bodyAnimation = bodyPiece.ChildrenOfType<TextureAnimation>().FirstOrDefault() as Drawable
                                ?? bodyPiece.ChildrenOfType<Sprite>().FirstOrDefault();

                return bodyAnimation != null && drawableHoldNote.Head.IsLoaded && drawableHoldNote.Tail.IsLoaded;
            });

            AddStep("capture initial hold colours", () =>
            {
                initialBodyColour = bodyAnimation!.Colour;
                initialHeadColour = drawableHoldNote.Head.Colour;
                initialTailColour = drawableHoldNote.Tail.Colour;
            });

            AddStep("force hold body miss", () => drawableHoldNote.TestBody.ForceMissForTesting());
            AddUntilStep("forced hold body miss applied", () => drawableHoldNote.TestBody.HasHoldBreak);
            AddAssert("hold body keeps body colour after miss", () => coloursMatch(bodyAnimation!.Colour, initialBodyColour));
            AddAssert("hold body keeps head colour after miss", () => coloursMatch(drawableHoldNote.Head.Colour, initialHeadColour));
            AddAssert("hold body keeps tail colour after miss", () => coloursMatch(drawableHoldNote.Tail.Colour, initialTailColour));
            AddStep("clear hold note miss-fade host", () => host.Expire());
        }

        [Test]
        public void TestOmsJudgementPieceLoads()
        {
            Drawable host = null!;

            AddStep("load OMS judgement host", () =>
            {
                var judgement = new DrawableManiaJudgement
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                judgement.Apply(new JudgementResult(new HitObject { StartTime = Time.Current }, new Judgement())
                {
                    Type = HitResult.Great,
                }, null);

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 5, useSkinGeometry: true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = judgement,
                    },
                });
            });

            AddUntilStep("OMS judgement piece loaded", () => this.ChildrenOfType<OmsManiaJudgementPiece>().Any(drawable => drawable.IsLoaded));
            AddStep("clear judgement host", () => host.Expire());
        }

        [Test]
        public void TestOmsBarLineLoads()
        {
            Drawable host = null!;

            AddStep("load OMS bar line host", () =>
            {
                Add(host = new SkinProvidingContainer(createTransformedSkin(9))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 9, useSkinGeometry: true)
                    {
                        Child = new DrawableBarLine(new BarLine { StartTime = Time.Current })
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                            Width = 1f,
                        },
                    },
                });
            });

            AddUntilStep("OMS bar line loaded", () => this.ChildrenOfType<OmsBarLine>().Any(drawable => drawable.IsLoaded));
            AddStep("clear bar line host", () => host.Expire());
        }

        [Test]
        public void TestOmsComboCounterLoads()
        {
            Drawable host = null!;
            DefaultSkinComponentsContainer hudComponents = null!;

            AddStep("load OMS combo counter host", () =>
            {
                var transformedSkin = createTransformedSkin(5);
                hudComponents = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new ManiaRuleset().RulesetInfo))!;

                foreach (var drawable in hudComponents.Children.Where(drawable => drawable is not OmsManiaComboCounter).ToArray())
                    hudComponents.Remove(drawable, false);

                Add(host = new SkinProvidingContainer(transformedSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = hudComponents,
                });
            });

            AddUntilStep("OMS combo counter loaded", () => this.ChildrenOfType<OmsManiaComboCounter>().Any(drawable => drawable.IsLoaded));
            AddAssert("combo counter no longer uses legacy sprite text", () => this.ChildrenOfType<OmsManiaComboCounter>().All(drawable => !drawable.ChildrenOfType<LegacySpriteText>().Any()));
            AddStep("clear combo counter host", () => host.Expire());
        }

        [Test]
        public void TestOmsJudgementPieceUsesSharedScorePositionForDualStages()
        {
            Drawable host = null!;
            OmsManiaJudgementPiece judgementPiece = null!;

            AddStep("load dual-stage OMS judgement host", () =>
            {
                var judgement = new DrawableManiaJudgement
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                judgement.Apply(new JudgementResult(new HitObject { StartTime = Time.Current }, new Judgement())
                {
                    Type = HitResult.Great,
                }, null);

                Add(host = new SkinProvidingContainer(createTransformedSkin(5, 5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 5, layoutStageColumns: new[] { 5, 5 }, useSkinGeometry: true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = judgement,
                    },
                });
            });

            AddUntilStep("dual-stage OMS judgement piece loaded", () =>
            {
                var loadedPiece = this.ChildrenOfType<OmsManiaJudgementPiece>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (loadedPiece == null)
                    return false;

                judgementPiece = loadedPiece;
                return true;
            });

            AddAssert("dual-stage judgement uses exact snapshot placement", () => judgementUsesExactSnapshotPlacement(judgementPiece));
            AddStep("clear dual-stage judgement host", () => host.Expire());
        }

        [Test]
        public void TestOmsJudgementPieceUsesFirstStageScorePositionForMixedStages()
        {
            Drawable host = null!;
            OmsManiaJudgementPiece judgementPiece = null!;

            AddStep("load mixed-stage OMS judgement host", () =>
            {
                var judgement = new DrawableManiaJudgement
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                judgement.Apply(new JudgementResult(new HitObject { StartTime = Time.Current }, new Judgement())
                {
                    Type = HitResult.Great,
                }, null);

                Add(host = new SkinProvidingContainer(createTransformedSkin(7, 6))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 7, layoutStageColumns: new[] { 7, 6 }, useSkinGeometry: true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = judgement,
                    },
                });
            });

            AddUntilStep("mixed-stage OMS judgement piece loaded", () =>
            {
                var loadedPiece = this.ChildrenOfType<OmsManiaJudgementPiece>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (loadedPiece == null)
                    return false;

                judgementPiece = loadedPiece;
                return true;
            });

            AddAssert("mixed-stage judgement uses exact first-stage snapshot placement", () => judgementUsesExactSnapshotPlacement(judgementPiece));
            AddStep("clear mixed-stage judgement host", () => host.Expire());
        }

        [Test]
        public void TestOmsSkinProvidesSharedJudgementHudPositionConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K score position uses OMS shared preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ScorePosition) - 520f) < 0.01f);
            AddAssert("5K combo position uses OMS shared preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ComboPosition) - 136f) < 0.01f);

            AddStep("create OMS 7K+6K transformer", () => transformedSkin = createTransformedSkin(7, 6));

            AddAssert("7K+6K hit position uses first-stage OMS preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HitPosition) - 8f) < 0.01f);
            AddAssert("7K+6K score position uses first-stage OMS preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ScorePosition) - 160f) < 0.01f);
            AddAssert("7K+6K combo position uses first-stage OMS preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ComboPosition) - 144f) < 0.01f);
        }

        [Test]
        public void TestOmsSkinProvidesSharedBarLineConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 7K transformer", () => transformedSkin = createTransformedSkin(7));

            AddAssert("7K bar line colour uses OMS shared preset", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.BarLineColour), new Color4(255, 255, 255, 150)));

            AddStep("create OMS 9K transformer", () => transformedSkin = createTransformedSkin(9));

            AddAssert("9K bar line height uses OMS shared preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.BarLineHeight) - 1.2f) < 0.01f);

            AddStep("create OMS 8K+9K transformer", () => transformedSkin = createTransformedSkin(8, 9));

            AddAssert("8K+9K bar line colour uses first-stage OMS preset", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.BarLineColour), new Color4(255, 255, 255, 150)));

            AddStep("create OMS 9K+8K transformer", () => transformedSkin = createTransformedSkin(9, 8));

            AddAssert("9K+8K bar line height uses first-stage OMS preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.BarLineHeight) - 1.2f) < 0.01f);
        }

        [TestCase(ManiaProgrammaticPartImplementation.Default)]
        [TestCase(ManiaProgrammaticPartImplementation.Argon)]
        [TestCase(ManiaProgrammaticPartImplementation.Oms)]
        [TestCase(ManiaProgrammaticPartImplementation.Legacy)]
        public void TestProgrammaticVisualPartsAreIndependentAndInputCannotBypassKeyFlashGate(ManiaProgrammaticPartImplementation implementation)
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;
            Drawable[] components = null!;
            ManiaGameplaySkinProgrammaticVisualPart keyFlashPart = default;
            Drawable animatedFlash = null!;

            AddStep($"load {implementation} programmatic visual parts", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                ISkin source = implementation switch
                {
                    ManiaProgrammaticPartImplementation.Argon => new ArgonSkin(skinManager),
                    ManiaProgrammaticPartImplementation.Legacy => new KeyOnlyLegacyUserSkin(renderer),
                    _ => historicalOmsSkin,
                };
                ISkin transformedSkin = ruleset.CreateSkinTransformer(source, beatmap) ?? source;

                components = implementation switch
                {
                    ManiaProgrammaticPartImplementation.Default => new Drawable[]
                    {
                        new DefaultColumnBackground(),
                        new DefaultHitTarget(),
                    },
                    ManiaProgrammaticPartImplementation.Argon => new Drawable[]
                    {
                        new ArgonColumnBackground(),
                        new ArgonHitTarget(),
                    },
                    ManiaProgrammaticPartImplementation.Oms => new Drawable[]
                    {
                        new OmsColumnBackground(),
                        new OmsHitTarget(),
                    },
                    ManiaProgrammaticPartImplementation.Legacy => new Drawable[]
                    {
                        new LegacyColumnBackground(),
                        new LegacyStageBackground(),
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(implementation), implementation, null),
                };

                Add(host = new SkinProvidingContainer(transformedSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = columnHost = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 4)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = components,
                        },
                    },
                });
            });
            AddUntilStep($"{implementation} visual parts loaded", () => components.All(component => component.IsLoaded));
            AddStep($"assert {implementation} one-key owner contract", () =>
            {
                ManiaGameplaySkinProgrammaticVisualPart[] parts = components
                    .OfType<IManiaGameplaySkinProgrammaticVisualPartProvider>()
                    .SelectMany(provider => provider.GameplaySkinProgrammaticVisualParts)
                    .ToArray();
                GameplaySkinSlotDescriptor[] expectedSlots = implementation switch
                {
                    ManiaProgrammaticPartImplementation.Default => new[]
                    {
                        GameplaySkinSlotCatalog.LaneSurface,
                        GameplaySkinSlotCatalog.KeyFlash,
                        GameplaySkinSlotCatalog.HitTarget,
                        GameplaySkinSlotCatalog.JudgementLine,
                    },
                    ManiaProgrammaticPartImplementation.Argon => new[]
                    {
                        GameplaySkinSlotCatalog.LaneSurface,
                        GameplaySkinSlotCatalog.KeyFlash,
                        GameplaySkinSlotCatalog.HitTarget,
                    },
                    ManiaProgrammaticPartImplementation.Oms => new[]
                    {
                        GameplaySkinSlotCatalog.LaneSurface,
                        GameplaySkinSlotCatalog.LaneDivider,
                        GameplaySkinSlotCatalog.HitTarget,
                        GameplaySkinSlotCatalog.JudgementLine,
                        GameplaySkinSlotCatalog.KeyFlash,
                    },
                    ManiaProgrammaticPartImplementation.Legacy => new[]
                    {
                        GameplaySkinSlotCatalog.StageBackground,
                        GameplaySkinSlotCatalog.PlayfieldBackdrop,
                        GameplaySkinSlotCatalog.PlayfieldBaseplate,
                        GameplaySkinSlotCatalog.LaneSurface,
                        GameplaySkinSlotCatalog.LaneDivider,
                        GameplaySkinSlotCatalog.HitTarget,
                        GameplaySkinSlotCatalog.JudgementLine,
                        GameplaySkinSlotCatalog.KeyFlash,
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(implementation), implementation, null),
                };

                Assert.Multiple(() =>
                {
                    Assert.That(parts.Select(part => part.Slot).Distinct(), Is.EquivalentTo(expectedSlots));
                    Assert.That(parts.Select(part => part.Owner).Distinct(ReferenceEqualityComparer.Instance).Count(), Is.EqualTo(parts.Length),
                        "One native wrapper must never be registered against multiple exact public-slot keys.");
                    Assert.That(parts.All(part => part.Owner.Parent != null), Is.True);
                    if (implementation == ManiaProgrammaticPartImplementation.Legacy)
                    {
                        Assert.That(parts.Count(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.StageBackground)), Is.EqualTo(2));
                        Assert.That(parts.Count(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop)), Is.EqualTo(1));
                        Assert.That(parts.Count(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBaseplate)), Is.EqualTo(1));
                        Assert.That(parts.Count(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneSurface)), Is.EqualTo(4));
                        Assert.That(parts.Count(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneDivider)), Is.EqualTo(4));
                        Assert.That(parts.Count(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.HitTarget)), Is.EqualTo(4));
                        Assert.That(parts.Count(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.JudgementLine)), Is.EqualTo(4));
                    }
                });

                keyFlashPart = parts.Single(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.KeyFlash));
                animatedFlash = ((Container)keyFlashPart.Owner).Children.Single();
                animatedFlash.Alpha = 0;
            });
            AddStep($"probe real {implementation} native flash input", () =>
                columnHost.ChildrenOfType<ManiaInputManager>().Single().KeyBindingContainer.TriggerPressed(ManiaAction.Key1));
            AddUntilStep($"{implementation} native flash animation ran", () => animatedFlash.Alpha > 0);
            AddStep($"release real {implementation} native flash probe", () =>
                columnHost.ChildrenOfType<ManiaInputManager>().Single().KeyBindingContainer.TriggerReleased(ManiaAction.Key1));
            AddStep($"arm {implementation} independent key-flash gate", () =>
            {
                animatedFlash.Alpha = 0;
                keyFlashPart.Owner.Alpha = 0;
            });
            AddStep($"press real gated {implementation} lane", () =>
                columnHost.ChildrenOfType<ManiaInputManager>().Single().KeyBindingContainer.TriggerPressed(ManiaAction.Key1));
            AddWaitStep($"allow {implementation} gated native animation", 5);
            AddAssert($"{implementation} key-flash gate survives press", () => keyFlashPart.Owner.Alpha == 0);
            AddStep($"release real {implementation} lane", () =>
                columnHost.ChildrenOfType<ManiaInputManager>().Single().KeyBindingContainer.TriggerReleased(ManiaAction.Key1));
            AddWaitStep($"allow {implementation} gated release animation", 5);
            AddAssert($"{implementation} key-flash gate survives release", () => keyFlashPart.Owner.Alpha == 0);
            AddStep($"restore {implementation} programmatic fallback after gate release", () => keyFlashPart.Owner.Alpha = 1);
            AddAssert($"{implementation} programmatic fallback restored", () => keyFlashPart.Owner.Alpha == 1);
            AddStep($"clear {implementation} programmatic visual parts", () => host.Expire());
        }

        [Test]
        public void TestOmsHudComboCountersUseStageLocalPositionsForDualStages()
        {
            Drawable host = null!;
            OmsManiaComboCounter[] comboCounters = null!;
            ManiaGameplayHudComponentsContainer hudComponents = null!;

            AddStep("load dual-stage OMS HUD combo", () =>
            {
                var transformedSkin = createTransformedSkin(5, 5);
                hudComponents = (ManiaGameplayHudComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new ManiaRuleset().RulesetInfo))!;

                comboCounters = hudComponents.ChildrenOfType<OmsManiaComboCounter>().ToArray();

                foreach (var drawable in hudComponents.Children.Where(drawable => drawable is not OmsManiaComboCounter).ToArray())
                    hudComponents.Remove(drawable, false);

                Add(host = new SkinProvidingContainer(transformedSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = hudComponents,
                });
            });

            AddUntilStep("dual-stage combo counters positioned", () => comboCounters.All(combo => combo.IsLoaded) && hudComponents.IsLoaded);
            AddAssert("dual-stage combos use exact stage-local compatibility surfaces", () =>
            {
                GameplaySkinLayoutSnapshot snapshot = hudComponents.LayoutSnapshot;
                GameplaySkinLayoutRect surface = hudComponents.LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect;
                return snapshot.Context.NativeContextId == "stages-5-5"
                       && comboCounters.Length == snapshot.Context.Topology.GroupsInLogicalOrder.Count
                       && comboCounters.Select((combo, stageIndex) => (combo, stageIndex)).All(pair =>
                       {
                           GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder[pair.stageIndex];
                           GameplaySkinLayoutRect groupRect = snapshot.GetGroup(group.Identity.Id).Rect;
                           return pair.combo.RelativePositionAxes == Axes.Both
                                  && Math.Abs(pair.combo.X - (groupRect.Left + groupRect.Width / 2)) < 0.001f
                                  && Math.Abs(pair.combo.Y - (surface.Top + surface.Height / 2)) < 0.001f;
                       });
            });
            AddStep("clear dual-stage HUD combo host", () => host.Expire());
        }

        [Test]
        public void TestOmsHudComboCountersUseStageLocalPositionsForMixedStages()
        {
            Drawable host = null!;
            OmsManiaComboCounter[] comboCounters = null!;
            ManiaGameplayHudComponentsContainer hudComponents = null!;

            AddStep("load mixed-stage OMS HUD combo", () =>
            {
                var transformedSkin = createTransformedSkin(7, 6);
                hudComponents = (ManiaGameplayHudComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new ManiaRuleset().RulesetInfo))!;

                comboCounters = hudComponents.ChildrenOfType<OmsManiaComboCounter>().ToArray();

                foreach (var drawable in hudComponents.Children.Where(drawable => drawable is not OmsManiaComboCounter).ToArray())
                    hudComponents.Remove(drawable, false);

                Add(host = new SkinProvidingContainer(transformedSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = hudComponents,
                });
            });

            AddUntilStep("mixed-stage combo counters positioned", () => comboCounters.All(combo => combo.IsLoaded) && hudComponents.IsLoaded);
            AddAssert("mixed-stage combos use exact stage-local compatibility surfaces", () =>
            {
                GameplaySkinLayoutSnapshot snapshot = hudComponents.LayoutSnapshot;
                GameplaySkinLayoutRect surface = hudComponents.LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect;
                return snapshot.Context.NativeContextId == "stages-7-6"
                       && comboCounters.Length == snapshot.Context.Topology.GroupsInLogicalOrder.Count
                       && comboCounters.Select((combo, stageIndex) => (combo, stageIndex)).All(pair =>
                       {
                           GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder[pair.stageIndex];
                           GameplaySkinLayoutRect groupRect = snapshot.GetGroup(group.Identity.Id).Rect;
                           return pair.combo.RelativePositionAxes == Axes.Both
                                  && Math.Abs(pair.combo.X - (groupRect.Left + groupRect.Width / 2)) < 0.001f
                                  && Math.Abs(pair.combo.Y - (surface.Top + surface.Height / 2)) < 0.001f;
                       });
            });
            AddStep("clear mixed-stage HUD combo host", () => host.Expire());
        }

        [Test]
        public void TestOmsHudComboCounterClearsImmediatelyOnBreak()
        {
            Drawable host = null!;
            OmsManiaComboCounter comboCounter = null!;

            AddStep("reset combo", () => scoreProcessor.Combo.Value = 0);

            AddStep("load OMS HUD combo", () =>
            {
                var transformedSkin = createTransformedSkin(5);
                var hudComponents = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new ManiaRuleset().RulesetInfo))!;

                comboCounter = hudComponents.ChildrenOfType<OmsManiaComboCounter>().Single();

                foreach (var drawable in hudComponents.Children.Where(drawable => drawable != comboCounter).ToArray())
                    hudComponents.Remove(drawable, false);

                Add(host = new SkinProvidingContainer(transformedSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = hudComponents,
                });
            });

            AddUntilStep("OMS HUD combo loaded", () => comboCounter.IsLoaded);
            AddAssert("combo counter uses a single OMS text node", () => comboCounter.ChildrenOfType<OsuSpriteText>().Count() == 1);

            AddStep("set combo to 12", () => scoreProcessor.Combo.Value = 12);
            AddAssert("combo display syncs immediately", () => comboCounter.DisplayedCount == 12);

            AddStep("break combo", () => scoreProcessor.Combo.Value = 0);
            AddAssert("combo break clears display immediately", () => comboCounter.DisplayedCount == 0);

            AddStep("clear OMS HUD combo host", () =>
            {
                scoreProcessor.Combo.Value = 0;
                host.Expire();
            });
        }

        [Test]
        public void TestOmsBarLineUsesSharedHeightForDualStages()
        {
            Drawable host = null!;
            OmsBarLine barLine = null!;

            AddStep("load dual-stage OMS bar line", () =>
            {
                Add(host = new SkinProvidingContainer(createTransformedSkin(9, 9))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 9, layoutStageColumns: new[] { 9, 9 }, useSkinGeometry: true)
                    {
                        Child = new DrawableBarLine(new BarLine { StartTime = Time.Current, Major = true })
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                            Width = 1f,
                        },
                    },
                });
            });

            AddUntilStep("dual-stage bar line loaded", () =>
            {
                var loadedBarLine = this.ChildrenOfType<OmsBarLine>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (loadedBarLine == null)
                    return false;

                barLine = loadedBarLine;
                return true;
            });

            AddAssert("dual-stage bar line keeps OMS shared height", () => Math.Abs(barLine.Height - 1.44f) < 0.01f);
            AddStep("clear dual-stage bar line host", () => host.Expire());
        }

        [Test]
        public void TestOmsBarLineUsesFirstStageHeightForMixedStages()
        {
            Drawable host = null!;
            OmsBarLine barLine = null!;

            AddStep("load mixed-stage OMS bar line", () =>
            {
                Add(host = new SkinProvidingContainer(createTransformedSkin(9, 8))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 9, layoutStageColumns: new[] { 9, 8 }, useSkinGeometry: true)
                    {
                        Child = new DrawableBarLine(new BarLine { StartTime = Time.Current, Major = true })
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                            Width = 1f,
                        },
                    },
                });
            });

            AddUntilStep("mixed-stage bar line loaded", () =>
            {
                var loadedBarLine = this.ChildrenOfType<OmsBarLine>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (loadedBarLine == null)
                    return false;

                barLine = loadedBarLine;
                return true;
            });

            AddAssert("mixed-stage bar line keeps first-stage OMS height", () => Math.Abs(barLine.Height - 1.44f) < 0.01f);
            AddStep("clear mixed-stage bar line host", () => host.Expire());
        }

        [Test]
        public void TestOmsBarLineRespondsToMajorState()
        {
            Drawable host = null!;
            BarLine barLineObject = null!;
            OmsBarLine barLine = null!;

            AddStep("load OMS major bar line host", () =>
            {
                barLineObject = new BarLine { StartTime = Time.Current, Major = true };

                Add(host = new SkinProvidingContainer(createTransformedSkin(9))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 9, useSkinGeometry: true)
                    {
                        Child = new DrawableBarLine(barLineObject)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                            Width = 1f,
                        },
                    },
                });
            });

            AddUntilStep("OMS major bar line loaded", () =>
            {
                var loadedBarLine = this.ChildrenOfType<OmsBarLine>().FirstOrDefault(drawable => drawable.IsLoaded);

                if (loadedBarLine == null)
                    return false;

                barLine = loadedBarLine;
                return true;
            });

            AddAssert("major bar line keeps full OMS height", () => Math.Abs(barLine.Height - 1.44f) < 0.01f);
            AddAssert("major bar line keeps full opacity", () => Math.Abs(barLine.ChildrenOfType<Box>().Single().Alpha - 1f) < 0.01f);

            AddStep("switch bar line to minor", () => barLineObject.Major = false);
            AddAssert("minor bar line reduces OMS height", () => Math.Abs(barLine.Height - 1.08f) < 0.01f);
            AddAssert("minor bar line dims OMS opacity", () => Math.Abs(barLine.ChildrenOfType<Box>().Single().Alpha - 0.65f) < 0.01f);

            AddStep("clear OMS major bar line host", () => host.Expire());
        }

        [Test]
        public void TestOmsSkinProvidesLayoutConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K hit position preset applied", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HitPosition) - 140.8f) < 0.01f);
            AddAssert("5K top padding preset applied", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.StagePaddingTop)) < 0.01f);
            AddAssert("5K bottom padding preset applied", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.StagePaddingBottom)) < 0.01f);
            AddAssert("5K widths preset applied", () =>
            {
                float[] expectedWidths = scaleLegacyWidths(46f, 40f, 46f, 40f, 46f);

                return expectedWidths.Select((expectedWidth, index) =>
                           Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnWidth, index) - expectedWidth) < 0.01f)
                       .All(matches => matches);
            });
            AddAssert("5K spacing preset applied", () =>
                Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LeftColumnSpacing, 2)) < 0.01f
                && Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.RightColumnSpacing, 2)) < 0.01f);
        }

        [Test]
        public void TestOmsSkinProvidesNoteHeightConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 4K transformer", () => transformedSkin = createTransformedSkin(4));
            AddAssert("4K note height keeps candidate override", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 0) - 60f * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR) < 0.01f);

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));
            AddAssert("5K note height falls back to stage min width", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 2) - scaleLegacyDimension(40f)) < 0.01f);

            AddStep("create OMS 7K transformer", () => transformedSkin = createTransformedSkin(7));
            AddAssert("7K note height keeps candidate override", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 3) - 35f * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR) < 0.01f);
        }

        [Test]
        public void TestOmsSkinRepeatsStagePresetForDualStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS dual 5K transformer", () => transformedSkin = createTransformedSkin(5, 5));

            AddAssert("dual 5K widths repeat per stage", () =>
            {
                float[] expectedWidths = scaleLegacyWidths(46f, 40f, 46f, 40f, 46f, 46f, 40f, 46f, 40f, 46f);

                return expectedWidths.Select((expectedWidth, index) =>
                           Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnWidth, index) - expectedWidth) < 0.01f)
                       .All(matches => matches);
            });
        }

        [Test]
        public void TestOmsSkinProvidesShellConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K judgement line preset applied", () => !getBoolConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ShowJudgementLine, 0));
            AddAssert("5K light position preset applied", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LightPosition, 0) - 107.2f) < 0.01f);
            AddAssert("5K light fps preset applied", () => getIntConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LightFramePerSecond, 0) == 24);
        }

        [Test]
        public void TestOmsSkinProvidesSharedShellAssetConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("stage left image uses OMS asset preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LeftStageImage) == "mania-stage-left");
            AddAssert("stage right image uses OMS asset preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.RightStageImage) == "mania-stage-right");
            AddAssert("stage bottom image uses OMS asset preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.BottomStageImage) == "mania-stage-bottom");
            AddAssert("hit target image uses OMS asset preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HitTargetImage) == "mania-stage-hint");
            AddAssert("light image uses OMS asset preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LightImage) == "mania-stage-light");
            AddAssert("keys stay above notes by OMS preset", () => !getBoolConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeysUnderNotes, 0));
        }

        [Test]
        public void TestOmsSkinProvidesJudgementAssetConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("300g image uses OMS judgement preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit300g) == "mania-hit300g");
            AddAssert("300 image uses OMS judgement preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit300) == "mania-hit300");
            AddAssert("200 image uses OMS judgement preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit200) == "mania-hit200");
            AddAssert("100 image uses OMS judgement preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit100) == "mania-hit100");
            AddAssert("50 image uses OMS judgement preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit50) == "mania-hit50");
            AddAssert("0 image uses OMS judgement preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit0) == "mania-hit0");
        }

        [Test]
        public void TestOmsSkinProvidesKeyImageConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 4K transformer", () => transformedSkin = createTransformedSkin(4));

            AddAssert("4K key image uses OMS key preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImage, 0) == "4k\\1");
            AddAssert("4K pressed key image uses OMS key preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImageDown, 3) == "4k\\1");

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K first key uses OMS default key asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImage, 0) == "mania-key1");
            AddAssert("5K second key uses OMS alternate key asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImage, 1) == "mania-key2");
            AddAssert("5K pressed key uses OMS default down asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImageDown, 1) == "mania-key2D");
        }

        [Test]
        public void TestOmsSkinProvidesNoteAssetConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 4K transformer", () => transformedSkin = createTransformedSkin(4));

            AddAssert("4K note image uses candidate asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.NoteImage, 0) == "mania-note1");
            AddAssert("4K hold head uses candidate asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage, 3) == "mania-note1");
            AddAssert("4K hold tail uses candidate asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteTailImage, 0) == "Notes4K\\LNBody");
            AddAssert("4K hold body uses candidate asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage, 2) == "A");

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K note image uses OMS default note asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.NoteImage, 1) == "mania-note2");
            AddAssert("5K hold head uses OMS default head asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage, 1) == "mania-note2H");
            AddAssert("5K hold tail uses OMS default tail asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteTailImage, 2) == "mania-note1T");
            AddAssert("5K hold body uses OMS default body asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage, 3) == "mania-note2L");
        }

        [Test]
        public void TestOmsSkinProvidesHoldBodySemanticConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("hold body light image uses OMS preset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteLightImage, 1) == "lightingL");
            AddAssert("hold body light scale uses OMS preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteLightScale, 1) - 1f) < 0.0001f);
            AddAssert("hold body style uses OMS stretch semantics", () => getNoteBodyStyleConfig(transformedSkin) == LegacyNoteBodyStyle.Stretch);
        }

        [Test]
        public void TestOmsSkinProvidesHitExplosionConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 4K transformer", () => transformedSkin = createTransformedSkin(4));

            AddAssert("4K hit explosion uses candidate animation", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionImage, 0) == "lightingN");
            AddAssert("4K hit explosion scale uses candidate width fallback", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 0) - scaleLegacyDimension(69f) / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K hit explosion uses OMS preset animation", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionImage, 1) == "lightingN");
            AddAssert("5K hit explosion scale uses OMS preset width", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 1) - scaleLegacyDimension(40f) / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);
        }

        [Test]
        public void TestOmsSkinProvidesShellColourConfig()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K column line colour stays white", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnLineColour), Color4.White));
            AddAssert("5K judgement line colour stays white", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.JudgementLineColour), Color4.White));
            AddAssert("5K background colour stays black", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour, 0), new Color4(0, 0, 0, 255)));
            AddAssert("5K light colour stays white", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnLightColour, 0), Color4.White));
        }

        [Test]
        public void TestOmsShellColourConfigUsesStageLocalPresetForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 8K+9K transformer", () => transformedSkin = createTransformedSkin(8, 9));

            AddAssert("8K first stage keeps black background", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour, 0), new Color4(0, 0, 0, 255)));
            AddAssert("9K second stage uses alternating background preset", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour, 9), new Color4(15, 15, 15, 255)));
            AddAssert("9K center lane uses dedicated accent background", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour, 12), new Color4(15, 15, 5, 255)));
            AddAssert("mixed-stage judgement line colour stays shared", () => coloursMatch(getColorConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.JudgementLineColour), Color4.White));
        }

        [Test]
        public void TestOmsKeyImageConfigUsesStageLocalPresetForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K+8K transformer", () => transformedSkin = createTransformedSkin(5, 8));

            AddAssert("5K first stage keeps OMS default key asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImage, 0) == "mania-key1");
            AddAssert("8K second stage uses candidate key asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImage, 5) == "7k\\0");
            AddAssert("8K second stage pressed key uses candidate down asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeyImageDown, 12) == "7k\\7p");
        }

        [Test]
        public void TestOmsNoteAssetConfigUsesStageLocalPresetForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K+9K transformer", () => transformedSkin = createTransformedSkin(5, 9));

            AddAssert("5K first stage keeps OMS default note asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.NoteImage, 0) == "mania-note1");
            AddAssert("9K second stage uses special note asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.NoteImage, 5) == "mania-noteS");
            AddAssert("9K second stage uses special head asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage, 13) == "mania-noteSH");
            AddAssert("9K second stage uses special body asset", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HoldNoteBodyImage, 13) == "mania-noteSL");
        }

        [Test]
        public void TestOmsNoteHeightConfigUsesStageLocalPresetForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 7K+6K transformer", () => transformedSkin = createTransformedSkin(7, 6));

            AddAssert("7K first stage keeps explicit note height override", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 0) - 35f * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR) < 0.01f);
            AddAssert("6K second stage falls back to its own min width", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 7) - scaleLegacyDimension(40f)) < 0.01f);
        }

        [Test]
        public void TestOmsHitExplosionConfigUsesStageLocalPresetForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K+8K transformer", () => transformedSkin = createTransformedSkin(5, 8));

            AddAssert("5K first stage keeps OMS explosion scale", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 0) - scaleLegacyDimension(46f) / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);
            AddAssert("8K second stage uses its own explosion scale", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 5) - scaleLegacyDimension(43f) / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);
            AddAssert("mixed-stage explosion image stays OMS-owned", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionImage, 5) == "lightingN");
        }

        [Test]
        public void TestOmsSharedShellAssetConfigStaysAvailableForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 7K+6K transformer", () => transformedSkin = createTransformedSkin(7, 6));

            AddAssert("mixed-stage hit target image stays OMS-owned", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.HitTargetImage) == "mania-stage-hint");
            AddAssert("mixed-stage keys-under-notes stays OMS-owned", () => !getBoolConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.KeysUnderNotes, 7));
        }

        [Test]
        public void TestOmsJudgementAssetConfigStaysSharedForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K+9K transformer", () => transformedSkin = createTransformedSkin(5, 9));

            AddAssert("mixed-stage 300g image stays OMS-owned", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit300g) == "mania-hit300g");
            AddAssert("mixed-stage miss image stays OMS-owned", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.Hit0) == "mania-hit0");
        }

        [Test]
        public void TestOmsShellConfigUsesStageLocalPresetForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 7K+6K transformer", () => transformedSkin = createTransformedSkin(7, 6));

            AddAssert("7K light position uses first stage preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LightPosition, 0) - 768f) < 0.01f);
            AddAssert("6K light position uses second stage preset", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LightPosition, 7) - 104f) < 0.01f);
        }

        [Test]
        public void TestOmsShellConfigProvidesEdgeLineWidths()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 8K transformer", () => transformedSkin = createTransformedSkin(8));

            AddAssert("8K first column keeps left edge line", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.LeftLineWidth, 0) - 1f) < 0.01f);
            AddAssert("8K middle column keeps no divider line", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.RightLineWidth, 3)) < 0.01f);
            AddAssert("8K last column keeps right edge line", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.RightLineWidth, 7) - 1f) < 0.01f);
        }

        [Test]
        public void TestOmsStageUsesLayoutConfig()
        {
            Drawable host = null!;
            Stage stage = null!;

            AddStep("load OMS 5K stage", () =>
            {
                ManiaAction action = ManiaAction.Key1;

                stage = new Stage(0, new StageDefinition(5), ref action)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    Height = 0.8f,
                };

                Add(host = new SkinProvidingContainer(createTransformedSkin(5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 5, useSkinGeometry: true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new ScrollingTestContainer(ScrollingDirection.Down)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            TimeRange = 2000,
                            Child = stage,
                        },
                    },
                });
            });

            AddUntilStep("stage loaded", () => stage.IsLoaded && stage.Columns.All(column => column.IsLoaded));
            AddAssert("stage columns project exact OMS snapshot widths", () => stageColumnsMatchSnapshot(stage));
            AddAssert("stage hit target projects exact OMS snapshot", () => stageHitTargetMatchesSnapshot(stage));
            AddStep("clear stage host", () => host.Expire());
        }

        [Test]
        public void TestOmsSecondStageUsesRepeatedStagePreset()
        {
            Drawable host = null!;
            Stage secondStage = null!;

            AddStep("load OMS dual 5K stages", () =>
            {
                ManiaAction action = ManiaAction.Key1;

                var firstStage = new Stage(0, new StageDefinition(5), ref action)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    Height = 0.8f,
                };

                secondStage = new Stage(5, new StageDefinition(5), ref action)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    Height = 0.8f,
                };

                Add(host = new SkinProvidingContainer(createTransformedSkin(5, 5))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 5, layoutStageColumns: new[] { 5, 5 }, useSkinGeometry: true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new ScrollingTestContainer(ScrollingDirection.Down)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            TimeRange = 2000,
                            Child = new FillFlowContainer
                            {
                                Direction = FillDirection.Horizontal,
                                RelativeSizeAxes = Axes.Y,
                                AutoSizeAxes = Axes.X,
                                Children = new Drawable[]
                                {
                                    firstStage,
                                    secondStage,
                                },
                            }
                        },
                    },
                });
            });

            AddUntilStep("second stage loaded", () => secondStage.IsLoaded && secondStage.Columns.All(column => column.IsLoaded));
            AddAssert("second stage projects repeated exact OMS snapshot widths", () => stageColumnsMatchSnapshot(secondStage));
            AddStep("clear dual stage host", () => host.Expire());
        }

        private static float scaleLegacyDimension(float value)
            => value * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;

        private static bool judgementUsesExactSnapshotPlacement(OmsManiaJudgementPiece judgementPiece)
        {
            GameplaySkinLayoutSnapshot snapshot = judgementPiece.LayoutSnapshot;
            GameplaySkinLayoutRect group = snapshot.GroupsInLogicalOrder[0].Rect;
            GameplaySkinLayoutRect judgement = snapshot.GetSurface(ManiaGameplaySkinLayout.JUDGEMENT_SURFACE).Rect;
            float expectedY = (judgement.Top + judgement.Height / 2 - group.Top) / group.Height;
            return judgementPiece.Anchor == Anchor.TopLeft
                   && judgementPiece.Origin == Anchor.Centre
                   && Math.Abs(judgementPiece.X - 0.5f) < 0.001f
                   && Math.Abs(judgementPiece.Y - expectedY) < 0.001f;
        }

        private static bool stageColumnsMatchSnapshot(Stage stage)
        {
            GameplaySkinLayoutGroup group = stage.LayoutSnapshot.GetGroup(stage.LayoutGroupId);
            return group.TopologyGroup.LanesInLogicalOrder.All(lane =>
            {
                float expectedWidth = stage.LayoutSnapshot.GetLane(lane.Identity.Id).Rect.Width / group.Rect.Width;
                float actualWidth = stage.Columns[lane.GroupLocalLogicalIndex].DrawWidth / stage.DrawWidth;
                return Math.Abs(actualWidth - expectedWidth) < 0.001f;
            });
        }

        private static bool stageHitTargetMatchesSnapshot(Stage stage)
        {
            GameplaySkinLayoutGroup group = stage.LayoutSnapshot.GetGroup(stage.LayoutGroupId);
            float expectedInset = ManiaGameplaySkinLayoutProjection.GetHitTargetInsetFraction(
                new ManiaGameplaySkinStageContext(stage.LayoutSnapshot, group.TopologyGroup)) * stage.DrawHeight;
            return Math.Abs(stage.Columns[0].HitObjectArea.Padding.Bottom - expectedInset) < 0.01f;
        }

        private static float[] scaleLegacyWidths(params float[] widths)
            => widths.Select(scaleLegacyDimension).ToArray();

        private ISkin createTransformedSkin(params int[] stageColumns)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(stageColumns[0]))
            {
                BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
            };

            for (int i = 1; i < stageColumns.Length; i++)
                beatmap.Stages.Add(new StageDefinition(stageColumns[i]));

            return new ManiaRuleset().CreateSkinTransformer(historicalOmsSkin, beatmap)!;
        }

        private static readonly object[] upstreamProtectedSkinIds =
        {
            new object[] { "Triangles", TrianglesSkin.CreateInfo().ID },
            new object[] { "Argon", ArgonSkin.CreateInfo().ID },
            new object[] { "ArgonPro", ArgonProSkin.CreateInfo().ID },
            new object[] { "Classic", DefaultLegacySkin.CreateInfo().ID },
            new object[] { "Retro", RetroSkin.CreateInfo().ID },
        };

        public enum ManiaProgrammaticPartImplementation
        {
            Default,
            Argon,
            Oms,
            Legacy,
        }

        private void removeAllUserSkins()
        {
            skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo;
            Realm.Write(r =>
            {
                foreach (var skin in r.All<SkinInfo>().Where(s => !s.Protected).ToArray())
                    r.Remove(skin);
            });
        }

        private void addImportUserSkin(string name, Action<Live<SkinInfo>> receive, string extraIni = "", bool includeKeyAssets = false)
        {
            MemoryStream archive = null!;
            Task<Live<SkinInfo>>? import = null;
            AddStep($"import ordinary {name}", () =>
            {
                archive = createOrdinaryRealmSkinArchive(name, extraIni, includeKeyAssets);
                import = skinManager.Import(new ImportTask(archive, $"mania-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep($"wait for {name} import", () => import?.IsCompleted == true);
            AddStep($"retain imported {name}", () =>
            {
                Live<SkinInfo> imported = import!.GetAwaiter().GetResult();
                Assert.That(imported.PerformRead(info => !info.Protected && info.Files.Any(file => file.Filename == "skin.ini")), Is.True);
                receive(imported);
                archive.Dispose();
            });
        }

        private MemoryStream createOrdinaryRealmSkinArchive(
            string name = "Mania current delete fixture", string extraIni = "", bool includeKeyAssets = false)
        {
            var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("skin.ini").Open(), new UTF8Encoding(false)))
                {
                    writer.Write($"[General]\nName: {name}\nAuthor: OMS tests\nVersion: 2.7\n" + extraIni);
                }

                if (includeKeyAssets)
                {
                    string resource = skinManager.DefaultOmsSkin.GetConfig<LegacyManiaSkinConfigurationLookup, string>(
                        new LegacyManiaSkinConfigurationLookup(5, LegacyManiaSkinConfigurationLookups.KeyImage, 0))!.Value;
                    using ZipArchive original = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, "Skins", "Canonical", "oms-simple.osk"));
                    ZipArchiveEntry source = original.GetEntry(resource + ".png")!;
                    Assert.That(source, Is.Not.Null, "Copy the actual ordinary author key image into the partial fixture.");
                    foreach (string key in new[] { "mania-key1.png", "mania-key1D.png" })
                    {
                        using Stream input = source.Open();
                        using Stream outputEntry = archive.CreateEntry(key).Open();
                        input.CopyTo(outputEntry);
                    }
                }
            }
            output.Position = 0;
            return output;
        }

        private Texture? canonicalLegacyTexture(LegacyManiaSkinConfigurationLookups lookup, int? column = 0, int columns = 5)
        {
            Skin canonical = skinManager.DefaultOmsSkin;
            if (!CanonicalSkinPackage.IsCanonicalSkin(canonical))
                throw new InvalidOperationException("The installed canonical package is unavailable.");
            string? resource = canonical.GetConfig<LegacyManiaSkinConfigurationLookup, string>(new LegacyManiaSkinConfigurationLookup(columns, lookup, column))?.Value;
            Assert.That(resource, Is.Not.Null.And.Not.Empty, "The actual package must declare this legacy resource.");
            return canonical.GetTexture(resource!);
        }

        private bool hasCanonicalTexture(Drawable drawable, LegacyManiaSkinConfigurationLookups lookup)
        {
            Texture? expected = canonicalLegacyTexture(lookup);
            return expected != null && hasTexture(drawable, expected);
        }

        private static readonly PropertyInfo texture_native = typeof(Texture).GetProperty("NativeTexture", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static bool hasSameTexture(Texture actual, Texture expected)
            => ReferenceEquals(texture_native.GetValue(actual), texture_native.GetValue(expected))
               && actual.GetTextureRect().Equals(expected.GetTextureRect());

        private static bool hasTexture(Drawable drawable, Texture expected)
            => drawable.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture is Texture actual && hasSameTexture(actual, expected));

        private void addCanonicalPublicFallbackTest(GameplaySkinSlotDescriptor slot, bool suppress)
        {
            Live<SkinInfo> userSkin = null!;
            CanonicalFallbackManiaHost host = null!;
            GameplaySkinSceneRuntimeHost scene = null!;
            string scope = slot == GameplaySkinSlotCatalog.HitExplosion ? "Lane" : slot == GameplaySkinSlotCatalog.BarLine ? "Group" : "Stage";
            string lane = scope == "Lane" ? " lane=mania.lane.column-1 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0" : "";
            string extraIni = suppress
                ? $"[GameplaySkin.Common:1]\nTarget: {scope} ruleset=mania keymode=any stage-mode=any group=mania.group.stage-1 group-logical=0 group-visual=0{lane}\n{slot.Id}: resource Suppress\n"
                : "";
            addImportUserSkin($"Missing {slot.StableName}", imported => userSkin = imported, extraIni, includeKeyAssets: true);
            AddStep("select the actual partial author package", () => skinManager.CurrentSkinInfo.Value = userSkin);
            AddUntilStep("the partial package owns current gameplay", () => skinManager.CurrentSkin.Value.SkinInfo.ID == userSkin.ID);
            AddStep("load actual mania playfield, notes, holds and public information", () => Add(host = new CanonicalFallbackManiaHost(skinManager.CurrentSkin.Value)));
            AddUntilStep("actual fallback scene is ready", () =>
            {
                scene ??= host.Drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()!;
                return scene?.IsSceneReady == true && host.Drawable.IsLoaded;
            });
            AddStep("the exact public part follows the package declaration", () =>
            {
                GameplaySkinResolvedMaterialEntry entry = scene.MaterialSet.Entries.First(candidate => candidate.Slot == slot);
                GameplaySkinSceneHostedSlot hosted = scene.HostedSlots.Single(candidate => candidate.Key.Equals(entry.Key));
                Assert.That(scene.RuntimeFaults, Is.Empty);
                Assert.That(scene.MaterialSet, Is.SameAs(host.Drawable.LayoutRevisionOwner.CurrentPublication!.MaterialSet));
                Assert.That(entry.Source.Kind, Is.EqualTo(suppress ? GameplaySkinResolvedMaterialSourceKind.SelectedPackage : GameplaySkinResolvedMaterialSourceKind.CanonicalPackage));
                Assert.That(entry.Source.ContentRevision, Is.EqualTo((suppress ? skinManager.CurrentSkin.Value : skinManager.DefaultOmsSkin).GameplaySkinDocument.Identity.ContentRevision));
                Assert.That(entry.State, Is.EqualTo(suppress || slot == GameplaySkinSlotCatalog.HitExplosion
                    ? GameplaySkinResolvedMaterialState.Suppress : GameplaySkinResolvedMaterialState.Provide));
                if (entry.State == GameplaySkinResolvedMaterialState.Suppress)
                {
                    Assert.That(hosted.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(hosted.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(hosted.AllowsProgrammaticVisual, Is.False);
                }
                else
                {
                    GameplaySkinPublicSlotMaterial material = entry.GetMaterial<GameplaySkinPublicSlotMaterial>();
                    Assert.That(material.IsProgrammaticFallback, Is.False);
                    Texture actual = material.Texture ?? throw new AssertionException("The resolved part must retain a texture.");
                    Texture original = skinManager.DefaultOmsSkin.GetTexture(material.ResourceName!)
                                       ?? throw new AssertionException("The canonical archive must contain the resolved resource.");
                    Assert.That(hasSameTexture(actual, original), Is.True);
                    Assert.That(hosted.IsReplacementReady, Is.True);
                    Assert.That(hosted.Route, Is.Not.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                }
                Assert.That(scene.MaterialSet.Entries.Where(candidate => candidate.Slot.Requirement == SkinSlotRequirement.Critical)
                    .All(candidate => candidate.State == GameplaySkinResolvedMaterialState.Provide), Is.True);
                Assert.That(scene.MaterialSet.Entries.Any(candidate => candidate.Source.Kind == GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback), Is.False);
            });
            AddStep("detach actual mania fallback host", () => host.Expire());
            AddUntilStep("all gameplay consumers detached", () => host.Parent == null);
        }

        private sealed partial class CanonicalFallbackManiaHost : SkinProvidingContainer
        {
            public DrawableManiaRuleset Drawable { get; }
            private readonly ScoreProcessor score;
            private readonly HealthProcessor health;
            private readonly GameplayClockContainer gameplayClock;
            private readonly GameplayState gameplayState;

            public CanonicalFallbackManiaHost(Skin selected)
                : base(selected)
            {
                RelativeSizeAxes = Axes.Both;
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                foreach (ManiaHitObject note in new ManiaHitObject[]
                         {
                             new Note { Column = 0, StartTime = 1_100 },
                             new HoldNote { Column = 0, StartTime = 1_200, Duration = 500 },
                         })
                {
                    note.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());
                    beatmap.HitObjects.Add(note);
                }
                score = ruleset.CreateScoreProcessor();
                health = ruleset.CreateHealthProcessor(0);
                score.ApplyBeatmap(beatmap);
                health.ApplyBeatmap(beatmap);
                gameplayState = new GameplayState(beatmap, ruleset, scoreProcessor: score, healthProcessor: health);
                gameplayClock = new GameplayClockContainer(new TrackVirtual(60_000), applyOffsets: false, requireDecoupling: false);
                gameplayClock.Seek(1_000);
                Drawable = (DrawableManiaRuleset)ruleset.CreateDrawableRulesetWith(beatmap);
                InternalChild = gameplayClock.WithChild(new RulesetSkinProvidingContainer(ruleset, beatmap, null, prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Drawable,
                });
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(score);
                dependencies.CacheAs(health);
                dependencies.CacheAs(gameplayClock);
                dependencies.CacheAs(gameplayState);
                return dependencies;
            }
        }

        private static ISkin unwrapSkin(ISkin skin)
        {
            while (skin is ISkinTransformer transformer)
                skin = transformer.Skin;

            return skin;
        }

        private static string describeSkinSource(ISkin skin)
            => skin is ResourceStoreBackedSkin
                ? nameof(ResourceStoreBackedSkin)
                : unwrapSkin(skin) is Skin raw && CanonicalSkinPackage.IsCanonicalSkin(raw)
                    ? "ValidatedCanonicalSkin"
                    : unwrapSkin(skin).GetType().Name;

        private sealed class LegacyResourceBeatmapSkin : LegacyBeatmapSkin
        {
            private readonly IRenderer renderer;

            public LegacyResourceBeatmapSkin(IRenderer renderer)
                : base(createBeatmapInfo(), null)
            {
                this.renderer = renderer;
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName == "score-0" ? renderer.WhitePixel : base.GetTexture(componentName, wrapModeS, wrapModeT);

            private static BeatmapInfo createBeatmapInfo()
            {
                var beatmapInfo = new TestBeatmap(new ManiaRuleset().RulesetInfo).BeatmapInfo;
                beatmapInfo.LocalFilePath = "test.osu";
                return beatmapInfo;
            }
        }

        private sealed class EmptyLegacyUserSkin : LegacySkin
        {
            public EmptyLegacyUserSkin()
                : base(new SkinInfo(), null, null, string.Empty)
            {
            }
        }

        private sealed class KeyOnlyLegacyUserSkin : LegacySkin
        {
            private readonly IRenderer renderer;

            public KeyOnlyLegacyUserSkin(IRenderer renderer)
                : base(new SkinInfo(), null, null, string.Empty)
            {
                this.renderer = renderer;
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName is "mania-key1" or "mania-key1D"
                    ? renderer.WhitePixel
                    : base.GetTexture(componentName, wrapModeS, wrapModeT);
        }

        private sealed class MixedLayerLegacyUserSkin : LegacySkin
        {
            private readonly IRenderer renderer;
            private readonly TestBmsComboCounter comboCounter = new TestBmsComboCounter();

            public MixedLayerLegacyUserSkin(IRenderer renderer)
                : base(new SkinInfo(name: nameof(MixedLayerLegacyUserSkin)), null, null, string.Empty)
            {
                this.renderer = renderer;
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup switch
                {
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter } => comboCounter,
                    _ => base.GetDrawableComponent(lookup),
                };

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName is "mania-key1" or "mania-key1D" or "mania-note1"
                    ? renderer.WhitePixel
                    : base.GetTexture(componentName, wrapModeS, wrapModeT);
        }

        private sealed class BmsOnlyUserSkin : Skin
        {
            private readonly TestBmsComboCounter comboCounter = new TestBmsComboCounter();

            public BmsOnlyUserSkin()
                : base(new SkinInfo(name: nameof(BmsOnlyUserSkin)), null)
            {
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup switch
                {
                    BmsSkinComponentLookup { Component: BmsSkinComponents.ComboCounter } => comboCounter,
                    _ => null,
                };

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;
        }

        private sealed partial class TestBmsComboCounter : BmsComboCounter
        {
        }

        private static float getFloatConfig(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, int? columnIndex = null)
            => skin.GetConfig<ManiaSkinConfigurationLookup, float>(new ManiaSkinConfigurationLookup(lookup, columnIndex))?.Value ?? float.NaN;

        private static bool getBoolConfig(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, int? columnIndex = null)
            => skin.GetConfig<ManiaSkinConfigurationLookup, bool>(new ManiaSkinConfigurationLookup(lookup, columnIndex))?.Value ?? false;

        private static int getIntConfig(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, int? columnIndex = null)
            => skin.GetConfig<ManiaSkinConfigurationLookup, int>(new ManiaSkinConfigurationLookup(lookup, columnIndex))?.Value ?? int.MinValue;

        private static string getStringConfig(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, int? columnIndex = null)
            => skin.GetConfig<ManiaSkinConfigurationLookup, string>(new ManiaSkinConfigurationLookup(lookup, columnIndex))?.Value ?? string.Empty;

        private static LegacyNoteBodyStyle getNoteBodyStyleConfig(ISkin skin, int? columnIndex = null)
            => skin.GetConfig<ManiaSkinConfigurationLookup, LegacyNoteBodyStyle>(new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.NoteBodyStyle, columnIndex))?.Value ?? default;

        private static Color4 getColorConfig(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, int? columnIndex = null)
            => skin.GetConfig<ManiaSkinConfigurationLookup, Color4>(new ManiaSkinConfigurationLookup(lookup, columnIndex))?.Value ?? default;

        private static bool coloursMatch(Color4 actual, Color4 expected)
            => Math.Abs(actual.R - expected.R) < 0.0001f
               && Math.Abs(actual.G - expected.G) < 0.0001f
               && Math.Abs(actual.B - expected.B) < 0.0001f
               && Math.Abs(actual.A - expected.A) < 0.0001f;

        private partial class TestDrawableNote : DrawableNote
        {
            public TestDrawableNote(Note hitObject)
                : base(hitObject)
            {
            }

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
            }
        }

        /// <summary>
        /// Explicitly marks component-only test trees which do not mount a gameplay renderer as compatibility layout
        /// consumers. A managed ruleset provider without gameplay intent still carries an exact package lease, but it
        /// deliberately has no exact gameplay layout publication for HUD components to consume.
        /// </summary>
        private partial class CompatibilityLayoutHost : Container
        {
            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.Cache(GameplaySkinLayoutRevisionOwner.CreateCompatibility());
                return dependencies;
            }
        }

        private partial class TestDrawableHoldNoteHead : DrawableHoldNoteHead
        {
            public TestDrawableHoldNoteHead(HeadNote hitObject)
                : base(hitObject)
            {
            }

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
            }
        }

        private partial class TestDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            public TestDrawableHoldNoteTail(TailNote hitObject)
                : base(hitObject)
            {
            }

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
            }
        }

        private partial class TestDrawableHoldNote : DrawableHoldNote
        {
            private bool? forcedHoldingState;

            public TestDrawableHoldNoteBody TestBody => (TestDrawableHoldNoteBody)Body;

            public TestDrawableHoldNote(HoldNote hitObject)
                : base(hitObject)
            {
            }

            public void ForceHoldingState(bool isHolding) => forcedHoldingState = isHolding;

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
            }

            protected override void Update()
            {
                base.Update();

                if (forcedHoldingState.HasValue)
                    ((Bindable<bool>)IsHolding).Value = forcedHoldingState.Value;
            }

            protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
            {
                switch (hitObject)
                {
                    case HeadNote head:
                        return new TestDrawableHoldNoteHead(head);

                    case TailNote tail:
                        return new TestDrawableHoldNoteTail(tail);

                    case HoldNoteBody body:
                        return new TestDrawableHoldNoteBody(body);
                }

                return base.CreateNestedHitObject(hitObject);
            }
        }

        private partial class TestDrawableHoldNoteBody : DrawableHoldNoteBody
        {
            public TestDrawableHoldNoteBody(HoldNoteBody hitObject)
                : base(hitObject)
            {
            }

            public void ForceMissForTesting() => ApplyMinResult();

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
            }
        }

        private class TestScrollingInfo : IScrollingInfo
        {
            public readonly Bindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

            IBindable<ScrollingDirection> IScrollingInfo.Direction => Direction;
            IBindable<double> IScrollingInfo.TimeRange { get; } = new Bindable<double>(5000);
            IBindable<IScrollAlgorithm> IScrollingInfo.Algorithm { get; } = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());
        }
    }
}
