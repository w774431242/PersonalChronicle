import fs from "fs";
import path from "path";
import vm from "vm";

// Execute the sibling land_career_slice.py after a mechanical Python→JS rewrite.
const pyPath = path.join(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "land_career_slice.py");
let py = fs.readFileSync(pyPath, "utf8");

function convert(src) {
  src = src.replace(/\r\n/g, "\n");
  src = src.replace(/^#.*\n/, "");
  src = src.replace(/^"""[\s\S]*?"""\n/, "");
  src = src.replace("from __future__ import print_function\n", "");
  src = src.replace("import os\nimport re\nimport io\n", "");
  src = src.replace(/ROOT = r"([^"]+)"/, 'const ROOT = "$1"');
  src = src.replace(/([A-Z_]+) = \{/g, "const $1 = {");
  src = src.replace(/EXISTING_SKILLS = \{/g, "const EXISTING_SKILLS = new Set([");
  src = src.replace(
    /const EXISTING_SKILLS = new Set\(\[([\s\S]*?)\n\}/,
    (m, inner) => "const EXISTING_SKILLS = new Set([" + inner.replace(/"\s*,?\s*\n\s*"/g, '", "') + "]);"
  );
  // tuples → arrays
  src = src.replace(/\("([^"]*)", "([^"]*)"\)/g, '["$1", "$2"]');
  src = src.replace(/\("([^"]*)", "([^"]*)", "([^"]*)", "([^"]*)", "([^"]*)", ([0-9.]+), "([^"]*)", (True|False), "([^"]*)"\)/g,
    '["$1","$2","$3","$4","$5",$6,"$7",$8==="True","$9"]');
  src = src.replace(/\bTrue\b/g, "true").replace(/\bFalse\b/g, "false");
  src = src.replace(/^def /gm, "function ");
  src = src.replace(/io\.open\(([^,]+), encoding="utf-8"\)\.read\(\)/g, "fs.readFileSync($1, \"utf8\")");
  src = src.replace(/io\.open\(([^,]+), "w", encoding="utf-8", newline="\\n"\)\.write\(([^)]+)\)/g, "fs.writeFileSync($1, $2)");
  src = src.replace(/os\.path\.join/g, "path.join");
  src = src.replace(/os\.path\.isdir/g, "fs.existsSync");
  src = src.replace(/os\.path\.isfile/g, "fs.existsSync");
  src = src.replace(/os\.makedirs\(([^)]+)\)/g, "fs.mkdirSync($1, { recursive: true })");
  src = src.replace(/re\.finditer\(/g, "matchAll(");
  src = src.replace(/re\.search\(/g, "search(");
  src = src.replace(/re\.match\(/g, "match(");
  src = src.replace(/re\.sub\(/g, "sub(");
  src = src.replace(/re\.escape\(/g, "escapeRe(");
  src = src.replace(/flags=re\.S/g, '"s"');
  src = src.replace(/m\.group\((\d+)\)/g, "m[$1]");
  src = src.replace(/m\.group\(0\)/g, "m[0]");
  src = src.replace(/for m in matchAll\(([^,]+), ([^)]+)\):/g, "for (const m of $2.matchAll(new RegExp($1.slice(1,-1), \"g\"))) {");
  src = src.replace(/print\(/g, "console.log(");
  src = src.replace(/if not ([^:\n]+):/g, "if (!$1) {");
  src = src.replace(/if ([^:\n]+):/g, "if ($1) {");
  src = src.replace(/return ([^\n]+)/g, "return $1;");
  src = src.replace(/None/g, "null");
  src = src.replace(/\.items\(\)/g, "");
  src = src.replace(/for ([^,]+), ([^ ]+) in ((?:v, k) for k, v in DIR_SKILL):/g, "for (const [k, v] of Object.entries(DIR_SKILL)) { const skill = v; const dir_name = k;");
  src = src.replace(/if __name__ == "__main__":\n    main\(\)/, "main();");
  return src;
}

const converted = convert(py);
fs.writeFileSync(path.join(path.dirname(pyPath), "_converted_preview.js"), converted.slice(0, 4000));
console.log("preview written", converted.length);
