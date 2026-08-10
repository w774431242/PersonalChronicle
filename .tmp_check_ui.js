const fs = require('fs');
const path = require('path');
const dir = path.join(__dirname, 'docs', 'ui-preview');
const file = fs.readdirSync(dir).find(f => f.includes('完整档案馆UI预览'));
if (!file) { console.error('FILE NOT FOUND'); process.exit(1); }
const html = fs.readFileSync(path.join(dir, file), 'utf8');
const m = html.match(/<script>([\s\S]*?)<\/script>/);
if (!m) { console.error('NO SCRIPT'); process.exit(1); }
try {
  new Function(m[1]);
  console.log('JS OK, script length=' + m[1].length);
} catch (e) {
  console.error('JS SYNTAX ERROR:', e.message);
  process.exit(1);
}
