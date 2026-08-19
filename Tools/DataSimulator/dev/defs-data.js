// 自动生成（node src/gen-data.js）：Defs/*.xml → 浏览器数据。请勿手改。
window.SIM_DEFS = {
 "skills": {
  "ProfessionalSkill_PrecisionManufacturing": {
   "defName": "ProfessionalSkill_PrecisionManufacturing",
   "profession": "Manufacturing",
   "direction": "Manufacturing_Precision",
   "sourceSkills": [
    "Crafting",
    "Intellectual"
   ],
   "practiceRecipeDefNames": [
    "Make_ComponentIndustrial",
    "Make_ComponentSpacer"
   ],
   "xpPerPracticeBase": 10,
   "xpDifficulty": 1,
   "xpCap": 5000,
   "maxLevel": 50,
   "abilityKeys": [
    "machining",
    "precisionControl",
    "processKnowledge",
    "qualityControl"
   ],
   "effectDefNames": [
    "ProfessionalEffect_ManufacturingWorkSpeed",
    "ProfessionalEffect_QualityBias"
   ],
   "qualificationTags": [
    "ManufacturingPrecision"
   ]
  },
  "ProfessionalSkill_WeaponManufacturing": {
   "defName": "ProfessionalSkill_WeaponManufacturing",
   "profession": "Manufacturing",
   "direction": "Manufacturing_Weaponry",
   "sourceSkills": [
    "Crafting",
    "Shooting"
   ],
   "practiceRecipeDefNames": [],
   "xpPerPracticeBase": 12,
   "xpDifficulty": 1,
   "xpCap": 4500,
   "maxLevel": 50,
   "abilityKeys": [
    "machining",
    "precisionControl",
    "processKnowledge",
    "qualityControl"
   ],
   "effectDefNames": [
    "ProfessionalEffect_ManufacturingWorkSpeed",
    "ProfessionalEffect_QualityBias"
   ],
   "effectOverrides": [
    {
     "effectDefName": "ProfessionalEffect_ManufacturingWorkSpeed",
     "hasValue": true,
     "value": 0.05,
     "ratingWeightScale": 1.5
    },
    {
     "effectDefName": "ProfessionalEffect_QualityBias",
     "hasValue": true,
     "value": 0,
     "ratingWeightScale": 0
    }
   ],
   "blueprint": true
  },
  "ProfessionalSkill_EquipmentManufacturing": {
   "defName": "ProfessionalSkill_EquipmentManufacturing",
   "profession": "Manufacturing",
   "direction": "Manufacturing_Equipment",
   "sourceSkills": [
    "Crafting",
    "Construction"
   ],
   "practiceRecipeDefNames": [],
   "xpPerPracticeBase": 10,
   "xpDifficulty": 1,
   "xpCap": 5000,
   "maxLevel": 50,
   "abilityKeys": [
    "materialApplication",
    "qualityControl",
    "machining",
    "processKnowledge"
   ],
   "effectDefNames": [
    "ProfessionalEffect_ManufacturingWorkSpeed"
   ],
   "effectOverrides": [
    {
     "effectDefName": "ProfessionalEffect_ManufacturingWorkSpeed",
     "hasValue": true,
     "value": 0.02,
     "ratingWeightScale": 0.8
    }
   ],
   "blueprint": true
  },
  "ProfessionalSkill_IndustrialManufacturing": {
   "defName": "ProfessionalSkill_IndustrialManufacturing",
   "profession": "Manufacturing",
   "direction": "Manufacturing_Industrial",
   "sourceSkills": [
    "Crafting",
    "Intellectual"
   ],
   "practiceRecipeDefNames": [],
   "xpPerPracticeBase": 11,
   "xpDifficulty": 1,
   "xpCap": 5500,
   "maxLevel": 50,
   "abilityKeys": [
    "processKnowledge",
    "machining",
    "precisionControl",
    "qualityControl"
   ],
   "effectDefNames": [
    "ProfessionalEffect_ManufacturingWorkSpeed"
   ],
   "effectOverrides": [
    {
     "effectDefName": "ProfessionalEffect_ManufacturingWorkSpeed",
     "hasValue": true,
     "value": 0.04,
     "ratingWeightScale": 1.2
    }
   ],
   "blueprint": true
  }
 },
 "directions": {
  "Manufacturing_Precision": {
   "defName": "Manufacturing_Precision",
   "profession": "Manufacturing",
   "skillDefNames": [
    "ProfessionalSkill_PrecisionManufacturing"
   ],
   "colorHex": "#e0c77a",
   "labelKey": "Profession.Direction.Manufacturing_Precision.Label",
   "specializationKey": "Quality",
   "specializationDescKey": "Profession.Direction.Manufacturing_Precision.Specialization",
   "order": 0
  },
  "Manufacturing_Weaponry": {
   "defName": "Manufacturing_Weaponry",
   "profession": "Manufacturing",
   "colorHex": "#c9a0a0",
   "labelKey": "Profession.Direction.Manufacturing_Weaponry.Label",
   "specializationKey": "Throughput",
   "specializationDescKey": "Profession.Direction.Manufacturing_Weaponry.Specialization",
   "order": 1
  },
  "Manufacturing_Equipment": {
   "defName": "Manufacturing_Equipment",
   "profession": "Manufacturing",
   "colorHex": "#7aa0c0",
   "labelKey": "Profession.Direction.Manufacturing_Equipment.Label",
   "specializationKey": "Material",
   "specializationDescKey": "Profession.Direction.Manufacturing_Equipment.Specialization",
   "order": 2
  },
  "Manufacturing_Industrial": {
   "defName": "Manufacturing_Industrial",
   "profession": "Manufacturing",
   "colorHex": "#a0b078",
   "labelKey": "Profession.Direction.Manufacturing_Industrial.Label",
   "specializationKey": "Volume",
   "specializationDescKey": "Profession.Direction.Manufacturing_Industrial.Specialization",
   "order": 3
  }
 },
 "effects": {
  "ProfessionalEffect_ManufacturingWorkSpeed": {
   "defName": "ProfessionalEffect_ManufacturingWorkSpeed",
   "kind": "WorkSpeed",
   "value": 0.03,
   "labelKey": "Professional.Skill.PrecisionManufacturing.EffectWorkSpeed"
  },
  "ProfessionalEffect_QualityBias": {
   "defName": "ProfessionalEffect_QualityBias",
   "kind": "QualityBias",
   "value": 1,
   "labelKey": "Professional.Skill.PrecisionManufacturing.EffectQualityBias"
  }
 },
 "ratings": [
  {
   "defName": "ProfessionalRating_Master",
   "labelKey": "Professional.Rating.Master.Label",
   "minLevel": 45,
   "workSpeedWeight": 0.1,
   "qualityBiasWeight": 0.06,
   "order": 0
  },
  {
   "defName": "ProfessionalRating_Senior",
   "labelKey": "Professional.Rating.Senior.Label",
   "minLevel": 38,
   "workSpeedWeight": 0.08,
   "qualityBiasWeight": 0.04,
   "order": 1
  },
  {
   "defName": "ProfessionalRating_Specialist",
   "labelKey": "Professional.Rating.Specialist.Label",
   "minLevel": 25,
   "workSpeedWeight": 0.05,
   "qualityBiasWeight": 0.02,
   "order": 2
  },
  {
   "defName": "ProfessionalRating_Proficient",
   "labelKey": "Professional.Rating.Proficient.Label",
   "minLevel": 10,
   "workSpeedWeight": 0.03,
   "qualityBiasWeight": 0,
   "order": 3
  }
 ],
 "mappings": [
  {
   "defName": "Mapping_PrecisionComponents",
   "recipeDefNames": [
    "Make_ComponentIndustrial",
    "Make_ComponentSpacer"
   ],
   "workTypeDefName": "Smithing",
   "mappingKey": "PrecisionComponents",
   "weights": [
    {
     "abilityKey": "precisionControl",
     "weight": 50
    },
    {
     "abilityKey": "processKnowledge",
     "weight": 30
    },
    {
     "abilityKey": "machining",
     "weight": 15
    },
    {
     "abilityKey": "qualityControl",
     "weight": 5
    }
   ]
  }
 ],
 "xpPolicies": [
  {
   "defName": "ProfessionalXpPolicy_Manufacturing",
   "qualityMultipliers": [
    {
     "qualityName": "Legendary",
     "multiplier": 5
    },
    {
     "qualityName": "Masterwork",
     "multiplier": 3
    },
    {
     "qualityName": "Excellent",
     "multiplier": 1.5
    },
    {
     "qualityName": "Good",
     "multiplier": 1.2
    }
   ]
  }
 ],
 "qualifications": [
  {
   "defName": "Q_Precision_Junior",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "titleDefName": "Title_Precision_Junior",
   "requiredMinLevel": 5,
   "requiredCareerTimeTicks": 60000,
   "requiredExam": false,
   "requiredThesis": false,
   "requiredDefense": false,
   "minimumScore": 0,
   "order": 0
  },
  {
   "defName": "Q_Precision_Assistant",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "titleDefName": "Title_Precision_Assistant",
   "requiredMinLevel": 15,
   "requiredCareerTimeTicks": 200000,
   "requiredPreviousTitle": "Q_Precision_Junior",
   "requiredExam": false,
   "requiredThesis": false,
   "requiredDefense": false,
   "minimumScore": 0,
   "order": 1
  },
  {
   "defName": "Q_Precision_Senior",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "titleDefName": "Title_Precision_Senior",
   "requiredMinLevel": 25,
   "requiredCareerTimeTicks": 600000,
   "requiredPreviousTitle": "Q_Precision_Assistant",
   "requiredExam": true,
   "requiredThesis": true,
   "requiredDefense": true,
   "minimumScore": 60,
   "order": 2
  },
  {
   "defName": "Q_Precision_Specialist",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "titleDefName": "Title_Precision_Specialist",
   "requiredMinLevel": 38,
   "requiredCareerTimeTicks": 1200000,
   "requiredPreviousTitle": "Q_Precision_Senior",
   "requiredExam": true,
   "requiredThesis": true,
   "requiredDefense": true,
   "minimumScore": 70,
   "order": 3
  },
  {
   "defName": "Q_Precision_Master",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "titleDefName": "Title_Precision_Master",
   "requiredMinLevel": 45,
   "requiredCareerTimeTicks": 1800000,
   "requiredPreviousTitle": "Q_Precision_Specialist",
   "requiredExam": true,
   "requiredThesis": true,
   "requiredDefense": true,
   "requiredAchievements": [
    {
     "achievementKey": "LegendaryMade",
     "minValue": 1
    }
   ],
   "minimumScore": 80,
   "order": 4
  }
 ],
 "titles": [
  {
   "defName": "Title_Precision_Junior",
   "qualificationDefName": "Q_Precision_Junior",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "order": 0,
   "autoGrant": true
  },
  {
   "defName": "Title_Precision_Assistant",
   "qualificationDefName": "Q_Precision_Assistant",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "order": 1,
   "autoGrant": true
  },
  {
   "defName": "Title_Precision_Senior",
   "qualificationDefName": "Q_Precision_Senior",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "order": 2,
   "autoGrant": true
  },
  {
   "defName": "Title_Precision_Specialist",
   "qualificationDefName": "Q_Precision_Specialist",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "order": 3,
   "autoGrant": true
  },
  {
   "defName": "Title_Precision_Master",
   "qualificationDefName": "Q_Precision_Master",
   "professionalSkillDefName": "ProfessionalSkill_PrecisionManufacturing",
   "order": 4,
   "autoGrant": true
  }
 ],
 "medals": [
  {
   "defName": "Medal_Labor_Model_Bronze",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "metricKey": "workTime",
   "threshold": 4800000,
   "iconPath": "Medals/Medal_Labor_Model_Bronze",
   "buffDefName": "PersonalChronicleMedalBuffWorkSpeedBronze",
   "order": 0
  },
  {
   "defName": "Medal_Labor_Model_Silver",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Silver",
   "metricKey": "workTime",
   "threshold": 12000000,
   "iconPath": "Medals/Medal_Labor_Model_Silver",
   "buffDefName": "PersonalChronicleMedalBuffWorkSpeedSilver",
   "order": 1
  },
  {
   "defName": "Medal_Labor_Model_Gold",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Gold",
   "metricKey": "workTime",
   "threshold": 30000000,
   "iconPath": "Medals/Medal_Labor_Model_Gold",
   "buffDefName": "PersonalChronicleMedalBuffWorkSpeedGold",
   "order": 2
  },
  {
   "defName": "Medal_Labor_Worker_Bronze",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "metricKey": "productionQuantity",
   "threshold": 300,
   "order": 3
  },
  {
   "defName": "Medal_Labor_Worker_Silver",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Silver",
   "metricKey": "productionQuantity",
   "threshold": 1000,
   "order": 4
  },
  {
   "defName": "Medal_Labor_Worker_Gold",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Gold",
   "metricKey": "productionQuantity",
   "threshold": 3000,
   "order": 5
  },
  {
   "defName": "Medal_Labor_TechAce_Bronze",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "metricKey": "productionSilver",
   "threshold": 30000,
   "buffDefName": "PersonalChronicleMedalBuffWorkSpeedBronze",
   "order": 6
  },
  {
   "defName": "Medal_Labor_TechAce_Silver",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Silver",
   "metricKey": "productionSilver",
   "threshold": 100000,
   "buffDefName": "PersonalChronicleMedalBuffWorkSpeedSilver",
   "order": 7
  },
  {
   "defName": "Medal_Labor_TechAce_Gold",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Gold",
   "metricKey": "productionSilver",
   "threshold": 300000,
   "buffDefName": "PersonalChronicleMedalBuffWorkSpeedGold",
   "order": 8
  },
  {
   "defName": "Medal_Combat_Hero_Bronze",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "metricKey": "kills",
   "threshold": 20,
   "buffDefName": "PersonalChronicleMedalBuffCombatBronze",
   "order": 9
  },
  {
   "defName": "Medal_Combat_Hero_Silver",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Silver",
   "metricKey": "kills",
   "threshold": 50,
   "buffDefName": "PersonalChronicleMedalBuffCombatSilver",
   "order": 10
  },
  {
   "defName": "Medal_Combat_Hero_Gold",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Gold",
   "metricKey": "kills",
   "threshold": 120,
   "buffDefName": "PersonalChronicleMedalBuffCombatGold",
   "order": 11
  },
  {
   "defName": "Medal_Combat_FirstClass_Bronze",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "metricKey": "damageDealt",
   "threshold": 2000,
   "order": 12
  },
  {
   "defName": "Medal_Combat_FirstClass_Silver",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Silver",
   "metricKey": "damageDealt",
   "threshold": 5000,
   "order": 13
  },
  {
   "defName": "Medal_Combat_FirstClass_Gold",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Gold",
   "metricKey": "damageDealt",
   "threshold": 12000,
   "order": 14
  },
  {
   "defName": "Medal_Combat_Enlistee_Bronze",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "metricKey": "participatedBattles",
   "threshold": 3,
   "order": 15
  },
  {
   "defName": "Medal_Combat_Enlistee_Silver",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Silver",
   "metricKey": "participatedBattles",
   "threshold": 8,
   "order": 16
  },
  {
   "defName": "Medal_Combat_Enlistee_Gold",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Gold",
   "metricKey": "participatedBattles",
   "threshold": 20,
   "order": 17
  },
  {
   "defName": "Medal_Support_Quartermaster_Bronze",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "metricKey": "consumptionSilver",
   "threshold": 8000,
   "order": 18
  },
  {
   "defName": "Medal_Support_Quartermaster_Silver",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Silver",
   "metricKey": "consumptionSilver",
   "threshold": 20000,
   "order": 19
  },
  {
   "defName": "Medal_Support_Quartermaster_Gold",
   "kind": "Threshold",
   "ownerType": "Pawn",
   "tier": "Gold",
   "metricKey": "consumptionSilver",
   "threshold": 50000,
   "order": 20
  },
  {
   "defName": "Medal_Legacy_Heirloom_Bronze",
   "kind": "Threshold",
   "ownerType": "Thing",
   "tier": "Bronze",
   "metricKey": "heirloomHolders",
   "threshold": 2,
   "order": 21
  },
  {
   "defName": "Medal_Legacy_Heirloom_Silver",
   "kind": "Threshold",
   "ownerType": "Thing",
   "tier": "Silver",
   "metricKey": "heirloomHolders",
   "threshold": 3,
   "order": 22
  },
  {
   "defName": "Medal_Legacy_Heirloom_Gold",
   "kind": "Threshold",
   "ownerType": "Thing",
   "tier": "Gold",
   "metricKey": "heirloomHolders",
   "threshold": 5,
   "order": 23
  },
  {
   "defName": "Medal_Legacy_KillerBlade_Bronze",
   "kind": "Threshold",
   "ownerType": "Thing",
   "tier": "Bronze",
   "metricKey": "legacyKills",
   "threshold": 30,
   "buffDefName": "PersonalChronicleMedalBuffCombatBronze",
   "order": 24
  },
  {
   "defName": "Medal_Legacy_KillerBlade_Silver",
   "kind": "Threshold",
   "ownerType": "Thing",
   "tier": "Silver",
   "metricKey": "legacyKills",
   "threshold": 100,
   "buffDefName": "PersonalChronicleMedalBuffCombatSilver",
   "order": 25
  },
  {
   "defName": "Medal_Legacy_KillerBlade_Gold",
   "kind": "Threshold",
   "ownerType": "Thing",
   "tier": "Gold",
   "metricKey": "legacyKills",
   "threshold": 250,
   "buffDefName": "PersonalChronicleMedalBuffCombatGold",
   "order": 26
  },
  {
   "defName": "Medal_Craft_Legend_Bronze",
   "kind": "Achievement",
   "ownerType": "Pawn",
   "tier": "Bronze",
   "achievementKey": "LegendaryMade",
   "achievementThreshold": 1,
   "order": 27
  },
  {
   "defName": "Medal_Craft_Legend_Silver",
   "kind": "Achievement",
   "ownerType": "Pawn",
   "tier": "Silver",
   "achievementKey": "LegendaryMade",
   "achievementThreshold": 5,
   "order": 28
  },
  {
   "defName": "Medal_Craft_Legend_Gold",
   "kind": "Achievement",
   "ownerType": "Pawn",
   "tier": "Gold",
   "achievementKey": "LegendaryMade",
   "achievementThreshold": 15,
   "order": 29
  },
  {
   "defName": "Medal_Craft_MajorProject_Gold",
   "kind": "Achievement",
   "ownerType": "Pawn",
   "tier": "Gold",
   "achievementKey": "MajorProjects",
   "achievementThreshold": 3,
   "order": 30
  }
 ],
 "fallbacks": []
};
