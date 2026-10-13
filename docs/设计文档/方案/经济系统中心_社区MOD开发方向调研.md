# 经济系统中心：社区 MOD 开发方向调研

> 目的：在拟定「经济系统中心 SDD」前，摸清创意工坊上经济类 MOD **已经在做什么、做到哪一步、踩过什么坑**，
> 从而确认我们的差异化定位，并直接吸收其架构经验。
>
> 证据分级（本工程沿用「优先一手来源」原则）：
> - **【一手-源码】** 本地已反编译源码，逐行核实
> - **【一手-官方页】** Steam 创意工坊官方页面原文（作者自述）
> - **【二手-存疑】** 仅见于内容农场站点，**未经官方页验证**，只作线索不作依据
>
> 核查日期：2026-09-15

---

## 一、结论速览

| 判断 | 结论 |
|---|---|
| 社区经济 MOD 有几条路线？ | **四条**：交易 QoL / 市场金融机制 / 实体店铺经营 / 生产物流 |
| 做「市场金融」的谁最强？ | **Vanilla Trading Expanded（VTE）**，唯一做到完整金融系统。**但该 MOD 已被 Steam 移除** |
| 做「实体经营」的谁最强？ | **RimSimManagementFramework（RSMF）**，本地已装且已反编译（443 个 .cs） |
| 做「殖民地经济观测与记账」的？ | **几乎没有** —— 这正是我们上一轮定位的空白区 |
| 最大的共同教训 | **财富 ↔ 袭击点数联动是所有经济 MOD 的死穴**（VTE 官方原文三次警示） |

---

## 二、四条开发方向全景

| 方向 | 代表 MOD | 改造深度 | 是否改原版机制 | 证据等级 |
|---|---|---|---|---|
| A. 交易 UI / QoL | TradeHelper、Trade UI Revised、[KV] Trading Spot、Better Tradable Items、Wealthy Traders | 浅 | 否（仅 UI/XML） | 一手（前两个本地已装） |
| B. 市场金融机制 | **Vanilla Trading Expanded** | **极深** | **是（改 MarketValue）** | 一手-官方页 |
| C. 实体店铺经营 | **RimSimManagementFramework + 菜篮子/RSMF 扩展包** | 深 | 部分（新增实体，不侵入原版） | **一手-源码** |
| D. 生产 / 物流（经济上游） | Project RimFactory、LWM's Deep Storage、Colony Manager | 中 | 新增 | 二手-存疑 |

**分布洞察**：做 A（UI 便利）的最多，做 C（经营模拟）的有成熟框架，做 B（金融）的**只有 VTE 一根独苗且已下架**。
**没有人做「殖民地产能—库存—财富的观测与记账」**。

---

## 三、【一手-官方页】深度案例：Vanilla Trading Expanded（VTE）

- Steam ID `2785616901`，作者 **Oskar Potocki（设计/美术/XML）+ Taranchuk（程序）**
- 订阅 **402,978** / 收藏 **17,760** / 标签 1.4-1.6
- ⚠️ **现状：已被 Steam 移除**（页面提示「由于违反《Steam 社区及内容指引》，该物品已被移除……此物品与 RimWorld 不兼容」）。
  侧栏显示 **39 个创意工坊项目依赖本 MOD** → 该生态位出现空缺。

### 3.1 功能矩阵（官方原文提炼）

| 子系统 | 内容 |
|---|---|
| 动态价格 / 供需 | 全物品价格随时间波动，模拟其他派系买卖；**每年尝试重新平衡**；玩家过量买卖也会改价，且 **按成交银两规模缩放**（越贵影响越大） |
| Squeeze / Crash | 短期涨跌超 **20%** 触发 squeeze，之后必有 crash；反向同理 |
| 行星新闻网络 | 新闻影响品类价格（某定居点备战 → 武器涨价；丰收 → 食物跌价）。玩家可据此预测、择高抛售 |
| 银行 Bank | Civil Outlander 银行 + Imperial 银行（需 Royalty）。存取均按比例收费，**同盟关系免手续费**。**支持 modder 添加更多银行** |
| 股票市场 | 随机生成轨道贸易公司，股价**只受新闻影响**；作者自述 "Stock market is a casino only affected by the news" |
| 贷款 Loans | 短期高息 / 长期按银行费率计息。**逾期 → 关系降至敌对 + 派袭击威胁 + 日息累积 + 存款自动扣抵** |
| 合同 Contracts | 匿名方高价收购（官方举例：人皮按 300% 价值收购）；玩家也可自发布采购合同，出价越高越可能有人接单 |

### 3.2 ★ 三条「财富 ↔ 袭击」官方原文警示（**对本 SDD 最重要**）

> 1. "**Please note: this change affects Market Value, which means fluctuating prices directly affect your wealth.**"
> 2. "**Good thing about the bank is that the silver no longer counts towards your colony wealth** - so if you're getting raids of enormous size, perhaps deposit some money!"
> 3. "**Shares you own don't count towards your wealth**, so noone will raid you for your portfolio."
>    （FAQ 补强：*"No, you can safely store your money in the bank or in stocks **to lower the raid sizes**."*）

**解读**：
- 第 1 条是**风险** —— 一旦让价格/账目影响 `MarketValue`，就会顺着 `WealthWatcher` 传导到袭击点数，
  这是原版经济 MOD 无法回避的结构性耦合。
- 第 2、3 条是 VTE 的**应对**，但本质是把「银行存款 / 股票」做成 **不计入财富的财富黑洞**，
  等于给玩家一个**合法逃课袭击点数**的后门。作者甚至把它当作卖点宣传。
- ⇒ **我们绝不能重蹈覆辙**。经济中心的账本必须是**纯观测层**：只读 `wealthWatcher`，**绝不反写 `MarketValue`**。

### 3.3 作者自己划的能力边界（可直接写进 SDD 的「非目标」）

> "**this is not meant to be a realistic representation of the economy** - I am not an economist, and I also don't want it to be realistic.
> It's **simplified and almost arcadey**, because it's a video game"
>
> "**If you expected an absolute economy simulation, you're in the wrong place.**"
>
> （被质疑不真实时回应）"It's a backwater planet with **shady, mob-run economy**."

⇒ 连最强的金融 MOD 都主动放弃"真实经济模拟"。我们做**观测与记账**而非**市场模拟**，与社区头部认知一致。

### 3.4 其他可借鉴点

- **中途加档兼容**：官方明确 "Yes, definitely"（对应我们的 GATE-SAVE 要求）。
- **可扩展银行**：`It's possible for modders to add more banks` → Def 化扩展点（同 RSMF 的 Def+WorkerClass 思路）。
- **与其他供需/银行 MOD 共存**：作者认为 "I think it should"（未做实测，仅作者判断）。

---

## 四、【一手-源码】深度案例：RimSimManagementFramework（RSMF）

- 本地已装（ID `3736621496`），作者「追踪虫 and Migua」，Harmony ID `com.Chezhou.simmanagementlib`
- **已反编译 443 个 .cs / 16 个命名空间**，源码在 `_research/decomp/simmgmt/SimManagementLib/`
- 配套扩展（本地已装）：菜篮子工程 `3680328274`、RSMF 顾客包 `3773552837`、店员扩展 `3773683722`、货柜补丁 `3773555083`

### 4.1 它到底是什么

**「零售店铺模拟经营框架」，不是通用经济引擎。**
以 `Zone_Shop`（商店区划）为容器，串联：货柜 → 收银台 → 员工岗位 → 顾客 AI → 账务流水 → 口碑评分，
并通过 `Api` 层对外扩展。

**明确没有的东西**（纠正可能的高估）：
- **无抽象账户** —— 货币直接用原版 `ThingDefOf.Silver`
- **无经济周期 / 市场波动** —— 需求侧只有 `ShopTuningDef` 静态权重 + EMA 口碑
- **无复式记账 / 资产负债表** —— 只有 `FinanceBillRecord` + `ShopFinanceState` 两层

### 4.2 核心数据模型（源码实证）

| 概念 | 类 | 位置 |
|---|---|---|
| 商店 | `Zone_Shop : Zone` | `SimZone/Zone_Shop.cs:19` |
| 货柜 / 收银台 | `Building_SimContainer`、`Building_CashRegister` | `SimThingClass/` |
| 顾客访问 | `LordJob_CustomerVisit`、`CustomerVisitSession` | `SimAI/CustomerVisit/` |
| 顾客种类 | `CustomerKindDef : Def`（预算/偏好/价格敏感度） | `SimDef/CustomerKindDef.cs:12` |
| 购物车 / 账单 | `CustomerCartItem`、`FinanceBillRecord`、`FinanceLineItem` | `Pojo/` |
| 财务总账 | `GameComponent_ShopFinanceManager` | `GameComp/GameComponent_ShopFinanceManager.cs:15` |
| 口碑 / 评分 | `GameComponent_ShopAnalyticsManager` | `GameComp/` |

**资金事务模型（严谨，值得抄）** —— `ThingComp_CashStorage` 用**预留 → 提交 → 回滚**二阶段：
```csharp
// SimThingComp/ThingComp_CashStorage.cs:47
public int AvailableForWithdraw => Math.Max(0, this.storedSilver - this.pendingWithdrawSilver);
```

**定价三级回退**（`Tool/ShopPricingUtility.cs:13`）：配置价 → 实物 `MarketValue` → `thingDef.BaseMarketValue`。

**顾客价格敏感度**（`Tool/CustomerPriceUtility.cs:76`）值得单独记：
`ratio = 售价/市价`；≤1 折扣区权重升至 1.35；1~1.5 缓降；1.5~2.5 降至 0.05；**>2.5 拒买**。

### 4.3 架构优点 TOP 5（建议直接吸收）

1. **`ShopTuningDef` 全参数外置** —— 约 **90 个**权重全部进 XML（评分权重、需求曲线、EMA 系数、容量、满意度常量），调平衡**零重编译**。数据驱动性价比最高的实践。
2. **多层缓存 + 预算节流**（性能设计扎实）：
   - `Zone_Shop` 运行时缓存 **61 tick**；设施缓存脏标记
   - `ProcessMetricsBudget(map, 64, 8)` —— 每 tick 最多评 **64 格 + 8 个设施**
   - `WorkGiverThingQueryCache` **2048 条** Reach/Reserve 结果缓存
   - `WorkGiver_RestockMegaStorage` 用 `NonScanJob` **替代全图扫描**
   - 补货重试 `now + 30` tick；顾客到达检查默认 **500 tick** 间隔
3. **四层扩展点**：① Def + `workerClass`（`ShopServiceDef` / `ShopFinanceLineTypeDef` / `ShopStaffRoleDef` / `ShopUiPageDef`）② 运行时注册表（`SimShopCheckoutApi` / `SimShopCustomerApi` / `SimShopOrderApi`）③ 事件总线（`SimShopEvents`，18 个静态事件，全部 `try/catch` 隔离）④ 目录注册（`GoodsCatalog.RegisterCategory`）。并声明 `SimShopApi.ApiVersion = "1.0"`。
4. **存档治理**：`TrimBillRecordsIfNeeded()` 按上限（默认 2000，钳制 200–50000）淘汰旧账单，**防存档无限膨胀**。
5. **Harmony 极克制**：全库仅 **15 条补丁 / 11 文件**，且几乎全是防御型（顾客不被判敌对、可开门、Gravship 兼容），**不侵入原版核心经济循环**。

### 4.4 缺陷与隐患（本工程红线对照）

| 缺陷 | 实证 | 对应本工程红线 |
|---|---|---|
| **硬编码中文** | 443 个文件中 **94 个（21%）**含中文，约 **1176 处**字面量，含玩家可见 UI 与设置项（如 `SimManagementLibMod.cs:73` `"启用顾客行程调试日志"`） | 违反「C# 出中文即 P1」+ 双语硬约束 |
| **反射写原版私有字段** | `Zone_Shop.cs:1020` `AccessTools.Field(typeof(ZoneManager), "zoneGrid")`、`:1023` `Zone.cellsShuffled` | 原版改字段名即静默崩溃 |
| **Def 名字符串硬编码** | `Zone_Shop.cs:592` `defName == "RestockMegaStorage"`、`:620` `GetNamedSilentFail("Restocking")` | 违反零硬编码 |
| **利润被截断** | `GameComponent_ShopFinanceManager.cs:508` `profit = Mathf.Max(0f, paidSilver - totalCost)` → **亏损永远记为 0** | 财务数据失真，绝不能学 |
| **UI 巨型类** | `MainTabWindow_BusinessManager` 约 **5900 行**、`Dialog_ShopManager` 约 **2600 行** | 违反单一职责 |
| **职责越界** | 内置 LLM 客户端（`CustomerReviewAiClient`）、蓝图/公告联网，`SimManagementLibSettings.cs:511` 硬编码 `https://api.openai.com/v1` | 与「经营框架」职责耦合 |

---

## 五、【一手】本地已装的两个轻量交易 MOD

| MOD | ID | 方向 | 说明 |
|---|---|---|---|
| **TradeHelper** | 2113372560 | 交易 UI QoL | 愿望单（商人持有目标物时整行高亮）、买入/卖出清单、**明确不改机制**（"only tweaks the Trading/Caravan UI"）、支持旧档、需 Harmony、需屏幕宽 ≥1300px |
| **Trade UI Revised** | 2683622537 | 交易 UI 布局 | 双列布局，区分「殖民地有 / 商人有」，显示谁在交易、花多少钱 |

两者都是**纯 UI 层**，佐证：社区对交易系统的痛点集中在**信息呈现**，而非机制。
（注意：本地「菜篮子工程 Shopping Basket Program」名字像经济 MOD，实为**纯 XML 食材/作物内容包**，非经济机制。）

---

## 六、信息可信度声明（重要）

本轮检索命中大量 **内容农场站点**（`eatsip365.com` / `eathealthy365.com` 系列），其 MOD 描述**存在明显失真**，例如：

- 称 "Let's Trade!" 是「从世界地图查看城镇库存与白银」的 UI 工具；
  但可查证的第三方目录（`catalogue.smods.ru`）显示其真实功能是 **"adds 30 new traders"（增加 30 种新商人）**，且为 B18 时代产物。
- 反复提及的 "Dynamic Trade" / "Dynamic Trade Routes"（商人记忆、卖多降价）**未能在任何 Steam 官方页面验证**，
  很可能是对 VTE 特性的混淆或虚构。

⇒ **本报告中只有标注【一手】的部分可作为 SDD 依据**；上表中的 [KV] Trading Spot、More Faction Interaction、
Dynamic Diplomacy、Project RimFactory 等列为**待验证线索**，未采信其具体功能描述。

---

## 七、空白区与定位建议

### 7.1 社区生态位地图

| 谁在做 | 做什么 | 空白 |
|---|---|---|
| VTE（已下架） | 市场金融：价格、银行、股票、贷款、合同 | — |
| RSMF | 实体经营：店铺、顾客、员工、账务 | — |
| TradeHelper 等 | 交易 UI 信息呈现 | — |
| **无人** | **殖民地产能 → 库存 → 财富的观测、记账与趋势分析** | **← 我们的位置** |

VTE 下架后，**金融机制方向出现 40 万订阅量的空缺**，且 39 个下游项目受影响。
但**不建议我们去补这个缺**——VTE 的教训（财富联动、复杂金融、作者自己都说"是赌场"）表明这条路线风险高、收益不确定。

### 7.2 对 SDD 的四条硬建议

1. **账本必须是纯观测层，绝不反写 `MarketValue`。**
   依据：VTE 官方原文「fluctuating prices directly affect your wealth」。
   我们的经济中心只读 `wealthWatcher` / `listerThings` / `ResourceCounter`，**永不修改任何影响财富的值**。
   这一条应写成 SDD 的**架构红线**。

2. **不要重蹈 VTE 的「财富黑洞」覆辙。**
   VTE 把存款/股票做成不计入财富的避风港，实质是给玩家逃课袭击点数。
   我们若引入任何"资产栏位"，必须明确其与原版财富的关系，并在 UI 上如实标注。

3. **吸收 RSMF 的三项工程实践**：
   - 调参全量外置（对标 `ShopTuningDef`，我们应做 `EconomyTuningDef`）
   - 缓存 + 预算节流（对标 `ProcessMetricsBudget(64,8)`、61 tick 缓存、`NonScanJob`）
   - 四层扩展点 + `ApiVersion` 声明 + 外部 Worker 全 `try/catch`

4. **严守本工程红线，规避 RSMF 的两大硬伤**：
   零硬编码中文（它是 21% 文件含中文）、禁止反射写原版私有字段。
   另：**绝不用 `Mathf.Max(0f, profit)`** —— 亏损必须在账本里如实呈现。

### 7.3 立项前新增的两个待决问题

1. **是否要复刻 VTE 的「合同 / 贷款」这类需求侧玩法？**
   （若是，则需评估与 `Faction.Notify_PlayerTraded`、好感、袭击的关系，且属于机制改造而非观测层）
2. **经济中心是否要为 VTE 这类 MOD 提供适配读数？**
   （VTE 已下架但存量用户巨大；若适配，需设计成**软依赖 + 能力探测**，符合本工程 COMP 门禁）

---

## 附：可复现核查路径

```bash
# 1. 本地 247 个 MOD 建立名称索引
for d in /e/SteamLibrary/steamapps/workshop/content/294100/*/; do
  id=$(basename "$d"); n=$(grep -m1 -oP '(?<=<name>)[^<]+' "$d"About/About.xml 2>/dev/null)
  [ -n "$n" ] && echo "$id|$n"
done

# 2. 筛经济相关
grep -iE "econom|trade|market|price|silver|money|bank|stock" <索引文件>

# 3. RSMF 源码（本地已反编译）
_research/decomp/simmgmt/SimManagementLib/   # 443 个 .cs
```
