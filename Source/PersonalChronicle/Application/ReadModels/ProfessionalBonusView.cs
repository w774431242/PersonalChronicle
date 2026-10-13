// 职业加成 Read Model 快照（属性面板「专业等级」字段浮层用）。
//
// 铁律：UI 只消费快照，不在窗口内做排序 / null-guard / Def 查询（GOV-006/PM 规范）。
// 本文件只承载**展示口径的派生**，不复制 Resolver 的加成公式——数值一律来自
// ProfessionalEffectResolver.ResolveContributions（单一真源）。
using System.Collections.Generic;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>单个专业技能的加成明细（浮层一行）。</summary>
    public sealed class ProfessionalBonusLine
    {
        /// <summary>ProfessionalSkillDef.defName。</summary>
        public string SkillDefName;

        /// <summary>已解析的展示名（翻译键优先，失败回退 defName）。</summary>
        public string DisplayLabel;

        /// <summary>该技能当前等级。</summary>
        public int Level;

        /// <summary>评级展示名（ProfessionalRatingDef.LabelCap；无则 "—"）。</summary>
        public string RatingLabel;

        /// <summary>速度加成百分比（6.0 = +6%）。</summary>
        public float SpeedPct;

        /// <summary>品质档位偏移（可正可负）。</summary>
        public int QualityLevels;

        /// <summary>常驻 Stat 加成百分比（5 = +5%；建造速度/医疗质量等）。</summary>
        public float GenericPct;

        /// <summary>常驻加成目标 Stat 展示名（已本地化；无则 null）。</summary>
        public string GenericStatLabel;

        /// <summary>当前工作/配方是否命中该技能实践白名单（false = 本次不生效）。</summary>
        public bool ActiveNow;
    }

    /// <summary>
    /// 职业加成快照：逐技能明细 + 两组合计（当前生效 / 理论上限）。
    /// 口径说明（PM 定）：加成是「配方/工作类型条件生效」的，故同时给出
    ///   · 当前生效：按 pawn 当前 Job 的工作类型折算的真实加成
    ///   · 理论上限：忽略白名单、假设全部技能命中的加成
    /// </summary>
    public sealed class ProfessionalBonusView
    {
        public List<ProfessionalBonusLine> Lines = new List<ProfessionalBonusLine>();

        /// <summary>当前从事的工作类型展示名；null = 当前未从事可判定工作（发呆/战斗/睡眠等）。</summary>
        public string CurrentWorkTypeLabel;

        /// <summary>当前生效的速度加成百分比（仅计 ActiveNow）。</summary>
        public float CurrentSpeedPct;

        /// <summary>理论上限速度加成百分比（全部技能命中）。</summary>
        public float MaxSpeedPct;

        /// <summary>当前生效的品质偏移档位。</summary>
        public int CurrentQualityLevels;

        /// <summary>理论上限品质偏移档位。</summary>
        public int MaxQualityLevels;

        /// <summary>是否有任何可展示的加成（false = 专业技能全为 0 级或无效果声明）。</summary>
        public bool HasAny => Lines != null && Lines.Count > 0;

        /// <summary>是否有当前生效的加成（false = 当前工作不生效）。</summary>
        public bool HasActiveNow
        {
            get
            {
                if (Lines == null) return false;
                for (int i = 0; i < Lines.Count; i++)
                    if (Lines[i] != null && Lines[i].ActiveNow) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// 职业加成快照构建器（Application 层，static 工具类）。
    /// 链路：PawnObject.CareerData.Professional → Resolver 明细 → 展示快照。
    /// 只读，绝不写回事实层。
    /// </summary>
    public static class ProfessionalBonusSnapshotBuilder
    {
        /// <summary>构建快照；pawn 无职业数据 / 无专业技能时返回空快照（HasAny=false）。</summary>
        public static ProfessionalBonusView Build(Pawn pawn)
        {
            ProfessionalBonusView view = new ProfessionalBonusView();
            if (pawn == null) return view;

            List<ProfessionalSkillData> skills = ProfessionalEffectService.GetPrimaryDirectionSkills(pawn);
            if (skills == null || skills.Count == 0) return view;

            List<ProfessionalSkillDef> skillDefs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            Dictionary<string, ProfessionalEffectDef> effectDefs = ProfessionalEffectIndex.EffectDefs;
            if (effectDefs == null || effectDefs.Count == 0)
            {
                effectDefs = ProfessionalEffectIndex.EffectDefs;
            }
            if (skillDefs == null || skillDefs.Count == 0 || effectDefs == null) return view;

            // 当前工作类型（Job → workGiverDef.workType；无 Job/无 workGiver → null）
            WorkTypeDef cur = CurrentWorkType(pawn);
            view.CurrentWorkTypeLabel = cur != null ? SafeLabel(cur) : null;
            string workTypeDefName = cur != null ? cur.defName : null;

            // 传在岗车间的评级档位偏移：浮层明细必须与真实注入值同口径（GOV-006 单一真源），
            // 否则在岗吃评级加成的 pawn 会看到"明细对不上实际值"。
            List<ProfessionalEffectResolver.SkillBonusContribution> contributions =
                ProfessionalEffectResolver.ResolveContributions(
                    skills, skillDefs, effectDefs, null, workTypeDefName,
                    ProfessionalEffectIndex.RatingDefs,
                    Effects.WorkshopRatingShift.Of(pawn));

            for (int i = 0; i < contributions.Count; i++)
            {
                ProfessionalEffectResolver.SkillBonusContribution c = contributions[i];
                if (c == null) continue;

                ProfessionalRatingDef rating = c.RatingDefName != null
                    ? DefDatabase<ProfessionalRatingDef>.GetNamedSilentFail(c.RatingDefName)
                    : null;

                view.Lines.Add(new ProfessionalBonusLine
                {
                    SkillDefName = c.SkillDefName,
                    DisplayLabel = SkillLabel(c.SkillDefName),
                    Level = c.Level,
                    RatingLabel = rating != null ? rating.LabelCap : "—",
                    SpeedPct = c.SpeedBonus * 100f,
                    QualityLevels = c.QualityLevels,
                    GenericPct = c.GenericBonus * 100f,
                    GenericStatLabel = StatLabel(c.GenericStatDefName),
                    ActiveNow = c.ActiveNow
                });

                view.MaxSpeedPct += c.SpeedBonus * 100f;
                view.MaxQualityLevels += c.QualityLevels;
                if (c.ActiveNow)
                {
                    view.CurrentSpeedPct += c.SpeedBonus * 100f;
                    view.CurrentQualityLevels += c.QualityLevels;
                }
            }
            return view;
        }

        /// <summary>当前从事的工作类型；无法判定返回 null（发呆/战斗/睡眠等）。</summary>
        private static WorkTypeDef CurrentWorkType(Pawn pawn)
        {
            try
            {
                Job job = pawn?.CurJob;
                WorkGiverDef wg = job?.workGiverDef;
                return wg != null ? wg.workType : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 技能展示名：Keyed "PersonalChronicle.Professional.Skill.&lt;defName&gt;.Label" 优先，
        /// 翻译缺失时回退 defName（与 ITab 现有口径一致）。
        /// </summary>
        private static string StatLabel(string statDefName)
        {
            if (string.IsNullOrEmpty(statDefName)) return null;
            try
            {
                StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statDefName);
                if (stat != null && !string.IsNullOrEmpty(stat.LabelCap))
                {
                    return stat.LabelCap;
                }
            }
            catch { /* Def 缺失 → 回退 defName */ }
            return statDefName;
        }

        private static string SkillLabel(string skillDefName)
        {
            if (string.IsNullOrEmpty(skillDefName)) return "";
            string key = "PersonalChronicle.Professional.Skill." + skillDefName + ".Label";
            try
            {
                if (key.CanTranslate())
                {
                    string t = key.Translate().ToString();
                    if (!string.IsNullOrEmpty(t) && t != key) return t;
                }
            }
            catch { /* 翻译异常 → 回退 defName */ }
            return skillDefName;
        }

        private static string SafeLabel(WorkTypeDef wt)
        {
            if (wt == null) return "";
            try { return string.IsNullOrEmpty(wt.LabelCap) ? wt.defName : wt.LabelCap; }
            catch { return wt.defName ?? ""; }
        }
    }
}
