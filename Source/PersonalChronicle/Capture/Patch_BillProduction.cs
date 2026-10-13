using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v1.1.4 劳模「工作场所」捕获点（方案 A）：RimWorld.Bill_Production.Notify_IterationCompleted。
    ///
    /// 1.6 target verification（反射核验）：
    ///   RimWorld.Bill_Production.Notify_IterationCompleted(Verse.Pawn billDoer,
    ///       System.Collections.Generic.List`1[Verse.Thing] ingredients) [public,
    ///       instance, non-virtual]
    ///   Bill_Production.billStack (public field) → BillStack.billGiver
    ///   (public field, type RimWorld.IBillGiver) → Building_WorkTable
    ///   （implements IBillGiver，含 Building_WorkTable.billStack 字段）→ defName。
    ///
    /// 制造完成迭代（配方一次完成）调用一次，低频；从这里能同时拿到：
    ///   * 工坊建筑 defName（Building_WorkTable.def.defName，稳定键）
    ///   * 工坊所在房间角色（billGiver.Position 所在 Room.Role）
    /// 与 <see cref="ChronicleGameComponent.SampleResidence"/>（方案 B 住所定期快照）
    /// 组合即「劳模住所 + 工作场所」双检测。
    ///
    /// V2.1 追加：同一回调承载「制造完成奖励」——<see cref="IArchiveService.RecordCraftCompleted"/>
    /// （recipe.workSkill → 职业技能工作量结算 XP）。该回调是所有工作台/简易制作点
    /// 制造完成的统一出口（覆盖 mod 自定义制造与 CraftingSpot），在此挂载可同时补齐
    /// 制造职业「完成即涨级」的体验。
    ///
    /// 只对玩家派系、chronicle 相关、humanlike 的殖民者记录；写入
    /// <see cref="IArchiveService.OnWorkplaceUsed"/>（数据层聚合 WorkplaceSnapshot，
    /// 不写事件流——工坊使用低频且非事件语义）。
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.Bill_Production.Notify_IterationCompleted(Pawn, List`1[Thing])
    ///   Patch Type        : Postfix (records archive and economic completion; never skips the original)
    ///   Reason            : 记录劳模的工作场所（Building_WorkTable defName + 房间角色）
    ///   Alternative       : Building_WorkTable.IsWorking 轮询 (rejected: 零轮询铁律)；
    ///                       JobDriver_DoBill Toils (rejected: 分散在多子类、patch 面广)
    ///   Compatibility Risk: 低 — Notify_IterationCompleted 是引擎稳定回调，public 非 virtual
    ///   Execution Order   : 无顺序契约；read-only Postfix 任何顺序均安全
    ///   Failure Behavior  : catch → Log.Warning，原方法不受影响
    /// </summary>
    [HarmonyPatch(typeof(Bill_Production), "Notify_IterationCompleted")]
    public static class Patch_BillProduction
    {
        /// <summary>
        /// Cached probe of the patched target signature. [HarmonyPatch] resolves
        /// lazily and silently skips a drifted target, so we verify it once at
        /// startup instead of letting the workplace capture degrade silently.
        /// </summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(Bill_Production),
                "Notify_IterationCompleted",
                new Type[] { typeof(Pawn), typeof(System.Collections.Generic.List<Thing>) }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture, "Bill_Production.Notify_IterationCompleted target signature changed; workplace capture silently disabled - update Patch_BillProduction");
            }
            return found;
        }

        public static void Postfix(Bill_Production __instance, Pawn billDoer)
        {
            PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.ProductionCompleted(__instance, billDoer);
            try
            {
                if (__instance == null || billDoer == null)
                {
                    return;
                }
                if (billDoer.Faction == null || !billDoer.Faction.IsPlayer)
                {
                    return;
                }
                if (!billDoer.RaceProps.Humanlike)
                {
                    return;
                }
                if (!ChronicleColonistScanner.TryClassifyCurrent(billDoer, out _))
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }

                // V2.3 制造整件结算（方案 A，独立于工作台类型：crafting spot / worktable 均生效）：
                // 制造期间被 Prefix 拦截缓存的原始经验 Flush 后，一次性经原生
                // pawn.skills.Learn(workSkill, total) 注入——原版 Passion/学习速率公式自动
                // 生效，随后 Patch_SkillRecord_Learn.Postfix 将其分流职业 XP（产出导向、无
                // 双重计数、制造中途取消不获）。置于 workbench 解析之前，确保 CraftingSpot
                // （非 Building_WorkTable）完成制造也不丢失。时序实证：Toils_Recipe 完成
                // toil 的 GetSkill.Learn 在 Notify_IterationCompleted 之前执行 → 缓存完整。
                if (__instance.recipe != null)
                {
                    float total = CraftXpSettlement.Flush(billDoer);
                    if (total > 0f && __instance.recipe.workSkill != null && billDoer.skills != null)
                    {
                        CraftXpSettlement.IsSettling = true;
                        try
                        {
                            billDoer.skills.Learn(__instance.recipe.workSkill, total, false, false);
                        }
                        finally
                        {
                            CraftXpSettlement.IsSettling = false;
                        }
                    }
                    // V2.1 制造完成实践统计（recipe.workSkill → 职业技能，append-only 只增不减；
                    // XP 部分已由 v2.3 整件注入取代，此处仅记实践/统计）。置于 workbench 解析
                    // 之前，确保 CraftingSpot（非 Building_WorkTable）完成制造也不丢失记录。
                    service.RecordCraftCompleted(billDoer, __instance.recipe, Find.TickManager.TicksGame);
                    // B4 经济劳动证据：一次真实完成迭代 = 一份证据（仅当该 Pawn 持有有效经济合同时生效；
                    // 无合同 = 零成本返回）。合同笔数上限与证据去重键共同保证不会重复计薪。
                    try
                    {
                        var economy = PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current;
                        if (economy != null) economy.ReportCraftIteration(billDoer, __instance, Find.TickManager.TicksGame);
                    }
                    catch (Exception error)
                    {
                        Log.Warning("[PC][Economy] craft labour evidence failed: " + error.Message);
                    }
                    // M10 再生产闭环：一次真实完成迭代也要向**授权账**核销交付（按工单稳定键回匹配，
                    // 无匹配即零成本返回）。与上面的工资证据互不依赖：那里结算劳动报酬，这里核销补货授权。
                    try
                    {
                        var economy = PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current;
                        if (economy != null) economy.ReportProductionDelivery(__instance, Find.TickManager.TicksGame);
                    }
                    catch (Exception error)
                    {
                        Log.Warning("[PC][Economy] production delivery evidence failed: " + error.Message);
                    }
                }

                // 劳模工作场所捕获（方案 A，仅 Building_WorkTable）。
                // __instance.billStack → billGiver (IBillGiver) → Building_WorkTable。
                // 反射核验：Building_WorkTable implements IBillGiver，as 转换有效。
                BillStack billStack = __instance.billStack;
                Building_WorkTable workbench = (billStack != null && billStack.billGiver != null)
                    ? billStack.billGiver as Building_WorkTable
                    : null;
                if (workbench == null || workbench.def == null)
                {
                    return;
                }
                service.OnWorkplaceUsed(billDoer, workbench);

                // P9 论文分阶段配方联动：识别论文流程配方，按 recipe 名驱动阶段推进（真实消耗方向书/文献 + 全局科研加成）。
                if (__instance.recipe != null && billDoer.Map != null)
                {
                    string recipeName = __instance.recipe.defName;
                    if (recipeName == "PC_MakeExamThesis_CiteBooks"
                        || recipeName == "PC_MakeExamThesis_CiteResearch"
                        || recipeName == "PC_MakeExamThesis_Final")
                    {
                        ChronicleGameComponent comp = Current.Game != null ? Current.Game.GetComponent<ChronicleGameComponent>() : null;
                        PawnObject po = comp != null ? null : null;
                        if (comp != null)
                        {
                            var recs = comp.GetRecordsFor(billDoer);
                            if (recs != null && recs.Count > 0) po = recs[0] as PawnObject;
                        }
                        if (po != null)
                        {
                            // 找到该 Pawn 进行中（未完成的）论文资格：取最近一个未完成 Thesis
                            ThesisEvidence t = FindActiveThesis(po);
                            if (t != null)
                            {
                                QualificationDef qd = DefDatabase<QualificationDef>.GetNamedSilentFail(t.QualificationDefName) as QualificationDef;
                                if (qd != null)
                                {
                                    if (recipeName == "PC_MakeExamThesis_CiteBooks")
                                        QualificationFlowService.CiteThesisBooks(po, qd, billDoer.Map);
                                    else if (recipeName == "PC_MakeExamThesis_CiteResearch")
                                        QualificationFlowService.CiteResearch(po, qd, billDoer.Map);
                                    else if (recipeName == "PC_MakeExamThesis_Final")
                                        QualificationFlowService.CompleteThesis(po, qd);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "Bill_Production.Notify_IterationCompleted patch failed: " + ex.Message);
            }
        }

        /// <summary>取 Pawn 进行中（未完成）的论文证据；无则 null。</summary>
        private static ThesisEvidence FindActiveThesis(PawnObject po)
        {
            if (po == null || po.CareerData == null || po.CareerData.Thesis == null
                || po.CareerData.Thesis.Theses == null) return null;
            for (int i = 0; i < po.CareerData.Thesis.Theses.Count; i++)
            {
                ThesisEvidence t = po.CareerData.Thesis.Theses[i];
                if (t != null && !t.Completed) return t;
            }
            return null;
        }
    }

    /// <summary>
    /// Startup hook that forces the one-time signature probe for
    /// RimWorld.Bill_Production.Notify_IterationCompleted.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_BillProductionStartup
    {
        static Patch_BillProductionStartup()
        {
            _ = Patch_BillProduction.TargetMethodExists;
        }
    }
}
