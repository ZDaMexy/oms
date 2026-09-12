param([Parameter(Mandatory = $true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

# Build inputs are the checked-in author files, never generated recipes or stale dist archives.
# Use the Windows build host consistently and only replace outputs when their bytes change.
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
function Get-Hash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
foreach ($name in @('oms-simple', 'oms-complex')) {
    $root = Join-Path $PSScriptRoot "sources/$name"
    $files = [Collections.Generic.SortedDictionary[string,string]]::new([StringComparer]::Ordinal)
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($root)
    [long]$total = 0
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        if (([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Skin source cannot be a link: $directory" }
        foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Skin source cannot be a link: $($entry.FullName)" }
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName); continue }
            $total += $entry.Length
            if ($total -gt 128MB -or $files.Count -ge 4096) { throw "Built-in skin exceeds package limits: $name" }
            $files.Add($entry.FullName.Substring($root.Length + 1).Replace('\', '/'), $entry.FullName)
        }
    }
    if (-not $files.ContainsKey('skin.ini')) { throw "Missing skin.ini: $name" }
    $destination = Join-Path $outputRoot "$name.osk"
    $temporary = Join-Path $outputRoot "$name.$([Guid]::NewGuid().ToString('N')).pending"
    try {
        $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
            try {
                foreach ($file in $files.GetEnumerator()) {
                    $entry = $archive.CreateEntry($file.Key, [IO.Compression.CompressionLevel]::Optimal)
                    $entry.LastWriteTime = [DateTimeOffset]::new(2026, 9, 9, 0, 0, 0, [TimeSpan]::Zero)
                    $inputStream = [IO.File]::OpenRead($file.Value)
                    try {
                        $destinationStream = $entry.Open()
                        try { $inputStream.CopyTo($destinationStream) } finally { $destinationStream.Dispose() }
                    } finally { $inputStream.Dispose() }
                }
            } finally { $archive.Dispose() }
            $stream.Flush($true)
        } finally { $stream.Dispose() }
        $hash = Get-Hash $temporary
        if (-not [IO.File]::Exists($destination)) { [IO.File]::Move($temporary, $destination) }
        elseif ((Get-Hash $destination) -ne $hash) { [IO.File]::Replace($temporary, $destination, [NullString]::Value) }
        $hashPath = Join-Path $outputRoot "$name.sha256"
        $hashText = $hash + "`n"
        if (-not [IO.File]::Exists($hashPath) -or [IO.File]::ReadAllText($hashPath) -cne $hashText) {
            [IO.File]::WriteAllText($hashPath, $hashText, [Text.UTF8Encoding]::new($false))
        }
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}
