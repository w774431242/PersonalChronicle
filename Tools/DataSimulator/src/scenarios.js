// 预置场景（对齐 SDD §2.4）。
// 每个场景 = { name, describe, configs: [simulateRun config, ...] }
'use strict';

const PRECISION_SKILL = 'ProfessionalSkill_PrecisionManufacturing';
const RECIPE = 'Make_ComponentIndustrial';

module.exports = {
  scenarios: {
    'precision-grind': {
      describe: '基准成长：单 pawn 精密制造 500 次组件（品质 mix），观察 XP/等级/评级/加成曲线与资格序列',
      configs: [
        {
          snapshotEvery: 25,
          pawns: [
            {
              name: 'PawnA',
              skillDefName: PRECISION_SKILL,
              recipeDefName: RECIPE,
              count: 500,
              intervalTicks: 1000,
              startTick: 0,
              quality: 'mix',
            },
          ],
        },
      ],
    },

    'quality-strategy': {
      describe: '品质策略对比：同条件三组 pawn（全 Normal / 全 Excellent / 全 Legendary）300 次，观察成长速度差异',
      configs: [
        {
          snapshotEvery: 25,
          pawns: [
            { name: 'AllNormal', skillDefName: PRECISION_SKILL, recipeDefName: RECIPE, count: 300, intervalTicks: 1000, startTick: 0, quality: 'all:Normal' },
            { name: 'AllExcellent', skillDefName: PRECISION_SKILL, recipeDefName: RECIPE, count: 300, intervalTicks: 1000, startTick: 0, quality: 'all:Excellent' },
            { name: 'AllLegendary', skillDefName: PRECISION_SKILL, recipeDefName: RECIPE, count: 300, intervalTicks: 1000, startTick: 0, quality: 'all:Legendary' },
          ],
        },
      ],
    },

    'direction-compare': {
      describe: '4 方向差异化对比（蓝图数据，P2-A §7.1）：同条件 400 次制造，对比等级/加成/评级缩放差异',
      configs: [
        {
          snapshotEvery: 40,
          pawns: [
            { name: 'Precision(品质)', skillDefName: PRECISION_SKILL, recipeDefName: RECIPE, count: 400, intervalTicks: 1000, startTick: 0, quality: 'mix' },
            { name: 'Weaponry(产量)', skillDefName: 'ProfessionalSkill_WeaponManufacturing', recipeDefName: RECIPE, count: 400, intervalTicks: 1000, startTick: 0, quality: 'mix' },
            { name: 'Equipment(材料)', skillDefName: 'ProfessionalSkill_EquipmentManufacturing', recipeDefName: RECIPE, count: 400, intervalTicks: 1000, startTick: 0, quality: 'mix' },
            { name: 'Industrial(批量)', skillDefName: 'ProfessionalSkill_IndustrialManufacturing', recipeDefName: RECIPE, count: 400, intervalTicks: 1000, startTick: 0, quality: 'mix' },
          ],
        },
      ],
    },

    'qualification-gap': {
      describe: '资格缺口分析：1000 次制造后各档职称缺口；对比 考试通过 vs 未通过（P1-5 已裁决：论文/答辩门槛临时关闭，不再阻塞 Senior+）',
      configs: [
        {
          snapshotEvery: 100,
          pawns: [
            {
              name: 'ExamPass(考试通过)',
              skillDefName: PRECISION_SKILL,
              recipeDefName: RECIPE,
              count: 1000,
              intervalTicks: 1000,
              startTick: 0,
              quality: 'mix',
              examMode: 'pass',
              thesisMode: 'pass',
            },
          ],
        },
        {
          snapshotEvery: 100,
          pawns: [
            {
              name: 'ExamBlocked(考试未通过)',
              skillDefName: PRECISION_SKILL,
              recipeDefName: RECIPE,
              count: 1000,
              intervalTicks: 1000,
              startTick: 0,
              quality: 'mix',
              examMode: 'blocked',
              thesisMode: 'pass',
            },
          ],
        },
      ],
    },
  },
};
