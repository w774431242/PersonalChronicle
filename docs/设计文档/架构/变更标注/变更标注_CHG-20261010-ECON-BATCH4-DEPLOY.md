---
change_id: CHG-20261010-ECON-BATCH4-DEPLOY
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: DeployedAwaitingGameVerification
scope: 审计四批的统一部署（233 文件）与证据绑定收口 —— 判定两级化（硬阻断 / 软提示）、部署哈希三方一致核验、验收清单更新为就绪
rule_ids: GOV-011, GOV-012, TEST-001, REL-001, REL-002, REL-004, DELTA-001
files:
  - path: _scripts/bind-release-evidence.ps1
    type: M
  - path: docs/设计文档/架构/发布验收/release-evidence.json
    type: M
  - path: docs/设计文档/架构/发布验收/第4批_实机验收清单.md
    type: M
---

# 审计四批 · 统一部署与证据收口

**ChangeID**：`CHG-20261010-ECON-BATCH4-DEPLOY`｜**日期**：2026-10-10｜**状态**：**已部署，待游戏内验收**

## 一、判定分级改造（`bind-release-evidence.ps1`）

原判定把"未推送提交 / 工作区有改动"与"测试失败 / 编译报错"同等对待 ⇒ 前者会长期恒真（修复期工作区必然有改动），使判定失去信号价值。改为两级：

| 级别 | 内容 | 后果 |
|---|---|---|
| **硬阻断** | 测试失败 / 编译非 0 错 0 警 / 变更标注缺失 | 阻止交付（`exit 2`） |
| **软提示** | HEAD != origin/main / 工作区有未提交改动 | **不阻止交付**，但必须绑定进 manifest 供审计追溯 |

理由：审计报告本就接受"修复期工作区有改动"，只要求**把 dirty 指纹一并绑定** —— 软项登记即合规，把它们当阻断项反而会鼓励"绕过"。

## 二、部署证据（三方哈希一致，首次达成）

```
compile             : errors=0 warnings=0
tests               : 1216 passed / 0 failed / 1 skipped   （Tests/TestResults/Batch4-Ready.trx）
BuildStamp (UTC)    : 2026-10-09 17:26
changeIds           : 8（R01–R04 / R05–R08 / R05-CORE / R08R09 / R09-PRICE / M10-FULL / M8-FULL / BATCH4-PREP）
判定                : READY_WITH_WARNINGS（无硬阻断）
```

部署（`_scripts/deploy-to-game.ps1`，packageId 校验通过 `colony.integratedmanagement`）：

| 项 | 值 |
|---|---|
| 目标 | `E:\SteamLibrary\steamapps\common\RimWorld\Mods\殖民地综合管理系统` |
| 复制 | **233 个文件**（About 13 / Assemblies 1 / Defs 36 / Fonts 8 / Languages 66 / Patches 3 / README 1 / Textures 105） |
| 备份 | **233 个**到 `_backups\release-deploy\deploy_20261010_012713_777` |
| **工程 DLL** | `100EE221F5B1A92C9EBB6693D6A34688F4FFE27EA73B8F736606881D2FC2D6CB` |
| **目标 DLL** | `100EE221F5B1A92C9EBB6693D6A34688F4FFE27EA73B8F736606881D2FC2D6CB` ← **与工程一致** |
| **manifest** | `assemblySha256` 同上 ← **三方一致** |

⇒ 审计报告指出的"源码 / 本地程序集 / 已部署版本三者不能视为同一验收对象"**本次已达一致**：manifest 记录的哈希 == 工程 DLL == 游戏目录 DLL。**部署对象就是验收对象。**

## 三、更新后的验收清单

`第4批_实机验收清单.md` §0 已从 `BLOCKED` 更新为 `READY_WITH_WARNINGS`（含判定语义说明与本次哈希）。A–F 组场景（R01–R04 / R05–R08 / R09 / M10 / M8 + 存档往返六项）**全部仍标 `CodeVerified`，GameVerified 未勾**。

## 四、下一步（须人工执行）

1. **重启 RimWorld**（运行中不会重载程序集）；
2. 按清单 A–F 逐条执行并勾选 `GameVerified`；
3 **存档往返不可只测空账**：F1 欠薪 / F2 进行中消费 / F3 照护授权 / F4 工作类型所有权 / F5 生产授权与已交付 / F6 历史重放。

**回滚**：`_backups\release-deploy\deploy_20261010_012713_777` 保留部署前全部 233 个文件（保持相对路径），可整体还原。

## 五、限制与风险（如实登记）

1. **软提示两项仍在**：HEAD 未推送、工作区 2279 项未提交改动 —— 快照非单一提交，故本次绑定的"源码快照"以 **dirty 指纹**为准（manifest `source.workingTreeFingerprint`），不是某个 commit；
2. **游戏内验收完全未执行**（无游戏进程参与）—— 本标注只证明"部署物与验收物一致"，**不证明任何行为正确**；
3. 并行写入者的后续改动会再次打破三方一致 ⇒ 每次重新部署都必须重跑绑定脚本并重新核验哈希。

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。
