# 速查卡（AI 任务入口首读）

> 任务入口先读本卡，再按路由表读分册。完整协议见 `references/07-AI开发规范.md`。

## 任务入口（AI-001）必输出
目标/非目标 · 涉及文件 · 任务层级 · 规则 ID · 新增/修改的 Def·Runtime·Save·UI·Key·Asset · 第三方影响 · 是否触发 Architecture Review · 扩展点计划 · Gate 计划。
信息不足用最小安全假设并标记。

## 10 道 Gate（四态：PASS / PASS WITH EXCEPTION / BLOCKED / N/A 须带理由）
ARCH 层级依赖无循环 · DATA Def静态/Save有ID · LOC Keyed+fallback · UI 状态分离 · COMP 缺失可运行 · SAVE 旧档可加载 · TEST 0错0警+Vanilla · PERF 热路径无扫描 · REL 版本/Changelog · **DELTA 标注⊆diff⊆Scope（验收定位）**。

## 扩展点风险升序（AI-003）
原生Def/XML → DefModExtension → Comp/GameComponent → Service/Contract/Provider → PatchOperations → Harmony Postfix → Prefix → Transpiler。
记录被跳过方案及原因；不得因省事直接选 Harmony。

## 禁止生成（AI-004）
第三方条件分支 · 按显示文本判断 · 完整覆写第三方Def · 裸Key/DefName/AssetPath · 无界静态单例 · Tick全库扫描/反射/无界分配 · 空catch/静默失败 · 未登记Harmony · UI/翻译/Texture写进Save。

## 实现顺序（AI-005）
Contract/schema → Domain/Core → Data/Def/Comp → Read Model/Provider → UI/Theme/Asset/Loc → Compatibility Adapter/Patch → 测试诊断。
不得同类中混合 采集/聚合/存档/UI绘制/兼容Patch。

## 触发 Architecture Review
Harmony=Yes / Hardcoding=Yes / Cross-Mod Direct Reference=Yes / Static Mutable State=Yes 任一成立。

## 违规分级（AI-011）
P0 循环依赖/破坏存档/污染全局第三方 → 禁止生成；P1 未登记Harmony/完整覆写Def/第三方硬引用/UI改领域 → 阻止交付；P2 依赖倒置/缓存无失效/性能证据缺 → 必须修复；P3 命名注释互链 → 交付前修正；P4 风格 → 可后续。
"能编译"不能覆盖 P0/P1。

## 交付（AI-012）+ 变更标注（AI-013 / GATE-DELTA）
交付 = Change Summary + Gate Results 表 + Remaining Risks。
**每次交付必须生成变更标注**（开发者向精确改动清单，精确到文件/符号/行），覆盖全部 git 改动、与 Scope 一致、无超范围/死代码，Change ID 贯穿 Intake→Summary→Annotation→Gate→Changelog。
可用 `scripts/gate-runner.ps1 -Annotation <标注.md>` 自动校验。

## 无运行环境降级（见 00-统一验收标准.md §13）
Tier A 有运行环境→全部Gate；Tier B 有编译+单测→TEST/PERF/DELTA 部分；Tier C 仅静态→DELTA+ARCH/DATA。无证据不得 PASS；N/A 必须带理由。
