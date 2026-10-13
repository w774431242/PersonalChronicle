$ErrorActionPreference = 'Stop'
$dir = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source\PersonalChronicle\Application'
$defs = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs\MedalDefs.xml'

"==== LINE COUNTS (red line 800)"
foreach ($f in @('ArchiveService.Work.cs','ArchiveService.Work.Intensity.cs','ArchiveService.Work.Aggregate.cs','ArchiveService.Work.Skills.cs')) {
    $n = (Get-Content "$dir\$f" -Encoding UTF8).Count
    "{0}: {1} {2}" -f $f, $n, $(if ($n -gt 800) {'OVER' } else {''})
}

"==== BOM CHECK MedalDefs.xml"
$b = [System.IO.File]::ReadAllBytes($defs)
if ($b[0] -eq 239 -and $b[1] -eq 187 -and $b[2] -eq 191) { 'BOM_STILL_PRESENT' } else { 'BOM_GONE' }

"==== PARTIAL DECL + METHOD PRESENCE (each method must appear exactly once across the 4 files)"
$files = @('ArchiveService.Work.cs','ArchiveService.Work.Intensity.cs','ArchiveService.Work.Aggregate.cs','ArchiveService.Work.Skills.cs')
$all = ''
foreach ($f in $files) { $all += [System.IO.File]::ReadAllText("$dir\$f") }

$methods = @(
    'GetWorkPriorities','GetWorkTimeStats','GetWorkIntensity','ComputeColonyWorkRank',
    'GetIntensityTiers','GetColonyWorkAggregate','GetWorkTypeBreakdown','RecordSample',
    'GetWorkIntensityPolicy','EvaluateWithProviders','FindTier','BuildColonyWorkAggregate',
    'BuildWorkTypeLeaderboard','FindColonyWorkType','RankOf','CompareWorkRows',
    'GetTotalWorkTicks','GetObservedDays','SafeAdd','EmptyColonyWorkAggregate','GetSkillArchive')

$partialCount = 0
foreach ($f in $files) {
    $t = [System.IO.File]::ReadAllText("$dir\$f")
    if ($t -match 'public sealed partial class ArchiveService') { $partialCount++ }
}
"partial declarations: {0} (expect 4)" -f $partialCount

foreach ($m in $methods) {
    $c = ([regex]::Matches($all, "($m)\s*\(")).Count
    $flag = if ($c -eq 1) { 'OK' } elseif ($c -eq 0) { 'MISSING' } else { 'DUPLICATE(x'+$c+')' }
    "{0}: {1}" -f $m, $flag
}
"VERIFY_DONE"
