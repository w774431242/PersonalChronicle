/* 临时：经济中心预览权威量测（CDP 直连，跑完即删）
   跑法：node _scripts/_tmp_measure_econ.js
   产出：stdout JSON（结构 + 几何）+ _verify_tmp/econ_preview.png 截图 */
const { spawn } = require('child_process');
const { pathToFileURL } = require('url');
const fs = require('fs');
const path = require('path');
const os = require('os');

const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const ROOT = 'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统';
const FILE = ROOT + '/系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html';
const OUT = ROOT + '/_verify_tmp/econ_preview.png';
const PORT = 9333;
const profile = path.join(os.tmpdir(), 'cdp-econ-' + Date.now());

const args = [
  '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
  '--remote-debugging-port=' + PORT, '--user-data-dir=' + profile,
  '--window-size=1600,1000', '--font-render-hinting=none', '--hide-scrollbars',
  pathToFileURL(FILE).href
];

const child = spawn(CHROME, args, { stdio: 'ignore', detached: false });

const sleep = ms => new Promise(r => setTimeout(r, ms));

async function targets() {
  const res = await fetch('http://127.0.0.1:' + PORT + '/json/list');
  return res.json();
}

const EXPR = `(() => {
  const px = v => Math.round(v * 100) / 100;
  const box = el => { if(!el) return null; const r = el.getBoundingClientRect();
    return { w: px(r.width), h: px(r.height), x: px(r.left), y: px(r.top) }; };
  const cs = (el, props) => { if(!el) return null; const s = getComputedStyle(el); const o = {};
    props.forEach(p => o[p] = s[p]); return o; };
  const one = sel => box(document.querySelector(sel));
  const txt = el => el ? el.textContent.trim() : '(missing)';
  const many = sel => [...document.querySelectorAll(sel)].map(e => box(e));
  const hMany = sel => [...document.querySelectorAll(sel)].map(e => px(e.getBoundingClientRect().height));

  const panels = [...document.querySelectorAll('.panel')].map(p => {
    const t = p.querySelector('.ptitle');
    const b = p.querySelector('.pbody');
    return {
      id: p.id || '(no-id)',
      title: t ? t.textContent.trim().slice(0, 24) : '',
      box: box(p),
      ptitle: box(t),
      ptitlePad: cs(t, ['paddingTop','paddingBottom','paddingLeft','fontSize','lineHeight']),
      body: box(b),
      bodyPad: cs(b, ['padding','gap']),
      bodyFold: b ? b.scrollHeight - b.clientHeight : 0,
      overflowX: p.scrollWidth - p.clientWidth
    };
  });

  return {
    viewport: { w: innerWidth, h: innerHeight },
    doc: { w: document.documentElement.scrollWidth, h: document.documentElement.scrollHeight },
    pagehead: { box: one('#pagehead'), big: cs(document.querySelector('#pagehead .big'), ['fontSize','letterSpacing']) },
    kstrip: { box: one('#kstrip'), card: one('.kpiw'), cards: many('.kpiw'),
              cardPad: cs(document.querySelector('.kpiw'), ['padding','gap']),
              v: cs(document.querySelector('.kpiw .v'), ['fontSize']),
              l: cs(document.querySelector('.kpiw .tx .l'), ['fontSize','lineHeight']),
              bar: box(document.querySelector('.kpiw .bar')) },
    sop: { box: one('#sop'), cols: cs(document.querySelector('#sop'), ['gridTemplateColumns','gridTemplateRows','gap']) },
    panels,
    // 社会背景
    soc: { verdict: box(document.querySelector('#soBody .verdict')),
           vq: cs(document.querySelector('#soBody .verdict .q'), ['fontSize','lineHeight']),
           vp: cs(document.querySelector('#soBody .verdict .p'), ['fontSize','lineHeight']),
           sech: box(document.querySelector('#soBody .sec-h')),
           tier: box(document.querySelector('#soBody .tier')),
           tierPad: cs(document.querySelector('#soBody .tier'), ['padding','gap']),
           drow: box(document.querySelector('#soBody .drow')),
           drowPad: cs(document.querySelector('#soBody .drow'), ['padding','fontSize']),
           chip: box(document.querySelector('#soBody .chip')),
           chips: box(document.querySelector('#soBody .chips')),
           qa: box(document.querySelector('#soBody .qa')) },
    // 派系状态
    fst: { drow: box(document.querySelector('#fstatusBody .drow')),
           bdg: box(document.querySelector('#fstatusBody .bdg')),
           evg: box(document.querySelector('#fstatusBody .evg')),
           evr: box(document.querySelector('#fstatusBody .evr')),
           evrPad: cs(document.querySelector('#fstatusBody .evr'), ['padding','gap']),
           eh: box(document.querySelector('#fstatusBody .evr .eh')),
           tg: cs(document.querySelector('#fstatusBody .evr .tg'), ['fontSize']),
           chipsEl: cs(document.querySelector('#fstatusBody .evr .chips'), ['fontSize','lineHeight']) },
    // 市场面板
    mkt: { msum: box(document.querySelector('#mkBody .msum')),
           msumEl: cs(document.querySelector('#mkBody .msum'), ['fontSize','padding']),
           table: box(document.querySelector('#mkBody .dt')),
           th: box(document.querySelector('#mkBody .dt thead th')),
           thEl: cs(document.querySelector('#mkBody .dt thead th'), ['fontSize','padding','letterSpacing']),
           tr: box(document.querySelector('#mkBody .dt tbody tr')),
           tds: many('#mkBody .dt tbody tr:first-child td'),
           tdEl: cs(document.querySelector('#mkBody .dt tbody td'), ['fontSize','padding','lineHeight']),
           theadThs: [...document.querySelectorAll('#mkBody .dt thead th')].map(t => px(t.getBoundingClientRect().width)),
           tbodyTd1: [...document.querySelectorAll('#mkBody .dt tbody tr')].map(r => px(r.querySelector('td').getBoundingClientRect().width)),
           fnote: box(document.querySelector('#mkBody .fnote')),
           fnoteEl: cs(document.querySelector('#mkBody .fnote'), ['fontSize','lineHeight','padding']),
           borderSpacing: cs(document.querySelector('#mkBody .dt'), ['borderSpacing']) },
    // 总库存
    inv: { chnav: box(document.querySelector('#chnav')),
           chnavBtn: box(document.querySelector('#chnav button')),
           chnavBtnEl: cs(document.querySelector('#chnav button'), ['fontSize','padding','lineHeight']),
           chcols: box(document.querySelector('#chcols')),
           chcolsGrid: cs(document.querySelector('#chcols'), ['gridTemplateColumns','gap']),
           chlbl: box(document.querySelector('#chcols .chlbl')),
           charw: box(document.querySelector('#chcols .charw')),
           chnode: box(document.querySelector('#chcols .chnode')),
           chnodePad: cs(document.querySelector('#chcols .chnode'), ['padding','gap']),
           chnodeNm: cs(document.querySelector('#chcols .chnode .nm'), ['fontSize']),
           chnodeTier: cs(document.querySelector('#chcols .chnode .tier'), ['fontSize']),
           chnodeStk: cs(document.querySelector('#chcols .chnode .stk'), ['fontSize']),
           chi: box(document.querySelector('#chcols .chi')),
           chiPad: cs(document.querySelector('#chcols .chi'), ['padding','gap']),
           chiHd: box(document.querySelector('#chcols .chi .hd')),
           chiB: cs(document.querySelector('#chcols .chi .hd b'), ['fontSize']),
           chbar: box(document.querySelector('#chcols .chbar')),
           chiNt: cs(document.querySelector('#chcols .chi .nt'), ['fontSize','lineHeight']),
           chcolH: hMany('#chcols .chcol'),
           fnote: box(document.querySelector('#chnote')),
           invwrap: box(document.querySelector('.invwrap')),
           invside: box(document.querySelector('.invside')),
           invsidePad: cs(document.querySelector('.invside'), ['paddingLeft','gap','flex']),
           invsideChildren: [...document.querySelectorAll('.invside > *')].map(e => ({ cls: e.className, ...box(e) })) },
    // 派系列表
    fl: { seg: box(document.querySelector('#flFilter')),
          segBtn: box(document.querySelector('#flFilter button')),
          segBtnEl: cs(document.querySelector('#flFilter button'), ['fontSize','padding']),
          fltbl: box(document.querySelector('.fltbl')),
          hd: box(document.querySelector('.fltbl .hd')),
          hdGrid: cs(document.querySelector('.fltbl .hd'), ['gridTemplateColumns','gap','height']),
          rw: box(document.querySelector('.fltbl .rw')),
          rwEl: cs(document.querySelector('.fltbl .rw'), ['fontSize','height']),
          grp: box(document.querySelector('.fltbl .grp')),
          rulebox: box(document.querySelector('.rulebox')),
          ruleboxEl: cs(document.querySelector('.rulebox'), ['fontSize','lineHeight','padding']),
          bdg: box(document.querySelector('.pbody.tight .bdg')),
          listRows: document.querySelectorAll('.fltbl .rw').length },
    // 文本样本
    sample: {
      mkSub: txt(document.getElementById('mk-sub')),
      invSub: txt(document.getElementById('inv-sub')),
      soId: txt(document.getElementById('so-id')),
      fsId: txt(document.getElementById('fs-id')),
      flCount: txt(document.getElementById('fl-count')),
      chnavLabels: [...document.querySelectorAll('#chnav button')].map(b => b.textContent.trim()),
      mkHead: [...document.querySelectorAll('#mkBody .dt thead th')].map(t => t.textContent.trim()),
      mkFirstRow: [...document.querySelectorAll('#mkBody .dt tbody tr')].slice(0,2).map(r => r.innerText.replace(/\\s+/g,' ').trim()),
      stencils: [...document.querySelectorAll('.ptitle .stencil')].map(e => e.textContent.trim()),
      ruifBadges: [...document.querySelectorAll('.ptitle .ruif')].map(e => e.textContent.trim()),
      subs: [...document.querySelectorAll('.ptitle .sub')].map(e => e.textContent.trim())
    }
  };
})()`;

(async () => {
  let list = null;
  for (let i = 0; i < 60 && !list; i++) {
    await sleep(250);
    try {
      const t = await targets();
      if (t && t.length && t.some(x => x.type === 'page' && x.webSocketDebuggerUrl)) list = t;
    } catch (e) { /* not up yet */ }
  }
  if (!list) { console.error('FATAL: chrome devtools not reachable'); child.kill(); process.exit(1); }
  const page = list.find(x => x.type === 'page');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let id = 0;
  const pending = new Map();
  const send = (method, params) => new Promise((res, rej) => {
    const mid = ++id; pending.set(mid, { res, rej });
    ws.send(JSON.stringify({ id: mid, method, params: params || {} }));
  });
  ws.addEventListener('message', ev => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id);
      m.error ? p.rej(new Error(JSON.stringify(m.error))) : p.res(m.result); }
  });
  await new Promise(r => ws.addEventListener('open', r));
  await send('Page.enable');
  await send('Runtime.enable');
  const navUrl = pathToFileURL(FILE).href;
  await send('Page.navigate', { url: navUrl });
  await sleep(1600);
  const loc = await send('Runtime.evaluate', { expression: 'location.href + " | " + document.title + " | " + document.body.innerHTML.length', returnByValue: true });
  console.error('LOC: ' + JSON.stringify(loc.result && loc.result.value));
  const r = await send('Runtime.evaluate', { expression: EXPR, returnByValue: true, awaitPromise: false });
  if (r.exceptionDetails) { console.error('EVAL ERROR: ' + JSON.stringify(r.exceptionDetails, null, 2)); }
  else { console.log(JSON.stringify(r.result.value, null, 2)); }

  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  const shot = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  fs.writeFileSync(OUT, Buffer.from(shot.data, 'base64'));
  console.error('SHOT: ' + OUT);

  ws.close(); child.kill();
  await sleep(200);
  process.exit(0);
})().catch(e => { console.error('FATAL: ' + e.stack); child.kill(); process.exit(1); });
