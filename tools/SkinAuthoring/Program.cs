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
                                        or GameplaySkinScriptException or UnknownImageFormatException or InvalidImageContentException)
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

        private static string[] captureFiles(string root)
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
                    if (total > max_package_bytes || files.Count >= 4096)
                        throw new InvalidDataException("作者包超出工具上限（128 MiB / 4096 个文件）。");
                    files.Add(path);
                }
            }

            return files.OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal).ToArray();
        }

        private static Dictionary<string, byte[]> check(string root)
        {
            string[] files = captureFiles(root);
            var captured = files.ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
            if (captured.Values.Sum(bytes => (long)bytes.Length) > max_package_bytes)
                throw new InvalidDataException("读取期间文件增长，作者包超过 128 MiB。");
            if (!captured.TryGetValue("skin.ini", out byte[]? ini))
                throw new InvalidDataException("skin.ini: 皮肤根目录缺少设置文件。");
            var folderCandidate = SkinPackageRevisionCapsuleFactory.Create(captured
                .Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray());
            if (!folderCandidate.IsSuccess)
                throw new InvalidDataException($"作者目录不能由游戏读取：{folderCandidate.RejectionReason}。");
            using (var capsule = folderCandidate.Capsule!)
            {
                if (!SkinManagedFolderPackageMetadataReader.TryRead(capsule, out _))
                    throw new InvalidDataException("skin.ini: 目录皮肤无法读取名称或作者，请保持文件不超过 1 MiB、名称与作者各不超过 256 字符，且不含控制字符。");
            }
            var document = GameplaySkinDocumentCodec.Decode(ini, GameplaySkinDocumentIdentity.CreateUnboundPackageParse(Convert.ToHexString(SHA256.HashData(ini))));
            var errors = new List<string>();
            foreach (var diagnostic in document.Diagnostics)
                errors.Add($"skin.ini:{diagnostic.LineNumber}: {diagnostic.Id}");
            foreach (var line in document.LegacySections.Where(section => section.Name is "Bms" or "Mania").SelectMany(section => section.Lines))
            {
                if (line.Key == null || line.Value == null || !(line.Key.StartsWith("NoteImage", StringComparison.Ordinal)
                    || line.Key.StartsWith("KeyImage", StringComparison.Ordinal) || line.Key is "StageLeft" or "StageRight" or "StageBottom" or "StageHint"))
                    continue;
                string resource = line.Value.Trim();
                if (!safeResource(resource) || !captured.ContainsKey(resource + ".png") && !captured.ContainsKey(resource + "-0.png"))
                    errors.Add($"skin.ini:{line.LineNumber}: 找不到兼容素材 {resource}。");
            }
            foreach (var entry in document.Sections.SelectMany(section => section.Entries))
            {
                if (entry.Operation != GameplaySkinDocumentOperation.Provide || entry.Value == null)
                    continue;
                if (!safeResource(entry.Value))
                    errors.Add($"skin.ini:{entry.LineNumber}: 素材路径必须位于包内。");
                else if (!captured.ContainsKey(entry.Value + ".png") && !captured.ContainsKey(entry.Value + "-0.png"))
                    errors.Add($"skin.ini:{entry.LineNumber}: 找不到素材 {entry.Value}.png 或首帧 -0.png。");
            }

            long pixels = 0;
            foreach ((string path, byte[] bytes) in captured.Where(item => item.Key.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            {
                if (bytes.Length > GameplaySkinPreparedSceneBudgets.MAX_RESOURCE_BYTES)
                    errors.Add($"{path}: 单张图片超过 16 MiB。");
                try
                {
                    var info = Image.Identify(bytes);
                    long area = (long)info.Width * info.Height;
                    pixels += area;
                    if (area > GameplaySkinPreparedSceneBudgets.MAX_TEXTURE_PIXELS || pixels > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_TEXTURE_PIXELS)
                        errors.Add($"{path}: 解码后的图片尺寸超出预算。");
                    if (errors.Count == 0)
                    {
                        using var decoded = Image.Load<Rgba32>(bytes);
                    }
                }
                catch (Exception error) when (error is UnknownImageFormatException or InvalidImageContentException)
                {
                    errors.Add($"{path}: 图片无法解码，请从图像编辑器重新保存有效 PNG。");
                }
            }

            if (captured.TryGetValue(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, out byte[]? manifestBytes))
            {
                var manifest = GameplaySkinSceneCodec.DecodeManifest(manifestBytes);
                errors.AddRange(manifest.Diagnostics.Select(item => $"gameplay-skin.json: {item.Id}"));
                if (manifest.Value != null)
                {
                    if (!captured.TryGetValue(manifest.Value.SceneFile, out byte[]? sceneBytes))
                        errors.Add("gameplay-skin.scene.json: 缺少演出文件。");
                    else
                    {
                        var scene = GameplaySkinSceneCodec.DecodeScene(sceneBytes, manifest.Value);
                        errors.AddRange(scene.Diagnostics.Select(item => $"gameplay-skin.scene.json: {item.Id}"));
                        foreach (var resource in manifest.Value.Resources)
                            if (!captured.ContainsKey(resource.Path))
                                errors.Add($"gameplay-skin.json: {resource.Id}: 缺少 {resource.Path}。");
                        if (manifest.Value.ScriptFile != null)
                        {
                            if (!captured.TryGetValue(manifest.Value.ScriptFile, out byte[]? scriptBytes))
                                errors.Add($"{manifest.Value.ScriptFile}: 缺少组合演出文件。");
                            else
                            {
                                var program = manifest.Value.ScriptFile == GameplaySkinSceneContracts.SCRIPT_FILE_NAME
                                    ? GameplaySkinScriptCompiler.Compile(scriptBytes)
                                    : GameplaySkinScriptCompiler.DecodeBytecode(scriptBytes);
                                if (scene.Value != null)
                                {
                                    var nodes = allNodes(scene.Value.Root).Concat(scene.Value.Templates.SelectMany(item => allNodes(item.Root))).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                                    foreach (var target in program.Targets)
                                        if (!nodes.Contains(target.NodeId))
                                            errors.Add($"{manifest.Value.ScriptFile}: 找不到获准节点 {target.NodeId}。");
                                }
                            }
                        }
                    }
                }
            }

            if (errors.Count > 0)
                throw new InvalidDataException(string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal)));
            Console.WriteLine("文件、公开设置、图片与演出语法检查通过。导入后仍需核对实际游玩画面与不同键数。");
            return captured;
        }

        private static IEnumerable<GameplaySkinSceneNode> allNodes(GameplaySkinSceneNode root)
            => new[] { root }.Concat(root.Children.SelectMany(allNodes));

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
        public string BmsAccent { get; set; } = "58d3ea";
        public string ManiaAccent { get; set; } = "b7a3ff";
        public string Highlight { get; set; } = "f3ca78";
        public string Background { get; set; } = "0b1019";
    }
}
