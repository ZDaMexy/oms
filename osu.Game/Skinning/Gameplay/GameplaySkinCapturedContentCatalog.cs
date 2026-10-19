// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// C2-owned encoded content. All package reads end at Capture; a gameplay root only composes retained values.
    /// Inactive images are identified for validity and individual bounds, but never decoded into pixel buffers.
    /// </summary>
    internal sealed class GameplaySkinCapturedContentCatalog
    {
        private readonly string packageRevision;
        private readonly Guid recordId;
        private readonly GameplaySkinScriptAuthorizationStore? authorizations;
        private readonly GameplaySkinContentCatalog catalog;
        private readonly GameplaySkinSceneManifest baseManifest;
        private readonly GameplaySkinSceneDocument baseScene;
        private readonly string baseIni;
        private readonly IReadOnlyDictionary<string, CapturedFile> files;
        private readonly IReadOnlyDictionary<GameplaySkinContentChoice, Branch> branches;
        private readonly GameplaySkinOptionPreviews optionPreviews;

        private GameplaySkinCapturedContentCatalog(string packageRevision, Guid recordId, GameplaySkinScriptAuthorizationStore? authorizations,
                                                  GameplaySkinContentCatalog catalog, GameplaySkinSceneManifest manifest,
                                                  GameplaySkinSceneDocument scene, string baseIni, Dictionary<string, CapturedFile> files,
                                                  Dictionary<GameplaySkinContentChoice, Branch> branches, GameplaySkinOptionPreviews optionPreviews)
        {
            this.packageRevision = packageRevision;
            this.recordId = recordId;
            this.authorizations = authorizations;
            this.catalog = catalog;
            baseManifest = manifest;
            baseScene = scene;
            this.baseIni = baseIni;
            this.files = files;
            this.branches = branches;
            this.optionPreviews = optionPreviews;
        }

        internal static GameplaySkinPreparedPackage Capture(Func<string, int, byte[]?> read, string packageRevision, Guid recordId,
                                                             GameplaySkinScriptAuthorizationStore? authorizations,
                                                             GameplaySkinSceneManifest manifest, byte[] manifestBytes, CancellationToken token,
                                                             Func<string, SkinPackageRevisionCapsule.ResourceReference?>? retainResource)
        {
            if (manifest.ScriptFile != null)
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
            var capture = new CaptureFiles(read, retainResource);
            capture.Add(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, manifestBytes, structured: true);
            GameplaySkinContentCatalog catalog = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeContentCatalog(
                capture.Read(manifest.ContentCatalogFile!, GameplaySkinContentBudgets.MAX_CATALOG_BYTES, structured: true)));
            GameplaySkinOptionDefinition[] options = [.. manifest.Options, .. catalog.Modules.Select(module => module.Option)];
            if (options.Length > GameplaySkinOptionBudgets.MAX_OPTIONS)
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            if (options.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count() != options.Length)
                throw fail(GameplaySkinSceneDiagnosticCode.DuplicateStableId);
            manifest = withOptions(manifest, options, manifest.ContentCatalogFile);
            GameplaySkinOptionPreviews optionPreviews = GameplaySkinOptionPreviews.Capture(options, read, token);
            validateContentVariantSelectors(catalog, manifest);
            GameplaySkinSceneDocument scene = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeScene(
                capture.Read(manifest.SceneFile, GameplaySkinSceneBudgets.MAX_SCENE_BYTES, structured: true), manifest));
            // A neutral dispatcher preserves each fragment's existing target and C3 geometry without introducing an
            // additional transformed or buffered parent around independently-authored public slot consumers.
            if (scene.Root.Type != GameplaySkinSceneNodeType.Container || scene.Root.Target.Kind != GameplaySkinSceneTargetKind.Global
                || scene.Root.Blend != GameplaySkinSceneBlendMode.Inherit || scene.Root.SlotId != null
                || scene.Root.Properties.Count != 0 || scene.Root.Effects.Count != 0
                || scene.Tracks.Any(track => track.TargetNodeId == scene.Root.Id)
                || scene.Bindings.Any(binding => binding.TargetNodeId == scene.Root.Id)
                || scene.Variants.Any(variant => variant.TargetNodeId == scene.Root.Id)
                || scene.StateMachines.SelectMany(machine => machine.States).SelectMany(state => state.Assignments)
                    .Any(assignment => assignment.TargetNodeId == scene.Root.Id))
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);

            byte[] iniBytes = capture.Read("skin.ini", GameplaySkinContentBudgets.MAX_INI_BYTES, structured: true);
            GameplaySkinDocument baseDocument = decodeIni(iniBytes);
            string baseIni = GameplaySkinDocumentCodec.Encode(baseDocument);
            captureResources(manifest, capture, token);

            var branches = new Dictionary<GameplaySkinContentChoice, Branch>();
            var sharedBranches = new Dictionary<(string?, string?, string?), Branch>();
            foreach (GameplaySkinContentChoice choice in catalog.Modules.SelectMany(module => module.Choices)
                .SelectMany(choice => new[] { choice }.Concat(choice.Variants.Select(variant => variant.Contribution))))
            {
                token.ThrowIfCancellationRequested();
                var key = (choice.ManifestFile, choice.SceneFile, choice.IniFile);
                if (!sharedBranches.TryGetValue(key, out Branch? branch))
                {
                    GameplaySkinSceneManifest? branchManifest = null;
                    GameplaySkinSceneDocument? branchScene = null;
                    if (choice.ManifestFile != null)
                    {
                        branchManifest = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeManifest(
                            capture.Read(choice.ManifestFile, GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES, structured: true)));
                        if (branchManifest.ContentCatalogFile != null || branchManifest.Options.Count != 0 || branchManifest.ScriptFile != null
                            || branchManifest.AuxiliaryDisplay != null)
                            throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
                        GameplaySkinSceneManifest combined = mergeManifest(manifest, new[] { branchManifest });
                        branchScene = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeScene(
                            capture.Read(choice.SceneFile!, GameplaySkinSceneBudgets.MAX_SCENE_BYTES, structured: true), combined));
                        captureResources(branchManifest, capture, token);
                    }
                    string ini = choice.IniFile == null ? string.Empty : GameplaySkinDocumentCodec.Encode(
                        decodeIni(capture.Read(choice.IniFile, GameplaySkinContentBudgets.MAX_INI_BYTES, structured: true)));
                    branch = new Branch(branchManifest, branchScene, ini);
                    // Validate inactive alternatives against the base as well: IDs, references, slot ownership and
                    // the original per-active-program budgets are checked without allocating any image pixels.
                    if (branchManifest != null)
                    {
                        GameplaySkinSceneManifest combined = mergeManifest(manifest, new[] { branchManifest });
                        mergeScene(scene, new[] { branchScene! }, combined);
                    }
                    decodeIni(Encoding.UTF8.GetBytes(baseIni + "\n" + ini));
                    sharedBranches.Add(key, branch);
                }
                branches.Add(choice, branch);
            }
            token.ThrowIfCancellationRequested();
            var captured = new GameplaySkinCapturedContentCatalog(packageRevision, recordId, authorizations,
                catalog, manifest, scene, baseIni, capture.Files, branches, optionPreviews);
            return new GameplaySkinPreparedPackage(packageRevision, manifest, scene,
                Array.Empty<GameplaySkinCapturedSceneResource>(), null, null, captured, optionPreviews: optionPreviews);
        }

        private static void validateContentVariantSelectors(GameplaySkinContentCatalog catalog, GameplaySkinSceneManifest manifest)
        {
            foreach (GameplaySkinContentModule module in catalog.Modules)
                foreach (GameplaySkinContentVariant variant in module.Choices.SelectMany(choice => choice.Variants))
                    foreach ((string id, string value) in variant.When)
                    {
                        // Catalog choices are already frozen scalars. Referencing another module reads its value;
                        // it never evaluates that module's contribution or creates an ordering/dependency graph.
                        if (id == module.Option.Id || !manifest.TryGetOption(id, out GameplaySkinOptionDefinition definition)
                            || definition.Kind is not (GameplaySkinOptionKind.Choice or GameplaySkinOptionKind.Toggle)
                            || !definition.TryValidateValue(value, out _))
                            throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
                    }
        }

        internal GameplaySkinPreparedPackage PrepareSelected(GameplaySkinOptionValues options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Branch[] selected = [.. selectedBranches(options)];
            GameplaySkinSceneManifest manifest = mergeManifest(baseManifest, selected.Where(branch => branch.Manifest != null).Select(branch => branch.Manifest!));
            GameplaySkinSceneDocument document = mergeScene(baseScene, selected.Where(branch => branch.Scene != null).Select(branch => branch.Scene!), manifest);
            GameplaySkinDocument materials = decodeIni(Encoding.UTF8.GetBytes(baseIni + "\n" + string.Join("\n", selected.Select(branch => branch.Ini))));
            return GameplaySkinPreparedPackage.PrepareActive(readCaptured, packageRevision, recordId, authorizations,
                manifest, document, token, materials, optionPreviews);
        }

        private byte[]? readCaptured(string path, int limit)
        {
            if (!files.TryGetValue(path, out CapturedFile? file))
                return null;
            if (file.Length > limit)
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            return file.ReadOwnedBytes();
        }

        private IEnumerable<Branch> selectedBranches(GameplaySkinOptionValues options)
        {
            foreach (GameplaySkinContentModule module in catalog.Modules)
            {
                if (!options.TryGetValue(module.Option.Id, out GameplaySkinOptionValue value))
                {
                    if (module.Option.Rulesets.Count == 0 && module.Option.Keymodes.Count == 0)
                        throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
                    continue;
                }
                if (value.Kind != GameplaySkinOptionKind.Choice || !module.Option.TryValidateValue(value.CanonicalValue, out _))
                    throw fail(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                GameplaySkinContentChoice choice = module.Choices.First(candidate => candidate.Value == value.CanonicalValue);
                GameplaySkinContentVariant? variant = choice.Variants.FirstOrDefault(candidate => candidate.When.All(condition =>
                    options.TryGetValue(condition.Key, out GameplaySkinOptionValue selector) && selector.CanonicalValue == condition.Value));
                yield return branches[variant?.Contribution ?? choice];
            }
        }

        private static GameplaySkinSceneManifest withOptions(GameplaySkinSceneManifest source, IEnumerable<GameplaySkinOptionDefinition> options, string? catalogFile)
            => new(source.Resources, source.ScriptFile, source.BitmapFonts, options,
                source.AuthorLayouts, catalogFile, source.Animations, source.AuxiliaryDisplay);

        private static GameplaySkinSceneManifest mergeManifest(GameplaySkinSceneManifest basis, IEnumerable<GameplaySkinSceneManifest> fragments)
        {
            JObject result = JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(basis));
            result.Remove("contentCatalog");
            foreach (GameplaySkinSceneManifest fragment in fragments)
            {
                JObject source = JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(fragment));
                foreach (string field in new[] { "resources", "bitmapFonts", "layouts", "animations" })
                {
                    if (source[field] is not JArray additions)
                        continue;
                    if (result[field] is not JArray destination)
                        result[field] = destination = [];
                    foreach (JToken addition in additions)
                    {
                        JToken? existing = destination.FirstOrDefault(item => (string?)item["id"] == (string?)addition["id"]);
                        if (existing == null)
                            destination.Add(addition.DeepClone());
                        else if (!JToken.DeepEquals(existing, addition))
                            throw fail(GameplaySkinSceneDiagnosticCode.DuplicateStableId);
                    }
                }
            }
            GameplaySkinSceneManifest decoded = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeManifest(result.ToString(Formatting.None)));
            return withOptions(decoded, basis.Options, null);
        }

        private static GameplaySkinSceneDocument mergeScene(GameplaySkinSceneDocument basis, IEnumerable<GameplaySkinSceneDocument> fragments,
                                                            GameplaySkinSceneManifest manifest)
        {
            JObject result = JObject.Parse(GameplaySkinSceneCodec.EncodeScene(basis));
            foreach (GameplaySkinSceneDocument fragment in fragments)
            {
                JObject source = JObject.Parse(GameplaySkinSceneCodec.EncodeScene(fragment));
                ((JArray)result["root"]!["children"]!).Add(source["root"]!.DeepClone());
                foreach (string field in new[] { "tracks", "stateMachines", "bindings", "variants", "templates", "instances" })
                    foreach (JToken addition in (JArray)source[field]!)
                        ((JArray)result[field]!).Add(addition.DeepClone());
                if (fragment.CompletionOutro > ((double?)result["completionOutro"] ?? 0))
                    result["completionOutro"] = fragment.CompletionOutro;
            }
            return GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeScene(result.ToString(Formatting.None), manifest));
        }

        private static GameplaySkinDocument decodeIni(byte[] bytes)
        {
            string revision = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(bytes, GameplaySkinDocumentIdentity.CreateUnboundPackageParse(revision));
            if (document.Diagnostics.Count != 0)
                throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
            return document;
        }

        private static void captureResources(GameplaySkinSceneManifest manifest, CaptureFiles capture, CancellationToken token)
        {
            foreach (GameplaySkinSceneResource resource in manifest.Resources)
            {
                token.ThrowIfCancellationRequested();
                if (!capture.IdentifiedImages.Add(resource.Path))
                    continue;
                byte[] bytes = capture.Read(resource.Path, GameplaySkinPreparedSceneBudgets.MAX_RESOURCE_BYTES);
                var info = GameplaySkinPreparedPackage.Identify(bytes);
                long pixels = (long)info.Width * info.Height;
                if (pixels <= 0 || pixels > GameplaySkinPreparedSceneBudgets.MAX_TEXTURE_PIXELS)
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
            foreach (GameplaySkinRecordedAnimationDefinition animation in manifest.Animations)
                capture.Read(animation.Path, GameplaySkinRecordedAnimationBudgets.MAX_ENCODED_BYTES, structured: true);
        }

        private sealed class Branch
        {
            public GameplaySkinSceneManifest? Manifest { get; }
            public GameplaySkinSceneDocument? Scene { get; }
            public string Ini { get; }

            public Branch(GameplaySkinSceneManifest? manifest, GameplaySkinSceneDocument? scene, string ini)
            {
                Manifest = manifest;
                Scene = scene;
                Ini = ini;
            }
        }

        private sealed class CapturedFile
        {
            private readonly byte[]? ownedBytes;
            private readonly SkinPackageRevisionCapsule.ResourceReference? reference;

            public int Length { get; }

            public CapturedFile(byte[] bytes)
            {
                ownedBytes = [.. bytes];
                Length = bytes.Length;
            }

            public CapturedFile(SkinPackageRevisionCapsule.ResourceReference reference)
            {
                this.reference = reference;
                Length = reference.Length;
            }

            public byte[] ReadOwnedBytes() => reference?.ReadOwnedBytes() ?? ownedBytes!;
        }

        private sealed class CaptureFiles
        {
            private readonly Func<string, int, byte[]?> read;
            private readonly Func<string, SkinPackageRevisionCapsule.ResourceReference?>? retainResource;
            private long totalBytes;
            private long structuredBytes;
            public Dictionary<string, CapturedFile> Files { get; } = new Dictionary<string, CapturedFile>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> IdentifiedImages { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public CaptureFiles(Func<string, int, byte[]?> read, Func<string, SkinPackageRevisionCapsule.ResourceReference?>? retainResource)
            {
                this.read = read;
                this.retainResource = retainResource;
            }

            public byte[] Read(string path, int limit, bool structured = false)
            {
                if (Files.TryGetValue(path, out CapturedFile? existing))
                {
                    if (existing.Length > limit)
                        throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    return existing.ReadOwnedBytes();
                }

                if (retainResource != null)
                {
                    SkinPackageRevisionCapsule.ResourceReference reference = retainResource(path)
                                                                           ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
                    if (reference.Length > limit)
                        throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    var file = new CapturedFile(reference);
                    add(path, file, structured);
                    return file.ReadOwnedBytes();
                }

                byte[] bytes = read(path, limit) ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
                if (bytes.Length > limit)
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                Add(path, bytes, structured);
                return Files[path].ReadOwnedBytes();
            }

            public void Add(string path, byte[] bytes, bool structured)
            {
                if (retainResource != null)
                {
                    add(path, new CapturedFile(retainResource(path) ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference)), structured);
                    return;
                }

                reserve(bytes.Length, structured);
                Files.Add(path, new CapturedFile(bytes));
            }

            private void add(string path, CapturedFile file, bool structured)
            {
                reserve(file.Length, structured);
                Files.Add(path, file);
            }

            private void reserve(int length, bool structured)
            {
                totalBytes += length;
                if (structured)
                    structuredBytes += length;
                if (Files.Count >= GameplaySkinContentBudgets.MAX_CAPTURED_FILES || totalBytes > GameplaySkinContentBudgets.MAX_CAPTURED_BYTES
                    || structuredBytes > GameplaySkinContentBudgets.MAX_STRUCTURED_BYTES)
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
        }

        private static GameplaySkinScenePreparationException fail(GameplaySkinSceneDiagnosticCode code) => new(code);
    }
}
