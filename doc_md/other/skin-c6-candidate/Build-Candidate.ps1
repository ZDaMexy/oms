param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../../../artifacts/skin-c6'))
$ErrorActionPreference = 'Stop'
$candidateRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $candidateRoot '../../..'))
$candidateOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $candidateOutput -Force | Out-Null
$compilerProject = Join-Path $repositoryRoot 'tools/SkinScriptCompiler/SkinScriptCompiler.csproj'
$source = Join-Path $candidateRoot 'gameplay-skin.script'
$bytecode = Join-Path $candidateOutput 'gameplay-skin.bytecode'
dotnet run --project $compilerProject -c Release -- compile $source $bytecode
if ($LASTEXITCODE -ne 0) { throw 'Script compilation failed.' }
dotnet run --project $compilerProject -c Release --no-build -- verify $bytecode
if ($LASTEXITCODE -ne 0) { throw 'Bytecode verification failed.' }

# Reproducible 1x1 white author texture. No hidden or built-in product resources.
$texture = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR4nGP4DwQACfsD/fteaysAAAAASUVORK5CYII=')
[IO.File]::WriteAllBytes((Join-Path $candidateRoot 'tile.png'), $texture)
Add-Type -AssemblyName System.IO.Compression
$packagePath = Join-Path $candidateOutput 'oms-complex-c6.osk'
$stream = [IO.File]::Open($packagePath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($name in @('gameplay-skin.json', 'gameplay-skin.scene.json', 'gameplay-skin.script', 'skin.ini', 'tile.png')) {
            $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(2026, 9, 9, 0, 0, 0, [TimeSpan]::Zero)
            $entryStream = $entry.Open()
            try { $bytes = [IO.File]::ReadAllBytes((Join-Path $candidateRoot $name)); $entryStream.Write($bytes, 0, $bytes.Length) }
            finally { $entryStream.Dispose() }
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
Get-FileHash -Algorithm SHA256 -LiteralPath $packagePath
Write-Output $packagePath
