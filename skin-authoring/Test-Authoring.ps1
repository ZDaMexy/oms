param([string]$Tool)
$ErrorActionPreference = 'Stop'
if (-not $Tool) {
    $Tool = Join-Path $PSScriptRoot 'bin/SkinAuthoring.exe'
    if (-not (Test-Path -LiteralPath $Tool)) { $Tool = Join-Path $PSScriptRoot '../tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe' }
}
$toolPath = [IO.Path]::GetFullPath($Tool)
if (-not (Test-Path -LiteralPath $toolPath)) { throw '找不到已编译的作者工具。' }
$verificationRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('work/verification-' + [Guid]::NewGuid().ToString('N'))))
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'work')) + [IO.Path]::DirectorySeparatorChar
if (-not $verificationRoot.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw '验证目录不在制作套件内。' }
New-Item -ItemType Directory -Path $verificationRoot | Out-Null
$results = [Collections.Generic.List[object]]::new()
function Get-ContentHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
function Invoke-Expected([string]$Case, [int]$ExitCode, [string[]]$ToolArguments) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = (& $toolPath @ToolArguments 2>&1 | Out-String); $actualExit = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previousPreference }
    if ($actualExit -ne $ExitCode) { throw "$Case 返回 $actualExit，预期 $ExitCode。$output" }
    $results.Add([pscustomobject]@{ Case = $Case; ExitCode = $actualExit; Output = $output.Trim() })
}
foreach ($name in @('oms-simple', 'oms-complex', 'aurora-study')) {
    $source = Join-Path $PSScriptRoot ('sources/' + $name)
    Invoke-Expected "$name valid" 0 @('check', $source)
    $first = Join-Path $verificationRoot ($name + '-a.osk')
    $second = Join-Path $verificationRoot ($name + '-b.osk')
    Invoke-Expected "$name pack first" 0 @('pack', $source, $first)
    Invoke-Expected "$name pack repeated" 0 @('pack', $source, $second)
    if ((Get-ContentHash $first) -ne (Get-ContentHash $second)) { throw "$name 重复打包的字节不一致。" }
    if ((Get-ContentHash $first) -ne (Get-ContentHash (Join-Path $PSScriptRoot ('dist/' + $name + '.osk')))) { throw "$name 成品与当前源文件不一致。" }
    $regenerated = Join-Path $verificationRoot ($name + '-regenerated')
    Copy-Item -LiteralPath $source -Destination $regenerated -Recurse
    Invoke-Expected "$name regenerate artwork from author profile" 0 @('generate', $regenerated)
    $regeneratedPackage = Join-Path $verificationRoot ($name + '-regenerated.osk')
    Invoke-Expected "$name pack regenerated artwork" 0 @('pack', $regenerated, $regeneratedPackage)
    if ((Get-ContentHash $first) -ne (Get-ContentHash $regeneratedPackage)) { throw "$name 重新绘制和打包后与保留成品不同。" }
}
$copy = Join-Path $verificationRoot 'damaged-author'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'sources/aurora-study') -Destination $copy -Recurse
$asset = Join-Path $copy 'bms/note-white.png'
$original = [IO.File]::ReadAllBytes($asset)
[IO.File]::WriteAllBytes($asset, [byte[]]@(0, 1, 2))
Invoke-Expected 'invalid PNG rejected' 1 @('check', $copy)
[IO.File]::WriteAllBytes($asset, $original)
$ini = Join-Path $copy 'skin.ini'
$iniOriginal = [IO.File]::ReadAllBytes($ini)
[IO.File]::AppendAllText($ini, "`n[GameplaySkin.Common:1]`nTarget: Global ruleset=any keymode=any stage-mode=any`nunknown.part: resource Provide `"missing`"`n", [Text.UTF8Encoding]::new($false))
Invoke-Expected 'unknown public field rejected with source line' 1 @('check', $copy)
[IO.File]::WriteAllBytes($ini, $iniOriginal)
[IO.File]::AppendAllText($ini, "`n//" + ('x' * 1048576), [Text.UTF8Encoding]::new($false))
Invoke-Expected 'oversized folder metadata rejected before distribution' 1 @('check', $copy)
[IO.File]::WriteAllBytes($ini, $iniOriginal)
$preserved = Join-Path $verificationRoot 'preserved.osk'
Invoke-Expected 'prepare previous package' 0 @('pack', $copy, $preserved)
$before = Get-ContentHash $preserved
[IO.File]::WriteAllBytes($preserved + '.pending', [byte[]]@(5, 8, 13))
Invoke-Expected 'unfinished output preserved' 1 @('pack', $copy, $preserved)
if ((Get-ContentHash $preserved) -ne $before) { throw '已有成品被中断操作覆盖。' }
if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($preserved + '.pending')) -ne 'BQgN') { throw '中断操作证据被改写。' }
Invoke-Expected 'source-contained package rejected' 1 @('pack', $copy, (Join-Path $copy 'recursive.osk'))
Invoke-Expected 'existing author destination preserved' 1 @('new', $copy, $copy, 'Do not overwrite')
Invoke-Expected 'repaired source passes' 0 @('check', $copy)
$toolAssembly = Join-Path ([IO.Path]::GetDirectoryName($toolPath)) 'SkinAuthoring.dll'
$engineAssembly = Join-Path ([IO.Path]::GetDirectoryName($toolPath)) 'osu.Game.dll'
$record = [pscustomobject]@{
    ExecutedUtc = [DateTime]::UtcNow.ToString('o')
    ToolSha256 = (Get-ContentHash $toolPath)
    ToolAssemblySha256 = $(if (Test-Path -LiteralPath $toolAssembly) { Get-ContentHash $toolAssembly } else { $null })
    EngineAssemblySha256 = $(if (Test-Path -LiteralPath $engineAssembly) { Get-ContentHash $engineAssembly } else { $null })
    EvidenceDirectory = $verificationRoot
    Results = $results
}
$evidence = Join-Path $PSScriptRoot 'docs/authoring-tool-verification.json'
[IO.File]::WriteAllText($evidence, ($record | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
Write-Output '作者正常制作、重复打包、错误拒绝与中断成品保护检查通过。'
Write-Output $evidence
