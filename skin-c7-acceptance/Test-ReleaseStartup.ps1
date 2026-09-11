param(
    [Parameter(Mandatory = $true)][string]$ReleaseDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($true)

# Hidden acceptance windows are omitted by Process.MainWindowHandle. Address only
# the retained child process's ordinary SDL window; WM_CLOSE still follows the
# game's existing exit confirmation, including its ongoing-operation veto.
if (-not ('OmsC7OwnedGameWindow' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class OmsC7OwnedGameWindow
{
    public sealed class CloseRequest
    {
        public string WindowHandle;
        public string WindowClass;
        public string WindowTitle;
        public int ProcessId;
        public bool Posted;
    }

    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static bool IsGameWindow(IntPtr window, int expectedProcessId)
    {
        uint ownerProcessId;
        if (GetWindowThreadProcessId(window, out ownerProcessId) == 0 || ownerProcessId != (uint)expectedProcessId ||
            GetWindow(window, 4) != IntPtr.Zero)
            return false;

        var windowClass = new StringBuilder(256);
        var windowTitle = new StringBuilder(256);
        return GetClassName(window, windowClass, windowClass.Capacity) > 0 &&
               GetWindowText(window, windowTitle, windowTitle.Capacity) > 0 &&
               windowClass.ToString() == "SDL_app" && windowTitle.ToString() == "osu!";
    }

    private static void AssertOwnedProcess(Process process, string expectedExecutable)
    {
        process.Refresh();
        if (process.HasExited)
            throw new InvalidOperationException("The retained acceptance process has already exited.");

        // Accessing Handle keeps this exact process identity alive across enumeration.
        if (process.Handle == IntPtr.Zero || !String.Equals(Path.GetFullPath(process.MainModule.FileName),
                Path.GetFullPath(expectedExecutable), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The retained process does not match this acceptance executable.");
    }

    public static CloseRequest PostClose(Process process, string expectedExecutable)
    {
        AssertOwnedProcess(process, expectedExecutable);
        var windows = new List<IntPtr>();
        EnumWindowCallback callback = (window, parameter) =>
        {
            if (IsGameWindow(window, process.Id))
                windows.Add(window);
            return true;
        };
        if (!EnumWindows(callback, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate the acceptance window.");
        GC.KeepAlive(callback);
        if (windows.Count != 1)
            throw new InvalidOperationException("Expected one owned SDL_app / osu! window, found " + windows.Count + ".");

        IntPtr target = windows[0];
        AssertOwnedProcess(process, expectedExecutable);
        if (!IsGameWindow(target, process.Id))
            throw new InvalidOperationException("The acceptance window identity changed before WM_CLOSE.");
        if (!PostMessage(target, 0x0010, IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not post WM_CLOSE to the acceptance window.");

        return new CloseRequest
        {
            WindowHandle = "0x" + target.ToInt64().ToString("x"),
            WindowClass = "SDL_app",
            WindowTitle = "osu!",
            ProcessId = process.Id,
            Posted = true
        };
    }
}
"@
}

function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose(); $stream.Dispose() }
}

function Assert-RegularAncestors([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if ((Test-Path -LiteralPath $current) -and (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw "路径含链接或重定向，已停止：$current"
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function Get-RegularFiles([string]$Root) {
    Assert-RegularAncestors $Root
    $pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $pending.Push((Get-Item -LiteralPath $Root -Force))
    while ($pending.Count -gt 0) {
        $entry = $pending.Pop()
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "文件树含链接，已停止：$($entry.FullName)" }
        if ($entry -is [IO.DirectoryInfo]) { foreach ($child in $entry.GetFileSystemInfos()) { $pending.Push($child) } }
        else { $entry }
    }
}

function Get-TreeSnapshot([string]$Root) {
    $snapshot = [ordered]@{}
    foreach ($file in @(Get-RegularFiles $Root | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($Root.TrimEnd('\', '/').Length + 1).Replace('\', '/')
        $snapshot.Add($relative, [ordered]@{ Sha256 = (Get-FileSha256 $file.FullName); Length = $file.Length; Attributes = [string]$file.Attributes })
    }
    return ,$snapshot
}

function Assert-SameSnapshot($Before, $After, [string]$Description) {
    if ($Before.Count -ne $After.Count) { throw "$Description 文件数量改变。" }
    foreach ($key in $Before.Keys) {
        if (-not $After.Contains($key) -or $Before[$key].Sha256 -ne $After[$key].Sha256 -or
            $Before[$key].Length -ne $After[$key].Length -or $Before[$key].Attributes -ne $After[$key].Attributes) {
            throw "$Description 文件内容或属性改变：$key"
        }
    }
}

function Assert-NoOmsInstance {
    $processes = @(Get-Process -Name 'osu!', 'osu', 'OMS' -ErrorAction SilentlyContinue)
    if ($processes.Count -gt 0) { throw ('已有游戏进程，未启动或关闭任何现有实例：' + (($processes | ForEach-Object { "$($_.ProcessName):$($_.Id)" }) -join ', ')) }
    $pipes = @([IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { [IO.Path]::GetFileName($_) -match '(^|[-_.])oms($|[-_.])' })
    if ($pipes.Count -gt 0) { throw ('已有 OMS 管道，未连接它或启动游戏：' + ($pipes -join ', ')) }
}

function Get-LogFiles([string[]]$Roots) {
    foreach ($root in ($Roots | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $root -PathType Container) { Get-RegularFiles $root | Where-Object { $_.Extension -eq '.log' } }
    }
}

function Read-SharedText([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true)
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

function Read-NewRuntime([string[]]$LogRoots, $OldPaths) {
    $texts = [Collections.Generic.List[string]]::new()
    foreach ($file in @(Get-LogFiles $LogRoots)) {
        if ($file.Name -like '*.runtime.log' -and -not $OldPaths.Contains($file.FullName)) {
            # The running logger may be rotating a file; retry its next complete read.
            try { $texts.Add((Read-SharedText $file.FullName)) }
            catch [IO.IOException] { }
        }
    }
    return $texts -join "`n"
}

function Invoke-ObservedStart([string]$Case, [string]$InstallRoot, [string]$DataRoot, [bool]$Custom) {
    $caseRoot = Join-Path $proofRoot "evidence/$Case"
    [IO.Directory]::CreateDirectory($caseRoot) | Out-Null
    $result = [ordered]@{
        Case = $Case; StartedUtc = [DateTime]::UtcNow.ToString('O'); InstallRoot = $InstallRoot; DataRoot = $DataRoot
        ProcessId = $null; Passed = $false; StableSeconds = 0; NormalExit = $false; ForcedTermination = $false
        ExitCode = $null; CloseRequests = @(); Checks = $null; WorkingCopySha256 = $null; CanonicalOriginals = @(); Logs = @(); Errors = @()
    }
    $process = $null
    $logRoots = @((Join-Path $InstallRoot 'data/logs'), (Join-Path $DataRoot 'logs'))
    $oldPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    try {
        @(Get-RegularFiles $InstallRoot) | Out-Null
        @(Get-RegularFiles $DataRoot) | Out-Null
        Assert-NoOmsInstance
        foreach ($file in @(Get-LogFiles $logRoots)) { $oldPaths.Add($file.FullName) | Out-Null }
        foreach ($name in @('oms-simple.osk', 'oms-complex.osk')) {
            $path = Join-Path $InstallRoot "Skins/Canonical/$name"
            $attributes = [IO.File]::GetAttributes($path)
            $result.CanonicalOriginals += [ordered]@{ File = $name; Attributes = [string]$attributes; Sha256 = (Get-FileSha256 $path) }
            if (($attributes -band [IO.FileAttributes]::ReadOnly) -eq 0) { throw '此发行副本的安装原件未保留只读属性；请用 Windows 资源管理器重新解压原 ZIP。本工具不会先补属性掩盖解包差异。' }
        }
        # Deliberately no import, chart, protocol or test-only product arguments.
        $process = Start-Process -FilePath (Join-Path $InstallRoot 'osu!.exe') -WorkingDirectory $InstallRoot -WindowStyle Hidden -PassThru
        $result.ProcessId = $process.Id
        $deadline = [DateTime]::UtcNow.AddSeconds(150)
        do {
            $process.Refresh()
            if ($process.HasExited) { throw '游戏在完整启动证据出现前退出，不能当作启动成功。' }
            $runtime = Read-NewRuntime $logRoots $oldPaths
            $checks = [ordered]@{
                PortableRoot = $runtime.Contains('Portable mode active. Data root: ' + (Join-Path $InstallRoot 'data'))
                PortableCache = Test-Path -LiteralPath (Join-Path $InstallRoot 'cache') -PathType Container
                Renderer = $runtime.Contains('Renderer initialised!')
                Running = $runtime.Contains('Host execution state changed to Running')
                Realm = $runtime.Contains('Opened realm "' + (Join-Path $DataRoot 'client.realm') + '"')
                FirstRunSetup = $runtime.Contains('Loaded FirstRunSetupOverlay!')
                Settings = $runtime.Contains('Loaded SettingsOverlay!')
                IntroOrMainMenu = [bool]($runtime -match 'OsuScreenStack[^\r\n]*entered (Intro|MainMenu)')
                CustomRoot = (-not $Custom) -or $runtime.Contains('Storage successfully changed to ' + $DataRoot)
                NoStorageFailure = -not $runtime.Contains('Custom storage location could not be used')
            }
            $result.Checks = $checks
            if (@($checks.Values | Where-Object { -not $_ }).Count -eq 0) { break }
            Start-Sleep -Milliseconds 300
        } while ([DateTime]::UtcNow -lt $deadline)
        if (@($result.Checks.Values | Where-Object { -not $_ }).Count -ne 0) { throw '等待完整启动超过 150 秒；缺少的标记保存在 Checks，原日志已保留。' }
        $stableUntil = [DateTime]::UtcNow.AddSeconds(8)
        while ([DateTime]::UtcNow -lt $stableUntil) {
            $process.Refresh()
            if ($process.HasExited) { throw '完成加载后未能稳定运行八秒。' }
            Start-Sleep -Milliseconds 250
        }
        $result.StableSeconds = 8
        $working = Join-Path $DataRoot 'skin-canonical/oms-simple.osk'
        Assert-RegularAncestors $working
        $result.WorkingCopySha256 = Get-FileSha256 $working
        if ($result.WorkingCopySha256 -ne (Get-FileSha256 (Join-Path $InstallRoot 'Skins/Canonical/oms-simple.osk'))) { throw '正式保底工作副本与本次安装原件不一致。' }
        for ($attempt = 1; $attempt -le 2; $attempt++) {
            $process.Refresh()
            if ($process.HasExited) { break }
            $close = [OmsC7OwnedGameWindow]::PostClose($process, (Join-Path $InstallRoot 'osu!.exe'))
            $result.CloseRequests += [ordered]@{
                Attempt = $attempt; Message = 'WM_CLOSE'; WindowAccepted = $close.Posted
                WindowHandle = $close.WindowHandle; WindowClass = $close.WindowClass; WindowTitle = $close.WindowTitle; ProcessId = $close.ProcessId
            }
            $process.WaitForExit(15000) | Out-Null
        }
        $process.Refresh()
        if (-not $process.HasExited) { throw '两次正常关闭请求后仍未退出；只结束本次进程并保留失败。' }
        $process.WaitForExit()
        $result.ExitCode = $process.ExitCode
        $runtime = Read-NewRuntime $logRoots $oldPaths
        $result.NormalExit = $result.ExitCode -eq 0 -and $runtime.Contains('Host execution state changed to Stopping') -and $runtime.Contains('Host execution state changed to Stopped')
        if (-not $result.NormalExit) { throw '退出码或 Stopping/Stopped 日志不满足正常退出证据。' }
        $badLines = @($runtime -split '\r?\n' | Where-Object { $_ -match '\[error\]|Too many unhandled exceptions|Unhandled exception|DllNotFoundException|already running' })
        if ($badLines.Count -gt 0) { $result.Errors += $badLines; throw '本次启动存在错误日志，不能按进程存活填写通过。' }
        $result.Passed = $true
    } catch {
        $result.Errors += $_.Exception.Message
    } finally {
        if ($null -ne $process) {
            $process.Refresh()
            if (-not $process.HasExited) {
                $result.ForcedTermination = $true
                $result.Passed = $false
                # The retained Process object identifies only the child created above.
                $process.Kill()
                $process.WaitForExit(10000) | Out-Null
            }
            if ($process.HasExited) { $result.ExitCode = $process.ExitCode }
        }
        $index = 0
        foreach ($file in @(Get-LogFiles $logRoots)) {
            if ($oldPaths.Contains($file.FullName)) { continue }
            $index++
            $copy = Join-Path $caseRoot ('{0:D2}-{1}' -f $index, $file.Name)
            Copy-Item -LiteralPath $file.FullName -Destination $copy
            $result.Logs += [ordered]@{ OriginalPath = $file.FullName; SavedPath = $copy; Sha256 = (Get-FileSha256 $copy) }
        }
        [IO.File]::WriteAllText((Join-Path $caseRoot 'result.json'), ($result | ConvertTo-Json -Depth 8), $utf8)
        if ($null -ne $process) { $process.Dispose() }
    }
    return $result
}

$sourceRoot = [IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\', '/')
$proofRoot = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/')
Assert-RegularAncestors $proofRoot
if (Test-Path -LiteralPath $proofRoot) { throw '输出目录必须尚不存在，以保留每次检查记录。' }
if ($sourceRoot.Equals($proofRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $sourceRoot.StartsWith($proofRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $proofRoot.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '发行源与检查输出必须互不包含。' }
[IO.Directory]::CreateDirectory($proofRoot) | Out-Null
$report = [ordered]@{
    Format = 'oms-c7-release-startup-proof.v1'; StartedUtc = [DateTime]::UtcNow.ToString('O'); CompletedUtc = $null
    ReleaseDirectory = $sourceRoot; OutputDirectory = $proofRoot; ScriptSha256 = (Get-FileSha256 $PSCommandPath)
    ReleaseManifestSha256 = $null; ReleaseExecutableSha256 = $null; ReleaseBuild = $null; SourceUnchanged = $false
    Outcome = 'Failed'; Cases = @(); CacheRecovery = $null; Update = $null; Errors = @()
    ManualAcceptance = 'V001-V004 0/4; V005 unsigned; C7 visual/GPU/device/long-duration unsigned'
}
try {
    $sourceBefore = Get-TreeSnapshot $sourceRoot
    if (Test-Path -LiteralPath (Join-Path $sourceRoot 'data')) { throw '请使用尚无 data/ 的完整新发行目录，不能复制用户旧保存根。' }
    foreach ($name in @('osu!.exe', 'osu!.runtimeconfig.json', 'osu.Game.dll', 'osu.Game.Rulesets.Bms.dll', 'osu.Game.Rulesets.Mania.dll', 'portable.ini', 'release-files.json', 'Skins/Canonical/oms-simple.osk', 'Skins/Canonical/oms-complex.osk', 'Update-OMS.ps1', 'skin-c7-acceptance/Create-CustomRootCopy.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $name) -PathType Leaf)) { throw "完整发行物缺少：$name" }
    }
    foreach ($name in @('oms-simple.osk', 'oms-complex.osk')) {
        if (([IO.File]::GetAttributes((Join-Path $sourceRoot "Skins/Canonical/$name")) -band [IO.FileAttributes]::ReadOnly) -eq 0) { throw '原发行来源的安装原件未保留只读属性；请用 Windows 资源管理器重新解压，本检查不修改来源属性。' }
    }
    $manifestPath = Join-Path $sourceRoot 'release-files.json'
    $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
    if ($manifest.Format -ne 'oms-offline-release-files.v1') { throw '发行清单无法识别。' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest.Files) {
        $relative = [string]$file.Path
        if ([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('\') -or $relative -match '(^/|:|(^|/)\.{1,2}(/|$)|[<>"|?*])' -or
            @($relative.Split('/') | Where-Object { $_ -eq '' -or $_ -match '[. ]$' }).Count -gt 0 -or -not $seen.Add($relative)) { throw '发行清单含无效或重复路径。' }
        if ($relative -match '(^|/)(data|storage\.ini)(/|$)') { throw '发行清单含用户数据或自定义保存指针。' }
        if (-not $sourceBefore.Contains($relative) -or $sourceBefore[$relative].Sha256 -ne $file.Sha256 -or $sourceBefore[$relative].Length -ne [long]$file.Length) { throw "发行文件缺失或损坏：$relative" }
    }
    foreach ($file in $sourceBefore.Keys) {
        if ($file -ne 'release-files.json' -and -not $seen.Contains($file)) { throw "发行目录含清单外文件，请重新完整解压：$file" }
    }
    $report.ReleaseManifestSha256 = Get-FileSha256 $manifestPath
    $report.ReleaseExecutableSha256 = Get-FileSha256 (Join-Path $sourceRoot 'osu!.exe')
    $report.ReleaseBuild = $manifest.Build
    [IO.File]::WriteAllText((Join-Path $proofRoot 'source-before.json'), ($sourceBefore | ConvertTo-Json -Depth 5), $utf8)
    Assert-NoOmsInstance
    $workspace = Join-Path $proofRoot 'workspace'
    [IO.Directory]::CreateDirectory($workspace) | Out-Null
    $app = Join-Path $workspace 'app'
    Copy-Item -LiteralPath $sourceRoot -Destination $app -Recurse
    [IO.Directory]::CreateDirectory((Join-Path $app 'data')) | Out-Null
    $case = Invoke-ObservedStart 'portable-first' $app (Join-Path $app 'data') $false
    $report.Cases += $case
    if (-not $case.Passed) { throw '便携首次启动未通过，后续操作未执行。' }

    $customScript = Join-Path $workspace 'Create-CustomRootCopy.ps1'
    Copy-Item -LiteralPath (Join-Path $app 'skin-c7-acceptance/Create-CustomRootCopy.ps1') -Destination $customScript
    & $customScript
    if (-not $?) { throw '独立自定义位置副本创建失败。' }
    $customApp = Join-Path $workspace 'app-custom'
    $customData = Join-Path $workspace 'custom-data'
    $pointer = Join-Path $customApp 'data/storage.ini'
    $pointerBefore = Get-FileSha256 $pointer
    $case = Invoke-ObservedStart 'custom-root' $customApp $customData $true
    $report.Cases += $case
    if (-not $case.Passed -or (Get-FileSha256 $pointer) -ne $pointerBefore) { throw '自定义位置启动失败或基础保存指针改变。' }

    Assert-NoOmsInstance
    $working = Join-Path $customData 'skin-canonical/oms-simple.osk'
    Assert-RegularAncestors $working
    $cacheEvidence = Join-Path $proofRoot 'evidence/cache-input'
    [IO.Directory]::CreateDirectory($cacheEvidence) | Out-Null
    Copy-Item -LiteralPath $working -Destination (Join-Path $cacheEvidence 'original-working.osk')
    [IO.File]::SetAttributes($working, ([IO.File]::GetAttributes($working) -band (-bnot [IO.FileAttributes]::ReadOnly)))
    [IO.File]::WriteAllBytes($working, [Text.Encoding]::ASCII.GetBytes('C7 deliberate damaged working copy'))
    $damagedHash = Get-FileSha256 $working
    Copy-Item -LiteralPath $working -Destination (Join-Path $cacheEvidence 'damaged-working.osk')
    $case = Invoke-ObservedStart 'damaged-cache-recovery' $customApp $customData $true
    $report.Cases += $case
    $preserved = @(Get-RegularFiles (Join-Path $customData 'skin-canonical') | Where-Object { $_.Name -like 'oms-simple.osk.preserved-*' -and (Get-FileSha256 $_.FullName) -eq $damagedHash })
    $report.CacheRecovery = [ordered]@{ DamagedSha256 = $damagedHash; PreservedPaths = @($preserved | ForEach-Object { $_.FullName }); PointerUnchanged = (Get-FileSha256 $pointer) -eq $pointerBefore }
    if (-not $case.Passed -or $preserved.Count -ne 1 -or -not $report.CacheRecovery.PointerUnchanged) { throw '工作副本恢复、坏数据保全或自定义指针保护未通过。' }

    # Reinstall this exact complete release through the shipped real updater.
    # This proves overwrite/recovery, not an unexecuted cross-version upgrade.
    Assert-NoOmsInstance
    $dataBefore = Get-TreeSnapshot $customData
    $bootstrapBefore = Get-TreeSnapshot (Join-Path $customApp 'data')
    $portableBefore = Get-FileSha256 (Join-Path $customApp 'portable.ini')
    $backupsBefore = @([IO.Directory]::GetDirectories($customApp, '.oms-update-backup-*'))
    & (Join-Path $sourceRoot 'Update-OMS.ps1') -UpdateSourceDirectory $sourceRoot -TargetDirectory $customApp
    if (-not $?) { throw '随包更新工具未完成。' }
    Assert-SameSnapshot $dataBefore (Get-TreeSnapshot $customData) '覆盖期间用户数据'
    Assert-SameSnapshot $bootstrapBefore (Get-TreeSnapshot (Join-Path $customApp 'data')) '覆盖期间基础保存根'
    $newBackups = @([IO.Directory]::GetDirectories($customApp, '.oms-update-backup-*') | Where-Object { $_ -notin $backupsBefore })
    if ($newBackups.Count -ne 1 -or (Get-FileSha256 (Join-Path $customApp 'portable.ini')) -ne $portableBefore) { throw '覆盖更新的备份或原便携模式不一致。' }
    $receiptPath = Join-Path $newBackups[0] 'update-receipt.json'
    $receipt = [IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json
    if ($receipt.State -ne 'Completed') { throw '实际覆盖记录尚未完成。' }
    $report.Update = [ordered]@{ Kind = 'Same-release complete overwrite'; ReceiptPath = $receiptPath; ReceiptSha256 = (Get-FileSha256 $receiptPath); UserDataUnchangedDuringUpdate = $true; BootstrapUnchangedDuringUpdate = $true; PortableMarkerUnchanged = $true }
    $case = Invoke-ObservedStart 'after-complete-overwrite' $customApp $customData $true
    $report.Cases += $case
    if (-not $case.Passed -or (Get-FileSha256 $pointer) -ne $pointerBefore) { throw '覆盖后的正常启动或自定义保存指针保护未通过。' }
    Assert-SameSnapshot $sourceBefore (Get-TreeSnapshot $sourceRoot) '原发行来源'
    $report.SourceUnchanged = $true
    $report.Outcome = 'Passed'
} catch {
    $report.Errors += $_.Exception.Message
} finally {
    $report.CompletedUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $proofRoot 'results.json'), ($report | ConvertTo-Json -Depth 10), $utf8)
}
Write-Host "实际启动记录：$(Join-Path $proofRoot 'results.json')"
if ($report.Outcome -ne 'Passed') { Write-Host ($report.Errors -join "`n"); exit 1 }
Write-Host '隔离副本的四次启动、保存位置、恢复和正常关闭通过；人工画面、设备与长期体验仍未签收。'
