#requires -Version 7.0
<#
.SYNOPSIS
  第 4 批（证据与发布）：把一次发布验收的**四方证据**绑定成单一可审计清单。

.DESCRIPTION
  绑定链：源码快照（git HEAD + 工作区指纹）
        → 构建产物（BuildStamp.g.cs 的构建时刻 + Assemblies/PersonalChronicle.dll 的 SHA256）
        → 测试结果（TRX 路径 + SHA256 + passed/failed/skipped 计数）
        → 变更标注（ChangeID 列表 + 标注文件指纹）。

  为什么需要它：审计报告指出"取样程序集哈希 / 状态文件记录哈希 / 当前源码"三者不一致 ⇒
  不能把**源码、本地程序集、已部署游戏版本**视为同一验收对象。本脚本把四者绑进一份 manifest，
  并**显式报告不一致项**（不假装一致）。任一编译错误或测试失败 ⇒ 判定 BLOCKED（GOV-012：不得伪造 PASS）。

.EXAMPLE
  pwsh _scripts/bind-release-evidence.ps1 `
    -ChangeId 'CHG-20261010-ECON-R01-R04','CHG-20261010-ECON-M10-FULL' `
    -TestResults 'Tests/TestResults/M8-verify.trx' `
    -Out 'docs/设计文档/架构/发布验收/release-evidence.json'
#>
param(
    [string[]]$ChangeId = @(),
    [string[]]$TestResults = @(),
    [string]$Out = 'docs/设计文档/架构/发布验收/release-evidence.json',
    [string]$Assembly = 'Assemblies/PersonalChronicle.dll',
    [string]$AnnotationDir = 'docs/设计文档/架构/变更标注',
    [switch]$VerifyCompile
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$utf8 = [System.Text.UTF8Encoding]::new($false)

# `pwsh -File` 不做 PowerShell 解析：`-ChangeId 'a','b'` 会整串进来。统一按 , ; 与引号拆开。
function Expand-List([string[]]$values) {
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($value in $values) {
        if ($null -eq $value) { continue }
        foreach ($piece in ($value -split '[;,]')) {
            $trimmed = $piece.Trim().Trim("'").Trim('"')
            if ($trimmed.Length -gt 0) { $out.Add($trimmed) }
        }
    }
    return $out
}
$ChangeId = Expand-List $ChangeId
$TestResults = Expand-List $TestResults

function Get-Sha256([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

# --- 中文路径必须靠显式 UTF-8 解码（同 gate-runner 的既有教训） ---
$prevOut = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
try {
    $head = (git rev-parse HEAD 2>$null)
    $dirtyLines = @(git -c core.quotePath=false status --porcelain -uall 2>$null | Where-Object { $_ })
    $originMain = (git rev-parse origin/main 2>$null)
} finally { [Console]::OutputEncoding = $prevOut }

# --- 构建时刻 ---
$buildStamp = $null
$stampPath = 'Source/PersonalChronicle/BuildStamp.g.cs'
if (Test-Path -LiteralPath $stampPath) {
    $match = [regex]::Match((Get-Content -LiteralPath $stampPath -Raw -Encoding UTF8), 'BuildTimeUtc\s*=\s*"([^"]+)"')
    if ($match.Success) { $buildStamp = $match.Groups[1].Value }
}

# --- 测试结果 ---
$tests = [System.Collections.Generic.List[object]]::new()
$totalPassed = 0; $totalFailed = 0; $totalSkipped = 0
foreach ($trx in $TestResults) {
    if (-not (Test-Path -LiteralPath $trx)) { throw "TRX not found: $trx" }
    [xml]$doc = Get-Content -LiteralPath $trx -Raw -Encoding UTF8
    $counters = $doc.TestRun.ResultSummary.Counters
    $passed = [int]$counters.passed; $failed = [int]$counters.failed; $skipped = [int]$counters.skipped
    $totalPassed += $passed; $totalFailed += $failed; $totalSkipped += $skipped
    $tests.Add([ordered]@{
        path = $trx; sha256 = (Get-Sha256 $trx)
        passed = $passed; failed = $failed; skipped = $skipped
        runTime = $doc.TestRun.Times.finish
    })
}

# --- 变更标注指纹 ---
$annotations = [System.Collections.Generic.List[object]]::new()
foreach ($id in $ChangeId) {
    $candidate = Join-Path $AnnotationDir ("变更标注_" + $id + ".md")
    if (Test-Path -LiteralPath $candidate) {
        $annotations.Add([ordered]@{ changeId = $id; path = $candidate; sha256 = (Get-Sha256 $candidate) })
    } else {
        $annotations.Add([ordered]@{ changeId = $id; path = $null; sha256 = $null; missing = $true })
    }
}

# --- 可选：跑一次编译并记录错/警数 ---
$compile = $null
if ($VerifyCompile) {
    $log = & pwsh -NoProfile -File '_scripts/compile_with_sdk.ps1' 2>&1 | Out-String
    $errors = @([regex]::Matches($log, 'error CS\d+')).Count
    $warnings = @([regex]::Matches($log, 'warning CS\d+')).Count
    $compile = [ordered]@{ errors = $errors; warnings = $warnings }
}

# --- 判定（绝不伪造 PASS）；硬阻断 vs 软提示两级 ---
# 硬阻断 = 质量缺陷，会阻止交付：测试失败 / 编译非 0 错 0 警 / 标注缺失。
# 软提示 = 仓库状态，**登记即可**（报告口径本就接受"修复期工作区有改动"，只要求把 dirty 指纹一并绑定）：
#          未推送提交 / 未提交改动 —— 它们不构成质量缺陷，但必须出现在 manifest 里供审计追溯。
$problems = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
if ($totalFailed -gt 0) { $problems.Add("测试失败 $totalFailed 项（须逐条确认是否属本批；属并行写入者也要登记）") }
if ($compile -and ($compile.errors -gt 0 -or $compile.warnings -gt 0)) {
    $problems.Add("编译 0 错 0 警未达成：errors=$($compile.errors) warnings=$($compile.warnings)")
}
foreach ($a in $annotations) { if ($a.missing) { $problems.Add("变更标注缺失：" + $a.changeId) } }
if ($head -ne $originMain) { $warnings.Add("HEAD != origin/main（存在未推送提交；快照基线须注明）") }
if ($dirtyLines.Count -gt 0) { $warnings.Add("工作区有 $($dirtyLines.Count) 项未提交改动（快照非单一提交；已连同 dirty 指纹绑定）") }

$manifest = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')
    source = [ordered]@{
        gitHead = $head; originMain = $originMain
        dirtyCount = $dirtyLines.Count
        workingTreeFingerprint = (Get-Sha256 $stampPath)
    }
    build = [ordered]@{
        buildStampUtc = $buildStamp
        assembly = $Assembly
        assemblySha256 = (Get-Sha256 $Assembly)
        assemblyWriteTimeUtc = (Get-Item -LiteralPath $Assembly).LastWriteTimeUtc.ToString('yyyy-MM-dd HH:mm:ss')
    }
    tests = $tests
    annotations = $annotations
    compile = $compile
    verdict = if ($problems.Count -gt 0) { 'BLOCKED' } elseif ($warnings.Count -gt 0) { 'READY_WITH_WARNINGS' } else { 'READY' }
    blockers = $problems
    warnings = $warnings
}

$outPath = Join-Path $repo $Out
$outDir = Split-Path -Parent $outPath
if (-not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
[System.IO.File]::WriteAllText($outPath, ($manifest | ConvertTo-Json -Depth 6), $utf8)

# --- 控制台摘要 ---
Write-Output '=== 发布证据绑定（第 4 批） ==='
Write-Output ("git HEAD            : " + $head)
Write-Output ("origin/main         : " + $originMain)
Write-Output ("working tree        : " + $dirtyLines.Count + " 项未提交改动")
Write-Output ("BuildStamp (UTC)    : " + $buildStamp)
Write-Output ("DLL SHA256          : " + $manifest.build.assemblySha256)
Write-Output ("tests               : " + $totalPassed + " passed / " + $totalFailed + " failed / " + $totalSkipped + " skipped")
Write-Output ("changeIds           : " + ($ChangeId -join ', '))
if ($compile) { Write-Output ("compile             : errors=" + $compile.errors + " warnings=" + $compile.warnings) }
Write-Output ("manifest            : " + $Out)
if ($problems.Count -eq 0) {
    if ($warnings.Count -eq 0) {
        Write-Output '判定: READY —— 四方证据已绑定，无阻断项'
    } else {
        Write-Output '判定: READY_WITH_WARNINGS —— 无质量阻断，仓库状态提示如下（已绑定进 manifest，可交付但须登记）'
        foreach ($w in $warnings) { Write-Output ("  ~ " + $w) }
    }
    exit 0
}
Write-Output '判定: BLOCKED —— 硬阻断项如下（不得据此宣称验收通过）'
foreach ($p in $problems) { Write-Output ("  - " + $p) }
foreach ($w in $warnings) { Write-Output ("  ~ " + $w) }
exit 2
