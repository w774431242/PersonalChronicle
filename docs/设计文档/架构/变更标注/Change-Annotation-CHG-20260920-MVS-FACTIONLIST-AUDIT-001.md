---
change_id: CHG-20260920-MVS-FACTIONLIST-AUDIT-001
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Economy/UI/**, Languages/**, docs/**, 系统中心蓝图/**]
files:
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: 玩家殖民地旁路（profile 种子 Neutral + profileOk/isPlayerFaction 门禁 + TechLevel 降级过滤赋值） / 副标计数句按 usesRealInterface 分键
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionStatus.cs
    type: M
    symbols: 类头/行上限/可见性注释正稿 / row.Name 删死 ?? / 排序 CompareOrdinal 兜底 / 删 TechRank 统一 TechLevelRank / 筛选空态新键
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs
    type: M
    symbols: 显式选中分支 + f.IsPlayer
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyFactionStatusSnapshot.cs
    type: M
    symbols: SubText/Columns/Name doc ×3 更正
  - path: Source/PersonalChronicle/Economy/UI/EconomyPanelChrome.cs
    type: M
    symbols: 副标四参悬停 / ToneColorOr(Warn) → UITheme.Threat
  - path: Source/PersonalChronicle/Economy/UI/EconomyFactionSelectorPanel.cs
    type: M
    symbols: 组头四参悬停
  - path: Source/PersonalChronicle/Economy/UI/EconomyFactionDetailPanel.cs
    type: M
    symbols: 溢出注色 Warn→Threat（三处同类统一）
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: FallbackTech 理由句改中性 / 新增 Sub.TechFiltered、FactionList.Empty.NoMatch / 组头两键对齐预览 / 注释更正 ×2
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: 同上（英文对称）
  - path: docs/changelog/v1.4.x_2026-09-20_派系列表审计修复.md
    type: A
    symbols: CHG-20260920-MVS-FACTIONLIST-AUDIT-001
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-FACTIONLIST-AUDIT-001.md
    type: A
    symbols: 本文件（自指）
  - path: docs/设计文档/架构/审计-派系列表-原版核心-2026-09-20.md
    type: A
    symbols: 专项审计报告（同批次前序产出，随本批入库）
  - path: docs/设计文档/架构/审计-经济中心五面板-2026-09-20.md
    type: M
    symbols: §4 追加 4-1 更正块 / §7-5 残键口径更正
  - path: docs/设计文档/架构/Change-Annotation-CHG-MVS-ITEMFLOW-001.md
    type: M
    symbols: ④ 玩家殖民地行追加 hidden 根因更正块
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_物品级产消读数.md
    type: M
    symbols: §16.3 根因证伪更正（删除线保留痕迹）
  - path: docs/changelog/v1.4.x_2026-09-18_经济中心五面板1比1落地.md
    type: M
    symbols: 「31=可见数」更正（删除线 + 登记指向）
  - path: 系统中心蓝图/待落地项/经济系统中心_五面板1比1对照清单.md
    type: M
    symbols: §3-6/§3-9 更正 / §4 追加 B 类登记 9（hidden 行集偏离）与 10（可见性语义层）
---

# 变更标注 · CHG-20260920-MVS-FACTIONLIST-AUDIT-001（派系列表 · 原版核心审计修复）

**Change ID：** `CHG-20260920-MVS-FACTIONLIST-AUDIT-001` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**派发链：** route-task（UNROUTED ⇒ 总控）→ `audit-subagents -Blocking` PASS → **pc-orchestrator** AI-001 核验（抓出 2 必改：`out` 短路链 CS0165 写法 / 玩家副标两句失实）→ 实施 → **pc-arch-gatekeeper + pc-data-steward + pc-ui-loc-guard** 三复审（建议全部随批消化）→ 复跑编译/单测/同步。

## Change Annotation
- 用户「按推荐执行」：落实 `审计-派系列表-原版核心-2026-09-20.md` 的 3 项裁决推荐 + 三批处置。
- **决策记录（ARCH 例外 E1 必填）**：
  1. **玩家殖民地旁路 = 采纳推荐 (a)**（承前裁决：五面板审计 §8-1(a) / 派系列表审计 §5-3）。**范围声明（有意最小面）**：三件套 = 列表例外（`def.hidden && !f.IsPlayer`）· 解析器例外（`!f.def.hidden || f.IsPlayer`）· 市场面板单点放行；**`FactionDetail`/`Society` 对玩家仍 NotModeled**（不污染共享门禁）。
  2. **被否决替代**：改共享门禁 `IsEconomicallyModeled`（会污染全部消费者 + 改动回落候选集）· 扩契约面（单一消费者不值得契约变更）——均否决正确，理由入档。
  3. 中性画像种子（`FactionArchetype.Neutral()` + 仅赋 `TechLevel`）⇒ bias 恒 1.0、父行 = ③ 基线原值；`TechLevel` 全方法消费点 = 3 处（`ForFaction` 不读它 / `IsVisible` 降级过滤）——DATA 复审逐点核过。
- **B3 承前留痕（ARCH 例外 E2）**：`Economy/EconomyMarketFormat.cs` ↔ `Application` 的双向引用**本批未触碰、状态未变**，处置（物理搬移 / 登记共享层）**待裁决**。
- **实机未执行（E3）**：点击玩家殖民地 → 选中保持 + 出表 + 副标渲染，**未验证**（无运行环境），不得宣称闭环。

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖本批全部改动文件（7 代码 + 2 语言包 + 8 文档；dll 走禁令①）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注类型与涉及符号
- [x] Change ID 与 Changelog / Gate Record 互链
- [x] 已跑 Keyed 审计脚本（中英 356=356 / only-in 无 / 占位符一致；报出的 9 项"referenced but missing"为脚本对**动态拼接前缀**的已知误报，与本批无关）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile … -Blocking` 自动校验

### 回滚定位
1. 玩家旁路：`MarketPanel.cs` 恢复原门禁早退 + `SystemCenterReadModelProvider.cs` 去掉 `|| f.IsPlayer`（回滚后玩家殖民地**可选中但市场表未建模**——旧行为）。
2. 色彩/悬停/文案：各自单点回退（Keyed 键值还原即可，新键删行）。
3. 记载更正：各载体原文均在删除线/更正块中保留，可反向还原。

**二进制**：`Assemblies/PersonalChronicle.dll` 已重编并两区同步（MD5 `4077C973F1DE34993AA8F552DAF7B800`），**需重启游戏**。
