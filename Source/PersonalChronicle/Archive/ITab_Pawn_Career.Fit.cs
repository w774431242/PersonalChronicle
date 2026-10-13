// ITab_Pawn_Career partial：职业规划适配分析输入构建（EnsureFit / BuildFitInput）。
// ARC-013 文件治理，物理切片零契约改动；见主文件 ITab_Pawn_Career.cs 类文档。
// 输入全部来自真实游戏事实：
//   · 原版技能等级/兴趣/禁用（活读 pawn.skills，TotallyDisabled 记入 DisabledSkills）；
//   · 背景先天加成（pawn.story.Childhood/Adulthood 的 BackstoryDef.skillGains，实时读 Def）；
//   · 特性加成（pawn.story.traits.allTraits 的 Trait.CurrentData.skillGains，实时读 Def）；
//   · 实践（practiceCountBySkill 经 sourceSkills 映射回原版键；旧档回退 CareerEvent 聚合）；
//   · 品质分布（CareerEvent.Quality 按 SkillDefName 归集，行为佐证维度）。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // ================= 职业规划 fit 数据 =================
        private void EnsureFit(Pawn pawn, DetailSnapshot snap)
        {
            try
            {
                string stableId = pawn != null ? pawn.GetUniqueLoadID() : null;
                long revision = snap != null ? snap.BuiltFromRevision : -1L;
                if (fitResults != null && fitResults.Count > 0
                    && fitPawnId == stableId && fitRevision == revision)
                {
                    return;
                }
                fitPawnId = stableId;
                fitRevision = revision;
                ProfessionalFitInput input = BuildFitInput(pawn, snap);
                fitResults = ProfessionalFitAnalyzer.Analyze(input);
            }
            catch (Exception ex)
            {
                Log.WarningOnce("PersonalChronicle: career fit analysis failed: " + ex.Message, 0x5C12A3);
                fitResults = new List<ProfessionalFitResult>();
            }
        }

        private static ProfessionalFitInput BuildFitInput(Pawn pawn, DetailSnapshot snap)
        {
            ProfessionalFitInput input = new ProfessionalFitInput();
            PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
            ReadVanillaSkills(pawn, input);
            ReadIntrinsicAptitude(pawn, input);
            ReadPracticeAndQuality(po, input);
            return input;
        }

        /// <summary>原版技能等级 + 兴趣；禁用技能（TotallyDisabled）记入 DisabledSkills 后跳过。</summary>
        private static void ReadVanillaSkills(Pawn pawn, ProfessionalFitInput input)
        {
            if (pawn == null || pawn.skills == null)
            {
                return;
            }
            for (int i = 0; i < VanillaSkills.Count; i++)
            {
                SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(VanillaSkills[i]);
                if (sd == null) continue;
                SkillRecord rec = pawn.skills.GetSkill(sd);
                if (rec == null) continue;
                if (rec.TotallyDisabled)
                {
                    input.DisabledSkills.Add(VanillaSkills[i]);
                    continue;
                }
                input.SkillLevels[VanillaSkills[i]] = rec.Level;
                int passion = rec.passion == Passion.Minor ? 50 : (rec.passion == Passion.Major ? 100 : 0);
                input.Passion[VanillaSkills[i]] = passion;
            }
        }

        /// <summary>背景（童年/成年）与特性的先天技能加成，实时读原版 Def，零持久化。</summary>
        private static void ReadIntrinsicAptitude(Pawn pawn, ProfessionalFitInput input)
        {
            if (pawn == null || pawn.story == null)
            {
                return;
            }
            AccumulateSkillGains(pawn.story.Childhood != null ? pawn.story.Childhood.skillGains : null, input.BackstoryGains);
            AccumulateSkillGains(pawn.story.Adulthood != null ? pawn.story.Adulthood.skillGains : null, input.BackstoryGains);
            List<Trait> traits = (pawn.story.traits != null && pawn.story.traits.allTraits != null)
                ? pawn.story.traits.allTraits
                : null;
            if (traits == null)
            {
                return;
            }
            for (int i = 0; i < traits.Count; i++)
            {
                Trait trait = traits[i];
                if (trait == null || trait.CurrentData == null) continue;
                AccumulateSkillGains(trait.CurrentData.skillGains, input.TraitGains);
            }
        }

        /// <summary>SkillGain 列表按原版 SkillDefName 累加（键 = defName，值 = amount 合计）。</summary>
        private static void AccumulateSkillGains(List<SkillGain> gains, Dictionary<string, int> into)
        {
            if (gains == null)
            {
                return;
            }
            for (int i = 0; i < gains.Count; i++)
            {
                SkillGain g = gains[i];
                if (g == null || g.skill == null || string.IsNullOrEmpty(g.skill.defName)) continue;
                into.TryGetValue(g.skill.defName, out int acc);
                into[g.skill.defName] = acc + g.amount;
            }
        }

        /// <summary>
        /// 实践 + 品质。实践：practiceCountBySkill 优先（键可能为 PC 职业技能 defName，
        /// 经 sourceSkills 映射回原版键）；映射后无任何条目才回退 CareerEvent 聚合
        /// （二者同源，同时使用会双重计数）。
        /// </summary>
        private static void ReadPracticeAndQuality(PawnObject po, ProfessionalFitInput input)
        {
            if (po == null || po.CareerData == null)
            {
                return;
            }
            ProfessionalState professional = po.CareerData.Professional;
            if (professional != null && professional.practiceCountBySkill != null)
            {
                foreach (KeyValuePair<string, int> kv in professional.practiceCountBySkill)
                {
                    AccumulatePractice(input, kv.Key, Mathf.Max(0, kv.Value));
                }
            }
            if (input.Practice.Count == 0)
            {
                AggregateEventPractice(po.CareerData, input);
            }
            AggregateEventQuality(po.CareerData, input);
        }

        /// <summary>单条实践计数累加：PC 职业技能键 → 按 sourceSkills 摊到原版技能；原版键直接用。</summary>
        private static void AccumulatePractice(ProfessionalFitInput input, string rawKey, int count)
        {
            if (string.IsNullOrEmpty(rawKey) || count <= 0)
            {
                return;
            }
            ProfessionalSkillDef pc = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(rawKey);
            if (pc != null && pc.sourceSkills != null && pc.sourceSkills.Count > 0)
            {
                for (int i = 0; i < pc.sourceSkills.Count; i++)
                {
                    BumpPractice(input, pc.sourceSkills[i], count);
                }
                return;
            }
            BumpPractice(input, rawKey, count);
        }

        private static void BumpPractice(ProfessionalFitInput input, string vanillaKey, int count)
        {
            if (string.IsNullOrEmpty(vanillaKey) || count <= 0)
            {
                return;
            }
            input.Practice.TryGetValue(vanillaKey, out int acc);
            input.Practice[vanillaKey] = acc + count;
        }

        /// <summary>旧档回退：CareerEvent 按 SkillDefName 聚合实践（仅 practiceCountBySkill 映射为空时调用）。</summary>
        private static void AggregateEventPractice(CareerData data, ProfessionalFitInput input)
        {
            if (data == null || data.Events == null)
            {
                return;
            }
            for (int i = 0; i < data.Events.Count; i++)
            {
                CareerEvent ev = data.Events[i];
                if (ev == null || string.IsNullOrEmpty(ev.SkillDefName)) continue;
                BumpPractice(input, ev.SkillDefName, 1);
            }
        }

        /// <summary>品质分布按 SkillDefName 归集（分技能维度）；全局口径同步保留供回退。</summary>
        private static void AggregateEventQuality(CareerData data, ProfessionalFitInput input)
        {
            if (data == null || data.Events == null)
            {
                return;
            }
            for (int i = 0; i < data.Events.Count; i++)
            {
                CareerEvent ev = data.Events[i];
                if (ev == null || string.IsNullOrEmpty(ev.Quality)) continue;
                if (!input.QualityCounts.TryGetValue(ev.Quality, out int gTotal))
                {
                    gTotal = 0;
                }
                input.QualityCounts[ev.Quality] = gTotal + 1;
                if (string.IsNullOrEmpty(ev.SkillDefName)) continue;
                if (!input.QualityBySkill.TryGetValue(ev.SkillDefName, out Dictionary<string, int> perSkill))
                {
                    perSkill = new Dictionary<string, int>();
                    input.QualityBySkill[ev.SkillDefName] = perSkill;
                }
                perSkill.TryGetValue(ev.Quality, out int gSkill);
                perSkill[ev.Quality] = gSkill + 1;
            }
        }
    }
}
