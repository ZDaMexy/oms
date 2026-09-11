param(
    [Parameter(Mandatory = $true)][string]$ActualCoreTrx,
    [Parameter(Mandatory = $true)][string]$ActualManiaTrx,
    [switch]$ResolvedCoreSampleFixture,
    [Parameter(Mandatory = $true)][string]$OutputFile
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Read-Failures([string]$Path) {
    [xml]$document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $failures = @($document.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object {
        $message = ([string]$_.Output.ErrorInfo.Message).Replace("`r`n", "`n").Trim()
        [pscustomobject]@{ Name = [string]$_.testName; Message = $message; Category = ($message -split "`n")[0] }
    } | Sort-Object Name)
    return ,$failures
}
$baselinePath = Join-Path $PSScriptRoot 'baselines/c6-failures.json'
$baselineDocument = Get-Content -LiteralPath $baselinePath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($baselineDocument.Format -cne 'oms-skin-c6-failure-baseline.v1' -or
    $baselineDocument.SourceCommit -cne 'de9eb3e39e84c2d0dffe49f313a37eae61149b80') { throw '冻结基线格式或原始提交不符。' }
$results = @()
foreach ($case in @(@{ Name = 'core'; Actual = $ActualCoreTrx; Expected = 6 }, @{ Name = 'mania'; Actual = $ActualManiaTrx; Expected = 4 })) {
    $source = @($baselineDocument.Suites | Where-Object { $_.Suite -ceq $case.Name })
    if ($source.Count -ne 1) { throw "冻结基线必须有唯一套件：$($case.Name)" }
    $baseline = @($source[0].Failures | ForEach-Object {
        $message = ([string]$_.Message).Replace("`r`n", "`n").Trim()
        if (([string]$_.Category) -cne ($message -split "`n")[0]) { throw '冻结基线类别与原完整消息不符。' }
        [pscustomobject]@{ Name = [string]$_.Name; Category = [string]$_.Category; Message = $message }
    } | Sort-Object Name)
    $actual = Read-Failures $case.Actual
    if ($baseline.Count -ne $case.Expected) { throw "冻结基线不完整：$($case.Name)" }
    $comparedBaseline = $baseline
    $resolved = @()
    $resolvedPassed = $true
    if ($ResolvedCoreSampleFixture -and $case.Name -eq 'core') {
        $resolvedName = 'TestSampleUpdatedBeforePlaybackWhenNotPresent'
        $oldFailure = @($baseline | Where-Object { $_.Name -ceq $resolvedName })
        if ($oldFailure.Count -ne 1) { throw '冻结基线必须包含唯一的原声音夹具失败身份。' }
        [xml]$actualDocument = Get-Content -LiteralPath $case.Actual -Raw -Encoding UTF8
        $actualResults = @($actualDocument.TestRun.Results.UnitTestResult | Where-Object { ([string]$_.testName) -ceq $resolvedName })
        $resolvedPassed = $actualResults.Count -eq 1 -and ([string]$actualResults[0].outcome) -ceq 'Passed'
        $resolved = @([pscustomobject]@{
            BaselineFailure = $oldFailure[0]
            RequiredActualOutcome = 'Passed'
            ActualOutcomes = @($actualResults | ForEach-Object { [string]$_.outcome })
            ActualPassed = $resolvedPassed
            Reason = '原暂停/换源夹具改用明确原始声音；所有原断言保持不变。'
        })
        $comparedBaseline = @($baseline | Where-Object { $_.Name -cne $resolvedName })
    }
    $matches = $resolvedPassed -and $actual.Count -eq $comparedBaseline.Count
    foreach ($failure in $comparedBaseline) {
        $matching = @($actual | Where-Object { $_.Name -ceq $failure.Name -and $_.Category -ceq $failure.Category -and $_.Message -ceq $failure.Message })
        if ($matching.Count -ne 1) { $matches = $false }
    }
    $results += [pscustomobject]@{
        Suite = $case.Name; ExactMatch = $matches; Actual = $actual; Baseline = $baseline; ComparedBaseline = $comparedBaseline; Resolved = $resolved
        BaselineSource = [pscustomobject]@{
            File = 'baselines/c6-failures.json'; SourceCommit = $baselineDocument.SourceCommit
            SourceTrx = $source[0].SourceTrx; SourceTrxSha256 = $source[0].SourceTrxSha256
            StartedUtc = $source[0].StartedUtc; FinishedUtc = $source[0].FinishedUtc; Command = $source[0].Command
        }
    }
}
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile), ($results | ConvertTo-Json -Depth 7), [Text.UTF8Encoding]::new($true))
if (@($results | Where-Object { -not $_.ExactMatch }).Count -ne 0) { throw '本轮失败与冻结基线不同，请逐项调查；不能按失败数量归因。' }
Write-Host '本轮剩余失败名称、类别及完整消息与冻结基线逐项一致；显式关闭项均有本轮 Passed 证据。'
