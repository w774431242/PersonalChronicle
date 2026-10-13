using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Startup diagnostics for capture targets. Harmony can skip an unresolved
    /// target without making the gameplay symptom obvious; failing loudly here
    /// keeps an event source from silently becoming static after a game update.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CapturePatchDiagnostics
    {
        static CapturePatchDiagnostics()
        {
            Verify(
                "Pawn.SetFaction",
                typeof(Pawn),
                nameof(Pawn.SetFaction),
                new Type[] { typeof(Faction), typeof(Pawn) });
            Verify(
                "Pawn.Kill",
                typeof(Pawn),
                nameof(Pawn.Kill),
                new Type[] { typeof(DamageInfo?), typeof(Hediff) });
            Verify(
                "IncidentWorker.TryExecute",
                typeof(IncidentWorker),
                nameof(IncidentWorker.TryExecute),
                new Type[] { typeof(IncidentParms) });
            Verify(
                "Frame.CompleteConstruction",
                typeof(Frame),
                nameof(Frame.CompleteConstruction),
                new Type[] { typeof(Pawn) });
            Verify(
                "Pawn_RelationsTracker.AddDirectRelation",
                typeof(Pawn_RelationsTracker),
                nameof(Pawn_RelationsTracker.AddDirectRelation),
                new Type[] { typeof(PawnRelationDef), typeof(Pawn) });
            Verify(
                "Pawn_RelationsTracker.RemoveDirectRelation",
                typeof(Pawn_RelationsTracker),
                nameof(Pawn_RelationsTracker.RemoveDirectRelation),
                new Type[] { typeof(PawnRelationDef), typeof(Pawn) });

            // CHG-20261004-WORKTAB-HIDE 工作面板隐藏功能（EXC-2026-017 / EXC-2026-018）。
            // 这两条是「静默不生效」的主要防线：Harmony 目标解析失败时游戏不会报错，
            // 只表现为功能消失。get_Pawns 是属性 getter，参数列表为空，故传 new Type[0]。
            Verify(
                "MainTabWindow_Work.get_Pawns",
                typeof(MainTabWindow_Work),
                "get_Pawns",
                new Type[0]);
            Verify(
                "Pawn.GetGizmos",
                typeof(Pawn),
                nameof(Pawn.GetGizmos),
                new Type[0]);

            // ⚠ 已核销（2026-10-04 审计 P2）：此处原有 3 条「接管面板」探测
            //   （MainTabWindow_Work.DoWindowContents / get_ExtraTopSpace /
            //    MainTabWindow_PawnTable.get_ExtraBottomSpace）。
            //   CHG-20261004-WORKTAB-ORIGINAL（玩家决策：删除自绘工作面板，只用原版）已把
            //   接管链路（Panel / Bridge / Prefix / 尺寸归零）**整块删除** ⇒ 这 3 条探测的目标
            //   虽仍是原版真实方法（永不报错），但它们守护的 patch 已不存在 = 死探测。
            //   注意与之区分：上面的 get_Pawns / GetGizmos 两条来自 **CHG-20261004-WORKTAB-HIDE**
            //   （隐藏名单与眼标），是**独立功能**、仍然生效，**不得**一并删除。

            // CHG-20261004-MEALSCHEDULE / CHG-20261004-IDLE（EXC-2026-022 / EXC-2026-024）：
            // 以下 4 条是新增 patch 的「静默不生效」防线 —— Harmony 目标解析失败时游戏不报错，
            // 只表现为「用餐时段按钮没出现 / 涂了格子不重算 / 未知时段红字刷屏且决策劣化」。
            //
            // 1·2 硬编码时段的 GetPriority 兜底：原版对未知 TimeAssignmentDef 直接
            //      throw NotImplementedException（IL 实证，Build 1.6.4871），PC 的
            //      PC_Meal / PC_Idle 落在这条分支上 ⇒ 目标消失即回到"红字 + 该时段不睡/不工作"。
            Verify(
                "JobGiver_Work.GetPriority",
                typeof(JobGiver_Work),
                "GetPriority",
                new Type[] { typeof(Pawn) });
            Verify(
                "JobGiver_GetRest.GetPriority",
                typeof(JobGiver_GetRest),
                "GetPriority",
                new Type[] { typeof(Pawn) });

            // 3 玩家在【管制】表涂「用餐」后立即重算（否则要等下一个餐段）。
            Verify(
                "Pawn_TimetableTracker.SetAssignment",
                typeof(Pawn_TimetableTracker),
                "SetAssignment",
                new Type[] { typeof(int), typeof(TimeAssignmentDef) });

            // 4 时段选择器按钮网格（原版硬编码 5 项，PC 追加第 6 个「用餐」）。
            Verify(
                "TimeAssignmentSelector.DrawTimeAssignmentSelectorGrid",
                typeof(TimeAssignmentSelector),
                "DrawTimeAssignmentSelectorGrid",
                new Type[] { typeof(UnityEngine.Rect) });

            // 5 CHG-20261004-EMPLOYEE-ACCESS（EXC-2026-025）：车间在编员工可从事被原版
            //   禁用（背景故事/特质）且属于其车间类型映射的工作类型。
            //   ⚠ 2026-10-04 修正：目标从 `WorkTypeIsDisabled` 改为**上游** `GetDisabledWorkTypes`
            //   —— 后者还有 13 个直调点（含 `SkillRecord.CalculateTotallyDisabled`），
            //   只 patch 前者会让"工人被允许烹饪、但 Cooking 技能仍被判 TotallyDisabled"
            //   ⇒ `FoodPoisonChance` 的 stat 一致性检查报红字（实机已发生）。
            //   目标消失 ⇒ 员工因背景故事"无法从事"时岗位映射永远勾不上（静默不生效）。
            Verify(
                "Pawn.GetDisabledWorkTypes",
                typeof(Pawn),
                nameof(Pawn.GetDisabledWorkTypes),
                new Type[] { typeof(bool) });

            // CHG-20261005-DEVRENAME（车间设备自定义名的原版显示；EXC-2026-001 族）：
            // 目标是实例标签的**唯一收口重载**（Cecil 实证：private static 的
            // GenLabel.NewThingLabel(Thing,int,bool,bool) 仅被本重载调用 ⇒ Label / LabelCap /
            // LabelNoCount / LabelShort / LabelNoParenthesis 全部经此）。
            // 目标消失 / 签名漂移 ⇒ 自定义名不再进原版 UI，而游戏**不会报错**
            // （只表现为「改名后原版面板没变」）—— 这正是本探测要挡的静默失效。
            // ⚠ 必须传完整参数表：GenLabel.ThingLabel 另有 (BuildableDef,ThingDef,int) 重载，
            //   只给方法名可能命中错的那一个（探测通过但补丁挂错 = 比不探测更糟）。
            Verify(
                "GenLabel.ThingLabel(Thing,int,bool,bool)",
                typeof(GenLabel),
                nameof(GenLabel.ThingLabel),
                new Type[] { typeof(Thing), typeof(int), typeof(bool), typeof(bool) });

            // WorkGate: check all three signatures without applying the two
            // WidgetsWork patches in an offline host (Unity ECall boundary).
            Verify("Pawn_WorkSettings.CacheWorkGiversInOrder", typeof(Pawn_WorkSettings),
                "CacheWorkGiversInOrder", Type.EmptyTypes);
            Verify("WidgetsWork.DrawWorkBoxFor", typeof(WidgetsWork), nameof(WidgetsWork.DrawWorkBoxFor),
                new Type[] { typeof(float), typeof(float), typeof(Pawn), typeof(WorkTypeDef), typeof(bool) });
            Verify("WidgetsWork.TipForPawnWorker", typeof(WidgetsWork), nameof(WidgetsWork.TipForPawnWorker),
                new Type[] { typeof(Pawn), typeof(WorkTypeDef), typeof(bool) });

            // S3 player-order completion targets (the Kill target is checked above).
            Verify("ExecutionUtility.DoExecutionByCut", typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut),
                new Type[] { typeof(Pawn), typeof(Pawn), typeof(int), typeof(bool) });
            Verify("InteractionWorker_RecruitAttempt.DoRecruit(6)", typeof(InteractionWorker_RecruitAttempt),
                nameof(InteractionWorker_RecruitAttempt.DoRecruit), new Type[] { typeof(Pawn), typeof(Pawn),
                    typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(bool), typeof(bool) });
            Verify("Toils_Haul.StartCarryThing", typeof(Toils_Haul), nameof(Toils_Haul.StartCarryThing),
                new Type[] { typeof(TargetIndex), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            Verify("Toils_Haul.PlaceHauledThingInCell", typeof(Toils_Haul), nameof(Toils_Haul.PlaceHauledThingInCell),
                new Type[] { typeof(TargetIndex), typeof(Toil), typeof(bool), typeof(bool) });
            Verify("Toils_Haul.DepositHauledThingInContainer", typeof(Toils_Haul), nameof(Toils_Haul.DepositHauledThingInContainer),
                new Type[] { typeof(TargetIndex), typeof(TargetIndex), typeof(Action) });
            Verify("Pawn_CarryTracker.TryDropCarriedThing(4)", typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                new Type[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) });
        }

        // Called explicitly after startup patching; static startup constructor
        // order is unspecified, so its constructor cannot assert installation.
        internal static void VerifyWorkGateInstallation(string owner)
        {
            VerifyRegistration(owner, AccessTools.Method(typeof(Pawn_WorkSettings),
                "CacheWorkGiversInOrder", Type.EmptyTypes), typeof(Patch_PawnWorkSettings_CacheGivers));
        }

        // Manual visual patches call this only after their deferred ApplyOnce.
        internal static bool VerifyRegistration(string owner, MethodBase target, Type adapter)
        {
            var info = target == null ? null : Harmony.GetPatchInfo(target);
            bool installed = info != null && info.Prefixes.Concat(info.Postfixes)
                .Concat(info.Transpilers).Concat(info.Finalizers)
                .Any(p => p.owner == owner && p.PatchMethod.DeclaringType == adapter);
            if (!installed)
                ChronicleLog.Error(ChronicleLog.Category.Capture, "capture patch missing: owner=" + owner
                    + "; adapter=" + adapter.FullName + "; target=" + (target?.ToString() ?? "unresolved"));
            return installed;
        }

        private static void Verify(string label, Type type, string methodName, Type[] argumentTypes)
        {
            if (AccessTools.Method(type, methodName, argumentTypes) == null)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture, "capture target missing: " + label);
            }
        }
    }
}
