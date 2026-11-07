#requires -Version 7.0
<#
Install a verified Omni package as a new, independently versioned skin folder.
No existing skin, player file, setting, database or launcher is overwritten.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][ValidatePattern('^v[0-9]{8}$')][string]$Version,
    [Parameter(Mandatory)][string]$Receipt,
    [string]$PlayerRoot = 'D:\beatoraja',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $repoRoot 'UseDevelopmentStorage.ps1')
$packageRoot = (Resolve-Path -LiteralPath $Package).Path.TrimEnd('\')
$playerPath = (Resolve-Path -LiteralPath $PlayerRoot).Path.TrimEnd('\')
$skinRoot = (Resolve-Path -LiteralPath (Join-Path $playerPath 'skin')).Path.TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $playerPath 'beatoraja.jar') -PathType Leaf)) {
    throw 'PlayerRoot must contain the intended beatoraja installation.'
}
$receiptPath = [IO.Path]::GetFullPath($Receipt)
$evidenceRoot = Join-Path $repoRoot 'artifacts\beatoraja-capture'
if (-not $receiptPath.StartsWith($evidenceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Receipt must be a new file inside artifacts/beatoraja-capture.'
}
if (Test-Path -LiteralPath $receiptPath) { throw 'Keep earlier installation evidence unchanged.' }

function Assert-PlainAncestors([string]$Path) {
    $current = $Path
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Reparse ancestor is outside this installation contract: $current"
            }
        }
        $current = Split-Path -Parent $current
    }
}
Assert-PlainAncestors $packageRoot
Assert-PlainAncestors $skinRoot
Assert-PlainAncestors $receiptPath
$sourceReceipt = Get-Content -LiteralPath (Join-Path $packageRoot 'package-receipt.json') -Raw | ConvertFrom-Json
$names = @{
    standard = 'Standard Model Skin Omni Until 26'
    lightning = 'Lightning Model Skin Omni Until 33'
}
if (-not $names.ContainsKey($sourceReceipt.family) -or $sourceReceipt.name -cne $names[$sourceReceipt.family]) {
    throw 'Package must declare one of the two independent Omni identities.'
}
$directoryName = $sourceReceipt.name + '.' + $Version
$target = [IO.Path]::GetFullPath((Join-Path $skinRoot $directoryName))
if ((Split-Path -Parent $target) -cne $skinRoot -or (Test-Path -LiteralPath $target)) {
    throw 'The exact version directory must be a new direct child of the player skin directory.'
}
[IO.Directory]::CreateDirectory((Split-Path -Parent $receiptPath)) | Out-Null
$workspace = Join-Path $repoRoot ('.dev-cache\temp\beatoraja-install-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($workspace) | Out-Null
$verifier = Join-Path $PSScriptRoot 'beatoraja_verify.py'
function Verify-Package([string]$Path, [string]$Name) {
    $verificationPath = Join-Path $workspace ($Name + '.json')
    & $Python -B $verifier package $Path --output $verificationPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Package verification failed: $Name" }
    return (Get-Content -LiteralPath $verificationPath -Raw | ConvertFrom-Json).results[0]
}
$source = Verify-Package $packageRoot 'source'
if (-not $source.payloadSha256 -or $source.files -lt 3) { throw 'No complete package inventory was verified.' }
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($skinRoot))
if ($drive.AvailableFreeSpace -lt ($source.bytes + 16MB)) { throw 'Insufficient space for the verified skin payload.' }
$staging = [IO.Path]::GetFullPath((Join-Path $skinRoot ('.oms-install-' + [Guid]::NewGuid().ToString('N'))))
if ((Split-Path -Parent $staging) -cne $skinRoot -or (Test-Path -LiteralPath $staging)) {
    throw 'Staging path must be a new direct child of the verified skin directory.'
}
$record = [ordered]@{
    schema = 1; family = $sourceReceipt.family; name = $sourceReceipt.name; version = $Version
    source = $packageRoot; target = $target; staging = $staging
    sourcePayloadSha256 = $source.payloadSha256; sourceReceiptSha256 = $source.receiptSha256
    files = $source.files; bytes = $source.bytes; startedUtc = [DateTime]::UtcNow.ToString('O')
    installed = $false; verified = $false; error = $null
    boundary = 'Only the verified skin inventory is copied into a new version directory. Player settings, databases, existing skins and current selection are not written. Separate preservation evidence covers those files.'
}
try {
    [IO.Directory]::CreateDirectory($staging) | Out-Null
    foreach ($entry in $source.inventory) {
        $origin = [IO.Path]::GetFullPath((Join-Path $packageRoot $entry.path))
        $destination = [IO.Path]::GetFullPath((Join-Path $staging $entry.path))
        if (-not $origin.StartsWith($packageRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $destination.StartsWith($staging + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A verified inventory path escaped the package or staging directory.'
        }
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
        Copy-Item -LiteralPath $origin -Destination $destination -ErrorAction Stop
    }
    $staged = Verify-Package $staging 'staging'
    if ($staged.payloadSha256 -cne $source.payloadSha256) { throw 'Staging differs from the verified source payload.' }
    Assert-PlainAncestors $staging
    Assert-PlainAncestors $target
    if ((Resolve-Path -LiteralPath $staging).Path.TrimEnd('\') -cne $staging -or
        (Split-Path -Parent $staging) -cne $skinRoot -or (Split-Path -Parent $target) -cne $skinRoot) {
        throw 'Final rename targets no longer match the verified skin directory.'
    }
    # Same-volume rename fails if target appeared in the meantime; it cannot
    # merge into or replace a pre-existing user directory.
    [IO.Directory]::Move($staging, $target)
    $record.installed = $true
    $installed = Verify-Package $target 'installed'
    if ($installed.payloadSha256 -cne $source.payloadSha256) { throw 'Installed payload differs from its source.' }
    $record.verified = $true
} catch {
    $record.error = $_.Exception.Message
    throw
} finally {
    $record.finishedUtc = [DateTime]::UtcNow.ToString('O')
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding utf8
}
Write-Output ([pscustomobject]@{ Installed=$record.installed; Verified=$record.verified; Target=$target; Receipt=$receiptPath })
