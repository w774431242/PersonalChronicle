# Gate 检查清单（可直接复制勾选）

结果四态：`PASS` / `PASS WITH EXCEPTION` / `BLOCKED` / `NOT APPLICABLE`（N/A 必须写明原因）。
任一 P0/P1 失败阻止 Release。没有可执行环境时诚实标记"未执行"，不得伪造通过。

## GATE-ARCH — 架构

关联：GOV-001 / MATRIX-001 / ARC-001

```text
[ ] 变更所属层级和模块已写明
[ ] 依赖出现在允许矩阵中
[ ] 无循环依赖
[ ] Core 未引用 UI/Theme/Asset/第三方 Mod
[ ] 跨模块访问通过 Contract/API/DTO
[ ] 继承、Harmony、全局状态都有必要性说明
[ ] 失败降级和替代方案已定义
[ ] 大文件/类按 ARC-013 治理：职责单一、partial 切片或按层归位，无 god class 堆砌
[ ] 文件结构变更仅做物理切片，未改动类名/签名/契约/Save schema/翻译键
```
失败条件：Core→Presentation、Core→Compatibility、UI 直接改写领域数据、未登记 Harmony、违反 ARC-013、或切片时改了契约。

## GATE-DATA — 数据

关联：DATA-001 / DATA-004 / SAVE-001

```text
[ ] Def 只包含静态配置
[ ] Runtime State 不被错误写入 Def 或 UI
[ ] Save Data 有稳定 ID、默认值和版本
[ ] DefModExtension/Comp/Service 选择有理由
[ ] Patch 使用最小 XPath 和条件
[ ] 缓存可从权威数据重建
[ ] 不依赖显示文本作为标识
```

## GATE-LOC — 本地化

关联：LOC-001 / LOC-004 / COMP-007

```text
[ ] DefName、Translation Key、显示文本职责分离
[ ] 用户可见代码字符串全部 Keyed
[ ] DefInjected 路径与 Def 类型匹配
[ ] Key 和 Asset 有唯一命名空间
[ ] 英文/默认语言可用
[ ] 目标语言缺失时 fallback 可解释
[ ] 占位符、复数、性别/格式化参数一致
[ ] 长文本、短文本、不同字体尺寸已验证
[ ] 未修改其他 Mod 的 Key 或全局翻译
```

## GATE-UI — UI 与视觉

关联：UI-001 / ASSET-001 / PERF-002

```text
[ ] Window/Panel/Component 职责清晰
[ ] UI State 不持有核心持久状态
[ ] Theme Token 集中管理，颜色无散落魔法值
[ ] Asset 路径隔离且引用可解析
[ ] Normal/Hover/Pressed/Selected/Disabled 状态完整
[ ] 多语言布局不依赖固定中文宽度
[ ] Empty/Loading/Error/Unavailable 状态明确
[ ] 大列表、图表和 Timeline 有分页/虚拟化/缓存策略
```

## GATE-COMP — 兼容

关联：COMP-001 / COMP-004

```text
[ ] Core 不直接依赖目标 Mod
[ ] 依赖类型明确：硬依赖/软依赖/能力探测
[ ] 目标 Mod 缺失时正常运行
[ ] 目标 Mod 存在时只执行条件适配
[ ] null、版本差异、初始化顺序有保护
[ ] Harmony Target 解析失败可安全降级
[ ] Patch 范围、顺序、冲突和回滚已记录
[ ] 中英文及目标 Mod 组合已测试
```

## GATE-SAVE — 存档与迁移

关联：SAVE-001 / SAVE-004

```text
[ ] Save schema 版本可识别
[ ] 新字段有默认值
[ ] 缺失/未知字段不会导致启动失败
[ ] DefName/Key 重命名有别名或 Migration
[ ] 旧版本 Save 已加载测试
[ ] Migration 幂等且失败可诊断
[ ] UI/Texture/运行时缓存未写入 Save
[ ] Breaking 变更有 Changelog 和恢复说明
```

## GATE-TEST — 测试

关联：TEST-001

```text
[ ] 编译通过且无新增警告
[ ] XML/Def/Translation 解析通过
[ ] Vanilla 启动与核心流程通过
[ ] 目标 DLC/第三方 Mod 组合通过（如适用）
[ ] Save/Load/迁移通过
[ ] 失败 Provider 不影响核心功能
[ ] UI 关键路径和输入边界通过
[ ] 语言切换与长文本通过
```

## GATE-PERF — 性能

关联：PERF-001 / PERF-003

```text
[ ] Tick/高频回调不做全库扫描、反射或无界分配
[ ] UI 重绘有刷新条件或 DataRevision
[ ] 大列表/图表有上限和降级
[ ] 日志不在热路径无界增长
[ ] 失败重试有退避/熔断策略
[ ] 提供至少一份基准或可复核指标
```

## GATE-REL — 发布

关联：REL-001

```text
[ ] 版本号、游戏版本、DLC、Harmony 范围明确
[ ] Change Summary 完整
[ ] Changelog 分类准确
[ ] Breaking/Save/Migration/Compatibility 明确
[ ] 包内目录和 About.xml 正确
[ ] 依赖与加载顺序说明完整
[ ] 已知问题、回滚和支持范围明确
[ ] 所有 Exception 仍在有效期内
```

## GATE-DELTA — 变更一致性（验收定位）

关联：GOV-013 / ANNO-001 / REL-013

```text
[ ] 已生成变更标注，覆盖所有实际改动文件
[ ] 变更标注与 git diff / 实际改动逐项一致（无未列出的修改）
[ ] 变更标注范围 ⊆ Change Summary 的 Scope（无超范围/偷偷改动）
[ ] 每个改动文件标注了改动类型与涉及的符号/DefName/Key
[ ] Change ID 在 Intake / Summary / Annotation / Gate / Changelog 间一致可互链
[ ] 无遗留调试代码、注释掉的死代码、临时 TODO / 实验分支
[ ] 删除/重命名项已登记 Deprecated 或 Migration
```
失败条件：标注缺失、标注与实际 diff 不符、存在未登记的超范围改动、或删除项无迁移/废弃说明。

## Gate Record 模板

```markdown
## Gate Record
- Change: <变更名称>
- Version: <版本>
- Date: YYYY-MM-DD
- Reviewer: <人或 AI>
- Scope: <模块/文件>

| Gate | Result | Rule IDs | Evidence | Risk |
|---|---|---|---|---|
| ARCH | PASS/BLOCKED/N/A | ARC-xxx | <路径/日志> | <风险> |
| DATA | PASS/BLOCKED/N/A | DATA-xxx | <证据> | <风险> |
| LOC  | PASS/BLOCKED/N/A | LOC-xxx  | <证据> | <风险> |
| UI   | PASS/BLOCKED/N/A | UI-xxx   | <证据> | <风险> |
| COMP | PASS/BLOCKED/N/A | COMP-xxx | <证据> | <风险> |
| SAVE | PASS/BLOCKED/N/A | SAVE-xxx | <证据> | <风险> |
| TEST | PASS/BLOCKED/N/A | TEST-xxx | <证据> | <风险> |
| PERF | PASS/BLOCKED/N/A | PERF-xxx | <证据> | <风险> |
| REL  | PASS/BLOCKED/N/A | REL-xxx  | <证据> | <风险> |
| DELTA| PASS/BLOCKED/N/A | ANNO-xxx | 变更标注路径/覆盖说明 | <风险> |
```
