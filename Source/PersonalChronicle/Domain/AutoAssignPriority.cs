using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// CHG-20261005-AUTOASSIGN · 纯逻辑：技能(+激情) → 工作优先级档位。
    ///
    /// <para>映射表不自建：原版 WorkTypeDef.relevantSkills 就是权威映射（全库 19 处消费，
    /// 含 Pawn_SkillTracker.AverageOfRelevantSkillsFor），本类只消费不复制。</para>
    ///
    /// <para>公式（全整数，可离线精确单测）：
    /// skillAvgX10 = round(avg * 10)（0..200）
    /// total = skillAvgX10*70/200 + (Major 30 / Minor 15 / None 0)
    /// raw = total>=70 ? 1 : total>=45 ? 2 : total>=20 ? 3 : 4
    /// return max(raw, floor(wt))</para>
    ///
    /// <para>S3 保护：relevantSkills 为空的工作类型一律【跳过不写】。这不是「写 0」——
    /// 写 0 会新增第二个 SetPriority(x,0) 调用点，而 0 是 EXC-2026-016 接管的哨兵值、
    /// 其授权点是全工程唯一的。「跳过」既满足永不高勾，又不碰 0。</para>
    /// </summary>
    public static class AutoAssignPriority
    {
        public const int ProtectedFloor = 2;
        public const int MinPriority = 1;

        private static readonly HashSet<string> ProtectedDefNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Hauling", "Cleaning", "Doctor", "Firefighter",
                "Patient", "PatientBedRest"
            };

        public static bool IsProtected(WorkTypeDef workType)
        {
            return workType != null && workType.defName != null
                && ProtectedDefNames.Contains(workType.defName);
        }

        /// <summary>档位硬钳：SetPriority 越界只 Log.Message 后照样写，必须我们钳。</summary>
        public static int Clamp(int priority)
        {
            if (priority < 0) { return 0; }
            return priority > Pawn_WorkSettings.LowestPriority
                ? Pawn_WorkSettings.LowestPriority : priority;
        }

        /// <summary>算出应写入的档位。false = 跳过不写（无 relevantSkills / 隐藏 / null）。</summary>
        public static bool TryCompute(WorkTypeDef workType, float avgRelevantSkill,
            Passion maxPassion, out int priority)
        {
            priority = 0;
            if (workType == null || !workType.visible)
            {
                return false;
            }
            if (workType.relevantSkills == null || workType.relevantSkills.Count == 0)
            {
                return false;
            }

            int scaled = (int)Math.Round(avgRelevantSkill * 10f);
            if (scaled < 0) { scaled = 0; }
            else if (scaled > 200) { scaled = 200; }

            int total = scaled * 70 / 200;
            if (maxPassion == Passion.Major) { total += 30; }
            else if (maxPassion == Passion.Minor) { total += 15; }

            int raw = total >= 70 ? 1 : (total >= 45 ? 2 : (total >= 20 ? 3 : 4));
            int floor = IsProtected(workType) ? ProtectedFloor : MinPriority;
            priority = Clamp(raw < floor ? floor : raw);
            return true;
        }
    }
}