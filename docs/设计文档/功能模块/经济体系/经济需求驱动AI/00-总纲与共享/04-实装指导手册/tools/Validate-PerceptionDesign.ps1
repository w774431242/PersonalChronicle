$ErrorActionPreference = 'Stop'
$designRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$documents = @(
    'M01-状态与需求感知/01-状态与需求感知_代码设计方案.md',
    '00-总纲与共享/02-代码设计总册/00-公有经济第一批代码设计.md',
    '00-总纲与共享/04-实装指导手册/00-总册与实施路线.md'
)
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$documentChecks = @(foreach ($relative in $documents) {
    $path = Join-Path $designRoot $relative
    $body = $strictUtf8.GetString([System.IO.File]::ReadAllBytes($path))
    if ($body.Contains([char]0xFFFD)) { throw "Replacement character in $relative" }
    $fences = [regex]::Matches($body, '(?m)^```').Count
    if ($fences % 2 -ne 0) { throw "Unpaired Markdown fence in $relative" }
    $localLinks = @()
    foreach ($match in [regex]::Matches($body, '\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim('<', '>')
        if ($target -match '^(https?://|#|app://)') { continue }
        $target = ($target -split '#', 2)[0]
        if (!$target) { continue }
        $resolved = if ([System.IO.Path]::IsPathRooted($target)) {
            $target
        } else {
            Join-Path (Split-Path -Parent $path) $target
        }
        if (!(Test-Path -LiteralPath $resolved)) { throw "Broken link in ${relative}: $target" }
        $localLinks += $target
    }
    [ordered]@{path=$relative;utf8=$true;pairedCodeFences=$true;localLinks=$localLinks}
})

$evidencePath = Join-Path $PSScriptRoot '../evidence/模块一_状态需求感知_接口证据.json'
$evidence = Get-Content -LiteralPath $evidencePath -Encoding UTF8 -Raw | ConvertFrom-Json
if ($evidence.types.Count -ne 25 -or $evidence.files.Count -ne 20) { throw 'Unexpected evidence inventory' }
if (!$evidence.freezeBoundary.family -or $evidence.freezeBoundary.public) { throw 'Freeze boundary mismatch' }
$fingerprints = @($evidence.assembly) + @($evidence.files)
foreach ($entry in $fingerprints) {
    if ((Get-FileHash -LiteralPath $entry.path).Hash -ne $entry.sha256) {
        throw "Stale evidence: $($entry.path)"
    }
}

# Arithmetic review of the documented equation only, not a runtime implementation test.
$examples = @(
    @{raw=0.8;maximum=2.0;target=0.8;expected=0.5},
    @{raw=0.0;maximum=1.0;target=0.8;expected=1.0},
    @{raw=0.8;maximum=1.0;target=0.8;expected=0.0},
    @{raw=1.0;maximum=1.0;target=0.8;expected=0.0}
)
foreach ($example in $examples) {
    $ratio = $example.raw / $example.maximum
    $gap = [Math]::Max(0.0, [Math]::Min(1.0, ($example.target - $ratio) / $example.target))
    if ([Math]::Abs($gap - $example.expected) -gt 1e-12) { throw 'Document equation example failed' }
}

$result = [ordered]@{
    checkedUtc=[DateTime]::UtcNow.ToString('o')
    scope='Design document consistency, current evidence fingerprints and equation arithmetic only.'
    documents=$documentChecks
    evidence=[ordered]@{types=$evidence.types.Count;sourceFingerprints=$evidence.files.Count;assemblyAndSourceHashesMatched=$true;protectedFreezeConfirmed=$true}
    equationExamples=$examples
    runtimeCompiled=$false
    runtimeTestsExecuted=$false
    gameValidated=$false
    limits=@('No capture implementation or RUIF page executed.', 'Freshness, lifecycle, compatibility, performance and full acceptance matrix remain implementation work.')
}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '../evidence/模块一_状态需求感知_设计校验.json') -Encoding UTF8
Write-Output "Validated $($documents.Count) documents, $($fingerprints.Count) current fingerprints and $($examples.Count) equation examples; runtime unvalidated."
