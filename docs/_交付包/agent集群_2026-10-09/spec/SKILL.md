---
name: RimWorld工程架构规范
version: 3.1.0
description: |
  RimWorld Mod Engineering Standard v3.0 —— 面向 RimWorld 1.6 Mod 的工程架构规范（治理/架构/数据/兼容/表现/发布/AI 协议）。
  当任务涉及：新增或重构 Mod 功能模块、分层与依赖方向判定（UI→Application→Domain←Data/Capture）、
  Def/DefModExtension/Comp/Service/Provider/Harmony 扩展点选型、Save schema 与 Migration、
  第三方 Mod 兼容接入、UI/Theme/Localization/Asset 规范、发布前 Gate 验收、例外（EXC）登记、
  需求 REQ/SDD 文档、以及「先架构后编码」的 AI 开发协议时使用。
  也用于回答「这条规则编号是什么」「GOV/ARC/DATA/COMP/UI/SAVE/LAND/GATE/AI-* 规则内容」「依赖矩阵是否允许」。
  当任务是把 HTML/RUIF 预览落地为游戏内界面（尺寸对齐、窗口居中、响应式分档、卡片规格复现）时，
  同样加载本 skill 并走 `11-预览落地标准化规范.md`（LAND-*）。
  本 skill 提供按任务类型路由的分册索引：先查路由表定位 1~2 个分册再按需读取，禁止一次读入全部。
  纯 API 用法查询（某字段是否存在、原版机制细节）改用「RimWorld模组开发知识库」skill。
  强制触发词：用户说「启用标准化agent集群」时，**必须**加载本 skill 并立即执行「子 agent 集群触发」协议（见下文），不得跳过、不得静默放行。
---

# RimWorld Mod Engineering Standard v3.0

把 Mod 开发从"能运行的功能"提升为"可维护、可扩展、可兼容、可验证、可持续交付的工程产品"。

```text
正确性 → 解耦 → 低耦合 → 可扩展 → 可兼容 → 可测试 → 可发布 → 可迁移
```

## 使用规则（渐进式披露）

1. **先路由，后读取**：在下方路由表选 1~2 个分册读取，不要批量读入全部（全量 24 册约 225KB，单册最大 `08c` 约 52KB）。
2. **规则 ID 是稳定契约**：引用规则一律写 `ARC-006` 这类 ID，不得因排版/文件移动改号；废弃规则保留原 ID 标 `Deprecated`。
3. **优先级裁决**：平台与安全(P0) > 治理与架构(P1) > 数据/扩展/兼容(P2) > 表现(P3) > 工程质量与交付(P4) > 实现建议(P5)。同级：更具体 > 明确"不得" > 平台事实 > 项目偏好；无法裁决则停止实现并提交 Architecture Review。
4. **平台事实优先**：具体 API 行为以锁定的游戏/DLC/Harmony/编译引用版本为准；与本规范冲突时以平台事实为准。
5. **示例占位**：文档中的 `MyMod`/`MyMod_`/`MyMod.` 必须替换为项目唯一固定标识。

## 子 agent 集群触发（本 skill 被调用时）

PersonalChronicle 工程内置子 agent 集群（`.codebuddy/agents/`：1 总控 + 6 个 Gate agent）。
**本 skill 被调用时**按下列顺序触发：

> **🔒 强制触发词（硬开关，覆盖一切路由判定）**：任务文本含「**启用标准化agent集群**」时，**必须**无条件执行本节的集群前置协议（加载规范 → `route-task.ps1` → `audit-subagents.ps1 -Blocking` → `task` 工具派发），不受下文「语义锚点」免责声明影响，也不得因路由 UNROUTED 而静默跳过——此时一律派发 `pc-orchestrator`（总控）统一分发。该短语是用户显式要求启用集群的硬指令。

1. **确定性路由**（推荐，可复现、可回归）：
   ```powershell
   pwsh .codebuddy/audit/route-task.ps1 -Task "<任务描述>"             # 任务文本路由
   pwsh .codebuddy/audit/route-task.ps1 -Skill RimWorld工程架构规范    # skill 触发
   ```
   输出「命中 agent + 命中词证据 + 覆盖 Gate + `P0–P4` 分级 + 并行/单选裁决」。
2. **语义路由**（退化路径）：由主 Agent 按各 agent 的 `description` 与 `TRIGGER_KEYWORDS` 自主判断。
3. **派发前置硬门禁**：`pwsh .codebuddy/audit/audit-subagents.ps1 -Blocking` ⇒ `BLOCKED` 时**不得派发**（`GOV-012`）。

**skill → agent 映射**（agent 侧 `SKILLS:` 契约；注册表见 `.codebuddy/audit/subagent-audit-checklist.md` §0.3）：

| skill | 唤起的 Gate agent |
|---|---|
| `RimWorld工程架构规范` | 全部 6 个（ARCH / DATA+SAVE / UI+LOC / COMP / PERF+TEST / REL+DELTA）+ 总控 |
| `RimWorld模组开发知识库` | DATA+SAVE / COMP / PERF+TEST |
| `RimWorld组件绘制` | UI+LOC |
| `RimWorld程序集API静态核实` | ARCH / COMP |
| `ai-bug-engineering` | PERF+TEST |
| `summarize`、`change-report` | REL+DELTA |

⚠️ `description`、`TRIGGER_KEYWORDS`、`SKILLS` 都只是**语义锚点**，不构成强制路由。要求 100% 确定时：用 `manual` 模式点名，或由 `pc-orchestrator` 统一分发。改动任何触发词或 `SKILLS` 后，**必须重跑** `audit-subagents.ps1` + `route-task.tests.ps1`。

## 路由表（任务类型 → 分册）

均在 `references/` 下。

| 任务 / 症状 | 首选分册 |
|---|---|
| **AI 任务入口速查（先读此卡再路由）** | `assets/cheat-sheet.md` |
| 规则冲突裁决、优先级、分层总览、规则 ID 域 | `00-规范体系总纲.md` |
| 某模块能否依赖另一模块、依赖矩阵 | `00-规范关系矩阵.md` |
| 术语含义（Read Model / Provider / Contract / Adapter…） | `00-统一术语与定义.md` |
| 需求文档、REQ/SDD 编写与评审 | `00-需求与SDD规范.md` |
| 开发流程 Stage、生命周期 | `00-工程生命周期规范.md` |
| 发布前验收、Gate 判定、Gate 记录模板 | `00-统一验收标准.md` |
| 验收阶段精准定位已修改内容、变更标注 | `assets/change-annotation-template.md` + `00-统一验收标准.md#11-gate-delta变更一致性-gate` |
| 偏离规范、例外登记 EXC | `00-规范例外与变更管理.md` |
| 查规则编号含义 / 编号范围 | `00-规则编号索引.md` |
| v1/v2 → v3.0 迁移对照 | `00-版本迁移说明.md` |
| 命名、ID、日志、硬编码治理、最小测试矩阵（BASE/TEST） | `01-基础层规范.md` |
| 分层边界、Core 纯净、Service/Contract/Provider/Registry、静态状态、事件（ARC） | `02-架构层-核心架构规范.md` |
| Def/Runtime/Save/UI 数据边界、DefModExtension、Comp、Patch、Save/Migration（DATA/SAVE） | `03-架构层-数据与扩展规范.md` |
| 第三方 Mod/DLC 接入、软硬依赖、Harmony 升级条件、降级（COMP） | `04-架构层-兼容与接入规范.md` |
| UI/Theme/Asset/Localization/布局/热路径性能（UI/LOC/ASSET/PERF） | `05-表现层规范.md` |
| 版本号、Changelog、Change Summary、回滚、发布包 | `06-变更日志与发布规范.md` |
| **AI 执行协议（任务入口/禁止清单/交付模板）** | `07-AI开发规范.md` |
| 代码轻量化、大文件治理、partial 切片 | `08-架构层-代码轻量化方案.md` |
| UI 对接数据契约、快照与刷新门控、Scribe 序列化、存档迁移（DL-1~25，正文以数字章节承载） | `08c-数据层契约与UI对接标准.md` |
| 模块/文件架构范式（目录骨架、命名范式） | `09-模块文件架构范式.md` |
| 美术资产产生/命名/格式/验收（图标·面板·勋章·贴图，ASSET 落地细则） | `10-美术资产标准化规范.md` |
| **把 HTML/RUIF 预览落地成游戏内 UI**（量预览、1:1 定义、窗口尺寸与居中、响应式分档、卡型阈值、字体档、截断兜底、缺口如实、LAND-Gate） | `11-预览落地标准化规范.md` |
| 项目落地映射：Provider/Read Model/事件/接入层 | `附录-项目架构实施映射.md` |
| 项目已验证的硬性教训合集 | `开发铁律.md` |
| 历史 Gate 记录 / 例外实例 / 已退役清单（写法参考） | `history-archive/` |

## 全局不可违反规则（GOV-001–012）

| ID | 规则 |
|---|---|
| GOV-001 | 先架构后编码：新功能必须先标注所属层、依赖和数据边界 |
| GOV-002 | 组合优先于继承：优先公开扩展点、服务、适配器 |
| GOV-003 | 数据、运行时状态、持久状态与表现分离 |
| GOV-004 | 依赖只能沿矩阵允许方向流动；冲突按总纲裁决 |
| GOV-005 | 跨模块通信走契约，不得读其他模块内部实现 |
| GOV-006 | 硬编码必须有分类、来源、生命周期；禁散落魔法数字/字符串 |
| GOV-007 | 先用原生扩展点，再考虑 Harmony；Harmony 须有理由、范围、回滚 |
| GOV-008 | 第三方 Mod 行为必须在 Compatibility 层，Core 不认识目标 Mod |
| GOV-009 | 程序逻辑依赖稳定 ID，禁依赖 label/description/翻译文本 |
| GOV-010 | 每个用户可见文本必须经 Localization 管道且可审计 |
| GOV-011 | Save schema / Migration / Testing / Performance / Release 属交付内容 |
| GOV-012 | AI 不得绕过 Gate、例外登记和人工确认 |

依赖主方向：`Foundation → Core/Data → Presentation`、`Foundation → Core/Data → Compatibility`、`Core/Data → Save/Testing/Release`。
禁止反向：`Core ✕→ UI/Theme/Asset/Compatibility`、`Domain ✕→ RimWorld UI 实现`、`Localization ✕→ 业务状态`、`Compatibility ✕→ 改 Core 设计迁就单一第三方 Mod`。

## AI 执行协议（摘要，完整见 `references/07-AI开发规范.md`）

**任务入口（AI-001）**：先输出 目标/非目标、涉及文件、任务层级、适用规则 ID、新增修改的 Def·Runtime·Save·UI·Key·Asset、第三方影响、是否触发 Architecture Review、扩展点计划、Gate 计划。信息不足用最小安全假设并标记。

**扩展点选择（AI-003）风险升序**：
```text
原生 Def/XML → DefModExtension → Comp/GameComponent → Service/Contract/Provider
→ PatchOperations → Harmony Postfix → Harmony Prefix → Harmony Transpiler
```
不是"越后越强"，是风险升级；必须记录被跳过方案及原因。

**禁止生成（AI-004）**：Core 中的第三方条件分支、按显示文本做逻辑判断、完整覆写第三方 Def、裸 Key/DefName/AssetPath、无界静态可变单例、Tick 中全库扫描/反射/无界分配、空 catch 与静默失败、未登记 Harmony、把 UI/翻译/Texture 写进 Save。确需则登记 EXC。

**触发 Architecture Review**：`Harmony=Yes`、`Hardcoding=Yes`、`Cross-Mod Direct Reference=Yes`、`Static Mutable State=Yes` 任一成立。

**实现顺序（AI-005）**：
```text
Contract/数据 schema → Domain/Core → Data/Def/Comp → Read Model/Provider
→ UI/Theme/Asset/Localization → Compatibility Adapter/Patch → 测试与诊断
```
不得在同一类里混合"采集、聚合、存档、UI 绘制、兼容 Patch"。

**交付粒度（AI-014）**：同一可交付单元（完整页面 / 端到端功能 / 强耦合模块组）须在同一轮内一次性产出全部文件与变更标注；仅允许因架构分层隔离、方向性阻塞、单轮输出上限或用户显式要求分批而拆分，禁止把可一次完成的单元切成 N 轮逐一交付（详见 `references/07-AI开发规范.md`）。

**违规等级（AI-011）**：P0 循环依赖/破坏存档/污染全局或第三方/绕过安全边界 → 禁止生成或立即修复；P1 未登记 Harmony、完整覆盖 Def、第三方硬引用、UI 改领域状态 → 阻止交付；P2 依赖倒置/缓存无失效/性能证据缺失 → 必须修复或明确风险；P3 命名注释文档互链 → 交付前修正；P4 纯风格 → 可后续优化。**"代码能编译"不能覆盖 P0/P1 架构问题。**

## Gate 体系

结果四态：`PASS` / `PASS WITH EXCEPTION` / `BLOCKED` / `NOT APPLICABLE`（N/A 必须写明原因）。
任一 P0/P1 失败阻止 Release。没有可执行环境时必须诚实标记"未执行"，不得伪造通过。

```text
GATE-ARCH  层级依赖、无循环、Core 不反向依赖、继承/Harmony/全局状态有必要性
GATE-DATA  Def 只放静态、Runtime 不入 Def/UI、Save 有 ID+默认值+版本、缓存可重建
GATE-LOC   DefName/Key/显示文本职责分离、全部 Keyed、fallback 可解释、占位符一致
GATE-UI    UI State 不持持久状态、Theme Token 集中、状态齐全、大列表有降级
GATE-COMP  Core 不直连目标 Mod、缺失可运行、null/版本/初始化有保护、Patch 可回滚
GATE-SAVE  schema 版本可识别、新字段有默认值、旧档可加载、Migration 幂等且可诊断
GATE-TEST  0 错 0 警编译、XML/Def/翻译解析、Vanilla 启动、存读档迁移、失败 Provider 不影响核心
GATE-PERF  热路径无全库扫描/反射/无界分配、UI 重绘有刷新条件、至少一份可复核指标
GATE-REL   版本与兼容声明、Change Summary、Changelog、回滚说明、EXC 在有效期内
GATE-DELTA 变更标注覆盖全部实际改动、与 diff/Scope 一致、无超范围/死代码、Change ID 可互链（验收定位门）
```

完整检查项与 Gate 记录模板见 `references/00-统一验收标准.md`；可直接复制的清单见 `assets/gate-checklist.md`。

## 最小执行清单

```text
[ ] 已识别任务层级和规则 ID
[ ] 已核对允许/禁止依赖
[ ] 已区分 Def / Runtime / Save / UI 数据
[ ] 已处理 Localization 与 fallback
[ ] 已评估 Compatibility / Harmony / Hardcoding
[ ] 已补测试、性能证据和 Migration 结论
[ ] 已通过适用 Gate
[ ] 已按「skill 产出验收步骤」核对本轮各 skill 产出
[ ] 已生成 Change Summary 并更新 Release 文档
```

## 交付模板

每次交付必须输出 Change Summary，模板见 `assets/change-summary-template.md`：
`Implementation Summary`（Goal / Changed files / Rule IDs / Architecture / Data / Localization / Compatibility / Performance / Migration / Exceptions）
+ `Gate Results`（Gate / Result / Evidence 表）+ `Remaining Risks`。

## 资源

| 需要什么 | 去哪 |
|---|---|
| 可复制的 Gate 检查清单 | `assets/gate-checklist.md` |
| Change Summary 交付模板 | `assets/change-summary-template.md` |
| 例外登记 EXC 模板 | `assets/exc-template.md` |
| 架构决策记录 ADR 模板 | `assets/adr-template.md` |
| 变更标注（开发者向精确改动清单） | `assets/change-annotation-template.md` |
| 从项目文档回同步本 skill（含规则引用 lint） | `scripts/sync_from_docs.ps1` |
| **GATE-DELTA 自动校验（标注↔git diff）** | `scripts/gate-runner.ps1` |
| AI 任务入口速查卡 | `assets/cheat-sheet.md` |
| 提交/PR 前 CI 检查清单 | `assets/ci-checklist.md` |

### GATE-DELTA 基线与范围（2026-09-18 修，实测踩坑）

`gate-runner.ps1` 校验「变更标注 ⟷ git 实际改动」互含（A⊆B 且 B⊆A）。三条硬规则：

1. **`-Base` 默认 `origin/main`，但仓库有未推送 commit 时必须显式传 `-Base HEAD`。**
   该默认值假设「已推送」。若本地 `HEAD` 领先 `origin/main`（常见：最后一个 commit 未推），
   `git diff origin/main` 会把**那一个 commit 的全部内容 + 全部历史未提交工作**都算成本次改动，
   于是标注永远「遗漏」数百文件。诊断口诀：`git rev-parse HEAD` vs `git rev-parse origin/main`，
   不相等就必须换基线。

2. **仓库存在大量历史未提交工作时，用 `-Exclude` / `-ExcludeFromFile` 声明范围外。**
   严格互含校验无法表达「预先存在的无关改动」；强行让标注覆盖全部历史改动 = 让标注失真。
   `-ExcludeFromFile` 接收每行一条的路径清单（支持 `#` 注释），适合 800+ 条的场景。
   生成清单时**务必排除本次变更文件**，并把「标注声明数 = 改动集命中数」作为自检断言。

3. **中文路径必须靠脚本内 UTF-8 解码才能匹配**（已于 2026-09-18 修复）。
   仅给 git 加 `-c core.quotePath=false` 不够：PowerShell 捕获外部进程 stdout 时按系统 ANSI
   代码页（简中 Windows = GBK/936）解码，会把 UTF-8 中文路径整体弄乱 ⇒ 中文路径文件
   **双向失配**（既误报 missing 又误报 phantom）。脚本现已在取 git 输出期间临时把
   `[Console]::OutputEncoding` / `InputEncoding` / `$OutputEncoding` 设为 UTF-8。
   ⚠ 若自行改脚本，别再退回 `-c core.quotePath=false` 单点方案。

4. **残留扫描不扫 `.md`**：变更标注/设计文档会「引用」`Console.WriteLine` 等 token
   （例："残留扫描结论：未见 Console.WriteLine"），扫 `.md` 必然自指假阳性。
   文档残留改由人工复核。

> 落地样例：`-Base HEAD -ExcludeFromFile .codebuddy/tmp/mvs-out-of-scope.txt -Blocking`
> ⇒ `PASS — 标注覆盖 33 个改动文件，无遗漏/超范围/残留`（EXITCODE=0）。

## 与相邻 skill 的分工

- **RimWorld模组开发知识库**：RimWorld 通用 API 事实（字段名、原版机制、XML/Harmony 写法）。本 skill 不管 API 细节。
- **RimWorld组件绘制**：组件怎么画对、画活 —— 度量与行高、原子绘制契约（反编译实测）、交互状态与命中检测、单元矩形排版、绘制事故排错表。本 skill 管「结构与门禁」，不管「一笔怎么画」。
- **ai-bug-engineering**：缺陷生命周期。本 skill 管架构合规与交付门禁。
- **工作记忆 MEMORY.md**：本工程已验证的具体教训与决策；本 skill 是跨项目规范。

## skill 产出验收步骤（逐 skill）

每个与本工程强绑定的 skill 交付产出时，按下表逐条验收；任一条不满足不得标记通过（GOV-012），未执行必须如实标注、不得伪造。

| skill | 产出物 | 验收步骤（判定通过的条件） |
|---|---|---|
| `RimWorld工程架构规范` | Change Summary + Gate Results + 变更标注 | ① Gate Results 四态齐全（PASS / PASS WITH EXCEPTION / BLOCKED / N/A 附原因），P0/P1 无未决项；② 变更标注过 `scripts/gate-runner.ps1 -Blocking` 且 EXITCODE=0（标注 ⟷ diff 互含）；③ 触及偏离时 EXC 已登记且在有效期；④ 无伪造 PASS |
| `RimWorld模组开发知识库` | 基于 API/Def 事实的结论 | ① 每条 API / 字段 / 机制结论可溯源（分册名 + 章节，或程序集核实记录）；② StatDef / 字段名与 `总览数据面板_属性字段清单.md` 或程序集一致，无凭记忆臆造；③ 版本存疑处写明核实方法与目标 Build |
| `RimWorld组件绘制` | 游戏内 UI 组件 / 界面 | ① 落地前四项取证齐（配色来源 / 数值语义 / 内外几何 / 组件自画内容），无假演示；② 预览与实现 1:1（逐元素对照可复现）；③ 交互件六态齐全，无反劣质 12 条黑名单项；④ 绘制事故清单（画歪 / 文字遮盖硬切 / 点不动 / 元素重叠）逐条排除 |
| `RimWorld程序集API静态核实` | API 事实核实结论 | ① 结论附证据（成员签名 / IL 引用点 / 子类或实现枚举）；② 自检正负样本通过（`subs RimWorld.StatPart` = 70 行、`refs PriceFactorBuy_Global` = 空）；③ 「存在」与「被消费」分开陈述，死钩子显式标注 |
| `ai-bug-engineering` | Bug 记录（复现 / 根因 / 修复 / 验证） | ① 根因附 Evidence 编号，未证实假设标 UNVERIFIED；② 修复后原始复现步骤重跑通过 + 相关测试 / 回归通过；③ 未执行的验证写 NOT RUN，不得写「已修复」；④ FIXED 与 CLOSED 分列，关闭满足门禁 |
| `summarize` | A 类变更总结 / B 类工程报告 | ① 以实际 diff 为准，文件清单与 git 一致；② A 类四要素齐（What changed / Before → After / Impact / Verification，见 `references/A-small-change.md`），B 类按 `references/B-engineering-report.md` 全要素输出；③ Verification 为真实执行记录，无编造 |
| `change-report` | 修改报告（文件 / 函数 / hunk 级） | ① 清单可与 `git diff` 逐项对上（抽查 hunk 行号不漂移）；② 每处改动附动机；③ 给出影响面 / 回归检查结论（模板见 `references/A-change-receipt.md` / `references/B-change-report.md`） |

> agent 集群（`pc-*`）的验收不走本表：见 `assets/gate-checklist.md` 与 `references/00-统一验收标准.md`。

## 源文档

规范源文件（人类可读主副本）位于 PersonalChronicle 工程 `docs/设计文档/架构/`。
本 skill 内 `references/` 是其**顶层 `.md`** 的逐文件副本：顶层出现 `Gate-Record-*.md` / `EXC-*.md` 时由脚本自动归档进 `references/history-archive/`，随后逐个复制并做 SHA256 校验（不一致退出码 2）。
源目录的 `工程约定/`（构建与发布路径约定、开发手册）属项目专属内容，**有意不同步**以控制 skill 体积，需要时直接读工程内文件。
在工程中改规范后运行 `scripts/sync_from_docs.ps1 -Source <源目录>` 回同步。
