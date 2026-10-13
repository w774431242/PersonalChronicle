using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间加成解析器（落地管线 Step 2：Domain 纯逻辑，可离线单测）。
    ///
    /// <para>⚠ 2026-09-21（CHG-SHOP-AUDIT-001 · A-1）：本文件原定义的
    /// <c>WorkshopEffectiveBonus</c> 快照已上移至
    /// <c>Application/Effects/WorkshopBonusData.cs</c>（Effects/Capture 需消费它，
    /// 留在 Automation 命名空间会造成跨层类型依赖）。</para>
    ///
    /// 输入 = 车间类型 Def + pawn；输出 = 该 pawn 在该车间的实际加成快照。
    /// 公式（蓝图 §加成公式）：
    ///   effective = 1 + (defValue − 1) × scale
    ///   scale     = levelFactor × cover × synergy，封顶 1.0
    ///   levelFactor = 主技能原版等级均值 / 20
    ///   cover       = 主技能中等级≥1 的占比
    ///   synergy     = 方向对齐 ? 1.0 : 1/1.10
    ///
    /// 因为 defValue 是"对齐上限"，scale=1 时正好取到它；不对齐时 scale 被 1/1.10
    /// 压低，加成向 1.0 收缩（降幅变小 / 增幅变小），与蓝图"方向对齐 vs 满级无方向"一致。
    ///
    /// 性能：本方法<b>不在热路径</b>——只在 pawn 上岗（NotifyDutyStarted）等事件里算一次并缓存。
    /// </summary>
    public static class WorkshopBonusResolver
    {
        /// <summary>原版技能满级（用于 levelFactor 归一化）。</summary>
        private const float VanillaSkillMax = 20f;

        /// <summary>方向协同系数（对齐=1.0；不对齐除以它）。蓝图示协同满级 ×1.10。</summary>
        private const float SynergyBonus = 1.10f;

        /// <summary>解析 pawn 在该车间的实际加成。def/pawn 任一为空 → 中性。</summary>
        public static WorkshopEffectiveBonus Resolve(WorkshopTypeDef def, Pawn pawn)
        {
            WorkshopEffectiveBonus bonus = WorkshopEffectiveBonus.Neutral;
            if (def == null || pawn == null)
            {
                return bonus;
            }
            // 专向风格 buff（2026-09-25）：数值即面板显示数值，无技能缩放 —— 显示 = 生效。
            if (def.workSpeedBonusPct != 0f)
            {
                // 工作速度 +pct% ⇒ 工时因子 = 1/(1+pct/100)（StatPart 再取倒数得速度倍率）。
                bonus.workTimeFactor = 1f / (1f + def.workSpeedBonusPct / 100f);
            }
            if (def.qualityBonus != 0)
            {
                bonus.qualityOffset = def.qualityBonus;
            }
            if (def.researchSpeedBonusPct != 0f)
            {
                bonus.researchSpeedFactor = 1f + def.researchSpeedBonusPct / 100f;
            }
            return bonus;
        }

        /// <summary>纯函数版（便于单测：显式传入等级/覆盖度/是否对齐）。</summary>
        public static WorkshopEffectiveBonus ResolveScaled(WorkshopTypeDef def, float levelFactor, float cover, bool aligned)
        {
            WorkshopEffectiveBonus bonus = WorkshopEffectiveBonus.Neutral;
            if (def == null)
            {
                return bonus;
            }
            float scale = Clamp01(levelFactor) * Clamp01(cover) * (aligned ? 1f : 1f / SynergyBonus);
            if (scale <= 0f)
            {
                return bonus;
            }
            bonus.workTimeFactor = ScaleFactor(def.workTimeFactor, scale);
            bonus.stuffCostFactor = ScaleFactor(def.stuffCostFactor, scale);
            bonus.yieldFactor = ScaleFactor(def.yieldFactor, scale);
            bonus.accuracyFactor = ScaleFactor(def.accuracyFactor, scale);
            bonus.maxHpFactor = ScaleFactor(def.maxHpFactor, scale);
            bonus.goodRateFactor = ScaleFactor(def.goodRateFactor, scale);
            bonus.throughputFactor = ScaleFactor(def.throughputFactor, scale);
            bonus.repairSpeedFactor = ScaleFactor(def.repairSpeedFactor, scale);
            bonus.researchSpeedFactor = ScaleFactor(def.researchSpeedFactor, scale);
            bonus.qualityOffset = ScaleOffset(def.qualityOffset, scale);
            bonus.ratingOffset = ScaleOffset(def.ratingOffset, scale);
            return bonus;
        }

        // ---- 内部 -----------------------------------------------------------

        private static float Scale(WorkshopTypeDef def, Pawn pawn)
        {
            return Clamp01(LevelFactor(def, pawn)) * Clamp01(CoverFactor(def, pawn))
                 * (IsAligned(def, pawn) ? 1f : 1f / SynergyBonus);
        }

        /// <summary>因子：以 1.0 为轴按 scale 收缩（&lt;1 的降幅变小，&gt;1 的增幅变小）。</summary>
        private static float ScaleFactor(float defValue, float scale)
        {
            return 1f + (defValue - 1f) * scale;
        }

        /// <summary>档位偏移：按 scale 缩放后四舍五入（不足半档归零）。</summary>
        private static int ScaleOffset(int defValue, float scale)
        {
            if (defValue == 0)
            {
                return 0;
            }
            return (int)Math.Round(defValue * scale, MidpointRounding.AwayFromZero);
        }

        /// <summary>主技能原版等级均值 / 20；无主技能声明或全无等级 → 0（无加成）。</summary>
        private static float LevelFactor(WorkshopTypeDef def, Pawn pawn)
        {
            if (def.skillDefNames == null || def.skillDefNames.Count == 0 || pawn.skills == null)
            {
                // 无主技能声明（如物流车间）→ 该维度不按技能缩放，返回满值。
                return 1f;
            }
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < def.skillDefNames.Count; i++)
            {
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(def.skillDefNames[i]);
                if (skillDef == null)
                {
                    continue;
                }
                SkillRecord record = pawn.skills.GetSkill(skillDef);
                if (record == null)
                {
                    continue;
                }
                sum += record.Level;
                n++;
            }
            if (n == 0)
            {
                return 0f;
            }
            return Clamp01(sum / n / VanillaSkillMax);
        }

        /// <summary>覆盖度：主技能中等级≥1 的占比（无声明 → 满）。</summary>
        private static float CoverFactor(WorkshopTypeDef def, Pawn pawn)
        {
            if (def.skillDefNames == null || def.skillDefNames.Count == 0)
            {
                return 1f;
            }
            if (pawn.skills == null)
            {
                return 0f;
            }
            int present = 0;
            int total = 0;
            for (int i = 0; i < def.skillDefNames.Count; i++)
            {
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(def.skillDefNames[i]);
                if (skillDef == null)
                {
                    continue;
                }
                total++;
                SkillRecord record = pawn.skills.GetSkill(skillDef);
                if (record != null && record.Level >= 1)
                {
                    present++;
                }
            }
            return total == 0 ? 1f : Clamp01(present / (float)total);
        }

        /// <summary>方向对齐：pawn 的 primaryDirection 命中车间 direction。</summary>
        private static bool IsAligned(WorkshopTypeDef def, Pawn pawn)
        {
            if (string.IsNullOrEmpty(def.direction))
            {
                return false;
            }
            string primary = PrimaryDirectionOf(pawn);
            return !string.IsNullOrEmpty(primary)
                && string.Equals(primary, def.direction, StringComparison.Ordinal);
        }

        /// <summary>
        /// 取 pawn 的主修方向（经 Application 门面 ChroniclePawnAccess →
        /// PawnObject.CareerData.Professional；Automation 层不直接摸 GameComponent）。
        /// 任意环节缺失返回 null（零协同，不崩）。
        /// </summary>
        private static string PrimaryDirectionOf(Pawn pawn)
        {
            PawnObject pawnObject = ChroniclePawnAccess.GetArchivedPawnObject(pawn);
            if (pawnObject == null || pawnObject.CareerData == null || pawnObject.CareerData.Professional == null)
            {
                return null;
            }
            return pawnObject.CareerData.Professional.primaryDirection;
        }

        private static float Clamp01(float v)
        {
            if (v <= 0f)
            {
                return 0f;
            }
            if (v >= 1f)
            {
                return 1f;
            }
            return v;
        }
    }
}
