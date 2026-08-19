// 数据管线核心（Tools/DataSimulator）。
// 公式与 C# 对齐（转写表见 docs/设计文档/功能模块/数据模拟工具/数据模拟工具需求与设计.md §2.3）：
//   ProfessionalXpEvaluator / ProfessionalRatingEvaluator / ProfessionalEffectResolver /
//   QualificationEvaluator / ExamScoring / AchievementEvaluator（2026-08-19 版本对齐）
// 铁律：事实层 append-only；评价层只派生不污染事实；数据全部来自 defs（不硬编码）。
// UMD 双环境：Node（module.exports）与浏览器（window.SimCore，供 dev 调试 UI 使用）。
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  } else {
    root.SimCore = factory();
  }
})(typeof self !== 'undefined' ? self : this, function () {
'use strict';

// ───────────────────────── ① 品质系数（ProfessionalXpEvaluator.QualityMultiplier） ─────────────────────────

function qualityMultiplier(qualityName, entries) {
  if (!qualityName) return 1;
  if (entries && entries.length > 0) {
    for (const e of entries) {
      if (e && e.qualityName === qualityName) return Number(e.multiplier);
    }
  }
  switch (qualityName) {
    case 'Legendary': return 5;
    case 'Masterwork': return 3;
    case 'Excellent': return 1.5;
    case 'Good': return 1.2;
    default: return 1;
  }
}

// ───────────────────────── ② 单次 XP（ComputePracticeXp） ─────────────────────────

function computePracticeXp(baseValue, recipeRelevance, qm, difficulty, quantity) {
  if (baseValue <= 0 || recipeRelevance <= 0) return 0;
  const q = quantity < 1 ? 1 : quantity > 4 ? 4 : quantity;
  const d = difficulty <= 0 ? 1 : difficulty;
  const qm2 = qm <= 0 ? 1 : qm;
  const rel = recipeRelevance > 1 ? 1 : recipeRelevance;
  return baseValue * rel * qm2 * d * q;
}

// ───────────────────────── ③ Level / Mastery（LevelFromXp / MasteryFromLevel） ─────────────────────────

function levelFromXp(xp, maxLevel, xpCap) {
  if (maxLevel <= 0 || xp <= 0 || xpCap <= 0) return 0;
  const t = Math.min(1, xp / xpCap);
  const levelF = maxLevel * (1 - Math.pow(1 - t, 0.4));
  const level = Math.floor(levelF);
  return level < 0 ? 0 : level > maxLevel ? maxLevel : level;
}

function masteryFromLevel(level, maxLevel) {
  if (maxLevel <= 0 || level <= 0) return 0;
  const m = (level / maxLevel) * 100;
  return m > 100 ? 100 : m;
}

// ───────────────────────── ④ 评级（ResolveRating：阈值≤level 的最高档，order 最小） ─────────────────────────

function resolveRating(level, ratings) {
  if (!ratings || ratings.length === 0) return null;
  let best = null;
  for (const def of ratings) {
    if (!def || level < Number(def.minLevel)) continue;
    if (!best || Number(def.order) < Number(best.order)) best = def;
  }
  return best;
}

// ───────────────────────── ⑤ 效果（ResolveSpeedFactor / ResolveQualityLevels / ClampQuality） ─────────────────────────

function findOverride(skillDef, effectDefName) {
  if (!skillDef || !skillDef.effectOverrides || skillDef.effectOverrides.length === 0 || !effectDefName) return null;
  for (const ov of skillDef.effectOverrides) {
    if (ov && ov.effectDefName === effectDefName) return ov;
  }
  return null;
}

function applyOverride(effectDef, skillDef, ratingWeight) {
  let baseValue = Number(effectDef.value);
  let rw = ratingWeight || 0;
  const ov = findOverride(skillDef, effectDef.defName);
  if (ov) {
    if (ov.hasValue) baseValue = Number(ov.value);
    if (ov.ratingWeightScale !== undefined && Number(ov.ratingWeightScale) !== 1) {
      rw *= Number(ov.ratingWeightScale);
    }
  }
  return { baseValue, rw };
}

function resolveSpeedFactor(skillData, skillDefs, effectDefs, recipeDefName, ratings) {
  // skillData: { skillDefName, level }；模拟器单技能 pawn，直接按技能解析
  const skillDef = skillDefs.get(skillData.skillDefName);
  if (!skillDef || !skillData.level || skillData.level < 1) return 1;
  const rating = resolveRating(skillData.level, ratings);
  const ratingWeight = rating ? Number(rating.workSpeedWeight || 0) : 0;
  let bonus = 0;
  for (const name of skillDef.effectDefNames || []) {
    const effectDef = effectDefs.get(name);
    if (!effectDef || effectDef.kind !== 'WorkSpeed') continue;
    if (recipeDefName && skillDef.practiceRecipeDefNames && skillDef.practiceRecipeDefNames.length > 0
      && !skillDef.practiceRecipeDefNames.includes(recipeDefName)) continue;
    const { baseValue, rw } = applyOverride(effectDef, skillDef, ratingWeight);
    bonus += baseValue * (1 + rw);
  }
  return 1 + bonus;
}

function resolveQualityLevels(skillData, skillDefs, effectDefs, recipeDefName, ratings) {
  const skillDef = skillDefs.get(skillData.skillDefName);
  if (!skillDef || !skillData.level || skillData.level < 1) return 0;
  const rating = resolveRating(skillData.level, ratings);
  const ratingWeight = rating ? Number(rating.qualityBiasWeight || 0) : 0;
  let levels = 0;
  for (const name of skillDef.effectDefNames || []) {
    const effectDef = effectDefs.get(name);
    if (!effectDef || effectDef.kind !== 'QualityBias') continue;
    if (recipeDefName && skillDef.practiceRecipeDefNames && skillDef.practiceRecipeDefNames.length > 0
      && !skillDef.practiceRecipeDefNames.includes(recipeDefName)) continue;
    const { baseValue, rw } = applyOverride(effectDef, skillDef, ratingWeight);
    levels += Math.trunc(baseValue * (1 + rw));
  }
  return levels;
}

function clampQuality(currentIndex, levels) {
  let index = currentIndex + levels;
  if (index < 0) index = 0;
  if (index > 6) index = 6;
  return index;
}

// ───────────────────────── ⑥ 考试评分（ExamScoring） ─────────────────────────

function qualityRank(quality) {
  if (!quality) return -1;
  switch (quality) {
    case 'Awful': return 0;
    case 'Poor': return 1;
    case 'Normal': return 2;
    case 'Good': return 3;
    case 'Excellent': return 4;
    case 'Masterwork': return 5;
    case 'Legendary': return 6;
    default: return -1;
  }
}

function countAtLeast(qualities, minQuality) {
  if (!qualities || qualities.length === 0 || !minQuality) return 0;
  const minRank = qualityRank(minQuality);
  if (minRank < 0) return 0;
  let met = 0;
  for (const q of qualities) if (qualityRank(q) >= minRank) met++;
  return met;
}

function scorePractical(requiredCount, producedCount, producedQualities, minQuality, startedTick, timeLimitTicks, nowTick) {
  if (requiredCount <= 0 || producedCount <= 0 || !producedQualities || producedQualities.length === 0) return 0;
  const q = Math.min(producedCount, requiredCount) / requiredCount;
  const met = countAtLeast(producedQualities, minQuality);
  const qd = met / producedQualities.length;
  const inTime = timeLimitTicks <= 0 || nowTick <= startedTick + timeLimitTicks;
  const tFactor = inTime ? 1 : 0.6;
  return 100 * q * (0.5 + 0.5 * qd) * tFactor;
}

// ───────────────────────── ⑦ 成就聚合（AchievementEvaluator.Aggregate） ─────────────────────────

function aggregateAchievements(events, grantedTitles) {
  let legendary = 0, major = 0, examPass = 0, titleGrant = 0;
  let first = Number.MAX_SAFE_INTEGER, last = 0;
  for (const ev of events) {
    if (ev.tick < first) first = ev.tick;
    if (ev.tick > last) last = ev.tick;
    if (ev.type === 'ItemProduced') {
      if (ev.quality === 'Legendary') legendary++;
      if (ev.metadata && ev.metadata.major === '1') major++;
    } else if (ev.type === 'ExamPassed') {
      examPass++;
    } else if (ev.type === 'TitleGranted') {
      titleGrant++;
    }
  }
  const out = {
    LegendaryMade: legendary,
    MajorProjects: major,
    ExamPassCount: examPass,
    TitleCount: titleGrant,
    LongServiceTicks: first === Number.MAX_SAFE_INTEGER ? 0 : last - first,
  };
  return out;
}

// ───────────────────────── ⑧ 资格判定（QualificationEvaluator.EvaluateOne） ─────────────────────────

function evaluateQualification(def, pawn) {
  // pawn: { level, spanTicks, achievements, grantedTitles, examsPassed, thesisPassed, defensePassed,
  //         practicalScore, theoryScore, thesisScore, defenseScore }
  const skillMaxLevel = 50; // 对齐 C# 兜底（运行时入口按技能 maxLevel，模拟器统一 50）

  // 1. 专业等级
  if (pawn.level < Number(def.requiredMinLevel || 0)) return { eligible: false, reason: 'level' };

  // 2. 职业时长
  if (pawn.spanTicks < Number(def.requiredCareerTimeTicks || 0)) return { eligible: false, reason: 'careerTime' };

  // 3. 事实门槛（requiredEvents，当前 Defs 未使用）
  // 4. 成就门槛
  if (def.requiredAchievements && def.requiredAchievements.length > 0) {
    for (const req of def.requiredAchievements) {
      if (!req || !req.achievementKey) continue;
      const val = pawn.achievements[req.achievementKey] || 0;
      if (val < Number(req.minValue)) return { eligible: false, reason: 'achievement:' + req.achievementKey };
    }
  }

  // 5. 前置职称（2026-08-19 与 C# 修复同步：requiredPreviousTitle 存资格 defName，双键匹配）
  if (def.requiredPreviousTitle
    && !pawn.grantedTitles.includes(def.requiredPreviousTitle)
    && !(pawn.grantedQuals || []).includes(def.requiredPreviousTitle)) {
    return { eligible: false, reason: 'previousTitle' };
  }

  // 6. 考试/论文/答辩
  if (def.requiredExam && !pawn.examsPassed) return { eligible: false, reason: 'exam' };
  if (def.requiredThesis && !pawn.thesisPassed) return { eligible: false, reason: 'thesis' };
  if (def.requiredDefense && !pawn.defensePassed) return { eligible: false, reason: 'defense' };

  // 综合评分：0.25 实践 + 0.20 理论 + 0.20 论文 + 0.15 答辩 + 0.20 等级
  const composite =
    0.25 * pawn.practicalScore +
    0.20 * pawn.theoryScore +
    0.20 * pawn.thesisScore +
    0.15 * pawn.defenseScore +
    0.20 * ((pawn.level / skillMaxLevel) * 100);
  if (composite < Number(def.minimumScore || 0)) return { eligible: false, reason: 'score' };

  return { eligible: true, reason: 'ok', compositeScore: composite };
}

// ───────────────────────── 品质分布采样 ─────────────────────────

function qualitySampler(spec) {
  if (!spec) spec = 'mix';
  if (typeof spec === 'string') {
    if (spec.startsWith('all:')) {
      const q = spec.slice(4);
      return () => q;
    }
    if (spec === 'mix') {
      return weighted(['Normal', 'Good', 'Excellent', 'Masterwork', 'Legendary'],
        [0.4, 0.3, 0.2, 0.08, 0.02]);
    }
    throw new Error('unknown quality spec: ' + spec);
  }
  if (typeof spec === 'object') {
    const keys = Object.keys(spec);
    const weights = keys.map((k) => Number(spec[k]));
    return weighted(keys, weights);
  }
  throw new Error('bad quality spec');
}

function weighted(keys, weights) {
  const total = weights.reduce((a, b) => a + b, 0);
  return function pick() {
    let r = Math.random() * total;
    for (let i = 0; i < keys.length; i++) {
      r -= weights[i];
      if (r <= 0) return keys[i];
    }
    return keys[keys.length - 1];
  };
}

// ───────────────────────── 主模拟 ─────────────────────────

/**
 * config: {
 *   pawns: [{
 *     name, skillDefName, recipeDefName,
 *     count, intervalTicks, startTick, quality (采样规格),
 *     quantity (产出数量，默认 1),
 *     examMode: 'pass' | 'blocked' | 'simulate'（默认 pass）
 *     thesisMode: 'pass' | 'blocked'（默认 pass）
 *   }],
 *   snapshotEvery: 快照间隔（行为次数），默认 25
 *   rng: 可选随机函数
 * }
 */
function simulateRun(config, defs) {
  const snapshotEvery = config.snapshotEvery || 25;
  const results = [];

  for (const cfg of config.pawns) {
    const skillDef = defs.skills.get(cfg.skillDefName);
    if (!skillDef) throw new Error('skill not found: ' + cfg.skillDefName);
    const pick = qualitySampler(cfg.quality);
    const rng = config.rng || Math.random;

    // pawn 状态
    const state = {
      xp: 0,
      level: 0,
      mastery: 0,
      abilityXp: {},
      practiceCount: 0,
      firstTick: 0,
      lastTick: 0,
      events: [],
      grantedTitles: [],
      grantedQuals: [],
    };

    const snapshots = [];
    const milestones = [];
    let tick = cfg.startTick || 0;

    for (let i = 0; i < cfg.count; i++) {
      tick += cfg.intervalTicks;
      const quality = pick();
      const quantity = cfg.quantity || 1;

      // ② 事实层：ItemProduced（append-only）
      state.events.push({ tick, type: 'ItemProduced', defName: cfg.recipeDefName, quality, quantity, recipeDefName: cfg.recipeDefName });
      if (state.firstTick <= 0) state.firstTick = tick;
      state.lastTick = tick;
      state.practiceCount++;

      // ③ 状态层：XP / 能力 / 等级
      const qm = qualityMultiplier(quality, firstXpPolicy(defs));
      const xp = computePracticeXp(Number(skillDef.xpPerPracticeBase || 10), 1, qm, Number(skillDef.xpDifficulty || 1), quantity);
      state.xp += xp;
      splitAbilityXp(state, skillDef, defs, cfg.recipeDefName, xp);
      state.level = levelFromXp(state.xp, Number(skillDef.maxLevel || 50), Number(skillDef.xpCap || 5000));
      state.mastery = masteryFromLevel(state.level, Number(skillDef.maxLevel || 50));

      // ④⑤ 评级 + 效果（当前状态）
      const rating = resolveRating(state.level, defs.ratings);
      const speedFactor = resolveSpeedFactor(
        { skillDefName: cfg.skillDefName, level: state.level }, defs.skills, defs.effects, cfg.recipeDefName, defs.ratings);
      const qualityLevels = resolveQualityLevels(
        { skillDefName: cfg.skillDefName, level: state.level }, defs.skills, defs.effects, cfg.recipeDefName, defs.ratings);

      // 里程碑：评级达成（首次）
      if (rating && !milestones.some((m) => m.type === 'rating' && m.label === rating.defName)) {
        milestones.push({ tick, type: 'rating', label: rating.defName, value: '评级 ' + rating.defName });
      }

      // 快照
      if ((i + 1) % snapshotEvery === 0 || i === cfg.count - 1) {
        snapshots.push({
          index: i + 1,
          tick,
          xp: round(state.xp, 1),
          level: state.level,
          mastery: round(state.mastery, 1),
          rating: rating ? rating.defName : null,
          speedFactor: round(speedFactor, 4),
          qualityLevels,
        });
      }
    }

    // ⑥⑦ 评价层：成就 + 资格链 + 授予
    const achievements = aggregateAchievements(state.events, state.grantedTitles);
    const spanTicks = state.lastTick - state.firstTick;
    const exam = simulateExam(cfg, state, defs, milestones);
    const thesis = simulateThesisDefense(cfg, state, milestones);

    const pawnEval = {
      level: state.level,
      spanTicks,
      achievements,
      grantedTitles: state.grantedTitles,
      grantedQuals: state.grantedQuals,
      examsPassed: exam.passed,
      thesisPassed: thesis.passed,
      defensePassed: thesis.passed,
      practicalScore: exam.practicalScore,
      theoryScore: exam.theoryScore,
      thesisScore: thesis.thesisScore,
      defenseScore: thesis.defenseScore,
    };

    const qualifications = [];
    for (const q of defs.qualifications) {
      if (!q.professionalSkillDefName || q.professionalSkillDefName !== cfg.skillDefName) continue;
      const res = evaluateQualification(q, pawnEval);
      const title = defs.titles.find((t) => t && t.defName === q.titleDefName);
      if (res.eligible && title && title.autoGrant !== false && !state.grantedTitles.includes(title.defName)) {
        state.grantedTitles.push(title.defName);
        state.grantedQuals.push(q.defName);
        state.events.push({ tick: state.lastTick, type: 'TitleGranted', defName: title.defName });
        milestones.push({ tick: state.lastTick, type: 'title', label: title.defName, value: '授予 ' + title.defName });
      }
      qualifications.push({
        defName: q.defName,
        titleDefName: q.titleDefName,
        requiredMinLevel: q.requiredMinLevel,
        requiredCareerTimeTicks: q.requiredCareerTimeTicks,
        eligible: res.eligible,
        reason: res.reason,
        compositeScore: res.eligible ? round(res.compositeScore, 1) : null,
        granted: state.grantedTitles.includes(q.titleDefName),
      });
    }

    results.push({
      name: cfg.name,
      skillDefName: cfg.skillDefName,
      recipeDefName: cfg.recipeDefName,
      qualitySpec: typeof cfg.quality === 'string' ? cfg.quality : 'custom',
      count: cfg.count,
      intervalTicks: cfg.intervalTicks,
      examMode: cfg.examMode || 'pass',
      thesisMode: cfg.thesisMode || 'pass',
      final: {
        xp: round(state.xp, 1),
        level: state.level,
        mastery: round(state.mastery, 1),
        practiceCount: state.practiceCount,
        spanTicks,
        rating: resolveRating(state.level, defs.ratings) ? resolveRating(state.level, defs.ratings).defName : null,
        speedFactor: resolveSpeedFactor({ skillDefName: cfg.skillDefName, level: state.level }, defs.skills, defs.effects, cfg.recipeDefName, defs.ratings),
        qualityLevels: resolveQualityLevels({ skillDefName: cfg.skillDefName, level: state.level }, defs.skills, defs.effects, cfg.recipeDefName, defs.ratings),
        abilityXp: roundMap(state.abilityXp),
        stats: achievements,
      },
      snapshots,
      milestones,
      qualifications,
      grantedTitles: state.grantedTitles.slice(),
      fallbacks: defs.fallbacks,
    });
  }
  return { config, results };
}

function firstXpPolicy(defs) {
  for (const p of defs.xpPolicies) {
    if (p.qualityMultipliers && p.qualityMultipliers.length > 0) return p.qualityMultipliers;
  }
  return null;
}

function splitAbilityXp(state, skillDef, defs, recipeDefName, xp) {
  const mapping = defs.mappings.find((m) => {
    if (!m.recipeDefNames || m.recipeDefNames.length === 0) return true;
    return m.recipeDefNames.includes(recipeDefName);
  });
  if (!mapping || !mapping.weights || mapping.weights.length === 0) return;
  const total = mapping.weights.reduce((a, w) => a + Number(w.weight), 0);
  if (total <= 0) return;
  for (const w of mapping.weights) {
    if (!w.abilityKey) continue;
    state.abilityXp[w.abilityKey] = (state.abilityXp[w.abilityKey] || 0) + (xp * Number(w.weight)) / total;
  }
}

function simulateExam(cfg, state, defs, milestones) {
  const mode = cfg.examMode || 'pass';
  if (mode === 'pass') {
    const passed = true;
    const practicalScore = 90;
    const theoryScore = 85;
    return { passed, practicalScore, theoryScore };
  }
  if (mode === 'blocked') {
    return { passed: false, practicalScore: 0, theoryScore: 0 };
  }
  if (mode === 'simulate') {
    // 实践考试：RequiredCount=3、MinQuality=Excellent、时限 100000 tick，按品质分布抽取
    const requiredCount = 3;
    const minQuality = 'Excellent';
    const timeLimit = 100000;
    const pick = qualitySampler(cfg.quality);
    const produced = [];
    let now = state.lastTick;
    for (let i = 0; i < requiredCount; i++) produced.push(pick());
    const started = Math.max(state.firstTick, now - 50000);
    const score = scorePractical(requiredCount, requiredCount, produced, minQuality, started, timeLimit, now);
    const met = countAtLeast(produced, minQuality);
    const passed = score > 0 && met >= requiredCount;
    if (passed) {
      state.events.push({ tick: now, type: 'ExamPassed', defName: cfg.skillDefName });
    }
    return { passed, practicalScore: round(score, 1), theoryScore: passed ? 85 : 0 };
  }
  return { passed: false, practicalScore: 0, theoryScore: 0 };
}

function simulateThesisDefense(cfg, state, milestones) {
  const mode = cfg.thesisMode || 'pass';
  if (mode === 'pass') {
    return { passed: true, thesisScore: 88, defenseScore: 90 };
  }
  return { passed: false, thesisScore: 0, defenseScore: 0 };
}

function round(v, d) {
  const p = Math.pow(10, d);
  return Math.round(v * p) / p;
}

function roundMap(map) {
  const out = {};
  for (const k of Object.keys(map)) out[k] = round(map[k], 1);
  return out;
}

return {
  simulateRun,
  qualitySampler,
  // 导出纯函数便于金样自测 / 浏览器调试 UI 复用
  qualityMultiplier,
  computePracticeXp,
  levelFromXp,
  masteryFromLevel,
  resolveRating,
  resolveSpeedFactor,
  resolveQualityLevels,
  clampQuality,
  qualityRank,
  countAtLeast,
  scorePractical,
  aggregateAchievements,
  evaluateQualification,
};

});
