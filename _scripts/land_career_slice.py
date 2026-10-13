# -*- coding: utf-8 -*-
"""Land exam ThingDef translations, direction skillDefNames, and major effect/skill XML."""
from __future__ import print_function
import os
import re
import io

ROOT = r"E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统"

MAJORS_EN = {
    "Engineering": "Engineering",
    "Manufacturing": "Manufacturing",
    "Agriculture": "Agriculture",
    "Forestry": "Forestry",
    "AnimalHusbandry": "Animal Husbandry",
    "Medicine": "Medicine",
    "Weapons": "Weapons",
    "Mining": "Mining",
    "Research": "Research",
    "Cooking": "Cooking",
    "Art": "Art",
    "Management": "Management",
}

KIND_EN = {
    "PracticeKit": ("Practice Kit", "Qualification practice workpiece samples made at the crafting spot for examiner verification."),
    "Textbook": ("Exam Textbook", "Theory-exam textbook on the specialty topic, bound at the crafting spot."),
    "ResearchDoc": ("Research Document", "Technical document recording completed research projects for the theory exam."),
    "SkillBook": ("Skill Monograph", "Monograph on a vanilla skill growth path for the specialty, matching theory-exam scoring."),
    "TheoryBook": ("Theory Treatise", "Treatise expounding the specialty theoretical system for the theory exam."),
}

BASE_EN = {
    "PC_Exam_Thesis": ("Thesis Scroll", "A bound thesis recording the holder's research for a qualification."),
    "PC_Exam_Diploma": ("Diploma", "A formal diploma certifying the holder passed qualification review and defense; displayable."),
    "PC_Exam_Practical": ("Practical Certificate", "A nameplate certifying the holder passed the practical exam; issued by the system or hand-pressed."),
    "PC_Exam_PracticeKit": ("Practice Kit", "Standard practice-exam workpieces pressed from wood, steel and cloth for examiner verification."),
    "PC_Exam_Textbook": ("Exam Textbook", "Theory-exam textbook on a specialty topic for candidates to study."),
    "PC_Exam_ResearchDoc": ("Research Document", "Technical document recording completed research projects, matching the theory-exam research-count requirement."),
    "PC_Exam_SkillBook": ("Skill Monograph", "Monograph on a single vanilla skill's growth path, matching the theory-exam skill-score basis."),
    "PC_Exam_TheoryBook": ("Theory Treatise", "Treatise expounding a specialty's theoretical system, matching the theory-exam theory-book requirement."),
    "PC_Exam_ThesisProposal": ("Thesis Proposal", "A thesis skeleton document recording the chosen direction; later book/research/final stages build on it."),
}

DIR_SKILL = {
    "Manufacturing_Precision": "ProfessionalSkill_PrecisionManufacturing",
    "Manufacturing_Weaponry": "ProfessionalSkill_WeaponryManufacturing",
    "Manufacturing_Equipment": "ProfessionalSkill_EquipmentManufacturing",
    "Manufacturing_Industrial": "ProfessionalSkill_IndustrialManufacturing",
    "Engineering_Structure": "ProfessionalSkill_StructuralEngineering",
    "Engineering_Power": "ProfessionalSkill_EngineeringPower",
    "Engineering_Control": "ProfessionalSkill_EngineeringControl",
    "Engineering_Precision": "ProfessionalSkill_EngineeringPrecision",
    "Agriculture_Tillage": "ProfessionalSkill_TillageAgriculture",
    "Agriculture_Irrigation": "ProfessionalSkill_AgricultureIrrigation",
    "Agriculture_Breeding": "ProfessionalSkill_AgricultureBreeding",
    "Agriculture_Greenhouse": "ProfessionalSkill_AgricultureGreenhouse",
    "Forestry_Harvest": "ProfessionalSkill_ForestryHarvest",
    "Forestry_Nursery": "ProfessionalSkill_ForestryNursery",
    "Forestry_Lumber": "ProfessionalSkill_ForestryLumber",
    "Forestry_Silviculture": "ProfessionalSkill_ForestrySilviculture",
    "AnimalHusbandry_Husbandry": "ProfessionalSkill_AnimalHusbandryHusbandry",
    "AnimalHusbandry_Breeding": "ProfessionalSkill_AnimalBreeding",
    "AnimalHusbandry_Veterinary": "ProfessionalSkill_AnimalHusbandryVeterinary",
    "AnimalHusbandry_Pasture": "ProfessionalSkill_AnimalHusbandryPasture",
    "Medicine_Clinical": "ProfessionalSkill_ClinicalMedicine",
    "Medicine_Surgery": "ProfessionalSkill_MedicineSurgery",
    "Medicine_Pharmacy": "ProfessionalSkill_MedicinePharmacy",
    "Medicine_Prevention": "ProfessionalSkill_MedicinePrevention",
    "Weapons_Melee": "ProfessionalSkill_MeleeWeaponry",
    "Weapons_Ranged": "ProfessionalSkill_RangedWeaponry",
    "Weapons_Heavy": "ProfessionalSkill_WeaponsHeavy",
    "Weapons_Ammo": "ProfessionalSkill_WeaponsAmmo",
    "Mining_Openpit": "ProfessionalSkill_OreExtraction",
    "Mining_Underground": "ProfessionalSkill_MiningUnderground",
    "Mining_Beneficiation": "ProfessionalSkill_MiningBeneficiation",
    "Mining_Blasting": "ProfessionalSkill_MiningBlasting",
    "Research_Theory": "ProfessionalSkill_TheoryResearch",
    "Research_Experiment": "ProfessionalSkill_ResearchExperiment",
    "Research_Applied": "ProfessionalSkill_ResearchApplied",
    "Research_Literature": "ProfessionalSkill_ResearchLiterature",
    "Cooking_Staple": "ProfessionalSkill_StapleCooking",
    "Cooking_Cuisine": "ProfessionalSkill_CookingCuisine",
    "Cooking_Baking": "ProfessionalSkill_CookingBaking",
    "Cooking_Preservation": "ProfessionalSkill_CookingPreservation",
    "Art_Painting": "ProfessionalSkill_ArtPainting",
    "Art_Sculpture": "ProfessionalSkill_SculptureArt",
    "Art_Music": "ProfessionalSkill_ArtMusic",
    "Art_Craft": "ProfessionalSkill_ArtCraft",
    "Management_Dispatch": "ProfessionalSkill_ManagementDispatch",
    "Management_Logistics": "ProfessionalSkill_ManagementLogistics",
    "Management_Personnel": "ProfessionalSkill_ManagementPersonnel",
    "Management_Diplomacy": "ProfessionalSkill_DiplomacyManagement",
}

EXISTING_SKILLS = {
    "ProfessionalSkill_PrecisionManufacturing",
    "ProfessionalSkill_StructuralEngineering",
    "ProfessionalSkill_TillageAgriculture",
    "ProfessionalSkill_OreExtraction",
    "ProfessionalSkill_TheoryResearch",
    "ProfessionalSkill_StapleCooking",
    "ProfessionalSkill_ClinicalMedicine",
    "ProfessionalSkill_DiplomacyManagement",
    "ProfessionalSkill_SculptureArt",
    "ProfessionalSkill_AnimalBreeding",
    "ProfessionalSkill_RangedWeaponry",
    "ProfessionalSkill_MeleeWeaponry",
}

# profession, sourceSkill, workType, effectDefName, kind, value, stat, isReduction, labelKey
SKILL_META = {
    "ProfessionalSkill_PrecisionManufacturing": ("Manufacturing", "Crafting", "Smithing", "ProfessionalEffect_ManufacturingWorkSpeed", "WorkSpeed", 0.03, "", False, "Professional.Skill.PrecisionManufacturing.EffectWorkSpeed"),
    "ProfessionalSkill_WeaponryManufacturing": ("Manufacturing", "Crafting", "Smithing", "ProfessionalEffect_ManufacturingWorkSpeed", "WorkSpeed", 0.05, "", False, "Professional.Skill.PrecisionManufacturing.EffectWorkSpeed"),
    "ProfessionalSkill_EquipmentManufacturing": ("Manufacturing", "Crafting", "Smithing", "ProfessionalEffect_QualityBias", "QualityBias", 1, "", False, "Professional.Skill.PrecisionManufacturing.EffectQualityBias"),
    "ProfessionalSkill_IndustrialManufacturing": ("Manufacturing", "Crafting", "Smithing", "ProfessionalEffect_ManufacturingWorkSpeed", "WorkSpeed", 0.04, "", False, "Professional.Skill.PrecisionManufacturing.EffectWorkSpeed"),
    "ProfessionalSkill_StructuralEngineering": ("Engineering", "Construction", "Construction", "ProfessionalEffect_Engineering_Structure", "StatBonus", 0.05, "ConstructionSpeed", False, "Professional.Effect.Engineering_Structure.Signature"),
    "ProfessionalSkill_EngineeringPower": ("Engineering", "Construction", "Construction", "ProfessionalEffect_Engineering_Power", "StatBonus", 0.04, "ConstructionSpeed", False, "Professional.Effect.Engineering_Power.Signature"),
    "ProfessionalSkill_EngineeringControl": ("Engineering", "Construction", "Construction", "ProfessionalEffect_Engineering_Control", "StatBonus", 0.03, "ConstructionSpeed", False, "Professional.Effect.Engineering_Control.Signature"),
    "ProfessionalSkill_EngineeringPrecision": ("Engineering", "Construction", "Construction", "ProfessionalEffect_Engineering_Precision", "StatBonus", 0.04, "ConstructionSpeed", False, "Professional.Effect.Engineering_Precision.Signature"),
    "ProfessionalSkill_TillageAgriculture": ("Agriculture", "Plants", "Growing", "ProfessionalEffect_Agriculture_Tillage", "YieldBonus", 0.10, "PlantHarvestYield", False, "Professional.Effect.Agriculture_Tillage.Signature"),
    "ProfessionalSkill_AgricultureIrrigation": ("Agriculture", "Plants", "Growing", "ProfessionalEffect_Agriculture_Irrigation", "YieldBonus", 0.10, "DrugHarvestYield", False, "Professional.Effect.Agriculture_Irrigation.Signature"),
    "ProfessionalSkill_AgricultureBreeding": ("Agriculture", "Plants", "Growing", "ProfessionalEffect_Agriculture_Breeding", "YieldBonus", 0.08, "PlantHarvestYield", False, "Professional.Effect.Agriculture_Breeding.Signature"),
    "ProfessionalSkill_AgricultureGreenhouse": ("Agriculture", "Plants", "Growing", "ProfessionalEffect_Agriculture_Greenhouse", "YieldBonus", 0.08, "PlantHarvestYield", False, "Professional.Effect.Agriculture_Greenhouse.Signature"),
    "ProfessionalSkill_ForestryHarvest": ("Forestry", "Plants", "Growing", "ProfessionalEffect_Forestry_Harvest", "StatBonus", 0.10, "PlantWorkSpeed", False, "Professional.Effect.Forestry_Harvest.Signature"),
    "ProfessionalSkill_ForestryNursery": ("Forestry", "Plants", "Growing", "ProfessionalEffect_Forestry_Nursery", "StatBonus", 0.08, "PlantWorkSpeed", False, "Professional.Effect.Forestry_Nursery.Signature"),
    "ProfessionalSkill_ForestryLumber": ("Forestry", "Plants", "Growing", "ProfessionalEffect_Forestry_Lumber", "StatBonus", 0.08, "PlantWorkSpeed", False, "Professional.Effect.Forestry_Lumber.Signature"),
    "ProfessionalSkill_ForestrySilviculture": ("Forestry", "Plants", "Growing", "ProfessionalEffect_Forestry_Silviculture", "YieldBonus", 0.08, "PlantHarvestYield", False, "Professional.Effect.Forestry_Silviculture.Signature"),
    "ProfessionalSkill_AnimalHusbandryHusbandry": ("AnimalHusbandry", "Animals", "Handling", "ProfessionalEffect_AnimalHusbandry_Husbandry", "StatBonus", 0.10, "TameAnimalChance", False, "Professional.Effect.AnimalHusbandry_Husbandry.Signature"),
    "ProfessionalSkill_AnimalBreeding": ("AnimalHusbandry", "Animals", "Handling", "ProfessionalEffect_AnimalHusbandry_Breeding", "StatBonus", 0.08, "TrainAnimalChance", False, "Professional.Effect.AnimalHusbandry_Breeding.Signature"),
    "ProfessionalSkill_AnimalHusbandryVeterinary": ("AnimalHusbandry", "Animals", "Handling", "ProfessionalEffect_AnimalHusbandry_Veterinary", "StatBonus", 0.10, "TrainAnimalChance", False, "Professional.Effect.AnimalHusbandry_Veterinary.Signature"),
    "ProfessionalSkill_AnimalHusbandryPasture": ("AnimalHusbandry", "Animals", "Handling", "ProfessionalEffect_AnimalHusbandry_Pasture", "YieldBonus", 0.10, "AnimalGatherYield", False, "Professional.Effect.AnimalHusbandry_Pasture.Signature"),
    "ProfessionalSkill_ClinicalMedicine": ("Medicine", "Medicine", "Doctor", "ProfessionalEffect_Medicine_Clinical", "StatBonus", 0.10, "MedicalTendQuality", False, "Professional.Effect.Medicine_Clinical.Signature"),
    "ProfessionalSkill_MedicineSurgery": ("Medicine", "Medicine", "Doctor", "ProfessionalEffect_Medicine_Surgery", "StatBonus", 0.10, "MedicalSurgerySuccessChance", False, "Professional.Effect.Medicine_Surgery.Signature"),
    "ProfessionalSkill_MedicinePharmacy": ("Medicine", "Medicine", "Doctor", "ProfessionalEffect_Medicine_Pharmacy", "StatBonus", 0.08, "MedicalTendSpeed", False, "Professional.Effect.Medicine_Pharmacy.Signature"),
    "ProfessionalSkill_MedicinePrevention": ("Medicine", "Medicine", "Doctor", "ProfessionalEffect_Medicine_Prevention", "StatBonus", 0.10, "ImmunityGainSpeed", False, "Professional.Effect.Medicine_Prevention.Signature"),
    "ProfessionalSkill_MeleeWeaponry": ("Weapons", "Melee", "", "ProfessionalEffect_Weapons_Melee", "CombatStat", 0.05, "MeleeHitChance", False, "Professional.Skill.Weapons.WorkSpeed"),
    "ProfessionalSkill_RangedWeaponry": ("Weapons", "Shooting", "Hunting", "ProfessionalEffect_Weapons_Ranged", "CombatStat", 0.05, "ShootingAccuracyPawn", False, "Professional.Skill.Weapons.WorkSpeed"),
    "ProfessionalSkill_WeaponsHeavy": ("Weapons", "Shooting", "Hunting", "ProfessionalEffect_Weapons_Heavy", "CombatStat", 0.04, "ShootingAccuracyPawn", False, "Professional.Skill.Weapons.WorkSpeed"),
    "ProfessionalSkill_WeaponsAmmo": ("Weapons", "Crafting", "Smithing", "ProfessionalEffect_Weapons_Ammo", "StatBonus", 0.04, "WorkSpeedGlobal", False, "Professional.Skill.Weapons.WorkSpeed"),
    "ProfessionalSkill_OreExtraction": ("Mining", "Mining", "Mining", "ProfessionalEffect_Mining_Openpit", "StatBonus", 0.10, "MiningSpeed", False, "Professional.Effect.Mining_Openpit.Signature"),
    "ProfessionalSkill_MiningUnderground": ("Mining", "Mining", "Mining", "ProfessionalEffect_Mining_Underground", "StatBonus", 0.10, "MiningSpeed", False, "Professional.Effect.Mining_Underground.Signature"),
    "ProfessionalSkill_MiningBeneficiation": ("Mining", "Mining", "Mining", "ProfessionalEffect_Mining_Beneficiation", "StatBonus", 0.08, "MiningSpeed", False, "Professional.Effect.Mining_Beneficiation.Signature"),
    "ProfessionalSkill_MiningBlasting": ("Mining", "Mining", "Mining", "ProfessionalEffect_Mining_Blasting", "StatBonus", 0.12, "MiningSpeed", False, "Professional.Effect.Mining_Blasting.Signature"),
    "ProfessionalSkill_TheoryResearch": ("Research", "Intellectual", "Research", "ProfessionalEffect_Research_Theory", "StatBonus", 0.10, "GlobalLearningFactor", False, "Professional.Effect.Research_Theory.Signature"),
    "ProfessionalSkill_ResearchExperiment": ("Research", "Intellectual", "Research", "ProfessionalEffect_Research_Experiment", "StatBonus", 0.08, "ResearchSpeed", False, "Professional.Effect.Research_Experiment.Signature"),
    "ProfessionalSkill_ResearchApplied": ("Research", "Intellectual", "Research", "ProfessionalEffect_Research_Applied", "StatBonus", 0.10, "ResearchSpeed", False, "Professional.Effect.Research_Applied.Signature"),
    "ProfessionalSkill_ResearchLiterature": ("Research", "Intellectual", "Research", "ProfessionalEffect_Research_Literature", "StatBonus", 0.10, "GlobalLearningFactor", False, "Professional.Effect.Research_Literature.Signature"),
    "ProfessionalSkill_StapleCooking": ("Cooking", "Cooking", "Cooking", "ProfessionalEffect_Cooking_Staple", "StatBonus", 0.10, "FoodPoisonChance", True, "Professional.Effect.Cooking_Staple.Signature"),
    "ProfessionalSkill_CookingCuisine": ("Cooking", "Cooking", "Cooking", "ProfessionalEffect_Cooking_Cuisine", "StatBonus", 0.10, "FoodPoisonChance", True, "Professional.Effect.Cooking_Cuisine.Signature"),
    "ProfessionalSkill_CookingBaking": ("Cooking", "Cooking", "Cooking", "ProfessionalEffect_Cooking_Baking", "StatBonus", 0.08, "FoodPoisonChance", True, "Professional.Effect.Cooking_Baking.Signature"),
    "ProfessionalSkill_CookingPreservation": ("Cooking", "Cooking", "Cooking", "ProfessionalEffect_Cooking_Preservation", "StatBonus", 0.08, "FoodPoisonChance", True, "Professional.Effect.Cooking_Preservation.Signature"),
    "ProfessionalSkill_ArtPainting": ("Art", "Artistic", "Art", "ProfessionalEffect_Art_Painting", "StatBonus", 0.08, "GeneralLaborSpeed", False, "Professional.Effect.Art_Painting.Signature"),
    "ProfessionalSkill_SculptureArt": ("Art", "Artistic", "Art", "ProfessionalEffect_Art_Sculpture", "StatBonus", 0.08, "GeneralLaborSpeed", False, "Professional.Effect.Art_Sculpture.Signature"),
    "ProfessionalSkill_ArtMusic": ("Art", "Artistic", "Art", "ProfessionalEffect_Art_Music", "StatBonus", 0.08, "SocialImpact", False, "Professional.Effect.Art_Music.Signature"),
    "ProfessionalSkill_ArtCraft": ("Art", "Artistic", "Art", "ProfessionalEffect_Art_Craft", "StatBonus", 0.10, "TradePriceImprovement", False, "Professional.Effect.Art_Craft.Signature"),
    "ProfessionalSkill_ManagementDispatch": ("Management", "Social", "Warden", "ProfessionalEffect_Management_Dispatch", "StatBonus", 0.08, "MoveSpeed", False, "Professional.Effect.Management_Dispatch.Signature"),
    "ProfessionalSkill_ManagementLogistics": ("Management", "Social", "Warden", "ProfessionalEffect_Management_Logistics", "StatBonus", 0.10, "CarryingCapacity", False, "Professional.Effect.Management_Logistics.Signature"),
    "ProfessionalSkill_ManagementPersonnel": ("Management", "Social", "Warden", "ProfessionalEffect_Management_Personnel", "StatBonus", 0.08, "SocialImpact", False, "Professional.Effect.Management_Personnel.Signature"),
    "ProfessionalSkill_DiplomacyManagement": ("Management", "Social", "Warden", "ProfessionalEffect_Management_Diplomacy", "StatBonus", 0.10, "TradePriceImprovement", False, "Professional.Effect.Management_Diplomacy.Signature"),
}

SKILL_EN_LABEL = {
    "ProfessionalSkill_StructuralEngineering": ("Structural Engineering", "Professional construction skill grown from vanilla Construction work."),
    "ProfessionalSkill_TillageAgriculture": ("Tillage Agriculture", "Professional growing skill grown from vanilla Plants work."),
    "ProfessionalSkill_OreExtraction": ("Ore Extraction", "Professional mining skill grown from vanilla Mining work."),
    "ProfessionalSkill_TheoryResearch": ("Theoretical Research", "Professional research skill grown from vanilla Intellectual work."),
    "ProfessionalSkill_StapleCooking": ("Staple Cooking", "Professional cooking skill grown from vanilla Cooking work."),
    "ProfessionalSkill_ClinicalMedicine": ("Clinical Medicine", "Professional medicine skill grown from vanilla Medicine work."),
    "ProfessionalSkill_DiplomacyManagement": ("Diplomatic Management", "Professional social skill grown from vanilla Social work."),
    "ProfessionalSkill_SculptureArt": ("Sculpture", "Professional art skill grown from vanilla Artistic work."),
    "ProfessionalSkill_AnimalBreeding": ("Animal Breeding", "Professional animal skill grown from vanilla Animals work."),
    "ProfessionalSkill_RangedWeaponry": ("Ranged Weaponry", "Professional shooting skill grown from vanilla Shooting."),
    "ProfessionalSkill_MeleeWeaponry": ("Melee Weaponry", "Professional melee skill grown from vanilla Melee."),
    "ProfessionalSkill_WeaponryManufacturing": ("Weapon Crafting", "Professional weapon-crafting skill grown from smithing."),
    "ProfessionalSkill_EquipmentManufacturing": ("Equipment Crafting", "Professional apparel and armor crafting skill."),
    "ProfessionalSkill_IndustrialManufacturing": ("Industrial Crafting", "Professional component and industrial crafting skill."),
    "ProfessionalSkill_EngineeringPower": ("Power Engineering", "Professional construction skill focused on power systems."),
    "ProfessionalSkill_EngineeringControl": ("Control Engineering", "Professional construction skill focused on control systems."),
    "ProfessionalSkill_EngineeringPrecision": ("Precision Engineering", "Professional construction skill focused on precision assembly."),
    "ProfessionalSkill_AgricultureIrrigation": ("Irrigation", "Professional growing skill focused on irrigation and drug crops."),
    "ProfessionalSkill_AgricultureBreeding": ("Crop Breeding", "Professional growing skill focused on cultivar quality."),
    "ProfessionalSkill_AgricultureGreenhouse": ("Greenhouse Horticulture", "Professional growing skill focused on greenhouse yield."),
    "ProfessionalSkill_ForestryHarvest": ("Timber Harvest", "Professional plant-cutting skill for timber harvest."),
    "ProfessionalSkill_ForestryNursery": ("Nursery", "Professional growing skill for seedling and stand tending."),
    "ProfessionalSkill_ForestryLumber": ("Lumber Processing", "Professional wood-processing skill."),
    "ProfessionalSkill_ForestrySilviculture": ("Silviculture", "Professional growing skill for wild woodland yield."),
    "ProfessionalSkill_AnimalHusbandryHusbandry": ("Animal Husbandry", "Professional handling skill for taming and husbandry."),
    "ProfessionalSkill_AnimalHusbandryVeterinary": ("Veterinary", "Professional handling skill for training and animal health."),
    "ProfessionalSkill_AnimalHusbandryPasture": ("Pasture Management", "Professional handling skill for gathering yield."),
    "ProfessionalSkill_MedicineSurgery": ("Surgery", "Professional medicine skill focused on surgical success."),
    "ProfessionalSkill_MedicinePharmacy": ("Pharmacy", "Professional medicine skill focused on tend speed."),
    "ProfessionalSkill_MedicinePrevention": ("Prevention", "Professional medicine skill focused on immunity."),
    "ProfessionalSkill_WeaponsHeavy": ("Heavy Weapons", "Professional shooting skill focused on heavy weapons."),
    "ProfessionalSkill_WeaponsAmmo": ("Ammunition Engineering", "Professional crafting skill focused on ammunition."),
    "ProfessionalSkill_MiningUnderground": ("Underground Mining", "Professional mining skill focused on deep drills."),
    "ProfessionalSkill_MiningBeneficiation": ("Ore Dressing", "Professional mining skill focused on ore quality."),
    "ProfessionalSkill_MiningBlasting": ("Blasting", "Professional mining skill focused on extraction rate."),
    "ProfessionalSkill_ResearchExperiment": ("Experimental Research", "Professional research skill focused on experiment quality."),
    "ProfessionalSkill_ResearchApplied": ("Applied Research", "Professional research skill focused on project speed."),
    "ProfessionalSkill_ResearchLiterature": ("Literature Research", "Professional research skill focused on learning factor."),
    "ProfessionalSkill_CookingCuisine": ("Cuisine", "Professional cooking skill focused on fine meals."),
    "ProfessionalSkill_CookingBaking": ("Baking", "Professional cooking skill focused on baked goods."),
    "ProfessionalSkill_CookingPreservation": ("Food Preservation", "Professional cooking skill focused on food safety."),
    "ProfessionalSkill_ArtPainting": ("Painting", "Professional art skill focused on beauty."),
    "ProfessionalSkill_ArtMusic": ("Music", "Professional art skill focused on social impact."),
    "ProfessionalSkill_ArtCraft": ("Craft Art", "Professional art skill focused on trade price."),
    "ProfessionalSkill_ManagementDispatch": ("Dispatch", "Professional social skill focused on movement and caravans."),
    "ProfessionalSkill_ManagementLogistics": ("Logistics", "Professional social skill focused on carrying capacity."),
    "ProfessionalSkill_ManagementPersonnel": ("Personnel", "Professional social skill focused on social impact."),
}

SKILL_CN_LABEL = {
    "ProfessionalSkill_StructuralEngineering": ("结构工程", "由原版建造工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_TillageAgriculture": ("耕作栽培", "由原版种植工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_OreExtraction": ("矿石开采", "由原版采矿工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_TheoryResearch": ("理论研究", "由原版智识工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_StapleCooking": ("日常主食", "由原版烹饪工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_ClinicalMedicine": ("临床医学", "由原版医疗工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_DiplomacyManagement": ("外交谈判", "由原版社交工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_SculptureArt": ("雕塑艺术", "由原版艺术工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_AnimalBreeding": ("良种繁育", "由原版畜牧工作持续提供成长来源的专业技能。"),
    "ProfessionalSkill_RangedWeaponry": ("远程枪械", "由原版射击技能持续提供成长来源的专业技能。"),
    "ProfessionalSkill_MeleeWeaponry": ("近战锻造", "由原版近战技能持续提供成长来源的专业技能。"),
}


def parse_keyed(path):
    text = io.open(path, encoding="utf-8").read()
    keys = {}
    for m in re.finditer(r"<([^>]+)>([^<]*)</\1>", text):
        keys[m.group(1)] = m.group(2)
    return keys


def xml_escape(s):
    return (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


def thing_en_for(def_name):
    if def_name in BASE_EN:
        return BASE_EN[def_name]
    m = re.match(r"PC_Exam_(PracticeKit|Textbook|ResearchDoc|SkillBook|TheoryBook)_(\w+)$", def_name)
    if m:
        kind, major = m.group(1), m.group(2)
        major_en = MAJORS_EN.get(major, major)
        kind_label, kind_desc = KIND_EN[kind]
        return ("%s %s" % (major_en, kind_label), "%s %s" % (major_en, kind_desc[0].lower() + kind_desc[1:]))
    return (def_name.replace("PC_Exam_", "").replace("_", " "), "Career exam sample item.")


def rewrite_thingdefs(cn_keys):
    path = os.path.join(ROOT, "Defs", "ThingDefs.xml")
    text = io.open(path, encoding="utf-8").read()
    text = text.replace(
        "    label 走翻译键（引擎自动 Translate）。",
        "    label 写英文默认值；中文由 Languages/ChineseSimplified/DefInjected/ThingDef/ 注入。Keyed 键仅供 UI Translate，不会驱动 ThingDef.Label。",
    )
    cn_inj = []
    en_inj = []

    def repl_block(m):
        block = m.group(0)
        def_m = re.search(r"<defName>([^<]+)</defName>", block)
        if not def_m:
            return block
        def_name = def_m.group(1)
        en_label, en_desc = thing_en_for(def_name)
        old_label = re.search(r"<label>([^<]*)</label>", block)
        old_desc = re.search(r"<description>([^<]*)</description>", block)
        key_label = old_label.group(1) if old_label else ""
        key_desc = old_desc.group(1) if old_desc else ""
        cn_label = cn_keys.get(key_label, en_label)
        cn_desc = cn_keys.get(key_desc, en_desc)
        if key_label.startswith("PersonalChronicle."):
            block = re.sub(r"<label>[^<]*</label>", "<label>" + xml_escape(en_label) + "</label>", block, count=1)
        if key_desc.startswith("PersonalChronicle."):
            block = re.sub(r"<description>[^<]*</description>", "<description>" + xml_escape(en_desc) + "</description>", block, count=1)
        cn_inj.append("  <%s.label>%s</%s.label>" % (def_name, xml_escape(cn_label), def_name))
        cn_inj.append("  <%s.description>%s</%s.description>" % (def_name, xml_escape(cn_desc), def_name))
        en_inj.append("  <%s.label>%s</%s.label>" % (def_name, xml_escape(en_label), def_name))
        en_inj.append("  <%s.description>%s</%s.description>" % (def_name, xml_escape(en_desc), def_name))
        return block

    text = re.sub(r"<ThingDef>.*?</ThingDef>", repl_block, text, flags=re.S)
    io.open(path, "w", encoding="utf-8", newline="\n").write(text)

    def write_inj(lang, rows):
        d = os.path.join(ROOT, "Languages", lang, "DefInjected", "ThingDef")
        if not os.path.isdir(d):
            os.makedirs(d)
        body = '<?xml version="1.0" encoding="utf-8"?>\n<LanguageData>\n' + "\n".join(rows) + "\n</LanguageData>\n"
        io.open(os.path.join(d, "ExamThings.xml"), "w", encoding="utf-8", newline="\n").write(body)

    write_inj("ChineseSimplified", cn_inj)
    write_inj("English", en_inj)
    print("ThingDefs rewritten, DefInjected rows:", len(cn_inj))


def recipe_key_for(def_name, suffix):
    # PC_MakeExamThesis -> PersonalChronicle.Recipe.MakeExamThesis.Label
    # PC_MakeExamPracticeKit_Engineering -> PersonalChronicle.Recipe.MakeExamPracticeKit.Engineering.Label
    # PC_MakeExamThesis_Proposal -> PersonalChronicle.Recipe.MakeExamThesis.Proposal.Label
    rest = def_name[len("PC_"):] if def_name.startswith("PC_") else def_name
    parts = rest.split("_")
    if len(parts) >= 3 and parts[0] == "MakeExam" and parts[1] in ("Thesis",) and parts[2] in ("Proposal", "CiteBooks", "CiteResearch", "Final"):
        return "PersonalChronicle.Recipe.MakeExamThesis.%s.%s" % (parts[2], suffix)
    if len(parts) >= 2 and parts[-1] in MAJORS_EN:
        major = parts[-1]
        mid = "_".join(parts[:-1])  # MakeExamPracticeKit
        return "PersonalChronicle.Recipe.%s.%s.%s" % (mid, major, suffix)
    return "PersonalChronicle.Recipe.%s.%s" % (rest, suffix)


def recipe_en_fallback(def_name, field):
    rest = def_name.replace("PC_MakeExam", "").replace("_", " ").strip()
    if field == "Job":
        return "Making " + rest
    if field == "Desc":
        return "Make %s at the crafting spot." % rest
    return "Make " + rest


def rewrite_recipedefs(cn_keys, en_keys):
    path = os.path.join(ROOT, "Defs", "RecipeDefs.xml")
    text = io.open(path, encoding="utf-8").read()
    cn_inj = []
    en_inj = []

    def repl_one(tag, def_name, value, inj_tag):
        return "<%s>%s</%s>" % (tag, xml_escape(value), tag), inj_tag

    def repl_block(m):
        block = m.group(0)
        def_m = re.search(r"<defName>([^<]+)</defName>", block)
        if not def_m:
            return block
        def_name = def_m.group(1)

        def pick(field, tag):
            key = recipe_key_for(def_name, field)
            en = en_keys.get(key)
            if not en or any("\u4e00" <= ch <= "\u9fff" for ch in en):
                en = recipe_en_fallback(def_name, field)
            cn = cn_keys.get(key, en)
            return en, cn

        en_l, cn_l = pick("Label", "label")
        en_d, cn_d = pick("Desc", "description")
        en_j, cn_j = pick("Job", "jobString")
        block = re.sub(r"<label>[^<]*</label>", "<label>" + xml_escape(en_l) + "</label>", block, count=1)
        block = re.sub(r"<description>[^<]*</description>", "<description>" + xml_escape(en_d) + "</description>", block, count=1)
        block = re.sub(r"<jobString>[^<]*</jobString>", "<jobString>" + xml_escape(en_j) + "</jobString>", block, count=1)
        cn_inj.extend([
            "  <%s.label>%s</%s.label>" % (def_name, xml_escape(cn_l), def_name),
            "  <%s.description>%s</%s.description>" % (def_name, xml_escape(cn_d), def_name),
            "  <%s.jobString>%s</%s.jobString>" % (def_name, xml_escape(cn_j), def_name),
        ])
        en_inj.extend([
            "  <%s.label>%s</%s.label>" % (def_name, xml_escape(en_l), def_name),
            "  <%s.description>%s</%s.description>" % (def_name, xml_escape(en_d), def_name),
            "  <%s.jobString>%s</%s.jobString>" % (def_name, xml_escape(en_j), def_name),
        ])
        return block

    text = re.sub(r"<RecipeDef>.*?</RecipeDef>", repl_block, text, flags=re.S)
    io.open(path, "w", encoding="utf-8", newline="\n").write(text)

    def write_inj(lang, rows):
        d = os.path.join(ROOT, "Languages", lang, "DefInjected", "RecipeDef")
        if not os.path.isdir(d):
            os.makedirs(d)
        body = '<?xml version="1.0" encoding="utf-8"?>\n<LanguageData>\n' + "\n".join(rows) + "\n</LanguageData>\n"
        io.open(os.path.join(d, "ExamRecipes.xml"), "w", encoding="utf-8", newline="\n").write(body)

    write_inj("ChineseSimplified", cn_inj)
    write_inj("English", en_inj)
    print("RecipeDefs rewritten, DefInjected rows:", len(cn_inj))


def insert_direction_skills():
    path = os.path.join(ROOT, "Defs", "ProfessionalSkills.xml")
    text = io.open(path, encoding="utf-8").read()
    text = text.replace(
        "       暂不挂 skillDefNames —— 前端 SkillsOfDirection 空时显示\"待落地\"，不阻断 UI。",
        "       各方向 skillDefNames 已挂接到对应专业技能；效果见 ProfessionalSkills.Landing.xml。",
    )

    def repl_dir(m):
        block = m.group(0)
        def_m = re.search(r"<defName>([^<]+)</defName>", block)
        if not def_m:
            return block
        dname = def_m.group(1)
        skill = DIR_SKILL.get(dname)
        if not skill:
            return block
        if "<skillDefNames>" in block:
            return block
        insert = "    <skillDefNames>\n      <li>%s</li>\n    </skillDefNames>\n" % skill
        block = re.sub(r"(<profession>[^<]+</profession>\n)", r"\1" + insert, block, count=1)
        return block

    text = re.sub(
        r"<PersonalChronicle\.Domain\.Profession\.ProfessionalDirectionDef>.*?</PersonalChronicle\.Domain\.Profession\.ProfessionalDirectionDef>",
        repl_dir,
        text,
        flags=re.S,
    )

    # Attach effectDefNames on existing extra skills
    for skill, meta in SKILL_META.items():
        if skill not in EXISTING_SKILLS or skill == "ProfessionalSkill_PrecisionManufacturing":
            continue
        effect = meta[3]
        pattern = r"(<defName>" + re.escape(skill) + r"</defName>.*?<qualificationTags>)"
        def add_effects(m):
            chunk = m.group(1)
            if "<effectDefNames>" in chunk:
                return chunk
            return chunk.replace(
                "<qualificationTags>",
                "<effectDefNames>\n      <li>%s</li>\n    </effectDefNames>\n    <qualificationTags>" % effect,
            )
        text = re.sub(pattern, add_effects, text, flags=re.S)

    io.open(path, "w", encoding="utf-8", newline="\n").write(text)
    print("Direction skillDefNames inserted")


def write_landing_xml():
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<Defs>",
        "  <!-- 二级方向垂直切片落地：其余方向技能 + 数值效果（注入原版 StatDef）。 -->",
        "  <!-- 已有技能（精密制造/结构工程等）不在此重复声明，仅在 ProfessionalSkills.xml 补 effectDefNames。 -->",
    ]
    seen_effects = set()
    # Precision manufacturing effects already exist
    seen_effects.add("ProfessionalEffect_ManufacturingWorkSpeed")
    seen_effects.add("ProfessionalEffect_QualityBias")

    for skill, dir_name in ((v, k) for k, v in DIR_SKILL.items()):
        meta = SKILL_META[skill]
        effect = meta[3]
        if effect in seen_effects:
            continue
        seen_effects.add(effect)
        kind, value, stat, is_red, label_key = meta[4], meta[5], meta[6], meta[7], meta[8]
        lines.append("  <PersonalChronicle.Domain.Profession.ProfessionalEffectDef>")
        lines.append("    <defName>%s</defName>" % effect)
        lines.append("    <kind>%s</kind>" % kind)
        lines.append("    <value>%s</value>" % value)
        if stat:
            lines.append("    <statDefName>%s</statDefName>" % stat)
        if is_red:
            lines.append("    <isReduction>true</isReduction>")
        lines.append("    <labelKey>%s</labelKey>" % label_key)
        lines.append("  </PersonalChronicle.Domain.Profession.ProfessionalEffectDef>")

    for dir_name, skill in DIR_SKILL.items():
        if skill in EXISTING_SKILLS:
            continue
        meta = SKILL_META[skill]
        profession, source, work, effect = meta[0], meta[1], meta[2], meta[3]
        en = SKILL_EN_LABEL.get(skill, (skill.replace("ProfessionalSkill_", ""), "Professional skill."))
        lines.append("  <PersonalChronicle.Domain.Profession.ProfessionalSkillDef>")
        lines.append("    <defName>%s</defName>" % skill)
        lines.append("    <label>%s</label>" % xml_escape(en[0]))
        lines.append("    <description>%s</description>" % xml_escape(en[1]))
        lines.append("    <profession>%s</profession>" % profession)
        lines.append("    <direction>%s</direction>" % dir_name)
        lines.append("    <sourceSkills>")
        lines.append("      <li>%s</li>" % source)
        lines.append("    </sourceSkills>")
        if work:
            lines.append("    <practiceWorkTypeDefNames>")
            lines.append("      <li>%s</li>" % work)
            lines.append("    </practiceWorkTypeDefNames>")
        lines.append("    <xpPerPracticeBase>10</xpPerPracticeBase>")
        lines.append("    <xpDifficulty>1</xpDifficulty>")
        lines.append("    <xpCap>5000</xpCap>")
        lines.append("    <maxLevel>50</maxLevel>")
        lines.append("    <effectDefNames>")
        lines.append("      <li>%s</li>" % effect)
        lines.append("    </effectDefNames>")
        if meta[4] == "WorkSpeed" and meta[5] != 0.03:
            lines.append("    <effectOverrides>")
            lines.append("      <li>")
            lines.append("        <effectDefName>%s</effectDefName>" % effect)
            lines.append("        <hasValue>true</hasValue>")
            lines.append("        <value>%s</value>" % meta[5])
            lines.append("      </li>")
            lines.append("    </effectOverrides>")
        lines.append("    <qualificationTags>")
        lines.append("      <li>%s</li>" % dir_name.replace("_", ""))
        lines.append("    </qualificationTags>")
        lines.append("  </PersonalChronicle.Domain.Profession.ProfessionalSkillDef>")

    lines.append("</Defs>")
    lines.append("")
    path = os.path.join(ROOT, "Defs", "ProfessionalSkills.Landing.xml")
    io.open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print("Wrote", path)


def append_keys(path, extra):
    text = io.open(path, encoding="utf-8").read()
    if not extra:
        return
    insert = []
    for k, v in extra:
        tag = "  <%s>%s</%s>" % (k, xml_escape(v), k)
        if "<%s>" % k not in text:
            insert.append(tag)
    if not insert:
        return
    if text.rstrip().endswith("</LanguageData>"):
        text = text.rstrip()[:-len("</LanguageData>")].rstrip() + "\n" + "\n".join(insert) + "\n</LanguageData>\n"
        io.open(path, "w", encoding="utf-8", newline="\n").write(text)
        print("Appended", len(insert), "keys to", path)


def honor_and_skill_keys():
    cn = [
        ("PersonalChronicle.UI.Career.Honor.SelectDisplay", "选择显示称号"),
        ("PersonalChronicle.UI.Career.Honor.SelectDisplay.Tip", "从已获职称与勋章中选择一条显示在姓名旁；顶栏只显示这一条，避免叠加。"),
        ("PersonalChronicle.UI.Career.Honor.Select.Title", "选择显示称号"),
        ("PersonalChronicle.UI.Career.Honor.Select.Hint", "地图名牌与殖民者栏只显示一条称号。未选择时自动取主方向最高职称，其次取最高档勋章。"),
        ("PersonalChronicle.UI.Career.Honor.Select.Auto", "自动（主方向最高职称 / 最高勋章）"),
        ("PersonalChronicle.UI.Career.Honor.Select.None", "不显示称号"),
        ("PersonalChronicle.UI.Career.Honor.Select.Group.Title", "已获职称"),
        ("PersonalChronicle.UI.Career.Honor.Select.Group.Medal", "已获勋章"),
        ("PersonalChronicle.UI.Career.Honor.Select.Empty", "尚无已获职称或勋章。"),
        ("PersonalChronicle.UI.Career.Honor.Select.Done", "已选择显示：{0}"),
        ("PersonalChronicle.UI.Career.Honor.Select.Cleared", "已恢复自动选择。"),
        ("PersonalChronicle.UI.Career.Honor.Select.Hidden", "已隐藏人物称号。"),
        ("PersonalChronicle.UI.Career.Honor.Select.Current", "当前显示"),
        ("PersonalChronicle.UI.Career.Honor.Select.Current.Auto", "自动"),
        ("PersonalChronicle.UI.Career.Honor.Select.Current.None", "隐藏"),
        ("PersonalChronicle.UI.Career.Honor.Select.NoCareer", "该殖民者尚无职业档案，无法保存显示称号。"),
        ("Professional.Effect.Engineering_Structure.Signature", "建造速度 +5%"),
        ("Professional.Effect.Engineering_Power.Signature", "建造速度 +4%"),
        ("Professional.Effect.Engineering_Control.Signature", "建造速度 +3%"),
        ("Professional.Effect.Engineering_Precision.Signature", "建造速度 +4%"),
    ]
    en = [
        ("PersonalChronicle.UI.Career.Honor.SelectDisplay", "Choose displayed title"),
        ("PersonalChronicle.UI.Career.Honor.SelectDisplay.Tip", "Pick one granted title or medal to show beside the name. The colonist bar shows only this entry."),
        ("PersonalChronicle.UI.Career.Honor.Select.Title", "Displayed title"),
        ("PersonalChronicle.UI.Career.Honor.Select.Hint", "Map badges and the colonist bar show a single honor. Auto uses the highest title for the primary direction, then the highest medal."),
        ("PersonalChronicle.UI.Career.Honor.Select.Auto", "Auto (highest primary-direction title / highest medal)"),
        ("PersonalChronicle.UI.Career.Honor.Select.None", "Hide title"),
        ("PersonalChronicle.UI.Career.Honor.Select.Group.Title", "Granted titles"),
        ("PersonalChronicle.UI.Career.Honor.Select.Group.Medal", "Granted medals"),
        ("PersonalChronicle.UI.Career.Honor.Select.Empty", "No granted titles or medals yet."),
        ("PersonalChronicle.UI.Career.Honor.Select.Done", "Now displaying: {0}"),
        ("PersonalChronicle.UI.Career.Honor.Select.Cleared", "Restored automatic title."),
        ("PersonalChronicle.UI.Career.Honor.Select.Hidden", "Pawn title hidden."),
        ("PersonalChronicle.UI.Career.Honor.Select.Current", "Currently shown"),
        ("PersonalChronicle.UI.Career.Honor.Select.Current.Auto", "Auto"),
        ("PersonalChronicle.UI.Career.Honor.Select.Current.None", "Hidden"),
        ("PersonalChronicle.UI.Career.Honor.Select.NoCareer", "This colonist has no career archive yet."),
        ("Professional.Effect.Engineering_Structure.Signature", "Construction speed +5%"),
        ("Professional.Effect.Engineering_Power.Signature", "Construction speed +4%"),
        ("Professional.Effect.Engineering_Control.Signature", "Construction speed +3%"),
        ("Professional.Effect.Engineering_Precision.Signature", "Construction speed +4%"),
    ]
    for skill, (label, desc) in SKILL_EN_LABEL.items():
        en.append(("Professional.Skill.%s.Label" % skill, label))
        en.append(("Professional.Skill.%s.Desc" % skill, desc))
    for skill, (label, desc) in SKILL_CN_LABEL.items():
        cn.append(("Professional.Skill.%s.Label" % skill, label))
        cn.append(("Professional.Skill.%s.Desc" % skill, desc))
    # Chinese labels for new skills that already have backup keys — still add current-name aliases
    extra_cn = {
        "ProfessionalSkill_WeaponryManufacturing": ("武器制造", "武器制造专业技能。"),
        "ProfessionalSkill_EquipmentManufacturing": ("装备制造", "装备制造专业技能。"),
        "ProfessionalSkill_IndustrialManufacturing": ("工业制造", "工业制造专业技能。"),
        "ProfessionalSkill_EngineeringPower": ("动力工程", "动力工程专业技能。"),
        "ProfessionalSkill_EngineeringControl": ("电控工程", "电控工程专业技能。"),
        "ProfessionalSkill_EngineeringPrecision": ("精密工程", "精密工程专业技能。"),
        "ProfessionalSkill_AgricultureIrrigation": ("灌溉排灌", "灌溉排灌专业技能。"),
        "ProfessionalSkill_AgricultureBreeding": ("良种繁育", "良种繁育专业技能。"),
        "ProfessionalSkill_AgricultureGreenhouse": ("温室园艺", "温室园艺专业技能。"),
        "ProfessionalSkill_ForestryHarvest": ("林木采伐", "林木采伐专业技能。"),
        "ProfessionalSkill_ForestryNursery": ("育苗抚育", "育苗抚育专业技能。"),
        "ProfessionalSkill_ForestryLumber": ("木材加工", "木材加工专业技能。"),
        "ProfessionalSkill_ForestrySilviculture": ("野化育林", "野化育林专业技能。"),
        "ProfessionalSkill_AnimalHusbandryHusbandry": ("家畜饲养", "家畜饲养专业技能。"),
        "ProfessionalSkill_AnimalHusbandryVeterinary": ("兽医防疫", "兽医防疫专业技能。"),
        "ProfessionalSkill_AnimalHusbandryPasture": ("放牧管理", "放牧管理专业技能。"),
        "ProfessionalSkill_MedicineSurgery": ("外科手术", "外科手术专业技能。"),
        "ProfessionalSkill_MedicinePharmacy": ("药剂调制", "药剂调制专业技能。"),
        "ProfessionalSkill_MedicinePrevention": ("预防医学", "预防医学专业技能。"),
        "ProfessionalSkill_WeaponsHeavy": ("重武器", "重武器专业技能。"),
        "ProfessionalSkill_WeaponsAmmo": ("弹药工程", "弹药工程专业技能。"),
        "ProfessionalSkill_MiningUnderground": ("井下开采", "井下开采专业技能。"),
        "ProfessionalSkill_MiningBeneficiation": ("选矿", "选矿专业技能。"),
        "ProfessionalSkill_MiningBlasting": ("爆破开采", "爆破开采专业技能。"),
        "ProfessionalSkill_ResearchExperiment": ("实验研究", "实验研究专业技能。"),
        "ProfessionalSkill_ResearchApplied": ("应用研究", "应用研究专业技能。"),
        "ProfessionalSkill_ResearchLiterature": ("文献研究", "文献研究专业技能。"),
        "ProfessionalSkill_CookingCuisine": ("精致菜肴", "精致菜肴专业技能。"),
        "ProfessionalSkill_CookingBaking": ("烘焙面点", "烘焙面点专业技能。"),
        "ProfessionalSkill_CookingPreservation": ("食品保存", "食品保存专业技能。"),
        "ProfessionalSkill_ArtPainting": ("绘画", "绘画专业技能。"),
        "ProfessionalSkill_ArtMusic": ("音乐", "音乐专业技能。"),
        "ProfessionalSkill_ArtCraft": ("工艺美术", "工艺美术专业技能。"),
        "ProfessionalSkill_ManagementDispatch": ("调度指挥", "调度指挥专业技能。"),
        "ProfessionalSkill_ManagementLogistics": ("后勤物流", "后勤物流专业技能。"),
        "ProfessionalSkill_ManagementPersonnel": ("人事培养", "人事培养专业技能。"),
    }
    for skill, (label, desc) in extra_cn.items():
        cn.append(("Professional.Skill.%s.Label" % skill, label))
        cn.append(("Professional.Skill.%s.Desc" % skill, desc))
    return cn, en


def main():
    cn_keys = parse_keyed(os.path.join(ROOT, "Languages", "ChineseSimplified", "Keyed", "Archive.xml"))
    en_keys = parse_keyed(os.path.join(ROOT, "Languages", "English", "Keyed", "Archive.xml"))
    rewrite_thingdefs(cn_keys)
    rewrite_recipedefs(cn_keys, en_keys)
    insert_direction_skills()
    write_landing_xml()
    cn_extra, en_extra = honor_and_skill_keys()
    append_keys(os.path.join(ROOT, "Languages", "ChineseSimplified", "Keyed", "Archive.xml"), cn_extra)
    append_keys(os.path.join(ROOT, "Languages", "English", "Keyed", "Archive.xml"), en_extra)
    nested_cn = os.path.join(ROOT, "Languages", "ChineseSimplified", "Keyed", "Keyed", "Archive.xml")
    nested_en = os.path.join(ROOT, "Languages", "English", "Keyed", "Keyed", "Archive.xml")
    if os.path.isfile(nested_cn):
        append_keys(nested_cn, cn_extra)
    if os.path.isfile(nested_en):
        append_keys(nested_en, en_extra)
    print("done")


if __name__ == "__main__":
    main()
