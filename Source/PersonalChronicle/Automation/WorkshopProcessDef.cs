using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 「标准细化 · 工序/岗位标准表」Def（`CHG-20261003-WORKSTATION`）。
    ///
    /// <para><b>要解决的问题</b>：车间类型原本是 N 个彼此孤立的 Def，每个各自声明 4 组
    /// 「岗位 → 原版 WorkTypeDef」映射。要表达"真实车间分工"（一道工序需要什么技能、配哪些
    /// 原版工作、用什么设备），就得在 C# 里写 N 个分支 —— 违反 GOV-006 / GOV-009。</para>
    ///
    /// <para><b>本 Def 的五个维度</b>（需求指定）：</para>
    /// <list type="number">
    /// <item><b>工序</b>：<see cref="processStage"/>（与 <see cref="WorkshopTypeDef.processStage"/> 同键）</item>
    /// <item><b>岗位</b>：<see cref="taskTypeKey"/>（对应 <c>WorkshopTaskType</c>，用字符串键以便 Def 可读）</item>
    /// <item><b>所需技能</b>：<see cref="skillDefNames"/>（原版 SkillDef defName）</item>
    /// <item><b>原版 WorkTypeDef</b>：<see cref="workTypeDefNames"/>（核心：决定勾选与档位）</item>
    /// <item><b>期望设备</b>：<see cref="expectedDeviceDefNames"/>（ThingDef defName，供 L1 设备反查）</item>
    /// </list>
    ///
    /// <para><b>纪律（GOV-006 / GOV-009）</b>：本表<b>只</b>在 XML（<c>Defs/WorkshopProcesses.xml</c>）
    /// 声明；C# 侧零 defName 字面量、零 <c>switch</c> 分支。解析一律 <c>GetNamedSilentFail</c> +
    /// 每 defName 只告警一次（沿 <c>MapComponent_WorkshopManager.ResolveWorkType</c> 先例）。</para>
    ///
    /// <para><b>与既有映射的关系</b>：本表是<b>细化层</b>，不替代 <see cref="WorkshopTypeDef"/> 的
    /// 4 组 <c>*WorkTypeDefNames</c>（EXC-012 / EXC-013 的授权面保持不变）。车间类型可用
    /// <see cref="WorkshopTypeDef.processKeys"/> 显式声明"我由哪些工序行组成"，把工序行的
    /// WorkTypeDef 并入保留集；<b>留空 = 完全沿用既有按岗位映射</b>（安全默认）。</para>
    /// </summary>
    public sealed class WorkshopProcessDef : Def
    {
        /// <summary>工序键（与 <see cref="WorkshopTypeDef.processStage"/> 同键空间）。</summary>
        public string processStage;

        /// <summary>
        /// 岗位键：<c>Production</c> / <c>Maintenance</c> / <c>QualityCheck</c> / <c>Standby</c>
        /// （对应 <c>WorkshopTaskType</c> 的<b>枚举名</b>，非序号 —— Def 里不写数字，GOV-009）。
        /// </summary>
        public string taskTypeKey;

        /// <summary>该工序岗位所需技能（原版 SkillDef defName 列表）。</summary>
        public List<string> skillDefNames = new List<string>();

        /// <summary>该工序岗位对应的原版 WorkTypeDef defName 列表（勾选 + 档位的唯一依据）。</summary>
        public List<string> workTypeDefNames = new List<string>();

        /// <summary>该工序的期望设备（ThingDef defName 列表），供 L1「设备 → 工序」反查。</summary>
        public List<string> expectedDeviceDefNames = new List<string>();

        /// <summary>
        /// 本行归属的车间类型 defName（可选，多个）。
        ///
        /// <para><b>留空 = 不被任何车间类型自动引用</b>，仅供 L1 设备反查使用
        /// （设备 → 工序 → 工作类型并集，用户裁决"设备进出车间也要触发"）。</para>
        ///
        /// <para>填了则 <see cref="WorkshopTypeDef.processKeys"/> 可按 defName 精确引用；
        /// 两者都可用：以 <b>processKeys 显式引用为准</b>，本字段只作分类标记。</para>
        /// </summary>
        public List<string> workshopTypeDefNames = new List<string>();

        private static readonly List<string> EmptyNames = new List<string>();

        /// <summary>安全取列表（永不为 null，防旧档 / 漏写 NPE）。</summary>
        public List<string> WorkTypeNames()
        {
            return workTypeDefNames ?? EmptyNames;
        }

        /// <summary>安全取期望设备列表（永不为 null）。</summary>
        public List<string> DeviceNames()
        {
            return expectedDeviceDefNames ?? EmptyNames;
        }

        /// <summary>安全取技能列表（永不为 null）。</summary>
        public List<string> SkillNames()
        {
            return skillDefNames ?? EmptyNames;
        }
    }
}