Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot 'import-copies'
foreach ($path in @($destination, (Join-Path $PSScriptRoot 'packages'))) {
    $ancestor = $path
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw '导入副本和原件目录不能经过链接。' }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
}
if (Test-Path -LiteralPath $destination) {
    if (((Get-Item -LiteralPath $destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '导入副本目录不能是链接。' }
} else { [IO.Directory]::CreateDirectory($destination) | Out-Null }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'packages') -Filter '*.osk' -File) {
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '原件不能是链接文件。' }
    $target = Join-Path $destination $file.Name
    if (Test-Path -LiteralPath $target) {
        $entry = Get-Item -LiteralPath $target -Force
        if ($entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '导入副本目标与现有目录或链接冲突。' }
    }
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    [IO.File]::SetAttributes($target, ([IO.File]::GetAttributes($target) -band (-bnot [IO.FileAttributes]::ReadOnly)))
}
Write-Host '导入副本已补齐；packages 中的原件始终保留。'
