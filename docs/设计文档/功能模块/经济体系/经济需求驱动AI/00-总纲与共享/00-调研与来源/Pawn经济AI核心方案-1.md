# Pawn 经济 AI 核心方案

## 1. 状态输入

读取 Pawn 当前状态：

- 生理状态
- 心理状态
- 健康状态
- 装备状态
- 个人资产
- 钱包余额
- 当前收入
- 周边商品

---

## 2. 动机变量

建立核心驱动力：

`UtilityGap`

表示：当前生活状态与期望状态之间的差距。

可拆为：

- 生存
- 舒适
- 娱乐
- 安全
- 地位
- 财富

---

## 3. 目标生成

根据最高缺口生成目标：

- 获得食物
- 购买衣物
- 改善娱乐
- 购买家具
- 储蓄
- 出售资产
- 获得收入

---

## 4. 候选行为

一个目标可以对应多种方案：

- 购买
- 使用公共物品
- 自己制作
- 出售旧物
- 储蓄等待
- 继续工作
- 放弃需求

---

## 5. 决策评分

统一计算：

```text
行为评分
=
效用收益
× 紧迫度
× 个体偏好
-
价格成本
-
时间成本
-
风险成本
```

选择得分最高的行为。

---

## 6. 个人经济

每个 Pawn 增加：

```text
PawnEconomyTracker

Wallet
Income
Expense
NetWorth
DesiredGoods
```

形成：

```text
工作
→ 收入
→ 钱包
→ 消费
```

---

## 7. 所有权系统

物品划分：

```text
Public      公共资产
Personal    私人资产
Reserved    已预订资产
ForSale     待售资产
```

第一阶段只让这些东西支持私人产权：

- 货币
- 衣服
- 武器
- 饰品
- 奢侈品
- 消费品

原材料继续保持公共所有。

---

## 8. AI 行为执行

接入 RimWorld 原有：

```text
ThinkTree
↓
JobGiver
↓
Job
↓
JobDriver
```

新增：

`JobGiver_EconomicAction`

负责：

- Buy
- Sell
- Save
- Consume
- Acquire

不重写整个 Pawn AI。

---

## 9. 行为反馈

行为完成后重新计算：

- Wallet
- Ownership
- Needs
- UtilityGap

示例：

```text
娱乐不足
→ 想买电视
→ 钱不足
→ 储蓄
→ 工作获得收入
→ 买电视
→ 成为私人资产
→ 娱乐改善
→ UtilityGap下降
```

---

## 最小闭环

```text
Pawn状态
↓
UtilityGap
↓
经济目标
↓
候选行为
↓
价值判断
↓
工作 / 储蓄 / 买卖
↓
所有权变化
↓
消费
↓
效用变化
↓
重新决策
```

## 第一阶段核心模块

```text
PawnEconomyTracker
UtilityEvaluator
GoalGenerator
DecisionEvaluator
Wallet
OwnershipTracker
JobGiver_EconomicAction
```

完成以上模块后，再扩展：

- 商品价格
- 商店
- 工资制度
- 企业
- 税收
- 市场
