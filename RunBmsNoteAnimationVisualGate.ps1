param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$repoRoot = $PSScriptRoot
$project = Join-Path $repoRoot 'osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj'
$executable = Join-Path $repoRoot "osu.Game.Rulesets.Bms.Tests\bin\$Configuration\net8.0\osu.Game.Rulesets.Bms.Tests.exe"
$scene = 'osu.Game.Rulesets.Bms.Tests.Skinning.TestSceneBmsManagedPackageNoteVisualGate'

dotnet build $project -p:Configuration=$Configuration -p:GenerateFullPaths=true -verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    throw "Visual gate build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Visual gate executable was not found: $executable"
}

$startupFrameworkConfig = Join-Path ([IO.Path]::GetDirectoryName($executable)) 'framework.ini'

if (Test-Path -LiteralPath $startupFrameworkConfig) {
    throw "Visual gate refuses startup-directory framework storage: $startupFrameworkConfig"
}

Write-Host 'Opening the isolated BMS note-animation visual gate.'
Write-Host '  storage: internally generated disposable host/data roots'
Write-Host "  scene:   $scene"
Write-Host '  result:  PASS remains visible for 3 seconds, then the window closes automatically'
Write-Host '           (load/step/watchdog failure returns 1; early close returns 3)'

& $executable --exact-test $scene
$visualGateExitCode = $LASTEXITCODE

if ($visualGateExitCode -ne 0) {
    [Console]::Error.WriteLine("Visual gate exited with code $visualGateExitCode.")
    exit $visualGateExitCode
}

Write-Host 'Visual gate completed successfully.'
