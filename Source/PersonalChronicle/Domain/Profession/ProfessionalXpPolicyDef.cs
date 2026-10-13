using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业技能 XP 策略 Def（V2.0 §7 品质系数表 Def 化 + V2.0+ 通用曲线/活动权重）。
    /// 数据驱动铁律：品质系数不再硬编码在 C# switch，改由 XML 定义。
    /// <see cref="ProfessionalXpEvaluator.QualityMultiplier(string, IReadOnlyList{QualityXpEntry})"/>
    /// 优先消费本表；无匹配或未定义时回退内建表（向后兼容旧 Def 缺失场景）。
    /// </summary>
    public sealed class ProfessionalXpPolicyDef : Def
    {
        /// <summary>品质系数表（qualityName → multiplier，如 Legendary→5）。</summary>
        public List<QualityXpEntry> qualityMultipliers = new List<QualityXpEntry>();

        /// <summary>通用等级曲线幂指数（V2.0+ 推荐 1.6：升级成本递增，越顶级越难）。1=线性；&lt;1 前期快。</summary>
        public float curvePower = 1.6f;

        /// <summary>活动类型→XP 权重表（Work/Research/Construct/Author/Harvest）。空表回退内建默认。</summary>
        public List<ActivityXpEntry> activityWeights = new List<ActivityXpEntry>();

        /// <summary>Def 未配置 curvePower 时使用的推荐值（与 Def 字段默认值保持一致）。</summary>
        public const float FallbackCurvePower = 1.6f;

        /// <summary>
        /// 全局默认等级曲线幂指数——**不绑定任何具体专业**。
        ///
        /// 铁律 6.5（规则数据驱动）+ 6.4（禁止重复实现）：
        /// 此前 Application / UI ReadModel / 存档迁移三处各自硬编码
        /// `GetNamedSilentFail("ProfessionalXpPolicy_Manufacturing")`，语义上取的是"全局口径"
        /// 却写死了"制造"这一个 Def 名——一旦该 Def 改名/删除，或将来按专业细分 policy，
        /// 三处会各自静默回退且互不一致。现统一收口到本属性：
        /// 取 DefDatabase 中第一个 curvePower 有效的 policy 作为全局口径，无 Def 时回退推荐值。
        /// 所有消费点必须调用本属性，禁止再写死 Def 名。
        /// </summary>
        public static float DefaultCurvePower
        {
            get
            {
                List<ProfessionalXpPolicyDef> all = DefDatabase<ProfessionalXpPolicyDef>.AllDefsListForReading;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        ProfessionalXpPolicyDef p = all[i];
                        if (p != null && p.curvePower > 0f)
                        {
                            return p.curvePower;
                        }
                    }
                }
                return FallbackCurvePower;
            }
        }
    }

    /// <summary>品质系数条目（纯数据，无 Verse 依赖，可离线单测）。</summary>
    public sealed class QualityXpEntry
    {
        /// <summary>QualityCategory 的 name（如 "Legendary"），语言无关稳定键。</summary>
        public string qualityName;

        /// <summary>品质系数（V2.0 §7：优秀×1.5 / 大师×3 / 传奇×5）。</summary>
        public float multiplier;
    }

    /// <summary>活动类型→XP 权重条目（纯数据，无 Verse 依赖）。</summary>
    public sealed class ActivityXpEntry
    {
        /// <summary>活动类型稳定键（Work/Research/Construct/Author/Harvest）。</summary>
        public string activityType;

        /// <summary>该活动类型对职业技能 XP 的权重（默认 Work=1.0 / Research=0.9 / Construct=0.8 / Author=0.85 / Harvest=0.6）。</summary>
        public float weight;
    }
}
