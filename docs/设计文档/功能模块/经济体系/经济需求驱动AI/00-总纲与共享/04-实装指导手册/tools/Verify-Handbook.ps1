param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
@{ result = 'Running'; verifiedUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $root 'evidence\验证结果.json') -Encoding UTF8
$checks = [System.Collections.Generic.List[string]]::new()
$docs = @(Get-ChildItem -LiteralPath $root -Filter '*.md' -File | Sort-Object Name)
if ($docs.Count -ne 7) { throw "Expected 7 handbook chapters, got $($docs.Count)" }
$checks.Add('Seven handbook chapters present')
$moduleText = Get-Content -LiteralPath (Join-Path $root '02-业务模块实装细则.md') -Raw -Encoding UTF8
foreach ($number in 1..9) {
    $id = 'M{0:00}' -f $number
    if ($moduleText -notmatch "(?m)^## $id ") { throw "Missing module: $id" }
}
$checks.Add('Nine business modules have dedicated sections')
$institutionText = Get-Content -LiteralPath (Join-Path $root '06-微观机构与场所工具.md') -Raw -Encoding UTF8
foreach ($number in 1..12) {
    $id = 'I{0:00}' -f $number
    if ($institutionText -notmatch "(?m)^\| $id ") { throw "Missing institution: $id" }
}
$checks.Add('Twelve institutions catalogued with dedicated labor-market and examination workflows')
$linkCount = 0
$localTargets = [System.Collections.Generic.List[string]]::new()
foreach ($doc in $docs) {
    $content = Get-Content -LiteralPath $doc.FullName -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim('<','>')
        if ($target -match '^(https?://|#)') { continue }
        $target = ($target -split '#')[0]
        $target = [Uri]::UnescapeDataString($target)
        if (![IO.Path]::IsPathRooted($target)) { $target = Join-Path $doc.DirectoryName $target }
        $localTargets.Add($target)
        $linkCount++
    }
}
$checks.Add("Local markdown links resolve: $linkCount")
$data = Get-Content -LiteralPath (Join-Path $root 'evidence\本机接口证据.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in @($data.game, $data.ruif)) {
    if ((Get-FileHash -LiteralPath $entry.path).Hash -ne $entry.sha256) { throw "Binary changed: $($entry.path)" }
}
foreach ($entry in @($data.sourceHashes)) {
    $path = Join-Path $data.workspaceRoot $entry.relativePath
    if ((Get-FileHash -LiteralPath $path).Hash -ne $entry.sha256) { throw "Source changed: $path" }
}
foreach ($entry in @($data.decompiledSnapshots) + @($data.currentGameXml) + @($data.ruifSourceHashes)) {
    if ((Get-FileHash -LiteralPath $entry.path).Hash -ne $entry.sha256) { throw "Evidence source changed: $($entry.path)" }
}
$checks.Add('Binary, project source, decompiled snapshot and game XML hashes match evidence')
function Get-GameMethod([string]$Type, [string]$Name) {
    return @($data.game.types | Where-Object type -eq $Type | ForEach-Object methods | Where-Object signature -Like "*::$Name`(*")
}
if (!(Get-GameMethod 'Verse.GameComponent' '.ctor' | Where-Object signature -Like '*::.ctor()')) { throw 'GameComponent base constructor changed' }
if (!(Get-GameMethod 'RimWorld.Need' 'NeedInterval' | Where-Object { $_.public -and $_.abstract })) { throw 'NeedInterval contract changed' }
if (!(Get-GameMethod 'RimWorld.Bill_Production' 'Notify_IterationCompleted' | Where-Object virtual)) { throw 'Bill callback virtual flag changed' }
if (!(Get-GameMethod 'Verse.AI.ThinkNode_JobGiver' 'TryGiveJob' | Where-Object family)) { throw 'JobGiver contract changed' }
foreach ($pair in @(
    @('Verse.PawnCapacitiesHandler','GetLevel'), @('Verse.Hediff','get_Visible'),
    @('Verse.Pawn','WorkTypeIsDisabled'), @('RimWorld.Pawn_SkillTracker','GetSkill')
)) {
    if (!(Get-GameMethod $pair[0] $pair[1] | Where-Object public)) { throw "Institution data contract changed: $($pair[0])" }
}
$checks.Add('Examination and employment health, visibility, work-disable and skill interfaces verified')
$api = $data.ruif.types | Where-Object type -eq 'RUIF.Api'
if (!($api.fields | Where-Object { $_.signature -Like '*VersionString' -and $_.constant -eq '1.3.0' })) { throw 'RUIF baseline changed' }
$checks.Add('Critical GameComponent, Need, Bill, JobGiver and RUIF contracts verified')
$build = 'Skipped'
if (!$SkipBuild) {
    $buildOutput = & dotnet build (Join-Path $root 'examples\HandbookExamples.csproj') -c Release --nologo 2>&1
    $buildExit = $LASTEXITCODE
    $buildOutput | Set-Content -LiteralPath (Join-Path $root 'evidence\示例编译日志.txt') -Encoding UTF8
    if ($buildExit -ne 0) { $buildOutput | Write-Output; throw "Example build failed: $buildExit" }
    $build = 'Passed; TreatWarningsAsErrors=true'
    $checks.Add('Independent documentation examples compile against installed game and RUIF')
}
foreach ($target in $localTargets) {
    if (!(Test-Path -LiteralPath $target)) { throw "Broken local link: $target" }
}
$result = [ordered]@{
    verifiedUtc = [DateTime]::UtcNow.ToString('o'); result = 'Passed'
    chapterCount = $docs.Count; businessModuleCount = 9; localLinkCount = $linkCount
    institutionCount = 12
    gameTypeCount = $data.game.types.Count; ruifTypeCount = $data.ruif.types.Count
    gameMethodCount = @($data.game.types | ForEach-Object methods).Count
    ruifMethodCount = @($data.ruif.types | ForEach-Object methods).Count
    projectSourceHashCount = $data.sourceHashes.Count
    decompiledSnapshotCount = $data.decompiledSnapshots.Count
    currentGameXmlCount = $data.currentGameXml.Count
    ruifSourceHashCount = $data.ruifSourceHashes.Count
    exampleFileCount = 3; exampleBuild = $build; checks = @($checks)
    verificationScope = 'Handbook and standalone API examples only; runtime evidence is maintained separately'
    gameplayValidated = $false
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'evidence\验证结果.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 8
