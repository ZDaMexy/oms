// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsBgaSharedContent
    {
        [TestCase(true)]
        [TestCase(false)]
        public void TestFirstVisibleViewCanDrawItsSourceInTheSameUpdate(bool initiallyEnabled)
        {
            bool? sourceVisibleWhenViewCreated = null;
            bool? sourceVisibleWhenEnabled = null;
            bool? sourceMaskedInFirstFrame = null;
            bool sourceBeforeConsumer = false;

            AddStep("load a view after its sibling source has updated", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K } };
                var working = new BgaWorkingBeatmap(beatmap);
                var session = new BmsBgaPlaybackSession(new[]
                {
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "base.png", false),
                }, BmsPoorBgaMode.Default)
                { PresentationEnabled = initiallyEnabled };
                BufferedContainer capture = session.ChildrenOfType<BufferedContainer>().Single();
                DependencyProvidingContainer parent = null!;
                var consumer = new FirstBgaViewConsumer(() =>
                {
                    Drawable view = session.CreateView(GameplaySkinBgaScaleMode.Fit);
                    sourceVisibleWhenViewCreated = capture.Alpha > 0;
                    session.PresentationEnabled = true;
                    sourceVisibleWhenEnabled = capture.Alpha > 0;
                    return view;
                }, () =>
                {
                    // Inspect the first masking pass, before a later session.Update can repair stale visibility.
                    parent.UpdateSubTreeMasking();
                    sourceMaskedInFirstFrame = (bool)typeof(Drawable)
                        .GetProperty("IsMaskedAway", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(capture)!;
                    sourceBeforeConsumer = ReferenceEquals(parent.Children[0], session);
                });
                Child = parent = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(working)),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Children = new Drawable[] { session, consumer },
                };
            });
            AddUntilStep("first consumer frame recorded", () => sourceMaskedInFirstFrame.HasValue);
            AddAssert("registration immediately honours presentation", () => sourceVisibleWhenViewCreated == initiallyEnabled);
            AddAssert("enabling immediately prepares the source", () => sourceVisibleWhenEnabled == true);
            AddAssert("first source is not culled before its view draws", () => sourceMaskedInFirstFrame == false);
            AddAssert("source draws before the consumer", () => sourceBeforeConsumer);
        }

        private partial class FirstBgaViewConsumer : Container
        {
            private readonly Func<Drawable> createView;
            private Action? recordFirstFrame;

            public FirstBgaViewConsumer(Func<Drawable> createView, Action recordFirstFrame)
            {
                this.createView = createView;
                this.recordFirstFrame = recordFirstFrame;
                RelativeSizeAxes = Axes.Both;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Child = createView();
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();
                recordFirstFrame?.Invoke();
                recordFirstFrame = null;
            }
        }
    }
}
