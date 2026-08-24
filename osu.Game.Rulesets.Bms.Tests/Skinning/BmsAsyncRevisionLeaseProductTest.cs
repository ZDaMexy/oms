// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Threading;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedPackageNoteProductTest
    {
        [Test]
        public void TestCompletedBmsAsyncLoadRetainsExactRevisionUntilQueuedCallbackIsCancelled()
        {
            ImportedSkin selected = importAndSelect(
                "BMS async outer-gap revision",
                () => createOsk(
                    "notes/outer-gap",
                    ("notes/outer-gap.png", createPng(7, 9, new Rgba32(40, 190, 120, 255)))));

            SkinCurrentRevision revision = null!;
            MutableSkinSourceContainer source = null!;
            BmsAsyncNoteDrawable noteHost = null!;
            int participantBaseline = 0;
            int retirementCount = 0;
            Task pendingWorkDetach = null!;
            Task pendingConsumerDetach = null!;
            Action<SkinCurrentRevision> retirementObserver = null!;
            var heldCallbackScheduler = new Scheduler();

            AddStep("mount exact revision behind a direct initial note", () =>
            {
                revision = skinManager.CurrentRevision;
                Assert.That(revision.RecordId, Is.EqualTo(selected.Info.ID));

                retirementObserver = retired =>
                {
                    if (ReferenceEquals(retired, revision))
                    {
                        Interlocked.Increment(ref retirementCount);
                        skinManager.CurrentRevisionRetired -= retirementObserver;
                    }
                };
                skinManager.CurrentRevisionRetired += retirementObserver;

                var initial = new BeatmapNoteSkin();
                ownedSkins.Add(initial);

                Add(source = new MutableSkinSourceContainer(
                    initial,
                    new BmsSkinTransformer(revision.Owner))
                {
                    Child = noteHost = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note)),
                });
            });
            AddUntilStep("wait for direct initial note", () =>
                noteHost.IsLoaded
                && noteHost.Drawable is BeatmapNoteDrawable { IsLoaded: true }
                && revision.WorkDetached.IsCompleted);
            AddStep("capture quiescent exact revision", () =>
            {
                participantBaseline = revision.ParticipantLeaseCount;

                Assert.Multiple(() =>
                {
                    Assert.That(participantBaseline, Is.GreaterThanOrEqualTo(2));
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                });
            });
            AddStep("start real BMS materializer replacement", () =>
            {
                noteHost.LoadCallbackScheduler = heldCallbackScheduler;
                source.Replace(new BmsSkinTransformer(revision.Owner));
            });
            AddStep("hold update while completed load callback is queued", () =>
            {
                Assert.That(
                    SpinWait.SpinUntil(
                        () => noteHost.PendingLoadTask?.IsCompleted == true
                              && heldCallbackScheduler.HasPendingTasks
                              && revision.ParticipantLeaseCount == participantBaseline + 1,
                        TimeSpan.FromSeconds(10)),
                    Is.True,
                    "The inner materializer and outer LoadComponentAsync task did not complete before their update callback.");

                pendingWorkDetach = revision.WorkDetached;
                pendingConsumerDetach = revision.ConsumersDetached;

                Assert.Multiple(() =>
                {
                    Assert.That(noteHost.Drawable, Is.TypeOf<BeatmapNoteDrawable>(),
                        "The completed provisional must not publish before its queued update callback.");
                    Assert.That(pendingWorkDetach.IsCompleted, Is.False,
                        "The outer BMS load must retain exact owner work after the inner materializer task completes.");
                    Assert.That(pendingConsumerDetach.IsCompleted, Is.False);
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                    Assert.That(retirementCount, Is.Zero);
                });

                Assert.That(source.Remove(noteHost, disposeImmediately: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(pendingWorkDetach.Wait(TimeSpan.FromSeconds(10)), Is.True,
                        "Cancelling the already-completed load must release its outer exact-revision work lease.");
                    Assert.That(pendingConsumerDetach.IsCompleted, Is.False,
                        "The real provider remains an exact lifecycle participant until it detaches.");
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline - 1));
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                    Assert.That(retirementCount, Is.Zero);
                });
            });
            AddStep("drain cancelled callback ownership", () =>
            {
                heldCallbackScheduler.Update();
                heldCallbackScheduler.Update();

                Assert.Multiple(() =>
                {
                    Assert.That(heldCallbackScheduler.HasPendingTasks, Is.False);
                    Assert.That(source.Count, Is.Zero,
                        "The cancelled provisional callback must not republish its disposed host.");
                    Assert.That(pendingWorkDetach.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline - 1));
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                    Assert.That(retirementCount, Is.Zero);
                });
            });
            AddStep("detach final exact provider", () => Assert.That(Remove(source, disposeImmediately: true), Is.True));
            AddUntilStep("wait for exact consumers to detach", () => pendingConsumerDetach.IsCompleted);
            AddStep("assert manager lease still prevents retirement", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ConsumersDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.Detached.IsCompleted, Is.False);
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                    Assert.That(retirementCount, Is.Zero);
                });

                skinManager.CurrentSkinInfo.Value = skinManager.DefaultOmsSkin.SkinInfo;
            });
            AddUntilStep("wait for exact revision retirement", () =>
                !ReferenceEquals(skinManager.CurrentRevision, revision)
                && revision.Retired.IsCompleted
                && Volatile.Read(ref retirementCount) == 1);
            AddStep("assert exactly-once owner retirement", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(revision.Detached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(retirementCount, Is.EqualTo(1));
                });
            });
        }

    }
}
