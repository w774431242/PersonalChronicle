# Gate Record — 2026-08-20 职业档案 UI 直查 Domain 修复（架构管线收尾）

- **Change:** 职业规划落地 + 六宫格 ITab + 预览 HTML 等前几轮改动的架构管线合规审计与修复
- **Version:** v1.1.4+（Save Schema 6，未 bump）
- **Date:** 2026-08-20
- **Reviewer:** AI（静态审计 + 编译/单测复跑）
- **Scope:** `ITab_Pawn_Career.Overview.cs` / `.Resume.cs` / `.Honor.cs` / `ITab_Pawn_Chronicle.cs` / `ArchiveUiDataProvider.Detail.cs` / `SectionSnapshots.Career.cs` / `SectionSnapshots.Detail.cs` + 重建 DLL

## 0. 审计方法

按 `07-AI开发规范.md` AI-005 实现协议 + `00-统一验收标准.md` GATE 流程，对前几轮改动做静态审计。环境具备 .NET SDK 8.0，可复跑编译与单测。

## 1. Gate 结果

| Gate | Result | Rule IDs | Evidence | Risk |
|---|---|---|---|---|
| ARCH | **PASS** | ARC-001/002/005, AI-005 | 消除 3 处 UI 直查 Domain 违规；事实聚合统一归属 Provider；Core 未引用 UI | 无 |
| DATA | **PASS** | DATA-001/006/010 | 新增字段为 Read Model 运行时快照（append-only 视图对象），未触碰 Def/Save schema | 无 |
| LOC | **PASS** | LOC-001/002 | 未新增任何硬编码文本；计数指标复用既有 Keyed | 无 |
| UI | **PASS** | UI-001/003 | 绘制态 prev 快照 + try/finally 配对恢复保持；空态守卫保留；三页改消费 `snap.FactCounts` | 无 |
| COMP | **N/A** | — | 本次未触碰 Harmony / 第三方 Mod 接入 | 不适用 |
| SAVE | **PASS** | SAVE-001/004 | 改动仅 Read Model 运行时字段（非持久化）；旧档行为无影响 | 无 |
| TEST | **PASS** | TEST-001 | 编译 0 错 0 警；单测 174 过 / 3 跳 / 0 失败 | 无 |
| PERF | **PASS** | PERF-001/002 | 事实计数由 Provider 单次聚合（原 UI 每帧 CountEvents 反更省）；DataRevision 机制未变 | 无 |
| REL | **PASS** | REL-006 | DLL 已同步发布区；未 bump 版本（纯架构修复） | 无 |

## 2. 发现与修复（按 AI-011 优先级）

### 审计发现的违规（P2 级，已修复）

| # | 问题 | 规则 | 修复 |
|---|---|---|---|
| P2-A1 | `ITab_Pawn_Career.Overview.cs:DrawIdentityBlock` 直接 `CountEvents(po, CareerEventType.*)` 在绘制路径聚合 Domain 事件计数 | UI-001 / ARC-002（Core 纯净）/ AI-005 | 改消费 `snap.FactCounts`（Provider 已聚合） |
| P2-A2 | `ITab_Pawn_Career.Resume.cs:DrawSummaryCol` 同样 `CountEvents` 直查 | UI-001 / ARC-002 | 改消费 `snap.FactCounts` |
| P2-A3 | `ITab_Pawn_Career.Honor.cs:DrawHonorContrib` 同样 `CountEvents` 直查（含 MedalGranted） | UI-001 / ARC-002 | 改消费 `snap.FactCounts` |
| P2-A4 | `ITab_Pawn_Career.cs:CountEvents` 死代码（违规写法载体） | AI-004（裸逻辑散落）/ ARC-013 | 删除；聚合统一收口到 `ArchiveUiDataProvider.BuildCareerFactCounts` |

### 修复落地（按 AI-005 管线顺序）

1. **Contract / Read Model**：`SectionSnapshots.Career.cs` 新增 `CareerFactCounts`（9 类事件计数只读快照）；`SectionSnapshots.Detail.cs` 的 `DetailSnapshot` 挂载 `FactCounts` 字段。
2. **Domain / Data**：无需改动；计数源 `CareerData.RecordCountByType` 已稳定存在。
3. **Read Model Provider**：`ArchiveUiDataProvider.Detail.cs` 新增 `BuildCareerFactCounts(pawn)`（从 `RecordCountByType` 一次取全 9 类），于 `BuildDetail` 的 Pawn 分支挂接 `snap.FactCounts`；同时为 `CareerOverviewView` 补 `Made/Built/Researched`（与 `Results` 同源口径，保持一致性，本次 Overview 实际改用统一 `FactCounts`）。
4. **UI / Presentation**：三页（Overview/Resume/Honor）删除 `CountEvents` 直查，改为消费 `snap.FactCounts`；绘制态 prev 快照 + try/finally 配对恢复不变。
5. **测试 / 诊断**：编译 0 错 0 警；单测 174 过 / 3 跳 / 0 失败（无新增用例，因仅重构消费方式，行为等价）。

## 3. 前几轮改动复核（本次审计范围外，确认无新增违规）

- `Dialog_SetPrimaryDirection.cs`（P2 缺口补全）：UI 只经 `component.MarkChanged()` 落盘，不直写存储细节；绘制态 prev 快照配对恢复；`ProfessionalDirectionDef.specializationKey` 等 §7.1 特化语义已落地；符合 ARC-002 / UI-001。
- `ITab_Pawn_Chronicle.cs`（六宫格遮盖修复）：`TabHeight` 720→640，纯布局常量，ScrollView 滚动兜底；无逻辑/分层违规。
- 预览 HTML（`职业档案Tab预览.html`）：调试工具，不影响 DLL；三按钮（🎲随机/📊初始化/🧪模拟）+ 空态守卫 + 行为面板 HTML/CSS 骨架已落地，JS 行为引擎为已知遗留（不影响架构 Gate）。

## 4. 结论

前几轮改动整体符合 v3.0 架构管线；本次审计发现并修复 4 处 P2 级 UI 直查 Domain 违规（统一收口到 Read Model Provider），编译/单测全绿，DLL 已同步发布区。无 P0/P1 风险，无 Save schema 变更，无需 bump 版本。
