---
change_id: CHG-20261004-UIROWS
version: v1.1.5
date: 2026-10-04
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DrawMembers, WorkshopManageView.DrawMemberRow
    lines: 49-84, 866-876, 935-1020
---

# 变更标注 · CHG-20261004-UIROWS

人员页表格布局 / 对齐 / 美化。Build **1.6.4871 rev590** · 编译 **0 错 0 警**。

## 触发

玩家实机截图指出「优化该界面的 ui 布局，做好对齐，美化」。截图核对后发现**一个真 bug** + 三处观感缺陷。

## ① 真 bug：活动列宽为负（该列从未显示过）

改前列宽：`ColNameShare=0.50 / ColDuty=0.18 / ColSkill=0.16 / ColPost=0.14`
⇒ 四项合计 **0.98**，再叠加 `removeColShare = RemoveBtnW/宽 ≈ 0.037`
⇒ **活动列 share = 1 − 0.98 − 0.037 ≈ −0.017**。

后果（两处同时中招）：

| 位置 | 代码 | 后果 |
|---|---|---|
| 表头 | `UIComponents.Tables.cs:27` `Mathf.Max(0f, w)` | 「活动」列标题被压成 **0 宽** ⇒ 截图里该标题**整个消失** |
| 行 | `actW = width − nameW − … − RemoveBtnW − Gap` | **负宽** `Rect` ⇒ 活动文本永不绘制 |

⇒ 即「活动」这一列自加入起就没生效过，此前无人发现（其余列正常，视觉上只是"少一列"）。

**修法**（组件绘制规范铁律 4「先算空间预算，不够就降级，不要画崩」）：
定宽列重分配为 `0.22 / 0.20 / 0.18 / 0.20`（合计 0.80），余量 0.20 留给活动列；
再对余量做**最小可读宽判定** `ActMinW = 60f`：不足即**整列隐藏**，
且**表头与行共用同一结论**（`DrawMembers` 算出 `showActivity` 传给 `DrawMemberRow`），
杜绝「有标题没内容」或反之。行内`actW` 亦 `Mathf.Max(0f, …)` 兜底。

## ② 岗位列表头与内容错位

`UIComponents.Tables.cs:28` 把所有列表头**硬编码** `TextAnchor.MiddleLeft`，
而岗位单元格走 `ThemedButton`（`UIComponents.Sidebar.cs:73` `MiddleCenter`）
⇒ 表头「岗位」贴左、框内文字居中，**两者起点差 ≈ 半个框宽**。

**修法**：单元格内改画**宽度自适应内容**的 chip。宽度贴合文字后，
居中与左排在视觉上重合 ⇒ 对齐成立，且 `ColPostShare` 降级为 chip 的**可用上限**。

> 顺带消除另一处观感缺陷：整格铺满的按钮让岗位列变成三条等宽「色块柱」，笨重；
> chip 后每行按文字长度收缩，整列呼吸感回归。

## ③ 同行元素基线不齐 + 魔数

移除键写死 `rect.y + 3f, RowH - 6f`（行高魔法数，违铁律 1），岗位 chip 若另取值即与它不齐。
⇒ 抽 `RowCtrlInset = 3f`，由 chip 与移除键**共用**，同时消除魔数。

## ④ 测宽口径

`Text.CalcSize` 在 1.6 **只有 1 参重载**且依赖当前 `Text.Font`
（编译实测：`CalcSize(string, GameFont)` 与 `GenUI.GetWidthCached(string, GameFont)` 均**不存在**）。
⇒ 沿用工程既有范式（`WorkshopManageView.Schedule.cs:1127`、`UIComponents.cs:214`）：
`Text.Font` 设→ `CalcSize(text).x` → **还原** `Text.Font`。缺口若不还原会污染后续所有文本绘制。

## 未改动（刻意）

- **未新增 `UIComponents` 方法**：chip 复用既有 `ThemedButton`，不违反「同功能两处实现」。
- **未改 `TableHeader` 的 MiddleLeft**：它是全工程表头契约，改动会波及其他 5 处表格。
  chip 宽度自适应已使对齐成立，无需动共享组件。
- **未给 `ZebraRow` 加整行悬停**：该组件的 `interactive` 默认为 `false`，其注释记载
  2026-09-30 审计结论「不可点行画悬停会暗示可点」；本表整行不可点（仅 chip 与移除键可点），
  故保持 `false` 正确。

## Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **N/A** | 单文件内布局常量与绘制分支，无新增层/依赖边/公开签名（`DrawMemberRow` 为 private） |
| DATA | **N/A** | 零 Def |
| SAVE | **N/A** | 零存档字段 |
| UI | PASS（静态） | 负宽根因已除（余量 + `ActMinW` 降级 + `Max(0f)` 兜底）；对齐按「表头与行同结论」处理；宽度取自 `Text.CalcSize`；魔数已抽常量。**未实机复验**（沙箱无游戏进程），观感需玩家确认 |
| LOC | **N/A** | 未新增/改动任何 Keyed 键（沿用 `ColActivity` / `PostUnassigned` 既有键） |
| COMP | **N/A** | 零 Harmony、零第三方 Mod |
| PERF | PASS | chip 测宽为 O(1) 且绘制期零堆分配（`CalcSize` 返回结构体）；隐藏活动列时反而**减少**绘制 |
| TEST | 部分 PASS | 0 错 0 警；**窄窗口降级路径未实机验证**（`showActivity == false` 分支） |
| REL | **未执行** | 版本 / Changelog 待定稿 |
| DELTA | 见§6 | 由 `gate-runner.ps1` 机械校验 |

## Remaining Risks

1. **P1** `DrawMemberRow` 新增第 4 参 `bool showActivity`（private 方法）。本类的定义与调用点均在
   `WorkshopManageView.cs:939`（定义）/ `:910`（唯一调用点），二者已同步。
   ⚠ 更正一处措辞：`Dialog_WorkshopCrewEditor.cs:85/144` 另有**同名方法**，但属另一个类
   （班组编辑器），与本改动**无调用关系**。编译 0 错 0 警即证明不存在「漏改某个调用点」
   ——若存在，签名不匹配会直接编译失败。
2. **P2** 活动列此前从未显示 ⇒ 本单元是该列**首次真正生效**。其文本来自原版 `job.GetReport()`，
   长度不可控；已沿用既有 `TruncateToWidth` 预截断，但**首实机可能暴露截断观感问题**。
3. **P2** chip 宽度随岗位名长度变化 ⇒ 不同人行的 chip 宽度不同（视觉上更自然，但不再是整齐列）。
   若玩家偏好等宽列，需回退到固定宽（并接受错位或改 `TableHeader`）。

## 附：与同会话其他单元的关系

- `CHG-20261004-POSTOVERRIDE`（本会话前序）：新增岗位覆盖功能，其 UI 入口即本单元改造的 chip。
  两个单元改**同一文件不同区段**（前者 `:965-1005` 的控件选型 vs 本单元的列宽/几何），
  语义无冲突，叠加后为最终形态。
