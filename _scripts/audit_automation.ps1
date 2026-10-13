# 自动化体系（车间/农机/传送带/组件）资产与代码体检
# 用法：.\_scripts\audit_automation.ps1 [-SkipSync]
# 检查项：
#   A. XML 合法性（单根 + 无 BOM）
#   B. ThingDef 契约（thingClass / 建筑 selectable+altitudeLayer）
#   C. 贴图资产（texPath 引用存在、尺寸合规、四角透明）
#   D. 传送带 12 张四向齐全
#   E. Keyed 翻译键（代码引用 vs 中英定义）
#   F. DefInjected 覆盖（每个自动化 defName 有中文/英文注入）
#   G. 开发区 <-> 发布区 哈希一致
param(
    [switch]$SkipSync
)

$ErrorActionPreference = 'Stop'
$dev = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
$rel = 'E:\SteamLibrary\steamapps\common\RimWorld\Mods\殖民地综合管理系统'
$issues = New-Object System.Collections.Generic.List[string]
$checks = 0

function Warn([string]$msg)
{
    $script:issues.Add($msg) | Out-Null
    Write-Output "  [FAIL] $msg"
}
function Pass([string]$msg) { Write-Output "  [ok]   $msg" }

Write-Output '=== A. XML 合法性 ==='
$xmlFiles = @(
    'Defs\Automation_AutoFarm.xml',
    'Defs\Automation_Conveyor.xml',
    'Defs\Automation_FarmComponents.xml',
    'Defs\Automation_Research.xml',
    'Defs\Automation_WorkType.xml',
    'Defs\Automation_Jobs.xml',
    'Patches\AutomationZone.xml',
    'Languages\ChineseSimplified\Keyed\Automation.xml',
    'Languages\English\Keyed\Automation.xml',
    'Languages\ChineseSimplified\DefInjected\ThingDef\Things_Automation.xml',
    'Languages\English\DefInjected\ThingDef\Things_Automation.xml',
    'Languages\ChineseSimplified\DefInjected\RecipeDef\Automation.xml',
    'Languages\English\DefInjected\RecipeDef\Automation.xml',
    'Languages\ChineseSimplified\DefInjected\ResearchProjectDef\AutomationResearch.xml',
    'Languages\English\DefInjected\ResearchProjectDef\AutomationResearch.xml',
    'Languages\ChineseSimplified\DefInjected\ResearchTabDef\AutomationResearch.xml',
    'Languages\English\DefInjected\ResearchTabDef\AutomationResearch.xml',
    'Languages\ChineseSimplified\DefInjected\WorkTypeDef\AutomationWork.xml',
    'Languages\English\DefInjected\WorkTypeDef\AutomationWork.xml',
    'Languages\ChineseSimplified\DefInjected\JobDef\AutomationJobs.xml',
    'Languages\English\DefInjected\JobDef\AutomationJobs.xml',
    'Languages\ChineseSimplified\DefInjected\WorkGiverDef\AutomationJobs.xml',
    'Languages\English\DefInjected\WorkGiverDef\AutomationJobs.xml'
)
$automationXml = @()
foreach ($f in $xmlFiles)
{
    $checks++
    $p = Join-Path $dev $f
    if (-not (Test-Path $p)) { Warn "XML 缺失: $f"; continue }
    $bytes = [IO.File]::ReadAllBytes($p)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { Warn "XML 带 BOM: $f" }
    try
    {
        $x = New-Object System.Xml.XmlDocument
        $x.Load($p)
        $automationXml += , @($f, $x)
        Pass "$f (root=$($x.DocumentElement.Name))"
    }
    catch { Warn "XML 解析失败: $f -> $($_.Exception.Message)" }
}

Write-Output '=== B. ThingDef 契约 ==='
foreach ($entry in $automationXml)
{
    $f = $entry[0]; $x = $entry[1]
    if ($f -notlike 'Defs\*') { continue }
    foreach ($d in $x.Defs.ChildNodes)
    {
        if ($d.Name -ne 'ThingDef') { continue }
        $checks++
        $id = if ($d.defName) { $d.defName } else { $d.Name }
        if (-not $d.thingClass -and -not $d.ParentName) { Warn "$id 缺 thingClass 且无 ParentName（进档崩溃级）" }
        if ($d.category -eq 'Building')
        {
            if (-not $d.selectable) { Warn "$id 建筑缺 selectable（无法选中交互）" }
            if (-not $d.altitudeLayer) { Warn "$id 建筑缺 altitudeLayer" }
        }
    }
}
Pass 'ThingDef 契约检查完成'

Write-Output '=== B2. Def 类型引用核对（Class= 是否存在于程序集） ==='
# 根因背景：CompProperties_PowerPlantSolar 在 1.6 已删除，XML 引用不存在的类型
# 会让整个 ThingDef 加载即抛异常被丢弃（建造菜单"缺大型机"事故，资产审计查不出）。
# 方法：把 Assembly-CSharp.dll + 本 mod dll 当字节流，检索类型名 ASCII 串。
# 局限：.NET 元数据将 namespace 与类名分开存储，点号全名不连续——带命名空间的类型
# 按末段类名检索（双库任一命中即可）。
$asmGame = 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll'
$asmMod = Join-Path $dev 'Assemblies\PersonalChronicle.dll'
$gameText = [System.Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($asmGame))
$modText = if (Test-Path $asmMod) { [System.Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($asmMod)) } else { '' }
$classRefs = New-Object System.Collections.Generic.HashSet[string]
foreach ($entry in $automationXml)
{
    $entry[1].SelectNodes('//*[@Class]') | ForEach-Object { [void]$classRefs.Add($_.Class) }
}
foreach ($t in $classRefs)
{
    $checks++
    $found = $gameText.Contains($t)
    if (-not $found -and $t.Contains('.'))
    {
        $shortName = ($t -split '\.')[-1]
        $found = $gameText.Contains($shortName) -or $modText.Contains($shortName)
    }
    if (-not $found) { Warn "Def 引用的类型不存在于程序集: $t（Def 将加载失败被丢弃）" }
}
Pass ('Def 类型引用核对完成（{0} 个 Class 引用）' -f $classRefs.Count)

Write-Output '=== C. 贴图资产 ==='
$texDefs = @('Defs\Automation_AutoFarm.xml', 'Defs\Automation_Conveyor.xml', 'Defs\Automation_FarmComponents.xml')
$expectSize = @{
    'PC_AutoFarm_S' = @(64, 64); 'PC_AutoFarm_M' = @(128, 128); 'PC_AutoFarm_L' = @(256, 256)
}
Add-Type -AssemblyName System.Drawing
foreach ($entry in $automationXml)
{
    $f = $entry[0]; $x = $entry[1]
    if ($f -notin @('Defs\Automation_AutoFarm.xml', 'Defs\Automation_Conveyor.xml', 'Defs\Automation_FarmComponents.xml')) { continue }
    foreach ($d in $x.Defs.ChildNodes)
    {
        if ($d.Name -ne 'ThingDef' -or -not $d.defName) { continue }
        $tex = $d.graphicData.texPath
        if (-not $tex) { continue }
        $checks++
        $p = Join-Path $dev ('Textures\' + $tex + '.png')
        if (-not (Test-Path $p)) { Warn "$($d.defName) 贴图缺失: $tex.png"; continue }
        $img = New-Object System.Drawing.Bitmap($p)
        $dimOk = $true
        if ($expectSize.ContainsKey($d.defName))
        {
            $ew = $expectSize[$d.defName][0]; $eh = $expectSize[$d.defName][1]
            $dimOk = (($img.Width -eq $ew -and $img.Height -eq $eh) -or ($img.Width -eq $ew * 2 -and $img.Height -eq $eh * 2))
        }
        else { $dimOk = ($img.Width -eq 64 -and $img.Height -eq 64) }
        if (-not $dimOk) { Warn "$($d.defName) 贴图尺寸异常: $($img.Width)x$($img.Height)" }
        $alphaBad = $false
        foreach ($pt in @(@(0, 0), @(($img.Width - 1), 0), @(0, ($img.Height - 1)), @(($img.Width - 1), ($img.Height - 1))))
        {
            if ($img.GetPixel($pt[0], $pt[1]).A -gt 12) { $alphaBad = $true }
        }
        if ($alphaBad) { Warn "$($d.defName) 贴图四角不透明（白底残留）" }
        if ($dimOk -and -not $alphaBad) { Pass "$($d.defName) <- $tex.png $($img.Width)x$($img.Height)" }
        $img.Dispose()
    }
}

Write-Output '=== D. 传送带四向 ==='
$checks++
$convDir = Join-Path $dev 'Textures\Things\Building\Automation'
$need = @()
foreach ($base in @('PC_Conveyor_Start', 'PC_Conveyor_Mid', 'PC_Conveyor_End'))
{
    foreach ($suf in @('', '_east', '_south', '_west'))
    {
        $need += ($base + $suf + '.png')
    }
}
$missing = $need | Where-Object { -not (Test-Path (Join-Path $convDir $_)) }
if ($missing.Count -gt 0) { Warn ("传送带四向缺失: " + ($missing -join ', ')) }
else { Pass "传送带 12 张齐全（$($need.Count)）" }

Write-Output '=== E. Keyed 翻译键 ==='
$code = ''
Get-ChildItem (Join-Path $dev 'Source\PersonalChronicle\Automation') -Filter *.cs | ForEach-Object { $code += [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8) }
$cnKeyed = [IO.File]::ReadAllText((Join-Path $dev 'Languages\ChineseSimplified\Keyed\Automation.xml'), [Text.Encoding]::UTF8)
$enKeyed = [IO.File]::ReadAllText((Join-Path $dev 'Languages\English\Keyed\Automation.xml'), [Text.Encoding]::UTF8)
$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in [regex]::Matches($code, 'Tr?\("([A-Za-z.]+)"\)')) { [void]$used.Add($m.Groups[1].Value) }
foreach ($m in [regex]::Matches($code, 'KeyPrefix \+ "([A-Za-z]+)"')) { [void]$used.Add($m.Groups[1].Value) }
foreach ($m in [regex]::Matches($code, '"(PersonalChronicle\.Automation\.[A-Za-z.]+)"'))
{
    $lit = $m.Groups[1].Value
    # 排除非翻译常量：Keyed 前缀常量（尾带点）与工时来源标识
    if ($lit.EndsWith('.')) { continue }
    if ($lit -eq 'PersonalChronicle.Automation.WorkshopDuty') { continue }
    [void]$used.Add($lit)
}
foreach ($u in $used)
{
    $checks++
    if ($u -like 'PersonalChronicle.Automation.*')
    {
        if ($cnKeyed -notmatch [regex]::Escape("<$u>")) { Warn "中文缺键: $u" }
        if ($enKeyed -notmatch [regex]::Escape("<$u>")) { Warn "英文缺键: $u" }
    }
    else
    {
        if ($cnKeyed -notmatch "\.Tab\.$u>") { Warn "中文缺键: Tab.$u" }
        if ($enKeyed -notmatch "\.Tab\.$u>") { Warn "英文缺键: Tab.$u" }
    }
}
Pass "Keyed 键引用 $($used.Count) 个已核对"

Write-Output '=== F. DefInjected 覆盖 ==='
$diMap = @(
    @('Defs\Automation_FarmComponents.xml', 'ThingDef', 'DefInjected\ThingDef\Things_Automation.xml', 'label'),
    @('Defs\Automation_AutoFarm.xml', 'ThingDef', 'DefInjected\ThingDef\Things_Automation.xml', 'label'),
    @('Defs\Automation_Conveyor.xml', 'ThingDef', 'DefInjected\ThingDef\Things_Automation.xml', 'label'),
    @('Defs\Automation_FarmComponents.xml', 'RecipeDef', 'DefInjected\RecipeDef\Automation.xml', 'label'),
    @('Defs\Automation_Research.xml', 'ResearchProjectDef', 'DefInjected\ResearchProjectDef\AutomationResearch.xml', 'label'),
    @('Defs\Automation_Research.xml', 'ResearchTabDef', 'DefInjected\ResearchTabDef\AutomationResearch.xml', 'label'),
    # WorkTypeDef 没有 label 字段（是 labelShort）；JobDef 没有 label（只有 reportString）
    @('Defs\Automation_WorkType.xml', 'WorkTypeDef', 'DefInjected\WorkTypeDef\AutomationWork.xml', 'labelShort'),
    @('Defs\Automation_Jobs.xml', 'JobDef', 'DefInjected\JobDef\AutomationJobs.xml', 'reportString'),
    @('Defs\Automation_Jobs.xml', 'WorkGiverDef', 'DefInjected\WorkGiverDef\AutomationJobs.xml', 'label')
)
foreach ($rule in $diMap)
{
    $srcFile = $rule[0]; $defType = $rule[1]; $diFile = $rule[2]; $keyField = $rule[3]
    $src = $automationXml | Where-Object { $_[0] -eq $srcFile } | Select-Object -First 1
    if (-not $src) { continue }
    $checks++
    $diPath = Join-Path $dev ("Languages\ChineseSimplified\" + $diFile)
    if (-not (Test-Path $diPath)) { Warn "缺中文注入文件: $diFile ($defType)"; continue }
    $di = [IO.File]::ReadAllText($diPath, [Text.Encoding]::UTF8)
    $names = $src[1].Defs.ChildNodes | Where-Object { $_.Name -eq $defType -and $_.defName } | ForEach-Object { $_.defName }
    $miss = @()
    foreach ($n in $names)
    {
        if ($di -notmatch [regex]::Escape("<$n.$keyField>")) { $miss += $n }
    }
    if ($miss.Count -gt 0) { Warn "中文注入缺 $keyField ($defType): $($miss -join ', ')" }
    else { Pass "$defType 中文注入齐全 ($($names.Count) 项, 键=$keyField)" }
}

Write-Output '=== G. 两区一致性 ==='
if ($SkipSync) { Write-Output '  [跳过] -SkipSync' }
else
{
    foreach ($f in $xmlFiles)
    {
        $checks++
        $s = Join-Path $dev $f
        if (-not (Test-Path $s)) { continue }
        $d = Join-Path $rel $f
        if (-not (Test-Path $d)) { Warn "发布区缺失: $f"; continue }
        if ((Get-FileHash $s -Algorithm SHA256).Hash -ne (Get-FileHash $d -Algorithm SHA256).Hash) { Warn "两区不一致: $f" }
    }
    foreach ($sub in @('Textures\Things\Building\AutoFarm', 'Textures\Things\Building\Automation', 'Textures\Things\Item\AutoFarm'))
    {
        foreach ($f in Get-ChildItem (Join-Path $dev $sub) -Filter *.png)
        {
            $checks++
            $relF = Join-Path (Join-Path $rel $sub) $f.Name
            if (-not (Test-Path $relF)) { Warn "发布区缺失贴图: $sub\$($f.Name)"; continue }
            if ((Get-FileHash $f.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $relF -Algorithm SHA256).Hash) { Warn "贴图两区不一致: $sub\$($f.Name)" }
        }
    }
    $checks++
    $dllS = Join-Path $dev 'Assemblies\PersonalChronicle.dll'
    $dllR = Join-Path $rel 'Assemblies\PersonalChronicle.dll'
    if ((Get-FileHash $dllS -Algorithm SHA256).Hash -ne (Get-FileHash $dllR -Algorithm SHA256).Hash) { Warn 'dll 两区不一致（编译后未同步）' }
    else { Pass 'dll 两区一致' }
}

Write-Output ''
Write-Output ("=== 结果：检查 $($checks) 项，问题 $($issues.Count) 个 ===")
if ($issues.Count -gt 0) { $issues | ForEach-Object { Write-Output "  - $_" } }
