# 交付模板：Change Summary + Gate Results（REL-002 / AI-012）

每次交付必须输出以下两部分。没有可执行环境时，Gate 写 `NOT RUN`，不得伪造通过。

```markdown
## Implementation Summary
- Change ID: <全局唯一，如 CHG-2026-0912-001；贯穿 Intake→Summary→Annotation→Gate→Changelog>
- Goal: <目标>
- Changed files: <文件>
- Rule IDs: <规则>
- Architecture: <层级/依赖>
- Data: <Def/Runtime/Save/UI>
- Localization: <Key/语言/fallback>
- Compatibility: <目标 Mod/DLC/降级>
- Performance: <热路径/证据>
- Migration: <无/有，说明>
- Exceptions: <EXC ID 或无>

## Change Summary
- Title: <用户可理解的变更标题>
- Scope: <模块/文件/功能>
- Reason: <动机>
- User impact: <用户行为变化>
- Tests/Gates: <结果>

## 变更标注（开发者向精确改动清单 · 验收定位）
> 与用户向 Changelog 职责分离：Changelog 不写行号（REL-004），变更标注精确到文件/符号/行。
> 模板：[change-annotation-template.md](change-annotation-template.md)；规则：ANNO-001 / GATE-DELTA。
- Annotation: <变更标注路径或内联；须覆盖实际改动、Change ID 可互链>

## Gate Results
| Gate | Result | Evidence |
|---|---|---|
| ARCH | PASS/BLOCKED/N/A | <证据> |
| DATA | PASS/BLOCKED/N/A | <证据> |
| LOC | PASS/BLOCKED/N/A | <证据> |
| UI | PASS/BLOCKED/N/A | <证据> |
| COMP | PASS/BLOCKED/N/A | <证据> |
| SAVE | PASS/BLOCKED/N/A | <证据> |
| TEST | PASS/BLOCKED/N/A | <证据> |
| PERF | PASS/BLOCKED/N/A | <证据> |
| REL | PASS/BLOCKED/N/A | <证据> |
| DELTA | PASS/BLOCKED/N/A | 变更标注路径/覆盖说明 |

## Remaining Risks
- <风险或"无">
```

## Changelog 模板（REL-010）

分类：Added / Changed / Fixed / Removed / Compatibility / Localization / Performance / Breaking。
没有内容的分类省略；一条记录只表达一个主要事实；写用户可感知影响，不写"修改 X.cs 第 N 行"。

```markdown
# Changelog

## [vX.Y.Z] - YYYY-MM-DD

### Added
- <用户可感知的新功能>

### Changed
- <已有行为变化>

### Fixed
- <问题及影响>

### Compatibility
- <Vanilla/DLC/第三方 Mod>

### Localization
- <Key/语言/布局>

### Performance
- <性能结果>

### Breaking
- <迁移、API、Save 或依赖影响>

### Migration
- <旧 Save/API 的处理方式>
```

## 语义化版本（REL-001）

```text
Major：破坏公共 API、Save schema、依赖方向或用户行为
Minor：向后兼容的新功能、Def、Key、Provider 能力
Patch：Bug、性能、兼容和文案修复，不改变契约语义
```

## 发布前 REL-012 自检

```text
[ ] Change Summary 完整
[ ] 变更标注已生成并覆盖实际改动，可定位到文件/符号/行
[ ] Changelog 分类准确且无空分类
[ ] 版本/平台/DLC/依赖/Save 范围明确
[ ] 适用 Gate 全部 PASS 或有效 PASS WITH EXCEPTION
[ ] Migration/回滚/已知问题明确
[ ] 发布包内容经过检查
[ ] 规则索引与文档互链未断
```
