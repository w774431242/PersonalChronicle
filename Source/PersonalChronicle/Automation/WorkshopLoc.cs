using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间 UI 的「枚举 → 本地化键」映射（单一实现）。
    ///
    /// <para>2026-10-02 减法审计：此前全工程 7 处直接写 T("Task" + shift.TaskType) ——
    /// ① 每次调用都做一次字符串拼接（绘制路径每帧多次）；② 键名散落七处，改名时无从全局核对，
    /// 且「枚举成员 ↔ 翻译键」的对应关系没有任何一处能一眼看全。</para>
    ///
    /// <para>现改为 switch 返回常量键：消除运行时拼接，同时把对应关系集中到一处，
    /// 便于审计（新增枚举成员时这里会显式暴露遗漏）。</para>
    /// </summary>
    internal static class WorkshopLoc
    {
        private const string Prefix = "PersonalChronicle.Automation.Tab.";

        /// <summary>岗位枚举 → 本地化名（静态键；无运行时拼接）。</summary>
        public static string TaskLabel(WorkshopTaskType task)
        {
            switch (task)
            {
                case WorkshopTaskType.Maintenance:
                    return (Prefix + "TaskMaintenance").Translate().ToString();
                case WorkshopTaskType.QualityCheck:
                    return (Prefix + "TaskQualityCheck").Translate().ToString();
                case WorkshopTaskType.Standby:
                    return (Prefix + "TaskStandby").Translate().ToString();
                default:
                    return (Prefix + "TaskProduction").Translate().ToString();
            }
        }
    }
}
