param(
    [ValidateSet('new', 'generate', 'check', 'pack', 'import', 'update')][string]$Action = 'check',
    [string]$Source,
    [string]$Output,
    [string]$Name = 'My OMS Skin',
    [string]$GameExecutable
)
$ErrorActionPreference = 'Stop'
if (-not $Source) { $Source = Join-Path $PSScriptRoot 'sources/oms-simple' }
$tool = Join-Path $PSScriptRoot 'bin/SkinAuthoring.exe'
if (-not (Test-Path -LiteralPath $tool)) {
    $tool = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe'))
}
if (-not (Test-Path -LiteralPath $tool)) {
    throw '缺少作者工具。请使用完整制作套件，或先按 README 在仓库编译一次工具。'
}
$sourcePath = [IO.Path]::GetFullPath($Source)
switch ($Action) {
    'new' {
        if (-not $Output) { throw 'new 需要 -Output 指定尚不存在的新作品目录。' }
        & $tool new $sourcePath ([IO.Path]::GetFullPath($Output)) $Name
    }
    'generate' { & $tool generate $sourcePath }
    'check' { & $tool check $sourcePath }
    'pack' {
        if (-not $Output) { $Output = Join-Path $PSScriptRoot ('dist/' + (Split-Path $sourcePath -Leaf) + '.osk') }
        & $tool pack $sourcePath ([IO.Path]::GetFullPath($Output))
    }
    { $_ -in 'import', 'update' } {
        if (-not $Output) { $Output = Join-Path $PSScriptRoot ('dist/' + (Split-Path $sourcePath -Leaf) + '.osk') }
        $package = [IO.Path]::GetFullPath($Output)
        & $tool pack $sourcePath $package
        if ($LASTEXITCODE -ne 0) { throw '检查或打包失败，未启动导入。' }
        # The ordinary game importer may consume its input. Preserve the author's finished package and source.
        $importRoot = Join-Path $PSScriptRoot ('work/import-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $importRoot | Out-Null
        $importCopy = Join-Path $importRoot ([IO.Path]::GetFileName($package))
        [IO.File]::Copy($package, $importCopy, $false)
        [IO.File]::SetAttributes($importCopy, ([IO.File]::GetAttributes($importCopy) -band (-bnot [IO.FileAttributes]::ReadOnly)))
        if (-not $GameExecutable -or -not (Test-Path -LiteralPath $GameExecutable)) {
            Write-Output "导入副本已准备：$importCopy"
            Write-Output '把该 .osk 拖入 OMS 窗口导入；更新包导入后在皮肤设置选中新版本。旧版本保留供对照。'
            break
        }
        $executable = [IO.Path]::GetFullPath($GameExecutable)
        Start-Process -FilePath $executable -ArgumentList ('"' + $importCopy + '"') -WindowStyle Hidden
        Write-Output '已通过普通 .osk 导入入口交给 OMS；请在皮肤设置中选择新作品。'
    }
}
if ($LASTEXITCODE -ne 0) { throw '作者工具检查未通过。请按上方文件名、行号或错误编号修正后再运行。' }
