using System;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// P1 CAREER-005 研究事实采集点：Postfix on ResearchManager.FinishProject ——
    /// 引擎所有研究完成路径（ResearchPerformed 攒满进度 / dev 完成/anomaly 等）
    /// 的统一出口。1.6 签名（反射核验 Assembly-CSharp）：
    ///   Void FinishProject(Verse.ResearchProjectDef, Boolean, Verse.Pawn, Boolean)
    /// 第三参为完成该项研究的研究者 Pawn（普通研究路径必有；dev 强制完成等为 null，
    /// 此时跳过——事实必须归属真实人物）。
    ///
    /// 注意：1.6 中 ResearchProjectDef 位于 Verse 命名空间（非 RimWorld）。
    /// 参数用 __0..__3 索引注入，规避引擎参数名漂移导致 Harmony 解析失败。
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.ResearchManager.FinishProject(ResearchProjectDef, bool, Pawn, bool)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : 记录 ResearchCompleted 职业事实（BE-005 第二个真实原版事件源）
    ///   Alternative       : ResearchManager.ResearchPerformed(float, Pawn)（rejected:
    ///                       每 tick 触发需自判完成跃迁，且 currentProj 为私有字段）
    ///   Compatibility Risk: 低 — FinishProject 是引擎稳定公开出口
    ///   Execution Order   : 无顺序契约；read-only Postfix 任何顺序均安全
    ///   Failure Behavior  : catch → Log.Warning，原方法不受影响
    /// </summary>
    // 治理登记（2026-09-15 审计补登）：本 patch 属 EXC-2026-001（捕获层 Harmony 例外 · E2）**清单 #18**。
    // 前提 = COMP-004「原生机制无法完整捕获」：研究完成是档案「科技进展」维度的权威信号，
    // RimWorld 未暴露可订阅的研究完成事件（仅 `ResearchManager` 内部流程）⇒ 必须 Postfix 钩取。
    // 替代方案被否：轮询 `ResearchManager` 已完成集合差分 —— 无法定位完成时刻与执行研究者，且为全扫（PERF-001）。
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject),
        new Type[] { typeof(ResearchProjectDef), typeof(bool), typeof(Pawn), typeof(bool) })]
    public static class Patch_ResearchFinishProject
    {
        public static void Postfix(Pawn __2, ResearchProjectDef __0)
        {
            try
            {
                if (__0 == null || string.IsNullOrEmpty(__0.defName))
                {
                    return;
                }
                // 事实必须归属真实人物：dev 强制完成等无研究者路径不计。
                if (__2 == null || __2.Faction == null || !__2.Faction.IsPlayer)
                {
                    return;
                }
                if (!__2.RaceProps.Humanlike)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                service.RecordCareerResearch(__2, __0.defName, Find.TickManager.TicksGame);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture,
                    "FinishProject patch failed: " + ex.Message);
            }
        }
    }
}
