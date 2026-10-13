---
change_id: CHG-20261002-ORPHANKEYS
version: 历史孤儿键清理专项
date: 2026-10-02
author: pc-orchestrator(AI)
scope: [Languages/]
files:
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: 26 孤儿键删除（逐键零引用确认）
    lines: 3057,3058,3063-3066,3069,3077,3092,3103,3107,3108,3135,3136,3145-3147,3316,3345(行内),3346,3349,3352,3353,3363,3365,3418（原行号）
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: 26 孤儿键删除（两语言同步）
    lines: 3053,3054,3059-3062,3065,3073,3088,3099,3103,3104,3131,3132,3141-3143,3312,3341(行内),3342,3345,3348,3349,3359,3361,3414（原行号）
---

# 变更标注 · CHG-20261002-ORPHANKEYS（历史孤儿键清理）

| 文件 | 改动 | 摘要 | 关联 Gate |
|---|---|---|---|
| ZH/EN Archive.xml | M | 26 键全源码零引用确认后删除（证据见 GR-20261002-ORPHANKEYS-LOC §2 逐键表）；行内混排 Run.Timeline 元素清理（Contract.NavUnavailable 独占行保留）；删除账目 -23 行/语言（+2 新键 −25 删行） | LOC |

### 一致性自检
- [x] 检索覆盖：全键名/半段拼接/无前缀短词/脚本目录四口径；7 条误报逐条鉴别（crew.Name/targetBill.Label 属性访问、Contract.RecipeUsers 在用键、TimelineSegments 常量、UITheme.Timeline 配色）
- [x] 只删清单确认项；清单外疑似孤儿（Crew.TimetableNote/TimelineSegments 常量）登记未动（不扩线）
- [x] 两语言同步（各自 25 行删除 + 1 行内清理）；XML 良构 + dup=0 + 保留键验证
- [x] 部署目录 OrphanLeft=0 抽查
