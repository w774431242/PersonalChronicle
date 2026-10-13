# 变更标注 · CHG-20261002-UIAUDIT

- date: 2026-10-02
- type: UI（游戏内绘制参数对齐 HTML 基准 + 空态提示落地 + 部署生效性诊断 + 全 tab 参数矩阵）
- scope: ①生产列表 UI 遮盖专项（结论：静态推演未复现遮盖；用户图红横线=HTML 配料扩展块燃料条，游戏内无对应区块）②全 tab UI 设计参数矩阵核查（HTML computed 真值 ↔ 代码常量）③7 处非受控差异修平 ④部署一致性核验（部署包=工程全等；「没生效」疑因未重启游戏）
- gates: COMP PASS(0 错 0 警) / TEST PASS(393 通过 0 失败) / DEPLOY PASS / DELTA 见 GR §5
- rules: UI-004 · LAND-003（受控降级）· LAND-007（字形档）· GOV-009
- exemptions: 无 EXC 新建；并行工作流 CHG-SHOP-4FIX-001 产物以 -ExcludeFromFile 声明范围外（本仓库存在 1000+ 历史未提交工作）

## files
| path | S | 变更 | 规则 |
|---|---|---|---|
| Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.cs | M | ①BodyPad 22/26/26→24/28/28（HTML:483 终值 24px 28px 28px）②MetricGap 10→8（HTML:450）③关闭键 right 8→14（HTML:140）④状态行色带（状态色 14% 底+45% 描边）+ dot 7→9（HTML:157-163/363-364）⑤新增 DrawCaptionBar+CaptionIndent：块标题 3px 色条+文本缩进 10（HTML:185-186/422）⑥BillQueue 空态提示落地（绘制+测量同式；键 Queue.Empty 原声明未消费）⑦注释同步（:17/:46/:174-177） | UI-004/LAND-010 |

### 注脚（untracked/gitignore 条目，不入判定集，信息保留）

- `.codebuddy/audit/gate-records/GR-20261002-UIAUDIT.md`（gitignore 内）+ 工程根同名副本：Gate Record（块 A 对照表/块 B 核查/块 C 矩阵/遗留风险；DELTA 实跑 PASS）。
- `.codebuddy/tmp/uiaudit-out-of-scope.txt`（gitignore 内）：GATE-DELTA 豁免清单（全量未提交 − 本文件）。
- Assemblies/PersonalChronicle.dll（.dll，判定集外）：合并树 csc 编译 16:57:50 后由并行工作流（SHOP4FIX/SHIFTTABLE）17:19 构建收敛为含全部三方符号版；部署目录 Mods/PersonalChronicleVer0.1（仓库外）同 hash。
- `_uiaudit_build.log` / `_uiaudit_build2.log`（.log，判定集外）。
- 任务 md 追加：`系统中心蓝图/待落地项/HTML同步任务_设备卡生产页_2026-10-01.md`（untracked）。
- 并行工作流 CHG-SHOP-4FIX-001（Automation ×8 + Defs ×1 + Languages ×2 + docs ×3）范围外豁免。

## 不改动声明（非目标）
- 不改 HTML 基准（除任务 md 追加段）；不改 Languages/Defs；圆角/呼吸动画/字体档差异为 LAND-003/LAND-007 受控；行背景/dashed 行线/状态列边框等待 PM 裁决（GR §C 待裁决清单）；热路径零 new/LINQ（本轮全部为常量/绘制分支，无分配）。