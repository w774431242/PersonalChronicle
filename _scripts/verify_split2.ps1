$ErrorActionPreference = 'Stop'
$dir = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source\PersonalChronicle\Application'
$files = @('ArchiveService.Work.cs','ArchiveService.Work.Intensity.cs','ArchiveService.Work.Aggregate.cs','ArchiveService.Work.Skills.cs')

"==== BOM CHECK + STRIP (defensive)"
foreach ($f in $files) {
    $p = "$dir\$f"
    $b = [System.IO.File]::ReadAllBytes($p)
    if ($b[0] -eq 239 -and $b[1] -eq 187 -and $b[2] -eq 191) {
        [System.IO.File]::WriteAllBytes($p, $b[3..($b.Length - 1)])
        "BOM_STRIPPED $f"
    }
}

"==== LINE COUNTS (red line 800)"
foreach ($f in $files) {
    $n = (Get-Content "$dir\$f" -Encoding UTF8).Count
    "{0}: {1} {2}" -f $f, $n, $(if ($n -gt 800) {'OVER' } else {''})
}

"==== PARTIAL DECL"
$pd = 0
foreach ($f in $files) {
    if ([System.IO.File]::ReadAllText("$dir\$f") -match 'public sealed partial class ArchiveService') { $pd++ }
}
"partial declarations: {0} (expect 4)" -f $pd

"==== METHOD DEFINITION COUNT (each must appear exactly once)"
$methods = @(
    'GetWorkPriorities','GetWorkTimeStats','GetWorkIntensity','ComputeColonyWorkRank',
    'GetIntensityTiers','GetColonyWorkAggregate','GetWorkTypeBreakdown','RecordSample',
    'GetWorkIntensityPolicy','EvaluateWithProviders','FindTier','BuildColonyWorkAggregate',
    'BuildWorkTypeLeaderboard','FindColonyWorkType','RankOf','CompareWorkRows',
    'GetTotalWorkTicks','GetObservedDays','SafeAdd','EmptyColonyWorkAggregate','GetSkillArchive')
$all = ''
foreach ($f in $files) { $all += [System.IO.File]::ReadAllText("$dir\$f") }
foreach ($m in $methods) {
    $pat = '(public|private|protected|internal)\s+(static\s+)?[\w<>,.\s]+\s*' + [regex]::Escape($m) + '\s*\('
    $c = ([regex]::Matches($all, $pat)).Count
    $flag = if ($c -eq 1) { 'OK' } elseif ($c -eq 0) { 'MISSING' } else { "DUP($c)" }
    "{0}: {1}" -f $m, $flag
}
"VERIFY2_DONE"
