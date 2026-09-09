param([Parameter(Mandatory = $true)][string]$OutputDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose(); $stream.Dispose() }
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw '输入目录已存在；请指定新的空目录，保留之前的验收记录。' }
$ancestor = [IO.Path]::GetDirectoryName($outputRoot)
while ($ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw '观察输入目录不能经过链接。' }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}
$videoFixture = Join-Path $PSScriptRoot 'fixtures/viewport.mp4'
if (-not (Test-Path -LiteralPath $videoFixture -PathType Leaf)) { throw '随包视频缺失；请修复完整验收工具包。' }
if ((Get-FileSha256 $videoFixture) -ne 'c2654af16841a1f01bcbcd9f33ce70de7d7c46af256c8ad32ce22bb1aaebdaee') { throw '随包视频损坏；请修复完整验收工具包。' }
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression


function Write-Text([string]$RelativePath, [string]$Content) {
    $path = Join-Path $outputRoot $RelativePath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path, $Content.Replace("`r`n", "`n"), $utf8)
}

function Write-Silence([string]$Path, [int]$Seconds) {
    $bytes = 22050 * 2 * $Seconds
    $stream = [IO.File]::Create($Path)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + $bytes))
        $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
        $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]22050)
        $writer.Write([int]44100); $writer.Write([int16]2); $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]$bytes)
        $writer.Write([byte[]]::new($bytes))
    } finally { $writer.Dispose() }
}

function New-Frame([int]$Index, [int]$Count, [string]$Kind) {
    $bitmap = [Drawing.Bitmap]::new(96, 32)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(255, 15, 35, 60))
        $colour = switch ($Kind) { 'head' { [Drawing.Color]::Cyan }; 'body' { [Drawing.Color]::Gold }; 'tail' { [Drawing.Color]::Magenta }; default { [Drawing.Color]::DeepSkyBlue } }
        $brush = [Drawing.SolidBrush]::new($colour)
        try { $graphics.FillRectangle($brush, 2, 2, 92, 28) } finally { $brush.Dispose() }
        $graphics.FillRectangle([Drawing.Brushes]::White, [int](($Index * 84) / $Count) + 2, 3, 10, 26)
        $stream = [IO.MemoryStream]::new()
        try { $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png); return ,$stream.ToArray() } finally { $stream.Dispose() }
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Write-Package([string]$Name, [int]$Frames, [bool]$Broken, [double]$BodyWidth) {
    $entries = [Collections.Generic.SortedDictionary[string, byte[]]]::new([StringComparer]::Ordinal)
    $ini = "[General]`nName: OMS C7 Third-party $Name`nAuthor: OMS acceptance fixture`nVersion: 2.7`n"
    foreach ($mode in @('5K', '7K', '9K', '9K_PMS', '14K')) {
        $ini += "`n[Bms]`nKeymode: $mode`nLongNoteBodyWidth: $($BodyWidth.ToString([Globalization.CultureInfo]::InvariantCulture))`n"
        foreach ($lane in @('1', 'S', 'S2')) {
            $ini += "NoteImage${lane}: ordinary`nNoteImage${lane}H: head`nNoteImage${lane}L: body`nNoteImage${lane}T: tail`n"
        }
    }
    $entries.Add('skin.ini', $utf8.GetBytes($ini))
    foreach ($kind in @('ordinary', 'head', 'body', 'tail')) {
        if ($Frames -eq 0) { $entries.Add("$kind.png", (New-Frame 0 1 $kind)); continue }
        for ($frame = 0; $frame -lt $Frames; $frame++) {
            if ($Broken -and $frame -eq 0 -and ($kind -ne 'ordinary' -or $Name -eq 'v001-broken')) { continue }
            $entries.Add("$kind-$frame.png", (New-Frame $frame $Frames $kind))
        }
    }
    $path = Join-Path $outputRoot "packages/$Name.osk"
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    $stream = [IO.File]::Create($path)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($pair in $entries.GetEnumerator()) {
            $entry = $archive.CreateEntry($pair.Key, [IO.Compression.CompressionLevel]::NoCompression)
            $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $entry.ExternalAttributes = 0
            $entryStream = $entry.Open()
            try { $entryStream.Write($pair.Value, 0, $pair.Value.Length) } finally { $entryStream.Dispose() }
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
}

# Short silent inputs are original observation fixtures, not substitutes for real charts or sound checks.
$bmsModes = @(
    @{ Name = '5K'; Extension = 'bms'; Channels = @(0x11, 0x12, 0x13, 0x14, 0x15, 0x16) },
    @{ Name = '7K'; Extension = 'bme'; Channels = @(0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x18, 0x19) },
    @{ Name = '9K-BMS'; Extension = 'bms'; Channels = @(0x11..0x19) },
    @{ Name = '9K-PMS'; Extension = 'pms'; Channels = @(0x11..0x19) },
    @{ Name = '14K'; Extension = 'bms'; Channels = @(0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x18, 0x19, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x28, 0x29) }
)
foreach ($mode in $bmsModes) {
    $folder = "chartbms/OMS-C7-$($mode.Name)"
    $chart = "#PLAYER $(if ($mode.Name -eq '14K') { 3 } else { 1 })`n#GENRE OMS Skin observation`n#TITLE OMS C7 $($mode.Name) Short and Hold`n#ARTIST OMS contributors`n#BPM 120`n#PLAYLEVEL 1`n#RANK 2`n#TOTAL 100`n#LNTYPE 1`n#WAV01 silent.wav`n#BMP01 viewport.png`n#BMP02 viewport.mp4`n#00004:01`n#00104:02`n#00504:02`n#00904:02`n#01304:02`n#01704:02`n"
    foreach ($channel in $mode.Channels) {
        $shortChannel = '{0:X2}' -f $channel
        $holdChannel = '{0:X2}' -f ($channel + 0x40)
        $chart += "#001${shortChannel}:0100010001000100`n#003${shortChannel}:0100010001000100`n"
        $chart += "#005${holdChannel}:0100000000000100`n#007${holdChannel}:0100000000000100`n"
        $chart += "#009${shortChannel}:0101010101010101`n#011${holdChannel}:0100000000000100`n"
        $chart += "#014${shortChannel}:0100010001000100`n#017${shortChannel}:0100010001000100`n"
    }
    Write-Text "$folder/observe.$($mode.Extension)" $chart
    Write-Silence (Join-Path $outputRoot "$folder/silent.wav") 1
    Copy-Item -LiteralPath $videoFixture -Destination (Join-Path $outputRoot "$folder/viewport.mp4")
    $bitmap = [Drawing.Bitmap]::new(320, 180)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(255, 28, 54, 84))
        $graphics.DrawRectangle([Drawing.Pens]::White, 1, 1, 317, 177)
        $graphics.DrawLine([Drawing.Pens]::Cyan, 0, 0, 319, 179)
        $graphics.DrawLine([Drawing.Pens]::Gold, 319, 0, 0, 179)
        $graphics.DrawString('BGA SAFE VIEWPORT', [Drawing.SystemFonts]::DefaultFont, [Drawing.Brushes]::White, 85, 80)
        $bitmap.Save((Join-Path $outputRoot "$folder/viewport.png"), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}

foreach ($keys in @(1..10) + @(12, 14, 16, 18)) {
    $chart = "osu file format v14`n`n[General]`nAudioFilename: silent.wav`nAudioLeadIn: 0`nPreviewTime: 5000`nMode: 3`n`n[Metadata]`nTitle:OMS C7 mania Short and Hold`nArtist:OMS contributors`nCreator:OMS acceptance fixture`nVersion:${keys}K`nBeatmapID:0`nBeatmapSetID:-1`n`n[Difficulty]`nHPDrainRate:3`nCircleSize:$keys`nOverallDifficulty:5`nApproachRate:5`nSliderMultiplier:1.4`nSliderTickRate:1`n`n[TimingPoints]`n0,500,4,1,0,60,1,0`n`n[HitObjects]`n"
    for ($lane = 0; $lane -lt $keys; $lane++) {
        $x = [int][Math]::Floor((($lane + 0.5) * 512) / $keys)
        foreach ($time in @(4000, 6000, 8000, 10000, 24000, 26000, 28000, 30000, 32000, 34000)) {
            $chart += "$x,192,$($time + $lane * 30),1,0,0:0:0:0:`n"
        }
        foreach ($time in @(12000, 16000, 20000)) { $chart += "$x,192,$time,128,0,$($time + 2500):0:0:0:0:`n" }
    }
    Write-Text "chartmania/OMS-C7-mania/observe-${keys}K.osu" $chart
}
Write-Silence (Join-Path $outputRoot 'chartmania/OMS-C7-mania/silent.wav') 40
Write-Package 'v001-v004-static' 0 $false 0.5775
Write-Package 'v001-v004-animation-a' 60 $false 0.45
Write-Package 'v001-v004-animation-b' 3 $false 0.8
Write-Package 'v002-v004-broken' 3 $true 0.5775
Write-Package 'v001-broken' 3 $true 0.5775

$hashes = @(Get-ChildItem -LiteralPath $outputRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($outputRoot.Length + 1).Replace('\', '/')
    "$(Get-FileSha256 $_.FullName)  $relative"
})
Write-Text 'SHA256SUMS.txt' (($hashes -join "`n") + "`n")
Write-Host "观察谱与第三方输入已生成：$outputRoot"
