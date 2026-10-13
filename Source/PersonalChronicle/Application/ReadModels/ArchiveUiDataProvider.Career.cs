using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// Partial of <see cref="ArchiveUiDataProvider"/> — 职业概览 Read Model 构建。
    /// </summary>
    /// <remarks>
    /// · 职责：BuildCareerOverview，把 <c>PawnObject.CareerData</c> 投影为 <c>CareerOverviewView</c>。
    /// · 调用方：ArchiveUiDataProvider（详情快照装配链路）。
    /// · 禁止：本文件只做投影，不在此处做业务判定；排序/去重/null 兜底之外一律不下沉。
    /// · 拆分：自 Detail.cs 物理拆分（机械切片，方法体原样）；private 成员跨 partial 文件可见，编译安全。
    /// </remarks>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {
        private static CareerOverviewView BuildCareerOverview(PawnObject pawn)
        {
            CareerOverviewView view = new CareerOverviewView();
            if (pawn == null || pawn.CareerData == null)
            {
                return view;
            }
            CareerData cd = pawn.CareerData;

            // —— 职业身份 ——
            // 当前职称 = 已授最高档（按 TitleDef.order 归并）。
            string roleName = null;
            string primarySkillName = null;
            ProfessionalDirectionDef primaryDir = null;
            if (cd.Professional != null && !string.IsNullOrEmpty(cd.Professional.primaryDirection))
            {
                primaryDir = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(cd.Professional.primaryDirection);
                if (primaryDir != null && primaryDir.skillDefNames != null && primaryDir.skillDefNames.Count > 0)
                {
                    primarySkillName = primaryDir.skillDefNames[0];
                }
            }
            ProfessionalTitleDef currentTitle = FindHighestGrantedTitle(cd, primarySkillName);
            if (currentTitle != null)
            {
                roleName = TitleLabel(currentTitle);
            }
            else if (primaryDir != null)
            {
                string dKey = primaryDir.GetDisplayLabel();
                roleName = dKey.CanTranslate() ? dKey.Translate().ToString() : primaryDir.defName;
            }
            // 主技能 = 已选主方向技能；无主方向则取等级最高的技能。
            string skillText = null;
            int maxLevel = 0;
            string directionLabel = null;
            if (primaryDir != null)
            {
                string dKey = primaryDir.GetDisplayLabel();
                directionLabel = dKey.CanTranslate() ? dKey.Translate().ToString() : primaryDir.defName;
            }
            if (cd.Professional != null && cd.Professional.skills != null)
            {
                if (!string.IsNullOrEmpty(primarySkillName))
                {
                    ProfessionalSkillData sd = cd.Professional.GetSkill(primarySkillName);
                    ProfessionalSkillDef sdef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(primarySkillName);
                    string sName = sdef != null ? sdef.LabelCap : primarySkillName;
                    int lv = sd != null ? sd.level : 0;
                    skillText = sName + " Lv" + lv;
                    maxLevel = lv;
                }
                if (skillText == null)
                {
                    for (int i = 0; i < cd.Professional.skills.Count; i++)
                    {
                        ProfessionalSkillData sd = cd.Professional.skills[i];
                        if (sd == null) continue;
                        ProfessionalSkillDef sdef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(sd.skillDefName);
                        string sName = sdef != null ? sdef.LabelCap : sd.skillDefName;
                        if (sd.level > maxLevel)
                        {
                            maxLevel = sd.level;
                            skillText = sName + " Lv" + sd.level;
                        }
                    }
                }
            }
            // 相关工时：按事件首末 tick 跨度换算。
            //
            // ⚠ 2026-09-19 修复（CHG-20260919-ECON-UI-009）：原实现写死 `2400`，注释断言
            // 「60000tick ≈ 25h → 1h ≈ 2400 tick」。**该断言本身是错的**：原版 1 天 = 60000 tick
            // = **24** 小时 ⇒ 1h = 60000 / 24 = **2500** tick（即 `GenDate.TicksPerHour`，本工程
            // `PawnMedal.cs` 已在用）。写死 2400 会让此项工时**系统性偏高约 4.2%**
            // （2500/2400 − 1），并与同人物在另一个界面（`PawnMedal` 的算式带）算出**不同的工时**。
            long spanTicks = CareerSpanTicks(cd);
            int hours = (int)(spanTicks / GenDate.TicksPerHour);
            // 事实计数指标：统一从 RecordCountByType 聚合（UI 只消费，禁止绘制路径直查 Domain）。
            int results = 0;
            int made = 0;
            int built = 0;
            int researched = 0;
            if (cd.RecordCountByType != null)
            {
                cd.RecordCountByType.TryGetValue(CareerEventType.ItemProduced, out results);
                cd.RecordCountByType.TryGetValue(CareerEventType.ItemProduced, out made);
                cd.RecordCountByType.TryGetValue(CareerEventType.ConstructionCompleted, out built);
                cd.RecordCountByType.TryGetValue(CareerEventType.ResearchCompleted, out researched);
            }
            int books = cd.Books != null ? cd.Books.Count : 0;

            view.HasData = roleName != null || skillText != null || spanTicks > 0 || results > 0 || books > 0;
            // v1.1.5：移除 "HasData=false 提前 return"——保留 HasData 作为信号，但字段全部填充默认值，
            // 让 UI 端在空数据时也能渲染完整区块（占位 `--` / `0`）；避免"暂无数据"白色背景与缺字段。
            view.RoleName = roleName;
            view.RoleDesc = directionLabel;
            view.SkillText = skillText;
            view.HoursText = hours > 0 ? hours + " h" : null;
            view.Results = results;
            view.Books = books;
            view.Made = made;
            view.Built = built;
            view.Researched = researched;

            // —— 下一职称 + 资格状态 + 预检 ——
            // v1.1.5 修复：资格状态必须跟随当前主方向（primaryDirection 对应一级专业 majorKey）。
            string majorKey = null;
            ProfessionalDirectionDef primaryDirDef = null;
            if (cd.Professional != null && !string.IsNullOrEmpty(cd.Professional.primaryDirection))
            {
                primaryDirDef = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(cd.Professional.primaryDirection);
                if (primaryDirDef != null) majorKey = primaryDirDef.profession;
            }
            QualificationDef nextQual = QualificationEvaluator.NextQualification(cd, DefDatabase<QualificationDef>.AllDefsListForReading, majorKey);
            List<QualificationEvaluator.Eligibility> eligibilities = QualificationEvaluator.Evaluate(pawn);
            if (nextQual != null)
            {
                view.NextTitle = TitleLabel(DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(nextQual.titleDefName));
                QualificationEvaluator.Eligibility el = FindEligibility(eligibilities, nextQual);
                float composite = el != null ? el.CompositeScore : 0f;
                view.Qual = BuildQualRows(cd, nextQual, composite);
                view.PreCheck = BuildPreCheckRows(cd, nextQual, composite);
                view.Progress = ComputeReadyPercent(view.PreCheck);
                view.NextGaps = ComputeGaps(view.Qual, view.PreCheck);
            }
            else
            {
                // v1.2.x 体检修复：nextQual 为空时给出可执行的空态提示，禁止 7 行 `--` 占位。
                view.QualEmptyReasonKey = PickQualEmptyReasonKey(cd, primaryDirDef, majorKey);
            }
            return view;
        }

        /// <summary>
        /// 资格状态为空时，根据数据状态选择提示键（比 7 行 `--` 更明确）。
        /// </summary>
        private static string PickQualEmptyReasonKey(CareerData cd, ProfessionalDirectionDef primaryDirDef, string majorKey)
        {
            if (cd == null) return "PersonalChronicle.UI.Career.Ov.NoCareerData";
            if (cd.Professional == null || string.IsNullOrEmpty(cd.Professional.primaryDirection))
                return "PersonalChronicle.UI.Career.Ov.NoPrimary";
            if (primaryDirDef == null)
                return "PersonalChronicle.UI.Career.Ov.InvalidPrimary";
            if (DefDatabase<QualificationDef>.AllDefsListForReading == null
                || DefDatabase<QualificationDef>.AllDefsListForReading.Count == 0)
                return "PersonalChronicle.UI.Career.Ov.NoQualDefs";
            if (!HasQualificationDefsForMajor(majorKey))
                return "PersonalChronicle.UI.Career.Ov.MajorNoQuals";
            // 已授予最高档（封顶）——仅针对当前主方向判断，避免跨专业已授职称误判。
            if (!string.IsNullOrEmpty(majorKey) && IsMajorCapped(cd, majorKey))
                return "PersonalChronicle.UI.Career.Ov.Maxed";
            return "PersonalChronicle.UI.Career.Ov.NoQual";
        }

        /// <summary>指定一级专业是否已封顶（已授最高 order 的资格）。</summary>
        private static bool IsMajorCapped(CareerData cd, string majorKey)
        {
            if (cd == null || string.IsNullOrEmpty(majorKey)) return false;
            List<QualificationDef> defs = DefDatabase<QualificationDef>.AllDefsListForReading;
            if (defs == null) return false;
            int maxOrder = -1;
            for (int i = 0; i < defs.Count; i++)
            {
                QualificationDef q = defs[i];
                if (q != null && string.Equals(q.majorKey, majorKey, System.StringComparison.Ordinal) && q.order > maxOrder)
                    maxOrder = q.order;
            }
            if (maxOrder < 0) return false;
            return QualificationEvaluator.FindHighestGrantedOrder(cd, defs, majorKey) >= maxOrder;
        }

        /// <summary>指定一级专业是否存在资格定义。</summary>
        private static bool HasQualificationDefsForMajor(string majorKey)
        {
            if (string.IsNullOrEmpty(majorKey)) return true;
            List<QualificationDef> defs = DefDatabase<QualificationDef>.AllDefsListForReading;
            if (defs == null) return false;
            for (int i = 0; i < defs.Count; i++)
            {
                QualificationDef q = defs[i];
                if (q != null && string.Equals(q.majorKey, majorKey, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 职业事实计数（UI-001 / ARC-002：事实聚合归属 Provider）。
        /// 统一从 <c>CareerData.RecordCountByType</c> 取全部 9 类事件计数，
        /// 窗口（Overview/Resume/Honor）只消费本快照，不再直查 Domain。
        /// </summary>
        private static CareerFactCounts BuildCareerFactCounts(PawnObject pawn)
        {
            CareerFactCounts c = new CareerFactCounts();
            if (pawn == null || pawn.CareerData == null || pawn.CareerData.RecordCountByType == null)
            {
                return c;
            }
            Dictionary<string, int> counts = pawn.CareerData.RecordCountByType;
            counts.TryGetValue(CareerEventType.WorkCompleted, out c.WorkCompleted);
            counts.TryGetValue(CareerEventType.ItemProduced, out c.ItemProduced);
            counts.TryGetValue(CareerEventType.ConstructionCompleted, out c.ConstructionCompleted);
            counts.TryGetValue(CareerEventType.ResearchCompleted, out c.ResearchCompleted);
            counts.TryGetValue(CareerEventType.BookProduced, out c.BookProduced);
            counts.TryGetValue(CareerEventType.ExamPassed, out c.ExamPassed);
            counts.TryGetValue(CareerEventType.ThesisDefended, out c.ThesisDefended);
            counts.TryGetValue(CareerEventType.TitleGranted, out c.TitleGranted);
            counts.TryGetValue(CareerEventType.MedalGranted, out c.MedalGranted);
            return c;
        }

        /// <summary>已授最高档职称 Def（按 TitleDef.order 取最大）。skillFilter 非空时只取该专业技能下的职称。</summary>
        private static ProfessionalTitleDef FindHighestGrantedTitle(CareerData cd, string skillFilter = null)
        {
            if (cd.GrantedTitles == null)
            {
                return null;
            }
            ProfessionalTitleDef best = null;
            ProfessionalTitleDef bestAny = null;
            for (int i = 0; i < cd.GrantedTitles.Count; i++)
            {
                GrantedTitle gt = cd.GrantedTitles[i];
                if (gt == null || string.IsNullOrEmpty(gt.TitleDefName)) continue;
                ProfessionalTitleDef td = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(gt.TitleDefName);
                if (td == null) continue;
                if (bestAny == null || td.order > bestAny.order) bestAny = td;
                if (!string.IsNullOrEmpty(skillFilter)
                    && !string.Equals(td.professionalSkillDefName, skillFilter, System.StringComparison.Ordinal))
                {
                    continue;
                }
                if (best == null || td.order > best.order) best = td;
            }
            return best != null ? best : (string.IsNullOrEmpty(skillFilter) ? bestAny : null);
        }

        /// <summary>
        /// 下一档资格：委托 <see cref="QualificationEvaluator.NextQualification"/>（Domain 纯逻辑，
        /// 职称/资格双键匹配 + 严格高于当前已授最高档），以 DefDatabase 全量资格为候选集。
        /// majorKey：仅返回指定一级专业下的资格；null = 不限制。
        /// </summary>
        private static QualificationDef FindNextQualification(CareerData cd, string majorKey = null)
        {
            if (cd == null) return null;
            return QualificationEvaluator.NextQualification(
                cd, DefDatabase<QualificationDef>.AllDefsListForReading, majorKey);
        }

        private static string TitleLabel(ProfessionalTitleDef titleDef)
        {
            if (titleDef == null) return "--";
            return ("PersonalChronicle.Professional.Title." + titleDef.defName + ".Label").Translate().ToString();
        }

        private static QualificationEvaluator.Eligibility FindEligibility(
            List<QualificationEvaluator.Eligibility> list, QualificationDef target)
        {
            if (list == null || target == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                QualificationEvaluator.Eligibility e = list[i];
                if (e != null && e.Def != null && string.Equals(e.Def.defName, target.defName, System.StringComparison.Ordinal))
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>资格状态 7 条件（对齐 P9 HTML：专业等级/职业资历/综合评分/实践考试/理论考试/论文答辩/评级评审）。
        /// 中间列 note 显示「资格档要求」（翻译档名，不硬编码 defName），Tooltip 显示「人物当前条件 + 结构透视」。
        /// 悬停由 DrawQualCell → TooltipHandler.TipRegion 绑定。
        /// v10 体检：中间列走 Qual.Req.* 翻译键（用户要求全简体中文无硬编码），Tooltip 走 Qual.Tooltip.* 结构化模板（多行渲染）。</summary>
        /// <summary>构建"当前资格状态"7 行快照（条件名/要求/状态/悬停 Tooltip），供总览与资格子页统一消费（v2.0 §14 一致性）。</summary>
        public static IReadOnlyList<CareerQualView> BuildQualRows(CareerData cd, QualificationDef nextQual, float composite)
        {
            List<CareerQualView> rows = new List<CareerQualView>();
            if (nextQual == null) return rows;
            ProfessionalSkillData skillData = null;
            if (cd.Professional != null && !string.IsNullOrEmpty(nextQual.professionalSkillDefName))
            {
                skillData = cd.Professional.GetSkill(nextQual.professionalSkillDefName);
            }
            int level = skillData != null ? skillData.level : 0;
            float xp = skillData != null ? skillData.xp : 0f;
            int practiceCount = skillData != null ? skillData.practiceCount : 0;
            long span = CareerSpanTicks(cd);
            bool practical = HasPassedExam(cd, nextQual.defName, true);
            bool theory = HasPassedExam(cd, nextQual.defName, false);
            // ⑥ 论文/答辩：仅当档位要求论文或答辩时才判定（低档 requiredThesis=false 且
            // requiredDefense=false 时不要求，视为满足——对齐 Domain EvaluateOne 的条件判定）。
            bool requiresThesisDefense = nextQual.requiredThesis || nextQual.requiredDefense;
            bool thesisDefense = requiresThesisDefense
                ? HasPassedThesisDefense(cd, nextQual.defName)
                : true;

            string tierLabel = QualTitleLabel(nextQual);

            // ── ① 专业等级：note=档名 ≥ Lv（翻译）；tooltip=等级 XP 来源结构
            ProfessionalSkillDef skillDef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(nextQual.professionalSkillDefName);
            string skillSourceList = SkillSourceList(skillDef);
            int xpToNext = XpToNext(xp, skillDef);
            CareerLevelDetail levelDetail = BuildLevelDetail(skillData, skillDef, level, (int)xp, xpToNext, practiceCount);
            CareerQualView levelRow = QualRow("PersonalChronicle.UI.Career.Qual.Level",
                "PersonalChronicle.UI.Career.Qual.Req.Level".Translate(tierLabel, nextQual.requiredMinLevel).ToString(),
                level >= nextQual.requiredMinLevel,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Level".Translate(tierLabel, nextQual.requiredMinLevel, level, xp.ToString("F0"), practiceCount, skillSourceList, xpToNext).ToString());
            levelRow.LevelDetail = levelDetail;
            rows.Add(levelRow);
            // ── ② 职业资历：note=档名 ≥ 时长（天/月/年）；tooltip=累计 + 来源分项
            SpanBreakdown breakdown1 = CareerSpanBreakdown(cd);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Time",
                "PersonalChronicle.UI.Career.Qual.Req.Time".Translate(tierLabel, FormatSpanHuman(nextQual.requiredCareerTimeTicks)).ToString(),
                span >= nextQual.requiredCareerTimeTicks,
                // ⚠ 2026-09-19：参数由 12 个减为 8 个 —— 分项不再产出"小时"（见 SpanBreakdown 的说明），
                // 占位符随之变成 {4}=制造次 / {5}=建造次 / {6}=研究次 / {7}=其他次（Keyed 已同步改）。
                "PersonalChronicle.UI.Career.Qual.Tooltip.Time".Translate(tierLabel, FormatSpanHuman(nextQual.requiredCareerTimeTicks), FormatSpanHuman(span), span, breakdown1.CraftingCount, breakdown1.ConstructionCount, breakdown1.ResearchCount, breakdown1.OtherCount).ToString()));
            // ── ③ 综合评分：note=档名 ≥ minScore；tooltip=5 项分项结构
            ScoreBreakdown sb = ScoreBreakdownFor(cd, nextQual, level, composite);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Score",
                "PersonalChronicle.UI.Career.Qual.Req.Score".Translate(tierLabel, nextQual.minimumScore.ToString("F0")).ToString(),
                composite >= nextQual.minimumScore,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Score".Translate(tierLabel, nextQual.minimumScore.ToString("F0"), composite.ToString("F1"),
                    sb.Level.ToString("F1"), (sb.Level * 0.20f).ToString("F1"),
                    sb.Practical.ToString("F1"), (sb.Practical * 0.25f).ToString("F1"),
                    sb.Theory.ToString("F1"), (sb.Theory * 0.20f).ToString("F1"),
                    sb.Thesis.ToString("F1"), (sb.Thesis * 0.20f).ToString("F1"),
                    sb.Defense.ToString("F1"), (sb.Defense * 0.15f).ToString("F1")).ToString()));
            // ── ④ 实践考试：tooltip=任务详情（件数/品质/上限/时限）+ 当前进度
            PracticalDetail pDetail = PracticalDetailFor(cd, nextQual);
            string practicalState = PracticalStateText(cd, nextQual);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Practical",
                "PersonalChronicle.UI.Career.Qual.Practical.Req".Translate(), practical,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Practical".Translate(tierLabel,
                    pDetail.ReqCount, pDetail.MinQuality, pDetail.MaxProduced, FormatSpanHuman(pDetail.TimeLimitTicks),
                    practicalState, pDetail.ProducedCount, pDetail.QualifiedCount, pDetail.Score.ToString("F0")).ToString()));
            // ── ⑤ 理论考试：tooltip=加权合成 4 项
            TheoryDetail tDetail = TheoryDetailFor(cd, nextQual);
            string theoryState = TheoryStateText(cd, nextQual);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Theory",
                "PersonalChronicle.UI.Career.Qual.Theory.Req".Translate(), theory,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Theory".Translate(tierLabel, theoryState,
                    tDetail.Score.ToString("F0"), tDetail.BookScore.ToString("F0"), tDetail.ResearchScore.ToString("F0"),
                    tDetail.SkillScore.ToString("F0"), tDetail.ActivityScore.ToString("F0")).ToString()));
            // ── ⑥ 论文/答辩：tooltip=论文+委员评分构成
            // v10 体检：低档（不要求论文/答辩）显示"不要求"，状态恒满足——避免误导玩家等待无意义的流程。
            DefenseDetail dDetail = DefenseDetailFor(cd, nextQual);
            string defenseState = DefenseStateText(cd, nextQual);
            if (requiresThesisDefense)
            {
                rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Defense",
                    "PersonalChronicle.UI.Career.Qual.Defense.Req".Translate(), thesisDefense,
                    "PersonalChronicle.UI.Career.Qual.Tooltip.Defense".Translate(tierLabel, defenseState,
                        dDetail.ThesisScore.ToString("F0"), dDetail.DefenseScore.ToString("F0"),
                        dDetail.CommitteeSize, dDetail.FinalScore.ToString("F0")).ToString()));
            }
            else
            {
                rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Defense",
                    "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate(), true,
                    "PersonalChronicle.UI.Career.Qual.Tooltip.Defense.NotRequired".Translate().ToString()));
            }
            // ── ⑦ 评级评审：tooltip=评审期长度+流程
            // v10 体检：评审是授予后流程（资格满足 → 评审期 → 到期授予），非申报前条件。
            // 低档（不要求论文/答辩）同样无需评审，显示"不要求"恒满足；
            // 高档（requiredThesis/Defense=true）评审状态由 Domain 纯函数 IsDue 驱动。
            int reviewDays = nextQual.reviewDays > 0 ? nextQual.reviewDays : QualificationReview.DefaultReviewDays;
            string reviewState;
            bool reviewMet;
            if (requiresThesisDefense)
            {
                ReviewStateText(cd, nextQual, out reviewState, out reviewMet);
            }
            else
            {
                reviewState = "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate().ToString();
                reviewMet = true;
            }
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Review",
                "PersonalChronicle.UI.Career.Qual.Review.Req".Translate(), reviewMet,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Review".Translate(tierLabel, reviewState, reviewDays).ToString()));
            return rows;
        }

        // ============== 详情结构（Tooltip 用） ==============

        /// <summary>原版技能 defName 列表翻译为玩家可读字符串（Crafting → 工艺）。</summary>
        private static string SkillSourceList(ProfessionalSkillDef def)
        {
            if (def == null || def.sourceSkills == null || def.sourceSkills.Count == 0)
                return "PersonalChronicle.UI.Career.Qual.SkillSource.None".Translate().ToString();
            // 走原版 SkillDef 的 LabelCap（RimWorld 自动本地化翻译）
            List<string> labels = new List<string>();
            for (int i = 0; i < def.sourceSkills.Count; i++)
            {
                string s = def.sourceSkills[i];
                if (string.IsNullOrEmpty(s)) continue;
                SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(s);
                if (sd != null) labels.Add(sd.LabelCap.ToString());
                else labels.Add(s);
            }
            return labels.Count > 0 ? string.Join("、", labels) : "PersonalChronicle.UI.Career.Qual.SkillSource.None".Translate().ToString();
        }

        /// <summary>
        /// 构建专业等级悬停浮层的结构化数据。
        /// XP 来源占比读 <see cref="ProfessionalSkillData.xpBySource"/>（精确溯源，append-only）；
        /// 无精确拆分（新档/未采集）时回退按各 sourceSkill 当前等级派生的近似。
        /// </summary>
        private static CareerLevelDetail BuildLevelDetail(ProfessionalSkillData skillData, ProfessionalSkillDef skillDef, int level, int xp, int xpToNext, int practiceCount)
        {
            CareerLevelDetail d = new CareerLevelDetail
            {
                Level = level,
                Xp = xp,
                XpToNext = xpToNext,
                PracticeCount = practiceCount
            };
            if (skillDef == null || skillDef.sourceSkills == null || skillDef.sourceSkills.Count == 0) return d;

            // 精确占比：当前职业技能的 xpBySource 已按原版来源技能累加。
            float exactTotal = 0f;
            if (skillData != null && skillData.xpBySource != null)
            {
                for (int i = 0; i < skillDef.sourceSkills.Count; i++)
                {
                    string s = skillDef.sourceSkills[i];
                    if (string.IsNullOrEmpty(s)) continue;
                    if (skillData.xpBySource.TryGetValue(s, out float v)) exactTotal += v;
                }
            }

            if (exactTotal > 0f)
            {
                // 精确分支：来源占比 = 各原版技能累计 XP / 总来源 XP。
                for (int i = 0; i < skillDef.sourceSkills.Count; i++)
                {
                    string s = skillDef.sourceSkills[i];
                    if (string.IsNullOrEmpty(s)) continue;
                    SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(s);
                    string name = sd != null ? sd.LabelCap.ToString() : s;
                    float v = skillData.xpBySource.TryGetValue(s, out float val) ? val : 0f;
                    d.Sources.Add(new CareerLevelSource { Name = name, Share = v / exactTotal });
                }
                return d;
            }

            // 回退分支：按各 sourceSkill 当前等级推算占比（旧档无 xpBySource）。
            List<float> weights = new List<float>();
            float total = 0f;
            for (int i = 0; i < skillDef.sourceSkills.Count; i++)
            {
                string s = skillDef.sourceSkills[i];
                if (string.IsNullOrEmpty(s)) { weights.Add(0f); continue; }
                int sk = 0;
                if (skillData != null && skillData.xpBySource == null)
                {
                    // 纯旧档：按 skillData.level 整体占比不可再分，逐来源等分权重。
                    sk = skillData.level;
                }
                float w = sk > 0 ? sk : 1f;   // 未习得技能给最小权重，避免占比 0 导致条不可见
                weights.Add(w);
                total += w;
            }
            for (int i = 0; i < skillDef.sourceSkills.Count; i++)
            {
                string s = skillDef.sourceSkills[i];
                if (string.IsNullOrEmpty(s)) continue;
                SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(s);
                string name = sd != null ? sd.LabelCap.ToString() : s;
                float share = total > 0f ? weights[i] / total : 0f;
                d.Sources.Add(new CareerLevelSource { Name = name, Share = share });
            }
            return d;
        }

        /// <summary>距离下一档还差多少 XP（按通用参数化曲线精确计算）。无曲线参数时回退占位。</summary>
        private static int XpToNext(float currentXp, ProfessionalSkillDef def)
        {
            if (def == null || def.xpCap <= 0f || def.maxLevel <= 0) return 0;
            // 曲线幂指数统一走全局口径（铁律 6.5：禁止在调用点硬编码 Def 名）。
            return Mathf.CeilToInt(ProfessionalXpEvaluator.XpToNext(
                currentXp, def.maxLevel, def.xpCap, ProfessionalXpPolicyDef.DefaultCurvePower));
        }

        /// <summary>
        /// 资历按 <c>CareerEvent</c> 类型分项（用于悬停）。
        ///
        /// <para>⚠ 2026-09-19（CHG-20260919-ECON-UI-009）：原实现对**每一条事件**都做
        /// <c>Hours += 1</c>，于是悬停里出现「制造 5 次（5 h）」这类**两个数永远相等**的假时长。
        /// 现**只计完成次数**（故本方法产出的是 Count 而非 Hours）。</para>
        ///
        /// <para>⚠ 事件类型用**字符串字面量**匹配 —— 这是上游 <c>CareerEvent.EventType</c> 的既有
        /// 存储形态（存档里就是字符串），非本处引入；它是**稳定 ID**而非显示文本，
        /// 与 GOV-009「逻辑不依赖 label/翻译」不冲突。</para>
        /// </summary>
        private static SpanBreakdown CareerSpanBreakdown(CareerData cd)
        {
            SpanBreakdown b = new SpanBreakdown();
            if (cd == null || cd.Events == null) return b;
            for (int i = 0; i < cd.Events.Count; i++)
            {
                CareerEvent e = cd.Events[i];
                if (e == null) continue;
                switch (e.EventType)
                {
                    case "ItemProduced": b.CraftingCount++; break;
                    case "ConstructionCompleted": b.ConstructionCount++; break;
                    case "ResearchCompleted": b.ResearchCount++; break;
                    // BookProduced 与其余类型同归「其他」（原来分两个 case 但行为相同 ⇒ 合并）
                    default: b.OtherCount++; break;
                }
            }
            return b;
        }
    }
}