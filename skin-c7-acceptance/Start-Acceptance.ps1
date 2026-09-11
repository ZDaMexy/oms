Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$appRoot = Join-Path $PSScriptRoot 'app'
# A complete multi-file release is required; the retired self-extracting entry can bypass portable.ini.
foreach ($runtimeFile in @('osu!.exe', 'osu!.runtimeconfig.json', 'osu.Game.dll', 'osu.Game.Rulesets.Bms.dll', 'osu.Game.Rulesets.Mania.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $appRoot $runtimeFile) -PathType Leaf)) { throw "验收运行文件缺失，请重新解压完整发行包：$runtimeFile" }
}
foreach ($relative in @('osu!.exe', 'osu!.runtimeconfig.json', 'osu.Game.dll', 'osu.Game.Rulesets.Bms.dll', 'osu.Game.Rulesets.Mania.dll', 'portable.ini', 'data/storage.ini', 'Skins/Canonical/oms-simple.osk', 'Skins/Canonical/oms-complex.osk')) {
    $ancestor = Join-Path $appRoot $relative
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw '验收入口不能经过链接文件或目录；请使用新解压的普通本地副本。' }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $appRoot 'portable.ini') -PathType Leaf)) { throw '隔离便携标记缺失；请重新解压验收包。' }
$storageConfig = Join-Path $appRoot 'data/storage.ini'
if (Test-Path -LiteralPath $storageConfig) {
    $values = @([IO.File]::ReadAllLines($storageConfig) | ForEach-Object { if ($_ -match '^\s*FullPath\s*=\s*(.*)$') { $Matches[1].Trim() } })
    if ($values.Count -ne 1 -or $values[0].Length -gt 0) { throw '本副本已指定其他保存位置，或配置无法确认；请保留当前数据并使用自定义副本的入口，或另解压验收包。' }
}
if (@(Get-Process -Name 'osu!', 'osu', 'OMS' -ErrorAction SilentlyContinue).Count -gt 0) { throw '已有游戏正在运行；请保存并关闭后再启动独立验收副本。' }
Write-Host '这是隔离便携副本；不会打开现有 OMS 保存目录。关闭后在 CHECKLIST.csv 填写实际观察。'
# This launcher is explicitly run by the user to open the interactive game window.
Start-Process -FilePath (Join-Path $appRoot 'osu!.exe') -WorkingDirectory $appRoot
