using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业技能 Pawn 状态（V2.0 §5 / P2-A §4.1）。
    /// 由 CareerEvent 事实派生（XP/Level/Mastery），append-only：能力 XP 只增不减。
    /// 绝不反向写回 CareerEvent（事实层不可污染）。
    /// </summary>
    public sealed class ProfessionalSkillData : IExposable
    {
        /// <summary>ProfessionalSkillDef.defName。</summary>
        public string skillDefName;

        /// <summary>累计经验。</summary>
        public float xp;

        /// <summary>当前等级（由 xpCurve 派生，持久化便于显示）。</summary>
        public int level;

        /// <summary>熟练度 0~100（由 levelCurve 派生）。</summary>
        public float mastery;

        /// <summary>首次获得技能 tick。</summary>
        public long firstAcquiredTick;

        /// <summary>末次实践 tick。</summary>
        public long lastPracticeTick;

        /// <summary>累计实践次数。</summary>
        public int practiceCount;

        /// <summary>能力维度 XP（key=abilityKey，独立成长 V2.0 §8）。</summary>
        public Dictionary<string, float> abilityXp = new Dictionary<string, float>();

        /// <summary>扩展统计（如各品质制造次数，append-only）。</summary>
        public Dictionary<string, int> statistics = new Dictionary<string, int>();

        /// <summary>
        /// XP 来源精确拆分（key=原版技能 defName，如 Crafting/Intellectual）。
        /// append-only：每次注入按来源技能累加，供浮层来源占比精确计算（V2.0+ 通用体系）。
        /// </summary>
        public Dictionary<string, float> xpBySource = new Dictionary<string, float>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref skillDefName, "skillDefName");
            Scribe_Values.Look(ref xp, "xp", 0f);
            Scribe_Values.Look(ref level, "level", 0);
            Scribe_Values.Look(ref mastery, "mastery", 0f);
            Scribe_Values.Look(ref firstAcquiredTick, "firstAcquiredTick", 0L);
            Scribe_Values.Look(ref lastPracticeTick, "lastPracticeTick", 0L);
            Scribe_Values.Look(ref practiceCount, "practiceCount", 0);
            Scribe_Collections.Look(ref abilityXp, "abilityXp", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref statistics, "statistics", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref xpBySource, "xpBySource", LookMode.Value, LookMode.Value);
            if (abilityXp == null) abilityXp = new Dictionary<string, float>();
            if (statistics == null) statistics = new Dictionary<string, int>();
            if (xpBySource == null) xpBySource = new Dictionary<string, float>();
        }

        /// <summary>能力维度 XP 读取（null-safe）。</summary>
        public float GetAbilityXp(string abilityKey)
        {
            if (string.IsNullOrEmpty(abilityKey))
            {
                return 0f;
            }
            if (abilityXp != null && abilityXp.TryGetValue(abilityKey, out float v))
            {
                return v;
            }
            return 0f;
        }

        /// <summary>按原版来源技能累加 XP（append-only）。</summary>
        public void AddXpBySource(string vanillaSkillName, float amount)
        {
            if (string.IsNullOrEmpty(vanillaSkillName) || amount <= 0f)
            {
                return;
            }
            if (xpBySource == null)
            {
                xpBySource = new Dictionary<string, float>();
            }
            if (xpBySource.TryGetValue(vanillaSkillName, out float v))
            {
                xpBySource[vanillaSkillName] = v + amount;
            }
            else
            {
                xpBySource[vanillaSkillName] = amount;
            }
        }
    }
}
