---
change_id: CHG-20260920-MVS-AUDIT-006
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Economy/UI/**, docs/**]
files:
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: BuildPlan（void→float，两条出口各自返回） / DrawPlan（+qaQuestionWidth 参数） / Draw（接收并传递） / 删除 static float qaQuestionWidth
    lines: 100-165, 164-260
  - path: docs/changelog/v1.4.x_2026-09-20_审计修复批6.md
    type: A
    symbols: CHG-20260920-MVS-AUDIT-006
    lines: 1-110
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-AUDIT-006.md
    type: A
    symbols: CHG-20260920-MVS-AUDIT-006
    lines: 1-90
---

# 变更标注 · CHG-20260920-MVS-AUDIT-006（审计收尾）

**Change ID：** `CHG-20260920-MVS-AUDIT-006` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**前置批次：** `...-AUDIT-001`（12）· `-002`（3）· `-003`（3）· `-004`（2）· `-005`（2 + 更正记录），均已 PASS

## Change Annotation
- 用户「**继续**」⇒ 处置审计 **§0.7 第 2 条**（该审计**唯一尚未处置**的条目）⇒ **审计收尾**
- Scope(摘要): `qaQuestionWidth` 隐式跨方法通道 → **显式数据流**（并删除静态字段）

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Economy/UI/EconomySocietyPanel.cs` | M | `BuildPlan` / `DrawPlan` / `Draw` | `BuildPlan` `void`→`float`（两出口各返回）· `DrawPlan` 加参数 · 删 `static float qaQuestionWidth` · 加纪律注释 | GOV-004 / DATA-001 / GATE-UI | UI / DATA |
| `docs/changelog/v1.4.x_2026-09-20_审计修复批6.md` | A | `CHG-...-AUDIT-006` | 更新日志 + **审计完成度总表** | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-AUDIT-006.md` | A | `CHG-...-AUDIT-006` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并同步（MD5 `ACCB051ADF2CA3A0DE4DE541C1FED529`，两区一致）。

### 关键内容片段

```diff
// 删静态字段（隐式跨方法通道）
- private static float qaQuestionWidth;

// BuildPlan：显式返回（两条出口各自返回）
- private static void BuildPlan(EconomySocietySnapshot snapshot, float width)
+ private static float BuildPlan(EconomySocietySnapshot snapshot, float width)
+ float qaQuestionWidth = QaQuestionWidthMin;   // 声明在任何 return 之前
  …
+ return qaQuestionWidth;   // 出口①：未建模（不是"忘返回"，注释写明）
  …
+ return qaQuestionWidth;   // 出口②：正常（本方法唯一算出该值的地方）

// Draw：接收两次调用的返回值（第二次是"宽度让出滚动条后重建计划"，必须收新值）
- BuildPlan(snapshot, body.width);
+ float qaQuestionWidth = BuildPlan(snapshot, body.width);
- BuildPlan(snapshot, innerWidth);
+ qaQuestionWidth = BuildPlan(snapshot, innerWidth);

// DrawPlan：以参数接收（签名上看得见"谁在写"）
- private static void DrawPlan(Rect inner, EconomySocietySnapshot snapshot)
+ private static void DrawPlan(Rect inner, EconomySocietySnapshot snapshot, float qaQuestionWidth)
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（本批 3 个）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无临时文件残留（`_scripts/_tmp_*` 跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 该值当前是否真是"隐式通道" | 读 `BuildPlan` 写入点 + `DrawPlan` 读取点 | ⚠ 是真：字段在 `BuildPlan` 写、在 `DrawPlan` 读，**签名上无痕迹**（`DrawQa(…, qaQuestionWidth)` 只是把它**转发**给下一层） |
| `BuildPlan` 有几条出口 | 读全文（`return;` 出现在未建模分支） | ✅ **两条**（`:178` 未建模 / 末尾正常）⇒ 改返回类型**必须两处都返回**（C# 会拦，但先看清更稳） |
| 是否真的"重排会读到残值" | 推演：未建模出口**不返回**时会保留上一帧字段值 | ⚠ **当前读不到**（未建模时不会 Plan `BlockQa` 块）⇒ **安全纯属巧合** —— 正是审计说的"当前调用序安全" |
| 改后是否零行为变化 | 逐处比对数值与时机 | ✅ 数值/时机/布局一字未改，只改"怎么从测量走到绘制" |

### ⚠ 本批**未做**（审计层面的唯一遗留，附代码依据）
**§2.3 预截断调用点逐个迁移** —— 机制已建（`AUDIT-004` 的 `TruncateToWidth(..., Rect)` 重载 +
`Label` 兜底）；实测 **266 处**（`MasterControlTabWindow.Roster.cs` 46 处最多），
需**逐个确认是否真会被截断**，否则会给从不截断的标签挂空悬停（纯增开销）。

### ✅ 审计完成度（详见 Changelog 的总表）
§0（7 条）· §1（2 条）· §2（10 条）· §3（4 条模式）· §5（5 步）**全部已处置**；
§4 为"未发现"正面记录。**本批即审计收尾。**

### 回滚定位
1. `BuildPlan` 改回 `void` 并在方法内写回字段、`DrawPlan` 去掉参数、恢复字段声明（**不建议**）。
2. **运行时止损**：无 —— 纯数据流重构，不触存档、不改游戏状态、**无可见行为变化**。
