# 市场经济框架（MVS）· 线上经济类 Mod 架构横向解析

- 目标 Build：**1.6.4871 rev590**（`Assembly-CSharp.dll` 15,777,280 bytes）
- 取证方式：本机 workshop 全量扫描（247 目录）＋ GitHub 官方源码直读（4 个仓库已克隆到 `_research/economy-mods/`）＋ **Mono.Cecil 静态读原版元数据与 IL 体**（不加载程序集、不启动游戏）
- 范围声明：本文全部结论为**静态取证**，未做游戏内运行时验证。凡属推断已逐条标注。
- 目的：为 MVS 的通道选型、作用域、供需数据源、守卫机制提供同类产品的实证对照，并修正前期方案中的错误假设。
- 部门裁决：架构设计部六项裁决 **PASS WITH EXCEPTION**（含 A/B 两项改判）；机制研发部五项事实裁决 **需修正**（3 条已被本轮 IL 裁定，2 条由主理人证伪）。

---

## 0. 结论摘要（先行）

1. **线上确实存在同类产品，且比预想的成熟。** 四个可对照样本：Vanilla Trading Expanded（VTE，旗舰，需 Harmony）、Economics & Demography（E&D，全球模拟，需 Harmony）、Dynamic economy（分层定价，需 Harmony）、KV Supply and Demand（3 行改库存量，需 Harmony）。
2. **VTE 的官方源码是开源的**（`Vanilla-Expanded/VanillaTradingExpanded`，含 1.6 分支）。此前"VTE 闭源只能反编译"的判断不成立。
3. **四个样本全部依赖 Harmony。** 无一家做到"零 Harmony 动态定价"——这反过来印证 MVS 的 StatPart 路线是**差异化优势**，而非折中方案。
4. **重大修正一：XPath 选型无法规避 Colony Wealth 联动。** IL 证实 `WealthWatcher.CalculateWealthItems` 走 `Thing.get_MarketValue`（`MarketValue` 链），`ForceRecount` 的建筑腿走 `MarketValueIgnoreHp`。**"经济即难度"是不可回避的默认语义**，只能靠"因子有界 clamp"弱化，或上 Harmony 隔离。
5. **重大修正二：回读原值会重入，是会把游戏打崩的坑。** `Verse.ThingDef.get_BaseMarketValue()` → `GetStatValueAbstract(MarketValue)`、`RimWorld.Tradeable.get_BaseMarketValue()` → `Thing.get_MarketValue`。在 MarketValue 的 `StatPart` 内读这两者都会无限递归。**安全读法：读 `def.statBases` 原始字段，或干脆不回读。**
6. **XPath 定稿裁决：挂子节点 `defName="MarketValue"/parts`**（架构部 A 项改判）。理由：精确 XPath/最小范围（DATA-005、COMP-003，P2）优先于生态惯例（P5）；且生态惯例**不构成硬兼容约束**——我方挂其子 defName，与 Pawnmorpher/Hauts 挂父 `MarketValueBase` 属**不同节点、不同父**，无同父兄弟冲突，改道不破坏它们，所谓"兼容面"实际为零。收益：不牵连 `MarketValueIgnoreHp` 建筑财富腿，且我方 part 恒为**链尾**（语义确定），无需 `PatchOperationConditional`，无居中顺序问题。
7. **平台事实确证**：`Verse.Thing.get_MarketValue` → `StatExtension.GetStatValue(MarketValue, applyPostProcess: **true**, cacheStaleAfterTicks: **-1**)`。既证实"每次访问重算、无缓存"，也证实"StatPart 会跑"——核心假设成立。
8. **XML 继承语义确证**：`li` 一律 `AppendChild` **追加合并**（非覆盖），且顺序是"父 li 先、子 li 后"。
9. **守卫定稿裁决：`[ThreadStatic]` 深度计数，置于 `StatPart` 内，零 Harmony。** 进入 `TransformValue` 时 `++`、`finally` 时 `--`，计数 `>1` 即判自重入 → 直接返回传入 `val`。首选仍是**彻底不回读**，计数仅作防御网。
10. **"零 Harmony" 定稿句**：核心动态定价全程零 Harmony——递归防护由 StatPart 内的自重入守卫（ThreadStatic 深度计数）承担，无需任何 Patch；但该守卫**并非"模型更简单"**，而是必要的一致性措施。
11. **修正第 4 条（第二轮）——财富联动并非只有"接受"一条路。** 已 IL 证死存在**第二条正交通道**：Harmony 写 `Tradeable` 的 private `pricePlayerBuy` / `pricePlayerSell`（唯一读取者是 `GetPriceFor`；而真实成交银额 = `数量 × GetPriceFor`，见 §9.2 IL）。改这两个字段**只动成交价，完全不动 `MarketValue` 与 Colony Wealth**。代价＝必须 Harmony（2 个 Postfix，闭源竞品 Trade Price Adjuster 已证明最小可行）。
12. **通道选型定稿（架构部 GATE-ARCH：PASS WITH EXCEPTION）**：**A（StatPart，零 Harmony）为默认主体 ＋ B（写 `Tradeable` 字段）为可选开关、默认关**；EXC-002 提前登记为 **E2**（scope 仅 B，绑 1.6→1.7）。B 通道**强制**带不变量 `if (pricePlayerSell >= pricePlayerBuy) pricePlayerSell = pricePlayerBuy;`，否则会造出**无限银套利循环**。
13. **1.6 同类竞品版图**：工坊真·动态定价系统约 10 家、支持 1.6 的 9 家；其中 **Volatile Markets（3748859691）与 MVS 同架构**（`StatPart` 挂 `MarketValueBase`），已占住"零 Harmony + 财富联动"这个位 ⇒ MVS 的差异化定位定为 **"双通道可选、财富耦合可关"**。详见 §9。

---

## 1. 线上经济类 Mod 全谱

### 1.1 本机 workshop 实扫结果

扫描 247 个目录（246 个成功解析 About.xml），关键词命中仅 **3 个**：

| Workshop ID | 名称 | packageId | 形态 | 与本课题关系 |
|---|---|---|---|---|
| 2113372560 | TradeHelper | TimmyLiang.TradeHelper | DLL | 交易窗口辅助（愿望清单高亮），不碰定价 |
| 2683622537 | Trade UI Revised | hobtook.tradeui | DLL | 交易 UI 双栏重排，不碰定价 |
| 3773683722 | RSMF 补货优化与店员扩展 | jianyuan.rsmf.restockstaffexpander | DLL | 补货/店员，不碰定价 |

**VTE / E&D / Dynamic economy / KV 均未安装在本机**，故本轮无法对其 DLL 反编译，改为直读官方源码（其中三个完全开源，见 §2）。

另注：本机装有 `3768871513 巴尔：国政与邦交`（Baal.PartiesAndCurrents），其描述含"国际市场独立结算层 / 国家票券 / 实时汇率结算"，属**货币与外汇**方向而非商品定价，建议登记为 MVS 的兼容对象。

### 1.2 线上候选全谱

| Mod | Workshop | 授权/开源 | Harmony | 定价挂钩通道 | 作用域 |
|---|---|---|---|---|---|
| **Vanilla Trading Expanded** | 2785616901 | ✅ 官方开源 | 是 | `StatWorker.GetBaseValueFor` Prefix 替换 | 全局 per-ThingDef |
| **Economics & Demography** | 1.6 在维护 | ✅ 开源 | 是 | `StatWorker_MarketValue.GetValueUnfinalized` Postfix 乘算 | 全球市场 |
| **Dynamic economy**（saloid / Mlie） | 2736917779 / 2974182214 | ✅ MIT | 是 | `Tradeable.GetPriceFor` Postfix | 分层（派系/定居点/轨道船） |
| **[KV] Supply and Demand** | 1511375007 | ✅ 开源 | 是 | `StockGenerator.RandomCountOf` Postfix | 全局（只改库存量） |
| RimBank | — | ✅ 开源 | 是 | `TradeUtility.GetPricePlayerBuy` Postfix | 单物品（钞票） |
| simWorld | — | ✅ 开源 | — | 自带 Economy 目录（原版类 vendoring） | — |
| Pawnmorpher | 已上架 | ✅ 开源 | 是 | **`StatPart` + XML Patch（同 MVS 路线）** | 品类 |
| Hauts-Framework | 已上架 | ✅ 开源 | 是 | **`StatPart` + XML Patch（同 MVS 路线）** | 单物品 |

> `_research/economy-mods/` 下已克隆：`VanillaTradingExpanded`、`Dynamic-economy`、`KVSupplyAndDemand`、`rimworld-supplyanddemand`。

---

## 2. 四个样本的架构解剖

### 2.1 Vanilla Trading Expanded（旗舰，最值得学）

**定价挂钩**——替换**基值**，而非乘算结果：

```csharp
[HarmonyPatch(typeof(StatWorker), "GetBaseValueFor")]
public class StatWorker_GetBaseValueFor_Patch
{
    public static bool outputOnlyVanilla;
    private static bool Prefix(StatDef ___stat, StatRequest request, ref float __result)
    {
        if (___stat == StatDefOf.MarketValue && !outputOnlyVanilla && request.BuildableDef is ThingDef thingDef)
        {
            if (TradingManager.Instance?.TryGetModifiedPriceFor(thingDef, out var tmpResult) ?? false)
            { __result = tmpResult; return false; }   // 跳过原版
        }
        return true;
    }
}
```

**这是本次最值得学的一手**：动态价被安在**基值**位置，于是原版 `StatPart_Quality / StatPart_Health / StatPart_Age` 仍在其上正常叠加——绝对价语义 + 保留原版 parts 组合。且守卫条件 `___stat == StatDefOf.MarketValue` 是**对象比较**，因此 `MarketValueIgnoreHp` 不受影响（建筑财富不联动）。

**状态与存档**（`TradingManager : GameComponent`）：
- `Dictionary<ThingDef, float> priceModifiers` 存**绝对银值**（初值 = `GetStatValueAbstract(MarketValue)`）
- `InitVars()` 用 `??=` 给全部集合兜底 → **旧档免 Migration**
- `Scribe_Collections.Look(ref priceModifiers, "priceModifiers", LookMode.Def, LookMode.Value, ref thingDefsKeys1, ref floatValues1)` → `LookMode.Def` 需要临时 key/value 列表
- `PostLoadInit` 时 `DoCleanup()`：`priceModifiers.RemoveAll(kvp => kvp.Key is null)` → **清理"Def 被卸载导致 null key"**，存档兼容必备动作

**供需模型（交易事件驱动）**：
- 挂钩**所有 `ITrader` 实现类**（`GenTypes.AllTypes` 扫 `ITrader` 的 `GiveSoldThingToTrader/ToPlayer`）累加 `±MarketValue × count`
- 每 3 天结算：`chance = |spent| / 10f / 100f`（花 1000 银 ≈ 1% 概率）；`impact = clamp(spent/1000, 1, 10)`；`AffectPrice(def, spent<0, Rand(0.01,0.1) × impact × setting)`
- `SimulateWorldTrading()`：每 3 天对约 `settings.amountOfItemsToFluctuate` 比例的 def 做**对称随机游走**（模拟"别处也在交易"）
- `DoPriceRebalances()`：向原版基值**均值回归**，`change = Rand(0.0005, 0.005) × |price/vanillaBase|`
- `SeasonalPriceUpdates()`：冬季食物涨 / 夏季落，加权 70% 不动 / 20% ±1% / 10% ±1–3%
- 涨用 `*= 1+p`、跌用 `/= 1+p`，下限 1e-7，另有 `TryClampPriceModifierIfNeeded` 把偏离度夹进设置范围

**守卫**：静态布尔 `outputOnlyVanilla`，用于 ① 包住 `GenStep_PreciousLump.Generate` / `GenStep_ScatterLumpsMineable.ChooseThingDef`（生成期抑制）② 包住内部读原版真值处（防自递归）。

> ⚠️ **实测到两处缺陷（我们不要重演）**：
> ① `TryClampPriceModifierIfNeeded()` 在 `vanillaMarketValue <= 0` 时不复位 `outputOnlyVanilla`；
> ② `DoPriceRebalances()` 里 `AffectPrice → TryClampPriceModifierIfNeeded` 会把静态标志提前翻成 `false`，导致后续迭代读到**被污染的**价格。
> 根因：**长寿命全局可变静态标志在长流程中必然漏**。MVS 必须改用作用域对象 / `[ThreadStatic]` 计数 + `try/finally`。

**扩展面**：`BankExtension : DefModExtension`（Core / Royalty 各一份 XML Patch）、`NewsDef` + `NewsWorker`（Def 驱动事件，文本走 `GrammarResolver`，含 `priceImpactTicksDelay` 延迟生效）、`Contract`。**Def 驱动 + DefModExtension 是其可扩展性的全部来源。**

### 2.2 Economics & Demography（最"模拟向"）

```csharp
[HarmonyPatch(typeof(StatWorker_MarketValue), "GetValueUnfinalized")]
[HarmonyPostfix] static void Postfix(StatRequest req, ref float __result)
{
    if (WorldPopulationManager.WealthCalculationDepth > 0 || Patch_MapGen_Tracker.IsGeneratingMap) return;  // 最廉价判断优先
    ... __result *= mult;
}
```

- 挂在**未终结值**上乘算（早于 parts 链）
- 状态：`WorldPopulationManager : WorldComponent`，`Dictionary<ThingDef,float> globalPriceModifiers` 存**因子**，另有全局 `currentInflation`
- 缓存：`multiplierCache` + `isDirty` **手动失效**
- 供需：**库存快照驱动**——世界银总量 / 世界人口 → 通胀；per-def「世界存量 vs 均衡量」→ 供需因子；`Lerp` 平滑；clamp [0.15, 10]
- 库存来源：卖方 = 全球虚拟库存 `VirtualStockpile`（按派系，`Dictionary<string,int>` 带品质后缀）；玩家 = `map.resourceCounter.AllCountedAmounts` 且 `if (kvp.Key.Minifiable) continue;`
- 守卫：`WealthCalculationDepth`（**ThreadStatic** 深度计数，专防 wealth 递归）+ `IsGeneratingMap`
- 另用 `IncidentWorker_TraderCaravanArrival.TryExecuteWorker` 的 **Prefix `return false`** 拦截"没货不发商队"

> 学到的关键一条：**ThreadStatic 深度计数**比全局布尔安全得多，是 VTE 缺陷的正确修法。

### 2.3 Dynamic economy（分层作用域范本）

- 定价挂钩：`Tradeable.GetPriceFor` Postfix 乘算成交价；`Tradeable.GetPriceTooltip` Postfix 追加**分项明细**（`DE_LocalFactor` / `DE_OrbitalTraderRandomFactor` / `DE_PlayerPurchasesFactor` / `DE_EventFactor`）
- 记账：`TradeDeal.TryExecute` 用 `__state` 在 Prefix 抓 `___tradeables`、**Postfix 确认成交后**才记账（避免"打开窗口就算成交"）
- 作用域分层（**目前最完整的市场分层实现**）：
  - `ComplexPriceModifier`（基类）
  - `TraderCaravansPriceModifier`（by Faction）
  - `SettlementPriceModifier`（by Settlement，含 `Find.WorldGrid[tile].hilliness` 地形 + `Biome` 的 `LocalPriceModifierDefExtension` + 派系 techLevel 差惩罚）
  - `OrbitalTraderPriceModifier`（by TradeShip，随机 base 偏移，离场即 `RemoveModifierForShip`）
  - 取用入口：`GetOrCreateIfNeededComplexModifier(ITrader trader)`——`Pawn` 非本图则转 `SettlementAt(tile)`，否则按 `Faction`
- 品类维度：`ModifierCategory { None, Standalone, Group }` + `PriceModifierCategoryDefExtension`（可挂 `ThingDef` 或 `ThingCategoryDef`），`GetModifierCategoryFor` **沿 `thingCategories` 向上找父链**
- 并额外挂 `StockGenerator.RandomCountOf` 缩放商队库存

### 2.4 KV Supply and Demand（最小侵入样板）

```csharp
[HarmonyPatch(typeof(StockGenerator), "RandomCountOf")]
private static void Postfix(ref int __result, StockGenerator __instance)
{
    var num  = Settings.ScaleAdjustment * 0.0008f;
    var wealth = Find.World.PlayerWealthForStoryteller;
    var num2 = 1f - Find.Storyteller.difficulty.tradePriceFactorLoss;
    if (!Settings.ScaleVisitors && !__instance.trader.orbital && !__instance.trader.defName.Contains("Base_")) return;
    __result = (int)(num * Math.Log(Math.E + wealth * num2) * __result);
}
```
**改的是商队"库存量"而非价格**：`ln` 压缩财富 → 边际递减，难度系数参与缩放，最小值 1。**不碰 MarketValue ⇒ 零财富联动、零递归风险。** 这是一个值得保留的"低耦合选项"。

---

## 3. 本轮新增硬证据（Mono.Cecil IL 取证）

> 以下全部为对 `Assembly-CSharp.dll` 的直接 IL 取证，是本报告最有价值的部分——修正了前期方案的三处假设。

### 3.1 `Thing.MarketValue` 的实现（证实核心假设）

```
=== IL: Verse.Thing::get_MarketValue() ===
   IL_0001: ldsfld   // StatDefOf::MarketValue
   IL_0006: ldc.i4.1  →  applyPostProcess: TRUE
   IL_0007: ldc.i4.m1 →  cacheStaleAfterTicks: -1
   IL_0008: call     // StatExtension.GetStatValue
```
⇒ 两个结论同时成立：**① StatPart 会跑（applyPostProcess=true）；② 每次访问重算、无缓存（cacheStaleAfterTicks=-1）**。

### 3.2 调用链定位（含对机制研发部一处判错的裁定）

| 事实 | 证据 |
|---|---|
| `StatWorker.GetBaseValueFor(StatRequest) -> Single` 为 **[pub virt]** | `search GetBaseValueFor` |
| **`GetValueUnfinalized` 内不含 parts 循环**（`StatPart.TransformValue` 出现 **0** 次） | `il RimWorld.StatWorker GetValueUnfinalized full` |
| **`StatDef.parts` → `StatPart.TransformValue` 在 `StatWorker.FinalizeValue` 内**（出现 **1** 次） | `il RimWorld.StatWorker FinalizeValue full` |
| `StatWorker_MarketValue` **确有** override `GetValueUnfinalized`（不是绑到基类） | `type RimWorld.StatWorker_MarketValue full` |
| `StatWorker_MarketValue` 另有 `[pub static] CalculatedBaseMarketValue(BuildableDef, ThingDef)` | 同上 |

**链路定稿**：`GetValue → GetValueUnfinalized{ GetBaseValueFor } → FinalizeValue{ parts[i].TransformValue }`。
> 机制研发部原判「parts 在 `GetValueUnfinalized` 内」**已被 IL 证伪**。位置描述必须纠正，否则后续排错会找错地方。
> **另需修正一条更早的错误说法**：「品质/耐久/年龄 parts 全是乘算，乘算可交换」**不成立**——`il RimWorld.StatPart_Quality TransformValue` 里同时含 `mul` 与 `sub`/`add`（原版除 `factorAwful..factorLegendary` 乘数外，还有 `maxGainGood/maxGainExcellent/maxGainMasterwork` **加成上限**，是相对基值的加法封顶）；`StatPart_Age` 只见 `mul`。
> ⇒ **原版 parts 链里存在加法型/封顶型环节，位置不可交换。** 这有两层后果：① 把我们的 part 追加到**子** `defName="MarketValue"` 使其恒居**链尾**，是最干净的语义选择；② 因为恒居链尾，`vNow = factor × vBase` **严格成立**，比率去归一化（§9.3）对 MVS 是安全可用的——而挂父 `MarketValueBase`（part 居中）的 Volatile Markets，其源码注释 "quality and HP cancel in the ratio" 在品质带加法上限时**并不严格成立**。

**三通道语义位置**（因此不同）：
- **VTE（`GetBaseValueFor`）**：替换**基值** → 原版 parts 在其上叠加 → **绝对 base 价语义 + 保留原版组合**
- **E&D（`StatWorker_MarketValue.GetValueUnfinalized`）**：乘在**未终结值** → 早于 parts → 因子语义；且只影响 `MarketValue`（`MarketValueIgnoreHp` 走基类 `StatWorker`，不被该 override 命中）
- **MVS（`StatPart`）**：追加在 parts 链**末尾** → `TransformValue` 的 `ref float val` 进参**已含**全部原版 parts 结果

### 3.3 财富耦合：XPath 选型**无法**规避

```
=== refs MarketValueIgnoreHp ===
RimWorld.WealthWatcher::ForceRecount  ->  MarketValueIgnoreHp     ← 唯一消费点

=== il RimWorld.WealthWatcher ForceRecount ===
   IL_0096: ldsfld StatDefOf::MarketValueIgnoreHp
   IL_009D: call   StatExtension.GetStatValue     → wealthBuildings
   IL_0119: callvirt Thing.get_MarketValue        → wealthPawns 侧

=== il RimWorld.WealthWatcher CalculateWealthItems (full) ===
   IL_0078: callvirt Thing.get_MarketValue        → wealthItems
```

⇒ **财富三条腿两种都踩到**：`wealthItems` 走 `Thing.get_MarketValue`（`MarketValue` 链）；建筑财富走 `MarketValueIgnoreHp`。
**结论**：无论 XPath 选父还是子，**动态价都会进入 Colony Wealth → 袭击点**。"规避财富联动"在零 Harmony 前提下**不可得**；要隔离只有 ① Harmony 在 `WealthWatcher.ForceRecount` 加守卫（破零 Harmony），或 ② 接受语义并用**有界 clamp 因子**锁死冲击幅度。
**XPath 的实际差异**：选子 `MarketValue` 只影响物品/卒子财富腿；选父 `MarketValueBase` 会**连建筑财富一起**参与变价。

### 3.4 XML 继承语义（1.6 反编译 `Verse/XmlInheritance.cs`）

```csharp
if (IsListElement(item3)) { current.AppendChild(ImportNode(item3, deep: true)); continue; }
...
private static bool IsListElement(XmlNode node)
    => node.Name == "li" || (node.ParentNode != null && allowDuplicateNodesFieldNames.Contains(node.ParentNode.Name));
```
⇒ **`li` 一律追加合并**（非覆盖）；同名**非 li** 兄弟节点会触发 `CheckForDuplicateNodes` → `Log.Error`。
⇒ 必须**往已存在的 `<parts>` 里追加 `<li>`**，不能新增第二个 `<parts>` 节点。

**顺序规则（新增，影响选型）**：追加合并是"**父 li 先、子 li 后**"。
- 追加到**子** `defName="MarketValue"/parts` → 我方 li 排在**所有**原版 parts **之后**（链尾）
- 追加到**父** `@Name="MarketValueBase"/parts` → 我方 li 落在 `StatPart_Quality` **之后**、`Health`/`Age` **之前**（居中）

乘算下两者数值等价；**加算型 part 不等价**（生态实例：Pawnmorpher 的 `TransformValue` 是 `val += ...`）。

### 3.5 回读原值会重入（本轮最重要的安全发现）

```
=== IL: Verse.ThingDef::get_BaseMarketValue() ===
   IL_0001: ldsfld StatDefOf::MarketValue
   IL_0006: ldnull
   IL_0007: call   StatExtension.GetStatValueAbstract     ← 重入 MarketValue 本身

=== IL: RimWorld.Tradeable::get_BaseMarketValue() ===
   IL_0001: callvirt Transferable.get_AnyThing
   IL_0006: callvirt Thing.get_MarketValue                ← 重入
```

⇒ **在 `MarketValue` 的 `StatPart` 内读 `def.BaseMarketValue` 或 `tradeable.BaseMarketValue` 都会无限递归。**
（`BaseMarketValue` 不在 `Verse.BuildableDef` 上——该类型只有 `[pub] List<StatModifier> statBases`；属性定义在 `Verse.ThingDef` 与 `RimWorld.Tradeable`。）
**安全读法**：① `def.statBases`（原始字段，零重入）；② **干脆不回读**——`TransformValue` 的 `val` 进参已是含前序 parts 的累计值，只在**首次见到该 Def 时**记一次基线，之后对基线缩放。若仍要回读，须在 StatPart 内加自重入守卫（`[ThreadStatic]` 计数，**无需 Harmony**）。

### 3.6 生产环境的同类挂载实例

```xml
<!-- Tachyonite/Pawnmorpher 1.6/Patches/Core/Core_StatWorkerPatches.xml -->
<PatchOperationAdd>
  <xpath>/Defs/StatDef[@Name="MarketValueBase"]/parts</xpath>
  <value><li Class="Pawnmorph.StatWorkers.StatParts.StatPart_MarketValue"/></value>
</PatchOperationAdd>
```
```xml
<!-- LaserToothLiger/Hauts-Framework 1.6/Patches/StatWorker_MarketValue_Patches.xml -->
<PatchOperationConditional>
  <xpath>/Defs/StatDef[@Name="MarketValueBase"]/parts</xpath>
  <nomatch Class="PatchOperationAdd">   <!-- parts 不存在时创建整个 parts -->
  <match   Class="PatchOperationAdd">   <!-- 存在时追加 li -->
</PatchOperationConditional>
```
> 两家都挂**父** `MarketValueBase`。MVS **刻意改挂子** `defName="MarketValue"`（见 §0 第 6 条裁决）——不同节点、不同父，无同父兄弟冲突，互不干扰。已确证 Core 中 `defName="MarketValue"` 自带 `<parts>` 节点，故**无需** `PatchOperationConditional`。

---

## 4. 横向对照矩阵

| 维度 | VTE | E&D | Dynamic economy | KV S&D | **MVS（定稿）** |
|---|---|---|---|---|---|
| 挂钩点 | `StatWorker.GetBaseValueFor` | `StatWorker_MarketValue.GetValueUnfinalized` | `Tradeable.GetPriceFor` | `StockGenerator.RandomCountOf` | **`StatPart` + XML（零 Harmony）** |
| XPath 目标 | —（Harmony） | —（Harmony） | —（Harmony） | —（Harmony） | **`defName="MarketValue"/parts`（链尾）** |
| 语义位置 | 替换**基值** | 乘**未终结值** | 乘**成交价** | 改**库存量** | 乘 parts 链**末尾值** |
| 存什么 | 绝对银值 | 因子 | 因子（分层） | 不存 | **因子** |
| 作用域 | 全局 per-ThingDef | 全球市场 | 分层（派系/定居点/轨道） | 全局 | **v1 全局，v2 分层** |
| 供需源 | 交易事件 | 库存快照 | 交易事件 | 财富规模 | **v1 库存快照** |
| 品类维度 | 无（忽略表） | 有（原始/制造/食物三缓存） | **有（DefModExtension + 父链）** | 无 | 注册表白名单 |
| 生成期守卫 | 静态布尔包 GenStep | `IsGeneratingMap` + ThreadStatic 深度 | 无（打成交价，无风险） | 无 | **ThreadStatic 计数 + try/finally** |
| 回读原值 | 需静态开关抑制 | 不回读 | 不回读 | 不涉及 | **不回读（或读 `statBases`）** |
| 财富联动 | **不触发建筑腿**（对象比较只命中 MarketValue） | **主动屏蔽**（深度计数） | 无（只改成交价） | 不触发 | **不可规避**，用有界因子弱化 |
| 扩展面 | `BankExtension` / `NewsDef`+Worker / `Contract` | ModSettings + 虚拟库存 | `PriceModifierCategoryDefExtension` | 仅 Settings | DefModExtension + Provider 契约 |
| Harmony | 是（约 8 个补丁） | 是（数十个补丁） | 是（约 6 个补丁） | 是（1 个） | **否** |

---

## 5. 对前期 5 个待拍板项的新答案

| # | 原问题 | 新答案（含证据） |
|---|---|---|
| 1 | **财富联动**：接受"经济即难度"，还是要更保守的注册集？ | **接受，但必须弱化**。IL 已证 XPath 无法规避（§3.3）。做法：① 存**因子**而非绝对价；② 因子 **clamp 有界**（参考 E&D [0.15,10]，建议更窄，如 [0.5,2.5] 作为默认档）；③ 提供"总开关 + 强度档"设置项。**不要**试图用 XPath 差异假装解决了问题。（XPath 仍选子节点，理由是范围最小 + 链尾语义确定，不是为了躲财富） |
| 2 | **市场作用域**：全局统一，还是按定居点/派系/区域分市场？ | **v1 走全局统一**（与 VTE 一致，实现与存档最简单）；**v2 分层范本已现成** = Dynamic economy 的 `ComplexPriceModifier` 三分支 + `GetOrCreateIfNeededComplexModifier(ITrader)`。明确记录：`"未注册 return 1f"` 只够当**副作用边界**，**不够当作用域收敛**（架构部裁决）。 |
| 3 | **供需数据源**：交易事件驱动，还是地图库存快照驱动？ | **v1 走库存快照**（架构部裁决，零 Harmony 且无递归）。**硬约束**：`map.resourceCounter` 只统计 `CountAsResource==true`，**药品/衣物/武器不在内**；E&D 还额外 `if (kvp.Key.Minifiable) continue;`。⇒ **对这三类商品不得伪造库存数据**，应标"数据不足"或改用交易事件（VTE 路线）补齐。v2 可做混合。 |
| 4 | **玩家可调性**：是否提供"市场强度/波动"与"一键回退 vanilla"？ | **都做**。VTE 有 `Settings`（价格波动限制、新闻冲击倍率、交易冲击倍率、物品波动比例）、KV 有 `ScaleAdjustment` + `ScaleVisitors`、E&D 有 `updateIntervalHours`/`inflationUpdateFactor`/`priceUpdateFactor`/`enableGoldStandard`。**这些正是他们发布后能持续调参的原因**。 |
| 5 | **实时重定价底线**：v1 交易窗口开着时不实时刷新（关窗重开生效），可否接受？ | **可接受，且是行业常态**。证据：VTE 价格 **每 3 天**才结算一次、价格历史**每天**记录一次；E&D 按 `updateIntervalHours` 批量更新，用 `isDirty` 手动失效 + 自己的 `SetDirty()` 通知窗口。⇒ "非实时"完全站得住；**窗口里显式提示"价格更新于 N 天前"即可**。 |

---

## 6. 刻意背离点与"绝对不要抄"清单

### 6.1 刻意背离（6 条）

| # | 背离什么 | 为什么 | 风险 |
|---|---|---|---|
| 1 | 背离全部样本的 **Harmony 依赖** | 差异化优势 + 更好地与 CE/CAI 等多 Harmony 大户共存 | 若需"成交价级"或"按 Trader 作用域"定价，必须回退到 Harmony（退出条件） |
| 2 | 背离 VTE 的 **绝对价**存法 | 绝对价与 parts 链耦合紧，且 clamp/均值回归都要反读原值（而反读会重入） | 因子语义下"想要固定价"需重归一化换算 |
| 3 | 背离 VTE 的**全局静态布尔守卫** | 已实证会漏（§2.1 两处缺陷） | 无（纯改进） |
| 4 | 背离 VTE 的**概率化冲击**（`Rand.Chance(spent/1000)`） | 玩家无法建立因果预期，调参不可复现 | 手感"更硬"，需靠设置项给"随机性"档 |
| 5 | 背离 E&D 的**主动屏蔽财富计算** | 那需要 ThreadStatic 深度贯穿自己的全部财富代码，且仍不能屏蔽原版 `WealthWatcher` | 保留财富联动（已接受） |
| 6 | 背离生态惯例：**挂子 `defName="MarketValue"` 而非父 `MarketValueBase`** | 精确 XPath（P2）优先于生态惯例（P5）；范围最小、链尾语义确定、不牵连建筑财富腿；且与两家**不同父**，无兄弟冲突，兼容面为零 | 若第三方 Mod 改写子 Def 的 `<parts>`（罕见），我们的 part 可能被移除；用 `PatchOperationConditional` 亦无法覆盖此情形 |

### 6.2 绝对不要抄（7 条，均附证据）

1. **长寿命全局可变静态布尔做守卫** —— VTE `outputOnlyVanilla` 在 `vanillaMarketValue <= 0` 时漏复位，且被内层调用提前翻 false。改用**作用域对象 + `try/finally`** 或 **`[ThreadStatic]` 深度计数**。
2. **`StatDef.parts.Add` 运行时注入** —— `get_Worker()` 懒构造致 `parentStat=null`；不触发 `PostLoad` 的 priority 重排；无缓存可清。（前期结论，本轮未推翻）
3. **两个 `<parts>` 节点** —— `CheckForDuplicateNodes` 直接 `Log.Error`。必须在已有 `<parts>` 内追加 `<li>`。
4. **在定价挂钩内回读原值** —— **实测会重入**（§3.5）。`def.BaseMarketValue` / `tradeable.BaseMarketValue` / `GetStatValue(MarketValue)` 三条路都会无限递归。安全读法：`def.statBases` 原始字段，或干脆不回读。
5. **把 ModSettings 的静态字段当存档状态** —— 三方均把运行时状态放 `GameComponent`/`WorldComponent`，Settings 只放参数。
6. **`Tradeable` 价格字段直改** —— private 且一次算完即缓存（`InitPriceDataIfNeeded`），改不动。
7. **改 `TradeUtility.PriceFactorBuy_Global / PriceFactorSell_Global / MinimumBuyPrice / MinimumSellPrice` 四个 public static 字段** —— 1.6 全库零引用，改了完全无效（死钩子，已用 `refs` 实证）。

### 6.3 值得直接照搬（5 条）

1. **`InitVars()` 用 `??=` 兜底全部集合** → 旧档免 Migration（VTE）。
2. **`Scribe.mode == LoadSaveMode.PostLoadInit` 时 `RemoveAll(kvp => kvp.Key is null)`** → 清理 Def 被卸载留下的 null key（VTE）。
3. **Def 驱动 + `DefModExtension` 的扩展面**：`BankExtension`、`PriceModifierCategoryDefExtension`（可挂 ThingDef 或 ThingCategoryDef 并沿父链回溯）、`NewsDef`+`NewsWorker`（含延迟生效字段）。
4. **成交记账放在 `TradeDeal.TryExecute` 的 Prefix 抓 + Postfix 确认**（Dynamic economy），避免"开了窗口没成交也记账"。
5. **守卫用 `[ThreadStatic]` 深度计数 + `try/finally`**（E&D 范式，替代 VTE 的静态布尔）。

---

## 7. 残余风险与未验证项

| 项 | 状态 |
|---|---|
| 部门裁决 | 架构设计部六项裁决（含 A/B 改判）**已回传**；机制研发部五项事实裁决**已回传**，其中「parts 在 GetValueUnfinalized 内」经 IL 证伪、「`MarketValueIgnoreHp` 读取点未验证」由主理人补验为 `WealthWatcher.ForceRecount`、「`BaseMarketValue` 可能重入」经 IL 证实并升级为高优先级安全项 |
| `StatPart` 追加在链尾时 `val` 进参的**精确语义** | 由机制部确认为「base + 前序 parts 累计值」；与我的 IL 结构判断一致。**仍未做游戏内验证** |
| `StatDef.PostLoad` 的 priority 重排方向 | 未逐字确认（不影响当前设计——我们是 XML 追加，不走运行时 add） |
| 三方 Mod 是否消费 `RandomPriceFactorSeed` | 未查（原版零引用已实证） |
| Dynamic economy / E&D 的实际运行时手感与 TPS 影响 | 未测（无本机安装） |
| VTE 的 `priceModifiers` 在长档中的漂移幅度 | 未测（README 自述"可能大幅偏离初始值"，并给了 dev 重置命令） |
| 第三方 Mod 改写 `defName="MarketValue"` 的 `<parts>` 导致我方 part 被移除 | **本方案新增风险**（选子节点的代价）。若发生，表现为"动态定价静默失效"；建议加启动期自检日志 |
| `3768871513 巴尔：国政与邦交` 的汇率层与 MVS 的潜在冲突 | 未评估，建议登记为兼容对象 |

---

## 8. 附：创意工坊经济类 Mod 清单（2026-09-17 线上检索）

检索方式：Steam Workshop 检索（`app_id=294100`），关键词 `economy` / `market price` / `supply demand` / `trade` / `wealth` / `经济 交易 市场`，按订阅量排序。**共命中 8480 项，下表为人工筛出的有意义条目**（已剔除纯主题换皮/道具包等同名误命中）。

### A 类 · 动态定价 / 供需市场系统（与 MVS 直接同类，优先研究）

| 名称 | ID | 订阅 | 版本 | 与 MVS 的关系 |
|---|---|---|---|---|
| **Vanilla Trading Expanded** | 2785616901 | **403,204** | 1.6/1.5/1.4 | 旗舰。波动价 + 银行/贷款/股票/新闻/合约；`StatWorker.GetBaseValueFor` 替换基值 |
| **Economics & Demography** | 3692156692 | 10,799 | 1.6 | 世界模拟：人口 + 全球供需 + 通胀通缩；`GetValueUnfinalized` 乘因子 |
| **[KV] Supply and Demand** | 1511375007 / 3290701666 | 7,724 / 2,918 | — | 按 `ln(财富×难度)` 缩放**商队库存量**，不碰价（最小侵入样板） |
| **Dynamic economy** | 2736917779 / 2974182214 | 1,422 / 1,840 | 1.3 / 1.6-1.3 | 分层地方价（派系/定居点/轨道船）+ 脑矿；`Tradeable.GetPriceFor` |
| **RimCryptoEconomy** | 3471141456 | 473 | 1.6 | 电力挖币，币价随机波动（单一商品的价格波动实现） |
| **Volatile Markets** ★新 | 3748859691 | 360 | 1.6 | 1.6 新作：价格随供需涨跌，"钢锭第一年与第十五年同价"的解法 |
| **RSMF- Quality Pricing** ★新 | 3766403833 | 387 | 1.6 | 品质感知定价，**运行时补丁不改框架本体**；可作 `StatPart` 之外的对照实现 |
| **Dynamic Market System** ★新 | 3522100861 | 105 | 1.6 | 定居点短缺/过剩直接影响价格与可售库存（Odyssey 旅行导向） |
| **Trade Price Adjuster** ★新 | 3765245029 | 41 | 1.6 | **改买卖价而不动 market value** —— 与我们"想把成交价与估值分离"的诉求同向，值得读实现 |
| **Kapitalist's Pay Them All** | 3592525342 | 498 | 1.6 | "全方位经济改造"，体量小，可看其侧重 |

### B 类 · 交易流程 / 商队 / 库存量（改"量"不改"价"，MVS 的相邻层）

| 名称 | ID | 订阅 | 备注 |
|---|---|---|---|
| Dynamic Trade Interface | 3020706506 | **182,811** | 高性能可定制交易界面（Trade UI Revised 的继任者） |
| Trade UI Revised | 2683622537 | **152,417** | 双栏交易界面（本机已装） |
| Traders Have Money | 2898006725 | 41,297 | 商人白银；已重构过价格逻辑 |
| More Trade Ships | 2006960340 | 35,695 | 提高轨道商船频率 |
| Better Traders | 2012774042 | 52,978 | 提升商人库存与白银 |
| [KV] Call Trade Ships | 2878346405 / 3513064727 | 22,662 / 24,873 | 主动叫商船（原作 / 1.6 补丁） |
| Better Orbital Traders | 3009866854 | 18,381 | 轨道商人整体重平衡 |
| Let's Trade! | 2373633407 | 14,644 | 派系间贸易扩展 |
| Universal Trade Hub | 3281403886 / 3527300742 | 11,916 / 873 | 随时下单；1.6 临时重编译版 |
| Realistic Trade | 2945931715 | 2,388 | 商队受行程距离/天气/财富/关系影响 |
| [DHM]BLACK MARKET - TradeNetwork | 3727592467 | 1,412 | 玩家间异步交易（RimWorld Together 生态） |
| Orbital Market | 3571541064 | 1,038 | 随时向轨道商人上架资源 |
| Wealth-Scaling Orbital Traders | 3116021482 | 465 | **Harmony 实现**：按财富放大轨道商人库存 |
| Configurable Trader Wealth | 3581590352 | 316 | 可调白银/库存/库存种数倍率 |
| Trade Broadcast | 3736915801 | 419 | 通讯台 24/7 广播"开放贸易" |
| Universal Trade Buying（通用贸易收购） | 3704317264 | 464 | 所有商人收购所有有价值物品 + 白银倍率（中文作者） |

### C 类 · 价格显示与单点修正（QoL；MVS 的 UI 参照 + 潜在冲突面）

| 名称 | ID | 订阅 | 备注 |
|---|---|---|---|
| The Price Is Right | 1498070208 | 16,969 | 商队成交价更接近市价（原版只有 2% 加成） |
| Weight price | 2363887111 | 12,394 | 显示"价格/重量" |
| The Price is What? | 2353882085 | 9,548 | 屏幕角显示市价挂件 |
| Trade Price Fix | 2937957652 | 3,993 | 修 1.3+ 交易议价上限失效 |
| Trade Price Improvement Makes Sense Now | 3008531948 | 2,200 | 重定义议价 100% 的实际含义 |
| UnCap TradePriceImprovement / No Max Value… | 2902808113 / 3226278439 | 560 / 1,146 | 解除议价上限 |
| Full Gun Sell Price | 1575464750 | 20,032 | 取消武器出售折价 |
| Marketable Craftsmanship | 3713340324 | 6,338 | 手工艺品生产与出售扩展 |
| Static Market Price | 2673314921 | 419 | 冻结市价（与动态定价**理念相反**，是潜在冲突面） |
| Balanced Floors Market Value | 2624663277 | 958 | 地板市价重平衡 |
| Market Value Filter / Overlay / Inspect | 2960434569 / 3346858002 / 3795480604 | 241 / 168 / 91 | 市价筛选/叠加层/检视面板 |

### D 类 · 财富调节（**MVS 的关键兼容面**：因动态价经 Colony Wealth 传导，这批 Mod 直接作用在同一条链上）

| 名称 | ID | 订阅 | 备注 |
|---|---|---|---|
| Visible Wealth | 3461137081 | **127,493** | 财富构成可视化（可用来验证 MVS 对财富的实际影响） |
| Wealth Display (Continued) | 3298960397 | 8,774 | HUD 显示财富 |
| Hide Your Wealth | 3236501970 | 6,008 | 扩容但不抬财富 |
| Delayed Wealth（战利品不纳入总财富） | 3577409644 | 4,033 | 战利品延迟计入财富 |
| Bionic Wealth | 3120453035 | 4,090 | 仿生体财富重平衡 |
| Wealth Tweaks | 3785545243 | 194 | 逐类目调物品/衣物/武器/建筑/卒子的财富贡献 |
| Animal Wealth Adjuster | 3662813689 | 62 | 单独调动物财富 |
| Patch: No Shelf Price | 3048270821 | 125 | 货架不计入财富 |

> **给 MVS 的两个结论**：① D 类这批 Mod 证明"财富被市价推高"是社区公认痛点，MVS 的有界 clamp 因子方向正确；② 它们也是最可能的**数值冲突源**——发布说明里应提示"MVS 会经 `MarketValue`/`MarketValueIgnoreHp` 影响财富，与财富调节类 Mod 叠加时建议逐项核对"。

### E 类 · 中文作者 / 中文社区条目

| 名称 | ID | 订阅 | 备注 |
|---|---|---|---|
| **巴尔：国政与邦交** | 3768871513 | — | 国际市场独立结算层 + 国家票券 + 实时汇率（**本机已装**，需登记为 MVS 兼容对象） |
| 星际贸易商 Interstellar trader | 2779368956 | 3,560 | 多种轨道贸易商，携带白银更多 |
| rubbish trader 垃圾贸易商 | 2878493515 | 833 | 回收垃圾的商船/商队 |
| Universal Trade Buying 通用贸易收购 | 3704317264 | 464 | 见 B 类 |
| 交易书籍折叠 Trade Book Groups | 3797399882 | 33 | **明确声明"不改书籍、价格或存档"**，是交易 UI 侧的良好兼容示范 |
| RWT Search / Quantity Patch（联机市场） | 3774377786 / 3777648626 | 9 / 5 | RimWorld Together 联机市场侧补丁 |

### F 类 · 同名但非市场系统（避免误判，列出以示已甄别）

`Fuel Economy`（1498067736，运输舱燃料）、`Human Milk Economy`（3695284513，生产链）、`Trade Goods`（2955535058，换皮）、`Trading Economy Mod 3.1`（1130945311，仅 1.0 已停更）、`Economy Bandaid`（962813905，0.17 已停更）、`Stationhouse/Office/Diner Supplies`（道具包，同名误命中）。

**版本分布提示**：A 类中真正支持 **1.6** 的只有 VTE、E&D、Dynamic economy (Continued)、RimCryptoEconomy、Volatile Markets、RSMF Quality Pricing、Dynamic Market System、Trade Price Adjuster、Kapitalist's Pay Them All 这几家；KV Supply and Demand 的最后更新止于 1.3，实际 1.6 可用性存疑。**MVS 在 1.6 的同类竞品数量远少于订阅量给人的印象。**

---

## 9. 工坊 5 家同类 Mod 下载 + 反编译架构解析（2026-09-17 第二轮）

### 9.1 获取方式与样本

用 Steam Workshop 下载工具（steamcmd 匿名登录）取到 `D:/toolbox/steamcmd/steamapps/workshop/content/294100/` 下 5 家。**其中 4 家自带完整 C# 源码**（比反编译拿到的东西更准），只有 1 家闭源需反编译。

| Mod | ID | 1.6 | 源码 | Harmony | 定价通道 | 是否动财富 |
|---|---|---|---|---|---|---|
| **Volatile Markets** | 3748859691 | ✅ | ✅ 自带 | 是 | `StatPart` 挂 `MarketValueBase` | **是** |
| **Market Modifiers**（Dynamic Market System） | 3522100861 | ✅ | ✅ 自带 | 是 | 写 `Tradeable` 私有价格字段 | **否** |
| **Trade Price Adjuster** | 3765245029 | ✅ | ❌ 已反编译 | 是 | 写 `Tradeable` 私有价格字段 | **否** |
| **RSMF 品质定价** | 3766403833 | ✅ | ✅ 自带 | 是（+硬依赖第三方框架） | 改第三方商店框架的定价方法 | 否（不涉原版） |
| **Kapitalist's Pay Them All** | 3592525342 | ✅ | ✅ 自带（367 文件） | 是（+HugsLib） | 未细读 | — |

> 全部 5 家依赖 `brrainz.harmony`。**加上上一轮的 VTE / E&D / Dynamic economy / KV S&D，已解析的 9 家同类产品无一零 Harmony。**

### 9.2 结论先行：市场上存在**两条正交的定价通道**，且都能改到真实成交额

```
通道 A（StatPart 家族：VTE / E&D / Volatile Markets / MVS 原方案）
  GetStatValue(MarketValue) → FinalizeValue → StatPart.TransformValue  →  val ×= 因子
  优势：零 Harmony（MVS）/ 一处改动全局生效（含物品栏显示、财富、敌人生成）
  代价：MarketValue 同时是 Colony Wealth 的输入 ⇒「经济即难度」不可分

通道 B（Tradeable 字段家族：Trade Price Adjuster / Market Modifiers / Dynamic economy）
  Tradeable.InitPriceDataIfNeeded(Prefix/Postfix) → 写 ___pricePlayerBuy / ___pricePlayerSell
  优势：只改"卖给你/从你买"的价，MarketValue 与 Colony Wealth 完全不动
  代价：必须 Harmony；只管交易窗口与商队成交，不管物品栏里的"价值"显示
```

**通道 B 的真实性已用 IL 证死**（本轮新增，`Assembly-CSharp.dll` 1.6.4871 rev590）：

```
=== IL: RimWorld.Tradeable::GetPriceFor(TradeAction action) ===
   IL_0001: call   Tradeable.InitPriceDataIfNeeded
   IL_0007: ldc.i4.1                                  ← 1 = TradeAction.PlayerBuys
   IL_0008: bne.un.s  IL_0011
   IL_000B: ldfld  Tradeable::pricePlayerBuy   → ret   ← 唯一读取点
   IL_0011: ldfld  Tradeable::pricePlayerSell  → ret

=== refs pricePlayerBuy / pricePlayerSell ===
写：Tradeable..ctor（初始化）、Tradeable.InitPriceDataIfNeeded（计算，各 5 处）
读：Tradeable.GetPriceFor（各 1 处）—— 别无他人

=== IL: RimWorld.Tradeable::get_CurTotalCurrencyCostForDestination() ===
   IL_000F: call   Transferable.get_CountToTransferToDestination
   IL_001C: callvirt Tradeable.GetPriceFor        ← 注意是 callvirt（虚派发生效）
   IL_0021: mul
同构：get_CurTotalCurrencyCostForSource = CountToTransferToSource × GetPriceFor

=== IL: RimWorld.StatWorker.FinalizeValue ===
   ldfld StatDef::parts → callvirt StatPart.TransformValue   ← 通道 A 的注入点在此
```

⇒ **三条硬结论**：
1. `GetPriceFor` 是 `pricePlayerBuy`/`pricePlayerSell` 的**唯一读取者**，而这两个字段是 `[private]`；用 Harmony 的 `___字段名` 注入改写即可改成交价。
2. **真实成交银额 = `数量 × GetPriceFor`**（`CurTotalCurrencyCostFor{Source,Destination}`），且走 `callvirt`，虚派生的改动同样生效。⇒ 通道 B **不是"只改显示"的假通道**。
3. 通道 A 的注入点确在 `StatWorker.FinalizeValue`（本轮由 Volatile Markets 源码注释独立佐证：*"Runs in FinalizeValue for every market-value evaluation"*）。

### 9.3 Volatile Markets（3748859691）——与 MVS 同通道的直接竞品，值得逐条学

**它是目前 1.6 上与我们架构最接近的产品**：`StatPart_MarketEvent : StatPart` 挂 `MarketValueBase`、乘因子、带 `ExplanationPart` tooltip。

**它的 XML 注释独立佐证了我们在 §3.4 用 IL 得到的继承结论**：
> *"StatDef `<parts>` lists are inherited by append - so this one operation reaches exactly those two stats and no others, without disturbing their existing parts (Quality / Health / Age)."*

**它也明确承认财富联动**（写在自己的 Commodities.xml 注释里）：
> *"A price event raises or lowers the MARKET VALUE of everything a commodity matches, which flows into trade prices **AND colony wealth / raid threat** (via the StatPart on MarketValueBase)."*

**比我们方案更成熟、值得照搬的四处**：

| 模式 | 它怎么做 | 价值 |
|---|---|---|
| **`CommodityDef : Def` 注册表** | 三轴定位 `stuffCategories` / `thingDefs` / `thingCategories`（用 `ContainedInThisOrDescendant` 含后代）；`eventCommonality` 权重 | 比"注册表白名单"更可扩展：第三方 Mod 直接写 `<CommodityDef>` 即可接入，无需我方代码 |
| **`AffectedDefs` 惰性 `HashSet<ThingDef>`** | 首次访问时遍历 `DefDatabase<ThingDef>` 建集合 | StatPart 内 O(1) 命中；配合首行 `if (modifiers.Count == 0 ...) return 1f;`，把"绝大多数情况"压成一次计数判断 |
| **存档版本戳** | `public const int CurrentSaveVersion = 1`，注释写明 *"LOCKED from first release. Bump it and add a step in MigrateIfNeeded()"* | 正是 GATE-SAVE 要的纪律，比 VTE 的"靠 `??=` 兜底"更进一步 |
| **比率去归一化** | 先抑制事件读 `vBase`，再读 `vNow`，`ratio = vNow/vBase`；注释 *"quality and HP cancel in the ratio, so it caches per def+stuff"* | 给 tooltip / Read Model 提供"这东西现在受几个点影响"而**不必反读原值**——正好绕开 §3.5 的重入坑 |

**它做错、我们已否决的一处**：`public static bool SuppressForBaseline` 全局静态布尔做基线抑制。虽然它比 VTE 规范（`try/finally` 成对复位），但仍是**进程级静态**——若抑制窗口内被其他 Mod 或线程读一次 `MarketValue`，就会拿到错误基线。我们已定的 `[ThreadStatic]` 深度计数是正确的升级。

**数值与调度参数（可直接作为 MVS v1 的起点基准）**：

| 档位 | 权重 | 涨幅 | 跌幅 | 时长 |
|---|---|---|---|---|
| Minor | 0.6 | 0.10~0.20 | 0.10~0.20 | 14~28 天 |
| Major | 0.3 | 0.30~0.40 | 0.30~0.40 | 28~56 天 |
| Severe | 0.1 | 0.80~1.00 | 0.50~0.70 | 56~112 天 |

- 幅度语义＝**绝对分数**：涨 0.20 → ×1.20，跌 0.20 → ×0.80（注意与 VTE 的 `/= 1+p` 倒数做法不同；VM 是线性加减）
- 调度：`mtbDays 8` / `maxConcurrentEvents 8` / `minDaysBeforeFirst 5`，即"平均每局 8 天一次事件，同时最多 8 种商品在事件中"
- **无全局 clamp**（靠 Def 的 range 天然有界，实际因子区间 ≈ [0.30, 2.00]）——比我们建议的默认 [0.5, 2.5] 更宽
- 事件走**自己的 MTB 调度**（`MarketSchedulerDef`），不占讲故事的 Misc 抽奖；另留 `IncidentDef` 且 `baseChance=0` 仅供 dev 菜单触发
- 价格事件信件用 `LetterDefOf.NeutralEvent`（涨跌对玩家好坏取决于持仓，判定正确）

### 9.4 Trade Price Adjuster（3765245029）——通道 B 的最小实现，7 条提前退出

闭源，已反编译。**全场只有 2 个补丁**：`Tradeable.InitPriceDataIfNeeded` + `Tradeable.GetPriceTooltip`。

**"一次且仅一次"的乘算守卫**（本节最易读错之处，按原始 IL 逐字对照）：
```
Prefix:  __state = !(___pricePlayerBuy > 0f)     ← 取字段、比 0、取反、存 __state
Postfix: IL_0000 ldarg.3(__state)
         IL_0001 brtrue.s IL_0004                ← 真则跳去继续执行
         IL_0003 ret                             ← 否则立刻返回
         ⇒ if (__state) 继续; else return;       ← 注意极性：__state == true 才继续
```
语义：`__state == true` ⇔ "`pricePlayerBuy` 仍是构造函数留下的初值（**-1f**），vanilla 这一趟刚把它算出来"。所以**只在首次初始化那一趟乘算**；此后 `pricePlayerBuy > 0` ⇒ `__state == false` ⇒ 立即返回，**不会重复叠乘**。
> **勘误（重要）**：我第一轮转述时把极性写成了 `if (__state) return`，导致机制研发部据此推断"必然每帧叠乘、疑漏一个 `!`"。经复核——**模组无 bug，是我的转述错误**。记录在此，避免后续有人按错误描述去"修"它。

Postfix 的提前退出链（按 IL 顺序，值得逐条学——全部退在最便宜的位置）：
1. `__state == false`（价格已初始化）→ 返回
2. `TradeSession.Active && TradeSession.giftMode` → 返回（礼物模式不改价）
3. `__instance.IsCurrency` → 返回（白银本身不定制价）
4. `!__instance.HasAnyThing` → 返回
5. `ThingDef == null` → 返回
6. `Store.TryGet(defName, out entry)` 未命中 → 返回（**未注册即零开销**，与我们的注册表 gate 同构）
7. 命中后才写两个字段：`___pricePlayerBuy = ApplyBuy(___pricePlayerBuy, entry.buy)`、`___pricePlayerSell = ApplySell(___pricePlayerSell, entry.sell)`

**写完字段之后的最后一步，是一条 MVS 必须照抄的不变量**（Postfix 尾部 IL_0063~006D，我第一轮截断漏掉了）：
```
IL_0063: ldarg.2 (___pricePlayerSell)
IL_0065: ldarg.1 (___pricePlayerBuy)
IL_0067: blt.un.s IL_006D (ret)      ← 若 sell < buy 直接结束
IL_0069-006C: ___pricePlayerSell = ___pricePlayerBuy
```
⇒ **`if (pricePlayerSell >= pricePlayerBuy) pricePlayerSell = pricePlayerBuy;`** —— **卖出价不得高于买入价**。
没有这道闸，动态定价会造出"低价买入 → 高价卖回"的**无限银套利循环**。通道 A 改的是 MarketValue（买卖两侧同源），天然不存在该问题；**通道 B 改的是分方向的买/卖价，必须显式加闸**。对照：Market Modifiers 未做同类保护，是它的一个空档。

配套设计：
- `PriceMath`：`ApplyBuy = Finish(base × mult, floor **0.5f**)`；`ApplySell = Finish(base × mult, floor **0.01f**)` —— **分方向地板**，避免价格归零导致交易逻辑异常
- `PriceEntry` 是 **`struct`**：`buy`/`sell` + `static MinMult/MaxMult` + `static readonly Neutral` 哨兵 + `Clamp(v)`
- `PriceMultiplierStore` = `Dictionary<**string**, PriceEntry>`，**以 `defName` 字符串为键**；`ToLines()` / `LoadFromLines(List<string>, Action<string> onBadLine)` **行式序列化进 ModSettings，容错坏行**
- 另有 `Dialog_ModSourcePicker` + `ModSourceSearch`：按 mod 来源筛物品来配价

> **对照我们原方案的三点修正意见**：① 键用 `defName` 字符串而非 `ThingDef` 引用 —— 存档里不出现 Def 引用，Def 被卸载/改名时**只是查不到、不会产生 null key**，比 VTE 的 `LookMode.Def` + `PostLoadInit` 清 null 更省迁移成本；② 因子表用 struct + `Clamp` 自带上下界，比裸 float 更安全；③ 分方向地板（买/卖各一条）是 MVS 目前没考虑的边界。

### 9.5 Market Modifiers / Dynamic Market System（3522100861）——`playerAware` 信息迷雾

- 状态放 **`WorldComponent_MarketSystem : WorldComponent`**（世界级，不是 `GameComponent`）——市场是"世界"的属性而非"某个存档局"的属性，这个分层判断值得注意
- `MarketModifier : IExposable`：`settlement` + `modifierType(Shortage/Overproduction)` + `targetType(ThingDef/ThingCategoryDef)` + `ticksUntilExpiry` + **`playerAware`** + `appliedToCurrentStock`
- 常量：`ShortagePriceMultiplier 1.5` / `OverproductionPriceMultiplier 0.7` / `TradeVolumeToAffectModifier 1000` / `ChanceToEndModifierPer5kValue 0.15` / `ChanceToGenerateModifier 0.3` / 默认时长 10 天
- 补丁共 6 处：`Tradeable.InitPriceDataIfNeeded`、`Tradeable.GetPriceTooltip`、`Settlement_TraderTracker.RegenerateStock`、`TradeSession.SetupWith`、`GiveSoldThingToPlayer/ToTrader`、`Settlement.GetInspectString`
- **`playerAware`** 是最有意思的设计：价格修饰存在但**玩家"发现"后才生效**（配合 `Settlement.GetInspectString` 展示）。相当于给经济加了信息迷雾——MVS 目前完全没有这个概念。

### 9.6 RSMF 品质定价（3766403833）——不碰原版，纯第三方框架扩展

中文作者（"边缘模拟经营框架-品质定价"），硬依赖 `chezhou.Framework.RimSimManagementFramework`。它改的是第三方商店框架的 9 个方法（`ShopPricingUtility.GetUnitPrice` / `CustomerPriceUtility.GetReferenceMarketValue` / `Building_SimContainer.TryVirtualBuy` / `LordJob_CustomerVisit.AddCartItem` / `GetCartItemsEstimatedValue` / `VendingMachineUtility.TryPurchaseBestItem` / `CustomerPurchaseDeliveryUtility.DeliverPurchasedItems` / `ShopDataUtility.ReturnCartItemsToShop` 等）。

**对我们有两点参考价值**：
1. **它用 `ThreadStatic` 在补丁间传递中间值**（`lastUnitPriceMult` / `pendingQuality` / `pendingBilledUnitPrice` / `pendingReturnPawnId`），并且**消费后立即置回默认值**（`QualityPricingUtility.lastUnitPriceMult = 1f;`）。这是社区里对"跨补丁传值"的通行解法，也正面印证我们要选的 `[ThreadStatic]` 路线；其"消费即复位"纪律正是 VTE 静态布尔缺陷的解药。
2. **反面**：调试日志硬编码中文（`"定价 " + ... + "：数量 " + ...`），违反 GOV-009。虽然只是 `DebugLog`，但 MVS 必须从一开始就全 Keyed，不给"以后再说"留口子。

### 9.7 部门裁决定稿（2026-09-17 第二轮）

**架构设计部（岳立基）GATE-ARCH：PASS WITH EXCEPTION —— 架构升级为「双通道」**

| # | 议题 | 裁决 |
|---|---|---|
| 1 | **通道分叉（核心）** | **A 通道（StatPart，零 Harmony）为默认主体 ＋ B 通道（写两个 `Tradeable` private 字段，2 个 Postfix）为可选开关、默认关**；**EXC-002 提前登记为 E2**（scope 仅 B，有效期绑 1.6→1.7 升级）。B 定位＝**兼容隔离档，非核心**，失败必须降级回 A。与同样改 `Tradeable` 的 Mod 共存须 `[HarmonyPriority]` / `[HarmonyAfter]` / `[HarmonyBefore]` 防叠乘 |
| 2 | **存储** | **权威因子表保留 `GameComponent + Scribe`（LookMode.Def/Value）**——运行时因子是会话状态，须带版本经 Scribe；字符串键会在 Def 改名/卸载时破坏别名与迁移；行式 ModSettings 无 schema 版本、无 Migration。**但用户配置（B 通道价格倍率）采 C 家 line-ModSettings + `LoadFromLines(…, onBadLine)` 容错坏行**——配置容忍缺失 Def 才正确。抄 A 的 `CurrentSaveVersion` const + `MigrateIfNeeded()`；缓存可重建、不入档，修饰集变动即 `Clear()`。**v1 用 `GameComponent`**，登记晋升触发器：出现定居点/商队级修饰再升 `WorldComponent`（不预升） |
| 3 | **注册表** | **采纳 `CommodityDef : Def` 取代白名单**（原生 Def/XML 是最低风险档）。三轴照搬（`ContainedInThisOrDescendant` 已证实存在）。**品质 / statBases 属"修饰条件"而非"定位轴"**，不塞进 CommodityDef 以防 Def 膨胀，改用可选谓词字段 / DefModExtension。命中集是缓存，Def 加载或 mod 表变化时必须重建 |
| 4 | **必抄 / 禁抄** | 见 §9.7.1 |
| 5 | **`playerAware`** | **纳入 v1，默认开、可关；只作用于价格通道与交易呈现，不得进入财富耦合通道**——财富没有"信息迷雾"，未发现而财富已变会产生不可解释的袭击点波动。flag 进 Save，仅门控呈现与成交价，不改权威因子计算 |
| 6 | **竞争定位（定稿句）** | Volatile Markets 已占住"零 Harmony + 财富联动"这个位，同架构正面撞车**无差异化**；**MVS 定位为「双通道可选、财富耦合可关」——零 Harmony 默认通道为体，可选隔热通道为差异化，财富联动是默认可控变量而非硬约束。** |

**GATE-ARCH 自检**：层级依赖 PASS（B 通道落 Compatibility，Core 不认识 `Tradeable`）；无循环 PASS；Core 不反向依赖 PASS。
**触发 Architecture Review**：Harmony = Yes、Static Mutable State = Yes、Save schema 变更 ⇒ 需 ARC-011 + GATE-COMP + GATE-SAVE。
**判定依据**：ARC-008/011、GOV-007/009/010、AI-003/004、DATA-001/002/005/006/007、SAVE-001/003/004/005、COMP-004/005/009、PERF。

**架构部的刻意背离清单（4 条）**

| # | 背离什么 | 为什么 | 风险与缓解 |
|---|---|---|---|
| 1 | 背离「先用原生扩展点」与原定零 Harmony（启用 B 通道） | XML 层无路径改成交价 | 与同样改 `Tradeable` 的 Mod 抢字段/叠乘；1.6 字段变更 ⇒ 缓解：EXC-002、HarmonyPriority/After/Before、能力探测、失败回退 A |
| 2 | 背离 A 家的 `static bool` 守卫，改用 `[ThreadStatic]` 作用域对象 | ARC-008 | 需保证 try-finally 与嵌套/多线程正确 |
| 3 | 背离"财富联动不可控"（A/VTE/E&D 都默认接受） | 差异化所在 | 双通道语义易混淆 ⇒ 缓解：UI/文档 + 诊断面板 |
| 4 | 背离 E 家的裸 `ThreadStatic`，升格为作用域对象 | 泄漏风险 | 实现复杂度上升 |

#### 9.7.1 必抄 / 禁抄（架构部定稿）

**必抄**
- **A 家**：惰性 `HashSet<ThingDef>` 实现 O(1) 命中；`CurrentSaveVersion` + `MigrateIfNeeded()`；缓存 `Clear()` 失效；热路径首行早退 `modifiers.Count == 0 || def == null → 1f`；`PostLoadInit` 清失效 Def；`ExplanationPart` tooltip（走 Keyed）
- **C 家**：2 补丁最小面 + 完整提前退出链；`PriceMath` 的 clamp ＋ `struct PriceEntry` ＋ `static readonly Neutral` 哨兵
- **A 家的比率去归一化：纳入** tooltip / Read Model，按 `def|stuff` 缓存。**对 MVS 严格成立**（子节点 ⇒ 链尾 ⇒ `vNow = factor × vBase`）；A 家因挂父节点、part 居中，其 "quality and HP cancel in the ratio" 注释在品质带加法上限时**并不严格成立**
- **通道 B 的强制不变量**：`if (pricePlayerSell >= pricePlayerBuy) pricePlayerSell = pricePlayerBuy;` —— **卖出价不得高于买入价**（§9.4 尾部 IL 实证）
- **E 家的"消费即重置" ThreadStatic 纪律**：升格采用，但升级为作用域对象 / try-finally，不学裸 ThreadStatic

**禁抄**
- **A 家的 `public static bool SuppressForBaseline`** —— **定性为"它做错了"**（违 ARC-008：无界、跨线程可变静态）。坚持我方 `[ThreadStatic]` + 作用域对象
- **A 家的 `MarketEventTracker.Current` 静态缓存** —— 最多只允许"只读 Registry + 明确标注生命周期"
- **A 家的无全局 clamp** —— Severe 档涨幅可达 +100%（×2.0）；财富可被推向极端，MVS 必须加 Def 配置化上下界
- **E 家的硬编码中文调试日志** —— 违 GOV-009/010；虽是 `DebugLog` 也不留口子
- **任何完整覆写 Def / 全局 Key 替换** —— 违 AI-004

---

## 10. 可行性裁决：派系背景驱动的稳定供需市场（2026-09-17 第三轮）

> 用户命题：「能否**根据派系背景**拟定**稳定的供需市场**，**奠定库存与贸易基础**？**先检查思路的可行性通过后再拟定方案**」。
> 本轮为**可行性门禁**，架构设计部（岳立基）+ 机制研发部（洪源清）并行论证，主理人用 Mono.Cecil 对账取证。**未出设计。**

### 10.1 裁决结论

**可行，但需修正口径（PASS WITH EXCEPTION）。**

路线本身走得通，但「派系背景」必须从 **"per-faction 成交价"** 收窄为 **"派系背景 → 决定该派系的货构 / 画像 / 库存 → 汇聚为全球供需因子"**。零 Harmony 的 A 通道承载"真实成交价下的 per-faction 差异化"：**不可行**。

### 10.2 七条判据（架构部裁决 + 主理人 IL 对账）

| # | 判据 | 裁决 | 关键依据 |
|---|---|---|---|
| ① | **per-faction × A 通道**（最大风险） | **不可行（纯 A）**，取混合形态 | IL：`RimWorld.StatRequest` **确有** `Faction` 字段/属性，但 `For(Thing)` 与 `For(Thing, Pawn)` 用 `initobj` 清空结构体后**只填** `thingInt`/`defInt`/`stuffDefInt`/`qualityCategoryInt`（`For(Thing,Pawn)` 另填 `pawn`），**从不填 `faction`**；唯一填 faction 的重载是 `For(RoyalTitleDef, Faction, Pawn)`。而 `Thing.get_MarketValue` 走的是 `StatRequest.For(thing)` ⇒ **MarketValue 路径上 `req.Faction` 恒为 null**。且 `StatRequest` 是可空 struct 按值传递，Harmony 后缀也改不到调用方副本。⇒ 派系差异化定价只剩 B 通道 |
| ② | **库存数据可得性** | **只能自建虚拟库存**，但**有可用锚点** | `ResourceCounter` 仅 `Dictionary<ThingDef,int> countedAmounts`，且 `ThingDef.CountAsResource` 是**属性**（`get_CountAsResource()`）——只有它为 true 的物品才被统计，**药品/衣物/武器不在内**（确认盲区）。可用锚点：`Settlement_TraderTracker` 暴露 `[public] List<Thing> StockListForReading`、`RegenerateStockEveryDays`、`NextRestockTick`、`RestockedSinceLastVisit` ⇒ 定居点商人库存**可读**；`StockGenerator` 有 **13 个子类**（`_Tag`/`_Category`/`SingleDef`/`MultiDef`/`_Techprints`/`_MiscItems`/`_Animals`…）⇒ 可**只读推导**"某 TraderKind 卖什么"，用于初始化与货构画像 |
| ③ | **稳定性如何被证明** | **先验可论证 + 离线仿真可验** | 判据：因子 clamp 有界；存在均值回归项 `α(基线−因子)`；单 tick Δ 上限；不变量 `sell ≤ buy`；闭环增益 < 1（无正反馈）；同 tick 无套利；存读档 bit 幂等。**须实测项**＝长期收敛性与无振荡（建议纯 C# 离线仿真，不需游戏内测试） |
| ④ | **存档可行性** | **可行，成本低**（新组件无旧档负担） | 键用 `Faction.loadID`(int) + `FactionDef.defName`(string) 双键，**不用 `LookMode.Reference`**（防派系被消灭后悬空）。但 **原版没有"派系消亡事件"**：`FactionManager` 只有 `[private] allFactions` / `toRemove` 列表，`Faction.defeated` 是 `[public]` **字段** ⇒ 只能靠 `PostLoadInit` / 周期性**对账轮询**，不能指望回调 |
| ⑤ | **兼容性** | **可行**（Def 驱动，零硬编码） | 原版**没有** `FactionDefExtension` 类型（`list FactionDefExtension` 空），但 `Verse.Def :: [pub] List<DefModExtension> modExtensions` 是**所有 Def 的公共字段** ⇒ 我们自定义 DefModExtension 挂 `FactionDef`，第三方 **XML-only 即可接入**。未挂载时按 `techLevel` 映射缺省档（确定、有限、无随机、不含第三方名） |
| ⑥ | **与既有定稿一致性** | **不推翻，仅执行既定晋升触发器** | 唯一改判：权威因子表 `GameComponent` → **`WorldComponent`**（per-faction 状态需跨图，正好命中上一轮预留的晋升触发器）。B 通道默认关；若走 per-faction 价则须允许启用 |
| ⑦ | **先例与差异化** | **无直接对标** | 9 家同类无一零 Harmony；最接近的是 Market Modifiers（per-Settlement 修饰 + `playerAware` 信息迷雾）。增量＝零 Harmony A 通道 + Def 驱动派系原型 + 显式稳定判据；风险＝零 Harmony 下派系分层价覆盖不到真实成交，能力弱于自建交易型先例 |

### 10.3 MVP 形态（架构部）

全球统一供需因子（A 通道 `StatPart`，链尾，零 Harmony）＋ `CommodityDef` 三轴注册表 ＋ `FactionEconomyExtension`（自定义 `DefModExtension` 挂 `FactionDef`）＋ `WorldComponent` 权威因子与自建库存（**采样重建 + 弹性 + 均值回归 + clamp**）＋ 行式 ModSettings。**无 UI。**

### 10.4 门槛条件（全过才进设计）

1. **用户确认口径**：接受"派系背景只作用于库存/货构/画像，价格按全球供需"的**弱口径**，还是要求派系差异化定价（须启用 B 通道 Harmony）。
2. ~~机制部 IL 核实~~ ⇒ **主理人已全部清完**，见 §10.5。
3. 数值设计部确认"库存守恒 / 弹性无失真爆炸"（离线仿真通过）。

### 10.5 本轮新增 IL 取证结果（主理人，1.6.4871 rev590）

| 取证项 | 结果 |
|---|---|
| `RimWorld.StatRequest` 成员 | `[private] Faction faction` + `Faction Faction { get; }` **存在** |
| `StatRequest.For` 重载 | `For(Thing)` / `For(Thing, Pawn)` / `For(BuildableDef, ThingDef, QualityCategory)` / `For(AbilityDef, Pawn)` / `For(RoyalTitleDef, Faction, Pawn)` / `ForEmpty()` |
| `For(Thing)` IL | 只写 `thingInt`、`defInt`(= `thing.def`)、`stuffDefInt`(= `thing.Stuff`)、`qualityCategoryInt`(=`QualityUtility.TryGetQuality`)；**`faction` 与 `pawn` 均未写** |
| `FactionDefExtension` | **不存在**（需自定义） |
| `Verse.Def.modExtensions` | `[pub] List<DefModExtension>` **存在** ⇒ 任意 Def 可挂扩展 |
| `Settlement_TraderTracker` | `[private] ThingOwner<Thing> stock`；`[public] List<Thing> StockListForReading`；`RegenerateStockEveryDays` / `NextRestockTick` / `RestockedSinceLastVisit` |
| `StockGenerator` 子类 | 13 个（`_Tag` `_Category` `_SingleDef` `_MultiDef` `_Techprints` `_MiscItems` `_Animals` `_BuyTradeTag` `_BuySingleDef` `_BuySlaves` `_BuyExpensiveSimple` `_Slaves` `_ReinforcedBarrels`） |
| `TraderKindDef.priceFactors` | **已移除**（`fieldsearch` 空，与 1.6 变更一致） |
| `ThingDef.CountAsResource` | 是**属性**（`get_CountAsResource()`），非字段 |
| `ResourceCounter` | `Dictionary<ThingDef,int> countedAmounts`、`Silver`、`AllCountedAmounts` |
| `Faction.defeated` | `[pub] Boolean` **字段** |
| `FactionManager` | `AllFactionsListForReading` 等遍历入口；**无"派系消亡"事件** |

### 10.6 勘误（重要，推翻我方前两轮的一处断言）

前两轮我写的 **"`StatRequest` 无 Map/Game/Faction ⇒ 唯一实现是 Core 反读 UI，违 ARC-002"** —— **其中"无 Faction"是错的**。`RimWorld.StatRequest` 明确有 `Faction` 字段与属性。

正确的表述是：**`StatRequest` 有 Faction 槽位，但在 MarketValue 路径上没有任何调用点为它填值**（`Thing.get_MarketValue` → `StatRequest.For(thing)` 不填 faction；唯一填 faction 的重载是给 `RoyalTitleDef` 用的）。

**实际结论不变**（通道 A 看不到交易对手派系），但**理由必须改写**——这条一直是立项论证的承重梁，写错理由会在后续设计里导出错误的绕行方案。

### 10.7 架构部新增 P0 的处理

架构部提出："`StatPart` 执行期能否安全访问 `Current.Game` / `Find.World`？它在 Def 加载期 / World 生成期 / tooltip 期各时机是否已就绪？"

**答：不能假定就绪，必须全部 null-guard。** 已有两条硬证据：
1. `WealthWatcher.ForceRecount(**Boolean allowDuringInit**)` —— 原版为"初始化期也要重算财富"专门留了这个参数，而 ForceRecount 会读 `MarketValueIgnoreHp` 与 `Thing.get_MarketValue` ⇒ **MarketValue 在初始化期就被读**。
2. Vanilla Trading Expanded 专门打了 `ResetMarketValue_Patch` 包住 `GenStep_PreciousLump.Generate` 与 `GenStep_ScatterLumpsMineable.ChooseThingDef`（生成期抑制）——如果地图生成期不读 MarketValue，这个补丁没有存在理由 ⇒ **地图生成期确实读**。

⇒ 结论：StatPart 会在"组件尚未建立 / 世界尚未生成"的时机被调用，**"只读自存权威表"必须先过 `Current.Game != null && Find.World != null && 组件已就绪` 三道门，未就绪一律 `return`（不改 `val`）**。这与既定的"未注册/未初始化一律 `return 1f`"是同一道闸，但**闸门条件现在从 2 个增加到 4 个**。

---

## 附：可复现取证命令

```bash
# 1) 本机 workshop 全量扫描（ID → 名称 → packageId → 是否含 DLL）
python _tmp_ws_scan.py

# 2) 源码直读（已克隆到 _research/economy-mods/）
git clone --depth 1 https://github.com/Vanilla-Expanded/VanillaTradingExpanded.git
git clone --depth 1 https://github.com/definitelyNotSaloid/Dynamic-economy
git clone --depth 1 https://github.com/KiameV/rimworld-supplyanddemand
git clone --depth 1 https://github.com/emipa606/KVSupplyAndDemand

# 3) 原版程序集静态取证（Mono.Cecil，不加载程序集）
cd C:/Users/Administrator/rwcec
DLL="E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed/Assembly-CSharp.dll"
dotnet run --project Dumper.csproj -- "$DLL" refs MarketValueIgnoreHp
dotnet run --project Dumper.csproj -- "$DLL" il Verse.Thing get_MarketValue
dotnet run --project Dumper.csproj -- "$DLL" il Verse.ThingDef get_BaseMarketValue
dotnet run --project Dumper.csproj -- "$DLL" il RimWorld.Tradeable get_BaseMarketValue
dotnet run --project Dumper.csproj -- "$DLL" il RimWorld.StatWorker FinalizeValue full
dotnet run --project Dumper.csproj -- "$DLL" il RimWorld.WealthWatcher CalculateWealthItems full
dotnet run --project Dumper.csproj -- "$DLL" type RimWorld.StatWorker_MarketValue full
dotnet run --project Dumper.csproj -- "$DLL" fieldsearch statBases
```
