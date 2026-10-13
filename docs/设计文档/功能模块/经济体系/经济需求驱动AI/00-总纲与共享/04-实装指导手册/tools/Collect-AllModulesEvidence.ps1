param([string]$GameRoot='E:\SteamLibrary\steamapps\common\RimWorld')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../../../../..')).Path
$ruifSource=Join-Path (Split-Path -Parent $repo) 'RUIF/Source'
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$gameDll=Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$ruifDll=Join-Path $GameRoot 'Mods/RUIF/Assemblies/RUIF.dll'
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameDll)
$ruif=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($ruifDll)
try {
    $names=@('Verse.AI.ThinkNode','Verse.AI.ThinkNode_JobGiver','Verse.AI.Pawn_JobTracker','Verse.AI.JobDriver','Verse.AI.Job','Verse.AI.Toil','Verse.AI.ReservationManager','Verse.AI.Group.LordJob','RimWorld.WorkGiver_DoBill','RimWorld.Bill_Production','Verse.GenRecipe','Verse.Thing','Verse.ThingOwner','Verse.ThingOwner`1','RimWorld.TradeDeal','RimWorld.Tradeable','RimWorld.Pawn_RelationsTracker','RimWorld.JobDriver_TendPatient','RimWorld.JobDriver_FeedBaby','RimWorld.JobDriver_Breastfeed','Verse.GameComponent','Verse.Scribe_Values','Verse.Scribe_Deep','Verse.Scribe_References','RimWorld.Need_Joy','RimWorld.JoyToleranceSet')
    $types=@(foreach($name in $names) {
        $type=$game.MainModule.Types | Where-Object FullName -eq $name | Select-Object -First 1
        if (!$type) {throw "Missing game type: $name"}
        [ordered]@{type=$name;baseType=[string]$type.BaseType;
            publicFields=@($type.Fields | Where-Object IsPublic | ForEach-Object ToString);
            methods=@($type.Methods | Where-Object {$_.IsPublic -or $_.IsFamily} | ForEach-Object {
                [ordered]@{signature=$_.ToString();public=$_.IsPublic;protected=$_.IsFamily;abstract=$_.IsAbstract;virtual=$_.IsVirtual}
            })}
    })
    $giver=$game.MainModule.Types | Where-Object FullName -eq 'Verse.AI.ThinkNode_JobGiver' | Select-Object -First 1
    $giveJob=$giver.Methods | Where-Object Name -eq 'TryGiveJob' | Select-Object -First 1
    if (!$giveJob.IsFamily -or !$giveJob.IsAbstract -or $giveJob.IsPublic) {throw 'TryGiveJob boundary differs; revise design'}
    $callRequirements=@(
        @{type='RimWorld.TradeDeal';method='TryExecute'},
        @{type='RimWorld.Tradeable';method='ResolveTrade'},
        @{type='RimWorld.Bill_Production';method='Notify_IterationCompleted'},
        @{type='Verse.GenRecipe';method='MakeRecipeProducts'}
    )
    $callEvidence=@(foreach($requirement in $callRequirements) {
        $type=$game.MainModule.Types | Where-Object FullName -eq $requirement.type | Select-Object -First 1
        $method=$type.Methods | Where-Object Name -eq $requirement.method | Select-Object -First 1
        if (!$method) {throw "Missing method: $($requirement.type).$($requirement.method)"}
        [ordered]@{method=$method.ToString();hasBody=$method.HasBody;calls=@($method.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.ToString()} | Select-Object -Unique)}
    })
    $ruifRequirements=@(
        @{type='RUIF.PanelAtoms';method='StatRow'},@{type='RUIF.DataAtoms';method='StatTable'},
        @{type='RUIF.FeedbackAtoms';method='AlertBanner'},@{type='RUIF.FeedbackAtoms';method='EmptyState'},
        @{type='RUIF.ProgressAtoms';method='CapacityBar'},@{type='RUIF.NavAtomsB';method='LayerSwitch'},
        @{type='RUIF.InputAtoms';method='Segmented'},@{type='RUIF.InputAtoms';method='SearchField'},
        @{type='RUIF.InputAtoms';method='Stepper'},@{type='RUIF.InputAtoms';method='Checkbox'},
        @{type='RUIF.ButtonAtoms';method='PrimaryButton'},@{type='RUIF.Layout';method='BeginScroll'},
        @{type='RUIF.Layout';method='EndScroll'}
    )
    $ruifMethods=@(foreach($requirement in $ruifRequirements) {
        $type=$ruif.MainModule.Types | Where-Object FullName -eq $requirement.type | Select-Object -First 1
        $methods=@($type.Methods | Where-Object {$_.IsPublic -and $_.Name -eq $requirement.method})
        if (!$methods.Count) {throw "Missing RUIF method: $($requirement.type).$($requirement.method)"}
        foreach($method in $methods) {[ordered]@{type=$requirement.type;signature=$method.ToString()}}
    })
    $files=@($names | ForEach-Object {
        $sourceName=($_ -replace '`1$','').Replace('.','/')+'.cs'
        Join-Path $repo "_research/vanilla/Assembly-CSharp/$sourceName"
    } | Select-Object -Unique)
    $files+=@('Domain/PawnEconomy/EconomyModel.cs','Application/PawnEconomy/EconomyService.cs','Economy/PawnEconomy/EconomyRuntime.cs','Economy/PawnEconomy/EconomyCommands.cs','Economy/PawnEconomy/EconomyWorld.cs','ChronicleSettings.cs') | ForEach-Object {Join-Path $repo "Source/PersonalChronicle/$_"}
    $files+=Join-Path $repo 'Source/PersonalChronicle.csproj'
    $files+=@('Components/PanelAtoms.cs','Components/DataAtoms.cs','Components/FeedbackAtoms.cs','Components/ProgressAtoms.cs','Components/NavAtomsB.cs','Components/InputAtoms.cs','Components/ButtonAtoms.cs','Layout/Layout.cs') | ForEach-Object {Join-Path $ruifSource $_}
    $result=[ordered]@{
        collectedUtc=[DateTime]::UtcNow.ToString('o');gameVersion=(Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim();
        method='Read-only current game/RUIF DLL metadata, selected IL call operands and source fingerprints; no game code executed.';
        assembly=[ordered]@{path=$gameDll;sha256=(Get-FileHash -LiteralPath $gameDll).Hash;mvid=$game.MainModule.Mvid.ToString()};
        ruif=[ordered]@{path=$ruifDll;sha256=(Get-FileHash -LiteralPath $ruifDll).Hash;assembly=$ruif.Name.ToString();methods=$ruifMethods};
        types=$types;selectedCallEvidence=$callEvidence;
        files=@($files | ForEach-Object {[ordered]@{path=(Resolve-Path -LiteralPath $_).Path;sha256=(Get-FileHash -LiteralPath $_).Hash}});
        publicReferences=@('https://harmony.pardeike.net/v2/articles/patching-prefix.html','https://harmony.pardeike.net/v2/articles/patching-postfix.html','https://spdskatr.github.io/RWModdingResources/saving-guide');
        limits=@('API/IL operands do not prove a complete safe execution or transaction hook.',
            'Production iterator construction is not proof of final products delivered or paid.',
            'Decompiled source explains semantics but is not guaranteed identical to current DLL bodies.',
            'Economy, household authority, claims, money and utility are Mod design, not vanilla APIs.',
            'No runtime implementation, build, deployment, performance test or in-game acceptance executed.')
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '../evidence/全模块_接口证据.json') -Encoding UTF8
    Write-Output "Verified $($types.Count) additional game types, $($files.Count) source fingerprints, $($ruifMethods.Count) RUIF overloads and $($callEvidence.Count) selected IL call lists."
} finally {$game.Dispose();$ruif.Dispose()}
