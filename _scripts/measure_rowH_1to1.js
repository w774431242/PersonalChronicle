/* 预览 ↔ 落地 行高 1:1 对照实测
   目的：GATE-UI 裁决 UI-001 要求落地 rowH=18，并同步修正预览 HTML。
        本脚本实测预览页市场表 .dt 行的真实渲染高度，与落地常量对比。
   跑法：NODE_PATH=<managed>/node_modules <managed node> _scripts/measure_rowH_1to1.js */
const puppeteer = require('puppeteer');

const FILE = 'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const LANDED_ROW_H = 18;   // Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs

(async () => {
  const browser = await puppeteer.launch({
    headless: 'new', executablePath: CHROME,
    args: ['--no-sandbox', '--disable-dev-shm-usage', '--font-render-hinting=none']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1000 });
  await page.goto('file:///' + FILE.replace(/\\/g, '/'), { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 500));

  const res = await page.evaluate(() => {
    const out = { tables: [] };
    document.querySelectorAll('table.dt').forEach((t, ti) => {
      const rows = Array.from(t.querySelectorAll('tbody tr'));
      const hs = rows.map(r => r.getBoundingClientRect().height);
      const cs = rows.length ? getComputedStyle(rows[0]) : null;
      const cellCs = rows.length && rows[0].children.length
        ? getComputedStyle(rows[0].children[0]) : null;
      out.tables.push({
        index: ti,
        rowCount: rows.length,
        heights: hs,
        min: hs.length ? Math.min(...hs) : null,
        max: hs.length ? Math.max(...hs) : null,
        rowLineHeight: cs ? cs.lineHeight : null,
        rowFontSize: cs ? cs.fontSize : null,
        cellPadding: cellCs ? cellCs.padding : null,
        cellLineHeight: cellCs ? cellCs.lineHeight : null,
        cellFontSize: cellCs ? cellCs.fontSize : null,
      });
    });
    return out;
  });

  console.log('=== 预览页 table.dt 实测 ===');
  for (const t of res.tables) {
    console.log(`\n[表 #${t.index}] 行数=${t.rowCount}`);
    console.log(`  行高 min=${t.min} max=${t.max} (px)`);
    console.log(`  tr  line-height=${t.rowLineHeight} font-size=${t.rowFontSize}`);
    console.log(`  td  padding=${t.cellPadding} line-height=${t.cellLineHeight} font-size=${t.cellFontSize}`);
    const avg = t.rowCount ? (t.heights.reduce((a, b) => a + b, 0) / t.rowCount) : 0;
    console.log(`  平均行高 = ${avg.toFixed(2)}px`);
    console.log(`  落地常量 = ${LANDED_ROW_H}px`);
    const d = avg - LANDED_ROW_H;
    console.log(`  偏差 = ${d >= 0 ? '+' : ''}${d.toFixed(2)}px  ${Math.abs(d) < 0.5 ? '✅ 1:1 一致' : '❌ 不一致'}`);
  }
  await browser.close();
})();
