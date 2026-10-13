/**
 * 市场面板列宽 1:1 一致性测量（预览 → 落地 RUIF shares）
 *
 * 背景：预览用 `table-layout:fixed` + `width:100%` + 固定 px 列宽
 *   （品种 124px / 指标 52px×4 / border-spacing 4px）。
 * 落地用 RUIF `StatTable(shares)` —— shares 是**纯比例**，无 px 概念。
 *
 * 因此必须实测预览在真实面板宽度下的**渲染比例**，再换算成 shares，
 * 而不是拿 CSS 里的 px 数字直接当 shares 用（那是估算，不是标定）。
 *
 * 输出：各列实测 px 宽度 + 归一化比例 + 对应 shares 建议值。
 */
const puppeteer = require('puppeteer');
const path = require('path');

const HTML = process.argv[2] || path.resolve(
  '系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html');

(async () => {
  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });
  const page = await browser.newPage();

  // 固定视口，保证可复现（与既有验收脚本同一尺寸）
  await page.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 1 });
  await page.goto('file:///' + HTML.replace(/\\/g, '/'), { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 600));

  const data = await page.evaluate(() => {
    const table = document.querySelector('table.dt');
    if (!table) return { error: 'table.dt not found' };

    const tableRect = table.getBoundingClientRect();

    // 表头单元格实测宽度（用表头，避免数据行缺单元格影响）
    const ths = Array.from(table.querySelectorAll('thead th'));
    const headers = ths.map(th => {
      const r = th.getBoundingClientRect();
      const cs = getComputedStyle(th);
      return {
        text: (th.textContent || '').trim(),
        width: +r.width.toFixed(3),
        cssWidth: cs.width,
        paddingLeft: cs.paddingLeft,
        paddingRight: cs.paddingRight
      };
    });

    // 数据行实测（核对与表头一致）
    const firstRow = table.querySelector('tbody tr');
    const tds = firstRow ? Array.from(firstRow.querySelectorAll('td')).map(td => {
      const r = td.getBoundingClientRect();
      return +r.width.toFixed(3);
    }) : [];

    // 行高实测（顺带复核 18px 约束仍然成立）
    const trRect = firstRow ? firstRow.getBoundingClientRect() : null;

    return {
      tableWidth: +tableRect.width.toFixed(3),
      borderSpacing: getComputedStyle(table).borderSpacing,
      tableLayout: getComputedStyle(table).tableLayout,
      headers,
      dataRowWidths: tds,
      rowHeight: trRect ? +trRect.height.toFixed(3) : null,
      rowCount: table.querySelectorAll('tbody tr').length,
      colCount: ths.length
    };
  });

  if (data.error) {
    console.log('!! ' + data.error);
    await browser.close();
    process.exit(1);
  }

  console.log('=== 预览市场表实测 ===');
  console.log('表格宽          :', data.tableWidth, 'px');
  console.log('table-layout    :', data.tableLayout);
  console.log('border-spacing  :', data.borderSpacing);
  console.log('列数 / 行数     :', data.colCount, '/', data.rowCount);
  console.log('行高            :', data.rowHeight, 'px  (落地目标 18px)');
  console.log('');
  console.log('列头实测宽度：');
  data.headers.forEach((h, i) => {
    console.log(`  [${i}] ${h.text.padEnd(12)} ${String(h.width).padStart(8)}px   (css:${h.cssWidth})`);
  });
  if (data.dataRowWidths.length) {
    console.log('');
    console.log('数据行实测宽度 :', data.dataRowWidths.join(' | '));
  }

  // ── 换算 shares ────────────────────────────────────────────────────────────
  // 取最小列宽为基准 1（整数化，避免浮点 shares）。
  // 显示压缩判据：比例必须严格保持，不做任何取整抹平 —— 若某列比例算出 3.98 而另一列
  // 4.02，就报出来让人判断，不要偷偷都写成 4。
  const widths = data.headers.map(h => h.width);
  const min = Math.min(...widths);
  const rawShares = widths.map(w => w / min);

  console.log('');
  console.log('=== 换算为 RUIF shares（以最窄列为 1）===');
  console.log('原始比例 :', rawShares.map(v => v.toFixed(3)).join(' : '));
  const rounded = rawShares.map(v => Math.round(v));
  console.log('取整后   :', rounded.join(' : '));
  const roundErr = rawShares.map((v, i) => Math.abs(v - rounded[i]));
  const maxErr = Math.max(...roundErr);
  console.log('取整最大误差 :', maxErr.toFixed(4), maxErr < 0.05 ? '✅ 可安全取整' : '❌ 取整会改变比例');

  // 落地侧当前使用的 shares —— 必须与预览实测比例一致。
  // ⚠ 2026-09-18：初版误写为 [4,1,1,1,1]（把 CSS 的 124px/52px 当成了比例，
  //   忽略了 table-layout:fixed 会把固定宽按比例放大填满容器）。实测纠正为 2.385:1。
  const landed = [2.385, 1, 1, 1, 1.001];
  const TOL = 0.02; // 允许的 shares 偏差（对应 <0.5px 级别）

  let maxShareErr = 0;
  const n = Math.min(landed.length, rawShares.length);
  for (let i = 0; i < n; i++) {
    maxShareErr = Math.max(maxShareErr, Math.abs(rawShares[i] - landed[i]));
  }

  console.log('');
  console.log('落地 shares  :', landed.join(' : '));
  console.log('实测 shares  :', rawShares.map(v => v.toFixed(3)).join(' : '));
  console.log('最大偏差     :', maxShareErr.toFixed(4), '(容差 ' + TOL + ')');
  console.log('一致性判定   :',
    maxShareErr <= TOL ? '✅ 1:1 一致' : '❌ 不一致 —— 需修正落地 EconomyMarketPanel.Shares');

  await browser.close();
  process.exit(maxShareErr <= TOL ? 0 : 2);
})();
