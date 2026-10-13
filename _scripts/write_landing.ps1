$ErrorActionPreference = "Stop"
$Root = "E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统"

function Xe([string]$s) { if ($null -eq $s) { return "" }; return (($s -replace '&','&amp;') -replace '<','&lt;') -replace '>','&gt;' }

# skill, dir, profession, source, work, effect, kind, value, stat, isRed, labelKey, enLabel, enDesc
$rows = @(
    @("ProfessionalSkill_WeaponryManufacturing","Manufacturing_Weaponry","Manufacturing","Crafting","Smithing","ProfessionalEffect_ManufacturingWorkSpeed","WorkSpeed","0.05","","0","Professional.Skill.PrecisionManufacturing.EffectWorkSpeed","Weapon Crafting","Professional weapon-crafting skill grown from smithing."),
    @("ProfessionalSkill_EquipmentManufacturing","Manufacturing_Equipment","Manufacturing","Crafting","Smithing","ProfessionalEffect_QualityBias","QualityBias","1","","0","Professional.Skill.PrecisionManufacturing.EffectQualityBias","Equipment Crafting","Professional apparel and armor crafting skill."),
    @("ProfessionalSkill_IndustrialManufacturing","Manufacturing_Industrial","Manufacturing","Crafting","Smithing","ProfessionalEffect_ManufacturingWorkSpeed","WorkSpeed","0.04","","0","Professional.Skill.PrecisionManufacturing.EffectWorkSpeed","Industrial Crafting","Professional component and industrial crafting skill."),
    @("ProfessionalSkill_EngineeringPower","Engineering_Power","Engineering","Construction","Construction","ProfessionalEffect_Engineering_Power","StatBonus","0.04","ConstructionSpeed","0","Professional.Effect.Engineering_Power.Signature","Power Engineering","Professional construction skill focused on power systems."),
    @("ProfessionalSkill_EngineeringControl","Engineering_Control","Engineering","Construction","Construction","ProfessionalEffect_Engineering_Control","StatBonus","0.03","ConstructionSpeed","0","Professional.Effect.Engineering_Control.Signature","Control Engineering","Professional construction skill focused on control systems."),
    @("ProfessionalSkill_EngineeringPrecision","Engineering_Precision","Engineering","Construction","Construction","ProfessionalEffect_Engineering_Precision","StatBonus","0.04","ConstructionSpeed","0","Professional.Effect.Engineering_Precision.Signature","Precision Engineering","Professional construction skill focused on precision assembly."),
    @("ProfessionalSkill_AgricultureIrrigation","Agriculture_Irrigation","Agriculture","Plants","Growing","ProfessionalEffect_Agriculture_Irrigation","YieldBonus","0.10","DrugHarvestYield","0","Professional.Effect.Agriculture_Irrigation.Signature","Irrigation","Professional growing skill focused on irrigation and drug crops."),
    @("ProfessionalSkill_AgricultureBreeding","Agriculture_Breeding","Agriculture","Plants","Growing","ProfessionalEffect_Agriculture_Breeding","YieldBonus","0.08","PlantHarvestYield","0","Professional.Effect.Agriculture_Breeding.Signature","Crop Breeding","Professional growing skill focused on cultivar quality."),
    @("ProfessionalSkill_AgricultureGreenhouse","Agriculture_Greenhouse","Agriculture","Plants","Growing","ProfessionalEffect_Agriculture_Greenhouse","YieldBonus","0.08","PlantHarvestYield","0","Professional.Effect.Agriculture_Greenhouse.Signature","Greenhouse Horticulture","Professional growing skill focused on greenhouse yield."),
    @("ProfessionalSkill_ForestryHarvest","Forestry_Harvest","Forestry","Plants","Growing","ProfessionalEffect_Forestry_Harvest","StatBonus","0.10","PlantWorkSpeed","0","Professional.Effect.Forestry_Harvest.Signature","Timber Harvest","Professional plant-cutting skill for timber harvest."),
    @("ProfessionalSkill_ForestryNursery","Forestry_Nursery","Forestry","Plants","Growing","ProfessionalEffect_Forestry_Nursery","StatBonus","0.08","PlantWorkSpeed","0","Professional.Effect.Forestry_Nursery.Signature","Nursery","Professional growing skill for seedling and stand tending."),
    @("ProfessionalSkill_ForestryLumber","Forestry_Lumber","Forestry","Plants","Growing","ProfessionalEffect_Forestry_Lumber","StatBonus","0.08","PlantWorkSpeed","0","Professional.Effect.Forestry_Lumber.Signature","Lumber Processing","Professional wood-processing skill."),
    @("ProfessionalSkill_ForestrySilviculture","Forestry_Silviculture","Forestry","Plants","Growing","ProfessionalEffect_Forestry_Silviculture","YieldBonus","0.08","PlantHarvestYield","0","Professional.Effect.Forestry_Silviculture.Signature","Silviculture","Professional growing skill for wild woodland yield."),
    @("ProfessionalSkill_AnimalHusbandryHusbandry","AnimalHusbandry_Husbandry","AnimalHusbandry","Animals","Handling","ProfessionalEffect_AnimalHusbandry_Husbandry","StatBonus","0.10","TameAnimalChance","0","Professional.Effect.AnimalHusbandry_Husbandry.Signature","Animal Husbandry","Professional handling skill for taming and husbandry."),
    @("ProfessionalSkill_AnimalHusbandryVeterinary","AnimalHusbandry_Veterinary","AnimalHusbandry","Animals","Handling","ProfessionalEffect_AnimalHusbandry_Veterinary","StatBonus","0.10","TrainAnimalChance","0","Professional.Effect.AnimalHusbandry_Veterinary.Signature","Veterinary","Professional handling skill for training and animal health."),
    @("ProfessionalSkill_AnimalHusbandryPasture","AnimalHusbandry_Pasture","AnimalHusbandry","Animals","Handling","ProfessionalEffect_AnimalHusbandry_Pasture","YieldBonus","0.10","AnimalGatherYield","0","Professional.Effect.AnimalHusbandry_Pasture.Signature","Pasture Management","Professional handling skill for gathering yield."),
    @("ProfessionalSkill_MedicineSurgery","Medicine_Surgery","Medicine","Medicine","Doctor","ProfessionalEffect_Medicine_Surgery","StatBonus","0.10","MedicalSurgerySuccessChance","0","Professional.Effect.Medicine_Surgery.Signature","Surgery","Professional medicine skill focused on surgical success."),
    @("ProfessionalSkill_MedicinePharmacy","Medicine_Pharmacy","Medicine","Medicine","Doctor","ProfessionalEffect_Medicine_Pharmacy","StatBonus","0.08","MedicalTendSpeed","0","Professional.Effect.Medicine_Pharmacy.Signature","Pharmacy","Professional medicine skill focused on tend speed."),
    @("ProfessionalSkill_MedicinePrevention","Medicine_Prevention","Medicine","Medicine","Doctor","ProfessionalEffect_Medicine_Prevention","StatBonus","0.10","ImmunityGainSpeed","0","Professional.Effect.Medicine_Prevention.Signature","Prevention","Professional medicine skill focused on immunity."),
    @("ProfessionalSkill_WeaponsHeavy","Weapons_Heavy","Weapons","Shooting","Hunting","ProfessionalEffect_Weapons_Heavy","CombatStat","0.04","ShootingAccuracyPawn","0","Professional.Skill.Weapons.WorkSpeed","Heavy Weapons","Professional shooting skill focused on heavy weapons."),
    @("ProfessionalSkill_WeaponsAmmo","Weapons_Ammo","Weapons","Crafting","Smithing","ProfessionalEffect_Weapons_Ammo","StatBonus","0.04","WorkSpeedGlobal","0","Professional.Skill.Weapons.WorkSpeed","Ammunition Engineering","Professional crafting skill focused on ammunition."),
    @("ProfessionalSkill_MiningUnderground","Mining_Underground","Mining","Mining","Mining","ProfessionalEffect_Mining_Underground","StatBonus","0.10","MiningSpeed","0","Professional.Effect.Mining_Underground.Signature","Underground Mining","Professional mining skill focused on deep drills."),
    @("ProfessionalSkill_MiningBeneficiation","Mining_Beneficiation","Mining","Mining","Mining","ProfessionalEffect_Mining_Beneficiation","StatBonus","0.08","MiningSpeed","0","Professional.Effect.Mining_Beneficiation.Signature","Ore Dressing","Professional mining skill focused on ore quality."),
    @("ProfessionalSkill_MiningBlasting","Mining_Blasting","Mining","Mining","Mining","ProfessionalEffect_Mining_Blasting","StatBonus","0.12","MiningSpeed","0","Professional.Effect.Mining_Blasting.Signature","Blasting","Professional mining skill focused on extraction rate."),
    @("ProfessionalSkill_ResearchExperiment","Research_Experiment","Research","Intellectual","Research","ProfessionalEffect_Research_Experiment","StatBonus","0.08","ResearchSpeed","0","Professional.Effect.Research_Experiment.Signature","Experimental Research","Professional research skill focused on experiment quality."),
    @("ProfessionalSkill_ResearchApplied","Research_Applied","Research","Intellectual","Research","ProfessionalEffect_Research_Applied","StatBonus","0.10","ResearchSpeed","0","Professional.Effect.Research_Applied.Signature","Applied Research","Professional research skill focused on project speed."),
    @("ProfessionalSkill_ResearchLiterature","Research_Literature","Research","Intellectual","Research","ProfessionalEffect_Research_Literature","StatBonus","0.10","GlobalLearningFactor","0","Professional.Effect.Research_Literature.Signature","Literature Research","Professional research skill focused on learning factor."),
    @("ProfessionalSkill_CookingCuisine","Cooking_Cuisine","Cooking","Cooking","Cooking","ProfessionalEffect_Cooking_Cuisine","StatBonus","0.10","FoodPoisonChance","1","Professional.Effect.Cooking_Cuisine.Signature","Cuisine","Professional cooking skill focused on fine meals."),
    @("ProfessionalSkill_CookingBaking","Cooking_Baking","Cooking","Cooking","Cooking","ProfessionalEffect_Cooking_Baking","StatBonus","0.08","FoodPoisonChance","1","Professional.Effect.Cooking_Baking.Signature","Baking","Professional cooking skill focused on baked goods."),
    @("ProfessionalSkill_CookingPreservation","Cooking_Preservation","Cooking","Cooking","Cooking","ProfessionalEffect_Cooking_Preservation","StatBonus","0.08","FoodPoisonChance","1","Professional.Effect.Cooking_Preservation.Signature","Food Preservation","Professional cooking skill focused on food safety."),
    @("ProfessionalSkill_ArtPainting","Art_Painting","Art","Artistic","Art","ProfessionalEffect_Art_Painting","StatBonus","0.08","GeneralLaborSpeed","0","Professional.Effect.Art_Painting.Signature","Painting","Professional art skill focused on beauty."),
    @("ProfessionalSkill_ArtMusic","Art_Music","Art","Artistic","Art","ProfessionalEffect_Art_Music","StatBonus","0.08","SocialImpact","0","Professional.Effect.Art_Music.Signature","Music","Professional art skill focused on social impact."),
    @("ProfessionalSkill_ArtCraft","Art_Craft","Art","Artistic","Art","ProfessionalEffect_Art_Craft","StatBonus","0.10","TradePriceImprovement","0","Professional.Effect.Art_Craft.Signature","Craft Art","Professional art skill focused on trade price."),
    @("ProfessionalSkill_ManagementDispatch","Management_Dispatch","Management","Social","Warden","ProfessionalEffect_Management_Dispatch","StatBonus","0.08","MoveSpeed","0","Professional.Effect.Management_Dispatch.Signature","Dispatch","Professional social skill focused on movement and caravans."),
    @("ProfessionalSkill_ManagementLogistics","Management_Logistics","Management","Social","Warden","ProfessionalEffect_Management_Logistics","StatBonus","0.10","CarryingCapacity","0","Professional.Effect.Management_Logistics.Signature","Logistics","Professional social skill focused on carrying capacity."),
    @("ProfessionalSkill_ManagementPersonnel","Management_Personnel","Management","Social","Warden","ProfessionalEffect_Management_Personnel","StatBonus","0.08","SocialImpact","0","Professional.Effect.Management_Personnel.Signature","Personnel","Professional social skill focused on social impact.")
)

$existingEffects = @(
    @("ProfessionalEffect_Engineering_Structure","StatBonus","0.05","ConstructionSpeed","0","Professional.Effect.Engineering_Structure.Signature"),
    @("ProfessionalEffect_Agriculture_Tillage","YieldBonus","0.10","PlantHarvestYield","0","Professional.Effect.Agriculture_Tillage.Signature"),
    @("ProfessionalEffect_Mining_Openpit","StatBonus","0.10","MiningSpeed","0","Professional.Effect.Mining_Openpit.Signature"),
    @("ProfessionalEffect_Research_Theory","StatBonus","0.10","GlobalLearningFactor","0","Professional.Effect.Research_Theory.Signature"),
    @("ProfessionalEffect_Cooking_Staple","StatBonus","0.10","FoodPoisonChance","1","Professional.Effect.Cooking_Staple.Signature"),
    @("ProfessionalEffect_Medicine_Clinical","StatBonus","0.10","MedicalTendQuality","0","Professional.Effect.Medicine_Clinical.Signature"),
    @("ProfessionalEffect_Management_Diplomacy","StatBonus","0.10","TradePriceImprovement","0","Professional.Effect.Management_Diplomacy.Signature"),
    @("ProfessionalEffect_Art_Sculpture","StatBonus","0.08","GeneralLaborSpeed","0","Professional.Effect.Art_Sculpture.Signature"),
    @("ProfessionalEffect_AnimalHusbandry_Breeding","StatBonus","0.08","TrainAnimalChance","0","Professional.Effect.AnimalHusbandry_Breeding.Signature"),
    @("ProfessionalEffect_Weapons_Ranged","CombatStat","0.05","ShootingAccuracyPawn","0","Professional.Skill.Weapons.WorkSpeed"),
    @("ProfessionalEffect_Weapons_Melee","CombatStat","0.05","MeleeHitChance","0","Professional.Skill.Weapons.WorkSpeed")
)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<Defs>')
[void]$sb.AppendLine('  <!-- 二级方向垂直切片落地：其余方向技能 + 数值效果（注入原版 StatDef）。 -->')

$seen = New-Object 'System.Collections.Generic.HashSet[string]'
[void]$seen.Add("ProfessionalEffect_ManufacturingWorkSpeed")
[void]$seen.Add("ProfessionalEffect_QualityBias")

function Write-Effect($name, $kind, $value, $stat, $isRed, $labelKey) {
    if (-not $script:seen.Add($name)) { return }
    [void]$script:sb.AppendLine("  <PersonalChronicle.Domain.Profession.ProfessionalEffectDef>")
    [void]$script:sb.AppendLine("    <defName>$name</defName>")
    [void]$script:sb.AppendLine("    <kind>$kind</kind>")
    [void]$script:sb.AppendLine("    <value>$value</value>")
    if ($stat) { [void]$script:sb.AppendLine("    <statDefName>$stat</statDefName>") }
    if ($isRed -eq "1") { [void]$script:sb.AppendLine("    <isReduction>true</isReduction>") }
    [void]$script:sb.AppendLine("    <labelKey>$labelKey</labelKey>")
    [void]$script:sb.AppendLine("  </PersonalChronicle.Domain.Profession.ProfessionalEffectDef>")
}

foreach ($e in $existingEffects) { Write-Effect $e[0] $e[1] $e[2] $e[3] $e[4] $e[5] }
foreach ($r in $rows) { Write-Effect $r[5] $r[6] $r[7] $r[8] $r[9] $r[10] }

foreach ($r in $rows) {
    $skill,$dir,$prof,$source,$work,$effect,$kind,$value,$stat,$isRed,$labelKey,$enLabel,$enDesc = $r
    [void]$sb.AppendLine("  <PersonalChronicle.Domain.Profession.ProfessionalSkillDef>")
    [void]$sb.AppendLine("    <defName>$skill</defName>")
    [void]$sb.AppendLine("    <label>$(Xe $enLabel)</label>")
    [void]$sb.AppendLine("    <description>$(Xe $enDesc)</description>")
    [void]$sb.AppendLine("    <profession>$prof</profession>")
    [void]$sb.AppendLine("    <direction>$dir</direction>")
    [void]$sb.AppendLine("    <sourceSkills>")
    [void]$sb.AppendLine("      <li>$source</li>")
    [void]$sb.AppendLine("    </sourceSkills>")
    if ($work) {
        [void]$sb.AppendLine("    <practiceWorkTypeDefNames>")
        [void]$sb.AppendLine("      <li>$work</li>")
        [void]$sb.AppendLine("    </practiceWorkTypeDefNames>")
    }
    [void]$sb.AppendLine("    <xpPerPracticeBase>10</xpPerPracticeBase>")
    [void]$sb.AppendLine("    <xpDifficulty>1</xpDifficulty>")
    [void]$sb.AppendLine("    <xpCap>5000</xpCap>")
    [void]$sb.AppendLine("    <maxLevel>50</maxLevel>")
    [void]$sb.AppendLine("    <effectDefNames>")
    [void]$sb.AppendLine("      <li>$effect</li>")
    [void]$sb.AppendLine("    </effectDefNames>")
    if ($kind -eq "WorkSpeed" -and $value -ne "0.03") {
        [void]$sb.AppendLine("    <effectOverrides>")
        [void]$sb.AppendLine("      <li>")
        [void]$sb.AppendLine("        <effectDefName>$effect</effectDefName>")
        [void]$sb.AppendLine("        <hasValue>true</hasValue>")
        [void]$sb.AppendLine("        <value>$value</value>")
        [void]$sb.AppendLine("      </li>")
        [void]$sb.AppendLine("    </effectOverrides>")
    }
    [void]$sb.AppendLine("    <qualificationTags>")
    [void]$sb.AppendLine("      <li>$($dir.Replace('_',''))</li>")
    [void]$sb.AppendLine("    </qualificationTags>")
    [void]$sb.AppendLine("  </PersonalChronicle.Domain.Profession.ProfessionalSkillDef>")
}
[void]$sb.AppendLine("</Defs>")
[void]$sb.AppendLine("")
[IO.File]::WriteAllText((Join-Path $Root "Defs\ProfessionalSkills.Landing.xml"), $sb.ToString())
Write-Host "Landing xml written"

function Append-Keys([string]$path, $pairs) {
    if (-not (Test-Path $path)) { return }
    $text = [IO.File]::ReadAllText($path)
    $insert = New-Object System.Collections.Generic.List[string]
    foreach ($p in $pairs) {
        $k = $p[0]; $v = $p[1]
        if ($text.Contains("<$k>")) { continue }
        $insert.Add("  <$k>$(Xe $v)</$k>") | Out-Null
    }
    if ($insert.Count -eq 0) { return }
    $trimmed = $text.TrimEnd()
    if (-not $trimmed.EndsWith("</LanguageData>")) { throw "no LanguageData close in $path" }
    $trimmed = $trimmed.Substring(0, $trimmed.Length - "</LanguageData>".Length).TrimEnd()
    $newText = $trimmed + "`n" + ($insert -join "`n") + "`n</LanguageData>`n"
    [IO.File]::WriteAllText($path, $newText)
    Write-Host "Appended $($insert.Count) keys to $path"
}

$cn = @(
    @("PersonalChronicle.UI.Career.Honor.SelectDisplay","选择显示称号"),
    @("PersonalChronicle.UI.Career.Honor.SelectDisplay.Tip","从已获职称与勋章中选择一条显示在姓名旁；顶栏只显示这一条，避免叠加。"),
    @("PersonalChronicle.UI.Career.Honor.Select.Title","选择显示称号"),
    @("PersonalChronicle.UI.Career.Honor.Select.Hint","地图名牌与殖民者栏只显示一条称号。未选择时自动取主方向最高职称，其次取最高档勋章。"),
    @("PersonalChronicle.UI.Career.Honor.Select.Auto","自动（主方向最高职称 / 最高勋章）"),
    @("PersonalChronicle.UI.Career.Honor.Select.None","不显示称号"),
    @("PersonalChronicle.UI.Career.Honor.Select.Group.Title","已获职称"),
    @("PersonalChronicle.UI.Career.Honor.Select.Group.Medal","已获勋章"),
    @("PersonalChronicle.UI.Career.Honor.Select.Empty","尚无已获职称或勋章。"),
    @("PersonalChronicle.UI.Career.Honor.Select.Done","已选择显示：{0}"),
    @("PersonalChronicle.UI.Career.Honor.Select.Cleared","已恢复自动选择。"),
    @("PersonalChronicle.UI.Career.Honor.Select.Hidden","已隐藏人物称号。"),
    @("PersonalChronicle.UI.Career.Honor.Select.Current","当前显示"),
    @("PersonalChronicle.UI.Career.Honor.Select.Current.Auto","自动"),
    @("PersonalChronicle.UI.Career.Honor.Select.Current.None","隐藏"),
    @("PersonalChronicle.UI.Career.Honor.Select.NoCareer","该殖民者尚无职业档案，无法保存显示称号。"),
    @("Professional.Effect.Engineering_Structure.Signature","建造速度 +5%"),
    @("Professional.Effect.Engineering_Power.Signature","建造速度 +4%"),
    @("Professional.Effect.Engineering_Control.Signature","建造速度 +3%"),
    @("Professional.Effect.Engineering_Precision.Signature","建造速度 +4%"),
    @("Professional.Skill.ProfessionalSkill_StructuralEngineering.Label","结构工程"),
    @("Professional.Skill.ProfessionalSkill_StructuralEngineering.Desc","由原版建造工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_TillageAgriculture.Label","耕作栽培"),
    @("Professional.Skill.ProfessionalSkill_TillageAgriculture.Desc","由原版种植工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_OreExtraction.Label","矿石开采"),
    @("Professional.Skill.ProfessionalSkill_OreExtraction.Desc","由原版采矿工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_TheoryResearch.Label","理论研究"),
    @("Professional.Skill.ProfessionalSkill_TheoryResearch.Desc","由原版智识工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_StapleCooking.Label","日常主食"),
    @("Professional.Skill.ProfessionalSkill_StapleCooking.Desc","由原版烹饪工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_ClinicalMedicine.Label","临床医学"),
    @("Professional.Skill.ProfessionalSkill_ClinicalMedicine.Desc","由原版医疗工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_DiplomacyManagement.Label","外交谈判"),
    @("Professional.Skill.ProfessionalSkill_DiplomacyManagement.Desc","由原版社交工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_SculptureArt.Label","雕塑艺术"),
    @("Professional.Skill.ProfessionalSkill_SculptureArt.Desc","由原版艺术工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_AnimalBreeding.Label","良种繁育"),
    @("Professional.Skill.ProfessionalSkill_AnimalBreeding.Desc","由原版畜牧工作持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_RangedWeaponry.Label","远程枪械"),
    @("Professional.Skill.ProfessionalSkill_RangedWeaponry.Desc","由原版射击技能持续提供成长来源的专业技能。"),
    @("Professional.Skill.ProfessionalSkill_MeleeWeaponry.Label","近战锻造"),
    @("Professional.Skill.ProfessionalSkill_MeleeWeaponry.Desc","由原版近战技能持续提供成长来源的专业技能。")
)
$en = @(
    @("PersonalChronicle.UI.Career.Honor.SelectDisplay","Choose displayed title"),
    @("PersonalChronicle.UI.Career.Honor.SelectDisplay.Tip","Pick one granted title or medal to show beside the name. The colonist bar shows only this entry."),
    @("PersonalChronicle.UI.Career.Honor.Select.Title","Displayed title"),
    @("PersonalChronicle.UI.Career.Honor.Select.Hint","Map badges and the colonist bar show a single honor. Auto uses the highest title for the primary direction, then the highest medal."),
    @("PersonalChronicle.UI.Career.Honor.Select.Auto","Auto (highest primary-direction title / highest medal)"),
    @("PersonalChronicle.UI.Career.Honor.Select.None","Hide title"),
    @("PersonalChronicle.UI.Career.Honor.Select.Group.Title","Granted titles"),
    @("PersonalChronicle.UI.Career.Honor.Select.Group.Medal","Granted medals"),
    @("PersonalChronicle.UI.Career.Honor.Select.Empty","No granted titles or medals yet."),
    @("PersonalChronicle.UI.Career.Honor.Select.Done","Now displaying: {0}"),
    @("PersonalChronicle.UI.Career.Honor.Select.Cleared","Restored automatic title."),
    @("PersonalChronicle.UI.Career.Honor.Select.Hidden","Pawn title hidden."),
    @("PersonalChronicle.UI.Career.Honor.Select.Current","Currently shown"),
    @("PersonalChronicle.UI.Career.Honor.Select.Current.Auto","Auto"),
    @("PersonalChronicle.UI.Career.Honor.Select.Current.None","Hidden"),
    @("PersonalChronicle.UI.Career.Honor.Select.NoCareer","This colonist has no career archive yet."),
    @("Professional.Effect.Engineering_Structure.Signature","Construction speed +5%"),
    @("Professional.Effect.Engineering_Power.Signature","Construction speed +4%"),
    @("Professional.Effect.Engineering_Control.Signature","Construction speed +3%"),
    @("Professional.Effect.Engineering_Precision.Signature","Construction speed +4%"),
    @("Professional.Skill.ProfessionalSkill_StructuralEngineering.Label","Structural Engineering"),
    @("Professional.Skill.ProfessionalSkill_StructuralEngineering.Desc","Professional construction skill grown from vanilla Construction work."),
    @("Professional.Skill.ProfessionalSkill_TillageAgriculture.Label","Tillage Agriculture"),
    @("Professional.Skill.ProfessionalSkill_TillageAgriculture.Desc","Professional growing skill grown from vanilla Plants work."),
    @("Professional.Skill.ProfessionalSkill_OreExtraction.Label","Ore Extraction"),
    @("Professional.Skill.ProfessionalSkill_OreExtraction.Desc","Professional mining skill grown from vanilla Mining work."),
    @("Professional.Skill.ProfessionalSkill_TheoryResearch.Label","Theoretical Research"),
    @("Professional.Skill.ProfessionalSkill_TheoryResearch.Desc","Professional research skill grown from vanilla Intellectual work."),
    @("Professional.Skill.ProfessionalSkill_StapleCooking.Label","Staple Cooking"),
    @("Professional.Skill.ProfessionalSkill_StapleCooking.Desc","Professional cooking skill grown from vanilla Cooking work."),
    @("Professional.Skill.ProfessionalSkill_ClinicalMedicine.Label","Clinical Medicine"),
    @("Professional.Skill.ProfessionalSkill_ClinicalMedicine.Desc","Professional medicine skill grown from vanilla Medicine work."),
    @("Professional.Skill.ProfessionalSkill_DiplomacyManagement.Label","Diplomatic Management"),
    @("Professional.Skill.ProfessionalSkill_DiplomacyManagement.Desc","Professional social skill grown from vanilla Social work."),
    @("Professional.Skill.ProfessionalSkill_SculptureArt.Label","Sculpture"),
    @("Professional.Skill.ProfessionalSkill_SculptureArt.Desc","Professional art skill grown from vanilla Artistic work."),
    @("Professional.Skill.ProfessionalSkill_AnimalBreeding.Label","Animal Breeding"),
    @("Professional.Skill.ProfessionalSkill_AnimalBreeding.Desc","Professional animal skill grown from vanilla Animals work."),
    @("Professional.Skill.ProfessionalSkill_RangedWeaponry.Label","Ranged Weaponry"),
    @("Professional.Skill.ProfessionalSkill_RangedWeaponry.Desc","Professional shooting skill grown from vanilla Shooting."),
    @("Professional.Skill.ProfessionalSkill_MeleeWeaponry.Label","Melee Weaponry"),
    @("Professional.Skill.ProfessionalSkill_MeleeWeaponry.Desc","Professional melee skill grown from vanilla Melee.")
)
foreach ($r in $rows) {
    $skill = $r[0]; $enLabel = $r[11]; $enDesc = $r[12]
    $en += ,@("Professional.Skill.$skill.Label", $enLabel)
    $en += ,@("Professional.Skill.$skill.Desc", $enDesc)
}
$cnExtraMap = @{
    ProfessionalSkill_WeaponryManufacturing = @("武器制造","武器制造专业技能。")
    ProfessionalSkill_EquipmentManufacturing = @("装备制造","装备制造专业技能。")
    ProfessionalSkill_IndustrialManufacturing = @("工业制造","工业制造专业技能。")
    ProfessionalSkill_EngineeringPower = @("动力工程","动力工程专业技能。")
    ProfessionalSkill_EngineeringControl = @("电控工程","电控工程专业技能。")
    ProfessionalSkill_EngineeringPrecision = @("精密工程","精密工程专业技能。")
    ProfessionalSkill_AgricultureIrrigation = @("灌溉排灌","灌溉排灌专业技能。")
    ProfessionalSkill_AgricultureBreeding = @("良种繁育","良种繁育专业技能。")
    ProfessionalSkill_AgricultureGreenhouse = @("温室园艺","温室园艺专业技能。")
    ProfessionalSkill_ForestryHarvest = @("林木采伐","林木采伐专业技能。")
    ProfessionalSkill_ForestryNursery = @("育苗抚育","育苗抚育专业技能。")
    ProfessionalSkill_ForestryLumber = @("木材加工","木材加工专业技能。")
    ProfessionalSkill_ForestrySilviculture = @("野化育林","野化育林专业技能。")
    ProfessionalSkill_AnimalHusbandryHusbandry = @("家畜饲养","家畜饲养专业技能。")
    ProfessionalSkill_AnimalHusbandryVeterinary = @("兽医防疫","兽医防疫专业技能。")
    ProfessionalSkill_AnimalHusbandryPasture = @("放牧管理","放牧管理专业技能。")
    ProfessionalSkill_MedicineSurgery = @("外科手术","外科手术专业技能。")
    ProfessionalSkill_MedicinePharmacy = @("药剂调制","药剂调制专业技能。")
    ProfessionalSkill_MedicinePrevention = @("预防医学","预防医学专业技能。")
    ProfessionalSkill_WeaponsHeavy = @("重武器","重武器专业技能。")
    ProfessionalSkill_WeaponsAmmo = @("弹药工程","弹药工程专业技能。")
    ProfessionalSkill_MiningUnderground = @("井下开采","井下开采专业技能。")
    ProfessionalSkill_MiningBeneficiation = @("选矿","选矿专业技能。")
    ProfessionalSkill_MiningBlasting = @("爆破开采","爆破开采专业技能。")
    ProfessionalSkill_ResearchExperiment = @("实验研究","实验研究专业技能。")
    ProfessionalSkill_ResearchApplied = @("应用研究","应用研究专业技能。")
    ProfessionalSkill_ResearchLiterature = @("文献研究","文献研究专业技能。")
    ProfessionalSkill_CookingCuisine = @("精致菜肴","精致菜肴专业技能。")
    ProfessionalSkill_CookingBaking = @("烘焙面点","烘焙面点专业技能。")
    ProfessionalSkill_CookingPreservation = @("食品保存","食品保存专业技能。")
    ProfessionalSkill_ArtPainting = @("绘画","绘画专业技能。")
    ProfessionalSkill_ArtMusic = @("音乐","音乐专业技能。")
    ProfessionalSkill_ArtCraft = @("工艺美术","工艺美术专业技能。")
    ProfessionalSkill_ManagementDispatch = @("调度指挥","调度指挥专业技能。")
    ProfessionalSkill_ManagementLogistics = @("后勤物流","后勤物流专业技能。")
    ProfessionalSkill_ManagementPersonnel = @("人事培养","人事培养专业技能。")
}
foreach ($k in $cnExtraMap.Keys) {
    $cn += ,@("Professional.Skill.$k.Label", $cnExtraMap[$k][0])
    $cn += ,@("Professional.Skill.$k.Desc", $cnExtraMap[$k][1])
}

Append-Keys (Join-Path $Root "Languages\ChineseSimplified\Keyed\Archive.xml") $cn
Append-Keys (Join-Path $Root "Languages\English\Keyed\Archive.xml") $en
$ncn = Join-Path $Root "Languages\ChineseSimplified\Keyed\Keyed\Archive.xml"
$nen = Join-Path $Root "Languages\English\Keyed\Keyed\Archive.xml"
if (Test-Path $ncn) { Append-Keys $ncn $cn }
if (Test-Path $nen) { Append-Keys $nen $en }
Write-Host "keys done"
