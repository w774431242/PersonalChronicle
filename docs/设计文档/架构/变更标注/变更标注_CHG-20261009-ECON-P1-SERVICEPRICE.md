---
change_id: CHG-20261009-ECON-P1-SERVICEPRICE
state: ImplementedAndDeployedByThisBatch
scope: P1 缺陷修复 —— InstitutionPolicySaveData.Read 漏传服务单价，导致读路径不一致与静默数据丢失
rule_ids: DATA-001, DATA-004, SAVE-003, SAVE-006, GOV-011, AI-014
files:
  - path: Source/PersonalChronicle/Data/PawnEconomy/InstitutionPolicySaveData.cs
  - path: Tests/InstitutionPolicyReadConsistencyTests.cs
  - path: Source/PersonalChronicle/BuildStamp.g.cs
---

# P1 修复：服务单价在读路径被静默丢弃

**触发**：M10 最小 slice 的只读评估中，`pc-data-steward` 登记了此项 P1；PM 批准后由主执行者实施（子 agent 工具集可能为只读，1 行修复不宜再派发）。

## 缺陷（证据链完整）

**构造函数**（`Domain/PawnEconomy/Governance/InstitutionPolicy.cs:22-23`）：

```csharp
public InstitutionPolicy(string anchorKey, long revision, int zoneId, long wagePerJobMinor, long foodPricePerNutritionMinor,
    bool open, int startHour, int endHour, long servicePricePerUnitMinor = 0)   // ← 第 9 参，默认 0
```

**同一存档行、两条读路径**（`Data/PawnEconomy/InstitutionPolicySaveData.cs`）：

| 方法 | 行 | 传参 | 结果 |
| --- | --- | --- | --- |
| `Read(anchorKey)` | 88-89 | `…, row.Open, row.StartHour, row.EndHour);` ⇒ **8 个** | 第 9 参落**默认 0** ❌ |
| `ReadAll()` | 95-96 | `…, row.EndHour, row.ServicePricePerUnitMinor);` ⇒ **9 个** | 真实值 ✅ |

⇒ **同一行两个答案**：`Read()` 看到 0，`ReadAll()` 看到真值。

**后果**：`PublishesServicePrice => ServicePricePerUnitMinor > 0` 在 `Read()` 路径下**恒为假** ⇒ 服务价被逐层丢弃：`QueryServicePricePolicy` → `ServiceReadiness.priceCap` → M8 `TrySettleCareAct`。

这属本会话反复出现的缺陷形态（"看起来存在、运行时永不生效"），但更严重 —— 前几次是"差一根接线"，**这次是两条读路径行为不一致的静默数据丢失**。

## 改动

| 文件 | 改动 |
| --- | --- |
| `Data/PawnEconomy/InstitutionPolicySaveData.cs` | `Read()` 补传第 9 参 `row.ServicePricePerUnitMinor`，并加注释说明"构造函数该参有默认值 0，漏传会静默丢弃服务价" |
| `Tests/InstitutionPolicyReadConsistencyTests.cs`（新增）| 4 用例 |

**测试落点是"两条路径必须一致"**，而不只是单条路径 —— 否则同类漏传会再次逃逸：

1. `ReadingOnePolicyAgreesWithReadingAllOfThem` —— `Read().ServicePricePerUnitMinor == ReadAll()[0].…` 且 `PublishesServicePrice == true`
2. `EveryOtherFieldSurvivesSoTheFixCannotShiftArguments` —— 逐一断言其余 9 个字段，防"补参补错位置"
3. `AnUnpublishedServicePriceStaysUnpublished` —— 反向样本：确实未发布时仍为假（防把构造默认值当成"已发布"）
4. `TheUpdatePathKeepsTheServicePriceToo` —— 更新路径同样只经 `Read` 取 `current`，确认改价后回读是新值而非被清零

**Save schema 无变更**：`Entry` 本身一直有该字段（`ExposeData` 有 `Scribe_Values.Look(ref ServicePricePerUnitMinor, …)`），缺的只是**读回时的传参**。故无 Migration、无默认值变更（SAVE-003/004 不受影响）。

## ⚠ 副作用登记（重要，不得忽略）

修复后**服务价首次真正生效** —— M8 的资金链 `ServiceReadiness.priceCap → TrySettleCareAct` **首次接通**。该链**未经实机验收**。

⇒ 建议把它**登记为待验收项**，不要默认它一定正确。这是本修复的**唯一**风险面。

## 验证基线

编译 **0 错 0 警**；测试 **1152 通过 / 0 失败 / 1 跳过**（`P1-SERVICEPRICE-V1.trx`，+4）；verify **PASS**。已部署。

**未执行**：实机验收（需游戏环境，`NOT EXECUTED`）。

## 三态

- **阻塞：0**
- **做不到：0**
- **未做**：实机验收服务链；M10 最小 slice 的缺口 1+2（规格已就绪，见 `docs/设计文档/架构/变更标注/` 内 M10 评估结论）

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
