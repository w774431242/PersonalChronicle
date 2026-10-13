param([string]$GameRoot='E:\SteamLibrary\steamapps\common\RimWorld')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../../../../..')).Path
if (!(Test-Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll'))) {
    $repo=(Get-Location).Path
}
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$dll=Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
$names=@('Need','NeedDef','Pawn_NeedsTracker','Need_Food','Need_Rest','Need_Joy','Need_Comfort','Need_Beauty','Need_RoomSize','Need_Outdoors','Need_Indoors','Need_Mood','Need_Play','Need_Learning','JobGiver_GetFood','JobGiver_GetRest','JobGiver_GetJoy','Pawn_Ownership','Pawn_RelationsTracker','InteractionWorker_RomanceAttempt','InteractionWorker_MarriageProposal','MarriageCeremonyUtility','GatheringWorker_MarriageCeremony','ChildcareUtility','LearningUtility','WorkGiver_PlayWithBaby','WorkGiver_Teach','WorkGiver_Breastfeed','WorkGiver_FeedBabyManually','PregnancyUtility','PregnancyApproach')
try {
    $types=@(foreach($name in $names) {
        $t=$asm.MainModule.Types | Where-Object FullName -eq "RimWorld.$name" | Select-Object -First 1
        if (!$t) { throw "Missing type: RimWorld.$name" }
        [ordered]@{type=$t.FullName; baseType=[string]$t.BaseType;
            properties=@($t.Properties | ForEach-Object ToString);
            fields=@($t.Fields | Where-Object IsPublic | ForEach-Object ToString);
            methods=@($t.Methods | Where-Object { $_.IsPublic -or $_.IsFamily } | ForEach-Object {
                [ordered]@{signature=$_.ToString();public=$_.IsPublic;family=$_.IsFamily;virtual=$_.IsVirtual}
            })}
    })
    $files=@((Join-Path $GameRoot 'Data/Core/Defs/NeedDefs/Needs.xml'),(Join-Path $GameRoot 'Data/Biotech/Defs/NeedDefs/Needs.xml'))
    $files+=@($names | ForEach-Object { Join-Path $repo "_research/vanilla/Assembly-CSharp/RimWorld/$_.cs" } | Where-Object {Test-Path -LiteralPath $_})
    $files+=@(Join-Path $repo '../RUIF/Source/Components/PanelAtoms.cs'); $files+=@(Join-Path $repo '../RUIF/Source/Components/DataAtoms.cs')
    $result=[ordered]@{
        collectedUtc=[DateTime]::UtcNow.ToString('o'); gameVersion=(Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim();
        method='Read-only current DLL metadata, installed XML, and decompiled-source file hashes; no game assembly execution. Source snapshots are explanatory and are not guaranteed byte-identical to current DLL.';
        assembly=[ordered]@{path=$dll;sha256=(Get-FileHash -LiteralPath $dll).Hash;mvid=$asm.MainModule.Mvid.ToString()};
        types=$types; files=@($files | ForEach-Object {[ordered]@{path=(Resolve-Path -LiteralPath $_).Path;sha256=(Get-FileHash -LiteralPath $_).Hash}})
    }
    $dest=Join-Path $PSScriptRoot '../evidence/原版需求与家庭机制_证据.json'
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $dest -Encoding UTF8
    Write-Output "Verified $($types.Count) types and $($files.Count) files; evidence saved."
} finally { $asm.Dispose() }
