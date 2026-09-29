<#
.SYNOPSIS
    Keep OMS development caches and temporary files beside the checkout.
.EXAMPLE
    . .\UseDevelopmentStorage.ps1
    dotnet build osu.Desktop.slnf -c Release
#>

& {
    $ErrorActionPreference = 'Stop'
    $storageRoot = Join-Path $PSScriptRoot '.dev-cache'

    # Refuse system-drive checkouts and redirected ancestors instead of silently
    # putting large caches back on the system drive.
    if ([IO.Path]::GetPathRoot($storageRoot).TrimEnd('\') -eq $env:SystemDrive) {
        throw 'Use an OMS checkout on a non-system drive before building.'
    }
    $ancestor = $storageRoot
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and
            ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Development storage cannot use redirected directories: $ancestor"
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }

    $directories = @{
        TEMP = 'temp'
        TMP = 'temp'
        NUGET_PACKAGES = 'nuget\packages'
        NUGET_HTTP_CACHE_PATH = 'nuget\http-cache'
        NUGET_SCRATCH = 'nuget\scratch'
        NUGET_PLUGINS_CACHE_PATH = 'nuget\plugins-cache'
        DOTNET_CLI_HOME = 'dotnet'
        DOTNET_BUNDLE_EXTRACT_BASE_DIR = 'dotnet-bundles'
    }
    foreach ($name in $directories.Keys) {
        $path = Join-Path $storageRoot $directories[$name]
        [IO.Directory]::CreateDirectory($path) | Out-Null
        [Environment]::SetEnvironmentVariable($name, $path, 'Process')
    }
    Write-Host "OMS development storage: $storageRoot"
}
