using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 岗位键（<see cref="WorkshopProcessDef.taskTypeKey"/> → <c>WorkshopTaskType</c>）。
    ///
    /// <para><b>为什么用字符串而不是把枚举直接写进 XML</b>：Def 里的枚举字段在 XML 中表现为数字
    /// 序号，<b>插入新值会让既有 Def 的含义漂移</b>（破坏性 schema 变更）。字符串键自描述、
    /// 可读且顺序无关（GOV-009：逻辑依赖稳定 ID，不依赖序号）。解析失败一律<b>回退
    /// Production</b> 并只告警一次，绝不抛异常打断 Def 加载。</para>
    /// </summary>
    public static class WorkshopTaskTypeKeys
    {
        public const string Production = "Production";
        public const string Maintenance = "Maintenance";
        public const string QualityCheck = "QualityCheck";
        public const string Standby = "Standby";

        /// <summary>该键是否为受支持的岗位键（Def 配置自检 / 单测用）。</summary>
        public static bool IsKnown(string key)
        {
            return key == Production || key == Maintenance
                || key == QualityCheck || key == Standby;
        }

        /// <summary>
        /// 工序行声明的岗位键 → 运行时枚举。<b>未识别的键回退 Production</b>（安全默认：
        /// 生产是收益最高、最该被对齐的岗位）。
        /// </summary>
        public static WorkshopTaskType Parse(string key)
        {
            if (key == Maintenance)
            {
                return WorkshopTaskType.Maintenance;
            }
            if (key == QualityCheck)
            {
                return WorkshopTaskType.QualityCheck;
            }
            if (key == Standby)
            {
                return WorkshopTaskType.Standby;
            }
            return WorkshopTaskType.Production;
        }
    }
    /// <summary>
    /// 车间类型 Def（蓝图 §车间类型 / 落地管线 Step 1）。
    ///
    /// 一个车间类型 = 一道工序（processStage）+ 主技能 + 加成方向 + 一组加成字段。
    /// 实例写在 Defs/WorkshopTypes.xml，数值取自蓝图「加大版」档位。
    ///
    /// 数值语义（重要）：<b>本 Def 里填的是「满级 + 主技能全覆盖 + 方向对齐」时的上限值</b>，
    /// 运行时由 <see cref="WorkshopBonusResolver"/> 按 (等级/20) × 覆盖度 × 协同系数 向 1.0 收缩。
    /// 因此这里存的是 cap，不再单独存 bonusCap。
    ///
    /// 因子 &lt; 1 表示降幅（工时/耗材），&gt; 1 表示增幅（产出量/精度/耐久/良品率/吞吐/速度），
    /// 档位偏移（品质/评级）用整数。缺省值 = 无影响（1f / 0）。
    /// </summary>
    public sealed class WorkshopTypeDef : Def
    {
        /// <summary>工序键（数据驱动的字符串，便于 XML 扩展，不绑死 C# 枚举）。</summary>
        public string processStage;

        /// <summary>主技能 defName 列表（原版 SkillDef，如 Crafting / Intellectual）。</summary>
        public List<string> skillDefNames = new List<string>();

        /// <summary>加成方向：ProfessionalDirectionDef 的 defName。命中才给协同系数。</summary>
        public string direction;

        /// <summary>
        /// 本工序的「原生锚点设备」（ThingDef defName 列表，非类名 —— 逻辑键一律 defName）。
        ///
        /// <para>用于两处：① 管理面板概览页的<b>设备匹配度</b>（匹配 / 多余 / 缺失）；
        /// ② L1 设备联动的「设备 → 工序」反查锚点（<see cref="WorkshopProcessDef"/> 同名字段）。
        /// <b>不参与任何加成计算</b>。留空 = 该工序尚无原生设备 ⇒ 概览页显示"未声明期望设备"，
        /// 而不是把它算成"缺失"。</para>
        /// </summary>
        public List<string> expectedDeviceDefNames = new List<string>();
        /// <summary>允许加入但不要求必备的辅助设备；通过设备等价表支持升级台。</summary>
        public List<string> acceptedDeviceDefNames = new List<string>();
        // ---- 专向风格数值加成 buff（2026-09-25 重设：按类型给固定加成）--------
        // 与上方旧加成因子字段解耦：这里只声明「该类型给在岗人员什么方向的 buff」，
        // 数值即玩家在面板看到的数值，也是 StatPart 实际生效的数值（无技能缩放、显示=生效）。

        /// <summary>工作速度加成（百分点，10 = +10%）。经 Resolve 落地为 workTimeFactor = 1/(1+pct/100)。</summary>
        public float workSpeedBonusPct = 0f;

        /// <summary>品质加成（QualityCategory 上移档数，1 = +1 品质）。在制造后由 WorkshopProductionEffects.Apply 落档。</summary>
        public int qualityBonus = 0;

        /// <summary>研究速度加成（百分点，研发车间专用，20 = +20%）。经 researchSpeedFactor = 1+pct/100 落地。</summary>
        public float researchSpeedBonusPct = 0f;

        // ---- 旧加成因子字段（保留以兼容存档与 ResolveScaled，新类型不再声明）----

        /// <summary>工时因子（&lt;1 = 更快）。工业化主线优势。</summary>
        public float workTimeFactor = 1f;

        /// <summary>耗材因子（&lt;1 = 更省料）。</summary>
        public float stuffCostFactor = 1f;

        /// <summary>产出量因子（&gt;1 = 同批料出更多）。</summary>
        public float yieldFactor = 1f;

        /// <summary>品质档位偏移（原生 QualityCategory 上移档数）。</summary>
        public int qualityOffset = 0;

        /// <summary>精度因子（最终武器 Accuracy* 统计值）。</summary>
        public float accuracyFactor = 1f;

        /// <summary>耐久因子（MaxHitPoints）。调质渗碳/磷化电镀的真实红利，可放大。</summary>
        public float maxHpFactor = 1f;

        /// <summary>良品率因子（&gt;1 = 报废更少，按 SPC 全检语义）。</summary>
        public float goodRateFactor = 1f;

        /// <summary>P4 评级档位偏移。</summary>
        public int ratingOffset = 0;

        /// <summary>吞吐因子（物流车间：仅出入库/转运效率）。</summary>
        public float throughputFactor = 1f;

        /// <summary>维修速度因子（维修车间）。</summary>
        public float repairSpeedFactor = 1f;

        /// <summary>研究速度因子（研发车间）。</summary>
        public float researchSpeedFactor = 1f;
        // ---- 岗位 → 原版工作类型映射（2026-10-02）--------------------------------
        // 用户需求：班次设置里选定「岗位 · 生产／维护／质检／待命」后，原版「工作」面板中
        // 对应的原版工作类型应自动勾选（否则被排班的成员会站着闲置，不干任何活）。
        //
        // 粒度刻意是「车间类型 × 岗位」而不是「岗位」：同样是「生产」，烹饪坊该勾 Cooking、
        // 锻造坊该勾 Smithing ⇒ 只有车间类型才知道该勾哪几个原版类型。
        //
        // 纪律（GOV-006 / BASE-010）：映射**只**在 XML（Defs/WorkshopTypes.xml）声明，
        // C# 侧零 defName 字面量、零 switch-like 业务分支；留空 = 该岗位不联动原版工作面板
        // （安全默认）。解析方一律 GetNamedSilentFail，未知/缺失 defName 静默跳过。

        /// <summary>「生产」岗位应自动勾选的原版 WorkTypeDef defName 列表。</summary>
        public List<string> productionWorkTypeDefNames = new List<string>();

        /// <summary>「维护」岗位应自动勾选的原版 WorkTypeDef defName 列表。</summary>
        public List<string> maintenanceWorkTypeDefNames = new List<string>();

        /// <summary>「质检」岗位应自动勾选的原版 WorkTypeDef defName 列表（原版无对应类型 ⇒ 常态留空）。</summary>
        public List<string> qualityCheckWorkTypeDefNames = new List<string>();

        /// <summary>「待命」岗位应自动勾选的原版 WorkTypeDef defName 列表（常态留空）。</summary>
        public List<string> standbyWorkTypeDefNames = new List<string>();

        /// <summary>
        /// 「设备」通道应自动勾选的原版 WorkTypeDef defName 列表
        /// （`CHG-20261003-WORKSTATION` 新增，<b>可选</b>，默认空）。
        ///
        /// <para><b>语义</b>：车间里的<b>设备</b>（有 <c>Bill_Production</c> 的工作台）本身就要人操作，
        /// 它们对应的工作类型应当被对齐 —— 即使用户还没给这些设备开工单。
        /// 用户裁决：「设备进出车间<b>也要</b>触发，不要求先开工单」。</para>
        ///
        /// <para><b>留空 = 降级到现有按岗位映射</b>（安全默认，向后兼容）。
        /// 非空时，其内容并入保留集（与岗位映射、工序表取并集）。</para>
        ///
        /// <para>纪律同上方：只在 XML 声明，C# 零 defName 字面量。</para>
        /// </summary>
        public List<string> deviceWorkTypeDefNames = new List<string>();

        /// <summary>
        /// 本车间类型由哪些<a cref="WorkshopProcessDef">工序/岗位标准表</a>行组成
        /// （<c>WorkshopProcessDef</c> 的 defName 列表，<b>可选</b>，默认空）。
        ///
        /// <para><b>标准化细的核心接线</b>：车间类型不再是孤立硬编码，而是引用若干工序行；
        /// 每行给出「工序 → 岗位 → 技能 → 原版 WorkTypeDef → 期望设备」五维数据
        /// （见 <c>Defs/WorkshopProcesses.xml</c>）。引用行的 <b>workTypeDefNames</b>
        /// 并入本车间类型的保留集。</para>
        ///
        /// <para><b>留空 = 完全沿用既有按岗位映射</b>（安全默认，EXC-012 / EXC-013 授权面不变）。</para>
        /// </summary>
        public List<string> processKeys = new List<string>();
        private static readonly List<string> EmptyWorkTypeNames = new List<string>();

        /// <summary>该岗位在本车间类型下应联动勾选的原版工作类型 defName 列表（永不为 null）。</summary>
        public List<string> WorkTypeDefNamesOf(WorkshopTaskType task)
        {
            switch (task)
            {
                case WorkshopTaskType.Maintenance:
                    return maintenanceWorkTypeDefNames ?? EmptyWorkTypeNames;
                case WorkshopTaskType.QualityCheck:
                    return qualityCheckWorkTypeDefNames ?? EmptyWorkTypeNames;
                case WorkshopTaskType.Standby:
                    return standbyWorkTypeDefNames ?? EmptyWorkTypeNames;
                default:
                    return productionWorkTypeDefNames ?? EmptyWorkTypeNames;
            }
        }

        /// <summary>「设备」通道的工作类型 defName 列表（永不为 null；空 = 回落岗位映射）。</summary>
        public List<string> DeviceWorkTypeDefNames()
        {
            return deviceWorkTypeDefNames ?? EmptyWorkTypeNames;
        }

        /// <summary>本车间引用的工序行 defName 列表（永不为 null）。</summary>
        public List<string> ProcessKeys()
        {
            return processKeys ?? EmptyWorkTypeNames;
        }

        /// <summary>该车间是否完全无生产加成（物流车间：只提吞吐）。</summary>
        public bool HasProductionBonus
        {
            get
            {
                return workTimeFactor != 1f || stuffCostFactor != 1f || yieldFactor != 1f
                    || qualityOffset != 0 || accuracyFactor != 1f || maxHpFactor != 1f
                    || goodRateFactor != 1f || ratingOffset != 0;
            }
        }
    }
}
