---
change_id: CHG-20261006-EVENTSTIME
version: v1.1.5
date: 2026-10-06
author: AI
scope: [Source, Languages]
files:
  - path: Source/PersonalChronicle/Manufacturing/UI/ManufacturingEventsPanels.cs
    type: M
    symbols: ManufacturingEventsPanels.RelativeTime（分钟/小时/天阶梯的除法基数与单位后缀错位修复 + 后缀 Keyed 化）, ManufacturingEventsPanels.T1（新增单参格式化翻译助手，走 NamedArgument 重载）
    lines: 199-246
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Events.HoursAgo / DaysAgo / YearsAgo（新增 3 键）
    lines: 2648-2651
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Events.HoursAgo / DaysAgo / YearsAgo（新增 3 键）
    lines: 2642-2645
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
    symbols: BuildInfo.BuildTimeUtc（compile_with_sdk.ps1 自动重写的生成物，未跟踪文件）
    lines: 1-6
---

# 变更标注 · CHG-20261006-EVENTSTIME

## 背景

2026-10-06 制造系统中心只读审计（GATE-UI / GATE-LOC）P2-1 项：⑤ 事件与设备健康
（`ManufacturingEventsPanels`）的事件流「相对时间」列**硬编码单位后缀**。

复核源码后发现该处**不止是本地化问题，而是用户可见信息错误**——原实现为：

```csharp
if (delta < GenDate.TicksPerHour)  return T("…Events.Now");                      // 刚刚
if (delta < GenDate.TicksPerDay)   return (delta / GenDate.TicksPerHour) + "m";  // ← 小时数配「分钟」后缀
if (delta < GenDate.TicksPerYear)  return (delta / GenDate.TicksPerDay)  + "h";  // ← 天数配「小时」后缀
return                                    (delta / GenDate.TicksPerYear) + "d";  // ← 年数配「天」后缀
```

三档的**除法基数都比后缀所代表的单位大一档** ⇒ 实际渲染："5 小时前"显示为 `5m`（读作 5 分钟）、
"10 天前"显示为 `10h`（读作 10 小时）。且后缀为裸字面量，中文界面直接出现英文字母单位（LOC-001）。

## 一、修复内容（`Manufacturing/UI/ManufacturingEventsPanels.cs`）

| 项 | 原 | 现 |
|---|---|---|
| 1 小时 ~ 1 天 | `delta / TicksPerHour` + 字面量 `"m"` | `delta / TicksPerHour` + Key `…Events.HoursAgo` ⇒「n 小时前」 |
| 1 天 ~ 1 年 | `delta / TicksPerDay` + 字面量 `"h"` | `delta / TicksPerDay` + Key `…Events.DaysAgo` ⇒「n 天前」 |
| ≥ 1 年 | `delta / TicksPerYear` + 字面量 `"d"` | `delta / TicksPerYear` + Key `…Events.YearsAgo` ⇒「n 年前」 |
| < 1 小时 | `…Events.Now`（未动） | 同（未动） |

- **基数与单位一一对齐**（数值语义修正，主目标）；
- 后缀全部走 Keyed（LOC-001）；**保留原有 4 档结构与 `TicksPerHour` 阈值** ⇒ 档位切换时点与
  `whenWidth = 62f` 等**布局常量均未变**（改动可逆）。⚠ 但**显示文本显著变长**（`5m` ≈ 12px →
  「23 小时前」≈ 51px）⇒ 是否触发 `TruncateToWidth` 截断见 §七-1。（此句原文写作"无布局回归面"，
  与 §七-1 自相矛盾，经独立复核指出后改正。）
- 新增私有助手 `T1(string, int)`，**显式走 `NamedArgument` 重载** —— 传 `object` 会落到已过时的
  `params object[]` 版本（CS0618），系本工程已踩过的坑（对照 `WorkshopLayoutCanvas.Primitives.cs:98-111`
  与 `WorkshopFloorPlan.Rails.cs:485-489` 的同款做法）；
- 类文档注释同步（原注释写作"3m 前 / 5h 前 / 2d 前"，与实现不符，已改写为真实档位并注明本次缺陷）。

## 二、本地化（LOC）

新增 3 键，中英逐键对称：

| Key | 中文 | English |
|---|---|---|
| `PersonalChronicle.UI.Events.HoursAgo` | `{0} 小时前` | `{0}h ago` |
| `PersonalChronicle.UI.Events.DaysAgo` | `{0} 天前` | `{0}d ago` |
| `PersonalChronicle.UI.Events.YearsAgo` | `{0} 年前` | `{0}y ago` |

- 键族与既有 `PersonalChronicle.UI.Events.Now`（未动）同级，归属 ⑤ 事件面板自身命名空间，
  **不跨模块复用** `PersonalChronicle.Automation.Tab.TrendDaysAgo`（同文案但属 Automation 模块，
  复用会造成 Key 归属与改动耦合）；
- 占位符 `{0}` 中英一致；无复数变体需求；不触碰任何第三方 / 原版 Key。

## 三、明确不做（非目标 / 范围外）

1. **P2-2**（app-shell 主路径面板级错误降级）、**P2-3**（底栏页签/内容分发双实现合并）——
   结构性改造，本轮不动，另行裁决；
2. 其余 P3 项（`StateColor` 裸键、制造色板不随主题、DataLayer 空态、Bridge 截断悬停、
   `zone.GetType().Name` 回退、`" A"/" B"` 与 `"build "` 等登记项）—— 本轮不动；
3. 绘制期翻译的缓存机制：本文件既有 `T()`（无参）本就在绘制期直调，本次未引入新的问题类别；
   `T1` 的调用量 = 事件面板可见行数上限（`body.height / RowHeight ≈ 12` 次/帧），与原实现的
   `ToString() + 拼接` 同阶。**未新增缓存**（最小改动原则），已在「残留与风险」登记。

## 四、校验证据

| 项 | 结果 |
|---|---|
| 编译 | `pwsh _scripts/compile_with_sdk.ps1` ⇒ `EXITCODE=0`，`error CS=0`，`warning CS=0`（`/warn:4`） |
| XML 良构 + 新键存在 | 两份 `Archive.xml` 由 `System.Xml.XmlDocument` 加载成功；`HoursAgo/DaysAgo/YearsAgo` 中英各 3 键均命中，无重键 |
| 键位对齐 | 中英各 +3 键，键名逐字相同 |
| 旧键保留 | `PersonalChronicle.UI.Events.Now` 未改动 |

## 五、Gate Results

| Gate | 结论 | 证据 |
|---|---|---|
| ARCH | N/A | 改动限于 UI 层一个 `internal static` 类的两个私有成员；无新增依赖边、无 Harmony、无静态可变状态、无硬编码类目 ⇒ 不触发 Architecture Review |
| DATA | N/A | 无 Def / DefModExtension / Comp / Read Model 契约变更 |
| LOC | PASS（已经独立复核） | 新增 3 键中英对称、占位符一致、无重名（§四）；`Manufacturing/UI/` 全目录穷举确认该处硬编码**已完全消除且无第二处同类**；未复用跨模块键；无孤儿键（仅新增）。独立复核结论与例外项见 §八 |
| UI | PASS（静态）／实机**未执行** | 档位阈值、`whenWidth`、布局常量均未变；`TruncateToWidth` 兜底仍在（`Label` 包装） |
| COMP | N/A | 纯原版 API（`GenDate` / `Find.TickManager` / `Translate`），无第三方触点 |
| SAVE | N/A | 无 Scribe 字段、无 schema 变化，旧档影响 0 |
| TEST | PASS（离线）／实机**未执行** | 编译 0 错 0 警（§四）；沙箱无法起游戏进程 |
| PERF | PASS（登记） | 绘制期翻译调用量 ≤ 可见行数（≈12 次/帧），与改动前同阶；未新增分配类别 |
| DELTA | **PASS** | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/eventstime-out-of-scope.txt -Blocking` ⇒ `PASS — 标注覆盖 4 个改动文件，无遗漏/超范围/残留`，`EXITCODE=0`。范围外清单于 2026-10-06 重建（A=1412 / 范围外=1408 / 本轮恰 4 文件）；期间工作树存在**并行** `CHG-20261006-TRKEY` 收口改动（新增 DefInjected 英文注入与 2 个审计脚本），故清单按最新改动集重建后复跑通过 |

## 六、部署（发布区，开发区之外）

游戏只扫描 `RimWorld\Mods\`，工程目录不在扫描范围 ⇒ 本次需经 `_scripts/deploy-to-game.ps1`
同步 `Assemblies/PersonalChronicle.dll` 与两份 `Languages/**/Archive.xml`，并**重启 RimWorld**
（运行中不重载程序集）后才能在游戏内看到效果。**本次未执行部署**（等待 PM 确认）。

## 七、残留与风险（照实登记）

1. **实机未执行**：沙箱无法启动游戏进程。「n 小时前 / n 天前 / n 年前」在 `whenWidth = 62f`
   下的实际观感（是否被 `TruncateToWidth` 截断）须 PM 实机复核；
2. 绘制期翻译未加缓存（§三-3），属本文件既有模式的延续，非本轮引入；
3. 本文件 `ManufacturingEventsPanels.cs` 为**未跟踪文件**（本模块整体尚未提交），本轮改动叠加在其上；
   `Languages/**/Archive.xml` 同文件另含 `CHG-20261006-TRKEY` 的未提交改动（键前缀统一 / 死键清理，
   该并行改动另在 ⑤ 区块插入 `…Events.Sub` 各 2 行 ⇒ 本标注行号已按当前实际对齐），
   本轮仅新增 3 键，未触碰其改动内容；
4. **62f 截断量算**（静态，非实测）：Tiny≈12px，CJK 全宽≈12px、数字≈6px、空格≈3px ⇒ 最宽候选
   「23 小时前」≈ 51px < 62f（余量约 18%），「365 天前」≈ 45px ⇒ **大概率不截断，但 Unity
   `Text.CalcSize` 真值无法离线复算，属未确认**。若实机确被截断，`Label` 现走 3 参 `TruncateToWidth`
   （无悬停补偿），可一行换用工程已有的 4 参带补偿重载（`Archive/UI/UIComponents.cs:102-111`）。

## 八、独立复核结论与处置（pc-ui-loc-guard，2026-10-06）

| 复核项 | 结果 | 处置 |
|---|---|---|
| 基数与 Key 语义逐分支对齐 / 档位边界 / 无死分支 / 无除零 | PASS（静态） | 无需改动 |
| `(int)` 溢出 | PASS | `tick ≥ 0` 前置拦截 ⇒ `delta ≤ int.MaxValue`；年档最坏 ≈ 596，远小于 int 上限 |
| `T1` 走 `NamedArgument` 重载（不落 CS0618） | PASS | 与 `WorkshopLayoutCanvas.Primitives.cs:108-111`、`WorkshopFloorPlan.Rails.cs:486-489` 逐字同款 |
| 3 键对称 / 占位符一致 / 无重名 / 无孤儿 / 无缺引 | PASS | — |
| 硬编码残留穷举（`Manufacturing/UI/` 全部 23 文件） | PASS | 已完全消除，**无第二处同类问题** |
| 标注行号漂移 | EXCEPTION（成立） | 因并行 `CHG-20261006-TRKEY` 补键；**已按当前实际改为 CS 2648-2651 / EN 2642-2645** |
| §一"无布局回归面"措辞 | EXCEPTION（成立） | 已改正（布局常量未变，但显示文本变长，截断风险移至 §七-1/§七-4） |
| `BuildStamp.g.cs` 未列 | EXCEPTION（成立） | 已补入 front matter（生成物，跟随 `CHG-20261006-TRKEY` 先例） |
| 编译 0 错 0 警 | 复核方未独立复现（无执行环境） | 本轮证据：`compile_with_sdk.ps1` ⇒ `EXITCODE=0 / error CS=0 / warning CS=0`（执行于改动之后） |

**复核报出的既有缺陷（非本轮引入，本轮不处置，登记备查）**：

| ID | 级别 | 事实 | 建议 |
|---|---|---|---|
| EV-01 | **P2** | `Application/ReadModels/ArchiveUiDataProvider.cs:592-595` 以 `"PersonalChronicle.UI.Ev" + kind` 拼键且无回退，而 `ChronicleEventKind` 有 9 值、`Ev*` 键仅 7 ⇒ `EvLeave` / `EvRoleChanged` 缺键时经 `LifeHealth.cs:300` 关键事件路径**可达裸 key 显示** | 并入 `CHG-20261006-TRKEY` 补键批次或另案：补 2 键，或给 `KindLabelLocal` 加 `EvOther` 回退 |
| EV-02 | P3 | `PersonalChronicle.UI.Events.Stream.Sub`（CS 2645 / EN 2639）现为**孤儿键**（副标实际消费 `…Events.Sub`） | TRKEY 收口时二选一：删除该键，或改建引用消费它 |
| EV-03 | P3 | `BuiltinManufacturingProvider.Analysis.cs:337-346` 用 `key.Translate(arg)` 直传，未显式 `(NamedArgument)` 转换 | 与工程主流写法统一（低优先） |
