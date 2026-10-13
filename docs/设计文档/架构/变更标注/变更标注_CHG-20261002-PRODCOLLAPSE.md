---
change_id: CHG-20261002-PRODCOLLAPSE
version: preview-2026-09-22（游戏侧工单摘要折叠落地轮）
date: 2026-10-02
author: pc-orchestrator(AI)
scope: [Source/PersonalChronicle/, Languages/, 系统中心蓝图/待落地项/]
files:
  - path: Source/PersonalChronicle/Application/ReadModels/FloorDeviceDetailSnapshot.cs
    type: M
    symbols: FloorDetailRowView.IsSummary 新增（LAND-010 折叠摘要行声明）
    lines: ~218-232
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.cs
    type: M
    symbols: RowSummary helper + BuildBillTab 4 行摘要标注 + remarks
    lines: ~592-643, ~1141-1155
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.cs
    type: M
    symbols: billSummaryExpanded 字段 / 标题行折叠开关 / HasSummaryRows-CountSummaryRows / 测量联动
    lines: ~89-97, ~822-847, ~950-1000, ~1100-1185
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.Draw.cs
    type: M
    symbols: DrawRows summaryOnly 参数 + 可见行计数分栏
    lines: ~63-95
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Bill.DetailShow/Bill.DetailHide 新增（▶ 详情 / ▼ 收起）
    lines: ~3097-3098（Bill.Empty 后新增）
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Bill.DetailShow/Bill.DetailHide 新增（▶ Details / ▼ Collapse）
    lines: ~3093-3094（Bill.Empty 后新增）
  - path: 系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html
    type: M
    symbols: 生产页摘要段注记（默认折叠/按钮文案/行级声明）
    lines: ~802-806
---

# 变更标注 · CHG-20261002-PRODCOLLAPSE（工单摘要折叠独立 CHG）

| 文件 | 改动 | 摘要 | 关联 Gate |
|---|---|---|---|
| FloorDeviceDetailSnapshot.cs | M | IsSummary 行级契约声明（LAND-010；禁止 UI 下标特判） | UI/DATA |
| WorkshopFloorViewProvider.Detail.cs | M | 摘要集合=配方/基础工时/重复模式/目标数量（行键映射；HTML 3 行语义 ↔ 游戏 4 行） | UI |
| FloorDeviceDetailWindow.cs | M | 折叠态窗口瞬时字段（默认折叠=对齐预览 hidden）；标题行开关复用 ThemedButton；测量/绘制同式；切换窗高重算（A8 先例） | UI/PERF/DATA/SAVE |
| FloorDeviceDetailWindow.Draw.cs | M | DrawRows summaryOnly：折叠只画摘要行、分栏按可见行数 | UI/PERF |
| 两份 Archive.xml | M | 新键两语言（LOC-001/LAND-013）；▶/▼ 为对 ▸/▾ 的受控偏离（S6 字库教训，待 PM 复核） | LOC |
| 预览 HTML | M | 注记同步（默认折叠 + 游戏侧按钮文案 + 行级声明） | DELTA |

### 一致性自检
- [x] 摘要集合按行键映射声明（非"前 N 行"序号）；UI 层零硬编码行下标
- [x] 折叠态不入存档、不进 Def（窗口瞬时字段）；SAVE-001 判 N/A 有理由
- [x] 测量（CountSummaryRows）与绘制（DrawRows visibleCount/drawn）同式口径
- [x] 文案两语言 Keyed；XML 良构 + dup=0；部署抽查 Show=2/Hide=2
- [x] 编译 0 错 0 警；单测 393/394（1 跳过=实机项）
- [x] Change ID 贯穿：GR-20261001 §1 契约来源 → 本标注 → GR-20261002-PRODCOLLAPSE → DELTA（MERGE）
