/**
 * 制造系统中心预览 —— 打开抽屉后量取 #drawer 内部全部参数
 * （抽屉默认 display/opacity 隐藏，内部元素 getBoundingClientRect 为 0，必须先唤起）
 */
const path = require('path');
const puppeteer = require('puppeteer');
const HTML = path.resolve(
  'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/系统中心蓝图/制造系统中心预览_RUIF单页统一版.html'
);

async function main() {
  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--allow-file-access-from-files']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1680, height: 1000, deviceScaleFactor: 1 });
  await page.goto('file:///' + HTML.replace(/\\/g, '/'), { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 500));

  // 唤起抽屉：用预览自己的数据 + selMachine(id)，再补 .open/.show 类
  const opened = await page.evaluate(() => {
    const log = {};
    let id = null;
    try {
      if (typeof S !== 'undefined' && S.machines && S.machines.length) id = S.machines[0].id;
    } catch (e) { log.dataErr = e.message; }
    if (id == null) {
      const m = document.querySelector('.machine');
      id = m ? (m.getAttribute('data-mid') || '0') : '0';
    }
    log.id = id;
    if (typeof window.selMachine === 'function') {
      try { window.selMachine(id); log.selMachine = 'ok'; } catch (e) { log.selMachine = 'err:' + e.message; }
    }
    const dr = document.getElementById('drawer');
    const mk = document.getElementById('mask');
    if (dr) { dr.classList.add('open'); dr.style.visibility = 'visible'; }
    if (mk) { mk.classList.add('show'); }
    log.drawerClass = dr ? dr.className : 'none';
    log.drawerDisplay = dr ? getComputedStyle(dr).display : 'no-drawer';
    log.innerHTMLLen = dr ? dr.innerHTML.length : 0;
    log.bodyLen = document.getElementById('dbody') ? document.getElementById('dbody').innerHTML.length : 0;
    return log;
  });
  console.error('OPEN_DRAWER:', JSON.stringify(opened));
  await new Promise(r => setTimeout(r, 300));

  const SELECTORS = [
    '#drawer', '#drawer::before', '#mask', '#dbody', '#dclose',
    '#drawer .dhead', '#drawer .dhead h2', '#drawer .did', '#drawer .dstate', '#drawer .lampdot',
    '#drawer .mkpi', '#drawer .mkpi>div', '#drawer .mkpi .v', '#drawer .mkpi .n',
    '#drawer .dtab', '#drawer .dtab button',
    '#drawer .dsec', '#drawer .dsec.on', '#drawer .dsub', '#drawer .sub2',
    '#drawer .dcols', '#drawer .dgrid2',
    '#drawer .drow2', '#drawer .drow2>span', '#drawer .drow2>b',
    '#drawer .tl', '#drawer .tl i',
    '#drawer .qh', '#drawer .qh i', '#drawer .qlab', '#drawer .qlab span',
    '#drawer .stbar', '#drawer .stbar i',
    '#drawer .skillbar', '#drawer .skillbar .sb', '#drawer .skillbar .sb i',
    '#drawer .navpair', '#drawer .navpair button', '#drawer .navpair button[disabled]',
    '#drawer .chips', '#drawer .chip'
  ];

  const result = await page.evaluate((SELECTORS) => {
    const PROPS = [
      'display', 'position', 'width', 'height', 'maxWidth', 'maxHeight', 'minHeight',
      'left', 'right', 'top', 'bottom', 'inset',
      'margin', 'marginTop', 'marginRight', 'marginBottom', 'marginLeft',
      'padding', 'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
      'gap', 'rowGap', 'columnGap',
      'flexDirection', 'flexWrap', 'flexGrow', 'flexBasis',
      'alignItems', 'justifyContent', 'textAlign', 'verticalAlign',
      'gridTemplateColumns', 'gridTemplateRows',
      'fontFamily', 'fontSize', 'fontWeight', 'lineHeight', 'letterSpacing',
      'color', 'backgroundColor', 'backgroundImage',
      'border', 'borderTop', 'borderBottom', 'borderLeft', 'borderRight',
      'borderWidth', 'borderStyle', 'borderColor', 'borderRadius',
      'boxShadow', 'opacity', 'overflow', 'zIndex', 'transform', 'transition'
    ];
    function read(el, pseudo) {
      const cs = getComputedStyle(el, pseudo || null);
      const o = {};
      for (const p of PROPS) {
        const v = cs[p];
        if (v == null) continue;
        if (v === 'none' && !['backgroundImage', 'boxShadow', 'transform', 'borderTop', 'borderBottom', 'borderLeft', 'borderRight'].includes(p)) continue;
        if (v === 'normal' && p !== 'fontWeight') continue;
        if (v === '0px' && ['borderRadius', 'letterSpacing'].includes(p)) continue;
        if (v === 'auto' && ['width', 'height'].includes(p)) continue;
        if (v === 'rgba(0, 0, 0, 0)' && p === 'backgroundColor') continue;
        o[p] = v;
      }
      return o;
    }
    const out = {};
    for (const sel of SELECTORS) {
      const m = sel.match(/^(.*)::(before|after)$/);
      const base = m ? m[1] : sel;
      const pseudo = m ? '::' + m[2] : null;
      let el = null;
      try { el = document.querySelector(base); } catch (e) { el = null; }
      if (!el) { out[sel] = { __missing: true }; continue; }
      const b = el.getBoundingClientRect();
      out[sel] = {
        __box: { x: +b.x.toFixed(2), y: +b.y.toFixed(2), w: +b.width.toFixed(2), h: +b.height.toFixed(2) },
        ...read(el, pseudo)
      };
    }
    // 抽屉内文本清单（用于核对文案条目数）
    const dr = document.getElementById('drawer');
    const mkpi = [...(dr ? dr.querySelectorAll('.mkpi>div') : [])].map(d => d.textContent.trim().replace(/\s+/g, ' '));
    const tabs = [...(dr ? dr.querySelectorAll('.dtab button') : [])].map(b => b.textContent.trim());
    const secs = [...(dr ? dr.querySelectorAll('.dsec h4') : [])].map(h => h.textContent.trim());
    return { selectors: out, mkpiTexts: mkpi, tabTexts: tabs, secHeadings: secs };
  }, SELECTORS);

  console.log(JSON.stringify(result, null, 1));
  await browser.close();
}
main().catch(e => { console.error('FAILED:', e.message); process.exit(1); });
