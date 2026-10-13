---
change_id: CHG-20261001-ALLTABS-HTMLSYNC
version: preview-2026-09-22
date: 2026-10-01
author: PC-Orchestrator(AI)
scope: [系统中心蓝图/待落地项/]
files:
  - path: 系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html
    type: M
    symbols: dt-overview 重建(BuildRunTab 15行) / dt-health 重建(BuildHealthTab+BuildQualTab) / dt-contract 重建(BuildContractTab 16行+DevMode门控) / CSS .drow2·.pv-api·.rk-av::after+.dot 修复 / 工具栏契约页组删除 / JS dev门控+dfold通用绑定 / 头部同步记录段
    lines: 8-29,188,322-326,488,505-511,685-768,944-1041,1043-1095,1104,1107-1112,1270-1281
  - path: 系统中心蓝图/待落地项/HTML同步任务_设备卡生产页_2026-10-01.md
    type: M
    symbols: 全页签检测段（检测矩阵摘要+游戏侧待修项登记）
    lines: 40-58
  - path: 系统中心蓝图/待落地项/变更标注_CHG-20261001-ALLTABS-HTMLSYNC.md
    type: A
    symbols: 本变更标注文件（GATE-DELTA 前置产物，自覆盖条目）
    lines: 1-36
---

# 变更标注 · CHG-20261001-ALLTABS-HTMLSYNC（五页签一致性检测 + 二轮反向同步）

| 文件 | 改动类型 | 涉及符号 / Key | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| 系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html | M | dt-overview / dt-health / dt-contract / .drow2 / .pv-api / .rk-av::after+.dot / tab5 / dfold | 8-29,188,322-326,488,505-511,685-768,944-1041,1043-1095,1104,1107-1112,1270-1281 | 概览→BuildRunTab 15 行键值+检视块（文案取 Archive/Automation 真值，BoolText=是/否，InHours=小时）；健康→BuildHealthTab 9 行+Bar01 64×8+直方图标签改原版真值(一般/极佳/大师级/传奇级)+BuildQualTab 7 档枚举逐行 API；契约→BuildContractTab 16 行真结构(15×API+SIM)且显隐绑 DevMode；旧设计块三处→设计稿演示件折叠；.drow2 min-height 22；.pv-api 绿→青(Info)；修复 .rk-av::after 未闭合致 .dot 失效；「契约页 4/5」工具栏组移除；dfold 增 data-fold 通用绑定 | UI-001/LAND-009/S13/GOV-009 | UI/LOC |
| 系统中心蓝图/待落地项/HTML同步任务_设备卡生产页_2026-10-01.md | M | 全页签检测段 | 40-58 | 追加五页签检测矩阵摘要 + 游戏侧待修项登记（Run.Grid 缺键 / OpenBills 同文 / QualityScope 同文 / Contract.Caption 重复键 / 品质双文案） | GOV-013 | DELTA |
| 系统中心蓝图/待落地项/变更标注_CHG-20261001-ALLTABS-HTMLSYNC.md | A | 本变更标注（YAML front matter + 可读表） | 1-36 | 新增本轮变更标注（含自覆盖条目，规避 runner 豁免逻辑斜杠缺陷） | ANNO-001/GOV-013 | DELTA |

### 一致性自检
- [x] 清单覆盖本轮实际改动全部文件（git 其余 450+ 项为范围外历史工作，经 -ExcludeFromFile 声明，勿误归因）
- [x] 范围 ⊆ Change Summary.Scope（仅预览资产 + 任务 md，零游戏侧代码改动）
- [x] 每个文件标注类型与涉及符号（node --check 通过 / div 267:267 配平 / state.tabs·data-t="tabs"·「大师 5」扫描归零）
- [x] Change ID 贯穿 Intake（AI-001）→ Summary（本交付）→ Annotation（本文件）→ Gate Record
- [x] 无调试代码/死代码残留（临时 ~sync_* 文件已清理）
- [x] gate-runner.ps1 自动校验（见 Gate Record）
