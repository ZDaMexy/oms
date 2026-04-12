// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Manages a JSON-based configuration file for registered external beatmap library roots.
    /// File: <c>library-roots.json</c> in the OMS data root.
    /// </summary>
    public class ExternalLibraryConfig
    {
        private const string config_filename = "library-roots.json";

        private readonly Storage storage;
        private List<ExternalLibraryRoot> roots = new List<ExternalLibraryRoot>();

        public IReadOnlyList<ExternalLibraryRoot> Roots => roots;

        public ExternalLibraryConfig(Storage storage)
        {
            this.storage = storage;
            load();
        }

        /// <summary>
        /// Add a new library root. No-op if the path is already registered.
        /// </summary>
        public bool AddRoot(string path, ExternalLibraryRootType type)
        {
            string normalised = Path.GetFullPath(path);

            if (roots.Any(r => string.Equals(r.Path, normalised, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (!Directory.Exists(normalised))
                throw new DirectoryNotFoundException($"External library root not found: {normalised}");

            roots.Add(new ExternalLibraryRoot
            {
                Path = normalised,
                Type = type,
                Enabled = true,
            });

            save();
            return true;
        }

        /// <summary>
        /// Remove a registered library root by path.
        /// </summary>
        public bool RemoveRoot(string path)
        {
            string normalised = Path.GetFullPath(path);
            int removed = roots.RemoveAll(r => string.Equals(r.Path, normalised, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                save();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Record that the given root has just been scanned.
        /// </summary>
        public void MarkScanned(ExternalLibraryRoot root)
        {
            root.LastScanTime = DateTimeOffset.UtcNow;
            save();
        }

        /// <summary>
        /// Get all enabled roots of the specified type.
        /// </summary>
        public IEnumerable<ExternalLibraryRoot> GetEnabledRoots(ExternalLibraryRootType? type = null)
            => roots.Where(r => r.Enabled && (type == null || r.Type == type));

        private void load()
        {
            try
            {
                if (!storage.Exists(config_filename))
                    return;

                using var stream = storage.GetStream(config_filename);
                using var reader = new StreamReader(stream);

                string json = reader.ReadToEnd();
                var deserialized = JsonConvert.DeserializeObject<List<ExternalLibraryRoot>>(json);

                if (deserialized != null)
                    roots = deserialized;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load external library configuration.");
            }
        }

        private void save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(roots, Formatting.Indented);

                using var stream = storage.CreateFileSafely(config_filename);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save external library configuration.");
            }
        }
    }
}
