/**
 * 制造系统中心预览 —— 参数实测脚本（LAND-001「先量后写」的可执行版）
 *
 * 为什么必须用浏览器量：
 *  1) 预览的同一选择器被声明 2~3 次，只有 CSS 层叠后「最后一次生效」的值才是真值；
 *  2) color-mix() / 渐变 / 百分比 / em 等单位必须求值后才是可落地参数；
 *  3) SVG 的 viewBox 坐标需按实际渲染尺寸换算成像素，才能和 IMGUI 对表。
 *
 * 输出：JSON（stdout）+ 控制台可读摘要
 */
const path = require('path');
const puppeteer = require('puppeteer');

const HTML = path.resolve(
  'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/系统中心蓝图/制造系统中心预览_RUIF单页统一版.html'
);

// 需要量取的选择器清单（分组便于阅读）
const SELECTORS = {
  全局令牌: ['html', 'body'],
  顶栏: [
    '#top', '#top .brand', '#top .brand b', '#top .brand .en',
    '#clockbox', '#clockbox .d', '#clockbox .t',
    '#top .grp', '#top .grp button',
    '#powerbox', '#powerbox .plbl', '#powerbox .pnum',
    '#pbar', '#pfill', '#pbar .capmark', '#estop'
  ],
  布局: ['#wrap', '.panel', '.panel::before', '.panel::after'],
  面板标题: ['.ptitle', '.ptitle .stencil', '.ptitle .sub', '.ptitle .ruif', '.ptitle .ruif.pending'],
  分区标题: ['h3.sec', 'h3.sec::before'],
  警示带: ['.hazard'],
  KPI: ['#oee', '.oee-c', '.oee-c .big', '.oee-c .t', '.oee-c .bar', '.oee-c .bar i'],
  仪表: ['#gauges', '.gauge', '.gauge .num', '.gauge .lbl', '.gauge .delta'],
  主区: ['#main', '#floorwrap', '#floor'],
  车间格: ['.zone', '.zone:hover', '.zone.sel', '.zone.t1', '.zcap', '.znm', '.zmeta', '.zbarbg', '.zbar'],
  设备: [
    '.machine .body', '.machine .nm', '.machine .op', '.machine .pw', '.machine .tier',
    '.machine.t1 .body', '.machine .pbar', '.machine .pfill', '.machine .lamp'
  ],
  监管链路: ['.suplink', '.suplink.over'],
  工人: ['.worker circle', '.worker text'],
  图例: ['.lgd', '.lgd .t', '.lgd .i', '.lfoot'],
  两侧轨道: [
    '.siderail', '.railtitle', '.rdock', '.rdock .chip',
    '.rsechead', '.rrow', '.rmn', '.rbar', '.rbar .ss', '.rv',
    '.rbig', '.rqual', '.rqual .q'
  ],
  '命令条': [
    '#cmdbar', '.cmdbar', '.routebar', '.routebar button',
    '.flowsum', '#flow-sum', '.sinkchip', '.cmdsep',
    '.stressbar', '.hcout', '.hcpass', '.hcfail'
  ],
  缩放条: ['.zoombar', '.zoombar button', '.zoombar .zval'],
  底栏Tab: ['.ftabs', '.ftabs button', '.ftabs button.on'],
  分析带: ['.panel .s4', '.panel .s6', '.panel .s3', '.main2', '.lower2'],
  抽屉: [
    '#mask', '#drawer', '#drawer::before', '#dbody', '#dclose',
    '.dhead', '.dhead .dname', '.dhead .dmeta',
    '.mkpi', '.mkpi>div', '.mkpi .v', '.mkpi .n',
    '.dtab', '.dtab button', '.dtab button.on',
    '.dsec', '.dsec h4', '.dcols', '.dgrid2', '.drow', '.drow .k', '.drow .v',
    '.timeline', '.timeline i', '.qhist', '.qhist i', '.dnav', '.dnav button'
  ],
  警报: ['#alert', '#alert.show', '.tk-cap', '#ticker', '#tkmove'],
  面板态: ['.panel.pending', '.badge']
};

async function main() {
  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--allow-file-access-from-files']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1680, height: 1000, deviceScaleFactor: 1 });
  await page.goto('file:///' + HTML.replace(/\\/g, '/'), { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 500));

  const result = await page.evaluate((SELECTORS) => {
    // 需要输出的属性白名单（全部与布局/视觉相关）
    const PROPS = [
      'display', 'position', 'width', 'height', 'minWidth', 'maxWidth', 'minHeight', 'maxHeight',
      'left', 'right', 'top', 'bottom', 'inset',
      'margin', 'marginTop', 'marginRight', 'marginBottom', 'marginLeft',
      'padding', 'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
      'gap', 'rowGap', 'columnGap',
      'flexDirection', 'flexWrap', 'flexGrow', 'flexShrink', 'flexBasis',
      'alignItems', 'justifyContent', 'textAlign',
      'gridTemplateColumns', 'gridTemplateRows', 'gridAutoFlow',
      'fontFamily', 'fontSize', 'fontWeight', 'lineHeight', 'letterSpacing',
      'color', 'backgroundColor', 'backgroundImage',
      'border', 'borderTop', 'borderRight', 'borderBottom', 'borderLeft',
      'borderWidth', 'borderStyle', 'borderColor', 'borderRadius', 'borderTopWidth',
      'boxShadow', 'opacity', 'overflow', 'overflowY', 'zIndex', 'cursor',
      'transform', 'transition', 'animation', 'filter',
      'stroke', 'strokeWidth', 'strokeDasharray', 'fill', 'visibility'
    ];

    // 伪元素用 getComputedStyle(el, '::before') 单独取
    function readProps(el, pseudo) {
      const cs = getComputedStyle(el, pseudo || null);
      const out = {};
      for (const p of PROPS) {
        const v = cs[p];
        if (v === undefined || v === null) continue;
        // 过滤掉「默认无信息」的值以压缩输出
        if (v === 'none' && !['backgroundImage', 'boxShadow', 'transform', 'filter', 'animation'].includes(p)) continue;
        if (v === 'normal' && p !== 'fontWeight') continue;
        if (v === '0px' && ['borderRadius', 'letterSpacing'].includes(p)) continue;
        if (v === 'auto' && ['width', 'height'].includes(p)) continue;
        if (v === 'rgba(0, 0, 0, 0)' && p === 'backgroundColor') continue;
        if (v === 'rgb(0, 0, 0)' && ['stroke', 'borderColor'].includes(p)) continue;
        out[p] = v;
      }
      return out;
    }

    const data = {};
    for (const [group, sels] of Object.entries(SELECTORS)) {
      data[group] = {};
      for (const sel of sels) {
        // 支持 "X::before" 写法
        const m = sel.match(/^(.*)::(before|after)$/);
        let el = null, pseudo = null, baseSel = sel;
        try {
          if (m) { baseSel = m[1]; pseudo = '::' + m[2]; }
          el = document.querySelector(baseSel);
        } catch (e) { /* 复杂选择器 */
          try { el = document.querySelector(baseSel); } catch (e2) { el = null; }
        }
        if (!el) { data[group][sel] = { __missing: true }; continue; }
        const box = el.getBoundingClientRect();
        const props = readProps(el, pseudo);
        data[group][sel] = {
          __box: {
            x: +box.x.toFixed(2), y: +box.y.toFixed(2),
            w: +box.width.toFixed(2), h: +box.height.toFixed(2)
          },
          ...props
        };
      }
    }

    // SVG 关键几何：viewBox + 全部 rect / circle / line 的实际属性
    const floor = document.getElementById('floor');
    if (floor) {
      data['__svg'] = {
        viewBox: floor.getAttribute('viewBox'),
        width: floor.getAttribute('width'),
        height: floor.getAttribute('height'),
        clientRect: (() => { const r = floor.getBoundingClientRect(); return { w: +r.width.toFixed(2), h: +r.height.toFixed(2) }; })()
      };
    }

    return data;
  }, SELECTORS);

  // SVG 内部几何单独抓（数量多，只抓统计 + 去重属性值）
  const svgGeom = await page.evaluate(() => {
    const floor = document.getElementById('floor');
    if (!floor) return null;
    const out = {};
    const kinds = ['rect', 'circle', 'line', 'path', 'text', 'polygon', 'g'];
    for (const k of kinds) {
      const nodes = [...floor.querySelectorAll(k)];
      const sig = new Map();
      for (const n of nodes) {
        const attrs = {};
        for (const a of n.attributes) {
          // 跳过文本内容与 id
          if (a.name === 'id' || a.name === 'class' && false) continue;
          attrs[a.name] = a.value;
        }
        // 组合出「结构签名」（忽略坐标值本身，保留形状类型 + 类名）
        const cls = n.getAttribute('class') || '';
        const key = `${k}.${cls}`;
        if (!sig.has(key)) sig.set(key, { count: 0, sample: attrs });
        sig.get(key).count++;
      }
      out[k] = [...sig.entries()].map(([key, v]) => ({ key, count: v.count, sample: v.sample }));
    }
    return out;
  });

  // 布局层级：每个分区容器的实际位置与尺寸
  const layout = await page.evaluate(() => {
    const ids = ['top', 'wrap', 'cmdbar', 'floorwrap', 'rail-left', 'rail-right', 'rightcol',
      'oee', 'gauges', 'main', 'lower', 'ticker', 'drawer', 'mask', 'alert'];
    const out = {};
    for (const id of ids) {
      const el = document.getElementById(id);
      if (!el) { out[id] = null; continue; }
      const r = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      out[id] = {
        x: +r.x.toFixed(1), y: +r.y.toFixed(1), w: +r.width.toFixed(1), h: +r.height.toFixed(1),
        display: cs.display, position: cs.position,
        gridCols: cs.gridTemplateColumns, gap: cs.gap, padding: cs.padding, margin: cs.margin
      };
    }
    return out;
  });

  const payload = { selectors: result, svgGeom, layout };
  console.log(JSON.stringify(payload, null, 1));
  await browser.close();
}

main().catch(e => { console.error('MEASURE_FAILED:', e.message); process.exit(1); });
