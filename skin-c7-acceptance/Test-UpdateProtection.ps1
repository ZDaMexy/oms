param([Parameter(Mandatory=$true)][string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$proofRoot=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\','/')
if(Test-Path -LiteralPath $proofRoot){throw '验证输出必须是新目录。'}
$ancestor=$proofRoot
while($ancestor){if((Test-Path -LiteralPath $ancestor) -and (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)){throw '验证输出不能经过链接。'};$ancestor=[IO.Path]::GetDirectoryName($ancestor)}
[IO.Directory]::CreateDirectory($proofRoot)|Out-Null
$encoding=[Text.UTF8Encoding]::new($true)
$updater=Join-Path $PSScriptRoot 'Update-Installation.ps1'
function Sha([string]$Path){$stream=[IO.File]::OpenRead($Path);$hash=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$hash.Dispose();$stream.Dispose()}}
function Write-Fixture([string]$Path,[string]$Text){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null;[IO.File]::WriteAllText($Path,$Text,$encoding)}
function Snapshot([string]$Root){
    $snapshot=[ordered]@{};$pending=[Collections.Generic.Stack[IO.FileSystemInfo]]::new();$pending.Push((Get-Item -LiteralPath $Root -Force))
    while($pending.Count -gt 0){$file=$pending.Pop();if(($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '验证文件树含链接。'}
        if($file -is [IO.DirectoryInfo]){foreach($child in $file.GetFileSystemInfos()){$pending.Push($child)}}
        else{$snapshot.Add($file.FullName.Substring($Root.Length+1).Replace('\','/'),[ordered]@{Hash=(Sha $file.FullName);Attributes=[string]$file.Attributes})}
    };return ,$snapshot
}
function Assert-Unchanged($Before,$After,[string]$Label){if($Before.Count -ne $After.Count){throw "$Label 文件数量改变。"};foreach($key in $Before.Keys){if(-not $After.Contains($key) -or $Before[$key].Hash -ne $After[$key].Hash -or $Before[$key].Attributes -ne $After[$key].Attributes){throw "$Label 内容或属性改变：$key"}}}
function New-Release([string]$Name){
    $root=Join-Path $proofRoot $Name;$files=@()
    $contents=[ordered]@{'osu!.exe'='new program fixture, not executable';'runtime/locked.dll'='new runtime fixture';'Skins/Canonical/oms-simple.osk'='new simple fixture';'Skins/Canonical/oms-complex.osk'='new complex fixture';'runtime/new.dat'='new runtime file';'portable.ini'=''}
    foreach($name in $contents.Keys){$path=Join-Path $root $name;Write-Fixture $path $contents[$name];$files += [ordered]@{Path=$name;Length=(Get-Item -LiteralPath $path).Length;Sha256=(Sha $path)}}
    Write-Fixture (Join-Path $root 'release-files.json') ([ordered]@{Format='oms-offline-release-files.v1';Files=$files}|ConvertTo-Json -Depth 5)
    foreach($name in @('oms-simple.osk','oms-complex.osk')){$path=Join-Path $root "Skins/Canonical/$name";[IO.File]::SetAttributes($path,([IO.File]::GetAttributes($path) -bor [IO.FileAttributes]::ReadOnly))};return $root
}
function New-Target([string]$Name,[string]$Mode){
    $root=Join-Path $proofRoot $Name;Write-Fixture (Join-Path $root 'osu!.exe') 'old program fixture';Write-Fixture (Join-Path $root 'runtime/locked.dll') 'old runtime fixture'
    foreach($canonicalName in @('oms-simple.osk','oms-complex.osk')){$path=Join-Path $root "Skins/Canonical/$canonicalName";Write-Fixture $path ('old '+$canonicalName);[IO.File]::SetAttributes($path,([IO.File]::GetAttributes($path) -bor [IO.FileAttributes]::ReadOnly))}
    if($Mode -ne 'nonportable'){Write-Fixture (Join-Path $root 'portable.ini') 'original portable marker'}
    $bootstrap=if($Mode -eq 'nonportable'){Join-Path $proofRoot "$Name-account-storage-fixture"}else{Join-Path $root 'data'}
    $data=if($Mode -eq 'custom'){Join-Path $proofRoot "$Name-custom-data"}else{$bootstrap}
    Write-Fixture (Join-Path $data 'user-skin.bin') 'existing user skin bytes';Write-Fixture (Join-Path $data 'unfinished-operation.bin') 'unknown user operation, keep exactly'
    Write-Fixture (Join-Path $bootstrap 'storage.ini') $(if($Mode -eq 'custom'){"FullPath = $data"}else{'FullPath = '})
    $author=Join-Path $proofRoot "$Name-external-author";Write-Fixture (Join-Path $author 'author-original.bin') 'external author bytes'
    $unknown=Join-Path $root '.oms-update-backup-unknown';Write-Fixture (Join-Path $unknown 'unrecognised.bin') 'unknown interrupted update, never guess ownership'
    return [pscustomobject]@{Root=$root;Mode=$Mode;Data=$data;Bootstrap=$bootstrap;Author=$author;Unknown=$unknown}
}
function Invoke-Update([string]$Source,[string]$Target,[string]$Log){& $updater -UpdateSourceDirectory $Source -TargetDirectory $Target *>&1|Out-File -LiteralPath (Join-Path $proofRoot $Log) -Encoding utf8;if(-not $?){throw '更新工具未成功完成。'}}
function Assert-Installed([string]$Source,$Target){
    $manifest=[IO.File]::ReadAllText((Join-Path $Source 'release-files.json'))|ConvertFrom-Json
    foreach($file in $manifest.Files){if($file.Path -ne 'portable.ini' -and (Sha (Join-Path $Target.Root $file.Path)) -ne $file.Sha256){throw "更新后文件不完整：$($file.Path)"}}
    if((Test-Path -LiteralPath (Join-Path $Target.Root 'portable.ini')) -ne ($Target.Mode -ne 'nonportable')){throw '原运行模式改变。'}
    foreach($name in @('oms-simple.osk','oms-complex.osk')){if(([IO.File]::GetAttributes((Join-Path $Target.Root "Skins/Canonical/$name")) -band [IO.FileAttributes]::ReadOnly) -eq 0){throw '安装原件失去只读保护。'}}
}
$results=[Collections.Generic.List[object]]::new()
$facts=[ordered]@{Format='oms-c7-update-protection-proof.v3';StartedUtc=[DateTime]::UtcNow.ToString('O');FinishedUtc=$null;UpdaterSha256=(Sha $updater);GameStarted=$false;Outcome='Failed';Results=@();Error=$null}
try{
    $source=New-Release 'new-release';$sourceBefore=Snapshot $source
    foreach($mode in @('portable','nonportable','custom')){
        $target=New-Target $mode $mode;$before=@{};$protectedRoots=@($target.Data,$target.Bootstrap,$target.Author,$target.Unknown)|Select-Object -Unique
        foreach($root in $protectedRoots){$before[$root]=Snapshot $root}
        $marker=if($mode -ne 'nonportable'){Sha (Join-Path $target.Root 'portable.ini')}else{$null}
        Invoke-Update $source $target.Root "$mode.log";Assert-Installed $source $target
        foreach($root in $protectedRoots){Assert-Unchanged $before[$root] (Snapshot $root) '用户根、作者或未知现场'}
        if($mode -ne 'nonportable' -and (Sha (Join-Path $target.Root 'portable.ini')) -ne $marker){throw '原便携标记字节改变。'}
        $backup=@(Get-ChildItem -LiteralPath $target.Root -Directory -Force -Filter '.oms-update-backup-*'|Where-Object{$_.FullName -ne $target.Unknown})
        if($backup.Count -ne 1 -or [IO.File]::ReadAllText((Join-Path $backup[0].FullName 'old/osu!.exe')) -ne 'old program fixture'){throw '旧程序未完整保留。'}
        if(([IO.File]::ReadAllText((Join-Path $backup[0].FullName 'update-receipt.json'))|ConvertFrom-Json).State -ne 'Completed'){throw '没有完整完成记录。'}
        foreach($name in @('oms-simple.osk','oms-complex.osk')){$old=Join-Path $backup[0].FullName "old/Skins/Canonical/$name";if([IO.File]::ReadAllText($old) -ne ('old '+$name) -or ([IO.File]::GetAttributes($old) -band [IO.FileAttributes]::ReadOnly) -eq 0){throw '旧原件内容或属性未保留。'}}
        $results.Add([ordered]@{Case=$mode;Passed=$true;Scope='complete replacement and new file; original mode/marker; both readonly originals/backups; actual isolated data/bootstrap/author roots and unknown operation preserved'})
    }
    foreach($damage in @('corrupt','missing','invalid-manifest')){
        $damaged=New-Release "source-$damage";$path=Join-Path $damaged 'Skins/Canonical/oms-simple.osk';[IO.File]::SetAttributes($path,([IO.File]::GetAttributes($path) -band (-bnot [IO.FileAttributes]::ReadOnly)))
        switch($damage){'corrupt'{[IO.File]::AppendAllText($path,'corrupt bytes')};'missing'{[IO.File]::Delete($path)};'invalid-manifest'{[IO.File]::WriteAllText((Join-Path $damaged 'release-files.json'),'{}')}}
        $target=New-Target "rejected-$damage" 'custom';$before=Snapshot $target.Root;$sourceSnapshot=Snapshot $damaged;$rejected=$false
        try{Invoke-Update $damaged $target.Root "$damage.log"}catch{$rejected=$true;Write-Fixture (Join-Path $proofRoot "$damage-error.txt") $_.Exception.Message}
        if(-not $rejected){throw '损坏来源被错误接受。'};Assert-Unchanged $before (Snapshot $target.Root) '拒绝前目标';Assert-Unchanged $sourceSnapshot (Snapshot $damaged) '损坏来源'
        $results.Add([ordered]@{Case="$damage-input";Passed=$true;Scope='rejected before any target write; source unchanged'})
    }
    $target=New-Target 'interrupted' 'custom';$protectedBefore=@{}
    foreach($root in @($target.Data,$target.Bootstrap,$target.Author,$target.Unknown)){$protectedBefore[$root]=Snapshot $root}
    $held=[IO.File]::Open((Join-Path $target.Root 'runtime/locked.dll'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read);$rejected=$false
    try{try{Invoke-Update $source $target.Root 'interrupted-first.log'}catch{$rejected=$true;Write-Fixture (Join-Path $proofRoot 'interrupted-error.txt') $_.Exception.Message}}finally{$held.Dispose()}
    if(-not $rejected){throw '占用导致的实际中途写入失败未出现。'}
    $backups=@(Get-ChildItem -LiteralPath $target.Root -Directory -Force -Filter '.oms-update-backup-*'|Where-Object{$_.FullName -ne $target.Unknown});if($backups.Count -ne 1){throw '中断缺少唯一现场。'}
    $interrupted=$backups[0].FullName;$receipt=[IO.File]::ReadAllText((Join-Path $interrupted 'update-receipt.json'))|ConvertFrom-Json
    if($receipt.State -ne 'Applying' -or (Sha (Join-Path $target.Root 'osu!.exe')) -ne (Sha (Join-Path $source 'osu!.exe')) -or [IO.File]::ReadAllText((Join-Path $target.Root 'runtime/locked.dll')) -ne 'old runtime fixture' -or [IO.File]::ReadAllText((Join-Path $interrupted 'old/osu!.exe')) -ne 'old program fixture'){throw '没有保留真实部分更新现场。'}
    $interruptedBefore=Snapshot $interrupted;Invoke-Update $source $target.Root 'interrupted-retry.log';Assert-Installed $source $target
    Assert-Unchanged $interruptedBefore (Snapshot $interrupted) '原中断现场';foreach($root in $protectedBefore.Keys){Assert-Unchanged $protectedBefore[$root] (Snapshot $root) '中断与重试期间用户及作者数据'}
    $completed=@(Get-ChildItem -LiteralPath $target.Root -Directory -Force -Filter '.oms-update-backup-*'|Where-Object{$_.FullName -ne $target.Unknown -and $_.FullName -ne $interrupted})
    if($completed.Count -ne 1 -or ([IO.File]::ReadAllText((Join-Path $completed[0].FullName 'update-receipt.json'))|ConvertFrom-Json).State -ne 'Completed'){throw '重试没有完成记录。'}
    $results.Add([ordered]@{Case='actual-partial-write-failure-and-retry';Passed=$true;Scope='first program actually replaced, second locked file refused; Applying/old/new evidence retained; shipped updater retry completes without changing original interrupted or unknown evidence; no process-kill claim'})

    foreach($name in @('oms-simple.osk','oms-complex.osk')){
        $target=New-Target ('hardlink-'+$name) 'custom';$canonical=Join-Path $target.Root "Skins/Canonical/$name"
        New-Item -ItemType HardLink -Path (Join-Path $target.Author 'linked-original.osk') -Target $canonical|Out-Null
        $protectedBefore=@{};foreach($root in @($target.Data,$target.Bootstrap,$target.Author,$target.Unknown)){$protectedBefore[$root]=Snapshot $root}
        Invoke-Update $source $target.Root ('hardlink-'+$name+'.log');Assert-Installed $source $target
        foreach($root in $protectedBefore.Keys){Assert-Unchanged $protectedBefore[$root] (Snapshot $root) '硬链接更新期间作者与用户原文件'}
        $backups=@(Get-ChildItem -LiteralPath $target.Root -Directory -Force -Filter '.oms-update-backup-*'|Where-Object{$_.FullName -ne $target.Unknown})
        if($backups.Count -ne 1 -or ([IO.File]::GetAttributes((Join-Path $backups[0].FullName "old/Skins/Canonical/$name")) -band [IO.FileAttributes]::ReadOnly) -eq 0){throw '硬链接旧原件没有保持只读备份。'}
        $results.Add([ordered]@{Case=('preserve-preexisting-hardlink-'+$name);Passed=$true;Scope='real NTFS alias preserved through complete update; old file moved without attribute writes; external author bytes/attributes and user roots unchanged'})
    }

    $line=@(Select-String -LiteralPath $updater -Pattern '^\s*\[IO.File\]::Move\(\$staged, \$file.Target\)')
    if($line.Count -ne 1){throw '找不到唯一的原件新文件移动位置。'}
    $target=New-Target 'canonical-collision' 'custom';$canonical=Join-Path $target.Root 'Skins/Canonical/oms-simple.osk'
    New-Item -ItemType HardLink -Path (Join-Path $target.Author 'linked-original.osk') -Target $canonical|Out-Null
    $global:omsUpdateCollisionTarget=$canonical;$global:omsUpdateCollisionSource=Join-Path $target.Author 'collision-original.osk'
    Write-Fixture $global:omsUpdateCollisionSource 'external collision bytes';[IO.File]::SetAttributes($global:omsUpdateCollisionSource,[IO.FileAttributes]::ReadOnly -bor [IO.FileAttributes]::Archive)
    $protectedBefore=@{};foreach($root in @($target.Data,$target.Bootstrap,$target.Author,$target.Unknown)){$protectedBefore[$root]=Snapshot $root}
    $breakpoint=Set-PSBreakpoint -Script $updater -Line $line[0].LineNumber -Action {
        if($file.Relative -eq 'Skins/Canonical/oms-simple.osk'){
            New-Item -ItemType HardLink -Path $global:omsUpdateCollisionTarget -Target $global:omsUpdateCollisionSource|Out-Null
        }
    }
    $rejected=$false;$reason=$null
    try{try{Invoke-Update $source $target.Root 'canonical-collision.log'}catch{$rejected=$true;$reason=$_.Exception.Message}}finally{Remove-PSBreakpoint -Breakpoint $breakpoint}
    if(-not $rejected -or (Sha $canonical) -ne (Sha $global:omsUpdateCollisionSource)){throw '两次移动间的新目标冲突未被保全。'}
    foreach($root in $protectedBefore.Keys){Assert-Unchanged $protectedBefore[$root] (Snapshot $root) '两次移动冲突期间用户、作者及未知现场'}
    foreach($name in @('osu!.exe','runtime/locked.dll')){if((Sha (Join-Path $target.Root $name)) -ne (Sha (Join-Path $source $name))){throw '未到达前两文件真实已更新的场景。'}}
    $backups=@(Get-ChildItem -LiteralPath $target.Root -Directory -Force -Filter '.oms-update-backup-*'|Where-Object{$_.FullName -ne $target.Unknown})
    if($backups.Count -ne 1 -or ([IO.File]::ReadAllText((Join-Path $backups[0].FullName 'update-receipt.json'))|ConvertFrom-Json).State -ne 'Applying'){throw '移动冲突缺少准确中途更新现场。'}
    if([IO.File]::ReadAllText((Join-Path $backups[0].FullName 'old/osu!.exe')) -ne 'old program fixture' -or [IO.File]::ReadAllText((Join-Path $backups[0].FullName 'old/runtime/locked.dll')) -ne 'old runtime fixture'){throw '前两文件旧字节未保全。'}
    $interrupted=$backups[0].FullName;$interruptedBefore=Snapshot $interrupted
    Invoke-Update $source $target.Root 'canonical-collision-retry.log';Assert-Installed $source $target
    Assert-Unchanged $interruptedBefore (Snapshot $interrupted) '原移动冲突现场';foreach($root in $protectedBefore.Keys){Assert-Unchanged $protectedBefore[$root] (Snapshot $root) '冲突重试期间用户与作者原件'}
    $results.Add([ordered]@{Case='canonical-no-replace-move-collision-and-retry';Passed=$true;Scope='after first two files and old canonical move, debugger adds real author hardlink at target; new move refuses overwrite; same-package retry completes and preserves original Applying scene plus both author aliases';Error=$reason})
    Remove-Variable -Name omsUpdateCollisionSource,omsUpdateCollisionTarget -Scope Global

    $target=New-Target 'canonical-process-interruption' 'custom';$canonical=Join-Path $target.Root 'Skins/Canonical/oms-simple.osk'
    $alias=Join-Path $target.Author 'linked-original.osk';New-Item -ItemType HardLink -Path $alias -Target $canonical|Out-Null
    $protectedBefore=@{};foreach($root in @($target.Data,$target.Bootstrap,$target.Author,$target.Unknown)){$protectedBefore[$root]=Snapshot $root}
    $childScript=Join-Path $proofRoot 'Interrupt-OwnUpdater.ps1';$interception=Join-Path $proofRoot 'canonical-process-interception.json'
    Write-Fixture $childScript @'
param([string]$Updater,[string]$Source,[string]$Target,[string]$Author,[string]$Evidence,[int]$BreakpointLine)
$ErrorActionPreference='Stop'
$global:omsProofAuthor=$Author;$global:omsProofEvidence=$Evidence
Set-PSBreakpoint -Script $Updater -Line $BreakpointLine -Action {
    if($file.Relative -eq 'Skins/Canonical/oms-simple.osk'){
        [IO.File]::WriteAllText($global:omsProofEvidence,([ordered]@{Point='old canonical move completed, new move not started';TargetExists=[IO.File]::Exists($file.Target);OldExists=[IO.File]::Exists($old);StagedExists=[IO.File]::Exists($staged);AuthorAttributes=[string][IO.File]::GetAttributes($global:omsProofAuthor);Utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json),[Text.UTF8Encoding]::new($true))
        [Diagnostics.Process]::GetCurrentProcess().Kill()
    }
}|Out-Null
& $Updater -UpdateSourceDirectory $Source -TargetDirectory $Target
throw 'Expected interruption did not occur'
'@
    $arguments=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',('"'+$childScript+'"'),'-Updater',('"'+$updater+'"'),'-Source',('"'+$source+'"'),'-Target',('"'+$target.Root+'"'),'-Author',('"'+$alias+'"'),'-Evidence',('"'+$interception+'"'),'-BreakpointLine',$line[0].LineNumber)
    $child=Start-Process -FilePath powershell.exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $proofRoot 'canonical-interrupted-stdout.log') -RedirectStandardError (Join-Path $proofRoot 'canonical-interrupted-stderr.log')
    if(-not $child.WaitForExit(15000)){$child.Kill();throw '本次更新子进程没有到达预期中断点。'}
    $point=Get-Content -LiteralPath $interception -Raw -Encoding UTF8|ConvertFrom-Json
    if($point.TargetExists -or -not $point.OldExists -or -not $point.StagedExists -or [IO.File]::Exists($canonical)){throw '没有保留两次移动之间的准确中断状态。'}
    foreach($root in $protectedBefore.Keys){Assert-Unchanged $protectedBefore[$root] (Snapshot $root) '强制中断期间用户与作者原件'}
    $backups=@(Get-ChildItem -LiteralPath $target.Root -Directory -Force -Filter '.oms-update-backup-*'|Where-Object{$_.FullName -ne $target.Unknown})
    if($backups.Count -ne 1 -or ([IO.File]::ReadAllText((Join-Path $backups[0].FullName 'update-receipt.json'))|ConvertFrom-Json).State -ne 'Applying'){throw '强制中断没有保留准确现场。'}
    $interrupted=$backups[0].FullName;$interruptedBefore=Snapshot $interrupted
    Invoke-Update $source $target.Root 'canonical-interrupted-retry.log';Assert-Installed $source $target
    Assert-Unchanged $interruptedBefore (Snapshot $interrupted) '原进程中断现场';foreach($root in $protectedBefore.Keys){Assert-Unchanged $protectedBefore[$root] (Snapshot $root) '中断重试期间用户与作者原件'}
    $results.Add([ordered]@{Case='canonical-two-move-process-interruption-and-retry';Passed=$true;Scope='only own updater process killed at actual between-moves breakpoint; old readonly author hardlink unchanged; target temporarily absent; same-package retry completes and preserves all original interrupted evidence';Interception=$point})
    Assert-Unchanged $sourceBefore (Snapshot $source) '完整新发行来源';$facts.Outcome='Passed'
}catch{$facts.Error=$_.Exception.Message}
finally{$facts.FinishedUtc=[DateTime]::UtcNow.ToString('O');$facts.Results=@($results.ToArray());[IO.File]::WriteAllText((Join-Path $proofRoot 'results.json'),($facts|ConvertTo-Json -Depth 6),$encoding)}
Write-Host "文件保护证据：$(Join-Path $proofRoot 'results.json')"
if($facts.Outcome -ne 'Passed'){Write-Host $facts.Error;exit 1}
Write-Host '覆盖、中途写入失败和重试的文件保护已核对；未启动游戏，不替代真实发行物和设备体验。'
