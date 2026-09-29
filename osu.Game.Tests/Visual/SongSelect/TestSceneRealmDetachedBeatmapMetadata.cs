// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneRealmDetachedBeatmapMetadata : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [BackgroundDependencyLoader]
        private void load() => Dependencies.Cache(Realm);

        [Test]
        public void TestCommittedBatchUpdatesExistingSnapshotAndRetainsUnavailableHistory()
        {
            RealmDetachedBeatmapStore store = null!;
            IBindableList<BeatmapSetInfo> sets = null!;
            BeatmapSetInfo source = null!;
            int refreshes = 0;
            int collectionChanges = 0;
            Guid[] ids = Array.Empty<Guid>();
            AddStep("create library and store", () =>
            {
                source = TestResources.CreateTestBeatmapSetInfo(3);
                Realm.Write(r => r.Add(source));
                Child = store = new RealmDetachedBeatmapStore();
                store.RulesetMetadataUpdated += _ => refreshes++;
            });
            AddUntilStep("store loaded", () => store.IsLoaded);
            AddStep("capture current list", () =>
            {
                sets = store.GetBeatmapSets(null);
                sets.BindCollectionChanged((_, _) => collectionChanges++);
            });
            AddAssert("all charts visible", () => sets.Single().Beatmaps.Count, () => Is.EqualTo(3));
            AddWaitStep("settle initial subscriptions", 3);
            AddStep("commit metadata batch", () =>
            {
                ids = source.Beatmaps.Select(b => b.ID).ToArray();
                Realm.Write(r =>
                {
                    foreach (Guid id in ids)
                        r.Find<BeatmapInfo>(id)!.Metadata.RulesetDataJson = "{\"difficulty_table_entries\":[]}";
                });
                Realm.Write(r => r.Find<BeatmapInfo>(ids[0])!.Metadata.RulesetDataJson = "{\"latest\":true}");
            });
            AddWaitStep("settle native Realm notifications", 3);
            AddStep("publish one batch", () =>
            {
                collectionChanges = 0;
                Realm.NotifyBeatmapRulesetDataChanged(ids);
            });
            AddUntilStep("batch delivered", () => refreshes == 1);
            AddAssert("latest committed metadata visible", () => sets.Single().Beatmaps[0].Metadata.RulesetDataJson, () => Is.EqualTo("{\"latest\":true}"));
            AddAssert("batch adds no collection replacements", () => collectionChanges, () => Is.Zero);
            AddStep("source unavailable", () => Realm.Write(r => r.Find<BeatmapSetInfo>(source.ID)!.FilesystemUnavailable = true));
            AddUntilStep("unavailable hidden", () => sets.Count == 0);
            AddAssert("history retained", () => Realm.Run(r => r.Find<BeatmapSetInfo>(source.ID)!.Beatmaps.Count), () => Is.EqualTo(3));
            AddStep("source restored", () => Realm.Write(r => r.Find<BeatmapSetInfo>(source.ID)!.FilesystemUnavailable = false));
            AddUntilStep("same source visible again", () => sets.Count == 1 && sets[0].ID == source.ID);
        }
    }
}
