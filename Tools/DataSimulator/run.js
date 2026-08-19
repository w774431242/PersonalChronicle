// 数据模拟工具 CLI 入口。
// 用法：
//   node run.js                 # 跑全部预置场景，输出到 out/<时间戳>/（含交互式 HTML 报告）
//   node run.js --scenario precision-grind
//   node run.js --list          # 列出场景
//   node run.js --selftest      # 公式金样自测（与 C# 转写表断言）
//   node run.js --out <目录>    # 指定输出目录
//   node run.js --doc <目录>    # 生成后额外复制一份 report.html 到指定目录（如 docs 文档目录）
//   node run.js --open          # 跑完后用系统默认浏览器打开 HTML 报告
'use strict';

const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const { loadDefs } = require('./src/defs-loader');
const { simulateRun } = require('./src/sim-core');
const { scenarios } = require('./src/scenarios');
const report = require('./src/report');

function parseArgs(argv) {
  const args = { scenario: null, out: null, list: false, selftest: false, open: false, doc: null };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--scenario') args.scenario = argv[++i];
    else if (a === '--out') args.out = argv[++i];
    else if (a === '--list') args.list = true;
    else if (a === '--selftest') args.selftest = true;
    else if (a === '--open') args.open = true;
    else if (a === '--doc') args.doc = argv[++i];
  }
  return args;
}

function openInBrowser(filePath) {
  try {
    if (process.platform === 'win32') {
      spawn('cmd', ['/c', 'start', '', filePath], { detached: true, stdio: 'ignore' }).unref();
    } else if (process.platform === 'darwin') {
      spawn('open', [filePath], { detached: true, stdio: 'ignore' }).unref();
    } else {
      spawn('xdg-open', [filePath], { detached: true, stdio: 'ignore' }).unref();
    }
    return true;
  } catch (e) {
    return false;
  }
}

// ───────────────────────── 金样自测（SDD §3.1） ─────────────────────────

function runSelfTest() {
  const core = require('./src/sim-core');
  const asserts = [];
  const check = (label, actual, expected, eps) => {
    const ok = Math.abs(actual - expected) <= (eps || 1e-6);
    asserts.push({ label, ok, actual, expected });
  };

  // 品质系数（含 policy 优先）
  check('qm Legendary', core.qualityMultiplier('Legendary', null), 5);
  check('qm Masterwork', core.qualityMultiplier('Masterwork', null), 3);
  check('qm Excellent', core.qualityMultiplier('Excellent', null), 1.5);
  check('qm Good', core.qualityMultiplier('Good', null), 1.2);
  check('qm Normal', core.qualityMultiplier('Normal', null), 1);
  check('qm policy 优先', core.qualityMultiplier('Excellent', [{ qualityName: 'Excellent', multiplier: 2 }]), 2);

  // 单次 XP（对齐 C# ComputePracticeXp：base×rel×qm×d×q，q clamp [1,4]）
  check('xp 基础', core.computePracticeXp(10, 1, 1, 1, 1), 10);
  check('xp 品质 x1.5', core.computePracticeXp(10, 1, 1.5, 1, 1), 15);
  check('xp 数量 clamp 4', core.computePracticeXp(10, 1, 1, 1, 8), 40);
  check('xp 相关度 0', core.computePracticeXp(10, 0, 1, 1, 1), 0);

  // Level 曲线：5000 XP @ xpCap 5000 → 50；2500 XP → 50×(1−0.5^0.4)≈50×0.242≈12
  check('level 0', core.levelFromXp(0, 50, 5000), 0);
  check('level cap', core.levelFromXp(5000, 50, 5000), 50);
  check('level half', core.levelFromXp(2500, 50, 5000), Math.floor(50 * (1 - Math.pow(0.5, 0.4))));
  check('mastery', core.masteryFromLevel(25, 50), 50);

  // 评级：阈值 10/25/38/45
  const ratings = [
    { defName: 'R4', minLevel: 45, order: 0 },
    { defName: 'R3', minLevel: 38, order: 1 },
    { defName: 'R2', minLevel: 25, order: 2 },
    { defName: 'R1', minLevel: 10, order: 3 },
  ];
  check('rating null@9', core.resolveRating(9, ratings) === null ? 1 : 0, 1);
  check('rating R1@10', core.resolveRating(10, ratings).defName === 'R1' ? 1 : 0, 1);
  check('rating R3@40', core.resolveRating(40, ratings).defName === 'R3' ? 1 : 0, 1);
  check('rating R4@50', core.resolveRating(50, ratings).defName === 'R4' ? 1 : 0, 1);

  // 品质 clamp（对齐 C# ClampQuality int）
  check('clamp Normal+1→Good(3)', core.clampQuality(2, 1), 3);
  check('clamp Legendary+2→Legendary(6)', core.clampQuality(6, 2), 6);
  check('clamp Awful−1→Awful(0)', core.clampQuality(0, -1), 0);
  check('clamp Masterwork−2→Good(3)', core.clampQuality(5, -2), 3);

  // 考试评分（对齐 ExamScoring.ScorePractical）
  check('exam 全优', core.scorePractical(3, 3, ['Excellent', 'Excellent', 'Excellent'], 'Excellent', 0, 1000, 500), 100);
  check('exam 超时 x0.6', core.scorePractical(3, 3, ['Excellent', 'Excellent', 'Excellent'], 'Excellent', 0, 1000, 2000), 60);
  check('exam 品质不足', core.scorePractical(3, 3, ['Excellent', 'Normal', 'Poor'], 'Excellent', 0, 1000, 500), 100 * (0.5 + 0.5 / 3));
  check('countAtLeast', core.countAtLeast(['Excellent', 'Normal', 'Masterwork', 'Poor'], 'Excellent'), 2);

  // 综合评分（对齐 QualificationEvaluator W 权重：0.25/0.2/0.2/0.15/0.2）
  const pawn = { level: 30, spanTicks: 1000, achievements: {}, grantedTitles: [], examsPassed: true, thesisPassed: true, defensePassed: true, practicalScore: 90, theoryScore: 90, thesisScore: 90, defenseScore: 90 };
  const def = { defName: 'Q1', requiredMinLevel: 25, requiredCareerTimeTicks: 500, requiredExam: true, requiredThesis: true, requiredDefense: true, minimumScore: 60 };
  const res = core.evaluateQualification(def, pawn);
  const expectedScore = 0.25 * 90 + 0.2 * 90 + 0.2 * 90 + 0.15 * 90 + 0.2 * (30 / 50 * 100);
  check('qual composite', res.eligible ? res.compositeScore : -1, expectedScore);
  check('qual eligible', res.eligible ? 1 : 0, 1);

  // 失败路径
  const p2 = { level: 20, spanTicks: 1000, achievements: {}, grantedTitles: [], examsPassed: false, thesisPassed: true, defensePassed: true, practicalScore: 0, theoryScore: 0, thesisScore: 90, defenseScore: 90 };
  check('qual exam fail', core.evaluateQualification(def, p2).eligible ? 0 : 1, 1);

  let fail = 0;
  for (const a of asserts) {
    if (!a.ok) {
      fail++;
      console.log('  ✗ ' + a.label + '  expected=' + a.expected + ' actual=' + a.actual);
    }
  }
  if (fail === 0) console.log('✓ 金样自测通过：' + asserts.length + ' 项断言全部一致（对齐 C# 转写表）');
  else console.log('✗ 金样自测失败：' + fail + '/' + asserts.length);
  return fail === 0;
}

// ───────────────────────── 主流程 ─────────────────────────

function main() {
  const args = parseArgs(process.argv.slice(2));

  if (args.selftest) {
    process.exit(runSelfTest() ? 0 : 1);
    return;
  }

  if (args.list) {
    console.log('可用场景：');
    for (const name of Object.keys(scenarios)) {
      console.log('  ' + name.padEnd(22) + scenarios[name].describe);
    }
    return;
  }

  const defs = loadDefs({ includeBlueprint: true });
  if (defs.fallbacks.length > 0) {
    console.log('⚠ 警告：以下 Defs 使用内置 fallback（XML 缺失或解析失败）：' + defs.fallbacks.join(', '));
  }
  console.log('✓ Defs 加载：技能 ' + defs.skills.size + ' / 方向 ' + defs.directions.size + ' / 效果 ' + defs.effects.size
    + ' / 评级 ' + defs.ratings.length + ' / 资格 ' + defs.qualifications.length + ' / 职称 ' + defs.titles.length);

  const names = args.scenario ? [args.scenario] : Object.keys(scenarios);
  const outRoot = args.out ? path.resolve(args.out) : path.join(__dirname, 'out', new Date().toISOString().replace(/[:.]/g, '-'));
  const plan = [];
  const rng = Math.random;

  for (const name of names) {
    const sc = scenarios[name];
    if (!sc) {
      console.error('✗ 未知场景: ' + name + '（--list 查看）');
      process.exit(1);
    }
    console.log('');
    console.log('▶ 场景: ' + name + ' — ' + sc.describe);
    for (let i = 0; i < sc.configs.length; i++) {
      const cfg = Object.assign({ rng }, sc.configs[i]);
      const res = simulateRun(cfg, defs);
      plan.push({ scenario: name, runIndex: i + 1, describe: sc.describe, results: res.results });
      console.log('  run ' + (i + 1) + ': ' + cfg.pawns.map((p) => p.name + '(' + p.count + '次)').join(', ') + ' 完成');
    }
  }

  console.log(report.consoleSummary(plan));
  const files = report.writeAll(outRoot, 'PersonalChronicle 职业数据模拟报告', plan);
  console.log('输出:');
  console.log('  ' + files.csvPath);
  console.log('  ' + files.htmlPath);

  // --doc：同步一份 report.html 到文档目录（固定名覆盖，对齐 docs 下"当前基准"预览惯例）
  if (args.doc) {
    try {
      const docDir = path.resolve(args.doc);
      fs.mkdirSync(docDir, { recursive: true });
      const docPath = path.join(docDir, '职业数据模拟报告.html');
      fs.copyFileSync(files.htmlPath, docPath);
      console.log('  📄 已同步到文档目录: ' + docPath);
    } catch (e) {
      console.log('  ⚠ --doc 同步失败: ' + e.message);
    }
  }

  if (args.open) {
    const ok = openInBrowser(files.htmlPath);
    console.log(ok ? '已用默认浏览器打开 HTML 报告。' : '打开浏览器失败，请手动打开上面的 html 路径。');
  }
}

main();
