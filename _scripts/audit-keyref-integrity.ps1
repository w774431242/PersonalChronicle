# 键引用完整性审计（Keyed 定义 ↔ 代码字面量 ↔ Defs 键字段）— 只读
#
# 存在意义（2026-10-06 CHG-20261006-TRKEY 教训）：批量改名时若只核对「键前缀/数量」，
# Source 与 XML 双侧被同一种错误变换一致改写时会被误判为「通过」；本脚本以**定义集为唯一真值**，
# 反向校验每一处引用（代码字面量 + Defs 里的 labelKey/specializationDescKey/displayBonus 等），
# 从而捕获「双侧一致但都错」的情况。
#
# 用法: pwsh -NoProfile -File _scripts/audit-keyref-integrity.ps1 [-Lang ChineseSimplified]
param([string]$Lang = 'ChineseSimplified')
$ErrorActionPreference = 'Stop'
$MOD = Split-Path -Parent $PSScriptRoot

function KeysOf([string]$p) {
    $d = New-Object System.Xml.XmlDocument; $d.Load($p)
    $k = @(); foreach ($n in $d.DocumentElement.ChildNodes) { if ($n.NodeType -eq 'Element') { $k += $n.Name } }
    return $k
}

# ① 定义集（Keyed，按语言）
$defined = @{}
Get-ChildItem "$MOD/Languages/$Lang/Keyed" -File -Filter *.xml | ForEach-Object {
    foreach ($key in @(KeysOf $_.FullName)) { $defined[$key] = 1 }
}
Write-Output ("定义键（{0}）= {1}" -f $Lang, $defined.Count)

# ② 引用集 A：Source 字符串字面量
$litFull = @{}; $litPrefix = @{}
Get-ChildItem "$MOD/Source" -Recurse -File -Filter *.cs | ForEach-Object {
    $t = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
    foreach ($m in [regex]::Matches($t, '"PersonalChronicle\.[A-Za-z0-9_.]*"')) {
        $lit = $m.Value.Trim('"')
        if ($lit.EndsWith('.')) { $litPrefix[$lit] = $_.FullName } else { $litFull[$lit] = $_.FullName }
    }
}
$badA = @($litFull.Keys | Where-Object { -not $defined.ContainsKey($_) })
$badP = @($litPrefix.Keys | Where-Object { $p = $_; -not ($defined.Keys | Where-Object { $_.StartsWith($p) } | Select-Object -First 1) })
Write-Output ("[Source] 完整字面量 = {0}（未定义 {1}）；前缀字面量 = {2}（无任何键匹配 {3}）" -f $litFull.Count, @($badA).Count, $litPrefix.Count, @($badP).Count)
$badA | ForEach-Object { Write-Output ("   MISSING-KEY  {0}   <- {1}" -f $_, $litFull[$_].Replace($MOD, '<MOD>')) }
$badP | ForEach-Object { Write-Output ("   EMPTY-PREFIX {0}   <- {1}" -f $_, $litPrefix[$_].Replace($MOD, '<MOD>')) }

# ③ 引用集 B：Defs 中的键字段值（形如 PersonalChronicle.* 的纯文本）
$badB = @()
Get-ChildItem "$MOD/Defs" -Recurse -File -Filter *.xml | ForEach-Object {
    $file = $_.FullName
    $d = New-Object System.Xml.XmlDocument; $d.Load($file)
    foreach ($n in $d.SelectNodes('//*')) {
        $name = $n.Name
        # 只看「持有翻译键的字段」：*Key / *Keys / displayBonus（thingClass/compClass/driverClass 等是 C# 类型名，天然不含 '.' 前缀判定）
        if ($name -notmatch 'Key$' -and $name -ne 'displayBonus') { continue }
        $v = $n.InnerText
        if ($v -and $v -match '^PersonalChronicle\.[A-Za-z0-9_.]+$') {
            $key = $v.Trim()
            if (-not $defined.ContainsKey($key)) { $badB += [pscustomobject]@{ Key = $key; File = $file; Line = $name } }
        }
    }
}
Write-Output ("[Defs] 未定义的键引用 = {0}" -f @($badB).Count)
$badB | Select-Object -First 20 | ForEach-Object { Write-Output ("   DEF-KEY-MISSING  {0}   (<{1}>)  <- {2}" -f $_.Key, $_.Line, $_.File.Replace($MOD, '<MOD>')) }

# ④ 已知「非翻译键」登记表
#    这些字面量形如 PersonalChronicle.*，但并非 Keyed 键；新增条目必须在此登记并写明类别。
$knownNonKey = @(
    # ---- ProviderId / API 契约标识（Api/DomainProviders）----
    'PersonalChronicle.Builtin.Battle', 'PersonalChronicle.Builtin.Place',
    'PersonalChronicle.Builtin.Production', 'PersonalChronicle.Builtin.Relation',
    'PersonalChronicle.Application.Effects.WorkshopBonusAccess', 'PersonalChronicle.Application.Workshop.WorkshopPaletteAccess',
    'PersonalChronicle.Application.ChroniclePawnAccess', 'PersonalChronicle.Application.ReadModels',
    'PersonalChronicle.Application.EquipmentStateText.TranslationKeyOf',
    'PersonalChronicle.WorkIntensity', 'PersonalChronicle.Domain.ObjectRef', 'PersonalChronicle.Domain.PlaceVisit.PlaceKey',
    'PersonalChronicle.Domain.WorkshopTime', 'PersonalChronicle.Domain.WorkshopCrewCycle',
    'PersonalChronicle.Domain.WorkshopCrewScoring.Score', 'PersonalChronicle.Domain.Career.CareerData',
    'PersonalChronicle.Domain.EquipmentStateResolver', 'PersonalChronicle.Domain.Honor.HonorDisplayKeys.None',
    'PersonalChronicle.Domain.Meal.MealScheduleDefaults.DutyMealPriority',
    # ---- LogOnce/调试分类、类型名、InternalsVisibleTo ----
    'PersonalChronicle.Capture', 'PersonalChronicle.Capture.Patch_GenConstruct', 'PersonalChronicle.Archive.Dialog_RenameWorkplace',
    'PersonalChronicle.Automation.WorkshopDuty', 'PersonalChronicle.Tests', 'PersonalChronicle.DevTest',
    # ---- 约定式「可选补充翻译」前缀（代码先查 CanTranslate，缺失即回退原 Def 文本）----
    'PersonalChronicle.FieldLabel.',
    # ---- 拼接用前缀（真实键为 ...Gap1/2/3，单独前缀不构成键）----
    'PersonalChronicle.UI.MasterControl.Contract.Gap',
    # ---- DevMode 夹具样本文案前缀 ----
    'PersonalChronicle.Dev.SampleGap',
    # ---- 事件流细分类前缀（真实键带后缀）----
    'PersonalChronicle.UI.Ev', 'PersonalChronicle.UI.Events.Sub'
)

# ⑤ 汇总
$unknownA = @($badA | Where-Object { $knownNonKey -notcontains $_ })
$unknownP = @($badP | Where-Object { $knownNonKey -notcontains $_ })
Write-Output ("[登记表] 已知非键引用 = {0}；其中本次命中 {1}" -f $knownNonKey.Count, (@($badA).Count + @($badP).Count - $unknownA.Count - $unknownP.Count))
$unknownP | ForEach-Object { Write-Output ("   FAIL-EMPTY-PREFIX {0}   <- {1}" -f $_, $litPrefix[$_].Replace($MOD, '<MOD>')) }
$total = @($unknownA).Count + @($unknownP).Count + @($badB).Count
Write-Output ("=== 结论: 未登记的未解析引用 = {0}（Defs 键引用未定义 {1}）===" -f $total, @($badB).Count)
if ($total -eq 0) {
    Write-Output 'OK —— 所有键引用均能在定义集中解析；未命中者均为登记过的非键字面量（0 裸键风险）'
} else {
    Write-Output 'FAIL —— 存在未登记的无法解析引用，玩家侧可能显示裸 key'
}
exit $(if ($total -eq 0) { 0 } else { 2 })
