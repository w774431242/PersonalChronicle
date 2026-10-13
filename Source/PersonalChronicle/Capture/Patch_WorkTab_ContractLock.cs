using System;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// **工作面板「合同锁」标识**（CHG-20261012-WORKGATE · W2）：勾选照旧，但"已勾选却不被授权"的格子
    /// 压暗 + 套原版警示叠层，并在悬停提示里写明原因 —— 否则面板会说谎（勾了却不动）。
    ///
    /// <para><b>为什么钉这两个点</b>：<c>WidgetsWork.DrawWorkBoxFor(x, y, p, wType, …)</c> 是格位方块的唯一绘制入口
    /// （实测方块尺寸 <c>25×25</c>，与调用点同坐标）；<c>WidgetsWork.TipForPawnWorker(p, wType, …)</c> 是格位悬停的
    /// 唯一文本来源。两处都**只加视觉/文本**，不触碰任何状态（零写路径）。</para>
    ///
    /// <para><b>只标"已勾选"的格子</b>：未勾选的格子本来就没人干，加锁是噪音；玩家要看的正是
    /// "我勾了、但它其实没生效"。</para>
    ///
    /// <para><b>复用原版叠层贴图</b>（<c>UI/Widgets/WorkBoxOverlay_Warning</c>）：不新增美术资产，
    /// 且"警示"语义与原版一致。</para>
    ///
    /// <para><b>⚠ 为什么这两个补丁是"手动挂"而不是 <c>[HarmonyPatch]</c> 自动扫</b>：它们的目标方法体里含
    /// <b>ECall</b>（<c>GUI.DrawTexture</c> 一族）——在**离线测试宿主**（无 Unity 的 NUnit 进程）里 PatchAll 会抛
    /// <c>SecurityException: ECall 方法必须打包到系统模块中</c>，把工程的 <c>HarmonyStartupTests</c>（全量 PatchAll 自检）
    /// 整条打红。因此改为**运行期手动挂**（每个目标各自 try/catch + 日志），离线自检不再看见它们，
    /// 而游戏内照常生效；挂点由闸门补丁首次生效时触发（闸门关着时不需要锁标）。</para>
    /// </summary>
    public static class Patch_WorkTab_ContractLock_Draw
    {
        private const float BoxSize = 25f;
        /// <summary>压暗强度（GATE-UI 建议从 0.42 降档：0.42 会把勾/数字对比压掉一半以上）。</summary>
        private const float DimAlpha = 0.30f;
        private static bool attempted;

        /// <summary>手动挂载（幂等；失败只记一行日志并放弃，绝不抛给调用方 —— fail-open）。</summary>
        public static void ApplyOnce()
        {
            if (attempted) return;
            attempted = true;
            var harmony = new Harmony("colony.integratedmanagement.workgate.lock");
            // Independent attempts: a failure in drawing must not suppress the
            // tooltip. Targets stay manual because of the offline ECall limit.
            ApplyTarget(harmony, nameof(WidgetsWork.DrawWorkBoxFor),
                new Type[] { typeof(float), typeof(float), typeof(Pawn), typeof(WorkTypeDef), typeof(bool) },
                typeof(Patch_WorkTab_ContractLock_Draw));
            ApplyTarget(harmony, nameof(WidgetsWork.TipForPawnWorker),
                new Type[] { typeof(Pawn), typeof(WorkTypeDef), typeof(bool) },
                typeof(Patch_WorkTab_ContractLock_Tip));
        }

        private static void ApplyTarget(Harmony harmony, string targetName, Type[] parameters, Type adapter)
        {
            try
            {
                var target = AccessTools.Method(typeof(WidgetsWork), targetName, parameters);
                if (target == null) throw new MissingMethodException(typeof(WidgetsWork).FullName, targetName);
                harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(adapter, nameof(Postfix))));
                if (CapturePatchDiagnostics.VerifyRegistration(harmony.Id, target, adapter))
                    Log.Message("[PC] WorkGate contract lock installed; target=WidgetsWork." + targetName);
            }
            catch (Exception ex)
            {
                Log.Warning("[PC] WorkGate contract lock failed; target=WidgetsWork." + targetName
                    + "; other targets continue; no retry: " + ex);
            }
        }

        internal static void Postfix(float x, float y, Pawn p, WorkTypeDef wType)
        {
            try
            {
                if (!ShouldLock(p, wType)) return;
                var rect = new Rect(x, y, BoxSize, BoxSize);
                // GATE-UI 2026-10-11 P2：原版该叠层贴图**从不与"勾/数字"共存**，全幅铺会遮挡它们 ⇒
                // 改为"轻压暗 + 右上角小叠层"，勾与优先级数字仍可读（诚实标注不牺牲可读性）。
                Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, DimAlpha));
                if (WidgetsWork.WorkBoxOverlay_Warning != null)
                {
                    float badge = BoxSize * 0.45f;
                    var badgeRect = new Rect(rect.xMax - badge, rect.y, badge, badge);
                    GUI.DrawTexture(badgeRect, WidgetsWork.WorkBoxOverlay_Warning);
                }
                Widgets.DrawBox(rect, 1);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[PC] 工作面板合同锁绘制失败：" + ex.Message, 0x5A17E4);
            }
        }

        internal static bool ShouldLock(Pawn p, WorkTypeDef wType)
        {
            if (p == null || wType == null) return false;
            var settings = PersonalChronicleMod.Settings;
            if (settings == null || !settings.EnableWorkContractGate) return false;
            var work = p.workSettings;
            if (work == null || work.GetPriority(wType) <= 0) return false;   // 未勾选 ⇒ 不加锁
            return !WorkAuthorityService.IsAuthorizedNow(p, wType);
        }
    }

    /// <summary>格位悬停：在原因链上补一行"为什么这格不算数"（与绘制同判据，避免两处口径分叉）。
    /// 与绘制同为**手动挂载**（见上方说明），故不带 <c>[HarmonyPatch]</c>。</summary>
    public static class Patch_WorkTab_ContractLock_Tip
    {
        internal static void Postfix(Pawn p, WorkTypeDef wDef, ref string __result)
        {
            try
            {
                if (!Patch_WorkTab_ContractLock_Draw.ShouldLock(p, wDef)) return;
                __result += "\n\n" + "PersonalChronicle.WorkGate.Lock".Translate().ToString();
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[PC] 工作面板合同锁提示失败：" + ex.Message, 0x5A17E5);
            }
        }
    }
}
