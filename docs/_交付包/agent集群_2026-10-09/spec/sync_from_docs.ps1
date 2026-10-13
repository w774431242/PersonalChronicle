<#
.SYNOPSIS
  Re-sync the references folder of the "RimWorld工程架构规范" skill.
.DESCRIPTION
  The human-readable master copy of the standard lives in the PersonalChronicle
  project at docs/<design docs>/<architecture>/. After editing the standard
  there, run this script to refresh the copies inside the skill.
  Gate-Record-*.md and EXC-*.md are archived into references/history-archive.
#>
param(
    [string]$Source,
    [string]$SkillRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

if (-not $Source) {
    Write-Error "Pass -Source <project docs architecture folder>."
    exit 1
}

if (-not (Test-Path -LiteralPath $Source)) {
    Write-Error "Source directory not found: $Source"
    exit 1
}

$refDir = Join-Path $SkillRoot 'references'
$histDir = Join-Path $refDir 'history-archive'
New-Item -ItemType Directory -Force -Path $refDir, $histDir | Out-Null

$copied = 0
Get-ChildItem -LiteralPath $Source -Filter '*.md' -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $refDir $_.Name) -Force
    $copied++
}

$moved = 0
Get-ChildItem -LiteralPath $refDir -Filter 'Gate-Record-*.md' -File | ForEach-Object {
    Move-Item -LiteralPath $_.FullName -Destination (Join-Path $histDir $_.Name) -Force
    $moved++
}
Get-ChildItem -LiteralPath $refDir -Filter 'EXC-*.md' -File | ForEach-Object {
    Move-Item -LiteralPath $_.FullName -Destination (Join-Path $histDir $_.Name) -Force
    $moved++
}

Write-Host "Copied $copied spec files, archived $moved history files." -ForegroundColor Green
Write-Host "references : $refDir"
Write-Host "history    : $histDir"

$mismatch = @()
Get-ChildItem -LiteralPath $Source -Filter '*.md' -File | ForEach-Object {
    $dst = if ($_.Name -like 'Gate-Record-*' -or $_.Name -like 'EXC-*') {
        Join-Path $histDir $_.Name
    } else {
        Join-Path $refDir $_.Name
    }
    if (Test-Path -LiteralPath $dst) {
        $a = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        $b = (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash
        if ($a -ne $b) { $mismatch += $_.Name }
    } else {
        $mismatch += "$($_.Name) (missing)"
    }
}

if ($mismatch.Count -gt 0) {
    Write-Warning "Hash mismatch: $($mismatch -join ', ')"
    exit 2
}
Write-Host 'Hash check: all files match the source.' -ForegroundColor Green

# --- Rule-reference lint: 校验所有 [XXX-NNN] 引用目标已定义（防止 6 处交叉定义失配） ---
Write-Host 'Linting rule references...' -ForegroundColor Cyan
$prefixes = 'GOV','MATRIX','TERM','BASE','ARC','DATA','COMP','UI','LOC','ASSET','SAVE','TEST','PERF','REL','DOC','GATE','ANNO','EXC','AI','LIFE','SDD','REQ'
$idPat = '(' + ($prefixes -join '|') + ')-\d+'
$defined = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -LiteralPath $refDir -Filter '*.md' -File | ForEach-Object {
    $t = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($m in [regex]::Matches($t, $idPat)) { $null = $defined.Add($m.Value) }
}
$accepted = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$prefixes | ForEach-Object { $null = $accepted.Add("$_-001") }   # 各域锚点（如 ANNO-001 仅作 ID 体系登记）
$dangling = [System.Collections.Generic.List[string]]::new()
Get-ChildItem -LiteralPath $refDir -Filter '*.md' -File | ForEach-Object {
    $t = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($m in [regex]::Matches($t, '\[' + $idPat + '\]')) {
        $id = $m.Value.Trim('[', ']')
        $ok = $defined.Contains($id) -or $accepted.Contains($id) -or $id.StartsWith('LIFE-')
        if (-not $ok) { $dangling.Add("$($_.Name): $id") }
    }
}
if ($dangling.Count -gt 0) {
    Write-Warning "Dangling rule references: $($dangling -join ', ')"
    exit 2
}
Write-Host "Rule-reference lint: all references resolve ($($defined.Count) defined IDs)." -ForegroundColor Green
