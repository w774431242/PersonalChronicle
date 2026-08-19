// ITab_Pawn_Career partial：职业规划适配分析输入构建（EnsureFit / BuildFitInput）。
// ARC-013 文件治理，物理切片零契约改动；见主文件 ITab_Pawn_Career.cs 类文档。
// 输入全部来自真实游戏事实：原版技能等级/兴趣（活读 pawn.skills）、
// 实践（ProfessionalState.practiceCountBySkill + CareerEvent 聚合）、品质分布（CareerEvent.Quality）。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
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

            // 原版技能等级 + 兴趣（活读游戏事实）。
            if (pawn != null && pawn.skills != null)
            {
                for (int i = 0; i < VanillaSkills.Length; i++)
                {
                    SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(VanillaSkills[i]);
                    if (sd == null) continue;
                    SkillRecord rec = pawn.skills.GetSkill(sd);
                    if (rec == null) continue;
                    input.SkillLevels[VanillaSkills[i]] = rec.Level;
                    int passion = rec.passion == Passion.Minor ? 50 : (rec.passion == Passion.Major ? 100 : 0);
                    input.Passion[VanillaSkills[i]] = passion;
                }
            }

            // 实践：ProfessionalState.practiceCountBySkill 优先，回退 CareerEvent 按 SkillDefName 聚合。
            if (po != null && po.CareerData != null)
            {
                if (po.CareerData.Professional != null && po.CareerData.Professional.practiceCountBySkill != null)
                {
                    foreach (KeyValuePair<string, int> kv in po.CareerData.Professional.practiceCountBySkill)
                    {
                        if (input.Practice.TryGetValue(kv.Key, out int existing))
                        {
                            input.Practice[kv.Key] = existing + Mathf.Max(0, kv.Value);
                        }
                        else
                        {
                            input.Practice[kv.Key] = Mathf.Max(0, kv.Value);
                        }
                    }
                }
                if (po.CareerData.Events != null)
                {
                    Dictionary<string, int> bySkill = new Dictionary<string, int>();
                    for (int i = 0; i < po.CareerData.Events.Count; i++)
                    {
                        CareerEvent ev = po.CareerData.Events[i];
                        if (ev == null || string.IsNullOrEmpty(ev.SkillDefName)) continue;
                        bySkill.TryGetValue(ev.SkillDefName, out int c);
                        bySkill[ev.SkillDefName] = c + 1;
                    }
                    foreach (KeyValuePair<string, int> kv in bySkill)
                    {
                        if (input.Practice.TryGetValue(kv.Key, out int existing))
                        {
                            input.Practice[kv.Key] = existing + kv.Value;
                        }
                        else
                        {
                            input.Practice[kv.Key] = kv.Value;
                        }
                    }
                    // 品质分布（真实事实）。
                    Dictionary<string, int> quality = new Dictionary<string, int>();
                    for (int i = 0; i < po.CareerData.Events.Count; i++)
                    {
                        CareerEvent ev = po.CareerData.Events[i];
                        if (ev == null || string.IsNullOrEmpty(ev.Quality)) continue;
                        quality.TryGetValue(ev.Quality, out int c);
                        quality[ev.Quality] = c + 1;
                    }
                    input.QualityCounts = quality;
                }
            }
            return input;
        }
    }
}
