param(
    [string]$OutputDirectory = 'artifacts\manual-gates\bms-note-animation',
    [switch]$StageOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$repoRoot = $PSScriptRoot
$project = Join-Path $repoRoot 'osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj'

if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    $resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
} else {
    $resolvedOutput = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}

$environmentName = 'OMS_BMS_NOTE_ANIMATION_GATE_OUTPUT'
$previousValue = [Environment]::GetEnvironmentVariable($environmentName, 'Process')

try {
    if (-not $StageOnly) {
        [Environment]::SetEnvironmentVariable($environmentName, $resolvedOutput, 'Process')

        dotnet test $project `
            --filter 'FullyQualifiedName=osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate.BmsNoteAnimationManualGateGeneratorTest.TestGenerateAndValidateManualGateArtifacts' `
            --logger 'console;verbosity=minimal'

        if ($LASTEXITCODE -ne 0) {
            throw "Manual gate generation test failed with exit code $LASTEXITCODE."
        }
    }
} finally {
    [Environment]::SetEnvironmentVariable($environmentName, $previousValue, 'Process')
}

$goodPackage = Join-Path $resolvedOutput 'bms-note-animation-manual-gate.osk'
$brokenPackage = Join-Path $resolvedOutput 'bms-note-animation-manual-gate-broken.osk'
$chart = Join-Path $resolvedOutput 'chartbms\bms-note-animation-manual-gate\bms-note-animation-manual-gate.bme'

foreach ($required in @($goodPackage, $brokenPackage, $chart)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Expected generated artifact was not found: $required"
    }
}

# SkinImporter intentionally consumes successful .osk imports. Keep the deterministic
# source tree and SHA256SUMS intact by exposing disposable copies for the UI gate.
$importStaging = Join-Path $resolvedOutput 'import-staging'

function Get-PathEntryOrNull([string]$LiteralPath) {
    try {
        return Get-Item -LiteralPath $LiteralPath -Force -ErrorAction Stop
    } catch [System.Management.Automation.ItemNotFoundException] {
        return $null
    }
}

function Test-IsReparsePoint([System.IO.FileSystemInfo]$Entry) {
    return ($Entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
}

function Get-Sha256([string]$LiteralPath) {
    $stream = [IO.File]::OpenRead($LiteralPath)
    $algorithm = [Security.Cryptography.SHA256]::Create()

    try {
        return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '')
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

$stagingEntry = Get-PathEntryOrNull $importStaging

if ($null -ne $stagingEntry) {
    if (Test-IsReparsePoint $stagingEntry) {
        throw "Disposable import staging must not be a reparse point: $importStaging"
    }

    if (-not $stagingEntry.PSIsContainer) {
        throw "Disposable import staging exists but is not a directory: $importStaging"
    }
} else {
    New-Item -ItemType Directory -Path $importStaging | Out-Null

    $stagingEntry = Get-PathEntryOrNull $importStaging

    if ($null -eq $stagingEntry -or -not $stagingEntry.PSIsContainer -or (Test-IsReparsePoint $stagingEntry)) {
        throw "Disposable import staging could not be created as a normal directory: $importStaging"
    }
}

$stagedGood = Join-Path $importStaging ([IO.Path]::GetFileName($goodPackage))
$stagedBroken = Join-Path $importStaging ([IO.Path]::GetFileName($brokenPackage))

foreach ($destination in @($stagedGood, $stagedBroken)) {
    $destinationEntry = Get-PathEntryOrNull $destination

    if ($null -eq $destinationEntry) {
        continue
    }

    if (Test-IsReparsePoint $destinationEntry) {
        throw "Disposable import staging file must not be a reparse point: $destination"
    }

    if ($destinationEntry.PSIsContainer) {
        throw "Disposable import staging file path is occupied by a directory: $destination"
    }
}

Copy-Item -LiteralPath $goodPackage -Destination $stagedGood -Force
Copy-Item -LiteralPath $brokenPackage -Destination $stagedBroken -Force

foreach ($pair in @(@($goodPackage, $stagedGood), @($brokenPackage, $stagedBroken))) {
    $sourceHash = Get-Sha256 $pair[0]
    $stagedHash = Get-Sha256 $pair[1]

    if ($sourceHash -ne $stagedHash) {
        throw "Disposable import staging copy did not match its deterministic source: $($pair[1])"
    }
}

Write-Host "Manual gate artifacts generated at: $resolvedOutput"
Write-Host "  good:  $goodPackage"
Write-Host "  broken: $brokenPackage"
Write-Host "  chart:  $chart"
Write-Host "  import staging (disposable): $importStaging"
