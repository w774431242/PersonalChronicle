/* 临时：MVS Keyed 键审计（跑完即删）
   ① C# 引用 vs 语言文件实有键
   ② 中英键集对称
   ③ 同名键占位符数量一致
   ④ 文件内重复键
*/
const fs = require('fs');
const path = require('path');

function readKeys(file) {
  const raw = fs.readFileSync(file, 'utf8');
  const map = new Map();
  const dup = [];
  const re = /<(MVS\.[A-Za-z0-9_.]+)>([\s\S]*?)<\/\1>/g;
  let m;
  while ((m = re.exec(raw))) {
    if (map.has(m[1])) dup.push(m[1]);
    map.set(m[1], m[2]);
  }
  const amp = raw.split('&').filter((_, i) => true).length - 1;
  return { map, dup, raw };
}

const zh = readKeys('Languages/ChineseSimplified/Keyed/MVS.xml');
const en = readKeys('Languages/English/Keyed/MVS.xml');

console.log('ZH keys=' + zh.map.size + '  EN keys=' + en.map.size);
console.log('ZH duplicates: ' + (zh.dup.length ? zh.dup.join(',') : 'none'));
console.log('EN duplicates: ' + (en.dup.length ? en.dup.join(',') : 'none'));

const onlyZh = [...zh.map.keys()].filter(k => !en.map.has(k));
const onlyEn = [...en.map.keys()].filter(k => !zh.map.has(k));
console.log('only in ZH: ' + (onlyZh.length ? onlyZh.join(',') : 'none'));
console.log('only in EN: ' + (onlyEn.length ? onlyEn.join(',') : 'none'));

function ph(s) {
  const r = /\{(\d+)\}/g;
  const out = [];
  let m;
  while ((m = r.exec(s))) out.push(m[1]);
  return out.sort().join(',');
}
const phBad = [];
for (const [k, v] of zh.map) {
  if (!en.map.has(k)) continue;
  if (ph(v) !== ph(en.map.get(k))) phBad.push(k + ' zh[' + ph(v) + '] en[' + ph(en.map.get(k)) + ']');
}
console.log('placeholder mismatch: ' + (phBad.length ? '\n  ' + phBad.join('\n  ') : 'none'));

// C# 引用
const cs = [];
(function walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name);
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith('.cs')) cs.push(p);
  }
})('Source/PersonalChronicle');
const used = new Set();
const cre = /"(MVS\.[A-Za-z0-9_.]+)"/g;
for (const f of cs) {
  const s = fs.readFileSync(f, 'utf8');
  let m;
  while ((m = cre.exec(s))) used.add(m[1]);
}
const defs = fs.readFileSync('Defs/MVS_Commodities.xml', 'utf8');
const dre = /<displayKey>([A-Za-z0-9_.]+)<\/displayKey>/g;
let dm;
while ((dm = dre.exec(defs))) used.add(dm[1]);

const missing = [...used].filter(k => !zh.map.has(k)).sort();
console.log('C#/Def referenced but MISSING in Keyed (' + missing.length + '): ' +
  (missing.length ? '\n  ' + missing.join('\n  ') : 'none'));
