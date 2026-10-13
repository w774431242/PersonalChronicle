/* RUIF 单页预览验收（puppeteer + 本机 Chrome）
   跑法：NODE_PATH=<managed>/node_modules <managed node> _scripts/verify_preview_ruif.js
   断言：① 无 pageerror / console.error ② 零页面级滚动 + 零横向溢出
        ③ 面板左→右顺序 ④ 社会背景一页式内容 ⑤ 派系列表（全量 31 + 规则分组 + 交互）
        ⑥ 市场面板干净数据表（17 品种 · 51 数值 · 无内部折叠 · 非帝国空态） */
const { pathToFileURL } = require('url');
const puppeteer = require('puppeteer');

const FILE = 'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const pad = n => (n < 10 ? '0' : '') + n;
const _now = new Date();
const STAMP = _now.getFullYear() + '-' + pad(_now.getMonth()+1) + '-' + pad(_now.getDate()) + '_' + pad(_now.getHours()) + pad(_now.getMinutes());
const SHOT = 'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/_verify_tmp/preview_' + STAMP + '.png';
const EXPECT = ['社会背景', '派系状态', '市场面板', '总库存'];
// 库存结构图：4 个节点；上游投入 >=1 项；下游流向恒取 TOP3；须同时含「上游原料」与「中游成品」两类
const EXPECT_CH_NODES = 4, EXPECT_CH_UP_MIN = 1, EXPECT_CH_DN = 3;
const EXPECT_TOTAL = 31, EXPECT_TRADEABLE = 9, EXPECT_BLOCKED = 22;
const EXPECT_MK_ROWS = 17, EXPECT_MK_BARS = 0, EXPECT_MK_NUMS = 68;

(async () => {
  const errors = [];
  const browser = await puppeteer.launch({
    headless: 'new', executablePath: CHROME,
    args: ['--no-sandbox', '--disable-dev-shm-usage', '--font-render-hinting=none']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1000 });
  page.on('pageerror', e => errors.push('PAGEERROR: ' + e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push('CONSOLE-ERR: ' + m.text()); });
  page.on('requestfailed', r => errors.push('REQFAIL: ' + r.url()));

  await page.goto(pathToFileURL(FILE).href, { waitUntil: 'load' });
  await new Promise(r => setTimeout(r, 400));

  const base = await page.evaluate(() => {
    const panels = [...document.querySelectorAll('.panel')].map(p => {
      const r = p.getBoundingClientRect(); const tl = p.querySelector('.ptitle');
      let title = '';
      if (tl) { const c = tl.cloneNode(true); const s = c.querySelector('.sub'); if (s) s.remove();
                c.querySelectorAll('.mark,.stencil,.ruif').forEach(n => n.remove()); title = c.textContent.trim(); }
      const body = p.querySelector('.pbody');
      return { t: title, top: Math.round(r.top), left: Math.round(r.left),
               isSelector: p.id === 'factionPanel', ox: p.scrollWidth - p.clientWidth,
               bodyFolded: body ? body.scrollHeight - body.clientHeight : 0,
               bodyBox: body ? Math.round(body.getBoundingClientRect().width) + 'x' + Math.round(body.getBoundingClientRect().height) : '' };
    });
    const rows = document.querySelectorAll('.fltbl .rw').length;
    const off = document.querySelectorAll('.fltbl .rw.off').length;
    const mkBody = document.getElementById('mkBody');
    const mkRows = document.querySelectorAll('#mkBody .dt tbody tr').length;
    const mkLists = [...document.querySelectorAll('#mkBody .dt tbody tr')].map(r => {
      const n = r.querySelectorAll('.num');
      return {
        nm: r.querySelector('td')?.innerText.trim(),
        sup: n[0]?.innerText.trim(),
        dem: n[1]?.innerText.trim(),
        delta: n[2]?.innerText.trim()
      };
    });
    return {
      docH: document.documentElement.scrollHeight, winH: window.innerHeight,
      docW: document.documentElement.scrollWidth, winW: window.innerWidth,
      rows: rows, blocked: off, tradeable: rows - off,
      groups: document.querySelectorAll('.fltbl .grp').length,
      tech: document.getElementById('k-tech').textContent,
      ftr: document.getElementById('k-ftr').textContent,
      panelOrder: panels.filter(p => !p.isSelector).sort((a, b) => (a.top - b.top) || (a.left - b.left)).map(p => p.t),
      overflowX: panels.filter(p => p.ox > 0).map(p => p.t + '(+' + p.ox + 'px)'),
      folded: panels.filter(p => p.bodyFolded > 0).map(p => p.t + '(+' + p.bodyFolded + 'px)'),
      mkBodyBox: Math.round(mkBody.getBoundingClientRect().width) + 'x' + Math.round(mkBody.getBoundingClientRect().height),
      mkFolded: mkBody.scrollHeight - mkBody.clientHeight,
      mkRows: mkRows, mkBars: document.querySelectorAll('#mkBody .dt .bar').length,
      mkNums: document.querySelectorAll('#mkBody .dt tbody .num').length,
      mkSub: document.getElementById('mk-sub').textContent,
      mkSample: mkLists.slice(0, 3).concat(mkLists.slice(-2)),
      rowTab: document.querySelector('.fltbl .rw')?.getAttribute('tabindex')
    };
  });

  const society = await page.evaluate(() => {
    const t = document.getElementById('soBody').innerText;
    return {
      定性: /残存舰队/.test(t),
      三层占比: /6%/.test(t) && /21%/.test(t) && /73%/.test(t),
      分层人数: /13 人/.test(t) && /45 人/.test(t) && /156 人/.test(t),
      人数模型: /45 人\/据点/.test(t) && /据点 3/.test(t) && /≈\s*214/.test(t),
      影响因子: /产能过剩 ×1\.5/.test(t) && /贸易净进口 ×1\.2/.test(t) && /军事化 ×1\.15/.test(t)
                && /社会停滞 ×0\.85/.test(t) && /灾后残存 ×0\.9/.test(t),
      市场特征: /Metallic/.test(t) && /无爵位不交易/.test(t) && /敏感度/.test(t),
      映射已删除: !/武装占社会/.test(t),
      未搬运字段: !/royalTitleInheritanceRelations/.test(t)
    };
  });

  await page.click('#flFilter button[data-f="tradeable"]');
  await new Promise(r => setTimeout(r, 120));
  const fTrade = await page.evaluate(() => document.querySelectorAll('.fltbl .rw').length);
  await page.click('#flFilter button[data-f="blocked"]');
  await new Promise(r => setTimeout(r, 120));
  const fBlocked = await page.evaluate(() => document.querySelectorAll('.fltbl .rw').length);
  await page.click('#flFilter button[data-f="base"]');
  await new Promise(r => setTimeout(r, 120));
  const fBase = await page.evaluate(() => document.querySelectorAll('.fltbl .rw').length);
  await page.click('#flFilter button[data-f="all"]');
  await new Promise(r => setTimeout(r, 120));

  await page.click('.fltbl .rw[data-id="TribeSavage"]');
  await new Promise(r => setTimeout(r, 150));
  const afterBlockedClick = await page.evaluate(() => ({
    sel: document.querySelector('.fltbl .rw.sel')?.dataset.id || null,
    invNodes: document.querySelectorAll('#chnav button').length,
    soEmpty: document.querySelectorAll('#soBody .verdict').length,
    mkEmpty: document.querySelectorAll('#mkBody .verdict').length,
    mkBodyText: document.getElementById('mkBody').innerText.slice(0, 40).replace(/\s+/g, ' ')
  }));

  // 转换校验：非 Empire 的可贸易派系也必须建模（通用字段反推模板，非 Empire 专属）
  await page.click('.fltbl .rw[data-id="TribeCivil"]');
  await new Promise(r => setTimeout(r, 150));
  const afterTradeableClick = await page.evaluate(() => ({
    sel: document.querySelector('.fltbl .rw.sel')?.dataset.id || null,
    mkEmpty: document.querySelectorAll('#mkBody .verdict').length,
    mkRows: document.querySelectorAll('#mkBody .dt tbody tr').length,
    soTiers: document.querySelectorAll('#soBody .tier').length,
    fsFactors: document.querySelectorAll('#fstatusBody .drow').length
  }));

  await page.click('.fltbl .rw[data-id="Empire"]');
  await page.focus('#flList');
  await page.keyboard.press('ArrowDown');
  const focused = await page.evaluate(() => document.activeElement?.dataset?.id || null);
  await page.keyboard.press('Enter');
  await new Promise(r => setTimeout(r, 150));
  const afterKeys = await page.evaluate(() => ({
    sel: document.querySelector('.fltbl .rw.sel')?.dataset.id || null,
    aria: document.querySelector('.fltbl .rw.sel')?.getAttribute('aria-selected') || null
  }));

  // 随机数据按钮稳定性：连点 5 次，校验无 pageerror / 无面板折叠 / 无页面竖滚 / 截图时间格式正确
  let randFold = '无', randTs = '—', randErr = '无';
  for(let k = 0; k < 5; k++){
    await page.click('#btnRandom');
    await new Promise(r => setTimeout(r, 80));
    const st = await page.evaluate(() => {
      const fold = [...document.querySelectorAll('.panel')].filter(p => {
        const b = p.querySelector('.pbody'); return b && (b.scrollHeight - b.clientHeight) > 0;
      }).length;
      return { fold, scroll: document.documentElement.scrollHeight - window.innerHeight,
               tshot: document.getElementById('tshot').textContent };
    });
    if(st.fold > 0) randFold = '折叠+' + st.fold;
    if(st.scroll > 1) randFold = '竖滚+' + st.scroll;
    randTs = st.tshot;
    if(!/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/.test(st.tshot)) randErr = 'tshot异常:' + st.tshot;
  }

  // 库存结构图：逐节点切换，校验上游投入 / 下游流向 与 TOP3 截断
  const chain = await page.evaluate(() => {
    const btns = [...document.querySelectorAll('#chnav button')];
    // ⚠ 组件切换会重建按钮：缓存的旧引用点击后已脱离文档树，事件不再冒泡到父级。
    //   必须按 selector 每次重新取「当前 DOM 里」的按钮。
    return btns.map(b => b.dataset.c).map(k => {
      document.querySelector('#chnav button[data-c="' + k + '"]').click();
      const cols = document.querySelectorAll('#chcols .chcol');
      const nd = document.querySelector('#chcols .chnode');
      const nm = nd ? nd.querySelector('.nm') : null;
      const tier = nd ? nd.querySelector('.tier') : null;
      return {
        节点: nm ? nm.textContent.trim() : '', key: k,
        层级: tier ? tier.textContent.trim() : '',
        上游: cols[0] ? cols[0].querySelectorAll('.chi').length : 0,
        下游: cols[1] ? cols[1].querySelectorAll('.chi').length : 0,
        副标题: document.getElementById('inv-sub').textContent
      };
    });
  });
  // 复位到首个节点（原木）后再截图，保证截图内容可读
  await page.click('#chnav button[data-c="wood"]');
  await new Promise(r => setTimeout(r, 150));

  await page.click('.fltbl .rw[data-id="Empire"]');
  await new Promise(r => setTimeout(r, 250));
  await page.screenshot({ path: SHOT });

  const orderOK = JSON.stringify(base.panelOrder) === JSON.stringify(EXPECT);
  const societyOK = Object.values(society).every(Boolean);
  const listOK = base.rows === EXPECT_TOTAL && base.tradeable === EXPECT_TRADEABLE
              && base.blocked === EXPECT_BLOCKED && fTrade === EXPECT_TRADEABLE && fBlocked === EXPECT_BLOCKED;
  const marketOK = base.mkRows === EXPECT_MK_ROWS && base.mkBars === EXPECT_MK_BARS
              && base.mkNums === EXPECT_MK_NUMS && base.mkFolded <= 0
              && /品种 17/.test(base.mkSub) && afterBlockedClick.mkEmpty === 1
              && afterTradeableClick.mkEmpty === 0 && afterTradeableClick.mkRows > 0
              && afterTradeableClick.soTiers > 0 && afterTradeableClick.fsFactors > 0;

  const chNodesOK  = chain.length === EXPECT_CH_NODES;
  const chStructOK = chain.every(c => c.上游 >= EXPECT_CH_UP_MIN && c.下游 === EXPECT_CH_DN);
  const chTierOK   = chain.some(c => /上游/.test(c.层级)) && chain.some(c => /中游/.test(c.层级));
  const chainOK    = chNodesOK && chStructOK && chTierOK && afterBlockedClick.invNodes === EXPECT_CH_NODES;

  console.log(JSON.stringify({
    '页面错误': errors.length ? errors : '无',
    '零滚动(竖)': base.docH <= base.winH + 1, 'docH/winH': base.docH + '/' + base.winH,
    '零溢出(横)': base.docW <= base.winW + 1, 'docW/winW': base.docW + '/' + base.winW,
    '面板左→右顺序': base.panelOrder, '顺序符合预期': orderOK,
    '面板横向溢出': base.overflowX.length ? base.overflowX : '无',
    '面板内折叠': base.folded.length ? base.folded : '无',
    '市场面板实测': { 区域: base.mkBodyBox, 折叠: base.mkFolded, 副标题: base.mkSub },
    '市场行/条/数值': base.mkRows + ' / ' + base.mkBars + ' / ' + base.mkNums,
    '市场样本': base.mkSample,
    '派系总行数': base.rows, '可贸易': base.tradeable, '不可贸易': base.blocked,
    '分组标题数': base.groups, '行 tabindex': base.rowTab,
    '筛选·可贸易': fTrade, '筛选·不可贸易': fBlocked, '筛选·有基地商人': fBase,
    '社会背景齐备': societyOK, '社会背景一页式': society,
    '初始选中/科技/可贸易KPI': 'Empire / ' + base.tech + ' / ' + base.ftr,
    '点不可贸易(野蛮部落)': afterBlockedClick,
    '点可贸易(温和部落·非Empire)': afterTradeableClick,
    '键盘移焦到': focused, '回车后选中/aria': afterKeys.sel + ' / ' + afterKeys.aria,
    '随机按钮稳定性(连点5)': { '折叠/滚动': randFold, '截图时间': randTs, '时间格式': randErr },
    '库存结构图·逐节点': chain,
    '结构图·节点数OK': chNodesOK, '结构图·上下游OK': chStructOK, '结构图·含上游+中游': chTierOK,
    '截图文件': SHOT,
    '断言': { orderOK, societyOK, listOK, marketOK, chainOK }
  }, null, 2));

  await browser.close();
  if (errors.length || !orderOK || !societyOK || !listOK || !marketOK || !chainOK || base.rowTab !== '-1' || randFold !== '无' || randErr !== '无') process.exit(1);
})().catch(e => { console.error('FATAL: ' + e.message); process.exit(1); });
