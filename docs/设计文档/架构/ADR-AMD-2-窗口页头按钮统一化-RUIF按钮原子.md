# ADR-AMD-2 — 窗口页头按钮统一化（RUIF `InputAtoms.Button`）

- Status: **Accepted**（2026-09-16 主理人拍板：范围 Phase A · tab 本轮不迁 · 主题边界条款 · `Button` 归 `InputAtoms`）
- Date: 2026-09-16
- Deciders: 岳立基（架构设计部 · 首席架构师）；主理人 董全程
- Rule IDs: GOV-002 / GOV-005 / GOV-007 / GOV-008；AI-003 / AI-004 / AI-011 / AI-014；COMP-*（版本门控与降级）
- Related Gates: GATE-ARCH / GATE-COMP / GATE-UI / GATE-TEST / GATE-DELTA
- 母件: `ADR-AMD-1`（跨库边界纪律 + **§8 增补条款**：B/R 档位命名 · 枚举类型位置 · 降级如实性）
- 相关工程: `PersonalChronicle`（PC，消费方）· `RUIF`（被依赖组件库）

---

## 1. Context

用户诉求（原话）：「**按钮组件统一化使用 RUIF**」。触发症状：窗口页头「返回」走 **vanilla `Widgets.ButtonText`**（`MasterControlTabWindow.Shell.cs:86`）⇒ 实机为**原版棕色按钮**，与相邻 PC 自制控件（主题开关）风格违和。

架构复核取证：
- **RUIF 无通用按钮原子**。最接近者：`NavAtoms.TabBar`（:49）/ `Pager`（:162）、`InputAtoms.Segmented`（:69）、`InputExAtoms.CycleButton`（:177）。**无** `Button` / `IconButton` / `GhostButton`。
- PC 按钮形态三套并存：① vanilla `Widgets.ButtonText`（40+ 调用点）② 自绘命中 `Widgets.ButtonInvisible`（50+ 处）③ 主题化助手 `UIComponents.Sidebar.cs:52`。
- 用户硬指令（2026-09-15）：「组件方向内容**必须引用 RUIF**，禁止自研组件」。

---

## 2. Decision

### D-1（问题 A）**选 A1：扩 RUIF 新增通用按钮原子**；范围 = **Phase A（页头按钮族）**
`InputAtoms.Button`（归属经主理人拍板 = `InputAtoms`，与 `Segmented`/`CycleButton` 同族）；RUIF 版本 **1.2.0 → 1.3.0**（minor，三处版本号 + DevDocs + About 计数同步）。

**Phase A 最终调用点清单**（主理人 grep 实测 + 架构复核；行号为 2026-09-16 冻结基线）：

| # | 文件:行 | 控件 | 处置 |
|---|---|---|---|
| 1 | `MasterControlTabWindow.Shell.cs:86` | 总控页头「返回」 | **纳入**（本轮症状源） |
| 2 | `MasterControlTabWindow.PopulationViews.cs:445` | 视图 tab 行 dev 按钮族（`Widgets.ButtonText(btn, label)` helper） | **纳入** |
| 3 | `MasterControlTabWindow.Dev.cs:52` | 同族 dev 按钮（测试数据等） | **纳入** |
| 4 | `Archive/ArchiveMainTabWindow.Embedded.cs:261` | 档案馆内层页头「返回」 | **不纳入**：该内层重复按钮组**本轮按用户要求删除** ⇒ 调用点**随删除消解**。若将来内层页头回归，按 Phase A 同规接入（本表即其入网凭据） |
| — | `MasterControlTabWindow.AppShell.cs:585/593/603/612/1151/1165/1272` | 制造中心**楼层工具条**（缩放 / 锁定 / 撤销 / 清空 / 原点） | **明确排除 → Phase B**（同为原生按钮，但属另一楼层视图、数量多、非本轮诉求） |

> ⇒ **Phase A 实际 = 3 处**（页头返回 + dev 按钮族两处）。**Phase B（楼层工具条及其余 `ButtonText`）另立 ADR-AMD-3**，**防 GATE-DELTA 判超范围**（AI-014 交付粒度 / §5 DELTA）。

### D-2（问题 B）**主题开关不迁入 RUIF** —— 边界正式条款
**「换肤 / 主题状态层不做（2026-09-12 决定维持）；通用输入·反馈**纯组件**（`Button` / `Segmented` / `TabBar` 等）可做。」**

理由（主理人批准，写死）：
1. RUIF 自述边界 =「**纯组件供应，不含主题 / 换肤 / 装饰层**」（`Mods/RUIF/About/About.xml:16/29`）。
2. 把**主题状态**搬进 RUIF 会**污染其 SemVer 冻结的公开面**，并**复活** 2026-09-12 被刻意移除的换肤层。
3. `Button` 与既有 `TabBar`/`CycleButton` 同类，属**纯组件** ⇒ 加它是**填补库空缺，不是越界**。

⇒ `UIComponents.ThemeSwitch` **保持 PC 复合控件**（持有 PC 主题状态 + 宽度协商 + D1/D2/D6 降级）；其视觉原语**允许**消费 RUIF 令牌与 `Draw.*`（现状已如此）；**不迁 RUIF**（可选优化，非本轮）。

### D-3（问题 C·拍板 2）**视图 tab 本轮不迁 `NavAtoms.TabBar`** + 登记 2 项库缺陷
架构独立复核 `11/RUIF/Source/Components/NavAtoms.cs:49-121`，确认主理人结论成立：
- **无逐 tab 禁用能力**：签名 `TabBar(Rect, string[] tabs, ref int sel, ref float indicatorX)`，非选中项**一律可点**（`:91`）⇒ 迁过去会**静默丢掉** `PopulationViews.cs:211/245` 的可用性判据（概览需 population / 档案两点需 archive）。
- 自带**「更多 n」`FloatMenu` 折叠**（`:101-120`）与「文字 + 底部指示条」外观 ⇒ 迁过去**改变 tab 行外观与交互**，超用户诉求。

⇒ 裁决：**tab 维持 PC 自绘**（现已由 RUIF 令牌/`Draw.*` 原子绘制，**不构成"自研组件"**）。注册为 **RUIF 库缺口**：
| 缺口 | 建议 | 验收条款 |
|---|---|---|
| `NavAtoms.TabBar` 缺 `bool[] enabled` 重载 | 后续 **1.4.0** 补 | **不得静默丢禁用语义**（禁用项不可点 + 降调前景） |

**⚠ 顺带发现的 RUIF 侧缺陷（本轮不修，登记）**：`NavAtoms.cs:123` `private static int pendingMorePick = -1` —— **`TabBar` 以静态可变字段中转 FloatMenu 选择**，违反 RUIF 自述「无隐藏状态」契约；若同帧两处 TabBar 或跨窗口复用，选择可能**错投到另一处**。建议 1.4.0 一并改为调用方持有的 `ref int`。

### D-4 `InputAtoms.Button` 确切签名与档位（原样转交表现层）
```csharp
namespace RUIF
{
    /// <summary>按钮档位（视觉权重）。默认 = Default。</summary>
    public enum ButtonKind { Default = 0, Secondary = 1, Danger = 2 }

    public static class InputAtoms
    {
        /// <summary>
        /// 通用按钮：自绘命中（不读除 Mouse/Event 外的状态），返回是否被点击。
        /// 交互态：默认 / 悬停 / 按下 / **禁用**（enabled=false ⇒ 不出命中 + 降调前景）。
        /// 契约：不持状态；文本按可用宽**截断（R2）**，不越出 rect。
        /// </summary>
        public static bool Button(Rect rect, string label,
                                  ButtonKind kind = ButtonKind.Default,
                                  bool enabled = true);
    }
}
```
- **建议随首发带上 `bool enabled`**：该成员 1.3.0 **全新**，此刻加参数**零兼容成本**；否则将来只为 `enabled` 再发一次版本（1.4.0）。
- **工具提示不进原子**：PC 在原子调用后对同 rect 调原版 `TooltipHandler.TipRegion`（保持原子单一职责，且现有页头「返回」的悬停提示行为不变）。
- **档位建议映射**（表现层可调，仅记录默认）：返回 → `Secondary`；dev 按钮族 → `Secondary`；未来「重置」类 → `Danger`。

**R 阶梯的责任划分（本轮定死，防越界）**：
| 档 | 归属 | 行为 |
|---|---|---|
| **R1** 全文案 | **RUIF 原子** | `label` 完整绘制 |
| **R2** 尾截断 | **RUIF 原子** | 可用宽不足时尾省略；永不越出 `rect` |
| **R3** 让位 / 不画 | **PC 布局** | 页头宽度协商：`TabBar`/标题优先，`Button` 让位；不足则由 PC 决定不画或折叠 |

> 刻意**不**让原子返回"是否绘制"——那会与"是否点击"的 `bool` 语义混淆（§8.3 如实性 + 单一返回语义）。**图标变体（`IconButton`）不在本轮**，登记为后续。

### D-5 桥与守卫形状（ADR-AMD-1 §8.2 落法）
- **`ButtonKind` 是 RUIF 枚举 ⇒ 版本敏感 ⇒ 禁止出现在 PC 的字段/属性/形参/返回/局部。** 桥**只暴露框架内类型**：
  ```csharp
  [NoInlining] internal static bool TryButton(Rect r, string label, int kind, bool enabled)   // 体内零 RUIF 类型
  { if (!available) return false;                                        // → B1
    if (VersionAtLeast(ButtonVersion)) return TryButtonV13(r, label, kind, enabled);           // B0
    return false; }                                                      // → B1
  [NoInlining] static bool TryButtonV13(Rect r, string label, int kind, bool enabled)          // 唯一可触碰 RUIF.ButtonKind 处
  ```
  `ButtonVersion = "1.3"`；`int kind` 编码 `0=Default / 1=Secondary / 2=Danger`，在 `TryButtonV13` 内映射。
- **档位双轴命名（ADR-AMD-1 §8.1）**：桥级 `B0/B1`；原子级 `R1/R2/R3`。**禁用 `D0/D1/D2`**（已被 StatTable 占用）。

### D-6（拍板 3）B1 回退的如实性
**B1 = vanilla `Widgets.ButtonText`**（"永在"渲染源 ⇒ 阶梯**必然可达**，优于 StatTable 的 D1）。**但**：注释 / 一次性日志 / Changelog **必须**写明「**RUIF < 1.3 时按钮回退为原版棕色按钮，非统一外观**」，**不得**宣称"旧版也统一"（LAND-009 / ADR-AMD-1 §8.3）。

---

## 3. Options Considered

| 方案 | 风险等级 | 优点 | 缺点 | 结论 |
|---|---|---|---|---|
| **A1 扩 RUIF（`InputAtoms.Button`）** | 中（库级 API 新增，已有 StatTable 先例与守卫模式） | 满足用户硬指令；填补库空缺；回退=vanilla 必达；页头本轮本就要改 = 最佳时机 | 新版本敏感面 + 1.3.0 版本联动 + 计数刷新 | **选定** |
| A2 不扩库（PC 统一为一个共用按钮组件） | 中（**架构违规**） | 零跨库版本风险 | **新增自制组件 ⇒ 违用户硬指令（AI-011 P1）**，且与本轮事故同型 | **否决** |
| A3 仅用既有原子（按钮仍走 A2） | 中 | tab 部分零风险 | RUIF 确实无按钮 ⇒ 用户症状仍在 | **本轮仅取"复用既有原子"精神**，但 **tab 迁移被 D-3 否决**（会丢禁用语义+改外观） |
| **迁移 4 视图 tab → `NavAtoms.TabBar`** | 中 | 零新增版本面 | 丢禁用语义 + 改外观 | **本轮跳过**（D-3；缺口登记 1.4.0） |
| **AppShell 楼层工具条一并改** | 中 | 一次性彻底统一 | **范围爆炸**（7+ 处、另一视图） | **跳过 → Phase B（ADR-AMD-3）** |

**AI-003 升序记录**：原生 Def/XML · DefModExtension · Comp/GameComponent · Service/Contract/Provider · PatchOperations **均 N/A**（UI 组件族）；Harmony ×3 **跳过**（改 RUIF 自身源码属库级扩展点，且 RUIF 零 Harmony）。

---

## 4. Consequences

- **正面**：满足用户硬指令；Phase A 3 处按钮收敛为 RUIF 原子；页头视觉一致；tab 保持既有正确语义（不静默丢禁用）。
- **负面**：RUIF 增 1 枚举 + 1 方法（SemVer minor 1.3.0）⇒ 三处版本号 + `DevDocs/API参考.md` + About 计数刷新；PC 新增 1 处版本敏感成员及其守卫。
- **补偿措施**：`B0/B1` 版本门控 + `B1` **如实**降级；枚举不外泄（§8.2）；`TryButtonV13` 守卫；Phase B 另立 ADR 防超范围。

---

## 5. Compliance

- **触发 Architecture Review**：**Yes** —— `Cross-Mod Direct Reference = Yes`（新增跨库类型/成员引用）。
- **例外登记**：**无 EXC**。
- **门禁计划**：
  | Gate | 判据 / 证据 |
  |---|---|
  | **GATE-ARCH** | ① 类型位置检索：`RUIF.ButtonKind` / `RUIF.InputAtoms` 在 PC **仅**出现于 `TryButtonV13` 体内（字段/形参/返回/局部 = 0）② 桥 `TryButton` 分派体零 RUIF 类型 ③ 0 Harmony、无新增继承 |
  | **GATE-COMP** | ① 两区 RUIF 制品 MD5 一致且含新枚举+方法 ② `B0/B1` 分派正确 ③ 强制 `B1`（RUIF<1.3）走 vanilla 且**日志如实**（**实机 → 移交 QA**） |
  | **GATE-UI** | 页头一行宽度协商不溢出（TabBar/标题优先、Button 让位、最后 R2 截断）；悬停/按下/禁用四态齐全；`ThemeSwitch` 未受影响 |
  | **GATE-TEST** | RUIF 单测（见 §6）0 失败；PC 0 错 0 警；Vanilla 启动；强制 B1 手测 |
  | **GATE-DELTA** | 调用点清单（**恰 3 处** + 档案馆返回"删除即消解"）↔ diff 一致；**Phase B 不得夹带**；无超范围/死代码 |
  | GATE-LOC | 按钮文案沿用既有 Key（`PersonalChronicle.UI.*`），无新增裸字符串（GOV-010） |
- **交付粒度（AI-014）**：Phase A 为**单一可交付单元**，须**同轮**产出 RUIF 原子 + 1.3.0 版本号 + PC 桥与 3 处调用点 + 变更标注；**不得**拆成多轮。

---

## 6. 单测与验收要点（RUIF.Tests，供机制研发部）

**可离线单测（纯计算）**：
1. `ButtonKind` 默认值 = `Default`（值 0）；枚举取值稳定（`0/1/2`）—— **禁止重排**（跨库契约）。
2. 档位 → 令牌色映射为纯函数（如 `InputFmt.ButtonTint(ButtonKind)`）：Default → `Tokens.Accent`、Secondary → `Tokens.Muted/BorderSoft`、Danger → `Tokens.Dead`。
3. R2 判据纯函数（如 `InputFmt.ShouldTruncate(labelWidth, rectWidth)`）：边界值（恰好相等 / 差 1px）**不得越界**。
4. `enabled = false` ⇒ 命中判据返回 false（若可注入）；否则标记 **实机**。

**必须实机（不得伪造，→ GATE-TEST）**：四态视觉（默认/悬停/按下/禁用）；页头一行不溢出；`B1` 强制降级为 vanilla 且日志如实。

---

## 附：变更记录

| 版本 | 日期 | 变更 |
|---|---|---|
| Proposed | 2026-09-16 | 初版裁决：A1（扩 RUIF）+ A3 的 tab 部分；主题开关不迁；3 项待拍板。 |
| **Accepted** | 2026-09-16 | 主理人拍板落地：① 范围 = **Phase A（恰 3 处）**，楼层工具条 → Phase B 另立 ADR；② **tab 本轮不迁**（复核 `NavAtoms.cs:49-121` 确认无 `enabled`、有 FloatMenu 折叠）+ 登记 2 项库缺陷（缺 `bool[] enabled`、`pendingMorePick` 静态状态）；③ 主题边界条款正式写死；④ `Button` 归 `InputAtoms`，给确切签名（含 `bool enabled`）、R 阶梯责任划分、桥守卫形状与单测要点。 |
