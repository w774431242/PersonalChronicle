---
change_id: CHG-20261002-UNITTEST-FIX
version: 单测失败诊断修复
date: 2026-10-02
author: pc-orchestrator(AI)
scope: [Source/PersonalChronicle/Domain/Profession/]
files:
  - path: Source/PersonalChronicle/Domain/Profession/ProfessionalEffectResolver.cs
    type: M
    symbols: ResolveContributions.ActiveNow 口径修复（纯常驻 Stat 技能双空上下文保持生效，对齐 ResolveStatBonus 注入真源；hasCraftEffect 追踪；B3 防护保留）
    lines: ~341, ~356-367, ~393-401
---

# 变更标注 · CHG-20261002-UNITTEST-FIX（ResolveContributions_IncludesStatBonusWhenNoWorkSpeed）

| 文件 | 改动 | 摘要 | 关联 Gate |
|---|---|---|---|
| ProfessionalEffectResolver.cs | M | 实现 bug 修复（非改测试）：ActiveNow 新增「纯常驻 Stat 技能 + 双空上下文 ⇒ 生效」分支——修复浮层（ActiveNow=false）与 pawn 面板注入（ResolveStatBonus 双空注入生效）的口径漂移（GOV-006）；B3 对制造效果（WorkSpeed/QualityBias）的门控完整保留 | TEST |

### 一致性自检
- [x] 裁决有完整证据链：三方对照（ResolveStatBonus :146-149 设计注释 / ProfessionalBonusView 浮层消费者 / B3 自检样本定义）+ 语义矩阵逐格复核
- [x] 未弱化语义：B3 防护（有制造效果技能双空/未命中⇒不生效）逐格保持；仅修正测试所捕获的回归格
- [x] 运行级 A/B：旧 dll 复现失败（392/394）→ 新 dll 修复通过（393/394，0 失败）
- [x] 全量 394 = 393 通过 / 0 失败 / 1 跳过（实机项）
