---
change_id: CHG-20261005-COLTUNE2
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DeviceMinColW, WorkshopManageView.DeviceColSharesFull, WorkshopManageView.DeviceColSharesCompact, WorkshopManageView.DrawDeviceRow (悬停提示补齐)
    lines: 590-670, 930-965
---

# 变更标注 · CHG-20261005-COLTUNE2

## 背景（玩家 2026-10-05，附设备页截图）

> 「设备页面的**列宽过多**，建议调整列宽」

截图症状：窗口约 895px，**仍走 10 列全档** ⇒「位置」列只剩约 54px，
`98,0,113` 与「效率」列的 `0.8` 几乎贴在一起（视觉上像 `0.8 98,0,113`）。

## 一、根因：窄档门槛没跨过去

```csharp
private const float DeviceMinColW = 88f;                                  // 上一批的值
private static readonly float DeviceCompactWidth = DeviceColSharesFull.Length * DeviceMinColW;
// = 10 × 88 = 880  <  895 ⇒ 判定为"够宽" ⇒ 不降级
```

⇒ 门槛**只差 15px** 就降级了，但恰好没跨过 ⇒ 10 列平摊，每列约 86px（扣 gap 后更少），
而「位置」列还要放 `98,0,113`（约需 60px）⇒ 与左邻列挤在一起。

## 二、修法（三件一起）

### 1. 门槛 88 → **100**（⇒ `10 × 100 = 1000`）

玩家的 895px 窗口**正确降级为 8 列**（每列约 105px，宽松），
拉宽窗口超过 1000px 才回到 10 列。

### 2. 两套列宽按"实际内容需要"重分

**全档（10 列）**：名称 `0.205` / 操作员 `0.08` / 状态 `0.065` / 制作 `0.165` / 产出 `0.10` /
运行 `0.095` / 产值 `0.10` / 建立 `0.07` / 效率 `0.06` / 位置 `0.06`

> 调整依据：状态列只放"•运行 / •待命"（很短，0.07 是浪费）；制作列多数行是 `--`
> （只有少数行有长工单名）⇒ 从 0.17 收到 0.165；位置列与效率同宽（都是 ~60px 量级的短文本）。

**窄档（8 列）**：名称 `0.22` / 操作员 `0.11` / 状态 `0.085` / 制作 `0.18` /
产出 `0.11` / 运行 `0.11` / 产值 `0.11` / 位置 `0.075`

> 位置列给到 `0.075`（895px 下约 67px）—— `98,0,113` 刚好不截断。

### 3. 被隐藏的两列**移入悬停提示**（信息不丢）

```csharp
bool bornHidden = iBorn < 0;   // 窄档才为 true
bool effHidden = iEff < 0;
TooltipHandler.TipRegion(rect, new TipSignal(() =>
{
    string tip = DeviceLabelOf(thing) + "\n" + stateText + "\n" + thing.Position;
    if (bornHidden) tip += "\n" + T("ColBorn") + ": " + bornText;
    if (effHidden)  tip += "\n" + T("ColEfficiency") + ": " + effText;
    return tip;
}, thing.thingIDNumber));
```

**为什么必须补**：列数降级的前提是"隐藏的信息在别处可及"。若不补，
玩家在窄窗口下就**彻底看不到**建立时间与效率 —— 那是"降级"变成"丢功能"（LAND-009 不藏信息）。
全档时（两列可见）**不重复补**，避免同一条信息两处出现。
提示仍是 `TipSignal` 延迟求值 ⇒ 绘制期不拼串（沿用既有性能纪律）。

## 存档影响：**无**（纯绘制层常量与提示文本）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-COLTUNE2.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **你那个约 895px 的窗口**：设备页应显示 **8 列**（名称/操作员/状态/当前制作/产出/累计运行/累计产值/位置），
   每列明显更宽松，「位置」列不再与「效率」贴在一起（效率列此时**已隐藏**）；
2. **悬停任意设备行** ⇒ 提示里应多出两行：`建立时间: 第 9 天` 与 `效率（件/机时）: 0.8`
   （窄档专属；全档时不出现，因为屏幕上已能直接读到）；
3. **拉宽窗口超过 1000px** ⇒ 自动回到 10 列（建立时间 / 效率 回归，提示不再补这两行）；
4. 排序仍可用（点列名 ⇒ ▲/▼）；
5. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **门槛是"列数 × 每列下限"的一刀切**：不同列的信息长度差异很大（状态 5 字符 vs 名称 20 字符），
   统一下限只是近似。若将来某列内容变化（例如位置改为"距离"文本），需重新评估；
2. **在 1000px 附近来回拖窗口宽**会有"8 列 ↔ 10 列"的切换跳变（这是降级机制的本性，非缺陷）；
3. **悬停提示是唯一的补偿通道**：如果玩家从不悬停，就看不到那两列 ——
   若这不可接受，替代方案是把它们**并入别的列**（如效率并入产出显示为 `8/168 · 0.8`），
   但那会让列内信息变复杂，本批未做；
4. **`ColDurability` 孤儿键**仍未清理（上一批遗留，零运行时影响）。
