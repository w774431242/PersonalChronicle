# 综合体检：扫描整个 Defs 目录，合并所有 ProfessionalSkillDef / ProfessionalEffectDef / ProfessionalDirectionDef
$ErrorActionPreference='Stop'
$dir='E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs'
$files=Get-ChildItem -Path $dir -Filter '*.xml' -Recurse | Where-Object{ $_.Name -like '*Professional*' -or $_.Name -like '*Skill*' }

$skillDefs=@{}   # defName -> direction
$effDefs=@{}     # defName -> kind
$dirDefs=@{}     # defName -> (profession, skillDefNames[])

foreach($f in $files){
    $x=New-Object System.Xml.XmlDocument
    try { $x.Load($f.FullName) } catch { Write-Host ("[LOAD FAIL] "+$f.Name); continue }
    $ns='PersonalChronicle.Domain.Profession.'
    $sk=$x.GetElementsByTagName($ns+'ProfessionalSkillDef')
    foreach($s in $sk){
        $dn=$s.defName
        $skillDefs[$dn]=if($s.direction){$s.direction}else{''}
    }
    $ef=$x.GetElementsByTagName($ns+'ProfessionalEffectDef')
    foreach($e in $ef){
        $effDefs[$e.defName]=if($e.kind){$e.kind}else{'(空)'}
    }
    $dd=$x.GetElementsByTagName($ns+'ProfessionalDirectionDef')
    foreach($d in $dd){
        $sd=@()
        if($d.skillDefNames){ $sd=@($d.skillDefNames.ChildNodes|ForEach-Object{$_.InnerText}) }
        $dirDefs[$d.defName]=@{prof=if($d.profession){$d.profession}else{''}; skills=$sd}
    }
}

Write-Host ("扫描文件数: "+$files.Count)
Write-Host ("ProfessionalSkillDef 总数: "+$skillDefs.Count)
Write-Host ("ProfessionalEffectDef 总数: "+$effDefs.Count)
Write-Host ("ProfessionalDirectionDef 总数: "+$dirDefs.Count)

# 交叉：每个方向引用的 skillDefName 是否都存在
$missDir=0; $okDir=0
foreach($dk in $dirDefs.Keys){
    $info=$dirDefs[$dk]
    $allExist=$true
    foreach($sn in $info.skills){ if(-not $skillDefs.ContainsKey($sn)){ $allExist=$false } }
    if($allExist){$okDir++} else {$missDir++; Write-Host ("  [方向缺技能] $dk -> skills=[$($info.skills -join ',')]")}
}
Write-Host ("方向→技能 全部存在: $okDir / 缺失: $missDir")

# 交叉：每个 skill 引用的 effectDefName 是否都存在
$missEff=0; $okEff=0
foreach($skn in $skillDefs.Keys){
    # 需要读 skill 的 effectDefNames；重新扫一遍太重，改为统计 effectDefs 覆盖
    $okEff++
}
Write-Host ""
Write-Host "=== effectDef kind 分布 ==="
$kc=@{}
foreach($k in $effDefs.Values){ if(-not $kc.ContainsKey($k)){$kc[$k]=0} $kc[$k]++ }
foreach($kv in $kc.GetEnumerator()){ Write-Host ("  kind={0,-14} 数量={1}" -f $kv.Key,$kv.Value) }

# skill 引用的 effect 是否存在（需要读 skill 的 effectDefNames，单独扫）
Write-Host ""
Write-Host "=== skill 引用的 effect 存在性 ==="
$missSkillEff=0
foreach($f in $files){
    $x=New-Object System.Xml.XmlDocument
    try { $x.Load($f.FullName) } catch { continue }
    $sk=$x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalSkillDef')
    foreach($s in $sk){
        $dn=$s.defName
        if($s.effectDefNames){
            foreach($ed in $s.effectDefNames.ChildNodes){
                $en=$ed.InnerText
                if(-not $effDefs.ContainsKey($en)){
                    $missSkillEff++
                    Write-Host ("  [技能缺effect] $dn 引用 $en (未定义)")
                }
            }
        }
    }
}
Write-Host ("技能引用未定义 effect 总数: $missSkillEff")
