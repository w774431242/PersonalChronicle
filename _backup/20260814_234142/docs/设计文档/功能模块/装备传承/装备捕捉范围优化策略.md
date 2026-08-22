# 装备捕捉范围优化（数据驱动）· v4.9.1

**日期**：2026-08-10
**状态**：已落地并编译通过，已同步到发布版本 `Mods\PersonalChronicleVer0.1`。

---

## 问题
`Overview › Thing（装备）`类别原先硬编码 `def.IsWeapon || def.IsApparel`，把**所有可穿戴物**都收进档案，包括无战斗价值的非战斗服装（防尘衣/工作服/时尚衣等），污染装备类别统计与侧栏。

## 方案（数据驱动 Def）
仿照 `SocialRelationPolicyDef`，新增：
- `Defs/ThingArchivePolicy.xml` — `PersonalChronicleThingArchivePolicy`
- `Domain/ThingArchivePolicyDef.cs` — 策略定义（含 `Captures(ThingDef)`）
- `Domain/ThingArchivePolicy.cs` — 静态解析入口（懒加载 + 缓存 + Def 缺失回退），镜像 `SocialRelationFilter`

**判定规则**：
- 武器：**始终捕捉**（除非进 `excludeApparelDefNames` 黑名单）。
- 服装：`excludeNonCombatApparel=true` 时，仅当 **Sharp/Blunt 最高单项 ≥ `minCombatArmorForApparel`(0.20)** 才捕捉。防尘衣(≈0.12)、皮夹克(≈0.12)排除；防弹背心(≈0.36)、复合/动力甲保留。Heat 护甲单独不算。

## 改动点
| 文件 | 改动 |
|---|---|
| `Domain/ThingArchivePolicyDef.cs` | 新增（Def + 判定） |
| `Domain/ThingArchivePolicy.cs` | 新增（解析入口） |
| `Defs/ThingArchivePolicy.xml` | 新增（默认配置，阈值可 PatchOperation 调） |
| `Application/ArchiveService.cs` | `IsEquipableDef` 改用 `ThingArchivePolicy.Captures` |
| `Capture/Patch_ThingDestroy.cs` | 退役捕获范围改用同一 policy（保持捕捉/退役范围一致） |

## 影响
- **单点收窄**：所有 Thing 类别来源（捕捉 → 侧栏/统计/详情）都走 `RegisterThingObject`，收窄 `IsEquipableDef` 即全链路一致，UI 侧无需改动。
- **向后兼容**：新增 Def，不破坏存档；已捕捉的旧 ThingObject 不受影响（仅新捕捉按新规则）。
- **可调优**：阈值/黑名单全在 XML，第三方可 PatchOperation 覆盖。

## 验证
- 编译 `0 警告 0 错误`。
- 判定抽查：防尘衣排除 ✓ / 防弹背心保留 ✓ / 武器恒捕捉 ✓。
