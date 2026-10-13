---
change_id: CHG-20261004-ADDSHIFT
version: v1.1.5
date: 2026-10-04
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopShiftEditor.cs
    type: M
    symbols: Dialog_WorkshopShiftEditor.isNew, Dialog_WorkshopShiftEditor.committed, Dialog_WorkshopShiftEditor.PostClose, Dialog_WorkshopShiftEditor..ctor
    lines: 48-52, 59-63, 169-172, 268-302
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Schedule.cs
    type: M
    symbols: WorkshopManageView.OpenNewShiftEditor
    lines: 936-945, 958-963
---

# 变更标注 · CHG-20261004-ADDSHIFT

## Change Summary

玩家在**游戏内验收** `CHG-20261004-WSAUDIT1` 时实测报告的缺陷：

> 「只要单击了添加班次，弹窗取消还是加入了一个未设置的班次。」

本条登记该缺陷的修复。

## 根因

新建班次路径**先把对象写进调度器、再打开编辑器**，而取消没有任何回滚路径：

```csharp
// WorkshopManageView.Schedule.cs · OpenNewShiftEditor（修复前）
var shift = new WorkshopShift { TargetZone = zone, StartHour = 6, EndHour = 18 };
manager.AddShift(shift);                                                    // ← 已落库
Find.WindowStack.Add(new Dialog_WorkshopShiftEditor(zone, shift, Refresh));  // ← 之后才开窗
```

该方法的 **summary 注释写着「取消也不会有半成品」，与实现自相矛盾**（文档漂移，
与审计报告 ARCH 组 #10 同型）。取消入口有多个（「确定」以外的 X / ESC /
点窗外 `closeOnClickedOutside` / 删除按钮），此前无一回滚 ⇒ 列表里留下一条
默认 `6-18` 的未设置班次。

## 修法

**回滚挂在 `Dialog_WorkshopShiftEditor.PostClose()`** —— 窗口关闭入口虽多，
`PostClose` 是唯一必经的收口点，挂钩一处即覆盖全部路径（GOV-005）。

| 位置 | 改动 |
|---|---|
| `Dialog_WorkshopShiftEditor.cs:48-52` | 新增 `readonly bool isNew` 与 `bool committed` |
| `:59-63` | 构造函数新增可选参数 `bool isNew = false`（编辑已有班次不传 ⇒ 行为不变） |
| `:169-172` | 「确定」按钮置 `committed = true` |
| `:268-302` | 新增 `PostClose()`：`isNew && !committed` ⇒ `Manager.RemoveShift(shift)` + `onChanged()` 刷新 UI |
| `WorkshopManageView.Schedule.cs:958-963` | 新建时传 `isNew: true`；`:936-945` 修正与实现不符的 summary 注释 |

- 回滚走既有 `RemoveShift`（会连带 `UnassignShift` 解除各班组显式指派，不留悬空引用）；
  若玩家在草稿里点过「删除」，`RemoveShift` 已执行 ⇒ `PostClose` 里是幂等 no-op。
- **语义**：点「确定」= 创建；其它任何方式关闭 = 取消（不留下班次）。

## 兼容性

- **存档影响：无。** 不新增/不删除任何 Scribe 字段，不改落盘结构。
- **行为影响**：仅"新建后取消"不再留下班次（这正是要修的）；**编辑已有班次**的取消语义
  保持不变（不撤销已做的修改），因为 `isNew` 默认 `false`。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`PostClose` override 有效） |
| 部署 | 已验证 | Defs 31 / Languages 37 / Assemblies 1 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261004-ADDSHIFT.md -ExcludeFromFile .codebuddy/tmp/addshift-out-of-scope.txt -Blocking` |
| 实机 | 待玩家验证 | 重启后：点「添加班次」→ 直接取消（X / ESC / 点窗外）⇒ 班次表**不应**多出条目；点「确定」⇒ 正常创建 |

## 范围说明（同批未提交改动）

本仓工作区另有 `CHG-20261004-WSAUDIT1` 已登记的 5 个文件改动（`ResolveShiftPool` 别名修复、
`managerShiftCount`、`dutyPlanBuiltTick`、`HourInShift`、`WorkTakeover` 作用域）**尚未提交**，
本次 DELTA 校验中以 `-ExcludeFromFile` 声明为范围外。
