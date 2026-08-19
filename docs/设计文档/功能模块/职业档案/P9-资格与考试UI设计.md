# P9 资格·考试·论文·答辩 UI 设计（P9-A）

> **状态**：UI 预览基线（2026-08-19，AI-002 先架构后编码）。预览文件经 PM 调整确认后，方可进入 P9-B 落地（ITab 扩展 + Dialog + Data 层考试/论文/答辩编排）。
> **需求**：REQ-Career-011（资格/考试/论文/答辩 UI 与主方向选择）
> **基线**：《制造类职业资格与荣誉体系设计（P5~P8）》§2.3/§2.4/§3/§4（D-E1/D-E2/D-B1/D-D1/D-D2/D-T1）、《职业生涯系统开发者指南V2.0》§14~§18、《职业档案ITab数据契约v0.1》
> **关联规则**：UI-001/004/008、DATA-008、GOV-009（评价只回写事实）

---

## 1. Change Intent 摘要

- **目标用户行为**：玩家在职业档案 ITab 中查看资格进度、报名/参与实践考试、积累理论证据、撰写论文、召开答辩、获得职称授予反馈——全部由真实游戏行为驱动，无假"考试按钮"。
- **非目标**：❌ 不新增 Harmony patch（D-D2：ITab footer 按钮 + Dialog 触发）；❌ 不依赖 DLC Activity/Precept API（D-D1：同阵营高等级 Pawn 自动委员）；❌ 选择题/问答 UI（D-E2：理论考试第一阶段证据加权合成）。
- **P1-5 衔接**：预览按 P9 落地后完整流程演示（含论文/答辩门槛）；落地时随 `QualificationDefs.xml` 门槛恢复同步启用（Senior+ 恢复 requiredThesis/requiredDefense=true）。

## 2. 界面结构（对齐预览 P9资格考试UI预览.html）

### 2.0 落地位置（2026-08-19 PM 确认）

**放置：人物「职业档案」ITab（`ITab_Pawn_Career`）新增第 4 个子页「资格」（Qualification）**。

```text
ITab_Pawn_Career 子页顺序（现状 3 页 → 新增后 4 页）：
  Overview（总览）→ Career（履历）→ Honor（勋章）→ Qualification（资格 · 本预览内容）
```

- 理由：资格/考试/论文/答辩/评级评审与职业档案天然一体；总览页已有"资格预检"概要区块，完整流程交互承载在独立子页，符合"总览给结论、子页给操作"的现有结构（勋章墙同模式）。
- 实现要点（P9-B）：
  - `SubTabs` 追加 `"Qualification"`；`SubTabKeys` 追加翻译键 `PersonalChronicle.UI.Career.Sub.Qualification`（中英各 1 键："资格" / "Qualification"）
  - 新增 partial `ITab_Pawn_Career.Qualification.cs`（ARC-013 物理切片，零契约改动）：职称阶梯/状态机/条件检查/考试任务/论文/答辩/评审卡 布局对齐本预览
  - 考试报名/论文/答辩经 Dialog（D-D2）从子页按钮打开；Data 层编排复用 `RecordExamProduced` 采集点与 `ThesisData` 写入
  - 顺序门控 + 按档隔离 + 评级评审期（v1.3~v1.5 规则）全部继承

| 区块 | 内容 | 数据来源（C#） |
|---|---|---|
| 职称阶梯 | 5 档职称卡（等级/资历门槛 + 状态：锁定/申报中/可授予/已授予） | `QualificationDefs.xml` + `QualificationState` |
| 资格状态机 | Locked→Eligible→Preparing→PracticalExam→TheoryExam→Thesis→Defense→Qualified→Granted 进度条 | `QualificationProgress.Status` |
| 申报条件检查 | 6 行（专业等级/职业资历/综合评分/实践考试/理论考试/论文答辩） | `QualificationEvaluator.EvaluateOne`（门槛链 + 综合评分 W 权重） |
| 实践考试 | 报名 → 任务卡（配方白名单/数量 3/最低品质 Excellent/时限）→ 制造证据流 → 评分+硬门槛判定 | `PracticalExamRecord` + `ExamScoring.ScorePractical/CountAtLeast`（P1-2/P1-3 修复语义） |
| 理论考试 | 四类证据（书籍/研究/技能/活动）累积 → 加权合成 0.4/0.3/0.2/0.1 | `TheoryExamRecord` + `ScoreTheory` |
| 论文 | 选题 → 引用书籍×2/研究×2 → ThesisQuality = 0.4×Book+0.3×Research+0.3×Professional | `ThesisEvidence` + `ScoreThesis` |
| 答辩 | 自动召集 3 名同阵营委员（姓名+等级）→ 委员会评分 → Final = Thesis×0.5+Committee×0.5 | `DefenseRecord`（QualificationDefName，P1-4）+ `ScoreDefense` |
| 职称记录 | 已授职称时间线（自动授予 D-T1 + 回写 TitleGranted） | `GrantedTitle` + `CareerEvent(TitleGranted)` |

## 3. 交互流（预览可操作顺序）

```text
空白殖民者 →（等级/资历积累）→ Lv1/Lv2 自动授予（无考试要求）
  → Lv3 申报：报名实践考试 → 真实制造 ×3（品质硬门槛）→ 评分 → 通过
  → 理论考试：证据累积（书籍/研究/技能/活动）→ 加权合成 → 通过
  → 论文：选题 → 引用证据 → 论文质量 → 完成
  → 答辩：自动召集委员 → 委员会评分 → 通过
  → 资格满足 → 自动授予 Lv3 职称 → 回写履历/事实
（失败路径：考试未过/超时 → 回 Preparing，进度保留）
```

## 4. 落地规划（P9-B，预览确认后）

| 层 | 落地内容 |
|---|---|
| Domain | 无需新逻辑（QualificationEvaluator/ExamScoring/ScoreTheory/ScoreThesis/ScoreDefense/QualificationReview 均已就绪，含 P1 修复与评审期） |
| Application/Data | 考试报名/证据捕获编排（复用 `RecordExamProduced` D-E1 采集点；论文/答辩记录写入 `ThesisData`，新增服务方法；评审期已由 RunQualification 门控） |
| UI | **`ITab_Pawn_Career` 新增第 4 子页「资格」**（§2.0）：`SubTabs` + 翻译键 + 新增 partial `ITab_Pawn_Career.Qualification.cs`；`Dialog_ExamApply`（报名/任务卡）+ `Dialog_Thesis`（选题/引用）+ `Dialog_Defense`（委员确认/评分）——沿用 footer 按钮 + Dialog 范式（D-D2） |
| Defs | `QualificationDefs.xml` 各档 `reviewDays`（默认 3，可调）；Senior+ 论文答辩门槛已恢复（P1-5 否决） |
| 翻译 | `PersonalChronicle.UI.Career.Sub.Qualification` + `Professional.Qualification.*` / `Career.Exam.*` 键（中英成对） |
| 测试 | 考试报名→证据→评分→评审→授予全链 NUnit（复用 CareerQualificationTests 模式） |

## 5. 验收标准

| # | 验收项 | 通过标准 |
|---|---|---|
| 1 | 预览交互 | `P9资格考试UI预览.html?p9test=1` 完整链路通过（考试/理论/论文/答辩/授予） |
| 2 | 真实行为驱动 | 实践考试证据来自真实制造（复用 P1 采集点），无假按钮（V2.0 §15 红线） |
| 3 | 状态机正确 | 失败回 Preparing；授予后 Granted；重进不重复授予 |
| 4 | 兼容 | 无新 Harmony；DLC 无关；旧档 null 兼容（考试/论文容器缺省） |
| 5 | 本地化 | 全部新文案中英成对；无硬编码 |

## 6. 变更记录

| 版本 | 日期 | 作者 | 说明 |
|---|---|---|---|
| v1.0 | 2026-08-19 | AI | P9-A UI 设计首版 + 预览文件（docs/UI预览/人物档案视窗/P9资格考试UI预览.html，含 ?p9test=1 自检，全链通过） |
| v1.1 | 2026-08-19 | AI | **玩家向改造**：标题/说明/交互文案玩家化，开发者调试（重置/模拟积累/品质固定/一键流程）与数据契约收入折叠区；**修复答辩通过后未授予职称的 bug**——根因：授予逻辑原藏于渲染函数 renderReqs() 且答辩回调只做局部渲染（renderDefense+renderFsm），授予判定从未触发；修复：抽取业务函数 `evaluateAndGrant()`（判定+授予+玩家可见授予横幅），所有状态变更点统一走 renderAll；回归：?p9test=1 改为**玩家路径断言**（答辩 click 后不额外调用即授予），结果 grantedAfterDefense: junior,assistant,senior ✅ |
| v1.2 | 2026-08-19 | AI | **流程修补**（反馈：符合资格后未经过考试论文答辩就被授予）：① 全档位（Lv1~Lv5）考试（实践+理论）必经——`QualificationDefs.xml` Junior/Assistant `requiredExam` false→true；② **P1-5 临时关闭否决**：Senior/Specialist/Master 恢复 `requiredThesis/requiredDefense=true`（论文/答辩为必经流程，随 P9-B 落地生效）；③ 预览 TIERS 同步（全档 exam:true、Lv3+ thesis:true），报名按钮不再按档拒绝；④ 自检/一键流程改为**逐档完整链路**（考试→理论→[论文→答辩]→授予）；验证：p9test flow:true/true/true + grantedAfterFullFlow: junior,assistant,senior；模拟器 FlowBlocked（考试未过）Lv50 仍 BLOCKED(exam) 不授予 ✅ |
| v1.3 | 2026-08-19 | AI | **流程通道修复**（反馈：考试无上限、多次制作后完成答辩未结算授予）：① **流程状态按申报档隔离**——考试/理论提交/论文/答辩均按档记录（`S.tiers[key]`，对齐 C# QualificationDefName 匹配语义），旧档结果不延续；② **实践考试制造上限**——一次报名最多制造 `MaxProduced`（默认 2×要求件数，数据驱动字段，C# `PracticalExamRecord.MaxProduced` + `RecordExamProduced` 上限判定 + 2 个回归用例），超限/超时未达标即失败结束、可重新报名；③ 答辩完成后统一 `renderAll → evaluateAndGrant` 结算授予；验证：p9test flow:true/true/true + seniorExamIndependent:Y；C# 编译 0 错 0 警 + 单测 170 过/3 跳/0 失败 |
| v1.4 | 2026-08-19 | AI | **严格顺序开放**（反馈：按顺序开放 实践考试→理论考试→论文编写→召开答辩→结算评级）：① 理论考试提交须**本档实践考试通过**（按钮禁用 + handler 守卫）；② 论文选题须**本档理论考试已提交**；③ 召开答辩须**本档论文完成**（已有）；④ 答辩通过 → 结算授予（已有）；⑤ 各环节提示"🔒 需先完成上一步"；验证：p9test `gates:Y/Y/Y/Y`（禁用+守卫双层拦截乱序）+ flow:true/true/true + grantedAfterFullFlow 全链 ✅ |
| v1.5 | 2026-08-19 | AI | **评级评审期**（反馈：①理论考试结束后即评级授予（应在答辩之后结算）②结算应以工作日答复而非自动授予）：① 结算评级改为独立最后一步——资格满足（含答辩）后进入 **Review 评审期**（状态机 Defense→Review→Granted），评审未答复不授予；② **N 个工作日（默认 3 日）答复**——`QualificationDef.reviewDays`（数据驱动，0=默认 3）+ `QualificationReview.IsDue` 纯函数 + `QualificationProgress.ReviewStartedTick/ReviewDays`（append-only）；C# `RunQualification`：Eligible 首次检测 → 记录评审开始 → 未到期保持 Review → 到期才授予；③ 预览：评审卡（已 X/N 工作日 + 等待按钮）、条件检查加"评级评审"行、答辩/理论完成后提示"已进入评级评审期"；验证：p9test reviewGated1/3:Y（理论后/答辩后均不即时授予）+ reviewState:pending + grantedAfterReview:Y/Y/Y（评审 3 日后授予）；C# 编译 0 错 0 警 + 单测 174 过/3 跳/0 失败（+4 评审用例） |
| v1.6 | 2026-08-19 | AI | **落地位置确认**（PM）：放置于人物「职业档案」ITab（`ITab_Pawn_Career`）**新增第 4 子页「资格」**（Overview→Career→Honor→Qualification）；§2.0 记录放置方案与实现要点（SubTabs/翻译键/新 partial/Dialog 系列）；预览页脚标注落地位置 |

> **落地（P9-B）时须继承本修复**：C# 侧职称授予（RunQualification 调度）不得依赖 UI 渲染触发；资格判定应随状态变更（考试通过/论文完成/答辩完成事件）由 Data 层统一驱动（现有 reconcile 节流已满足，P9-B 仅补 Dialog 编排）。
