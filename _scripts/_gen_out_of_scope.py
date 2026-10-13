# -*- coding: utf-8 -*-
"""生成 GATE-DELTA 范围外清单：历史未提交工作中与 CHG-20260918-MVS-001 无关的文件。

输出：.codebuddy/tmp/mvs-out-of-scope.txt（每行一个路径，供 -ExcludeFromFile 使用）
"""
import subprocess

REPO = r'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
OUT = REPO + r'\.codebuddy\tmp\mvs-out-of-scope.txt'

# 本次变更（CHG-20260918-MVS-001）的 33 个文件 —— 绝不排除
MVS = {
    'Source/PersonalChronicle/MVS/Domain/MvsConstants.cs',
    'Source/PersonalChronicle/MVS/Domain/MarketFactor.cs',
    'Source/PersonalChronicle/MVS/Domain/SupplyDemandModel.cs',
    'Source/PersonalChronicle/MVS/Domain/FactionArchetype.cs',
    'Source/PersonalChronicle/MVS/Domain/FlowGraph.cs',
    'Source/PersonalChronicle/MVS/Data/MVSCommodityDef.cs',
    'Source/PersonalChronicle/MVS/Data/FactionEconomyExtension.cs',
    'Source/PersonalChronicle/MVS/Data/MVSCommodityRegistry.cs',
    'Source/PersonalChronicle/MVS/Data/WorldComponent_Market.cs',
    'Source/PersonalChronicle/MVS/Application/MarketEventLog.cs',
    'Source/PersonalChronicle/MVS/Application/MarketEventSink.cs',
    'Source/PersonalChronicle/MVS/Application/Contracts/IMarketFactorProvider.cs',
    'Source/PersonalChronicle/MVS/Compatibility/StatPart_MarketValue.cs',
    'Source/PersonalChronicle.csproj',
    'Source/PersonalChronicle/Capture/Patch_IncidentWorker.cs',
    'Source/PersonalChronicle/ChronicleMod.cs',
    'Defs/MVS_Commodities.xml',
    'Defs/MVS_FlowEdges.xml',
    'Patches/MVS_IncidentImpacts.xml',
    'Patches/MVS_StatParts.xml',
    'Languages/ChineseSimplified/Keyed/MVS.xml',
    'Languages/English/Keyed/MVS.xml',
    'Languages/ChineseSimplified/DefInjected/MVSCommodityDef/MVS_Commodities.xml',
    'Languages/ChineseSimplified/DefInjected/MVSFlowEdgeDef/MVS_FlowEdges.xml',
    'Languages/English/DefInjected/MVSCommodityDef/MVS_Commodities.xml',
    'Languages/English/DefInjected/MVSFlowEdgeDef/MVS_FlowEdges.xml',
    'docs/设计文档/架构/EXC-2026-006.md',
    'docs/设计文档/架构/EXC-2026-007.md',
    'docs/设计文档/架构/EXC-2026-008.md',
    'docs/设计文档/架构/00-规范例外与变更管理.md',
    'docs/设计文档/方案/市场经济框架_v1设计方案.md',
    'docs/changelog/v1.4.x_2026-09-18_市场经济框架MVS.md',
    '.gitignore',
}

SKIP = ['.log', '.cache', '.dll', '.pdb', '.bak', '/obj/', '\\obj\\',
        '/bin/', '\\bin\\', '_backups/', '_backup/', '/.vs/', '\\.vs\\']


def run(args):
    return subprocess.run(args, cwd=REPO, capture_output=True, text=True,
                          encoding='utf-8', errors='replace').stdout


files = []
for args in (
    ['git', '-c', 'core.quotePath=false', 'diff', '--name-only', 'HEAD'],
    ['git', '-c', 'core.quotePath=false', 'ls-files', '--others',
     '--exclude-standard'],
):
    files += [x.strip() for x in run(args).split('\n') if x.strip()]

files = sorted(set(files))
files = [f for f in files if not any(s in f.lower() for s in SKIP)]

out = [f for f in files if f not in MVS]
miss = [m for m in MVS if m not in files]

with open(OUT, 'w', encoding='utf-8') as fh:
    fh.write('# GATE-DELTA 范围外清单 · 供 gate-runner.ps1 -ExcludeFromFile 使用\n')
    fh.write('# 生成：_scripts/_gen_out_of_scope.py\n')
    fh.write('# 语义：这些改动早于/独立于 CHG-20260918-MVS-001，非本次交付范围。\n')
    fh.write('# 本次变更文件的权威清单见 .codebuddy/tmp/mvs-annotation.md 的 front matter。\n')
    for f in out:
        fh.write(f + '\n')

print('改动集总数(过滤后):', len(files))
print('本次变更(MVS)在改动集中命中:', len(MVS) - len(miss))
print('范围外清单条数:', len(out))
if miss:
    print('⚠ 标注声明但改动集中未命中（需人工确认）:')
    for m in miss:
        print('   ', m)
