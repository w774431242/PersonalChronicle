const fs = require('fs');
const vm = require('vm');
const path = require('path');

// 定位人物档案视窗目录下的职业档案Tab预览.html
const dir = __dirname;
const files = fs.readdirSync(dir).filter(f => f.includes('职业档案') && f.endsWith('.html'));
if (!files.length) { console.log('FAIL: 未找到目标 html'); process.exit(1); }
const f = path.join(dir, files[0]);
const html = fs.readFileSync(f, 'utf8');

let ok = true;
function fail(m){ ok = false; console.log('FAIL:', m); }

// 1. plan-cat 数量应 = 12
const planCats = (html.match(/class="plan-cat[^"]*"/g) || []);
console.log('plan-cat 卡片数:', planCats.length);
if (planCats.length !== 12) fail('plan-cat 应为 12，实际 ' + planCats.length);

// 2. 12 个专业名均出现在 plan-cats 中
const majors = ['工程类','制造类','农业类','林业类','畜牧类','医学类','武器类','矿业类','科研类','烹饪类','艺术类','管理类'];
for (const m of majors) {
  if (!html.includes('>' + m + '<')) fail('plan-cats 缺少专业: ' + m);
}

// 3. 标签平衡
for (const tag of ['div','section','script','style']) {
  const open = (html.match(new RegExp('<'+tag+'[\\s>]','g'))||[]).length;
  const close = (html.match(new RegExp('</'+tag+'>','g'))||[]).length;
  console.log(`<${tag}> open=${open} close=${close}`);
  if (open !== close) fail(`标签 ${tag} 不平衡 ${open}/${close}`);
}

// 4. JS 语法校验
const scripts = html.match(/<script[^>]*>([\s\S]*?)<\/script>/g) || [];
let i = 0;
for (const s of scripts) {
  i++;
  const code = s.replace(/^<script[^>]*>/, '').replace(/<\/script>$/, '');
  if (!code.trim()) continue;
  try { new vm.Script(code); console.log(`script#${i} 语法 OK (${code.length}字符)`); }
  catch(e){ fail(`script#${i} 语法错误: ${e.message}`); }
}

// 5. MAJOR_SKILLS 公平性
const m = html.match(/const MAJOR_SKILLS = (\{[\s\S]*?\});/);
if (m) {
  const obj = eval('(' + m[1] + ')');
  for (const k of Object.keys(obj)) {
    const sum = Object.values(obj[k]).reduce((a,b)=>a+b,0);
    if (sum !== 4) fail(`专业 ${k} 核心权重和=${sum} ≠ 4`);
    else console.log(`专业 ${k} 核心权重和=4 OK (skills=${Object.keys(obj[k]).join('/')})`);
  }
  if (Object.keys(obj).length !== 12) fail('MAJOR_SKILLS 键数 ' + Object.keys(obj).length);
} else fail('未找到 MAJOR_SKILLS');

console.log(ok ? '\nALL CHECKS PASSED' : '\nHAS FAILURES');
process.exit(ok ? 0 : 1);
