---
change_id: CHG-20260919-MVS-EXC-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [docs/设计文档/功能模块/经济体系/**, docs/changelog/**, docs/设计文档/架构/**]
files:
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/EXC_MVS定价通道A.md
    type: A
    symbols: EXC-MVS-001
    lines: 1-160
  - path: docs/changelog/v1.4.x_2026-09-19_MVS定价通道A例外登记.md
    type: A
    symbols: CHG-20260919-MVS-EXC-001
    lines: 1-110
  # ⚠ `docs/设计文档/功能模块/经济体系/README.md` **有意不列入**（见下方"未跟踪文件说明"）
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/EXC_动态价格登记.md
    type: M
    symbols: 禁区范围限定 + T3 关系提示
    lines: 24-45
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/裁决包_T3与P0.md
    type: M
    symbols: P0 已裁决标记 + T3 额外前置
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-EXC-001.md
    type: A
    symbols: CHG-20260919-MVS-EXC-001
    lines: 1-120
---

# 变更标注 · CHG-20260919-MVS-EXC-001（MVS 定价通道 A 例外登记 + 三线边界澄清）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-EXC-001`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-EXC-001
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；决策由用户 2026-09-19 批准）
- Scope(摘要): 登记 `EXC-MVS-001`（E3）+ 为两条红线补范围限定

### 一句话动机
MVS 的 `StatPart_MarketValue` **同时违反两份文档写的红线**，而"红线被违反却没人处置"等于**没有约束力**。
用户批准 **P0-c + P0-a 组合** ⇒ 登记自有例外 + 写清三条经济线各自的红线边界。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `.../市场经济框架/EXC_MVS定价通道A.md` | A | `EXC-MVS-001` | 1-160 | **新例外登记（E3 / Approved）**：含冲突性质分析（**红线各属不同子系统**）、必要性论证、**两条被否替代方案**、风险、**9 条缓解措施**、**未执行验证项如实列出**、复审触发与到期日 | GOV-012 / GOV-007 | REL / COMP |
| `docs/changelog/v1.4.x_2026-09-19_MVS定价通道A例外登记.md` | A | `CHG-20260919-MVS-EXC-001` | 1-110 | **模板强制项**：`exc-template.md` 要求「E2 以上例外必须出现在 Changelog」⇒ 本文件即该条目 | REL-001 / REL-004 | REL |
| `.../市场经济框架/EXC_动态价格登记.md` | M | 禁区范围限定 | 24-45 | 声明禁区**只约束 MarketFramework 独立 MOD**；并补一条**与 T3 的关系提示**（第三条禁区「保持原版货单来源」） | GOV-012 / COMP-003 | REL / COMP |
| `.../市场经济框架/裁决包_T3与P0.md` | M | P0 已裁决标记 / T3 额外前置 | 1-130 | 标记 P0 已裁决并留下落实索引；**新增 T3 前置**：实施前须先判定是否落在 `EXC-FM-001` 第三条禁区内 | REQ-002 / GOV-012 | REQ |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-EXC-001.md` | A | `CHG-20260919-MVS-EXC-001` | 1-120 | 本文件自身（标注须覆盖全部改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 本批零代码改动**：无 `.cs` / 无 `.xml`（Def/Keyed）/ 无 dll 变更 ⇒
> **无编译、无单测、无需重启游戏、无玩法与存档影响**。
> **⚠ 二进制产物**：无（本轮不动代码）。

### ⚠ 未跟踪文件说明（GATE-DELTA 首次 BLOCKED 的根因，如实登记）

本批**还修改了** `docs/设计文档/功能模块/经济体系/README.md`（在立项红线下方补三线边界表），
但该文件在 git 里是 **untracked（`??`）** —— **从未被跟踪**（`git ls-files` 空），
故 `git diff HEAD` 不含它。而 GATE-DELTA 的规则是「标注只能声明 diff 中存在的文件」，
把它写进 `files:` 会触发 `BLOCKED：标注中存在 git 无对应改动的文件`。

⇒ 处置：**移出 `files:`，由 `-ExcludeFromFile` 承载**（exclude 的语义正是"范围外/预先存在的无关内容"），
并**在此如实记录该修改**，避免"因为工具限制就当没改过"。

| 文件（未跟踪，故不入 `files:`） | 类型 | 改动摘要 |
|---|---|---|
| `docs/设计文档/功能模块/经济体系/README.md` | M（untracked） | 红线下方补**三线边界表**（账本线 ❌ / MarketFramework ❌ / MVS ✅须 EXC 有效）+ 说明"为何补这条限定" |

📌 **可复用结论**：GATE-DELTA 的 `files:` **不能声明 untracked 文件**（`git diff` 看不见它们）；
遇到必须修改的未跟踪文档时，正确做法是 **exclude + 在标注正文里如实登记**，而不是把它塞进 `files:` 让门禁 BLOCKED。

### 关键内容片段

```yaml
# EXC-MVS-001 头部（模板 YAML）
id: EXC-MVS-001
status: Approved          # 用户（项目负责人）批准
level: E3                 # 全局 Patch + 影响财富/袭击点
rule_ids: [GOV-012]
expires: 2027-03-19
review_trigger: RimWorld 大版本 / StatDef[MarketValue] 结构变化 /
                StatDef.PostLoad 的 parts 排序规则变化 / 同类 Mod 冲突报告
```

```text
三条经济线的红线边界（补入 README + EXC-FM-001 + EXC-MVS-001 §8）
  经济体系（账本线）        ：❌ 不可反写 MarketValue
  MarketFramework（独立 MOD）：❌ 不可（动态价走 Harmony 价格出口）
  MVS 定价通道 A（主 mod）   ：✅ 可，但须 EXC-MVS-001 有效
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 前序批次（EXC/BIAS/VIS/SCALE 等）由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无调试代码残留 / 死代码（**本批零代码**）
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 两条红线各自属于哪个子系统 | 读 `经济体系/README.md` 定位句 + `AI001_任务入口.md` §2 | ✅ README=账本线（自我定位"观测"）；EXC-FM-001=**独立 MOD**（明确"不进主 mod"）⇒ **两条红线互不覆盖 MVS** |
| MVS 是否用了 Harmony（决定 EXC 等级与非侵入性论证） | 读 `Patches/MVS_StatParts.xml` | ✅ **零 Harmony** —— 走原版 `StatPart` 原生扩展点，符合 GOV-007 |
| 缓解措施是否真实存在（不能空口承诺） | 逐项回读 `StatPart_MarketValue.cs` / `MVS_StatParts.xml` / `MarketShockModelTests.cs` | ✅ 四闸门、`priority=-1000` 的 IL 依据、`offset==0 ⟺ 因子=1.0` 头号断言、schema v2 均为**既有实现**，非本次新增承诺 |
| EXC-FM-001 第三条禁区是否与 T3 相关 | 重读 `EXC_动态价格登记.md` §3 禁区 | ⚠ **相关**（「保持原版货单来源」）⇒ 已作为 **T3 额外前置**写入决策包 |

### 回滚定位
1. **只退边界限定**：还原 README / EXC-FM-001 两处追加段落（纯文档）。
2. **整体退回**：删除 `EXC_MVS定价通道A.md` 与对应 Changelog 条目 ⇒ 回到"红线被违反却无处置"的**原状态**
   （⚠ 不建议：那正是本批要消除的问题）。
3. **运行时止损**：**不适用** —— 本批不触任何运行路径。
