// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Audio;
using osu.Game.Rulesets.Judgements;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.Skinning.Oms;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.ClicksPerSecond;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osu.Game.Beatmaps.ControlPoints;
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
        [Cached(Type = typeof(IScrollingInfo))]
        private readonly TestScrollingInfo scrollingInfo = new TestScrollingInfo();

        [Cached]
        private readonly ScoreProcessor scoreProcessor = new ScoreProcessor(new ManiaRuleset());

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        public TestSceneOmsBuiltInSkin()
        {
            scrollingInfo.Direction.Value = ScrollingDirection.Down;
        }

        [Test]
        public void TestOmsBuiltInSkinIsRegisteredAndProvidesResources()
        {
            Skin skin = null!;
            int selectableProtectedSkinCount = 0;

            AddStep("load OMS skin", () =>
            {
                var skins = skinManager.GetAllUsableSkins();
                selectableProtectedSkinCount = skins.Count(s => s.PerformRead(info => info.Protected));

                Assert.That(skins.First().ID, Is.EqualTo(OmsSkin.CreateInfo().ID));

                var skinInfo = skins.Single(s => s.ID == OmsSkin.CreateInfo().ID);
                skin = skinInfo.PerformRead(skinManager.GetSkin);
            });

            AddAssert("is OMS skin", () => skin is OmsSkin);
            AddAssert("is protected", () => skin.SkinInfo.PerformRead(s => s.Protected));
            AddAssert("OMS is only selectable built-in skin", () => selectableProtectedSkinCount == 1);
            AddAssert("has mania stage texture", () => skin.GetTexture("mania-stage-left") != null);
            AddAssert("has mania key texture", () => skin.GetTexture("mania-key1") != null);
        }

        [Test]
        public void TestCanSelectOmsBuiltInSkin()
        {
            AddStep("select OMS skin", () =>
                skinManager.CurrentSkinInfo.Value = skinManager.GetAllUsableSkins().Single(s => s.ID == OmsSkin.CreateInfo().ID));

            AddAssert("current skin is OMS", () => skinManager.CurrentSkin.Value is OmsSkin);
        }

        [TestCaseSource(nameof(upstreamProtectedSkinIds))]
        public void TestUpstreamProtectedSkinIdsFallbackToOms(string skinName, Guid skinId)
        {
            AddStep($"set skin from {skinName} id", () => skinManager.SetSkinFromConfiguration(skinId.ToString()));

            AddAssert("current skin info is OMS", () => skinManager.CurrentSkinInfo.Value.ID == OmsSkin.CreateInfo().ID);
            AddAssert("current skin instance is OMS", () => skinManager.CurrentSkin.Value is OmsSkin);
        }

        [TestCaseSource(nameof(upstreamProtectedSkinIds))]
        public void TestUpstreamBuiltInSkinsAreNotRegisteredInDatabase(string skinName, Guid skinId)
        {
            AddAssert($"{skinName} built-in is absent from realm", () => skinManager.Query(s => s.ID == skinId) == null);
        }

        [Test]
        public void TestLegacyBeatmapCompatibilityFallbackUsesOmsSkin()
        {
            BeatmapSkinProvidingContainer provider = null!;

            AddStep("set current skin to triangles", () => skinManager.CurrentSkinInfo.Value = TrianglesSkin.CreateInfo().ToLiveUnmanaged());

            AddStep("setup ruleset provider", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Child = new RulesetSkinProvidingContainer(ruleset, beatmap, new LegacyResourceBeatmapSkin(renderer))
                {
                    Child = new Container(),
                };

                provider = this.ChildrenOfType<BeatmapSkinProvidingContainer>().Single();
            });

            AddUntilStep("compatibility fallback available", () => provider.AllSources.Skip(1).FirstOrDefault() != null);
            AddAssert("compatibility fallback wraps OMS skin", () => unwrapSkin(provider.AllSources.ElementAt(1)) is OmsSkin);
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
            AddAssert("ruleset resources precede OMS fallback", () => sourceOrder, () => Is.EqualTo("ResourceStoreBackedSkin -> OmsSkin"));

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
            AddAssert("ruleset resources sit between legacy user and OMS fallback", () => sourceOrder, () => Is.EqualTo("EmptyLegacyUserSkin -> ResourceStoreBackedSkin -> OmsSkin"));

            AddStep("clear wrapped ruleset provider", () => host.Expire());
        }

        [Test]
        public void TestBmsOnlyUserSkinFallsBackToOmsNotePiece()
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

            AddUntilStep("OMS note piece loaded through BMS-only fallback", () => this.ChildrenOfType<OmsNotePiece>().Any(drawable => drawable.IsLoaded));
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
            AddAssert("OMS note piece not used when legacy note assets exist", () => !this.ChildrenOfType<OmsNotePiece>().Any());
            AddAssert("BMS combo counter not used in mania note path", () => !this.ChildrenOfType<TestBmsComboCounter>().Any());
            AddStep("clear mixed-layer note host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutNoteAssetsFallsBackToOmsNotePiece()
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

            AddUntilStep("OMS note piece loaded through legacy partial override", () => this.ChildrenOfType<OmsNotePiece>().Any(drawable => drawable.IsLoaded));
            AddAssert("legacy note piece not used when note assets are missing", () => !this.ChildrenOfType<LegacyNotePiece>().Any());
            AddStep("clear key-only legacy note host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutHoldBodyAssetsFallsBackToOmsHoldBodyPiece()
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

            AddUntilStep("OMS hold body piece loaded through legacy partial override", () => this.ChildrenOfType<OmsHoldNoteBodyPiece>().Any(drawable => drawable.IsLoaded));
            AddAssert("legacy hold body piece not used when body assets are missing", () => !this.ChildrenOfType<LegacyBodyPiece>().Any());
            AddStep("clear key-only legacy hold host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutJudgementAssetsFallsBackToOmsJudgementPiece()
        {
            Drawable host = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load key-only legacy user skin judgement host", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                var judgement = new DrawableManiaJudgement
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                judgement.Apply(new JudgementResult(new HitObject { StartTime = Time.Current }, new Judgement())
                {
                    Type = HitResult.Great,
                }, null);

                Add(host = new SkinProvidingContainer(new KeyOnlyLegacyUserSkin(renderer))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = judgement,
                    },
                });
            });

            AddUntilStep("OMS judgement piece loaded through legacy partial override", () => this.ChildrenOfType<OmsManiaJudgementPiece>().Any(drawable => drawable.IsLoaded));
            AddAssert("legacy judgement piece not used when judgement assets are missing", () => !this.ChildrenOfType<LegacyManiaJudgementPiece>().Any());
            AddStep("clear key-only legacy judgement host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutHitExplosionAssetsFallsBackToOmsHitExplosion()
        {
            Drawable host = null!;
            ColumnTestContainer columnHost = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load key-only legacy user skin hit explosion host", () =>
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

            AddUntilStep("hit explosion column host loaded", () => columnHost.IsLoaded);
            AddStep("add hit explosion under key-only legacy user skin", () => columnHost.Add(new PoolableHitExplosion
            {
                RelativeSizeAxes = Axes.Both,
            }));

            AddUntilStep("OMS hit explosion loaded through legacy partial override", () => this.ChildrenOfType<OmsHitExplosion>().Any(drawable => drawable.IsLoaded));
            AddAssert("legacy hit explosion not used when explosion assets are missing", () => !this.ChildrenOfType<LegacyHitExplosion>().Any());
            AddStep("clear key-only legacy hit explosion host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutComboFontFallsBackToOmsComboCounter()
        {
            Drawable host = null!;
            RulesetSkinProvidingContainer provider = null!;
            DefaultSkinComponentsContainer hudComponents = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load key-only legacy user skin combo host", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(5))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                Add(host = new SkinProvidingContainer(new KeyOnlyLegacyUserSkin(renderer))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = provider = new RulesetSkinProvidingContainer(ruleset, beatmap, null)
                    {
                        Child = new Container(),
                    },
                });
            });

            AddUntilStep("combo ruleset provider loaded", () => provider.IsLoaded);
            AddStep("resolve HUD components through runtime provider", () =>
            {
                hudComponents = (DefaultSkinComponentsContainer)provider.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new ManiaRuleset().RulesetInfo))!;

                foreach (var drawable in hudComponents.Children.Where(drawable => drawable is not OmsManiaComboCounter && drawable is not LegacyManiaComboCounter).ToArray())
                    hudComponents.Remove(drawable, false);

                provider.Child = hudComponents;
            });

            AddUntilStep("OMS combo counter loaded through legacy partial override", () => this.ChildrenOfType<OmsManiaComboCounter>().Any(drawable => drawable.IsLoaded));
            AddAssert("legacy combo counter not used when combo font is missing", () => !this.ChildrenOfType<LegacyManiaComboCounter>().Any());
            AddStep("clear key-only legacy combo host", () => host.Expire());
        }

        [Test]
        public void TestLegacyUserSkinWithoutBarLineConfigFallsBackToOmsBarLine()
        {
            Drawable host = null!;

            AddStep("set current skin to OMS", () => skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo);

            AddStep("load key-only legacy user skin bar line host", () =>
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

            AddUntilStep("OMS bar line loaded through legacy partial override", () => this.ChildrenOfType<OmsBarLine>().Any(drawable => drawable.IsLoaded));
            AddAssert("legacy bar line not used when bar line config is missing", () => !this.ChildrenOfType<LegacyBarLine>().Any());
            AddStep("clear key-only legacy bar line host", () => host.Expire());
        }

        [Test]
        public void TestOmsSkinUsesSharedTransformerShell()
        {
            ISkin transformedSkin = null!;
            ISkin wrappedSkin = null!;
            DefaultSkinComponentsContainer globalHudShell = null!;
            DefaultSkinComponentsContainer songSelectShell = null!;
            DefaultSkinComponentsContainer playfieldShell = null!;
            DefaultSkinComponentsContainer hudComponents = null!;

            AddStep("create OMS transformer shell", () =>
            {
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };

                transformedSkin = ruleset.CreateSkinTransformer(skinManager.DefaultOmsSkin, beatmap)!;
                wrappedSkin = ((ISkinTransformer)transformedSkin).Skin;
                globalHudShell = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))!;
                songSelectShell = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect))!;
                playfieldShell = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Playfield, ruleset.RulesetInfo))!;
                hudComponents = (DefaultSkinComponentsContainer)transformedSkin.GetDrawableComponent(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset.RulesetInfo))!;
            });

            AddAssert("uses OMS transformer shell", () => transformedSkin is OmsSkinTransformer);
            AddAssert("wraps explicit mania transformer", () => wrappedSkin is ManiaOmsSkinTransformer);
            AddAssert("provides global HUD shell", () => globalHudShell is DefaultSkinComponentsContainer);
            AddAssert("provides song select shell", () => songSelectShell is DefaultSkinComponentsContainer);
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
            SkinLayoutInfo playfieldLayout = null!;
            SerialisedDrawableInfo[] maniaPlayfieldComponents = null!;

            AddStep("load OMS embedded layout metadata", () =>
            {
                skin = skinManager.DefaultOmsSkin;
                globalHudLayout = skin.LayoutInfos[GlobalSkinnableContainers.MainHUDComponents];
                songSelectLayout = skin.LayoutInfos[GlobalSkinnableContainers.SongSelect];
                playfieldLayout = skin.LayoutInfos[GlobalSkinnableContainers.Playfield];

                Assert.That(playfieldLayout.TryGetDrawableInfo(new ManiaRuleset().RulesetInfo, out var components), Is.True);
                maniaPlayfieldComponents = components!;
            });

            AddAssert("exposes three global layout targets", () => skin.LayoutInfos.Count == 3);
            AddAssert("global HUD layout contains song progress", () => globalHudLayout.AllDrawables.Select(i => i.Type).Contains(typeof(DefaultSongProgress)));
            AddAssert("global HUD layout contains Argon score counter", () => globalHudLayout.AllDrawables.Select(i => i.Type).Contains(typeof(ArgonScoreCounter)));
            AddAssert("global HUD layout contains judgement counter", () => globalHudLayout.AllDrawables.Select(i => i.Type).Contains(typeof(ArgonJudgementCounterDisplay)));
            AddAssert("song select layout stays intentionally empty", () => !songSelectLayout.AllDrawables.Any());
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

                var transformedSkin = new ManiaRuleset().CreateSkinTransformer(skinManager.DefaultOmsSkin, beatmap)!;

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
                            firstColumnHost = new ColumnTestContainer(0, ManiaAction.Key1, stageColumns: 7)
                            {
                                Width = 80,
                                Height = 200,
                            },
                            secondColumnHost = new ColumnTestContainer(7, ManiaAction.Key1, stageColumns: 6)
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

            AddAssert("first-stage note piece keeps taller 7K profile", () => firstNotePiece.DrawHeight > secondNotePiece.DrawHeight);
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
                    Child = judgement,
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
                    Child = new DrawableBarLine(new BarLine { StartTime = Time.Current })
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.X,
                        Width = 1f,
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
                    Child = judgement,
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

            AddAssert("dual-stage judgement keeps OMS anchor", () => judgementPiece.Anchor == Anchor.BottomCentre);
            AddAssert("dual-stage judgement reuses OMS score position", () => Math.Abs(judgementPiece.Y + 107.2f) < 0.01f);
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
                    Child = judgement,
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

            AddAssert("mixed-stage judgement uses first-stage OMS anchor", () => judgementPiece.Anchor == Anchor.TopCentre);
            AddAssert("mixed-stage judgement uses first-stage OMS score position", () => Math.Abs(judgementPiece.Y - 160f) < 0.01f);
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

        [Test]
        public void TestOmsHudComboCounterUsesSharedComboPositionForDualStages()
        {
            Drawable host = null!;
            OmsManiaComboCounter comboCounter = null!;

            AddStep("load dual-stage OMS HUD combo", () =>
            {
                var transformedSkin = createTransformedSkin(5, 5);
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

            AddUntilStep("dual-stage combo counter positioned", () => comboCounter.IsLoaded && Math.Abs(comboCounter.Y - 136f) < 0.01f);
            AddAssert("dual-stage combo counter keeps top anchor", () => comboCounter.Anchor == Anchor.TopCentre);
            AddStep("clear dual-stage HUD combo host", () => host.Expire());
        }

        [Test]
        public void TestOmsHudComboCounterUsesFirstStageComboPositionForMixedStages()
        {
            Drawable host = null!;
            OmsManiaComboCounter comboCounter = null!;

            AddStep("load mixed-stage OMS HUD combo", () =>
            {
                var transformedSkin = createTransformedSkin(7, 6);
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

            AddUntilStep("mixed-stage combo counter positioned", () => comboCounter.IsLoaded && Math.Abs(comboCounter.Y - 144f) < 0.01f);
            AddAssert("mixed-stage combo counter keeps top anchor", () => comboCounter.Anchor == Anchor.TopCentre);
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
                    Child = new DrawableBarLine(new BarLine { StartTime = Time.Current, Major = true })
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.X,
                        Width = 1f,
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
                    Child = new DrawableBarLine(new BarLine { StartTime = Time.Current, Major = true })
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.X,
                        Width = 1f,
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
                    Child = new DrawableBarLine(barLineObject)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.X,
                        Width = 1f,
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
                float[] expectedWidths = { 46f, 40f, 46f, 40f, 46f };

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
                AddAssert("5K note height falls back to stage min width", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 2) - 40f) < 0.01f);

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
                float[] expectedWidths = { 46f, 40f, 46f, 40f, 46f, 46f, 40f, 46f, 40f, 46f };

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
            AddAssert("4K hit explosion scale uses candidate width fallback", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 0) - 69f / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);

            AddStep("create OMS 5K transformer", () => transformedSkin = createTransformedSkin(5));

            AddAssert("5K hit explosion uses OMS preset animation", () => getStringConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionImage, 1) == "lightingN");
            AddAssert("5K hit explosion scale uses OMS preset width", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 1) - 40f / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);
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
            AddAssert("6K second stage falls back to its own min width", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale, 7) - 40f) < 0.01f);
        }

        [Test]
        public void TestOmsHitExplosionConfigUsesStageLocalPresetForMixedStages()
        {
            ISkin transformedSkin = null!;

            AddStep("create OMS 5K+8K transformer", () => transformedSkin = createTransformedSkin(5, 8));

            AddAssert("5K first stage keeps OMS explosion scale", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 0) - 46f / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);
            AddAssert("8K second stage uses its own explosion scale", () => Math.Abs(getFloatConfig(transformedSkin, LegacyManiaSkinConfigurationLookups.ExplosionScale, 5) - 43f / LegacyManiaSkinConfiguration.DEFAULT_COLUMN_SIZE) < 0.0001f);
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
                    Child = new ScrollingTestContainer(ScrollingDirection.Down)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        AutoSizeAxes = Axes.X,
                        TimeRange = 2000,
                        Child = new ManiaInputManager(new ManiaRuleset().RulesetInfo, 5)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = stage,
                        },
                    },
                });
            });

            AddUntilStep("stage loaded", () => stage.IsLoaded && stage.Columns.All(column => column.IsLoaded));
            AddAssert("stage columns use OMS widths", () =>
            {
                float[] expectedWidths = { 46f, 40f, 46f, 40f, 46f };

                return stage.Columns.Select(column => column.DrawWidth)
                            .Zip(expectedWidths, (actualWidth, expectedWidth) => Math.Abs(actualWidth - expectedWidth) < 0.01f)
                            .All(matches => matches);
            });
            AddAssert("stage hit position uses OMS preset", () => Math.Abs(stage.Columns[0].HitObjectArea.Padding.Bottom - 140.8f) < 0.01f);
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
                    Child = new ScrollingTestContainer(ScrollingDirection.Down)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        AutoSizeAxes = Axes.X,
                        TimeRange = 2000,
                        Child = new ManiaInputManager(new ManiaRuleset().RulesetInfo, 10)
                        {
                            RelativeSizeAxes = Axes.Both,
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
                            },
                        },
                    },
                });
            });

            AddUntilStep("second stage loaded", () => secondStage.IsLoaded && secondStage.Columns.All(column => column.IsLoaded));
            AddAssert("second stage uses repeated OMS widths", () =>
            {
                float[] expectedWidths = { 46f, 40f, 46f, 40f, 46f };

                return secondStage.Columns.Select(column => column.DrawWidth)
                                  .Zip(expectedWidths, (actualWidth, expectedWidth) => Math.Abs(actualWidth - expectedWidth) < 0.01f)
                                  .All(matches => matches);
            });
            AddStep("clear dual stage host", () => host.Expire());
        }

        private ISkin createTransformedSkin(params int[] stageColumns)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(stageColumns[0]))
            {
                BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo },
            };

            for (int i = 1; i < stageColumns.Length; i++)
                beatmap.Stages.Add(new StageDefinition(stageColumns[i]));

            return new ManiaRuleset().CreateSkinTransformer(skinManager.DefaultOmsSkin, beatmap)!;
        }

        private static readonly object[] upstreamProtectedSkinIds =
        {
            new object[] { "Triangles", TrianglesSkin.CreateInfo().ID },
            new object[] { "Argon", ArgonSkin.CreateInfo().ID },
            new object[] { "ArgonPro", ArgonProSkin.CreateInfo().ID },
            new object[] { "Classic", DefaultLegacySkin.CreateInfo().ID },
            new object[] { "Retro", RetroSkin.CreateInfo().ID },
        };

        private static ISkin unwrapSkin(ISkin skin)
        {
            while (skin is ISkinTransformer transformer)
                skin = transformer.Skin;

            return skin;
        }

        private static string describeSkinSource(ISkin skin)
            => skin is ResourceStoreBackedSkin
                ? nameof(ResourceStoreBackedSkin)
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
