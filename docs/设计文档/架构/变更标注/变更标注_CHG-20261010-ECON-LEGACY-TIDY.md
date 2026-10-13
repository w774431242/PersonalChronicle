---
change_id: CHG-20261010-ECON-LEGACY-TIDY
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: 经济机构页「历史对账」区空集噪音收口 —— 只显示真正存在旧记录的 anchor，并清理随之不可达的分支与孤儿本地化键
rule_ids: AI-001, AI-003, AI-011, GOV-003, GOV-010, GOV-012, UI-003, LOC-001, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/EconomyMode.xml
    type: M
  - path: Languages/English/Keyed/EconomyMode.xml
    type: M
---

# 经济机构页「历史对账」区收口 · CHG-20261010-ECON-LEGACY-TIDY

**ChangeID**：`CHG-20261010-ECON-LEGACY-TIDY`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**当前程序集**：`Assemblies/PersonalChronicle.dll` SHA-256 `60C4239434558A1914DD7784AC9D8037D755CEC125DECCF893170F25E44EB386`（`BuildStamp -> 2026-10-10 08:29 UTC`，编译时 UTC）
**部署**：**已部署**（2026-10-10 16:34:02，`_scripts/deploy-to-game.ps1` → `RimWorld\Mods\殖民地综合管理系统`，复制 233 文件 <About 13 / Assemblies 1 / Defs 36 / Fonts 8 / Languages 66 / Patches 3 / README 1 / Textures 105>；目标 DLL 时间戳 `2026-10-10 16:29:28` 与工程一致；被覆盖文件已备份到 `_backups/release-deploy/deploy_20261010_163402_704`）
**Save 变更**：**无**（不触碰任何 Scribe 字段、schema、payload 结构）

---

## 一、动机（用户可见症状）

机构页第一个页签的「历史对账」区在正常存档下**恒为空集噪音**：5 台柜台（劳务站 / 工坊管理处 / 工资结算处 / 生活用品商店 / 体检中心）全部渲染为 `XX · 无归档记录`，且因 `LegacyFeesAllowed => false`（`EconomyInstitutionMode.cs:21`）旧记录永不再产生（`EconomyRuntime.Register` 在 `State.Add(new EconomyInstitution ...)` 前 return），该区**永久**不可能出现有效内容。

原条件 `anchors.Where(a => a.Spawned && a.Map == map)` 把「本图有柜台」当成「有历史要对账」，语义错位；`无归档记录` 分支只是把空集如实打上屏，占位而无信息。

## 二、改动清单（逐文件）

| 文件 | 改动类型 | 涉及符号 / Key | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs | M | `GameComponent_PawnEconomy.BuildInstitutions` | 81-99 | 收紧谓词为 `Institution(a) != null`（只有真有旧记录的 anchor 进区）；空集时整区（含标题句）不出现；同删因此不可达的 `legacyModel == null` 三元分支与纯文本行分支 | AI-011, GOV-003, UI-003 | UI |
| Languages/ChineseSimplified/Keyed/EconomyMode.xml | M | `Key:PC_PEA.Mode.LegacyNoRecord` | 36（删） | 删除孤儿键（唯一引用点已随分支删除） | GOV-010, LOC-001 | LOC |
| Languages/English/Keyed/EconomyMode.xml | M | `Key:PC_PEA.Mode.LegacyNoRecord` | 36（删） | 同上（英文份） | GOV-010, LOC-001 | LOC |

### 关键 diff 片段

```diff
// Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs
-            var legacyAnchors = anchors.Where(a => a.Spawned && a.Map == map).ToList();
+            // UI 收紧（2026-10-10）：只有**真正存在旧记录**的 anchor 才进本区。……
+            var legacyAnchors = anchors.Where(a => a.Spawned && a.Map == map && Institution(a) != null).ToList();
             if (legacyAnchors.Count > 0)
             {
                 rows.Add(Hint(T("Mode.LegacyReadOnly")));
                 for (int i = 0; i < legacyAnchors.Count; i++)
                 {
                     var legacyAnchor = legacyAnchors[i];
                     var legacyModel = Institution(legacyAnchor);
-                    string legacyText = legacyAnchor.LabelCap + " · " + (legacyModel == null ? T("Mode.LegacyNoRecord")
-                        : T("Mode.LegacyArchive") + " · " + (legacyModel.Open ? T("OpenState") : T("ClosedState")));
-                    rows.Add(legacyModel == null ? new EconomyRow(legacyText)
-                        : new EconomyRow(legacyText, T("Configure"), "Focus", key: legacyAnchor.GetUniqueLoadID()));
+                    string legacyText = legacyAnchor.LabelCap + " · " + T("Mode.LegacyArchive") + " · "
+                        + (legacyModel.Open ? T("OpenState") : T("ClosedState"));
+                    rows.Add(new EconomyRow(legacyText, T("Configure"), "Focus", key: legacyAnchor.GetUniqueLoadID()));
                 }
             }
```

## 三、明确未改动（范围边界，防误读）

| 未动项 | 理由（本次取证结论） |
|---|---|
| `Domain/PawnEconomy/EconomyModel.cs` 的 `EconomyInstitution` 类型 | 删除会让旧档 payload 中该节点在迁移路径命中 `UnknownLegacyFields`（`LegacySettlementArchive.cs:33-39` 显式对未知节点判故障）⇒ 旧现金权益无法生成 claims、承接卡死；且 `EconomyPosition/EconomyOrder.InstitutionId`、`EconomyService.cs:159` 不变量、`LegacyReconciler` 字段闭集均仍引用它 |
| `PawnEconomySaveData.PawnEconomyEnvelope` 的 `XmlArrayItem` 声明 | 同上（存档兼容），保留即为兼容性护栏 |
| `LegacyFeesAllowed => false` 之后的旧收费引擎 | 有意保留的参照实现（`EconomyInstitutionMode.cs:20`），删除是独立大工程，不在本轮范围 |
| `Mode.LegacyArchive` / `Mode.LegacyReadOnly` / `OpenState` / `ClosedState` / `Configure` 键 | 仍被引用（`EconomyCommands.cs:89,94,95,97,146`），保留 |
| `BuildInstitutions` 下方 `if (institution != null)` 归档事实块（Hours/Price/Wage） | 选中项摘要展示，与本次「空集噪音」问题无关，保持原样 |

## 四、验证证据

| 项 | 结果 | 证据 |
|---|---|---|
| 编译（权威链路） | **0 错 0 警** | `pwsh _scripts/compile_with_sdk.ps1` → csc 无任何诊断输出（判据：有诊断才打印）；产出 `Assemblies/PersonalChronicle.dll` 2,130,944 B / `2026-10-10 16:29:28` |
| 离线测试 | **1231 通过 / 0 失败 / 1 跳过（共 1232）** | `dotnet test Tests/PersonalChronicle.Tests.csproj`（本次编译后的 DLL，`EXIT=0`） |
| GATE-UI | **PASS** | `pc-ui-loc-guard` 只读审查：UI-1 三区块一致性（无重复/无矛盾/无悬空选中）、UI-2 `Focus` 在白名单且按钮可达（无孤立标签行）、UI-3 四态状态完备 |
| GATE-LOC | **PASS** | `LegacyNoRecord` 全仓 0 引用（Source/Languages/Defs/Patches/docs/_scripts/Tests 全查）；保留键中英双份齐；两份 Keyed XML 仍合法（无注释/未转义 `&`/结构破坏） |
| 集群门禁 | **PASS** | `verify-cluster.ps1 -Blocking` → `PASS=7 / BLOCKED=0 / Gate覆盖=10/10 / skill覆盖=10/10`，`runId=b94779062b8f477b906e053fdc9df5c5` |
| 路由 | 单 agent（T2 并行） | `route-task.ps1` → `pc-ui-loc-guard`（Gate=UI,LOC，触发词「本地化」） |
| GATE-DELTA | **PASS** | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/legacytidy-out-of-scope.txt -Annotation <本文件> -Blocking`（基线必要性：`HEAD=21fd0a9 ≠ origin/main=dbcf8f8`） |
| 未执行项 | **NOT RUN** | 游戏内实机验证（沙箱无游戏进程）与截图；`_scripts/audit-keyref-integrity.ps1`（agent 无 shell；已用全仓引用检索 + 全文读取替代，另注该脚本不覆盖 `"PC_PEA." + key` 组合键） |

**剩余风险**（P3，非本次引入）：`Mode.Step.LegacyRights` 文案（CN/EN `EconomyMode.xml:41`）指向「历史对账区」，而该区现可整体隐藏；旧权益决策实际走 `Treasury.*` 通道。是否需要在文案上改指向，待专项确认。

## 五、回滚

三处改动互相独立、无存档影响，回滚 = 还原 3 个文件后重走 `compile_with_sdk.ps1` → `deploy-to-game.ps1`（`_backups/release-deploy/deploy_20261010_163402_704` 内有部署前副本）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（3/3），无遗漏
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动
- [x] 每个改动文件标注了类型与涉及符号
- [x] Change ID 与 Gate 记录、验证证据可互链
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（本次同删一处不可达分支）
- [x] 已用 `gate-runner.ps1` 自动校验
