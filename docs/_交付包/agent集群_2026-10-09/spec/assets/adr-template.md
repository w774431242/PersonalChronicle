# 架构决策记录（ADR）模板

用于记录"为什么这么做"，与 Gate 记录互补：Gate 记录**是否符合规范**，ADR 记录**取舍过程**。
扩展点选型（Def / DefModExtension / Comp / Service / Provider / Patch / Harmony）默认必须留 ADR。

```markdown
# ADR-YYYY-NNN — <决策标题>

- Status: Proposed | Accepted | Deprecated | Superseded by ADR-YYYY-MMM
- Date: YYYY-MM-DD
- Deciders: <人员/AI>
- Rule IDs: <GOV/ARC/DATA/COMP-xxx>
- Related Gates: <GATE-ARCH / GATE-COMP / ...>

## Context
<问题背景、约束、涉及层与模块、第三方 Mod/DLC 影响>

## Decision
<选定的方案，一句话可概括>

## Options Considered
| 方案 | 风险等级 | 优点 | 缺点 | 结论 |
|---|---|---|---|---|
| 原生 Def/XML | 低 | | | |
| DefModExtension | 低 | | | |
| Comp / GameComponent | 中 | | | |
| Service / Provider | 中 | | | |
| PatchOperations | 中 | | | |
| Harmony Postfix | 高 | | | |
| Harmony Prefix | 高 | | | |
| Harmony Transpiler | 最高 | | | |

## Consequences
- 正面：<...>
- 负面：<...>
- 需要的补偿措施：<缓存失效 / 降级路径 / Migration / EXC>

## Compliance
- 是否触发 Architecture Review：<Yes/No + 触发项>
- 是否需要例外登记：<EXC ID 或无>
- 验证方式：<测试/日志/实机步骤>
```

## 风险升级顺序（AI-003）

```text
原生 Def/XML → DefModExtension → Comp/GameComponent → Service/Contract/Provider
→ PatchOperations → Harmony Postfix → Harmony Prefix → Harmony Transpiler
```

不是"越后越强"，而是风险升级。必须记录被跳过方案及其原因；不得因"实现简单"直接选 Harmony。

## 触发 Architecture Review 的四种情况

```text
Harmony = Yes
Hardcoding = Yes
Cross-Mod Direct Reference = Yes
Static Mutable State = Yes
```
