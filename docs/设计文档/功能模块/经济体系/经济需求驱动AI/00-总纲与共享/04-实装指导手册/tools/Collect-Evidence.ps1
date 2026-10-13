param(
    [string]$WorkspaceRoot = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统',
    [string]$GameRoot = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$RuifSourceRoot = 'E:\SteamLibrary\steamapps\common\RimWorld\11\RUIF',
    [string]$CecilPath = '',
    [string]$OutputPath = ''
)
$ErrorActionPreference = 'Stop'
if (!$CecilPath) { $CecilPath = Join-Path $WorkspaceRoot 'InspectTypes\bin\Release\net8.0\Mono.Cecil.dll' }
if (!$OutputPath) { $OutputPath = Join-Path $PSScriptRoot '..\evidence\本机接口证据.json' }
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath)

function Read-AssemblyEvidence([string]$Path, [hashtable]$Targets) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    try {
        $types = @()
        foreach ($name in ($Targets.Keys | Sort-Object)) {
            $type = $assembly.MainModule.Types | Where-Object FullName -eq $name | Select-Object -First 1
            if (!$type) { throw "Type missing: $name" }
            $methods = @()
            if ($Targets[$name] -ne '*') {
                foreach ($required in $Targets[$name]) {
                    if (!($type.Methods | Where-Object Name -eq $required)) {
                        throw "Method missing: $name::$required"
                    }
                }
            }
            foreach ($method in $type.Methods) {
                if ($Targets[$name] -ne '*' -and $method.Name -notin $Targets[$name]) { continue }
                $calls = @()
                if ($method.HasBody) {
                    $calls = @($method.Body.Instructions | Where-Object {
                        $_.OpCode.Name -in @('call', 'callvirt', 'newobj')
                    } | ForEach-Object { $_.Operand.ToString() } | Select-Object -Unique)
                }
                $methods += [ordered]@{
                    signature = $method.ToString(); token = $method.MetadataToken.ToInt32()
                    public = $method.IsPublic; family = $method.IsFamily
                    static = $method.IsStatic; abstract = $method.IsAbstract
                    virtual = $method.IsVirtual; calls = $calls
                }
            }
            if ($methods.Count -eq 0 -and $Targets[$name] -ne '*') { throw "Methods missing: $name" }
            $types += [ordered]@{
                type = $name; baseType = [string]$type.BaseType
                fields = @($type.Fields | Where-Object IsPublic | ForEach-Object {
                    [ordered]@{ signature = $_.ToString(); constant = $(if ($_.HasConstant) { $_.Constant } else { $null }) }
                })
                methods = $methods
                nestedRelevantCallSites = @($type.NestedTypes | ForEach-Object {
                    $nested = $_
                    foreach ($nestedMethod in $nested.Methods) {
                        if (!$nestedMethod.HasBody) { continue }
                        foreach ($instruction in $nestedMethod.Body.Instructions) {
                            if ($instruction.OpCode.Name -notin @('call','callvirt','newobj')) { continue }
                            $operand = $instruction.Operand.ToString()
                            if ($operand -match 'MakeRecipeProducts|ConsumeIngredients|Notify_IterationCompleted|PostProcessProduct|NeedInterval') {
                                [ordered]@{ method = $nestedMethod.ToString(); offset = $instruction.Offset; target = $operand }
                            }
                        }
                    }
                })
            }
        }
        return [ordered]@{
            path = (Resolve-Path -LiteralPath $Path).Path
            sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
            assembly = $assembly.Name.FullName; mvid = $assembly.MainModule.Mvid.ToString()
            types = $types
        }
    } finally { $assembly.Dispose() }
}
$gameTargets = @{
    'Verse.Game' = @('FillComponents')
    'Verse.GameComponent' = '*'
    'Verse.MapComponent' = '*'
    'Verse.ThingComp' = @('.ctor','PostExposeData','PostSpawnSetup','PostDeSpawn','PostDestroy','PreAbsorbStack','PostSplitOff','AllowStackWith')
    'Verse.ThingWithComps' = @('TryAbsorbStack','SplitOff','CanStackWith')
    'Verse.Thing' = @('GetUniqueLoadID','SplitOff','TryAbsorbStack')
    'RimWorld.Need' = @('.ctor','ExposeData','NeedInterval')
    'RimWorld.NeedDef' = '*'
    'RimWorld.Need_Food' = @('NeedInterval')
    'RimWorld.Pawn_NeedsTracker' = @('NeedsTrackerTickInterval','ExposeData')
    'Verse.Scribe_Values' = @('Look')
    'Verse.Scribe_Collections' = @('Look')
    'Verse.Scribe_Deep' = @('Look')
    'Verse.Scribe_References' = @('Look')
    'Verse.DefModExtension' = '*'
    'Verse.Def' = @('GetModExtension')
    'Verse.IThingHolder' = '*'
    'Verse.ThingOwner' = @('TryAddOrTransfer','TryDrop','ExposeData')
    'Verse.ThingOwner`1' = @('.ctor')
    'Verse.AI.ThinkNode_JobGiver' = @('TryGiveJob','TryIssueJobPackage')
    'Verse.AI.ThinkNode_SubtreesByTag' = @('ResolveSubnodes','TryIssueJobPackage')
    'Verse.AI.ThinkNode_PrioritySorter' = @('TryIssueJobPackage')
    'Verse.AI.JobDriver' = @('MakeNewToils','TryMakePreToilReservations','ExposeData','Cleanup')
    'Verse.AI.Pawn_JobTracker' = @('StartJob','EndCurrentJob')
    'Verse.AI.ReservationManager' = @('Reserve','CanReserve','Release','ReleaseClaimedBy')
    'Verse.AI.Toils_Recipe' = @('DoRecipeWork','FinishRecipeAndStartStoringProduct')
    'RimWorld.Bill_Production' = @('Notify_IterationCompleted')
    'Verse.GenRecipe' = @('PostProcessProduct')
    'RimWorld.WorkGiver_DoBill' = @('JobOnThing')
    'RimWorld.Pawn_ApparelTracker' = @('Wear')
    'RimWorld.Pawn_Ownership' = @('get_OwnedRoom','get_OwnedBed')
    'Verse.Pawn' = @('GetDisabledWorkTypes','WorkTypeIsDisabled','get_Downed','get_Dead')
    'Verse.Pawn_HealthTracker' = @('get_Downed','get_Dead','ExposeData')
    'Verse.PawnCapacitiesHandler' = @('GetLevel','CapableOf')
    'Verse.Hediff' = @('get_Visible')
    'Verse.HediffSet' = @('ExposeData')
    'RimWorld.Pawn_SkillTracker' = @('GetSkill')
    'RimWorld.SkillRecord' = @('get_Level','GetLevel')
}
$ruifTargets = @{
    'RUIF.Api' = '*'
    'RUIF.DataAtoms' = @('StatTable')
    'RUIF.PanelAtoms' = @('StatRow')
    'RUIF.ButtonAtoms' = @('PrimaryButton')
    'RUIF.InputAtoms' = @('Checkbox','SearchField','Stepper','Segmented')
    'RUIF.FeedbackAtoms' = @('AlertBanner','EmptyState')
    'RUIF.ProgressAtoms' = @('CapacityBar')
    'RUIF.NavAtomsB' = @('LayerSwitch')
    'RUIF.Layout' = @('Column','Row','Split','BeginScroll','EndScroll')
    'RUIF.VStack' = @('Take','get_Rest')
    'RUIF.WindowFrame' = @('Draw')
}
$sourcePaths = @(
    'Source/PersonalChronicle.csproj', 'About/About.xml',
    'Source/PersonalChronicle/Capture/Patch_BillProduction.cs',
    'Source/PersonalChronicle/Capture/Patch_GenRecipe.cs',
    'Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Runtime.cs',
    'Source/PersonalChronicle/Automation/Patch_WorkshopDutyRuntime.cs',
    'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinColonyEconomyProvider.cs',
    'Source/PersonalChronicle/Data/ChronicleGameComponent.cs'
    'Source/PersonalChronicle/Automation/CompItemStorage.cs'
    'Defs/SystemCenterDefs.xml'
    'Source/PersonalChronicle/Application/SystemCenters/SystemCenterDataRegistry.cs'
    'Source/PersonalChronicle/Automation/WorkshopPostAssignmentService.cs'
)
$sourceHashes = @()
foreach ($relative in $sourcePaths) {
    $path = Join-Path $WorkspaceRoot $relative
    if (!(Test-Path -LiteralPath $path)) { throw "Source missing: $path" }
    $sourceHashes += [ordered]@{ relativePath = $relative; sha256 = (Get-FileHash -LiteralPath $path).Hash }
}
$snapshots = @()
$snapshotFiles = @(
    'Verse/Game.cs', 'Verse/GameComponent.cs', 'Verse/ThingComp.cs',
    'Verse/ThingWithComps.cs', 'Verse/ThingOwner.2.cs', 'Verse/IThingHolder.cs',
    'Verse/AI/ThinkNode_SubtreesByTag.cs', 'Verse/AI/ThinkNode_PrioritySorter.cs',
    'Verse/AI/Toils_Recipe.cs', 'Verse/AI/JobDriver.cs',
    'Verse/AI/ThinkNode_JobGiver.cs', 'Verse/AI/ReservationManager.cs',
    'Verse/GenRecipe.cs', 'RimWorld/Bill_Production.cs',
    'RimWorld/Pawn_NeedsTracker.cs', 'RimWorld/Need.cs',
    'RimWorld/Pawn_ApparelTracker.cs', 'RimWorld/Pawn_Ownership.cs'
    'Verse/Pawn.cs', 'Verse/Pawn_HealthTracker.cs', 'Verse/PawnCapacitiesHandler.cs'
    'Verse/Hediff.cs', 'Verse/HediffSet.cs', 'RimWorld/Pawn_SkillTracker.cs', 'RimWorld/SkillRecord.cs'
)
foreach ($relative in $snapshotFiles) {
    $path = Join-Path $WorkspaceRoot ('_research\vanilla\Assembly-CSharp\' + $relative)
    if (!(Test-Path -LiteralPath $path)) { throw "Decompiled snapshot missing: $path" }
    $snapshots += [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $path).Hash }
}
$xmlFiles = @()
foreach ($name in @('Humanlike.xml','SubTrees_Misc.xml')) {
    $path = Join-Path $GameRoot ('Data\Core\Defs\ThinkTreeDefs\' + $name)
    $xmlFiles += [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $path).Hash }
}
$ruifSources = @()
foreach ($relative in @(
    'Source/Core/Api.cs', 'Source/Layout/Layout.cs',
    'Source/Components/DataAtoms.cs', 'Source/Components/ButtonAtoms.cs',
    'Source/Components/InputAtoms.cs', 'Source/Components/PanelAtoms.cs',
    'Source/Components/FeedbackAtoms.cs', 'Source/Components/ProgressAtoms.cs',
    'Source/Components/NavAtomsB.cs'
)) {
    $path = Join-Path $RuifSourceRoot $relative
    $ruifSources += [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $path).Hash }
}
$evidence = [ordered]@{
    collectedUtc = [DateTime]::UtcNow.ToString('o')
    workspaceRoot = (Resolve-Path -LiteralPath $WorkspaceRoot).Path
    method = 'Read-only Mono.Cecil metadata and IL call operands; no game assembly executed.'
    gameVersion = (Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim()
    game = Read-AssemblyEvidence (Join-Path $GameRoot 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll') $gameTargets
    ruif = Read-AssemblyEvidence (Join-Path $GameRoot 'Mods\RUIF\Assemblies\RUIF.dll') $ruifTargets
    sourceHashes = $sourceHashes
    decompiledSnapshotNotice = 'Existing local snapshots, not freshly decompiled in this task. Interface claims cross-checked against current DLL metadata; selected nested IL calls recorded separately.'
    decompiledSnapshots = $snapshots
    currentGameXml = $xmlFiles
    ruifSourceHashes = $ruifSources
    ruifApiSourceHash = (Get-FileHash -LiteralPath (Join-Path $RuifSourceRoot 'Source\Core\Api.cs')).Hash
}
$parent = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$evidence | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output "Evidence saved: $OutputPath"
Write-Output "Game types: $($evidence.game.types.Count); RUIF types: $($evidence.ruif.types.Count)"
