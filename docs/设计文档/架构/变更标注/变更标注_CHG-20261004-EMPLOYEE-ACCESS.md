# 变更标注 · CHG-20261004-EMPLOYEE-ACCESS

**Change ID**：CHG-20261004-EMPLOYEE-ACCESS（员工忽略「无法从事」+ 岗位页文案修正 + 技能翻译孤儿键清理）
**日期**：2026-10-04
**宿主**：RimWorld 1.6.4871（rev590）
**关联**：EXC-2026-025（新增 Harmony）、EXC-2026-016（车间接管红线）、CHG-20261004-WSFIX

---

## 触发（玩家反馈，三条）

1. 「成为员工后，我需要忽略【无法从事】」——角色卡显示"无法从事：艺术工作、烹饪工作"。
2. 「为什么车间管理界面-岗位显示尚未实现？进行审计」。
3. 「检查游戏日志」时发现 `Translation data for Simplified Chinese has 11 errors`。

---

## 一、员工忽略「无法从事」（EXC-2026-025）

### 根因（IL 实证）

```csharp
// Verse.Pawn::WorkTypeIsDisabled(WorkTypeDef w)  —— 全文只有一行
public bool WorkTypeIsDisabled(WorkTypeDef w) => GetDisabledWorkTypes(false).Contains(w);
```

该判定有 **62 个消费点**（Cecil `refs` 全量），其中三个必须同时放行，否则会出现
"界面允许但实际不干"或反之：

| 消费点 | 不放行的后果 |
|---|---|
| `JobGiver_Work.PawnCanUseWorkGiver` | 档位勾上了但不派 Job |
| `Pawn_WorkSettings.SetPriority` | 写非 0 档时 `Log.Error` 并 return |
| `WidgetsWork.DrawWorkBoxFor` | 工作面板仍画成"无法从事" |

### 实现

`Capture/Patch_Pawn_WorkTypeIsDisabled.cs`（Prefix）+ 
`MapComponent_WorkshopManager.EmployeeAccessAllows(Pawn, WorkTypeDef)`（判定）。

**两道短路**（影响面控制）：① 先问原版 `GetDisabledWorkTypes(false)` —— 没禁用则直接放行原方法
（绝大多数调用）；② 确实被禁用时，才问车间授权，条件三者**同时**成立：

* pawn 是某车间的**在编成员**（人员页 `zone.Members`）；
* 该车间**已指定类型**（`zone.WorkshopType != null`）；
* 该类型确实是该车间类型的**映射类型**（Production/Maintenance/QC/Standby 四类的并集）。

**可逆**：无额外开关，判定现算不缓存 ⇒ 把人移出车间名单立即恢复原版限制。
**fail-open**：任一前提不成立即交还原版。

## 二、岗位页「尚未实现」审计结论（**更正此前的判断**）

```csharp
// WorkshopManageView.Posts.cs:390
string tag = row.Delivers ? T("PostDelivers") : T("PostNotDelivers");
// row.Delivers = def.deliversOwnService   (:98)
```

* 显示的是 **`deliversOwnService`**（不是此前说的 `implementedWorkTypeDefNames`）。
* 该字段的源码注释写明：它是 **Phase 4「能力对等闸门」的唯一判定依据**，
  「**不得为'看起来完整'而虚填 true** —— 虚填会直接导致 Phase 4 禁用原版工作后，
  殖民地失去该服务且无人替代」。
* ⇒ 它是**设计状态、不是故障**，且 **不可通过填 true 消除**（红线）。
  **它与"员工能否工作"完全无关**（员工开工取决于车间类型 + 原版「手动优先级」）。

**本次修改**：只改文案与说明（不改字段值）——
`PostNotDelivers`「尚未实现」→「**规划中·未接管**」（英文 `not implemented yet` →
`Planned - no takeover yet`）；`PostsNoticePhase1` 追加一句明确
"该标记与员工能不能干活无关"。

## 三、11 个翻译错误 = 过时机制残留（零文案工作即修复）

* 技能文案的实际机制是 **Keyed**（`Languages/*/Keyed/Archive.xml` 的
  `Professional.Skill.<defName>.Label/.Desc`）——`Defs/ProfessionalSkills.xml` 头注释明确
  「Def 内不写硬编码 label，由 `Languages/Archive.xml` 提供」。
* 核对结果：**48 个技能 Def，中/英 Keyed 缺键均为 0**（全覆盖）。
* 而 `Languages/ChineseSimplified/DefInjected/ProfessionalSkillDef/Skills.xml`
  是**旧机制残留**，其中 11 个键指向已重命名的 Def（`..._AgricultureTillage` /
  `..._MiningOpenpit` / `..._WeaponsMelee` 等）⇒ 触发游戏启动时的 11 条翻译错误。
* **修法**：脚本按 `Defs/**` 的 defName 全集精确剔除孤儿键 —— 删 **22 个**（11 Def × 2 字段），
  保留 **96 个 = 48 Def × 2**（与有效 Def 数精确对应）。**未重写任何翻译**。

## files

```yaml
files:
  - path: Source/PersonalChronicle/Capture/Patch_Pawn_WorkTypeIsDisabled.cs
    type: A
    symbols: Patch_Pawn_WorkTypeIsDisabled.Prefix
    lines: 1-71
  - path: Source/PersonalChronicle/Capture/CapturePatchDiagnostics.cs
    type: M
    symbols: CapturePatchDiagnostics..cctor
    lines: 100-110
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs
    type: M
    symbols: MapComponent_WorkshopManager.EmployeeAccessAllows, MapComponent_WorkshopManager.WorkshopTypeMapsWorkType, MapComponent_WorkshopManager.NamesContainWorkType
    lines: 699-777
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.PostsNoticePhase1, PersonalChronicle.Automation.Tab.PostNotDelivers
    lines: 428, 432
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.PostsNoticePhase1, PersonalChronicle.Automation.Tab.PostNotDelivers
    lines: 425, 429
  - path: Languages/ChineseSimplified/DefInjected/ProfessionalSkillDef/Skills.xml
    type: M
    symbols: (剔除 11 个已重命名 Def 的 22 个孤儿键；保留 48 个有效 Def 的 96 个键)
    lines: 全文
  - path: docs/设计文档/架构/EXC-2026-025.md
    type: A
    symbols: EXC-2026-025
    lines: 1-96
```

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | **已验证** | 0 错 0 警 |
| XML 解析 | **已验证** | 技能 DefInjected 重写后由 `XmlDocument` 正常读写 |
| 孤儿键剔除 | **已验证** | 22 删 / 96 留（= 48 有效 Def × 2） |
| 部署 | ⚠ **被阻塞** | 游戏运行中占用 `PersonalChronicle.dll` |
| 实机行为 | **未验证** | 需重启后确认工作面板不再显示"无法从事" |
| GATE-DELTA | **未跑** | 标注已写，待发版终检 |

## 已知代价（如实声明）

* 经 patch 放行的 pawn 在**其他原版系统**中也被视作"能做该工作类型"（如 bill 的
  「可执行该工作的殖民者」下拉会包含他）。这与玩家意图一致，但确实放宽了原版对背景故事的约束。
* **约束**：`Defs/WorkshopTypes.xml` 的映射表**不得**加入 Hauling / Cleaning / Doctor /
  Firefighter（与 EXC-2026-016 同一条红线），否则本 patch 会绕过车间体系的强制保留清单。
