---
change_id: CHG-20261001-CREWPAGE-HTMLSYNC
version: preview-2026-09-22
date: 2026-10-01
author: PC-Orchestrator(AI)
scope: [系统中心蓝图/待落地项/]
files:
  - path: 系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html
    type: M
    symbols: dt-crew 区块 DOM 重排 / CSS crewcols·crow·crowbar·msgline / JS 派工toggle+crewfold / 头部同步记录段
    lines: 45-66,252-306,815-911,1093-1145
  - path: 系统中心蓝图/待落地项/HTML同步任务_设备卡生产页_2026-10-01.md
    type: M
    symbols: 人员页补充段（反向同步要点+游戏侧待修项）
    lines: 22-42
  - path: 系统中心蓝图/待落地项/变更标注_CHG-20261001-CREWPAGE-HTMLSYNC.md
    type: A
    symbols: 本变更标注文件（GATE-DELTA 前置产物，自覆盖条目）
    lines: 1-33
---

# 变更标注 · CHG-20261001-CREWPAGE-HTMLSYNC（人员页 HTML 反向同步）

| 文件 | 改动类型 | 涉及符号 / Key | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| 系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html | M | dt-crew / crewcols / crow / crowbar / msgline / crewfold | 45-66,252-306,815-911,1093-1145 | 决策台→单块键值行列表；行序补齐4行；排名值三段合一+条64×8；评分实算修正76/55/35/25；LEDGER行徽标如实呈现；pickcard→折叠演示件；dutybar/rk系死样式清理+dot迁移恢复 | UI-001/LAND-009/S13 | UI/LOC |
| 系统中心蓝图/待落地项/HTML同步任务_设备卡生产页_2026-10-01.md | M | 人员页补充段 | 22-42 | 追加反向同步要点与游戏侧待修项登记 | GOV-013 | DELTA |
| 系统中心蓝图/待落地项/变更标注_CHG-20261001-CREWPAGE-HTMLSYNC.md | A | 本变更标注（YAML front matter + 可读表） | 1-33 | 新增本轮变更标注（含自覆盖条目，规避 runner 豁免逻辑斜杠缺陷） | ANNO-001/GOV-013 | DELTA |

### 一致性自检
- [x] 清单覆盖本轮实际改动全部文件（git status 内其余 445 项为范围外历史工作，经 -ExcludeFromFile 声明）
- [x] 范围 ⊆ Change Summary.Scope（仅预览资产，零游戏侧代码改动）
- [x] 每个文件标注类型与涉及符号
- [x] Change ID 贯穿 Intake（AI-001）→ Summary（本交付）→ Annotation（本文件）→ Gate Record
- [x] 无调试代码/死代码残留（node --check 通过；rk 系残留扫描归零）
- [x] gate-runner.ps1 自动校验（见 Gate Record）
