# 体检 3：ProfessionalEffectDef 真实 kind 值分布 + 每个技能引用的 effect 实际 kind
$ErrorActionPreference='Stop'
$x=New-Object System.Xml.XmlDocument
$x.Load('E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs\ProfessionalSkills.xml')

$effNodes=$x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalEffectDef')
$skillNodes=$x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalSkillDef')

function GetList($n,$t){ if($n.$t){ return @($n.$t.ChildNodes|ForEach-Object{$_.InnerText}) } return @() }

Write-Host "=== 全部 ProfessionalEffectDef 的 (defName | kind | value) ==="
$kindCount=@{}
foreach($e in $effNodes){
    $k=if($e.kind){$e.kind}else{'(空)'}
    $v=if($e.value){$e.value}else{'-'}
    if(-not $kindCount.ContainsKey($k)){$kindCount[$k]=0}
    $kindCount[$k]++
    Write-Host ("  {0,-44} kind={1,-14} value={2}" -f $e.defName,$k,$v)
}
Write-Host ""
Write-Host "=== kind 分布统计 ==="
foreach($kv in $kindCount.GetEnumerator()){ Write-Host ("  kind={0,-14} 数量={1}" -f $kv.Key,$kv.Value) }

Write-Host ""
Write-Host "=== 每个技能的 effect 实际 kind（决定评级表能否显示速度/品质）==="
$effIdx=@{}
foreach($e in $effNodes){ $effIdx[$e.defName]=$e.kind }
foreach($s in $skillNodes){
    $effs=GetList $s effectDefNames
    $kinds=@()
    foreach($ed in $effs){ if($effIdx.ContainsKey($ed)){ $kinds += $effIdx[$ed] } else { $kinds += '<undef>' } }
    $hasSpeed=($kinds -contains 'WorkSpeed') -or ($kinds -contains 'StatBonus') -or ($kinds -contains 'CombatStat') -or ($kinds -contains 'YieldBonus')
    $hasQuality=($kinds -contains 'QualityBias')
    Write-Host ("  {0,-42} -> kinds=[{1}]  speed={2} quality={3}" -f $s.defName,($kinds -join ','),$hasSpeed,$hasQuality)
}
