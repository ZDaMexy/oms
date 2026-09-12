$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('oms-builtin-build-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Build-BuiltInSkins.ps1') -Destination $fixture
$output = Join-Path $fixture 'output'
foreach ($name in @('oms-simple', 'oms-complex')) {
    $source = Join-Path $fixture "sources/$name"
    [IO.Directory]::CreateDirectory($source) | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'skin.ini'), "[General]`nName: $name`nAuthor: OMS`n")
    [IO.File]::WriteAllText((Join-Path $source 'material.txt'), 'first')
}
function Build-Fixture {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixture 'Build-BuiltInSkins.ps1') -OutputDirectory $output
    if ($LASTEXITCODE -ne 0) { throw 'Fixture build failed.' }
}
function Read-Material {
    $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $output 'oms-simple.osk'))
    try {
        $entry = $archive.GetEntry('material.txt')
        if ($null -eq $entry) { return $null }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $archive.Dispose() }
}
Build-Fixture
$package = Join-Path $output 'oms-simple.osk'
$unchangedTime = [IO.File]::GetLastWriteTimeUtc($package)
Build-Fixture
if ([IO.File]::GetLastWriteTimeUtc($package) -ne $unchangedTime) { throw 'Unchanged package was rewritten.' }
$material = Join-Path $fixture 'sources/oms-simple/material.txt'
$oldTime = [IO.File]::GetLastWriteTimeUtc($material)
[IO.File]::WriteAllText($material, 'newer')
[IO.File]::SetLastWriteTimeUtc($material, $oldTime)
Build-Fixture
if ((Read-Material) -cne 'newer') { throw 'Same-length, same-timestamp edit was not packaged.' }
[IO.File]::Delete($material)
Build-Fixture
if ($null -ne (Read-Material)) { throw 'Deleted material remained in the package.' }
foreach ($name in @('oms-simple', 'oms-complex')) {
    $stream = [IO.File]::OpenRead((Join-Path $output "$name.osk"))
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $actual = ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
    if ($actual -cne [IO.File]::ReadAllText((Join-Path $output "$name.sha256")).Trim()) { throw 'Embedded checksum input does not match package.' }
}
[IO.File]::Delete((Join-Path $fixture 'sources/oms-simple/skin.ini'))
$ErrorActionPreference = 'Continue'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixture 'Build-BuiltInSkins.ps1') -OutputDirectory $output 2>&1 | Out-String | Write-Host
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -eq 0) { throw 'Missing required source unexpectedly succeeded.' }
Write-Host "PASS: both skins, stable outputs, same-timestamp edits, deletions, checksum pairing and missing-source rejection. Evidence: $fixture"
