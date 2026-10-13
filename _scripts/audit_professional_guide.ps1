# 体检：专业成长蓝图可能"缺漏"的数据面（所有 ProfessionalSkillDef × ProfessionalEffectDef）
# 注意：XML 节点为全限定名 PersonalChronicle.Domain.Profession.*
$ErrorActionPreference = 'Stop'
$xmlPath = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs\ProfessionalSkills.xml'
$x = New-Object System.Xml.XmlDocument
$x.Load($xmlPath)

$skillNodes = $x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalSkillDef')
$effNodes   = $x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalEffectDef')
$rateNodes  = $x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalRatingDef')
$dirNodes   = $x.GetElementsByTagName('PersonalChronicle.Domain.Profession.ProfessionalDirectionDef')

$effIdx = @{}
foreach ($e in $effNodes) {
    $dn = $e.defName
    $kind = if ($e.kind) { $e.kind } else { '' }
    $val = if ($e.value) { $e.value } else { '0' }
    $effIdx[$dn] = @{ kind = $kind; value = [double]::Parse($val) }
}

Write-Host "=== 4 档 RatingDef（全 mod 通用权重）==="
$rateList = @()
foreach ($r in $rateNodes) {
    $rateList += [pscustomobject]@{
        defName=$r.defName
        minLevel=if($r.minLevel){$r.minLevel}else{'?'}
        workSpeedWeight=if($r.workSpeedWeight){$r.workSpeedWeight}else{'?'}
        qualityBiasWeight=if($r.qualityBiasWeight){$r.qualityBiasWeight}else{'?'}
        order=if($r.order){$r.order}else{99999}
    }
}
foreach ($r in ($rateList | Sort-Object { [int]$_.order })) {
    Write-Host ("  {0,-34} minLevel={1,-4} workSpeedWeight={2,-6} qualityBiasWeight={3}" -f $r.defName,$r.minLevel,$r.workSpeedWeight,$r.qualityBiasWeight)
}

Write-Host ""
Write-Host ("=== 全部 ProfessionalSkillDef 体检（共 {0} 个 / 方向 {1} 个）===" -f $skillNodes.Count, $dirNodes.Count)

$total = $skillNodes.Count
$cSource=0;$cWorkType=0;$cRecipe=0;$cXp=0;$cEff=0
$cNoXpSourceAtAll=0
$cNoWorkSpeed=0
$cNoQuality=0
$problemRows=@()

function GetList { param($node,$tag) if($node.$tag){ return @($node.$tag.ChildNodes | ForEach-Object{$_.InnerText}) } return @() }

foreach ($s in $skillNodes) {
    $dn=$s.defName
    $dir=if($s.direction){$s.direction}else{''}
    $srcList=GetList $s sourceSkills
    $workList=GetList $s practiceWorkTypeDefNames
    $recList=GetList $s practiceRecipeDefNames
    $xpBase=if($s.xpPerPracticeBase){[double]::Parse($s.xpPerPracticeBase)}else{0.0}
    $effList=GetList $s effectDefNames

    $hasSrc=($srcList.Count -gt 0)
    $hasWork=($workList.Count -gt 0)
    $hasRec=($recList.Count -gt 0)
    $hasXp=($xpBase -gt 0)
    $hasEff=($effList.Count -gt 0)
    if($hasSrc){$cSource++}
    if($hasWork){$cWorkType++}
    if($hasRec){$cRecipe++}
    if($hasXp){$cXp++}
    if($hasEff){$cEff++}

    $noXpSource=(-not $hasSrc) -and (-not $hasWork) -and (-not $hasRec) -and (-not $hasXp)
    if($noXpSource){$cNoXpSourceAtAll++}

    $hasSpeed=$false;$hasQuality=$false
    foreach($ed in $effList){
        if($effIdx.ContainsKey($ed)){
            $k=$effIdx[$ed].kind
            if($k -eq 'WorkSpeed' -or $k -eq 'StatBonus' -or $k -eq 'CombatStat' -or $k -eq 'YieldBonus'){$hasSpeed=$true}
            if($k -eq 'QualityBias'){$hasQuality=$true}
        }
    }
    if(-not $hasSpeed){$cNoWorkSpeed++}
    if(-not $hasQuality){$cNoQuality++}

    if($noXpSource -or (-not $hasSpeed) -or (-not $hasQuality)){
        $problemRows += [pscustomobject]@{
            defName=$dn; direction=$dir
            src=if($hasSrc){$srcList.Count}else{0}
            work=if($hasWork){$workList.Count}else{0}
            rec=if($hasRec){$recList.Count}else{0}
            xp=$xpBase
            eff=if($hasEff){$effList.Count}else{0}
            speed=$hasSpeed; quality=$hasQuality
        }
    }
}

Write-Host ""
Write-Host "=== 填充率 ==="
Write-Host ("  总技能数            : {0}" -f $total)
Write-Host ("  sourceSkills 有     : {0} ({1:p0})" -f $cSource,($cSource/$total))
Write-Host ("  practiceWorkType 有 : {0} ({1:p0})" -f $cWorkType,($cWorkType/$total))
Write-Host ("  practiceRecipe 有   : {0} ({1:p0})" -f $cRecipe,($cRecipe/$total))
Write-Host ("  xpPerPracticeBase>0 : {0} ({1:p0})" -f $cXp,($cXp/$total))
Write-Host ("  effectDefNames 有   : {0} ({1:p0})" -f $cEff,($cEff/$total))
Write-Host ""
Write-Host ("  触发 XpFromEmpty(三项源空+无XP) : {0}" -f $cNoXpSourceAtAll)
Write-Host ("  评级 SpeedPct 全0(缺 WorkSpeed类) : {0}" -f $cNoWorkSpeed)
Write-Host ("  评级 QualityLevels 全0(缺 QualityBias): {0}" -f $cNoQuality)

Write-Host ""
Write-Host "=== 问题技能清单 ==="
if($problemRows.Count -eq 0){ Write-Host "  (无)" }
else {
    foreach($p in $problemRows){
        Write-Host ("  {0,-44} dir={1,-28} src={2} work={3} rec={4} xp={5} eff={6} speed={7} quality={8}" -f `
            $p.defName,$p.direction,$p.src,$p.work,$p.rec,$p.xp,$p.eff,$p.speed,$p.quality)
    }
}
