---
change_id: CHG-YYYYMMDD-NNN
version: vX.Y.Z
date: YYYY-MM-DD
author: <作者或 AI>
scope: [Source/..., Defs/...]
files:
  - path: Source/.../X.cs
    type: M          # M=修改 A=新增 D=删除 R=重命名 C=复制/切片
    symbols: ClassX.MethodY / DefName:Y / Key:Y / AssetID:Y
    lines: 120-145
  - path: Defs/.../Y.xml
    type: M
    symbols: DefName:Medal_Z
    lines: 30-40
---

# 变更标注模板（开发者向 · 验收定位用）

**规则：** ANNO-001 / GOV-013（唯一规范性定义） / GATE-DELTA 前置产物  
**定位：** 本模板是「开发者向的精确改动清单」，与「用户向的 Changelog」职责分离：
- Changelog 写用户可感知影响，刻意不写行号（REL-004 避免「修改 X.cs 第 N 行」）。
- 变更标注写精确到「文件 / 改动类型 / 涉及符号 / 行范围 / diff 摘要」的改动，供验收时精准定位与回滚。

> 顶部 YAML front matter 为机器可解析段（`scripts/gate-runner.ps1` 据此自动校验）；下方 Markdown 表格为人工可读段，两者须一致。

## Change Annotation
- Change ID: <全局唯一，如 CHG-2026-0912-001；贯穿 Intake → Summary → Annotation → Gate → Changelog>
- Version: <版本>
- Date: YYYY-MM-DD
- Author/AI: <作者或 AI>
- Scope(摘要): <模块/功能>

### 改动清单（逐文件）
对每一个实际改动的文件一行一条：

| 文件 | 改动类型 | 涉及符号 / DefName / Key / AssetID | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| Source/.../X.cs | M | ClassX.MethodY / 字段 Z | 120-145 | 新增参数校验与异常日志 | ARC-006 | ARCH/DATA |
| Defs/.../Y.xml | M | DefName:Medal_Z | 30-40 | 新增 <effect> 字段 | DATA-004 | DATA |
| Languages/.../Archive.xml | M | Key:Archive_X | 12 | 新增中文键 | LOC-001 | LOC |
| Textures/.../a.png | A | AssetID:a | — | 新增贴图 | ASSET-001 | ASSET |

改动类型：M=修改，A=新增，D=删除，R=重命名，C=复制/拆分/物理切片。  
涉及符号：代码写 `类.方法` 或 `字段`；Def 写 `DefName` 或 tag；翻译写 `Key`；资产写 `AssetID`。

### 一致性自检（GATE-DELTA 前置）
- [ ] 清单覆盖 git diff / 实际改动的全部文件，无遗漏
- [ ] 清单范围 ⊆ Change Summary.Scope，无超范围改动
- [ ] 每个改动文件都标注了类型与涉及符号
- [ ] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [ ] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [ ] 已用 `pwsh scripts/gate-runner.ps1 -Annotation <本文件>` 自动校验通过

### 关键 diff 片段（可选）
难以用一句话摘要时，附最小可定位的 diff 片段：
```diff
// Source/.../X.cs
+ public void MethodY(...) { ... }
```
