---
change_id: CHG-20261002-RUNTAB-EMPTYSTATE
version: preview-2026-09-22（概览运行读数空态挤兑修复轮）
date: 2026-10-02
author: pc-orchestrator(AI)
scope: [Source/PersonalChronicle/, 系统中心蓝图/待落地项/]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.cs
    type: M
    symbols: BuildRunTab 行集恒定（车间加成 3 行/本班 4 行/电网储能行恒占位「—」；EmptyHint 分支删除；flickable 局部变量删除）
    lines: ~339-510
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.cs
    type: M
    symbols: MeasureSectionHeight 补 DrawRows 块尾 6f；MeasureContentHeight 缓存键补快照引用 cachedDetailRef（LAND-011）
    lines: ~911-915, ~922-931, ~967, ~1015-1022
  - path: 系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html
    type: M
    symbols: 概览块 rmline 注记一条（空态恒占位说明；结构/行序/文案零改动）
    lines: ~716-721
  - path: 系统中心蓝图/待落地项/HTML同步任务_设备卡生产页_2026-10-01.md
    type: M
    symbols: 追加「空态修复段」
    lines: ~110-125
---

# 变更标注 · CHG-20261002-RUNTAB-EMPTYSTATE（概览运行读数空态挤兑修复）

| 文件 | 改动 | 摘要 | 关联 Gate |
|---|---|---|---|
| WorkshopFloorViewProvider.Detail.cs | M | BuildRunTab 行集恒定 15 行：车间加成 3 行（Nullable 承载）/本班 4 行/电网储能行空态「—」恒占位（LAND-009，对齐 HTML 基准 696-713）；删除 EmptyHint 分支（「界面消失」直接形态） | UI |
| FloorDeviceDetailWindow.cs | M | 测量补 DrawRows 块尾 6f（锚点互指 Draw.cs:182；修「每块估矮 6px→溢出遮盖」的挤兑缺口）；测量缓存键补快照引用（LAND-011 纵深防御） | UI/PERF |
| 预览 HTML | M | 仅 rmline 注记一条（空态恒占位说明）——HTML 结构/行序/文案零改动 | DELTA |
| 任务 md | M | 追加「空态修复段」 | DELTA |
| GR-20261002-RUNTAB-EMPTYSTATE.md | A | Gate Record（证据落盘） | 全部 |

### 一致性自检
- [x] 空态/满态行集恒等 15 行；两态对账表 Δ=0（GR §3，静态推演；实机复核待 PM）
- [x] 测量与绘制同源同序：DrawRows drawn 游标不变；测量口径补齐 6f 后与绘制恒等
- [x] 测量缓存键含快照引用（LAND-011）；窗口瞬时态不入存档（SAVE N/A 有理由）
- [x] 热路径零 new/LINQ（PERF-001）：测量/绘制路径仅 O(1) 引用比对；快照构建路径允许一次性拼接
- [x] 编译 0 错 0 警；单测 394=393/0/1（1 跳过=既有引擎 ECall Ignore）；部署 49 文件 DLL 13:57:09 一致
- [x] Change ID 贯穿：Intake（AI-001 声明）→ GR → 本标注 → DELTA
