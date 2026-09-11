param(
    [Parameter(Mandatory = $true)][string]$ReleaseDirectory,
    [string]$AuthoringKitDirectory,
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose(); $stream.Dispose() }
}
if (-not $AuthoringKitDirectory) { $AuthoringKitDirectory = Join-Path $ReleaseDirectory 'skin-authoring' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\', '/'))) ('oms-skin-c7-acceptance-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$releaseRoot = [IO.Path]::GetFullPath($ReleaseDirectory)
$kitRoot = [IO.Path]::GetFullPath($AuthoringKitDirectory)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw '验收目录已存在；请使用新目录保留既有记录。' }
$outputAncestor = [IO.Path]::GetDirectoryName($outputRoot)
while ($outputAncestor) {
    if ((Test-Path -LiteralPath $outputAncestor) -and (((Get-Item -LiteralPath $outputAncestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw '验收输出不能经过链接目录。' }
    $outputAncestor = [IO.Path]::GetDirectoryName($outputAncestor)
}
foreach ($sourceRoot in @($releaseRoot, $kitRoot)) {
    if ($outputRoot.Equals($sourceRoot, [StringComparison]::OrdinalIgnoreCase) -or $outputRoot.StartsWith($sourceRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $sourceRoot.StartsWith($outputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '验收输出必须与发行物和作者套件分开，不能互相嵌套。' }
}
foreach ($file in @('osu!.exe', 'osu!.runtimeconfig.json', 'osu.Game.dll', 'osu.Game.Rulesets.Bms.dll', 'osu.Game.Rulesets.Mania.dll', 'Skins/Canonical/oms-simple.osk', 'Skins/Canonical/oms-complex.osk', 'release-files.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot $file) -PathType Leaf)) { throw "发行物不完整：$file" }
}
if (Test-Path -LiteralPath (Join-Path $releaseRoot 'data')) { throw '请使用刚解压的发行物，不能把已有用户保存目录打入验收包。' }
if (-not (Test-Path -LiteralPath (Join-Path $kitRoot 'Author.ps1') -PathType Leaf)) { throw '缺少完整作者套件。' }
foreach ($root in @($releaseRoot, $kitRoot, $PSScriptRoot)) {
    $ancestorPath = $root
    while ($ancestorPath) {
        if (((Get-Item -LiteralPath $ancestorPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '来源不能经过链接目录。' }
        $ancestorPath = [IO.Path]::GetDirectoryName($ancestorPath)
    }
    $pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $pending.Push((Get-Item -LiteralPath $root -Force))
    while ($pending.Count -gt 0) {
        $item = $pending.Pop()
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw '来源中存在链接目录，已停止打包。' }
        if ($item -is [IO.DirectoryInfo]) { foreach ($child in $item.GetFileSystemInfos()) { $pending.Push($child) } }
    }
}
$legacyRoot = Join-Path $PSScriptRoot 'legacy'
$legacyHashes = @{}
foreach ($line in [IO.File]::ReadAllLines((Join-Path $legacyRoot 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$' -or $legacyHashes.ContainsKey($Matches[2])) { throw '原验收输入摘要清单无效。' }
    $legacyHashes.Add($Matches[2], $Matches[1])
}
foreach ($name in @('oms-complex-c6.osk', 'bms-note-animation-manual-gate.osk', 'bms-note-animation-manual-gate-broken.osk', 'bms-note-animation-manual-gate/bms-note-animation-manual-gate.bme', 'original-v001-SHA256SUMS.txt', 'SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md.snapshot')) {
    $file = Join-Path $legacyRoot $name
    if (-not $legacyHashes.ContainsKey($name) -or -not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileSha256 $file) -ne $legacyHashes[$name]) { throw "原 V-001 或 V-005 输入缺失或改变，请修复完整验收工具包：$name" }
}
foreach ($name in @('sources', 'docs', 'bin/SkinAuthoring.exe', 'tool-source/SkinRecipe.cs', 'Author.ps1', 'Build-Skins.ps1', 'Test-Authoring.ps1', 'README.md', 'dist/oms-simple.osk', 'dist/oms-complex.osk', 'dist/aurora-study.osk', 'dist/oms-simple.sha256', 'dist/oms-complex.sha256', 'dist/aurora-study.sha256', 'dist/oms-simple-preview.png', 'dist/oms-complex-preview.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $kitRoot $name))) { throw "作者套件缺件，请使用完整发行物：$name" }
}
foreach ($name in @('oms-simple.osk', 'oms-complex.osk')) {
    if ((Get-FileSha256 (Join-Path $releaseRoot "Skins/Canonical/$name")) -ne (Get-FileSha256 (Join-Path $kitRoot "dist/$name"))) { throw "安装原件与作者成品不一致：$name" }
    if (([IO.File]::GetAttributes((Join-Path $releaseRoot "Skins/Canonical/$name")) -band [IO.FileAttributes]::ReadOnly) -eq 0) { throw '安装原件未保留发行 ZIP 的只读属性；请使用 Windows 资源管理器重新解压。组装不会补属性掩盖解包差异。' }
}
$releaseManifest = Get-Content -LiteralPath (Join-Path $releaseRoot 'release-files.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($releaseManifest.Format -ne 'oms-offline-release-files.v1') { throw '发行物清单无效。' }
foreach ($file in $releaseManifest.Files) {
    $relative = [string]$file.Path
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or @($relative.Replace('\', '/').Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) { throw '发行物清单包含无效相对路径。' }
    $sourceFile = [IO.Path]::GetFullPath((Join-Path $releaseRoot $relative))
    if (-not $sourceFile.StartsWith($releaseRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '发行物清单不能越过来源目录。' }
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf) -or (Get-FileSha256 $sourceFile) -ne $file.Sha256) { throw "发行物文件缺失或损坏：$($file.Path)" }
}
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$appRoot = Join-Path $outputRoot 'app'
Copy-Item -LiteralPath $releaseRoot -Destination $appRoot -Recurse
if (-not (Test-Path -LiteralPath (Join-Path $appRoot 'portable.ini'))) { [IO.File]::WriteAllText((Join-Path $appRoot 'portable.ini'), '') }
$deliveredKit = Join-Path $outputRoot 'skin-authoring'
[IO.Directory]::CreateDirectory($deliveredKit) | Out-Null
foreach ($name in @('sources', 'docs', 'bin', 'tool-source', 'Author.ps1', 'Build-Skins.ps1', 'Test-Authoring.ps1', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $kitRoot $name) -Destination $deliveredKit -Recurse
}
[IO.Directory]::CreateDirectory((Join-Path $deliveredKit 'dist')) | Out-Null
foreach ($name in @('oms-simple.osk', 'oms-complex.osk', 'aurora-study.osk', 'oms-simple.sha256', 'oms-complex.sha256', 'aurora-study.sha256', 'oms-simple-preview.png', 'oms-complex-preview.png')) {
    Copy-Item -LiteralPath (Join-Path $kitRoot "dist/$name") -Destination (Join-Path $deliveredKit 'dist')
}
& (Join-Path $PSScriptRoot 'Generate-Inputs.ps1') -OutputDirectory (Join-Path $outputRoot 'inputs')
if (-not $?) { throw '观察输入生成失败。' }
[IO.Directory]::CreateDirectory((Join-Path $appRoot 'data')) | Out-Null
foreach ($folder in @('chartbms', 'chartmania')) { Copy-Item -LiteralPath (Join-Path $outputRoot "inputs/$folder") -Destination (Join-Path $appRoot 'data') -Recurse }
[IO.Directory]::CreateDirectory((Join-Path $outputRoot 'packages')) | Out-Null
foreach ($package in @('oms-simple.osk', 'oms-complex.osk')) { Copy-Item -LiteralPath (Join-Path $releaseRoot "Skins/Canonical/$package") -Destination (Join-Path $outputRoot 'packages') }
Copy-Item -LiteralPath (Join-Path $kitRoot 'dist/aurora-study.osk') -Destination (Join-Path $outputRoot 'packages')
Get-ChildItem -LiteralPath (Join-Path $outputRoot 'inputs/packages') -File | Copy-Item -Destination (Join-Path $outputRoot 'packages')
foreach ($legacy in @('oms-complex-c6.osk', 'bms-note-animation-manual-gate.osk', 'bms-note-animation-manual-gate-broken.osk')) {
    Copy-Item -LiteralPath (Join-Path $legacyRoot $legacy) -Destination (Join-Path $outputRoot 'packages')
}
$legacyChart = Join-Path $legacyRoot 'bms-note-animation-manual-gate'
Copy-Item -LiteralPath $legacyChart -Destination (Join-Path $appRoot 'data/chartbms') -Recurse
foreach ($file in @('README.md', 'CHECKLIST.csv', 'STARTUP-CHECK.md', 'Test-ReleaseStartup.ps1', 'Start-Acceptance.ps1', 'Reset-ImportCopies.ps1', 'Update-Installation.ps1', 'Create-CustomRootCopy.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $outputRoot $file)
}
$legacyChecklist = Join-Path $legacyRoot 'SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md.snapshot'
Copy-Item -LiteralPath $legacyChecklist -Destination (Join-Path $outputRoot 'V001-V005-原验收清单.md')
& (Join-Path $outputRoot 'Reset-ImportCopies.ps1')
$facts = [ordered]@{ CreatedUtc = [DateTime]::UtcNow.ToString('O'); VisualAcceptance = 'V001-V004 0/4; V005 unsigned; C7 visual unsigned'; ReleaseExecutableSha256 = (Get-FileSha256 (Join-Path $appRoot 'osu!.exe')); ReleaseManifestSha256 = (Get-FileSha256 (Join-Path $releaseRoot 'release-files.json')) }
if ($releaseManifest.PSObject.Properties.Name -contains 'Build') { $facts.ReleaseBuild = $releaseManifest.Build }
[IO.File]::WriteAllText((Join-Path $outputRoot 'build-evidence.json'), ($facts | ConvertTo-Json), [Text.UTF8Encoding]::new($true))
Write-Host "人工验收包已生成：$outputRoot"
Write-Host '运行 Start-Acceptance.ps1 打开隔离便携副本；从 import-copies 拖入两款皮肤。'
