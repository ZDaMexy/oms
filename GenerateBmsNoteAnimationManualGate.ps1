param(
    [string]$OutputDirectory = 'artifacts\manual-gates\bms-note-animation'
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
    [Environment]::SetEnvironmentVariable($environmentName, $resolvedOutput, 'Process')

    dotnet test $project `
        --filter 'FullyQualifiedName=osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate.BmsNoteAnimationManualGateGeneratorTest.TestGenerateAndValidateManualGateArtifacts' `
        --logger 'console;verbosity=minimal'

    if ($LASTEXITCODE -ne 0) {
        throw "Manual gate generation test failed with exit code $LASTEXITCODE."
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

Write-Host "Manual gate artifacts generated at: $resolvedOutput"
Write-Host "  good:  $goodPackage"
Write-Host "  broken: $brokenPackage"
Write-Host "  chart:  $chart"
