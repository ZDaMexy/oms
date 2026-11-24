// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Scoring;
using Realms;
using Realms.Schema;

namespace osu.Game.Tests.Database
{
    public class ScoreHashIndexRealmTest : RealmTest
    {
        [Test]
        public void TestExistingScoreCanBeOpenedWithAndWithoutTheIndex()
        {
            RunTestWithRealm((reference, storage) =>
            {
                const string filename = "unindexed-scores.realm";
                const string chartHash = "previously-saved-chart-hash";
                Guid id = Guid.NewGuid();

                // Recreate the former schema exactly apart from the one indexed column.
                var formerSchema = new RealmSchema.Builder();
                foreach (ObjectSchema model in reference.Realm.Schema)
                {
                    if (model.Name != "Score")
                    {
                        formerSchema.Add(model);
                        continue;
                    }

                    var scoreSchema = new ObjectSchema.Builder(model.Name, model.BaseType);
                    foreach (Property property in model)
                        scoreSchema.Add(property.Name == nameof(ScoreInfo.BeatmapHash)
                            ? new Property(property.Name, property.Type, indexType: IndexType.None, managedName: property.ManagedName)
                            : property);
                    formerSchema.Add(scoreSchema);
                }

                var formerConfiguration = new RealmConfiguration(storage.GetFullPath(filename, true))
                {
                    SchemaVersion = 58,
                    Schema = formerSchema,
                };

                using (var formerRealm = Realm.GetInstance(formerConfiguration))
                {
                    formerRealm.Write(() =>
                    {
                        dynamic score = formerRealm.DynamicApi.CreateObject("Score", id);
                        score.BeatmapHash = chartHash;
                        score.TotalScore = 682L;
                    });
                    dynamic saved = formerRealm.DynamicApi.Find("Score", id)!;
                    Assert.That((string)saved.BeatmapHash, Is.EqualTo(chartHash));
                }

                // Open directly first so schema errors surface instead of the client's backup/recovery fallback.
                using (var migrated = Realm.GetInstance(new RealmConfiguration(formerConfiguration.DatabasePath) { SchemaVersion = 58 }))
                {
                    Assert.That(migrated.Find<ScoreInfo>(id), Is.Not.Null);
                }

                using (var current = new RealmAccess(storage, filename))
                {
                    ScoreInfo score = current.Realm.All<ScoreInfo>().Single(s => s.BeatmapHash == chartHash);
                    Assert.That((score.ID, score.TotalScore), Is.EqualTo((id, 682L)));
                    Assert.That(current.Realm.Config.SchemaVersion, Is.EqualTo(58UL));
                }

                using (var formerRealm = Realm.GetInstance(formerConfiguration))
                {
                    dynamic score = formerRealm.DynamicApi.Find("Score", id)!;
                    Assert.That((long)score.TotalScore, Is.EqualTo(682L));
                }
            });
        }
    }
}
