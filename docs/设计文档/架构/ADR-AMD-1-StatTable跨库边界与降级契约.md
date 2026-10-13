# ADR-AMD-1 — StatTable 跨库边界与降级契约

- Status: **Accepted**（契约口径 v2 已收窄定死）；**底层实现整改尚未闭合**，见 §0 整改状态
- Date: 2026-09-15
- Deciders: 岳立基（架构设计部 · 首席架构师）；主理人 董全程
- 被修订件: Phase 1 架构裁决「人员绩效单表 1:1 落地」（2026-09-15，本 ADR 之母；下称 *母裁决*）
- Rule IDs: GOV-004 / GOV-005 / GOV-007 / GOV-008 / GOV-012；AI-003 / AI-004 / AI-011；COMP-*（第三方接入与降级）
- Related Gates: **GATE-ARCH** / **GATE-COMP** / GATE-DELTA / GATE-REL
- 相关工程: `PersonalChronicle`（消费方，下称 **PC**）· `RUIF`（被依赖组件库，下称 **RUIF**）

---

## 0. 整改状态（本 ADR 不是"已放行"，是"已定契约、待闭合"）

| 项 | 状态 | 证据 |
|---|---|---|
| RUIF 侧：真 5 参重载恢复（选项 A） | **已落地** | `Source/Components/DataAtoms.cs:225-226`（5 参委托）+ `:250-251`（10 参去默认值） |
| RUIF 制品：开发区 ↔ 发布区二进制一致 | **已闭合** | `Mods/RUIF/Assemblies/RUIF.dll` ↔ `11/RUIF/Assemblies/RUIF.dll` **MD5 = `404d74e2`**（129024 B, 15:08:53）；侧车 `RUIF.xml` 同时含 **5 参 + 10 参** `StatTable(…)` 与 `T:RUIF.StatCol` |
| PC 侧：3 处类型级引用 + 1 处版本敏感调用点清除 | **已闭合（源码级）** | 见 §0.1 逐处证据 |
| 桥级降级结构（分派 / D0 / D1 三方法） | **已闭合（源码级）** | `RuifBridge.cs:479`（分派，体内零 RUIF 类型）· `:524 TryStatTableV12` · `:281 TryStatTableLegacy`（恰好 5 实参，双通） |
| **GATE-ARCH 复审（2026-09-16 冻结基线）** | **PASS** | §6 复审判定表；实机项移交 GATE-TEST |
| **GATE-COMP 复审** | **PASS（源码/元数据级）**；实机降级验证 **未执行** | §6；诚实标记，未伪造 |

### 0.1 PC 侧闭合逐处证据（2026-09-16 冻结基线）

| # | 原违规点（本 ADR §3） | 现状态 | 证据（行号为冻结基线快照） |
|---|---|---|---|
| 1 | `Population.cs:347` `static readonly RUIF.StatCol[] CorePerfCols`（字段类型 + 静态初始化器） | **已清** | `Population.cs:352` `int[] CorePerfColAligns` · `:359` `bool[] CorePerfColDims`；残留 `RUIF.StatCol` 仅 `:346-347` **XML 注释** |
| 2 | `RuifBridge.cs:449` `TryStatTableEx(…, RUIF.StatCol[] cols, …)` | **已清** | `RuifBridge.cs:479-480` 签名 = `int[] aligns, bool[] dims, int, float, float[], string[]`（全框架内类型） |
| 3 | `Roster.cs:1531-1533` `DrawCoreTable(…, RUIF.StatCol[] cols = null, …)` | **已清** | `Roster.cs:1534-1536` 签名 = `int[] aligns, bool[] dims, …`（**D2 兜底恢复可达**） |
| 4 | 10 参 `StatTable` 调用点（版本敏感成员） | **已迁守卫** | `RuifBridge.cs:542`，位于 `TryStatTableV12`（`:524` `[NoInlining]`），仅由 `:486` 版本门控分支（`:490`）调用 |
| — | 唯一允许的 RUIF 类型位置 | 符合 §2 D-1 | `RuifBridge.cs:529` `statColBuf as RUIF.StatCol[]` · `:532` `new RUIF.StatCol[n]` —— **均在 `TryStatTableV12` 体内** |
| — | 分派体零 RUIF 类型 | 已确认 | `RuifBridge.cs:479-512` 体内无任何 RUIF 类型（仅 `available` / `VersionAtLeast` / 调 V12·Legacy） |
| — | `RUIF.StatCol` 全库类型位置 = 0 | 已确认 | 检索命中仅：`Population.cs:346-347` · `Roster.cs:1526` · `RuifBridge.cs:448-449/516/520-521`（**全为注释**）+ `:529/:532`（守卫体内） |

> 静态状态登记：`RuifBridge.cs:454` `private static object statColBuf` —— **有界（长度 = 表头列数 ≤ 8）· 仅 `TryStatTableV12` 内写 · IMGUI 主线程独占 · 仅持值类型数组（不持 RUIF 对象图）** ⇒ 判为**有界不变缓存**，符合 AI-004「禁**无界**静态可变单例」，**准予登记通过**（代价：单槽缓存永不收缩，容量上界 = 全应用最大列数）。

---

## 1. Context

母裁决选定扩展点 = **A：扩 RUIF `DataAtoms.StatTable`（新增向后兼容重载：逐列对齐/暗前景 + 阈值带色列）**，以 1:1 落地预览 `人员系统中心预览_RUIF单页统一版.html` 第 1004-1087 行（核心面板 B，8 列 `StatTable` + 表底合计行）。

实施过程中出现两类**跨库边界缺陷**，由兼容与接入部在 GATE-COMP 审计中提出、架构设计部独立复核成立：

1. **类型级引用穿透版本门控**：PC 侧出现 `RUIF.StatCol` 的**声明级**引用（字段/签名），使「RUIF 版本低于 1.2」时在**任何方法体守卫之外**解析类型而失败。
2. **D1 幽灵降级**：RUIF 侧把 `StatTable` 做成**签名替换**（5 → 10 参）而非**新增重载**，导致"RUIF<1.2 回退旧 5 参"的承诺在二进制层不存在。

约束（平台事实，由兼容与接入部实测提供）：
- RimWorld 1.6 `About.xml` **无法表达最低版本**（`Verse.ModDependency` 仅 `downloadUrl`/`steamWorkshopUrl`；`ModRequirement` 仅 `packageId`/`alternativePackageIds`/`displayName`）⇒ 游戏只校验 packageId 是否存在，**必须假设玩家可能装旧版 RUIF**。
- 运行时门控（`RUIF.Api` 版本常量 + 消费方自判）是**唯一**最低版本防线。
- PC 对 RUIF 为**硬依赖**（`About.xml modDependencies` + csproj 无条件 `<Reference>`，缺失即编译失败）；"软依赖/不装也能跑"的旧表述与事实矛盾。
- 工程当前 `MinSupportedRUIF` = **1.1.2**。

---

## 2. Decision

### D-1 跨库边界契约（v2 收窄版，取代母裁决中"PC 静态 readonly `StatCol[]`"的不精确表述）

**判据 = 「类型位置」× 「版本敏感性」；判定链如下（三步，缺一不可）：**

1. **该标识是否为 RUIF 类型/成员？** 否 → 无关。
2. **它是否在 `MinSupportedRUIF`（= 1.1.2）中存在过？** 是 → **不属版本敏感**，位置不限（含调用点）。否 → **版本敏感**，进入第 3 步。
3. **它的引用是否出现在「类型位置」？** 类型位置定义 = **编译期产生类型引用**的写法：字段/属性类型、参数/返回类型、局部变量声明、泛型实参、数组元素类型、`new RUIF.X`、`typeof(RUIF.X)`、`is/as RUIF.X`、静态/实例初始化器、attribute 类型、委托/事件类型。
   - **是** → **禁止**（unconditional 违规）。版本敏感类型**必须**改用框架内类型跨边界；确需 RUIF 类型者，仅可出现在 `[MethodImpl(NoInlining)]` **且仅在版本门控通过后才会被调用**的方法体内。
   - **否（纯调用点）** → 若被调用**成员**本身版本敏感（本次 = 10 参 `StatTable`），同样必须落守卫方法内（JIT 会解析被调用签名）；否则允许保留。

**⚠ 关键纠正（补母裁决漏洞）：值类型 ≠ 版本安全。**
`StatCol` 是 `struct`（值类型），却**仍然**不可外泄。依据 JIT 契约：`newobj`/`initobj` 与**签名（字段/形参/返回/局部）**中的类型解析，都会在**方法 JIT 期**（字段则在类型加载/首个静态访问期）要求该类型**可解析**；此要求与「值类型/引用类型」**无关**。故母裁决与 `RuifBridge.cs:24` 旧注「跨边界只传值类型」是**不充分**的判据 —— 真正的判据是 **「目标最低版本中是否存在该类型/成员」**。

**本次范围（精准闭合，不扩大）**：
- **必改（版本敏感）**：`RUIF.StatCol` 声明 **3 处** + 10 参 `StatTable` 调用点 **1 处**。
- **不动（1.1.2 既有成员）**：桥外约 53 处对 `RUIF.Draw` / `Tokens` / `Measure` / `Shapes` / `NavAtoms` / `PanelAtoms` / `ProgressAtoms` / `StatusAtoms` / `StatusFmt` 的**调用** —— 它们是 2026-09-15 用户明示「组件方向必须引用 RUIF、禁止自研组件」的产物，**保留**。

### D-2 RUIF 兼容（选项 A）
RUIF **恢复真实 5 参重载**（委托 10 参），10 参**去掉全部可选默认值**以消除 `CS0121` 重载二义性；旧 5 参**不加** `[Obsolete]`（行为等同的便捷重载，且 RUIF 自称 SemVer 冻结）。

### D-3 降级阶梯（按事实重贴，消除幽灵语义）
| 档 | 条件 | 行为 |
|---|---|---|
| **D0** | RUIF ≥ 1.2 | 完整 1:1（逐列对齐 / 暗前景 / 阈值带色列） |
| **D1** | RUIF == 1.1.x | 走**真 5 参重载**（左对齐、无带色）+ 一次性日志（文案须与事实一致） |
| **D2** | RUIF ≥ 1.2 但 `StatTable` 调用**抛异常** | 本工程同版式自绘兜底 |
| **N/A** | **RUIF 缺失** | **不支持场景**（编译期即失败）；**不再**声称"缺失可运行" |

### D-4 治理方向（固化为后续前置核对）
「版本敏感性判据」应固化为**新增任何 RUIF 成员引用前的前置核对方法**（新成员先查是否存在于 `MinSupportedRUIF`，再按 D-1 三步定位）。
**注意：这不是"整个桥不许用 RUIF 类型"** —— 判据是**版本存在性**，不是"类型出现在哪一层"。

---

## 3. 违规清单（本次必改，行号为 2026-09-15 快照）

| # | 位置 | 性质 | 后果 | 处置 |
|---|---|---|---|---|
| 1 | `MasterControlTabWindow.Population.cs:347-357` `private static readonly RUIF.StatCol[] CorePerfCols = { new RUIF.StatCol{…} ×8 }` | **字段类型 + 静态初始化器** | 无条件路径；`.cctor` 解析 `RUIF.StatCol` ⇒ `TypeInitializationException`，与 `available`/`try-catch` 无关 | 改为框架内类型 `int[] CorePerfColAligns` + `bool[] CorePerfColDims` |
| 2 | `Archive/UI/RuifBridge.cs` `TryStatTableEx(…, RUIF.StatCol[] cols, …)` | **桥方法形参** | 该方法 JIT 即解析类型 ⇒ 降级分支永不可达 | 形参改框架内类型；桥体拆 `TryStatTableEx`(分派) / `TryStatTableV12`(≥1.2) / `TryStatTableLegacy`(任意) 三 `NoInlining` 方法 |
| 3 | `MasterControlTabWindow.Roster.cs:1531-1533` `DrawCoreTable(…, RUIF.StatCol[] cols = null, …)` | **PC 方法形参** | `DrawCoreTable` 本身就是 **D2 自绘兜底方法** ⇒ 其 JIT 失败使 **D2 一并失效** | 形参改框架内类型（`int[] aligns, bool[] dims, …`） |
| 4 | `Archive/UI/RuifBridge.cs` `TryStatTableEx` 体内 10 参 `StatTable` 调用 | **版本敏感调用点** | 该成员 1.1.2 不存在 ⇒ 与 #2 同源 | 迁入 `TryStatTableV12`（体内方可出现 `RUIF.StatCol`） |

---

## 4. Options Considered（AI-003 风险升序）

母裁决已记录扩展点升序（原生 Def/XML → DefModExtension → Comp/GameComponent → Service/Contract/Provider → PatchOperations → Harmony ×3 全部 N/A 或跳过，见母裁决）。本 ADR 记录**兼容策略**的选项：

| 方案 | 风险等级 | 优点 | 缺点 | 结论 |
|---|---|---|---|---|
| **A：RUIF 恢复真 5 参重载（委托）** | 低 | 恢复母裁决契约；D1 变真；消除对工坊外部预编译消费者的二进制破坏；成本 = 1 个委托方法 | 需重建 + 同步 DLL | **选定** |
| B：删除 D1，仅留 D0/D2 | 低 | 改动更小 | RUIF<1.2 时丧失 StatTable 版式（退自绘）；须删假日志 | **仅作应急**；若 A 不可行则改 B，**不得留幽灵 D1** |
| C：反射 `MethodInfo.Invoke` 精确绑 5 参 | 高 | 不动 RUIF | 违反 PERF-001（绘制热路径反射）+ 复杂度无收益 | **否决** |

---

## 5. Consequences

- **正面**：① 恢复"旧 5 参签名"契约，RUIF 对外部消费者的 SemVer 冻结承诺成立；② D1/D2 降级阶梯**真实可达**；③ 附带收益：`RuifBridge` 非 Ex 的 `TryStatTable`（5 实参）在 1.1.2 / 1.2.0 **双通**，StatTable 主路径不再依赖版本。
- **负面**：① RUIF 需重建并重同步 DLL（已闭合）；② PC 需清 4 处并重编；③ RUIF `docs/API参考.md` 与 About 的「公开方法 176 / 类型 115」计数需刷新。
- **补偿措施**：运行时版本门控（唯一最低版本防线）；降级阶梯 D0/D1/D2；`RuifBridge.cs:10-24` 文案改为「**RUIF = 硬依赖；桥的意义 = 版本门控 + 原子异常降级**」；`RuifBridge.cs:24`「只传值类型」升级为「**版本敏感类型不外泄**」。

---

## 6. Compliance

- **是否触发 Architecture Review**：**Yes** — 触发项 `Cross-Mod Direct Reference = Yes`（本次改动新增的跨库类型级引用）。
- **是否需要例外登记**：**无 EXC**。本 ADR 为**整改裁决**，非偏离规范；未见需豁免项。
- **复审结论（2026-09-16 冻结基线 16:28）**：**GATE-ARCH = PASS**（源码/元数据级）；**GATE-COMP = PASS**（源码/元数据级，实机部分**未执行**，移交 GATE-TEST）。

### 6.1 复审判定表（按 §2 D-1 收窄判据：版本敏感类型 × 类型位置）

| # | 判据 | 证据级别 | 结果 | 证据 |
|---|---|---|---|---|
| ① | **声明级清零**：版本敏感类型（`StatCol`）在 PC 的字段/形参/返回/局部 = 0 | **源码级** | **PASS** | §0.1 —— 3 处声明全清；残留命中仅注释 + `TryStatTableV12` 体内 |
| ② | **二进制对齐**：两区 RUIF 制品一致且含 D1 绑定目标 | **元数据级** | **PASS** | MD5 `404d74e2` 双端一致；侧车含 5 参 + 10 参 + `T:RUIF.StatCol` |
| ③ | **绑定正确**：分派体零 RUIF 类型；5 实参走 5 参重载 | **源码级**（IL 级待 QA） | **PASS（源码）** | `TryStatTableEx:479-512` 体内零 RUIF 类型；`TryStatTableLegacy:289` 恰 5 实参；`TryStatTableV12` 走 10 参。**无 CS0121**（10 参去默认值，源码可证） |
| ④ | **降级可达**：强制 D1（RUIF 1.1.x）/ D2（注入异常）实机可绘 | **实机** | **未执行** | 无实机环境 —— **如实标记，不得伪造**；移交 GATE-TEST（QA） |
| ⑤ | Harmony / 继承 / 无界全局状态 | **源码级** | **PASS** | 0 处 Harmony；无新增继承；`statColBuf` 有界已登记（§0） |

### 6.2 残留项（P3 · 不阻断本 ADR 的 PASS，但 Release 前应清）

| 项 | 级别 | 说明 |
|---|---|---|
| `RuifBridge.cs:35-36` 「跨边界只传值类型」措辞 | P3 | 未带**版本维度**（§2 D-1 已证"值类型不豁免"）。权威规则见 `:22-25` 与 §2 D-1，故非阻断；建议改写为"版本敏感类型不外泄" |
| `UITheme.cs:105` 「RUIF 软依赖」字样 | P3 | 与硬依赖事实矛盾（主理人已登记，下一轮清） |
| `RuifBridge.cs:193` 「装/不装 RUIF、装哪一版」措辞 | P4 | 历史叙述，非规范陈述；可选清 |

> 说明：以上均为**文档/注释**级（AI-011 P3/P4 = 交付前修正，非阻止交付），且**权威规则已在 `RuifBridge.cs:22-25` 与 §2 D-1 正确陈述** ⇒ 不适用 `PASS WITH EXCEPTION`（无 EXC 登记），判 **PASS + 残留清单**。

---

## 7. Scope 排除（防误判）

外部并发写入（非本团队授权，疑似用户并行会话）：`Source/PersonalChronicle/Archive/` 下 6 个文件（15:06，内容为战役卡 222.5 + 内联伤亡、删除 `expandedBattles` 交互）与 `Archive/UI/UIComponents.cs`（14:54）。**不计入本 ADR 范围**；本 ADR 仅对上述 4 处 `StatCol` / 10 参调用出结论。

---

## 8. 增补条款（2026-09-16）：新增控件的档位命名、类型位置与降级阶梯

> 适用：任何后续「把 PC 自制控件换成 RUIF 原子」的变更（首个适用对象 = 窗口页头按钮族，见 ADR-AMD-2）。

### 8.1 两条正交的「档位」轴必须分开命名（防 D-档语义重载）

上一轮教训：把「RUIF 版本降级」与「控件内部宽度自适应」**都叫 D0/D1/D2**，直接导致「幽灵降级」。故新增控件一律按两轴分开命名：

| 轴 | 命名 | 含义 | 例（Button） |
|---|---|---|---|
| **桥级（版本）** | `B0 / B1` | 版本 ≥ 引入版 → RUIF 原子；否则 → 回退 | B0 = RUIF `Button`；B1 = vanilla `Widgets.ButtonText` |
| **原子级（空间）** | `R1 / R2 / R3` | 控件在自身 rect 内的响应式降级 | R1 全文案 → R2 截断 → R3 只图标 → 不画 |

**禁止**再用 `D0/D1/D2` 命名新增控件的档位 —— 该组已被 §2 D-3（StatTable）占用。

### 8.2 类型位置规则（沿用 §2 D-1，**显式覆盖枚举**）

- 新增控件的**枚举/结构**（如 `ButtonKind`）与类型同等对待：**版本敏感 ⇒ 不得出现在 PC 的字段/属性/形参/返回/局部**。
- 桥签名**只暴露框架内类型**（`int`/`string`/`bool`/数组…），由**守卫方法体内**映射到 RUIF 枚举。
- 版本敏感成员的**调用点**必须落在 `[NoInlining]` + 版本门控分支内的守卫方法（与 §3 #4 同规）。

### 8.3 降级阶梯必须「可达 + 如实」

- 回退目标优先选**永在**的渲染源（vanilla `Widgets.ButtonText`）—— 不依赖回退版本存在，**优于** StatTable 的 D1。
- 降级态 = **视觉回退（非 1:1）**，注释/日志/ADR 必须如实声明，**不得**声称"旧版也统一"（LAND-009 诚实性）。

---

## 附：变更记录

| 版本 | 日期 | 变更 |
|---|---|---|
| v1 | 2026-09-15 | 初版判定：采纳 compat 提案（清 3 处类型级引用）；问题 2 裁定 A。措辞「PC 侧零 `RUIF.*` 类型（字段/签名/属性/**静态成员**）」——**已被 v2 取代**。 |
| **v2** | 2026-09-15 | **收窄口径**：判据 = 类型位置 × 版本敏感性（三步判定链），**不含调用点**；纠正「值类型 = 版本安全」的错误假设（`StatCol` 值类型仍不可外泄，JIT 契约依据）；范围精准 = 3 处声明 + 1 处调用点；新增 D1/D2 事实化标签与治理方向。 |
| **v3** | 2026-09-16 | **整改闭合 + 复审**：§0 增 §0.1 逐处闭合证据、PC 侧转「已闭合」、`statColBuf` 静态状态登记通过；§6 改为复审判定表（GATE-ARCH = PASS，实机项移交 GATE-TEST，附 P3/P4 残留）；新增 **§8 增补条款**（新增控件的 B/R 档位命名、枚举类型位置规则、降级如实性）。 |
