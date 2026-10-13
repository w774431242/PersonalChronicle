# 2026-10-09 工作日志

## 创建 structured-memory skill（用户级）
- 路径：`C:/Users/Administrator/.workbuddy/skills/structured-memory/`（user 级，跨工作区可用）
- 背景：用户不满 agent 把记忆全塞进单个 memory.md，要求学做"结构化记忆" skill。
- 产出文件：
  - `SKILL.md`：触发描述 + 域分段/时间戳/validity 写入与检索协议
  - `references/memory-template.md`：schema 范本 + 扁平文件迁移指南
  - `references/sample_memory.md`：可跑演示数据
  - `scripts/retrieve_memory.py`：按域/关键词/过期过滤的确定性检索器
- 已用 `package_skill.py` 校验通过，打包为 `structured-memory.zip`。
- 设计取舍：文件层结构化（域+时间戳+过期标记+按需召回），不引外部图数据库，零额外成本；真·图谱需求再上 Graphiti+Kuzu。
