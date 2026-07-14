// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinConfigurationDeclarationTest
    {
        [Test]
        public void TestDefaultAndAbsentRemainAbsent()
        {
            GameplaySkinConfigurationDeclaration<int> defaultDeclaration = default;
            GameplaySkinConfigurationDeclaration<int> absent = GameplaySkinConfigurationDeclaration<int>.Absent;

            Assert.Multiple(() =>
            {
                Assert.That(defaultDeclaration.IsDeclared, Is.False);
                Assert.That(absent.IsDeclared, Is.False);
                Assert.That(defaultDeclaration.TryGetValue(out int defaultValue), Is.False);
                Assert.That(defaultValue, Is.Zero);
                Assert.That(() => absent.Value, Throws.InvalidOperationException);
                Assert.That(absent.ToString(), Is.EqualTo("Absent"));
            });
        }

        [Test]
        public void TestDeclaredDefaultValuesRemainExplicit()
        {
            GameplaySkinConfigurationDeclaration<int> zero = GameplaySkinConfigurationDeclaration<int>.Declared(0);
            GameplaySkinConfigurationDeclaration<bool> falseValue = GameplaySkinConfigurationDeclaration<bool>.Declared(false);
            GameplaySkinConfigurationDeclaration<string> empty = GameplaySkinConfigurationDeclaration<string>.Declared(string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(zero.IsDeclared, Is.True);
                Assert.That(zero.Value, Is.Zero);
                Assert.That(zero.TryGetValue(out int declaredZero), Is.True);
                Assert.That(declaredZero, Is.Zero);
                Assert.That(falseValue.IsDeclared, Is.True);
                Assert.That(falseValue.Value, Is.False);
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value, Is.Empty);
                Assert.That(empty.ToString(), Is.EqualTo("Declared"));
            });
        }

        [Test]
        public void TestNullDeclarationRejected()
        {
            Assert.That(() => GameplaySkinConfigurationDeclaration<string>.Declared(null!), Throws.ArgumentNullException);
        }

        [Test]
        public void TestToStringDoesNotExposeValue()
        {
            const string private_value = "private/path/value";
            GameplaySkinConfigurationDeclaration<string> declaration = GameplaySkinConfigurationDeclaration<string>.Declared(private_value);

            Assert.That(declaration.ToString(), Is.EqualTo("Declared").And.Not.Contain(private_value));
        }

        [Test]
        public void TestPublicSurfaceIsPresenceOnly()
        {
            Type declarationType = typeof(GameplaySkinConfigurationDeclaration<int>);
            string[] publicMemberNames = declarationType.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                                                        .Select(member => member.Name)
                                                        .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(declarationType.IsValueType, Is.True);
                Assert.That(declarationType.GetConstructors(), Is.Empty);
                Assert.That(declarationType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(publicMemberNames, Does.Not.Contain("Suppress"));
                Assert.That(publicMemberNames, Does.Not.Contain("Provide"));
                Assert.That(publicMemberNames, Does.Not.Contain("Inherit"));
                Assert.That(publicMemberNames, Does.Not.Contain("Kind"));
                Assert.That(publicMemberNames, Does.Not.Contain("op_Implicit"));
            });
        }
    }
}
