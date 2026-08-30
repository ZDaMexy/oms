// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    /// <summary>
    /// A test scene for a mania hitobject.
    /// </summary>
    public abstract partial class ManiaHitObjectTestScene : ManiaSkinnableTestScene
    {
        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            SetContents(_ => new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Height = 0.7f,
                Direction = FillDirection.Horizontal,
                Children = new Drawable[]
                {
                    new ColumnTestContainer(0, ManiaAction.Key1, true)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        Width = 80,
                        Child = new ScrollingHitObjectContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                        }.With(c =>
                        {
                            c.Add(CreateHitObject().With(h =>
                            {
                                setColumn(h, 0);
                                h.HitObject.StartTime = Time.Current + 5000;
                                h.AccentColour.Value = Color4.Orange;
                            }));
                        })
                    },
                    new ColumnTestContainer(1, ManiaAction.Key2, true)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        Width = 80,
                        Child = new ScrollingHitObjectContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                        }.With(c =>
                        {
                            c.Add(CreateHitObject().With(h =>
                            {
                                setColumn(h, 1);
                                h.HitObject.StartTime = Time.Current + 5000;
                            }));
                        })
                    },
                }
            });
        });

        private static void setColumn(DrawableManiaHitObject drawable, int column)
        {
            drawable.HitObject.Column = column;

            foreach (ManiaHitObject nested in drawable.HitObject.NestedHitObjects.OfType<ManiaHitObject>())
                nested.Column = column;
        }

        protected abstract DrawableManiaHitObject CreateHitObject();
    }
}
