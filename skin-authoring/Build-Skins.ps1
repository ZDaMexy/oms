param([switch]$RebuildAssets)
$ErrorActionPreference = 'Stop'
foreach ($name in @('oms-simple', 'oms-complex')) {
    $source = Join-Path $PSScriptRoot ('sources/' + $name)
    if ($RebuildAssets) { & (Join-Path $PSScriptRoot 'Author.ps1') -Action generate -Source $source }
    & (Join-Path $PSScriptRoot 'Author.ps1') -Action pack -Source $source -Output (Join-Path $PSScriptRoot ('dist/' + $name + '.osk'))
    $stream = [IO.File]::OpenRead((Join-Path $PSScriptRoot ('dist/' + $name + '.osk')))
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $hash = ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot ('dist/' + $name + '.sha256')), $hash + "`n", [Text.UTF8Encoding]::new($false))
}
