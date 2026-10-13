# 变更标注 · CHG-20261002-RUNTAB-100PCT

- date: 2026-10-02
- type: UI + LOC（游戏内代码 + Keyed 两语言 + HTML 基准注记）
- scope: ①概览页 RunTab 空态=「无」（用户裁决，仅 RunTab）②倍率加成 ×N→NN %（工时/耗材/KPI 第 4 格）③概览页 100% 还原修平（OpenBills 标签、Run.Grid 镜像修正）④生产页 Bar01 白杠缺陷根因修复 ⑤产物行对齐 HTML「产物 | 名 ×数」⑥配料充足行 tone 0→1
- gates: COMP PASS(0 错 0 警) / TEST PASS(393 通过 0 失败) / DEPLOY PASS / LOC PASS / DELTA PASS(降级法)
- rules: LOC-001 · LAND-009（用户 2026-10-02 显式裁决覆盖「—」惯例，仅 RunTab）· GOV-006 · LAND-010
- exemptions: 无 EXC 登记（品质 +1 维持为裁决空间内选择，见 GR §1-R3）

## files
| path | S | 变更 | 规则 |
|---|---|---|---|
| Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.cs | M | RunTab 15 行空态→NoneValue；加成三行×N→NN%（FormatBonusPercent）；KPI 第 4 格同形；产物行「产物｜名 ×数」；配料 tone 0→1；新增 NoneValue/FormatBonusPercent（:719-734）；CHG 注记 4 处 | LAND-009/LOC-001 |
| Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.Draw.cs | M | Bar01 条填充 Color.white→ToneColor(row.Tone)；删无效 GUI.color 操纵（DrawBoxSolid 参数色机制） | GOV-006/LAND-010 |
| Languages/ChineseSimplified/Keyed/Archive.xml | M | +EmptyValue=无（Run.Empty 行后）；Shift.OpenBills 本班待加工工单→待加工工单；Bill.ProductLabel 产出 {0}→产物 | LOC-001 |
| Languages/English/Keyed/Archive.xml | M | +EmptyValue=None；Shift.OpenBills→Bills to process；Bill.ProductLabel→Product | LOC-001 |

### 注脚（untracked/gitignore 条目，不入 YAML 判定集，信息保留）

- `系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html`（untracked）：概览段+用户裁决注记 1 条（空态「无」、加成 NN %）；Run.Grid「缺键」镜像→电网储能（:711）+待修注记更新（:722-723）；mkpi 前注释；生产段 rmline 注记 1 条（LAND-010）。
- `系统中心蓝图/待落地项/HTML同步任务_设备卡生产页_2026-10-01.md`（untracked）：追加「100% 还原段」（用户三条裁决 + 白条根因 + 产物行对齐）。
- `系统中心蓝图/待落地项/设备卡弹窗_落地基线与批次计划_2026-09-23.md`（untracked）：追加 CHG-20261002-RUNTAB-100PCT 批次段（任务追溯）。
- `.codebuddy/audit/gate-records/GR-20261002-RUNTAB-100PCT.md`（gitignore 内）：Gate Record（15 行对照表 + Gate 证据 + 遗留风险 R-1..R-5）。
- `变更标注_CHG-20261002-RUNTAB-100PCT.md`：本文件。

## 不改动声明（非目标）
- 人员页 ×系数三行、健康页耐久 tone 阈值、Tab.HandoverOpenBills 键值、Run.Empty 失引用键、ResolveWorkLeftText 共享函数。
- 热路径零 new/LINQ：本轮仅字符串格式化与布尔分支，无分配热点。不入存档。
