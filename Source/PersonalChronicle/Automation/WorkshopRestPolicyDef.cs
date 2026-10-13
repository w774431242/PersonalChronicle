using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间作息策略档位（<b>Def 驱动 · GOV-006：行为参数与可选集合一律走 XML，C# 零硬编码</b>）。
    ///
    /// <para><b>玩家可选哪几档、默认哪一档、最小时长多少</b>，全部由
    /// <c>Defs/WorkshopRestPolicies.xml</c> 决定；C# 侧只实现
    /// <see cref="WorkshopShiftPolicy"/> 的<b>语义分支</b>。
    /// 第三方 mod 因此可以 XML-patch 增删档位或改默认值，而不需要引用本工程实现类。</para>
    ///
    /// <para><b>为什么用 Def 而不是 C# enum 直接存存档</b>：存档只存
    /// <b>稳定 ID 字符串</b>（GOV-009）；Def 被移除/改名时按
    /// <see cref="WorkshopShiftPolicy.Normalize"/> 回落到默认档，
    /// <b>绝不抛异常、绝不丢档</b>（读档路径的硬要求）。</para>
    /// </summary>
    public sealed class WorkshopRestPolicyDef : Def
    {
        /// <summary>策略语义（<see cref="WorkshopRestPolicyKind"/>；XML 里写稳定名）。</summary>
        public WorkshopRestPolicyKind policy = WorkshopRestPolicyKind.FixedPerShift;

        /// <summary>
        /// 最小连续值勤时长（小时，GOV-006 登记项）。
        /// <para>含义：到点后若本次值勤<b>不足</b>该时长，则<b>不打断</b>（修成因 B）。
        /// 缺省见 <see cref="WorkshopShiftPolicy.DefaultMinContinuousHours"/>。</para>
        /// </summary>
        public int minContinuousHours = WorkshopShiftPolicy.DefaultMinContinuousHours;

        /// <summary>缺省档位的 Def 名（GOV-009 稳定 ID；配置缺失时用它）。</summary>
        public const string DefaultPolicyDefName = "PC_WorkshopRestPolicy_Fixed";

        /// <summary>UI 显示名（Keyed，GOV-010）。</summary>
        public string labelKey;

        /// <summary>UI 说明（Keyed，GOV-010）；该档的语义与副作用。</summary>
        public string descKey;

        /// <summary>
        /// <b>警示文本</b>（Keyed）：仅 <see cref="WorkshopRestPolicyKind.RotateDaily"/> 需要
        /// —— UI 必须显式提示"会按天翻转睡眠时段"（成因 A 的根因，不得静默）。
        /// </summary>
        public string warningKey;

        /// <summary>该档是否参与 UI 下拉（关闭可隐藏但保留读档兼容）。</summary>
        public bool selectable = true;

        /// <summary>规范化最小时长（转发 Domain，避免三处各写一遍钳制规则）。</summary>
        public int MinContinuousHours
        {
            get { return WorkshopShiftPolicy.NormalizeMinContinuousHours(minContinuousHours); }
        }
    }
}