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

# ── 1. 清理上次 publish 残留 ──────────────────────────────────
if (Test-Path $publishDir) {
    Write-Host '[1/5] Cleaning previous publish output...' -ForegroundColor Cyan
    Remove-Item $publishDir -Recurse -Force
}

# ── 2. dotnet publish ─────────────────────────────────────────
Write-Host '[2/5] Building Release...' -ForegroundColor Cyan
dotnet publish "$repoRoot/osu.Desktop" `
    -c Release `
    -r $Runtime `
    --self-contained `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error 'dotnet publish failed.'
    exit 1
}

# ── 3. 写入 portable.ini 标记 ─────────────────────────────────
Write-Host '[3/5] Writing portable.ini marker...' -ForegroundColor Cyan
New-Item -Path (Join-Path $publishDir 'portable.ini') -ItemType File -Force | Out-Null

# ── 4. 可选：移除 PDB ────────────────────────────────────────
if (-not $KeepPdb) {
    Write-Host '[4/5] Removing PDB files...' -ForegroundColor Cyan
    Get-ChildItem -Path $publishDir -Filter '*.pdb' -Recurse | Remove-Item -Force
} else {
    Write-Host '[4/5] Keeping PDB files (diagnostic mode).' -ForegroundColor Cyan
}

# ── 5. 打包 ZIP ──────────────────────────────────────────────
Write-Host '[5/5] Creating ZIP archive...' -ForegroundColor Cyan

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
