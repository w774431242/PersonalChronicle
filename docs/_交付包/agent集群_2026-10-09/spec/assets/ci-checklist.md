# CI / 提交前检查清单

> 映射「规范 → 可自动执行动作」。脚本均位于 `scripts/`。

## 提交前（本地）
- [ ] `pwsh scripts/gate-runner.ps1 -Annotation <变更标注.md>` → GATE-DELTA 无 BLOCKED（遗漏/超范围/调试残留）。
- [ ] 已生成变更标注（YAML front matter + 表格），Change ID 贯穿全链路。
- [ ] 已生成/更新 Change Summary 与 Changelog（用户向，不写行号）。
- [ ] 适用 Gate 在 Gate Record 填好四态 + Evidence；无证据不标 PASS。

## 同步规范（改了工程 docs 后）
- [ ] `pwsh scripts/sync_from_docs.ps1 -Source <工程docs/架构>` → 复制 + SHA256 校验 + **规则引用 lint**（悬空引用退出码 2）。

## PR / 发布前
- [ ] GATE-ARCH/DATA/LOC/UI/COMP/SAVE/TEST/PERF/REL/DELTA 全部 PASS 或有效 PASS WITH EXCEPTION。
- [ ] 任何 P0/P1 失败或 GATE-DELTA BLOCKED → 阻止 Release。
- [ ] 无运行环境时按「证据分级协议」（§13）标 N/A + 理由，转人工复核。

## 规则引用 lint 说明
`sync_from_docs.ps1` 在复制后扫描所有 `[XXX-NNN]` 引用，校验目标规则 ID 已定义；发现悬空引用即退出码 2 并打印，防止 6 处交叉定义失配。
