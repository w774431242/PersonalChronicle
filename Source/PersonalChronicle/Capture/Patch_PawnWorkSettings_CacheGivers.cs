using System;
using System.Collections.Generic;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// **工作合同闸门**（CHG-20261012-WORKGATE）：在原版重建"有序作业表"之后，把**未被授权**的工种对应的 giver
    /// 从两张表里移除 —— 于是"勾选状态"照旧（面板不动），但没人会去干没有合同覆盖的活。
    ///
    /// <para><b>为什么挂这里（已取证）</b>：<c>Pawn_WorkSettings.CacheWorkGiversInOrder</c> 是唯一的入表点——
    /// <c>wtsByPrio</c> 只收 <c>GetPriority &gt; 0</c> 的工种，紧急表与普通表**都**从它构建；而派工侧
    /// （<c>JobGiver_Work</c>）只消费这两张表。命令类（标记/蓝图）、订单类（<c>WorkGiver_DoBill</c>）、
    /// 自发类（World 扫描）**一网打尽**。</para>
    ///
    /// <para><b>为什么不碰 <c>GetPriority</c> / <c>WorkIsActive</c></b>：它们是"面板 + 非作业系统"的读数
    /// （<c>WidgetsWork</c> / <c>BillDialogUtility</c> / <c>GenConstruct</c> / <c>ITab_Pawn_Visitor</c> / <c>Ideo</c> 均在读）
    /// —— 改它们会连带改掉面板显示与建造前置判定，正是本方案要避免的。</para>
    ///
    /// <para><b>fail-open</b>：任何异常 ⇒ 不改表（退化为原版）；日志只在首次出错时一行（<c>ErrorOnce</c>）。</para>
    /// </summary>
    [HarmonyPatch(typeof(Pawn_WorkSettings), "CacheWorkGiversInOrder")]
    public static class Patch_PawnWorkSettings_CacheGivers
    {
        private static bool selfChecked;

        static void Postfix(Pawn_WorkSettings __instance, Pawn ___pawn,
            List<WorkGiver> ___workGiversInOrderNormal, List<WorkGiver> ___workGiversInOrderEmerg)
        {
            try
            {
                if (!WorkAuthorityService.Enabled) return;
                // 闸门已生效 ⇒ 顺手把"合同锁"视觉补丁挂上（手动挂载：目标含 ECall，离线 PatchAll 自检不能扫到它）。
                Patch_WorkTab_ContractLock_Draw.ApplyOnce();
                HashSet<string> allowed = WorkAuthorityService.AuthorizedWorkTypes(___pawn);
                if (allowed == null) return;   // 闸门关闭 / 无法判定 ⇒ 不过滤
                Filter(___workGiversInOrderNormal, allowed, ___pawn);
                // ⚠ 紧急表**必须一起过滤**：灭火 / 抢救等 emergency giver 走的是这张表（否则从紧急通道漏过）。
                Filter(___workGiversInOrderEmerg, allowed, ___pawn);
                SelfCheckOnce();
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[PC] 工作合同闸门过滤失败（本次按原版放行）：" + ex.Message, 0x5A17E3);
            }
        }

        private static void Filter(List<WorkGiver> givers, HashSet<string> allowed, Pawn pawn)
        {
            if (givers == null || givers.Count == 0) return;
            for (int i = givers.Count - 1; i >= 0; i--)
            {
                WorkGiver giver = givers[i];
                WorkTypeDef workType = giver?.def?.workType;
                // workType 为空 ⇒ 该 giver 不属于任何工种（原版 3 个）⇒ 与勾选无关，不纳管。
                if (!WorkAuthority.ShouldGate(workType?.defName)) continue;
                if (!allowed.Contains(workType.defName) || !WorkAuthorityService.IsGiverAuthorizedForScope(pawn, giver))
                    givers.RemoveAt(i);
            }
        }

        /// <summary>补丁生效自检（工程有 <c>PatchAll</c> 整类未生效的前科）：首次成功过滤后留一行日志，
        /// 让"补丁没挂上"这件事在 Player.log 里可分辨。</summary>
        private static void SelfCheckOnce()
        {
            if (selfChecked) return;
            selfChecked = true;
            Log.Message("[PC] 工作合同闸门已生效（CacheWorkGiversInOrder 过滤正常）。");
        }
    }
}
