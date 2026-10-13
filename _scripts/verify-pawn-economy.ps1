param(
    [string]$RimWorldPath = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$InstitutionDefsPath = '',
    [string]$ContractDefsPath = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $root 'InspectTypes\bin\Release\net8.0\Mono.Cecil.dll')
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $RimWorldPath 'RimWorldWin64_Data\Managed'))
$resolver.AddSearchDirectory((Join-Path $root 'Assemblies'))
$options = [Mono.Cecil.ReaderParameters]::new()
$options.AssemblyResolver = $resolver
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $RimWorldPath 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'), $options)
$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'Assemblies\PersonalChronicle.dll'), $options)
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Fields($type) {
    $names = @()
    while ($null -ne $type) {
        $names += @($type.Fields | ForEach-Object Name)
        $names += @($type.Properties | ForEach-Object Name)
        $type = if ($null -ne $type.BaseType) { $type.BaseType.Resolve() } else { $null }
    }
    return $names
}
function EnumFields($type) {
    $enums = @{}
    while ($null -ne $type) {
        foreach ($field in $type.Fields) {
            if ($field.FieldType.IsArray -or $field.FieldType.IsGenericInstance) { continue }
            $resolved = $field.FieldType.Resolve()
            if ($null -ne $resolved -and $resolved.IsEnum) {
                $enums[$field.Name] = @($resolved.Fields | Where-Object IsLiteral | ForEach-Object Name)
            }
        }
        $type = if ($null -ne $type.BaseType) { $type.BaseType.Resolve() } else { $null }
    }
    return $enums
}
if (-not $InstitutionDefsPath) { $InstitutionDefsPath = Join-Path $root 'Defs\PawnEconomy_Institutions.xml' }
if (-not $ContractDefsPath) { $ContractDefsPath = Join-Path $root 'Defs\PawnEconomy_WorkContract.xml' }
$definitions = [xml](Get-Content -LiteralPath $InstitutionDefsPath -Raw -Encoding UTF8)
$contractDefinitions = [xml](Get-Content -LiteralPath $ContractDefsPath -Raw -Encoding UTF8)
$count = 0
$enumCount = 0
foreach ($node in @($definitions.Defs.ChildNodes) + @($contractDefinitions.Defs.ChildNodes)) {
    if ($node.NodeType -ne 'Element') { continue }
    $gameType = $game.MainModule.GetType('Verse.' + $node.LocalName)
    if ($null -eq $gameType) { $gameType = $game.MainModule.GetType('RimWorld.' + $node.LocalName) }
    Require ($null -ne $gameType) ('Unknown Def type: ' + $node.LocalName)
    $names = Fields $gameType
    $enumFields = EnumFields $gameType
    foreach ($field in $node.ChildNodes) {
        if ($field.NodeType -ne 'Element') { continue }
        Require ($names -contains $field.LocalName) ('Unknown ' + $node.LocalName + ' field: ' + $field.LocalName)
        if ($enumFields.ContainsKey($field.LocalName)) {
            # Verse Enum.Parse is case-sensitive. Field existence alone cannot catch a fatal Def value typo.
            Require ($enumFields[$field.LocalName] -ccontains $field.InnerText.Trim()) (
                'Invalid enum value: ' + $field.LocalName + '=' + $field.InnerText +
                ' (expected exact case: ' + ($enumFields[$field.LocalName] -join ', ') + ')')
            $enumCount++
        }
        if ($field.LocalName -in @('thingClass', 'driverClass', 'giverClass')) {
            Require ($null -ne $mod.MainModule.GetType($field.InnerText)) ('Missing compiled class: ' + $field.InnerText)
        }
        if ($field.LocalName -eq 'building') {
            $nested = Fields ($game.MainModule.GetType('RimWorld.BuildingProperties'))
            foreach ($entry in $field.ChildNodes) { Require ($nested -contains $entry.LocalName) ('Unknown BuildingProperties field: ' + $entry.LocalName) }
        }
    }
    $count++
}
$paper = @($contractDefinitions.Defs.ThingDef | Where-Object { $_.defName -eq 'PC_PEA_WorkContractDocument' })
Require ($paper.Count -eq 1) 'Expected one physical contract paper Def.'
if ([double]::Parse([string]$paper[0].statBases.Flammability, [Globalization.CultureInfo]::InvariantCulture) -gt 0) {
    Require ([string]$paper[0].useHitPoints -ceq 'true' -and [int]$paper[0].statBases.MaxHitPoints -gt 0) 'Flammable contract paper must have hitpoints.'
}
$vanilla = [xml](Get-Content -LiteralPath (Join-Path $RimWorldPath 'Data\Core\Defs\ThinkTreeDefs\SubTrees_Misc.xml') -Raw)
$patch = [xml](Get-Content -LiteralPath (Join-Path $root 'Patches\PawnEconomy_ThinkTree.xml') -Raw)
$targets = $vanilla.SelectNodes($patch.Patch.Operation.xpath)
Require ($targets.Count -eq 1) 'Economic ThinkTree XPath must match exactly one vanilla sorter.'
Require ($null -ne $mod.MainModule.GetType($patch.Patch.Operation.value.li.Class)) 'Missing ThinkNode implementation.'
Require (@($definitions.Defs.ThingDef | Where-Object { $_.defName }).Count -eq 5) 'Expected five physical institutions.'
Require (@($definitions.Defs.JobDef).Count -eq 4) 'Expected four concrete JobDefs.'
$keys = @{}
foreach ($language in @('ChineseSimplified', 'English')) {
    $document = [xml](Get-Content -LiteralPath (Join-Path $root "Languages\$language\Keyed\PawnEconomy.xml") -Raw -Encoding UTF8)
    $keys[$language] = @($document.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    Require ($keys[$language].Count -eq @($keys[$language] | Sort-Object -Unique).Count) 'Duplicate economic translation keys.'
}
Require (@(Compare-Object $keys.ChineseSimplified $keys.English).Count -eq 0) 'Chinese/English key mismatch.'
# This legacy check resolves T() under PC_PEA.; the new presenter's PC_PEA.Needs.
# keys are verified independently (including placeholders) by verify-pawn-needs-ui.ps1.
# 排除"自带 T 前缀"的呈现器：它们用**另一套**键命名空间（`Planning.*` / 需求面板），由
# verify-pawn-needs-ui.ps1 独立校验（含占位符）—— 本脚本只校验 `PC_PEA.*`。
# C-1（2026-10-10）：`ShadowPlanPresenter.cs` 与 `NeedDashboardPresenter.cs` 同属此类
# （其 `T(key)` = `text.Text("Planning." + key)`），此前漏排除 ⇒ `T("Unknown")` 被误判为缺键。
# C-3（CHG-20261012 · B1，2026-10-11）：`Dialog_WorkContract.cs` 是第三类 —— 它**自带前缀**
# `PC_PEA.Dialog.WorkContract.`（`KeyPrefix` 常量）。此前既不排除也不认前缀 ⇒ 它的每个
# `T("Title")` 都被按 `PC_PEA.Title` 查、必然缺键（该脚本自 CONTRACTIZE 段起恒 BLOCKED，
# 而恒红的检查等于没有检查）。修法：按文件给**前缀映射**（并保留逐文件归属，报错能定位到文件）。
# C-4：存在性口径修正 —— 经济域的 Keyed **不止一个文件**（PawnEconomy.xml / PawnEconomyStorage.xml /
# EconomyMode.xml …）。旧口径只查 PawnEconomy.xml ⇒ 一旦扫描面放宽（收点号键），定义在别的 Keyed 文件里的
# 键（如 `PC_PEA.Mode.Explanation` 在 `EconomyMode.xml`）会被**误判为缺键**。正解：先并入全部英文 Keyed 键。
$englishAll = @{}
foreach ($keyedFile in Get-ChildItem -LiteralPath (Join-Path $root 'Languages\English\Keyed') -Filter '*.xml') {
    $keyed = [xml](Get-Content -LiteralPath $keyedFile.FullName -Raw -Encoding UTF8)
    if ($null -eq $keyed.LanguageData) { continue }
    foreach ($name in @($keyed.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)) {
        $englishAll[$name] = $keyedFile.Name
    }
}
$prefixByFile = @{ 'Dialog_WorkContract.cs' = 'PC_PEA.Dialog.WorkContract.'; 'Dialog_WorkPrices.cs' = 'PC_PEA.WorkPrices.'; 'Thing_WorkContractDocument.cs' = '' }
$scanned = 0
# C-4 续：`PawnEconomyFactsPresenter.cs` 同属"自带前缀"族（`T(key) => text.Text("Planning." + key)`，
# 实际键形 `PC_PEA.Needs.Planning.*`，定义在 `PawnEconomyPlanning.xml`，由 verify-pawn-needs-ui.ps1 校验）。
# 旧正则漏点号键 ⇒ 该文件此前从未入网；放宽扫描面后必须一并排除，否则是**把既有不改的键当成缺键**。
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $root 'Source\PersonalChronicle\Economy\PawnEconomy') -Filter '*.cs' |
        Where-Object { $_.Name -notin @('NeedDashboardPresenter.cs', 'ShadowPlanPresenter.cs', 'PawnEconomyFactsPresenter.cs') })) {
    # Partial presenter files share the parent's actual Planning namespace.
    # Keep them in the check; missing real keys must still fail validation.
    $prefix = if ($file.Name -like 'ShadowPlanPresenter.*.cs') { 'PC_PEA.Needs.Planning.' }
        elseif ($prefixByFile.ContainsKey($file.Name)) { $prefixByFile[$file.Name] } else { 'PC_PEA.' }
    $text = Get-Content -LiteralPath $file.FullName -Raw
    # C-4（CHG-20261012 · B1，GATE-LOC 席 2026-10-11 指出）：旧正则 `T\("([^".]+)"\)` 的字符类**排除点号**
    # ⇒ 所有含 `.` 的键（本工程绝大多数，包括本次 9 新键）根本不入网，等于"恒绿的检查"。改为
    # `[TW]\("([^"]+)"\)`：① 收点号键；② 收 `W("…")`（窗口包装 = `TranslatedOrId("PC_PEA." + key, key)`）；
    # ③ 动态拼装（`T("A." + x)`）因结尾不是 `")` 天然不匹配，故不会被误报为缺键。
    foreach ($match in [regex]::Matches($text, '[TW]\("([^"]+)"\)')) {
        $scanned++
        $fullKey = $prefix + $match.Groups[1].Value
        Require ($englishAll.ContainsKey($fullKey)) ('Missing key: ' + $fullKey + ' referenced in ' + $file.Name)
    }
}
# C-4：储物键文件此前**完全不在网内**（键集奇偶 / 重复键 / 关键键在位都要查 —— B1 在此删过 2 键）。
foreach ($language in @('ChineseSimplified', 'English')) {
    $storage = [xml](Get-Content -LiteralPath (Join-Path $root "Languages\$language\Keyed\PawnEconomyStorage.xml") -Raw -Encoding UTF8)
    $storageKeys = @($storage.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name)
    Require ($storageKeys.Count -eq @($storageKeys | Sort-Object -Unique).Count) ('Duplicate storage keys: ' + $language)
    if (-not $storageNames) { $storageNames = @{} }
    $storageNames[$language] = $storageKeys
}
Require (@(Compare-Object $storageNames.ChineseSimplified $storageNames.English).Count -eq 0) 'Storage Chinese/English key mismatch.'
Require ($storageNames.English -contains 'PersonalChronicle.Storage.PublicZoneActive') 'Storage key PublicZoneActive missing.'
Require (-not ($storageNames.English -contains 'PersonalChronicle.Storage.NoQuote')) 'Removed storage key NoQuote is back.'
Require (-not ($storageNames.English -contains 'PersonalChronicle.Storage.PriceValue')) 'Removed storage key PriceValue is back.'
# 末批（删聚合窗口 2026-10-11）：RUIF 断言改指**幸存宿主**（政策页内容层 —— 它现在是本域最大的 RUIF 使用者）。
$ui = Get-Content -LiteralPath (Join-Path $root 'Source\PersonalChronicle\Economy\PawnEconomy\Dialog_WorkContract.cs') -Raw
Require ($ui -notmatch 'Widgets\.|GUI\.') 'Economic dialog must use RUIF controls.'
# C-1（2026-10-10）：滚动容器实现自 P0-6 起在 EconomyRowList.cs（行族）—— 断言指向实现所在文件，
# 否则实现搬家即假失败（旧口径指向对话框主文件，自那之后恒不满足，GATE-UI 已指出）。
$rowListSource = Get-Content -LiteralPath (Join-Path $root 'Source\PersonalChronicle\Economy\PawnEconomy\EconomyRowList.cs') -Raw
Require ($ui -match 'RUIF.ButtonAtoms' -and $rowListSource -match 'RUIF.Layout.BeginScroll') 'RUIF frontend missing.'
$game.Dispose(); $mod.Dispose()
Write-Output "PASS: $count Def nodes; $enumCount exact-case enum values; flammable paper hitpoints; 5 institutions; 4 institution Jobs + 3 contract Jobs; exact ThinkTree XPath; compiled classes; $($keys.English.Count) bilingual keys; $scanned T() references; RUIF frontend."
