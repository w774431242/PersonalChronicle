---
change_id: CHG-20261010-ECON-DEPLOY-MERGE
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: DeployedAwaitingGameVerification
scope: 合并部署（把 M8 缺口补齐并入第 4 批验收版本）+ 验收清单更新（判定/哈希/E5-E6 新场景）
rule_ids: GOV-011, GOV-012, TEST-001, REL-001, REL-002, REL-004, DELTA-001
files:
  - path: docs/设计文档/架构/发布验收/release-evidence.json
    type: M
  - path: docs/设计文档/架构/发布验收/第4批_实机验收清单.md
    type: M
---

# 合并部署（第二次）· 验收版本收口

**ChangeID**：`CHG-20261010-ECON-DEPLOY-MERGE`｜**日期**：2026-10-10｜**状态**：**已部署，待游戏内验收**

## 一、为什么合并部署

`CHG-20261010-ECON-GAP2-CAREUI`（M8 执行者指派 UI）在首次部署之后落地 ⇒ 工程 DLL 与游戏目录 DLL 分叉（三方哈希被打破）。若分别验收，同一份清单要跑两轮；合并后**一次重启即可验收全部 10 个 ChangeID 的内容**。

## 二、判定与哈希（三方一致）

```
compile             : errors=0 warnings=0
tests               : 1216 passed / 0 failed / 0 skipped   （Tests/TestResults/Gap2-final.trx）
BuildStamp (UTC)    : 2026-10-09 18:00
changeIds           : 10（R01–R04 / R05–R08 / R05-CORE / R08R09 / R09-PRICE / M10-FULL / M8-FULL
                        / BATCH4-PREP / BATCH4-DEPLOY / GAP2-CAREUI）
判定                : READY_WITH_WARNINGS（软提示：HEAD 未推送、工作区 2287 项未提交 —— 已绑定进 manifest）
```

| 对象 | SHA256 |
|---|---|
| 工程 `Assemblies/PersonalChronicle.dll` | `A6FBC9DC7738EFB786944C5E841BE8E353517A19D204944FF18388A82427F405` |
| 游戏目录 `Mods\殖民地综合管理系统\Assemblies\PersonalChronicle.dll` | `A6FBC9DC7738EFB786944C5E841BE8E353517A19D204944FF18388A82427F405` |
| manifest `build.assemblySha256` | `A6FBC9DC7738EFB786944C5E841BE8E353517A19D204944FF18388A82427F405` |

⇒ **部署对象 = 验收对象**（第二次达成）。

## 三、验收清单更新

- §0 判定段更新为第二次部署（10 个 ChangeID + 新哈希 + 新 TRX）；
- §3 部署记录更新（第二次部署 + 三方哈希表）；
- **E 组新增两条本批专属场景**：
  - **E5**：Grant 时选「指定执行者 · 某殖民者」⇒ 授权行显示执行者；让**另一名**医生治疗 ⇒ 拒绝记账（`AuthorizationUnknown`），无证据无扣费；
  - **E6**（对照）：Grant 时选「不限执行者」⇒ 与旧行为一致（任意非受益者医生可执行）。

## 四、限制与风险（如实登记）

1. **游戏内验收仍未执行**（无游戏进程）—— 本标注只证明"部署物 = 验收物"，不证明任何行为正确；
2. **软提示两项仍在**（HEAD 未推送、工作区 2287 项未提交）⇒ 本次"源码快照"以 manifest 的 `source.workingTreeFingerprint` 为准，非某个 commit；
3. **并行写入者若再改动并重新构建，三方一致会再次失效** ⇒ 每次重部署都要重跑绑定脚本并重核哈希；
4. **缺口 ①（M8 喂养/学习接线）与 ③（R09 到店任务链）未在本版本内** —— 它们是独立轮工程（前者需 ArchReview + EXC）。**验收时不要用它们的预期去判本版本**。

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。
