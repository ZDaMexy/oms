<#
.SYNOPSIS
    OMS 一键 Release 打包脚本
.DESCRIPTION
    构建 Release 发行物并打包为便携 ZIP，输出到 release-repo/ 目录。
    命名格式：oms_YYYYMMDD.zip（同日多次构建自动追加序号 _2, _3 ...）
.EXAMPLE
    .\build-release.ps1
#>

param(
    [string]$Runtime = 'win-x64',
    [switch]$KeepPdb
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose(); $stream.Dispose() }
}
function Assert-OrdinaryDeliveryEntry([string]$Path) {
    $entry = Get-Item -LiteralPath $Path -Force
    $ancestorPath = $entry.FullName
    while ($ancestorPath) {
        if (((Get-Item -LiteralPath $ancestorPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Delivery source cannot use redirected ancestors.' }
        $ancestorPath = [IO.Path]::GetDirectoryName($ancestorPath)
    }
    $pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $pending.Push($entry)
    while ($pending.Count -gt 0) {
        $item = $pending.Pop()
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Delivery source contains a redirected entry.' }
        if ($item -is [IO.DirectoryInfo]) { foreach ($child in $item.GetFileSystemInfos()) { $pending.Push($child) } }
    }
}

$repoRoot   = $PSScriptRoot
$publishDir = Join-Path $repoRoot 'publish'
$releaseDir = Join-Path $repoRoot 'release-repo'
$desktopDir = Join-Path $repoRoot 'osu.Desktop'
$authoringDir = Join-Path $repoRoot 'skin-authoring'
dotnet publish (Join-Path $repoRoot 'tools/SkinAuthoring') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $authoringDir 'bin')
if ($LASTEXITCODE -ne 0) { throw 'The standalone authoring tool could not be published.' }
foreach ($required in @('Author.ps1', 'bin/SkinAuthoring.exe', 'dist/oms-simple.osk', 'dist/oms-complex.osk', 'dist/aurora-study.osk')) {
    if (-not (Test-Path -LiteralPath (Join-Path $authoringDir $required) -PathType Leaf)) {
        throw "Authoring Kit is incomplete: $required. Build the public authoring tool and both skins before release packaging."
    }
}

# Refuse redirected output roots before any recursive cleanup.
$resolvedRepo = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/')
$resolvedPublish = [IO.Path]::GetFullPath($publishDir).TrimEnd('\', '/')
if (-not [IO.Path]::GetDirectoryName($resolvedPublish).Equals($resolvedRepo, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($resolvedPublish) -ne 'publish') { throw 'Publish cleanup escaped the repository output directory.' }
$ancestor = $resolvedPublish
while ($ancestor) {
    if (Test-Path -LiteralPath $ancestor) {
        if (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Publish cleanup cannot use a redirected directory.' }
    }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}

# ── 1. 清理上次 publish 残留 ──────────────────────────────────
if (Test-Path -LiteralPath $resolvedPublish) {
    Assert-OrdinaryDeliveryEntry $resolvedPublish
    Write-Host '[1/6] Cleaning previous publish output...' -ForegroundColor Cyan
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}

# ── 2. dotnet publish（single-file 便携发行）──────────────────
Write-Host '[2/6] Building single-file Release...' -ForegroundColor Cyan

$publishArgs = @(
    "$repoRoot/osu.Desktop",
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained',
    '-o', $publishDir,
    '-p:OmsReleasePackaging=true',
    '-p:PublishSingleFile=true',
    '-p:IncludeAllContentForSelfExtract=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:GenerateDocumentationFile=false'
)

if (-not $KeepPdb) {
    $publishArgs += '-p:DebugSymbols=false'
    $publishArgs += '-p:DebugType=None'
}

dotnet publish @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error 'dotnet publish failed.'
    exit 1
}

# ── 3. 补齐 single-file 包需要保留在旁路的图标资源 ───────────
Write-Host '[3/6] Copying required release-side assets...' -ForegroundColor Cyan
Copy-Item (Join-Path $desktopDir 'lazer.ico') (Join-Path $publishDir 'lazer.ico') -Force
Copy-Item (Join-Path $desktopDir 'beatmap.ico') (Join-Path $publishDir 'beatmap.ico') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'skin-c7-acceptance/Update-Installation.ps1') -Destination (Join-Path $publishDir 'Update-OMS.ps1')
Assert-OrdinaryDeliveryEntry (Join-Path $repoRoot 'skin-c7-acceptance')
Copy-Item -LiteralPath (Join-Path $repoRoot 'skin-c7-acceptance') -Destination (Join-Path $publishDir 'skin-c7-acceptance') -Recurse
$publishedAuthoring = Join-Path $publishDir 'skin-authoring'
[IO.Directory]::CreateDirectory($publishedAuthoring) | Out-Null
foreach ($name in @('sources', 'docs', 'bin', 'Author.ps1', 'Build-Skins.ps1', 'Test-Authoring.ps1', 'README.md')) {
    $entry = Get-Item -LiteralPath (Join-Path $authoringDir $name) -Force
    Assert-OrdinaryDeliveryEntry $entry.FullName
    Copy-Item -LiteralPath $entry.FullName -Destination $publishedAuthoring -Recurse
}
$publishedDist = Join-Path $publishedAuthoring 'dist'
[IO.Directory]::CreateDirectory($publishedDist) | Out-Null
foreach ($name in @('oms-simple.osk', 'oms-complex.osk', 'aurora-study.osk', 'oms-simple.sha256', 'oms-complex.sha256', 'aurora-study.sha256', 'oms-simple-preview.png', 'oms-complex-preview.png')) {
    $entry = Get-Item -LiteralPath (Join-Path $authoringDir "dist/$name") -Force
    Assert-OrdinaryDeliveryEntry $entry.FullName
    if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $entry.PSIsContainer) { throw 'Finished author package must be an ordinary file.' }
    Copy-Item -LiteralPath $entry.FullName -Destination $publishedDist
}
$toolSource = Join-Path $publishedAuthoring 'tool-source'
[IO.Directory]::CreateDirectory($toolSource) | Out-Null
foreach ($name in @('SkinAuthoring.csproj', 'Program.cs', 'SkinRecipe.cs', 'SkinPreview.cs')) {
    $sourceFile = Join-Path $repoRoot "tools/SkinAuthoring/$name"
    Assert-OrdinaryDeliveryEntry $sourceFile
    Copy-Item -LiteralPath $sourceFile -Destination $toolSource
}
$legacyRoot = Join-Path $publishDir 'skin-c7-acceptance/legacy'
[IO.Directory]::CreateDirectory($legacyRoot) | Out-Null
foreach ($file in @('artifacts/skin-c6/oms-complex-c6.osk', 'artifacts/manual-gates/bms-note-animation/bms-note-animation-manual-gate.osk', 'artifacts/manual-gates/bms-note-animation/bms-note-animation-manual-gate-broken.osk', 'doc_md/other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md')) {
    Assert-OrdinaryDeliveryEntry (Join-Path $repoRoot $file)
    Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination $legacyRoot
}
$legacyChart = Join-Path $repoRoot 'artifacts/manual-gates/bms-note-animation/chartbms/bms-note-animation-manual-gate'
Assert-OrdinaryDeliveryEntry $legacyChart
Copy-Item -LiteralPath $legacyChart -Destination $legacyRoot -Recurse

# ── 4. 写入 portable.ini 标记 ─────────────────────────────────
Write-Host '[4/6] Writing portable release markers...' -ForegroundColor Cyan
New-Item -Path (Join-Path $publishDir 'portable.ini') -ItemType File -Force | Out-Null

$updateGuidePath = Join-Path $publishDir 'how to update.txt'
$updateGuideContent = @'
OMS manual update guide / OMS 手动更新说明

中文

更新步骤：
1. 完全退出 OMS。
2. 下载新的 oms_YYYYMMDD(.zip)。
3. 把新包解压到另一个普通本地目录，不直接覆盖旧安装。
4. 在新包目录运行：
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Update-OMS.ps1 -UpdateSourceDirectory . -TargetDirectory "原安装目录"
5. 工具完成后，从原安装目录启动 osu!.exe。

注意事项：
- 不要在 OMS 仍在运行时覆盖文件。
- 新包必须完整解压，不能只替换 osu!.exe；保底皮肤位于 Skins/Canonical/。
- 更新工具保留原运行模式：便携模式保留 portable.ini 和 data/；非便携模式仍保持程序旁没有 portable.ini。
- 自定义保存位置由基础保存目录中的 storage.ini 决定：便携时在 data/storage.ini，非便携时在系统默认的游戏保存目录；它不在程序旁，也不随数据迁移到自定义目标目录。
- 不删除或替换原 storage.ini、自定义保存目录、谱面或皮肤。普通手动全包覆盖会带入 portable.ini，使原非便携安装改变保存位置，请使用随包工具。
- 更新前的程序文件与中断修复说明保留在原安装的 .oms-update-backup-*；不要猜测清理。
- 两款可导入皮肤、完整源文件和作者工具见 skin-authoring/；集中体验说明见 skin-c7-acceptance/。
- 正确的手动覆盖更新不会要求重新导入本地谱面或重建现有用户数据。

English

Steps:
1. Exit OMS completely.
2. Download the new oms_YYYYMMDD(.zip).
3. Extract the complete new package into a separate ordinary local directory.
4. Run in that new directory:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Update-OMS.ps1 -UpdateSourceDirectory . -TargetDirectory "your existing install directory"
5. After the tool finishes, launch osu!.exe from your existing directory.

Notes:
- Do not overwrite files while OMS is still running.
- Keep the complete package, including Skins/Canonical/; do not replace only osu!.exe.
- The updater preserves the target's mode: portable.ini and data/ stay in portable installs; non-portable installs continue without a marker next to the executable.
- Preserve storage.ini in the bootstrap storage root: data/storage.ini in portable mode, or the host's default game storage in non-portable mode. It is neither beside the executable nor moved into the custom data root.
- User data, custom storage, charts and skins are not copied over. Plain extraction over a non-portable install would add portable.ini and switch its bootstrap storage; use the supplied updater.
- Original program files and interruption recovery instructions remain in .oms-update-backup-* in the target installation. Do not guess which old data to remove.
- Both importable skins, their complete sources and author tools are in skin-authoring/; focused experience instructions are in skin-c7-acceptance/.
- A correct manual overwrite update does not require re-importing local beatmaps or rebuilding existing user data.
'@

[IO.File]::WriteAllText($updateGuidePath, $updateGuideContent, [Text.UTF8Encoding]::new($true))

# ── 5. 清理非运行时杂项 ──────────────────────────────────────
if (-not $KeepPdb) {
    Write-Host '[5/6] Removing publish leftovers...' -ForegroundColor Cyan
    Get-ChildItem -Path $publishDir -Filter '*.pdb' -Recurse | Where-Object { -not $_.FullName.StartsWith($publishedAuthoring + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) } | Remove-Item -Force
} else {
    Write-Host '[5/6] Removing non-runtime leftovers and keeping PDB files (diagnostic mode).' -ForegroundColor Cyan
}

Get-ChildItem -Path $publishDir -Filter '*.lib' -Recurse | Where-Object { -not $_.FullName.StartsWith($publishedAuthoring + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) } | Remove-Item -Force
Get-ChildItem -Path $publishDir -Filter '*.xml' -Recurse | Where-Object { -not $_.FullName.StartsWith($publishedAuthoring + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) } | Remove-Item -Force

$releaseFiles = @(Get-ChildItem -LiteralPath $publishDir -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        Path = $_.FullName.Substring($resolvedPublish.Length + 1).Replace('\', '/')
        Length = $_.Length
        Sha256 = (Get-FileSha256 $_.FullName)
    }
})
foreach ($name in @('oms-simple.osk', 'oms-complex.osk')) {
    $canonicalPath = Join-Path $publishDir "Skins/Canonical/$name"
    [IO.File]::SetAttributes($canonicalPath, ([IO.File]::GetAttributes($canonicalPath) -bor [IO.FileAttributes]::ReadOnly))
}
$releaseManifest = [ordered]@{
    Format = 'oms-offline-release-files.v1'
    Build = [ordered]@{ CreatedUtc = [DateTime]::UtcNow.ToString('O'); RepositoryHead = (& git -C $repoRoot rev-parse HEAD); RepositoryDirty = [bool](& git -C $repoRoot status --porcelain) }
    Files = $releaseFiles
}
[IO.File]::WriteAllText((Join-Path $publishDir 'release-files.json'), ($releaseManifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($true))

# ── 6. 打包 ZIP ──────────────────────────────────────────────
Write-Host '[6/6] Creating ZIP archive...' -ForegroundColor Cyan

if (-not (Test-Path $releaseDir)) {
    New-Item -Path $releaseDir -ItemType Directory -Force | Out-Null
}

$datestamp = Get-Date -Format 'yyyyMMdd'
$baseName  = "oms_$datestamp"
$zipName   = "$baseName.zip"
$zipPath   = Join-Path $releaseDir $zipName

# 同日多次构建追加序号
if (Test-Path $zipPath) {
    $seq = 2
    while (Test-Path (Join-Path $releaseDir "${baseName}_${seq}.zip")) {
        $seq++
    }
    $zipName = "${baseName}_${seq}.zip"
    $zipPath = Join-Path $releaseDir $zipName
}

Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

# ── 完成 ──────────────────────────────────────────────────────
$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ''
Write-Host "Done! $zipName ($sizeMB MB)" -ForegroundColor Green
Write-Host "  -> $zipPath"
