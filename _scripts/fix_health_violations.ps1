$ErrorActionPreference = 'Stop'

# ===== Part A: strip UTF-8 BOM from Defs/MedalDefs.xml (FIX-008, iron rule 1.1) =====
$p = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs\MedalDefs.xml'
$b = [System.IO.File]::ReadAllBytes($p)
if ($b.Length -ge 3 -and $b[0] -eq 239 -and $b[1] -eq 187 -and $b[2] -eq 191) {
    [System.IO.File]::WriteAllBytes($p, $b[3..($b.Length - 1)])
    "BOM_REMOVED MedalDefs.xml (new length={0})" -f ($b.Length - 3)
} else {
    "NO_BOM MedalDefs.xml (already clean)"
}

# ===== Part B: split ArchiveService.Work.cs into 4 partials (FIX-009, ARC-013 / iron rule 6.2) =====
$enc = [System.Text.UTF8Encoding]::new($false)
$src = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source\PersonalChronicle\Application\ArchiveService.Work.cs'
$dir = Split-Path $src
$lines = Get-Content $src -Encoding UTF8

function Build($doc, $ranges) {
    $o = @()
    $o += 'using System;'
    $o += 'using System.Collections.Generic;'
    $o += 'using System.Linq;'
    $o += 'using PersonalChronicle.Api;'
    $o += 'using PersonalChronicle.Data;'
    $o += 'using PersonalChronicle.Domain;'
    $o += 'using RimWorld;'
    $o += 'using RimWorld.Planet;'
    $o += 'using Verse;'
    $o += 'using Verse.AI;'
    $o += 'using Verse.AI.Group;'
    $o += ''
    $o += 'namespace PersonalChronicle.Application'
    $o += '{'
    $o += '    /// <summary>'
    foreach ($l in $doc) { $o += '    /// ' + $l }
    $o += '    /// </summary>'
    $o += '    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink'
    $o += ''
    foreach ($r in $ranges) {
        for ($i = $r[0]; $i -le $r[1]; $i++) { $o += $lines[$i - 1] }
        $o += ''
    }
    $o += '    }'
    $o += '}'
    return $o
}

# Work slice: priority read + stats + sample intake (+ shared SafeAdd/GetTotalWorkTicks)
$docWork = @(
    'Partial of ArchiveService - work priority read, work-time stats, and sample intake.',
    'Priority read degrades to archived snapshot (LA-1) for dead/absent pawns.',
    'Sample intake is the single write gateway; delegates to ChronicleGameComponent.')
[System.IO.File]::WriteAllLines("$dir\ArchiveService.Work.cs",
    (Build $docWork @(@(33,82), @(84,134), @(358,392), @(674,700), @(728,737))), $enc)

# Intensity slice: evaluation + tier + colony rank + work-type breakdown
$docInt = @(
    'Partial of ArchiveService - work-intensity evaluation, tier lookup, colony rank, work-type breakdown.')
[System.IO.File]::WriteAllLines("$dir\ArchiveService.Work.Intensity.cs",
    (Build $docInt @(@(136,184), @(186,214), @(216,233), @(268,356), @(394,406), @(408,445), @(447,470))), $enc)

# Aggregate slice: colony aggregate + work-type leaderboard + shared helpers
$docAgg = @(
    'Partial of ArchiveService - colony work aggregate, work-type leaderboard, and shared helpers.')
[System.IO.File]::WriteAllLines("$dir\ArchiveService.Work.Aggregate.cs",
    (Build $docAgg @(@(235,266), @(472,570), @(572,621), @(623,640), @(642,657), @(659,672), @(702,726), @(739,743))), $enc)

# Skills slice: skill archive (join vs end snapshot delta)
$docSkill = @(
    'Partial of ArchiveService - skill archive (join vs end snapshot delta).')
[System.IO.File]::WriteAllLines("$dir\ArchiveService.Work.Skills.cs",
    (Build $docSkill @(@(745,809))), $enc)

"SPLIT_DONE"
