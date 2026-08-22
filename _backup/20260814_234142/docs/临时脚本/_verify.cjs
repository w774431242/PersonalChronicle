// 临时最终校验：JS 语法 + t() 调用引用完整性 + 地图志容器
const fs = require("fs");
const path = require("path");
const dir = path.resolve(__dirname);
const file = fs.readdirSync(dir).find(x => x.endsWith(".html"));
const html = fs.readFileSync(path.join(dir, file), "utf8");
const m = html.match(/<script>([\s\S]*?)<\/script>/);
const js = m[1];
let pass = 0, fail = 0;
const assert = (n, c) => { c ? pass++ : (fail++, console.log("  FAIL " + n)); };

try { new Function(js); assert("JS syntax", true); } catch (e) { assert("JS syntax: " + e.message, false); }

const zh = js.match(/zh:\s*\{([\s\S]*?)\n\s*\},\s*\n\s*en:/)[1];
const en = js.match(/en:\s*\{([\s\S]*?)\n\s*\}\s*\};/)[1];
const zhKeys = new Set([...zh.matchAll(/\b([A-Za-z][A-Za-z0-9]*)\s*:/g)].map(x => x[1]));
const enKeys = new Set([...en.matchAll(/\b([A-Za-z][A-Za-z0-9]*)\s*:/g)].map(x => x[1]));
// 所有 t("key") 调用必须在 zh 定义；排除 createElement("...") 误匹配（Element 的 t + ("div") 会形成 t("div"))
const stripped = js.replace(/createElement\("([A-Za-z0-9]*)"\)/g, "createElementX($1)");
const tCalls = new Set([...stripped.matchAll(/(^|[^A-Za-z0-9])t\("([A-Za-z][A-Za-z0-9]*)"\)/g)].map(x => x[2]));
const missingZh = [...tCalls].filter(k => !zhKeys.has(k));
assert("all t() keys defined in zh (" + missingZh.length + " missing)", missingZh.length === 0);
if (missingZh.length) console.log("   missing zh: " + missingZh.join(","));
// zh 与 en 键完全对称（t 调用的键在 en 也有）
const missingEn = [...tCalls].filter(k => !enKeys.has(k));
assert("all t() keys defined in en (" + missingEn.length + " missing)", missingEn.length === 0);
if (missingEn.length) console.log("   missing en: " + missingEn.join(","));

// 地图志容器
["location-grid", "location-kpis", "location-total", "side-location-count", "home-location-count", "btn-reroll-loc"].forEach(id => {
  assert("id " + id, html.includes('id="' + id + '"'));
});
assert("renderLocationOverview defined", js.includes("function renderLocationOverview"));
assert("renderLocationOverview wired in renderAll", /renderAll\s*\(\)\s*\{[\s\S]*?renderLocationOverview\(\)/.test(js));

console.log("\n==== RESULT: " + pass + " PASS / " + fail + " FAIL ====");
process.exit(fail > 0 ? 1 : 0);
