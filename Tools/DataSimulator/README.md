# 数据模拟工具（Tools/DataSimulator）

离线模拟职业系统数据管线，快速进行数据测试（不启动 RimWorld、零 npm 依赖、Node.js ≥ 18）。

需求与设计：`docs/设计文档/功能模块/数据模拟工具/数据模拟工具需求与设计.md`（Change Intent + SDD，含管线映射与公式转写表）。

## 快速开始

```bash
node run.js                     # 跑全部预置场景 → out/<时间戳>/（含交互式 HTML 报告）
node run.js --open              # 跑完后自动用默认浏览器打开 HTML 报告
node run.js --list              # 列出场景
node run.js --scenario precision-grind
node run.js --selftest          # 公式金样自测（对齐 C# 转写表）
node run.js --out ./my-out      # 指定输出目录
node run.js --doc <文档目录>    # 生成后同步一份「职业数据模拟报告.html」到指定目录（如 docs/设计文档/功能模块/职业档案）
```

## 交互式 HTML 报告（report.html）

零依赖、`file://` 直接打开，浏览器端渲染：

- **场景 tab** 切换（precision-grind / quality-strategy / direction-compare / qualification-gap）
- **pawn 多选对比**：勾选/取消 pawn，曲线实时叠加/移除
- **指标切换**：等级 / XP / 熟练度 / 速度加成 / 品质偏置
- **悬停 tooltip**：任意数据点显示（制造次数、指标值）；评级阶梯图显示档位
- **能力雷达图**：每 pawn 最终能力 XP 占比
- **职称时间线**：各档资格达成/缺口（原因）可视化 + 里程碑列表

## 场景

| 场景 | 内容 | 用途 |
|---|---|---|
| `precision-grind` | 单 pawn 精密制造 500 次（品质 mix） | 基准成长曲线/里程碑 |
| `quality-strategy` | 全 Normal / 全 Excellent / 全 Legendary 对比 | 品质策略对成长速度影响 |
| `direction-compare` | 4 方向（含 P2-A §7.1 蓝图数据）同条件对比 | 验证差异化特化点 |
| `qualification-gap` | 论文/答辩 已实现 vs 未实现（P9）对比 | 资格可达性 / P1-5 可视化 |

## 输出

- `report.html` — **交互式可视化**（场景切换 / pawn 多选对比 / 指标切换 / 悬停 tooltip / 能力雷达 / 职称时间线），`--open` 自动打开
- `report.csv` — 逐 tick 快照（Excel 分析）
- 控制台摘要 — 最终等级/评级/加成/职称序列/资格缺口原因

## 开发者调试 UI（数据初始化 + 行为模拟）

打开 `docs/UI预览/人物档案视窗/职业档案Tab预览.html`：

- **📊 数据初始化** → 空白原版殖民者（无职业档案数据，显示空态）
- **🧪 数据模拟** → 制作物品（**221 种原版可制作物品** + 16 条配方，批量 1~1000）/ 建造（建筑可选）/ 研究 / 著书
- **评价模式开关** → 考试/论文答辩 通过·未通过（调试资格缺口）；**勋章判定**（阈值+成就）实时刷新勋章墙；**履历分段**（每 100 次一段）
- 自检：URL 加 `?simtest=1` 无头回归（初始化 → 批量制作 → 评价缺口 → 勋章 → 输出结果）

重新生成浏览器数据（Defs 或原版制造数据变更后）：

```bash
node src/gen-data.js --rimworld "E:\SteamLibrary\steamapps\common\RimWorld"
```

> 桥接脚本：`Tools/DataSimulator/dev/`（sim-core UMD + defs-data + recipes-data + sim-bridge）；模拟逻辑与游戏 C# 公式一致（白名单配方才涨 XP）。

## 管线（对齐游戏架构）

```
行为模拟 → 事实层(CareerEvent) → 状态层(XP/能力/等级) → 评级 → 效果加成
        → 评价层(资格/成就/考试评分) → 授予层(职称) → 输出
```

公式与 C# 逐条对齐（`ProfessionalXpEvaluator` / `ProfessionalRatingEvaluator` / `ProfessionalEffectResolver` / `QualificationEvaluator` / `ExamScoring` / `AchievementEvaluator`）；数据源实时读取 `Defs/*.xml`（缺失时回退内置数据并告警）。

## 约束

- 模拟"数据层"而非"引擎层"：无 job/工时/派系/存档模拟，行为序列由场景配置驱动
- 不替代 NUnit 单测：单测验证函数正确性，模拟器验证管线时序与数据调参
- C# 公式改动后，先跑 `--selftest` 与场景对照，防转写漂移
