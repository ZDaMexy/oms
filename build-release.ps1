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
Write-Host '[4/6] Writing portable.ini marker...' -ForegroundColor Cyan
New-Item -Path (Join-Path $publishDir 'portable.ini') -ItemType File -Force | Out-Null

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
