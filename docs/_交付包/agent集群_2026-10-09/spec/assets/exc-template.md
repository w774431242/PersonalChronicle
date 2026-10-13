# 例外登记模板（EXC-001）

例外是有范围、有理由、有期限的**风险承诺**，不是规则豁免。
任何偏离"必须/不得"的实现，在实现或合并前必须登记。AI 不得以"功能必须实现"为理由跳过登记。
P0 平台/安全规则不得通过普通 Exception 永久豁免。

```yaml
id: EXC-YYYY-NNN
status: Proposed | Approved | Expired | Rejected
level: E0 | E1 | E2 | E3
rule_ids: [GOV-007, COMP-004]
scope: <模块/文件/目标 Mod>
reason: <为什么默认方案不可行>
alternatives_rejected: <至少一个替代方案及拒绝原因>
risk: <兼容/Save/性能/安全风险>
mitigation: <降低风险的措施>
owner: <负责人>
approver: <批准人>
expires: YYYY-MM-DD
review_trigger: <游戏版本/API/目标 Mod 变化时复审>
```

## 例外等级与批准要求

| 等级 | 典型偏离 | 批准要求 |
|---|---|---|
| E0 | 文案、排版、示例风格偏离 P3/P4 建议 | 代码 Review 记录 |
| E1 | 临时兼容、非热路径硬编码、旧 API 过渡 | 模块负责人 + 到期日 |
| E2 | Harmony、跨模块边界、第三方内部类型、Save 迁移特殊处理 | Architecture Review + 回归证据 |
| E3 | 影响 P0/P1、破坏存档、全局 Patch、不可回滚行为 | 默认拒绝；仅项目负责人批准的紧急变更可执行 |

## 专项例外卡

### EXC-002 Harmony 例外 — 必须证明

```text
[ ] 原生 XML/Def/DefModExtension/Comp/GameComponent/API/事件无法完成
[ ] 目标方法和签名可稳定解析
[ ] Patch 只影响必要范围
[ ] Prefix 不会无理由阻止原方法
[ ] 失败时有降级和诊断
[ ] 有 Vanilla/目标 Mod 回归证据
```

### EXC-003 第三方内部访问例外

仅在目标 Mod 无稳定 API、无法通过数据或能力探测完成、且兼容收益明确时允许。
必须使用 Adapter，禁止把第三方类型扩散到 Core、Save 或公共 API。

### EXC-004 硬编码例外

临时硬编码必须同时具备：命名常量、来源注释、影响范围、替换计划、到期日。
禁止把 DefName、Translation Key、路径或目标 Mod 名称散落在多个文件中。

## Markdown 记录模板

```markdown
## EXC-YYYY-NNN — <标题>
- Status: Proposed/Approved/Expired/Rejected
- Level: E0/E1/E2/E3
- Rule IDs: GOV-xxx, ARC-xxx
- Scope: <明确范围>
- Reason: <必要性>
- Alternatives rejected: <替代方案>
- Risk: <风险>
- Mitigation: <缓解措施>
- Owner / Approver: <人员>
- Expires: YYYY-MM-DD
- Review trigger: <触发条件>
- Evidence: <测试/日志/评审记录>
```

## 生命周期

```text
发现必要性 → 记录方案与替代方案 → 风险评估与批准 → 限范围实现
→ 验证与发布标注 → 到期复审
   ├── 转为正式规则（更新规范版本）
   ├── 延期（重新证明必要性）
   └── 移除（清理兼容代码）
```

E2 以上例外必须出现在 Changelog 或 Release Note。
