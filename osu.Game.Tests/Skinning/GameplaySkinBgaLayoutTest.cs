// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.Skinning
{
    [TestFixture]
    public class GameplaySkinBgaLayoutTest
    {
        [Test]
        public void TestOrderedWindowsPreserveIndependentShapesAndModes()
        {
            Assert.That(GameplaySkinBgaLayout.TryParse(" .1, 2e-1, .3, .4, fit; 0,0,.1,.2,fill; .8,.8,.2,.2,stretch ",
                out GameplaySkinBgaLayout? layout, out string? diagnostic), Is.True);
            Assert.That(diagnostic, Is.Null);
            Assert.That(layout!.Viewports, Is.EqualTo(new[]
            {
                GameplaySkinLayoutRect.Create(.1f, .2f, .3f, .4f),
                GameplaySkinLayoutRect.Create(0, 0, .1f, .2f),
                GameplaySkinLayoutRect.Create(.8f, .8f, .2f, .2f),
            }));
            Assert.That(layout.ScaleModes, Is.EqualTo(new[] { GameplaySkinBgaScaleMode.Fit, GameplaySkinBgaScaleMode.Fill, GameplaySkinBgaScaleMode.Stretch }));
            Assert.That(() => ((IList<GameplaySkinLayoutRect>)layout.Viewports)[0] = default, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<GameplaySkinBgaScaleMode>)layout.ScaleModes)[0] = GameplaySkinBgaScaleMode.Fill, Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void TestNoneExplicitlyDeclaresNoWindows()
        {
            Assert.That(GameplaySkinBgaLayout.TryParse(" none ", out GameplaySkinBgaLayout? layout, out string? diagnostic), Is.True);
            Assert.That(layout!.Viewports, Is.Empty);
            Assert.That(layout.ScaleModes, Is.Empty);
            Assert.That(diagnostic, Is.Null);
        }

        [TestCase("", "invalid-format")]
        [TestCase("0,0,.2,.2", "invalid-format")]
        [TestCase("0,0,.2,.2,fit;", "invalid-format")]
        [TestCase("none;0,0,.2,.2,fit", "invalid-format")]
        [TestCase("0,0,NaN,.2,fit", "invalid-number")]
        [TestCase("0,0,Infinity,.2,fit", "invalid-number")]
        [TestCase("0,0,1e50,.2,fit", "invalid-number")]
        [TestCase("0,0,nope,.2,fit", "invalid-number")]
        [TestCase("0,0,0,.2,fit", "invalid-bounds")]
        [TestCase("0,0,.2,-.2,fit", "invalid-bounds")]
        [TestCase("-.1,0,.2,.2,fit", "invalid-bounds")]
        [TestCase("0,.9,.2,.2,fit", "invalid-bounds")]
        [TestCase(".9,0,.2,.2,fit", "invalid-bounds")]
        [TestCase("0,0,.2,.2,Fit", "invalid-mode")]
        [TestCase("0,0,.2,.2,0", "invalid-mode")]
        [TestCase("0,0,.2,.2,crop", "invalid-mode")]
        public void TestInvalidDeclarationFailsAtomically(string value, string reason)
        {
            Assert.That(GameplaySkinBgaLayout.TryParse(value, out GameplaySkinBgaLayout? layout, out string? diagnostic), Is.False);
            Assert.That(layout, Is.Null);
            Assert.That(diagnostic, Is.EqualTo($"bms.layout.bga-viewports.{reason}"));
        }

        [Test]
        public void TestExistingEventWindowBudgetIsEnforced()
        {
            string maximum = string.Join(';', Enumerable.Repeat("0,0,.1,.1,fit", GameplaySkinEventBudgets.MAX_BGA_VIEWPORTS));
            Assert.That(GameplaySkinBgaLayout.TryParse(maximum, out GameplaySkinBgaLayout? layout, out _), Is.True);
            Assert.That(layout!.Viewports, Has.Count.EqualTo(GameplaySkinEventBudgets.MAX_BGA_VIEWPORTS));
            Assert.That(GameplaySkinBgaLayout.TryParse(maximum + ";0,0,.1,.1,fit", out layout, out string? diagnostic), Is.False);
            Assert.That(layout, Is.Null);
            Assert.That(diagnostic, Is.EqualTo("bms.layout.bga-viewports.budget-exceeded"));
        }
    }
}
