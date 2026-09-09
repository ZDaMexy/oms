param([string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('oms-c7-update-proof-' + [Guid]::NewGuid().ToString('N'))))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose(); $stream.Dispose() }
}
$proofRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $proofRoot) { throw '验证输出目录必须尚不存在。' }
$source = Join-Path $proofRoot 'new-release'
[IO.Directory]::CreateDirectory((Join-Path $source 'Skins/Canonical')) | Out-Null
$encoding = [Text.UTF8Encoding]::new($false)
foreach ($pair in @(@('osu!.exe', 'new program fixture'), @('portable.ini', ''), @('Skins/Canonical/oms-simple.osk', 'new canonical fixture'))) {
    [IO.File]::WriteAllText((Join-Path $source $pair[0]), $pair[1], $encoding)
}
$files = @(Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
    [ordered]@{ Path = $_.FullName.Substring($source.Length + 1).Replace('\', '/'); Length = $_.Length; Sha256 = (Get-FileSha256 $_.FullName) }
})
[IO.File]::WriteAllText((Join-Path $source 'release-files.json'), (@{ Format = 'oms-offline-release-files.v1'; Files = $files } | ConvertTo-Json -Depth 5), $encoding)
$results = [Collections.Generic.List[object]]::new()
foreach ($mode in @('portable', 'nonportable', 'custom')) {
    $target = Join-Path $proofRoot $mode
    [IO.Directory]::CreateDirectory((Join-Path $target 'data')) | Out-Null
    [IO.File]::WriteAllText((Join-Path $target 'osu!.exe'), 'old program fixture', $encoding)
    [IO.Directory]::CreateDirectory((Join-Path $target 'Skins/Canonical')) | Out-Null
    $oldCanonical = Join-Path $target 'Skins/Canonical/oms-simple.osk'
    [IO.File]::WriteAllText($oldCanonical, 'old canonical fixture', $encoding)
    [IO.File]::SetAttributes($oldCanonical, [IO.FileAttributes]::ReadOnly)
    [IO.File]::WriteAllText((Join-Path $target 'data/user-skin.bin'), 'user skin unchanged', $encoding)
    if ($mode -ne 'nonportable') { [IO.File]::WriteAllText((Join-Path $target 'portable.ini'), 'original marker bytes', $encoding) }
    if ($mode -eq 'custom') { [IO.File]::WriteAllText((Join-Path $target 'data/storage.ini'), 'FullPath = private custom fixture location', $encoding) }
    $protectedFiles = @(Get-ChildItem -LiteralPath $target -Recurse -File | Where-Object { $_.Name -notin @('osu!.exe', 'oms-simple.osk') } | ForEach-Object { @{ Path = $_.FullName; Hash = (Get-FileSha256 $_.FullName) } })
    & (Join-Path $PSScriptRoot 'Update-Installation.ps1') -UpdateSourceDirectory $source -TargetDirectory $target
    if ([IO.File]::ReadAllText((Join-Path $target 'osu!.exe')) -ne 'new program fixture') { throw '程序文件未更新。' }
    if ([IO.File]::ReadAllText($oldCanonical) -ne 'new canonical fixture' -or ([IO.File]::GetAttributes($oldCanonical) -band [IO.FileAttributes]::ReadOnly) -eq 0) { throw '只读安装原件未完成更新或失去只读保护。' }
    if ((Test-Path -LiteralPath (Join-Path $target 'portable.ini')) -ne ($mode -ne 'nonportable')) { throw '运行模式被改变。' }
    foreach ($file in $protectedFiles) { if ((Get-FileSha256 $file.Path) -ne $file.Hash) { throw '用户文件发生变化。' } }
    $backup = @(Get-ChildItem -LiteralPath $target -Directory -Filter '.oms-update-backup-*')
    if ($backup.Count -ne 1 -or [IO.File]::ReadAllText((Join-Path $backup[0].FullName 'old/osu!.exe')) -ne 'old program fixture') { throw '更新前程序未完整保留。' }
    $results.Add([pscustomobject]@{ Case = $mode; Result = 'passed'; Scope = 'file replacement, original mode, data/config preservation, old-file receipt' })
}
$rejectedTarget = Join-Path $proofRoot 'corrupt-rejection'
[IO.Directory]::CreateDirectory($rejectedTarget) | Out-Null
[IO.File]::WriteAllText((Join-Path $rejectedTarget 'osu!.exe'), 'old untouched', $encoding)
[IO.File]::AppendAllText((Join-Path $source 'Skins/Canonical/oms-simple.osk'), ' corruption')
$rejected = $false
try { & (Join-Path $PSScriptRoot 'Update-Installation.ps1') -UpdateSourceDirectory $source -TargetDirectory $rejectedTarget } catch { $rejected = $true }
if (-not $rejected -or [IO.File]::ReadAllText((Join-Path $rejectedTarget 'osu!.exe')) -ne 'old untouched' -or @(Get-ChildItem -LiteralPath $rejectedTarget).Count -ne 1) { throw '坏新包在拒绝前更改了目标。' }
$results.Add([pscustomobject]@{ Case = 'corrupt-package'; Result = 'passed'; Scope = 'reject before any target mutation' })
[IO.File]::WriteAllText((Join-Path $proofRoot 'results.json'), ($results | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($true))
Write-Host "文件保护验证完成；这不代替发行物启动或用户体验：$proofRoot"
