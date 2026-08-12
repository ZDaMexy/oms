// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning.IO;
using Realms;

namespace osu.Game.Skinning
{
    public class SkinImporter : RealmArchiveModelImporter<SkinInfo>
    {
        private const string skin_info_file = "skininfo.json";

        private readonly IStorageResourceProvider skinResources;

        private readonly ModelManager<SkinInfo> modelManager;

        private readonly Func<SkinInfo, bool> canUpdateExisting;

        public SkinImporter(
            Storage storage,
            RealmAccess realm,
            IStorageResourceProvider skinResources,
            Func<SkinInfo, bool>? canUpdateExisting = null)
            : base(storage, realm)
        {
            this.skinResources = skinResources;
            this.canUpdateExisting = canUpdateExisting ?? (_ => true);

            modelManager = new ModelManager<SkinInfo>(storage, realm);
        }

        public override IEnumerable<string> HandledExtensions => new[] { ".osk" };

        protected override string[] HashableFileTypes => new[] { ".ini", ".json" };

        protected override bool UseFastImportPrecheck => false;

        protected override bool UseTransactionalFileImportScope => true;

        protected override ValueTask<ArchiveReader> OpenArchiveReaderAsync(ImportTask task, CancellationToken cancellationToken)
            => SkinArchiveReader.OpenAsync(task, cancellationToken);

        protected override bool ShouldDeleteArchive(string path) => string.Equals(Path.GetExtension(path), @".osk", StringComparison.OrdinalIgnoreCase);

        protected override SkinInfo CreateModel(ArchiveReader archive, ImportParameters parameters)
        {
            var result = new SkinInfo { Name = archive.Name ?? @"No name" };

            if (archive is SkinArchiveReader skinArchive)
                result.InstantiationInfo = getInstantiationInfo(skinArchive.InstantiationKind);

            return result;
        }

        private const string unknown_creator_string = @"Unknown";

        /// <summary>
        /// Update an existing skin with the contents of a path
        /// </summary>
        /// <param name="notification">The progress notification</param>
        /// <param name="task">The <see cref="ImportTask"/> to update the <paramref name="original"/> with</param>
        /// <param name="original">The <see cref="SkinInfo"/> to update</param>
        /// <returns></returns>
        public override async Task<Live<SkinInfo>?> ImportAsUpdate(ProgressNotification notification, ImportTask task, SkinInfo original)
        {
            return await Realm.WriteAsync<Live<SkinInfo>?>(r =>
            {
                var skinInfo = r.Find<SkinInfo>(original.ID)!;

                if (!canUpdateExisting(skinInfo))
                    throw new InvalidOperationException("This skin cannot be updated through the Realm package importer.");

                skinInfo.Files.Clear();

                string[] filesInMountedDirectory = Directory.EnumerateFiles(task.Path, "*.*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(task.Path, f)).ToArray();

                foreach (string file in filesInMountedDirectory)
                {
                    using var stream = File.OpenRead(Path.Combine(task.Path, file));

                    modelManager.AddFile(skinInfo, stream, file, r);
                }

                string skinIniPath = Path.Combine(task.Path, "skin.ini");

                if (File.Exists(skinIniPath))
                {
                    using (var stream = File.OpenRead(skinIniPath))
                    using (var lineReader = new LineBufferedReader(stream))
                    {
                        var decodedSkinIni = new LegacySkinDecoder().Decode(lineReader);

                        if (!string.IsNullOrEmpty(decodedSkinIni.SkinInfo.Name))
                            skinInfo.Name = decodedSkinIni.SkinInfo.Name;

                        if (!string.IsNullOrEmpty(decodedSkinIni.SkinInfo.Creator))
                            skinInfo.Creator = decodedSkinIni.SkinInfo.Creator;
                    }
                }

                return skinInfo.ToLive(Realm);
            }).ConfigureAwait(false);
        }

        protected override void Populate(SkinInfo model, ArchiveReader? archive, Realm realm, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Never activate the CLR type string from an imported skininfo.json. The skin archive reader resolves it to a
            // closed compatibility kind before model construction; this second application also protects direct ImportModel callers.
            model.InstantiationInfo = getInstantiationInfo(SkinArchiveInstantiationPolicy.Resolve(model.InstantiationInfo));

            // Always rewrite instantiation info (even after parsing in from the skin json) for sanity.
            model.InstantiationInfo = resolveInstantiationInfo(createInstance(model));

            cancellationToken.ThrowIfCancellationRequested();
            checkSkinIniMetadata(model, realm, cancellationToken);
        }

        private void checkSkinIniMetadata(
            SkinInfo item,
            Realm realm,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var instance = createInstance(item);

            // This function can be run on fresh import or save. The logic here ensures a skin.ini file is in a good state for both operations.
            // `Skin` will parse the skin.ini and populate `Skin.Configuration` during construction above.
            string skinIniSourcedName = instance.Configuration.SkinInfo.Name;
            string skinIniSourcedCreator = instance.Configuration.SkinInfo.Creator;
            string archiveName = item.Name.Replace(@".osk", string.Empty, StringComparison.OrdinalIgnoreCase);

            bool isImport = !item.IsManaged;

            if (isImport)
            {
                item.Name = !string.IsNullOrEmpty(skinIniSourcedName) ? skinIniSourcedName : archiveName;
                item.Creator = !string.IsNullOrEmpty(skinIniSourcedCreator) ? skinIniSourcedCreator : unknown_creator_string;

                // For imports, we want to use the archive or folder name as part of the metadata, in addition to any existing skin.ini metadata.
                // In an ideal world, skin.ini would be the only source of metadata, but a lot of skin creators and users don't update it when making modifications.
                // In both of these cases, the expectation from the user is that the filename or folder name is displayed somewhere to identify the skin.
                if (archiveName != item.Name
                    // lazer exports use this format
                    // GetValidFilename accounts for skins with non-ASCII characters in the name that have been exported by lazer.
                    && archiveName != item.GetDisplayString().GetValidFilename())
                    item.Name = @$"{item.Name} [{archiveName}]";
            }

            // By this point, the metadata in SkinInfo will be correct.
            // Regardless of whether this is an import or not, let's write the skin.ini if non-existing or non-matching.
            // This is (weirdly) done inside ComputeHash to avoid adding a new method to handle this case. After switching to realm it can be moved into another place.
            if (skinIniSourcedName != item.Name)
                UpdateSkinIniMetadata(item, realm, cancellationToken);
        }

        public void UpdateSkinIniMetadata(
            SkinInfo item,
            Realm realm,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string nameLine = @$"Name: {item.Name}";
            string authorLine = @$"Author: {item.Creator}";

            List<string> newLines = new List<string>
            {
                @"// The following content was automatically added by osu! in order to use metadata that more closely matches user expectations.",
                @"[General]",
                nameLine,
                authorLine,
            };

            var existingFile = item.GetFile(@"skin.ini");

            if (existingFile == null)
            {
                // skins without a skin.ini are supposed to import using the "latest version" spec, unless we're making a copy of the retro skin which specifies 1.0.
                // see https://github.com/peppy/osu-stable-reference/blob/1531237b63392e82c003c712faa028406073aa8f/osu!/Graphics/Skinning/SkinManager.cs#L297-L298
                decimal version = item.InstantiationInfo == typeof(RetroSkin).GetInvariantInstantiationInfo() ? 1.0M : SkinConfiguration.LATEST_VERSION;
                newLines.Add(FormattableString.Invariant($"Version: {version}"));

                // In the case a skin doesn't have a skin.ini yet, let's create one.
                writeNewSkinIni();
            }
            else
            {
                using (Stream stream = new MemoryStream())
                {
                    using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                    {
                        using (var existingStream = Files.Storage.GetStream(existingFile.File.GetStoragePath()))
                        using (var sr = new StreamReader(existingStream))
                        {
                            string? line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                sw.WriteLine(line);
                            }
                        }

                        sw.WriteLine();

                        foreach (string line in newLines)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            sw.WriteLine(line);
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    modelManager.ReplaceFile(item, existingFile, stream, realm);
                }
            }

            // The hash is already populated at this point in import.
            // As we have changed files, it needs to be recomputed.
            cancellationToken.ThrowIfCancellationRequested();
            item.Hash = ComputeHash(item, cancellationToken);

            void writeNewSkinIni()
            {
                using (Stream stream = new MemoryStream())
                {
                    using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                    {
                        foreach (string line in newLines)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            sw.WriteLine(line);
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    modelManager.AddFile(item, stream, @"skin.ini", realm);
                }

                cancellationToken.ThrowIfCancellationRequested();
                item.Hash = ComputeHash(item, cancellationToken);
            }
        }

        private Skin createInstance(SkinInfo item) => item.CreateInstance(skinResources);

        // OMS: route plain legacy skins through the BMS ruleset's BmsLegacySkin so imported/saved user skins additionally
        // parse the [Bms] skin.ini sections (the inherited LegacySkin still handles mania/general sections). Resolved by
        // reflection so osu.Game keeps no compile-time dependency on the ruleset; when the BMS ruleset assembly is absent
        // this stays null and skins keep their original type, leaving non-OMS environments completely untouched.
        private static readonly string? bms_legacy_skin_instantiation_info =
            Type.GetType("osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms")?.GetInvariantInstantiationInfo();

        private static string resolveInstantiationInfo(Skin instance)
            => instance.GetType() == typeof(LegacySkin) && bms_legacy_skin_instantiation_info != null
                ? bms_legacy_skin_instantiation_info
                : instance.GetType().GetInvariantInstantiationInfo();

        private static string getInstantiationInfo(SkinArchiveInstantiationKind kind) => kind switch
        {
            SkinArchiveInstantiationKind.Legacy => bms_legacy_skin_instantiation_info ?? typeof(LegacySkin).GetInvariantInstantiationInfo(),
            SkinArchiveInstantiationKind.DefaultLegacy => typeof(DefaultLegacySkin).GetInvariantInstantiationInfo(),
            SkinArchiveInstantiationKind.Triangles => typeof(TrianglesSkin).GetInvariantInstantiationInfo(),
            SkinArchiveInstantiationKind.Argon => typeof(ArgonSkin).GetInvariantInstantiationInfo(),
            SkinArchiveInstantiationKind.ArgonPro => typeof(ArgonProSkin).GetInvariantInstantiationInfo(),
            SkinArchiveInstantiationKind.Retro => typeof(RetroSkin).GetInvariantInstantiationInfo(),
            SkinArchiveInstantiationKind.Oms => typeof(OmsSkin).GetInvariantInstantiationInfo(),
            _ => bms_legacy_skin_instantiation_info ?? typeof(LegacySkin).GetInvariantInstantiationInfo(),
        };

        /// <summary>
        /// Save a skin, serialising any changes to skin layouts to relevant JSON structures.
        /// </summary>
        /// <returns>Whether any change actually occurred.</returns>
        public bool Save(Skin skin)
        {
            bool hadChanges = false;

            skin.SkinInfo.PerformWrite(s =>
            {
                // Update for safety
                s.InstantiationInfo = resolveInstantiationInfo(skin);

                // Serialise out the SkinInfo itself.
                string skinInfoJson = JsonConvert.SerializeObject(s, new JsonSerializerSettings { Formatting = Formatting.Indented });

                using (var streamContent = new MemoryStream(Encoding.UTF8.GetBytes(skinInfoJson)))
                {
                    modelManager.AddFile(s, streamContent, skin_info_file, s.Realm!);
                }

                // Then serialise each of the drawable component groups into respective files.
                foreach (var drawableInfo in skin.LayoutInfos)
                {
                    string json = JsonConvert.SerializeObject(drawableInfo.Value, new JsonSerializerSettings { Formatting = Formatting.Indented });

                    using (var streamContent = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        string filename = @$"{drawableInfo.Key}.json";

                        var oldFile = s.GetFile(filename);

                        if (oldFile != null)
                            modelManager.ReplaceFile(s, oldFile, streamContent, s.Realm!);
                        else
                            modelManager.AddFile(s, streamContent, filename, s.Realm!);
                    }
                }

                string newHash = ComputeHash(s);

                hadChanges = newHash != s.Hash;

                s.Hash = newHash;
            });

            return hadChanges;
        }
    }
}
