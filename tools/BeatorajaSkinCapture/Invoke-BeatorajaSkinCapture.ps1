#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Skin,
    [ValidateSet('sp1','sp2','dp')][string]$Mode = 'sp1',
    [ValidateSet('autoplay','success','failure','hcn-recovery','timing')][string]$Scenario = 'autoplay',
    [ValidateSet('LN','CN','HCN')][string]$LongNoteMode = 'LN',
    [ValidateSet('AssistEasy','Easy','Normal','Hard','ExHard','Hazard')][string]$Gauge = 'Hard',
    [string[]]$SkinOption = @(),
    [string[]]$SkinFile = @(),
    [AllowEmptyString()][string]$SideOptionName = 'Play Side',
    [ValidateSet('HD','FULLHD','WQHD','ULTRAHD')][string]$Resolution = 'HD',
    [ValidateRange(0.25,10)][double]$HiSpeed = 2.0,
    [ValidateRange(0.0,1.0)][double]$LaneCover,
    [ValidateRange(0.0,1.0)][double]$Lift,
    [ValidateRange(0.0,1.0)][double]$Hidden,
    [switch]$ShowReadouts,
    [ValidateRange(20,180)][int]$TimeoutSeconds = 60,
    [string]$Jar,
    [string]$Java,
    [string]$Javac = 'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.8-hotspot\bin\javac.exe',
    [string]$ResourceRoot,
    [string]$NativeRoot,
    [string]$OutputRoot
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $repo 'UseDevelopmentStorage.ps1')
if (-not $OutputRoot) { $OutputRoot = Join-Path $repo '.dev-cache\temp\beatoraja-capture-dev' }
if (-not $Java) {
    $Java = @('D:\beatoraja\jre\bin\java.exe', 'D:\beatoraja0.8.8-jre-win64\jre\bin\java.exe') | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $Java) { throw 'Pass -Java with the JavaFX-enabled Java runtime.' }
}
if (-not $ResourceRoot) {
    $ResourceRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $Java))
    if (-not (Test-Path -LiteralPath (Join-Path $ResourceRoot 'skin\default') -PathType Container)) {
        throw 'Pass -ResourceRoot with the installed official default skin and font resources.'
    }
}
if (-not $Jar) { $Jar = Join-Path $ResourceRoot 'beatoraja.jar' }
if (-not $NativeRoot) { $NativeRoot = Join-Path (Split-Path -Parent $Jar) 'natives' }
foreach ($name in @('Skin','Jar','Java','Javac','ResourceRoot','NativeRoot')) {
    $value = Get-Variable -Name $name -ValueOnly
    Set-Variable -Name $name -Value (Resolve-Path -LiteralPath $value).Path
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$allowed = @((Join-Path $repo '.dev-cache\temp\beatoraja-capture-dev'), (Join-Path $repo 'artifacts\beatoraja-capture'))
if (-not ($allowed | Where-Object { $OutputRoot.Equals($_, [StringComparison]::OrdinalIgnoreCase) -or $OutputRoot.StartsWith($_ + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })) {
    throw 'OutputRoot must be inside .dev-cache/temp/beatoraja-capture-dev or artifacts/beatoraja-capture.'
}
# Refuse junction/symlink ancestors so the isolated output contract cannot redirect into an installation.
$ancestor = $OutputRoot
while ($ancestor) {
    if (Test-Path -LiteralPath $ancestor) {
        $item = Get-Item -LiteralPath $ancestor -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse output ancestor: $ancestor" }
    }
    $ancestor = Split-Path -Parent $ancestor
}
[IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$caseName = '{0}-{1}-{2}-{3}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $Mode, $Scenario, ([Guid]::NewGuid().ToString('N').Substring(0,8))
$caseRoot = Join-Path $OutputRoot $caseName
[IO.Directory]::CreateDirectory($caseRoot) | Out-Null
$classes = Join-Path $caseRoot 'classes'
$tempPath = Join-Path $caseRoot 'temp'
[IO.Directory]::CreateDirectory($classes) | Out-Null
[IO.Directory]::CreateDirectory($tempPath) | Out-Null
$manifest = [ordered]@{ schema=1; purpose='Disposable beatoraja skin capture'; engineJar=$Jar; engineSha256=(Get-FileHash -LiteralPath $Jar -Algorithm SHA256).Hash; harnessSourceSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Capture.java') -Algorithm SHA256).Hash; launcherSourceSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash; java=$Java; skin=$Skin; skinEntrySha256=(Get-FileHash -LiteralPath $Skin -Algorithm SHA256).Hash; resourceRoot=$ResourceRoot; nativeRoot=$NativeRoot; scenario=$Scenario; mode=$Mode; createdUtc=[DateTime]::UtcNow.ToString('O') }
$packageReceipt = Join-Path (Split-Path -Parent $Skin) 'package-receipt.json'
if (Test-Path -LiteralPath $packageReceipt -PathType Leaf) {
    $manifest.packageReceipt = $packageReceipt
    $manifest.packageReceiptSha256 = (Get-FileHash -LiteralPath $packageReceipt -Algorithm SHA256).Hash
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $caseRoot 'capture-workspace.json') -Encoding utf8
& $Javac '-J-Duser.language=en' '-J-Dfile.encoding=UTF-8' --release 17 -encoding UTF-8 -cp $Jar -d $classes (Join-Path $PSScriptRoot 'Capture.java') 2>&1 | Tee-Object -FilePath (Join-Path $caseRoot 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Capture compilation failed. Evidence: $caseRoot" }
$lnIndex = @{ LN=0; CN=1; HCN=2 }[$LongNoteMode]
$gaugeIndex = @{ AssistEasy=0; Easy=1; Normal=2; Hard=3; ExHard=4; Hazard=5 }[$Gauge]
$arguments = @('-Xms256m','-Xmx2g', '-Dfile.encoding=UTF-8', '-Dsun.stdout.encoding=UTF-8', '-Dsun.stderr.encoding=UTF-8', ('-Djava.io.tmpdir=' + $tempPath), ('-Djava.library.path=' + $NativeRoot), '-cp', ($classes + [IO.Path]::PathSeparator + $Jar), 'Capture', '--work', $caseRoot, '--skin', $Skin, '--resources', $ResourceRoot, '--scenario', $Scenario, '--mode', $Mode, '--side-option', $SideOptionName, '--lnmode', "$lnIndex", '--resolution', $Resolution, '--hispeed', $HiSpeed.ToString([Globalization.CultureInfo]::InvariantCulture), '--timeout', "$TimeoutSeconds")
$arguments += @('--gauge', "$gaugeIndex")
if ($ShowReadouts) { $arguments += @('--show-readouts', 'true') }
foreach ($option in $SkinOption) { $arguments += @('--option', $option) }
foreach ($file in $SkinFile) { $arguments += @('--file', $file) }
foreach ($readingName in @('LaneCover','Lift','Hidden')) {
    if ($PSBoundParameters.ContainsKey($readingName)) {
        $readingValue = [double]$PSBoundParameters[$readingName]
        if (-not [double]::IsFinite($readingValue)) { throw "$readingName must be finite." }
        $arguments += @(('--' + $readingName.ToLowerInvariant()), $readingValue.ToString([Globalization.CultureInfo]::InvariantCulture))
    }
}
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $Java; $start.WorkingDirectory = $caseRoot; $start.UseShellExecute = $false
$start.CreateNoWindow = $true; $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
$start.StandardOutputEncoding = [Text.Encoding]::UTF8; $start.StandardErrorEncoding = [Text.Encoding]::UTF8
$start.Environment['TEMP'] = $tempPath; $start.Environment['TMP'] = $tempPath
foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
$stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
$timedOut = -not $process.WaitForExit(($TimeoutSeconds + 20) * 1000)
if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
[IO.File]::WriteAllText((Join-Path $caseRoot 'stdout.log'), $stdout.GetAwaiter().GetResult())
[IO.File]::WriteAllText((Join-Path $caseRoot 'stderr.log'), $stderr.GetAwaiter().GetResult())
$exitCode = $process.ExitCode
$hostReceipt = [ordered]@{ pid=$process.Id; exited=$process.HasExited; exitCode=$exitCode; timedOut=$timedOut; completedUtc=[DateTime]::UtcNow.ToString('O') }
$hostReceipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $caseRoot 'host-receipt.json') -Encoding utf8
$process.Dispose()
$receiptPath = Join-Path $caseRoot 'receipt.json'
# The bundled LWJGL 2 backend deliberately calls System.exit(-1) after normal disposal.
if ($timedOut -or $exitCode -notin @(0,-1) -or -not (Test-Path -LiteralPath $receiptPath)) { throw "Capture did not exit successfully. Evidence: $caseRoot" }
$receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
if (-not $receipt.success -or -not $receipt.normalDispose) {
    $reason = if ($receipt.error) { $receipt.error } elseif (-not $receipt.diagnosticsClean) { 'Unexpected WARNING/SEVERE; inspect receipt warnings' } else { 'Incomplete outcome or disposal' }
    throw "Capture assertions failed: $reason. Evidence: $caseRoot"
}
if ((Get-FileHash -LiteralPath $Skin -Algorithm SHA256).Hash -ne $manifest.skinEntrySha256 -or
    ($manifest.Contains('packageReceiptSha256') -and (Get-FileHash -LiteralPath $packageReceipt -Algorithm SHA256).Hash -ne $manifest.packageReceiptSha256)) {
    throw "Skin entry or package receipt changed during capture. Evidence: $caseRoot"
}
[pscustomobject]@{ Success=$true; Scenario=$Scenario; Mode=$Mode; Frames=$receipt.frames.Count; Skin=$receipt.skinLoaded.name; Receipt=$receiptPath; ExitCode=$exitCode }
