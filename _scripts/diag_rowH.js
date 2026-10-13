/* 诊断：定位 table.dt 行高的真实驱动因素 */
const puppeteer = require('puppeteer');
const FILE = 'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';

(async () => {
  const browser = await puppeteer.launch({
    headless: 'new', executablePath: CHROME,
    args: ['--no-sandbox', '--disable-dev-shm-usage', '--font-render-hinting=none']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1000 });
  await page.goto('file:///' + FILE.replace(/\\/g, '/'), { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 400));

  const r = await page.evaluate(() => {
    const tr = document.querySelector('table.dt tbody tr');
    if (!tr) return { err: 'no row' };
    const td = tr.children[0];
    const out = { tr: {}, td: {}, children: [] };
    const g = el => {
      const c = getComputedStyle(el);
      return {
        h: el.getBoundingClientRect().height,
        w: el.getBoundingClientRect().width,
        lineHeight: c.lineHeight, fontSize: c.fontSize, fontFamily: c.fontFamily,
        padding: c.padding, borderBottom: c.borderBottomWidth, boxSizing: c.boxSizing,
        display: c.display, verticalAlign: c.verticalAlign,
      };
    };
    out.tr = g(tr);
    out.td = g(td);
    for (const ch of td.children) {
      out.children.push({ tag: ch.tagName, cls: ch.className, ...g(ch) });
    }
    // 也测一个纯文本 td（数值列）
    const td2 = tr.children[1];
    if (td2) out.td2 = { ...g(td2), text: td2.textContent.trim().slice(0, 20) };
    return out;
  });
  console.log(JSON.stringify(r, null, 2));
  await browser.close();
})();
