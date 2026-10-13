# -*- coding: utf-8 -*-
"""[LEGACY/已取代] 曾把「供应 + 需求」合并为市场面板（双轨进度条 MarketDualBarList）。
   现市场面板已手动改写为「干净数据表（复用 RUIF .dt 原子）」，GAP-UI-DUALBAR 已退役；
   本脚本的 MK_JS 仍生成旧双轨结构，重跑会与现行 HTML 冲突，请勿再执行。"""
import io, sys

P = r"E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html"
src = io.open(P, encoding="utf-8").read()
orig_len = len(src)

# ── 1) 文件头注释：面板顺序 ────────────────────────────────────────
old = "面板顺序（严格左 → 右）：社会背景 → 市场结构·供应 → 市场结构·需求 → 总库存 → 派系贸易 → 派系档案"
new = "面板顺序（严格左 → 右）：社会背景 → 市场面板 → 总库存 → 派系贸易 → 派系档案\n  市场面板＝原「市场结构·供应」+「市场结构·需求」合并，改用双轨进度条组件 MarketDualBarList"
assert src.count(old) == 1, "head comment"
src = src.replace(old, new)

# ── 2) 页头 desc ──────────────────────────────────────────────────
old = '<span class="desc">派系经济背景 × 供需市场 · 社会背景 → 供应 → 需求 → 总库存 → 派系贸易 → 派系档案</span>'
new = '<span class="desc">派系经济背景 × 供需市场 · 社会背景 → 市场 → 总库存 → 派系贸易 → 派系档案</span>'
assert src.count(old) == 1, "pagehead desc"
src = src.replace(old, new)

# ── 3) DOM：两个 section → 一个市场面板 ────────────────────────────
i0 = src.index('    <section class="panel p3">\n      <div class="ptitle"><i class="mark"></i>市场结构 · 供应')
i1 = src.index('    <section class="panel accent p3">\n      <div class="ptitle"><i class="mark"></i>总库存')
new_dom = (
'    <section class="panel accent p6" id="marketPanel">\n'
'      <div class="ptitle"><i class="mark"></i>市场面板<span class="stencil">SUPPLY × DEMAND</span>\n'
'        <span class="ruif">MarketDualBarList</span><span class="sub" id="mk-sub">—</span></div>\n'
'      <div class="pbody" id="mkBody"></div>\n'
'    </section>\n\n'
)
src = src[:i0] + new_dom + src[i1:]

# ── 4) 注入品种数据 + 渲染函数（插在 renderDetail 之前）─────────────
MK_JS = u'''
  /* ── 市场品种表（17 个品种 · 由机制研发部按原版 ThingCategoryDef 归并裁定）──
     s=供应 d=需求，单位「件/日」；lv = 数据级别 ①原版锚 ②官方推导 ③设计假设。
     量纲锚：Need_Food 每人每日 1.0 nutrition；CookMealSimple = 0.5 nutrition/餐 ⇒ 2 餐/日；
     生食材 nutrition 0.05/单位 ⇒ 214 人 ≈ 4280 件/日总食材需求（素 60% / 荤 40%）。
     品种口径由 CommodityDef 三轴（thingDefs / thingCategories 含后代 / stuffCategories）命中。 */
  var MK = [
    {id:'BasicRaw', n:'基础原料',   s: 150,   d: 240,   lv:3, tc:'ResourcesRaw'},
    {id:'Textile',  n:'织物与皮革', s:  80,   d: 150,   lv:3, tc:'Textiles'},
    {id:'Meat',     n:'生肉',       s: 620,   d: 900,   lv:1, tc:'MeatRaw'},
    {id:'Egg',      n:'食用蛋',     s:  90,   d: 130,   lv:3, tc:'EggsUnfertilized'},
    {id:'Crop',     n:'农作物',     s:1180,   d:1538,   lv:1, tc:'PlantFoodRaw'},
    {id:'Meal',     n:'餐食',       s: 428,   d: 428,   lv:1, tc:'FoodMeals'},
    {id:'ProcFood', n:'加工食品',   s:  60,   d:  95,   lv:3, tc:'thingDefs×6'},
    {id:'Drug',     n:'成瘾品',     s: 140,   d:  60,   lv:3, tc:'Drugs'},
    {id:'Medicine', n:'药品',       s:  12,   d:  24,   lv:3, tc:'Medicine'},
    {id:'Weapon',   n:'武器',       s:   6,   d:   3,   lv:2, tc:'Weapons'},
    {id:'Shell',    n:'炮弹',       s:  24,   d:  12,   lv:3, tc:'MortarShells'},
    {id:'Apparel',  n:'服装护甲',   s:   5,   d: 2.5,   lv:2, tc:'Apparel'},
    {id:'Bionic',   n:'义体',       s: 3.6,   d: 1.2,   lv:3, tc:'BodyParts'},
    {id:'IndGoods', n:'工业制成品', s:  45,   d:  70,   lv:3, tc:'thingDefs×14'},
    {id:'Relic',    n:'圣物神器',   s: 0.9,   d: 0.4,   lv:3, tc:'Artifacts+InertRelics'},
    {id:'Book',     n:'书籍',       s:   2,   d:   1,   lv:3, tc:'Books'},
    {id:'Precious', n:'贵重杂项',   s: 1.5,   d: 0.6,   lv:3, tc:'ItemsMisc'}
  ];

  function mkFmt(v){
    if (v >= 1000) return String(Math.round(v));
    if (v >= 100)  return v.toFixed(v % 1 ? 1 : 0);
    if (v >= 10)   return v.toFixed(v % 1 ? 1 : 0);
    if (v >= 1)    return v.toFixed(1);
    return v.toFixed(1);
  }
  /* 条长：共用全局线性基准；下限 0.4% 仅为保证「有值即可见」，不改变比例语义 */
  function mkPct(v, basis){ return Math.max(0.4, Math.round(v / basis * 1000) / 10); }

  function renderMarket(f){
    var el = document.getElementById('mkBody');
    var sub = document.getElementById('mk-sub');

    if (f.id !== 'Empire'){
      sub.textContent = f.id + ' · 未建模';
      el.innerHTML =
          '<div class="verdict"><div class="q">尚未建立经济画像</div>'
        + '<div class="p">本面板的品种市场量只对<b style="color:var(--text)">已建模派系</b>显示。'
        +   '当前派系未建立供需结构，按 <b style="color:var(--text)">LAND-009</b> 不填占位数值。</div></div>'
        + '<div class="sec-h">还原方法<span class="n">两步</span></div>'
        + '<div class="qa"><span class="q">① 定体量</span><span class="a">据点数为<em>运行时真值</em>'
        +   '（<span class="mono">WorldObjects.Settlements</span>），人口按 <em>45 人/据点 × 社会因子</em></span></div>'
        + '<div class="qa"><span class="q">② 定结构</span><span class="a">按 <em>techLevel</em> 映射自给率与偏好；'
        +   '军事化派系上调 <em>Weapons / Apparel</em>，封闭社会下调 <em>Drug</em> 流通</span></div>';
      return;
    }

    var basis = 0;
    MK.forEach(function(m){ basis = Math.max(basis, m.s, m.d); });
    sub.textContent = '单位 件/日 · 品种 ' + MK.length + ' · 基准 ' + mkFmt(basis);

    var h = '<div class="mk-cols mkhead">'
          +   '<span>品种</span><span>供应 → 需求（共用线性基准）</span>'
          +   '<span class="r">供应</span><span class="r">需求</span><span class="r">差额</span>'
          + '</div><div class="mklist" role="list">';

    MK.forEach(function(m){
      var gap = m.d - m.s;
      var cls = Math.abs(gap) < basis * 0.002 ? 'eq' : (gap > 0 ? 'loss' : 'gain');
      var sign = Math.abs(gap) < basis * 0.002 ? '±' : (gap > 0 ? '−' : '+');
      h += '<div class="mrow mk-cols" role="listitem" tabindex="-1"'
        +      ' title="' + esc(m.n) + '　CommodityDef 命中轴：' + esc(m.tc) + '　数据级别 ' + m.lv + '">'
        +   '<span class="nm">' + esc(m.n) + '<span class="lv">' + m.lv + '</span></span>'
        +   '<span class="bars">'
        +     '<span class="bar sup" role="img" aria-label="供应 ' + mkFmt(m.s) + '"><i style="width:' + mkPct(m.s, basis) + '%"></i></span>'
        +     '<span class="bar dem" role="img" aria-label="需求 ' + mkFmt(m.d) + '"><i style="width:' + mkPct(m.d, basis) + '%"></i></span>'
        +   '</span>'
        +   '<span class="num">' + mkFmt(m.s) + '</span>'
        +   '<span class="num">' + mkFmt(m.d) + '</span>'
        +   '<span class="num ' + cls + '">' + sign + mkFmt(Math.abs(gap)) + '</span>'
        + '</div>';
    });
    h += '</div>';

    h += '<div class="mk-key">'
      +   '<span><i style="background:var(--info)"></i>供应</span>'
      +   '<span><i style="background:var(--accent)"></i>需求</span>'
      +   '<span>品种名后数字 = 数据级别 ①原版锚 ②官方推导 ③设计假设</span>'
      +   '<span style="margin-left:auto">差额 <em style="color:var(--loss)">−</em> 净进口 · <em style="color:var(--gain)">+</em> 净出口</span>'
      + '</div>'
      + '<div class="mk-key" style="border-top:0;padding-top:0">'
      +   '供应侧锚：木质消耗者 <em style="color:var(--text)">76</em> 品类 · costStuffCount 合计 '
      +   '<em style="color:var(--text)">5310</em> · 前 7 名中 <em style="color:var(--loss)">4 个</em>为奢侈/权力象征'
      + '</div>';
    el.innerHTML = h;
  }

'''

anchor_fn = '  function renderDetail(){'
assert src.count(anchor_fn) == 1, "renderDetail anchor"
src = src.replace(anchor_fn, MK_JS + anchor_fn)

# ── 5) 在 renderDetail 内调用 ──────────────────────────────────────
old = '    renderSociety(f);\n'
assert src.count(old) == 1, "renderSociety call"
src = src.replace(old, '    renderSociety(f);\n    renderMarket(f);\n')

io.open(P, "w", encoding="utf-8").write(src)
print("OK  %d -> %d bytes (+%d)" % (orig_len, len(src), len(src) - orig_len))
