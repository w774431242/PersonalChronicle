# CodeBuddy CN 与当前工程适配

官方资料（制作时核实2026-10-10）：
- Skills：https://www.codebuddy.cn/docs/cli/skills
- 子代理：https://www.codebuddy.cn/docs/cli/sub-agents
- Agent Teams：https://www.codebuddy.cn/docs/cli/agent-teams

技能安装目录：用户级 `~/.codebuddy/skills/engineering-orchestrator/` 或项目级 `.codebuddy/skills/engineering-orchestrator/`。同名不要多处维护不同版本。入口仅使用name/description通用frontmatter；不含自动执行hooks，不开启未授权权限模式或Teams功能，不修改API密钥。

在支持Skills的会话使用 `/engineering-orchestrator` 或明确“使用 engineering-orchestrator 技能”。必须确认工具实际加载了SKILL.md，不能仅凭自称已加载。老会话可能需要重开，桌面显示需实际核验。

先发现本次工具/代理清单，使用当前版本真实调用签名；CLI `--model`/子代理模型配置与模型实际执行证据分开。此包不固定所有项目模型；当前用户指定的CLI主模型为deepseek-v4.1-flash时，遵守该选择并验证元数据，不能以模型自报确认。

## PersonalChronicle 项目

先遵守已有 `.codebuddy/rules/agent-cluster-frontdoor.md`、工程架构规范与pc-orchestrator，不创建第二总控。

映射候选角色：pc-arch-gatekeeper=契约/架构；pc-data-steward=资金/状态/存档；pc-ui-loc-guard=只读展示/本地化；pc-compat-auditor=游戏API/原版保护；pc-perf-tester=复现/测试/性能；pc-release-clerk=证据与部署清单。现有Gate若只授权评审，不派它写业务代码；发现可写执行者能力或由已授权主执行者集成。

当前用户要求Codex承担经理职能，CodeBuddy承担执行，本包不扩大Codex的业务修改授权。项目界面基于RUIF、原版数据基于源码/公开modding；公有机构由玩家控制；工资与购买遵循唯一真实资金源。以当前用户指令为准，不把历史“禁止部署”当作永恒规则。

Windows命令优先使用已授权的原生PowerShell工具；不要通过Bash启动PowerShell绕开工具策略。读取失败/自动审批拒绝时报告真实边界，不通过伪造沙箱变量或放宽权限重试。

一个front-door规则可以提示自动加载技能，但提示文件不是硬执行器。必须用真实任务记录验证并行；本技能不保证较弱模型必然遵守复杂约束。
