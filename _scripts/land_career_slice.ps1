$ErrorActionPreference = "Stop"
$Root = "E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统"

function Parse-Keyed([string]$path) {
    $map = @{}
    $text = [IO.File]::ReadAllText($path)
    [regex]::Matches($text, '<([^>]+)>([^<]*)</\1>') | ForEach-Object {
        $map[$_.Groups[1].Value] = $_.Groups[2].Value
    }
    return $map
}

function Xml-Escape([string]$s) {
    if ($null -eq $s) { return "" }
    return (($s -replace '&', '&amp;') -replace '<', '&lt;') -replace '>', '&gt;'
}

$cnKeys = Parse-Keyed (Join-Path $Root "Languages\ChineseSimplified\Keyed\Archive.xml")
$enKeys = Parse-Keyed (Join-Path $Root "Languages\English\Keyed\Archive.xml")

$majorsEn = @{
    Engineering = "Engineering"; Manufacturing = "Manufacturing"; Agriculture = "Agriculture"
    Forestry = "Forestry"; AnimalHusbandry = "Animal Husbandry"; Medicine = "Medicine"
    Weapons = "Weapons"; Mining = "Mining"; Research = "Research"; Cooking = "Cooking"
    Art = "Art"; Management = "Management"
}
$kindEn = @{
    PracticeKit = @("Practice Kit", "Qualification practice workpiece samples made at the crafting spot for examiner verification.")
    Textbook = @("Exam Textbook", "Theory-exam textbook on the specialty topic, bound at the crafting spot.")
    ResearchDoc = @("Research Document", "Technical document recording completed research projects for the theory exam.")
    SkillBook = @("Skill Monograph", "Monograph on a vanilla skill growth path for the specialty, matching theory-exam scoring.")
    TheoryBook = @("Theory Treatise", "Treatise expounding the specialty theoretical system for the theory exam.")
}
$baseEn = @{
    PC_Exam_Thesis = @("Thesis Scroll", "A bound thesis recording the holder's research for a qualification.")
    PC_Exam_Diploma = @("Diploma", "A formal diploma certifying the holder passed qualification review and defense; displayable.")
    PC_Exam_Practical = @("Practical Certificate", "A nameplate certifying the holder passed the practical exam; issued by the system or hand-pressed.")
    PC_Exam_PracticeKit = @("Practice Kit", "Standard practice-exam workpieces pressed from wood, steel and cloth for examiner verification.")
    PC_Exam_Textbook = @("Exam Textbook", "Theory-exam textbook on a specialty topic for candidates to study.")
    PC_Exam_ResearchDoc = @("Research Document", "Technical document recording completed research projects, matching the theory-exam research-count requirement.")
    PC_Exam_SkillBook = @("Skill Monograph", "Monograph on a single vanilla skill's growth path, matching the theory-exam skill-score basis.")
    PC_Exam_TheoryBook = @("Theory Treatise", "Treatise expounding a specialty's theoretical system, matching the theory-exam theory-book requirement.")
    PC_Exam_ThesisProposal = @("Thesis Proposal", "A thesis skeleton document recording the chosen direction; later book/research/final stages build on it.")
}

function Thing-En([string]$defName) {
    if ($baseEn.ContainsKey($defName)) { return $baseEn[$defName] }
    if ($defName -match '^PC_Exam_(PracticeKit|Textbook|ResearchDoc|SkillBook|TheoryBook)_(\w+)$') {
        $kind = $Matches[1]; $major = $Matches[2]
        $majorEn = $majorsEn[$major]; if (-not $majorEn) { $majorEn = $major }
        $pair = $kindEn[$kind]
        $desc = $majorEn + " " + $pair[1].Substring(0,1).ToLower() + $pair[1].Substring(1)
        return @("$majorEn $($pair[0])", $desc)
    }
    return @(($defName -replace 'PC_Exam_','' -replace '_',' '), "Career exam sample item.")
}

# ---- ThingDefs ----
$thingPath = Join-Path $Root "Defs\ThingDefs.xml"
$thingText = [IO.File]::ReadAllText($thingPath)
$thingText = $thingText.Replace(
    "    label 走翻译键（引擎自动 Translate）。",
    "    label 写英文默认值；中文由 Languages/ChineseSimplified/DefInjected/ThingDef/ 注入。Keyed 键仅供 UI Translate，不会驱动 ThingDef.Label。"
)
$cnInjThing = New-Object System.Collections.Generic.List[string]
$enInjThing = New-Object System.Collections.Generic.List[string]
$thingText = [regex]::Replace($thingText, '(?s)<ThingDef>.*?</ThingDef>', {
    param($m)
    $block = $m.Value
    if ($block -notmatch '<defName>([^<]+)</defName>') { return $block }
    $defName = $Matches[1]
    $en = Thing-En $defName
    $keyLabel = if ($block -match '<label>([^<]*)</label>') { $Matches[1] } else { "" }
    $keyDesc = if ($block -match '<description>([^<]*)</description>') { $Matches[1] } else { "" }
    $cnLabel = if ($cnKeys.ContainsKey($keyLabel)) { $cnKeys[$keyLabel] } else { $en[0] }
    $cnDesc = if ($cnKeys.ContainsKey($keyDesc)) { $cnKeys[$keyDesc] } else { $en[1] }
    if ($keyLabel.StartsWith("PersonalChronicle.")) {
        $block = [regex]::Replace($block, '<label>[^<]*</label>', "<label>$(Xml-Escape $en[0])</label>", 1)
    }
    if ($keyDesc.StartsWith("PersonalChronicle.")) {
        $block = [regex]::Replace($block, '<description>[^<]*</description>', "<description>$(Xml-Escape $en[1])</description>", 1)
    }
    $cnInjThing.Add("  <$defName.label>$(Xml-Escape $cnLabel)</$defName.label>") | Out-Null
    $cnInjThing.Add("  <$defName.description>$(Xml-Escape $cnDesc)</$defName.description>") | Out-Null
    $enInjThing.Add("  <$defName.label>$(Xml-Escape $en[0])</$defName.label>") | Out-Null
    $enInjThing.Add("  <$defName.description>$(Xml-Escape $en[1])</$defName.description>") | Out-Null
    return $block
})
[IO.File]::WriteAllText($thingPath, $thingText)

function Write-Inj([string]$lang, [string]$folder, [string]$file, $rows) {
    $dir = Join-Path $Root "Languages\$lang\DefInjected\$folder"
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $body = "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n<LanguageData>`n" + ($rows -join "`n") + "`n</LanguageData>`n"
    [IO.File]::WriteAllText((Join-Path $dir $file), $body)
}
Write-Inj "ChineseSimplified" "ThingDef" "ExamThings.xml" $cnInjThing
Write-Inj "English" "ThingDef" "ExamThings.xml" $enInjThing
Write-Host "ThingDefs $($cnInjThing.Count) inject rows"

function Recipe-Key([string]$defName, [string]$suffix) {
    $rest = if ($defName.StartsWith("PC_")) { $defName.Substring(3) } else { $defName }
    $parts = $rest.Split("_")
    if ($parts.Length -ge 3 -and $parts[0] -eq "MakeExam" -and $parts[1] -eq "Thesis" -and @("Proposal","CiteBooks","CiteResearch","Final") -contains $parts[2]) {
        return "PersonalChronicle.Recipe.MakeExamThesis.$($parts[2]).$suffix"
    }
    if ($parts.Length -ge 2 -and $majorsEn.ContainsKey($parts[-1])) {
        $major = $parts[-1]
        $mid = ($parts[0..($parts.Length-2)] -join "_")
        return "PersonalChronicle.Recipe.$mid.$major.$suffix"
    }
    return "PersonalChronicle.Recipe.$rest.$suffix"
}
function Recipe-EnFallback([string]$defName, [string]$field) {
    $rest = ($defName -replace 'PC_MakeExam','' -replace '_',' ').Trim()
    if ($field -eq "Job") { return "Making $rest" }
    if ($field -eq "Desc") { return "Make $rest at the crafting spot." }
    return "Make $rest"
}

$recipePath = Join-Path $Root "Defs\RecipeDefs.xml"
$recipeText = [IO.File]::ReadAllText($recipePath)
$cnInjRecipe = New-Object System.Collections.Generic.List[string]
$enInjRecipe = New-Object System.Collections.Generic.List[string]
$recipeText = [regex]::Replace($recipeText, '(?s)<RecipeDef>.*?</RecipeDef>', {
    param($m)
    $block = $m.Value
    if ($block -notmatch '<defName>([^<]+)</defName>') { return $block }
    $defName = $Matches[1]
    function Pick([string]$field) {
        $key = Recipe-Key $defName $field
        $en = $enKeys[$key]
        if (-not $en -or $en -match '[\u4e00-\u9fff]') { $en = Recipe-EnFallback $defName $field }
        $cn = if ($cnKeys.ContainsKey($key)) { $cnKeys[$key] } else { $en }
        return @($en, $cn)
    }
    $l = Pick "Label"; $d = Pick "Desc"; $j = Pick "Job"
    $block = [regex]::Replace($block, '<label>[^<]*</label>', "<label>$(Xml-Escape $l[0])</label>", 1)
    $block = [regex]::Replace($block, '<description>[^<]*</description>', "<description>$(Xml-Escape $d[0])</description>", 1)
    $block = [regex]::Replace($block, '<jobString>[^<]*</jobString>', "<jobString>$(Xml-Escape $j[0])</jobString>", 1)
    $cnInjRecipe.Add("  <$defName.label>$(Xml-Escape $l[1])</$defName.label>") | Out-Null
    $cnInjRecipe.Add("  <$defName.description>$(Xml-Escape $d[1])</$defName.description>") | Out-Null
    $cnInjRecipe.Add("  <$defName.jobString>$(Xml-Escape $j[1])</$defName.jobString>") | Out-Null
    $enInjRecipe.Add("  <$defName.label>$(Xml-Escape $l[0])</$defName.label>") | Out-Null
    $enInjRecipe.Add("  <$defName.description>$(Xml-Escape $d[0])</$defName.description>") | Out-Null
    $enInjRecipe.Add("  <$defName.jobString>$(Xml-Escape $j[0])</$defName.jobString>") | Out-Null
    return $block
})
[IO.File]::WriteAllText($recipePath, $recipeText)
Write-Inj "ChineseSimplified" "RecipeDef" "ExamRecipes.xml" $cnInjRecipe
Write-Inj "English" "RecipeDef" "ExamRecipes.xml" $enInjRecipe
Write-Host "RecipeDefs $($cnInjRecipe.Count) inject rows"

$dirSkill = [ordered]@{
    Manufacturing_Precision = "ProfessionalSkill_PrecisionManufacturing"
    Manufacturing_Weaponry = "ProfessionalSkill_WeaponryManufacturing"
    Manufacturing_Equipment = "ProfessionalSkill_EquipmentManufacturing"
    Manufacturing_Industrial = "ProfessionalSkill_IndustrialManufacturing"
    Engineering_Structure = "ProfessionalSkill_StructuralEngineering"
    Engineering_Power = "ProfessionalSkill_EngineeringPower"
    Engineering_Control = "ProfessionalSkill_EngineeringControl"
    Engineering_Precision = "ProfessionalSkill_EngineeringPrecision"
    Agriculture_Tillage = "ProfessionalSkill_TillageAgriculture"
    Agriculture_Irrigation = "ProfessionalSkill_AgricultureIrrigation"
    Agriculture_Breeding = "ProfessionalSkill_AgricultureBreeding"
    Agriculture_Greenhouse = "ProfessionalSkill_AgricultureGreenhouse"
    Forestry_Harvest = "ProfessionalSkill_ForestryHarvest"
    Forestry_Nursery = "ProfessionalSkill_ForestryNursery"
    Forestry_Lumber = "ProfessionalSkill_ForestryLumber"
    Forestry_Silviculture = "ProfessionalSkill_ForestrySilviculture"
    AnimalHusbandry_Husbandry = "ProfessionalSkill_AnimalHusbandryHusbandry"
    AnimalHusbandry_Breeding = "ProfessionalSkill_AnimalBreeding"
    AnimalHusbandry_Veterinary = "ProfessionalSkill_AnimalHusbandryVeterinary"
    AnimalHusbandry_Pasture = "ProfessionalSkill_AnimalHusbandryPasture"
    Medicine_Clinical = "ProfessionalSkill_ClinicalMedicine"
    Medicine_Surgery = "ProfessionalSkill_MedicineSurgery"
    Medicine_Pharmacy = "ProfessionalSkill_MedicinePharmacy"
    Medicine_Prevention = "ProfessionalSkill_MedicinePrevention"
    Weapons_Melee = "ProfessionalSkill_MeleeWeaponry"
    Weapons_Ranged = "ProfessionalSkill_RangedWeaponry"
    Weapons_Heavy = "ProfessionalSkill_WeaponsHeavy"
    Weapons_Ammo = "ProfessionalSkill_WeaponsAmmo"
    Mining_Openpit = "ProfessionalSkill_OreExtraction"
    Mining_Underground = "ProfessionalSkill_MiningUnderground"
    Mining_Beneficiation = "ProfessionalSkill_MiningBeneficiation"
    Mining_Blasting = "ProfessionalSkill_MiningBlasting"
    Research_Theory = "ProfessionalSkill_TheoryResearch"
    Research_Experiment = "ProfessionalSkill_ResearchExperiment"
    Research_Applied = "ProfessionalSkill_ResearchApplied"
    Research_Literature = "ProfessionalSkill_ResearchLiterature"
    Cooking_Staple = "ProfessionalSkill_StapleCooking"
    Cooking_Cuisine = "ProfessionalSkill_CookingCuisine"
    Cooking_Baking = "ProfessionalSkill_CookingBaking"
    Cooking_Preservation = "ProfessionalSkill_CookingPreservation"
    Art_Painting = "ProfessionalSkill_ArtPainting"
    Art_Sculpture = "ProfessionalSkill_SculptureArt"
    Art_Music = "ProfessionalSkill_ArtMusic"
    Art_Craft = "ProfessionalSkill_ArtCraft"
    Management_Dispatch = "ProfessionalSkill_ManagementDispatch"
    Management_Logistics = "ProfessionalSkill_ManagementLogistics"
    Management_Personnel = "ProfessionalSkill_ManagementPersonnel"
    Management_Diplomacy = "ProfessionalSkill_DiplomacyManagement"
}

$skillPath = Join-Path $Root "Defs\ProfessionalSkills.xml"
$skillText = [IO.File]::ReadAllText($skillPath)
$skillText = $skillText.Replace(
    '       暂不挂 skillDefNames —— 前端 SkillsOfDirection 空时显示"待落地"，不阻断 UI。',
    "       各方向 skillDefNames 已挂接到对应专业技能；效果见 ProfessionalSkills.Landing.xml。"
)
$skillText = [regex]::Replace($skillText, '(?s)<PersonalChronicle\.Domain\.Profession\.ProfessionalDirectionDef>.*?</PersonalChronicle\.Domain\.Profession\.ProfessionalDirectionDef>', {
    param($m)
    $block = $m.Value
    if ($block -notmatch '<defName>([^<]+)</defName>') { return $block }
    $dname = $Matches[1]
    $skill = $dirSkill[$dname]
    if (-not $skill) { return $block }
    if ($block.Contains("<skillDefNames>")) { return $block }
    $insert = "    <skillDefNames>`n      <li>$skill</li>`n    </skillDefNames>`n"
    return [regex]::Replace($block, '(<profession>[^<]+</profession>\r?\n)', { param($mm) $mm.Value + $insert }, 1)
})

$existingEffect = @{
    ProfessionalSkill_StructuralEngineering = "ProfessionalEffect_Engineering_Structure"
    ProfessionalSkill_TillageAgriculture = "ProfessionalEffect_Agriculture_Tillage"
    ProfessionalSkill_OreExtraction = "ProfessionalEffect_Mining_Openpit"
    ProfessionalSkill_TheoryResearch = "ProfessionalEffect_Research_Theory"
    ProfessionalSkill_StapleCooking = "ProfessionalEffect_Cooking_Staple"
    ProfessionalSkill_ClinicalMedicine = "ProfessionalEffect_Medicine_Clinical"
    ProfessionalSkill_DiplomacyManagement = "ProfessionalEffect_Management_Diplomacy"
    ProfessionalSkill_SculptureArt = "ProfessionalEffect_Art_Sculpture"
    ProfessionalSkill_AnimalBreeding = "ProfessionalEffect_AnimalHusbandry_Breeding"
    ProfessionalSkill_RangedWeaponry = "ProfessionalEffect_Weapons_Ranged"
    ProfessionalSkill_MeleeWeaponry = "ProfessionalEffect_Weapons_Melee"
}
foreach ($sk in $existingEffect.Keys) {
    $eff = $existingEffect[$sk]
    $skillText = [regex]::Replace($skillText, "(?s)(<defName>$sk</defName>.*?<qualificationTags>)", {
        param($m)
        $chunk = $m.Groups[1].Value
        if ($chunk.Contains("<effectDefNames>")) { return $chunk }
        return $chunk.Replace("<qualificationTags>", "<effectDefNames>`n      <li>$eff</li>`n    </effectDefNames>`n    <qualificationTags>")
    })
}
[IO.File]::WriteAllText($skillPath, $skillText)
Write-Host "Direction skills inserted"

Write-Host "land_career_slice.ps1 xml transforms done"
