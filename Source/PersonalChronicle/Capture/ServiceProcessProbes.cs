using System;
using HarmonyLib;
using PersonalChronicle.Domain.PawnEconomy.Care;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>U6-B1：按服务类型探测**原版交付过程的存在性**（只读、启动期一次）。判据是"原版类型/方法在不在"，
    /// 不是"我们希不希望它在"—— 探测不到就报 NotDeliverable，**绝不为某类服务造近似过程**（设计 07:45）。
    ///
    /// 取证依据（游戏 1.6.4871 rev590；方法名来自 `_research/vanilla/Assembly-CSharp` 反编译与本期裁定）：
    /// - Treatment：`TendUtility.DoTend` —— 已由 `Patch_TendUtility` 摄取（EXC-2026-029 登记）。
    /// - Childcare：`JobDriver_Breastfeed` / `JobDriver_BottleFeedBaby`（Biotech 才有）—— 过程在，摄取**未接**。
    /// - Education：`JobDriver_Lessontaking` / `JobDriver_Lessongiving`（Biotech 才有）—— 过程在，摄取**未接**。
    /// - Examination / Bed：**原版无独立交付过程**（Bed 只有持续使用、Examination 只有我们自己的旧流程）⇒ 恒不可证。</summary>
    internal static class ServiceProcessProbes
    {
        /// <summary>启动期一次性求值（AccessTools 类型查找），此后只读：避免 tick/UI 路径反复反射。</summary>
        private static readonly ServiceEvidenceStatus TreatmentStatus =
            Patch_TendUtility.TargetMethodExists ? ServiceEvidenceStatus.CaptureWired : ServiceEvidenceStatus.NotDeliverable;
        private static readonly ServiceEvidenceStatus ChildcareStatus =
            ProcessExists("RimWorld.JobDriver_Breastfeed") || ProcessExists("RimWorld.JobDriver_BottleFeedBaby")
                ? ServiceEvidenceStatus.ProcessExistsCaptureNotWired : ServiceEvidenceStatus.NotDeliverable;
        private static readonly ServiceEvidenceStatus EducationStatus =
            ProcessExists("RimWorld.JobDriver_Lessontaking") || ProcessExists("RimWorld.JobDriver_Lessongiving")
                ? ServiceEvidenceStatus.ProcessExistsCaptureNotWired : ServiceEvidenceStatus.NotDeliverable;

        public static ServiceEvidenceStatus StatusOf(ServiceKind kind)
        {
            switch (kind)
            {
                case ServiceKind.Treatment: return TreatmentStatus;
                case ServiceKind.Childcare: return ChildcareStatus;
                case ServiceKind.Education: return EducationStatus;
                // 体检与床位没有独立原版交付过程：恒 NotDeliverable（不得用"在旁边站一会儿"之类的近似证据）。
                case ServiceKind.Examination:
                case ServiceKind.Bed: return ServiceEvidenceStatus.NotDeliverable;
                default: return ServiceEvidenceStatus.NotDeliverable;
            }
        }

        private static bool ProcessExists(string typeName)
        {
            try { return AccessTools.TypeByName(typeName) != null; }
            catch (Exception) { return false; }   // 探测失败按"不存在"处理：宁少报不错报
        }
    }
}
