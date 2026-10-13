namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 车间作息策略（<b>稳定 ID 枚举 · GOV-009</b>；C# 侧零硬编码文案，仅承载语义）。
    ///
    /// <para><b>为什么需要它（成因 A · 用户实机确认）</b>：原实现在
    /// <c>SyncVanillaTimetables</c> 里按「该班组<b>当日轮到</b>的班次」改写<b>原版作息表</b>。
    /// 一个班组同时被指派日班(08-17) + 夜班(20-08) 时，绑定关系<b>每天轮换</b>
    /// =&gt; pawn 的 Work 小时在<b>日与日之间翻转约 12 小时</b>
    /// =&gt; 睡眠时段剧烈跳变（人反复昼夜颠倒）。</para>
    ///
    /// <para><b>三档语义</b>：</para>
    /// <list type="bullet">
    /// <item><see cref="FixedPerShift"/>（<b>默认</b>）：岗位<b>永久绑定</b>一个班次，不按天翻转
    /// =&gt; 还原真实工厂（人固定上白班或夜班），<b>无节律破坏</b>；</item>
    /// <item><see cref="NaturalRest"/>：<b>不写</b>原版作息表，靠 Needs_Rest 自然休息
    /// + 值勤 Job 控上下工；</item>
    /// <item><see cref="RotateDaily"/>：旧行为（按当日轮换改写作息表），<b>仅作对照</b>，
    /// 默认关闭且 UI 必须警示"会翻转睡眠时段"。</item>
    /// </list>
    ///
    /// <para><b>注意</b>：此处是<b>语义枚举</b>，玩家可选的<b>具体档位集合</b>由
    /// <c>WorkshopRestPolicyDef</c>（Def，配置驱动）提供；本枚举只保证 C# 侧
    /// 行为分支可穷举、且不把"能不能写作息表"这类规则散落到各处。</para>
    /// </summary>
    public enum WorkshopRestPolicyKind : byte
    {
        /// <summary>岗位永久绑定一个班次；写入该班的固定时段（<b>默认</b>）。</summary>
        FixedPerShift = 0,

        /// <summary>不写原版作息表；靠自然休息需求 + 值勤 Job 控上下工。</summary>
        NaturalRest = 1,

        /// <summary>旧行为：按当日轮换改写作息表（会翻转睡眠时段；仅作对照）。</summary>
        RotateDaily = 2
    }

    /// <summary>
    /// 班次模块的纯判定：作息策略 + "某人在某小时是否当班" + 连续值勤时长
    /// （<b>纯函数，可离线单测；零 Verse 依赖</b>）。
    ///
    /// <para><b>模块边界（GOV-004 · 三模块拆分 · 班次侧）</b>：
    /// 本类<b>不写 timetable</b>、<b>不碰 JobDriver</b>；它只回答"应该怎样"。
    /// 写表与下发/收工 Job 全部在 Automation.WorkshopDutyCoordinator（值勤模块）。</para>
    /// </summary>
    public static class WorkshopShiftPolicy
    {
        /// <summary>缺省最小时长（小时）—— 未配置时的安全值（见 GOV-006 登记）。</summary>
        public const int DefaultMinContinuousHours = 1;

        /// <summary>
        /// 该策略下<b>是否应写入原版作息表</b>。
        /// <para><c>NaturalRest</c> 返回 false（明确承诺"不碰玩家手排"）。</para>
        /// </summary>
        public static bool WritesTimetable(WorkshopRestPolicyKind kind)
        {
            return kind != WorkshopRestPolicyKind.NaturalRest;
        }

        /// <summary>
        /// 该策略下，作息表是否<b>按天变化</b>（= 会翻转睡眠时段 ⇒ UI 必须警示）。
        /// </summary>
        public static bool FlipsSleepAcrossDays(WorkshopRestPolicyKind kind)
        {
            return kind == WorkshopRestPolicyKind.RotateDaily;
        }

        /// <summary>规范化策略值（越界/未知 =&gt; 默认档，<b>绝不抛异常</b>）。</summary>
        public static WorkshopRestPolicyKind Normalize(WorkshopRestPolicyKind kind)
        {
            switch (kind)
            {
                case WorkshopRestPolicyKind.FixedPerShift:
                case WorkshopRestPolicyKind.NaturalRest:
                case WorkshopRestPolicyKind.RotateDaily:
                    return kind;
                default:
                    return WorkshopRestPolicyKind.FixedPerShift;
            }
        }

        /// <summary>规范化<b>最小时长</b>：&lt;= 0 走缺省值；上限 24（全天班不可能连续更久）。</summary>
        public static int NormalizeMinContinuousHours(int configured)
        {
            if (configured <= 0)
            {
                return DefaultMinContinuousHours;
            }
            return configured > 24 ? 24 : configured;
        }
    }
}