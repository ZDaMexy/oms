Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'app'
$target = Join-Path $PSScriptRoot 'app-custom'
$customData = Join-Path $PSScriptRoot 'custom-data'
if ((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath $customData)) { throw '自定义位置副本已存在；保留记录后请另解压一份验收包。' }
if (-not (Test-Path -LiteralPath (Join-Path $source 'osu!.exe') -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $source 'portable.ini') -PathType Leaf)) { throw '请从完整的独立便携验收副本创建自定义位置。' }
$storageConfig = Join-Path $source 'data/storage.ini'
if (Test-Path -LiteralPath $storageConfig) {
    $values = @([IO.File]::ReadAllLines($storageConfig) | ForEach-Object { if ($_ -match '^\s*FullPath\s*=\s*(.*)$') { $Matches[1].Trim() } })
    if ($values.Count -ne 1 -or $values[0].Length -gt 0) { throw '此副本已经指定自定义保存位置，或配置无法确认；请保全当前数据，另解压一份验收包进行本项体验。' }
}
if (@(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.Equals((Join-Path $source 'osu!.exe'), [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0) { throw '请先关闭验收副本。' }
$ancestorPath = $source
while ($ancestorPath) {
    if (((Get-Item -LiteralPath $ancestorPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '验收副本不能经过链接目录。' }
    $ancestorPath = [IO.Path]::GetDirectoryName($ancestorPath)
}
$pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
$pending.Push((Get-Item -LiteralPath $source -Force))
while ($pending.Count -gt 0) {
    $item = $pending.Pop()
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '验收副本含链接，已停止复制。' }
    if ($item -is [IO.DirectoryInfo]) { foreach ($child in $item.GetFileSystemInfos()) { $pending.Push($child) } }
}
[IO.Directory]::CreateDirectory($target) | Out-Null
foreach ($entry in Get-ChildItem -LiteralPath $source -Force) {
    if ($entry.Name -ne 'data') { Copy-Item -LiteralPath $entry.FullName -Destination $target -Recurse }
}
Copy-Item -LiteralPath (Join-Path $source 'data') -Destination $customData -Recurse
[IO.Directory]::CreateDirectory((Join-Path $target 'data')) | Out-Null
[IO.File]::WriteAllText((Join-Path $target 'data/storage.ini'), "FullPath = $customData`r`n", [Text.UTF8Encoding]::new($false))
Write-Host '已创建 app-custom 与 custom-data。运行 app-custom/osu!.exe，核对原皮肤与谱面仍可使用。'
Write-Host '覆盖更新时使用 Update-Installation.ps1；不要移动或删除 app-custom/data/storage.ini。'
