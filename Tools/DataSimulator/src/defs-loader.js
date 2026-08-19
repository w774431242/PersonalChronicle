// Defs 加载器：读取项目 Defs XML → 结构化数据。
// 数据驱动铁律（与游戏一致）：不硬编码门槛/数值；XML 缺失或解析失败时回退内建数据并告警。
// 内建数据与 2026-08-19 的 Defs/*.xml 同步；direction-compare 场景使用 P2-A §7.1 蓝图技能数据。
'use strict';

const fs = require('fs');
const path = require('path');
const { parseXml } = require('./xml');

// src/ → Tools/DataSimulator → Tools → 项目根 → Defs
const DEFS_DIR = path.resolve(__dirname, '../../../Defs');

// ───────────────────────── 内建 fallback（与 Defs XML 同步） ─────────────────────────

const FALLBACK = {
  ProfessionalSkillsXml: `
<Defs>
  <ProfessionalSkillDef>
    <defName>ProfessionalSkill_PrecisionManufacturing</defName>
    <profession>Manufacturing</profession>
    <direction>Manufacturing_Precision</direction>
    <sourceSkills><li>Crafting</li><li>Intellectual</li></sourceSkills>
    <practiceRecipeDefNames><li>Make_ComponentIndustrial</li><li>Make_ComponentSpacer</li></practiceRecipeDefNames>
    <xpPerPracticeBase>10</xpPerPracticeBase><xpDifficulty>1</xpDifficulty><xpCap>5000</xpCap><maxLevel>50</maxLevel>
    <abilityKeys><li>machining</li><li>precisionControl</li><li>processKnowledge</li><li>qualityControl</li></abilityKeys>
    <effectDefNames><li>ProfessionalEffect_ManufacturingWorkSpeed</li><li>ProfessionalEffect_QualityBias</li></effectDefNames>
  </ProfessionalSkillDef>
  <ProfessionalDirectionDef>
    <defName>Manufacturing_Precision</defName><profession>Manufacturing</profession>
    <skillDefNames><li>ProfessionalSkill_PrecisionManufacturing</li></skillDefNames>
    <colorHex>#e0c77a</colorHex><specializationKey>Quality</specializationKey><order>0</order>
  </ProfessionalDirectionDef>
  <ProfessionalEffectDef>
    <defName>ProfessionalEffect_ManufacturingWorkSpeed</defName><kind>WorkSpeed</kind><value>0.03</value>
  </ProfessionalEffectDef>
  <ProfessionalEffectDef>
    <defName>ProfessionalEffect_QualityBias</defName><kind>QualityBias</kind><value>1</value>
  </ProfessionalEffectDef>
  <AbilityMappingDef>
    <defName>Mapping_PrecisionComponents</defName>
    <recipeDefNames><li>Make_ComponentIndustrial</li><li>Make_ComponentSpacer</li></recipeDefNames>
    <workTypeDefName>Smithing</workTypeDefName><mappingKey>PrecisionComponents</mappingKey>
    <weights>
      <li><abilityKey>precisionControl</abilityKey><weight>50</weight></li>
      <li><abilityKey>processKnowledge</abilityKey><weight>30</weight></li>
      <li><abilityKey>machining</abilityKey><weight>15</weight></li>
      <li><abilityKey>qualityControl</abilityKey><weight>5</weight></li>
    </weights>
  </AbilityMappingDef>
  <ProfessionalXpPolicyDef>
    <defName>ProfessionalXpPolicy_Manufacturing</defName>
    <qualityMultipliers>
      <li><qualityName>Legendary</qualityName><multiplier>5</multiplier></li>
      <li><qualityName>Masterwork</qualityName><multiplier>3</multiplier></li>
      <li><qualityName>Excellent</qualityName><multiplier>1.5</multiplier></li>
      <li><qualityName>Good</qualityName><multiplier>1.2</multiplier></li>
    </qualityMultipliers>
  </ProfessionalXpPolicyDef>
  <ProfessionalRatingDef>
    <defName>ProfessionalRating_Proficient</defName><minLevel>10</minLevel><workSpeedWeight>0.03</workSpeedWeight><qualityBiasWeight>0</qualityBiasWeight><order>3</order>
  </ProfessionalRatingDef>
  <ProfessionalRatingDef>
    <defName>ProfessionalRating_Specialist</defName><minLevel>25</minLevel><workSpeedWeight>0.05</workSpeedWeight><qualityBiasWeight>0.02</qualityBiasWeight><order>2</order>
  </ProfessionalRatingDef>
  <ProfessionalRatingDef>
    <defName>ProfessionalRating_Senior</defName><minLevel>38</minLevel><workSpeedWeight>0.08</workSpeedWeight><qualityBiasWeight>0.04</qualityBiasWeight><order>1</order>
  </ProfessionalRatingDef>
  <ProfessionalRatingDef>
    <defName>ProfessionalRating_Master</defName><minLevel>45</minLevel><workSpeedWeight>0.10</workSpeedWeight><qualityBiasWeight>0.06</qualityBiasWeight><order>0</order>
  </ProfessionalRatingDef>
</Defs>`,
  QualificationDefsXml: `
<Defs>
  <QualificationDef>
    <defName>Q_Precision_Junior</defName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName>
    <titleDefName>Title_Precision_Junior</titleDefName><requiredMinLevel>5</requiredMinLevel><requiredCareerTimeTicks>60000</requiredCareerTimeTicks>
    <requiredExam>false</requiredExam><requiredThesis>false</requiredThesis><requiredDefense>false</requiredDefense><minimumScore>0</minimumScore><order>0</order>
  </QualificationDef>
  <QualificationDef>
    <defName>Q_Precision_Assistant</defName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName>
    <titleDefName>Title_Precision_Assistant</titleDefName><requiredMinLevel>15</requiredMinLevel><requiredCareerTimeTicks>200000</requiredCareerTimeTicks>
    <requiredPreviousTitle>Q_Precision_Junior</requiredPreviousTitle><requiredExam>false</requiredExam><requiredThesis>false</requiredThesis><requiredDefense>false</requiredDefense><minimumScore>0</minimumScore><order>1</order>
  </QualificationDef>
  <QualificationDef>
    <defName>Q_Precision_Senior</defName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName>
    <titleDefName>Title_Precision_Senior</titleDefName><requiredMinLevel>25</requiredMinLevel><requiredCareerTimeTicks>600000</requiredCareerTimeTicks>
    <requiredPreviousTitle>Q_Precision_Assistant</requiredPreviousTitle><requiredExam>true</requiredExam><requiredThesis>true</requiredThesis><requiredDefense>true</requiredDefense><minimumScore>60</minimumScore><order>2</order>
  </QualificationDef>
  <QualificationDef>
    <defName>Q_Precision_Specialist</defName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName>
    <titleDefName>Title_Precision_Specialist</titleDefName><requiredMinLevel>38</requiredMinLevel><requiredCareerTimeTicks>1200000</requiredCareerTimeTicks>
    <requiredPreviousTitle>Q_Precision_Senior</requiredPreviousTitle><requiredExam>true</requiredExam><requiredThesis>true</requiredThesis><requiredDefense>true</requiredDefense><minimumScore>70</minimumScore><order>3</order>
  </QualificationDef>
  <QualificationDef>
    <defName>Q_Precision_Master</defName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName>
    <titleDefName>Title_Precision_Master</titleDefName><requiredMinLevel>45</requiredMinLevel><requiredCareerTimeTicks>1800000</requiredCareerTimeTicks>
    <requiredPreviousTitle>Q_Precision_Specialist</requiredPreviousTitle><requiredExam>true</requiredExam><requiredThesis>true</requiredThesis><requiredDefense>true</requiredDefense>
    <requiredAchievements><li><achievementKey>LegendaryMade</achievementKey><minValue>1</minValue></li></requiredAchievements>
    <minimumScore>80</minimumScore><order>4</order>
  </QualificationDef>
  <ProfessionalTitleDef>
    <defName>Title_Precision_Junior</defName><qualificationDefName>Q_Precision_Junior</qualificationDefName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName><order>0</order><autoGrant>true</autoGrant>
  </ProfessionalTitleDef>
  <ProfessionalTitleDef>
    <defName>Title_Precision_Assistant</defName><qualificationDefName>Q_Precision_Assistant</qualificationDefName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName><order>1</order><autoGrant>true</autoGrant>
  </ProfessionalTitleDef>
  <ProfessionalTitleDef>
    <defName>Title_Precision_Senior</defName><qualificationDefName>Q_Precision_Senior</qualificationDefName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName><order>2</order><autoGrant>true</autoGrant>
  </ProfessionalTitleDef>
  <ProfessionalTitleDef>
    <defName>Title_Precision_Specialist</defName><qualificationDefName>Q_Precision_Specialist</qualificationDefName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName><order>3</order><autoGrant>true</autoGrant>
  </ProfessionalTitleDef>
  <ProfessionalTitleDef>
    <defName>Title_Precision_Master</defName><qualificationDefName>Q_Precision_Master</qualificationDefName><professionalSkillDefName>ProfessionalSkill_PrecisionManufacturing</professionalSkillDefName><order>4</order><autoGrant>true</autoGrant>
  </ProfessionalTitleDef>
</Defs>`,
  MedalDefsXml: '<Defs></Defs>', // 模拟器当前不消费勋章 Def，保留空壳
};

// ───────────────────────── XML 树 → 值 ─────────────────────────

function coerce(text) {
  const s = (text || '').trim();
  if (s === '') return null;
  if (s === 'true') return true;
  if (s === 'false') return false;
  // 数字（排除颜色/defName 等以字母/特殊符号开头的字符串）
  if (/^-?\d+(\.\d+)?$/.test(s)) return Number(s);
  return s;
}

function nodeToValue(node) {
  if (!node) return null;
  const children = (node.children || []).filter((c) => c.tag && c.tag.length > 0);
  if (children.length > 0) {
    const isList = children.every((c) => c.tag === 'li');
    if (isList) {
      return children.map((c) => nodeToValue(c));
    }
    const obj = {};
    for (const c of children) {
      const v = nodeToValue(c);
      if (v !== null) obj[c.tag] = v;
    }
    return obj;
  }
  return coerce(node.text);
}

// 顶层按类名分组：返回 { className(短名) : [objects] }
// 项目 Defs XML 的 tag 为完整类名（如 PersonalChronicle.Domain.Profession.ProfessionalSkillDef），
// 统一取短名（最后一段）作为分组键。
function shortTag(tag) {
  const idx = tag.lastIndexOf('.');
  return idx >= 0 ? tag.slice(idx + 1) : tag;
}

function loadDefsXml(fileName) {
  const filePath = path.join(DEFS_DIR, fileName);
  let text;
  let warned = false;
  try {
    text = fs.readFileSync(filePath, 'utf8');
  } catch (e) {
    const fallbackKey = fileName === 'ProfessionalSkills.xml' ? 'ProfessionalSkillsXml'
      : fileName === 'QualificationDefs.xml' ? 'QualificationDefsXml' : 'MedalDefsXml';
    text = FALLBACK[fallbackKey] || '<Defs></Defs>';
    warned = true;
  }
  let root;
  try {
    root = parseXml(text);
  } catch (e) {
    const fallbackKey = fileName === 'ProfessionalSkills.xml' ? 'ProfessionalSkillsXml'
      : fileName === 'QualificationDefs.xml' ? 'QualificationDefsXml' : 'MedalDefsXml';
    root = parseXml(FALLBACK[fallbackKey] || '<Defs></Defs>');
    warned = true;
  }
  const grouped = {};
  for (const child of root.children || []) {
    if (!child.tag || child.tag === 'Defs') continue;
    const key = shortTag(child.tag);
    if (!grouped[key]) grouped[key] = [];
    grouped[key].push(nodeToValue(child));
  }
  return { fileName, grouped, fallbackUsed: warned };
}

// ───────────────────────── 蓝图技能（P2-A §7.1，direction-compare 场景用） ─────────────────────────

// 蓝图数据对齐 制造类职业领域设计.md §7.1 数据表（数值 D5 未冻结，标注来源）
const BLUEPRINT_SKILLS = [
  {
    defName: 'ProfessionalSkill_WeaponManufacturing',
    profession: 'Manufacturing',
    direction: 'Manufacturing_Weaponry',
    sourceSkills: ['Crafting', 'Shooting'],
    practiceRecipeDefNames: [], // 配方随方向垂直切片核验后填写
    xpPerPracticeBase: 12,
    xpDifficulty: 1,
    xpCap: 4500,
    maxLevel: 50,
    abilityKeys: ['machining', 'precisionControl', 'processKnowledge', 'qualityControl'],
    effectDefNames: ['ProfessionalEffect_ManufacturingWorkSpeed', 'ProfessionalEffect_QualityBias'],
    effectOverrides: [
      { effectDefName: 'ProfessionalEffect_ManufacturingWorkSpeed', hasValue: true, value: 0.05, ratingWeightScale: 1.5 },
      { effectDefName: 'ProfessionalEffect_QualityBias', hasValue: true, value: 0, ratingWeightScale: 0 },
    ],
    blueprint: true,
  },
  {
    defName: 'ProfessionalSkill_EquipmentManufacturing',
    profession: 'Manufacturing',
    direction: 'Manufacturing_Equipment',
    sourceSkills: ['Crafting', 'Construction'],
    practiceRecipeDefNames: [],
    xpPerPracticeBase: 10,
    xpDifficulty: 1,
    xpCap: 5000,
    maxLevel: 50,
    abilityKeys: ['materialApplication', 'qualityControl', 'machining', 'processKnowledge'],
    effectDefNames: ['ProfessionalEffect_ManufacturingWorkSpeed'],
    effectOverrides: [
      { effectDefName: 'ProfessionalEffect_ManufacturingWorkSpeed', hasValue: true, value: 0.02, ratingWeightScale: 0.8 },
    ],
    blueprint: true,
  },
  {
    defName: 'ProfessionalSkill_IndustrialManufacturing',
    profession: 'Manufacturing',
    direction: 'Manufacturing_Industrial',
    sourceSkills: ['Crafting', 'Intellectual'],
    practiceRecipeDefNames: [],
    xpPerPracticeBase: 11,
    xpDifficulty: 1,
    xpCap: 5500,
    maxLevel: 50,
    abilityKeys: ['processKnowledge', 'machining', 'precisionControl', 'qualityControl'],
    effectDefNames: ['ProfessionalEffect_ManufacturingWorkSpeed'],
    effectOverrides: [
      { effectDefName: 'ProfessionalEffect_ManufacturingWorkSpeed', hasValue: true, value: 0.04, ratingWeightScale: 1.2 },
    ],
    blueprint: true,
  },
];

// ───────────────────────── 加载入口 ─────────────────────────

function loadDefs(options) {
  const opts = options || {};
  const skillsXml = loadDefsXml('ProfessionalSkills.xml');
  const qualsXml = loadDefsXml('QualificationDefs.xml');
  const medalsXml = loadDefsXml('MedalDefs.xml');

  const defs = {
    skills: new Map(),
    directions: new Map(),
    effects: new Map(),
    ratings: [],
    mappings: [],
    xpPolicies: [],
    qualifications: [],
    titles: [],
    medals: [],
    fallbacks: [],
  };

  if (skillsXml.fallbackUsed) defs.fallbacks.push('ProfessionalSkills.xml(内置)');
  if (qualsXml.fallbackUsed) defs.fallbacks.push('QualificationDefs.xml(内置)');
  if (medalsXml.fallbackUsed) defs.fallbacks.push('MedalDefs.xml(内置)');

  for (const d of skillsXml.grouped.ProfessionalSkillDef || []) {
    if (d && d.defName) defs.skills.set(d.defName, d);
  }
  if (opts.includeBlueprint) {
    for (const b of BLUEPRINT_SKILLS) {
      if (!defs.skills.has(b.defName)) defs.skills.set(b.defName, b);
    }
  }
  for (const d of skillsXml.grouped.ProfessionalDirectionDef || []) {
    if (d && d.defName) defs.directions.set(d.defName, d);
  }
  for (const d of skillsXml.grouped.ProfessionalEffectDef || []) {
    if (d && d.defName) defs.effects.set(d.defName, d);
  }
  defs.ratings = (skillsXml.grouped.ProfessionalRatingDef || []).filter((d) => d && d.defName);
  defs.mappings = (skillsXml.grouped.AbilityMappingDef || []).filter((d) => d && d.defName);
  defs.xpPolicies = (skillsXml.grouped.ProfessionalXpPolicyDef || []).filter((d) => d && d.defName);
  defs.qualifications = (qualsXml.grouped.QualificationDef || []).filter((d) => d && d.defName);
  defs.titles = (qualsXml.grouped.ProfessionalTitleDef || []).filter((d) => d && d.defName);
  defs.medals = (medalsXml.grouped.MedalDef || []).filter((d) => d && d.defName);

  // 排序：资格按 order，评级按 order（order 小者高档）
  defs.qualifications.sort((a, b) => (a.order || 0) - (b.order || 0));
  defs.ratings.sort((a, b) => (a.order || 0) - (b.order || 0));
  return defs;
}

module.exports = { loadDefs, BLUEPRINT_SKILLS };
