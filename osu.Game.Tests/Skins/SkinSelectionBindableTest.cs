// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Database;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinSelectionBindableTest
    {
        [Test]
        public void TestRejectedDirectRequestDoesNotChangeCommittedValue()
        {
            Live<SkinInfo> original = new SkinInfo { Name = "original" }.ToLiveUnmanaged();
            Live<SkinInfo> rejected = new SkinInfo { Name = "rejected" }.ToLiveUnmanaged();
            var bindable = new SkinSelectionBindable(original)
            {
                SelectionRequested = _ => false,
            };
            int changes = 0;
            bindable.ValueChanged += _ => changes++;

            bindable.Value = rejected;

            Assert.Multiple(() =>
            {
                Assert.That(bindable.Value, Is.SameAs(original));
                Assert.That(changes, Is.Zero);
            });
        }

        [Test]
        public void TestRejectedBoundCopyRequestDoesNotChangeCommittedValue()
        {
            Live<SkinInfo> original = new SkinInfo { Name = "original" }.ToLiveUnmanaged();
            Live<SkinInfo> rejected = new SkinInfo { Name = "rejected" }.ToLiveUnmanaged();
            int requests = 0;
            var bindable = new SkinSelectionBindable(original)
            {
                SelectionRequested = _ =>
                {
                    requests++;
                    return false;
                },
            };
            Bindable<Live<SkinInfo>> boundCopy = bindable.GetBoundCopy();
            int changes = 0;
            bindable.ValueChanged += _ => changes++;

            boundCopy.Value = rejected;

            Assert.Multiple(() =>
            {
                Assert.That(bindable.Value, Is.SameAs(original));
                Assert.That(boundCopy.Value, Is.SameAs(original));
                Assert.That(changes, Is.Zero);
                Assert.That(requests, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestPreparedCommitChangesValueOnceWithoutReenteringRequest()
        {
            Live<SkinInfo> original = new SkinInfo { Name = "original" }.ToLiveUnmanaged();
            Live<SkinInfo> prepared = new SkinInfo { Name = "prepared" }.ToLiveUnmanaged();
            var bindable = new SkinSelectionBindable(original);
            int requests = 0;
            int changes = 0;
            bindable.SelectionRequested = _ =>
            {
                requests++;
                return false;
            };
            bindable.ValueChanged += _ => changes++;

            bindable.CommitPrepared(prepared);

            Assert.Multiple(() =>
            {
                Assert.That(bindable.Value, Is.SameAs(prepared));
                Assert.That(requests, Is.Zero);
                Assert.That(changes, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestPlainBindingCannotBypassSelectionGuard()
        {
            Live<SkinInfo> original = new SkinInfo { Name = "original" }.ToLiveUnmanaged();
            var guarded = new SkinSelectionBindable(original);
            var plain = new Bindable<Live<SkinInfo>>(original);

            Assert.Multiple(() =>
            {
                Assert.That(() => plain.BindTo(guarded), Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => guarded.BindTo(plain), Throws.TypeOf<InvalidOperationException>());
                Assert.That(guarded.Value, Is.SameAs(original));
                Assert.That(plain.Value, Is.SameAs(original));
            });
        }

        [Test]
        public void TestLeaseCannotBypassSelectionGuard()
        {
            Live<SkinInfo> original = new SkinInfo { Name = "original" }.ToLiveUnmanaged();
            var guarded = new SkinSelectionBindable(original);

            Assert.That(() => guarded.BeginLease(revertValueOnReturn: false), Throws.TypeOf<InvalidOperationException>());
            Assert.That(guarded.Value, Is.SameAs(original));
        }

        [Test]
        public void TestSameValueDoesNotIssueSelectionRequest()
        {
            Live<SkinInfo> original = new SkinInfo { Name = "original" }.ToLiveUnmanaged();
            int requests = 0;
            var guarded = new SkinSelectionBindable(original)
            {
                SelectionRequested = _ =>
                            {
                                requests++;
                                return false;
                            },
                Value = original
            };

            Assert.That(requests, Is.Zero);
            Assert.That(guarded.Value, Is.SameAs(original));
        }

        [Test]
        public void TestDisabledValueDoesNotIssueSelectionRequest()
        {
            Live<SkinInfo> original = new SkinInfo { Name = "original" }.ToLiveUnmanaged();
            Live<SkinInfo> requested = new SkinInfo { Name = "requested" }.ToLiveUnmanaged();
            int requests = 0;
            var guarded = new SkinSelectionBindable(original)
            {
                SelectionRequested = _ =>
                {
                    requests++;
                    return false;
                },
                Disabled = true,
            };

            Assert.That(() => guarded.Value = requested, Throws.TypeOf<InvalidOperationException>());
            Assert.That(requests, Is.Zero);
            Assert.That(guarded.Value, Is.SameAs(original));
        }
    }
}
