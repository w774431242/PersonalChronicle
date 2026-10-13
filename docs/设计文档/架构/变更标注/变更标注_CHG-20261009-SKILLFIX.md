---
change-id: CHG-20261009-SKILLFIX
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 集群升级（按外部评估报告修复 P0 错误放行项）
scope: doc-guard 三处门禁缺陷 + 两个报告模板预填 + 分级冲突 + doc-standard/memory-standard 文本漂移；不改 agent 契约与注册表
base: HEAD
source: 外部评估报告 `skills评估与优化方向.md`（Codex，2026-10-09）
files: []
---

# 变更标注 · CHG-20261009-SKILLFIX

> 全部改动位于 git 忽略区（`.codebuddy/**`）⇒ `files: []`，以 §1 登记 + §2 实测证据覆盖（同既有纪律）。
> 评估报告共列 13 项工具层问题 + 8 条文字漂移；**记忆类（M1–M7）已在 `CHG-20261009-MEM-V2` 中解决**（v2 已实现真实日期解析 / 审核状态枚举与目录绑定 / 重复字段检测 / 长期禁有效期 / 检索自动排除），本变更处理**其余 P0**。

## 1. 修复清单（P0，「错误放行」类）

| # | 报告项 | 修复 | 文件 |
|---|---|---|---|
| 1 | **D6 存量检索漏报**：`Escape + SimpleMatch` 组合导致含点符号名（如 `Provider.Contract`）永不命中 | 去掉 `Escape`（`-SimpleMatch` 本身即字面匹配） | `.codebuddy/audit/doc-guard.ps1` |
| 2 | **D2 审批时间非证据**：`approved: NOT_A_TIMESTAMP` 被接受 | `approved` 增加**格式校验**（`YYYY-MM-DD HH:MM`）+ **真实时间**校验（`TryParseExact`） | 同上 |
| 3 | **D1 新增伪装 update**：新增文件写 `status: update` 且无 approved 仍 PASS | 资格与自报一致性检查：本循环只处理"相对基线的新增文件"⇒ `status=update` 一律 BLOCKED（新增件必须 `new/supersede` + `approved`） | 同上 |
| 4 | 模板**预填"已验证"**（无环境也显示成功） | summarize B §13、change-report B §7 的编译/运行/单测改为「未执行」，并加"**默认未执行（禁止预填）**"警示 | `skills/summarize/references/B-engineering-report.md`、`skills/change-report/references/B-change-report.md` |
| 5 | **A/B 分级冲突**：A 模板列「API 小改动」与主入口"动核心 API 按大体量"矛盾 | 删去该示例，改为显式"公共 API/存档/依赖边界变化 ⇒ 一律升级 B" | `skills/summarize/references/A-small-change.md` |
| 6 | **doc-standard 文本漂移 ×6**：① 「六字段」实为 5；② 基线称"路径+哈希"实为仅路径集合；③ 流程块"六字段"；④ EXITCODE=0 判据（沙箱不可观测）；⑤ 索引铁律与默认 WARN 不等强；⑥ "审批前不得写任何文件" vs `pending` 自相矛盾 | 逐条校正（字段数与门禁对齐；基线表述；EXITCODE 改"以输出判读"；索引违规三级处置"`-Strict` / 人工裁决 / 交付说明登记"；明确 `doc-journal/pending` 属留痕、非正式文档） | `skills/doc-standard/SKILL.md` |
| 7 | `scaffold_day.py` **无 `--root`**（文本却统一要求 `--root`） | 适配节澄清：该脚本用**位置参数** `.codebuddy/memory-journal <日期>` | `skills/memory-standard/SKILL.md` |

## 2. 验证证据（本机实测）

| 项 | 命令 | 结果 |
|---|---|---|
| D6 修复 | `doc-guard.ps1 -Find 'doc-guard.ps1'` | **内容命中 28 条**（修复前该含点模式必 0） |
| D6 对照 | `doc-guard.ps1 -Find 'Provider.Contract'` | 0 条 —— 经独立检索确认该文本**在本工程不存在**（报告样例来自其隔离探针）⇒ 0 命中正确，非残留缺陷 |
| 门禁回归 | `doc-guard.ps1 -Blocking` | PASS（新增 47 走豁免 / BLOCKED 0 / WARN 0） |
| 集群自检 | `verify-cluster.ps1 -Blocking` | PASS（audit 7/7、Gate 10/10、skill 9/9、路由 22/22） |

## 3. 未处理项（P1/P2，需单独批准）

**P1（能力扩展，中等改动）**
- **sync lint 定义集**（报告 S2：把"引用"当"定义"）：需**先试跑**，否则可能令现有规范全量 dangling ⇒ 建议单独一轮（含回退方案）。
- **doc-guard C7**：本轮**修改**的基线内文件也要检查（报告 D5/D7；需哈希基线，当前基线仅路径）。
- **audit-subagents skill 来源可用性**（报告 S1：skill 覆盖来自声明，不校验文件存在/`name` 一致 ⇒ 建议拆分"声明覆盖 / 来源可用性 / 行为验证"三项报告）。
- **DELTA 增强**：Scope 边界 + 机器段↔正文一致性 + schema 检查（报告 C1；需改外部 `gate-runner.ps1`）。
- **memory 看板状态语义**（报告 M7：committed 中待审显示为已通过、过期仍收集）——需核验 v2 `visualize_memory.py` 是否已修，再定。

**P2（架构级，需设计）**
- `change-context.json` 共享输入（summarize/change-report 同源渲染）+ 任务 dirty 起点快照。
- 生命周期触发落到 orchestrator FINALIZE 步骤（报告：AUTO-TRIGGER 只是意图声明）。
- 版本治理（4 skill 增版本/依赖清单）与模板压缩（A 类 5–8 行；"无问题"改"已检查范围内未发现问题"）。

## 4. 边界声明（照实登记）

- 本变更**不改变**任何 Gate 归属、agent 六行契约与 skill 注册表（`verify-cluster` 可证）。
- 报告的行为层结论（自动触发率、审批遵循率）**本轮未做模型行为评测**，不得据本变更宣称改善。
- `doc-guard` 仍**只拦新增**（D5/D7 的"既有文件修改合规"需 C7，未实现）——doc-standard SKILL 已如实收窄表述。

## 5. DELTA 终检

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-SKILLFIX.md -ExcludeFromFile .codebuddy\tmp\skillfix-out-of-scope.txt -Blocking
（结果见 §6 回填）
```

## 6. 回填

```text
⇒ GATE-DELTA: PASS — 标注覆盖 0 个改动文件，无遗漏/超范围/残留。（范围外声明 2202+ 不参与判定）
```

**`files: []` 说明**：本轮全部改动位于 git 忽略区（`.codebuddy/audit/doc-guard.ps1` + 4 个 skill 的 SKILL/references），无 git 可见改动 ⇒ 以 §1 登记 + §2 实测覆盖。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09；问题来源：Codex 评估报告 `skills评估与优化方向.md`。
