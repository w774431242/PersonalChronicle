using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 终局管理系统「系统中心」的注册凭据（Level 1 Def）。
    ///
    /// 对应《系统中心标准化规范书》§2.2 的 <c>CenterManifest</c>：中心清单是
    /// 接入调度层的唯一凭据。本 Def **只承载静态注册信息**（归属、档位、能力与
    /// 事件契约），不承载任何运行时状态 —— 运行态属中心自身，表现属 UI 层
    /// （GOV-003 数据/运行时/表现分离）。
    ///
    /// <para>中心清单的数据来源是 Def 而非 C# 常量：新增/调整中心 = 改 XML，
    /// 零代码（GOV-006 零硬编码 / 规范书 P8）。<see cref="defName"/> 是逻辑主键
    /// （<c>snake_case</c>，如 <c>manufacturing_center</c>），<see cref="Def.label"/>
    /// 仅用于显示（GOV-009）。</para>
    /// </summary>
    public sealed class SystemCenterDef : Def
    {
        /// <summary>英文名（与 label 并列显示，纯展示，不参与逻辑）。</summary>
        public string nameEn = string.Empty;

        /// <summary>
        /// 调度优先级档位：同 tick 争用共享资源时数值小者先执行
        /// （规范书 §3.4 规则 1）。
        /// </summary>
        public int priorityTier = 50;

        /// <summary>中心自身版本（独立于规范版本，规范书 §7 center version）。</summary>
        public string centerVersion = "1.0.0";

        /// <summary>
        /// true = 预留中心：ID 与档位已登记，玩法规则待对齐后启用。
        /// UI 对预留中心只显示清单，不显示 KPI（避免用样例数据冒充真实数据）。
        /// </summary>
        public bool reserved;

        /// <summary>负责的职责条目（纯描述文本，来自规范书 §4.x.2）。</summary>
        public List<string> responsible = new List<string>();

        /// <summary>不负责 / 委托给其他中心的职责条目。</summary>
        public List<string> delegated = new List<string>();

        /// <summary>依赖的中心 defName 列表（启动拓扑序依据，稳定 ID）。</summary>
        public List<string> dependsOn = new List<string>();

        /// <summary>对外提供的能力标识（<c>cap.&lt;center&gt;.&lt;name&gt;</c>）。</summary>
        public List<string> provides = new List<string>();

        /// <summary>发出的事件类型（<c>evt.&lt;center&gt;.&lt;name&gt;</c>）。</summary>
        public List<string> emits = new List<string>();

        /// <summary>订阅的事件类型。</summary>
        public List<string> consumes = new List<string>();

        /// <summary>接入的调度钩子（<c>OnTick</c> / <c>OnPhase(x)</c> / <c>OnRound</c>）。</summary>
        public List<string> hooks = new List<string>();
    }
}
