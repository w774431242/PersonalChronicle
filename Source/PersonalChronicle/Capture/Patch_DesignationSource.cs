using System;
using System.Diagnostics;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// 工作任务的**来源**捕获（CHG-20261010-ECON-JOB-INTENT，EXC-2026-001 台账登记项）：
    /// 区分"**玩家设计器**下达的 designation"与"建筑 / 植物自维护产生的自动标记"，
    /// 只有前者才算玩家发布的工作任务。
    ///
    /// <para><b>为什么挂在 <c>AddDesignation</c> 而不是各 <c>Designator</c> 子类</b>：Cecil 反查
    /// <c>refs AddDesignation</c> 显示**每个设计器子类都在自己的 <c>DesignateThing</c> /
    /// <c>DesignateSingleCell</c> 里直接调用**（<c>Designator_Plants</c> / <c>Designator_PlantsCut</c> /
    /// <c>Designator_PlantsHarvestWood</c> / <c>Designator_Mine</c> / <c>Designator_MineVein</c> … 十余个子类），
    /// 挂子类需枚举且会漏第三方自定义设计器；<c>AddDesignation</c> 是**唯一下游入口**。</para>
    ///
    /// <para><b>来源判定 = 栈内是否存在 <c>Verse.Designator</c> 帧</b>（扫描有限帧，任一命中即玩家下达）。
    /// 之所以"任一帧"而非"第一帧"：<c>Designator_MineVein</c> 的矿脉标记发生在**闭包**
    /// （<c>Designator_MineVein/&lt;&gt;c__DisplayClass10_0::&lt;FloodFillDesignations&gt;b__1</c>）内，
    /// 其上方还有 <c>FloodFiller</c> 等帧 —— 只看首帧会漏记整条矿脉。
    /// <c>CompAutoCut.DesignatePlantsToCut</c>（风力发电机等自动清障，每 <c>CompTickLong</c> 重标）
    /// 与 <c>Plant.PlantCollected</c>（枯萎漫延、树桩补标）的调用栈内**没有** <c>Designator</c> 帧 ⇒ 被排除。</para>
    ///
    /// <para><b>自动重标会清除玩家键</b>：玩家标记过某植株后取消，若该目标随后被建筑**自动重标**，
    /// 本 Prefix 会判定为自动来源并**移除该键** ⇒ 它不再被计入待办。这同时关闭了
    /// "残留键 + 自动重标 ⇒ 误计 ⇒ 错付"这条路径（旧设计只保留键、由计数侧按存在性过滤，
    /// 无法区分"玩家重标"与"建筑重标"）。</para>
    ///
    /// Patch 档案：
    ///   Patch Target      : Verse.DesignationManager.AddDesignation(Designation)
    ///   Patch Type        : Prefix (判定来源 + 记录/清除玩家键；**绝不** return false，不改原方法行为)
    ///   Reason            : 精准识别玩家发布的工作任务（劳务站待办口径）；见 CHG-20261010-ECON-JOB-INTENT
    ///   Alternative       : ① 挂各 Designator 子类的 Designate* —— **否决**（子类各自实现，需枚举且漏第三方）；
    ///                       ② 只看首帧调用者类型 —— **否决**（矿脉闭包会使整条矿脉漏记）；
    ///                       ③ 排除 CompAutoCut 清障范围 —— **否决**（只能覆盖风电机/畜栏，枯萎与树桩补标仍误计）；
    ///                       ④ 读 designation 自身找来源 —— **不存在**（原版无来源字段）
    ///   Compatibility Risk: 低 — 只读判定 + 集合增删，异常一律吞掉（最坏 = 该次下达不被识别为玩家来源）
    ///   Execution Order   : 无顺序契约
    ///   Failure Behavior  : catch → Log.WarningOnce，原版下达不受影响；启动期签名探测（下方 TargetMethodExists）
    ///   Rollback          : 删除本文件即回滚（已记录来源随档保留，仅不再新增）
    /// </summary>
    [HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.AddDesignation))]
    public static class Patch_DesignationSource
    {
        /// <summary>扫描帧数上界。审计（2026-10-10）实测玩家矿脉路径的 Designator 帧落在第 **7** 帧
        /// ⇒ 原 8 帧余量仅 0–1 帧；提到 12 帧（命中即停，成本可忽略）留出安全余量。</summary>
        private const int MaximumFramesToInspect = 12;

        /// <summary>签名探测（漂移 ⇒ Harmony 静默跳过 ⇒ 启动期显式报出，与既有多数 Patch 同构）。</summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(typeof(DesignationManager), "AddDesignation",
                new[] { typeof(Designation) }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "DesignationManager.AddDesignation signature changed; player-issued work capture disabled - update Patch_DesignationSource");
            }
            return found;
        }

        public static void Prefix(Designation newDes)
        {
            try
            {
                if (newDes?.def == null) return;
                var runtime = PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current;
                if (runtime == null) return;
                switch (Classify())
                {
                    case Source.Player: runtime.RecordPlayerDesignation(newDes); break;
                    case Source.Automatic: runtime.RecordAutoDesignation(newDes); break;
                    default: break;   // Unknown：不计待办（宁少不多），也不排除计酬（工人确实干了活）
                }
            }
            catch (Exception error)
            {
                Log.WarningOnce("[PC][Economy] designation source capture failed: " + error.Message, 0x1EC0E01);
            }
        }

        /// <summary>来源三态（CHG-20261010-ECON-LINKFIX-A，链路审计修复）。
        ///
        /// <para><b>为什么从二值改为三态</b>：旧实现把所有"非玩家"路径一律归为自动 —— 但**玩家也会走
        /// 非设计器路径**，例如 <c>Plant.GetGizmos</c> 的「全砍枯萎」命令（其调用栈内没有 Designator 帧）
        /// ⇒ 被误归自动 ⇒ 那些割除**既不计待办也不计酬**（审计 A 席 P2-⑥ 实测）。三态把"无法判定"单列：
        /// **不计待办，但计酬照付**。</para>
        ///
        /// <para><b>三态判据</b>：
        /// <list type="bullet">
        /// <item><b>Player</b> = 栈内存在 <c>Designator</c> 的**实例方法帧**（玩家下达一律经实例方法；
        ///   拖动框选收敛到 <c>DesignateMultiCell</c>/<c>DesignateThing</c>/<c>DesignateSingleCell</c>）。</item>
        /// <item><b>Automatic</b> = 栈内存在 <c>CompAutoCut</c>（风电机/畜栏自动清障）**或</b> <c>JobDriver</c>
        ///   （作业驱动的自维护：枯萎漫延、树桩补标、矿脉续标都在 JobDriver 链内）**或** <c>Designator</c> 的
        ///   **静态**方法帧（<c>Designator_MineVein.FloodFillDesignations</c> —— 静态却声明在 Designator 子类上）。</item>
        /// <item><b>Unknown</b> = 其余（如 gizmo 命令）：不进白名单也不进黑名单。</item>
        /// </list>
        /// 区分「玩家 gizmo」与「自动维护」的关键：前者**不在 JobDriver 链内**，后者**在**。</para></summary>
        private enum Source { Player, Automatic, Unknown }

        private static Source Classify()
        {
            var trace = new StackTrace(1, false);
            int frames = Math.Min(trace.FrameCount, MaximumFramesToInspect);
            bool jobDriver = false, autoCut = false, staticDesignator = false;
            for (int i = 0; i < frames; i++)
            {
                var method = trace.GetFrame(i)?.GetMethod();
                var declaring = method?.DeclaringType;
                if (declaring == null) continue;
                string name = declaring.Name;
                if (name.StartsWith("CompAutoCut", StringComparison.Ordinal)) autoCut = true;
                else if (name.StartsWith("JobDriver", StringComparison.Ordinal)) jobDriver = true;
                if (!typeof(Designator).IsAssignableFrom(declaring)) continue;
                if (method.IsStatic) { staticDesignator = true; continue; }
                return Source.Player;   // 实例方法帧 ⇒ 玩家下达（优先于其它判据）
            }
            if (autoCut || jobDriver || staticDesignator) return Source.Automatic;
            return Source.Unknown;
        }
    }

    /// <summary>启动期一次性强制探测（与 <c>Patch_ItemFlowPlantStartup</c> 同构）。</summary>
    [StaticConstructorOnStartup]
    public static class Patch_DesignationSourceStartup
    {
        static Patch_DesignationSourceStartup()
        {
            _ = Patch_DesignationSource.TargetMethodExists;
        }
    }
}
