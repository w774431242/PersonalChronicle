// 报告生成：CSV（逐 tick 快照）+ 交互式 HTML（数据嵌入 + 浏览器端渲染器）+ 控制台摘要。
'use strict';

const fs = require('fs');
const path = require('path');

// ───────────────────────── CSV ─────────────────────────

function buildCsv(plan) {
  // plan: [{ scenario, runIndex, results }]
  const lines = ['scenario,run,pawn,skill,index,tick,xp,level,mastery,rating,speedFactor,qualityLevels'];
  for (const p of plan) {
    for (const r of p.results) {
      for (const s of r.snapshots) {
        lines.push([
          p.scenario, p.runIndex, r.name, r.skillDefName, s.index, s.tick, s.xp, s.level,
          s.mastery, s.rating || '', s.speedFactor, s.qualityLevels,
        ].join(','));
      }
    }
  }
  return lines.join('\n');
}

// ───────────────────────── 控制台摘要 ─────────────────────────

function pad(s, n) {
  s = String(s);
  return s.length >= n ? s : s + ' '.repeat(n - s.length);
}

function consoleSummary(plan) {
  const out = [];
  for (const p of plan) {
    out.push('');
    out.push('===== ' + p.scenario + ' (run ' + p.runIndex + ') =====');
    out.push('');
    for (const r of p.results) {
      const f = r.final;
      out.push(pad('pawn: ' + r.name, 26)
        + pad('level ' + f.level + '/50', 12)
        + pad('rating: ' + (f.rating || '-'), 18)
        + pad('speed x' + f.speedFactor.toFixed(4), 14)
        + 'qualityBias +' + f.qualityLevels);
      out.push(pad('  span: ' + f.spanTicks + ' tick', 26)
        + pad('xp: ' + f.xp, 14)
        + pad('legendary: ' + f.stats.LegendaryMade, 18)
        + 'titles: ' + (r.grantedTitles.join(', ') || '-'));
      if (r.qualifications.length > 0) {
        for (const q of r.qualifications) {
          out.push('  [资格] ' + pad(q.defName, 22)
            + (q.eligible ? 'ELIGIBLE' : 'BLOCKED(' + q.reason + ')')
            + (q.compositeScore != null ? ' score=' + q.compositeScore : '')
            + (q.granted ? ' → 已授予 ' + q.titleDefName : ''));
        }
      }
    }
  }
  out.push('');
  const fb = plan.length > 0 ? plan[0].results.flatMap((r) => r.fallbacks || []) : [];
  if (fb.length > 0) out.push('⚠ fallback 数据使用中: ' + fb.join(', '));
  return out.join('\n');
}

// ───────────────────────── 交互式 HTML ─────────────────────────

const RENDERER_PATH = path.join(__dirname, 'renderer.js');

const CSS = `
  * { box-sizing: border-box; }
  body { font-family: system-ui, "Microsoft YaHei", sans-serif; margin: 0; background: #f3f4f6; color: #1f2937; }
  #app { max-width: 1180px; margin: 0 auto; padding: 20px 24px 60px; }
  h1 { font-size: 22px; margin: 8px 0 4px; }
  .meta { color: #6b7280; font-size: 12px; margin: 0 0 16px; }
  h2 { font-size: 16px; border-bottom: 2px solid #e5e7eb; padding-bottom: 6px; margin: 20px 0 10px; }
  h3 { font-size: 14px; margin: 0 0 6px; } h3 small { color: #6b7280; font-weight: normal; font-size: 12px; }
  .tabs { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 14px; }
  .tab { padding: 6px 14px; border: 1px solid #d1d5db; background: #fff; border-radius: 6px; cursor: pointer; font-size: 13px; }
  .tab.active { background: #111827; color: #fff; border-color: #111827; }
  .panel { display: none; }
  .selectors { background: #fff; border: 1px solid #e5e7eb; border-radius: 8px; padding: 10px 14px; margin-bottom: 12px; }
  .checkrow { display: flex; flex-wrap: wrap; gap: 6px 16px; font-size: 13px; }
  .check, .radio { display: inline-flex; align-items: center; gap: 4px; cursor: pointer; }
  .radiorow { margin-top: 8px; padding-top: 8px; border-top: 1px dashed #e5e7eb; display: flex; flex-wrap: wrap; gap: 6px 14px; font-size: 13px; }
  .charts { display: flex; flex-wrap: wrap; gap: 16px; }
  .chartbox { flex: 1 1 460px; background: #fff; border: 1px solid #e5e7eb; border-radius: 8px; padding: 10px 12px; }
  .chart-title { font-size: 12px; color: #6b7280; margin-bottom: 4px; }
  .chart { width: 100%; height: auto; }
  .host { position: relative; }
  .tt { position: absolute; background: rgba(17,24,39,0.92); color: #fff; border-radius: 6px; padding: 6px 10px; font-size: 12px; pointer-events: none; z-index: 10; min-width: 130px; }
  .tt-name { font-weight: 600; margin-bottom: 3px; }
  .tt-row { display: flex; justify-content: space-between; gap: 12px; }
  .tt-k { color: #d1d5db; } .tt-v { font-family: ui-monospace, Consolas, monospace; }
  .radars { display: flex; flex-wrap: wrap; gap: 14px; margin-top: 14px; }
  .radarbox { background: #fff; border: 1px solid #e5e7eb; border-radius: 8px; padding: 8px 10px; flex: 1 1 260px; max-width: 300px; }
  .radar-cap { font-size: 12px; color: #374151; margin-bottom: 2px; }
  .empty { color: #9ca3af; font-size: 12px; padding: 8px; }
  .qualsection { margin-top: 14px; }
  .card { background: #fff; border: 1px solid #e5e7eb; border-radius: 8px; padding: 12px 16px; margin-bottom: 12px; }
  .tl-title { font-size: 12px; color: #6b7280; margin: 8px 0 4px; }
  .tl-row { display: flex; align-items: center; gap: 8px; margin: 3px 0; font-size: 12px; }
  .tl-bar { height: 12px; border-radius: 3px; min-width: 8px; }
  .tl-ok { background: #16a34a; } .tl-bad { background: #f87171; }
  .tl-label { white-space: nowrap; } .tl-status { white-space: nowrap; }
  .ok { color: #16a34a; font-weight: 600; } .bad { color: #dc2626; font-weight: 600; }
  .milestones { font-size: 12px; color: #374151; margin-top: 8px; }
  .milestones ul { margin: 4px 0 0; padding-left: 18px; }
`;

function buildHtml(plan, title) {
  // 展平为嵌入数据：场景 → runs → pawns
  const scenarios = plan.map((p) => ({
    name: p.scenario,
    describe: p.describe || '',
    runs: [
      {
        runIndex: p.runIndex,
        results: p.results.map((r) => ({
          name: r.name,
          skillDefName: r.skillDefName,
          qualitySpec: r.qualitySpec,
          count: r.count,
          intervalTicks: r.intervalTicks,
          examMode: r.examMode,
          thesisMode: r.thesisMode,
          final: r.final,
          snapshots: r.snapshots,
          milestones: r.milestones,
          qualifications: r.qualifications.map((q) => ({
            defName: q.defName,
            titleDefName: q.titleDefName,
            reqTicks: q.requiredCareerTimeTicks,
            eligible: q.eligible,
            reason: q.reason,
            compositeScore: q.compositeScore,
            granted: q.granted,
          })),
          grantedTitles: r.grantedTitles,
        })),
      },
    ],
  }));

  const fallbackUsed = plan.length > 0
    && plan[0].results.length > 0
    && plan[0].results[0].fallbacks
    && plan[0].results[0].fallbacks.length > 0;

  const data = {
    title,
    generatedAt: new Date().toLocaleString(),
    dataSource: fallbackUsed ? 'Defs/*.xml（部分使用内置 fallback）' : 'Defs/*.xml（实时读取）',
    scenarios,
  };

  const renderer = fs.readFileSync(RENDERER_PATH, 'utf8');

  return `<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8"><title>${esc(title)}</title>
<style>${CSS}</style></head>
<body>
<div id="app"></div>
<script>window.__SIM_DATA__ = ${JSON.stringify(data)};</script>
<script>${renderer}</script>
</body></html>`;
}

function esc(s) {
  return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// ───────────────────────── 写出 ─────────────────────────

function writeAll(outDir, title, plan) {
  fs.mkdirSync(outDir, { recursive: true });
  const csv = buildCsv(plan);
  const html = buildHtml(plan, title);
  fs.writeFileSync(path.join(outDir, 'report.csv'), csv, 'utf8');
  fs.writeFileSync(path.join(outDir, 'report.html'), html, 'utf8');
  return { csvPath: path.join(outDir, 'report.csv'), htmlPath: path.join(outDir, 'report.html') };
}

module.exports = { buildCsv, buildHtml, consoleSummary, writeAll };
