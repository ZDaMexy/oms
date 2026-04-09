param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateRange(1, 300)]
    [int]$WaitSeconds = 8
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$desktopOutput = Join-Path $repoRoot "osu.Desktop\\bin\\$Configuration\\net8.0"
$executable = Join-Path $desktopOutput 'osu!.exe'

if (-not (Test-Path $executable))
{
    throw "Desktop executable not found at '$executable'. Build osu.Desktop ($Configuration) first."
}

$process = $null

try
{
    $process = Start-Process -FilePath $executable -WorkingDirectory $desktopOutput -PassThru

    Start-Sleep -Seconds $WaitSeconds
    $process.Refresh()

    if ($process.HasExited)
    {
        throw "osu! exited before completing the smoke test (exit code $($process.ExitCode))."
    }

    Write-Host "osu! remained running for $WaitSeconds seconds (PID $($process.Id))."
}
finally
{
    if ($process -and -not $process.HasExited)
    {
        Stop-Process -Id $process.Id
        Write-Host 'osu! process stopped after smoke test.'
    }
}