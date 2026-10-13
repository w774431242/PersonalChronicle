param([string]$GameRoot='E:\SteamLibrary\steamapps\common\RimWorld')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../../../../..')).Path
$ruifSource=Join-Path (Split-Path -Parent $repo) 'RUIF/Source/Components'
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$gameDll=Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$ruifDll=Join-Path $GameRoot 'Mods/RUIF/Assemblies/RUIF.dll'
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameDll)
$ruif=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($ruifDll)
try {
    $names=@('RimWorld.Pawn_TimetableTracker','RimWorld.TimeAssignmentDef','RimWorld.GenDate','RimWorld.GenLocalDate','RimWorld.Pawn_StoryTracker','RimWorld.TraitSet','RimWorld.Trait','RimWorld.Pawn_WorkSettings','RimWorld.Pawn_SkillTracker','RimWorld.SkillRecord','RimWorld.FoodUtility','RimWorld.CompQuality')
    $types=@(foreach($name in $names) {
        $type=$game.MainModule.Types | Where-Object FullName -eq $name | Select-Object -First 1
        if (!$type) { throw "Missing game type: $name" }
        [ordered]@{type=$name;baseType=[string]$type.BaseType;
            publicFields=@($type.Fields | Where-Object IsPublic | ForEach-Object ToString);
            methods=@($type.Methods | Where-Object IsPublic | ForEach-Object {
                [ordered]@{signature=$_.ToString();public=$_.IsPublic;virtual=$_.IsVirtual}
            })}
    })
    $date=$game.MainModule.Types | Where-Object FullName -eq 'RimWorld.GenDate' | Select-Object -First 1
    $constants=[ordered]@{}
    foreach($name in @('TicksPerDay','TicksPerHour','HoursPerDay')) {
        $field=$date.Fields | Where-Object Name -eq $name | Select-Object -First 1
        if (!$field.IsPublic -or !$field.HasConstant) { throw "Missing public time constant: $name" }
        $constants[$name]=[int]$field.Constant
    }
    if ($constants.TicksPerDay -ne 60000 -or $constants.TicksPerHour -ne 2500 -or $constants.HoursPerDay -ne 24) {
        throw 'Time constants differ; revise planning assumptions'
    }
    $ruifRequirements=@(
        @{type='RUIF.PanelAtoms';method='StatRow'},
        @{type='RUIF.DataAtoms';method='StatTable'},
        @{type='RUIF.FeedbackAtoms';method='AlertBanner'},
        @{type='RUIF.FeedbackAtoms';method='EmptyState'},
        @{type='RUIF.ProgressAtoms';method='CapacityBar'}
    )
    $ruifMethods=@(foreach($requirement in $ruifRequirements) {
        $type=$ruif.MainModule.Types | Where-Object FullName -eq $requirement.type | Select-Object -First 1
        $methods=@($type.Methods | Where-Object {$_.IsPublic -and $_.Name -eq $requirement.method})
        if (!$methods.Count) { throw "Missing RUIF method: $($requirement.type).$($requirement.method)" }
        foreach($method in $methods) { [ordered]@{type=$requirement.type;signature=$method.ToString()} }
    })
    $paths=@('Pawn_TimetableTracker','TimeAssignmentDef','GenDate','GenLocalDate','Pawn_StoryTracker','TraitSet','Trait','Pawn_WorkSettings','Pawn_SkillTracker','SkillRecord','FoodUtility','CompQuality')
    $files=@($paths | ForEach-Object {Join-Path $repo "_research/vanilla/Assembly-CSharp/RimWorld/$_.cs"})
    $files+=@(
        (Join-Path $GameRoot 'Data/Core/Defs/TraitDefs/Traits_Spectrum.xml'),
        (Join-Path $GameRoot 'Data/Core/Defs/TraitDefs/Traits_Singular.xml'),
        (Join-Path $GameRoot 'Data/Core/Defs/Misc/TimeAssignmentDefs/TimeAssignments.xml'),
        (Join-Path $GameRoot 'Data/Core/Defs/ThingDefs_Items/Items_Food.xml'),
        (Join-Path $repo 'Source/PersonalChronicle.csproj'),
        (Join-Path $repo 'Source/PersonalChronicle/Application/PawnEconomy/EconomyService.cs')
    )
    $files+=@('PanelAtoms.cs','DataAtoms.cs','FeedbackAtoms.cs','ProgressAtoms.cs') | ForEach-Object {Join-Path $ruifSource $_}
    $traitNodes=@()
    foreach($relative in @('Traits_Spectrum.xml','Traits_Singular.xml')) {
        $xml=[xml](Get-Content -LiteralPath (Join-Path $GameRoot "Data/Core/Defs/TraitDefs/$relative") -Encoding UTF8 -Raw)
        foreach($trait in $xml.Defs.TraitDef) {
            if($trait.defName -in @('Industriousness','Ascetic','Gourmand','NightOwl','QuickSleeper','Greedy')) {
                $traitNodes += [ordered]@{defName=[string]$trait.defName;degrees=@($trait.degreeDatas.li | ForEach-Object {
                    [ordered]@{degree=if ($null -eq $_.degree) {0} else {[int]$_.degree};label=[string]$_.label}
                })}
            }
        }
    }
    if ($traitNodes.Count -ne 6) {throw 'Trait rule inputs differ; revise rules'}
    $result=[ordered]@{
        collectedUtc=[DateTime]::UtcNow.ToString('o');gameVersion=(Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim();
        method='Read-only current game/RUIF DLL metadata, installed Core XML and source fingerprints; no game code executed.';
        assembly=[ordered]@{path=$gameDll;sha256=(Get-FileHash -LiteralPath $gameDll).Hash;mvid=$game.MainModule.Mvid.ToString()};
        ruif=[ordered]@{path=$ruifDll;sha256=(Get-FileHash -LiteralPath $ruifDll).Hash;assembly=$ruif.Name.ToString();methods=$ruifMethods};
        types=$types;timeConstants=$constants;traits=$traitNodes;
        files=@($files | ForEach-Object {[ordered]@{path=(Resolve-Path -LiteralPath $_).Path;sha256=(Get-FileHash -LiteralPath $_).Hash}});
        publicReferences=@('https://spdskatr.github.io/RWModdingResources/saving-guide','https://harmony.pardeike.net/v2/articles/patching-postfix.html');
        limits=@('Economic personality mappings and planning rules are Mod design, not vanilla APIs or calibrated empirical results.',
            'Metadata proves signatures and constants, not in-game performance or complete third-party semantics.',
            'Decompiled source is explanatory and not guaranteed byte-identical to current DLL method bodies.',
            'Budget/quantity conversion, household coordination and action arbitration remain separate module contracts.',
            'No runtime implementation, build, deployment or game acceptance was executed.')
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '../evidence/模块二_长期生活规划_接口证据.json') -Encoding UTF8
    Write-Output "Verified $($types.Count) game types, $($files.Count) file fingerprints, $($ruifMethods.Count) RUIF overloads and $($traitNodes.Count) trait inputs."
} finally {$game.Dispose();$ruif.Dispose()}
