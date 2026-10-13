$ErrorActionPreference='Stop'
$designRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$manifestPath=Join-Path $PSScriptRoot '../evidence/全模块_设计清单.json'
$reportPath=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../evidence/全模块_设计校验.json'))
$manifest=Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
if ($manifest.modules.Count -ne 12 -or $manifest.implemented) {throw 'Incorrect complete-design scope'}
$expectedIds=@(1..12 | ForEach-Object {"M$_"})
if (@($manifest.modules.id | Select-Object -Unique).Count -ne 12) {throw 'Duplicate module ID'}
foreach($id in $expectedIds) {if ($id -notin $manifest.modules.id) {throw "Missing module $id"}}
foreach($module in $manifest.modules) {
    foreach($peer in @($module.inputs)+@($module.outputs)) {
        if ($peer -match '^M\d+$' -and $peer -notin $expectedIds) {throw "Unknown peer $peer"}
    }
}
if (@($manifest.modules.owns | Select-Object -Unique).Count -ne 12) {throw 'Ambiguous manifest ownership'}
$strictUtf8=[System.Text.UTF8Encoding]::new($false,$true)
$documents=@($manifest.master)+@($manifest.modules.file)+@($manifest.merged)
$checks=@(foreach($relative in $documents) {
    $path=Join-Path $designRoot $relative
    $body=$strictUtf8.GetString([System.IO.File]::ReadAllBytes($path))
    if ($body.Contains([char]0xFFFD)) {throw "Invalid replacement text: $relative"}
    if ([regex]::Matches($body,'(?m)^```').Count % 2 -ne 0) {throw "Unpaired fence: $relative"}
    if ($relative -in $manifest.modules.file) {
        foreach($marker in @('输入','输出','接口','保存','RUIF','验收')) {
            if (!$body.Contains($marker)) {throw "Missing structural marker ${marker}: $relative"}
        }
    }
    $links=0
    foreach($match in [regex]::Matches($body,'\]\(([^)]+)\)')) {
        $target=$match.Groups[1].Value.Trim('<','>')
        if ($target -match '^(https?://|#|app://)') {continue}
        $target=($target -split '#',2)[0]
        if (!$target) {continue}
        $resolved=if ([System.IO.Path]::IsPathRooted($target)) {$target} else {Join-Path (Split-Path -Parent $path) $target}
        $resolved=[System.IO.Path]::GetFullPath($resolved)
        # This validator creates its own report after all checks finish.
        if ($resolved -ne $reportPath -and !(Test-Path -LiteralPath $resolved)) {throw "Broken link in ${relative}: $target"}
        $links++
    }
    [ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $path).Hash;utf8=$true;pairedFences=$true;localLinks=$links;length=$body.Length}
})
$evidenceChecks=@(foreach($name in @('模块一_状态需求感知_接口证据.json','模块二_长期生活规划_接口证据.json','全模块_接口证据.json')) {
    $evidence=Get-Content -LiteralPath (Join-Path $PSScriptRoot "../evidence/$name") -Encoding UTF8 -Raw | ConvertFrom-Json
    $entries=@($evidence.assembly)+@($evidence.files)
    if ($evidence.ruif) {$entries+=@($evidence.ruif)}
    foreach($entry in $entries) {
        if ((Get-FileHash -LiteralPath $entry.path).Hash -ne $entry.sha256) {throw "Stale evidence: $($entry.path)"}
    }
    [ordered]@{file=$name;types=$evidence.types.Count;sourceFingerprints=$evidence.files.Count;matched=$true}
})

# Independent hypothetical arithmetic, not a runtime economy simulation.
$examples=[System.Collections.Generic.List[object]]::new()
function Assert-Example([string]$name,[decimal]$actual,[decimal]$expected) {
    if ([Math]::Abs($actual-$expected) -gt [decimal]0.000000001) {throw "Example mismatch ${name}: $actual vs $expected"}
    $examples.Add([ordered]@{name=$name;actual=$actual;expected=$expected})
}
Assert-Example 'M3 first effective help' (1000+100+200+300+50) 1650
Assert-Example 'M3 risk slack is not need percentage' (1800-1650) 150
Assert-Example 'M5 usable cash excludes hard reserve' (1000-300) 700
Assert-Example 'M5 reserve cost excludes protected obligation' (800-300) 500
Assert-Example 'M5 no duplicated current soft budget in target comparison' (700-500) 200
Assert-Example 'M5 daily reserve target' (600*2) 1200
Assert-Example 'M5 lower refill band, minor units' (1200*[decimal]7000/10000) 840
Assert-Example 'M5 limited financing shortfall' (1200-500) 700
Assert-Example 'M5 surplus saving, not whole wage' ((1000-700-200)*[decimal]2000/10000) 20
Assert-Example 'M7 incremental partial settlement' (400-250) 150
Assert-Example 'M7 unperformed held amount release' (1000-400) 600
Assert-Example 'M8 bounded family cost sharing' (400+300+300) 1000
Assert-Example 'M8 public shortfall not unauthorized spouse debit' (1000-400-300-100) 200
Assert-Example 'M9 initial estimate has prior, not certainty' (1/[decimal]2) 0.5
Assert-Example 'M9 one known timely outcome posterior example' ((1+1)/[decimal](2+1)) ([decimal]2/3)
Assert-Example 'M9 first retry delay' (150*[Math]::Pow(2,0)) 150
Assert-Example 'M9 bounded retry delay' ([Math]::Min(6000,150*[Math]::Pow(2,6))) 6000
Assert-Example 'M10 initial net production need' (12+6+4-8-5-0) 9
Assert-Example 'M10 after authorizing seven, only two new' (12+6+4-8-5-7) 2
Assert-Example 'M10 seven starts, total planned coverage unchanged' (12+6+4-8-12-0) 2
Assert-Example 'M10 seven finishes, inventory replaces WIP' (12+6+4-15-5-0) 2
Assert-Example 'M11 deposit silver to minor units' (20*100) 2000
$treasury=[decimal]2000;$institution=[decimal]0;$pawn=[decimal]0
$treasury-=1000;$institution+=1000
Assert-Example 'M11 public allocation conserves all balances' ($treasury+$institution+$pawn) 2000
$institution-=300;$pawn+=300
Assert-Example 'M11 real wage transfer conserves all balances' ($treasury+$institution+$pawn) 2000
$pawn-=200;$institution+=200
Assert-Example 'M11 consumption transfer conserves all balances' ($treasury+$institution+$pawn) 2000
Assert-Example 'M11 worker current balance after consumption' $pawn 100
Assert-Example 'M12 need minus existing public coverage' (36-6) 30
Assert-Example 'M12 added public procurement real cost' (6*3) 18
Assert-Example 'M12 private intent unchanged by public payment' (18+6) 24
Assert-Example 'M12 total covered including old public support' (18+6+6) 30
Assert-Example 'M12 unpaid real need still remains' (36-30) 6

$runtimeFolders=@('Perception','LifePlanning','Goals','Actions','Budget','Employment','Consumption','Care','Feedback','Institutions','Finance','Rights','Transactions')
$result=[ordered]@{
    checkedUtc=[DateTime]::UtcNow.ToString('o');scope='All twelve module design documents, local links, current evidence fingerprints, declared ownership and independent arithmetic examples only.';
    modules=$manifest.modules.Count;documents=$checks;evidence=$evidenceChecks;manifestOwnershipUnique=$true;
    structuralMarkersOnly=$true;hypotheticalArithmetic=$examples;
    runtimeCompiled=$false;runtimeTestsExecuted=$false;simulationExecuted=$false;performanceMeasured=$false;gameValidated=$false;
    limits=@('Manifest ownership and document markers do not prove an implementation satisfies contracts.',
        'Independent arithmetic is not a test of proposed game modules or a stability proof.',
        'Native ordering, individual completion hooks, rights access, silver arrival, save recovery and RUIF runtime remain prototype gates.',
        'No game DLL or decompiled source is distributed in the documentation package.')
}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Output "Validated 12 modules / $($checks.Count) documents, three current evidence inventories and $($examples.Count) hypothetical arithmetic examples; runtime unvalidated."
