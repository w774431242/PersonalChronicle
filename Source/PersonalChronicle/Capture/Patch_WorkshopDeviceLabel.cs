using System.Collections.Generic;
using HarmonyLib;
using PersonalChronicle.Automation;
using PersonalChronicle.Data;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// 车间设备自定义名的**原版显示单点**（CHG-20261005-DEVRENAME，2026-10-05 玩家诉求：
    /// 「改名后要求原版全局都显示新名」）。
    ///
    /// <para><b>① 为什么挂这一个方法</b>（Cecil 实证，build 1.6.4871 rev590）：
    /// <c>RimWorld.GenLabel.NewThingLabel(Thing,int,bool,bool)</c> 是 private static，且<b>仅</b>被
    /// <see cref="GenLabel.ThingLabel(Thing,int,bool,bool)"/> 调用（refs 实测 2 条）⇒ 该重载是
    /// <b>实例标签的唯一收口点</b>：<c>Thing.get_LabelNoCount</c>、<c>get_LabelNoParenthesis</c>、
    /// <c>GetCustomLabelNoCount</c> 全部汇入它 ⇒ 一处即覆盖
    /// Label / LabelCap / LabelNoCount / LabelShort / LabelNoParenthesis。</para>
    ///
    /// <para><b>② 为什么不用更窄的 <c>ThingWithComps.get_LabelNoCount</c></b>：它跑在
    /// <c>ThingComp.TransformLabel</c> 之后、语义更干净（不会被 comp 后缀污染），但会漏掉
    /// <c>LabelNoParenthesis</c> 与 <c>GetCustomLabelNoCount</c>（两者直连收口点、不经该 getter）
    /// ⇒ 不满足 REQ-WORKSHOP-002「原版全局」。取舍已登记（设计文档 §18.6）。</para>
    ///
    /// <para><b>③ 与原版原生扩展点的关系</b>：<c>Verse.ThingComp.TransformLabel</c> 才是原版为
    /// per-instance 改名提供的正式钩子（原版用它做艺术标题 / 生成名 / 生物编码），<b>风险序低于 Harmony</b>；
    /// 但它只能经 <c>ThingDef.comps</c>（Def 级静态）挂载 ⇒ 需逐 Def XML Patch，而本功能的对象是
    /// 「<b>任意</b>车间已追踪工作台」（含第三方与后续新增 Def）⇒ 不可穷举，不能作唯一路径。
    /// 若将来只需覆盖本 Mod 自有设备 Def，应优先改用该钩子以缩小 Harmony 面。</para>
    ///
    /// <para><b>④ 覆盖边界（照实登记）</b>：<c>MinifiedThing</c>（收存箱）的
    /// <c>get_LabelNoCount</c> 完全自建、不调 base ⇒ <b>收存态不显示自定义名</b>（设计内行为，
    /// 与台账淘汰窗口语义自洽）。</para>
    ///
    /// <para><b>⑤ 落的规则</b>：
    /// REQ-WORKSHOP-002（原版全局）· REQ-WORKSHOP-005（回落链
    /// <b>车间名 ?? 档案覆盖 ?? 原版默认名</b>）· P0-C（设置开关<b>只</b>关「车间名替换段」，
    /// 不影响档案覆盖 —— 后者是 EXC-2026-001 #14 的既有行为，关掉它就是回归）·
    /// PERF-001（热路径双早退、命中路径零分配）。</para>
    ///
    /// <para><b>⚠ ⑥ 历史铁律</b>：本类<b>必须 public</b> —— <c>PatchAll</c> 会<b>静默跳过</b> internal 类型，
    /// 旧版 Patch_BuildingLabel / Patch_RoomRoleLabel 曾因此<b>从未生效</b>
    /// （docs/问题跟踪/实机测试问题记录.md 问题十五）。另有
    /// <see cref="CapturePatchDiagnostics"/> 的启动签名探测作第二道防线（目标漂移 ⇒ Log 报错）。</para>
    /// </summary>
    [HarmonyPatch(typeof(GenLabel), nameof(GenLabel.ThingLabel),
        new[] { typeof(Thing), typeof(int), typeof(bool), typeof(bool) })]
    public static class Patch_WorkshopDeviceLabel
    {
        /// <summary>节流告警上限（热路径不得无界刷日志，PERF-001）。</summary>
        private const int MaxWarnings = 8;

        private static int warningCount;

        // ── 单条目缓存（P0-D）：热路径不得每次做 map.GetComponent<T>() 的组件列表扫描 ───────
        // 用弱引用持有，避免静态字段跨存档把地图/管理器/Game 钉在内存里。
        // cachedKnown 使「本图没有该组件」也能被缓存，否则无组件的图上每次调用都会重新全扫（P2-2）。
        private static System.WeakReference cachedMapRef;
        private static System.WeakReference cachedManagerRef;
        private static bool cachedManagerKnown;

        private static System.WeakReference cachedGameRef;
        private static System.WeakReference cachedGameCompRef;
        private static bool cachedGameCompKnown;

        private static MapComponent_WorkshopManager ManagerFor(Map map)
        {
            if (map == null)
            {
                return null;
            }
            if (cachedManagerKnown && ReferenceEquals(cachedMapRef == null ? null : cachedMapRef.Target, map))
            {
                return cachedManagerRef == null ? null : (MapComponent_WorkshopManager)cachedManagerRef.Target;
            }
            MapComponent_WorkshopManager mgr = map.GetComponent<MapComponent_WorkshopManager>();
            cachedMapRef = new System.WeakReference(map);
            cachedManagerRef = mgr == null ? null : new System.WeakReference(mgr);
            cachedManagerKnown = true;
            return mgr;
        }

        private static Dictionary<int, string> ArchiveOverrides()
        {
            Game game = Current.Game;
            if (game == null)
            {
                return null;
            }
            if (cachedGameCompKnown && ReferenceEquals(cachedGameRef == null ? null : cachedGameRef.Target, game))
            {
                ChronicleGameComponent cached = cachedGameCompRef == null
                    ? null : (ChronicleGameComponent)cachedGameCompRef.Target;
                return cached == null ? null : cached.BuildingLabelOverrides;
            }
            ChronicleGameComponent component = game.GetComponent<ChronicleGameComponent>();
            cachedGameRef = new System.WeakReference(game);
            cachedGameCompRef = component == null ? null : new System.WeakReference(component);
            cachedGameCompKnown = true;
            return component == null ? null : component.BuildingLabelOverrides;
        }

        private static void Postfix(Thing __0, ref string __result)
        {
            // ⚠ 参数用 __0 而非 __instance：本补丁的宿主类是 static GenLabel，没有实例。
            Thing t = __0;
            if (t == null)
            {
                return;
            }
            try
            {
                int id = t.thingIDNumber;
                if (id <= 0)
                {
                    return;
                }

                // ── ① 车间自定义名（最高优先级；受设置开关控制 —— P0-C：开关只关这一段）──────
                ChronicleSettings settings = PersonalChronicleMod.Settings;
                if (settings == null || settings.DeviceRenameApplyToVanilla)
                {
                    MapComponent_WorkshopManager mgr = ManagerFor(t.Map);
                    if (mgr != null)
                    {
                        WorkshopDeviceLedger ledger = mgr.DeviceLedger;
                        // 早退：本图无任何自定义名 ⇒ 不做字典查找（零分配）。
                        if (ledger != null && ledger.CustomNameCount > 0)
                        {
                            string custom = ledger.CustomNameOf(t);
                            if (!string.IsNullOrEmpty(custom))
                            {
                                __result = custom;
                                return;
                            }
                        }
                    }
                }

                // ── ② 档案覆盖（EXC-2026-001 #14 的既有行为；**不受**本功能开关控制，P0-C）────
                if (__result != null && __result.Length == 0)
                {
                    // 空结果不值得覆盖（防御：原版不会返回空串，但第三方可能）。
                    return;
                }
                Dictionary<int, string> overrides = ArchiveOverrides();
                if (overrides == null || overrides.Count == 0)
                {
                    return;
                }
                string archive;
                if (overrides.TryGetValue(id, out archive) && !string.IsNullOrWhiteSpace(archive))
                {
                    __result = archive;
                }
            }
            catch (System.Exception ex)
            {
                // 热路径绝不打断调用点；节流记录一次，避免异常被静默吞掉（EXC-002 可诊断性）。
                if (warningCount < MaxWarnings)
                {
                    warningCount++;
                    ChronicleLog.Warning(ChronicleLog.Category.Capture,
                        "device label override failed (throttled): " + ex);
                }
            }
        }
    }
}
