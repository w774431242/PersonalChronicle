using System;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 车间**胜任度评分**（S3-4 前置 · 2026-09-23）。
    ///
    /// <para><b>口径（本 mod 自研，透明可调）</b>：满分 100 =
    /// 技能 60（<c>min(等级, 20) / 20 × 60</c>，即每级 3 分）
    /// + 激情 20（Major 20 / Minor 10 / None 0）
    /// + 心情 20（<c>clamp01(心情) × 20</c>）。</para>
    ///
    /// <para><b>为什么放 Domain</b>：纯算术、零引擎依赖 ⇒ 走纯函数以便**离线单测**；
    /// UI 只显示结果、不重算阈值（LAND-006 / ARC-006）。调用方负责把
    /// <c>SkillRecord.Level</c> / <c>Passion</c> 档位 / <c>mood.CurLevelPercentage</c>
    /// 折算成本函数的入参。</para>
    ///
    /// <para><b>不可用者</b>：调用方传 <c>available = false</c>（倒下 / 死亡 / 未在场）时返回
    /// <see cref="Unavailable"/>（-1）—— UI 据此置灰并排到末尾，**不参与**正常排序，
    /// 而不是给它一个低分（低分与"不可用"是两件事）。</para>
    /// </summary>
    public static class WorkshopCrewScoring
    {
        /// <summary>满分（技能 60 + 激情 20 + 心情 20）。</summary>
        public const int MaxScore = 100;

        /// <summary>不可用标记（调用方按"置灰 + 排末尾"处理）。</summary>
        public const int Unavailable = -1;

        /// <summary>
        /// 计算胜任度。
        /// </summary>
        /// <param name="skillLevel">该配方对应技能等级（0–20；越界自动夹取）。</param>
        /// <param name="passionLevel">激情档：0 = 无 / 1 = 次要 / 2 = 主要（其他值按 0 处理）。</param>
        /// <param name="mood01">心情占比（0–1；越界夹取）。</param>
        /// <param name="available">是否可上岗（false ⇒ 返回 <see cref="Unavailable"/>）。</param>
        public static int Score(int skillLevel, int passionLevel, float mood01, bool available)
        {
            if (!available)
            {
                return Unavailable;
            }
            int level = Clamp(skillLevel, 0, 20);
            int skillPart = level * 60 / 20;
            int passionPart = passionLevel >= 2 ? 20 : (passionLevel == 1 ? 10 : 0);
            int moodPart = (int)Math.Round(Clamp01(mood01) * 20.0);
            return skillPart + passionPart + moodPart;
        }

        private static int Clamp(int value, int lo, int hi)
        {
            return value < lo ? lo : (value > hi ? hi : value);
        }

        private static double Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0.0;
            }
            return value >= 1f ? 1.0 : value;
        }
    }
}
