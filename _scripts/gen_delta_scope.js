/* 临时：生成本批变更标注的范围外清单（跑完即删）
   读 `git status --porcelain`（core.quotePath=false ⇒ UTF-8 原样），
   剔除本批文件，其余全部写入排除清单。
*/
const fs = require('fs');
const { execFileSync } = require('child_process');

const MINE = [
  'Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs',
  'Source/PersonalChronicle/Application/ReadModels/EconomyFlowGraphSnapshot.cs',
  'Source/PersonalChronicle/Application/ReadModels/EconomyFactionStatusSnapshot.cs',
  'Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs',
  'Source/PersonalChronicle/Application/ReadModels/EconomyFactionDetailSnapshot.cs',
  'Source/PersonalChronicle/MVS/Data/MVSCommodityDef.cs',
  'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.cs',
  'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs',
  'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs',
  'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionStatus.cs',
  'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs',
  'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionDetail.cs',
  'Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs',
  'Source/PersonalChronicle/Economy/UI/EconomyPanelChrome.cs',
  'Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs',
  'Source/PersonalChronicle/Economy/UI/EconomyFactionDetailPanel.cs',
  'Source/PersonalChronicle/Economy/UI/EconomyInventoryChainPanel.cs',
  'Source/PersonalChronicle/Economy/UI/EconomyFactionSelectorPanel.cs',
  'Source/PersonalChronicle/Economy/UI/EconomyFlowGraphPanel.cs',
  'Source/PersonalChronicle/Economy/UI/EconomyFactionStatusPanel.cs',
  'Source/PersonalChronicle/Economy/EconomyMarketFormat.cs',
  'Source/PersonalChronicle/Archive/UI/RuifBridge.cs',
  'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs',
  'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs',
  'Defs/MVS_Commodities.xml',
  'Languages/ChineseSimplified/Keyed/MVS.xml',
  'Languages/English/Keyed/MVS.xml',
  'Tests/EconomySnapshotContractTests.cs',
  'Assemblies/PersonalChronicle.dll',
  'docs/changelog/v1.4.x_2026-09-18_经济中心五面板1比1落地.md',
  '系统中心蓝图/待落地项/经济系统中心_五面板1比1对照清单.md',
  '_scripts/measure_econ_preview.js',
  '_scripts/audit_mvs_keyed.js',
  '_scripts/crop_shot.py',
  '_scripts/sync_release.ps1'
];

// ⚠ 必须 -uall：默认的 `status.showUntrackedFiles=normal` 会把**整个未跟踪目录**
//   折叠成一条 `?? dir/` ⇒ 未跟踪文件（本工程 Economy/ 等目录从未提交）根本列不出来，
//   标注里的逐文件路径会被判成 phantom。
const raw = execFileSync('git', ['-c', 'core.quotePath=false', 'status', '--porcelain', '-uall'],
  { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
const all = [];
for (const line of raw.split(/\r?\n/)) {
  if (!line.trim()) continue;
  let p = line.slice(3);
  const arrow = p.indexOf(' -> ');
  if (arrow >= 0) p = p.slice(arrow + 4);
  p = p.replace(/^"|"$/g, '');
  all.push(p);
}
const mineSet = new Set(MINE);
const missing = MINE.filter(m => !all.includes(m));
const exclude = all.filter(p => !mineSet.has(p));
const hit = all.filter(p => mineSet.has(p));

fs.writeFileSync('.codebuddy/tmp/econ-ui-002-exclude.txt',
  '# CHG-20260918-ECON-UI-002 范围外（历史未提交工作）—— 由 _scripts/_tmp_gen_delta_scope.js 生成\n'
  + exclude.join('\n') + '\n', 'utf8');

console.log('git changed total = ' + all.length);
console.log('annotation declared = ' + MINE.length + '   hit in diff = ' + hit.length);
console.log('declared but NOT in git status (' + missing.length + '): ' + (missing.join('\n  ') || 'none'));
console.log('exclude list written = ' + exclude.length);
console.log('--- mine seen ---');
console.log(hit.join('\n'));
