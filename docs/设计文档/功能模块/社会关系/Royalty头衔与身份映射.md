# RimWorld — Royalty DLC 头衔清单与身份维度映射

> 适用范围：RimWorld 1.6 + Royalty DLC
> 关联：PersonalChronicle 兼容方案（v4.8 拟定）——「身份维度 × 动态强度档名」
> 风格约定：**沿用生涯强度评估（WorkIntensity）范式**——每档「名(label) + 副标语(tag)」配对、Def 驱动、阈值/区间触发、纯函数解析、零硬编码。
> 本文档用于对齐 `RoyaltyIdentityMap.xml` 的 `RoyalTitleDef.defName → IdentityKey` 映射，
> 以及预览文件 `完整档案馆UI预览.html` 中的身份维度演示。

---

## 1. 头衔完整清单（由低到高）

头衔等级由 `RoyalTitleDef` 的 `seniority` 决定，分两类：

### 1.1 可授予殖民者（通过帝国任务 + 荣誉点数晋升）

| 序号 | 英文 (defName) | 中文译名（社区通行） | 说明 |
|----|----------------|--------------------|------|
| 1 | Freeman    | 自由民   | 最基础头衔，完成首个帝国任务即可获得 |
| 2 | Yeoman     | 自耕农   | 低阶贵族起点 |
| 3 | Esquire    | 乡绅     | 文书/侍从类 |
| 4 | Knight     | 骑士     | 可调用帝国援军（骑士团） |
| 5 | Baron      | 男爵     | 拥有领地，需配皇室房间 |
| 6 | Count      | 伯爵     | 高阶领主 |
| 7 | Duke       | 公爵     | 高阶领主，需求更大的皇室房间 |
| 8 | Prince     | 亲王/王子 | 玩家可获最高头衔 |

### 1.2 不可授予（帝国高层 NPC，游戏中玩家无法获得）

| 序号 | 英文 (defName) | 中文译名 | 说明 |
|----|----------------|----------|------|
| 9 | King / Queen     | 国王 / 女王 | 帝国君主 |
| 10| Emperor / Empress| 皇帝 / 女皇 | 星系主宰，至高无上 |

> 注：`defName` 为游戏内部稳定键（区分大小写），映射表一律用 `defName` 而非显示名，避免硬编码本地化文案（P1 合规）。

---

## 2. 身份维度评估轴（沿用强度评估风格）

强度评估是「工时 → 五档」的**单轴评估**。身份维度是第二条正交评估轴：
**「头衔等级 → 三档身份」**，结构与强度轴完全同构——

- 每档有 **名(label) + 副标语(tag)** 配对（对齐 `WorkIntensityTierDef.labelKey/tagKey`）。
- 触发条件为**头衔区间**（对齐强度轴的 `minimumDailyHours` 阈值）。
- 无 DLC / 无头衔 → 回落基准档 `Commoner`（对齐强度轴"无采样 → 不评级"）。

### 2.1 身份维度三档（评估轴定义）

> 标签统一为 4 字现代化评估用语，与强度档名（亦 4 字）同构，构成「双轴评估」视觉一致性。

| IdentityKey | 中文名(label) | 副标语(tag) | 触发头衔区间（有 Royalty 时） | 语义 |
|-------------|---------------|-------------|------------------------------|------|
| `Commoner`  | 基础序列 | 无衔在野 | 默认 / 无头衔 / `HighestTitle()==null` | 无 Royalty 或仅 Freeman~Esquire |
| `Retainer`  | 臣属序列 | 受封履职 | Knight ~ Baron | 低阶贵族，受封领役 |
| `Peer`      | 领主序列 | 裂土主政 | Count ~ Prince | 高阶贵族，自有领地 |

> 英文 label/tag：
> - `Commoner` — *Base Tier* / "Un-titled"
> - `Retainer` — *Retainer Tier* / "Enfeoffed"
> - `Peer` — *Peer Tier* / "Landed"

### 2.2 解析风格（对齐 `tierOf` 纯函数）

身份解析与强度档解析同属「Read Model 层纯函数」，不依赖第三方内部状态：

```csharp
// Domain.IdentityResolver —— 与 WorkIntensityDefCatalog 同级，纯函数 + Def 驱动
public static class IdentityResolver
{
    public const string Commoner = "Commoner";
    public const string Retainer = "Retainer";
    public const string Peer     = "Peer";

    public static string Resolve(Pawn pawn)
    {
        // ① 零 DLC 依赖短路（对齐 DlcStatus.IsRoyaltyActive）
        if (!DlcStatus.IsRoyaltyActive || pawn?.royalty == null)
            return Commoner;
        // ② 取最高头衔（原生 API，不读内部字段）
        RoyalTitleDef title = pawn.royalty.HighestTitle();
        if (title == null) return Commoner;
        // ③ Def 驱动映射（对齐 WorkIntensityDefCatalog.ToSpec），无映射 → 回落基准档
        return RoyaltyIdentityMap.Lookup(title.defName) ?? Commoner;
    }
}
```

档名解析同理（对齐 `resolveTierNameKey`）：

```csharp
// 给定 tier + 身份维度，返回应显示的 labelKey；无 override → 回落主 labelKey
public static string ResolveTierNameKey(WorkIntensityTierView tier, string identityKey)
{
    if (tier?.IdentityLabelKeys != null
        && tier.IdentityLabelKeys.TryGetValue(identityKey, out var k))
        return k;
    return tier?.LabelKey;
}
```

---

## 3. 推荐映射表（→ `RoyaltyIdentityMap.xml`）

> 映射为「推荐值」，可由子 mod 通过 XML Patch 调整，不写死 C#（对齐强度档 Def 驱动范式）。

| RoyalTitleDef.defName | 中文 | 身份维度 IdentityKey |
|----------------------|------|---------------------|
| Freeman    | 自由民   | Commoner  |
| Yeoman     | 自耕农   | Commoner  |
| Esquire    | 乡绅     | Commoner  |
| Knight     | 骑士     | Retainer  |
| Baron      | 男爵     | Retainer  |
| Count      | 伯爵     | Peer      |
| Duke       | 公爵     | Peer      |
| Prince     | 亲王     | Peer      |
| King/Queen | 国王/女王 | Peer（NPC，仅参考） |
| Emperor    | 皇帝     | Peer（NPC，仅参考） |

### 对应 XML 结构（Def 驱动，零硬编码）

```xml
<PersonalChronicle.Domain.RoyaltyIdentityMapDef>
  <defName>RoyaltyIdentityMap</defName>
  <mappings>
    <li><titleDefName>Knight</titleDefName><identity>Retainer</identity></li>
    <li><titleDefName>Baron</titleDefName><identity>Retainer</identity></li>
    <li><titleDefName>Count</titleDefName><identity>Peer</identity></li>
    <li><titleDefName>Duke</titleDefName><identity>Peer</identity></li>
    <li><titleDefName>Prince</titleDefName><identity>Peer</identity></li>
  </mappings>
</PersonalChronicle.Domain.RoyaltyIdentityMapDef>
```

> Freeman/Yeoman/Esquire 不进映射表 → `RoyaltyIdentityMap.Lookup` 返回 null → `IdentityResolver.Resolve` 回落 `Commoner`（无映射即庶民阶，符合语义）。

---

## 4. 双轴动态档名矩阵（强度档 × 身份维度）

沿用强度评估「名 + 副标语」配对范式：每个组合产出 **档名(label) + 档tag**，tag 随身份维度同步风格化。
强度五档阈值不变（≥12 / 9 / 6 / 3 h·天⁻¹）。

### 4.1 中文矩阵（每格：`名` / `tag`）

> 庶民阶（Commoner）恢复为原始设计档名，**且不带副标语 tag**；臣属阶（Retainer）与领主阶（Peer）沿用身份化 4 字评估用语（含副标语 tag），突出 Royalty 头衔对应的职责/地位意象。

| 强度档 | 阈值 | 基础序列 Commoner | 臣属序列 Retainer | 领主序列 Peer |
|--------|------|-------------------|-------------------|---------------|
| **T0** | ≥12h | 核动力驴 | 殿下引擎<br>↳ 随召随到 | 领地核心<br>↳ 中枢引擎 |
| **T1** | 9–12 | 超级牛马 | 忠勤臣属<br>↳ 事优先办 | 雄镇栋梁<br>↳ 一方主理 |
| **T2** | 6–9  | 常规牛马 | 安稳侍从<br>↳ 按规履职 | 治世良绅<br>↳ 井然有序 |
| **T3** | 3–6  | 劣质牛马 | 慵懒随从<br>↳ 召之方动 | 疏懒贵胄<br>↳ 偶务政务 |
| **T4** | <3   | 社会懒汉 | 闲散内侍<br>↳ 象征在编 | 尊贵闲王<br>↳ 授权治理 |

### 4.2 英文矩阵

| 强度档 | Commoner | Retainer | Peer |
|--------|----------|----------|------|
| **T0** | Reactor Mule | Crown Engine<br>↳ On call | Domain Core<br>↳ Hub engine |
| **T1** | Super Workhorse | Loyal Retainer<br>↳ Crown first | Fief Pillar<br>↳ Runs fief |
| **T2** | Regular Workhorse | Steady Attendant<br>↳ By the book | Good Gentry<br>↳ All in order |
| **T3** | Poor Workhorse | Idle Attendant<br>↳ Summoned | Idle Noble<br>↳ Rarely on |
| **T4** | Social Slacker | Idle Courtier<br>↳ On paper | Idle Liege<br>↳ Delegated |

> 矩阵中每格的「名」来自 `WorkIntensityTierDef.labelKey`（Commoner）或 `identityLabelOverrides[identity]`（Retainer/Peer，4 字评估风）；
> 「tag」来自 `tagKey`（当前全局共用 4 字评估风，后续可扩展为按身份覆盖的 `identityTagOverrides`，范式一致）。

---

## 5. 无 DLC 兼容说明

- 未启用 Royalty：`ModsConfig.RoyaltyActive == false` → `DlcStatus.IsRoyaltyActive` 短路 → `IdentityResolver.Resolve` 永远返回 `Commoner`。
- 档名/档tag 全部回落主 `labelKey`/`tagKey`（即 Commoner 列），与 v4.7.0 现版行为完全一致。
- `RoyaltyIdentityMap.xml` 在无 Royalty 时无匹配项，天然安全（不会触发任何映射）。
- 身份维度评估轴的「副标语 tag」在无 DLC 时不可见（维度恒为 Commoner，仅显示基准档 tag）。

---

## 6. 与强度评估体系的一致性对照

| 维度 | 生涯强度评估（WorkIntensity） | 身份维度（Identity） |
|------|------------------------------|---------------------|
| 评估轴 | 工时 → 档 | 头衔 → 档 |
| 档结构 | 5 档（T0–T4） | 3 档（Commoner/Retainer/Peer） |
| 名+tag | `labelKey` + `tagKey` | `identity` label + 副标语 tag |
| 触发条件 | `minimumDailyHours` 阈值 | 头衔区间（映射表） |
| 基准回落 | 无采样 → 不评级 | 无 DLC/无头衔 → Commoner |
| 数据源 | `WorkIntensity.xml`（Def） | `RoyaltyIdentityMap.xml`（Def） |
| 解析层 | `WorkIntensityDefCatalog.ToSpec` | `IdentityResolver.Resolve`（纯函数） |
| 渲染消费 | `ArchiveUiDataProvider` 快照 | `ArchiveUiDataProvider` 注入 `IdentityKey` |

> 两者均在 Read Model 层完成解析，UI 只消费快照（`§5` 边界），不实时查 live `Pawn`/第三方状态。

---

*文档生成于 2026-08-10，对应 PersonalChronicle 兼容方案 v4.8 拟定稿。身份维度已按生涯强度评估风格（名+tag 配对、Def 驱动、纯函数解析）优化。*
