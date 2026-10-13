# 二级体检：方向↔技能映射 + 每个技能的实际 effect 构成（kind/value）
$ErrorActionPreference = 'Stop'
$p='E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs\ProfessionalSkills.xml'
$x=New-Object System.Xml.XmlDocument; $x.Load($p)

$skillNodes=$x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalSkillDef')
$dirNodes=$x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalDirectionDef')
$effNodes=$x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalEffectDef')

$effIdx=@{}
foreach($e in $effNodes){ $effIdx[$e.defName]=@{kind=$e.kind; val=[double]::Parse($(if($e.value){$e.value}else{'0'}))} }

function GetList($n,$t){ if($n.$t){ return @($n.$t.ChildNodes|ForEach-Object{$_.InnerText}) } return @() }

# 1) 方向 → skillDefNames
Write-Host "=== 48 方向各自的 skillDefNames（指向的技能）==="
$dirSkillMap=@{}
foreach($d in $dirNodes){
    $sk=GetList $d skillDefNames
    $dirSkillMap[$d.defName]=$sk
    $maj=if($d.profession){$d.profession}else{'?'}
    Write-Host ("  {0,-30} major={1,-16} order={2,-3} skillDefNames=[{3}]" -f $d.defName,$maj,$(if($d.order){$d.order}else{'?'}),($sk -join ','))
}

# 2) 12 技能的实际 effect 构成
Write-Host ""
Write-Host "=== 12 技能：direction 归属 + effect 明细 ==="
$skillByDir=@{}
foreach($s in $skillNodes){
    $sd=$s.defName
    $declDir=if($s.direction){$s.direction}else{''}
    $effs=GetList $s effectDefNames
    $effDetail=@()
    foreach($ed in $effs){
        if($effIdx.ContainsKey($ed)){ $effDetail += ($ed+':'+$effIdx[$ed].kind+'='+$effIdx[$ed].val) }
        else { $effDetail += ($ed+':<未定义>') }
    }
    $skillByDir[$declDir]=$sd
    Write-Host ("  {0,-40}" -f $sd)
    Write-Host ("      declaredDir={0,-28} eff=[{1}]" -f $declDir,($effDetail -join ' | '))
}

# 3) 交叉验证：每个方向能否通过 (def.direction == 方向defName) 找到技能
Write-Host ""
Write-Host "=== 交叉验证：方向→可匹配技能（按 UI 现有逻辑 def.direction==方向defName）==="
$hit=0;$miss=0
foreach($d in $dirNodes){
    if($skillByDir.ContainsKey($d.defName)){ $hit++ }
    else { $miss++; Write-Host ("  [MISS] {0,-30} skillDefNames=[{1}]" -f $d.defName,($dirSkillMap[$d.defName] -join ',')) }
}
Write-Host ("  能匹配: {0}/48   不能匹配(将空快照/折叠): {1}/48" -f $hit,$miss)
