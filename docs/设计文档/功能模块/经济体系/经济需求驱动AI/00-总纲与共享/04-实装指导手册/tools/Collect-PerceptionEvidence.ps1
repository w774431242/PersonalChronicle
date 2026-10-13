param([string]$GameRoot='E:\SteamLibrary\steamapps\common\RimWorld')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../../../../..')).Path
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$dll=Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
$names=@('Verse.Pawn','Verse.MapPawns','RimWorld.Need','RimWorld.Need_Seeker','RimWorld.Pawn_NeedsTracker','RimWorld.Need_Food','RimWorld.Need_Rest','RimWorld.Need_Joy','RimWorld.Need_Comfort','RimWorld.Need_Beauty','RimWorld.Need_RoomSize','RimWorld.Need_Outdoors','RimWorld.Need_Indoors','RimWorld.Need_Play','RimWorld.Need_Learning','Verse.Pawn_HealthTracker','Verse.HediffSet','Verse.Hediff','RimWorld.Pawn_ApparelTracker','RimWorld.Pawn_RelationsTracker','RimWorld.DirectPawnRelation','RimWorld.Pawn_Ownership','RimWorld.RestUtility','Verse.GenTemperature','RimWorld.PregnancyUtility')
try {
    $records=@(foreach($name in $names) {
        $t=$asm.MainModule.Types | Where-Object FullName -eq $name | Select-Object -First 1
        if (!$t) {throw "Missing type: $name"}
        [ordered]@{type=$name;baseType=[string]$t.BaseType;
            publicFields=@($t.Fields | Where-Object IsPublic | ForEach-Object ToString);
            methods=@($t.Methods | Where-Object {$_.IsPublic -or $_.IsFamily} | ForEach-Object {
                [ordered]@{signature=$_.ToString();public=$_.IsPublic;family=$_.IsFamily;virtual=$_.IsVirtual}
            })}
    })
    $need=$asm.MainModule.Types | Where-Object FullName -eq 'RimWorld.Need' | Select-Object -First 1
    $frozen=$need.Methods | Where-Object Name -eq 'get_IsFrozen' | Select-Object -First 1
    if (!$frozen.IsFamily -or $frozen.IsPublic) {throw 'Freeze visibility changed; revise the design before collecting evidence'}
    $paths=@('RimWorld/Need.cs','RimWorld/Need_Seeker.cs','RimWorld/Need_Beauty.cs','RimWorld/Need_Comfort.cs','RimWorld/Need_RoomSize.cs','RimWorld/Pawn_NeedsTracker.cs','RimWorld/Need_Food.cs','RimWorld/Need_Rest.cs','RimWorld/Pawn_Ownership.cs','RimWorld/Pawn_RelationsTracker.cs','RimWorld/PregnancyUtility.cs','Verse/MapPawns.cs','Verse/Hediff.cs','Verse/HediffSet.cs','Verse/GenTemperature.cs')
    $files=@($paths | ForEach-Object { Join-Path $repo "_research/vanilla/Assembly-CSharp/$_" })
    $files+=@(Join-Path $GameRoot 'Data/Core/Defs/NeedDefs/Needs.xml'),(Join-Path $GameRoot 'Data/Biotech/Defs/NeedDefs/Needs.xml')
    $files+=@(Join-Path $repo 'Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs'),(Join-Path $repo 'Source/PersonalChronicle.csproj')
    $files+=Join-Path $repo 'Source/PersonalChronicle/Application/PawnEconomy/EconomyService.cs'
    $result=[ordered]@{
        collectedUtc=[DateTime]::UtcNow.ToString('o');gameVersion=(Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim();
        method='Read-only current DLL metadata, installed NeedDefs and source fingerprints; no game code executed. Decompiled source semantics are explanatory, not guaranteed identical to this DLL.';
        assembly=[ordered]@{path=$dll;sha256=(Get-FileHash -LiteralPath $dll).Hash;mvid=$asm.MainModule.Mvid.ToString()};
        types=$records;
        freezeBoundary=[ordered]@{signature=$frozen.ToString();public=$frozen.IsPublic;family=$frozen.IsFamily;firstBatch='Unknown without a separately verified freeze adapter; do not call protected getter directly.'};
        files=@($files | ForEach-Object { [ordered]@{path=(Resolve-Path -LiteralPath $_).Path;sha256=(Get-FileHash -LiteralPath $_).Hash} });
        limits=@('Getter visibility does not prove read-only semantics of every getter or third-party override.',
            'Current and maximum levels must be finite and maximum positive before deriving ratios.',
            'Need interval scheduling and source caches require runtime ordering/profiling checks.',
            'No complete capture lifecycle, family responsibility assignment, service quantity forecast or RUIF page was executed.')
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '../evidence/模块一_状态需求感知_接口证据.json') -Encoding UTF8
    Write-Output "Verified $($records.Count) types, $($files.Count) fingerprints and protected freeze boundary."
} finally {$asm.Dispose()}
