$ErrorActionPreference='Stop'
$designRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$documents=@('M01-状态与需求感知/01-状态与需求感知_代码设计方案.md','M02-长期生活规划/02-长期生活规划_代码设计方案.md','M01-状态与需求感知/01-状态与需求感知_方案评审.md','00-总纲与共享/02-代码设计总册/00-公有经济第一批代码设计.md','00-总纲与共享/04-实装指导手册/00-总册与实施路线.md')
$strictUtf8=[System.Text.UTF8Encoding]::new($false,$true)
$documentChecks=@(foreach($relative in $documents) {
    $path=Join-Path $designRoot $relative
    $body=$strictUtf8.GetString([System.IO.File]::ReadAllBytes($path))
    if ($body.Contains([char]0xFFFD)) {throw "Replacement character: $relative"}
    if ([regex]::Matches($body,'(?m)^```').Count % 2 -ne 0) {throw "Unpaired fence: $relative"}
    $links=@()
    foreach($match in [regex]::Matches($body,'\]\(([^)]+)\)')) {
        $target=$match.Groups[1].Value.Trim('<','>')
        if ($target -match '^(https?://|#|app://)') {continue}
        $target=($target -split '#',2)[0]
        if (!$target) {continue}
        $resolved=if([System.IO.Path]::IsPathRooted($target)) {$target} else {Join-Path (Split-Path -Parent $path) $target}
        if (!(Test-Path -LiteralPath $resolved)) {throw "Broken link in ${relative}: $target"}
        $links+=$target
    }
    [ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $path).Hash;utf8=$true;pairedFences=$true;localLinks=$links.Count}
})
$m1=Get-Content -LiteralPath (Join-Path $designRoot $documents[0]) -Encoding UTF8 -Raw
foreach($marker in @('enum FactState { Unknown = 0','enum SubjectLocation { Unknown = 0','enum TargetState { Unknown = 0','maxAgeTicksByGroup','PublicationBatchId','CurrentConditionCapturedTick','RawLevelShortfall','PerceptionPageSnapshot')) {
    if (!$m1.Contains($marker)) {throw "M1 revision contract marker absent: $marker"}
}

$evidenceChecks=@(foreach($name in @('模块一_状态需求感知_接口证据.json','模块二_长期生活规划_接口证据.json')) {
    $evidence=Get-Content -LiteralPath (Join-Path $PSScriptRoot "../evidence/$name") -Encoding UTF8 -Raw | ConvertFrom-Json
    $entries=@($evidence.assembly)+@($evidence.files)
    if ($evidence.ruif) {$entries+=@($evidence.ruif)}
    foreach($entry in $entries) {
        if ((Get-FileHash -LiteralPath $entry.path).Hash -ne $entry.sha256) {throw "Stale evidence: $($entry.path)"}
    }
    [ordered]@{path=$name;types=$evidence.types.Count;sourceFingerprints=$evidence.files.Count;assemblyAndSourceHashesMatched=$true}
})
$parameters=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../evidence/模块二_参数设计样例.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ($parameters.stage -ne 'design_sample_only_not_runtime_configuration' -or $parameters.runtimeValidated) {throw 'Incorrect parameter artifact stage'}
if ($parameters.refillLowBasisPoints -ge $parameters.refillHighBasisPoints) {throw 'Refill band invalid'}
foreach($need in $parameters.numericNeedExamples.PSObject.Properties) {
    $band=$need.Value
    if ($band.enterBelow -lt 0 -or $band.enterBelow -ge $band.completeAt -or $band.completeAt -gt $band.desiredRatio -or $band.desiredRatio -gt 1) {throw "Invalid illustrative execution band: $($need.Name)"}
}

# These are independently written arithmetic reviews of documented examples.
# They do not instantiate or test any proposed runtime planning types.
$arithmetic=[System.Collections.Generic.List[object]]::new()
function Assert-DesignNumber([string]$case,[decimal]$actual,[decimal]$expected) {
    if ([Math]::Abs($actual-$expected) -gt [decimal]0.000000001) {throw "Design arithmetic mismatch ${case}: $actual vs $expected"}
    $arithmetic.Add([ordered]@{case=$case;actual=$actual;expected=$expected})
}
$maxLevel=[decimal]2; $raw=[decimal]0.8; $target=[decimal]0.8
$ratio=$raw/$maxLevel
Assert-DesignNumber 'M1 raw to ratio' $ratio 0.4
Assert-DesignNumber 'M1 relative deficit' (($target-$ratio)/$target) 0.5
Assert-DesignNumber 'M1 absolute ratio deficit' ($target-$ratio) 0.4
Assert-DesignNumber 'M1 raw level shortfall' ($target*$maxLevel-$raw) 0.8
Assert-DesignNumber 'same relative deficit, smaller absolute amount' (([decimal]0.4-[decimal]0.2)/[decimal]0.4) 0.5

$expectedWork=@(6,8,10);$expectedReserve=@(1,2,3);$expectedSaving=@(0,0.2,0.4);$expectedPremium=@(0,0.25,0.5);$expectedLeisure=@(1,2,3)
$expectedFood=@(0.8,0.85,0.9);$expectedJoy=@(0.6,0.7,0.8)
$styleValues=@([decimal]0,[decimal]0.5,[decimal]1)
for($index=0;$index -lt $styleValues.Count;$index++) {
    $offset=$styleValues[$index]-[decimal]0.5
    Assert-DesignNumber "work style $index" ($parameters.workBaseHours+4*$offset) $expectedWork[$index]
    Assert-DesignNumber "reserve style $index" ($parameters.reserveBaseDays+2*$offset) $expectedReserve[$index]
    Assert-DesignNumber "saving style $index" ($parameters.savingBaseBasisPoints/[decimal]10000+[decimal]0.4*$offset) $expectedSaving[$index]
    Assert-DesignNumber "premium style $index" ($parameters.premiumBaseBasisPoints/[decimal]10000+[decimal]0.5*$offset) $expectedPremium[$index]
    Assert-DesignNumber "leisure style $index" ($parameters.leisureBaseHours+2*$offset) $expectedLeisure[$index]
    Assert-DesignNumber "food target style $index" ([decimal]0.85+[decimal]0.10*$offset) $expectedFood[$index]
    Assert-DesignNumber "joy target style $index" ([decimal]0.70+[decimal]0.20*$offset) $expectedJoy[$index]
}
$blocks=$parameters.timeExampleHours
$baseNonCare=$blocks.sleep+$blocks.food+$blocks.travel+$blocks.leisure+$blocks.slack
$free24=24-$baseNonCare-$blocks.care
$freeCare6=24-$baseNonCare-6
Assert-DesignNumber 'care 2h leaves maximum 8h ordinary work' $free24 8
Assert-DesignNumber 'care 6h leaves maximum 4h ordinary work' $freeCare6 4
Assert-DesignNumber 'existing work counted once, original care' ($free24-$blocks.existingWork) 4
Assert-DesignNumber 'existing work counted once, increased care' ($freeCare6-$blocks.existingWork) 0
Assert-DesignNumber 'game day to hours' ($parameters.gameTicksPerDay/$parameters.gameTicksPerHour) 24

$cash=$parameters.cashExampleMinorUnits
$reserve=$cash.dailyUncoveredNecessaryCost*$parameters.reserveBaseDays
Assert-DesignNumber 'reserve target in minor units' $reserve 1200
Assert-DesignNumber 'refill low water in minor units' ($reserve*$parameters.refillLowBasisPoints/[decimal]10000) 840
Assert-DesignNumber 'conditional end cash, 8h wages after work' ($cash.cashAvailable+8*$cash.wagePerHourConditional-$cash.dailyUncoveredNecessaryCost) 1500
Assert-DesignNumber 'conditional end cash, care limits work to 4h' ($cash.cashAvailable+4*$cash.wagePerHourConditional-$cash.dailyUncoveredNecessaryCost) 700
Assert-DesignNumber 'food due before wage requires immediate extra cash' ($cash.dailyUncoveredNecessaryCost-$cash.cashAvailable) 100
Assert-DesignNumber 'surplus saving is not gross-wage withholding' ((1000-700-200)*$parameters.savingBaseBasisPoints/[decimal]10000) 20
Assert-DesignNumber 'zero surplus means no extra saving' ((1000-700-300)*$parameters.savingBaseBasisPoints/[decimal]10000) 0

$result=[ordered]@{
    checkedUtc=[DateTime]::UtcNow.ToString('o');scope='Design documents, current evidence fingerprints, contract text markers and hypothetical arithmetic only.';
    documents=$documentChecks;evidence=$evidenceChecks;parameterBandsValid=$true;defaultUnknownContractTextPresent=$true;
    arithmeticChecks=$arithmetic;runtimeCompiled=$false;runtimeTestsExecuted=$false;performanceMeasured=$false;gameValidated=$false;
    limits=@('Text markers do not prove a Fact or query implementation is correct.',
        'Arithmetic examples do not simulate future demand, wages, service capacity or real gameplay.',
        'Trait rules and parameter defaults require implementation and playtesting; budget, cooperation and arbitration remain separate modules.')
}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '../evidence/模块一优化与模块二_设计校验.json') -Encoding UTF8
Write-Output "Validated $($documentChecks.Count) documents, both current evidence inventories, parameter bands and $($arithmetic.Count) arithmetic examples; runtime unvalidated."
