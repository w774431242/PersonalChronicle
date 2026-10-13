using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业效果类型（V2.0 §10/§11）。P2 只定义结构；Resolver/Adapter 实现留 P3。
    /// 2026-08-22 P0 v2.0 扩展：新增 YieldBonus/XpBonus/StatBonus/CombatStat 四种（多维加成前置）。
    /// </summary>
    public enum ProfessionalEffectKind
    {
        WorkSpeed,          // 工作速度修正（statDefName 指向专业基调官方 Stat）
        QualityBias,        // 品质倾向修正
        RecipeUnlock,       // 配方资格（P2 批）
        MaterialEfficiency, // 材料效率（P2 批）
        SpecialWork,        // 特殊制造工作（P3 批）
        YieldBonus,         // 产量加成（PlantHarvestYield/ButcheryFleshEfficiency 等）
        XpBonus,            // 学习系数（专业经验成长倍率）
        StatBonus,          // 通用 Stat 加成（成功率/交易价格/美观/携带等副维度）
        CombatStat,         // 战斗类 Stat 加成（武器伤害/射程/护甲穿透）
    }

    /// <summary>
    /// 专业效果 Def（Effect Library 声明层）。效果强度经 Rating 档位聚合后决定生效值（P3）。
    /// </summary>
    public sealed class ProfessionalEffectDef : Def
    {
        /// <summary>效果类型。</summary>
        public ProfessionalEffectKind kind;

        /// <summary>效果强度（如 0.05 = +5% 速度）。</summary>
        public float value;

        /// <summary>目标 StatDef defName（kind=WorkSpeed/StatBonus/CombatStat/YieldBonus 时必填；空=由 Resolver 按上下文解析）。</summary>
        public string statDefName;

        /// <summary>
        /// 语义翻转标识（2026-08-22 v2.0）：true = "越低越好"的 Stat（如食物中毒率 FoodPoisonChance），
        /// value 为正向减少量（0.05 = 中毒率 -5%）。false = 默认"越高越好"。
        /// </summary>
        public bool isReduction;

        /// <summary>
        /// 效果名称翻译键（P3-A 文档字段名；2026-08-19 验收 P3-3 修复：由基类 description
        /// 迁移至此，避免把翻译键塞进 Def.description 被引擎 Def 浏览器原样显示）。
        /// </summary>
        public string labelKey;
    }
}
