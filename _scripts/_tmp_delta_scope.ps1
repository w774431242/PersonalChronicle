# 临时脚本（GATE-DELTA 范围外清单生成，任务完成后删除）
$ErrorActionPreference = 'Stop'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$prevOut = [Console]::OutputEncoding
[Console]::OutputEncoding = $utf8NoBom
$prevPSOut = $OutputEncoding
$OutputEncoding = $utf8NoBom

$root = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
Set-Location $root
$tmp = Join-Path $root '.codebuddy\tmp'
if (-not (Test-Path $tmp)) { New-Item -ItemType Directory -Path $tmp | Out-Null }

$tracked = @(& git -c core.quotePath=false diff --name-only HEAD)
$untracked = @(& git -c core.quotePath=false ls-files --others --exclude-standard)
$all = @($tracked + $untracked) | Where-Object { $_ -ne '' } | Sort-Object -Unique
[System.IO.File]::WriteAllLines((Join-Path $tmp 'shop-all-changes.txt'), $all, $utf8NoBom)

# 本批清单（必须与 Change-Annotation front matter 的 path 一致）
$mine = @(
    'Source/PersonalChronicle/Application/Effects/WorkshopBonusData.cs',
    'Source/PersonalChronicle/Application/Effects/WorkshopBonusAccess.cs',
    'Source/PersonalChronicle/Application/EquipmentStateText.cs',
    'Source/PersonalChronicle/Application/Workshop/WorkshopProductionEffects.cs',
    'Source/PersonalChronicle/Domain/Workshop/WorkshopProductionRules.cs',
    'Source/PersonalChronicle/Automation/WorkshopBonusAccessBinder.cs',
    'Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs',
    'Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.Tabs.cs',
    'Source/PersonalChronicle/Automation/WorkshopManageView.Schedule.cs',
    'Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.Draw.cs',
    'Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Palette.cs',
    'Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Device.cs',
    'Source/PersonalChronicle/Effects/StatPart_WorkshopWorkSpeed.cs',
    'Source/PersonalChronicle/Effects/StatPart_WorkshopResearchSpeed.cs',
    'Source/PersonalChronicle/Effects/StatPart_WorkshopProduct.cs',
    'Source/PersonalChronicle/Effects/WorkshopStatPartInjector.cs',
    'Source/PersonalChronicle/Capture/Patch_GenRecipe.cs',
    'Source/PersonalChronicle/Automation/WorkshopBonusResolver.cs',
    'Source/PersonalChronicle/Automation/WorkshopProductRegistry.cs',
    'Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs',
    'Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.cs',
    'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs',
    'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Dev.cs',
    'Source/PersonalChronicle/Domain/EquipmentState.cs',
    'Source/PersonalChronicle/Domain/WorkshopDeviceReadings.cs',
    'Source/PersonalChronicle/Automation/WorkshopManageView.cs',
    'Source/PersonalChronicle/Automation/WorkshopHandoverBoard.cs',
    'Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.cs',
    'Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.cs',
    'Source/PersonalChronicle/Automation/WorkshopDeviceStateResolver.cs',
    'Source/PersonalChronicle/Automation/IWorkshopDevice.cs',
    'Source/PersonalChronicle/Automation/Zone_Workshop.cs',
    'Source/PersonalChronicle/Automation/Dialog_LogisticsManage.cs',
    'Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinManufacturingProvider.Events.cs',
    'Tests/WorkshopProductionRulesTests.cs',
    'Tests/WorkshopProductRegistryTests.cs',
    'Tests/EquipmentStateResolverTests.cs',
    'Languages/English/DefInjected/WorkshopTypeDef/WorkshopTypes.xml',
    'Languages/ChineseSimplified/Keyed/Automation.xml',
    'Languages/English/Keyed/Automation.xml',
    'Languages/ChineseSimplified/Keyed/Archive.xml',
    'Languages/English/Keyed/Archive.xml',
    'docs/设计文档/架构/审计-车间体系-2026-09-21.md',
    'docs/设计文档/架构/EXC-2026-010.md',
    'docs/设计文档/架构/EXC-2026-003.md',
    'docs/设计文档/架构/EXC-2026-005.md',
    'docs/设计文档/架构/Change-Annotation-CHG-SHOP-AUDIT-001.md',
    'docs/changelog/v1.4.x_2026-09-21_车间体系审计修复.md'
)

$mineSet = @{}
foreach ($m in $mine) { $mineSet[$m.ToLowerInvariant()] = $true }

$outside = @($all | Where-Object { -not $mineSet.ContainsKey($_.ToLowerInvariant()) })
$outFile = Join-Path $tmp 'shop-out-of-scope.txt'
$lines = @('# CHG-SHOP-AUDIT-001 范围外（工作树中其它批次/历史未提交改动）') + $outside
[System.IO.File]::WriteAllLines($outFile, $lines, $utf8NoBom)

$missing = @($mine | Where-Object { $all -notcontains $_ })
Write-Host ("[scope] all={0} mine={1} outside={2}" -f $all.Count, $mine.Count, $outside.Count)
if ($missing.Count -gt 0) {
    Write-Host ("[scope][WARN] 本批清单未在 diff 中出现（" + $missing.Count + " 项）：")
    $missing | ForEach-Object { Write-Host ("   - " + $_) }
} else {
    Write-Host '[scope] 本批清单 100% 命中 diff'
}

[Console]::OutputEncoding = $prevOut
$OutputEncoding = $prevPSOut
