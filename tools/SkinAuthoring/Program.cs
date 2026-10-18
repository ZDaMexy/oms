// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;
using osu.Game.Skinning.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SkinAuthoring
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions json_options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private static readonly UTF8Encoding utf8 = new(false, true);
        private static readonly DateTimeOffset package_epoch = new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        private const long max_package_bytes = 128 * 1024 * 1024;

        public static async Task<int> Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            if (args.Length < 2 || args[0] is not ("generate" or "check" or "pack" or "new" or "catalog" or "preview"))
            {
                Console.WriteLine("generate SOURCE | check SOURCE | pack SOURCE OUTPUT.osk | new TEMPLATE DESTINATION NAME | catalog OUTPUT.md | preview SOURCE OUTPUT.png");
                return 2;
            }

            try
            {
                switch (args[0])
                {
                    case "generate":
                        var profile = JsonSerializer.Deserialize<AuthorProfile>(File.ReadAllText(Path.Combine(args[1], "author.json")), json_options)
                                      ?? throw new InvalidDataException("author.json: 制作配置为空。");
                        SkinRecipe.Generate(Path.GetFullPath(args[1]), profile);
                        Console.WriteLine("素材、两种玩法设置与演出文件已重新生成。继续检查，再打包。");
                        break;
                    case "check":
                        check(Path.GetFullPath(args[1]));
                        break;
                    case "pack" when args.Length == 3:
                        await pack(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])).ConfigureAwait(false);
                        break;
                    case "new" when args.Length == 4:
                        create(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), args[3]);
                        break;
                    case "catalog":
                        File.WriteAllText(args[1], GameplaySkinSlotCatalogDocumentation.GenerateMarkdownTable() + "\n" + information_bindings, utf8);
                        break;
                    case "preview" when args.Length == 3:
                        SkinPreview.Create(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                        break;
                    default:
                        return 2;
                }

                return 0;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException
                                        or GameplaySkinScriptException or GameplaySkinScenePreparationException or ImageFormatException)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        private const string information_bindings = """
            ## 游玩信息绑定

            下列公开只读字段可在普通场景文件中绑定。数值绑定保持原值；绑定到 `text` 时使用表内显示方式。

            | 字段 | 数值范围 | 文字显示 |
            | --- | --- | --- |
            | `score.accuracy` | `0..1` | 两位小数百分比，如 `98.75%` |
            | `timing.progress` | `0..1` | 整数百分比，如 `42%` |

            准确率来自当前玩法的实际得分状态。进度沿当前谱面的可玩起止时间与统一游玩时钟：首个物件前为 0，最后物件后为 1，零时长为 0。暂停时保持不变；重试和跳转跟随同一游玩状态更新，新加入的观察者会收到当前完整状态。它们不会改动判定或计分规则。

            完整绑定写法与成品例子见 [制作说明](AUTHORING.md)。
            """;

        private static string[] captureFiles(string root, bool hasContentCatalog)
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("作者目录不能是链接。请复制为普通目录后重试。");
            var files = new List<string>();
            long total = 0;
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.TryPop(out string? directory))
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
                {
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException($"{Path.GetRelativePath(root, path)}: 不打包链接。请使用包内普通文件。");
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(path);
                        continue;
                    }

                    total += new FileInfo(path).Length;
                    if (total > (hasContentCatalog ? GameplaySkinContentBudgets.MAX_CAPTURED_BYTES : max_package_bytes)
                        || files.Count >= (hasContentCatalog ? GameplaySkinContentBudgets.MAX_CAPTURED_FILES : 4096))
                        throw new InvalidDataException(hasContentCatalog
                            ? "内容目录包超出上限（512 MiB / 8192 个文件）。"
                            : "作者包超出工具上限（128 MiB / 4096 个文件）。");
                    files.Add(path);
                }
            }

            return files.OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal).ToArray();
        }

        private static Dictionary<string, byte[]> check(string root)
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("作者目录不能是链接。请复制为普通目录后重试。");
            string manifestPath = Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
            byte[]? initialManifest = File.Exists(manifestPath) ? readBoundedFile(manifestPath, GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES) : null;
            GameplaySkinSceneManifest? declaration = initialManifest == null ? null : requireValid(
                GameplaySkinSceneCodec.DecodeManifest(initialManifest), GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
            bool hasContentCatalog = declaration?.ContentCatalogFile != null;
            long maxBytes = hasContentCatalog ? GameplaySkinContentBudgets.MAX_CAPTURED_BYTES : max_package_bytes;
            string[] files = captureFiles(root, hasContentCatalog);
            var captured = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            foreach (string path in files)
            {
                byte[] bytes = readBoundedFile(path, Math.Min(SkinPackageRevisionCapsuleLimits.Default.MaxFileBytes, maxBytes - totalBytes));
                totalBytes += bytes.Length;
                captured.Add(Path.GetRelativePath(root, path).Replace('\\', '/'), bytes);
            }
            captured.TryGetValue(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, out byte[]? manifestBytes);
            if (initialManifest == null ? manifestBytes != null : manifestBytes == null || !initialManifest.AsSpan().SequenceEqual(manifestBytes))
                throw new InvalidDataException("读取期间演出清单发生变化，请保持作者目录不变后重新检查。");
            if (!captured.TryGetValue("skin.ini", out byte[]? ini))
                throw new InvalidDataException("skin.ini: 皮肤根目录缺少设置文件。");
            var folderCandidate = SkinPackageRevisionCapsuleFactory.Create(captured
                .Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray());
            if (!folderCandidate.IsSuccess)
                throw new InvalidDataException($"作者目录不能由游戏读取：{folderCandidate.RejectionReason}。");
            string revision;
            using (var capsule = folderCandidate.Capsule!)
            {
                if (!SkinManagedFolderPackageMetadataReader.TryRead(capsule, out _))
                    throw new InvalidDataException("skin.ini: 目录皮肤无法读取名称或作者，请保持文件不超过 1 MiB、名称与作者各不超过 256 字符，且不含控制字符。");
                revision = capsule.ContentRevision;
            }
            var errors = new List<string>();
            validateIni("skin.ini", ini, captured, errors);
            if (hasContentCatalog)
            {
                // A misspelled or unselected INI is still package input. Do not let an inactive contribution hide
                // an invalid resource declaration behind the currently selected default.
                foreach ((string path, byte[] bytes) in captured.Where(item => item.Key.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
                                                                             && !item.Key.Equals("skin.ini", StringComparison.OrdinalIgnoreCase)))
                    validateIni(path, bytes, captured, errors);
            }
            var previewPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (declaration != null)
            {
                IEnumerable<GameplaySkinOptionDefinition> options = declaration.Options;
                if (declaration.ContentCatalogFile != null && captured.TryGetValue(declaration.ContentCatalogFile, out byte[]? catalogBytes))
                    options = options.Concat(requireValid(GameplaySkinSceneCodec.DecodeContentCatalog(catalogBytes), declaration.ContentCatalogFile)
                        .Modules.Select(module => module.Option));
                foreach (GameplaySkinOptionChoice choice in options.SelectMany(option => option.Choices))
                    if (choice.PreviewPath != null)
                        previewPaths.Add(choice.PreviewPath);
            }
            validateImages(captured, hasContentCatalog, previewPaths, errors);
            if (errors.Count > 0)
                throw new InvalidDataException(string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal)));

            if (declaration != null)
            {
                string lastRead = GameplaySkinSceneContracts.MANIFEST_FILE_NAME;
                byte[]? read(string path, int limit)
                {
                    lastRead = path;
                    if (!captured.TryGetValue(path, out byte[]? bytes))
                        return null;
                    if (bytes.Length > limit)
                        throw new InvalidDataException($"{path}: 文件超过此类内容的预算。");
                    return bytes;
                }
                GameplaySkinPreparedPackage package;
                try
                {
                    package = GameplaySkinPreparedPackage.Prepare(read, revision, Guid.Empty, null, CancellationToken.None);
                    lastRead = "皮肤配置缩略图";
                    package.OptionPreviews.ValidateAll(CancellationToken.None);
                }
                catch (GameplaySkinScenePreparationException error)
                {
                    throw new InvalidDataException($"{lastRead}: {error.Message}", error);
                }
                if (hasContentCatalog)
                    validateContentCatalog(package, declaration, captured, read);
                else
                    validateBitmapGlyphs(package);
            }
            Console.WriteLine("文件、公开设置、图片与演出语法检查通过。导入后仍需核对实际游玩画面与不同键数。");
            return captured;
        }

        private static byte[] readBoundedFile(string path, long limit)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"{Path.GetFileName(path)}: 不读取链接，请使用包内普通文件。");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > limit)
                throw new InvalidDataException($"{Path.GetFileName(path)}: 文件超过作者包或此类内容的字节预算。");
            byte[] bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
                throw new InvalidDataException($"{Path.GetFileName(path)}: 读取期间文件增长，请保持作者目录不变后重新检查。");
            return bytes;
        }

        private static T requireValid<T>(GameplaySkinSceneDecodeResult<T> result, string path) where T : class
            => result.Status == GameplaySkinSceneDecodeStatus.Valid && result.Value != null ? result.Value
                : throw new InvalidDataException($"{path}: {string.Join(", ", result.Diagnostics.Select(item => item.Id))}");

        private static void validateIni(string path, byte[] bytes, IReadOnlyDictionary<string, byte[]> captured, List<string> errors)
        {
            var document = GameplaySkinDocumentCodec.Decode(bytes, GameplaySkinDocumentIdentity.CreateUnboundPackageParse(Convert.ToHexString(SHA256.HashData(bytes))));
            foreach (var diagnostic in document.Diagnostics)
                errors.Add($"{path}:{diagnostic.LineNumber}: {diagnostic.Id}");
            foreach (var line in document.LegacySections.Where(section => section.Name == "Bms").SelectMany(section => section.Lines))
            {
                if (line.Key is GameplaySkinBgaLayout.CONFIGURATION_KEY or "BgaViewportsP1" or "BgaViewportsP2" && line.Value != null
                    && !GameplaySkinBgaLayout.TryParse(line.Value, out _, out string? diagnostic))
                    errors.Add($"{path}:{line.LineNumber}: {diagnostic}");
            }
            foreach (var line in document.LegacySections.Where(section => section.Name is "Bms" or "Mania").SelectMany(section => section.Lines))
            {
                if (line.Key == null || line.Value == null || !(line.Key.StartsWith("NoteImage", StringComparison.Ordinal)
                    || line.Key.StartsWith("KeyImage", StringComparison.Ordinal) || line.Key is "StageLeft" or "StageRight" or "StageBottom" or "StageHint"))
                    continue;
                string resource = line.Value.Trim();
                if (!safeResource(resource) || !captured.ContainsKey(resource + ".png") && !captured.ContainsKey(resource + "-0.png"))
                    errors.Add($"{path}:{line.LineNumber}: 找不到兼容素材 {resource}。");
            }
            foreach (var entry in document.Sections.SelectMany(section => section.Entries))
            {
                if (entry.Operation != GameplaySkinDocumentOperation.Provide || entry.Value == null)
                    continue;
                if (!safeResource(entry.Value))
                    errors.Add($"{path}:{entry.LineNumber}: 素材路径必须位于包内。");
                else if (!captured.ContainsKey(entry.Value + ".png") && !captured.ContainsKey(entry.Value + "-0.png"))
                    errors.Add($"{path}:{entry.LineNumber}: 找不到素材 {entry.Value}.png 或首帧 -0.png。");
            }
        }

        private static void validateImages(IReadOnlyDictionary<string, byte[]> captured, bool hasContentCatalog,
                                           IReadOnlySet<string> previewPaths, List<string> errors)
        {
            long pixels = 0;
            foreach ((string path, byte[] bytes) in captured.Where(item => item.Key.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            {
                // Declared settings thumbnails have their own small byte/dimension budget and complete decoder
                // validation below. They do not consume the gameplay resource budget merely by being in a package.
                if (previewPaths.Contains(path))
                    continue;
                if (bytes.Length > GameplaySkinPreparedSceneBudgets.MAX_RESOURCE_BYTES)
                {
                    errors.Add($"{path}: 单张图片超过 16 MiB。");
                    continue;
                }
                try
                {
                    var info = Image.Identify(bytes);
                    long area = (long)info.Width * info.Height;
                    pixels += area;
                    if (area <= 0 || area > GameplaySkinPreparedSceneBudgets.MAX_TEXTURE_PIXELS
                        || !hasContentCatalog && pixels > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_TEXTURE_PIXELS
                        || area * Math.Max(1, info.FrameMetadataCollection.Count) > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_TEXTURE_PIXELS)
                    {
                        errors.Add($"{path}: 解码后的图片尺寸超出预算。");
                        continue;
                    }
                    // Fully validate every PNG, including unknown/inactive files, one image at a time. A previous
                    // unrelated diagnostic must not cause the remaining package closure to skip its decoders.
                    using var decoded = Image.Load<Rgba32>(bytes);
                }
                catch (ImageFormatException)
                {
                    errors.Add($"{path}: 图片无法解码，请从图像编辑器重新保存有效 PNG。");
                }
            }
        }

        private static void validateBitmapGlyphs(GameplaySkinPreparedPackage package)
        {
            if (package.Manifest == null)
                return;
            var resources = package.Resources.ToDictionary(resource => resource.Source.Id, StringComparer.Ordinal);
            foreach (GameplaySkinBitmapFont font in package.Manifest.BitmapFonts)
                foreach (GameplaySkinBitmapGlyph glyph in font.Glyphs)
                {
                    GameplaySkinCapturedSceneResource texture = resources[glyph.ResourceId];
                    GameplaySkinBitmapGlyphRect? rect = glyph.Rect;
                    int width = rect?.Width ?? texture.Width;
                    int height = rect?.Height ?? texture.Height;
                    if (width > GameplaySkinBitmapFontBudgets.MAX_GLYPH_DIMENSION || height > font.LineHeight
                        || rect is { } bounds && ((long)bounds.X + bounds.Width > texture.Width || (long)bounds.Y + bounds.Height > texture.Height))
                        throw new InvalidDataException($"bitmapFonts:{font.Id}/{glyph.Character}: OMS-SKIN-SCENE-{(int)GameplaySkinSceneDiagnosticCode.InvalidResource:000}，字形超出原图或字体尺寸。");
                }
        }

        private static void validateContentCatalog(GameplaySkinPreparedPackage package, GameplaySkinSceneManifest declaration,
                                                   IReadOnlyDictionary<string, byte[]> captured, Func<string, int, byte[]?> read)
        {
            string catalogPath = declaration.ContentCatalogFile!;
            GameplaySkinContentCatalog catalog = requireValid(GameplaySkinSceneCodec.DecodeContentCatalog(captured[catalogPath]), catalogPath);
            var manifests = new Dictionary<string, GameplaySkinSceneManifest>(StringComparer.OrdinalIgnoreCase)
            {
                [GameplaySkinSceneContracts.MANIFEST_FILE_NAME] = declaration,
            };
            var structured = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "skin.ini", GameplaySkinSceneContracts.MANIFEST_FILE_NAME, declaration.SceneFile, catalogPath,
            };
            var declaredInis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameplaySkinContentChoice contribution in catalog.Modules.SelectMany(module => module.Choices)
                         .SelectMany(choice => new[] { choice }.Concat(choice.Variants.Select(variant => variant.Contribution))))
            {
                if (contribution.ManifestFile != null)
                {
                    structured.Add(contribution.ManifestFile);
                    structured.Add(contribution.SceneFile!);
                    if (!manifests.ContainsKey(contribution.ManifestFile))
                        manifests.Add(contribution.ManifestFile, requireValid(GameplaySkinSceneCodec.DecodeManifest(captured[contribution.ManifestFile]), contribution.ManifestFile));
                }
                if (contribution.IniFile != null)
                {
                    structured.Add(contribution.IniFile);
                    declaredInis.Add(contribution.IniFile);
                }
            }
            foreach (GameplaySkinSceneManifest manifest in manifests.Values)
            {
                foreach (GameplaySkinRecordedAnimationDefinition animation in manifest.Animations)
                {
                    structured.Add(animation.Path);
                    clips.Add(animation.Path);
                }
            }
            var errors = new List<string>();
            foreach ((string path, byte[] bytes) in captured)
            {
                bool json = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                bool compressedJson = path.EndsWith(".json.br", StringComparison.OrdinalIgnoreCase);
                if (json || compressedJson || path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                    structured.Add(path);
                // Receipt / author metadata may be outside the scene closure. It still counts toward the complete
                // structured budget, and malformed unknown JSON must not become unchecked package payload.
                if (json)
                {
                    try
                    {
                        using JsonDocument ignored = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = GameplaySkinSceneBudgets.MAX_JSON_DEPTH });
                    }
                    catch (JsonException)
                    {
                        errors.Add($"{path}: JSON 结构无效。");
                    }
                }
                if (compressedJson && !clips.Contains(path))
                    errors.Add($"{path}: 压缩演出文件未由任何内容分支的 animations 清单声明。");
            }
            if (structured.Sum(path => captured[path].LongLength) > GameplaySkinContentBudgets.MAX_STRUCTURED_BYTES)
                errors.Add("内容目录全部结构文件超过 64 MiB。");
            foreach (string path in declaredInis.Where(path => !path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)))
                validateIni(path, captured[path], captured, errors);
            if (errors.Count > 0)
                throw new InvalidDataException(string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal)));

            // Capture validates every inactive fragment against the base without image allocations. Decode each
            // declared clip as well, even when an exhaustive condition table makes its fallback unreachable.
            foreach ((string path, GameplaySkinSceneManifest manifest) in manifests)
            {
                try
                {
                    GameplaySkinRecordedAnimationCodec.PrepareAnimations(manifest, read, CancellationToken.None);
                }
                catch (GameplaySkinScenePreparationException error)
                {
                    throw new InvalidDataException($"{path}: {error.Message}", error);
                }
            }

            GameplaySkinSceneManifest frozenManifest = package.Manifest!;
            var optionsById = frozenManifest.Options.ToDictionary(option => option.Id, StringComparer.Ordinal);
            string[] keymodes = new[] { "7k" }.Concat(frozenManifest.Options.SelectMany(option => option.Keymodes))
                .Concat(manifests.Values.SelectMany(manifest => manifest.AuthorLayouts).Select(layout => layout.KeymodeId))
                .Distinct(StringComparer.Ordinal).ToArray();
            var contexts = new[] { "bms", "mania" }.SelectMany(ruleset => keymodes.Select(keymode => (Ruleset: ruleset, Keymode: keymode))).ToArray();
            var validated = new HashSet<string>(StringComparer.Ordinal);

            // Scope changes can enable a different set of modules. Cover each distinct default contribution set.
            foreach (var context in contexts)
                validateSelection(GameplaySkinOptionValues.Resolve(frozenManifest.Options, null, context.Ruleset, context.Keymode), "default", context.Ruleset, context.Keymode);

            foreach (GameplaySkinContentModule module in catalog.Modules)
            {
                foreach (GameplaySkinContentChoice choice in module.Choices)
                {
                    validateRepresentative(new Dictionary<string, string>(StringComparer.Ordinal) { [module.Option.Id] = choice.Value }, module.Option.Id + "/" + choice.Value);
                    for (int index = 0; index < choice.Variants.Count; index++)
                    {
                        var overrides = new Dictionary<string, string>(choice.Variants[index].When, StringComparer.Ordinal)
                        {
                            [module.Option.Id] = choice.Value,
                        };
                        validateRepresentative(overrides, module.Option.Id + "/" + choice.Value + $"/variant-{index}");
                    }
                }
                Console.WriteLine($"内容类 {module.Option.Id}: {module.Choices.Count} 个选项及全部条件分支检查通过。");
            }
            Console.WriteLine($"内容目录已准备 {validated.Count} 种不同的代表组合；各选项使用其余默认值，条件分支使用明确条件，不展开全部排列。");

            void validateRepresentative(Dictionary<string, string> overrides, string label)
            {
                bool applicable = false;
                foreach (var context in contexts)
                {
                    if (!overrides.Keys.All(id => optionsById[id].AppliesTo(context.Ruleset, context.Keymode)))
                        continue;
                    GameplaySkinOptionValues values = GameplaySkinOptionValues.Resolve(frozenManifest.Options, overrides, context.Ruleset, context.Keymode);
                    if (!overrides.All(pair => values.TryGetValue(pair.Key, out GameplaySkinOptionValue value) && value.CanonicalValue == pair.Value))
                        continue;
                    applicable = true;
                    validateSelection(values, label, context.Ruleset, context.Keymode);
                }
                if (!applicable)
                    throw new InvalidDataException($"{catalogPath}: {label}: 选项作用范围无法同时满足其选择条件。");
            }

            void validateSelection(GameplaySkinOptionValues values, string label, string ruleset, string keymode)
            {
                var contributionPaths = new List<string?>();
                foreach (GameplaySkinContentModule module in catalog.Modules)
                {
                    if (!values.TryGetValue(module.Option.Id, out GameplaySkinOptionValue value))
                        continue;
                    GameplaySkinContentChoice choice = module.Choices.First(item => item.Value == value.CanonicalValue);
                    GameplaySkinContentVariant? variant = choice.Variants.FirstOrDefault(item => item.When.All(condition =>
                        values.TryGetValue(condition.Key, out GameplaySkinOptionValue selector) && selector.CanonicalValue == condition.Value));
                    GameplaySkinContentChoice contribution = variant?.Contribution ?? choice;
                    contributionPaths.Add(contribution.ManifestFile);
                    contributionPaths.Add(contribution.SceneFile);
                    contributionPaths.Add(contribution.IniFile);
                }
                // A combination depends on retained contribution paths, not labels or raw option IDs. Identical
                // source contributions are checked once; this does not skip any independently-authored branch.
                if (!validated.Add(JsonSerializer.Serialize(contributionPaths)))
                    return;
                try
                {
                    GameplaySkinPreparedPackage selected = package.Select(values, CancellationToken.None);
                    validateBitmapGlyphs(selected);
                }
                catch (GameplaySkinScenePreparationException error)
                {
                    throw new InvalidDataException($"{catalogPath}: {label} ({ruleset}/{keymode}): {error.Message}", error);
                }
            }
        }

        private static bool safeResource(string path)
            => !Path.IsPathRooted(path) && !path.Contains(':') && path.Split('/', '\\').All(part => part.Length > 0 && part is not "." and not "..");

        private static async Task pack(string root, string output)
        {
            if (output.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("输出 .osk 必须放在作者目录外，避免把上次成品打进新包。");
            Dictionary<string, byte[]> files = check(root);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            string temporary = output + ".pending";
            if (File.Exists(temporary))
                throw new InvalidDataException("上次打包留下 .pending，请先保留并检查它，再换输出文件名。");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    foreach ((string path, byte[] bytes) in files.OrderBy(item => item.Key, StringComparer.Ordinal))
                    {
                        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                        entry.LastWriteTime = package_epoch;
                        using Stream destination = entry.Open();
                        destination.Write(bytes);
                    }
                }

                stream.Flush(true);
            }

            // The ordinary importer is the final package authority; successful ZIP creation alone is not an import promise.
            using (var reader = await SkinArchiveReader.OpenAsync(new ImportTask(temporary), CancellationToken.None).ConfigureAwait(false))
            {
                foreach (string filename in reader.Filenames)
                {
                    using Stream input = reader.GetStream(filename);
                    await input.CopyToAsync(Stream.Null).ConfigureAwait(false);
                }
            }

            if (File.Exists(output))
                File.Replace(temporary, output, output + ".previous", true);
            else
                File.Move(temporary, output);
            Console.WriteLine($"已打包：{output}");
            Console.WriteLine($"SHA256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))).ToLowerInvariant()}");
        }

        private static void create(string template, string destination, string name)
        {
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new InvalidDataException("新作品目录已存在；不会合并或覆盖已有作品。");
            Dictionary<string, byte[]> files = check(template);
            Directory.CreateDirectory(destination);
            foreach ((string path, byte[] bytes) in files)
            {
                string output = Path.Combine(destination, path);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllBytes(output, bytes);
            }

            string profilePath = Path.Combine(destination, "author.json");
            var profile = JsonSerializer.Deserialize<AuthorProfile>(File.ReadAllText(profilePath), json_options)!;
            profile.Name = name;
            File.WriteAllText(profilePath, JsonSerializer.Serialize(profile, json_options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n", utf8);
            SkinRecipe.Generate(destination, profile);
            check(destination);
            Console.WriteLine($"新作品已准备：{destination}");
        }
    }

    internal sealed class AuthorProfile
    {
        public string Name { get; set; } = "OMS Simple";
        public string Author { get; set; } = "OMS contributors";
        public bool Complex { get; set; }
        public bool CompactLayout { get; set; }
        public string BmsAccent { get; set; } = "58d3ea";
        public string ManiaAccent { get; set; } = "b7a3ff";
        public string Highlight { get; set; } = "f3ca78";
        public string Background { get; set; } = "0b1019";
    }
}
