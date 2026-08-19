// 生成浏览器端数据文件（Tools/DataSimulator/dev/）：
//   defs-data.js    ← Defs/*.xml（职业系统数据，经 defs-loader 序列化）
//   recipes-data.js ← RimWorld 原版 Recipes_Production.xml（开发者调试可选的"大部分原版物品"配方）
// 用法：node src/gen-data.js [--rimworld <RimWorld 安装目录>]
'use strict';

const fs = require('fs');
const path = require('path');
const { loadDefs } = require('./defs-loader');
const { parseXml } = require('./xml');

const DEV_DIR = path.resolve(__dirname, '../dev');
const RW_DEFAULT = 'E:\\SteamLibrary\\steamapps\\common\\RimWorld';

function mapToObj(map) {
  const out = {};
  for (const [k, v] of map) out[k] = v;
  return out;
}

function writeDefsData() {
  const defs = loadDefs({ includeBlueprint: true });
  const payload = {
    skills: mapToObj(defs.skills),
    directions: mapToObj(defs.directions),
    effects: mapToObj(defs.effects),
    ratings: defs.ratings,
    mappings: defs.mappings,
    xpPolicies: defs.xpPolicies,
    qualifications: defs.qualifications,
    titles: defs.titles,
    medals: defs.medals,
    fallbacks: defs.fallbacks,
  };
  const body = '// 自动生成（node src/gen-data.js）：Defs/*.xml → 浏览器数据。请勿手改。\n'
    + 'window.SIM_DEFS = ' + JSON.stringify(payload, null, 1) + ';\n';
  fs.writeFileSync(path.join(DEV_DIR, 'defs-data.js'), body, 'utf8');
  console.log('✓ defs-data.js（技能 ' + Object.keys(payload.skills).length + ' / 方向 ' + Object.keys(payload.directions).length
    + ' / 资格 ' + payload.qualifications.length + ' / 勋章 ' + payload.medals.length
    + (payload.fallbacks.length ? ' ⚠fallback:' + payload.fallbacks.join(',') : '') + '）');
}

// ───────── 原版配方提取（深度扫描：RecipeDefs 文件 + ThingDef 内联 <RecipeDef> 块） ─────────

function walkFiles(dir, ext) {
  const out = [];
  let entries;
  try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch (e) { return out; }
  for (const ent of entries) {
    const full = path.join(dir, ent.name);
    if (ent.isDirectory()) out.push(...walkFiles(full, ext));
    else if (ent.name.endsWith(ext)) out.push(full);
  }
  return out;
}

function extractRecipes(rwDir) {
  const dls = ['Core', 'Anomaly', 'Odyssey', 'Biotech'];
  const recipes = [];
  const seen = new Set();
  for (const dl of dls) {
    const defsDir = path.join(rwDir, 'Data', dl, 'Defs');
    if (!fs.existsSync(defsDir)) { console.log('  skip DLC（不存在）: ' + dl); continue; }
    const files = walkFiles(defsDir, '.xml');
    for (const f of files) {
      let text;
      try { text = fs.readFileSync(f, 'utf8'); } catch (e) { continue; }
      if (text.indexOf('<RecipeDef') < 0) continue;
      const re = /<RecipeDef\b[^>]*>([\s\S]*?)<\/RecipeDef>/g;
      let m;
      while ((m = re.exec(text))) {
        const block = m[1];
        const dm = /<defName>([^<]+)<\/defName>/.exec(block);
        if (!dm || seen.has(dm[1])) continue;
        const dn = dm[1];
        // 过滤非"机械能制作"类：手术/屠宰/火化/收获/移除/安装/处决等
        if (/Surgery|Butcher|Cremat|Harvest|Remove|Install|Implant|Amputat|Execute|Resurrect|Destroy|Smelt|Extract|Designate|Fill|Empty|Load|Unload|Repair|Refuel|Deconstruct/i.test(dn)) continue;
        const workSpeedStat = /<workSpeedStat>([^<]+)<\/workSpeedStat>/.exec(block);
        if (!workSpeedStat) continue; // 无制造速度 stat = 非生产行为
        const productsM = /<products>([\s\S]*?)<\/products>/.exec(block);
        let product = null;
        if (productsM) {
          const p = productsM[1];
          const liTd = /<li\b[^>]*>[\s\S]*?<thingDef>([^<]+)<\/thingDef>/.exec(p);
          if (liTd) product = liTd[1];
          else {
            const simple = /<([A-Za-z_][\w.]*)>\s*\d+\s*<\/\1>/.exec(p);
            if (simple) product = simple[1];
          }
        }
        if (!product) continue;
        seen.add(dn);
        const wt = /<requiredGiverWorkType>([^<]+)<\/requiredGiverWorkType>/.exec(block);
        const wa = /<workAmount>(\d+)<\/workAmount>/.exec(block);
        recipes.push({
          defName: dn,
          product,
          workSpeedStat: workSpeedStat[1],
          workType: wt ? wt[1] : null,
          workAmount: wa ? Number(wa[1]) : null,
        });
      }
    }
  }
  recipes.sort((a, b) => (a.product || '').localeCompare(b.product || ''));
  return recipes;
}

// ───────── 原版可制作物品提取（ThingDef 内 <costList> = 可制造，1.6 制造系统主体） ─────────

function extractCraftables(rwDir) {
  const dls = ['Core', 'Anomaly', 'Odyssey', 'Biotech'];
  const items = [];
  const seen = new Set();
  const dirNames = ['ThingDefs_Items', 'ThingDefs_Buildings', 'ThingDefs_Misc'];
  for (const dl of dls) {
    const defsDir = path.join(rwDir, 'Data', dl, 'Defs');
    if (!fs.existsSync(defsDir)) continue;
    const files = walkFiles(defsDir, '.xml').filter((f) => dirNames.some((d) => f.indexOf(path.sep + d + path.sep) >= 0));
    for (const f of files) {
      let text;
      try { text = fs.readFileSync(f, 'utf8'); } catch (e) { continue; }
      if (text.indexOf('<costList>') < 0 || text.indexOf('<ThingDef') < 0) continue;
      const re = /<ThingDef\b[^>]*>([\s\S]*?)<\/ThingDef>/g;
      let m;
      while ((m = re.exec(text))) {
        const block = m[1];
        const dm = /<defName>([^<]+)<\/defName>/.exec(block);
        if (!dm || seen.has(dm[1])) continue;
        if (block.indexOf('<costList>') < 0) continue; // 无成本 = 不可制作（或自然产出）
        const dn = dm[1];
        // 排除明显不可"机械制作"的类别
        if (/^Corpse|^Minified|^Plant_|^Tree_|^RawAnimal|^Meat_|^Leather_|^Chunk/i.test(dn)) continue;
        seen.add(dn);
        const label = /<label>([^<]+)<\/label>/.exec(block);
        const cat = /<category>([^<]+)<\/category>/.exec(block);
        const eq = /<equipmentType>([^<]+)<\/equipmentType>/.exec(block);
        const app = block.indexOf('<apparel>') >= 0;
        const wtm = /<workToMake>([^<]+)<\/workToMake>/.exec(block);
        const stuff = block.indexOf('<stuffCategories>') >= 0;
        items.push({
          defName: dn,
          label: label ? label[1] : dn,
          category: cat ? cat[1] : 'Item',
          equipmentType: eq ? eq[1] : null,
          apparel: app,
          stuffable: stuff,
          workToMake: wtm ? Number(wtm[1]) : null,
        });
      }
    }
  }
  items.sort((a, b) => (a.label || '').localeCompare(b.label || ''));
  return items;
}

function writeRecipesData(rwDir) {
  const recipes = extractRecipes(rwDir);
  const craftables = extractCraftables(rwDir);
  // 物品 → 配方映射（模拟"制作物品"时还原配方名以命中技能白名单；同物品多配方取第一个）
  const productToRecipe = {};
  for (const r of recipes) {
    if (r.product && !productToRecipe[r.product]) productToRecipe[r.product] = r.defName;
  }
  const body = '// 自动生成（node src/gen-data.js）：RimWorld 原版制造数据（开发者调试选择用）。\n'
    + '// 数据源: ' + (rwDir || 'RimWorld 安装目录') + '\n'
    + 'window.VANILLA_RECIPES = ' + JSON.stringify(recipes) + ';\n'
    + 'window.VANILLA_CRAFTABLES = ' + JSON.stringify(craftables) + ';\n'
    + 'window.VANILLA_PRODUCT_TO_RECIPE = ' + JSON.stringify(productToRecipe) + ';\n';
  fs.writeFileSync(path.join(DEV_DIR, 'recipes-data.js'), body, 'utf8');
  console.log('✓ recipes-data.js（RecipeDef 配方 ' + recipes.length + ' 条 / 可制作物品 ' + craftables.length + ' 种 / 物品→配方映射 ' + Object.keys(productToRecipe).length + '）');
  return recipes.length + craftables.length;
}

// ───────── 主流程 ─────────

function main() {
  const args = process.argv.slice(2);
  const rwIdx = args.indexOf('--rimworld');
  const rwDir = rwIdx >= 0 && args[rwIdx + 1] ? args[rwIdx + 1] : RW_DEFAULT;
  fs.mkdirSync(DEV_DIR, { recursive: true });
  // dev/ 自包含：复制 sim-core（UMD 双环境）供浏览器调试 UI 引用
  fs.copyFileSync(path.resolve(__dirname, 'sim-core.js'), path.join(DEV_DIR, 'sim-core.js'));
  console.log('✓ sim-core.js 已同步到 dev/（浏览器调试 UI 引用）');
  writeDefsData();
  const n = writeRecipesData(rwDir);
  console.log(n === 0 ? '⚠ 未提取到原版配方（检查 RimWorld 目录与 Recipes_Production.xml）' : '完成。可在 dev-ui 中「制作物品」下拉选择。');
}

main();
