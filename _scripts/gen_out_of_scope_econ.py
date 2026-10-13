# -*- coding: utf-8 -*-
"""生成 GATE-DELTA 范围外清单（通用化版本）。

与旧版 _gen_out_of_scope.py 的差异：
  旧版把 MVS 批次当作唯一在范围，本批的 29 个文件会被误列为「范围外」而掩盖问题。
  本版把「历次已标注批次」全部并入在范围内集合，只把真正未经任何标注的历史未提交
  工作列为范围外。

输出：.codebuddy/tmp/econ-out-of-scope.txt（每行一个路径，供 -ExcludeFromFile 使用）
"""
import subprocess

REPO = r'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
OUT = REPO + r'\.codebuddy\tmp\econ-out-of-scope.txt'

# ---- 已标注批次 1：CHG-20260918-MVS-001（33 文件，取自 mvs-annotation.md）----
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
    'Languages/English/DefInjected/MVSCommodityDef/MVS_Commodities.xml',
    'Languages/ChineseSimplified/DefInjected/MVSFlowEdgeDef/MVS_FlowEdges.xml',
    'Languages/English/DefInjected/MVSFlowEdgeDef/MVS_FlowEdges.xml',
}

# ---- 已标注批次 2：CHG-20260918-ECON-UI-001（本批 29 文件）----
ECON = {
    'Source/PersonalChronicle/MVS/Application/Adapters/MarketFactorAdapter.cs',
    'Source/PersonalChronicle/MVS/Application/Adapters/FactionProfileAdapter.cs',
    'Source/PersonalChronicle/MVS/Application/Adapters/MarketEventReadModelAdapter.cs',
    'Source/PersonalChronicle/MVS/Application/Adapters/FlowGraphAdapter.cs',
    'Source/PersonalChronicle/MVS/Application/Adapters/MarketAccess.cs',
    'Source/PersonalChronicle/MVS/Data/WorldComponent_Market.cs',
    'Source/PersonalChronicle/Application/SystemCenters/IEconomyMarketPanelDataProvider.cs',
    'Source/PersonalChronicle/Application/SystemCenters/IEconomyFlowGraphDataProvider.cs',
    'Source/PersonalChronicle/Application/SystemCenters/IEconomyFactionStatusDataProvider.cs',
    'Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs',
    'Source/PersonalChronicle/Application/ReadModels/EconomyFlowGraphSnapshot.cs',
    'Source/PersonalChronicle/Application/ReadModels/EconomyFactionStatusSnapshot.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionStatus.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/MarketContracts.cs',
    'Source/PersonalChronicle/Application/SystemCenters/SystemCenterDataRegistry.cs',
    'Source/PersonalChronicle/Application/ReadModels/SystemCenterSnapshots.cs',
    'Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs',
    'Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs',
    'Source/PersonalChronicle/Economy/UI/EconomyFlowGraphPanel.cs',
    'Source/PersonalChronicle/Economy/UI/EconomyFactionStatusPanel.cs',
    'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs',
    'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.PopulationViews.cs',
    'Source/PersonalChronicle/Data/ChronicleGameComponent.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Dev/DevArchiveFixtures.cs',
    'Languages/ChineseSimplified/Keyed/MVS.xml',
    'Languages/English/Keyed/MVS.xml',
}

# ---- 批次 2 追加：市场面板 1:1 还原 RUIF + 测试补全（用户裁定「测试没有补全」）----
ECON_APPEND = {
    'Source/PersonalChronicle/Archive/UI/RuifBridge.cs',
    'Source/PersonalChronicle/AssemblyInfo.Visibility.cs',
    'Source/PersonalChronicle/Economy/EconomyMarketFormat.cs',
    'Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs',
    'Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs',
    'Source/PersonalChronicle.csproj',
    'Tests/EconomyMarketFormatTests.cs',
    'Tests/EconomySnapshotContractTests.cs',
    'Tests/MvsAdapterContractTests.cs',
    '_scripts/measure_market_columns_1to1.js',
    '系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html',
}

IN_SCOPE = MVS | ECON | ECON_APPEND


def git_changed():
    """收集 git 实际改动文件集（与 gate-runner.ps1 口径一致，UTF-8 直读）。"""
    def run(args):
        r = subprocess.run(['git', '-c', 'core.quotePath=false'] + args,
                           cwd=REPO, capture_output=True)
        # git 在 Windows 下按 UTF-8 输出（quotePath=false）
        return [x.strip().strip('"') for x in
                r.stdout.decode('utf-8', 'replace').splitlines() if x.strip()]

    files = []
    files += run(['diff', '--name-only', 'HEAD'])
    files += run(['ls-files', '--others', '--exclude-standard'])
    return files


def main():
    changed = git_changed()
    # 生成物 / 日志类不参与（与脚本 ShouldSkip 对齐）。
    # ⚠ 陷阱：不能用「子串包含」判 .cache —— 会把 ArchiveMainTabWindow.Cache.cs 这类
    #   正常源码误判为生成物并从清单中漏掉（实测踩中）。改为按**扩展名**与**目录段**判定。
    skip_ext = {'.log', '.cache', '.dll', '.pdb', '.bak'}
    skip_dir_seg = {'obj', 'bin', '_backups', '_backup', '.vs'}

    def should_skip(p):
        norm_p = p.replace('\\', '/')
        segs = [s.lower() for s in norm_p.split('/')]
        if any(s in skip_dir_seg for s in segs):
            return True
        name = segs[-1]
        return any(name.endswith(e) for e in skip_ext)

    out_of_scope = []
    in_scope_hit = []
    for p in sorted(set(changed)):
        if should_skip(p):
            continue
        norm = p.replace('\\', '/')
        if norm in IN_SCOPE:
            in_scope_hit.append(norm)
        else:
            out_of_scope.append(norm)

    with open(OUT, 'w', encoding='utf-8') as f:
        f.write('# GATE-DELTA 范围外清单 · 供 gate-runner.ps1 -ExcludeFromFile 使用\n')
        f.write('# 语义：这些改动早于/独立于 CHG-20260918-MVS-001 与 CHG-20260918-ECON-UI-001，\n')
        f.write('#       属历史未提交工作，非本次交付范围。\n')
        f.write('# 在范围内的两批文件见各自的 *.md 变更标注 front matter。\n')
        for p in out_of_scope:
            f.write(p + '\n')

    print('被 git 判定改动的文件总数: %d' % len(set(changed)))
    print('两批在范围内命中        : %d' % len(in_scope_hit))
    print('范围外（写入清单）      : %d' % len(out_of_scope))
    miss = sorted(IN_SCOPE - set(in_scope_hit))
    if miss:
        print('!! 标注声明但 git 未见（可能是 D 类型或路径写错）: %d' % len(miss))
        for m in miss:
            print('   - ' + m)


if __name__ == '__main__':
    main()
