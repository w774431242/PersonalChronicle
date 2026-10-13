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
    /// Partial of <see cref="ArchiveUiDataProvider"/> — 资格/前置检查行 Read Model 构建。
    /// </summary>
    /// <remarks>
    /// · 职责：QualRow（资格达成行）、PreCheckRow（前置条件行），输出可直接绑定的视图行。
    /// · 调用方：ArchiveUiDataProvider（详情快照装配链路）。
    /// · 禁止：labelKey 只传键不传字面文案，翻译一律走 LanguageData；判定口径不在此处定义。
    /// · 拆分：自 Detail.cs 物理拆分（机械切片，方法体原样）；private 成员跨 partial 文件可见，编译安全。
    /// </remarks>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {
        /// <summary>实践考试实时状态（对齐 P9 HTML：未报名/进行中/通过/未通过）。</summary>
        private static string PracticalStateText(CareerData cd, QualificationDef def)
        {
            if (!def.requiredExam) return "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate().ToString();
            if (cd == null || cd.Exams == null || cd.Exams.Practical == null || cd.Exams.Practical.Count == 0)
                return "PersonalChronicle.UI.Career.Qual.State.NotApplied".Translate().ToString();
            // 取本档最新一条
            PracticalExamRecord latest = null;
            for (int i = cd.Exams.Practical.Count - 1; i >= 0; i--)
            {
                if (cd.Exams.Practical[i] != null && cd.Exams.Practical[i].QualificationDefName == def.defName)
                {
                    latest = cd.Exams.Practical[i];
                    break;
                }
            }
            if (latest == null) return "PersonalChronicle.UI.Career.Qual.State.NotApplied".Translate().ToString();
            if (latest.Passed) return "PersonalChronicle.UI.Career.Qual.State.Passed".Translate(latest.Score.ToString("F0")).ToString();
            if (latest.Finished) return "PersonalChronicle.UI.Career.Qual.State.Failed".Translate(latest.Score.ToString("F0")).ToString();
            return "PersonalChronicle.UI.Career.Qual.State.InProgress".Translate().ToString();
        }

        /// <summary>理论考试实时状态（待提交/通过）。</summary>
        private static string TheoryStateText(CareerData cd, QualificationDef def)
        {
            if (!def.requiredExam) return "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate().ToString();
            if (cd == null || cd.Exams == null || cd.Exams.Theory == null || cd.Exams.Theory.Count == 0)
                return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
            for (int i = cd.Exams.Theory.Count - 1; i >= 0; i--)
            {
                TheoryExamRecord r = cd.Exams.Theory[i];
                if (r != null && r.QualificationDefName == def.defName)
                {
                    return r.Passed
                        ? "PersonalChronicle.UI.Career.Qual.State.Passed".Translate(r.Score.ToString("F0")).ToString()
                        : "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
                }
            }
            return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
        }

        /// <summary>论文/答辩实时状态（待进行/答辩待进行/通过）。</summary>
        private static string DefenseStateText(CareerData cd, QualificationDef def)
        {
            if (!def.requiredThesis && !def.requiredDefense) return "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate().ToString();
            if (cd == null || cd.Thesis == null) return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
            ThesisEvidence thesis = null;
            DefenseRecord defense = null;
            if (cd.Thesis.Theses != null)
            {
                for (int i = cd.Thesis.Theses.Count - 1; i >= 0; i--)
                {
                    if (cd.Thesis.Theses[i] != null && cd.Thesis.Theses[i].QualificationDefName == def.defName)
                    {
                        thesis = cd.Thesis.Theses[i];
                        break;
                    }
                }
            }
            if (cd.Thesis.Defenses != null)
            {
                for (int i = cd.Thesis.Defenses.Count - 1; i >= 0; i--)
                {
                    if (cd.Thesis.Defenses[i] != null && cd.Thesis.Defenses[i].QualificationDefName == def.defName)
                    {
                        defense = cd.Thesis.Defenses[i];
                        break;
                    }
                }
            }
            if (defense != null && defense.Passed && thesis != null && thesis.Completed)
                return "PersonalChronicle.UI.Career.Qual.State.ThesisDefense".Translate(thesis.ComputedScore.ToString("F0"), defense.FinalScore.ToString("F0")).ToString();
            if (thesis != null && thesis.Completed)
                return "PersonalChronicle.UI.Career.Qual.State.DefensePending".Translate().ToString();
            return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
        }

        /// <summary>评级评审实时状态：待结算/评审中（已 X/Y 个工作日）/已答复（授予）。
        /// 口径统一走 Domain 纯函数 QualificationReview.IsDue（2026-08-19 P1-4 对齐），UI 不重复实现业务判定。</summary>
        private static void ReviewStateText(CareerData cd, QualificationDef def, out string state, out bool met)
        {
            met = false;
            state = "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
            if (cd == null || cd.Qualification == null || def == null) return;
            QualificationProgress prog = cd.Qualification.Get(def.defName);
            if (prog == null) return;
            long startTick = prog.ReviewStartedTick;
            int reviewDays = prog.ReviewDays > 0 ? prog.ReviewDays : def.reviewDays;
            if (reviewDays <= 0) reviewDays = QualificationReview.DefaultReviewDays;
            if (startTick <= 0L)
            {
                state = "PersonalChronicle.UI.Career.Qual.State.ReviewPending".Translate().ToString();
                return;
            }
            long nowTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            // 与 ChronicleGameComponent / DrawDefenseBlock 同一判定：IsDue 纯函数。
            if (QualificationReview.IsDue(startTick, reviewDays, nowTick))
            {
                state = "PersonalChronicle.UI.Career.Qual.State.ReviewGranted".Translate().ToString();
                met = true;
            }
            else
            {
                long elapsedDays = (nowTick - startTick) / 60000L;
                if (elapsedDays < 0) elapsedDays = 0;
                state = "PersonalChronicle.UI.Career.Qual.State.ReviewInProgress".Translate(elapsedDays, reviewDays).ToString();
            }
        }

        private static CareerQualView QualRow(string labelKey, string note, bool met, string tooltip = null)
        {
            return new CareerQualView
            {
                Label = labelKey.Translate().ToString(),
                Note = note,
                StateKey = met ? "ok" : "wait",
                StateText = met ? "PersonalChronicle.UI.Career.Qual.Met".Translate().ToString()
                                   : "PersonalChronicle.UI.Career.Qual.Unmet".Translate().ToString(),
                Tooltip = tooltip
            };
        }

        /// <summary>资格档显示名（用于 Tooltip 引用）。走 titleDefName → ProfessionalTitleDef.defName → 翻译键。</summary>
        private static string QualTitleLabel(QualificationDef def)
        {
            if (def == null) return "--";
            if (!string.IsNullOrEmpty(def.titleDefName))
            {
                return ("PersonalChronicle.Professional.Title." + def.titleDefName + ".Label").Translate().ToString();
            }
            return def.LabelCap.ToString();
        }

        /// <summary>tick → 天/月/年自适应显示：≥ 1 年 用「X 年 Y 月 Z 日」；≥ 1 月 用「X 月 Y 日」；否则「X 日」。
        /// 单位约定：1 月 = 30 天，1 年 = 12 月 = 360 天（与原版季节 15 天/季解耦，便于跨 Tile 直观）。</summary>
        private static string FormatSpanHuman(long ticks)
        {
            const long TicksPerDay = 60000L;
            long days = ticks / TicksPerDay;
            if (days >= 360L)
            {
                long years = days / 360L;
                long months = (days % 360L) / 30L;
                long remDays = days % 30L;
                if (months > 0 && remDays > 0) return years + " 年 " + months + " 月 " + remDays + " 日";
                if (months > 0) return years + " 年 " + months + " 月";
                return years + " 年 " + remDays + " 日";
            }
            if (days >= 30L)
            {
                long months = days / 30L;
                long remDays = days % 30L;
                if (remDays > 0) return months + " 月 " + remDays + " 日";
                return months + " 月";
            }
            return days + " 日";
        }

        /// <summary>资格预检 6 条件（对齐前端：核心技能/职业履历/成果记录/实践考试/理论考试/论文答辩）。</summary>
        private static IReadOnlyList<CareerPreCheckView> BuildPreCheckRows(CareerData cd, QualificationDef nextQual, float composite)
        {
            List<CareerPreCheckView> rows = new List<CareerPreCheckView>();
            if (nextQual == null) return rows;
            int level = 0;
            if (cd.Professional != null && !string.IsNullOrEmpty(nextQual.professionalSkillDefName))
            {
                ProfessionalSkillData sd = cd.Professional.GetSkill(nextQual.professionalSkillDefName);
                if (sd != null) level = sd.level;
            }
            long span = CareerSpanTicks(cd);
            bool practical = HasPassedExam(cd, nextQual.defName, true);
            bool theory = HasPassedExam(cd, nextQual.defName, false);
            bool thesisDefense = (nextQual.requiredThesis || nextQual.requiredDefense)
                ? HasPassedThesisDefense(cd, nextQual.defName)
                : true;
            bool hasAchievements = cd.Events != null && cd.Events.Count > 0;

            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Skill",
                level >= nextQual.requiredMinLevel, true));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Resume",
                span >= nextQual.requiredCareerTimeTicks, true));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Achieve",
                hasAchievements, true));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Practical",
                practical, false));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Theory",
                theory, false));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Defense",
                thesisDefense, false));
            return rows;
        }

        private static CareerPreCheckView PreCheckRow(string labelKey, bool done, bool hasProgress)
        {
            string stateKey;
            string stateText;
            if (done)
            {
                stateKey = "done";
                stateText = "PersonalChronicle.UI.Career.Pre.Ready".Translate().ToString();
            }
            else if (hasProgress)
            {
                stateKey = "pending";
                stateText = "PersonalChronicle.UI.Career.Pre.Preparing".Translate().ToString();
            }
            else
            {
                stateKey = "not-started";
                stateText = "PersonalChronicle.UI.Career.Pre.NotStarted".Translate().ToString();
            }
            return new CareerPreCheckView
            {
                Label = labelKey.Translate().ToString(),
                StateKey = stateKey,
                StateText = stateText
            };
        }

        /// <summary>晋升准备度 = done 条件占比。</summary>
        private static int ComputeReadyPercent(IReadOnlyList<CareerPreCheckView> preCheck)
        {
            if (preCheck == null || preCheck.Count == 0) return 0;
            int done = 0;
            for (int i = 0; i < preCheck.Count; i++)
            {
                if (preCheck[i] != null && string.Equals(preCheck[i].StateKey, "done", System.StringComparison.Ordinal)) done++;
            }
            return (int)((float)done / preCheck.Count * 100f);
        }

        /// <summary>缺口 = 资格状态中未满足条件的 label 列表（封顶/无缺口 → 空）。</summary>
        private static IReadOnlyList<string> ComputeGaps(IReadOnlyList<CareerQualView> qual, IReadOnlyList<CareerPreCheckView> preCheck)
        {
            List<string> gaps = new List<string>();
            if (qual != null)
            {
                for (int i = 0; i < qual.Count; i++)
                {
                    CareerQualView q = qual[i];
                    if (q != null && string.Equals(q.StateKey, "wait", System.StringComparison.Ordinal) && !string.IsNullOrEmpty(q.Label))
                    {
                        gaps.Add(q.Label);
                    }
                }
            }
            if (gaps.Count == 0 && preCheck != null)
            {
                for (int i = 0; i < preCheck.Count; i++)
                {
                    CareerPreCheckView p = preCheck[i];
                    if (p != null && !string.Equals(p.StateKey, "done", System.StringComparison.Ordinal) && !string.IsNullOrEmpty(p.Label))
                    {
                        gaps.Add(p.Label);
                    }
                }
            }
            return gaps;
        }

        private static bool HasPassedExam(CareerData cd, string qualificationDefName, bool practical)
        {
            if (cd == null || cd.Exams == null || string.IsNullOrEmpty(qualificationDefName)) return false;
            if (practical && cd.Exams.Practical != null)
            {
                for (int i = 0; i < cd.Exams.Practical.Count; i++)
                {
                    PracticalExamRecord r = cd.Exams.Practical[i];
                    if (r != null && string.Equals(r.QualificationDefName, qualificationDefName, System.StringComparison.Ordinal) && r.Passed)
                    {
                        return true;
                    }
                }
            }
            if (!practical && cd.Exams.Theory != null)
            {
                for (int i = 0; i < cd.Exams.Theory.Count; i++)
                {
                    TheoryExamRecord r = cd.Exams.Theory[i];
                    if (r != null && string.Equals(r.QualificationDefName, qualificationDefName, System.StringComparison.Ordinal) && r.Passed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasPassedThesisDefense(CareerData cd, string qualificationDefName)
        {
            if (cd == null || string.IsNullOrEmpty(qualificationDefName)) return false;
            if (cd.Thesis != null && cd.Thesis.Theses != null)
            {
                for (int i = 0; i < cd.Thesis.Theses.Count; i++)
                {
                    ThesisEvidence t = cd.Thesis.Theses[i];
                    if (t != null && string.Equals(t.QualificationDefName, qualificationDefName, System.StringComparison.Ordinal) && t.Completed)
                    {
                        return true;
                    }
                }
            }
            if (cd.Thesis != null && cd.Thesis.Defenses != null)
            {
                for (int i = 0; i < cd.Thesis.Defenses.Count; i++)
                {
                    DefenseRecord d = cd.Thesis.Defenses[i];
                    if (d == null || !d.Passed) continue;
                    // 2026-08-19 验收 P1-4 对齐：优先按 QualificationDefName 精确匹配；
                    // 旧记录该字段为空时回退 ThesisId 匹配（兼容早期 DevTest 数据）。
                    // 与 Domain 权威判定（QualificationEvaluator.DefensePassed）同一口径。
                    if (!string.IsNullOrEmpty(d.QualificationDefName))
                    {
                        if (string.Equals(d.QualificationDefName, qualificationDefName, System.StringComparison.Ordinal)) return true;
                    }
                    else if (string.Equals(d.ThesisId, qualificationDefName, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static long CareerSpanTicks(CareerData cd)
        {
            if (cd == null || cd.Events == null || cd.Events.Count == 0) return 0L;
            long first = long.MaxValue;
            long last = 0L;
            for (int i = 0; i < cd.Events.Count; i++)
            {
                CareerEvent ev = cd.Events[i];
                if (ev == null) continue;
                if (ev.Tick < first) first = ev.Tick;
                if (ev.Tick > last) last = ev.Tick;
            }
            return first == long.MaxValue ? 0L : (last - first);
        }

        /// <summary>
        /// 职业档案 · 职称链（5 档，对齐 Defs/QualificationDefs.xml）。
        /// 状态：已获(granted) / 当前(current) / 下一阶(next) / 未开始(locked)。
        /// </summary>
        private static IReadOnlyList<CareerTitleTierView> BuildTitleTiers(PawnObject pawn)
        {
            List<CareerTitleTierView> tiers = new List<CareerTitleTierView>();
            if (pawn == null || pawn.CareerData == null)
            {
                return tiers;
            }
            CareerData cd = pawn.CareerData;
            List<QualificationDef> quals = DefDatabase<QualificationDef>.AllDefsListForReading
                .Where(q => q != null && !string.IsNullOrEmpty(q.titleDefName))
                .OrderBy(q => q.order)
                .ToList();
            if (quals.Count == 0)
            {
                return tiers;
            }
            // 当前档 index = 已授最高档；下一档 = 第一个未授予。
            int currentIdx = -1;
            for (int i = 0; i < quals.Count; i++)
            {
                if (QualificationEvaluator.HasGrantedTitleKey(cd, quals[i].titleDefName))
                {
                    currentIdx = i;
                }
            }
            int nextIdx = currentIdx + 1;
            for (int i = 0; i < quals.Count; i++)
            {
                QualificationDef q = quals[i];
                string defName = q.titleDefName;
                string label = TitleLabel(DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(defName));
                string stateKey;
                string stateText;
                if (i < currentIdx)
                {
                    stateKey = "granted";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Granted".Translate().ToString();
                }
                else if (i == currentIdx)
                {
                    stateKey = "current";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Current".Translate().ToString();
                }
                else if (i == nextIdx)
                {
                    stateKey = "next";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Next".Translate().ToString();
                }
                else
                {
                    stateKey = "locked";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Locked".Translate().ToString();
                }
                tiers.Add(new CareerTitleTierView
                {
                    DefName = defName,
                    Label = label,
                    StateKey = stateKey,
                    StateText = stateText,
                    Note = "PersonalChronicle.UI.Career.Title.Requires".Translate(q.requiredMinLevel).ToString()
                });
            }
            return tiers;
        }
    }
}