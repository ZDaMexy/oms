param(
    [Parameter(Mandatory = $true)][string]$UpdateSourceDirectory,
    [Parameter(Mandatory = $true)][string]$TargetDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose(); $stream.Dispose() }
}
$sourceRoot = [IO.Path]::GetFullPath($UpdateSourceDirectory).TrimEnd('\', '/')
$targetRoot = [IO.Path]::GetFullPath($TargetDirectory).TrimEnd('\', '/')
$utf8 = [Text.UTF8Encoding]::new($true)

function Assert-RegularPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $entry = Get-Item -LiteralPath $current -Force
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '路径包含链接或重定向目录；请把安装包放到普通本地目录后重试。' }
        }
        $parent = [IO.Path]::GetDirectoryName($current)
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

Assert-RegularPath $sourceRoot
Assert-RegularPath $targetRoot
if ($sourceRoot.Equals($targetRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $sourceRoot.StartsWith($targetRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $targetRoot.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '新包和旧安装必须是互不包含的两个独立目录。' }
foreach ($root in @($sourceRoot, $targetRoot)) {
    if (-not (Test-Path -LiteralPath (Join-Path $root 'osu!.exe') -PathType Leaf)) { throw '所选目录缺少 osu!.exe。' }
}
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.Equals((Join-Path $targetRoot 'osu!.exe'), [StringComparison]::OrdinalIgnoreCase) })
if ($running.Count -ne 0) { throw '请先完全退出目标安装中的 OMS，再运行更新。' }

$manifestPath = Join-Path $sourceRoot 'release-files.json'
Assert-RegularPath $manifestPath
Assert-RegularPath (Join-Path $targetRoot 'release-files.json')
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw '新包缺少完整性清单；请使用完整的 OMS 离线发行包。' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.Format -ne 'oms-offline-release-files.v1') { throw '无法识别新包清单。' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$files = [Collections.Generic.List[object]]::new()
foreach ($file in $manifest.Files) {
    $relative = [string]$file.Path
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('\') -or $relative -match '(^/|:|(^|/)\.{1,2}(/|$)|[<>"|?*])' -or
        @($relative.Split('/') | Where-Object { $_ -eq '' -or $_ -match '[. ]$' }).Count -gt 0 -or -not $seen.Add($relative)) { throw '新包清单含无效或重复文件名。' }
    if ($relative -match '(^|/)(data|storage\.ini|portable\.ini)(/|$)') {
        if ($relative -eq 'portable.ini') { continue }
        throw '新包清单包含用户保存位置或用户数据，已停止。'
    }
    $source = Join-Path $sourceRoot $relative
    $target = Join-Path $targetRoot $relative
    Assert-RegularPath $source
    Assert-RegularPath $target
    if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or
        (Get-Item -LiteralPath $source).Length -ne [long]$file.Length -or
        (Get-FileSha256 $source) -ne $file.Sha256) { throw "新包文件缺失或损坏：$relative。请重新解压完整安装包。" }
    if (Test-Path -LiteralPath $target -PathType Container) { throw "安装中存在同名目录，原内容已保留：$relative" }
    $files.Add([pscustomobject]@{ Relative = $relative; Source = $source; Target = $target; Sha256 = $file.Sha256 })
}
if (-not $seen.Contains('osu!.exe') -or -not $seen.Contains('Skins/Canonical/oms-simple.osk')) { throw '新包缺少程序或正式保底外观。' }
# The source manifest is copied too; portable.ini is intentionally governed by the target's existing mode.
$files.Add([pscustomobject]@{ Relative = 'release-files.json'; Source = $manifestPath; Target = (Join-Path $targetRoot 'release-files.json'); Sha256 = (Get-FileSha256 $manifestPath) })
$wasPortable = Test-Path -LiteralPath (Join-Path $targetRoot 'portable.ini') -PathType Leaf
$backupRoot = Join-Path $targetRoot ('.oms-update-backup-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
$receipt = [pscustomobject]@{ Format = 'oms-offline-update-receipt.v1'; State = 'Preparing'; Portable = $wasPortable; Files = @($files | ForEach-Object { [pscustomobject]@{ Path = $_.Relative; Existed = [IO.File]::Exists($_.Target); Sha256 = $_.Sha256 } }) }
$receiptPath = Join-Path $backupRoot 'update-receipt.json'
function Save-Receipt { [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 5), $utf8) }
Save-Receipt
[IO.File]::WriteAllText((Join-Path $backupRoot '修复说明.txt'), "本目录保留更新前原文件（old/）和本次待安装文件（new/）。用户 data/、portable.ini、基础保存目录中的 storage.ini 与自定义保存目录未被操作。`r`n正式皮肤原件更新时，旧件先原样移入 old/，再放入本次新件；两步之间中断可使安装原件暂时缺失，但旧件、来源和作者文件不改。若状态不是 Completed，请退出 OMS，用同一完整新包再次运行更新工具完成覆盖；保留本目录，不要猜测删除旧文件。若需恢复旧版本，保留此目录及 update-receipt.json，按列明文件逐项恢复 old/；原先不存在的文件不据名称自动清理。`r`n", $utf8)
try {
    foreach ($file in $files) {
        $staged = Join-Path $backupRoot ('new/' + $file.Relative)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($staged)) | Out-Null
        [IO.File]::Copy($file.Source, $staged, $false)
        if ((Get-FileSha256 $staged) -ne $file.Sha256) { throw '准备期间新包发生变化，已保留更新现场。' }
    }
    $receipt.State = 'Applying'
    Save-Receipt
    foreach ($file in $files) {
        Assert-RegularPath $file.Target
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file.Target)) | Out-Null
        $staged = Join-Path $backupRoot ('new/' + $file.Relative)
        $isCanonical = $file.Relative -in @('Skins/Canonical/oms-simple.osk', 'Skins/Canonical/oms-complex.osk')
        if ($isCanonical) {
            # ReadOnly belongs to the file, including any external hard links. Never change the old file.
            # Prepare only this invocation's private copy, then preserve the old name before installing it.
            [IO.File]::SetAttributes($staged, ([IO.File]::GetAttributes($staged) -bor [IO.FileAttributes]::ReadOnly))
            if ([IO.File]::Exists($file.Target)) {
                $old = Join-Path $backupRoot ('old/' + $file.Relative)
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($old)) | Out-Null
                [IO.File]::Move($file.Target, $old)
            }
            [IO.File]::Move($staged, $file.Target)
        } elseif ([IO.File]::Exists($file.Target)) {
            $old = Join-Path $backupRoot ('old/' + $file.Relative)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($old)) | Out-Null
            [IO.File]::Replace($staged, $file.Target, $old)
        } else { [IO.File]::Move($staged, $file.Target) }
    }
    if ((Test-Path -LiteralPath (Join-Path $targetRoot 'portable.ini') -PathType Leaf) -ne $wasPortable) { throw '更新期间便携标记发生外部变化；请保留现场并核对原运行模式。' }
    $receipt.State = 'Completed'
    Save-Receipt
    Write-Host '更新完成。原来的运行模式、保存位置与用户数据保持不变。'
    Write-Host "原程序文件备份：$backupRoot"
} catch {
    Write-Host "更新尚未完成；备份与修复说明已保留：$backupRoot"
    throw
}
