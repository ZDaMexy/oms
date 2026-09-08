[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$rootPrefix = $root + [System.IO.Path]::DirectorySeparatorChar
$failures = New-Object 'System.Collections.Generic.List[string]'
$warnings = New-Object 'System.Collections.Generic.List[string]'

function get-relative-path([string] $path)
{
    $fullPath = [System.IO.Path]::GetFullPath($path)

    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Path is outside the repository root: $fullPath"
    }

    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function get-line-number([string] $text, [int] $index)
{
    if ($index -le 0)
    {
        return 1
    }

    return ([regex]::Matches($text.Substring(0, $index), "`n").Count + 1)
}

Push-Location $root

try
{
    $markdownPaths = @(& rg --files --hidden -g '*.md' -g '!.git/**')

    if ($LASTEXITCODE -ne 0 -or $markdownPaths.Count -eq 0)
    {
        throw 'rg 未能枚举 Markdown 文件。'
    }

    $markdownFiles = @($markdownPaths | ForEach-Object { Join-Path $root $_ })
    $linkPattern = [regex]'!?\[[^\]]*\]\((?<target>[^)]+)\)'
    $wikiPattern = [regex]'\[\[(?<target>[^\]|#]+)(?:#[^\]|]+)?(?:\|[^\]]+)?\]\]'
    $relativeLinkCount = 0
    $wikiLinkCount = 0

    foreach ($file in $markdownFiles)
    {
        $text = [System.IO.File]::ReadAllText($file)
        $fileRelative = get-relative-path $file
        $parent = Split-Path -Parent $file

        foreach ($match in $linkPattern.Matches($text))
        {
            $rawTarget = $match.Groups['target'].Value.Trim()

            if ($rawTarget.StartsWith('<'))
            {
                $closing = $rawTarget.IndexOf('>')

                if ($closing -lt 2)
                {
                    $failures.Add("$($fileRelative):$((get-line-number $text $match.Index)) 非法尖括号链接：$rawTarget")
                    continue
                }

                $target = $rawTarget.Substring(1, $closing - 1)
            }
            else
            {
                # 含空格的本地路径必须按仓库约定使用 <...>；裸目标的空格后内容视作 title。
                $target = $rawTarget.Split(' ', 2)[0]
            }

            if ([string]::IsNullOrWhiteSpace($target) -or
                $target.StartsWith('#') -or
                $target.StartsWith('//') -or
                $target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:')
            {
                continue
            }

            $pathPart = ($target -split '[#?]', 2)[0]

            if ([string]::IsNullOrWhiteSpace($pathPart))
            {
                continue
            }

            try
            {
                $pathPart = [Uri]::UnescapeDataString($pathPart).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
                if ($pathPart.StartsWith([System.IO.Path]::DirectorySeparatorChar))
                {
                    $failures.Add("$($fileRelative):$((get-line-number $text $match.Index)) 使用仓库根链接：$target；请改为相对当前文件的标准链接")
                    continue
                }

                $candidate = Join-Path $parent $pathPart

                $candidate = [System.IO.Path]::GetFullPath($candidate)
            }
            catch
            {
                $failures.Add("$($fileRelative):$((get-line-number $text $match.Index)) 无法解析链接：$target")
                continue
            }

            $relativeLinkCount++

            if (-not (Test-Path -LiteralPath $candidate))
            {
                $failures.Add("$($fileRelative):$((get-line-number $text $match.Index)) 断链：$target")
            }
        }

        if ($file.StartsWith((Join-Path $root '.Codex\memory'), [StringComparison]::OrdinalIgnoreCase))
        {
            foreach ($match in $wikiPattern.Matches($text))
            {
                $target = $match.Groups['target'].Value.Trim()

                if ([string]::IsNullOrWhiteSpace($target))
                {
                    continue
                }

                $wikiLinkCount++
                $targetPath = if ([System.IO.Path]::HasExtension($target)) { $target } else { "$target.md" }
                $candidate = [System.IO.Path]::GetFullPath((Join-Path $parent $targetPath))

                if (-not (Test-Path -LiteralPath $candidate))
                {
                    $failures.Add("$($fileRelative):$((get-line-number $text $match.Index)) 断开的 memory wiki 链：$target")
                }
            }
        }

        if ($text -match '</?(?:content|invoke)>')
        {
            $failures.Add("$fileRelative 含工具封装残片 </content> 或 </invoke>")
        }

        if ([System.IO.Path]::GetFileName($file) -ne 'CHANGELOG.md' -and
            $text -match '下一新对话|实现暂停于|next conversation|next session|implementation (?:is )?paused|paused at')
        {
            $failures.Add("$fileRelative 含会话级临时交接；完成后应改成稳定的下一 gate，历史只留 CHANGELOG")
        }

        if ($text -match 'originSessionId\s*:')
        {
            $failures.Add("$fileRelative 含不应入库的 originSessionId")
        }

        if ($text -match '(?i)(?:[a-z]:\\users\\[^\\\r\n]+\\|/home/[^/\s]+/)')
        {
            $failures.Add("$fileRelative 含可识别个人/机器的 home 绝对路径；请改为脱敏 authority 或占位符")
        }

        foreach ($match in [regex]::Matches($text, '(?i)\b[0-9a-f]{64}\b'))
        {
            $lineStart = $text.LastIndexOf("`n", [Math]::Max(0, $match.Index - 1)) + 1
            $lineEnd = $text.IndexOf("`n", $match.Index)

            if ($lineEnd -lt 0)
            {
                $lineEnd = $text.Length
            }

            $line = $text.Substring($lineStart, $lineEnd - $lineStart)

            if ($line -match '(?i)(?:client\.realm|\brealm\b|schema\s*56|生产(?:数据|文件)?|production\s+(?:data|realm)|用户数据(?:库|文件)?)')
            {
                $failures.Add("$($fileRelative):$((get-line-number $text $match.Index)) 含生产/用户数据精确指纹；请只保留一致性结论与仓库外证据 authority")
            }
            else
            {
                $warnings.Add("$($fileRelative):$((get-line-number $text $match.Index)) 含 64 位指纹；若是公开制品 checksum 可保留，否则请脱敏")
            }
        }

        foreach ($match in [regex]::Matches($text, '(?i)\b[a-z]:\\[^\r\n`]+'))
        {
            if ($match.Value -notmatch '(?i)^[a-z]:\\users\\')
            {
                $warnings.Add("$($fileRelative):$((get-line-number $text $match.Index)) 含绝对路径示例；请确认它是公开/通用示例而非本机取证值")
            }
        }

        $lineNumber = 0

        foreach ($line in [System.IO.File]::ReadAllLines($file))
        {
            $lineNumber++

            if ($line -match '(?i)(?:client\.realm|\brealm\b|schema\s*56|生产(?:数据|文件)?|production\s+(?:data|realm)|用户数据(?:库|文件)?)' -and
                $line -match '(?i)(?:\b\d{4}-\d{2}-\d{2}T[^\s`]+|\b[\d,]{4,}\s*bytes?\b)')
            {
                $failures.Add("$fileRelative`:$lineNumber 含生产/用户数据精确 mtime 或 byte size；请只保留一致性结论与仓库外证据 authority")
            }
        }
    }

    $requiredSublineFiles = @('DEVELOPMENT_PLAN.md', 'DEVELOPMENT_STATUS.md', 'CHANGELOG.md', 'TECHNICAL_CONSTRAINTS.md')

    foreach ($subline in Get-ChildItem (Join-Path $root 'doc_md\subline') -Directory | Where-Object Name -Like 'P1-*')
    {
        foreach ($required in $requiredSublineFiles)
        {
            if (-not (Test-Path -LiteralPath (Join-Path $subline.FullName $required)))
            {
                $failures.Add("doc_md/subline/$($subline.Name) 缺少 $required")
            }
        }
    }

    $miniIndexPath = Join-Path $root 'doc_md\mini\README.md'
    $miniIndexText = [System.IO.File]::ReadAllText($miniIndexPath)

    foreach ($mini in Get-ChildItem (Join-Path $root 'doc_md\mini') -Directory)
    {
        foreach ($required in $requiredSublineFiles)
        {
            if (-not (Test-Path -LiteralPath (Join-Path $mini.FullName $required)))
            {
                $failures.Add("doc_md/mini/$($mini.Name) 缺少 $required")
            }
        }

        if ($miniIndexText -notmatch "\($([regex]::Escape($mini.Name))/")
        {
            $failures.Add("doc_md/mini/README.md 未索引 $($mini.Name)")
        }
    }

    $memoryIndexPath = Join-Path $root '.Codex\memory\MEMORY.md'
    $memoryIndexText = [System.IO.File]::ReadAllText($memoryIndexPath)

    foreach ($memoryLeaf in Get-ChildItem (Join-Path $root '.Codex\memory') -File -Filter '*.md' | Where-Object Name -ne 'MEMORY.md')
    {
        if ($memoryIndexText -notmatch "\($([regex]::Escape($memoryLeaf.Name))\)")
        {
            $failures.Add(".Codex/memory/MEMORY.md 未索引 $($memoryLeaf.Name)")
        }
    }

    $otherIndexPath = Join-Path $root 'doc_md\other\README.md'
    $otherIndexText = [System.IO.File]::ReadAllText($otherIndexPath)

    foreach ($otherDocument in Get-ChildItem (Join-Path $root 'doc_md\other') -File -Filter '*.md' | Where-Object Name -ne 'README.md')
    {
        if ($otherIndexText -notmatch "\($([regex]::Escape($otherDocument.Name))\)")
        {
            $failures.Add("doc_md/other/README.md 未索引 $($otherDocument.Name)")
        }
    }

    $statusFiles = @(
        (Join-Path $root 'doc_md\mainline\DEVELOPMENT_STATUS.md')
        Get-ChildItem (Join-Path $root 'doc_md\subline') -Recurse -File -Filter 'DEVELOPMENT_STATUS.md' | ForEach-Object FullName
        Get-ChildItem (Join-Path $root 'doc_md\mini') -Recurse -File -Filter 'DEVELOPMENT_STATUS.md' | ForEach-Object FullName
    )

    foreach ($statusFile in $statusFiles)
    {
        $statusLines = [System.IO.File]::ReadAllLines($statusFile)
        $lineCount = $statusLines.Count

        if ($lineCount -gt 120)
        {
            $failures.Add("$(get-relative-path $statusFile) 有 $lineCount 行，超过 STATUS 120 行预算")
        }

        # 只检查约定的快照结构，不从日期、测试数字或其它标题猜测验证语义。
        $validationSectionCounts = @{ '最近一次验证' = 0; '文档治理验证' = 0 }
        $validationSection = $null
        $fenceCharacter = $null
        $fenceLength = 0
        $lineNumber = 0

        foreach ($line in $statusLines)
        {
            $lineNumber++
            $fence = [regex]::Match($line, '^ {0,3}(?<fence>`{3,}|~{3,})(?<info>.*)$')

            if ($null -ne $fenceCharacter)
            {
                # 关闭 fence 必须同字符、长度不少于 opening fence，且后面只有空白。
                if ($fence.Success -and $fence.Groups['fence'].Value[0] -eq $fenceCharacter -and
                    $fence.Groups['fence'].Value.Length -ge $fenceLength -and
                    [string]::IsNullOrWhiteSpace($fence.Groups['info'].Value))
                {
                    $fenceCharacter = $null
                }

                continue
            }

            if ($fence.Success -and ($fence.Groups['fence'].Value[0] -ne '`' -or -not $fence.Groups['info'].Value.Contains('`')))
            {
                $fenceCharacter = $fence.Groups['fence'].Value[0]
                $fenceLength = $fence.Groups['fence'].Value.Length
                continue
            }

            $heading = [regex]::Match($line, '^(?<level>#{1,6})[ \t]+(?<title>.+)')

            if (-not $heading.Success)
            {
                continue
            }

            $level = $heading.Groups['level'].Value.Length
            $title = $heading.Groups['title'].Value.Trim()

            if ($level -le 2)
            {
                $validationSection = $null

                if ($level -eq 2 -and $validationSectionCounts.ContainsKey($title))
                {
                    $validationSectionCounts[$title]++
                    $validationSection = $title

                    if ($validationSectionCounts[$title] -gt 1)
                    {
                        $failures.Add("$(get-relative-path $statusFile):$lineNumber 重复的「$title」章节；每种验证快照最多保留一个")
                    }
                }
            }
            elseif ($null -ne $validationSection)
            {
                $failures.Add("$(get-relative-path $statusFile):$lineNumber 「$validationSection」内不设下级标题；同次测试面用列表或表格，旧轮次移入 CHANGELOG")
            }
        }
    }

    $planFiles = @(
        (Join-Path $root 'doc_md\mainline\DEVELOPMENT_PLAN.md')
        Get-ChildItem (Join-Path $root 'doc_md\subline') -Recurse -File -Filter 'DEVELOPMENT_PLAN.md' | ForEach-Object FullName
        Get-ChildItem (Join-Path $root 'doc_md\mini') -Recurse -File -Filter 'DEVELOPMENT_PLAN.md' | ForEach-Object FullName
    )

    foreach ($planFile in $planFiles)
    {
        $planText = [System.IO.File]::ReadAllText($planFile)
        $lineCount = [System.IO.File]::ReadAllLines($planFile).Count

        if ($lineCount -gt 180)
        {
            $warnings.Add("$(get-relative-path $planFile) 有 $lineCount 行；建议继续把完成史归回 CHANGELOG")
        }

        if ($planText -match '下一新对话|下一轮|本轮|暂停于|next conversation|next session|implementation (?:is )?paused|paused at' -or
            $planText -match '(?:commit|提交|实现停在|暂停于|锚点)\s*(?:[:=：]|at|is)?\s*[0-9a-f]{7,40}\b')
        {
            $failures.Add("$(get-relative-path $planFile) 含提交锚点或会话级交接语；PLAN 只应保留未来动作/依赖/验收")
        }
    }

    foreach ($documentFile in @($statusFiles) + @($planFiles))
    {
        $documentText = [System.IO.File]::ReadAllText($documentFile)
        $isStatus = [System.IO.Path]::GetFileName($documentFile) -eq 'DEVELOPMENT_STATUS.md'
        $documentKind = if ($isStatus) { 'STATUS' } else { 'PLAN' }
        $characterBudget = if ($isStatus) { 6000 } else { 10000 }

        if ($documentText.Length -gt $characterBudget)
        {
            $warnings.Add("$(get-relative-path $documentFile) 有 $($documentText.Length) 字符，超过 $documentKind $characterBudget 字符建议预算；请检查是否有历史或重复合同")
        }

        $lineNumber = 0

        foreach ($line in [System.IO.File]::ReadAllLines($documentFile))
        {
            $lineNumber++

            if ($line.Length -gt 800)
            {
                $warnings.Add("$(get-relative-path $documentFile):$lineNumber 有 $($line.Length) 字符，超过 $documentKind 单行 800 字符建议预算；请检查段落或表格是否需要精简")
            }
        }
    }

    foreach ($readme in Get-ChildItem (Join-Path $root 'doc_md') -Recurse -File -Filter 'README.md')
    {
        $lineCount = [System.IO.File]::ReadAllLines($readme.FullName).Count

        if ($lineCount -gt 80)
        {
            $failures.Add("$(get-relative-path $readme.FullName) 有 $lineCount 行，超过治理 README 80 行预算")
        }
    }

    foreach ($memory in Get-ChildItem (Join-Path $root '.Codex\memory') -File -Filter '*.md')
    {
        $lineNumber = 0

        foreach ($line in [System.IO.File]::ReadAllLines($memory.FullName))
        {
            $lineNumber++

            if ($line.Length -gt 800)
            {
                $failures.Add("$(get-relative-path $memory.FullName):$lineNumber 有 $($line.Length) 字符，超过 memory 单行 800 字符预算")
            }
        }
    }

    foreach ($warning in $warnings)
    {
        Write-Warning $warning
    }

    if ($failures.Count -gt 0)
    {
        Write-Host "文档健康检查失败（$($failures.Count) 项）：" -ForegroundColor Red
        $failures | Sort-Object -Unique | ForEach-Object { Write-Host "- $_" -ForegroundColor Red }
        exit 1
    }

    Write-Host "文档健康检查通过：$($markdownFiles.Count) 个 Markdown，$relativeLinkCount 个相对链接，$wikiLinkCount 个 memory wiki 链。" -ForegroundColor Green
}
finally
{
    Pop-Location
}
