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

$repoRoot   = $PSScriptRoot
$publishDir = Join-Path $repoRoot 'publish'
$releaseDir = Join-Path $repoRoot 'release-repo'
$desktopDir = Join-Path $repoRoot 'osu.Desktop'

# ── 1. 清理上次 publish 残留 ──────────────────────────────────
if (Test-Path $publishDir) {
    Write-Host '[1/6] Cleaning previous publish output...' -ForegroundColor Cyan
    Remove-Item $publishDir -Recurse -Force
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

# ── 4. 写入 portable.ini 标记 ─────────────────────────────────
Write-Host '[4/6] Writing portable release markers...' -ForegroundColor Cyan
New-Item -Path (Join-Path $publishDir 'portable.ini') -ItemType File -Force | Out-Null

$updateGuidePath = Join-Path $publishDir 'how to update.txt'
$updateGuideContent = @'
OMS manual update guide / OMS 手动更新说明

中文

正确更新步骤：
1. 完全退出 OMS。
2. 下载新的 oms_YYYYMMDD(.zip)。
3. 解压并覆盖到当前程序目录。
4. 保留 data/（便携模式）以及 storage.ini（如果你正在使用自定义数据根）。
5. 启动 osu!.exe。

注意事项：
- 不要在 OMS 仍在运行时覆盖文件，否则会遇到 Windows 文件锁。
- 当前发行物不是“只有一个 exe”的布局；请同时保留 portable.ini、lazer.ico、beatmap.ico 和本说明文件。
- 如果你在便携模式下误删 portable.ini，下次启动将不再继续使用同级 data/ 作为数据根。
- 如果你正在使用 storage.ini 指向自定义数据根，覆盖更新时不要删除该文件；否则下次启动会改变数据根位置。
- 正确的手动覆盖更新不会要求你重新导入本地谱面或重建现有用户数据。

English

Correct update steps:
1. Exit OMS completely.
2. Download the new oms_YYYYMMDD(.zip).
3. Extract it over your current install directory.
4. Keep data/ in portable mode, and keep storage.ini if you use a custom data root.
5. Launch osu!.exe.

Notes:
- Do not overwrite files while OMS is still running, or Windows file locks may block the update.
- The current release is not a "single exe only" layout; keep portable.ini, lazer.ico, beatmap.ico, and this guide file together.
- If you delete portable.ini in portable mode, the next launch will stop using the sibling data/ directory as the data root.
- If you use storage.ini to point to a custom data root, do not delete it during update, or the next launch will switch data roots.
- A correct manual overwrite update does not require re-importing your local beatmaps or rebuilding existing user data.
'@

Set-Content -Path $updateGuidePath -Value $updateGuideContent -Encoding utf8BOM

# ── 5. 清理非运行时杂项 ──────────────────────────────────────
if (-not $KeepPdb) {
    Write-Host '[5/6] Removing publish leftovers...' -ForegroundColor Cyan
    Get-ChildItem -Path $publishDir -Filter '*.pdb' -Recurse | Remove-Item -Force
} else {
    Write-Host '[5/6] Removing non-runtime leftovers and keeping PDB files (diagnostic mode).' -ForegroundColor Cyan
}

Get-ChildItem -Path $publishDir -Filter '*.lib' -Recurse | Remove-Item -Force
Get-ChildItem -Path $publishDir -Filter '*.xml' -Recurse | Remove-Item -Force

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
