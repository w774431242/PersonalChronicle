// 浏览器端渲染器（Tools/DataSimulator 报告）。
// 零依赖：数据在 window.__SIM_DATA__，本脚本生成交互式 SVG/HTML。
// 交互：场景 tab 切换、pawn 多选对比、指标切换（等级/XP/熟练度/速度/品质偏置）、
//       悬停 tooltip、评级阶梯图、能力雷达图、职称时间线与资格缺口表。
(function () {
  'use strict';
  var DATA = window.__SIM_DATA__;
  if (!DATA) return;

  var COLORS = ['#d97706', '#2563eb', '#16a34a', '#9333ea', '#dc2626', '#0891b2', '#7c3aed', '#0d9488'];

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  function fmt(v, d) {
    if (v == null || isNaN(v)) return '-';
    var p = Math.pow(10, d == null ? 2 : d);
    return String(Math.round(v * p) / p);
  }

  function tickLabel(t) {
    var days = Math.floor(t / 60000);
    var q = Math.floor(days / 15);
    var y = Math.floor(q / 4);
    q = q % 4;
    days = days % 15;
    var s = y > 0 ? y + '年' : '';
    if (q > 0 || y > 0) s += q + '季';
    s += days + '天';
    return s;
  }

  // ───────── SVG 折线图（多系列 + tooltip） ─────────
  function lineChart(container, series, opts) {
    opts = opts || {};
    var W = 760, H = 240, PAD = { l: 52, r: 16, t: 18, b: 30 };
    var minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    series.forEach(function (s) {
      s.points.forEach(function (p) {
        if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
        if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
      });
    });
    if (!isFinite(minX)) { container.appendChild(el('div', 'empty', '无数据')); return; }
    if (minY === maxY) { minY -= 1; maxY += 1; }
    var xSpan = maxX - minX || 1, ySpan = maxY - minY || 1;
    function X(x) { return PAD.l + ((x - minX) / xSpan) * (W - PAD.l - PAD.r); }
    function Y(y) { return H - PAD.b - ((y - minY) / ySpan) * (H - PAD.t - PAD.b); }

    var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);
    svg.setAttribute('class', 'chart');
    // 网格
    for (var g = 0; g <= 4; g++) {
      var gy = minY + (ySpan * g) / 4, py = Y(gy);
      var gl = document.createElementNS('http://www.w3.org/2000/svg', 'line');
      gl.setAttribute('x1', PAD.l); gl.setAttribute('y1', py);
      gl.setAttribute('x2', W - PAD.r); gl.setAttribute('y2', py);
      gl.setAttribute('stroke', '#e5e7eb'); gl.setAttribute('stroke-width', '1');
      svg.appendChild(gl);
      var tx = document.createElementNS('http://www.w3.org/2000/svg', 'text');
      tx.setAttribute('x', PAD.l - 6); tx.setAttribute('y', py + 4);
      tx.setAttribute('text-anchor', 'end'); tx.setAttribute('font-size', '10'); tx.setAttribute('fill', '#6b7280');
      tx.textContent = Math.round(gy);
      svg.appendChild(tx);
    }
    var lb = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    lb.setAttribute('x', X(minX)); lb.setAttribute('y', H - 6); lb.setAttribute('font-size', '10'); lb.setAttribute('fill', '#6b7280');
    lb.textContent = opts.xLabel || '制造次数 ' + minX;
    svg.appendChild(lb);
    var rb = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    rb.setAttribute('x', X(maxX)); rb.setAttribute('y', H - 6); rb.setAttribute('text-anchor', 'end'); rb.setAttribute('font-size', '10'); rb.setAttribute('fill', '#6b7280');
    rb.textContent = maxX;
    svg.appendChild(rb);

    var hit = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    hit.setAttribute('x', PAD.l); hit.setAttribute('y', PAD.t);
    hit.setAttribute('width', W - PAD.l - PAD.r); hit.setAttribute('height', H - PAD.t - PAD.b);
    hit.setAttribute('fill', 'transparent');
    svg.appendChild(hit);

    var tooltip = el('div', 'tt');
    tooltip.style.display = 'none';
    container.appendChild(tooltip);

    var allPts = [];
    series.forEach(function (s, si) {
      var color = COLORS[si % COLORS.length];
      if (opts.step) {
        // 阶梯线（评级）
        for (var i = 0; i < s.points.length - 1; i++) {
          var a = s.points[i], b = s.points[i + 1];
          var pl = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
          pl.setAttribute('points', X(a.x) + ',' + Y(a.y) + ' ' + X(b.x) + ',' + Y(a.y) + ' ' + X(b.x) + ',' + Y(b.y));
          pl.setAttribute('fill', 'none'); pl.setAttribute('stroke', color); pl.setAttribute('stroke-width', '2');
          pl.setAttribute('stroke-dasharray', s.dash ? '5,3' : '');
          svg.appendChild(pl);
        }
      } else {
        var pts = s.points.map(function (p) { return X(p.x).toFixed(1) + ',' + Y(p.y).toFixed(1); }).join(' ');
        var poly = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
        poly.setAttribute('points', pts);
        poly.setAttribute('fill', 'none'); poly.setAttribute('stroke', color); poly.setAttribute('stroke-width', '2');
        poly.setAttribute('stroke-linejoin', 'round');
        svg.appendChild(poly);
      }
      s.points.forEach(function (p) {
        var c = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        c.setAttribute('cx', X(p.x)); c.setAttribute('cy', Y(p.y));
        c.setAttribute('r', opts.step ? 2.5 : 1.5);
        c.setAttribute('fill', color);
        c.setAttribute('data-si', si); c.setAttribute('data-pi', s.points.indexOf(p));
        svg.appendChild(c);
        allPts.push({ si: si, p: p, color: color });
      });
    });
    container.appendChild(svg);

    function showTooltip(si, pi) {
      var s = series[si], p = s.points[pi];
      tooltip.innerHTML = '';
      tooltip.appendChild(el('div', 'tt-name', s.label));
      var rows = (opts.tipRows ? opts.tipRows(p) : [['值', fmt(p.y)]]);
      rows.forEach(function (r) {
        var row = el('div', 'tt-row');
        row.appendChild(el('span', 'tt-k', r[0]));
        row.appendChild(el('span', 'tt-v', String(r[1])));
        tooltip.appendChild(row);
      });
      tooltip.style.display = 'block';
      var r = svg.getBoundingClientRect();
      var px = PAD.l + ((p.x - minX) / xSpan) * (W - PAD.l - PAD.r);
      var py2 = Y(p.y);
      tooltip.style.left = (r.left + px - container.getBoundingClientRect().left) + 'px';
      tooltip.style.top = Math.max(0, py2 - 46) + 'px';
    }

    hit.addEventListener('mousemove', function (ev) {
      var rect = svg.getBoundingClientRect();
      var mx = ((ev.clientX - rect.left) / rect.width) * W;
      var my = ((ev.clientY - rect.top) / rect.height) * H;
      var best = null, bestD = Infinity;
      allPts.forEach(function (pt) {
        var d = Math.pow(X(pt.p.x) - mx, 2) + Math.pow(Y(pt.p.y) - my, 2);
        if (d < bestD) { bestD = d; best = pt; }
      });
      if (best && bestD < 2500) showTooltip(best.si, allPts.indexOf(best));
      else tooltip.style.display = 'none';
    });
    hit.addEventListener('mouseleave', function () { tooltip.style.display = 'none'; });
  }

  // ───────── 雷达图（能力 XP 占比） ─────────
  function radarChart(container, label, abilityXp) {
    var keys = Object.keys(abilityXp || {});
    if (keys.length === 0) { container.appendChild(el('div', 'empty', '无能力数据')); return; }
    var max = 1;
    keys.forEach(function (k) { if (abilityXp[k] > max) max = abilityXp[k]; });
    var W = 260, H = 220, cx = W / 2, cy = H / 2 + 6, R = 82;
    var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);
    svg.setAttribute('class', 'chart radar');
    var n = keys.length;
    function pt(i, r) {
      var a = -Math.PI / 2 + (i * 2 * Math.PI) / n;
      return [cx + r * Math.cos(a), cy + r * Math.sin(a)];
    }
    for (var ring = 1; ring <= 4; ring++) {
      var poly = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
      poly.setAttribute('points', keys.map(function (_, i) { return pt(i, (R * ring) / 4).join(','); }).join(' '));
      poly.setAttribute('fill', 'none'); poly.setAttribute('stroke', '#e5e7eb');
      svg.appendChild(poly);
    }
    var dataPts = keys.map(function (k, i) {
      var v = abilityXp[k] / max;
      return pt(i, Math.max(4, R * Math.min(1, v)));
    });
    var fill = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
    fill.setAttribute('points', dataPts.map(function (p) { return p.join(','); }).join(' '));
    fill.setAttribute('fill', 'rgba(217,119,6,0.18)'); fill.setAttribute('stroke', '#d97706'); fill.setAttribute('stroke-width', '1.5');
    svg.appendChild(fill);
    keys.forEach(function (k, i) {
      var c = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
      c.setAttribute('cx', dataPts[i][0]); c.setAttribute('cy', dataPts[i][1]);
      c.setAttribute('r', '2.5'); c.setAttribute('fill', '#d97706');
      svg.appendChild(c);
      var t = document.createElementNS('http://www.w3.org/2000/svg', 'text');
      var lp = pt(i, R + 22);
      t.setAttribute('x', lp[0]); t.setAttribute('y', lp[1] + 4);
      t.setAttribute('text-anchor', 'middle'); t.setAttribute('font-size', '10'); t.setAttribute('fill', '#374151');
      t.textContent = k + ' ' + fmt(abilityXp[k], 0);
      svg.appendChild(t);
    });
    var cap = el('div', 'radar-cap', label);
    container.appendChild(cap);
    container.appendChild(svg);
  }

  // ───────── 职称时间线 ─────────
  function timeline(container, pawn) {
    var span = pawn.final.spanTicks || 1;
    var box = el('div', 'tl');
    pawn.qualifications.forEach(function (q) {
      var row = el('div', 'tl-row');
      var bar = el('div', 'tl-bar' + (q.granted ? ' tl-ok' : ' tl-bad'));
      var w = q.granted ? Math.max(8, (q.reqTicks / span) * 100) : 100;
      bar.style.width = Math.min(100, w) + '%';
      var label = el('span', 'tl-label', q.defName + ' → ' + (q.titleDefName || '-'));
      var st = el('span', 'tl-status' + (q.granted ? ' ok' : ' bad'),
        q.granted ? '✅ 已授予' : '✗ ' + (q.reason || '未达成'));
      row.appendChild(bar); row.appendChild(label); row.appendChild(st);
      box.appendChild(row);
    });
    container.appendChild(box);
  }

  // ───────── 主渲染 ─────────
  function render() {
    document.title = DATA.title;
    var app = document.getElementById('app');
    app.innerHTML = '';
    app.appendChild(el('h1', null, DATA.title));
    var meta = el('p', 'meta', '生成 ' + DATA.generatedAt + ' · 公式对齐 C# 转写表（数据模拟工具需求与设计.md §2.3）· 数据源 ' + DATA.dataSource);
    app.appendChild(meta);

    // 场景 tab
    var tabs = el('div', 'tabs');
    var panels = [];
    DATA.scenarios.forEach(function (sc, si) {
      var tab = el('button', 'tab' + (si === 0 ? ' active' : ''), sc.name);
      tab.title = sc.describe;
      tab.addEventListener('click', function () {
        Array.prototype.forEach.call(tabs.children, function (t) { t.classList.remove('active'); });
        tab.classList.add('active');
        panels.forEach(function (p, pi) { p.style.display = pi === si ? 'block' : 'none'; });
      });
      tabs.appendChild(tab);
      panels.push(buildScenarioPanel(sc, si));
    });
    app.appendChild(tabs);
    panels.forEach(function (p, i) {
      p.style.display = i === 0 ? 'block' : 'none';
      app.appendChild(p);
    });
  }

  function buildScenarioPanel(sc, si) {
    var panel = el('div', 'panel');
    panel.appendChild(el('h2', null, sc.name + ' — ' + sc.describe));

    // 收集 pawn（跨 run 展平，带 run 序号）
    var all = [];
    sc.runs.forEach(function (run) {
      run.results.forEach(function (p) { all.push({ pawn: p, run: run.runIndex }); });
    });

    // 选择区
    var sel = el('div', 'selectors');
    var checkRow = el('div', 'checkrow');
    var checks = [];
    all.forEach(function (item, idx) {
      var lbl = el('label', 'check');
      var cb = document.createElement('input');
      cb.type = 'checkbox'; cb.checked = true; cb.dataset.idx = idx;
      var name = item.pawn.name + (sc.runs.length > 1 ? ' (run' + item.run + ')' : '');
      lbl.appendChild(cb);
      lbl.appendChild(document.createTextNode(name));
      checkRow.appendChild(lbl);
      checks.push(cb);
    });
    sel.appendChild(checkRow);

    // 指标切换
    var METRICS = [
      { key: 'level', label: '等级' },
      { key: 'xp', label: 'XP' },
      { key: 'mastery', label: '熟练度' },
      { key: 'speedFactor', label: '速度加成' },
      { key: 'qualityLevels', label: '品质偏置' },
    ];
    var radioRow = el('div', 'radiorow');
    var metric = 'level';
    METRICS.forEach(function (m, i) {
      var lbl = el('label', 'radio');
      var rd = document.createElement('input');
      rd.type = 'radio'; rd.name = 'metric' + si; rd.value = m.key;
      rd.checked = i === 0;
      rd.addEventListener('change', function () { metric = m.key; refresh(); });
      lbl.appendChild(rd);
      lbl.appendChild(document.createTextNode(m.label));
      radioRow.appendChild(lbl);
    });
    sel.appendChild(radioRow);
    panel.appendChild(sel);

    // 图区
    var charts = el('div', 'charts');
    var metricBox = el('div', 'chartbox');
    metricBox.appendChild(el('div', 'chart-title', '成长曲线（多 pawn 叠加对比）'));
    var metricHost = el('div', 'host');
    metricBox.appendChild(metricHost);
    charts.appendChild(metricBox);

    var ratingBox = el('div', 'chartbox');
    ratingBox.appendChild(el('div', 'chart-title', '评级阶梯（悬停查看）'));
    var ratingHost = el('div', 'host');
    ratingBox.appendChild(ratingHost);
    charts.appendChild(ratingBox);
    panel.appendChild(charts);

    // 雷达区
    var radarRow = el('div', 'radars');
    panel.appendChild(radarRow);

    // 资格区
    var qualSection = el('div', 'qualsection');
    panel.appendChild(qualSection);

    function selected() {
      return all.filter(function (_, i) { return checks[i].checked; });
    }

    function refresh() {
      var items = selected();
      // 成长曲线
      metricHost.innerHTML = '';
      var series = items.map(function (item) {
        return {
          label: item.pawn.name,
          points: item.pawn.snapshots.map(function (s) { return { x: s.index, y: s[metric] }; }),
        };
      });
      var m = METRICS.filter(function (x) { return x.key === metric; })[0];
      lineChart(metricHost, series, {
        xLabel: '制造次数',
        tipRows: function (p) { return [['制造次数', p.x], [m.label, fmt(p.y)]]; },
      });

      // 评级阶梯（y=档位序 0..3）
      ratingHost.innerHTML = '';
      var rSeries = items.map(function (item) {
        var pts = [];
        var last = -1;
        item.pawn.snapshots.forEach(function (s) {
          var rank = ratingRank(s.rating);
          if (rank !== last) { pts.push({ x: s.index, y: rank }); last = rank; }
        });
        if (pts.length === 0) pts.push({ x: 0, y: -1 });
        return { label: item.pawn.name, points: pts, dash: false };
      });
      lineChart(ratingHost, rSeries, {
        step: true,
        xLabel: '制造次数',
        tipRows: function (p) { return [['制造次数', p.x], ['档位', p.y < 0 ? '未评级' : ['', '熟练', '专业', '高级', '大师'][p.y] + '(' + p.y + ')']]; },
      });

      // 雷达
      radarRow.innerHTML = '';
      items.forEach(function (item) {
        var box2 = el('div', 'radarbox');
        radarChart(box2, item.pawn.name, item.pawn.final.abilityXp);
        radarRow.appendChild(box2);
      });

      // 资格表 + 时间线
      qualSection.innerHTML = '';
      items.forEach(function (item) {
        var card = el('div', 'card');
        var h = el('h3', null, item.pawn.name);
        var small = el('small', null, ' ' + item.pawn.skillDefName + ' · ' + item.pawn.qualitySpec + ' ×' + item.pawn.count
          + ' · 最终 Lv' + item.pawn.final.level + '/50 · ' + (item.pawn.final.rating || '未评级')
          + ' · 速度 x' + fmt(item.pawn.final.speedFactor, 4) + ' · 品质 +' + item.pawn.final.qualityLevels
          + ' · 职称: ' + (item.pawn.grantedTitles.join(' → ') || '-'));
        h.appendChild(small);
        card.appendChild(h);
        card.appendChild(el('div', 'tl-title', '职称达成时间线（' + tickLabel(item.pawn.final.spanTicks) + '）'));
        timeline(card, item.pawn);
        // 里程碑
        if (item.pawn.milestones.length > 0) {
          var ms = el('div', 'milestones');
          ms.appendChild(el('b', null, '里程碑 '));
          var ul = el('ul');
          item.pawn.milestones.forEach(function (m) {
            var li = el('li', null, 't=' + m.tick + '（' + tickLabel(m.tick) + '） ' + m.value);
            ul.appendChild(li);
          });
          ms.appendChild(ul);
          card.appendChild(ms);
        }
        qualSection.appendChild(card);
      });
    }

    function ratingRank(name) {
      if (!name) return -1;
      if (name.indexOf('Proficient') >= 0) return 1;
      if (name.indexOf('Specialist') >= 0) return 2;
      if (name.indexOf('Senior') >= 0) return 3;
      if (name.indexOf('Master') >= 0) return 4;
      return -1;
    }

    refresh();
    return panel;
  }

  render();
})();
