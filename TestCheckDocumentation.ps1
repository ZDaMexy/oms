[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 仅复制 checker 和最小 Markdown fixture，不读写真实文档或产品输出。
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('oms-doc-check-' + [guid]::NewGuid().ToString('N'))
$utf8 = New-Object System.Text.UTF8Encoding($false)
$defaultStatus = "# 状态`n`n## 最近一次验证`n`n审查完成。`n"
$defaultTarget = "# 目标`n`n## 保留合同`n"
$defaultReadme = "# 入口`n`n[合同](target.md#保留合同)`n"
$cases = @(
    @{ Name = 'cross-file CJK'; Readme = $defaultReadme },
    @{ Name = 'same-page'; Readme = "# 入口`n`n[入口](#入口)" },
    @{ Name = 'code and punctuation'; Target = '### 5.4 小节线 / 颜色 `[部分]`'; Readme = '[颜色](target.md#54-小节线--颜色-部分)' },
    @{ Name = 'code underscore'; Target = '## `Load_Chart`'; Readme = '[code](target.md#load_chart)' },
    @{ Name = 'code boundary spaces'; Target = '## ` Load_Chart `'; Readme = '[code](target.md#load_chart)' },
    @{ Name = 'CJK arrow'; Target = '## BMS→mania 音频合同'; Readme = '[声音](target.md#bmsmania-音频合同)' },
    @{ Name = 'encoded path and fragment'; Readme = '[合同](target%20name.md#%E4%BF%9D%E7%95%99%E5%90%88%E5%90%8C)' },
    @{ Name = 'angle path and title'; Readme = '[合同](<target name.md#保留合同> "title")' },
    @{ Name = 'duplicate heading collisions'; Target = "## Repeat`n## Repeat`n## Repeat-1`n## Repeat"; Readme = '[1](target.md#repeat) [2](target.md#repeat-1) [3](target.md#repeat-1-1) [4](target.md#repeat-2)' },
    @{ Name = 'explicit anchors do not number headings'; Target = '<a name="Repeat"></a><a id="repeat"></a><a name=''custom-point''></a><a id=plain-point></a>' + "`n## Repeat`n## Repeat"; Readme = '[case](target.md#Repeat) [heading](target.md#repeat-1) [name](target.md#custom-point) [id](target.md#plain-point)' },
    @{ Name = 'ATX indent and closing hashes'; Target = '   ### 保留合同 ###'; Readme = '[合同](target.md#保留合同)' },
    @{ Name = 'fenced and inline source examples'; Readme = (@('# 入口', '```markdown', '[example](#absent)', '```', '~~~md', '[example](#absent)', '~~~', '`[example](#absent)`', '[入口](#入口)') -join "`n") },
    @{ Name = 'fence length and character'; Target = (@('````powershell', '### 假标题', '```', '~~~', '### 仍是假标题', '`````', '## 保留合同') -join "`n") },
    @{ Name = 'tilde fence longer close'; Target = "~~~ code`n## 假标题`n~~~~`n## 保留合同" },
    @{ Name = 'invalid backtick info is not a fence'; Target = '``` bad`info' + "`n## 保留合同" },
    @{ Name = 'HTML anchor in inline code is not an anchor'; Target = '`<a name="fake"></a>`'; Readme = '[fake](target.md#fake)'; Exit = 1; Contains = '断开的 Markdown 锚点' },
    @{ Name = 'fenced heading is not an anchor'; Target = (@('```md', '## fake', '```') -join "`n"); Readme = '[fake](target.md#fake)'; Exit = 1; Contains = '断开的 Markdown 锚点' },
    @{ Name = 'wrong fence character stays open'; Target = '```md' + "`n~~~`n## fake"; Readme = '[fake](target.md#fake)'; Exit = 1; Contains = '断开的 Markdown 锚点' },
    @{ Name = 'short fence stays open'; Target = (@('````md', '```', '## fake') -join "`n"); Readme = '[fake](target.md#fake)'; Exit = 1; Contains = '断开的 Markdown 锚点' },
    @{ Name = 'indented fence close stays open'; Target = (@('```md', '    ```', '## fake') -join "`n"); Readme = '[fake](target.md#fake)'; Exit = 1; Contains = '断开的 Markdown 锚点' },
    @{ Name = 'cross-file dead anchor and line number'; Readme = "# 入口`n`n[坏链](target.md#不存在)"; Exit = 1; Contains = 'README.md:3 断开的 Markdown 锚点' },
    @{ Name = 'same-page dead anchor'; Readme = "# 入口`n`n[坏链](#不存在)"; Exit = 1; Contains = 'README.md:3 断开的 Markdown 锚点' },
    @{ Name = 'missing duplicate suffix'; Target = "## Repeat`n## Repeat"; Readme = '[bad](target.md#repeat-2)'; Exit = 1; Contains = '断开的 Markdown 锚点' },
    @{ Name = 'original missing-file check'; Readme = '[bad](missing.md#entry)'; Exit = 1; Contains = '断链：missing.md#entry' },
    @{ Name = 'original root-link rejection'; Readme = '[bad](/target.md#保留合同)'; Exit = 1; Contains = '使用仓库根链接' },
    @{ Name = 'non-Markdown fragments and remote links'; Readme = '[asset](asset.txt#unknown) [web](https://example.org/page#unknown)' },
    @{ Name = 'unsupported Setext warns'; Target = "Setext`n======"; Readme = '[manual](target.md#setext)'; Contains = '无法确认锚点' },
    @{ Name = 'unsupported inline link warns'; Target = '# [label](asset.txt)'; Readme = '[manual](target.md#label)'; Contains = '无法确认锚点' },
    @{ Name = 'unsupported Unicode warns'; Target = '# Δ'; Readme = '[manual](target.md#δ)'; Contains = '无法确认锚点' },
    @{ Name = 'validation matrix and documentation gate'; Status = "# 状态`n## 最近一次验证`n| 5K | 7K |`n| --- | --- |`n| 3/5 | 7/9 |`n## 文档治理验证`n文档检查通过。" },
    @{ Name = 'duplicate product validation rejected'; Status = "# 状态`n## 最近一次验证`n旧结果`n## 最近一次验证`n新结果"; Exit = 1; Contains = '重复的「最近一次验证」' },
    @{ Name = 'nested validation rejected'; Status = "# 状态`n## 最近一次验证`n### 第二次测试"; Exit = 1; Contains = '内不设下级标题' },
    @{ Name = 'validation headings in fences ignored'; Status = (@('# 状态', '## 最近一次验证', '```powershell', '### Run focused validation', '## 最近一次验证', '```', '~~~', '### Another example', '~~~') -join "`n") },
    @{ Name = 'validation heading after fence rejected'; Status = (@('# 状态', '## 最近一次验证', '````powershell', '### Example', '```', '~~~', '`````', '### 真实子标题') -join "`n"); Exit = 1; Contains = 'DEVELOPMENT_STATUS.md:8 「最近一次验证」内不设下级标题' },
    @{ Name = 'public checksum warning retained'; Target = "# 目标`n## 保留合同`nPublic checksum: " + ('a' * 64); Contains = '含 64 位指纹' }
)

try
{
    foreach ($directory in @('doc_md/mainline', 'doc_md/subline', 'doc_md/mini', 'doc_md/other', '.Codex/memory'))
    {
        New-Item -ItemType Directory -Path (Join-Path $fixtureRoot $directory) -Force | Out-Null
    }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CheckDocumentation.ps1') -Destination (Join-Path $fixtureRoot 'CheckDocumentation.ps1')

    $files = @{
        'doc_md/mainline/DEVELOPMENT_PLAN.md' = '# 计划';
        'doc_md/mini/README.md' = '# mini';
        'doc_md/other/README.md' = '# other';
        '.Codex/memory/MEMORY.md' = '# memory';
        'target name.md' = $defaultTarget;
        'asset.txt' = 'asset';
    }

    foreach ($entry in $files.GetEnumerator())
    {
        [System.IO.File]::WriteAllText((Join-Path $fixtureRoot $entry.Key), $entry.Value, $utf8)
    }

    foreach ($case in $cases)
    {
        $readme = if ($case.ContainsKey('Readme')) { $case.Readme } else { $defaultReadme }
        $target = if ($case.ContainsKey('Target')) { $case.Target } else { $defaultTarget }
        $status = if ($case.ContainsKey('Status')) { $case.Status } else { $defaultStatus }
        [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'README.md'), $readme, $utf8)
        [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'target.md'), $target, $utf8)
        [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'doc_md/mainline/DEVELOPMENT_STATUS.md'), $status, $utf8)

        $expectedExit = if ($case.ContainsKey('Exit')) { $case.Exit } else { 0 }
        $output = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixtureRoot 'CheckDocumentation.ps1') 2>&1) -join "`n"

        if ($LASTEXITCODE -ne $expectedExit -or ($case.ContainsKey('Contains') -and -not $output.Contains($case.Contains)))
        {
            throw "Fixture '$($case.Name)' failed (exit $LASTEXITCODE, expected $expectedExit):`n$output"
        }

        if (-not $case.ContainsKey('Contains') -and $output.Contains('无法确认锚点'))
        {
            throw "Fixture '$($case.Name)' passed through an unsupported-syntax warning:`n$output"
        }

        Write-Host "PASS $($case.Name)"
    }

    Write-Host "文档 checker fixtures 通过：$($cases.Count)/$($cases.Count)。"
}
finally
{
    # 仅清理本次随机生成且确定位于临时目录内的 fixture。
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
    $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedFixture.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($resolvedFixture) -notlike 'oms-doc-check-*')
    {
        throw 'Fixture 清理路径不在预期临时目录内。'
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
