---
change_id: CHG-20261010-ECON-BATCH4-PREP
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ReadyNotDeployed
scope: 第 4 批（证据与发布）准备件 —— 四方证据绑定脚本 + 实机验收清单 + 存档往返清单；当前判定 BLOCKED（阻断项如实登记）
rule_ids: AI-001, AI-011, GOV-011, GOV-012, TEST-001, REL-001, REL-004, DELTA-001
files:
  - path: _scripts/bind-release-evidence.ps1
    type: A
  - path: docs/设计文档/架构/发布验收/release-evidence.json
    type: A
  - path: docs/设计文档/架构/发布验收/第4批_实机验收清单.md
    type: A
---

# 第 4 批（证据与发布）准备件

**ChangeID**：`CHG-20261010-ECON-BATCH4-PREP`｜**日期**：2026-10-10｜**部署**：**未部署（也不应部署 —— 见判定）**

## 一、为什么先做"准备件"而不是直接部署

审计报告第 4 批的原文要求：*"固定源码快照，绑定构建/TRX/程序集哈希；按现有要求统一部署一次，重启游戏验收。完整存档往返必须包含欠薪、进行中消费、照护授权、工作类型所有权、生产授权与历史重放，不能只测空账读档。"*
而报告"结论与证据边界"已指出：**取样程序集哈希 / 状态文件记录哈希 / 当前源码三者不一致** ⇒ 不能把源码、本地程序集、已部署版本视为同一验收对象。

⇒ 在部署之前，必须先把"四方证据"绑定成单一可查对象，并**显式暴露阻断项**。本批即交付这套机制与场景表。

## 二、交付件

### 1. `_scripts/bind-release-evidence.ps1`（新增）
把 **源码快照**（git HEAD + origin/main + dirty 计数 + 工作区指纹）→ **构建产物**（`BuildStamp.g.cs` 构建时刻 + `Assemblies/PersonalChronicle.dll` SHA256 + 写盘时刻）→ **测试结果**（TRX 路径 + SHA256 + passed/failed/skipped）→ **变更标注**（ChangeID + 标注文件 SHA256）绑成一份 manifest，并给出 READY / BLOCKED 判定。
- **绝不伪造 PASS**：任一测试失败、编译非 0 错 0 警、标注缺失即列阻断项并 `exit 2`；
- `-VerifyCompile` 可选跑一次编译并记录 errors/warnings；
- 依 gate-runner 既有教训，取 git 输出期间显式切 UTF-8（中文路径）；
- `pwsh -File` 不做 PowerShell 解析 ⇒ 参数按 `,;` 与引号统一拆分（本批实测踩到并已修）。

### 2. `docs/设计文档/架构/发布验收/release-evidence.json`（首次产出）
本次绑定结果摘要：
```
git HEAD          : 21fd0a9fdc89a0c9a3fb80fae9d27a6cc908c0b6
origin/main       : dbcf8f84954a6750108e9ffbae658b68a11e798b   ← 不等，已列阻断
working tree      : 2275 项未提交改动                          ← 已列阻断
BuildStamp (UTC)  : 2026-10-09 17:08
DLL SHA256        : F445F0EF9D566210509B9695AB34BADBD7CB765F54B7D3BBC91E36ACAA7675AD
tests             : 1199 passed / 2 failed
changeIds         : 7（R01–R04 / R05–R08 / R05-CORE / R08R09 / R09-PRICE / M10-FULL / M8-FULL）
```

### 3. `docs/设计文档/架构/发布验收/第4批_实机验收清单.md`（新增）
- **A–E 五组实机场景**（对应 R01–R04 / R05–R08 / R09 / M10 / M8）：每条给"游戏内操作 → 预期 → 现状（一律 `CodeVerified`，GameVerified 未勾）"；
- **F 组存档往返六项**（欠薪 / 进行中消费 / 照护授权 / 工作类型所有权 / 生产授权与已交付 / 历史重放）；
- **部署步骤**含关键校验："游戏目录内 DLL 的 SHA256 必须等于 manifest 的 `assemblySha256`"，不等即停止验收；
- **回滚表**（代码 / Save / 部署 / 例外四个层面）。

## 三、当前判定与阻断项（**不得据此宣称验收通过**）

| # | 阻断项 | 归属 | 处理 |
|---|---|---|---|
| 1 | 测试失败 2 项：`B241_...` / `B242_...`（`Expected Draining, But was: Off`） | **并行写入者**（经理侧 B 系列在改） | 由并行方收口；本批**未越界修改** |
| 2 | 编译 1 个错误：`EconomyCommands.cs` `ReverifyHint` 未定义 | **并行写入者**（半成品） | 同上 |
| 3 | HEAD != origin/main（未推送提交） | 仓库状态 | 绑定快照时须注明基线 |
| 4 | 工作区 2275 项未提交改动 | 并行多路写入 | 快照须连同 dirty 指纹绑定（脚本已记录） |

**⇒ 判定 `BLOCKED`。** 清零 1、2 之后重跑脚本，期望 `READY`，方可执行部署。

## 四、验证

| 项 | 值 |
|---|---|
| 脚本实跑 | **通过**：正确识别 7 个 ChangeID 标注、读取 TRX 计数、产出 manifest、判定 BLOCKED 并列出 3 类阻断项（EXITCODE=2） |
| 参数解析修复 | 实测 `-File` 模式把 `'a','b'` 整串传入 ⇒ 已加统一拆分并复测通过（7 个 ID 全部命中） |
| 本批是否跑过游戏 | **NOT EXECUTED**（无游戏进程；清单已标注 GameVerified 未达） |

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。
