# DefInjected 孤儿注入审计（REL 发布前校验）— 只读
#
# 规则（2026-10-06 GATE-LOC 提出、GATE-COMP 背书）：
#   `Defs/` 中被删除或重命名的 defName，必须全局检索并清理 `Languages/*/DefInjected/**` 中同名路径；
#   命中即 BLOCKED。同源漏删会（a）持续产出 `Found no X named Y` 噪声，
#   （b）一旦该 defName 被其他 mod 复用，我方注入会**真的写到别人的 def 上**（越权覆盖）。
# 先例：CHG-20261006-TRKEY 修掉的 `MainButtonDef/PersonalChronicleArchive`（2026-10-05 移除独立档案馆的漏删资产）。
#
# 用法: pwsh -NoProfile -File _scripts/audit-definject-orphans.ps1
$ErrorActionPreference = 'Stop'
$MOD = Split-Path -Parent $PSScriptRoot

function DefNames([string]$dir) {
    $set = @{}
    Get-ChildItem $dir -Recurse -File -Filter *.xml -ErrorAction SilentlyContinue | ForEach-Object {
        try { $d = New-Object System.Xml.XmlDocument; $d.Load($_.FullName) } catch { return }
        foreach ($n in $d.SelectNodes('//defName')) { if ($n.InnerText) { $set[$n.InnerText.Trim()] = 1 } }
        foreach ($n in $d.SelectNodes('//*[@Name]')) { $v = $n.GetAttribute('Name'); if ($v) { $set[$v] = 1 } }
    }
    return $set
}

$defined = DefNames "$MOD/Defs"
Write-Output ("Defs 中定义的 defName/Name = {0}" -f $defined.Count)

$orphans = @()
Get-ChildItem "$MOD/Languages" -Recurse -File -Filter *.xml | Where-Object { $_.DirectoryName -match '\\DefInjected' } | ForEach-Object {
    $file = $_.FullName
    $defType = Split-Path (Split-Path $file -Parent) -Leaf
    try { $d = New-Object System.Xml.XmlDocument; $d.Load($file) } catch { Write-Output ("   [XML-ERROR] " + $file.Replace($MOD, '<MOD>')); return }
    foreach ($n in $d.DocumentElement.ChildNodes) {
        if ($n.NodeType -ne 'Element') { continue }
        # 路径式键 defName.field… 或嵌套式 <defName><field>
        $defName = ($n.Name -split '\.')[0]
        if (-not $defined.ContainsKey($defName)) {
            $orphans += [pscustomobject]@{ Type = $defType; Def = $defName; File = $file.Replace($MOD, '<MOD>') }
        }
    }
}
Write-Output ("DefInjected 孤儿注入目标 = {0}" -f @($orphans).Count)
$orphans | Sort-Object Type, Def | ForEach-Object { Write-Output ("   ORPHAN  {0}/{1}   <- {2}" -f $_.Type, $_.Def, $_.File) }
if (@($orphans).Count -eq 0) { Write-Output 'OK —— 无孤儿注入（Defs 删除/重命名后的 DefInjected 清理已完成）'; exit 0 }
Write-Output 'BLOCKED —— 存在孤儿注入，发布前必须清理（见本脚本头部规则）'
exit 2
